# Proteone — Next Work

Owner: Tugay İLTUŞ. Only work queue. Finished plans are in the Git history; decisions are in `docs/adr/`.

**Product goal:** a service management tool in the style of ServiceNow for small and medium teams, without its complexity. A feature that needs training or a workflow engine is out.

## This branch (`feature/proteone-cleanup`)

1. Former name: removed from documents and code, except stored-data literals and the upgrade path (ADR-0037).
2. Documents: finished task lists removed; current behavior, design, rules and planned work stay.
3. Menu: the long IT group is split by job; the home page links every personal area.
4. `/admin` general settings: licence notice days and warranty window, now fixed in code.
5. Announcements: an admin publishes a dated message; every signed-in user sees it on the home page.
6. Admin overview: shortcuts beside the set-up checks. Conversation before the links on the ticket page; row actions on one line.
7. Checks in this order: lint, focused tests, full tests, E2E, UI sweep, review of the whole diff, then confirmation by an independent agent.
8. Last: guide, screenshots, PDF, README and the GitHub page.

Rules kept: moving a screen never changes a right (ADR-0038). The software list stays in the user application because IT staff maintain it.

## Waits for the owner

- Archive of the TGYSM repository; removal of the `tgysm` remote, old local branches and `~/claude/TGYSM`.
- Move of the real TGYSM installation (`docs/operations.md`). The upgrade tools and fallbacks stay until then.
- Sign-in test with a real Entra tenant and a real Active Directory.
- Host sizing after the pilot.

## Known small gaps

- An unchanged update writes an empty audit row.
- Task links are 404 for owner-team colleagues (follows `TaskScope`).
- A too short search shows the empty text.
- Deactivating a service and choosing it can race.
- Impact counts differ from the "Tümünü göster" lists.
- The admin asset dialog stops at 20 assets; the asset list has no service filter.
- Focus on server field errors, row button context, comment tabs pattern.
