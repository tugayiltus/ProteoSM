# Proteone

Proteone is a web-based IT service management tool for one company, made for small and medium teams: the parts of a large service desk suite that are used every day, without a workflow engine or training. Employees report faults and request services in plain language; IT staff work team queues; admins run everything from a separate `/admin` area. The UI is Turkish and mobile-first; code and URLs are English. One HTTPS site per company host.

| Area | What it covers |
|---|---|
| Support | Incidents, requests, approvals, SLA, email, attachments, knowledge base, central search |
| Change and problem | Tasks, changes with approval and a maintenance calendar, problems, releases |
| Assets and services | Inventory, assignments, QR labels, service catalogue with a light CMDB, software and licences |
| Insight | Reports with drill-down and Excel export |
| Integration | Microsoft Entra ID or Active Directory sign-in, signed monitoring webhook |
| Administration | Users, organization, catalogs, SLA, email templates, announcements, general settings, login method, audit |

Requirements: [`proteone.md`](proteone.md). Open work: [`docs/status.md`](docs/status.md).

## Architecture

| Layer | Project / folder | Responsibility |
|---|---|---|
| Domain | `backend/src/Proteone.Domain` | Entities, business rules, state transitions; no framework dependency |
| Application | `backend/src/Proteone.Application` | Use-case handlers, validation, ports, audit recording |
| Infrastructure | `backend/src/Proteone.Infrastructure` | EF Core + Npgsql, migrations, ticket number counter, password hashing, local file store (attachments and export files), outbox and export job claiming, email transports (MailKit SMTP, pickup folder), active panel SMTP sender with target policy and password protector (ADR-0045), account-link protector, Excel writer (ClosedXML), Active Directory client over LDAP (ADR-0030) |
| Worker | `backend/src/Proteone.Worker` | Generic Host running `OutboxDispatcher`, `EmailSender`, `SlaMonitor`, `ExportRunner` (background Excel exports) and `Cleanup` jobs on timers, row leases (`FOR UPDATE SKIP LOCKED`), heartbeats in `worker_heartbeats` (ADR-0014); references Application and Infrastructure, never Api |
| API | `backend/src/Proteone.Api` | Minimal API endpoints under `/api/v1`, cookie auth, Microsoft Entra ID sign-in (OpenID Connect, ADR-0030), antiforgery, ProblemDetails, serves the built SPA from `wwwroot`; CLI commands `setup-admin`, `reset-password`, `migrate` (migrations + post-migration data steps; `--print-latest`) and `seed-pilot` (`backend/src/Proteone.Api/Cli/`) |
| Web | `frontend/` | React application with two entry pages (users and `/admin`, ADR-0038) on the Astryx design system (left side navigation, breadcrumbs, light/dark/auto color modes; ADR-0027): tickets, tasks, changes, problems, IT workspace, inventory with QR labels, approvals, knowledge base, services with their impact view, reports and `/admin` (including the device catalog, the service catalogue and the login method) |
| Deployment | `Dockerfile`, `deploy/` | Images `proteone-api` and `proteone-worker`; `deploy/compose.yaml` with `postgres`, one-shot `migrate`, `api`, `worker`, `nginx` (TLS), `backup` and the `restore` profile; `deploy/nginx/`, `deploy/backup/` (ADR-0024, ADR-0025) |
| CI | `.github/` | GitHub Actions workflow `ci.yml` (lint, tests, E2E, both image builds on every push and pull request); Dependabot `dependabot.yml` |

Solution: `backend/Proteone.slnx`. Device catalog and generated asset tags: ADR-0023, ADR-0029. Login method: ADR-0030. Tasks and the combined work list: ADR-0031. Change management and change approval: ADR-0031, ADR-0032. Ticket-change links and central search (`GET /api/v1/search`): ADR-0033. Problem management: ADR-0034. Service catalogue and service-asset links: ADR-0036. Knowledge base: ADR-0019. Reports and exports: ADR-0020 — small exports stream from the API; larger ones become `export_jobs` rows that the Worker's `ExportRunner` builds into the file store and the API serves for download. Notification flow: handlers write `notification_outbox` events in the same transaction as the change; the Worker dispatches them into `email_deliveries` and sends them (ADR-0015, ADR-0016). Browser and API are same-origin; Vite proxies `/api` in development (ADR-0003). In production nginx terminates TLS and proxies to the API, which trusts forwarded headers only from `Http:KnownProxyNetworks` (ADR-0024).

## Stack

- Backend: .NET 11 (SDK pinned in `global.json`, ADR-0035), ASP.NET Core Minimal APIs, EF Core 11 + Npgsql 11, FluentValidation 12; `TreatWarningsAsErrors=true`, `Nullable=enable`.
- Database: PostgreSQL 17 with `pg_trgm` (ADR-0002).
- Directory login: Microsoft Entra ID through ASP.NET Core OpenID Connect, Active Directory through `Novell.Directory.Ldap.NETStandard` (LDAPS or StartTLS, managed code, no native library in the Linux image); settings and Data Protection–encrypted secrets in `auth_settings` (ADR-0030).
- Email: MailKit 5.0 SMTP in production, pickup folder of `.eml` files in development and tests; selected by `Email:Mode`. Other keys: `App:PublicBaseUrl` (absolute URL for links in email), `App:TimeZone`, `Email:Smtp:TimeoutSeconds`, `Worker:Jobs:<Job>:*`. See `capabilities.md` (Configuration). The SMTP password comes from user-secrets or the environment only. An admin can instead set, test and activate an SMTP sender (Google or other SMTP) in `/admin/email-sending`; while it is active, `Smtp` mode sends with it and its password is stored with Data Protection (ADR-0045).
- Frontend: React 19, TypeScript ~6.0 (strict), built on Node 26 LTS (ADR-0035), Vite 8, Astryx 0.7 (`@astryxdesign/core` with the neutral theme `@astryxdesign/theme-neutral`, pre-built CSS, `AstryxProvider` in `src/shared/ui`; self-hosted Figtree font from `@fontsource/figtree`; Lucide icons through the `Icons` map; light/dark/auto color modes; ADR-0027 — it superseded CoreUI, ADR-0021 and ADR-0026; UI code follows `frontend/CLAUDE.md`), React Router, TanStack Query 5, react-hook-form + zod 4, i18next, `react-markdown` 10 for article bodies (ADR-0008, ADR-0019), Chart.js 4 through `react-chartjs-2` with Astryx data colors for report charts (ADR-0020, ADR-0022). Shared UI pieces: `src/shared/form` (react-hook-form bound fields), `src/shared/list` (`DataList`, `ListToolbar`, URL filter state), `src/shared/feedback` (dialogs, loading/empty/error states), `src/shared/page`, `src/shared/layout`, `src/shared/navigation`.
- Build chunks (`frontend/vite.config.ts`, Rolldown `codeSplitting`, warning limit 500 KB): `vendor-react`, `vendor-router`, `vendor-tanstack`, `vendor-astryx-theme`, `vendor-astryx-runtime`, `vendor-astryx-forms`, `vendor-astryx-data`, `vendor-astryx-overlays`, `vendor-astryx`, `vendor-icons`, `vendor-forms`, `vendor-i18n`, `vendor-markdown`, `vendor-charts`, `vendor-other`, `app-shared`, `feature-common`, `feature-admin`, `feature-assets`, `feature-kb`, `feature-reports`, `feature-tickets`, `feature-changes`, `feature-problems`, `feature-tasks`, `feature-search`, `feature-services`. The chart component loads lazily, so `vendor-charts` loads only with the first chart; admin pages and the home page are lazy routes.
- Excel export: ClosedXML 0.105 (ADR-0020).
- File store: `Files:RootPath` (API and Worker). Outside Development it is required (startup fails without it) and both hosts must point at the same absolute folder, because the Worker writes export files that the API serves; both log the resolved root at startup. Empty in Development means the user's local application data folder.
- Data Protection: key ring in `DataProtection:KeysPath`, shared by API and Worker; required outside Development (ADR-0024).
- Deployment: Docker Compose, `postgres:17`, `nginx:stable`, images on `mcr.microsoft.com/dotnet/aspnet:11.0` (frontend built on `node:26-bookworm-slim`) running as non-root `app`; settings from `deploy/.env` (template `deploy/.env.example`). Operator manual: `docs/operations.md`.
- CI: GitHub Actions (`.github/workflows/ci.yml`), Dependabot weekly for NuGet, npm, Docker and GitHub Actions (`.github/dependabot.yml`).
- Tests: xUnit v3 on Microsoft.Testing.Platform (`global.json`; run `dotnet test --solution backend/Proteone.slnx`), NetArchTest, coverlet.MTP, Vitest + React Testing Library (jsdom 31), Playwright; directory contract tests against OpenLDAP and `mock-oauth2-server` containers (`tools/directory-contract-test.sh`, ADR-0030).

## Commands

| Task | Command |
|---|---|
| Prepare local database | `tools/db-up.sh` |
| Apply migrations | `tools/migrate.sh` |
| Create first admin | `tools/setup-admin.sh --email <email> --first-name <name> --last-name <name>` |
| Issue a temporary password | `tools/reset-password.sh --email <email>` |
| Add a migration | `tools/add-migration.sh <Name>` |
| Run API + Worker + web in this terminal | `tools/run.sh` (API `http://localhost:5311`, web `http://proteone.localhost:5310`; Worker writes `.eml` files to `tmp/email-pickup`); refuses to start while the launchd dev services run |
| Keep API + Worker + web running in the background (macOS launchd user agents, start at login, restart on exit; logs in `tmp/logs/`) | `tools/dev-service.sh install\|uninstall\|start\|stop\|restart\|status\|logs` |
| Lint (`dotnet format`, ESLint, TypeScript, Astryx doctor, doc paths) | `tools/lint.sh` |
| Tests (+ coverage gates) | `tools/test.sh` |
| E2E (stop `tools/run.sh` or the dev services first; it uses the same ports; also starts the Worker, with `Email:Mode=Pickup` into a per-run temp folder and 2 s job intervals, E2E only) | `tools/e2e.sh` |
| Browser QA sweep: every screen in light/dark on phone and desktop, on a fresh E2E database; contact sheets and `problems.txt` (overflow, console errors) in `tmp/ui-qa/`; stops and restarts the dev services | `tools/ui-qa.sh` |
| Regenerate API types | `tools/gen-api-types.sh` |
| Build | `tools/build.sh` |
| Lint the GitHub Actions workflows (`actionlint`) | `tools/ci-lint.sh` |
| Check that every `tools/`… and `deploy/`… path named in the docs exists | `tools/check-doc-paths.sh [file ...]` |
| Pilot seed data into a local `proteone_pilot` database (`--fresh` drops and recreates it, `--drop` removes it) | `tools/seed-pilot.sh [--fresh\|--drop] [--scale <factor>]` |
| Performance measurement at pilot volume (writes `docs/release/performance.md`) | `PERF_PASSWORD=<pilot password> tools/perf-measure.sh` |
| User-guide screenshots from the pilot database | `GUIDE_PASSWORD=<pilot password> tools/guide-screenshots.sh [id ...]` |
| User guide `combined.md` + A4 `combined.pdf` | `tools/build-user-guide.sh` |
| Start local PostgreSQL for one command | `tools/with-db.sh <command>` |
| Plan task status from the SDD ledger, branch, recent commits, uncommitted changes | `tools/progress.sh [plan-file]` |
| Re-run `tools/progress.sh` on an interval (default 300 s) | `tools/progress-watch.sh [interval-seconds] [plan-file]` |
| Directory sign-in contract test: real LDAP client and OpenID Connect handler against local OpenLDAP and mock OIDC containers (`deploy/test-directory/compose.yaml`; starts and stops Colima as needed; needs Docker, not in CI; last results recorded in `docs/release/directory-contract-test.md`) | `tools/directory-contract-test.sh` |

The launchd services run `tools/dev-api.sh`, `tools/dev-worker.sh` and `tools/dev-web.sh`; API and Worker go through `tools/dev-dotnet-host.sh`, which builds into `tmp/dev-artifacts/` and starts the last good build when the current build fails. Stop the services (`tools/dev-service.sh stop`) before `tools/e2e.sh`, which needs the same ports, and restart them afterwards.

### Deployment (Docker)

Full procedure, backups, restore, rollback and key handling: [`docs/operations.md`](docs/operations.md).

| Task | Command |
|---|---|
| Build images `proteone-api`, `proteone-worker` (tags: Git short hash, `latest`) | `tools/docker-build.sh` |
| Deploy: gates, images, `compose up`, wait for health through nginx | `tools/deploy.sh [--skip-gates]` |
| Any compose command on the stack (`deploy/compose.yaml` + `deploy/.env`) | `tools/compose.sh <args>` |
| Self-signed test certificate in `deploy/certs/` | `tools/gen-self-signed-cert.sh [host-name]` |
| Backup now | `tools/backup.sh` |
| Restore an archive | `tools/restore.sh <archive.tar.gz> [--force]` |
| Restore drill on throw-away stacks (writes `docs/release/restore-drill.md`) | `tools/restore-drill.sh` |

On macOS, Docker runs in Colima; Colima shares only the home folder with containers.

Requires a running local PostgreSQL 17 (`brew services start postgresql@17`). Local ports use the block 5310–5313 so they do not collide with other local projects: web 5310 (`http://proteone.localhost:5310`; macOS resolves `*.localhost` to loopback, no hosts entry), API 5311, E2E and guide preview 5312, guide and performance API 5313. Development in Chrome or Firefox (Secure cookies on `*.localhost`).

## Quality gates

- C#: zero warnings (`TreatWarningsAsErrors`), `dotnet format --verify-no-changes`.
- TypeScript/React: ESLint `--max-warnings=0`, no `eslint-disable`, `tsc -b` clean; `npx astryx doctor` has no failed check and at most 16 purity findings (the budget in `tools/lint.sh` covers known, reasoned exceptions).
- Tests green; Domain + Application and frontend line coverage ≥ 80%.
- `TranslationCoverageTests` green (every error code and audit action has Turkish text).
- CI (`.github/workflows/ci.yml`) runs `tools/lint.sh`, `tools/test.sh`, `tools/e2e.sh` and both image builds on every push and pull request; a dependency bump merges only with a green run.

## Documentation

- [`capabilities.md`](capabilities.md) — features and tech stack
- [`SECURITY.md`](SECURITY.md) — security policy
- [`docs/adr/`](docs/adr/README.md) — architecture decisions
- [`docs/operations.md`](docs/operations.md) — operator manual (install, update, rollback, backup, restore, keys)
- [`docs/release/`](docs/release/acceptance.md) — acceptance evidence, `performance.md`, `restore-drill.md`, `directory-contract-test.md`
- [`docs/user-guides/`](docs/user-guides/README.md) — end-user guide (Turkish, `combined.pdf`)
- [`CLAUDE.md`](CLAUDE.md) — agent working notes
- [`proteone.md`](proteone.md) — product analysis and roadmap
