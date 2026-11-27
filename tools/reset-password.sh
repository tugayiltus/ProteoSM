#!/usr/bin/env bash
# Issue a temporary password for an account from the console. Usage: tools/reset-password.sh --email <email>
# The user must choose a new password at the next sign-in.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly DB_HOST="${PROTEONE_DB_HOST:-localhost}"
readonly DB_NAME="${PROTEONE_DB_NAME:-proteone_dev}"
readonly DB_USER="${PROTEONE_DB_USER:-proteone}"

tools/migrate.sh
export ConnectionStrings__Default="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
dotnet run --project backend/src/ProteoSM.Api --no-launch-profile -- reset-password "$@"
