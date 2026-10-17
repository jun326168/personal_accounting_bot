-- Double-entry ledger schema.
--
-- Accuracy guarantees enforced by the database itself (not just the app):
--   * every journal entry balances (sum of debits = sum of credits), has >= 2 lines,
--     and every line uses an account in the entry's currency;
--   * posted entries and lines are immutable; mistakes are fixed with a reversing entry;
--   * a user can only ever see or touch their own rows (row-level security keyed on
--     the per-transaction setting app.user_id), even if the app has a bug.

\set ON_ERROR_STOP on
BEGIN;

CREATE SCHEMA ledger;
REVOKE ALL ON SCHEMA ledger FROM PUBLIC;
GRANT USAGE ON SCHEMA ledger TO accounting_app;

CREATE TYPE ledger.account_type AS ENUM ('asset', 'liability', 'equity', 'income', 'expense');
CREATE TYPE ledger.entry_side AS ENUM ('debit', 'credit');

-- The user the current transaction acts for. The app runs
--   SELECT set_config('app.user_id', '<uuid>', true)
-- at the start of every transaction; NULL (no rows visible) when unset.
CREATE FUNCTION ledger.current_user_id() RETURNS uuid
    LANGUAGE sql STABLE
    AS $$ SELECT NULLIF(current_setting('app.user_id', true), '')::uuid $$;

------------------------------------------------------------------------------
-- Tables
------------------------------------------------------------------------------

CREATE TABLE ledger.users (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    line_user_id text NOT NULL UNIQUE CHECK (line_user_id ~ '^U[0-9a-f]{32}$'),
    created_at   timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE ledger.accounts (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     uuid NOT NULL REFERENCES ledger.users (id) ON DELETE CASCADE,
    name        text NOT NULL CHECK (char_length(btrim(name)) BETWEEN 1 AND 50),
    type        ledger.account_type NOT NULL,
    currency    char(3) NOT NULL DEFAULT 'TWD' CHECK (currency ~ '^[A-Z]{3}$'),
    is_archived boolean NOT NULL DEFAULT false,
    created_at  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (user_id, name),
    UNIQUE (id, user_id) -- target for composite FKs that pin rows to one user
);

CREATE TABLE ledger.journal_entries (
    id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id           uuid NOT NULL REFERENCES ledger.users (id) ON DELETE CASCADE,
    occurred_on       date NOT NULL,
    description       text NOT NULL CHECK (char_length(description) BETWEEN 1 AND 200),
    currency          char(3) NOT NULL DEFAULT 'TWD' CHECK (currency ~ '^[A-Z]{3}$'),
    -- LINE message that produced this entry, for tracing (one message may create several entries).
    source_message_id text CHECK (char_length(source_message_id) <= 64),
    -- Set when this entry cancels an earlier one; each entry can be reversed at most once.
    reverses_entry_id uuid UNIQUE,
    created_at        timestamptz NOT NULL DEFAULT now(),
    UNIQUE (id, user_id),
    FOREIGN KEY (reverses_entry_id, user_id) REFERENCES ledger.journal_entries (id, user_id),
    CHECK (reverses_entry_id IS DISTINCT FROM id)
);

CREATE INDEX journal_entries_user_occurred_idx
    ON ledger.journal_entries (user_id, occurred_on DESC, created_at DESC);

CREATE TABLE ledger.journal_lines (
    id         bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    entry_id   uuid NOT NULL,
    user_id    uuid NOT NULL,
    account_id uuid NOT NULL,
    side       ledger.entry_side NOT NULL,
    amount     numeric(18, 2) NOT NULL CHECK (amount > 0),
    FOREIGN KEY (entry_id, user_id) REFERENCES ledger.journal_entries (id, user_id) ON DELETE CASCADE,
    -- Deferred so deleting a whole user (cascading to both accounts and lines) works;
    -- deleting a single account that has postings still fails at commit.
    FOREIGN KEY (account_id, user_id) REFERENCES ledger.accounts (id, user_id)
        DEFERRABLE INITIALLY DEFERRED
);

CREATE INDEX journal_lines_entry_idx ON ledger.journal_lines (entry_id);
CREATE INDEX journal_lines_account_idx ON ledger.journal_lines (account_id);

-- LINE redelivers webhooks; recording the event id makes processing idempotent.
-- Holds no user data, so no RLS. Old rows can be purged by the admin role.
CREATE TABLE ledger.line_webhook_events (
    webhook_event_id text PRIMARY KEY CHECK (char_length(webhook_event_id) <= 64),
    received_at      timestamptz NOT NULL DEFAULT now()
);

------------------------------------------------------------------------------
-- Integrity triggers
------------------------------------------------------------------------------

-- Checked at COMMIT, after all lines of an entry are inserted.
CREATE FUNCTION ledger.check_entry_balanced() RETURNS trigger
    LANGUAGE plpgsql
    SET search_path = ledger, pg_temp
    AS $$
DECLARE
    v_entry_id   uuid;
    v_lines      int;
    v_debit      numeric;
    v_credit     numeric;
    v_bad_ccy    int;
    v_archived   int;
BEGIN
    IF TG_TABLE_NAME = 'journal_entries' THEN
        v_entry_id := NEW.id;
    ELSE
        v_entry_id := NEW.entry_id;
    END IF;

    SELECT count(*),
           coalesce(sum(l.amount) FILTER (WHERE l.side = 'debit'), 0),
           coalesce(sum(l.amount) FILTER (WHERE l.side = 'credit'), 0),
           count(*) FILTER (WHERE a.currency <> e.currency),
           count(*) FILTER (WHERE a.is_archived)
      INTO v_lines, v_debit, v_credit, v_bad_ccy, v_archived
      FROM journal_entries e
      JOIN journal_lines l ON l.entry_id = e.id
      JOIN accounts a ON a.id = l.account_id
     WHERE e.id = v_entry_id;

    IF v_lines < 2 THEN
        RAISE EXCEPTION 'journal entry % must have at least 2 lines', v_entry_id;
    END IF;
    IF v_debit <> v_credit THEN
        RAISE EXCEPTION 'journal entry % is unbalanced: debit % <> credit %', v_entry_id, v_debit, v_credit;
    END IF;
    IF v_bad_ccy > 0 THEN
        RAISE EXCEPTION 'journal entry % uses accounts in a different currency', v_entry_id;
    END IF;
    IF v_archived > 0 THEN
        RAISE EXCEPTION 'journal entry % posts to an archived account', v_entry_id;
    END IF;
    RETURN NULL;
END $$;

CREATE CONSTRAINT TRIGGER journal_entries_balanced
    AFTER INSERT ON ledger.journal_entries
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW EXECUTE FUNCTION ledger.check_entry_balanced();

CREATE CONSTRAINT TRIGGER journal_lines_balanced
    AFTER INSERT ON ledger.journal_lines
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW EXECUTE FUNCTION ledger.check_entry_balanced();

-- Posted history is append-only, for every role. (Deletes are additionally not
-- granted to the app; the admin can still delete a whole user via ON DELETE CASCADE.)
CREATE FUNCTION ledger.reject_update() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    RAISE EXCEPTION '% is append-only; post a reversing entry instead', TG_TABLE_NAME;
END $$;

CREATE TRIGGER journal_entries_immutable
    BEFORE UPDATE ON ledger.journal_entries
    FOR EACH ROW EXECUTE FUNCTION ledger.reject_update();

CREATE TRIGGER journal_lines_immutable
    BEFORE UPDATE ON ledger.journal_lines
    FOR EACH ROW EXECUTE FUNCTION ledger.reject_update();

-- An account's type and currency define how its history is read; never change them.
CREATE FUNCTION ledger.check_account_update() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NEW.type <> OLD.type OR NEW.currency <> OLD.currency OR NEW.user_id <> OLD.user_id THEN
        RAISE EXCEPTION 'account type, currency and owner cannot be changed';
    END IF;
    RETURN NEW;
END $$;

CREATE TRIGGER accounts_update_guard
    BEFORE UPDATE ON ledger.accounts
    FOR EACH ROW EXECUTE FUNCTION ledger.check_account_update();

------------------------------------------------------------------------------
-- Row-level security
------------------------------------------------------------------------------

ALTER TABLE ledger.users ENABLE ROW LEVEL SECURITY;
ALTER TABLE ledger.accounts ENABLE ROW LEVEL SECURITY;
ALTER TABLE ledger.journal_entries ENABLE ROW LEVEL SECURITY;
ALTER TABLE ledger.journal_lines ENABLE ROW LEVEL SECURITY;

CREATE POLICY own_user ON ledger.users
    USING (id = ledger.current_user_id());
CREATE POLICY own_rows ON ledger.accounts
    USING (user_id = ledger.current_user_id()) WITH CHECK (user_id = ledger.current_user_id());
CREATE POLICY own_rows ON ledger.journal_entries
    USING (user_id = ledger.current_user_id()) WITH CHECK (user_id = ledger.current_user_id());
CREATE POLICY own_rows ON ledger.journal_lines
    USING (user_id = ledger.current_user_id()) WITH CHECK (user_id = ledger.current_user_id());

------------------------------------------------------------------------------
-- Read model
------------------------------------------------------------------------------

-- Balance in each account's normal direction (assets/expenses: debit - credit;
-- liabilities/equity/income: credit - debit). security_invoker keeps RLS in force.
CREATE VIEW ledger.account_balances WITH (security_invoker = true) AS
SELECT a.id AS account_id,
       a.user_id,
       a.name,
       a.type,
       a.currency,
       a.is_archived,
       coalesce(sum(CASE WHEN l.side = 'debit' THEN l.amount ELSE -l.amount END), 0)
         * CASE WHEN a.type IN ('asset', 'expense') THEN 1 ELSE -1 END AS balance
  FROM ledger.accounts a
  LEFT JOIN ledger.journal_lines l ON l.account_id = a.id
 GROUP BY a.id;

------------------------------------------------------------------------------
-- User provisioning
------------------------------------------------------------------------------

-- The only way the app resolves a LINE user to an internal id. Runs with the
-- owner's rights because RLS hides users until app.user_id is known. Creates the
-- user with a default chart of accounts on first contact.
CREATE FUNCTION ledger.ensure_user(p_line_user_id text) RETURNS uuid
    LANGUAGE plpgsql
    SECURITY DEFINER
    SET search_path = ledger, pg_temp
    AS $$
DECLARE
    v_id uuid;
BEGIN
    SELECT id INTO v_id FROM users WHERE line_user_id = p_line_user_id;
    IF FOUND THEN
        RETURN v_id;
    END IF;

    INSERT INTO users (line_user_id) VALUES (p_line_user_id)
        ON CONFLICT (line_user_id) DO NOTHING
        RETURNING id INTO v_id;
    IF v_id IS NULL THEN -- lost a race with a concurrent first message
        SELECT id INTO v_id FROM users WHERE line_user_id = p_line_user_id;
        RETURN v_id;
    END IF;

    INSERT INTO accounts (user_id, name, type)
    SELECT v_id, d.name, d.type::account_type
      FROM (VALUES
            ('現金', 'asset'),
            ('銀行存款', 'asset'),
            ('信用卡', 'liability'),
            ('期初餘額', 'equity'),
            ('薪資', 'income'),
            ('其他收入', 'income'),
            ('餐飲', 'expense'),
            ('交通', 'expense'),
            ('購物', 'expense'),
            ('居住', 'expense'),
            ('娛樂', 'expense'),
            ('醫療', 'expense'),
            ('其他支出', 'expense')
           ) AS d(name, type);

    RETURN v_id;
END $$;

------------------------------------------------------------------------------
-- Privileges for the API role: read + append only, no DDL, no deletes.
------------------------------------------------------------------------------

REVOKE ALL ON ALL TABLES IN SCHEMA ledger FROM PUBLIC;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA ledger FROM PUBLIC;

GRANT SELECT ON ledger.users TO accounting_app;
GRANT SELECT, INSERT ON ledger.accounts TO accounting_app;
GRANT UPDATE (name, is_archived) ON ledger.accounts TO accounting_app;
GRANT SELECT, INSERT ON ledger.journal_entries TO accounting_app;
GRANT SELECT, INSERT ON ledger.journal_lines TO accounting_app;
GRANT SELECT ON ledger.account_balances TO accounting_app;
GRANT INSERT ON ledger.line_webhook_events TO accounting_app;

GRANT EXECUTE ON FUNCTION ledger.current_user_id() TO accounting_app;
GRANT EXECUTE ON FUNCTION ledger.ensure_user(text) TO accounting_app;
GRANT EXECUTE ON FUNCTION ledger.check_entry_balanced() TO accounting_app;
GRANT EXECUTE ON FUNCTION ledger.reject_update() TO accounting_app;
GRANT EXECUTE ON FUNCTION ledger.check_account_update() TO accounting_app;

COMMIT;
