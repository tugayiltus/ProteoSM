#!/usr/bin/env bash
# Directory sign-in contract test (Phase 11 Task 10, ADR-0030): runs the real LDAP client and the real OpenID Connect
# handler against local fake servers from deploy/test-directory/compose.yaml.
#   1. NovellLdapDirectoryTests over LDAPS, then over StartTLS, inside the .NET SDK container (it trusts the test CA).
#   2. OidcContractTests on this machine against mock-oauth2-server, with local PostgreSQL (tools/with-db.sh).
# Starts Colima when it is not running; on every exit path the containers and volumes are removed, and Colima is
# stopped again when this script started it (a Colima the owner already had running is left as it was).
# A skipped test fails the run (--fail-skips on): a skip here means the settings never reached the test.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly COMPOSE_FILE=deploy/test-directory/compose.yaml
# Only the project that holds the contract classes: with the whole solution, the other test projects run zero
# tests under the class filter and Microsoft.Testing.Platform fails them with exit code 8.
readonly TEST_PROJECT=backend/tests/Tgysm.Api.IntegrationTests
readonly LDAP_TEST_CLASS=Tgysm.Api.IntegrationTests.Auth.NovellLdapDirectoryTests
readonly OIDC_TEST_CLASS=Tgysm.Api.IntegrationTests.Auth.OidcContractTests
readonly LDAP_SERVICE=openldap
readonly OIDC_SERVICE=oidc
readonly RUNNER_SERVICE=ldap-tests
readonly READY_ATTEMPTS=60
readonly READY_DELAY_SECONDS=2

# Fake test directory (deploy/test-directory/seed.ldif). The tests reach it by its service name inside compose.
readonly LDAP_HOST_IN_COMPOSE=openldap
readonly LDAPS_PORT=636
readonly STARTTLS_PORT=389
readonly LDAP_BASE_DN="dc=tgysm-test,dc=example"
readonly LDAP_BIND_DN="cn=svc-tgysm,ou=service-accounts,dc=tgysm-test,dc=example"
readonly LDAP_BIND_PASSWORD="Svc-test-pass-2026"
readonly LDAP_USER="deniz.test"
readonly LDAP_PASSWORD="Deniz-test-pass-2026"

# mock-oauth2-server on the host port published in compose.yaml; the issuer id doubles as the tenant id.
readonly OIDC_URL="http://127.0.0.1:8089"
readonly OIDC_TENANT="3c0f7a2e-5b1d-4e8a-9c6f-2d4b8e1a7f30"
readonly OIDC_METADATA_PATH="/.well-known/openid-configuration"

export TGYSM_TEST_OIDC_TENANT="$OIDC_TENANT"

colima_started_here=false

compose() {
  docker compose -f "$COMPOSE_FILE" "$@"
}

cleanup() {
  local status=$?
  trap - EXIT INT TERM
  echo "==> Removing the test directory containers"
  compose --profile runner down --volumes --remove-orphans >/dev/null 2>&1 || true
  if [[ "$colima_started_here" == true ]]; then
    echo "==> Stopping Colima"
    colima stop >/dev/null 2>&1 || true
  fi
  exit "$status"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

for tool in colima docker curl; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "error: $tool is required" >&2
    exit 1
  fi
done

if ! colima status >/dev/null 2>&1; then
  echo "==> Starting Colima"
  colima_started_here=true
  colima start
fi

echo "==> Starting OpenLDAP (test CA, seed) and the OIDC mock"
compose up --detach --build "$LDAP_SERVICE" "$OIDC_SERVICE"

wait_for() {
  local description=$1
  shift
  local attempt
  for ((attempt = 1; attempt <= READY_ATTEMPTS; attempt++)); do
    if "$@" >/dev/null 2>&1; then
      echo "    $description: ready"
      return 0
    fi
    sleep "$READY_DELAY_SECONDS"
  done
  echo "error: $description did not become ready" >&2
  compose logs "$LDAP_SERVICE" "$OIDC_SERVICE" >&2 || true
  return 1
}

ldap_healthy() {
  [[ "$(docker inspect --format '{{.State.Health.Status}}' "$(compose ps --quiet "$LDAP_SERVICE")")" == healthy ]]
}

wait_for "OpenLDAP (seed loaded, LDAPS and StartTLS bind)" ldap_healthy
wait_for "OIDC mock metadata" curl --silent --fail "$OIDC_URL/$OIDC_TENANT$OIDC_METADATA_PATH"

# The seed is loaded by the container on its first start (deploy/test-directory/openldap/entrypoint.sh); check
# both test accounts are there before any test runs.
echo "==> Checking the seed"
for account in "(sAMAccountName=$LDAP_USER)" "(mail=ece.deneme@tgysm-test.example)"; do
  compose exec -T "$LDAP_SERVICE" ldapsearch -x -LLL -H "ldaps://localhost:$LDAPS_PORT" -D "$LDAP_BIND_DN" \
    -w "$LDAP_BIND_PASSWORD" -b "$LDAP_BASE_DN" "$account" dn | grep -q '^dn:'
done

export TGYSM_TEST_LDAP_HOST="$LDAP_HOST_IN_COMPOSE"
export TGYSM_TEST_LDAP_BASE_DN="$LDAP_BASE_DN"
export TGYSM_TEST_LDAP_BIND_DN="$LDAP_BIND_DN"
export TGYSM_TEST_LDAP_BIND_PASSWORD="$LDAP_BIND_PASSWORD"
export TGYSM_TEST_LDAP_USER="$LDAP_USER"
export TGYSM_TEST_LDAP_PASSWORD="$LDAP_PASSWORD"

echo "==> LDAP contract over LDAPS (port $LDAPS_PORT)"
export TGYSM_TEST_LDAP_PORT="$LDAPS_PORT" TGYSM_TEST_LDAP_SECURITY=Ldaps
compose --profile runner run --rm "$RUNNER_SERVICE" "$LDAP_TEST_CLASS"

echo "==> LDAP contract over StartTLS (port $STARTTLS_PORT)"
export TGYSM_TEST_LDAP_PORT="$STARTTLS_PORT" TGYSM_TEST_LDAP_SECURITY=StartTls
compose --profile runner run --rm "$RUNNER_SERVICE" "$LDAP_TEST_CLASS"

echo "==> OIDC contract against the mock (local PostgreSQL)"
export TGYSM_TEST_OIDC_URL="$OIDC_URL"
tools/with-db.sh dotnet test --project "$TEST_PROJECT" --filter-class "$OIDC_TEST_CLASS" --fail-skips on

echo "==> Directory contract test passed"
