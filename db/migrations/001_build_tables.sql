CREATE TABLE users (
  id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  line_user_id  varchar(33) NOT NULL UNIQUE,
  created_at    timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE messages (
  id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  user_id         BIGINT NOT NULL REFERENCES users(id),
  role            varchar(4) NOT NULL CHECK (role in ('bot', 'user')),
  line_message_id varchar(32) NOT NULL UNIQUE,
  text_content    TEXT,
  json_content    jsonb, -- for flex message or template
  type            TEXT NOT NULL CHECK (type in ('text', 'flex_message', 'template')) DEFAULT 'text',  -- text, flex message or template
  is_transaction  boolean NOT NULL DEFAULT false,
  created_at      timestamptz NOT NULL DEFAULT now(),
  CHECK (role = 'bot' OR type = 'text')
);

CREATE TABLE transactions (
  id                      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  message_id              BIGINT NOT NULL REFERENCES messages(id),
  description             TEXT, -- the description extracted from the message if the message contains a transaction (or multiple)
  date                    date NOT NULL,
  kind                    text NOT NULL DEFAULT 'normal'
                          CHECK (kind IN ('opening', 'normal', 'reversal', 'adjusting')),
  reverses_transaction_id BIGINT UNIQUE REFERENCES transactions(id), -- unique prevents repeated reversal
  note                    TEXT,
  created_at              timestamptz NOT NULL DEFAULT now(),
  CHECK ((kind = 'reversal') = (reverses_transaction_id IS NOT NULL))
);

-- make sure only one opening transaction
CREATE UNIQUE INDEX uq_one_opening ON transactions ((true)) WHERE kind = 'opening';

CREATE TABLE accounts (
  id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  name            varchar(50) NOT NULL UNIQUE, -- 現金 (Cash), 應收帳款 (Accounts Receivable), etc
  type            varchar(9)  NOT NULL CHECK (type IN ('asset','liability','equity','revenue','expense')),
  normal_balance  varchar(6) GENERATED ALWAYS AS (
    CASE WHEN type IN ('asset','expense') THEN 'debit' ELSE 'credit' END
  ) STORED,
  created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE journal_entries (
  id              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  transaction_id  BIGINT NOT NULL REFERENCES transactions(id),
  account_id      BIGINT NOT NULL REFERENCES accounts(id),
  amount          numeric(14,2) NOT NULL CHECK (amount <> 0), -- can be negative, e.g. if account's normal balance is debit but amount is negative -> credit
  created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE proposals (
  id          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  message_id  BIGINT NOT NULL REFERENCES messages(id),
  content     jsonb NOT NULL,
  status      text NOT NULL DEFAULT 'pending'
              CHECK (status IN ('pending', 'confirmed', 'cancelled')),
  created_at  timestamptz NOT NULL DEFAULT now(),
  decided_at  timestamptz
);


-- indexes
CREATE INDEX idx_journal_entries_transaction_id ON journal_entries (transaction_id);
CREATE INDEX idx_journal_entries_account_id     ON journal_entries (account_id);
CREATE INDEX idx_transactions_message_id        ON transactions (message_id);


-- make account.type immutable
CREATE FUNCTION forbid_account_type_change() RETURNS trigger AS $$
BEGIN
  IF NEW.type <> OLD.type THEN
    RAISE EXCEPTION 'account type is immutable (account %)', OLD.id;
  END IF;
  RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_account_type_immutable
  BEFORE UPDATE ON accounts
  FOR EACH ROW EXECUTE FUNCTION forbid_account_type_change();


-- make sure entries are immutable
CREATE FUNCTION forbid_entry_mutation() RETURNS trigger AS $$
BEGIN
  RAISE EXCEPTION '% on journal_entries is not allowed; post a reversal instead', TG_OP;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_entries_immutable
  BEFORE UPDATE OR DELETE ON journal_entries
  FOR EACH ROW EXECUTE FUNCTION forbid_entry_mutation();


-- make sure transactions are immutable
CREATE FUNCTION forbid_transaction_mutation() RETURNS trigger AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    RAISE EXCEPTION 'transactions cannot be deleted; post a reversal instead';
  END IF;
  IF (NEW.id, NEW.message_id, NEW.date, NEW.kind, NEW.reverses_transaction_id, NEW.created_at)
     IS DISTINCT FROM
     (OLD.id, OLD.message_id, OLD.date, OLD.kind, OLD.reverses_transaction_id, OLD.created_at) THEN
    RAISE EXCEPTION 'only description and note of a transaction can be changed';
  END IF;
  RETURN NEW;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER trg_transactions_immutable
  BEFORE UPDATE OR DELETE ON transactions
  FOR EACH ROW EXECUTE FUNCTION forbid_transaction_mutation();


-- check if debit and credit are balanced every time transactions or entries happens
CREATE FUNCTION check_transaction_balanced() RETURNS trigger AS $$
DECLARE
  tid   bigint;
  n     int;
  total numeric;
BEGIN
  IF TG_TABLE_NAME = 'transactions' THEN
    tid := NEW.id;
  ELSE
    tid := NEW.transaction_id;
  END IF;

  SELECT count(*),
         COALESCE(SUM(CASE a.normal_balance WHEN 'debit' THEN e.amount ELSE -e.amount END), 0)
    INTO n, total
    FROM journal_entries e
    JOIN accounts a ON a.id = e.account_id
   WHERE e.transaction_id = tid;

  IF n = 0 THEN
    RAISE EXCEPTION 'transaction % has no entries', tid;
  END IF;
  IF total <> 0 THEN
    RAISE EXCEPTION 'transaction % is not balanced (off by %)', tid, total;
  END IF;
  RETURN NULL;
END $$ LANGUAGE plpgsql;

CREATE CONSTRAINT TRIGGER trg_tx_balanced_on_entry
  AFTER INSERT ON journal_entries
  DEFERRABLE INITIALLY DEFERRED -- delay the check until commit for multiple entries
  FOR EACH ROW EXECUTE FUNCTION check_transaction_balanced();

CREATE CONSTRAINT TRIGGER trg_tx_balanced_on_header
  AFTER INSERT ON transactions
  DEFERRABLE INITIALLY DEFERRED
  FOR EACH ROW EXECUTE FUNCTION check_transaction_balanced();


-- re-grant the user privileges
GRANT SELECT, INSERT ON
  app.users, app.messages, app.accounts, app.transactions, app.journal_entries, app.proposals
  TO :"app_user";

GRANT UPDATE (is_transaction)    ON app.messages     TO :"app_user";
GRANT UPDATE (name)              ON app.accounts     TO :"app_user";
GRANT UPDATE (description, note) ON app.transactions TO :"app_user";
GRANT UPDATE (status, decided_at) ON app.proposals   TO :"app_user";
