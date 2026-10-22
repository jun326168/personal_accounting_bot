#!/bin/sh
set -e

PSQL="psql -h db -U $POSTGRES_USER -d $POSTGRES_DB -v ON_ERROR_STOP=1 -v app_user=$APP_DB_USER"

# Tracking table lives in public so app_user (search_path = app) can't see it
$PSQL -c "CREATE TABLE IF NOT EXISTS public.schema_migrations (
  filename   text PRIMARY KEY,
  applied_at timestamptz NOT NULL DEFAULT now()
)"

for f in /migrations/*.sql; do
  [ -e "$f" ] || { echo "no migrations found"; break; }   # empty folder guard
  name=$(basename "$f")

  if [ "$($PSQL -tA -c "SELECT 1 FROM public.schema_migrations WHERE filename = '$name'")" = "1" ]; then
    echo "skip  $name"
    continue
  fi

  echo "apply $name"
  # One transaction: the migration and its record commit together or not at all
  $PSQL --single-transaction \
    -c "SET search_path TO app" \
    -f "$f" \
    -c "INSERT INTO public.schema_migrations (filename) VALUES ('$name')"
done

echo "migrations complete"