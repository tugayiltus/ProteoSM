# ADR-0047: ProteoSM Name and the Move from Proteone

**Date**: 2026-11-28
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The product was TGYSM, then Proteone (ADR-0037). The owner chose ProteoSM as the product name and ProteONE as the umbrella brand of all his products, shown as "ProteoSM by ProteONE". The name appeared in three kinds of places: what users see (texts, titles, e-mail), what the code and operations use (project names, images, database, environment variables, cookies), and what stored data or outside tools depend on (Data Protection purposes, monitoring webhook headers, applied migrations). An installation runs under the Compose project `proteone` with volumes and a database of that name; Docker volumes follow the project name, so an updated checkout starts empty.

## Decision

- **Names.** The product is spelled `ProteoSM` everywhere users read it. `ProteONE` is the umbrella brand. The pattern "ProteoSM by ProteONE" (Turkish UI text: "ProteoSM · bir ProteONE ürünü") appears on the sign-in screen, under both side menus, in the footer of the default e-mail texts and in the documents. Wordmark text, page titles, export file names (`proteosm-<report>-…xlsx`), the pilot category ("ProteoSM Pilot") and the user guide use the new name.
- **Code and operations identifiers** (commits `a0ba1466`, `3b58340a`): solution and projects `ProteoSM.*`; images `proteosm-api`, `proteosm-worker`; Compose project, database and role `proteosm`; environment prefix `PROTEOSM_`; session cookies `__Host-proteosm` and `__Host-proteosm-af`; dev host `proteosm.localhost`. The repository is `tugayiltus/ProteoSM`; GitHub redirects the old address.
- **Kept literals, because stored data or outside tools depend on them:** the Data Protection application name `Tgysm` and the purposes `Tgysm.AuthSettings`, `Tgysm.Notifications.AccountLinkToken`, `Proteone.EmailSettings`, `Proteone.MonitoringSecret`, `Proteone.LicenceKey` (changing them makes stored secrets unreadable); the monitoring webhook headers `X-Proteone-Timestamp` and `X-Proteone-Signature` (contract with external tools); applied migration names; the browser storage keys `proteone-theme` and `proteone.sidebar.narrow` (renaming resets a user's saved mode); internal session claim names and the directory test data names. Older ADRs, specs and release evidence keep the name of their time.
- **Cookies.** The renamed cookies mean everyone signs in once more after the move. This is accepted and written in `docs/operations.md`.
- **Environment fallback.** `PROTEONE_*` variables are still read while the `PROTEOSM_*` one is unset (`tools/lib/product-env.sh`, `product_env_value`, nested Compose defaults, `TestEnvironment`), so an existing `deploy/.env` keeps working.
- **Move by backup and restore** (commits `e236d98a`, `51ebc253`): back up the Proteone stack, bring it down without removing volumes, restore the archive into the new `proteosm` stack (`tools/restore.sh` also accepts `proteone-` archives), then deploy. `tools/deploy.sh` refuses an empty start beside `proteone_pgdata` unless `--fresh-install`. Old volumes, archives and images stay for a rollback; data written after the move is lost on rollback. `tools/proteosm-move-drill.sh` rehearses it on throw-away stacks; the report is `docs/release/proteosm-migration-drill.md`.
- **Default e-mail texts** (commit `2af39822`): the migration `ProteoSMEmailDefaults` replaces the name and adds the footer line only in a template whose subject and body still equal the shipped default text. An edited template, sent e-mail and audit history are not touched. The expected default texts are listed in the migration; an integration test proves that an unchanged default changes and an edited one stays byte-identical.
- **Guard test.** `FormerProductNameTests` fails when the bare word `Proteone` or a `PROTEONE_` name appears outside the kept list (kept literals, migrations, ADRs, specs and plans, release evidence, the env fallback, the restore of old archives, the deploy guard, the drill and the machine path `~/claude/Proteone`). `ProteONE` and `ProteoSM` do not match. The `TGYSM` guard stays.
- User-facing change landed in commit `38b21d2f`; documents in `01813ea8`; requirements file `proteone.md` renamed to `proteosm.md` there.

## Alternatives Considered

### Alternative 1: Rename only what users see, keep `Proteone` in code and operations
- **Pros**: No move procedure, no new cookies, no environment changes, much smaller diff.
- **Cons**: Every developer and operator meets two names; images, volumes and the database keep a name the product no longer has; the mismatch grows with each new script or document.
- **Why not**: The owner wants one name. The cost of the move is paid once and is rehearsed in a drill.

### Alternative 2: Rename the database, role and volumes in place
- **Pros**: No second stack and no restore.
- **Cons**: Docker has no volume rename; a Compose project rename leaves the old volumes behind. A role and database rename needs an exclusive lock and a hand-edited connection string, with no tested rollback.
- **Why not**: Backup and restore uses tools that already exist and are drilled, keeps the old stack intact for rollback, and checks row counts against the archive manifest.

## Consequences

### Positive
- One product name in the interface, code, operations and documents, with the umbrella brand visible.
- A host moves with a tested procedure and can roll back.
- The guard test keeps the former name from returning.
- Customized e-mail templates survive the rename.

### Negative
- Everyone signs in once more after the move (new cookie names).
- Kept literals (`Proteone.*` purposes, `X-Proteone-*` headers, storage keys) stay as a visible leftover, listed and guarded.
- A short write freeze during the move, and data written after it is lost on rollback.
- Two names for environment variables for a while.

### Risks
- A template whose text differs from the default by one character does not get the new name or footer. Mitigation: it is an edited template by definition, and an admin can change it by hand.
- Removing the `PROTEONE_` fallback too early breaks an old `deploy/.env`. Mitigation: remove it only in a later decision, after the hosts have moved.
- An old bookmark or script using `proteone.localhost` or old image names. Mitigation: the dev host alias still works; image and volume names are documented in `docs/operations.md`.

Related: ADR-0037 (name and compatibility), ADR-0043 (no upgrade path from TGYSM), ADR-0025 (backup and restore), ADR-0004 (cookies), ADR-0045 (mail settings).
