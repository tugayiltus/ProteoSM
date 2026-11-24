#!/usr/bin/env bash
# Upgrade drill (ADR-0037): runs a throw-away stack of a TGYSM image, leaves an account link waiting in its outbox, backs
# it up with the scripts of that time, restores the archive into a throw-away Proteone stack and checks what the rename
# could break. Never touches a real stack or its volumes. Needs tools/docker-build.sh, tools/gen-self-signed-cert.sh and
# a local tgysm-api/tgysm-worker image.
#
#   tools/upgrade-drill.sh <tgysm image tag> [git ref of the TGYSM scripts]
set -euo pipefail
cd "$(dirname "$0")/.."

readonly USAGE="usage: tools/upgrade-drill.sh <tgysm image tag> [git ref]"
readonly OLD_TAG="${1:?$USAGE}"
# The last commit under the former name.
readonly OLD_REF="${2:-b4523a3}"
readonly OLD_PROJECT="tgysm-upgrade-drill"
readonly NEW_PROJECT="proteone-upgrade-drill"
readonly OLD_PORT=9445
readonly NEW_PORT=9446
readonly OLD_HTTP_PORT=9082
readonly NEW_HTTP_PORT=9083
readonly READY_TIMEOUT_SECONDS=240
readonly MAIL_TIMEOUT_SECONDS=120
readonly ADMIN_EMAIL="upgrade-admin@example.com"
readonly TEMP_PASSWORD_PREFIX="Temporary password (shown once): "

mkdir -p tmp
work="$(mktemp -d "$PWD/tmp/upgrade-drill.XXXXXX")"
failures=0
old() { (cd "$work/old" && COMPOSE_PROJECT_NAME="$OLD_PROJECT" TGYSM_ENV_FILE="$work/old.env" TGYSM_VERSION="$OLD_TAG" tools/compose.sh "$@"); }
new() { COMPOSE_PROJECT_NAME="$NEW_PROJECT" PROTEONE_ENV_FILE="$work/new.env" PROTEONE_VERSION=latest tools/compose.sh "$@"; }
teardown() {
  old down -v --remove-orphans >/dev/null 2>&1 || true
  new down -v --remove-orphans >/dev/null 2>&1 || true
  rm -rf "$work"
}
trap teardown EXIT
record() {
  if [[ "$2" == "$3" ]]; then echo "pass: $1"; else echo "FAIL: $1 (expected $2, got $3)"; failures=$((failures + 1)); fi
}
write_env() {
  sed -e "s/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=drill-$(openssl rand -hex 12)/" -e "s#^PUBLIC_BASE_URL=.*#PUBLIC_BASE_URL=https://localhost:$3#" \
    -e "s/^HTTPS_PORT=.*/HTTPS_PORT=$3/" -e "s/^HTTP_PORT=.*/HTTP_PORT=$4/" -e "s#_SUBNET=.*#_SUBNET=$5#" \
    -e "s/^EMAIL_MODE=.*/EMAIL_MODE=Pickup/" "$1" > "$2"
}
wait_healthy() {
  for ((second = 0; second < READY_TIMEOUT_SECONDS; second++)); do
    if "$1" ps api --format '{{.Status}}' 2>/dev/null | grep -q '(healthy)'; then return 0; fi
    sleep 1
  done
  echo "Timed out waiting for the API" >&2
  return 1
}
new_sql() { new exec -T postgres psql -U proteone -d proteone -XtAc "$1" | tr -d '[:space:]'; }

mkdir -p "$work/old" "$work/archives"
git archive "$OLD_REF" deploy tools | tar -x -C "$work/old"
cp deploy/certs/proteone.crt "$work/old/deploy/certs/tgysm.crt"
cp deploy/certs/proteone.key "$work/old/deploy/certs/tgysm.key"
write_env "$work/old/deploy/.env.example" "$work/old.env" "$OLD_PORT" "$OLD_HTTP_PORT" "172.29.3.0/24"
write_env deploy/.env.example "$work/new.env" "$NEW_PORT" "$NEW_HTTP_PORT" "172.29.4.0/24"

echo "== TGYSM stack ($OLD_TAG)"
old up -d >/dev/null 2>&1
wait_healthy old
temporary="$(old run --rm -T api setup-admin --email "$ADMIN_EMAIL" --first-name Upgrade --last-name Admin 2>/dev/null | sed -n "s/^${TEMP_PASSWORD_PREFIX}//p")"
# Stopped, so the activation link stays in the outbox, protected with the key ring of the old stack.
old stop worker >/dev/null 2>&1
python3 - "https://localhost:$OLD_PORT" "$ADMIN_EMAIL" "$temporary" <<'PY'
import sys
sys.path.insert(0, "tools/lib")
import proteone_drill as drill
admin = drill.Client(sys.argv[1])
admin.sign_in(sys.argv[2], sys.argv[3], drill.ADMIN_PASSWORD)
admin.json("POST", "/admin/users", {"firstName": "Waiting", "lastName": "Link", "email": "waiting@example.com", "role": "employee",
                                    "departmentId": None, "locationId": None, "delivery": "email_link"})
PY
archive_path="$(old exec -T backup bash /backup/backup.sh | sed -n 's/^Backup written: //p')"
old cp "backup:$archive_path" "$work/archives/" >/dev/null 2>&1
old down >/dev/null 2>&1

echo "== Proteone stack"
code=0
COMPOSE_PROJECT_NAME="$NEW_PROJECT" PROTEONE_ENV_FILE="$work/new.env" PROTEONE_VERSION=latest tools/restore.sh "$work/archives/$(basename "$archive_path")" || code=$?
record "a TGYSM archive restores with equal row counts and migrates" "0" "$code"
record "no former name in the email templates" "0" "$(new_sql "SELECT count(*) FROM email_templates WHERE subject ILIKE '%tgysm%' OR body ILIKE '%tgysm%'")"
record "the admin signs in with the same password" "200" "$(python3 - "https://localhost:$NEW_PORT" "$ADMIN_EMAIL" <<'PY'
import sys
sys.path.insert(0, "tools/lib")
import proteone_drill as drill
print(drill.Client(sys.argv[1]).request("POST", "/auth/login", {"email": sys.argv[2], "password": drill.ADMIN_PASSWORD})[0])
PY
)"
mail=""
for ((second = 0; second < MAIL_TIMEOUT_SECONDS; second++)); do
  mail="$(new exec -T api sh -c 'cat /data/files/email-pickup/*.eml 2>/dev/null' || true)"
  if [[ -n "$mail" ]]; then break; fi
  sleep 1
done
sent="$(new_sql "SELECT count(*) FROM email_deliveries WHERE status = 'sent' AND recipient_email = 'waiting@example.com'")"
record "the waiting account link is readable and its email is sent" "1" "$sent"
record "the sent email names Proteone" "From: Proteone" "$(grep -oE '^From: "?Proteone' <<<"$mail" | tr -d '"' | head -1)"

echo "== Rollback"
old up -d >/dev/null 2>&1
wait_healthy old && record "the untouched TGYSM stack starts again" "healthy" "healthy"

if (( failures > 0 )); then echo "$failures check(s) failed" >&2; exit 1; fi
echo "Upgrade drill passed"
