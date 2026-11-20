# Phase 11 — Light Problem Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** IT staff record a shared cause of repeat incidents as a `PRB-` problem with root cause and workaround, link incidents and fixing changes to it, publish the known error as an internal draft article, and see problems in search, on incidents and changes, and in a report.

**Architecture:** A new `Problem` aggregate next to `WorkTask` and `ChangeRequest` (ADR-0031 pattern): counter kind `problem` in `ticket_counters`, `ProblemScope` (read for all IT staff, write for admin + owner team), `ProblemWork` (scope → version → change → one save), append-only `problem_events`, audit. Two typed link tables (ADR-0033 pattern): `problem_tickets` (one problem per incident) and `problem_changes`. The article is created through the existing `CreateArticleHandler`.

**Tech Stack:** .NET 10, EF Core + Npgsql, FluentValidation, Minimal APIs; React 19, TanStack Query, Astryx; xUnit (Microsoft.Testing.Platform), Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-11-20-problem-management-design.md` (approved 2026-11-20). Patterns: tasks `backend/src/Tgysm.Domain/Tasks/WorkTask.cs`, `Features/Tasks/TaskScope.cs`, `TaskWork.cs`, `AssignTask.cs` (transfer), `TaskComments.cs` (notes); links `Features/Changes/ChangeTicketLinks.cs`; report `Reports/Metrics/TaskMetrics.cs` + `TaskReportSource.cs`; search `Features/Search/Search.cs`; frontend `frontend/src/features/tasks/*`, `features/changes/ChangeTicketLinks.tsx`.

## Global Constraints

- Number `PRB-YYYY-NNNNNN`; counter kind `"problem"` in `ticket_counters`.
- Statuses `open`, `investigating`, `known_error`, `resolved`, `cancelled` (snake_case, CHECK). Transitions: `open → investigating` (start); `open|investigating → known_error` (root cause and workaround both non-empty); `open|investigating|known_error → resolved` (resolution required); same three → `cancelled` (reason required). No reopen. Invalid move 409 `problems.invalid_transition`.
- Editable until `resolved`/`cancelled`: title (≤ 200), description, impact (`low|medium|high`, reuse `Level`), root cause, workaround. Closed problem edit 409 `problems.closed`.
- Read: every active IT staff and admin sees every problem. Employees 404 on every problem route and on `GET /tickets/{n}/problem`, `GET /changes/{n}/problems`.
- Write on the problem (edit, state, notes, change links, article, transfer, owner): admin or member of the owner team; others 403 `common.forbidden`.
- Create: IT staff or admin; `ownerTeamId` must be one of the creator's teams (admins: any active team), else 409 `problems.owner_team_not_allowed`. Optional `ownerId` must be an active member of that team (409 `problems.owner_not_in_team`). Optional `incidentNumber` links that incident in the same save.
- Incident links: allowed for (a) admin or a member of the incident's team (`TicketWork` rule), or (b) admin or owner-team member of the problem when the incident is visible to them. Incidents only (requests 409 `problems.ticket_not_incident`). One problem per incident: unique `ticket_id`, second link 409 `problems.ticket_already_linked`. Problem `resolved`/`cancelled` → 409 `problems.closed` on add; remove always allowed for the same people.
- Change links: write rule on the problem; change must be visible to the actor (`ChangeScope.Visible`) else 404; unique pair, 409 `problems.change_link_exists`.
- Linked rows the viewer cannot open: number, title, status only plus `canOpen = false` (no team-only fields beyond ADR-0033's set, no windows).
- Links touch the ticket's `updated_at` (ADR-0033 convention), never status/SLA; audit + event on each side (ticket event internal).
- Article: allowed in `known_error` or `resolved` with `article_id` null, else 409 `problems.article_not_allowed`. Created via `CreateArticleHandler` as `Draft`, `Internal`, `CategoryId` null; title `"Bilinen hata: {title}"` truncated to the article title limit; summary = first line of workaround (truncated to the summary limit); body Markdown sections `## Belirti`, `## Kök neden`, `## Geçici çözüm`, `## Kalıcı çözüm` (only when resolved), last line `Problem: PRB-…`. Turkish headings live in a backend constant class (product content, like email template defaults), not in locale JSON.
- Report `problems`: scope `all` for IT staff and admins; filter `from`, `to`, `teamId` (owner team). Figures `known_errors_open` (snapshot), `resolved` (resolved_at in period), `resolution_time` (created → resolved, duration pattern), `by_impact` (created in period), `incidents_per_problem` (created in period; buckets `1`, `2_5`, `6_plus`, problems with 0 links excluded).
- UI Turkish copy (exact): menu "Problemler"; statuses Açık, İnceleniyor, Bilinen hata, Çözüldü, İptal edildi; actions "Yeni problem", "İncelemeye al", "Bilinen hata olarak işaretle", "Çözüldü olarak kapat", "İptal et", "Makale oluştur", "Probleme bağla", "Problem oluştur", "Arıza bağla", "Değişiklik bağla"; sections "Belirti", "Kök neden", "Geçici çözüm", "Kalıcı çözüm", "İlgili arızalar", "Düzelten değişiklikler", "Bilinen hata makalesi"; change panel "İlgili problemler"; workspace card "Ekibimin problemleri"; search group "Problemler"; report tab "Problemler", figures "Açık bilinen hatalar", "Çözülen problemler", "Çözüm süresi", "Etkiye göre problemler", "Problem başına arıza".
- Every API change: `tools/gen-api-types.sh`; `TranslationCoverageTests` (error codes, audit actions, event kinds) and `ExportHeadersCoverageTests` green; English code, Turkish only in locale JSON (except article body constants above); no magic numbers/strings.
- Gates per task: backend tasks `tools/lint.sh` + `tools/test.sh`; frontend-only tasks `tools/lint.sh` + `cd frontend && npx vitest run <folders>`. Phase end: `tools/test.sh`, `tools/e2e.sh` (stop dev services, restart after), `tools/ui-qa.sh`, green CI before merge.

## Review Focus

- Desk staff (incident team, not problem owner team) link their incident to another team's problem → 201; the same person editing the problem → 403. Test in Task 3.
- An incident already on problem A linked to problem B → 409 `problems.ticket_already_linked`; two parallel links of one incident to two problems → one 201, one 409, never 500. Test in Task 3.
- "Problem oluştur" from an incident by desk staff choosing a team they are not in → 409 `problems.owner_team_not_allowed`, nothing saved (no orphan problem, no link). Tests in Task 2 (team) and Task 3 (incident).
- "Makale oluştur" twice (double click / two tabs) → one article, second call 409; a problem with a 200-character title still makes a valid article title. Test in Task 4.
- An employee who requested a linked incident: `GET /tickets/{n}/problem` 404, ticket detail JSON unchanged, search returns no problem group. Tests in Tasks 3 and 6.

---

### Task 1: Problem domain, link entities and migration

**Files:**
- Create: `backend/src/Tgysm.Domain/Problems/Problem.cs`, `ProblemEnums.cs`, `ProblemNumber.cs`, `ProblemEvent.cs`, `ProblemTicketLink.cs`, `ProblemChangeLink.cs`; `backend/src/Tgysm.Infrastructure/Persistence/Configurations/Problem*Configuration.cs`; migration `tools/add-migration.sh AddProblems`
- Modify: `ErrorCodes.cs`, `IAppDbContext` + `AppDbContext`, `SerializationLocks` (`ProblemLink`), `frontend/src/shared/i18n/locales/tr/errors.json`
- Test: `backend/tests/Tgysm.Domain.Tests/Problems/ProblemTests.cs`

**Interfaces — Produces:**
- `public enum ProblemStatus { Open, Investigating, KnownError, Resolved, Cancelled }`; `ProblemEventKind { Created, Updated, Started, KnownError, Resolved, Cancelled, Commented, Transferred, OwnerChanged, TicketLinked, TicketUnlinked, ChangeLinked, ChangeUnlinked, ArticleCreated }`.
- `Problem.Create(string number, string title, string description, Level impact, Guid ownerTeamId, Guid? ownerId, Guid createdById, DateTimeOffset now)`; methods `Edit(title, description, impact, rootCause, workaround, now)`, `Start(now)`, `MarkKnownError(now)`, `Resolve(string resolution, now)`, `Cancel(string reason, now)`, `Transfer(Guid teamId, now)` (clears owner), `SetOwner(Guid? ownerId, now)`, `AttachArticle(Guid articleId, now)`, `Touch(now)`; `bool IsClosed`. Fields per spec Data section incl. `KnownErrorAt`, `ResolvedAt`, `ClosedAt`, `ArticleId`.
- `ProblemTicketLink.Create(Guid problemId, Guid ticketId, TicketKind kind, Guid by, DateTimeOffset now)` throws `problems.ticket_not_incident` for requests; `ProblemChangeLink.Create(Guid problemId, Guid changeId, Guid by, DateTimeOffset now)`.
- Tables `problems` (unique `number`, unique `article_id` where not null, trigram on `search_text`, status CHECK), `problem_tickets` (unique `ticket_id` `ux_problem_tickets_ticket_id`), `problem_changes` (unique pair `ux_problem_changes_problem_change`), `problem_events`. FKs Restrict.
- Error codes: `problems.invalid_transition`, `problems.closed`, `problems.owner_team_not_allowed`, `problems.owner_not_in_team`, `problems.ticket_not_incident`, `problems.ticket_already_linked`, `problems.change_link_exists`, `problems.article_not_allowed`, `problems.known_error_incomplete`.

- [ ] **Step 1:** Domain tests: each allowed transition sets status and its timestamp; `MarkKnownError` without root cause or workaround throws `problems.known_error_incomplete`; `Resolve` with blank resolution throws validation; any move from `Resolved`/`Cancelled` throws `problems.invalid_transition`; `Edit` on closed throws `problems.closed`; `Transfer` clears `OwnerId`; `ProblemTicketLink.Create` for a request throws `problems.ticket_not_incident`.
- [ ] **Step 2:** `dotnet test --solution backend/Tgysm.slnx --filter-class "*ProblemTests"` — Expected: FAIL (types missing).
- [ ] **Step 3:** Implement entities, configurations, DbSets, codes + Turkish texts; `tools/add-migration.sh AddProblems`; check the migration creates only the four tables, their indexes and checks.
- [ ] **Step 4:** `tools/lint.sh`, `tools/test.sh` — exit 0. Commit `feat(problems): problem aggregate, link tables and migration`.

### Task 2: Problem API (create, list, detail, edit, state, notes, transfer, owner)

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Problems/` — `ProblemScope.cs`, `ProblemWork.cs`, `ProblemDtos.cs`, `CreateProblem.cs`, `ListProblems.cs`, `GetProblem.cs`, `UpdateProblem.cs`, `ProblemStatusActions.cs`, `ProblemNotes.cs`, `TransferProblem.cs`, `ProblemHistory.cs`; `backend/src/Tgysm.Api/Endpoints/Problems/ProblemEndpoints.cs`; `backend/tests/Tgysm.Api.IntegrationTests/Problems/ProblemWorld.cs`, `ProblemApiTests.cs`
- Modify: `AuditActions.cs` (`problems.created`, `problems.updated`, `problems.status_changed`, `problems.transferred`, `problems.owner_changed`), `AuditEntityTypes.Problem = "problem"`, audit/event Turkish texts, DI, `tools/gen-api-types.sh`

**Interfaces:**
- Consumes: Task 1.
- Produces:
  - `ProblemViewer(Guid UserId, UserRole Role, IReadOnlyList<Guid> TeamIds)` with `CanWork(Problem)`; `ProblemScope.ViewerAsync`, `Visible(viewer)` (all problems for staff/admin), `LoadAsync(number)` (employees → 404), `LoadForWorkAsync(number)` (not owner team/admin → 403).
  - `ProblemListItem(Number, Title, Status, Impact, OwnerTeamName, OwnerName?, IncidentCount, UpdatedAt)`; `ProblemDetail(Number, Title, Description, Impact, Status, OwnerTeamId, OwnerTeamName, OwnerId?, OwnerName?, RootCause?, Workaround?, Resolution?, CancelReason?, ArticleId?, ArticleTitle?, CreatedAt, KnownErrorAt?, ResolvedAt?, CanWork, Version)`.
  - Routes under `/api/v1/problems`: `POST ""` `{title, description, impact, ownerTeamId, ownerId?, incidentNumber?}` → 201 detail; `GET ""` `queue` = `mine|team|open|known_errors`, `status`, `impact`, `search`, paging; `GET /{number}`; `PUT /{number}` `{version, title, description, impact, rootCause, workaround}`; `GET /{number}/timeline`; `POST /{number}/notes` `{text}`; `POST /{number}/start|known-error` `{version}`; `/resolve` `{version, resolution}`; `/cancel` `{version, reason}`; `/transfer` `{version, teamId, reason}`; `/owner` `{version, ownerId?}`.

- [ ] **Step 1:** Integration tests `ProblemApiTests`: desk creates in own team → 201 `PRB-` number, audit `problems.created`; create for a team the creator is not in → 409 `problems.owner_team_not_allowed` and no problem row, no link row; admin any team → 201; field staff (other team) `GET` detail → 200 with `canWork = false`, `PUT` → 403; employee every route → 404; full lifecycle start → known-error (409 `problems.known_error_incomplete` until root cause + workaround saved) → resolve; stale version → 409 `concurrency.conflict`; edit after resolve → 409 `problems.closed`; transfer clears owner and records `transferred` event; queues `mine`, `team`, `open`, `known_errors` and `search` by number and title (Turkish casing).
- [ ] **Step 2:** Run the class — FAIL.
- [ ] **Step 3:** Implement handlers and endpoints (task pattern; `xmin` guards single-row state moves). `CreateProblemCommand` already carries `IncidentNumber?`; in this task a non-null value is rejected with 400 until Task 3 wires it.
- [ ] **Step 4:** `tools/gen-api-types.sh`, `tools/lint.sh`, `tools/test.sh` — exit 0. Commit `feat(problems): problem records with lifecycle, notes and transfer`.

### Task 3: Incident and change links

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Problems/ProblemLinks.cs`, `ProblemLinkRules.cs`; `backend/tests/Tgysm.Api.IntegrationTests/Problems/ProblemLinkTests.cs`; migration `tools/add-migration.sh ExtendEventKindChecksForProblemLinks` (constraint-only)
- Modify: `ProblemEndpoints.cs`, `TicketEndpoints.cs`, `ChangeEndpoints.cs`, `TicketEventKind` (`ProblemLinked`, `ProblemUnlinked`), `ChangeEventKind` (same), audit actions `problems.ticket_linked|ticket_unlinked|change_linked|change_unlinked`, `tickets.problem_linked|problem_unlinked`, `changes.problem_linked|problem_unlinked`, Turkish texts

**Interfaces:**
- Consumes: Tasks 1–2, `TicketScope`, `TicketWork` rule, `ChangeScope.Visible`, `SerializationLocks.ProblemLink`, `SaveTranslatingDuplicateAsync`.
- Produces:
  - `ProblemTicketItem(Number, Title, Status, TeamName?, LinkedAt, CanOpen)` (`TeamName` null when `!CanOpen`); `ProblemChangeItem(Number, Title, Status, LinkedAt, CanOpen)`; `TicketProblemItem(Number, Title, Status, Workaround?, OwnerTeamName)`.
  - `GET|POST /problems/{n}/tickets` `{ticketNumber}`, `DELETE /problems/{n}/tickets/{ticketNumber}`; `GET|POST /problems/{n}/changes` `{changeNumber}`, `DELETE /problems/{n}/changes/{changeNumber}`; `GET /tickets/{n}/problem` → `TicketProblemItem` or 204; `GET /changes/{n}/problems` → `ProblemChangeSideItem(Number, Title, Status)[]`.

- [ ] **Step 1:** Integration tests `ProblemLinkTests`: create with `incidentNumber` → problem and link in one save; create with `incidentNumber` of an incident already on a problem → 409 `problems.ticket_already_linked` and no problem row; desk member of the incident's team links it to a field-team problem → 201, ticket `updated_at` moves, ticket and problem events, audit both sides; same desk member `PUT` the problem → 403; owner-team member links an incident of another team they can see → 201; incident on problem A to problem B → 409 `problems.ticket_already_linked`; parallel link of one incident to two problems → one 201 one 409, no 500; request → 409 `problems.ticket_not_incident`; resolved problem add → 409 `problems.closed`, remove → 200; change link by owner team → 201, duplicate → 409 `problems.change_link_exists`, invisible change → 404; non-openable rows have `teamName` null; employee `GET /tickets/{n}/problem` → 404 and ticket detail JSON has no problem field; `GET /changes/{n}/problems` lists the problem for a change viewer.
- [ ] **Step 2:** Run — FAIL.
- [ ] **Step 3:** Implement; ticket-side add/remove go through the same handlers as problem-side. Wire Task 2's `incidentNumber` to `ProblemLinkRules`.
- [ ] **Step 4:** `tools/gen-api-types.sh`, `tools/lint.sh`, `tools/test.sh` — exit 0. Commit `feat(problems): link incidents and changes to problems`.

### Task 4: Known error article

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Problems/CreateProblemArticle.cs`, `KnownErrorArticleText.cs` (heading and title constants); tests in `ProblemArticleTests.cs`
- Modify: `ProblemEndpoints.cs` (`POST /problems/{n}/article` → 201 `ProblemDetail`), audit `problems.article_created`

**Interfaces:** Consumes `CreateArticleHandler`, `Problem.AttachArticle`. Produces `KnownErrorArticleText.Build(Problem p, string number) -> (string Title, string Summary, string Body)` (pure, unit-tested).

- [ ] **Step 1:** Tests: unit `Build` — title `"Bilinen hata: VPN kopuyor"`, body contains `## Belirti`, `## Kök neden`, `## Geçici çözüm`, no `## Kalıcı çözüm` before resolve and present after, last line `Problem: PRB-…`; 200-char problem title → title within the article limit; integration — `known_error` → 201, article is `draft` + `internal`, `articleId` set; second call → 409 `problems.article_not_allowed`; parallel two calls → one article; `open` problem → 409; non-owner staff → 403.
- [ ] **Step 2:** Run — FAIL.
- [ ] **Step 3:** Implement inside one `ProblemWork` frame so the article insert and `article_id` commit together; the unique `article_id` index plus the version check make the second call fail.
- [ ] **Step 4:** Gates, commit `feat(problems): create the known error article from a problem`.

### Task 5: Problems report

**Files:**
- Create: `backend/src/Tgysm.Application/Reports/ProblemReportSource.cs`, `Reports/Metrics/ProblemMetrics.cs`; migration `tools/add-migration.sh WidenExportJobsReportConstraintForProblems` (constraint-only); `backend/tests/Tgysm.Api.IntegrationTests/Reports/ProblemReportTests.cs`
- Modify: `ReportName` (`Problems`), `ReportCatalog`, `ReportScope` (problems: `all` for staff and admin), export headers + `ExportHeadersCoverageTests`, `tools/gen-api-types.sh`

- [ ] **Step 1:** Tests: each figure from the Global Constraints with a seeded set (problems in each state, resolve times, impacts, 0/1/3/7 linked incidents → buckets `1`, `2_5`, `6_plus`, the 0 one absent); IT staff `scopeLabel = all`; `teamId` filters by owner team; drill-down rows and sync Excel export; background export job accepted for `problems`.
- [ ] **Step 2:** Run — FAIL. **Step 3:** Implement on the task report pattern. **Step 4:** Gates, commit `feat(reports): problems report`.

### Task 6: Problems in central search and perf

**Files:** Modify `Features/Search/Search.cs` (`SearchHitType.Problem`, staff only, `SearchText` like, exact `PRB-` number), `SearchTests`, `tools/perf-measure.sh` (problem list + problem search rows), `tools/gen-api-types.sh`

- [ ] **Step 1:** Tests: staff search by title word returns a `problem` group; full `PRB-` number → `exact`; employee response has no `problem` group.
- [ ] **Step 2:** Run — FAIL. **Step 3:** Implement. **Step 4:** Gates, commit `feat(search): problems in central search`.

### Task 7: Problem pages (frontend)

**Files:**
- Create: `frontend/src/features/problems/` — `api.ts`, `ProblemListPage.tsx`, `ProblemDetailPage.tsx`, `ProblemFormDialog.tsx`, `ProblemActions.tsx`, `ProblemActionDialog.tsx`, `ProblemBadges.tsx`, `ProblemLinks.tsx` (İlgili arızalar, Düzelten değişiklikler), `ProblemArticleBox.tsx`, `ProblemTimeline.tsx`, `ProblemQueueCard.tsx`, tests next to each page; `frontend/src/shared/i18n/locales/tr/problems.json`
- Modify: routes + `routeMeta.ts` (`/problems`, `/problems/:number`, breadcrumb parent Problemler), sidebar IT operasyonu "Problemler" with an `Icons` entry, `/workspace` card "Ekibimin problemleri", `vite.config.ts` chunk group if the folder needs one

- [ ] **Step 1:** Vitest: list renders rows and keeps queue/status/impact/search in the URL; detail shows sections and one primary action per status (`open` "İncelemeye al", `investigating` "Bilinen hata olarak işaretle", `known_error` "Çözüldü olarak kapat"); `canWork = false` hides write actions but shows content; "Makale oluştur" visible only in `known_error`/`resolved` without article, then shows the article link; link rows with `canOpen = false` render without a link; 409 in a dialog shows the message and closes (`useCloseOnConflict`).
- [ ] **Step 2:** Run — FAIL. **Step 3:** Implement with Astryx components (`frontend/CLAUDE.md`). **Step 4:** `tools/lint.sh`, `cd frontend && npx vitest run src/features/problems src/features/workspace` — pass. Commit `feat(frontend): problem list and detail pages`.

### Task 8: Problem on incidents, changes, search and reports (frontend)

**Files:** Create `frontend/src/features/tickets/TicketProblemCard.tsx` (+ test), `frontend/src/features/changes/ChangeProblems.tsx` (+ test). Modify ticket detail (IT staff only), change detail, search palette group "Problemler" + `listPathForType`, report tab "Problemler" + figure labels in `reports.json`.

- [ ] **Step 1:** Vitest: linked incident card shows number, status and workaround text with a link; unlinked card offers "Probleme bağla" (number dialog) and "Problem oluştur" (form prefilled with ticket subject and description, sends `incidentNumber`); employee view renders no card and makes no request; change panel lists problems; search shows the Problemler group and "Tümünü gör" opens `/problems?search=…`; report tab shows five figures.
- [ ] **Step 2:** Run — FAIL. **Step 3:** Implement. **Step 4:** Lint + vitest on touched folders; commit `feat(frontend): problems on incidents, changes, search and reports`.

### Task 9: Pilot seed, E2E, ADR and phase exit

**Files:** Modify `PilotSeed*` (new `PilotSeedSession.Problems.cs`: a few problems per state, linked incidents, one fixing change, one article); create `frontend/e2e/problems.spec.ts` (+ `world.ts` helpers); create `docs/adr/0034-light-problem-management.md` (template `~/.claude/templates/adr-template.md`, Deciders Tugay İLTUŞ, alternatives B and C from the spec) + row in `docs/adr/README.md`; update `tgysm.md` §2 (light problem management in scope; full Problem Management, trend analysis still out) and §19 (Phase 11 row).

- [ ] **Step 1:** E2E (phone and desktop): desk opens an incident → "Problem oluştur" → field team member opens the problem, fills root cause + workaround → "Bilinen hata olarak işaretle" → "Makale oluştur" → article draft exists → links a change → resolves; desk sees workaround on a second incident after "Probleme bağla"; employee requester sees no problem card.
- [ ] **Step 2:** Pilot seed test (existing seed test class) asserts problem counts per state.
- [ ] **Step 3:** `tools/test.sh`, `tools/e2e.sh` (stop dev services first, restart after), `tools/ui-qa.sh` — all pass; `tools/check-doc-paths.sh`.
- [ ] **Step 4:** Commit `test(e2e): problem management; ADR-0034`. Docs (`capabilities.md`, `README.md`, `docs/status.md`, guide) follow at phase end as the last step (owner rule).
