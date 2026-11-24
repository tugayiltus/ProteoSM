#!/usr/bin/env bash
# Deploys the Proteone stack (ADR-0024): quality gates, image build, fresh third-party images, then compose up (migrate
# runs before api/worker), then waits until the API answers through nginx. The deployed tag (Git short hash) is
# recorded in deploy/.version, so later tools/compose.sh calls keep using it. Refuses a working tree with uncommitted
# changes. --skip-gates skips lint and tests (explicit only). --fresh-install
# starts an empty stack on a host that still holds TGYSM data (refused otherwise).
set -euo pipefail
cd "$(dirname "$0")/.."
source tools/lib/env-file.sh

readonly READY_TIMEOUT_SECONDS=180
readonly HEALTH_PATH="/api/v1/health"
readonly ENV_FILE="${PROTEONE_ENV_FILE:-deploy/.env}"
readonly VERSION_FILE="deploy/.version"
readonly DEFAULT_HTTPS_PORT=443
# Images compose pulls from a registry; proteone-api and proteone-worker are built locally.
readonly THIRD_PARTY_SERVICES=(postgres nginx)
readonly USAGE="usage: tools/deploy.sh [--skip-gates] [--fresh-install]"
# A TGYSM installation on this host (before the rename) keeps its data in this volume.
readonly LEGACY_DATA_VOLUME="tgysm_pgdata"
readonly DATA_VOLUME="${COMPOSE_PROJECT_NAME:-proteone}_pgdata"
skip_gates=false
fresh_install=false
for argument in "$@"; do
  case "$argument" in
    --skip-gates) skip_gates=true ;;
    --fresh-install) fresh_install=true ;;
    *) echo "$USAGE" >&2; exit 1 ;;
  esac
done

for file in deploy/certs/proteone.crt deploy/certs/proteone.key "$ENV_FILE"; do
  if [[ ! -f "$file" ]]; then
    echo "Missing $file (see docs/operations.md)." >&2
    exit 1
  fi
done

if [[ -n "$(git status --porcelain)" ]]; then
  echo "Refused: the working tree has uncommitted changes (git status). Deploy a committed state only." >&2
  exit 1
fi

volume_exists() { docker volume inspect "$1" >/dev/null 2>&1; }
# Without this check the stack would start empty beside the old data and look as if everything were lost.
if [[ "$fresh_install" == false ]] && volume_exists "$LEGACY_DATA_VOLUME" && ! volume_exists "$DATA_VOLUME"; then
  echo "Refused: this host has TGYSM data ($LEGACY_DATA_VOLUME) and no Proteone data yet. Move it first (docs/operations.md," >&2
  echo "Moving a TGYSM installation), or pass --fresh-install to start empty on purpose; the old volume is not touched." >&2
  exit 1
fi

if [[ "$skip_gates" == false ]]; then
  tools/lint.sh
  tools/test.sh
fi

tools/docker-build.sh
version="$(git rev-parse --short HEAD)"
previous="$(cat "$VERSION_FILE" 2>/dev/null || true)"
echo "$version" > "$VERSION_FILE"
echo "Deploying $version (previous: ${previous:-none}); recorded in $VERSION_FILE."
tools/compose.sh pull "${THIRD_PARTY_SERVICES[@]}"
tools/compose.sh up -d --remove-orphans

https_port="$(env_value "$ENV_FILE" HTTPS_PORT)"
https_port="${https_port:-$DEFAULT_HTTPS_PORT}"
for ((second = 0; second < READY_TIMEOUT_SECONDS; second++)); do
  if curl -fsSk -o /dev/null "https://localhost:$https_port$HEALTH_PATH"; then
    echo "Proteone is up on https://localhost:$https_port"
    exit 0
  fi
  sleep 1
done

echo "Timed out waiting for $HEALTH_PATH through nginx; see tools/compose.sh logs" >&2
tools/compose.sh ps >&2
exit 1
