#!/usr/bin/env bash
# Tests the guard of tools/deploy.sh for the move from Proteone to ProteoSM (tools/lib/move-guard.sh): an empty start
# beside the volume of a Proteone stack (proteone_pgdata) is refused unless --fresh-install, also when the ProteoSM
# volume exists but its database is empty. A fake docker on PATH answers "volume inspect" from FAKE_VOLUMES and the
# compose calls of the guard from FAKE_HISTORY ("none": no migration history table, else its row count; "down": the
# database does not answer), so no Docker is needed. tools/deploy.sh runs stop before the image build (missing env
# file); the database cases call move_guard directly with an env file.
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
if [[ "$1" == compose ]]; then
  echo "$*" >> "$FAKE_LOG"
  shift 5  # compose -f <file> --env-file <file>
  case "$1 ${2:-} ${4:-}" in
    "ps --services"*) exit 0 ;;
    "up -d"* | "stop postgres"*) exit 0 ;;
    "exec -T pg_isready") [[ "$FAKE_HISTORY" != down ]]; exit ;;
    "exec -T psql")
      if [[ "$*" == *pg_tables* ]]; then [[ "$FAKE_HISTORY" == none ]] && echo 0 || echo 1; else echo "$FAKE_HISTORY"; fi
      exit 0 ;;
  esac
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

# Calls move_guard with an env file (optional content) and the given migration history; prints "refused" or "passed".
database_guard() {
  local volumes="$1" history="$2" env_content="${3:-}" output
  printf '%s\n' "$env_content" > "$work/test.env"
  output="$(cd "$PWD" && PATH="$work/bin:$PATH" FAKE_VOLUMES="$volumes" FAKE_HISTORY="$history" FAKE_LOG="$work/compose.log" \
    PROTEOSM_ENV_FILE="$work/test.env" COMPOSE_PROJECT_NAME= MOVE_GUARD_READY_TIMEOUT_SECONDS=2 bash -c \
    'source tools/lib/env-file.sh; source tools/lib/move-guard.sh; move_guard "$PROTEOSM_ENV_FILE"' 2>&1 || true)"
  if grep -q "$GUARD_TEXT" <<<"$output"; then echo refused; else echo passed; fi
}

check() {
  if [[ "$2" == "$3" ]]; then echo "PASS $1"; else echo "FAIL $1 (got '$2', expected '$3')"; failures=$((failures + 1)); fi
}

check "Proteone data and no ProteoSM data: refused, pointing to the move procedure" "$(guard "proteone_pgdata")" refused
check "--fresh-install starts empty on purpose" "$(guard "proteone_pgdata" --fresh-install)" passed
check "both volumes, no env file yet: the guard leaves the stop to the missing-file check" "$(guard "proteone_pgdata proteosm_pgdata")" passed
check "after the move (both volumes, migrations applied): deploys" "$(database_guard "proteone_pgdata proteosm_pgdata" 42)" passed
check "both volumes, new DB empty: refused" "$(database_guard "proteone_pgdata proteosm_pgdata" none)" refused
check "both volumes, migration history empty: refused" "$(database_guard "proteone_pgdata proteosm_pgdata" 0)" refused
check "both volumes, new DB does not answer: refused" "$(database_guard "proteone_pgdata proteosm_pgdata" down)" refused
check "project name from the env file: its volume is checked" \
  "$(database_guard "proteone_pgdata proteosm_pgdata" 42 "COMPOSE_PROJECT_NAME=site")" refused
check "project name from the env file, its database filled: deploys" \
  "$(database_guard "proteone_pgdata site_pgdata" 42 "COMPOSE_PROJECT_NAME=site")" passed
check "a populated ProteoSM stack alone: no database check" "$(database_guard "proteosm_pgdata" none)" passed
check "a host without Proteone data: deploys" "$(guard "")" passed
check "an existing ProteoSM stack: deploys" "$(guard "proteosm_pgdata")" passed

if [[ "$failures" -gt 0 ]]; then echo "$failures check(s) failed"; exit 1; fi
echo "All deploy guard checks passed"
