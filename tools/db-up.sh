#!/usr/bin/env bash
# Ensure the local PostgreSQL 17 development role and database exist.
set -euo pipefail
cd "$(dirname "$0")/.."
source tools/lib/product-env.sh

readonly DB_HOST="${PROTEOSM_DB_HOST:-localhost}"
readonly DB_NAME="${PROTEOSM_DB_NAME:-proteosm_dev}"
readonly DB_USER="${PROTEOSM_DB_USER:-proteosm}"
readonly ADMIN_DB="postgres"

# CI: the service container has no OS-named superuser, so psql connects as the configured role (password: PGPASSWORD).
if [[ "${CI:-}" == "true" ]]; then
  export PGUSER="$DB_USER"
fi

if ! pg_isready -h "$DB_HOST" >/dev/null; then
  echo "PostgreSQL is not running on $DB_HOST. Start it: brew services start postgresql@17" >&2
  exit 1
fi

if [[ "$(psql -h "$DB_HOST" -d "$ADMIN_DB" -tAc "SELECT 1 FROM pg_roles WHERE rolname = '$DB_USER'")" != "1" ]]; then
  psql -h "$DB_HOST" -d "$ADMIN_DB" -c "CREATE ROLE \"$DB_USER\" LOGIN"
fi

if [[ "$(psql -h "$DB_HOST" -d "$ADMIN_DB" -tAc "SELECT 1 FROM pg_database WHERE datname = '$DB_NAME'")" != "1" ]]; then
  psql -h "$DB_HOST" -d "$ADMIN_DB" -c "CREATE DATABASE \"$DB_NAME\" OWNER \"$DB_USER\""
fi

# CI passes the connection string through the environment; user-secrets are a developer-machine store.
if [[ "${CI:-}" == "true" ]]; then
  echo "Database $DB_NAME is ready on $DB_HOST (role $DB_USER)."
  exit 0
fi

dotnet user-secrets set "ConnectionStrings:Default" "Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER" \
  --project backend/src/ProteoSM.Api >/dev/null

echo "Database $DB_NAME is ready on $DB_HOST (role $DB_USER)."
