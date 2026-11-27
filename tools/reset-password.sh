#!/usr/bin/env bash
# Issue a temporary password for an account from the console. Usage: tools/reset-password.sh --email <email>
# The user must choose a new password at the next sign-in.
set -euo pipefail
cd "$(dirname "$0")/.."
source tools/lib/product-env.sh

readonly DB_HOST="${PROTEOSM_DB_HOST:-localhost}"
readonly DB_NAME="${PROTEOSM_DB_NAME:-proteosm_dev}"
readonly DB_USER="${PROTEOSM_DB_USER:-proteosm}"

tools/migrate.sh
export ConnectionStrings__Default="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
dotnet run --project backend/src/ProteoSM.Api --no-launch-profile -- reset-password "$@"
