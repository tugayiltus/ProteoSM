# ADR-0043: No Upgrade Path from a TGYSM Installation

**Date**: 2026-11-25
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

ADR-0037 kept tools and fallbacks to move an existing TGYSM installation into a Proteone stack. The owner decided on 2026-11-25 that the data of that installation is not needed and that nothing will be moved. The upgrade code would be kept and tested without a user.

## Decision

- Remove `tools/upgrade-drill.sh`, the `tgysm_pgdata` check and `--fresh-install` of `tools/deploy.sh`, the `TGYSM_SUBNET` and `TGYSM_DEPLOYMENT` fallbacks, the retention of `tgysm-` archives, the old browser keys and the move section of the operator manual.
- Keep the Data Protection name `Tgysm` and its purposes, and all applied migrations, including `ProteoneBrand` and `ProteoneHistory`.
- This replaces the upgrade points of ADR-0037. Its naming decisions and the guard `FormerProductNameTests` stay in force.

## Alternatives Considered

### Alternative 1: Keep the upgrade path
- **Pros**: A TGYSM installation could still be moved.
- **Cons**: Code, checks and documents for a case that does not occur.
- **Why not**: No installation will be moved.

### Alternative 2: Also rename the Data Protection values
- **Pros**: The former name leaves the code except in migrations.
- **Cons**: Secrets in existing Proteone databases and backups become unreadable.
- **Why not**: The values are never shown; the loss is real.

## Consequences

### Positive
- Deploy, backup and browser code have one name and fewer branches.

### Negative
- A browser that holds only the old keys starts with the default color mode and a wide menu.
- A host that still has TGYSM data gets no warning before an empty start.

### Risks
- An environment file with `TGYSM_` keys is ignored without a message. Mitigation: `deploy/.env.example` lists the current keys.
- A later wish to move TGYSM data needs the removed tools. Mitigation: they are in the Git history at `c4b6ca1`.
