# ADR-0036: Service Catalogue and Light CMDB

**Date**: 2026-11-23
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

When SAP, e-mail or VPN failed, IT could not see in one place which devices carry that service, which incidents and requests are open against it, which changes touch it and which problems sit under it. The desk found out by reading ticket subjects. `tgysm.md` §2 excludes a full CMDB and a service dependency map. The owner chose a light service catalogue as the third light-ITIL module, after the ticket-change links (ADR-0033) and problems (ADR-0034). The owner chose: services are for IT only (an impact view, not an employee catalogue), one optional service per record, and the service can be set on closed records too. Design: `docs/superpowers/specs/2026-11-23-service-catalogue-light-cmdb-design.md`. Related decisions: ADR-0033 (link access through `TicketWork`, central search), ADR-0034 (problems, `ProblemWork`), ADR-0013 (employee visibility), ADR-0022 (report charts).

## Decision

- **Own aggregate and many-to-many asset link.** `services` (name unique on `normalized_name`, optional description, required owner team, active flag, `xmin`, `search_text` for trigram search) and `service_assets` (unique pair, `created_by`, `created_at`). An asset can carry many services. Commit `0d66191`.
- **Approach A: one optional `service_id` per record.** `tickets`, `change_requests` and `problems` each get a nullable foreign key to `services` with an index. A record counts once per service, so the reports do not double count. Commit `0d66191`.
- **Admin writes, IT reads.** Admins create, edit, activate and deactivate services and link and unlink assets in `/admin` → "Hizmetler" (`/api/v1/admin/services`). IT staff read the list, the detail and the impact view (`/api/v1/services`). Employees get 404 on the service endpoints (the spec sketched 403; not found matches the problem and ticket scope rules), and the service field is left out of every employee DTO. There is no delete; deactivation is allowed while records point at the service. Commits `a304d9b`, `bee723d`.
- **Link rules under the asset lock.** Linking a `Retired` or `Lost` asset returns 409 (`ErrorCodes.ServiceAssetNotLinkable`); an existing link stays when the asset is later retired or lost. The link write runs under `SerializationLocks.AssetLifecycle`, so the status check cannot race an asset retire. Commits `a304d9b`, `1205e68`.
- **`set-service` is its own action, in every state.** `PUT /api/v1/tickets/{number}/service`, `PUT /api/v1/changes/{number}/service` and `PUT /api/v1/problems/{number}/service` go through `TicketWork`, `ChangeWork` and `ProblemWork` with the version check, scope and access of each record. Any IT staff member who sees the ticket may set it, as for the ticket-change links (ADR-0033). The action skips the lifecycle guards, so closed records can be tagged for history; it never changes status, SLA or `completed_at`, is not part of `ChangePlanFingerprint` and never voids an approval. Only an active service can be set. It writes a `service_set` or `service_cleared` timeline event and an audit row. Commits `160f3e0`, `a508402`.
- **Hint, not automation.** When the ticket's linked assets carry exactly one active service, the picker pre-selects it as a suggestion; nothing is saved without a user action. The service never routes a ticket, picks a team or changes an SLA. Commit `d66211e`.
- **Impact view, list filters, report and search.** The impact query returns capped sections (assets, open tickets, planned and running changes, open problems) with totals (`7e206d1`); the IT pages `/services` and `/services/{id}` in `1c79fbf` and `0a0adae`; the "Hizmet" field with the picker and the list filters in `d66211e`; a "Hizmet" distribution figure on the incident, request, change and problem report tabs, with no service filter or drill-down (API `4b8d752`, UI `d66211e`); services as an IT-only result group in central search, keyed by name (`9b2c521`, `d13da82`).
- **Pilot data.** The pilot seed creates eight services with an owner team and one to four assets, and sets a service on about 30% of the tickets and on some changes and problems. Its draws come from a random of their own, so the rest of the seed stays deterministic as before.

## Alternatives Considered

### Alternative 1: Approach B — many-to-many link tables per record type
- **Pros**: A record can name every service it touches (`service_tickets`, `service_changes`, `service_problems`).
- **Cons**: Every list, filter and report has to handle one record in several services, and the counts double. Every record detail needs a multi-picker.
- **Why not**: Heavier than the light brief needs. A move from approach A to B stays possible later.

### Alternative 2: Approach C — service on the category
- **Pros**: No new field on tickets; the ticket inherits the service from `categories.service_id`.
- **Cons**: Changes and problems have no category. The category tree describes what happened, not what is affected, so it does not map cleanly to services.
- **Why not**: It covers only tickets and gives wrong answers when one category spans several services.

### Alternative 3: A full CMDB with CI classes and a dependency map
- **Pros**: Service-to-service impact, discovery and relationship types.
- **Cons**: Large data model, discovery tooling and upkeep that a single-company IT team cannot keep current.
- **Why not**: `tgysm.md` §2 excludes it; the owner asked for a light catalogue.

## Consequences

### Positive
- One page per service shows its devices and its open incidents, requests, changes and problems.
- Reports group incidents, requests, changes and problems by service without double counting.
- No change to ticket, change or problem lifecycle rules; old records stay valid with `service_id` null.

### Negative
- A record names only one service; a fault that hits two services is tagged with the main one.
- Services have no status, health, SLA or service hours; the open incidents show the health.
- The service-asset links are kept by hand in `/admin`; nothing discovers them.

### Risks
- The links go stale when devices change and nobody updates them. Mitigation: the impact view shows the asset status, and a retired or lost asset stays visible on the service until an admin removes it.
- Staff may skip the field, so the per-service report undercounts. Mitigation: the picker suggests the service of the linked device.

## Amendment 2026-11-24

The figure "Hizmete göre" shows counts only; it has no links to record lists, as designed. The API keeps the rows behind the figure, because exports and the consistency check of figures use them. The record lists have their own service filter.
