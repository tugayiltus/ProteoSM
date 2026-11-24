#!/usr/bin/env bash
# Create the first admin (once). Usage: tools/setup-admin.sh --email <email> --first-name <name> --last-name <name>
set -euo pipefail
cd "$(dirname "$0")/.."

readonly DB_HOST="${PROTEONE_DB_HOST:-localhost}"
readonly DB_NAME="${PROTEONE_DB_NAME:-proteone_dev}"
readonly DB_USER="${PROTEONE_DB_USER:-proteone}"

tools/migrate.sh
export ConnectionStrings__Default="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
dotnet run --project backend/src/Proteone.Api --no-launch-profile -- setup-admin "$@"
