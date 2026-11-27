# Backlog index (2026-11-28)

Ten designs. Design only; none is approved for code. Each needs an owner decision on its "Open decisions" and, where noted, an ADR. Kept decisions: P1-P4 matrix, satisfaction rating, no reopen of closed tickets; full CMDB and an extra portal are separate scope decisions. Workflow ideas compared with the Odoo Helpdesk ITSM Pack; no screens or texts copied.

| # | File | One line |
|---|---|---|
| 1 | `2026-11-28-backlog-service-routing.md` | The service chosen on the form decides the target team; fallback to the intake queue. |
| 2 | `2026-11-28-backlog-sla-escalation.md` | One mail to an authorized, active contact on breach; queued vs delivered shown. |
| 3 | `2026-11-28-backlog-known-error-suggestion.md` | Known errors of the same service on the incident; only published text goes to the reply. |
| 4 | `2026-11-28-backlog-service-sla.md` | Per-service targets and 24/7 mode, with a clear precedence and fallback to the default. |
| 5 | `2026-11-28-backlog-critical-outage.md` | Active outage on the service page with coordinator and updates; linked incidents grouped. |
| 6 | `2026-11-28-backlog-bulk-complete.md` | Preview and partial bulk completion of a problem's linked incidents, with per-row checks. |
| 7 | `2026-11-28-backlog-change-template-review.md` | Change templates and a structured closing review; approval unchanged. |
| 8 | `2026-11-28-backlog-inbound-mail.md` | Shared mailbox, sender and duplicate checks, replies on the right ticket, review queue. |
| 9 | `2026-11-28-backlog-employee-service-options.md` | Employee-facing service choices mapped to request types, internal data hidden. |
| 10 | `2026-11-28-backlog-operations.md` | Real Entra/AD pilot, CI image smoke and rollback drill, off-server backup with RPO/RTO, capacity test. |

## Suggested order and reasons
1. Operations B (CI image smoke and health-before-active): cheap, protects every later release.
2. Operations C (off-server backup, RPO/RTO): go-live blocker, no feature dependency.
3. Operations A (real Entra/AD pilot): go-live blocker; needs owner and tenant access, so start early.
4. Item 9 then item 1: service options give routing its clean input; routing is the largest daily-use win.
5. Item 4 (service SLA): reads the service set by item 1.
6. Item 2 (SLA escalation): small, independent; can run in parallel with item 4.
7. Item 3 (known error suggestion): small, builds on services already on tickets.
8. Item 6 (bulk completion): after 3; saves desk time once problems carry many incidents.
9. Item 5 (critical outage): after 1 and 6, which it shares link patterns with; needs ADR and announcement decision.
10. Item 7 (change template and review): independent; schedule when change volume justifies it.
11. Item 8 (inbound mail): largest and riskiest (security, loops); do after operations is stable, with a real mailbox.
12. Operations D (capacity): run after items 1-4 land, so the measured mix is the real one; repeat before go-live.
