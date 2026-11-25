# ADR-0042: General Settings and Announcements in the Admin Area

**Date**: 2026-11-25
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The licence notice period and the warranty window were constants in code, so a change needed a release. IT had no place to tell everybody about an outage or a maintenance evening. The product stays simple: small and medium teams run it without training.

## Decision

- One row `system_settings` holds the company-wide values (`licence_notice_days`, `warranty_window_days`, 1–365). The migration inserts it. Admins edit it on `/admin/settings`; each change is audited and version-checked.
- Announcements have a title, a text, a level (`info`, `warning`), a start and an optional end. Admins manage them on `/admin/announcements`. Every signed-in user reads the current ones on the home page, at most five, newest first.
- An announcement is ended, never deleted, so the audit trail stays whole.
- Security values (session length, rate limits) stay in the environment: a compromised admin account must not be able to weaken them.

## Alternatives Considered

### Alternative 1: Key-value settings table
- **Pros**: A new setting needs no migration.
- **Cons**: No types, no database checks, keys drift.
- **Why not**: Two typed columns are simpler and safer.

### Alternative 2: Announcements per department or role
- **Pros**: Targeted messages.
- **Cons**: Audience rules, more screens.
- **Why not**: Small companies tell everybody.

### Alternative 3: Announcements by email
- **Pros**: Reaches people who do not open the product.
- **Cons**: Mass email, templates, opt-out.
- **Why not**: The company already has email.

## Consequences

### Positive
- Admins change the values without a release.
- One visible place for outage messages.

### Negative
- The home page makes one more request.

### Risks
- A background export without a chosen end date uses the warranty window of the moment it runs. Mitigation: a chosen date is stored with the job.
