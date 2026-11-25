# Proteone — Next Work

Owner: Tugay İLTUŞ. Only work queue. Finished plans are in the Git history; decisions are in `docs/adr/`.

**Product goal:** a service management tool in the style of ServiceNow for small and medium teams, without its complexity. A feature that needs training or a workflow engine is out.

Rules kept: moving a screen never changes a right (ADR-0038). The software list stays in the user application because IT staff maintain it.

## Waits for the owner

- Sign-in test with a real Entra tenant and a real Active Directory.
- Host sizing after the pilot.

## Open check

- One E2E run on 2026-11-25 stayed on the login page after a successful sign-in (`frontend/e2e/tasks.spec.ts`, `mobile-360`). Cause unknown; two later full runs and CI passed.
