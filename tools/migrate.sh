#!/usr/bin/env bash
# Apply EF Core migrations and the post-migration data steps to the database named by PROTEOSM_DB_* (default: local
# proteosm_dev), through the API's "migrate" command — the same path the deployment uses (ADR-0024).
set -euo pipefail
cd "$(dirname "$0")/../backend"
source ../tools/lib/product-env.sh

readonly DB_HOST="${PROTEOSM_DB_HOST:-localhost}"
readonly DB_NAME="${PROTEOSM_DB_NAME:-proteosm_dev}"
readonly DB_USER="${PROTEOSM_DB_USER:-proteosm}"

export ConnectionStrings__Default="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
export ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/ProteoSM.Api --no-launch-profile -v quiet -- migrate
