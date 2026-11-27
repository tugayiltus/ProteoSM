# Problem SLA and email — design

Date: 2026-11-27. Owner: Tugay İLTUŞ. Module queue item 3 (light ITIL, after satisfaction rating and outgoing mail settings).

## Intent

Problems (ADR-0034) have no time target and send no email. A problem can sit in "Açık" for weeks and nobody is told,
and the people who work or reported the linked incidents never hear that the cause was found. The owner wants
ITIL-style completeness without complexity: one target that keeps problem work moving, and email at the moments that
matter to the people involved.

Success: every new problem gets a due time by impact; its owner (or team) is warned before and told at the breach;
the problem report shows how often the target was met; owners, teams, incident teams and incident requesters get
email when a problem is opened, assigned, becomes a known error and is resolved.

## Goals

- One SLA timer per problem: from creation until the problem becomes **Bilinen hata** or **Çözüldü**
  (owner choice: "time to known error"; root-cause fix time is not measured).
- Targets by impact (`High`, `Medium`, `Low`), in business time on the existing business calendar (ADR-0018),
  set by an admin.
- Warning and breach email, as for tickets.
- Email events (owner chose all four):
  1. **Problem opened** — to the active members of the owner team.
  2. **Problem assigned** — to the new owner when an owner is set or changed; to the new team's members when the
     problem moves to another team (the move clears the owner).
  3. **Known error / resolved** — to the people working the linked open incidents.
  4. **Linked incident requesters** — employees whose open incident is linked get a plain notice that the cause was
     found / the issue was fixed.
- SLA state visible on the problem page and list; SLA compliance in the problem report.

## Non-goals

- No timer for the root-cause fix (known error → resolved).
- No pause rules: problems have no on-hold state.
- No change to the ticket SLA (`sla_instances`, policy versions, `/admin/sla/policy` matrix).
- No reopen of problems (ADR-0034 stays).
- No timer for problems that exist before the deploy (see Decisions).

## Anti-goals

- An employee email that leaks problem internals: problems are hidden from employees (ADR-0034), so the employee
  email never carries the problem number, title, root cause, workaround or a problem link.
- Email noise that teaches people to ignore problem mail: the actor never gets their own action's email, and one
  person gets one email per event, not one per linked incident (except the per-incident requester notice).
- A broken or riskier ticket SLA because problems were squeezed into it.

## Constraints

- Layers and rules from `CLAUDE.md`: handlers per use case, `TreatWarningsAsErrors`, no magic numbers, Turkish text
  only in locale files, `ErrorCodes` + `errors.json`, audit actions translated (`TranslationCoverageTests`).
- Notifications go through the transactional outbox (ADR-0015): events are added before the single save; recipients
  are resolved at dispatch from current data (`RecipientResolver`); inactive users never get email.
- Templates are admin-managed and plain text (ADR-0016); new event types need default Turkish templates through a
  migration and allowed-variable sets in `TemplateVariables`.
- Problem writes already run under `SerializationLocks.ProblemLink` for state changes (ADR-0034).
- Business-time math: `BusinessTimeCalculator` over `BusinessCalendarRules` (ADR-0018).

## Approaches

- **A. Extend the ticket SLA tables.** `sla_instances` gets a nullable problem id, the policy gets problem × impact
  targets, `SlaMonitor` and the SLA email path are reused as they are. One SLA engine and one admin page, but it
  changes the core tables and the policy matrix validation of the working ticket SLA, and many queries assume a
  ticket. High risk for the main product path.
- **B. A separate, simple problem timer (chosen by the owner).** Own targets table and own timer table; the timer is
  created with the problem and copies its target; the Worker's SLA monitor runs a second pass for problem timers.
  The ticket SLA is untouched; no pauses and one metric keep it small. Cost: some repeated warn/breach code.
- **C. No stored timer.** Compute due time on the fly from `created_at` + current target; keep only warned/breached
  flags on the problem. Least code, but a target change rewrites old problems and the report's met/breached history
  is not reliable.

Owner picked **B**.

## Design

### Targets

- Table `problem_sla_targets`: one row per impact (`impact` unique, CHECK in `High`/`Medium`/`Low`),
  `target_minutes` (business minutes, > 0), `warning_percent` (same range as ticket targets,
  `SlaTarget.MinWarningPercent`..max), `xmin` concurrency.
- Seed defaults through the migration (builder's call on exact numbers; proposal: High 2 business days,
  Medium 5, Low 10, warning 80 %; one business day = the calendar's working minutes is not fixed, so store minutes:
  High 960, Medium 2400, Low 4800 at 8 h days).
- Admin: a "Problem hedefleri" section on `/admin/sla/policy` (or a sibling page `/admin/sla/problems`, builder's
  call by layout fit): three rows, target as days/hours/minutes like the ticket editor, warning percent, one save with
  `version`. `GET|PUT /admin/sla/problem-targets`. Audited `problem_sla.targets_updated` (old and new values).
- A target change applies to problems created afterwards and to impact changes afterwards; running timers keep the
  target they copied.

### Timer

- Table `problem_sla_timers`: `problem_id` (unique FK), `target_minutes`, `warning_percent`, `started_at`,
  `due_at`, `warn_at`, `warned_at`, `breached_at`, `stopped_at`, `result` (`met`, `breached`, `cancelled`), `xmin`.
  Index for the monitor on open timers by `warn_at` / `due_at`.
- Created in `CreateProblemHandler` in the same save, from the current target for the problem's impact;
  `due_at` / `warn_at` from `BusinessTimeCalculator.AddSeconds` on the current calendar.
- Stops on **Bilinen hata** (`MarkKnownError`) or **Çözüldü** (`Resolve` from open/investigating): result `met` when
  stopped at or before `due_at`, else `breached`. A later resolve after known error does nothing to the timer.
- **İptal** stops it with result `cancelled`; cancelled timers do not count in compliance.
- **Impact change** (`UpdateProblemHandler`) while running: re-target from the current target of the new impact,
  keep `started_at`, recompute `due_at` / `warn_at`. Same rule as `SlaInstance.ChangeTarget` for a ticket priority
  change: an earlier breach stays recorded, and the warning mark resets while the new warning threshold is not yet
  used up (so a later warning can be sent again).
- **Calendar or holiday change:** running problem timers are re-planned like ticket instances
  (`SlaInstance.ApplyCalendarChange`, ADR-0018 amendment 2026-10-01): business time up to now under the old rules,
  the rest of the target under the new rules; the warning mark resets as above. This runs in the same calendar/holiday
  save handlers and lock that already re-plan ticket instances.

### Monitor

- The Worker's `SlaMonitor` job runs a second pass: running problem timers with `warn_at <= now` and no
  `warned_at` → set `warned_at`, add `problem_sla_warning`; `due_at <= now` and no `breached_at` → set
  `breached_at`, add `problem_sla_breached`. Claimed with a conditional update (`ExecuteUpdate` on "still not
  warned/breached") so two Workers never double-send. Batch size from the existing `SlaMonitor` options.

### Email events

New `NotificationEventTypes`, outbox subject type `Problem` (and `TicketId` for the requester event):

| Event | Raised when | Recipients (resolved at dispatch) | Actor excluded |
|---|---|---|---|
| `problem_created` | problem created | active members of the owner team, except the owner set at creation | yes |
| `problem_assigned` | owner set or changed (to non-null), also an owner given at creation; team transfer | owner set: that user; transfer: members of the new team | yes |
| `problem_sla_warning`, `problem_sla_breached` | monitor | owner, else owner-team members | n/a |
| `problem_known_error` | `MarkKnownError` | for each linked incident not completed/cancelled: its assignee, else its team members; de-duplicated | yes |
| `problem_resolved` | `Resolve` | same as `problem_known_error` | yes |
| `ticket_cause_found` | `MarkKnownError`, one event per linked open incident | that incident's requester | no |
| `ticket_cause_resolved` | `Resolve`, one event per linked open incident | that incident's requester | no |

- A problem created with an owner raises both events; the owner gets only `problem_assigned`, so one person gets
  one email for the creation.
- "Open incident" = linked incident whose status is not completed or cancelled at dispatch time; a dispatch that
  finds the incident closed sends nothing (event `skipped`).
- A problem resolved straight from open/investigating sends only the resolved pair, never a known-error mail.
- Team-member recipients: active members of the team; no role filter (all team members are IT staff by rule).

### Templates and variables

- Problem set: `problemNumber`, `subject` (problem title), `status`, `impact`, `teamName`, `problemUrl`,
  `recipientName`; SLA events add `dueAt`. Ticket-cause events use the existing `TicketCommon` set (ticket number,
  ticket subject, status, ticket url, recipient name) and nothing from the problem.
- Default Turkish templates seeded by migration; the `ticket_cause_*` texts say only that the cause of the reported
  issue was found and the IT team is applying a fix / that the issue was fixed, with the ticket link.
- Event type labels added to the email templates and deliveries pages (`eventTypes.ts`, `admin.json`).

### UI

- Problem detail: an SLA line ("Bilinen hataya kadar") with the existing SLA badge style (remaining, warned,
  breached, met, cancelled), reusing the ticket SLA badge component if it fits.
- Problem list: SLA badge column / card line.
- Problem report: metric `known_error_sla_compliance` (met ÷ stopped non-cancelled timers, dated by stop time) with
  drill-down and export, like `sla_compliance` for incidents.

### Audit

- `problem_sla.targets_updated`. Timer start/stop is not audited separately (the problem's own status audit covers
  it); warning/breach are recorded as problem history events if the history has a fitting kind (builder's call).

## Decisions made without the owner (marked as my calls)

- No backfill: problems that exist at deploy get no timer; the report counts only problems with a timer. Reason: a
  backfilled timer would mostly start breached and send a burst of breach mail on first monitor run.
- Default targets (above) are proposals; the admin changes them in the page.
- Calendar change and impact change mirror the ticket timer exactly (builder check found the ticket rule).
- An owner given at creation gets the assigned email only, not the team's opened email.
- Employee notice is per linked incident (one requester may get two mails for two incidents); team notices are
  de-duplicated per person.

## Left to the builder

- Exact page placement of "Problem hedefleri", component reuse, history event kinds, index shape, batch sizes,
  and template wording within the rules above.
