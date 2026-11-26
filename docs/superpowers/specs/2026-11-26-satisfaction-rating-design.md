# Satisfaction rating on closed tickets — design

Date: 2026-11-26. Owner: Tugay İLTUŞ. Branch: a new feature branch from `master`. First of four modules that complete the product (order: satisfaction rating, problem SLA and email, standard change, inbound email).

## Intent

The owner wants Proteone to stand next to other ITSM tools without visible gaps. A satisfaction score after a ticket closes is a standard feature there. Today a ticket closes with `Tamamlandı` and nothing asks the requester how it went. The rating gives IT staff and managers one simple quality figure per team and period.

## Goals

- The requester of a completed incident or request gives one score from 1 to 5 and an optional short comment.
- **Approach C (owner decision):** the completion email holds a "Puan ver" link. The link opens the ticket page in the application; the score is given there. The ticket page and a home card offer the same form. No anonymous endpoint.
- Reports show the average score and the response rate per team and period, with drill-down to the rated tickets.
- IT staff on the ticket see the score and the comment.

## Non-goals

- Multi-question surveys, a survey designer, NPS.
- Ratings for changes, problems, tasks or releases.
- Reopening a ticket from a low score (the product has no reopen, `proteone.md` §7).
- Reminder emails.

## Anti-goals

- A token in a link that records a score without sign-in.
- Extra steps for IT staff when they close a ticket.
- A score that anyone other than the requester can give or change.

## Rules

- Rateable: status `Completed`, outcome `Resolved` or `Fulfilled`. Not rateable: `Cancelled`, and outcomes `ServiceRequestRequired` and `ApprovalRejected` (my call: they are not a resolution of the user's need, so a score would measure the wrong thing).
- Only the requester rates. Other viewers see the result when they may see the ticket; employees see only their own.
- One rating per ticket. The requester may change it inside the window (my call: a mistaken click stays fixable). The latest value counts.
- Window: 14 days after `CompletedAt`, a named constant. After it, the form is gone and the API refuses (`ratings.window_closed`).
- Score 1–5, required. Comment optional, at most 500 characters (named constant).
- The rating stores the team and the assignee at completion time (snapshot), so a later transfer of the closed record cannot move the score. Completed tickets do not transfer today; the snapshot keeps reports stable if that changes.
- Audit: `ratings.submitted`, `ratings.updated`. Timeline: an internal event "Puan verildi". The requester's comment is not sent to anyone by email.

## Data

New table `ticket_ratings`: `id` (uuid v7), `ticket_id` (unique, FK), `score` (smallint, CHECK 1–5), `comment` (text, null), `team_id`, `assignee_id` (null), `rated_by`, `rated_at`, `updated_at`, `xmin` concurrency. Index on `(team_id, rated_at)` for reports.

## API

- `PUT /api/v1/tickets/{number}/rating` — body `{ score, comment }`. Creates or replaces. Loads through `TicketScope`; refuses with 404 outside scope, `ratings.not_requester` (403), `ratings.not_rateable` (409), `ratings.window_closed` (409). Error codes go to `ErrorCodes` and `errors.json`.
- Ticket detail DTO gains `rating` (score, comment, ratedAt or null) and `canRate` (bool) and `rateUntil`.
- `GET /api/v1/tickets?queue=to_rate` — the requester's rateable, unrated tickets inside the window, for the home card.

## Email

- New template variable `ratingUrl` in `TicketCompletedSet`: `App:PublicBaseUrl` + `/tickets/{number}?rate=1`. Present only when the ticket is rateable.
- A migration adds the link to the default `ticket_completed` template. A template an admin already edited is left unchanged; the admin page lists `ratingUrl` among the variables so the admin can add it.

## Screens

- **Ticket page (requester, rateable):** a "Bu kayıt nasıl çözüldü?" section under the resolution: five choices (1–5, labelled, keyboard reachable), optional comment, "Gönder". `?rate=1` scrolls to the section and focuses the first choice. After sending, the section shows the score with "Değiştir" until the window ends.
- **Ticket page (IT staff, admin):** the score and the comment in the details area; "Puan yok" when not rated.
- **Home page (employee):** a card "Puan bekleyen kayıtlar" with up to five rows from `queue=to_rate`; hidden when empty.
- **Reports:** new figures on the incident and request reports: average score, response rate (rated / rateable in the period), score distribution. Filters: period, team. Drill-down lists rated tickets with score and comment; Excel export follows the existing export pipeline.

```
Kayıt INC-2026-000123 · Tamamlandı
Çözüm: Yazıcı sürücüsü yeniden kuruldu.

Bu kayıt nasıl çözüldü?
( 1 Çok kötü ) ( 2 ) ( 3 ) ( 4 ) ( 5 Çok iyi )
Yorum (isteğe bağlı) [______________________]
[Gönder]                     25 Kasım'a kadar değiştirebilirsiniz.
```

## Approaches considered

- **A. Anonymous email link:** one click records a score. Highest response rate. Needs a public endpoint and one-time tokens; a forwarded link lets another person rate. Rejected: works against the authorization rules of the product.
- **B. In-app only:** the ticket page and a home card. Simplest. Low response, because many requesters never reopen a closed ticket. Rejected as the only path.
- **C. Email link into the app (chosen by the owner):** the email reaches the requester; the score is given behind the normal sign-in and scope. One write path. Cost: a signed-out user signs in first.

## Constraints

- Handlers follow the project pattern: one class per use case in `Proteone.Application/Features/Ratings/`, validators `internal sealed`.
- Ticket writes go through `TicketScope`; the rating write does not change the ticket version (it touches `updated_at` like comments do).
- New error codes and audit actions get Turkish texts (`TranslationCoverageTests`).
- `tools/gen-api-types.sh` after the endpoint changes.
- An ADR records the decision (approach C, rateable outcomes, window, snapshot) and amends `proteone.md` §2/§19.

## Left to the builder

- Exact labels for scores 2–4, and the chart type for the distribution (follow `DistributionChart`).
- Pilot data: a few ratings in `seed-pilot` so the reports show figures.
- Guide page text and screenshots, when the owner asks for the docs.
