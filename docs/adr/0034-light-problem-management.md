# ADR-0034: Light Problem Management

**Date**: 2026-11-22
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The same fault came back in many incidents (a VPN that drops every Monday, one printer model that jams). Each incident was fixed alone, and the shared cause, the workaround and the permanent fix lived only in single tickets or in people's heads. `proteone.md` §2 listed full Problem Management as out of scope. The owner chose a light version as the second light-ITIL module after ADR-0033. Design: `docs/superpowers/specs/2026-11-20-problem-management-design.md`. Related decisions: ADR-0031 (tasks and changes as separate records), ADR-0033 (typed link tables, disclosure of linked rows, link touches `updated_at`), ADR-0019 (knowledge article flow), ADR-0013 (employee visibility).

## Decision

- **Own aggregate.** A problem (`PRB-YYYY-NNNNNN`, counter kind `problem`) is its own record with the statuses `open`, `investigating`, `known_error`, `resolved` and `cancelled`. There is no reopen: a recurrence opens a new problem. Commits `9e1bc0f`, `d63f53c`.
- **Typed link tables.** `problem_tickets` (unique on `ticket_id`: one problem per incident; incidents only) and `problem_changes` (unique pair), each with real foreign keys, as in ADR-0033. Links touch the ticket's `updated_at` but never its status, SLA or version. Commit `b43393e`.
- **One global link lock.** All link writes and all state changes that can close a problem run under one `SerializationLocks.ProblemLink` key. A per-problem key does not stop the cross-problem race "one incident, two problems"; the unique index would answer one of the two calls with a 500. One key is cheap because link writes are rare. State changes take the same key so that no link lands on a problem that is being resolved or cancelled. Commits `b43393e`, `e2a43f3`.
- **Who may do what.** Every active IT staff and admin reads every problem; employees get 404 everywhere. Writes on the problem need an admin or a member of the owner team. An incident may also be linked by its own team (the `TicketWork` rule), so the desk can attach a repeat incident to another team's problem without edit rights. Removing a change link needs only the problem write rule, because the change side has no work right to check. Commits `d63f53c`, `b43393e`.
- **Known error article in one transaction.** "Makale oluştur" runs `CreateArticleHandler` inside one transaction together with the problem update, so a double click or two tabs make one article and a 409 for the second call. The article is an internal draft; later edits to the problem do not rewrite it. Publishing follows ADR-0019. Commit `f5ec48f`.
- **`team_open` queue.** The workspace card "Ekibimin problemleri" counts only open problems of the viewer's teams (`open`, `investigating`, `known_error`) through the queue `team_open`; the older queue `team` keeps all statuses. Without it the card counted closed problems nobody needs to work. Commit `a768d94`.
- **Report, search and UI.** The report `problems` (known errors open, resolved, resolution time, by impact, incidents per problem) in `9de03e5`; problems in central search in `09aaae8`; list and detail pages in `c95ca77`; problem panels on incident and change, search and report tab in `c194ec5` (follow-up `c5d2da4`).

## Alternatives Considered

### Alternative 1: `tickets.kind = problem`
- **Pros**: Comments, files and timeline come for free.
- **Cons**: Tickets are visible to employees and carry SLA, requester and approval rules. Every ticket rule would need a "not a problem" exception.
- **Why not**: Problems are internal IT records without requester, SLA or approval. A separate aggregate follows the pattern of tasks and changes (ADR-0031) and keeps the incident rules untouched.

### Alternative 2: Generic `record_links` table
- **Pros**: One table for every record pair; later pairs need no migration.
- **Cons**: No foreign keys on both sides; per-pair rules (incidents only, one problem per incident) move to code; the working `change_ticket_links` of ADR-0033 would have to be migrated.
- **Why not**: Typed tables keep foreign keys, simple unique indexes and plain report joins. The same reasoning as ADR-0033 applies.

## Consequences

### Positive
- Desk staff see "known problem, workaround: ..." on a repeat incident; the shared cause is written once.
- Incident, ticket and SLA rules did not change; employees see nothing new.
- The one-problem-per-incident rule is enforced by an index and the link lock; races end in 201 or 409, never 500.

### Negative
- A global lock serialises all link writes and problem state changes, also for unrelated problems.
- No reopen, no problem SLA, no emails, no trend analysis; full Problem Management stays out of scope.
- The article is a copy: it can drift from the problem after later edits.

### Risks
- If link or state-change volume grows a lot, the global key becomes a bottleneck; a key per incident plus per problem would need a lock-order rule.
- Every IT staff reads every problem, so a team-private problem is not possible; the owner accepted this for a single-company tool.
