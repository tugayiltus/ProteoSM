# Phase 12 — Service Catalogue + Light CMDB Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admins keep a list of IT services (owner team, carrying assets). IT staff tag incidents, requests, changes and problems with one service, and they see each service's impact (assets, open records, active changes, open problems) on one page and in the reports.

**Architecture:** There is a new `Service` aggregate in `Tgysm.Domain/Services` and a `service_assets` link table. A nullable `service_id` FK goes on `tickets`, `change_requests` and `problems`. The admin CRUD follows the teams feature (`Features/Organization/Teams`). A separate `set-service` action per record type goes through each record's work class. It skips the lifecycle guards but keeps the scope, access and version checks. There is a read-only impact query, plus a `by_service` distribution figure in four report tabs and a service group in search.

**Tech Stack:** .NET 11, EF Core + Npgsql, FluentValidation, Minimal APIs; React 19, TanStack Query, Astryx; xUnit (Microsoft.Testing.Platform), Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-11-23-service-catalogue-light-cmdb-design.md` (approved 2026-11-23). Patterns:
- Teams admin: `Features/Organization/Teams/*`, `Api/Endpoints/Admin/TeamEndpoints.cs`.
- Record work: `Features/Tickets/TicketWork.cs`, `Features/Changes/ChangeWork.cs`, `Features/Problems/ProblemWork.cs`.
- Ticket load for links: `Features/Changes/ChangeTicketLinks.cs`.
- Distribution figure: `Reports/Metrics/ProblemMetrics.cs` (`ByImpact`).
- Search: `Features/Search/Search.cs`.

## Global Constraints

- `services`: `name` ≤ 100 and unique by `normalized_name` (409 `services.name_taken`); `description` ≤ 1000, optional; `owner_team_id` must be an active team (409 `services.owner_team_inactive`); `is_active`; `search_text` with a trigram index; `xmin`. No delete endpoint.
- `service_assets`: unique pair `ux_service_assets_service_asset` (409 `services.asset_link_exists`). An asset in `Retired` or `Lost` status cannot be linked (409 `services.asset_not_linkable`). An existing link stays when the asset later changes status. The link and unlink writes run under `SerializationLocks.AssetLifecycle` (7_003). Create, rename and activate run under the new `SerializationLocks.ServiceCatalog = 7_012`.
- `tickets.service_id`, `change_requests.service_id`, `problems.service_id`: nullable FK with `ON DELETE RESTRICT`, each indexed.
- Setting the service: only an active service (409 `services.inactive`). Clearing (`null`) is always allowed. It is allowed in every record state, open or closed. It never changes the status, the SLA, `completed_at`, the change approval or `ChangePlanFingerprint`. It writes one timeline event (`service_set` / `service_cleared`, with old and new names in `data`) and one audit row (`tickets.service_set`, `changes.service_set`, `problems.service_set`). The same service set again is a no-op: 200, no event.
- Access:
  - Admin: services admin API.
  - IT staff + admin: `GET` services and impact.
  - Tickets: any IT staff member who sees the ticket in `TicketScope` (the same load as `ChangeTicketLinks`), not only workers.
  - Changes: `ChangeScope` visible + the actor can work the change (`ChangeWork` rule).
  - Problems: the `ProblemScope` write rule (admin or owner-team member).
  - Employees: 404 on every service route. Service fields are never in employee DTOs.
- Version: the ticket and problem `set-service` calls take the record `version` (409 `common.concurrency_conflict` on a stale version). The change `set-service` takes the change `version` the same way.
- Impact: each section is capped at `ServiceImpactLimits.PerSection = 20` with a total count:
  - Assets: all linked assets.
  - Open records: tickets whose status is not in `Completed` or `Cancelled`.
  - Changes: `Scheduled`, `Implementing`, `Verifying`, plus `PendingApproval`.
  - Problems: not `resolved` or `cancelled`.
  - Rows the viewer cannot open: number, title, status, `canOpen = false` (ADR-0033 rule).
- Reports: figure key `ProblemMetrics`-style constant `ByService = "by_service"` in `IncidentMetrics`, `RequestMetrics`, `ChangeMetrics` and `ProblemMetrics`. It counts records created in the period, grouped by service name, with null as label key `none`. The `Incidents`, `Requests`, `Changes` and `Problems` export rows get a `service` column (header "Hizmet").
- Search: `SearchHitType.Service`. `Key` is the service id (as articles use the article id). IT staff and admins only. Inactive services are included and show the status "Pasif".
- UI Turkish copy (exact):
  - Menu: "Hizmetler".
  - Fields: "Hizmet", "Sahip ekip", "Açıklama", "Durum".
  - Badges: "Aktif", "Pasif".
  - Detail sections: "Varlıklar", "Açık kayıtlar", "Değişiklikler", "Açık problemler"; "Tümünü göster".
  - Empty value: "Hizmet yok". Suggestion: "Önerilen: {{name}}".
  - Actions: "Yeni hizmet", "Varlık bağla", "Bağı kaldır", "Pasife al", "Aktif et".
  - Report figure: "Hizmete göre". Search group: "Hizmetler".
- Every API change: run `tools/gen-api-types.sh`. `TranslationCoverageTests` (error codes, audit actions, event kinds) and `ExportHeadersCoverageTests` stay green. English code; Turkish only in the locale JSON. No magic numbers or strings.
- Gates per task:
  - Backend tasks: `tools/lint.sh` + focused `dotnet test --solution backend/Tgysm.slnx --filter-class …`.
  - Frontend tasks: `tools/lint.sh` + `cd frontend && npx vitest run <folders>`.
  - Phase end: `tools/test.sh`, `tools/e2e.sh` (stop the dev services, restart them after), `tools/ui-qa.sh`, and a green CI before the merge.
  - The phase needs SDK 11 (`global.json`). Until the owner installs it, run dotnet through the scratchpad SDK (`DOTNET_ROOT` + `PATH`).

## Review Focus

- An IT staff member who is not on the ticket's team sets the service on a completed incident they can see: 200, event written, status and `completed_at` unchanged. Test in Task 3.
- An admin deactivates "SAP" while 30 tickets point at it: the tickets keep it, the picker no longer offers it, and setting it again returns 409 `services.inactive`. Test in Tasks 1 and 3.
- An asset is linked to a service at the same moment it is retired: one of the two wins, and the result is never a link to a retired asset. Test in Task 2 (under `AssetLifecycle`).
- An employee who requested a ticket tagged "SAP": the ticket JSON has no service field, `GET /services/{id}` returns 404, and search has no service group. Tests in Tasks 2, 3 and 6.
- Setting the service on a change in `PendingApproval` does not void the approval or change the plan fingerprint. Test in Task 3.

---

### Task 1: Service domain, link table, record columns and migration

**Files:**
- Create: `backend/src/Tgysm.Domain/Services/Service.cs`, `ServiceAsset.cs`, `ServiceLimits.cs`; `backend/src/Tgysm.Infrastructure/Persistence/Configurations/ServiceConfiguration.cs`, `ServiceAssetConfiguration.cs`; migration `AddServices`.
- Modify:
  - `Ticket.cs`, `ChangeRequest.cs`, `Problem.cs` (each gets `ServiceId` + `SetService`).
  - Their EF configurations.
  - `ErrorCodes.cs`, `AuditActions.cs`, the ticket, change and problem event kinds.
  - `IAppDbContext` + `AppDbContext`, `SerializationLocks` (`ServiceCatalog = 7_012`).
  - `locales/tr/errors.json` and the audit and event locale files.
- Test: `backend/tests/Tgysm.Domain.Tests/Services/ServiceTests.cs`, plus a `SetService` case in the existing ticket, change and problem domain tests.

**Interfaces — Produces:**
- `Service.Create(string name, string? description, Guid ownerTeamId, DateTimeOffset now)`; `Update(name, description, ownerTeamId, now)`; `SetActive(bool active, DateTimeOffset now)`; `IsActive`, `NormalizedName`, `SearchText`.
- `ServiceAsset.Create(Guid serviceId, Asset asset, Guid by, DateTimeOffset now)`. It throws `services.asset_not_linkable` when `asset.Status` is `Retired` or `Lost`.
- `Ticket.SetService(Guid? serviceId, DateTimeOffset now) : bool changed`; the same on `ChangeRequest` and `Problem`. No status guard. It touches `UpdatedAt`. It returns false when the value is unchanged.
- Error codes: `services.name_taken`, `services.owner_team_inactive`, `services.inactive`, `services.asset_not_linkable`, `services.asset_link_exists`.
- Audit: `services.created`, `services.updated`, `services.activated`, `services.deactivated`, `services.asset_linked`, `services.asset_unlinked`, `tickets.service_set`, `changes.service_set`, `problems.service_set`.
- Event kinds: `ServiceSet`, `ServiceCleared` on the ticket, change and problem events (snake_case `service_set`, `service_cleared`). Widen the event kind CHECKs.

- [ ] **Step 1:** Write the domain tests:
  - `Create` trims and normalizes the name; it rejects a blank name and a name over 100 characters.
  - `ServiceAsset.Create` throws for `Retired` and for `Lost`, and passes for `InStock`, `Assigned` and `Maintenance`.
  - `Ticket.SetService` on a `Completed` ticket changes `ServiceId`, keeps `Status` and `CompletedAt`, and returns true. The same value again returns false.
  - `ChangeRequest.SetService` on a `PendingApproval` change leaves the plan fingerprint equal.
  - `Problem.SetService` on a `Resolved` problem returns true.
- [ ] **Step 2:** `dotnet test --solution backend/Tgysm.slnx --filter-class "*ServiceTests"`. Expected: FAIL (types missing).
- [ ] **Step 3:** Implement the entities, configurations, DbSets, codes and Turkish texts. Run `tools/add-migration.sh AddServices`. Check that the migration creates only `services` and `service_assets`, the three FK columns with their indexes, and the widened CHECKs.
- [ ] **Step 4:** `tools/lint.sh` and the focused tests (domain + `TranslationCoverageTests`) exit 0. Commit `feat(services): service aggregate, asset links and record columns`.

### Task 2: Service admin API and IT read API

**Files:**
- Create: `backend/src/Tgysm.Application/Features/Services/` `CreateService.cs`, `UpdateService.cs`, `SetServiceActive.cs`, `LinkServiceAsset.cs`, `UnlinkServiceAsset.cs`, `ListServices.cs`, `GetService.cs`, `ServiceDtos.cs`; `backend/src/Tgysm.Api/Endpoints/Admin/ServiceEndpoints.cs`, `backend/src/Tgysm.Api/Endpoints/Services/ServiceEndpoints.cs`.
- Modify: endpoint registration, DI registration.
- Test: `backend/tests/Tgysm.Api.Tests/Services/ServiceAdminEndpointTests.cs`, `ServiceReadEndpointTests.cs`.

**Interfaces — Produces:**
- `POST /api/v1/admin/services` `{ name, description, ownerTeamId }` returns 201 `ServiceDetail`. `PUT /api/v1/admin/services/{id}` `{ name, description, ownerTeamId, version }`. `POST /api/v1/admin/services/{id}/activate` and `/deactivate`.
- `POST /api/v1/admin/services/{id}/assets` `{ assetId }` returns 201. `DELETE /api/v1/admin/services/{id}/assets/{assetId}` returns 204.
- `GET /api/v1/services?active=&teamId=&q=&page=` returns `Paged<ServiceListItem(Guid Id, string Name, Guid OwnerTeamId, string OwnerTeamName, bool IsActive, int AssetCount, int OpenTicketCount)>`.
- `GET /api/v1/services/{id}` returns `ServiceDetail(Guid Id, string Name, string? Description, Guid OwnerTeamId, string OwnerTeamName, bool IsActive, uint Version)`.
- `GET /api/v1/assets/{tag}/services` returns `IReadOnlyList<ServiceRef(Guid Id, string Name, bool IsActive)>`.

- [ ] **Step 1:** Write the endpoint tests:
  - Admin create → 201. Duplicate name in other case/Turkish İ → 409 `services.name_taken`. Inactive owner team → 409 `services.owner_team_inactive`.
  - IT staff on the admin routes → 403. Employee on `GET /services` and `/services/{id}` → 404.
  - Link a retired asset → 409 `services.asset_not_linkable`. Link twice → 409 `services.asset_link_exists`.
  - Parallel link + retire of one asset → the end state never holds a link to a retired asset.
  - Deactivate → the list `active=true` excludes the service.
- [ ] **Step 2:** Run with `--filter-class "*Service*EndpointTests"`. Expected: FAIL.
- [ ] **Step 3:** Implement the handlers, validators (`internal sealed`) and endpoints, following the teams feature. Run `tools/gen-api-types.sh`.
- [ ] **Step 4:** Lint and the focused tests exit 0. Commit `feat(services): admin and read API`.

### Task 3: Set service on tickets, changes and problems; list filters; DTO fields

**Files:**
- Create: `Features/Tickets/SetTicketService.cs`, `Features/Changes/SetChangeService.cs`, `Features/Problems/SetProblemService.cs`.
- Modify:
  - The ticket, change and problem endpoints and list queries (`serviceId` filter).
  - The IT detail DTOs (`ServiceRef? Service`); the employee ticket DTOs stay unchanged.
  - The history readers (render `service_set` / `service_cleared`).
- Test: `backend/tests/Tgysm.Api.Tests/Services/SetServiceEndpointTests.cs`.

**Interfaces:**
- Consumes: `Ticket/ChangeRequest/Problem.SetService` (Task 1), `ServiceRef` (Task 2).
- Produces:
  - `PUT /api/v1/tickets/{number}/service` `{ serviceId: Guid?, version }`, `PUT /api/v1/changes/{number}/service` `{ serviceId, version }` and `PUT /api/v1/problems/{number}/service` `{ serviceId, version }`. Each returns the record detail.
  - The list endpoints take `serviceId`.
  - Detail DTO field `service: ServiceRef | null`.

- [ ] **Step 1:** Write the endpoint tests:
  - IT staff not on the incident's team, ticket `Completed` → 200, `service` set, status and `completedAt` unchanged, one `service_set` event, one audit row.
  - The same call again → 200, no second event.
  - Inactive service → 409 `services.inactive`. Clear → 200 and a `service_cleared` event.
  - Stale version → 409 `common.concurrency_conflict`.
  - Change in `PendingApproval` → approval rows and status unchanged.
  - Problem `Resolved` by an owner-team member → 200. A non-owner-team member on the problem → 403.
  - Employee requester `GET /tickets/{n}` → JSON has no `service` property. `PUT …/service` → 404.
  - `GET /tickets?serviceId=` returns only tagged tickets.
- [ ] **Step 2:** Run with `--filter-class "*SetServiceEndpointTests"`. Expected: FAIL.
- [ ] **Step 3:** Implement the handlers through `TicketWork` (scope load as in `ChangeTicketLinks`, version check, one save), `ChangeWork` and `ProblemWork` without the lifecycle guards. Add the list filters and the DTO fields. Run `tools/gen-api-types.sh`.
- [ ] **Step 4:** Lint and the focused tests, plus the existing ticket, change and problem endpoint tests, exit 0. Commit `feat(services): set service on tickets, changes and problems`.

### Task 4: Service impact query

**Files:**
- Create: `Features/Services/GetServiceImpact.cs`, `ServiceImpactLimits.cs`.
- Modify: `Api/Endpoints/Services/ServiceEndpoints.cs`.
- Test: `backend/tests/Tgysm.Api.Tests/Services/ServiceImpactEndpointTests.cs`.

**Interfaces — Produces:** `GET /api/v1/services/{id}/impact` returns `ServiceImpact(Section<ImpactAsset> Assets, Section<ImpactRecord> Tickets, Section<ImpactRecord> Changes, Section<ImpactRecord> Problems)`:
- `Section<T>(IReadOnlyList<T> Items, int Total)`.
- `ImpactAsset(string Tag, string Name, string Status)`.
- `ImpactRecord(string Number, string Title, string Status, bool CanOpen)`.

- [ ] **Step 1:** Write the tests:
  - The sections hold only the statuses listed in Global Constraints. A completed ticket and a closed change are absent.
  - With 25 open tickets: 20 items, `Total = 25`.
  - A ticket outside the viewer's scope: number, title and status, `canOpen = false`, no other fields.
  - A retired linked asset is listed with status `retired`.
  - Employee → 404.
- [ ] **Step 2:** Run the focused tests. Expected: FAIL.
- [ ] **Step 3:** Implement with `AsNoTracking` queries per section (count + top 20 by `created_at` desc; for changes, by the window start). Run `tools/gen-api-types.sh`.
- [ ] **Step 4:** Lint and the tests exit 0. Commit `feat(services): service impact view API`.

### Task 5: Reports — `by_service` figure and export column

**Files:**
- Modify: `Reports/Metrics/IncidentMetrics.cs`, `RequestMetrics.cs`, `ChangeMetrics.cs`, `ProblemMetrics.cs`, `ReportExportRows.cs`, the export header names, `ReportCatalog.cs` (if figures are listed there), `locales/tr/reports.json`.
- Test: the existing metric tests per report (one new case each) and `ExportHeadersCoverageTests`.

**Interfaces — Produces:** figure `by_service` (Distribution) on `incidents`, `requests`, `changes` and `problems`; buckets keyed by the service name; null is the `none` key with the label "Hizmet yok". The export column is `service`.

- [ ] **Step 1:** Write the tests: two incidents on "SAP", one without a service, one on "VPN" outside the period → buckets `SAP: 2`, `none: 1`. Do the same for the other three reports. The export row has `service` = "SAP" or empty.
- [ ] **Step 2:** Run the focused metric tests. Expected: FAIL.
- [ ] **Step 3:** Implement following `ProblemMetrics.ByImpact`.
- [ ] **Step 4:** Lint and the tests exit 0. Commit `feat(reports): records by service`.

### Task 6: Central search — services

**Files:**
- Modify: `Features/Search/Search.cs` (`SearchHitType.Service`, the query on `services.search_text` with `LikePattern.Containing`), `locales/tr` search texts.
- Test: the existing search tests (`SearchEndpointTests`): a new case each for IT and employee.

- [ ] **Step 1:** Write the tests: IT `q=sap` → a `service` hit with `key` = id. Inactive → status "inactive". Employee → no service hits.
- [ ] **Step 2:** Run. Expected: FAIL.
- [ ] **Step 3:** Implement within `SearchLimits.PerType`.
- [ ] **Step 4:** Lint and the tests exit 0. Commit `feat(search): services in central search`.

### Task 7: Frontend — `/admin` Hizmetler and the asset services card

**Files:**
- Create: `frontend/src/features/admin/services/` (`ServicesAdminPage.tsx`, `ServiceFormDialog.tsx`, `ServiceAssetsDialog.tsx`, `api.ts`, tests).
- Modify: admin navigation + route metadata (breadcrumbs), the asset detail (a "Hizmetler" card: read-only for IT, link/unlink for admin), `locales/tr/*.json`, `vite.config.ts` (only if a new feature folder group is needed).
- Test: `ServicesAdminPage.test.tsx`, `ServiceAssetsDialog.test.tsx`, the asset detail test case.

- [ ] **Step 1:** Write the tests:
  - The list shows name, owner team, the Aktif/Pasif badge and the asset count.
  - Create with a duplicate name shows the Turkish `services.name_taken` text on the field.
  - "Pasife al" calls deactivate.
  - The asset link dialog searches by tag and shows the `services.asset_not_linkable` text.
  - The asset detail card has no edit buttons for IT staff.
- [ ] **Step 2:** `cd frontend && npx vitest run src/features/admin/services src/features/assets`. Expected: FAIL.
- [ ] **Step 3:** Implement with the teams admin page pattern, `src/shared/form`, `src/shared/list` and Astryx components (check them with `npx astryx component <Name>`).
- [ ] **Step 4:** Lint and vitest exit 0. Commit `feat(services): admin services page and asset services card`.

### Task 8: Frontend — IT Hizmetler list and service detail with impact

**Files:**
- Create: `frontend/src/features/services/` (`ServicesPage.tsx`, `ServiceDetailPage.tsx`, `ImpactSection.tsx`, `api.ts`, tests).
- Modify: the sidebar ("Hizmetler" for IT staff and admins), routes, route metadata, `locales/tr`.
- Test: `ServicesPage.test.tsx`, `ServiceDetailPage.test.tsx`.

- [ ] **Step 1:** Write the tests:
  - The list filters by text, team and active through the URL state.
  - The detail renders the four sections with their counts.
  - When `total > items`, it shows "Tümünü göster", which links to the filtered list (`/tickets?serviceId=…`, `/changes?serviceId=…`, `/problems?serviceId=…`, or the asset list).
  - A `canOpen = false` row has no link.
  - The breadcrumbs from a record opened here return to the service.
- [ ] **Step 2:** Run vitest on `src/features/services`. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Lint and vitest exit 0. Commit `feat(services): services list and impact page`.

### Task 9: Frontend — service picker on records, list filters, report figure, search group

**Files:**
- Create: `frontend/src/features/services/ServicePicker.tsx`, `ServiceField.tsx` (+ tests).
- Modify:
  - Ticket, change and problem detail (a "Hizmet" field, IT views only).
  - Ticket, change and problem list toolbars (a "Hizmet" filter).
  - Reports tabs (a "Hizmete göre" figure).
  - The search results page (a "Hizmetler" group, linking to `/services/{id}`).
- Test: `ServiceField.test.tsx` plus one case each in the existing list, report and search tests.

- [ ] **Step 1:** Write the tests:
  - The picker offers only active services.
  - When the ticket's linked assets carry exactly one active service, the picker opens with that service pre-selected and the hint "Önerilen: SAP". Nothing is saved until the user confirms.
  - With two candidate services there is no pre-selection.
  - Clear sends `serviceId: null`.
  - A 409 `services.inactive` shows the Turkish text.
  - The employee ticket view has no "Hizmet" field.
  - The report tab renders the `by_service` figure with the "Hizmet yok" label.
- [ ] **Step 2:** Run vitest on the touched folders. Expected: FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Lint and vitest exit 0. Commit `feat(services): service field, filters, report figure and search group`.

### Task 10: Pilot seed, E2E, ADR, requirements and status

**Files:**
- Modify: the pilot seed (`Features/Pilot/*`).
- Create: `frontend/e2e/services.spec.ts`, `docs/adr/0036-service-catalogue-light-cmdb.md`.
- Modify: `docs/adr/README.md`, `tgysm.md` §2 (the exclusion line keeps the "full CMDB / dependency map" exclusion and names the light catalogue as in scope) and §19 (roadmap row), `docs/status.md`.
- Test: the pilot seed tests (counts), E2E.

- [ ] **Step 1:** Seed 8 services (SAP, E-posta, VPN, Dosya sunucusu, Yazıcılar, İnternet, ERP mobil, Telefon). Each gets an owner team and 1–4 assets. About 30% of the pilot incidents and requests and some changes and problems get a service. Extend the seed count test.
- [ ] **Step 2:** E2E (data through the admin API, `frontend/e2e/world.ts`): admin creates "SAP" and links an asset; IT staff sets "SAP" on an incident; the service detail shows the incident under "Açık kayıtlar"; the employee view of the same ticket shows no "Hizmet".
- [ ] **Step 3:** Write ADR-0036 from the template (status `accepted`, Deciders Tugay İLTUŞ, approaches A/B/C as in the spec, related ADR-0033/0034) and add its index row. Update `tgysm.md` §2/§19 and `docs/status.md`. Run `tools/check-doc-paths.sh`.
- [ ] **Step 4:** Phase end, in one pass:
  - `tools/test.sh`.
  - `tools/e2e.sh`: stop the dev services first, then `tools/dev-service.sh start`.
  - `tools/ui-qa.sh`.

  Fix what fails. Commit `test(services): pilot data and E2E; docs: ADR-0036`.
