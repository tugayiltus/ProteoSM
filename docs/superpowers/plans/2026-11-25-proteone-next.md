# Proteone — Next Work

Owner: Tugay İLTUŞ. Only work queue. Finished plans are in the Git history; decisions are in `docs/adr/`.

**Product goal:** a service management tool in the style of ServiceNow for small and medium teams, without its complexity. A feature that needs training or a workflow engine is out.

Rules kept: moving a screen never changes a right (ADR-0038). The software list stays in the user application because IT staff maintain it.

## Waits for the owner

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
