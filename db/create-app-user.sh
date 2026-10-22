#!/bin/sh
set -e
psql -h db -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 \
  -v app_user="$APP_DB_USER" -v app_pass="$APP_DB_PASSWORD" -v db_name="$POSTGRES_DB" <<'SQL'
-- Create app user if not exist, update password if exists
SELECT format('CREATE ROLE %I LOGIN PASSWORD %L', :'app_user', :'app_pass')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = :'app_user')
\gexec
SELECT format('ALTER ROLE %I PASSWORD %L', :'app_user', :'app_pass')
\gexec

ALTER ROLE :"app_user" SET search_path TO app;

GRANT CONNECT ON DATABASE :"db_name" TO :"app_user";
GRANT USAGE ON SCHEMA app TO :"app_user";
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA app TO :"app_user";
ALTER DEFAULT PRIVILEGES IN SCHEMA app
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"app_user";
ALTER DEFAULT PRIVILEGES IN SCHEMA app
  GRANT USAGE, SELECT ON SEQUENCES TO :"app_user";
SQL