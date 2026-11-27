#!/usr/bin/env bash
# Create the first admin (once). Usage: tools/setup-admin.sh --email <email> --first-name <name> --last-name <name>
set -euo pipefail
cd "$(dirname "$0")/.."
source tools/lib/product-env.sh

readonly DB_HOST="${PROTEOSM_DB_HOST:-localhost}"
readonly DB_NAME="${PROTEOSM_DB_NAME:-proteosm_dev}"
readonly DB_USER="${PROTEOSM_DB_USER:-proteosm}"

tools/migrate.sh
export ConnectionStrings__Default="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
dotnet run --project backend/src/ProteoSM.Api --no-launch-profile -- setup-admin "$@"
