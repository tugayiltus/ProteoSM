# Service Catalogue + Light CMDB — design

Date: 2026-11-23. Owner: Tugay İLTUŞ. Status: approved by the owner on 2026-11-23. Owner choices: the service is for IT only (an impact view, not an employee catalogue); approach A (one optional `service_id` per record, plus many-to-many service ↔ asset); the service can be set on closed records too.

Builder calls (owner to check): asset link blocked for `Retired`/`Lost`, existing links kept; link write under `SerializationLocks.AssetLifecycle`; `set-service` is separate from the change plan and never voids an approval; any in-scope IT staff sets it on a ticket; reports get a distribution figure only (no service filter or drill-down); search hit key is the service name.

Context: this is the third light-ITIL module. The owner brief of 2026-11-18 set the order: related records, then Problem, then service catalogue + light CMDB, then software/licence assets, light Release and the monitoring webhook. Phase 10 (ADR-0033) added the ticket ↔ change links and central search. Phase 11 (ADR-0034) added problems. `proteone.md` §2 excludes "Tam CMDB ve servis bağımlılık haritası". This module stays inside that exclusion: it has services and their assets, but no dependency map, no discovery and no CI classes. `proteone.md` §2 and §19 are updated with it.

## Intent

When SAP, e-mail or VPN fails, IT today cannot see in one place which devices carry that service, which incidents are open against it, which changes touch it and which problems sit under it. The desk finds out by reading ticket subjects.

Success:

- An IT staff member opens "SAP" and sees its owner team, its assets, its open incidents and requests, its planned and running changes and its open problems.
- The reports group incidents, changes and problems by service.

## Goals

- `services`: name, description, owner team, active flag, managed by admins in `/admin`.
- `service_assets`: many-to-many service ↔ asset ("this server or device carries this service").
- One optional `service_id` on tickets (incident and request), changes and problems. IT staff set and clear it.
- A service detail page in the IT workspace with the impact view.
- A services list in the IT workspace.
- Asset detail shows the services that asset carries.
- Service filter on the ticket, change and problem lists. A "Hizmet" grouping in the reports.
- Services in central search (IT only).
- Pilot seed data.

## Non-goals

- Service dependencies (service → service), CI classes, discovery, relationship types.
- Service status or health ("degraded", "down"). The open incidents show the health.
- Service SLA, service hours, service-level targets. SLA rules stay on priority and request type (unchanged).
- An employee-facing catalogue. "Hizmet Talep Et" stays on request types and categories.
- Routing by service: `service_id` never picks the team, the assignee or the SLA.
- More than one service per record (approach B rejected).

## Anti-goals

- Employees never see services, service fields or service links. This includes their own ticket detail, the portal, e-mails and exports they can run.
- No change to ticket, change or problem lifecycle rules. Setting a service is not a work action that changes status. It follows the existing field-edit rules of each record (version check where the record has one).
- A service never becomes required on any record. Old records stay valid with `service_id` null.

## Approaches (owner picked A)

- **A — one optional `service_id` per record + `service_assets` (chosen).** It is the simplest model for "impact view" and "report by service". Each record counts once per service, so reports do not double count. Foreign keys on both sides. A later move to many services per record stays possible.
- **B — many-to-many link tables per record type** (`service_tickets`, `service_changes`, `service_problems`). This is more flexible, but every list, filter and report has to deal with one record in several services. The UI needs a multi-picker on every record. Rejected as heavier than the "light" brief needs.
- **C — service on the category** (`categories.service_id`, ticket inherits). This adds no field on tickets. But changes and problems have no category, and the category tree (what happened) does not map cleanly to services (what is affected). Rejected.

## Design

### Data

- `services`: `id` (uuid v7), `name`, `normalized_name` (unique, as for teams), `description` (optional, plain text), `owner_team_id` (FK `teams`, required), `is_active`, `created_at`, `updated_at`, `xmin` concurrency. `search_text` (trigram) for central search.
- `service_assets`: `service_id`, `asset_id`, unique pair, `created_by`, `created_at`. An asset can carry many services, and a service can have many assets.
- `tickets.service_id`, `change_requests.service_id`, `problems.service_id`: nullable FK `services`, with an index on each.
- Audit actions `services.created`, `services.updated`, `services.activated`, `services.deactivated`, `services.asset_linked`, `services.asset_unlinked`. They get Turkish texts (`TranslationCoverageTests`).
- Timeline events on ticket, change and problem: `service_set`, `service_cleared` (with old and new service name in `data`).
- `export_jobs`: no new report kind. The `Incidents`, `Requests`, `Changes` and `Problems` report exports get a "Hizmet" column. `ReportScope` already rejects employees.

### Rules

- Only an active service can be set on a record. An inactive service stays on records that already have it, and it is shown with an "Pasif" badge.
- Deactivating a service is allowed while records point at it. Deleting a service is not offered (the same as teams).
- Linking an asset in `Retired` or `Lost` status returns 409 with a new `ErrorCodes` constant and a Turkish text (builder call). An existing link stays when the asset is later retired or lost. The impact view shows it with the asset status badge (builder call).
- The service ↔ asset link write runs under the existing `SerializationLocks.AssetLifecycle` key, so the status check cannot race an asset retire (builder call). The service rename uses its own new key.
- Setting the service is its own small action on each record (`set-service`). It is not part of the change plan edit: it is not in `ChangePlanFingerprint` and never voids an approval (builder call).
- Tickets: any IT staff member who sees the ticket in scope (`TicketScope` load, as for ticket ↔ change links in ADR-0033) can set it, through `TicketWork` with the version check. Changes and problems: through `ChangeWork` / `ProblemWork`, with the same access as the link actions on those records (builder call).
- Closed records (owner choice, 2026-11-23): the service can be set and cleared in every state, also on completed tickets, closed changes and resolved or cancelled problems, so reports can cover history. The `set-service` action skips the lifecycle guards (`ChangeRequest.Edit`, `ChangePlanLocked`, `RequireNotClosed`) but still checks scope, access and version, and writes a `service_set` / `service_cleared` timeline event. It does not touch status, SLA or `completed_at`.
- Hint, not automation: when a ticket's linked assets (`ticket_assets`) carry exactly one active service, the service picker pre-selects that service as a suggestion. The user still saves it. Nothing is set without a user action.

### Access

- `admin`: create, edit, activate and deactivate services, and link and unlink assets, in `/admin` → "Hizmetler".
- `it_staff`: read the service list and details. Set or clear the service on records that they can already edit. Read-only on the service ↔ asset links.
- `employee`: no access. The endpoints return 403. The service fields are left out of employee DTOs.

### API (sketch)

- `GET /api/v1/services` (filter: active, owner team, text), `GET /api/v1/services/{id}`, `GET /api/v1/services/{id}/impact` (asset list, open tickets, active changes, open problems, counts).
- `POST/PUT /api/v1/admin/services`, `POST /api/v1/admin/services/{id}/activate|deactivate`, `POST/DELETE /api/v1/admin/services/{id}/assets/{assetId}`.
- New `PUT /api/v1/tickets/{id}/service`, `PUT /api/v1/changes/{id}/service`, `PUT /api/v1/problems/{id}/service` (body: `serviceId` or null, plus `version` where the record has one). List endpoints take a `serviceId` filter.
- Regenerate the OpenAPI types with `tools/gen-api-types.sh`.

### UI

- IT workspace sidebar: "Hizmetler" (list: name, owner team, asset count, open incident count, active badge).
- Service detail ("SAP"): header with owner team and description. Then four sections: Varlıklar, Açık kayıtlar, Değişiklikler (planned + running), Açık problemler. Each row links to its record, and the breadcrumbs return to the service.
- `/admin` → "Hizmetler": the list and form follow the teams pattern, with an asset link dialog that searches by asset tag.
- Ticket, change and problem detail: a "Hizmet" field with a picker. Lists: a "Hizmet" filter in `ListToolbar` (URL state).
- Asset detail: a "Hizmetler" card (read-only for IT staff, editable for admins).
- Reports: a "Hizmet" distribution figure on the Arızalar, Talepler, Değişiklikler and Problemler tabs, using the existing chart components. No new report filter and no drill-down by service in this module (builder call); the list filters cover "show me SAP's records".
- Central search: services as a result group (IT only). `SearchHit.Key` gets the service name, and the link goes to the service id (builder call).
- All texts are in Turkish in `locales/tr`, and the UI uses Astryx components (`frontend/CLAUDE.md`).

## Left to the builder

- Exact impact query shape and paging (cap each section and link to the filtered list for "tümü").
- Seed: about 8 services (SAP, E-posta, VPN, Dosya sunucusu, Yazıcılar, İnternet, ERP mobil, Telefon). Each gets assets and some existing pilot records.
- Test split: domain rules (active-only set, retired asset link), handler tests for scope and access, frontend tests for the picker suggestion and the impact page, and one E2E path (admin creates a service, IT sets it on an incident, the impact page shows it).
- ADR-0036 records the decision. `proteone.md` §2/§19 are updated in the same change.
