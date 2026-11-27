#!/usr/bin/env bash
# Move drill (docs/operations.md, "Proteone to ProteoSM move"): on throw-away stacks only, never a real stack or its
# volumes.
#   1. A fresh ProteoSM stack starts empty, the first admin signs in.
#   2. A Proteone stack runs the images and scripts of the last commit before the rename (a git worktree; it builds
#      proteone-api/proteone-worker there when they are missing), gets pilot data, the restore-drill data (attachment,
#      report export file, deactivated user), a custom email template, saved SMTP/LDAP/Entra secrets and a monitoring
#      key; its own tools/backup.sh writes a proteone- archive.
#   3. tools/restore.sh of this checkout restores that archive into a new ProteoSM stack; row counts of every table and
#      row samples are compared with the Proteone database, then tools/lib/proteosm_move_drill.py checks sign-in,
#      files, templates and that every stored secret still decrypts.
# Writes docs/release/proteosm-migration-drill.md. Needs Docker, the ProteoSM images (tools/docker-build.sh) and a
# certificate pair in deploy/certs/ (tools/gen-self-signed-cert.sh).
#
#   tools/proteosm-move-drill.sh [git ref of the Proteone stack]
set -euo pipefail
export PYTHONDONTWRITEBYTECODE=1
cd "$(dirname "$0")/.."
source tools/lib/product-env.sh

readonly USAGE="usage: tools/proteosm-move-drill.sh [git ref]"
# The last commit before the ProteoSM rename (origin/master when the rename started).
readonly OLD_REF="${1:-0355db4}"
if (( $# > 1 )); then echo "$USAGE" >&2; exit 1; fi
readonly FRESH_PROJECT="proteosm-move-drill-fresh"
readonly OLD_PROJECT="proteone-move-drill-old"
readonly NEW_PROJECT="proteosm-move-drill-new"
readonly FRESH_PORT=9445
readonly OLD_PORT=9446
readonly NEW_PORT=9447
readonly FRESH_HTTP_PORT=9082
readonly OLD_HTTP_PORT=9083
readonly NEW_HTTP_PORT=9084
readonly FRESH_SUBNET="172.29.5.0/24"
readonly OLD_SUBNET="172.29.3.0/24"
readonly NEW_SUBNET="172.29.4.0/24"
readonly READY_TIMEOUT_SECONDS=240
readonly ADMIN_EMAIL="move-admin@example.com"
readonly TEMP_PASSWORD_PREFIX="Temporary password (shown once): "
readonly PILOT_SCALE="0.2"
readonly REPORT="docs/release/proteosm-migration-drill.md"
readonly NEW_VERSION="${PROTEOSM_VERSION:-latest}"
# Exact row count of every table in one query.
readonly TABLE_COUNTS_SQL="SELECT table_name || '=' || (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', table_schema, table_name), false, true, '')))[1]::text FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE' ORDER BY table_name"
# Row samples: a digest over whole rows, so any changed value shows.
readonly SAMPLE_TABLES=(users tickets ticket_assets change_ticket_links problem_tickets ticket_attachments export_jobs email_templates audit_events monitoring_sources auth_settings email_settings)
# Must be equal after the move; other tables may grow once the new Worker runs (heartbeats, SLA monitor, outbox).
readonly SUMMARY_TABLES=(users tickets ticket_assets change_ticket_links problem_tickets ticket_comments ticket_attachments export_jobs email_templates audit_events monitoring_sources)

for file in deploy/certs/proteosm.crt deploy/certs/proteosm.key; do
  if [[ ! -f "$file" ]]; then echo "Missing $file (tools/gen-self-signed-cert.sh for a test host)" >&2; exit 1; fi
done

mkdir -p tmp
work="$(mktemp -d "$PWD/tmp/move-drill.XXXXXX")"
worktree="$work/proteone"
failures=0
results=()
counts_rows=()

fresh() { COMPOSE_PROJECT_NAME="$FRESH_PROJECT" PROTEOSM_ENV_FILE="$work/fresh.env" PROTEOSM_VERSION="$NEW_VERSION" tools/compose.sh "$@"; }
new() { COMPOSE_PROJECT_NAME="$NEW_PROJECT" PROTEOSM_ENV_FILE="$work/new.env" PROTEOSM_VERSION="$NEW_VERSION" tools/compose.sh "$@"; }
# The old stack runs its own scripts, which know only the PROTEONE_ names.
old() { (cd "$worktree" && env -u PROTEOSM_ENV_FILE -u PROTEOSM_VERSION COMPOSE_PROJECT_NAME="$OLD_PROJECT" PROTEONE_ENV_FILE="$work/old.env" PROTEONE_VERSION="$old_tag" tools/compose.sh "$@"); }
teardown() {
  fresh down -v --remove-orphans >/dev/null 2>&1 || true
  new down -v --remove-orphans >/dev/null 2>&1 || true
  if [[ -d "$worktree" ]]; then
    old down -v --remove-orphans >/dev/null 2>&1 || true
    git worktree remove --force "$worktree" >/dev/null 2>&1 || true
  fi
  rm -rf "$work"
}
trap teardown EXIT

record() {
  local check="$1" expected="$2" actual="$3" result="pass"
  if [[ "$expected" != "$actual" ]]; then result="FAIL"; failures=$((failures + 1)); fi
  results+=("| $check | $expected | $actual | $result |")
  echo "$result: $check (expected $expected, got $actual)"
}
record_lines() {
  local line
  while IFS= read -r line; do
    [[ -z "$line" ]] && continue
    record "$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["check"])' "$line")" \
      "$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["expected"])' "$line")" \
      "$(python3 -c 'import json,sys; print(json.loads(sys.argv[1])["actual"])' "$line")"
  done < "$1"
}
write_env() {
  local template="$1" file="$2" https_port="$3" http_port="$4" subnet="$5"
  sed -e "s/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=drill-$(openssl rand -hex 12)/" \
      -e "s#^PUBLIC_BASE_URL=.*#PUBLIC_BASE_URL=https://localhost:$https_port#" \
      -e "s/^HTTPS_PORT=.*/HTTPS_PORT=$https_port/" -e "s/^HTTP_PORT=.*/HTTP_PORT=$http_port/" \
      -e "s#^\(PROTEO[A-Z]*\)_SUBNET=.*#\1_SUBNET=$subnet#" -e "s/^EMAIL_MODE=.*/EMAIL_MODE=Pickup/" \
      -e "s/^BACKUP_HOST_DIR=.*/BACKUP_HOST_DIR=/" -e "s/^# \(PROTEO[A-Z]*_DEPLOYMENT=pilot\)/\1/" "$template" > "$file"
}
wait_healthy() {
  for ((second = 0; second < READY_TIMEOUT_SECONDS; second++)); do
    if "$1" ps api --format '{{.Status}}' 2>/dev/null | grep -q '(healthy)'; then return 0; fi
    sleep 1
  done
  echo "Timed out waiting for the API of $1" >&2
  return 1
}
setup_admin() { "$1" run --rm -T api setup-admin --email "$ADMIN_EMAIL" --first-name Move --last-name Admin 2>/dev/null | sed -n "s/^${TEMP_PASSWORD_PREFIX}//p"; }
sql() { "$1" exec -T postgres psql -U "$2" -d "$2" -XtAc "$3"; }
digest() { sql "$1" "$2" "SELECT md5(coalesce(string_agg(t::text, E'\\n' ORDER BY t::text), '')) FROM $3 t" | tr -d '[:space:]'; }

echo "== 1. Fresh ProteoSM stack"
write_env deploy/.env.example "$work/fresh.env" "$FRESH_PORT" "$FRESH_HTTP_PORT" "$FRESH_SUBNET"
fresh up -d >/dev/null 2>&1
wait_healthy fresh
record "fresh: database and role are proteosm" "proteosm" "$(sql fresh proteosm "SELECT current_database() || ':' || current_user" | tr -d '[:space:]' | cut -d: -f1)"
fresh_temporary="$(setup_admin fresh)"
python3 tools/lib/proteosm_move_drill.py fresh "https://localhost:$FRESH_PORT" "$ADMIN_EMAIL" "$fresh_temporary" > "$work/fresh.jsonl" || true
record_lines "$work/fresh.jsonl"
fresh down -v >/dev/null 2>&1

echo "== 2. Proteone stack ($OLD_REF)"
git worktree add --detach "$worktree" "$OLD_REF" >/dev/null 2>&1
old_tag="$(git rev-parse --short "$OLD_REF")"
if ! docker image inspect "proteone-api:$old_tag" >/dev/null 2>&1 || ! docker image inspect "proteone-worker:$old_tag" >/dev/null 2>&1; then
  (cd "$worktree" && tools/docker-build.sh) >"$work/old-build.log" 2>&1 || { tail -20 "$work/old-build.log" >&2; exit 1; }
fi
mkdir -p "$worktree/deploy/certs"
cp deploy/certs/proteosm.crt "$worktree/deploy/certs/proteone.crt"
cp deploy/certs/proteosm.key "$worktree/deploy/certs/proteone.key"
write_env "$worktree/deploy/.env.example" "$work/old.env" "$OLD_PORT" "$OLD_HTTP_PORT" "$OLD_SUBNET"
old up -d >/dev/null 2>&1
wait_healthy old
old_temporary="$(setup_admin old)"
# The seed command directly: the compose environment also sets DOTNET_ENVIRONMENT=Production, so both names are set.
old run --rm -T -e ASPNETCORE_ENVIRONMENT=Pilot -e DOTNET_ENVIRONMENT=Pilot api seed-pilot --scale "$PILOT_SCALE" >"$work/pilot.log" 2>&1 \
  || { tail -20 "$work/pilot.log" >&2; exit 1; }
python3 tools/lib/proteosm_drill.py seed "https://localhost:$OLD_PORT" "$work/state.json" "$ADMIN_EMAIL" "$old_temporary"
python3 tools/lib/proteosm_move_drill.py extra "https://localhost:$OLD_PORT" "$work/state.json"
# Writes stop: only the database and the backup service stay up, as in the operator procedure.
old stop api worker nginx >/dev/null 2>&1
sql old proteone "$TABLE_COUNTS_SQL" > "$work/old-counts.txt"
for table in "${SAMPLE_TABLES[@]}"; do echo "$table=$(digest old proteone "$table")"; done > "$work/old-samples.txt"
archive_path="$(cd "$worktree" && env -u PROTEOSM_ENV_FILE COMPOSE_PROJECT_NAME="$OLD_PROJECT" PROTEONE_ENV_FILE="$work/old.env" PROTEONE_VERSION="$old_tag" tools/backup.sh | sed -n 's/^Backup written: //p')"
mkdir -p "$work/archives"
old cp "backup:$archive_path" "$work/archives/" >/dev/null 2>&1
archive="$work/archives/$(basename "$archive_path")"
record "the Proteone stack writes a proteone- archive" "proteone-" "$(basename "$archive" | cut -c1-9)"
# Down without -v: the old volumes stay for a rollback until the drill's teardown.
old down >/dev/null 2>&1

echo "== 3. Restore into a new ProteoSM stack"
write_env deploy/.env.example "$work/new.env" "$NEW_PORT" "$NEW_HTTP_PORT" "$NEW_SUBNET"
code=0
COMPOSE_PROJECT_NAME="$NEW_PROJECT" PROTEOSM_ENV_FILE="$work/new.env" PROTEOSM_VERSION="$NEW_VERSION" tools/restore.sh "$archive" || code=$?
record "tools/restore.sh restores the proteone- archive (row counts match its manifest, migrate ran)" "0" "$code"
new stop api worker nginx >/dev/null 2>&1
sql new proteosm "$TABLE_COUNTS_SQL" > "$work/new-counts.txt"
for table in "${SAMPLE_TABLES[@]}"; do echo "$table=$(digest new proteosm "$table")"; done > "$work/new-samples.txt"
new up -d >/dev/null 2>&1
wait_healthy new

# The full-table equality right after pg_restore is tools/restore.sh's manifest check (above). Here the new stack has
# run, so tables its Worker writes may have grown; they are listed, not failed.
grown="$(diff "$work/old-counts.txt" "$work/new-counts.txt" | sed -n 's/^> \([a-z_]*\)=.*/\1/p' | tr '\n' ' ' | sed 's/ $//' || true)"
for table in "${SUMMARY_TABLES[@]}"; do
  before="$(sed -n "s/^$table=//p" "$work/old-counts.txt")"
  after="$(sed -n "s/^$table=//p" "$work/new-counts.txt")"
  if [[ "$before" == "$after" ]]; then result=pass; else result=FAIL; failures=$((failures + 1)); fi
  counts_rows+=("| \`$table\` | $before | $after | $result |")
done
for table in "${SAMPLE_TABLES[@]}"; do
  record "rows of $table identical (digest)" "$(sed -n "s/^$table=//p" "$work/old-samples.txt")" "$(sed -n "s/^$table=//p" "$work/new-samples.txt")"
done
record "the new stack's database and role are proteosm" "proteosm" "$(sql new proteosm "SELECT tableowner FROM pg_tables WHERE tablename = 'tickets'" | tr -d '[:space:]')"
python3 tools/lib/proteosm_move_drill.py verify "https://localhost:$NEW_PORT" "$work/state.json" > "$work/verify.jsonl" || true
record_lines "$work/verify.jsonl"

echo "== Rollback"
old up -d >/dev/null 2>&1
if wait_healthy old; then record "the untouched Proteone stack starts again" "healthy" "healthy"; else record "the untouched Proteone stack starts again" "healthy" "not healthy"; fi

mkdir -p "$(dirname "$REPORT")"
{
  echo "# ProteoSM move drill"
  echo
  echo "Run by \`tools/proteosm-move-drill.sh\` on $(date -u +%Y-%m-%dT%H:%M:%SZ) at commit \`$(git rev-parse --short HEAD)\`: Proteone stack from \`$OLD_REF\` (images \`proteone-*:$old_tag\`), ProteoSM images \`proteosm-*:$NEW_VERSION\`. Procedure: \`docs/operations.md\`, \"Proteone to ProteoSM move\". Throw-away Compose projects \`$FRESH_PROJECT\`, \`$OLD_PROJECT\`, \`$NEW_PROJECT\`; all removed at the end."
  echo
  echo "Steps: (1) fresh ProteoSM stack + \`setup-admin\`; (2) Proteone stack, \`seed-pilot --scale $PILOT_SCALE\` (Pilot environment), \`tools/lib/proteosm_drill.py seed\`, \`tools/lib/proteosm_move_drill.py extra\`, writes stopped, \`tools/backup.sh\` of that checkout; (3) \`tools/restore.sh $(basename "$archive")\` into the ProteoSM stack, counts and digests compared with writes stopped, \`tools/lib/proteosm_move_drill.py verify\`; (4) the old stack starts again (rollback)."
  echo
  echo "## Row counts"
  echo
  echo "| Table | Proteone | ProteoSM | Result |"
  echo "|---|---|---|---|"
  printf '%s\n' "${counts_rows[@]}"
  echo
  echo "Tables that grew after the new stack started (Worker jobs on restored data, not part of the archive comparison): ${grown:-none}."
  echo
  echo "## Checks"
  echo
  echo "| Check | Expected | Actual | Result |"
  echo "|---|---|---|---|"
  printf '%s\n' "${results[@]}"
  echo
  if [[ "$failures" -eq 0 ]]; then echo "Result: **all checks passed**."; else echo "Result: **$failures check(s) failed**."; fi
} > "$REPORT"
echo "Report: $REPORT"
if [[ "$failures" -gt 0 ]]; then exit 1; fi
echo "Move drill passed"
