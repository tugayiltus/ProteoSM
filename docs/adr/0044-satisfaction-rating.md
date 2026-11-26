# ADR-0044: Satisfaction Rating on Completed Tickets

**Date**: 2026-11-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

A ticket ends with `Tamamlandı`, and nothing asks the requester how the work went. Other ITSM tools ask for a satisfaction score after a ticket closes, and the owner wants Proteone to have no visible gap there (light ITIL scope expansion, first of four modules). IT staff and managers need one simple quality figure per team and period. The product rules apply: every read and write goes through sign-in and `TicketScope`, there is no reopen (`proteone.md` §7), and an anonymous public endpoint is not part of the product. Design: `docs/superpowers/specs/2026-11-26-satisfaction-rating-design.md`.

## Decision

Approach C, chosen by the owner: the completion email holds a rating link into the application, and the score is given there behind the normal sign-in.

- **Who and what:** only the requester (`ticket.RequesterId`) rates. One score from 1 to 5 is required; a comment is optional, trimmed, empty becomes null, at most `RatingLimits.CommentMaxLength = 500` characters. One rating per ticket; the requester may change it inside the window and the latest value counts. Everyone who may see the ticket reads the result; employees see only their own tickets.
- **Rateable outcomes:** status `Completed` with outcome `Resolved` or `Fulfilled` (`TicketRating.RateableOutcomes`, one source for the domain check and the EF predicates). `Cancelled`, `ServiceRequestRequired` and `ApprovalRejected` are not rateable: they do not resolve the user's need, so a score would measure the wrong thing.
- **Window:** `RatingLimits.Window = 14 days` after `CompletedAt`. A request at or after `CompletedAt + Window` is refused with `ratings.window_closed` (409); the form is then gone.
- **Snapshot:** `ticket_ratings` stores the team and the assignee at rating time (`team_id`, `assignee_id`), with an index on `(team_id, rated_at)`. A rated ticket counts under that team in the report team filter, so a later move of the record does not move its score.
- **API:** `PUT /api/v1/tickets/{number}/rating` creates or replaces the rating through `TicketScope` (404 outside scope, `ratings.not_requester` 403, `ratings.not_rateable` 409). Two first ratings at once leave one row; the other request gets a 409 conflict. The ticket detail carries `rating`, `canRate` and `rateUntil`; `GET /api/v1/tickets?queue=to_rate` lists the requester's rateable, unrated tickets inside the window.
- **Write path:** the rating needs no ticket version. It touches `updated_at` through `TicketActivity.TouchAsync`, like a comment, records the audit actions `ratings.submitted` / `ratings.updated` and the internal timeline event `RatingGiven` ("Puan verildi"). The comment is never emailed and stays out of the audit log.
- **Email:** the `ticket_completed` template gets the variable `ratingUrl` (`App:PublicBaseUrl` + `/tickets/{number}?rate=1`), present only for a rateable ticket. A migration adds the link to the default template only. The renderer drops a line whose only variables are empty line-optional ones, so a mail without a link has no empty "Puan ver" line.
- **Screens:** the ticket page section "Bu kayıt nasıl çözüldü?" (five labelled choices, comment, "Gönder"; `?rate=1` focuses the first choice; after sending the score shows with "Değiştir"); IT staff see the score and comment, or "Puan yok"; the employee home card "Puan bekleyen kayıtlar" (up to five rows, hidden when empty). The incident and request reports gain `rating_average` (shown as "3,7 / 5"), `rating_response_rate` and `rating_distribution` with drill-down and Excel export. The pilot seed rates some completed tickets.

Commits: `2346bfe`, `0671351`, `6e705f4`, `8d74d0b`, `8180a34`, `1a2de12`, `33c059b`, `6e1dd3d`, `6409daa`, `34e6c0c`, `bd9541b`, `5b3e2a6`, `19a698e`, `fbceac0`, `3051e2c`. Related: ADR-0022 (reports and charts), ADR-0027 (Astryx UI).

## Alternatives Considered

### Alternative A: Anonymous one-click email link
- **Pros**: highest response rate; one click in the email records the score, no sign-in.
- **Cons**: needs a public endpoint and one-time tokens; a forwarded link lets another person rate; a second write path next to the ticket page.
- **Why not**: works against the authorization rules of the product (anti-goal: no token that records a score without sign-in).

### Alternative B: In-app only
- **Pros**: simplest; one write path, no email change.
- **Cons**: low response, because many requesters never open a completed ticket again.
- **Why not**: rejected as the only path. Its screens (ticket page section, home card) stay in approach C, and the email link leads to them.

## Consequences

### Positive
- One write path behind sign-in and `TicketScope`; the email link, the home card and the ticket page use the same form and endpoint.
- Teams get an average score, a response rate and a distribution per period, with drill-down to the rated tickets.
- The team snapshot keeps a rated ticket's score under the team that did the work when the report filters by team.
- IT staff close a ticket with no extra step.

### Negative
- A signed-out requester signs in first, which lowers the response rate compared with approach A.
- The rating moves the ticket `xmin` version like a comment does. A client that still holds the old version gets a concurrency message on its next versioned action and reloads.
- The report scope of IT staff follows the ticket's current team. If a rated ticket moves to another team, it leaves the figures of the old team's IT staff, although the team filter of a full-scope user still counts it under the snapshot team.
- A `ticket_completed` template that an admin already edited does not get the link automatically; the admin adds `{{ratingUrl}}` by hand (the admin page lists the variable).

### Risks
- The renderer drops a line whose only variables are empty line-optional ones. An admin line that holds only such a variable on purpose disappears when the value is empty.
- Few ratings in a period make the average move strongly; the card shows the score sum over the rated count beside the average, but readers may still over-read a small sample.
- Completed tickets do not transfer today; if that changes, the two team rules above (snapshot in the filter, current team in the IT staff scope) can show different numbers for the same team.
