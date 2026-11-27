#!/usr/bin/env bash
# Tests tools/lib/env-file.sh: values are read the way docker compose reads deploy/.env.
set -euo pipefail
cd "$(dirname "$0")/../.."
source tools/lib/env-file.sh

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
failures=0

expect() {
  local label="$1" key="$2" expected="$3" actual
  actual="$(env_value "$work/test.env" "$key")"
  if [[ "$actual" == "$expected" ]]; then echo "PASS $label"; else echo "FAIL $label (got '$actual', expected '$expected')"; failures=$((failures + 1)); fi
}

printf '%s\r\n' 'PLAIN=8443' 'DOUBLE="9443"' "SINGLE='7443'" '  SPACED =1' 'SPACES=  6443  ' 'COMMENT=5443 # host port' \
  'export EXPORTED=4443' 'TWICE=1' 'TWICE=2' 'EMPTY=' 'QUOTED_COMMENT="3443" # note' '# HIDDEN=1' > "$work/test.env"

expect "plain value with CRLF" PLAIN 8443
expect "double quotes" DOUBLE 9443
expect "single quotes" SINGLE 7443
expect "surrounding spaces" SPACES 6443
expect "inline comment" COMMENT 5443
expect "export prefix" EXPORTED 4443
expect "last assignment wins" TWICE 2
expect "empty value" EMPTY ""
expect "quoted value with comment" QUOTED_COMMENT 3443
expect "commented-out key is absent" HIDDEN ""
expect "missing key is absent" MISSING ""
if [[ -z "$(env_value "$work/none.env" PLAIN)" ]]; then echo "PASS missing file"; else echo "FAIL missing file"; failures=$((failures + 1)); fi

check() {
  if [[ "$2" == "$3" ]]; then echo "PASS $1"; else echo "FAIL $1 (got '$2', expected '$3')"; failures=$((failures + 1)); fi
}

# Keys from before the ProteoSM rename count while the new key is missing or empty.
printf '%s\n' 'PROTEONE_DEPLOYMENT=pilot' 'PROTEONE_SUBNET=10.1.0.0/24' 'PROTEOSM_SUBNET=10.2.0.0/24' 'PROTEOSM_EMPTY=' 'PROTEONE_EMPTY=old' > "$work/product.env"
check "former env-file key is read while the new one is missing" "$(product_env_value "$work/product.env" DEPLOYMENT)" pilot
check "new env-file key wins over the former one" "$(product_env_value "$work/product.env" SUBNET)" 10.2.0.0/24
check "former env-file key is read while the new one is empty" "$(product_env_value "$work/product.env" EMPTY)" old
check "missing env-file key in both names is empty" "$(product_env_value "$work/product.env" MISSING)" ""

# Shell variables from before the rename are exported under the new name; a set new name is never overwritten.
check "former variable is exported under the new name" \
  "$(env -u PROTEOSM_DB_NAME PROTEONE_DB_NAME=old_db bash -c 'source tools/lib/product-env.sh; printf %s "$PROTEOSM_DB_NAME"')" old_db
check "new variable wins over the former one" \
  "$(PROTEONE_DB_NAME=old_db PROTEOSM_DB_NAME=new_db bash -c 'source tools/lib/product-env.sh; printf %s "$PROTEOSM_DB_NAME"')" new_db
check "an empty new variable is kept (set on purpose)" \
  "$(PROTEONE_DB_NAME=old_db PROTEOSM_DB_NAME= bash -c 'source tools/lib/product-env.sh; printf %s "${PROTEOSM_DB_NAME-unset}"')" ""
check "the fallback reaches child processes" \
  "$(env -u PROTEOSM_VERSION PROTEONE_VERSION=abc123 bash -c 'source tools/lib/product-env.sh; bash -c "printf %s \"\$PROTEOSM_VERSION\""')" abc123
check "no former variable, no new one" \
  "$(env -u PROTEOSM_ENV_FILE -u PROTEONE_ENV_FILE bash -c 'source tools/lib/product-env.sh; printf %s "${PROTEOSM_ENV_FILE-unset}"')" unset

if [[ "$failures" -gt 0 ]]; then echo "$failures check(s) failed"; exit 1; fi
echo "All env file checks passed"
