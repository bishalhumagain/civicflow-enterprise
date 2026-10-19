#!/usr/bin/env bash
set -euo pipefail

: "${CIVICFLOW_RUNTIME_PASSWORD:?CIVICFLOW_RUNTIME_PASSWORD is required}"
: "${CIVICFLOW_MIGRATOR_PASSWORD:?CIVICFLOW_MIGRATOR_PASSWORD is required}"

psql -v ON_ERROR_STOP=1 \
  -v runtime_password="$CIVICFLOW_RUNTIME_PASSWORD" \
  -v migrator_password="$CIVICFLOW_MIGRATOR_PASSWORD" \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" <<'SQL'
CREATE ROLE civicflow_migrator LOGIN PASSWORD :'migrator_password';
CREATE ROLE civicflow_runtime LOGIN PASSWORD :'runtime_password';

GRANT CONNECT ON DATABASE civicflow TO civicflow_migrator;
GRANT CONNECT ON DATABASE civicflow TO civicflow_runtime;

GRANT USAGE, CREATE ON SCHEMA public TO civicflow_migrator;
GRANT USAGE ON SCHEMA public TO civicflow_runtime;
REVOKE CREATE ON SCHEMA public FROM civicflow_runtime;

ALTER DEFAULT PRIVILEGES FOR ROLE civicflow_migrator IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE, REFERENCES ON TABLES TO civicflow_runtime;
ALTER DEFAULT PRIVILEGES FOR ROLE civicflow_migrator IN SCHEMA public
    GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO civicflow_runtime;
SQL
