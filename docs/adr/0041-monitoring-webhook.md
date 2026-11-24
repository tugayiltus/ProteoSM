# ADR-0041: Signed Monitoring Webhook

**Date**: 2026-11-25
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

A monitoring tool could not open an incident without a person. `proteone.md` §2 and §19 left integrations out of the first release. The owner chose a signed webhook whose incidents are closed by hand. It is the first anonymous write endpoint besides sign-in, so it must not be usable to flood or forge. Design: `docs/superpowers/specs/2026-11-24-licences-releases-monitoring-design.md`. Related decisions: ADR-0004 (antiforgery), ADR-0024 (proxy trust), ADR-0030 (Data Protection secrets).

## Decision

- **Sources.** An admin creates sources in `/admin` → "İzleme kaynakları": team, requester (active IT user), optional service and category, impact, urgency. The secret (32 random bytes as 64 hex characters) is shown once, stored with the purpose `Proteone.MonitoringSecret`, and replaceable; the former one stops at once, also for a request that waits for the lock.
- **Signature.** `POST /api/v1/monitoring/{sourceId}/events`, at most 16 KB. `X-Proteone-Timestamp` (Unix seconds, at most 5 minutes off) and `X-Proteone-Signature`: lower-case hex HMAC-SHA256 of `timestamp.body`, keyed with the text of the secret, compared in constant time. Nothing is parsed before it. Unknown or inactive source, bad time and bad signature give the same 401 body; an unknown source costs the same HMAC.
- **Antiforgery.** Only this endpoint carries `SkipCsrfMetadata`; a test names it as the only one. A session cookie gives the request no rights: records carry the requester of the source.
- **Replay and races.** (`source`, `eventId`) is unique; a repeat is 200 without effect, and event ids leave after 30 days. One advisory lock per source, so parallel events open one incident.
- **Limits.** 60 requests a minute per client address before any check (`Security:MonitoringPermitLimitPerMinute`), 30 new incidents an hour per source (429, one audit row per hour; the event id stays unused, so the tool may retry).
- **Effects.** `firing` opens an incident in the team of the source, or adds an internal note to the open one of the same alert, at most one in 5 minutes. `resolved` adds one internal note; IT closes the incident. Control characters become spaces; the text is never rendered as HTML.
- **Counters apart.** Last event and refused requests live in `monitoring_source_statuses`, so an event never makes an admin's edit fail. They show on the list; the mark "İzleme" on an incident is for IT only.

## Alternatives Considered

### Alternative 1: An API key in a header
- **Pros**: Simpler for tools.
- **Cons**: A copied request or a proxy log is enough to forge events.
- **Why not**: The signature covers the body and the time.

### Alternative 2: Close the incident on `resolved`
- **Pros**: Less work for flapping alerts.
- **Cons**: Nobody looks at the cause, and the SLA stops without a person.
- **Why not**: The owner chose closing by hand.

### Alternative 3: Pull from the monitoring tool
- **Pros**: No anonymous endpoint.
- **Cons**: One client per tool, and stored credentials of that tool.
- **Why not**: The light brief needs one format.

## Consequences

### Positive
- Any tool that can sign a request opens incidents with the normal SLA, scope, email and report rules.

### Negative
- Tools without HMAC support need a small script (`docs/operations.md`).
- An alert that flaps within 5 minutes may show "resolved" last until the next firing note.

### Risks
- The secret is readable to whoever holds the database and the key ring. A lost key ring makes every source answer 401 until its secret is replaced.
- The per-address limit counts a whole network behind one address as one client. Closing the path at the network level stays possible in nginx.
