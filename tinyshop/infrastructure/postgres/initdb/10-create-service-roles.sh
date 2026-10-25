#!/usr/bin/env bash
# Creates the two least-privilege roles a service uses for its own database
# (architecture.md §10, "Database roles"):
#
#   <prefix>_migrator   owns the schema: creates, alters and drops tables. Used only by the
#                       one-off migration job.
#   <prefix>_app        the running service: only the table privileges its contract needs,
#                       never DDL.
#
# The superuser (POSTGRES_USER) is used for nothing else afterwards.
#
# The official postgres image runs this once, as POSTGRES_USER, from /docker-entrypoint-initdb.d,
# and only when the data directory is empty. Changing it later has no effect until the volume is
# recreated.
#
# Shared by every service database; each one configures it through its container's environment:
#   DB_ROLE_PREFIX         e.g. "product" -> product_migrator, product_app
#   MIGRATOR_PASSWORD      password for <prefix>_migrator
#   APP_PASSWORD           password for <prefix>_app
#   APP_TABLE_PRIVILEGES   comma-separated, from SELECT, INSERT, UPDATE, DELETE (default: SELECT)
#
# The image runs this file if it's executable, and otherwise *sources* it into the entrypoint's
# own shell. So: no "set -u" (it would leak into the entrypoint), and every failure ends in an
# explicit "exit 1", because "set -e" is only in effect when the file is sourced. Exiting is
# right in both modes: a failure here must abort initialisation.

: "${DB_ROLE_PREFIX:?DB_ROLE_PREFIX must be set}"
: "${MIGRATOR_PASSWORD:?MIGRATOR_PASSWORD must be set}"
: "${APP_PASSWORD:?APP_PASSWORD must be set}"
app_table_privileges="${APP_TABLE_PRIVILEGES:-SELECT}"

# The privilege list is pasted into SQL as-is, so accept nothing but the four DML keywords.
if ! [[ "$app_table_privileges" =~ ^(SELECT|INSERT|UPDATE|DELETE)(,[[:space:]]*(SELECT|INSERT|UPDATE|DELETE))*$ ]]; then
  echo "APP_TABLE_PRIVILEGES must list SELECT/INSERT/UPDATE/DELETE, got: $app_table_privileges" >&2
  exit 1
fi

# Inserting into an identity column draws a value from its sequence; reading doesn't.
if [[ "$app_table_privileges" == *INSERT* ]]; then
  app_sequence_privileges="USAGE, SELECT"
else
  app_sequence_privileges=""
fi

echo "Creating roles ${DB_ROLE_PREFIX}_migrator and ${DB_ROLE_PREFIX}_app (app tables: $app_table_privileges)"

# Values go in as psql variables (-v), never pasted into the SQL text by the shell:
# :"name" quotes an identifier, :'name' quotes a string literal, so passwords can contain anything.
# The quoted heredoc ('SQL') stops the shell from expanding anything inside it.
psql -v ON_ERROR_STOP=1 \
  --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v db="$POSTGRES_DB" \
  -v migrator="${DB_ROLE_PREFIX}_migrator" \
  -v app="${DB_ROLE_PREFIX}_app" \
  -v migrator_password="$MIGRATOR_PASSWORD" \
  -v app_password="$APP_PASSWORD" \
  -v app_table_privileges="$app_table_privileges" \
  -v app_sequence_privileges="$app_sequence_privileges" \
  <<'SQL'
CREATE ROLE :"migrator" LOGIN PASSWORD :'migrator_password';
CREATE ROLE :"app" LOGIN PASSWORD :'app_password';

-- PUBLIC means "every role". By default it may connect to any database and create temporary
-- tables there. Take that away, then let in exactly these two roles.
REVOKE ALL ON DATABASE :"db" FROM PUBLIC;
GRANT CONNECT ON DATABASE :"db" TO :"migrator", :"app";

-- The migrator owns the schema the tables live in, so it alone can create, alter and drop them.
-- The app may only look things up in it (USAGE), not create anything (no CREATE).
ALTER SCHEMA public OWNER TO :"migrator";
REVOKE ALL ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO :"app";

-- Grants apply to tables that already exist; migrations create tables later. Default privileges
-- fix that: every table the migrator creates from now on is automatically granted to the app.
ALTER DEFAULT PRIVILEGES FOR ROLE :"migrator" IN SCHEMA public
  GRANT :app_table_privileges ON TABLES TO :"app";

SELECT :'app_sequence_privileges' <> '' AS grant_sequences \gset
\if :grant_sequences
ALTER DEFAULT PRIVILEGES FOR ROLE :"migrator" IN SCHEMA public
  GRANT :app_sequence_privileges ON SEQUENCES TO :"app";
\endif
SQL
psql_status=$?
if (( psql_status != 0 )); then
  echo "Creating roles for ${DB_ROLE_PREFIX} failed (psql exit code $psql_status)" >&2
  exit 1
fi
