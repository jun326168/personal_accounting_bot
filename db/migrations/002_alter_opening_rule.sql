-- the old index enforces one opening globally, so it has to go
DROP INDEX uq_one_opening;

CREATE OR REPLACE FUNCTION enforce_one_opening_per_user() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
  v_user_id bigint;
BEGIN
  SELECT user_id INTO v_user_id FROM messages WHERE id = NEW.message_id;

  -- serialize concurrent openings for the same user; no table privileges needed
  PERFORM pg_advisory_xact_lock(v_user_id);

  IF EXISTS (
    SELECT 1
    FROM transactions t
    JOIN messages m ON m.id = t.message_id
    WHERE m.user_id = v_user_id
      AND t.kind = 'opening'
      AND t.id IS DISTINCT FROM NEW.id
  ) THEN
    RAISE EXCEPTION 'user % already has an opening transaction', v_user_id
      USING ERRCODE = 'unique_violation',
            CONSTRAINT = 'uq_one_opening_per_user';
  END IF;

  RETURN NEW;
END $$;

CREATE TRIGGER trg_one_opening_per_user
BEFORE INSERT OR UPDATE OF kind, message_id ON transactions
FOR EACH ROW
WHEN (NEW.kind = 'opening')
EXECUTE FUNCTION enforce_one_opening_per_user();