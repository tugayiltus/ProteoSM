# ProteoSM move drill

Run by `tools/proteosm-move-drill.sh` on 2026-11-27T22:44:43Z at commit `e236d98a`: Proteone stack from `0355db4` (images `proteone-*:0355db4b`), ProteoSM images `proteosm-*:latest`. Procedure: `docs/operations.md`, "Proteone to ProteoSM move". Throw-away Compose projects `proteosm-move-drill-fresh`, `proteone-move-drill-old`, `proteosm-move-drill-new`; all removed at the end.

Steps: (1) fresh ProteoSM stack + `setup-admin`; (2) Proteone stack, `seed-pilot --scale 0.2` (Pilot environment), `tools/lib/proteosm_drill.py seed`, `tools/lib/proteosm_move_drill.py extra`, writes stopped, `tools/backup.sh` of that checkout; (3) `tools/restore.sh proteone-20261127-224331.tar.gz` into the ProteoSM stack, counts and digests compared with writes stopped, `tools/lib/proteosm_move_drill.py verify`; (4) the old stack starts again (rollback).

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
| rows of users identical (digest) | f6be57d3f398a7028e48ff92d25d19dc | f6be57d3f398a7028e48ff92d25d19dc | pass |
| rows of tickets identical (digest) | c6d941a325af9303574f4265de556239 | c6d941a325af9303574f4265de556239 | pass |
| rows of ticket_assets identical (digest) | 637fb68d59b59c82a9c96a81e92bf4fd | 637fb68d59b59c82a9c96a81e92bf4fd | pass |
| rows of change_ticket_links identical (digest) | ee448aae07ed38ff047289603b84e25e | ee448aae07ed38ff047289603b84e25e | pass |
| rows of problem_tickets identical (digest) | ef733369f12b557b45a907b708c388b0 | ef733369f12b557b45a907b708c388b0 | pass |
| rows of ticket_attachments identical (digest) | 5d0ed82777d0b0f29db38955ba007570 | 5d0ed82777d0b0f29db38955ba007570 | pass |
| rows of export_jobs identical (digest) | 9500b631ada0aa9e469395bb94d86cc3 | 9500b631ada0aa9e469395bb94d86cc3 | pass |
| rows of email_templates identical (digest) | 9b9319402cc2a1284727bea3a42093f4 | 9b9319402cc2a1284727bea3a42093f4 | pass |
| rows of audit_events identical (digest) | 250eeb585386cff0310d59df0cae078c | 250eeb585386cff0310d59df0cae078c | pass |
| rows of monitoring_sources identical (digest) | 01af89feb3ae928f7801bc92bb539180 | 01af89feb3ae928f7801bc92bb539180 | pass |
| rows of auth_settings identical (digest) | 9e27a0158151b973f1bc705f99d919cc | 9e27a0158151b973f1bc705f99d919cc | pass |
| rows of email_settings identical (digest) | 1e47e3147762cf299f0e764297b7cdb0 | 1e47e3147762cf299f0e764297b7cdb0 | pass |
| the new stack's database and role are proteosm | proteosm | proteosm | pass |
| health | 200 | 200 | pass |
| a session from the Proteone stack ends (cookie renamed): users sign in once more | 401 | 401 | pass |
| the admin signs in with the same password | 200 | 200 | pass |
| the employee signs in with the same password | 200 | 200 | pass |
| attachment downloads with the same size | 2176 | 2176 | pass |
| report export file downloads with the same size | 12470 | 12470 | pass |
| deactivated user cannot sign in | HTTP 401 auth.invalid_credentials | HTTP 401 auth.invalid_credentials | pass |
| custom email template kept | Drill custom account_activation | Drill custom account_activation | pass |
| default email templates kept as defaults | 0 | 0 | pass |
| SMTP password decrypts (same test result as before the move) | ok=True code=None | ok=True code=None | pass |
| LDAP bind password decrypts (same test result) | ok=False code=auth.directory_unavailable | ok=False code=auth.directory_unavailable | pass |
| Entra client secret decrypts (same test result) | ok=False code=auth.entra_tenant_not_found | ok=False code=auth.entra_tenant_not_found | pass |
| monitoring key verifies a signed event | 200 | 200 | pass |
| monitoring key refuses a wrong signature | 401 | 401 | pass |
| the untouched Proteone stack starts again | healthy | healthy | pass |

Result: **all checks passed**.
