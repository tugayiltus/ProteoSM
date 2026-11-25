# Directory contract test

Run by `tools/directory-contract-test.sh` on 2026-11-22 at commit `32987b6` (the run used that tree before it was
committed). The real LDAP client and the real OpenID Connect handler ran against local fake servers from
`deploy/test-directory/compose.yaml`; fake data only. ADR-0030; Phase 11 Task 10.

## Containers

| Service | Image | Role |
|---|---|---|
| `test-ca`, `openldap` | `proteone-test-openldap:local`, built from `alpine:3.20.3` (OpenLDAP slapd 2.6.8) | Active Directory stand-in: `user` class, `sAMAccountName`, `userPrincipalName`, `objectGUID` (`deploy/test-directory/openldap/ad-lite.schema`), throw-away CA, LDAPS 636 + StartTLS 389 |
| `oidc` | `ghcr.io/navikt/mock-oauth2-server:2.1.10` | Microsoft Entra ID stand-in: code flow with PKCE, `form_post`, RS256 tokens, claims per client id |
| `postgres` | `postgres:17.6-alpine` | throw-away database for the API-level LDAP tests |
| `ldap-tests` | `mcr.microsoft.com/dotnet/sdk:10.0.401` | runs the LDAP classes on Linux with the test CA trusted |

The OIDC class ran on the host (.NET SDK 10.0.401, local PostgreSQL 17 through `tools/with-db.sh`).

## Results

Every run used `--fail-skips on`: a skipped test counts as failed.

| Run | Classes | Passed | Failed | Skipped |
|---|---|---|---|---|
| OIDC (host) | `OidcContractTests` | 7 | 0 | 0 |
| LDAPS, port 636 | `NovellLdapDirectoryTests` (4), `LdapApiContractTests` (5) | 9 | 0 | 0 |
| StartTLS, port 389 | `NovellLdapDirectoryTests` (4), `LdapApiContractTests` (5) | 9 | 0 | 0 |
| OpenLDAP container stopped | `LdapOutageContractTests` | 1 | 0 | 0 |

What the classes check:

- `NovellLdapDirectoryTests`: service account reaches the base DN; user found by name, only the right password binds;
  filter injection finds no one; a never-answering directory is unavailable within the time limit.
- `LdapApiContractTests`, through the API: `POST /api/v1/admin/auth-settings/test` with `method=ldap` passes and the
  switch to LDAP follows; sign-in by Windows user name and by email reaches the same user (with `ldap_object_guid` set);
  a wrong password gives 401; 5 wrong passwords lock the name (the right password then gives 401); the local admin
  still signs in with a password (break-glass).
- `LdapOutageContractTests`: with the OpenLDAP container stopped, `POST /api/v1/auth/login/ldap` gives 503
  `auth.directory_unavailable`.
- `OidcContractTests`: full code flow with PKCE through `GET /api/v1/auth/oidc/start` and the callback ends with a
  Proteone session (`/auth/me` 200); another tenant, a disallowed email domain, a bad signature and a wrong issuer are each
  refused at `/login?error=sso` with no session and no user created, and each test asserts its own cause (Proteone's
  tenant or domain check on a validly signed token, or the handler's signature or issuer exception);
  `POST /api/v1/admin/auth-settings/test` with `method=entra` passes against the mock; `EntraConnectionTester` fails
  cleanly (`auth.directory_unavailable`) against a wrong authority.
- A mutation check during the run (tenant test pointed at the member client; signature test given the issuer fault)
  turned both tests red, so the cause assertions can fail.

## Open gaps

- Real directories are still needed before go-live: a real Microsoft Entra tenant and a real Active Directory
  (`docs/status.md`). The fakes prove Proteone's side of the contract, not Microsoft's.
- Mock fidelity (OIDC):
  - `tid`, `oid` and `preferred_username` come from mock configuration, so the tenant and domain refusals prove
    `EntraIdentityMapper` refuses a validly signed token with those claims, not that Entra issues them that way.
  - The bad-signature and wrong-issuer cases change what the API expects (a foreign key under the same key id, another
    issuer on the same server) instead of receiving a forged token; the handler's rejection is the same.
  - The mock speaks plain HTTP on `127.0.0.1`; production metadata is always HTTPS (`RequireHttpsMetadata` unchanged).
  - The mock serves metadata for any tenant, so "unknown tenant gives `auth.entra_tenant_not_found`" cannot be shown
    with it; only "wrong authority gives `auth.directory_unavailable`".
  - Guest detection (`idp`, `acct`, `#EXT#`) is not exercised against the mock; it stays covered by unit tests.
- Directory fidelity (LDAP): OpenLDAP with a small AD-shaped schema, not Active Directory. Referrals, nested groups,
  account-disabled flags and AD password policy are not exercised.
- The run needs Docker (Colima) and is not part of CI; adding it to CI is an owner decision.
