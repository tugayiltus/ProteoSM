# Backlog: Toplu tamamlama (bulk completion from a problem)

Status: design only. No code. Date: 2026-11-28.

## Gap
- ADR-0034: a problem links incidents (an incident belongs to one problem). Resolving the problem does not touch linked tickets; it only mails them (ADR-0046). Each incident is completed one by one (resolution text, outcome rules, open tasks warning, approvals).
- Reference only (Odoo ITSM Pack): resolve every linked incident with the permanent fix.
- Missing: after "Çözüldü" on a problem with 8 incidents, the desk does 8 manual completions.

## Proposal
- On the problem detail (status `resolved`, or `known_error` for workaround-only completion later): action "Bağlı arızaları tamamla".
- Step 1, preview (read-only, no write): per linked incident show eligible or not and the reason. Summary line "8 kayıt seçildi, 2 kayıt işlem dışında".
- Not eligible: already completed or cancelled; viewer lacks work rights (not admin and not in the ticket's team, ADR-0013); on_hold with approval pending; incident outcome rules not satisfiable by the shared text; open tasks do not block (existing behavior: warn) but are counted.
- Step 2, commit: one shared resolution text (default from the problem's `resolution`, editable) and outcome `resolved`. Each ticket is completed through the same handler as a single completion (`TicketWork`: scope, version, change, one save), one transaction per ticket, so a stale version or lost right affects only that row.
- The preview returns each ticket's version; commit sends those versions; a changed ticket becomes "atlandı: kayıt değişti" and is not forced.
- Result is partial and reported per row: tamamlandı / atlandı (reason code). No all-or-nothing rollback.
- Public text: the shared resolution is employee-visible. The screen warns and the problem's internal root cause is not prefilled; only the `resolution` field is offered.
- Audit: one `problems.bulk_completed` row (counts) plus the normal ticket audit rows. Per-user limit, e.g. 50 tickets per run.

## Screen sketch
```
PRB-2026-000041 > Bağlı arızaları tamamla
Çözüm metni (çalışana görünür): [ Sertifika yenilendi ...            ]
8 kayıt seçildi, 2 kayıt işlem dışında
 [x] INC-2026-000101  Ayşe Y.   Tamamlanabilir
 [x] INC-2026-000104  Ali K.    Tamamlanabilir
 [ ] INC-2026-000090  Veli T.   İşlem dışı: yetkiniz yok
 [ ] INC-2026-000087  Zeynep A. İşlem dışı: zaten tamamlandı
                         [Vazgeç]  [8 kaydı tamamla]
Sonuç: 7 tamamlandı, 1 atlandı (kayıt değişti)
```

## Dependencies
- ADR-0034, ADR-0046 (mails: `ticket_cause_resolved`, `ticket_completed` per ticket; avoid duplicate-looking mails, see decisions), ADR-0013, ADR-0044 (rating window starts per ticket), ADR-0018 (SLA stops per ticket).
- Reuses `TicketWork`; no new status. Item 3 and item 5 share the "linked record" pattern.

## Acceptance scenarios
1. Given a resolved problem with 10 incidents (2 completed already), When the preview opens, Then 8 are eligible and 2 listed as not processed with reasons.
2. Given one incident edited by someone after the preview, When committing, Then that row is skipped with `concurrency` reason and the other rows complete.
3. Given the user is not in team B, Then team B's incident is not eligible and is never completed.
4. Given 8 completions, Then each ticket has its own timeline event, SLA stop, `completed_at` and rating window.
5. Given an incident awaiting approval, Then it is not eligible.
6. Given the commit is sent twice (double click), Then no ticket is completed twice and the second result shows all "zaten tamamlandı".
7. Given an employee, Then 404 on every bulk route.

## Open decisions for the owner
- Send both `ticket_completed` and `ticket_cause_resolved` to the requester, or suppress one in bulk?
- Allow bulk completion from `known_error` (workaround) with outcome `resolved`? (not proposed)
- Cap per run (50 proposed)?
