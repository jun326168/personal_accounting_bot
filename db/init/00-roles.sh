#!/bin/sh
# Runs once, on first start with an empty data volume (docker-entrypoint-initdb.d).
# Creates the least-privilege role the API connects as. Schema objects are owned by
# the admin role (POSTGRES_USER), so the API role can never alter or drop them.
set -eu
: "${APP_DB_PASSWORD:?APP_DB_PASSWORD must be set}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
    -v app_password="$APP_DB_PASSWORD" -v db_name="$POSTGRES_DB" <<'SQL'
CREATE ROLE accounting_app LOGIN PASSWORD :'app_password'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS
    CONNECTION LIMIT 20;

REVOKE ALL ON DATABASE :"db_name" FROM PUBLIC;
GRANT CONNECT ON DATABASE :"db_name" TO accounting_app;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
SQL
