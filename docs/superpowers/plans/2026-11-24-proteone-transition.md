# TGYSM to Proteone — Transition Plan

Date: 2026-11-24. Owner: Tugay İLTUŞ. Status: approved by the owner on 2026-11-24. Base commit `b4523a3`, branch `feature/proteone`.

**Goal:** The product continues as Proteone: new brand, renamed code and deployment, an own entry point for `/admin`, and a new public repository with the Git history.

## Owner decisions

- A real TGYSM installation with data exists and is moved by backup and restore into a new Proteone stack. The old volumes stay for rollback.
- Admin: one Vite build with two entries on the same origin. API, session cookie, CSRF and Astryx stay shared. No authorization policy changes.
- Repository: a new public repository with history. A full secret scan comes first. Archiving TGYSM is a separate decision.
- Tests: each commit runs a build and `tools/lint.sh` (exception to the per-commit test gate for this work). After the code: one review of the whole diff, then all tests, shortest first.
- Repository creation, push, CI and deployment settings, and archiving wait for one final approval.

## Compatibility decisions

| Item | Decision |
|---|---|
| Namespaces, projects, solution, package and image names | rename; migration ids unchanged; model snapshot in the same commit |
| Data Protection `Tgysm`, `Tgysm.AuthSettings`, `Tgysm.Notifications.AccountLinkToken` | keep the literal values: stored directory secrets and waiting account links depend on them |
| `__Host-tgysm`, `__Host-tgysm-af`, `tgysm:*` claims | rename; every user signs in once again |
| `tgysm-theme`, `tgysm.sidebar.narrow` | rename; the old key is read once when the new one is missing |
| Compose project, database name and user | `proteone` for the new stack; upgrade by backup and restore; deploy refuses an empty install beside old volumes unless confirmed |
| `TGYSM_VERSION`, `TGYSM_SUBNET`, `TGYSM_DEPLOYMENT` | `PROTEONE_*` first, the old name as fallback |
| CI, test and local tool variables, launchd label | rename |
| Backup prefix | `proteone-`; retention also cleans `tgysm-` archives; restore accepts both |
| Database (owner decision 2026-11-24: no TGYSM left there) | database, role and test database names become `proteone*`; a new migration replaces the name in `email_templates`; schema objects already carry no brand; Data Protection purposes are not stored as text |
| Audit rows and sent emails that hold the old name | replaced by the migration `ProteoneHistory` (owner decision); addresses and directory paths stay |
| Applied migrations' content, event kinds, record number prefixes, user-secrets id, OIDC callback path | keep |
| Accepted ADRs, earlier specs and plans, release records | keep (history) |

## Earlier phases

Phases 0–12 are complete and on `master` under the TGYSM name (`docs/status.md`): 0 design, 1 foundation, 2 assets, 3 support, 4 SLA and email, 5 knowledge and reports, 6 first release, 7 tasks, 8 changes, 9 calendar and reports, 10 related records and search, 11 problems, 12 service catalogue. They are not rebuilt. Their deferred review notes and the open work below join the queue.

## How earlier work fits the new decisions

This file is the only work queue from 2026-11-24. Earlier plans, ledgers and the "Açık işler" list of `docs/status.md` are history or sources; `docs/status.md` points here at the close. Where an older text differs, this table wins.

| Earlier text | Now |
|---|---|
| TGYSM names, `Tgysm.*` paths, `TGYSM_*` variables in open items | read as the Proteone name after step 3–5; no item is done under the old name |
| ADR-0003, `tgysm.md` §4: one SPA, no separate admin frontend | one build with two entries (ADR-0038); same origin, session, CSRF and policies |
| Admin items (asset dialog limit, admin cache after 401, category form, admin tables) | done in the admin entry after step 8, not before |
| Daily operation items (approvals, impact view, service choice, asset card link) | stay in the user app; moving a screen never changes a right |
| Service catalogue (ADR-0036) | definition in admin, choice and impact in the user app; its rules stay |
| Notes about CoreUI, Tailwind or Radix components | closed: the code was replaced by Astryx (ADR-0027) |
| Docs and screenshot items | once, in step 18, on the running Proteone build |
| Test and performance items | code and tests with the item; runs once, in step 17 |
| Guide and pilot database `tgysm_pilot` | a new `proteone_pilot`; the old database is left alone |
| Real Entra and Active Directory test | on the Proteone installation and its address (redirect URI changes when the host changes) |
| Dependabot pull requests on TGYSM | not moved; the new repository creates its own |
| Software and licence assets, light Release, monitoring webhook | built only as Proteone, after the transition |

## Queue

Order: rename first (every path changes), then fixes of earlier phases, then one review and one test pass, then documents, then approvals. New modules follow.

### A. Proteone code (steps 1–8 done on `feature/proteone`)

1. This plan.
2. Tests that pin the Data Protection values.
3. Mechanical rename: backend, tools, OpenAPI.
4. Cookies, claims, browser storage, export file name.
5. Docker, compose, nginx, CI, backup and restore compatibility.
6. Brand assets and surfaces from one shared source.
7. Imports between the user app and the admin folder removed.
8. Admin entry point, API fallback, tests.

### B. Open work of earlier phases

The notes come from the phase ledgers. Some may be fixed already: check each against the code first, close it with a reason when it no longer applies, and prove each behavior fix with a failing test.

9. Security and data safety
   - Done: change-password has the per-client limit; first-admin set-up runs serialized; only the creator commits an import job.
   - Closed after a check: `TRUNCATE` on `audit_events` is blocked (`audit_events_no_truncate`); no CSV export exists and the template holds fixed text.
   - Done: import jobs leave after 30 days. Closed: foreign keys on asset actor columns (users are never deleted, and a failing validation could stop the upgrade of an existing installation).
   - Done (owner decision): a session ends 24 hours after sign-in.
10. Wrong behavior
    - Done: stale version on unchanged values is a conflict; import and asset duplicates report the serial; `approval_requested` event on related and voided requests; record numbers use the company year; article link race is 409; article errors and stale article choice; a failed approvals list no longer hides the other one.
    - Closed after a check: cancelled requests are logged as debug; admin cache leaves on 401; a superseded search releases its spinner.
    - Done: a truncated upload form is 400 and a too large body 413; the next task action after a 409 sends the current version; no new service link on retired and lost assets; inactive services are marked in the list filter.
    - Closed: a JSON body never reaches the comment route; the open task dialog keeps its version on purpose (the user must see the conflict); a chosen service outside the first page needs more than 100 services.
    - Parked: empty audit row of an unchanged update; dead task links for owner-team colleagues (404, follows `TaskScope`); short search shows the empty text; deactivate and set-service race (M2); impact counts differ from the "Tümünü göster" lists (M3); admin asset dialog stops at 20 and the asset list has no service filter (a service can be unlinked from the asset page).
    - Done (owner decision): a transfer without assignee opens a ticket in progress.
11. Scale: closed for a single company. Audit has indexes for time, actor and entity; 2000 locations, the approvals list and the problem link lock are far from their limits. `tools/perf-measure.sh` runs in step 17.
12. Parked: focus of server field errors, row button context, comment tabs pattern, thin tests and duplicated helpers from the ledgers. No behavior depends on them.
13. ADR-0036 and the `by_service` drill-down: done (owner decision): the figure has no links; ADR-0036 amendment.
14. Done in part: PKCE and code flow, guest and domain rules, lockout, plan fingerprint, self-approval and task scope match the code. The other `SECURITY.md` lines are checked with the document step.

### C. Close

15. Done: ADR-0037 (name and compatibility), ADR-0038 (admin entry, partly replaces ADR-0003), requirements §4.
16. Done by the author of the change; no separate reviewer.
17. Done. CI of `e8b1317` in the new repository is green. Tests: unit and lint, integration, E2E and UI QA, Docker images, backup, restore and upgrade drills, performance.
18. Done, except the remaining `SECURITY.md` lines of step 14. Documents: guide, screenshots, PDF, operator manual, README, capabilities, SECURITY, `CLAUDE.md`, `docs/status.md`. Repository links after the new repository is verified.
19. Done on 2026-11-24: new public repository, push, switch of active development, local database and role rename, name in history. SDK 11 is postponed by the owner. Open: archive of TGYSM, move of the real installation. List: SDK 11 on the development machine, secret scan result, new repository, push, CI and deployment move, open Dependabot pull requests, switch of active development, archive of TGYSM.

### D. After the transition

20. Directory sign-in against a real Entra tenant and a real Active Directory (by hand; needs the owner's environment).
21. Host sizing after the pilot.
22. Software and licence asset types, then light Release, then the monitoring webhook. Each needs a design, an ADR and owner approval before code.

## Acceptance

- No `tgysm` outside an allow list that a test guards.
- Clean install: first admin, sign-in, email and export under the Proteone name.
- Upgrade: equal row counts; a stored directory secret and a waiting account link still work; a TGYSM archive restores; rollback rehearsed.
- Access: anonymous 401; employee and IT 403 on `/api/v1/admin/*`; employee 404 on service routes; deep `/admin/...` links and reload work.
- Service catalogue E2E passes unchanged.
- The same commit is green in the new repository's CI.
