# Problem SLA and Email Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One business-time SLA timer per problem (creation until known error or resolved) with targets by impact, warning/breach email, and problem email events for teams, owners, incident teams and incident requesters.

**Architecture:** A separate, simple timer (`problem_sla_timers`) and target table (`problem_sla_targets`) beside the ticket SLA, which stays untouched. Problem handlers start, retarget and stop the timer in their existing save; the Worker's `MonitorSlaHandler` gets a second pass; the calendar replanner re-plans problem timers too. Email goes through the existing outbox with a new subject type `problem`.

**Tech Stack:** .NET 11, EF Core + Npgsql, xUnit (Microsoft.Testing.Platform), React 19 + TanStack Query + Astryx, Playwright.

**Spec:** `docs/superpowers/specs/2026-11-27-problem-sla-and-email-design.md`

## Global Constraints

- Ticket SLA code paths (`SlaInstance`, `sla_instances`, policy versions, `/admin/sla/policy` matrix) keep their behavior; only `SlaCalendarReplanner` and `MonitorSlaHandler` gain a problem part.
- Impacts: `Level.High`, `Level.Medium`, `Level.Low`. Default targets (business minutes): High 960, Medium 2400, Low 4800; warning 80 %.
- Timer stops at `MarkKnownError` or `Resolve` (from open/investigating); `Cancel` stops it with result `cancelled`; cancelled timers never count in compliance.
- Impact change and calendar change mirror `SlaInstance.ChangeTarget` and `SlaInstance.ApplyCalendarChange` exactly (breach stays recorded; warning mark resets while below the new threshold).
- No timer for problems created before the deploy; no backfill.
- Employee email (`ticket_cause_found`, `ticket_cause_resolved`) carries only ticket variables (`TicketCommon`), never problem number, title, root cause, workaround or problem link.
- The actor never gets their own action's email (except the requester events); team recipients are active members; one person one email per event.
- New error codes in `ErrorCodes` + `errors.json`; new audit actions in `AuditActions` + `admin.json` (`TranslationCoverageTests`); new event types get Turkish labels in `admin.json` and an entry in `eventTypes.ts`.
- English code; Turkish only in locale files and seeded templates; named constants; `TreatWarningsAsErrors`; ESLint `--max-warnings=0`; after endpoint changes run `tools/gen-api-types.sh`.
- Per task: focused tests + `tools/lint.sh`. Full `tools/test.sh`, `tools/e2e.sh`, `tools/ui-qa.sh` once at the end.
- Shells need `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH` (SDK 11).

## Review Focus

1. A problem created with an owner: the owner receives `problem_assigned` only, not `problem_created` (test in Task 6).
2. Known error then resolve: the timer result is set at known error and the resolve does not change it; requester gets two different mails (Task 4, Task 6).
3. A linked incident that is completed before dispatch: no requester or team mail for it (Task 6).
4. Two Worker passes at the same time on one due timer: exactly one breach event (conditional update; Task 5).
5. Impact changed after a warning to a longer target: warning mark resets, so a second warning can be sent later (Task 1, Task 4).

---

### Task 1: Domain — targets and timer

**Files:**
- Create: `backend/src/Proteone.Domain/Problems/ProblemSlaTarget.cs`, `backend/src/Proteone.Domain/Problems/ProblemSlaTimer.cs`
- Test: `backend/tests/Proteone.Domain.Tests/Problems/ProblemSlaTimerTests.cs`, `backend/tests/Proteone.Domain.Tests/Problems/ProblemSlaTargetTests.cs`

**Interfaces:**
- Produces:
  - `ProblemSlaTarget : Entity` — `Level Impact`, `int TargetMinutes`, `int WarningPercent`; `static ProblemSlaTarget Create(Level impact, int targetMinutes, int warningPercent)`; `void Change(int targetMinutes, int warningPercent)`; validation reuses the ticket target range (`1..SlaTarget.MaxTargetMinutes` minutes, `SlaTarget.MinWarningPercent..SlaTarget.MaxWarningPercent` percent) and throws `DomainException(ErrorCodes.InvalidValue, …)` like `SlaTarget`.
  - `ProblemSlaTarget.Defaults`: `IReadOnlyList<(Level Impact, int TargetMinutes, int WarningPercent)>` = High 960/80, Medium 2400/80, Low 4800/80.
  - `ProblemSlaTimer : Entity` — `Guid ProblemId`, `int TargetMinutes`, `int WarningPercent`, `DateTimeOffset StartedAt`, `DateTimeOffset DueAt`, `DateTimeOffset WarnAt`, `DateTimeOffset? WarnedAt`, `DateTimeOffset? BreachedAt`, `DateTimeOffset? StoppedAt`, `SlaResult? Result`, `bool IsStopped`.
  - `static ProblemSlaTimer Start(Guid problemId, int targetMinutes, int warningPercent, DateTimeOffset now, BusinessCalendarRules rules)`
  - `void Stop(DateTimeOffset now)` — result `SlaResult.Met` when `now <= DueAt` and not breached, else `SlaResult.Breached`; no-op when already stopped.
  - `void Cancel(DateTimeOffset now)` — result `SlaResult.Cancelled`; no-op when already stopped.
  - `void ChangeTarget(int targetMinutes, int warningPercent, DateTimeOffset now, BusinessCalendarRules rules)` and `void ApplyCalendarChange(DateTimeOffset now, BusinessCalendarRules oldRules, BusinessCalendarRules newRules)` — same math as `SlaInstance` (no pauses: the timer always runs from `StartedAt`); throw on stopped for `ChangeTarget`, no-op on stopped for `ApplyCalendarChange`.
  - Check `SlaResult` has `Met`, `Breached`, `Cancelled`; if a value is missing, add it there (it is shared by ticket instances; adding a value is safe).

- [ ] **Step 1: Write failing tests** — `Start_sets_due_and_warn_from_business_time` (Mon 09:00, 960 min on a 09:00–17:00 calendar → due Tue 17:00; warn at 80 %), `Stop_before_due_is_met`, `Stop_after_due_is_breached`, `Stop_twice_keeps_first_result`, `Cancel_sets_cancelled`, `ChangeTarget_keeps_start_and_breach_and_resets_warning_below_threshold`, `ApplyCalendarChange_counts_elapsed_under_old_rules`, `ProblemSlaTarget_rejects_zero_minutes_and_out_of_range_percent`.
- [ ] **Step 2:** `dotnet test --solution backend/Proteone.slnx --filter-class "*ProblemSla*"` — FAIL (types missing).
- [ ] **Step 3:** Implement both entities; reuse `BusinessTimeCalculator.AddSeconds` / `SecondsBetween`.
- [ ] **Step 4:** Same command — PASS.
- [ ] **Step 5:** Commit `feat(problem-sla): domain targets and timer to known error`.

### Task 2: Persistence and migration

**Files:**
- Create: `backend/src/Proteone.Infrastructure/Persistence/Configurations/ProblemSlaTargetConfiguration.cs`, `.../ProblemSlaTimerConfiguration.cs`, migration via `tools/add-migration.sh ProblemSla`
- Modify: `IAppDbContext` + `AppDbContext` (`DbSet<ProblemSlaTarget> ProblemSlaTargets`, `DbSet<ProblemSlaTimer> ProblemSlaTimers`)
- Test: `backend/tests/Proteone.Api.IntegrationTests/Persistence/ProblemSlaPersistenceTests.cs`

**Interfaces:**
- Produces tables `problem_sla_targets` (unique `impact`, CHECK on the three values, `xmin`) seeded with `ProblemSlaTarget.Defaults`, and `problem_sla_timers` (unique FK `problem_id` → `problems` cascade, `result` CHECK, `xmin`, partial index on `(due_at)` and `(warn_at)` where `stopped_at IS NULL`).

- [ ] **Step 1:** Failing tests `Targets_are_seeded_for_every_impact` (3 rows, defaults), `Second_timer_for_a_problem_is_rejected` (unique violation).
- [ ] **Step 2:** Run `--filter-class "*ProblemSlaPersistenceTests"` — FAIL.
- [ ] **Step 3:** Configurations + migration (seed rows with fixed `uuid` v7 constants named in the migration).
- [ ] **Step 4:** Run — PASS; `tools/migrate.sh` on the dev database.
- [ ] **Step 5:** Commit `feat(problem-sla): problem_sla_targets and problem_sla_timers tables`.

### Task 3: Admin targets — API and page

**Files:**
- Create: `backend/src/Proteone.Application/Features/Sla/Admin/ProblemTargets.cs` (`GetProblemTargetsHandler`, `UpdateProblemTargetsHandler`, validator, DTOs)
- Modify: `backend/src/Proteone.Api/Endpoints/Admin/SlaEndpoints.cs`, `AuditActions.cs` (`ProblemSlaTargetsUpdated = "problem_sla.targets_updated"`), `admin.json` (audit text), `frontend/src/features/admin/sla/SlaPolicyPage.tsx` + `api.ts`, `admin.json` texts
- Test: `backend/tests/Proteone.Api.IntegrationTests/Sla/ProblemTargetsTests.cs`, `frontend/src/features/admin/sla/SlaPolicyPage.test.tsx`

**Interfaces:**
- Produces: `GET /api/v1/admin/sla/problem-targets` → `ProblemTargetsDto(IReadOnlyList<ProblemTargetDto> Targets, uint Version)`, `ProblemTargetDto(Level Impact, int TargetMinutes, int WarningPercent)`; `PUT` same route with `UpdateProblemTargetsCommand(IReadOnlyList<ProblemTargetDto> Targets, uint Version)` — all three impacts required exactly once (400 `validation.invalid` on `targets`), stale version 409. `Version` = the highest `xmin` of the three rows is not stable; use the `xmin` of the `High` row as the set's version and update all three rows in one save under `SerializationLocks.SlaPolicy`.
- Frontend: a `SectionCard` "Problem hedefleri" below the ticket policy on `/admin/sla/policy` with one row per impact (target via the same duration fields the ticket editor uses, warning percent), "Kaydet".

- [ ] **Step 1:** Failing API tests: `Admin_reads_default_targets`, `Admin_updates_targets_and_audit_records_old_and_new`, `Missing_impact_is_400`, `Stale_version_is_409`, `Non_admin_is_403`.
- [ ] **Step 2:** Run — FAIL. **Step 3:** Implement handlers + endpoints; `tools/gen-api-types.sh`.
- [ ] **Step 4:** Failing frontend test `saves_problem_targets_with_version`; implement section; tests PASS; `tools/lint.sh`.
- [ ] **Step 5:** Commit `feat(problem-sla): admin targets by impact`.

### Task 4: Timer lifecycle in problem handlers, calendar re-plan, DTO

**Files:**
- Create: `backend/src/Proteone.Application/Problems/ProblemSlaTracker.cs`
- Modify: `CreateProblem.cs`, `UpdateProblem.cs`, `ProblemStatusActions.cs` (`MarkKnownErrorHandler`, `ResolveProblemHandler`, `CancelProblemHandler`), `SlaCalendarReplanner.cs`, `ProblemDtos.cs` + `ProblemQueries.cs` (detail and list), `DependencyInjection.cs`
- Test: `backend/tests/Proteone.Api.IntegrationTests/Problems/ProblemSlaLifecycleTests.cs`, extend `backend/tests/Proteone.Api.IntegrationTests/Sla/` calendar replan test

**Interfaces:**
- Produces: `ProblemSlaTracker(IAppDbContext db, CalendarProvider calendar)` with `Task StartAsync(Problem problem, DateTimeOffset now, CancellationToken ct)`, `Task RetargetAsync(Problem problem, DateTimeOffset now, CancellationToken ct)` (no-op when no timer or stopped), `Task StopAsync(Guid problemId, DateTimeOffset now, CancellationToken ct)`, `Task CancelAsync(Guid problemId, DateTimeOffset now, CancellationToken ct)` — all add to the caller's unit of work, never save.
- `ProblemSlaDto(string Metric = "known_error", DateTimeOffset DueAt, long? RemainingSeconds, bool IsPaused = false, bool IsWarned, bool IsBreached, SlaResult? Result)` — same JSON shape as one ticket `sla` entry so `SlaBadge` renders it; `ProblemDetail.Sla` and `ProblemListItem.Sla` (`ProblemSlaDto?`, null for old problems).
- `SlaCalendarReplanner.RunAsync` also locks and re-plans running `ProblemSlaTimers` in the same transaction.

- [ ] **Step 1:** Failing tests: `Create_starts_timer_with_impact_target`, `Known_error_stops_timer_met`, `Resolve_after_known_error_keeps_result`, `Resolve_from_open_stops_timer`, `Cancel_sets_cancelled`, `Impact_change_retargets_running_timer`, `Impact_change_after_warning_resets_warning_mark` (Review Focus 5), `Calendar_change_replans_running_problem_timer`, `Old_problem_without_timer_has_null_sla`, `Detail_and_list_carry_sla`.
- [ ] **Step 2:** Run `--filter-class "*ProblemSlaLifecycleTests"` — FAIL. **Step 3:** Implement. **Step 4:** PASS; `tools/gen-api-types.sh`; `tools/lint.sh`.
- [ ] **Step 5:** Commit `feat(problem-sla): start, retarget and stop the timer with the problem`.

### Task 5: Monitor pass and SLA email events

**Files:**
- Modify: `backend/src/Proteone.Application/Sla/MonitorSla.cs`, `NotificationEventTypes.cs` (`ProblemSlaWarning = "problem_sla_warning"`, `ProblemSlaBreached = "problem_sla_breached"`), `OutboxSubjectTypes.cs` (`Problem = "problem"`)
- Test: `backend/tests/Proteone.Api.IntegrationTests/Sla/ProblemSlaMonitorTests.cs`

**Interfaces:**
- Produces: `MonitorSlaHandler.HandleAsync` returns the ticket counts as before and also processes problem timers (same `batch`); each mark is a conditional `ExecuteUpdateAsync` (`WarnedAt == null && WarnAt <= now && StoppedAt == null`, same for breach) and adds `outbox.AddForSubject(eventType, OutboxSubjectTypes.Problem, problemId, userId: null, data with dueAt)` in the same per-timer transaction.
- Recipients and templates for these two events come in Task 6; until then `RecipientResolver` must not throw: add the two cases in this task returning owner else owner-team active members.

- [ ] **Step 1:** Failing tests: `Due_timer_is_breached_once_and_one_event_is_written`, `Two_monitor_runs_at_once_write_one_breach` (Review Focus 4; run two handlers concurrently on separate scopes), `Stopped_timer_is_ignored`, `Warn_then_breach_write_two_events`.
- [ ] **Step 2:** FAIL. **Step 3:** Implement. **Step 4:** PASS.
- [ ] **Step 5:** Commit `feat(problem-sla): monitor warns and breaches problem timers`.

### Task 6: Problem email events, recipients, templates

**Files:**
- Modify: `NotificationEventTypes.cs` (`ProblemCreated = "problem_created"`, `ProblemAssigned = "problem_assigned"`, `ProblemKnownError = "problem_known_error"`, `ProblemResolved = "problem_resolved"`, `TicketCauseFound = "ticket_cause_found"`, `TicketCauseResolved = "ticket_cause_resolved"`), `TemplateVariables.cs` (`ProblemNumber = "problemNumber"`, `ProblemUrl = "problemUrl"`, `Impact = "impact"`; `ProblemSet` = ProblemNumber, Subject, Status, Impact, TeamName, ProblemUrl, RecipientName; SLA events `ProblemSet + DueAt`; cause events `TicketCommon`), `RecipientResolver.cs`, `NotificationContextLoader.cs` (problem subject block, problem URL `/problems/{number}`), handlers `CreateProblem.cs`, `TransferProblem.cs` (transfer + set owner), `ProblemStatusActions.cs`; migration `tools/add-migration.sh ProblemEmailTemplates` (default Turkish templates for all eight events); `frontend/src/features/admin/email/eventTypes.ts`, `admin.json` labels
- Test: `backend/tests/Proteone.Api.IntegrationTests/Notifications/ProblemNotificationTests.cs`, `backend/tests/Proteone.Domain.Tests/Notifications/` template variable test if one exists for the sets

**Interfaces:**
- Emits (all before the handler's single save):
  - create: `ProblemCreated` (subject problem, `userId` = owner given at creation or null); if an owner was given, also `ProblemAssigned` with `userId` = owner.
  - set owner to non-null different value: `ProblemAssigned` (`userId` = new owner); transfer: `ProblemAssigned` (`userId` null → new team members).
  - `MarkKnownError`: `ProblemKnownError` + per linked incident `TicketCauseFound` via `outbox.Add(type, ticketId, userId: null)`.
  - `Resolve`: `ProblemResolved` + per linked incident `TicketCauseResolved`.
- Recipients: `ProblemCreated` → owner-team active members minus event `userId` (the creation owner) minus actor; `ProblemAssigned` → `userId` or team members, minus actor; SLA events → owner else team; `ProblemKnownError`/`ProblemResolved` → for each linked incident not completed/cancelled now: its assignee else team members, distinct, minus actor; `TicketCause*` → the ticket's requester when the ticket is not completed/cancelled now, else none. Actor id comes from the event data the same way existing events carry it.

- [ ] **Step 1:** Failing tests: `Created_mails_team_members_except_actor`, `Created_with_owner_sends_owner_only_assigned` (Review Focus 1), `Owner_change_mails_new_owner`, `Transfer_mails_new_team`, `Known_error_mails_linked_incident_assignees_once_each`, `Known_error_mails_open_incident_requesters_without_problem_data` (assert rendered body has no problem number), `Completed_incident_gets_no_cause_mail` (Review Focus 3), `Resolve_after_known_error_sends_resolved_pair` (Review Focus 2), `Templates_exist_for_all_new_events`, `TranslationCoverageTests` pass.
- [ ] **Step 2:** FAIL. **Step 3:** Implement; seed templates. **Step 4:** PASS; `tools/lint.sh`.
- [ ] **Step 5:** Commit `feat(problem-sla): problem email events and default templates`.

### Task 7: UI badge and report metric

**Files:**
- Modify: `frontend/src/features/problems/ProblemDetailPage.tsx`, `ProblemListPage.tsx` (badge via `SlaBadge`, label "Bilinen hataya kadar"), `frontend/src/features/tickets/SlaBadge.tsx` only if its prop type must widen to `TicketSla | ProblemSla`; `backend/src/Proteone.Application/Reports/Metrics/ProblemMetrics.cs` (`KnownErrorSlaCompliance = "known_error_sla_compliance"`: met ÷ stopped non-cancelled timers, dated by `stopped_at`, drill-down rows = problems), `reports.json` label "Bilinen hata SLA uyumu"
- Test: `ProblemDetailPage.test.tsx`, `ProblemListPage.test.tsx`, `backend/tests/Proteone.Api.IntegrationTests/Reports/ProblemReportTests.cs` (extend)

- [ ] **Step 1:** Failing tests: `detail_shows_known_error_sla_badge`, `old_problem_shows_no_badge`, `list_shows_sla_state`; backend `Known_error_sla_compliance_counts_met_over_stopped_excluding_cancelled` with drill-down numerator/denominator.
- [ ] **Step 2:** FAIL. **Step 3:** Implement; `tools/gen-api-types.sh`. **Step 4:** PASS; `tools/lint.sh`.
- [ ] **Step 5:** Commit `feat(problem-sla): SLA badge on problems and compliance in the problem report`.

### Task 8: E2E, UI sweep, ADR

**Files:**
- Create: `frontend/e2e/problem-sla.spec.ts`, `docs/adr/0046-problem-sla-and-email.md`
- Modify: UI sweep spec (add `/admin/sla/policy` problem section state if not already covered), `docs/adr/README.md`

- [ ] **Step 1:** E2E: admin sets the High target; IT creates a High problem with a linked incident → badge visible; marks known error → badge "karşılandı"; pickup folder has the team mail and the requester mail (requester mail without `PRB-`).
- [ ] **Step 2:** `tools/e2e.sh` (end of phase, with the full gates).
- [ ] **Step 3:** ADR-0046 from the template: decision B, alternatives A and C, commits; index row.
- [ ] **Step 4:** Commit `test(problem-sla): E2E for targets, badge and mails` and `docs(adr): ADR-0046 problem SLA and email`.
