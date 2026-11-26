# Outgoing mail settings in the admin panel — design

Date: 2026-11-26. Owner: Tugay İLTUŞ. Branch: a new feature branch from `master`. Second module of the November 2026 queue (after the satisfaction rating, ADR-0044).

## Intent

The system sends its automatic emails (notifications, account links, rating links) from one no-reply sender. Today the sender lives only in `deploy/.env` (`EMAIL_FROM`, `SMTP_HOST`, `SMTP_PORT`, `SMTP_USE_STARTTLS`, `SMTP_USERNAME`, `SMTP_PASSWORD`, bound to `Email:*`). A change needs shell access to the host and a restart. The owner wants an admin to set and test the sender in `/admin`, like the login method (ADR-0030).

## Goals

- `/admin` page "E-posta gönderimi" (group "Sistem ve denetim"): sender name, sender address, provider, server, port, security, user name, password.
- Providers (owner decision): **Google** (preset `smtp.gmail.com`, port 587, STARTTLS; the user enters the account and an app password) and **Diğer SMTP** (every field by hand). Office 365 is not built now (owner decision); see "Later".
- "Test maili gönder" sends one message with the saved settings to the signed-in admin's own address and shows the exact failure text (connect, TLS, authentication, rejected sender).
- Saved panel settings take effect only after a successful test ("Kullanıma al"), the same rule as the login method: the test stores a fingerprint of the settings, any change breaks it (my call: a wrong save must never stop all mail).
- "`.env` ayarlarına dön" switches back to the environment values without deleting the saved panel settings.
- The Worker picks up a change at the next send; no restart.
- Audit: settings saved (never the password value), test run with result, activation, return to `.env`.

## Non-goals

- Inbound mail (module 5 of the queue).
- Several senders, per-team senders, reply-to per template.
- Office 365 / Microsoft Graph sending (later step).

## Anti-goals

- The password readable anywhere: API response, page, logs, audit, `last_error`.
- A saved but untested setting that silently breaks every email.
- Breaking an existing installation: with no panel settings the system behaves exactly as today.

## Rules

- Source of truth: `Email:Mode` from the environment stays. `Pickup` (development, E2E) always writes `.eml` files and ignores panel settings; the page says so. `Smtp` uses the active panel settings when they exist, else `Email:From` + `Email:Smtp:*` from the environment (today's behaviour).
- Security choices: STARTTLS (587), SSL/TLS (465), none. "None" is allowed only without a user name (an internal relay on port 25), so a password never travels in clear text (my call).
- The password is write-only: shown as "kayıtlı"; replaced only when a new value is typed; stored protected with Data Protection like the login secrets (shared application name, API and Worker read it).
- Sender address must be a valid mailbox; sender name optional; host at most 253 characters; port 1–65535. Named constants.
- The send timeout stays `Email:Smtp:TimeoutSeconds` from the environment (it is checked against the Worker's row lease at startup; a panel value could break that check).
- One row, `xmin` concurrency, writes serialized like `AuthSettings`.

## Data

New one-row table `email_settings` (or columns in a settings row, builder's choice following `AuthSettings`): provider, sender name, sender address, host, port, security, user name, protected password, tested fingerprint, active flag, `xmin`.

## Screens

```
E-posta gönderimi
Gönderim şu an: Panel ayarları (son test 26 Kas 14:05, başarılı)   [.env ayarlarına dön]

Sağlayıcı      (•) Google   ( ) Diğer SMTP
Gönderen adı   [Proteone                    ]
Gönderen adres [no-reply@firma.com          ]
Sunucu         [smtp.gmail.com] Port [587]  Güvenlik [STARTTLS v]
Kullanıcı      [no-reply@firma.com          ]
Şifre          [kayıtlı — değiştirmek için yazın]

[Kaydet]  [Test maili gönder]  [Kullanıma al]
```

## Approaches considered

- **A. Database row, read per send (chosen):** the pattern the login method already uses; works in Docker; no restart; tested before use.
- **B. Write the values back to `deploy/.env`:** the API container cannot write the host file safely, and the Worker would still need a restart. Rejected.
- **C. Reloadable `appsettings` file on a volume:** a second configuration source to back up and secure, secrets in a file. Rejected.

## Later (on the list, not built)

- Office 365 sending. The login method already holds an Entra app registration (ADR-0030). Graph `Mail.Send` (application permission, limited to the no-reply mailbox) on the same registration may avoid a second registration and a second secret. Evaluate when the owner asks for it.

## Constraints

- Handlers in `Proteone.Application/Features/Email/Settings/` (or next to `Features/Auth/Settings`), validators `internal sealed`; admin policy only.
- New error codes and audit actions get Turkish texts (`TranslationCoverageTests`); `tools/gen-api-types.sh` after the endpoints.
- `docs/operations.md` gets the operator note only when the owner asks for docs.
- An ADR records the decision (approach A, test-before-use, `.env` fallback, Pickup rule, no clear-text password).

## Left to the builder

- Exact table shape (own table or `AuthSettings`-like row), the fingerprint method (reuse the login one).
- How the Worker reads the settings per send without a database round trip on every message (short cache with version check is acceptable).
