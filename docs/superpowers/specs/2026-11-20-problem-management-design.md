# Light Problem Management — design

Date: 2026-11-20. Owner: Tugay İLTUŞ. Status: draft, waits for owner approval. Owner choices so far: approach A, four-step lifecycle, "Makale oluştur" as internal draft, incident links by any IT staff who works the incident.

Context: second light-ITIL module (owner brief 2026-11-18: related records → Problem → service catalogue + light CMDB → software/licence assets → light Release → monitoring webhook). Phase 10 (ADR-0033) added ticket ↔ change links and central search. `tgysm.md` §2 still lists "Full Problem Management" as out of scope; this module adds a light version and updates §2/§19 with it.

## Intent

The same fault comes back in many incidents (VPN drops every Monday, one printer model jams). Today each incident is fixed alone; the shared cause, the workaround and the permanent fix live only in heads or in single tickets' `root_cause` fields. A problem record holds that shared cause once: many incidents point to it, the desk reads the workaround from it, and the change that fixes it is linked to it. When the cause is known, the knowledge goes to the knowledge base as a "known error" article.

Success: desk staff opening a repeat incident see "this is a known problem, workaround: …"; the report shows how many known errors are open and how long problems take to resolve.

## Goals

- `PRB-YYYY-NNNNNN` problem records owned by a team, worked by IT staff.
- Four-step lifecycle (owner choice): Açık → İnceleniyor → Bilinen hata → Çözüldü, plus İptal.
- Root cause and workaround on the problem; resolution note on close.
- Links: incidents ↔ problem, problem ↔ fixing change.
- "Makale oluştur": an internal draft knowledge article built from the problem (owner choice), linked to the problem.
- Problem panel on the incident, problems in central search, a problems report.

## Non-goals

- Problem SLA, problem emails, proactive trend analysis, CAB, multi-stage review.
- Attachments and threaded comments on problems (notes as history events, like tasks).
- Reopening a resolved problem (a recurrence opens a new problem; see Builder notes).
- Service or CI fields on the problem: the service catalogue is the next module and adds them.

## Anti-goals

- No change to incident rules: a link never changes a ticket's status, SLA or version rules beyond the existing "link touches `updated_at`" convention (ADR-0033).
- Employees never see problems, problem links or problem fields.
- No ticket-kind reuse (approach B rejected): problems are not tickets.

## Approaches (owner picked A)

- **A — own aggregate + typed link tables (chosen).** `problems`, `problem_tickets`, `problem_changes`, same pattern as tasks and changes (ADR-0031): counter kind, scope class, work class, audit, events. Real foreign keys, simple report joins, follows ADR-0033's "one typed table per pair".
- **B — `tickets.kind = problem`.** Comments, files and timeline for free, but tickets are visible to employees, carry SLA, requester and approval rules; every ticket rule would need a "not a problem" exception. Rejected.
- **C — generic `record_links`.** One table for all pairs, but no foreign keys on both sides, per-pair rules move to code, and Phase 10's working `change_ticket_links` would be migrated. Rejected.

## Design

### Data

- `problems`: `number` (unique, `PRB-YYYY-NNNNNN`, counter kind `problem` in `ticket_counters`), `title` + `search_text` (trigram), `description` (symptom), `impact` (`low`, `medium`, `high`), `owner_team_id`, `owner_id` (optional, active member of the owner team), `status` (`open`, `investigating`, `known_error`, `resolved`, `cancelled`; CHECK), `root_cause`, `workaround`, `resolution`, `cancel_reason`, `article_id` (optional FK to `knowledge_articles`, unique when set), `created_by`, timestamps (`known_error_at`, `resolved_at`, `closed_at`), `xmin`.
- `problem_tickets`: `problem_id`, `ticket_id` **unique** (an incident belongs to at most one problem), `created_by`, `created_at`. Incidents only (requests 409).
- `problem_changes`: `problem_id`, `change_id`, unique pair, `created_by`, `created_at`. Meaning: the change fixes or works around the problem.
- `problem_events`: append-only history (`kind`, actor, `data` jsonb), notes as `commented` events (task pattern).
- Audit actions `problems.*`; event kind checks extended for the ticket and change timelines (`problem_linked`, `problem_unlinked`).
- `export_jobs` report CHECK widened for `problems` (constraint-only migration, as in Phase 9).

### Lifecycle

| From | To | Rule |
|---|---|---|
| `open` | `investigating` | Start ("İncelemeye al"). |
| `open`, `investigating` | `known_error` | Root cause and workaround both filled. |
| `open`, `investigating`, `known_error` | `resolved` | Resolution note required. |
| `open`, `investigating`, `known_error` | `cancelled` | Reason required (e.g. duplicate). |

Root cause, workaround, title, description and impact stay editable until `resolved`/`cancelled`. Concurrency: `xmin`, 409 `concurrency.conflict`. State changes and link writes for one problem run under `SerializationLocks` (new key, global like `ChangeTicketLink`, or per-problem — builder's call, documented).

### Who

- Employees: 404 on every problem route; no problem data on tickets.
- Read: **every active IT staff member and admin** sees every problem (my call: a problem crosses teams and the desk must read its workaround; no new disclosure rule needed).
- Write on the problem (edit, state changes, change links, notes, "Makale oluştur", transfer, owner): admins and members of the owner team. Others get 403 `common.forbidden` (they can see the record, so 404 would be wrong).
- Incident links (owner decision 2026-11-20): any IT staff member or admin who may work on the incident (admin or member of the incident's team, the `TicketWork` rule) links it to any non-closed problem and removes that link. Owner-team members and admins of the problem may also link and unlink any incident visible to them. Reason: the desk sees the repeat incident and is rarely in the problem's owner team.
- Create: any IT staff member or admin. The owner team must be one of the creator's teams (admins: any team), so the creator can always work the new problem; "Problem oluştur" from an incident therefore always succeeds in linking it (my call).
- Owner change (my call, task pattern): `POST /problems/{number}/transfer` (`teamId`, reason; clears `owner_id`) and `POST /problems/{number}/owner` (`ownerId`, active member of the owner team, or null). Both audited and in history.
- Linking an incident: the incident must be visible to the actor (`TicketScope.Visible`), else 404. Linking to a `resolved` or `cancelled` problem: 409. An incident already linked to another problem: 409 (remove first). Linking a change: the change must be visible to the actor (`ChangeScope.Visible`), else 404. On the problem page linked rows the viewer cannot open show number, title, status (same `canOpen` pattern as ADR-0033).

### Known error article

"Makale oluştur" (status `known_error` or `resolved`, no `article_id` yet): creates a `draft`, `internal` article through the existing article create path. Title "Bilinen hata: <problem title>". Body Markdown with headings Belirti / Kök neden / Geçici çözüm (and Kalıcı çözüm when resolved) and the problem number. Sets `article_id`. The problem shows the linked article afterwards; publishing and visibility follow the normal article flow (ADR-0019). Later problem edits do not rewrite the article.

### API (IT staff, admin; employees 404)

- `POST /problems`, `GET /problems` (queues `mine` (I own), `team`, `open` (not resolved/cancelled), `known_errors`; `status`, `impact`, `search`, paging), `GET /problems/{number}`, `PUT /problems/{number}`, `GET /problems/{number}/timeline`, `POST /problems/{number}/notes`.
- `POST /problems/{number}/start`, `/known-error`, `/resolve`, `/cancel` with `version`.
- `GET|POST /problems/{number}/tickets`, `DELETE /problems/{number}/tickets/{ticketNumber}`; same for `/changes`.
- `POST /problems/{number}/article`.
- `GET /tickets/{number}/problem` (the incident's problem or 204/empty), `GET /changes/{number}/problems`.
- New error codes in `ErrorCodes` + `errors.json`; `tools/gen-api-types.sh`.

### UI

- Sidebar IT operasyonu: "Problemler" → `/problems` (list with toolbar filters in the URL, "Yeni problem"), `/problems/:number` (status, impact, owner; Belirti, Kök neden, Geçici çözüm, Kalıcı çözüm; one primary action per state; "İlgili arızalar" and "Düzelten değişiklikler" panels with add/remove; "Bilinen hata makalesi" box with "Makale oluştur" or the article link; history with notes).
- Incident detail (IT staff): "Problem" card. Linked: number, status, workaround text, link. Not linked: "Probleme bağla" (by number) and "Problem oluştur" (opens the create form prefilled with title and description, links the incident on save).
- Change detail: "İlgili problemler" panel (read-only list; links are managed on the problem).
- Central search: new group "Problemler"; exact `PRB-` number jump.
- İş listem: card "Ekibimin problemleri" (open ones of my teams).

### Report

New report tab "Problemler" (`from`, `to`, `teamId` = owner team). Scope `all` for IT staff and admins alike, because every IT staff member reads every problem (my call; the change/task reports narrow by team only because their records are team-scoped):
- `known_errors_open` — snapshot of problems in `known_error`.
- `resolved` — problems resolved in the period.
- `resolution_time` — creation to `resolved_at`, average + median (duration figure pattern).
- `by_impact` — problems created in the period by impact.
- `incidents_per_problem` — distribution: problems created in the period by linked-incident count buckets (builder picks buckets, e.g. 1, 2–5, 6+).
Drill-down and Excel export like the other reports.

### Other

- Pilot seed: a few problems per state with linked incidents and one fixing change.
- Perf: problem list and search rows in `tools/perf-measure.sh`.
- ADR-0034; `tgysm.md` §2/§19 update; E2E for create → link incident → known error → article → resolve.

## Builder notes (left to the builder)

- Exact lock key granularity, migration names, DTO shapes, Turkish texts (follow existing tone).
- Report bucket edges for `incidents_per_problem`.
- Recurrence after resolve: no reopen in v1; the UI on a resolved problem may offer "Yeni problem aç" prefilled — optional.
