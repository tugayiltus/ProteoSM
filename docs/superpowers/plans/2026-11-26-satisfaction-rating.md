# Satisfaction Rating Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The requester of a completed incident or request gives a 1–5 score and an optional comment; IT sees it, reports show average, response rate and distribution.

**Architecture:** New Domain entity `TicketRating` (one row per ticket, snapshot of team and assignee). One Application use case `RateTicket` behind `PUT /api/v1/tickets/{number}/rating`, loaded through `TicketScope`. The ticket detail DTO carries the rating and `canRate`; the `to_rate` queue feeds a home card. The completion email gets `{{ratingUrl}}`. Report metrics live in `RatingMetrics`.

**Tech Stack:** .NET 11, EF Core + Npgsql, FluentValidation, xUnit (Microsoft.Testing.Platform); React 19, TanStack Query, Astryx, Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-11-26-satisfaction-rating-design.md`

## Global Constraints

- Score 1–5, required. Comment optional, trimmed, empty becomes null, at most 500 characters: `RatingLimits.CommentMaxLength = 500`.
- Window: `RatingLimits.Window = TimeSpan.FromDays(14)` after `CompletedAt`; a request at or after `CompletedAt + Window` is refused.
- Rateable: `Status == Completed` and `Outcome is Resolved or Fulfilled`.
- Only `ticket.RequesterId == actor` rates. Everyone else who sees the ticket reads the result; employees see only their own tickets (already the scope rule).
- The write never changes `tickets.version`; it touches `updated_at` through `TicketActivity.TouchAsync`.
- Error codes: `ratings.not_requester` (403), `ratings.not_rateable` (409), `ratings.window_closed` (409); each in `ErrorCodes` and `frontend/src/shared/i18n/locales/tr/errors.json`.
- Audit actions: `ratings.submitted`, `ratings.updated` (`AuditActions`, Turkish text for `TranslationCoverageTests`).
- Timeline: internal event kind `RatingGiven`, Turkish label "Puan verildi". The comment is never emailed.
- English code and identifiers; Turkish only in `locales/tr/*.json`. No magic numbers or strings. No `eslint-disable`.
- Per task: focused tests + `tools/lint.sh`. Full `tools/test.sh`, `tools/e2e.sh`, `tools/ui-qa.sh` once at the end.
- Owner: Tugay İLTUŞ. No AI attribution anywhere.

## Review Focus

1. Two first ratings sent at once for one ticket (double click, two tabs): one row stays, the other request gets a 409 conflict, never a 500. Test in Task 3.
2. Window edge: a rating exactly at `CompletedAt + 14 days` is refused, one second before is accepted. Test in Task 3 with `FakeClockApiFactory`.
3. An IT staff member who is also the requester of their own ticket may rate it; an IT staff member who is not the requester gets `ratings.not_requester` even with `CanWork`. Test in Task 3.
4. A comment of only spaces is stored as null and the score alone counts. Test in Task 1.
5. A rating on a ticket whose team later changes still reports under the team at completion. Test in Task 5 (report groups by `ticket_ratings.team_id`).

---

### Task 1: Domain entity `TicketRating`

**Files:**
- Create: `backend/src/Proteone.Domain/Tickets/TicketRating.cs`
- Create: `backend/src/Proteone.Domain/Tickets/RatingLimits.cs`
- Modify: `backend/src/Proteone.Domain/Common/ErrorCodes.cs` (three codes above)
- Test: `backend/tests/Proteone.Domain.Tests/TicketRatingTests.cs`

**Interfaces:**
- Produces: `RatingLimits.MinScore = 1`, `MaxScore = 5`, `CommentMaxLength = 500`, `Window = TimeSpan.FromDays(14)`.
- Produces: `static bool TicketRating.IsRateable(Ticket ticket)`; `static DateTimeOffset? TicketRating.RateUntil(Ticket ticket)` (null when not rateable).
- Produces: `static TicketRating TicketRating.Create(Ticket ticket, Guid raterId, int score, string? comment, DateTimeOffset now)`; `void Update(int score, string? comment, DateTimeOffset now)`; properties `Id, TicketId, Score, Comment, TeamId, AssigneeId, RatedBy, RatedAt, UpdatedAt, Version`.
- `Create` throws `DomainException(ErrorCodes.RatingNotRateable)` or `DomainException(ErrorCodes.RatingWindowClosed)`; a score outside 1–5 throws `ArgumentOutOfRangeException` (the validator catches it first).

- [ ] **Step 1: Write the failing tests** — `Completed_resolved_ticket_is_rateable_until_fourteen_days`, `Cancelled_and_service_request_required_and_rejected_are_not_rateable` (each of `Cancelled`, `ServiceRequestRequired`, `ApprovalRejected` → `IsRateable == false`, `RateUntil == null`), `Create_snapshots_team_and_assignee` (`rating.TeamId == ticket.TeamId`, `rating.AssigneeId == ticket.AssigneeId`), `Blank_comment_becomes_null` (`"   "` → `Comment == null`), `Create_at_window_end_throws_window_closed` (`now = CompletedAt + Window` → `RatingWindowClosed`; `now = CompletedAt + Window - 1 s` → ok), `Update_changes_score_comment_and_updated_at_but_keeps_rated_at`. Build tickets with the existing domain test helpers in `TicketTests.cs`.
- [ ] **Step 2: Run, expect compile failure.** `dotnet test --project backend/tests/Proteone.Domain.Tests --filter-class "*TicketRatingTests"` → FAIL (`TicketRating` missing).
- [ ] **Step 3: Implement** the entity in `TicketRating.cs` (derive from `Entity` for `xmin` `Version`, `Guid.CreateVersion7()` id) and the constants in `RatingLimits.cs`.
- [ ] **Step 4: Run** the same command → PASS.
- [ ] **Step 5: Commit** `feat(ratings): domain rules of the satisfaction rating`.

### Task 2: Persistence and migration

**Files:**
- Create: `backend/src/Proteone.Infrastructure/Persistence/Configurations/TicketRatingConfiguration.cs`
- Modify: `IAppDbContext` (`DbSet<TicketRating> TicketRatings`), `AppDbContext`
- Create: migration `AddTicketRatings` via `tools/add-migration.sh AddTicketRatings`
- Test: `backend/tests/Proteone.Api.IntegrationTests/Persistence/TicketRatingPersistenceTests.cs`

**Interfaces:**
- Consumes: Task 1 entity.
- Produces: table `ticket_ratings` with `ticket_id` unique FK to `tickets` (cascade restrict), `score smallint` CHECK `score BETWEEN 1 AND 5` named `ck_ticket_ratings_score`, `comment text null` with max length 500, `team_id` FK `teams`, `assignee_id` FK `users` null, `rated_by` FK `users`, `rated_at`, `updated_at`, `xmin`; index `ix_ticket_ratings_team_id_rated_at`.

- [ ] **Step 1: Write the failing test** `Second_rating_row_for_one_ticket_violates_the_unique_index` (insert two rows for one ticket → `DbUpdateException`) and `Score_check_rejects_six` (raw SQL insert with 6 → `PostgresException` with constraint `ck_ticket_ratings_score`).
- [ ] **Step 2: Run** `dotnet test --project backend/tests/Proteone.Api.IntegrationTests --filter-class "*TicketRatingPersistenceTests"` → FAIL.
- [ ] **Step 3: Implement** configuration, DbSet, migration.
- [ ] **Step 4: Run** → PASS; `tools/migrate.sh` applies cleanly on the dev database.
- [ ] **Step 5: Commit** `feat(ratings): ticket_ratings table`.

### Task 3: Rate a ticket (API) and show it on the ticket

**Files:**
- Create: `backend/src/Proteone.Application/Features/Ratings/RateTicket.cs` (command, `internal sealed` validator, handler)
- Create: `backend/src/Proteone.Application/Features/Ratings/RatingDtos.cs`
- Modify: `Features/Tickets/TicketDtos.cs` (`TicketDetail` gains `TicketRatingView? Rating`, `bool CanRate`, `DateTimeOffset? RateUntil`), `Features/Tickets/TicketQueries.cs` (`LoadDetailAsync` fills them)
- Modify: `Proteone.Domain/Tickets/TicketEnums.cs` (`TicketEventKind.RatingGiven`), `Proteone.Domain/Audit/AuditActions.cs`
- Modify: `Proteone.Api/Endpoints/Tickets/TicketEndpoints.cs` (`MapPut("/{number}/rating", …)`)
- Modify: Turkish texts: `errors.json`, audit action texts, timeline label file
- Test: `backend/tests/Proteone.Api.IntegrationTests/Tickets/TicketRatingTests.cs`, `TicketRatingWindowTests.cs` (uses `FakeClockApiFactory`)

**Interfaces:**
- Produces: `public sealed record RateTicketCommand(string Number, int Score, string? Comment)`; `RateTicketHandler.HandleAsync(RateTicketCommand, CancellationToken) : Task<TicketDetail>`.
- Produces: `public sealed record TicketRatingView(int Score, string? Comment, DateTimeOffset RatedAt)`. `Comment` is null for viewers who are neither the requester nor `SeesInternal`.
- Produces: JSON `rating`, `canRate`, `rateUntil` on `GET /api/v1/tickets/{number}`.
- Handler: `scope.LoadAsync` (404 outside scope) → requester check → `TicketRating.Create` or `Update` → `TicketTimeline.Record(..., TicketEventKind.RatingGiven, Visibility.Internal, ...)` with data `{ score }` → audit (`ratings.submitted` on create, `ratings.updated` on update, `newValues: { score }`, no comment) → `TicketActivity.TouchAsync` → one save. A `DbUpdateException` from the unique index maps to `ConcurrencyException` (409).

- [ ] **Step 1: Write the failing tests:**
  - `Requester_rates_a_resolved_incident_and_desk_sees_score_and_comment` — complete via `TicketWorld`, `PUT rating {score:4, comment:"Hızlıydı"}` → 200, detail `rating.score == 4`, `canRate == true`, `rateUntil == completedAt + 14 d`; desk detail shows `"Hızlıydı"`; audit `ratings.submitted` exists; ticket `version` unchanged.
  - `Second_put_updates_and_writes_ratings_updated`.
  - `Desk_member_who_is_not_requester_gets_403_not_requester` (`world.Desk`, with `CanWork`).
  - `It_staff_rates_own_ticket` (IT user opens and later rates own incident → 200).
  - `Cancelled_ticket_is_409_not_rateable`; `Open_ticket_is_409_not_rateable`.
  - `Other_employee_gets_404`.
  - `Score_zero_and_six_and_comment_501_chars_are_400`.
  - `Two_parallel_first_ratings_leave_one_row_and_one_409` (`Task.WhenAll` of two PUTs; status codes `{200, 409}`; one row).
  - Window tests: `Rating_one_second_before_window_end_is_accepted`, `Rating_at_window_end_is_409_window_closed` (`factory.Clock.SetUtcNow` / `Advance`).
- [ ] **Step 2: Run** `dotnet test --project backend/tests/Proteone.Api.IntegrationTests --filter-class "*TicketRating*Tests"` → FAIL.
- [ ] **Step 3: Implement** handler, DTO fields, endpoint (`.ProducesProblem` 400/403/404/409), event kind, audit actions, Turkish texts; register the handler where the other ticket handlers are registered.
- [ ] **Step 4: Run** the tests → PASS; `TranslationCoverageTests` → PASS.
- [ ] **Step 5:** `tools/gen-api-types.sh`; `tools/lint.sh` → exit 0.
- [ ] **Step 6: Commit** `feat(ratings): requester rates a completed ticket`.

### Task 4: `to_rate` queue

**Files:**
- Modify: `Features/Tickets/ListTickets.cs` (`TicketQueue.ToRate`)
- Test: `backend/tests/Proteone.Api.IntegrationTests/Tickets/TicketRatingQueueTests.cs`

**Interfaces:**
- Produces: `GET /api/v1/tickets?queue=to_rate` → the actor's own tickets that are rateable, inside the window and without a rating; newest `CompletedAt` first.

- [ ] **Step 1: Failing test** `To_rate_lists_only_own_rateable_unrated_tickets_in_window`: one resolved (listed), one rated (absent), one cancelled (absent), one of another employee (absent); after `Advance(14 d)` the list is empty (`FakeClockApiFactory`).
- [ ] **Step 2: Run** → FAIL. **Step 3: Implement** with `RatingLimits.Window` and the `IsRateable` statuses as an EF expression. **Step 4: Run** → PASS; `tools/gen-api-types.sh`.
- [ ] **Step 5: Commit** `feat(ratings): queue of tickets waiting for a rating`.

### Task 5: Report figures

**Files:**
- Create: `backend/src/Proteone.Application/Reports/Metrics/RatingMetrics.cs`
- Modify: `Reports/ReportCatalog.cs` (add to `All`), metric labels in `locales/tr/reports.json`
- Test: `backend/tests/Proteone.Api.IntegrationTests/Reports/RatingReportTests.cs`

**Interfaces:**
- Produces, for `ReportName.Incidents` and `ReportName.Requests`, `DateBasis = Completed`:
  - `rating_average` — `Shape = Ratio`; `Numerator` = sum of scores, `Denominator` = rated tickets; `Value` = average.
  - `rating_response_rate` — `Shape = Ratio`; rated ÷ rateable tickets completed in the period.
  - `rating_distribution` — `Shape = Distribution`; buckets `"1"`…`"5"` with counts; `BucketRows` = tickets with that score (drill-down per score).
- The team filter of the report applies to `ticket_ratings.team_id` for these three figures.

- [ ] **Step 1: Failing tests** `Average_rate_and_distribution_for_the_period` (three rated of four rateable: scores 5, 4, 2 → average 3.67, rate 0.75, buckets 1:0 2:1 3:0 4:1 5:1), `Drilldown_of_bucket_five_lists_that_ticket`, `Team_filter_uses_team_at_completion` (transfer the completed ticket's team in the database after rating; figure stays under the old team).
- [ ] **Step 2: Run** `--filter-class "*RatingReportTests"` → FAIL. **Step 3: Implement.** **Step 4: Run** → PASS; Excel export test of the report still passes.
- [ ] **Step 5: Commit** `feat(ratings): rating figures in the incident and request reports`.

### Task 6: Completion email link

**Files:**
- Modify: `Proteone.Domain/Notifications/TemplateVariables.cs` (`RatingUrl = "ratingUrl"` in `TicketCompletedSet`), `Proteone.Application/Notifications/NotificationContextLoader.cs`
- Create: migration `RatingLinkInCompletedTemplate` (updates the `ticket_completed` body only where it still equals the seeded default; down migration restores it)
- Modify: variable help text for the admin template page (`locales/tr/admin.json`)
- Test: `backend/tests/Proteone.Api.IntegrationTests/Notifications/RatingLinkEmailTests.cs`

**Interfaces:**
- Produces: `{{ratingUrl}}` = `App:PublicBaseUrl` + `/tickets/{number}?rate=1` for rateable tickets; empty string otherwise.

- [ ] **Step 1: Failing tests** `Completed_email_of_resolved_ticket_contains_rating_link`, `Completed_email_of_rejected_request_has_no_rating_link`, `Edited_template_is_not_changed_by_the_migration`.
- [ ] **Step 2: Run** → FAIL. **Step 3: Implement.** **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `feat(ratings): rating link in the completion email`.

### Task 7: Rating section on the ticket page

**Files:**
- Create: `frontend/src/features/tickets/TicketRating.tsx`, `TicketRating.test.tsx`
- Modify: `frontend/src/features/tickets/api.ts` (`useRateTicket`), `TicketDetailPage.tsx`, `locales/tr/tickets.json`

**Interfaces:**
- Consumes: `rating`, `canRate`, `rateUntil` from Task 3 (generated `schema.d.ts`).
- Produces: `<TicketRating ticket={ticket} focusOnMount={boolean} />`; `useRateTicket(number)` mutation → invalidates the ticket query.

- [ ] **Step 1: Failing tests:**
  - `requester_sees_five_choices_and_sends_score_and_comment` (radio group "Bu kayıt nasıl çözüldü?", choose 4, type comment, "Gönder" → `PUT` body `{ score: 4, comment: 'Hızlıydı' }`).
  - `after_rating_shows_score_and_change_until_date` ("Değiştir" visible, text contains the formatted `rateUntil`).
  - `desk_view_shows_score_and_comment_without_form`; `desk_view_shows_puan_yok_when_null`.
  - `rate_query_param_focuses_first_choice` (`?rate=1` → first radio has focus).
  - `server_window_closed_error_shows_in_the_form_alert`.
- [ ] **Step 2: Run** `cd frontend && npx vitest run src/features/tickets/TicketRating.test.tsx` → FAIL.
- [ ] **Step 3: Implement** with Astryx (`RadioGroup` or the component `npx astryx component --list` names for a single choice, `TextArea`, `Button isLoading`); look up props with `npx astryx component <Name> --dense`. Score labels: 1 "Çok kötü", 2 "Kötü", 3 "Orta", 4 "İyi", 5 "Çok iyi" (my call).
- [ ] **Step 4: Run** → PASS; `tools/lint.sh` → exit 0.
- [ ] **Step 5: Commit** `feat(ratings): rating section on the ticket page`.

### Task 8: Home card

**Files:**
- Modify: `frontend/src/features/home/HomePage.tsx`, `HomePage.test.tsx`, `frontend/src/features/tickets/api.ts` (`TicketQueue` gains `'to_rate'`), `locales/tr/home.json`

- [ ] **Step 1: Failing tests** `employee_sees_puan_bekleyen_kayitlar_with_rows_linking_to_rate` (row href `/tickets/INC-…?rate=1`, at most 5 rows), `card_is_hidden_when_queue_is_empty`.
- [ ] **Step 2: Run** → FAIL. **Step 3: Implement** with the existing card pattern on the page (`useTickets({ queue: 'to_rate', pageSize: 5 })`). **Step 4: Run** → PASS; lint.
- [ ] **Step 5: Commit** `feat(ratings): home card of tickets waiting for a rating`.

### Task 9: Report screen, pilot data, E2E, ADR

**Files:**
- Modify: report figure labels/format if `formatMetric.ts` needs the average format (one decimal); `ReportsPage.test.tsx`
- Modify: `backend/src/Proteone.Application/Features/Pilot/` (a few ratings on completed pilot tickets)
- Create: `frontend/e2e/ratings.spec.ts`
- Create: `docs/adr/0044-satisfaction-rating.md`; modify `docs/adr/README.md`, `proteone.md` §2 and §19 (ADR rule of `itil-scope-expansion`)
- Modify: `tools/ui-qa.sh` screen list and `tools/guide-screenshots.sh` list if they enumerate ticket states

- [ ] **Step 1: Failing E2E** `employee_rates_from_the_email_link_and_desk_sees_it`: desk completes an incident; employee opens `/tickets/{n}?rate=1`, chooses 5, sends; home card no longer lists it; desk sees "5" on the ticket; reports page shows the average figure. Run on both viewports.
- [ ] **Step 2: Run** `E2E_PROJECTS=mobile-360 tools/e2e.sh ratings.spec.ts` → FAIL before the pilot/report wiring, then implement, then PASS.
- [ ] **Step 3:** Seed test `SeedPilotCommandTests` still passes with ratings present.
- [ ] **Step 4:** ADR-0044 from `~/.claude/templates/adr-template.md` (Deciders `Tugay İLTUŞ`, approach C, rateable outcomes, window, snapshot; alternatives A and B), index row, `proteone.md` lines.
- [ ] **Step 5: Commit** `feat(ratings): report screen, pilot data, E2E and ADR-0044`.
- [ ] **Step 6: Phase end:** `tools/test.sh`, `tools/e2e.sh`, `tools/ui-qa.sh` (zero problems), one whole-branch review; findings fixed; then the owner's merge question.

Docs that need a line after this lands (only on request): `capabilities.md`, `README.md` feature list, user guide page for tickets and reports.
