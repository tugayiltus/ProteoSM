# ADR-0045: Outgoing Mail Settings in the Admin Area

**Date**: 2026-11-27
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The system sends its automatic emails (notifications, account links, rating links) from one no-reply sender. Until now the sender lived only in `deploy/.env` (`EMAIL_FROM`, `SMTP_HOST`, `SMTP_PORT`, `SMTP_USE_STARTTLS`, `SMTP_USERNAME`, `SMTP_PASSWORD`, bound to `Email:*`; template `deploy/.env.example`). A change needed shell access to the host and a restart of the API and Worker. The owner wants an admin to set and test the sender in `/admin`, like the login method (ADR-0030). The anti-goals are strict: the password must never be readable (API, page, logs, audit, `last_error`), a saved but untested setting must never stop all mail, and an installation without panel settings must behave exactly as before. Office 365 sending is out of scope for now (owner decision). Design: `docs/superpowers/specs/2026-11-26-outgoing-mail-settings-design.md`.

## Decision

Approach A of the design: a one-row `email_settings` table that the Worker reads per send.

- **Page and providers.** `/admin/email-sending` ("E-posta gönderimi", menu group "Sistem ve denetim", admin policy only, ADR-0038). Providers: `Google` (preset `smtp.gmail.com`, port 587, STARTTLS; account and app password, whitespace removed from the app password) and `Smtp` ("Diğer SMTP", every field by hand). Security: STARTTLS (587), SSL/TLS (465), none (25). "None" is allowed only without a user name, so a password never travels in clear text.
- **Test before use.** "Test maili gönder" sends one fixed message (`EmailTestMessageText`) with the saved settings to the signed-in admin's own address. A passing test stores a fingerprint of the settings (SHA-256 over every field incl. the protected password; the login-method helper, moved into a shared class). "Kullanıma al" works only while the stored fingerprint matches the current settings; any change breaks it.
- **Saving active settings.** A save while the panel settings are active runs the test send first. A failing test refuses the save (409 with the test's error code); the working settings stay, so a wrong save never stops mail (the ADR-0030 rule).
- **A failed test never switches the sender.** On active settings a failed test changes nothing: it does not deactivate them and does not make the Worker fall back to `.env` (a transient outage must not switch the sender silently). On settings that are not active a failed test clears the tested fingerprint, so activation needs the latest test to pass. ".env ayarlarına dön" sets the row inactive, keeps every panel value, and clears the fingerprint when the latest test failed.
- **`.env` fallback and Pickup.** `Email:Mode` from the environment stays the source of truth. `Smtp` uses the panel settings while the row is active, else `Email:From` + `Email:Smtp:*` exactly as before (environment `UseStartTls=false` stays `SecureSocketOptions.Auto`). `Pickup` (development, E2E) always writes `.eml` files and ignores panel and SMTP values; the page shows a notice. A Pickup test send writes the test mail as `.eml` with the panel sender and counts as passed.
- **Password.** Write-only: the API returns only `hasPassword`; an empty value keeps the stored one. It is stored through Data Protection (`EmailSecretProtector.Purpose = "Proteone.EmailSettings"`, shared key ring, API and Worker read it). When an endpoint or login field (provider, host, port, security, user name) changes and a password is stored, the save requires the password again (`email_settings.password_missing`), so a stored password is never sent to a new server.
- **Failure text and redaction.** A test result names the failing step with an error code (connect, TLS, authentication, sender, recipient, timeout, other) and a Turkish text, plus the server's message sanitized by `ErrorSanitizer` with the password replaced by `[redacted]`, also in its base64 `AUTH PLAIN` and `AUTH LOGIN` forms. The same redaction applies to the Worker's `last_error`. The audit stores the result flag and the code only.
- **Target policy.** `SmtpTargetPolicy` checks the panel host after DNS resolution, for the test send and the Worker: it refuses link-local and unspecified addresses, known cloud metadata addresses (`fd00:ec2::/32`, `100.100.100.200`, `168.63.129.16`), the translation prefixes (`64:ff9b::/96`, `64:ff9b:1::/48`, `2002::/16`, `2001::/32`) and scoped IPv6 that is not link-local, and unwraps IPv4-mapped and IPv4-compatible forms before it checks them (`email_settings.host_not_allowed`). Private and loopback ranges stay allowed, because internal relays are a goal. The environment path keeps its connect without a target check: the operator sets that host, not an admin.
- **Worker reads.** `ActiveSmtpEndpointProvider` caches the row for 10 s (`CacheLifetime`); after that it reads only the `xmin` version and loads the row and unprotects the password again only when the version changed. A change takes effect at a later send without a restart. An unprotect failure fails the send with a clear, retryable error and is not cached.
- **Timeout and writes.** The send timeout stays `Email:Smtp:TimeoutSeconds` from the environment for the Worker and the test send (the Worker validates it against its row lease at startup). Writes use `xmin` concurrency through `SetExpectedVersion`, like `AuthSettings`.
- **Audit.** `email_settings.updated`, `email_settings.tested`, `email_settings.activated`, `email_settings.environment_restored`, never with the password value.

Commits: `476064f`, `898b07b`, `ca2ef2b`, `427b69c`, `22c7e28`, `a825d71`, `0035309`, `4e7e92f`, `4e52a4c`, `d6f315e`, `7e6c1be`, `9e3d940`, `e3feb31`, `caa3910`, `84d22b5`, `41dc877`, `6bdd471`, `2300c0d`, `b96df9f` (E2E test). Related: ADR-0014 and ADR-0015 (Worker and outbox), ADR-0030 (login method, the test-before-use pattern), ADR-0038 (admin entry).

## Alternatives Considered

### Alternative 1: Write the values back to `deploy/.env` (approach B)
- **Pros**: One configuration source; operators keep reading all settings in one file; no new table.
- **Cons**: The API container cannot write the host file safely; the API and Worker still need a restart to read new values; the password lands in clear text in a file; no place for a tested fingerprint.
- **Why not**: It keeps the restart the owner wants to remove and breaks the "no clear-text password" rule.

### Alternative 2: Reloadable `appsettings` file on a volume (approach C)
- **Pros**: .NET reloads the file without a restart; no database table.
- **Cons**: A second configuration source to back up, secure and share between the API and Worker containers; the secret sits in a file; concurrent admin edits need their own locking; test-before-use needs extra state outside the file.
- **Why not**: The database row already has backup, concurrency, audit and Data Protection; the login method uses the same pattern.

## Consequences

### Positive
- An admin sets, tests and switches the sender without shell access or a restart; the Worker picks the change up within about 10 s.
- A wrong setting cannot stop mail: activation and any save of active settings need a passing test, and a failed test on active settings never switches the sender.
- An installation without panel settings behaves exactly as before; ".env ayarlarına dön" is a one-click way back.
- The test result names the failing step and shows the server's sanitized text.

### Negative
- Two places can hold the sender (panel and `.env`); the operator must read the page to know which one sends. Pickup ignores the panel, so on a Pickup installation the page cannot prove the SMTP path.
- After a provider, host, port, security or user name change, the admin must type the password again.
- The Worker reads one version value per send after the cache expires, and settings changes reach it up to 10 s late.
- Office 365 is not supported; Microsoft 365 tenants that block SMTP AUTH must keep a relay or wait for a later step.

### Risks
- An active row can carry a stale tested mark when a test failed after activation; the status line shows the failed test date, but the sender keeps sending with those values until an admin acts.
- If the Data Protection key ring is lost or rotated, the stored password cannot be unprotected: every send fails (retryable) until the admin types the password again or returns to `.env`.
- Redaction covers the clear and base64 forms of the password; a server that echoes it in another encoding could still show it in a detail text.
- The target policy is a denylist after DNS resolution: an admin can still probe private and loopback hosts through the test send, and DNS can change between the check and the connect. The environment host is not checked (by ruling), so an operator who sets a metadata address is not stopped.
- Later: Office 365 sending through Microsoft Graph `Mail.Send` (application permission limited to the no-reply mailbox) may reuse the Entra app registration of the login method (ADR-0030) and avoid a second registration and secret; to evaluate when the owner asks.
