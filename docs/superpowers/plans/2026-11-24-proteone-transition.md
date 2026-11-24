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
| Migrations' content, audit and event values, record number prefixes, user-secrets id, OIDC callback path | keep |
| Accepted ADRs, earlier specs and plans, release records | keep (history) |

## Steps

1. This plan.
2. Tests that pin the Data Protection values.
3. Mechanical rename: backend, tools, OpenAPI.
4. Cookies, claims, browser storage, export file name.
5. Docker, compose, nginx, CI, backup and restore compatibility.
6. Brand assets and surfaces from one shared source.
7. Imports between the user app and the admin folder removed.
8. Admin entry point, API fallback, tests.
9. ADR-0037 (name and compatibility), ADR-0038 (admin entry, partly replaces ADR-0003), requirements §4.
10. One review of the whole diff; fixes.
11. Tests: unit and lint, integration, E2E and UI QA, Docker images, backup, restore and upgrade drills.
12. Guide, screenshots, PDF, operator manual, README, capabilities, SECURITY. Repository links after the new repository is verified.

## Acceptance

- No `tgysm` outside an allow list that a test guards.
- Clean install: first admin, sign-in, email and export under the Proteone name.
- Upgrade: equal row counts; a stored directory secret and a waiting account link still work; a TGYSM archive restores; rollback rehearsed.
- Access: anonymous 401; employee and IT 403 on `/api/v1/admin/*`; employee 404 on service routes; deep `/admin/...` links and reload work.
- Service catalogue E2E passes unchanged.
- The same commit is green in the new repository's CI.
