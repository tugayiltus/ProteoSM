#!/usr/bin/env bash
# Runs test classes inside the .NET SDK container of deploy/test-directory/compose.yaml (service ldap-tests).
# Why a container: NovellLdapDirectory leaves certificate checks to the platform, and on macOS the platform store is
# the Keychain, which cannot trust a CA without an interactive prompt. On Linux, update-ca-certificates trusts the
# throw-away test CA for this container only, the same way the production image would trust a company CA.
# The repository is mounted read-only and copied without bin/obj, so the host build output is never touched.
set -euo pipefail

readonly CA_SOURCE=/certs/ca.crt
readonly CA_TARGET=/usr/local/share/ca-certificates/tgysm-test-directory-ca.crt
readonly REPO=/repo
readonly WORK=/work
# Only the project with the contract class: other test projects would run zero tests and fail with exit code 8.
readonly TEST_PROJECT=backend/tests/Tgysm.Api.IntegrationTests

if [[ $# -eq 0 ]]; then
  echo "usage: ldap-tests-in-container.sh <dotnet test filter options, e.g. --filter-class Ns.Class>" >&2
  exit 2
fi

cp "$CA_SOURCE" "$CA_TARGET"
update-ca-certificates >/dev/null

mkdir -p "$WORK"
tar -C "$REPO" --exclude=bin --exclude=obj --exclude=TestResults -cf - global.json backend | tar -C "$WORK" -xf -
cd "$WORK"

# --fail-skips: a skipped test here means the directory settings did not reach the test, which is a failure.
dotnet test --project "$TEST_PROJECT" --fail-skips on "$@"
