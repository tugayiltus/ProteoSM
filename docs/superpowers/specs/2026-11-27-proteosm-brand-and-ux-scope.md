# ProteoSM — brand move, UX/UI audit and fixes: scope and order

Date: 2026-11-27. Owner: Tugay İLTUŞ. Request: owner brief "ProteoSM — Marka geçişi, UX/UI denetimi ve geliştirme".

## Names (owner decision 2026-11-27)

- Product: **ProteoSM** — the only user-visible product spelling. Umbrella brand of all products: **ProteONE**.
- Pattern "ProteoSM by ProteONE": wordmark and titles say ProteoSM; the sign-in screen, an about/footer line, the e-mail footer and the docs carry "ProteoSM by ProteONE" (or "bir ProteONE ürünü" in Turkish text).
- Technical ids: .NET `ProteoSM.*`, lower-case ids `proteosm` (images, Compose, DB, role, archives, dev host `proteosm.localhost`), env prefix `PROTEOSM_`.
- An earlier, uncommitted attempt with the name "ProteoneSM" was discarded; nothing of it is on the branch.

## Starting state (verified 2026-11-27)

- GitHub default branch `master` = `7f36d25` (outgoing mail settings, ADR-0045). Branch `feature/problem-sla` (ADR-0046) is pushed and waits for CI, then merges by owner approval given earlier.
- Dev services (launchd) serve the working tree: API `:5311`, Vite `http://proteone.localhost:5310`.
- The earlier observation "admin content disappears, `EmailSendingPage` import unresolved" was taken while that page was being written; on the current tree the file exists. P0 re-tests it before any fix.
- Precedent: ADR-0037 moved TGYSM to Proteone with the same kind of rename; ADR-0043 later removed the TGYSM upgrade path but kept the Data Protection literals `Tgysm`, `Tgysm.AuthSettings`, `Tgysm.Notifications.AccountLinkToken` and all applied migrations.

## Order

1. **P0 — admin loading.** Reproduce on the current tree (menu, direct URL, reload; dev server and built preview). Add a route-level error screen for both entries (lazy chunk or render failure: "Yeniden dene", "Uygulamaya dön"). Add a CI check that builds both entries and opens every admin route once (Playwright smoke over the built preview). Clarify the menu and titles of E-posta şablonları / E-posta gönderimleri / E-posta gönderimi.
2. **Brand — ProteoSM** (new ADR-0047, supersedes the naming part of ADR-0037 and amends ADR-0043 for a Proteone → ProteoSM move):
   - 2a. User-facing: every visible name, titles, wordmark (keep the symbol), favicon title, login, sidebars, error pages, locale files, default email texts (new migration only for templates that still equal the shipped default; customized templates, sent mail and audit history untouched), sender display-name default, QR label, report and export names, demo/pilot content, guide screenshots and docs.
   - 2b. Code identities: solution `ProteoSM.slnx`, projects/assemblies/namespaces `ProteoSM.*`, frontend package name, test projects. One mechanical, scripted rename commit; no logic change in it.
   - 2c. Operations identities: images `proteosm-api`/`proteosm-worker`, Compose project `proteosm`, DB/role `proteosm` (local `proteosm_dev`, `_e2e`, `_pilot`), env prefix `PROTEOSM_` with read fallback to `PROTEONE_` when the new key is missing, backup archive prefix `proteosm-` with restore accepting `proteone-` archives, `tools/deploy.sh` refuses an empty start while a `proteone_pgdata` volume exists, dev host `proteosm.localhost` with `proteone.localhost` still accepted.
   - Kept on purpose (compatibility): Data Protection application name and purposes (`Tgysm*`, `Proteone.EmailSettings`), migration ids and files, monitoring webhook header/route contract, OIDC/LDAP claim contracts. Cookie names move to `__Host-proteosm*` (one sign-in again after the update; documented).
   - Migration of an existing Proteone install: backup in the old stack → restore into the new stack (data, files, key ring) → smoke. Old DB, role, volumes and archives stay for rollback; writes stop during the move; data written after the move is lost on rollback (documented). Drill in isolation: fresh ProteoSM install, and restore of a realistic Proteone backup with comparison of users, tickets, links, attachments, report files, default/custom templates, encrypted values and audit.
   - Guard: `FormerProductNameTests` extended so a bare "Proteone" (not followed by `SM`) outside the allow list fails.
   - Recommended GitHub target: `tugayiltus/ProteoSM` (owner decides; remote not changed here).
3. **Click-path audit** (`ecc:click-path-audit`) over every menu route for employee, IT staff and admin, phone and desktop, on the isolated E2E database. Findings feed P1–P3; the final report table lists screen, role, path, observation, change, verification.
4. **P1 — İş listem and incident detail** (brief section P1).
5. **P2 — employee and shared screens** (brief section P2).
6. **P3 — admin setup and operations UX** (brief section P3).
7. **Backlog documents** (no code): ITSM backlog items 1–9 and the operations backlog, each with gap, screen sketch, dependency and acceptance scenario, written against current ADRs and code; Odoo ITSM Pack used as a workflow reference only.

## Acceptance per phase

- **P0:** the admin route smoke spec is green on the built preview; a deliberately broken lazy import shows the error screen with both actions (unit test); CI builds both entries.
- **Brand:** old/new identity table in the ADR; migration drill report `docs/release/proteosm-migration-drill.md` green for the fresh install and the Proteone-backup move (users, tickets, links, attachments, report files, default/custom templates, encrypted values, audit compared); `FormerProductNameTests` fails on a bare "Proteone" outside its allow list; full tests, E2E and UI QA green.
- **Audit:** `docs/release/proteosm-audit.md` — one table: screen, role, path, observation, change, verification (live / from code / action-tested); screenshots in `docs/release/proteosm-screens/`.
- **P1 (E2E):** a technician finds a risky ticket from the SLA-risk view, sees its due time on the detail, "Yanıtla" moves focus to the reply box, and returning restores filter, sort and page; on a phone the first screen shows subject, status, due time and the main action.
- **P2/P3:** each touched list shows first-use empty, no filter result and load error differently (tests); report figure, drill-down and Excel agree on isolated data (test); previews never publish or send (test); readable durations ("150 iş dakikası = 2 sa 30 dk").
- **Backlog:** one short design per item under `docs/superpowers/specs/` (gap, screen sketch, dependency, acceptance scenario); kept decisions: P1–P4 matrix, satisfaction rating, no reopen.

## Working rules for this run

- Test-first per task (project rule and TDD); long runs once per phase end; whole-branch review on the most capable model per phase with one fix wave.
- Odoo ITSM Pack is a workflow reference only: no copying of its screens, text or visual design.

- Code first, then tests, review, verification (owner process note). Dev services may be stopped while building; they are restarted at the end of each phase.
- Each phase is its own commit series on `feature/proteosm`, verified before the next. Long runs (full tests, E2E, UI QA, guide PDF) once per phase end.
- Not done without a separate owner decision: production deploy, GitHub repository rename or remote change, deleting data, volumes or archives.
