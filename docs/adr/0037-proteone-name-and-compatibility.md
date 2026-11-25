# ADR-0037: Proteone Name and Compatibility with TGYSM Installations

**Date**: 2026-11-24
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The product continues under the name Proteone. The former name TGYSM was in namespaces, settings, cookies, the database, images, backups and stored texts. An installation with data exists and must be moved. Some strings are part of how stored data was protected, so a blind rename would make that data unreadable.

## Decision

- Rename the code, projects, images, settings, database, role, cookies, claims and tools to Proteone (`00063a9`, `4207497`). Migration ids do not change.
- Keep three literal values: the Data Protection application name `Tgysm` and the purposes `Tgysm.AuthSettings` and `Tgysm.Notifications.AccountLinkToken`. Tests pin them (`8da9759`).
- Replace the name in stored email templates with the migration `ProteoneBrand`. Applied migrations are not edited. `ProteoneHistory` does the same for sent emails, the pilot category and audit rows; the append-only trigger is off only inside that transaction.
- Move an installation by backup and restore into a new stack. Restore accepts archives of either name. `tools/deploy.sh` refuses an empty start beside `tgysm_pgdata` unless `--fresh-install` is given.
- Read the old browser keys, `TGYSM_SUBNET` and `TGYSM_DEPLOYMENT` while the new ones are missing. Backup retention also removes expired `tgysm-` archives.
- `FormerProductNameTests` fails when the old name appears outside an allow list.

## Alternatives Considered

### Alternative 1: Change only the visible brand
- **Pros**: Small change, no upgrade work.
- **Cons**: Two names in settings, logs, backups and code for the life of the product.
- **Why not**: The owner wants one name, also in the database.

### Alternative 2: Rename the Data Protection values and re-encrypt
- **Pros**: No old literal remains.
- **Cons**: Needs a step that reads with the old name and writes with the new one; a restored old archive would need it again.
- **Why not**: The values are never shown and are not stored as text. The risk buys nothing.

### Alternative 3: Rename the running stack in place
- **Pros**: No restore.
- **Cons**: Docker volumes follow the project name, so the stack would start empty; the database and role need a manual rename.
- **Why not**: Backup and restore leaves the old volumes untouched for a rollback.

## Consequences

### Positive
- One name everywhere, and a test that keeps it.
- Stored directory secrets and waiting account links stay readable.

### Negative
- Every user signs in once after the move.
- The migration `ProteoneHistory` replaces the name also in sent emails and audit rows (owner decision). These rows no longer show the exact words of their time; the replacement leaves its own audit row. Names in directory paths and web addresses stay.
- Accepted ADRs and earlier plans keep the old name. Free text of users (tickets, comments, articles) is not changed.

### Risks
- Data written after the move is lost in a rollback to the old stack. Mitigation: move in a short, announced window.
- An environment file that sets only old names works for the two listed keys only. Mitigation: the operator manual lists the new keys.

## Amendment 2026-11-25

The owner asked for the Proteone name in all documents. Accepted ADRs, designs and release records now use it; this file, the upgrade instructions and the stored-data literals keep the former name. Finished plans left the working tree and stay in the Git history.

## Amendment 2026-11-25 (upgrade path)

The upgrade path and the fallbacks for TGYSM installations are removed (ADR-0043). The naming decisions and the stored-data literals stay.
