# Outgoing Mail Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An admin sets, tests and switches on the no-reply sender in `/admin` ("E-posta gönderimi"); the Worker uses the active panel settings at the next send, and falls back to the `.env` values when none are active.

**Architecture:** New one-row Domain entity `EmailSettings` (pattern of `AuthSettings`, ADR-0030: protected secret, tested fingerprint, `xmin`). Application use cases in `Features/Email/Settings/` behind `/api/v1/admin/email-settings`. Infrastructure gets one MailKit routine `SmtpMailClient` that both the Worker sender and the test send use; the Worker reads the active row through a singleton `ActiveSmtpEndpointProvider` (10 s cache, version check). `Email:Mode=Pickup` keeps writing `.eml` files and ignores the panel.

**Tech Stack:** .NET 11, EF Core + Npgsql, FluentValidation, MailKit/MimeKit, ASP.NET Core Data Protection, xUnit (Microsoft.Testing.Platform); React 19, TanStack Query, react-hook-form + zod, Astryx, Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-11-26-outgoing-mail-settings-design.md`

## Global Constraints

- Providers: `EmailProvider.Google` (preset `smtp.gmail.com`, port `587`, `STARTTLS`; the user enters the account and an app password) and `EmailProvider.Smtp` ("Diğer SMTP", every field by hand). No Office 365.
- Security: `EmailSecurity.StartTls` (default port 587), `SslTls` (465), `None` (25). `None` only without a user name, so a password never travels in clear text.
- Sender address: valid mailbox (`UserRules.ValidEmail`, at most `User.EmailMaxLength` = 254). Sender name optional, at most `EmailSettings.SenderNameMaxLength = 100`. Host at most `EmailSettings.HostMaxLength = 253`. Port `EmailSettings.MinPort = 1` … `MaxPort = 65535`. User name at most 254. Password input at most `EmailSettings.PasswordMaxLength = 512`. Named constants only.
- Password is write-only: the API returns only `hasPassword`; a null or empty value keeps the stored one; stored through Data Protection (`EmailSecretProtector.Purpose = "Proteone.EmailSettings"`, shared key ring, API and Worker read it). Never in an API response, page, log, audit, `last_error` or test detail.
- The send timeout stays `Email:Smtp:TimeoutSeconds` (`SmtpOptions.EffectiveTimeout`) for the Worker and the test send; no panel field (the Worker validates it against its row lease at startup).
- `Email:Mode` stays the source of truth. `Pickup` always writes `.eml` files (Worker sends and the test send) and ignores panel and SMTP values; the page says so. Ruling: a Pickup test send writes the test mail as `.eml` with the panel sender and counts as passed (E2E runs in Pickup; the panel is ignored there anyway). `Smtp` uses the panel settings when `IsActive` (activation already required a passing test, and an active row's values change only through a save whose test passed), else `Email:From` + `Email:Smtp:*` exactly as today (environment `UseStartTls=false` stays `SecureSocketOptions.Auto`).
- Activation ("Kullanıma al") only after a passing test of the current settings: the test stores a fingerprint, any change breaks it. Fingerprint = the login one (SHA-256 over all fields incl. the protected password), moved into a shared helper.
- ".env ayarlarına dön" sets `IsActive = false` and keeps every panel value.
- Ruling (spec silent): saving settings **while they are active** runs the test send first; a failing test refuses the save (409 with the test's error code) and the working settings stay — the ADR-0030 rule, so a wrong save never stops mail.
- Ruling: a failed test clears the tested fingerprint only while the settings are not active (activation needs the latest test to pass). On active settings a failed test changes nothing: it never deactivates them and never makes the Worker fall back to `.env`.
- Ruling: "exact failure text" = an error code per stage (connect, TLS, authentication, sender, recipient, timeout, other) with a Turkish text, plus the server's message sanitized by `ErrorSanitizer` with the password value replaced by `[redacted]`. The audit stores `ok` and the code only.
- Error codes (`ErrorCodes`, Turkish text in `errors.json` under `email_settings`): `email_settings.not_tested` (409), `email_settings.plain_text_login` (400 field error), and test results `email_settings.not_configured`, `email_settings.password_missing`, `email_settings.connect_failed`, `email_settings.tls_failed`, `email_settings.auth_failed`, `email_settings.sender_rejected`, `email_settings.recipient_rejected`, `email_settings.timeout`, `email_settings.send_failed`.
- Audit actions (`AuditActions`, Turkish label in `admin.json` `auditActions.email_settings`): `email_settings.updated` "E-posta gönderim ayarları değiştirildi", `email_settings.tested` "E-posta gönderimi test edildi", `email_settings.activated` "Panel e-posta ayarları kullanıma alındı", `email_settings.environment_restored` "E-posta gönderimi .env ayarlarına döndü". Entity type `email_settings` "E-posta gönderim ayarları".
- Writes: one row, `xmin` concurrency through `db.SetExpectedVersion` (what `AuthSettings` does; it takes no `RunSerializedAsync` lock, so neither does this).
- Page texts (exact, from the spec sketch): title "E-posta gönderimi" (menu group "Sistem ve denetim"); "Gönderim şu an: Panel ayarları (son test {{date}}, başarılı)"; ".env ayarlarına dön"; "Sağlayıcı" with "Google" / "Diğer SMTP"; "Gönderen adı", "Gönderen adres", "Sunucu", "Port", "Güvenlik" with "STARTTLS" / "SSL/TLS" / "Yok"; "Kullanıcı"; "Şifre" with placeholder "kayıtlı — değiştirmek için yazın"; buttons "Kaydet", "Test maili gönder", "Kullanıma al". Turkish only in `locales/tr/*.json`.
- Test email content (generated content, like `KnownErrorArticleText`): constants in `EmailTestMessageText`: subject "Proteone test e-postası", body "Bu e-posta, yönetim panelindeki giden posta ayarlarını denemek için gönderildi. Yanıt vermeniz gerekmez."
- Admin policy only (the admin route group). English code and identifiers. No `eslint-disable`, no warnings.
- Per task: focused tests + `tools/lint.sh`. Full `tools/test.sh`, `tools/e2e.sh`, `tools/ui-qa.sh` once at the end. `docs/operations.md` only when the owner asks.
- Owner: Tugay İLTUŞ. No AI attribution anywhere.

## Review Focus

1. An admin edits the active settings and types a wrong password: the save is refused with `email_settings.auth_failed`, the old settings keep sending, nothing is half-saved. Test in Task 3.
2. A test send is still running when another admin saves new values: the test answer is a 409 conflict and the new values are not marked tested. Test in Task 3 (`FakeEmailTestSender.BeforeSend`).
3. The SMTP server echoes the password in its error text (some relays print the AUTH line): the test detail, the audit row and the Worker's `last_error` show `[redacted]`. Tests in Task 2 (sanitizer path) and Task 3 (handler redaction).
4. The key ring cannot unprotect the stored password (keys lost or rotated): each Worker send fails with a clear, retryable error that contains no protected text, the provider does not cache the failure, and the admin page still loads. Test in Task 4.
5. A Google app password pasted as Google shows it, `abcd efgh ijkl mnop`: whitespace is removed before it is protected (app passwords never contain spaces). Test in Task 1.

---

### Task 1: Domain entity `EmailSettings` and persistence

**Files:**
- Create: `backend/src/Proteone.Domain/Email/EmailSettings.cs`, `EmailProvider.cs`, `EmailSecurity.cs`
- Create: `backend/src/Proteone.Domain/Common/SettingsFingerprint.cs`; modify `Proteone.Domain/Auth/AuthSettings.cs` (`Fingerprint` uses it, output unchanged)
- Modify: `backend/src/Proteone.Domain/Common/ErrorCodes.cs` (all `email_settings.*` codes above), `frontend/src/shared/i18n/locales/tr/errors.json` (Turkish texts, my call, e.g. `auth_failed`: "Sunucu kullanıcı adını veya şifreyi kabul etmedi. Google için uygulama şifresi kullanın.")
- Create: `backend/src/Proteone.Infrastructure/Persistence/Configurations/EmailSettingsConfiguration.cs`; modify `IAppDbContext` (`DbSet<EmailSettings> EmailSettings`, `void SetExpectedVersion(EmailSettings settings, uint expectedVersion)`), `AppDbContext`
- Create: migration via `tools/add-migration.sh AddEmailSettings` (table + seed row by SQL, like `20261112141814_AuthSettings.cs`)
- Modify: `backend/tests/Proteone.Api.IntegrationTests/Infrastructure/PostgresDatabase.cs` (`TruncateAllSql` puts the row back to its migration default)
- Test: `backend/tests/Proteone.Domain.Tests/Email/EmailSettingsTests.cs`, `backend/tests/Proteone.Api.IntegrationTests/Persistence/EmailSettingsPersistenceTests.cs`

**Interfaces:**
- Produces: `enum EmailProvider { Google, Smtp }`, `enum EmailSecurity { StartTls, SslTls, None }` (JSON `google`/`smtp`, `start_tls`/`ssl_tls`/`none`).
- Produces: `EmailSettings.SingletonId = new("00000000-0000-7000-8000-00000000e3a1")`; constants above; `GoogleHost = "smtp.gmail.com"`, `GooglePort = 587`, `GoogleSecurity = EmailSecurity.StartTls`; `static int DefaultPort(EmailSecurity security)` (587/465/25); `LastTestErrorCodeMaxLength = 100`; `TestDetailMaxLength = 500`.
- Produces properties: `Id, Version (uint, xmin), Provider, SenderName (string?), SenderAddress, Host, Port, Security, UserName (string?), PasswordProtected (string?), TestedFingerprint (string?), IsActive, LastTestedAt (DateTimeOffset?), LastTestSucceeded (bool?), LastTestErrorCode (string?), UpdatedAt`; `bool IsConfigured` (sender address and host not empty); `bool IsTested` (fingerprint matches).
- Produces methods: `static EmailSettings CreateDefault(DateTimeOffset now)` (Google preset, inactive); `void Update(EmailProvider provider, string? senderName, string senderAddress, string host, int port, EmailSecurity security, string? userName, string? protectedPassword, DateTimeOffset now)` — Google forces the preset host/port/security; trims; blank name or user name → null; null `protectedPassword` keeps the stored one; a null user name drops the stored password; `None` with a user name throws `DomainException(ErrorCodes.EmailSettingsPlainTextLogin)`; `string Fingerprint()`; `void RecordTest(bool succeeded, string? errorCode, DateTimeOffset now)` (a failure on active settings keeps the fingerprint and `IsActive`; test `Failed_test_on_active_settings_keeps_them_active`); `void Activate(DateTimeOffset now)` (throws `DomainException(ErrorCodes.EmailSettingsNotTested)` unless `IsTested`); `void UseEnvironment(DateTimeOffset now)`.
- Produces: `static string EmailSettings.NormalizePassword(EmailProvider provider, string password)` — Google: all whitespace removed; Smtp: unchanged (callers protect its result).
- Produces: `static string SettingsFingerprint.Compute(params string[] parts)` (SHA-256 hex over parts joined by `'\n'`).
- Produces: table `email_settings` (`provider`, `security` snake_case enum text; `sender_name` 100, `sender_address` 254, `host` 253, `user_name` 254, `password_protected text null`, `tested_fingerprint`, `is_active`, `last_tested_at`, `last_test_succeeded`, `last_test_error_code` 100, `updated_at`, `xmin`), CHECK `ck_email_settings_port` (`port BETWEEN 1 AND 65535`), seed row: `google`, `''`, `smtp.gmail.com`, `587`, `start_tls`, inactive.

- [ ] **Step 1: Write the failing domain tests** — `Google_update_forces_preset_host_port_and_security` (input `"x.local"`, 25, `None` → `smtp.gmail.com`, 587, `StartTls`), `Null_password_keeps_the_stored_one`, `Clearing_the_user_name_drops_the_stored_password`, `None_security_with_a_user_name_throws_plain_text_login`, `Each_field_change_breaks_the_tested_fingerprint` (theory over provider, name, address, host, port, security, user name, password), `Activate_untested_throws_not_tested`, `Passing_test_then_activate_sets_active`, `Failed_test_clears_fingerprint_but_keeps_active` (`LastTestSucceeded == false`, `LastTestErrorCode` stored, `IsActive` still true), `Use_environment_keeps_every_value`, `Google_password_loses_its_spaces` (`NormalizePassword(Google, "abcd efgh ijkl mnop") == "abcdefghijklmnop"`; Smtp keeps spaces). Existing `AuthSettings` tests stay green after the fingerprint move.
- [ ] **Step 2: Run** `dotnet test --project backend/tests/Proteone.Domain.Tests --filter-class "*EmailSettingsTests"` → FAIL (types missing).
- [ ] **Step 3: Implement** the enums, entity, `SettingsFingerprint`, `AuthSettings.Fingerprint` on top of it, error codes and Turkish error texts.
- [ ] **Step 4: Run** the same command and `--filter-class "*AuthSettings*"` → PASS.
- [ ] **Step 5: Write the failing persistence tests** `Migration_seeds_one_inactive_google_row` (`SingleAsync` → `Provider == Google`, `Host == "smtp.gmail.com"`, `IsActive == false`), `Port_check_rejects_zero` (raw SQL `UPDATE email_settings SET port = 0` → `PostgresException` with constraint `ck_email_settings_port`), `Stale_version_save_throws_concurrency` (two contexts, both `SetExpectedVersion`, second save → `ConcurrencyException`).
- [ ] **Step 6: Run** `dotnet test --project backend/tests/Proteone.Api.IntegrationTests --filter-class "*EmailSettingsPersistenceTests"` → FAIL; implement configuration, DbSet, `SetExpectedVersion` overload, migration, test reset SQL; run → PASS; `tools/migrate.sh` applies cleanly; `TranslationCoverageTests` → PASS.
- [ ] **Step 7:** `tools/lint.sh` → exit 0. **Commit** `feat(email-settings): one-row outgoing mail settings with tested fingerprint`.

### Task 2: SMTP transport, test sender and secret protector (Infrastructure)

**Files:**
- Create: `backend/src/Proteone.Application/Abstractions/IEmailTestSender.cs` (`SmtpTransport`, `EmailTestResult`, `IEmailTestSender`), `IEmailSecretProtector.cs`, `IEmailEnvironment.cs`
- Create: `backend/src/Proteone.Infrastructure/Security/EmailSecretProtector.cs`
- Create: `backend/src/Proteone.Infrastructure/Email/SmtpEndpoint.cs`, `SmtpMailClient.cs`, `SmtpFailureClassifier.cs`, `MailKitEmailTestSender.cs`, `EmailEnvironment.cs`
- Modify: `Email/SmtpEmailSender.cs` (sends through `SmtpMailClient` with the environment endpoint; behaviour unchanged), `Email/PickupFolderEmailSender.cs` (`internal Task WriteAsync(MailboxAddress from, EmailMessage message, CancellationToken)`), `Email/EmailMimeMessageFactory.cs` (`Build(MailboxAddress from, EmailMessage message)` overload), `DependencyInjection.cs`
- Create: `backend/tests/Proteone.Api.IntegrationTests/Infrastructure/ScriptedSmtpServer.cs`
- Test: `backend/tests/Proteone.Api.IntegrationTests/Notifications/MailKitEmailTestSenderTests.cs`; keep `SmtpEmailSenderTimeoutTests` green

**Interfaces:**
- Consumes: `EmailSecurity` (Task 1).
- Produces (Application): `public sealed record SmtpTransport(string? SenderName, string SenderAddress, string Host, int Port, EmailSecurity Security, string? UserName, string? Password)` with `ToString()` overridden to `"{Host}:{Port}"` (never prints the password); `public sealed record EmailTestResult(bool Ok, string? ErrorCode, string? Detail)`; `public interface IEmailTestSender { Task<EmailTestResult> SendTestAsync(SmtpTransport transport, EmailMessage message, CancellationToken cancellationToken); }`; `public interface IEmailSecretProtector { string Protect(string secret); string Unprotect(string protectedSecret); }`; `public sealed record EmailEnvironmentInfo(bool IsPickup, string From, string Host, int Port)`; `public interface IEmailEnvironment { EmailEnvironmentInfo Current { get; } }`.
- Produces (Infrastructure, internal): `sealed class SmtpEndpoint` (class, not record: no generated `ToString`) with `MailboxAddress From, string Host, int Port, SecureSocketOptions Socket, string? UserName, string? Password`; `static SmtpEndpoint FromEnvironment(EmailOptions options)` (`UseStartTls ? StartTls : Auto`, as today); `static SmtpEndpoint FromTransport(SmtpTransport transport)` (`StartTls → StartTls`, `SslTls → SslOnConnect`, `None → None`).
- Produces: `enum SmtpStage { Connect, Authenticate, Send }`; `sealed class SmtpStageException(SmtpStage stage, Exception inner) : Exception`; `static Task SmtpMailClient.SendAsync(SmtpEndpoint endpoint, EmailMessage message, TimeSpan timeout, CancellationToken cancellationToken)` — one deadline as today; own deadline → `TimeoutException`; each failure wrapped with its stage; authenticates when `UserName` is not empty.
- Produces: `static string SmtpFailureClassifier.Classify(SmtpStageException failure)` → codes: `TimeoutException` → `timeout`; Connect + `SocketException` → `connect_failed`; Connect + `SslHandshakeException` / `System.Security.Authentication.AuthenticationException` / STARTTLS not offered (`NotSupportedException`) → `tls_failed`; Authenticate (any, incl. `MailKit.Security.AuthenticationException`, "no AUTH offered") → `auth_failed`; Send + `SmtpCommandException` `SenderNotAccepted` → `sender_rejected`, `RecipientNotAccepted` → `recipient_rejected`; else `send_failed`. Mind the two `AuthenticationException` types.
- Produces: `MailKitEmailTestSender(IOptionsMonitor<EmailOptions> options) : IEmailTestSender` — Pickup mode: `PickupFolderEmailSender.WriteAsync` with the transport's sender, `Ok = true`; Smtp mode: `SmtpMailClient` with `EffectiveTimeout`; failure → `(false, code, ErrorSanitizer.Sanitize(inner, EmailSettings.TestDetailMaxLength = 500))`. Never throws for a send failure.
- DI: `IEmailSecretProtector` and `IEmailEnvironment` singletons, `IEmailTestSender` scoped.
- Test helper: `sealed class ScriptedSmtpServer : IAsyncDisposable` on `IPAddress.Loopback`, port 0; `static ScriptedSmtpServer Start(SmtpScript script)`; `record SmtpScript(bool Silent = false, bool OfferAuth = false, int AuthReply = 235, int MailFromReply = 250, int RcptReply = 250)`; `int Port`; `IReadOnlyList<string> Commands`. Plain text only (no TLS); no network beyond loopback.

- [ ] **Step 1: Write the failing tests** (mode Smtp, `TimeoutSeconds = 1`, `FixedOptionsMonitor` as in `SmtpEmailSenderTimeoutTests`):
  - `Happy_server_gets_the_panel_sender_and_recipient` (`Ok`, `Commands` contains `MAIL FROM:<no-reply@firma.com>` and `RCPT TO:<admin@firma.com>`).
  - `Closed_port_is_connect_failed` (listener started and stopped to get a free port).
  - `Ssl_tls_against_a_plain_server_is_tls_failed`; `Start_tls_not_offered_is_tls_failed`.
  - `Auth_535_is_auth_failed` (`OfferAuth`, `AuthReply = 535`); `No_auth_offered_with_a_user_name_is_auth_failed`.
  - `Mail_from_550_is_sender_rejected`; `Rcpt_550_is_recipient_rejected`.
  - `Silent_server_is_timeout_within_the_configured_seconds`.
  - `Detail_redacts_credential_shaped_text` (server replies `535 password=hunter2` → `Detail` contains `[redacted]`, not `hunter2`).
  - `Pickup_mode_writes_an_eml_with_the_panel_sender` (`.eml` in the folder, `From:` has the panel address, no TCP connection).
  - `Smtp_transport_to_string_omits_the_password`.
- [ ] **Step 2: Run** `dotnet test --project backend/tests/Proteone.Api.IntegrationTests --filter-class "*MailKitEmailTestSenderTests"` → FAIL.
- [ ] **Step 3: Implement** the ports, endpoint, client, classifier, test sender, protector, environment, DI; move `SmtpEmailSender` onto `SmtpMailClient` (its catch blocks and sanitizing stay).
- [ ] **Step 4: Run** the new tests and `--filter-class "*SmtpEmailSenderTimeoutTests"` and `"*EmailSenderTests"` → PASS.
- [ ] **Step 5:** `tools/lint.sh` → exit 0. **Commit** `feat(email-settings): shared SMTP client and a test sender that names the failing step`.

### Task 3: Admin use cases and endpoints

**Files:**
- Create: `backend/src/Proteone.Application/Features/Email/Settings/EmailSettingsDtos.cs`, `GetEmailSettings.cs`, `UpdateEmailSettings.cs` (command, `internal sealed` validator, handler), `TestEmailSettings.cs` (`EmailSettingsTester`, handler, `EmailTestMessageText`), `SwitchEmailSource.cs`
- Modify: `Proteone.Application/DependencyInjection.cs` (`services.AddScoped<Features.Email.Settings.EmailSettingsTester>()`)
- Modify: `Proteone.Domain/Audit/AuditActions.cs`, `AuditEntityTypes.cs` (`EmailSettings = "email_settings"`), `frontend/src/shared/i18n/locales/tr/admin.json` (audit and entity labels from Global Constraints)
- Create: `backend/src/Proteone.Api/Endpoints/Admin/EmailSettingsEndpoints.cs`; modify `Http/ApiRoutes.cs` (`EmailSettings = "/email-settings"`), `Endpoints/Admin/AdminEndpoints.cs` (`admin.MapEmailSettingsEndpoints()`)
- Create: `backend/tests/Proteone.Api.IntegrationTests/Infrastructure/FakeEmailTestSender.cs`; modify `ApiFactory.cs` (`public FakeEmailTestSender EmailTestSender { get; }`, registered scoped in `ConfigureTestServices`)
- Test: `backend/tests/Proteone.Api.IntegrationTests/Admin/EmailSettingsTests.cs`

**Interfaces:**
- Consumes: Task 1 entity, Task 2 ports.
- Produces: `public sealed record EmailSettingsDto(EmailProvider Provider, string? SenderName, string SenderAddress, string Host, int Port, EmailSecurity Security, string? UserName, bool HasPassword, bool IsTested, bool IsActive, DateTimeOffset? LastTestedAt, bool? LastTestSucceeded, string? LastTestErrorCode, bool IsPickup, string EnvironmentFrom, string EnvironmentHost, uint Version)`; `EmailSettingsMapping.ToDto(EmailSettings settings, EmailEnvironmentInfo environment)`.
- Produces: `UpdateEmailSettingsCommand(EmailProvider Provider, string? SenderName, string SenderAddress, string Host, int Port, EmailSecurity Security, string? UserName, string? Password, uint Version)`; `ActivateEmailSettingsCommand(uint Version)`; `UseEnvironmentEmailCommand(uint Version)`.
- Produces handlers: `GetEmailSettingsHandler.HandleAsync(ct) : Task<EmailSettingsDto>`; `UpdateEmailSettingsHandler.HandleAsync(UpdateEmailSettingsCommand, ct) : Task<EmailSettingsDto>`; `TestEmailSettingsHandler.HandleAsync(ct) : Task<EmailTestResult>`; `SwitchEmailSourceHandler.ActivateAsync(ActivateEmailSettingsCommand, ct)` and `UseEnvironmentAsync(UseEnvironmentEmailCommand, ct) : Task<EmailSettingsDto>`.
- Produces: `EmailSettingsTester.TestAsync(EmailSettings settings, CancellationToken) : Task<EmailTestResult>` — not configured → `(false, not_configured, null)`; user name without stored password → `password_missing`; recipient = the signed-in user's email (`ICurrentActor.UserId` → `db.Users`); message from `EmailTestMessageText`; replaces the plain password in `Detail` with `[redacted]`.
- Validator: provider/security `IsInEnum`; sender address `ValidEmail`; sender name ≤ 100; when `Smtp`: host not empty ≤ 253, port 1–65535; when `Google`: user name required and `ValidEmail`; user name ≤ 254; password ≤ 512; `Security == None` with a user name → error code `email_settings.plain_text_login` on `UserName`.
- Update flow: validate → load + `SetExpectedVersion` → `Update(..., Protect(NormalizePassword(...)) or null, now)` → audit `email_settings.updated` (`newValues`: every field but the password, `PasswordChanged`) → if `IsActive`: `tester.TestAsync`, fail → `throw new DomainException(result.ErrorCode, …)`, pass → `RecordTest(true, …)` → save → DTO from a no-tracking read.
- Test flow: load (tracked, no version) → tester → `RecordTest` → audit `email_settings.tested` (`newValues: { Ok, ErrorCode }`) → save (an `xmin` change since the load → 409).
- Endpoints (tag "Admin: Email sending"): `GET /api/v1/admin/email-settings`; `PUT /api/v1/admin/email-settings` (`UpdateEmailSettingsRequest`, 400/409); `POST /api/v1/admin/email-settings/test` (200 with `EmailTestResult`, 409); `POST /api/v1/admin/email-settings/activate` and `/use-environment` (`EmailSourceRequest(uint Version)`, 409). Audit `email_settings.activated` / `email_settings.environment_restored`.
- Test fake: `FakeEmailTestSender : IEmailTestSender` with `EmailTestResult Result` (default ok), `Func<Task>? BeforeSend`, `IReadOnlyList<(SmtpTransport Transport, EmailMessage Message)> Sent`, `Reset()`.

- [ ] **Step 1: Write the failing tests** (`[Collection(ApiCollection.Name)]`, reset factory + fake in `InitializeAsync`, admin via `ApiClient.LoginAsNewUserAsync`):
  - `Password_is_write_only_and_never_in_responses_or_audit` (PUT with `"gizli-şifre-123"` → GET body lacks it, `hasPassword == true`, row value differs, audit text lacks it).
  - `Google_save_stores_the_preset` (host `"x"`, port 25, `none` → GET `smtp.gmail.com`, 587, `start_tls`).
  - `Invalid_values_are_field_errors` (port 0 and 65536, host of 254 chars, sender `"no-reply"`, Google without user name → 400 with those fields); `None_security_with_a_user_name_is_plain_text_login`.
  - `Test_sends_to_the_signed_in_admin_with_the_saved_settings` (fake got recipient = admin email, transport password = plain value; GET `isTested`, `lastTestSucceeded`; audit `email_settings.tested` with `ok: true`).
  - `Failed_test_returns_code_and_redacted_detail_and_activation_is_409` (fake `(false, auth_failed, "535 gizli-şifre-123 rejected")` → body detail contains `[redacted]`, not the password; `activate` → 409 `email_settings.not_tested`).
  - `A_change_after_a_passing_test_needs_a_new_test` (test ok → PUT new port → `isTested == false` → activate 409).
  - `Activate_then_use_environment_keeps_the_values` (`isActive` true then false; host unchanged; both audit actions).
  - `Saving_active_settings_with_a_failing_test_keeps_the_old_ones` (Review Focus 1: active, fake fails, PUT new host → 409 `email_settings.auth_failed`; GET still old host, still active).
  - `Test_racing_a_save_is_409_and_does_not_mark_tested` (Review Focus 2: `BeforeSend` PUTs a new port through a second admin client → test response 409; GET `isTested == false`).
  - `Stale_version_is_409`; `Only_admins_reach_email_settings` (theory over the other roles → 403).
- [ ] **Step 2: Run** `dotnet test --project backend/tests/Proteone.Api.IntegrationTests --filter-class "*Admin.EmailSettingsTests"` → FAIL.
- [ ] **Step 3: Implement** handlers, tester, audit constants, endpoints, routes, fake, Turkish labels.
- [ ] **Step 4: Run** the tests → PASS; `TranslationCoverageTests`, `DependencyInjectionTests` → PASS.
- [ ] **Step 5:** `tools/gen-api-types.sh`; `tools/lint.sh` → exit 0.
- [ ] **Step 6: Commit** `feat(email-settings): admin endpoints to save, test and switch the sender`.

### Task 4: Worker sends with the active panel settings

**Files:**
- Create: `backend/src/Proteone.Infrastructure/Email/ActiveSmtpEndpointProvider.cs`
- Modify: `Email/SmtpEmailSender.cs` (constructor `(IOptionsMonitor<EmailOptions> options, ActiveSmtpEndpointProvider endpoints)`), `DependencyInjection.cs` (provider singleton)
- Modify: `backend/tests/Proteone.Api.IntegrationTests/Notifications/SmtpEmailSenderTimeoutTests.cs` (new constructor)
- Test: `backend/tests/Proteone.Api.IntegrationTests/Notifications/ActiveSmtpEndpointProviderTests.cs` (uses `FakeClockApiFactory` and `ScriptedSmtpServer`)

**Interfaces:**
- Consumes: Task 1 entity, Task 2 `SmtpEndpoint`, `SmtpMailClient`, `IEmailSecretProtector`.
- Produces: `internal sealed class ActiveSmtpEndpointProvider(IServiceScopeFactory scopes, IOptionsMonitor<EmailOptions> options, IEmailSecretProtector secrets, TimeProvider clock)`; `public const` lifetime `CacheLifetime = TimeSpan.FromSeconds(10)`; `ValueTask<SmtpEndpoint> GetAsync(CancellationToken)`.
- Cache rule: inside `CacheLifetime` no database query; after it, read only `Version`; reload the row and unprotect only when the version changed. Panel endpoint only when `IsActive` (a failed test on active settings never switches the sender; Global Constraints ruling), else `SmtpEndpoint.FromEnvironment(options.CurrentValue)` (read fresh on every call). An unprotect failure throws `InvalidOperationException("The stored SMTP password of the panel settings cannot be read.")` and is not cached. Thread-safe (one `SemaphoreSlim` for the refresh).
- `IEmailSender` selection by `Email:Mode` stays in `DependencyInjection.cs`; Pickup never resolves the provider.

- [ ] **Step 1: Write the failing tests:**
  - `No_active_row_uses_the_environment_values` (env host `"env.example"` → `Host == "env.example"`, `Socket == Auto` when `UseStartTls = false`).
  - `Activated_settings_are_used_after_the_cache_lifetime` (first call env; activate in the DB; call → still env; `factory.Clock.Advance(CacheLifetime)` → panel host, plain password).
  - `Use_environment_switches_back_after_the_cache_lifetime`.
  - `Active_row_changed_outside_the_rules_falls_back_to_the_environment` (raw SQL host change, so the fingerprint no longer matches).
  - `Unreadable_password_fails_each_send_without_caching` (Review Focus 4: raw SQL `password_protected = 'broken'` → `InvalidOperationException` whose message lacks `broken`; restore a valid value → next call after the lifetime succeeds; `GET /api/v1/admin/email-settings` → 200).
  - `Smtp_sender_uses_the_panel_sender_address` (active row pointing at `ScriptedSmtpServer` → `MAIL FROM:<panel address>`).
  - `Pickup_mode_ignores_active_panel_settings` (Mode Pickup, active row → `IEmailSender` resolves to `PickupFolderEmailSender`, `.eml` written, scripted server got no connection).
- [ ] **Step 2: Run** `dotnet test --project backend/tests/Proteone.Api.IntegrationTests --filter-class "*ActiveSmtpEndpointProviderTests"` → FAIL.
- [ ] **Step 3: Implement** the provider, the sender change, DI.
- [ ] **Step 4: Run** the new tests, `"*SmtpEmailSenderTimeoutTests"`, `"*EmailSenderTests"`, `"*WorkerServicesTests"` → PASS.
- [ ] **Step 5:** `tools/lint.sh` → exit 0. **Commit** `feat(email-settings): the worker sends with the active panel settings`.

### Task 5: Admin page "E-posta gönderimi" with its route and menu entry

**Files:**
- Create: `frontend/src/features/admin/email/EmailSendingPage.tsx`, `EmailSendingPage.test.tsx`, `sendingApi.ts`
- Modify: `frontend/src/shared/api/errors.ts` (`emailSettingsNotTested`, `emailSettingsPlainTextLogin`), `frontend/src/shared/i18n/locales/tr/admin.json` (`emailSending.*`)
- Modify: `frontend/src/shared/routes.ts` (`adminEmailSending: '/admin/email-sending'`), `src/shared/navigation/routeMeta.ts` (id in `ROUTE_IDS` after `adminLoginMethod`; `admin({ path, icon: 'envelopeOpen', titleKey: 'routes.adminEmailSending', parent: 'adminOverview', group: 'system', menu: { order: 16 } })` — between "Giriş yöntemi" 15 and monitoring 17), `src/shared/i18n/locales/tr/common.json` (`routes.adminEmailSending`: "E-posta gönderimi"), `src/app/adminRouter.tsx` (lazy route to `features/admin/email/EmailSendingPage`), and the route-meta/menu test if it lists admin routes

**Interfaces:**
- Consumes: Task 3 endpoints through the generated `schema.d.ts` (`EmailSettingsDto`, `UpdateEmailSettingsRequest`, `EmailTestResult`).
- Produces: `useEmailSettings()`, `useSaveEmailSettings()`, `useActivateEmailSettings()`, `useUseEnvironmentEmail()` (set the query data from the response), `useTestEmailSettings()` (invalidates the settings query). Query key `['admin', 'email-settings']`.
- Produces: `export function EmailSendingPage()` with `PageHeader icon={ROUTE_META.adminEmailSending.icon}`; route id `adminEmailSending`, path `/admin/email-sending`.

- [ ] **Step 1: Write the failing tests** (pattern of `LoginMethodPage.test.tsx`, `mockApi` + `renderRoute('/admin/email-sending')`):
  - `shows_panel_status_with_last_test_and_env_button` (`isActive`, `lastTestSucceeded` → "Gönderim şu an: Panel ayarları (son test …, başarılı)" with `formatDateTime`; ".env ayarlarına dön" visible).
  - `google_shows_preset_server_read_only` ("Sunucu" `smtp.gmail.com` disabled, "Port" 587, "Güvenlik" STARTTLS).
  - `other_smtp_security_change_suggests_its_port` ("SSL/TLS" → port 465; "Yok" → 25).
  - `password_is_empty_with_kayitli_placeholder_and_saves_without_it` (PUT body has no `password` when nothing is typed).
  - `test_button_is_disabled_while_the_form_has_unsaved_changes`.
  - `failed_test_shows_code_text_and_server_detail` (`{ ok: false, errorCode: 'email_settings.auth_failed', detail: '535 …' }` → Turkish text and the detail).
  - `kullanima_al_is_disabled_until_tested` and `kullanima_al_posts_activate_with_version`.
  - `env_button_confirms_then_posts_use_environment` (confirm dialog text my call: "Gönderim .env ayarlarına dönsün mü? Panel ayarları silinmez.").
  - `admin_menu_lists_e_posta_gonderimi_under_sistem_ve_denetim`.
  - `pickup_mode_shows_the_file_banner` (`isPickup` → banner, my call: "Bu kurulum e-postaları dosyaya yazar (Pickup). Panel ve .env SMTP ayarları kullanılmaz.").
- [ ] **Step 2: Run** `cd frontend && npx vitest run src/features/admin/email/EmailSendingPage.test.tsx` → FAIL.
- [ ] **Step 3: Implement** with react-hook-form + zod and the bound fields of `src/shared/form`; provider as `RadioList`/`RadioListItem` (see `features/admin/users/DeliveryChoice.tsx`); `UnsavedChangesGuard`; one `SectionCard`; primary action "Kaydet", the other two secondary. Look up props with `npx astryx component <Name> --dense`. Google hint (my call): "Google hesabında iki adımlı doğrulama açık olmalı; Şifre alanına uygulama şifresini yazın."
- [ ] **Step 4: Run** → PASS; `tools/lint.sh` → exit 0.
- [ ] **Step 5: Commit** `feat(email-settings): admin page to set, test and switch the sender`.

### Task 6: E2E, ui-qa, ADR-0045

**Files:**
- Create: `frontend/e2e/email-sending.spec.ts`
- Modify: `frontend/e2e/ui-qa.spec.ts` (add `'/admin/email-sending'` to the admin list); `tools/guide-screenshots.sh` only if it lists admin pages
- Create: `docs/adr/0045-outgoing-mail-settings.md`; modify `docs/adr/README.md`, `proteone.md` §2 (Office 365 sending out of scope; outgoing SMTP set and tested in `/admin`, ADR-0045) and §19 (row "Giden posta ayarları")

- [ ] **Step 1: Failing E2E** `admin_tests_and_activates_the_panel_sender_and_returns_to_env`: admin opens `/admin/email-sending`, sees the Pickup banner, chooses "Diğer SMTP", fills sender `no-reply@firma.com`, server `smtp.firma.local`, "Güvenlik" "Yok", empty "Kullanıcı", "Kaydet"; "Kullanıma al" is disabled; "Test maili gönder" → success message; `waitForEmail(adminEmail, 'giden posta ayarlarını denemek')` returns a mail with `From` `no-reply@firma.com`; "Kullanıma al" → status "Panel ayarları"; ".env ayarlarına dön" → confirm → status ".env ayarları", the sender field still `no-reply@firma.com`; `expectNoHorizontalScroll`. Menu group "Sistem ve denetim" lists "E-posta gönderimi".
- [ ] **Step 2: Run** `E2E_PROJECTS=mobile-360 tools/e2e.sh email-sending.spec.ts` → fix what fails (first run against the real API in Pickup mode), then both viewports → PASS.
- [ ] **Step 3:** ADR-0045 from `~/.claude/templates/adr-template.md` (status `accepted`, Deciders `Tugay İLTUŞ`, cite the commits): approach A (database row read per send, 10 s cache with version check), test before use by fingerprint, save of active settings tested first, `.env` fallback, Pickup rule, no clear-text password (write-only, Data Protection, redacted detail), timeout from the environment; alternatives B (write `deploy/.env`) and C (reloadable file on a volume) with why not; later: Office 365 through Graph `Mail.Send`. Index row; `proteone.md` lines; `tools/check-doc-paths.sh docs/adr/0045-outgoing-mail-settings.md proteone.md` → exit 0.
- [ ] **Step 4: Commit** `feat(email-settings): admin menu entry, E2E and ADR-0045`.
- [ ] **Step 5: Phase end:** `tools/test.sh`, `tools/e2e.sh`, `tools/ui-qa.sh` (zero problems), one whole-branch review; findings fixed; then the owner's merge question.

Docs that need a line after this lands (only on request): `capabilities.md` (admin email sending, endpoints, `email_settings`), `README.md` feature list, `SECURITY.md` (SMTP password in the database, protected), `docs/operations.md` (operator note: `.env` values are the fallback; Pickup ignores the panel), user guide admin page.
