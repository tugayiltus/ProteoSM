# ADR-0046: Problem SLA and Email

**Date**: 2026-11-27
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Problems (ADR-0034) had no time target and sent no email. A problem could stay in "Açık" for weeks without anyone being told, and the staff and requesters of the linked incidents never heard that the cause was found. The owner asked for one target that keeps problem work moving (time to known error) and email at the moments that matter, without the complexity of the ticket SLA. Problems are hidden from employees (ADR-0034), so no employee email may carry problem data. The work uses the business calendar (ADR-0018), the transactional outbox (ADR-0015), admin-managed templates (ADR-0016) and the Worker with row leases (ADR-0014).

## Decision

Approach B: a separate, simple problem timer. The ticket SLA is not changed.

- Table `problem_sla_targets`: one row per impact (`High`, `Medium`, `Low`) with business minutes and a warning percent. Admins edit them in the "Problem hedefleri" section of `/admin/sla/policy` through `GET|PUT /api/v1/admin/sla/problem-targets` (one save with `version`, audited). A change applies only to problems created afterwards and to later impact changes.
- Table `problem_sla_timers`: one timer per problem. `CreateProblemHandler` creates it in the same save and copies the current target, so a running timer keeps its own target. It stops at known error or at resolve from open or investigating (`met` or `breached`), and on cancel (`cancelled`, not counted in compliance). An impact change re-targets a running timer; a calendar or holiday change re-plans it like a ticket instance.
- The Worker `SlaMonitor` runs a second pass over open problem timers. A conditional `ExecuteUpdate` on "not yet warned/breached" claims each timer, so two Workers never send twice.
- Six email events: `problem_created`, `problem_assigned`, `problem_sla_warning`, `problem_sla_breached`, `problem_known_error`, `problem_resolved`; and two requester events, `ticket_cause_found` and `ticket_cause_resolved`. They go through the outbox, with recipients resolved at dispatch. The two SLA events have no actor. The `ticket_cause_*` events deliberately do not exclude the actor. The other problem events exclude the actor. The requester email never carries the problem number, title, root cause, workaround or link.
- The SLA badge shows on the problem detail and list. The problem report has the metric `known_error_sla_compliance`.
- No timer is created for problems that exist before the deploy (no backfill).

Commits on `feature/problem-sla`: `66b239b`, `4ea9bc6`, `629d93e`, `654bd41`, `7c2aaf5`, `63c2474`, `eec54a3`, `2c77dcd`, `1b10d81`, `b687672`.

Related: ADR-0014, ADR-0015, ADR-0016, ADR-0018, ADR-0034.

## Alternatives Considered

### Alternative 1: Extend the ticket SLA tables (A)
- **Pros**: One SLA engine and one admin page. `SlaMonitor` and the SLA email path are reused as they are.
- **Cons**: `sla_instances` gets a nullable problem id and the policy matrix gets problem targets. Many queries assume a ticket.
- **Why not**: It changes the core tables and the validation of the working ticket SLA. The risk to the main product path is high for a small feature.

### Alternative 2: No stored timer (C)
- **Pros**: Least code. Due time is computed from `created_at` and the current target; only warned and breached flags are stored.
- **Cons**: A target change rewrites the due time of old problems. The met and breached history is not reliable.
- **Why not**: The problem report needs a stable record of whether each target was met.

## Consequences

### Positive
- The ticket SLA, its tables and its policy matrix stay untouched.
- Running timers keep the target they were created with, so reports stay stable.
- Owners, teams, incident teams and requesters hear about the important problem moments, without leaking problem data to employees.

### Negative
- Warn and breach code is repeated in a second monitor pass instead of being shared with the ticket SLA.
- No backfill: problems that exist at deploy time have no timer, no badge and no compliance data.
- No pause rules and only one metric (time to known error); the fix time is not measured.

### Risks
- The two SLA paths can drift apart; a later change to the ticket warn or breach rules must be checked against the problem pass.
- More email events can add noise. The actor exclusion and one mail per person per event limit this; admins can edit the templates (ADR-0016).
