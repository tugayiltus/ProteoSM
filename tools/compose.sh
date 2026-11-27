#!/usr/bin/env bash
# docker compose for the ProteoSM stack: always deploy/compose.yaml with deploy/.env (ADR-0024). The image tag is the one
# tools/deploy.sh recorded in deploy/.version; PROTEOSM_VERSION in the environment overrides it.
set -euo pipefail
cd "$(dirname "$0")/.."
source tools/lib/product-env.sh

readonly COMPOSE_FILE="deploy/compose.yaml"
readonly ENV_FILE="${PROTEOSM_ENV_FILE:-deploy/.env}"
readonly VERSION_FILE="deploy/.version"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing $ENV_FILE: copy deploy/.env.example and fill it in." >&2
  exit 1
fi

if [[ -z "${PROTEOSM_VERSION:-}" && -s "$VERSION_FILE" ]]; then
  PROTEOSM_VERSION="$(tr -d '[:space:]' < "$VERSION_FILE")"
  export PROTEOSM_VERSION
fi

exec docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" "$@"
