# ProteoSM click-path and UX audit

Date: 2026-11-28. Branch `feature/proteosm`. Method: `ecc:click-path-audit` (button, form, API, state, URL, visible result) plus quick accessibility checks.
Tool: `frontend/e2e/click-path-audit.spec.ts`, run with `AUDIT_OUT=<dir> tools/e2e.sh e2e/click-path-audit.spec.ts` on a fresh `proteosm_e2e` database (never the dev database). Screenshots: `docs/release/proteosm-screens/<role>-<view>-<theme>-<route>.png`.

Screens of employee and shared pages may be fixed by the P2 branch (marked "may be fixed by P2"); the audit tree does not contain those edits.

## Coverage

| Item | Covered |
| --- | --- |
| Roles | employee (separate role, 11 routes), IT staff (44 routes), admin (66 routes) |
| Views | desktop 1280 (light and dark for employee and admin, light for IT), phone 360 (light) |
| Role x route pairs | 121; walks: 242 (each opened by menu, by URL and after reload); screenshots: 242 after trimming (dark desktop shots dropped; light desktop and phone kept per role and route) |
| Reports | all 10 report tabs, drill-down list, "Dışa aktarımlarım" (phone uses the report selector) |
| Filled module details | change (with tasks and approval), task, problem (with SLA timer), release, licence (with assignment), service (with asset), asset, KB article, ticket, announcement |
| Admin sections | all 22 admin routes, including the last ones (`/admin/system`, `/admin/audit`, `/admin/monitoring`, `/admin/login-method`) |
| Denied routes | employee: `/workspace`, `/reports`, `/admin`, `/licences`, `/changes`, `/approvals`; IT: `/admin`, `/approvals`, `/admin/users` |
| Flows (action-tested, both views, all passed) | empty incident form validation, create incident, create request, approve, claim, internal note, reply, complete, employee sees resolution but not the note, link problem dialog, change form opens, search palette opens a result, Excel export from drill-down, account menu by keyboard |
| Console and page errors | 0 on both views; no unreachable route |

Not covered: the SLA-risk ticket state (needs a two-minute wait; covered by `e2e/sla.spec.ts`), mail flows, LDAP/OIDC sign-in.
Harness notes: "empty-state" hits are text matches (for example the value "Yok") and are not findings; a sidebar-link check on the phone drawer was timing-sensitive in the first run and passes after waiting for the menu button.

## Findings

| Screen | Role | Path | Observation | Change needed | Verification | Severity |
| --- | --- | --- | --- | --- | --- | --- |
| Approvals | admin | `/approvals` (phone 360) | The change card collapses to a one-character-wide text column. The title, number and window wrap per letter. "Onayla" and "Reddet" sit beside it and "Düzeltmeye gönder" is outside the viewport. | FIXED. `ChangeApprovalRow` and the ticket `ApprovalRow` put the buttons under the content on phones (`useIsPhone`); beside it on wide screens. | `ApprovalsPage.test.tsx` (phone order); `ui-qa.spec.ts` sweep, no problem on `/approvals` at 360 px. | P1 |
| Task list | IT, admin | `/tasks` (desktop 1280) | The table is wider than the content area. The primary button "Yeni görev" is cut at the right edge and the "Aç" column is clipped. The page itself does not scroll sideways, so the user cannot reach them. | FIXED. `DataList`: proportional columns get a 64 px floor instead of 120 px, so nine columns fit the content area; the headerless action column is pinned to the end edge (`useTableStickyColumns`). | `list.test.tsx`; `ui-qa.spec.ts` `layoutProbe`: red before (table 1357 px > 1280 px), green after. | P1 |
| Problem list | IT, admin | `/problems` (desktop) | Same as the task list: "Yeni problem" outside the viewport. | FIXED by the same `DataList` change; the "Durum ve SLA" column keeps its layout. | Same probe; `light_problems.png` checked. | P1 |
| Change list | IT, admin | `/changes` (desktop) | Same: "Yeni değişiklik" outside the viewport. | FIXED by the same `DataList` change. | Same probe. The probe also found `/admin/email-deliveries` (same overflow); fixed by the same change. | P1 |
| Breadcrumbs | IT, admin | task, problem, change, release detail and change calendar (phone) | The collapsed-parent menu trigger is a button without an accessible name. Screen readers read "button". May be fixed by P2 (shared navigation). | ALREADY FIXED by P2: the trigger icon carries `breadcrumb.more` as its label. No change. | `navigation.test.tsx` finds the button named "Diğer üst sayfalar". | P2 |
| Ticket detail | IT | `/tickets/<number>` after "Tamamla" | Focus falls to `body` when the complete dialog closes (desktop and phone). The keyboard user starts again at the page top. May be fixed by P2. | FIXED. `TicketActions` moves focus to the page title (`focusMainHeadingIfLost`) when the dialog closes or the action list vanishes and focus is on `body`. | `TicketActions.test.tsx` (red without the fix, green with it). | P2 |
| Sidebar | all staff roles | all pages, desktop 1280x800 | The last group "Analiz" (Raporlar) is cut off under the footer text. The list does not show that more entries exist. | FIXED. Side menu items use `size="sm"`; the app menu now fits at 1280x800. The scroll area above the footer stays for longer menus (admin). | `ui-qa.spec.ts` probe flags a cut-off app menu; `light_tasks.png` shows "Raporlar". | P2 |
| Route guard | employee | `/workspace`, `/changes`, `/licences`, `/admin`, `/approvals` | Typing a staff URL sends the employee to the home page without a message. Only `/reports` shows a forbidden screen. | KEPT (P3): needs a product decision on the message and screen. | Not changed. | P3 |
| Several screens | all | phone 360, 52 role/route pairs (home, ticket forms, KB, lists, admin) | Controls smaller than 24 px (WCAG 2.2 target size): inline "Aç" links and small icon buttons. Home, new incident and new request show 5 to 9 each. | KEPT (P3): touch targets need a page-by-page sweep; larger than the cheap scope. | Not changed. | P3 |
| Login method | admin | `/admin/login-method` (phone) | The redirect URL field and its "Kopyala" button are clipped ("Kop…"). The LDAP filter hint is cut. Desktop: one input is narrower than its text. | FIXED. The copy button sits under the redirect field on phones (`RedirectUriField`). The LDAP hint wrap was not changed. | `LoginMethodPage.test.tsx`. | P3 |
| Asset and licence detail | IT, admin | `/assets/<id>`, `/licences/<id>` (phone) | One value span is clipped (text 291 px in 248 px, 173 px in 138 px). | KEPT (P3): clipped value spans need a screenshot pass per field. | Not changed. | P3 |
| Reports | IT, admin | `/reports/*` (phone) | The page is very long. Empty figures (rating, SLA, first response) take a full card each. | KEPT (P3): folding empty figures changes the report design. | Not changed. | P3 |
| Employee screens | employee | home, tickets, new forms, KB, my assets, my software | No overflow, no console error, headings present, forms labelled, validation message shown on an empty incident form and the first invalid field is focused. May be fixed by P2 (no change made here). | None | live and action-tested | OK |
| Admin sections | admin | all 22 `/admin/*` routes | No console error, no page overflow, headings present, dark mode readable. Only the login-method phone issue above. | None | live | OK |
| Module details | IT, admin | change, task, problem, release, licence, service, asset | Filled data renders in all sections (tasks by phase, approval box, timeline, SLA card). No overflow except the clipped spans above. | None | live | OK |

## Findings to fix (by severity)

1. P1: phone approvals row layout. `frontend/src/features/approvals/ChangeApprovalRow.tsx`, `ApprovalsPage.tsx`.
2. P1: desktop list pages push the primary button and row action out of reach. `frontend/src/shared/list/DataList.tsx`, `frontend/src/features/tasks/TaskListPage.tsx`, `frontend/src/features/problems/ProblemListPage.tsx`, `frontend/src/features/changes/ChangeListPage.tsx`.
3. P2: breadcrumb menu trigger has no name. `frontend/src/shared/navigation/Breadcrumbs.tsx` (Astryx `BreadcrumbItem` collapse menu; may be fixed by P2).
4. P2: focus lost after the complete dialog. `frontend/src/features/tickets/` (ticket actions; may be fixed by P2).
5. P2: sidebar bottom group cut off at 800 px height. `frontend/src/shared/layout/AppSidebar.tsx`.
6. P3: silent redirect of denied routes. `frontend/src/app/router.tsx`, `frontend/src/features/auth/guards`.
7. P3: small touch targets on phone. Row actions in `frontend/src/shared/list/DataList.tsx` and the home cards.
8. P3: login-method phone layout. `frontend/src/features/admin/` (login method page).
9. P3: clipped values on asset and licence detail, long empty report cards. `frontend/src/features/assets/AssetDetailPage.tsx`, `frontend/src/features/licences/LicenceDetailPage.tsx`, `frontend/src/features/reports/ReportsPage.tsx`.

Counts: P0 0, P1 4, P2 3, P3 5 (3 further rows are OK results).

Status after the fix round: P1 4 fixed, P2 3 resolved (breadcrumb already fixed by P2), P3 1 fixed (login method), 4 kept. New guard: `frontend/e2e/layoutProbe.ts`, run by the UI QA sweep (`tools/ui-qa.sh`); it fails on page overflow, tables wider than the viewport or with a hidden last column, controls outside the viewport and a cut-off app side menu, at 1280x800 and 360x800.
