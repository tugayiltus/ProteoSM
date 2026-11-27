#!/usr/bin/env bash
# Tests the guard of tools/deploy.sh for the move from Proteone to ProteoSM: an empty start beside the volume of a
# Proteone stack (proteone_pgdata) is refused unless --fresh-install. A fake docker on PATH answers "volume inspect"
# from FAKE_VOLUMES, so no Docker is needed; every run stops before the image build (missing env file).
set -euo pipefail
cd "$(dirname "$0")/../.."

readonly GUARD_TEXT="Refused: this host has Proteone data"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
failures=0

mkdir -p "$work/bin"
cat > "$work/bin/docker" <<'EOF'
#!/usr/bin/env bash
if [[ "$1 $2" == "volume inspect" ]]; then
  for volume in $FAKE_VOLUMES; do [[ "$volume" == "$3" ]] && exit 0; done
  exit 1
fi
echo "fake docker: unexpected call: $*" >&2
exit 99
EOF
chmod +x "$work/bin/docker"

# Runs tools/deploy.sh with the given volumes present; prints "refused" or "passed" (the guard let it through).
guard() {
  local volumes="$1" output
  shift
  output="$(PATH="$work/bin:$PATH" FAKE_VOLUMES="$volumes" PROTEOSM_ENV_FILE="$work/missing.env" COMPOSE_PROJECT_NAME= \
    tools/deploy.sh "$@" 2>&1 || true)"
  if grep -q "$GUARD_TEXT" <<<"$output"; then
    if grep -q 'docs/operations.md' <<<"$output"; then echo refused; else echo "refused without the procedure"; fi
  else
    echo passed
  fi
}

check() {
  if [[ "$2" == "$3" ]]; then echo "PASS $1"; else echo "FAIL $1 (got '$2', expected '$3')"; failures=$((failures + 1)); fi
}

check "Proteone data and no ProteoSM data: refused, pointing to the move procedure" "$(guard "proteone_pgdata")" refused
check "--fresh-install starts empty on purpose" "$(guard "proteone_pgdata" --fresh-install)" passed
check "after the move (both volumes): deploys" "$(guard "proteone_pgdata proteosm_pgdata")" passed
check "a host without Proteone data: deploys" "$(guard "")" passed
check "an existing ProteoSM stack: deploys" "$(guard "proteosm_pgdata")" passed

if [[ "$failures" -gt 0 ]]; then echo "$failures check(s) failed"; exit 1; fi
echo "All deploy guard checks passed"
