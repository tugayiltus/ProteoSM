# ADR-0039: Software Licences

**Date**: 2026-11-24
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

IT could not see in Proteone which software the company may use, how many seats are taken, by whom, and what ends soon. `proteone.md` §2 and §19 left licences out of the first release. The owner chose a light module: own records, IT and admins manage, a full licence refuses, employees see only their own software, an email before the end, stored keys and a report tab. Design: `docs/superpowers/specs/2026-11-24-licences-releases-monitoring-design.md`. Related decisions: ADR-0015 (outbox), ADR-0030 (Data Protection secrets), ADR-0036 (service routes answer employees with 404).

## Decision

- **Own records.** `software` (vendor and name, unique together, ignoring Turkish case), `licences` (kind per user or per device, quantity, owner team, optional end date) and `licence_assignments` (exactly one of user and asset, released rows stay). Partial unique indexes allow one active seat per licence and target. Commits `0889f5d`, `6764008`.
- **Seats under one lock.** Assignments, releases, quantity changes and the expiry sweep run under `SerializationLocks.LicenceSeats` (7_013). A full or ended licence, or a quantity below the used seats, answers 409. A user deactivated or an asset retired later keeps the seat and is marked, so IT releases it.
- **Keys.** Stored as Data Protection ciphertext with the purpose `Proteone.LicenceKey`. Only an admin sets, clears or shows a key. Showing saves the audit row `licences.key_shown` before the key is returned, and the response is `no-store`. Lists, details, exports, search, audit values and logs never hold the key.
- **Access.** `/api/v1/software` and `/api/v1/licences` are for IT staff and admins; employees get 404. `GET /api/v1/me/software` returns vendor and name of the software the caller has, by an own seat or by a seat of a device assigned to them.
- **Expiry email.** The Worker job `LicenceMonitor` runs every hour and writes one `licence_expiring` event per licence and end date (`expiry_notified_for`), 30 days before the end or at once when less is left. It goes to the active members of the owner team. An hourly run instead of a daily one means a failed run is retried the same day.
- **Report and search.** A report tab counts all, full, ended and ending licences (30, 60 and 90 days); its rows show the use of each licence. Software is an IT-only group of central search.

## Alternatives Considered

### Alternative 1: A licence as an asset type
- **Pros**: Reuses the asset list, history and import.
- **Cons**: An asset has one holder, a location and a device lifecycle; a licence has many seats and an end date.
- **Why not**: Every asset rule would need an exception.

### Alternative 2: No stored keys
- **Pros**: No secret in the database and in backups.
- **Cons**: IT keeps the keys in files or mail, without an audit.
- **Why not**: The owner chose to store them.

### Alternative 3: Warn instead of refuse when a licence is full
- **Pros**: Work never stops at a wrong quantity.
- **Cons**: Overuse is recorded as normal.
- **Why not**: The owner chose to refuse.

## Consequences

### Positive
- Seats, free capacity and end dates are in one place, and a race cannot exceed the quantity.
- Every showing of a key has a name and a time.

### Negative
- Seats are kept by hand; nothing discovers installed software.
- The expiry notice changes the row version once per end date, so a form open at that moment gets a conflict.
- All seat writes of the company share one lock; the writes are short.

### Risks
- Whoever holds the database and the key ring can read the keys; backups contain both, as for directory secrets.
- A lost key ring makes stored keys unreadable; they have to be entered again.

## Amendment 2026-11-25

The notice period is a general setting now, 30 days at first (ADR-0042).
