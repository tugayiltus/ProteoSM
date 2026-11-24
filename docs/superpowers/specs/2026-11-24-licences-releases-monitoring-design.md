# Licences, Light Release and Monitoring Webhook — design

Date: 2026-11-24. Owner: Tugay İLTUŞ. Status: draft, awaiting owner approval.

Order: deferred notes, licences, releases, monitoring. Reviews and full tests run once, after all code. Each module gets its ADR (0039–0041) and updates `proteone.md` §2 and §19.

## 0. Deferred notes (owner chose all four groups)

- Services: the impact lists and their "Tümünü göster" links show the same records; the asset list gets a service filter, so the 20 limit of the admin dialog no longer hides assets; deactivation and set-service run under one lock.
- Search, tasks, audit: a short search shows the hint; a task the viewer cannot open is plain text; an update without a change writes no audit row.
- Accessibility: focus moves to the first field with a server error; row buttons name their record; the comment tabs follow the tabs pattern with the keyboard.
- Tests and helpers: race test for the article link, a test for the approvals list, shared helpers for duplicated code. No behavior change.
- The remaining lines of `SECURITY.md` are compared with the code.

## 1. Licences

Intent: IT sees which software the company may use, how many seats are used and by whom, and what ends soon.

Choices of the owner: own licence records (not an asset type); IT and admins manage; a full licence refuses an assignment; employees see only their own software; email before the end; keys are stored; a report tab.

Data:
- `software`: name, vendor, unique together (normalized), active flag.
- `licences`: software, name, kind `per_user` or `per_device`, quantity (1–100,000), owner team, `expires_on` (optional), notes, encrypted key (optional), `expiry_notified_for`, `xmin`.
- `licence_assignments`: licence, user or asset (exactly one, matching the kind), assigned and released time and actor. One active assignment per licence and target (partial unique index).

Rules:
- Assign and quantity changes run under a new lock (7_013). No free seat: 409. Quantity below the used seats: 409. Expired licence: no new assignment (409); existing ones stay.
- Only an active user, or an asset that is not retired or lost, can be assigned. A later deactivation or retirement keeps the assignment and marks it, so IT releases it.
- Key: protected with Data Protection under the new purpose `Proteone.LicenceKey`. Only an admin sets or shows it. Each showing writes an audit row. It never appears in lists, exports, search, audit values or logs.
- Email `licence_expiring`: a daily Worker job writes one event per licence and end date, 30 days before the end (or at once when less is left), to the active members of the owner team. Deduplicated by `expiry_notified_for`.
- Access: IT and admins read and write; employees 404 on licence routes; `GET /me/software` returns the names of their own software only.

UI: "Lisanslar" list and detail in the IT workspace (seats, assignments, end date); "Yazılımlarım" for employees; a card on the asset page; software in central search (IT); report tab "Lisanslar": use per licence, ending within 30/60/90 days, full licences, Excel.

Not included: purchase, contracts, cost, discovery of installed software, approval.

## 2. Light Release

Intent: changes that go live together have one record. Deployment stays the implementation phase of each change; ADR-0032 is not touched.

- `REL-YYYY-NNNNNN`: title, description, owner team, owner, target window, notes. States `planned` → `in_progress` → `completed`, or `cancelled` with a reason. No approval.
- `change_requests.release_id`: a change is in at most one release. It never changes the plan fingerprint, approval or state of the change.
- Add or remove a change while the release is planned or in progress. An added change must be neither closed nor cancelled, and the actor must be able to work it. Completion needs every change closed or cancelled; a cancelled release releases its changes.
- Read: all IT staff and admins; rows the viewer cannot open follow the ADR-0033 rule. Write: admin or owner team. Employees 404.
- UI: "Sürümler" list and detail, a "Sürüm" field on the change, releases in central search. History and audit as for problems. No report, no calendar entry.

## 3. Monitoring webhook

Intent: a monitoring tool opens an incident without a person, and cannot be used to flood or forge.

Sources (`/admin` → "İzleme kaynakları", admin only): name, team, requester (an active IT user in whose name the incident is opened), optional service and category, impact and urgency, active flag. The secret (32 random bytes) is shown once, stored with Data Protection (`Proteone.MonitoringSecret`), and can be replaced; the old one stops at once.

Endpoint `POST /api/v1/monitoring/{sourceId}/events`, anonymous, outside CSRF for this exact path only, at most 16 KB JSON: `eventId`, `alertKey`, `state` (`firing` or `resolved`), `title`, `description`.
- `X-Proteone-Timestamp` (Unix seconds, at most 5 minutes off) and `X-Proteone-Signature` = HMAC-SHA256 of `timestamp.body`, compared in constant time. Unknown or inactive source, bad time and bad signature all give the same 401 without a body difference.
- Replay: (`source`, `eventId`) is unique; a repeat is 200 without effect. Events leave after 30 days.
- Limits: 60 requests a minute per client address before any check, and 30 new incidents an hour per source (then 429 and one audit row).
- `firing`: an open incident of the same source and `alertKey` gets an internal note (at most one in 5 minutes); otherwise a new incident in the source's team. One lock per source, so parallel events open one incident.
- `resolved`: an internal note only. IT closes the incident. No matching incident: 200, no effect.
- Text is plain text, length-limited, never rendered as HTML. Incidents follow normal SLA, scope, email and report rules.

UI: admin list, form, one-time secret dialog, last event time and last error count. "İzleme" mark on the incident for IT. `docs/operations.md` gets a signing example.

Not included: outgoing webhooks, pull of monitoring data, automatic closing, tool-specific formats, network allow list.

## Risks

- Licence keys and webhook secrets are readable to whoever holds the database and the key ring. Backups contain both, as for directory secrets.
- The webhook is the first anonymous write endpoint besides sign-in. Limits and the signature reduce the risk; closing it at the network level stays possible in nginx.
