# Proteone — ITSM web platform

Single-company ITSM: incident and request management, IT inventory and asset management, SLA, email, knowledge base and reports. UI: Astryx (ADR-0027), user application plus `/admin` (ADR-0038). Goal: ServiceNow-like for small and medium teams, without its complexity. Merges to `master` only with owner approval. Work queue: `docs/superpowers/plans/2026-11-25-proteone-next.md`. Status: `docs/status.md`. Requirements: `proteone.md`.

## Stack & Architecture
- **Backend:** .NET 11 modular monolith (ADR-0001, runtime ADR-0035) — `backend/src/Proteone.Domain` (rules, no framework deps) → `Proteone.Application` (handlers + FluentValidation, ports) → `Proteone.Infrastructure` (EF Core + Npgsql, migrations) → `Proteone.Api` (Minimal APIs, auth, CLI). `backend/src/Proteone.Worker` (Generic Host, jobs `OutboxDispatcher`, `EmailSender`, `SlaMonitor`, `ExportRunner`, `Cleanup`, row leases; references Application + Infrastructure, never Api; ADR-0014/0015) runs next to the API. Solution: `backend/Proteone.slnx`.
- **Database:** PostgreSQL 17, local Homebrew (ADR-0002). snake_case, `uuid` v7, `xmin` concurrency.
- **Frontend:** React 19 + Vite + TypeScript in `frontend/` (ADR-0008). Server state via TanStack Query; typed client `src/shared/api/client.ts` generated from OpenAPI. UI on Astryx (`@astryxdesign/core`, neutral theme, `AstryxProvider` in `src/shared/ui`, Lucide icons through the `Icons` map; ADR-0027) — UI code follows `frontend/CLAUDE.md` (look up components with `npx astryx component <Name>`). Shared pieces: `src/shared/form` (react-hook-form bound fields), `src/shared/list` (`DataList`, `ListToolbar`, URL filter state), `src/shared/feedback` (dialogs, states), `src/shared/page`, brand in `src/shared/brand`, two entry pages (`index.html`, `admin/index.html`; `src/app/router.tsx`, `src/app/adminRouter.tsx`; nothing outside the admin entry imports `src/features/admin`; ADR-0038), layout in `src/shared/layout`, breadcrumbs/route metadata in `src/shared/navigation`. `react-markdown` (KB, no raw HTML), Chart.js via `react-chartjs-2` (reports, ADR-0022); Rolldown chunk groups in `vite.config.ts` — new heavy dependency or feature folder gets its own group.
- **Deployment:** `Dockerfile` (images `proteone-api`, `proteone-worker`, non-root), `deploy/compose.yaml` + `deploy/.env` (git-ignored; template `deploy/.env.example`), nginx TLS (`deploy/nginx/`, certs in git-ignored `deploy/certs/`), backup scripts in `deploy/backup/`. API CLI commands in `backend/src/Proteone.Api/Cli/`: `setup-admin`, `reset-password`, `migrate`, `seed-pilot`. Docker on macOS = Colima: start only for a Docker step, stop afterwards.
- **CI:** `.github/workflows/ci.yml` (lint, test, E2E, image builds), `.github/dependabot.yml`.
- Out of scope unless asked: mobile app.

## Commands
Run through `tools/` scripts.
- `tools/db-up.sh` — ensure local PostgreSQL role/database + API user-secret connection string.
- `tools/migrate.sh` — apply EF Core migrations.
- `tools/setup-admin.sh --email … --first-name … --last-name …` — create first admin (once).
- `tools/reset-password.sh --email …` — temporary password from the console (locked-out operator).
- `tools/add-migration.sh <Name>` — add an EF Core migration.
- `tools/run.sh` — API on `http://localhost:5311`, Worker (email to `tmp/email-pickup`) + Vite on `http://proteone.localhost:5310`, in the terminal; refuses while the dev services run.
- `tools/dev-service.sh install|uninstall|start|stop|restart|status|logs` — launchd agents keep API/Worker/Vite running (last-good-build fallback via `tools/dev-dotnet-host.sh`). Stopping them for builds or E2E is fine, but restart them afterwards (`tools/dev-service.sh start`, or `restart` to pick up backend changes).
- `tools/lint.sh` — `dotnet format --verify-no-changes` + ESLint + TypeScript.
- `tools/test.sh` — backend tests with coverage gate + frontend tests with coverage gate. Backend uses Microsoft.Testing.Platform (repo-root `global.json`): `dotnet test --solution backend/Proteone.slnx`, filters `--filter-class` / `--filter-method`.
- `tools/e2e.sh` — Playwright E2E on a fresh `proteone_e2e` database per viewport; refuses to start while `tools/run.sh` holds ports 5311/5312 (stop the dev services first). Also starts the Worker (`Email:Mode=Pickup` into a per-run temp folder, 2 s job intervals; E2E-only env overrides). Tests seed their data through the admin API (`frontend/e2e/world.ts`).
- `tools/ui-qa.sh` — browser QA sweep on a fresh E2E database: every screen in light/dark, phone and desktop, into `tmp/ui-qa/` (contact sheets + `problems.txt` for overflow/console errors); stops and restarts the dev services.
- `tools/gen-api-types.sh` — regenerate OpenAPI JSON and `schema.d.ts` after API contract changes.
- `tools/build.sh` — lint + tests, then Release build + frontend build with chunk sizes.
- `tools/docker-build.sh` — build `proteone-api`/`proteone-worker` images (tags: Git short hash, `latest`).
- `tools/deploy.sh [--skip-gates]` — gates, images, compose up, wait for health through nginx.
- `tools/compose.sh <args>` — `docker compose` with `deploy/compose.yaml` + `deploy/.env`.
- `tools/gen-self-signed-cert.sh [host]` — test certificate in `deploy/certs/`.
- `tools/backup.sh`, `tools/restore.sh <archive> [--force]`, `tools/restore-drill.sh` — backup now, restore, drill on throw-away stacks (writes `docs/release/restore-drill.md`).
- `tools/seed-pilot.sh [--fresh|--drop|--compose] [--scale <f>]` — pilot data (local `proteone_pilot*` DB or deployed stack).
- `tools/perf-measure.sh` (`PERF_PASSWORD`) — perf run, writes `docs/release/performance.md`.
- `tools/guide-screenshots.sh` (`GUIDE_PASSWORD`), `tools/build-user-guide.sh` — guide screenshots; `combined.md` + A4 `combined.pdf`.
- `tools/ci-lint.sh` — actionlint on the workflows.
- `tools/check-doc-paths.sh [file ...]` — every `tools/`/`deploy/` path named in the docs exists; run after doc edits.
- `tools/with-db.sh <command>` — start local PostgreSQL for one command and stop it again (when the owner keeps the database stopped).
- `tools/progress.sh [plan-file]` — plan task status from the SDD ledger (`.superpowers/sdd/<plan>/progress.md`) + branch, recent commits, uncommitted changes. Default: newest plan. `tools/progress-watch.sh [seconds] [plan-file]` re-runs it on an interval.
- `tools/directory-contract-test.sh` — LDAP/OIDC sign-in contract tests against local fake directories (`deploy/test-directory/`); needs Docker (starts/stops Colima itself), not in CI; results in `docs/release/directory-contract-test.md`.

## Quality gates (must pass before commit)
- **C#:** `TreatWarningsAsErrors=true`, `Nullable=enable` — zero warnings.
- **C# tests gate builds:** `tools/build.sh` runs `tools/test.sh` first; Domain + Application line coverage ≥ 80%.
- **TS/React:** ESLint errors only, `--max-warnings=0`, no `eslint-disable`; frontend line coverage ≥ 80%.
- **CI:** green run of `.github/workflows/ci.yml` on the commit to merge.
- **Contracts:** after changing an endpoint, run `tools/gen-api-types.sh`; `TranslationCoverageTests` must pass (every error code and audit action has Turkish text).
- No magic numbers/strings — named constants.

## Conventions
- English code, identifiers, DB names, endpoints, URL paths; Turkish only in `frontend/src/shared/i18n/locales/tr/*.json` (ADR-0006).
- New API error → add a constant to `ErrorCodes` and a Turkish text to `errors.json`.
- Cross-row rules (last admin, location cycles, asset lifecycle, import commit, intake team, device catalog renames vs asset writes) run inside `IAppDbContext.RunSerializedAsync` with a key from `SerializationLocks`.
- Ticket endpoints load through `TicketScope` (404 outside scope); work actions go through `TicketWork` (scope → version → change → one save). Comments/links touch `updated_at` without the version check.
- Attachments: `AttachmentTypes` (Domain) decides allowed types; storage through `IFileStore`, scanning through `IFileScanner`. `Files:RootPath` must be one absolute folder shared by API and Worker outside Development (export files).
- Substring search: `EF.Functions.Like` with `LikePattern.Containing` + `LikePattern.EscapeText`, never `string.Contains` (bypasses trigram indexes).
- `users` has an application-managed `version` (bump only on admin-editable changes via `BumpVersion`); other tables use `xmin`.
- Handlers: one class per use case in `Proteone.Application/Features/<Area>/`, `HandleAsync(command, ct)`; validators `internal sealed`.
- Git: stage named paths only — never `git add -A`/`.`; no dotfiles/dot-dirs unless told. No push or merge to `master` without owner approval.
- Approvals batched at the end (owner rule, all work in this project): never stop mid-work to ask. Never wait for "devam et" either: when a task, review or fix round ends, start the next step at once. Do everything that needs no approval (code, tests, local commits on a feature branch, requested docs); decide small open points yourself and record them. Collect push, merge, deploy, destructive steps and open decisions into one final `AskUserQuestion`.
- Short work first, long work last (owner rule, all work in this project). Start quick tests, reviews and implementations first. Per task: run focused tests and `tools/lint.sh`. Leave long runs for one pass at phase end, before the final review: full `tools/test.sh`, `tools/e2e.sh`, `tools/ui-qa.sh`, guide PDF, Docker and perf. A long run may go early only when it is the only check for a risk the current task adds; record that as a ruling.
- Owner: Tugay İLTUŞ. Never add AI attribution to any file, commit, or PR.

## Architectural decisions
- Recorded as ADRs in `docs/adr/`, template `~/.claude/templates/adr-template.md`, index `docs/adr/README.md`.
- Significant architectural decision: add an ADR in the same change.

## Docs
- `README.md` — overview, architecture, commands, quality gates.
- Docs change only on request (`~/.claude/guides/docs-updates.md`); after a code change, name the doc that needs a line and stop.
- `capabilities.md` — authoritative feature + tech-stack reference; ends with the `DOC-BASELINE` marker.
- `SECURITY.md` — security policy and posture.
- `docs/operations.md` — operator manual (install, update/rollback, backup, restore, keys). `docs/release/` — acceptance, performance, restore-drill evidence.
- User guide in `docs/user-guides/`. When asked to edit: update `PROGRESS.md`, regenerate `combined.md` / `combined.pdf` via `tools/build-user-guide.sh` (PDF only if a guide page changed; always A4).

## Session continuity & memory
- **Plans/specs:** `docs/superpowers/specs/`, `docs/superpowers/plans/`. Read the latest on resume.
- **Memory index:** `~/.claude/projects/-Users-lektuel-claude-Proteone/memory/MEMORY.md` — one fact per `name.md` file with a one-line pointer in the index.
- When updating any index, create a dedicated `.md` file then add the reference line — never inline content into the index; no orphan files.

## Graphify

This repo is indexed in Graphify. For questions about code structure, callers, dependencies, or the impact of a change, use Graphify's MCP tools before grep or reading files one by one: query_graph for how something works, graphify_find for where a symbol is defined, graphify_callers for who calls it, graphify_impact for what a change could affect, review_status for Graphify's review of a pull request. Use grep for exact strings, such as error messages or config keys. If a Graphify tool fails or the repository isn't indexed, say so, then fall back to grep.
