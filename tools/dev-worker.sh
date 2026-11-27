#!/usr/bin/env bash
# Runs the Worker in the foreground for local development (email to tmp/email-pickup; build + last-good fallback in
# tools/dev-dotnet-host.sh). Used by tools/run.sh and by the launchd service (tools/dev-service.sh).
set -euo pipefail
cd "$(dirname "$0")/.."

readonly ROOT="$(pwd)"
readonly DB_HOST="${PROTEONE_DB_HOST:-localhost}"
readonly DB_NAME="${PROTEONE_DB_NAME:-proteone_dev}"
readonly DB_USER="${PROTEONE_DB_USER:-proteone}"
# The Worker has no user secrets of its own, so it gets the API's connection string through the environment.
readonly WORKER_CONNECTION="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
# Absolute: Email:PickupFolder is the only Worker setting whose development default is a relative path.
readonly PICKUP_FOLDER="$ROOT/tmp/email-pickup"

exec tools/dev-dotnet-host.sh worker backend/src/ProteoSM.Worker ProteoSM.Worker \
  "ConnectionStrings__Default=$WORKER_CONNECTION" DOTNET_ENVIRONMENT=Development "Email__PickupFolder=$PICKUP_FOLDER"
