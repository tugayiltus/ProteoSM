# ADR-0040: Light Release

**Date**: 2026-11-24
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Changes that go live together had no common record. `proteone.md` §2 left release management out of the first release. The owner chose a light module: a group of changes, without approval, report or calendar entry. Deployment stays the implementation phase of each change (ADR-0032). Design: `docs/superpowers/specs/2026-11-24-licences-releases-monitoring-design.md`. Related decisions: ADR-0011 (numbers), ADR-0033 (rows the viewer cannot open), ADR-0034 (access rules of problems).

## Decision

- **Own record.** `releases` (`REL-YYYY-NNNNNN`) with title, description, owner team and owner, target window and notes. States: planned, in progress, completed, or cancelled with a reason. History in `release_events`, audit `releases.*`.
- **One release per change.** `change_requests.release_id`. Joining a release never touches the plan fingerprint, approval or state of the change; both sides write history.
- **Rules.** A change joins or leaves while the release is planned or in progress. It must be neither closed nor cancelled when it joins, and the actor must be able to work on both records. Completion needs at least one change and all of them closed or cancelled. Cancelling a release frees its changes in the same save.
- **One lock.** Every release write after creation runs under `SerializationLocks.ChangeWork` (7_009), so a change is never closed, cancelled or moved while its release is completed or cancelled.
- **Access.** All IT staff and admins read every release; a change they cannot open shows number, title, status and team. Writes need an admin or the owner team (403). Employees get 404. Releases are an IT-only group of central search.

## Alternatives Considered

### Alternative 1: A release as a change of its own kind
- **Pros**: Reuses approval, calendar and tasks.
- **Cons**: A second approval above the approvals of its changes.
- **Why not**: The owner chose no approval for a release.

### Alternative 2: Many releases per change
- **Pros**: A change can be planned into several releases.
- **Cons**: Completion and the change page become ambiguous.
- **Why not**: The light brief needs one answer to "when does this go live".

## Consequences

### Positive
- One page shows what goes live together and what is still open.
- The change lifecycle is unchanged.

### Negative
- A release has no report, calendar entry or email.
- Titles of changes outside the viewer's scope are visible on a release, the same exception as ADR-0033.

### Risks
- The target window is not compared with the windows of the changes; the change calendar stays the place for conflicts.
