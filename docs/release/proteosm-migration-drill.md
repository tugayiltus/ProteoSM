# ProteoSM move drill

Run by `tools/proteosm-move-drill.sh` on 2026-11-28T08:34:52Z at commit `55961eb3`: Proteone stack from `0355db4` (images `proteone-*:0355db4b`), ProteoSM images `proteosm-*:latest`. Procedure: `docs/operations.md`, "Proteone to ProteoSM move". Throw-away Compose projects `proteosm-move-drill-fresh`, `proteone-move-drill-old`, `proteosm-move-drill-new`; all removed at the end.

Steps: (1) fresh ProteoSM stack + `setup-admin`; (2) Proteone stack, `seed-pilot --scale 0.2` (Pilot environment), `tools/lib/proteosm_drill.py seed`, `tools/lib/proteosm_move_drill.py extra`, writes stopped, `tools/backup.sh` of that checkout; (3) `tools/restore.sh proteone-20261128-083340.tar.gz` into the ProteoSM stack, counts and digests compared with writes stopped, `tools/lib/proteosm_move_drill.py verify`; (4) the old stack starts again (rollback).

## Row counts

| Table | Proteone | ProteoSM | Result |
|---|---|---|---|
| `users` | 16 | 16 | pass |
| `tickets` | 402 | 402 | pass |
| `ticket_assets` | 79 | 79 | pass |
| `change_ticket_links` | 2 | 2 | pass |
| `problem_tickets` | 27 | 27 | pass |
| `ticket_comments` | 455 | 455 | pass |
| `ticket_attachments` | 1 | 1 | pass |
| `export_jobs` | 1 | 1 | pass |
| `email_templates` | 23 | 23 | pass |
| `audit_events` | 2667 | 2667 | pass |
| `monitoring_sources` | 2 | 2 | pass |

Tables that grew after the new stack started (Worker jobs on restored data, not part of the archive comparison): email_deliveries notification_outbox sla_events worker_heartbeats.

## Checks

| Check | Expected | Actual | Result |
|---|---|---|---|
| fresh: database and role are proteosm | proteosm | proteosm | pass |
| fresh: health | 200 | 200 | pass |
| fresh: the first admin signs in | 200 | 200 | pass |
| fresh: session cookie is __Host-proteosm | True | True | pass |
| fresh: default email templates exist | True | True | pass |
| the Proteone stack writes a proteone- archive | proteone- | proteone- | pass |
| tools/restore.sh restores the proteone- archive (row counts match its manifest, migrate ran) | 0 | 0 | pass |
| rows of users identical (digest) | df9e37c465584cab036d43f47fcc2006 | df9e37c465584cab036d43f47fcc2006 | pass |
| rows of tickets identical (digest) | b59ad7268ba0da641acd76e82af77e5c | b59ad7268ba0da641acd76e82af77e5c | pass |
| rows of ticket_assets identical (digest) | 475d5027aeff3106e8becd0112066650 | 475d5027aeff3106e8becd0112066650 | pass |
| rows of change_ticket_links identical (digest) | c6530ee816755da3fbbe0a001df14d53 | c6530ee816755da3fbbe0a001df14d53 | pass |
| rows of problem_tickets identical (digest) | 92e9f749de511954b052e8db29dce618 | 92e9f749de511954b052e8db29dce618 | pass |
| rows of ticket_attachments identical (digest) | 53cca108ab2c833d59f2f22875df192c | 53cca108ab2c833d59f2f22875df192c | pass |
| rows of export_jobs identical (digest) | b18713c8338f9af9d9b684169051e783 | b18713c8338f9af9d9b684169051e783 | pass |
| rows of audit_events identical (digest) | c1fb5af5428cd79236ca606bd096f31b | c1fb5af5428cd79236ca606bd096f31b | pass |
| rows of monitoring_sources identical (digest) | 7ae62cec37133ac1b92ce8a59f14cfe8 | 7ae62cec37133ac1b92ce8a59f14cfe8 | pass |
| rows of auth_settings identical (digest) | ebbb26888da0405f5b29b3d8b69c1dbe | ebbb26888da0405f5b29b3d8b69c1dbe | pass |
| rows of email_settings identical (digest) | d1efca87ad60504c26e2cfa6475c3011 | d1efca87ad60504c26e2cfa6475c3011 | pass |
| the new stack's database and role are proteosm | proteosm | proteosm | pass |
| health | 200 | 200 | pass |
| a session from the Proteone stack ends (cookie renamed): users sign in once more | 401 | 401 | pass |
| the admin signs in with the same password | 200 | 200 | pass |
| the employee signs in with the same password | 200 | 200 | pass |
| attachment downloads with the same size | 2176 | 2176 | pass |
| report export file downloads with the same size | 12469 | 12469 | pass |
| deactivated user cannot sign in | HTTP 401 auth.invalid_credentials | HTTP 401 auth.invalid_credentials | pass |
| custom email template byte-identical (subject and body) | True | True | pass |
| every unchanged default gets the ProteoSM wording and footer (same subject) | 22 | 22 | pass |
| no unchanged default still names Proteone | 0 | 0 | pass |
| SMTP password decrypts (same test result as before the move) | ok=True code=None | ok=True code=None | pass |
| LDAP bind password decrypts (same test result) | ok=False code=auth.directory_unavailable | ok=False code=auth.directory_unavailable | pass |
| Entra client secret decrypts (same test result) | ok=False code=auth.entra_tenant_not_found | ok=False code=auth.entra_tenant_not_found | pass |
| monitoring key verifies a signed event | 200 | 200 | pass |
| monitoring key refuses a wrong signature | 401 | 401 | pass |
| the untouched Proteone stack starts again | healthy | healthy | pass |

Result: **all checks passed**.
