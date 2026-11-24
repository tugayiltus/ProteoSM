#!/usr/bin/env bash
# Apply EF Core migrations and the post-migration data steps to the database named by PROTEONE_DB_* (default: local
# proteone_dev), through the API's "migrate" command — the same path the deployment uses (ADR-0024).
set -euo pipefail
cd "$(dirname "$0")/../backend"

readonly DB_HOST="${PROTEONE_DB_HOST:-localhost}"
readonly DB_NAME="${PROTEONE_DB_NAME:-proteone_dev}"
readonly DB_USER="${PROTEONE_DB_USER:-proteone}"

export ConnectionStrings__Default="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
export ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/Proteone.Api --no-launch-profile -v quiet -- migrate
