# Backlog: SLA eskalasyonu (SLA escalation)

Status: design only. No code. Date: 2026-11-28.

## Gap
- ADR-0018 + `SlaMonitor` (60 s): marks warning and breach with conditional updates; writes `sla_events` and outbox events `sla_warning` / `sla_breached`. Recipient: the assignee, or the team's active members when unassigned. Problem timers do the same (ADR-0046).
- Missing: nobody above the team hears about a breach; no single, accountable receiver; the outbox shows "dispatched" but the desk cannot tell queued from delivered mail.
- Reference only (Odoo ITSM Pack): escalation rules that move team, raise priority and notify leaders. We keep only the notify part in v1.

## Proposal
- Escalation contact per team: one optional "eskalasyon alıcısı" (`teams.escalation_user_id`, an active IT staff or admin who may see the team's tickets). Fallback: none (the existing breach mail stays).
- On breach (first time per instance, same conditional update already used) the monitor adds one `sla_escalated` outbox event. Recipient resolved at dispatch: the escalation user if still active and authorized for the ticket (ADR-0013 scope); else skipped with a visible reason. Exactly one mail per breach and instance, never repeated.
- Delivery states shown to admins: `queued` (outbox pending), `sent` (delivery sent), `failed`, `skipped` (no valid recipient, reason). Derived from `notification_outbox` and `email_deliveries`; no new status machine.
- v1 never changes team, assignee or priority. Ticket timeline gets an internal event "SLA ihlali bildirildi: <ad>"; the requester sees nothing.
- Template `sla_escalated` uses the ticket variables plus `slaMetric`, `dueAt`; no internal notes.
- Out of scope: multi-level chains, auto priority raise, auto transfer.

## Screen sketch
```
/admin/teams  > Ağ Ekibi
  Eskalasyon alıcısı   [ Ayşe Yılmaz (Yönetici)   v]
  (SLA ihlalinde tek bildirim gider)

Kayıt detayı (IT) > Geçmiş
  14:05  SLA ihlali (İlk yanıt)
  14:05  Eskalasyon bildirimi: Ayşe Yılmaz  [Kuyrukta | Teslim edildi | Hata | Atlandı]

/admin/email-deliveries  filtre: Tür = sla_escalated
```

## Dependencies
- ADR-0018, ADR-0014/0015 (outbox, leases), ADR-0016 (templates), ADR-0013 (scope check at dispatch).
- Migration: `teams.escalation_user_id`, new template row, new event kind. `TemplateVariables` list for the event.
- `TranslationCoverageTests` for the new audit action and error codes.

## Acceptance scenarios
1. Given an escalation user on the team, When a first-response instance breaches, Then exactly one `sla_escalated` mail is queued, even with two Workers running.
2. Given the same ticket later breaches resolution, Then a second mail is sent (one per instance), not more.
3. Given the escalation user was deactivated, When dispatching, Then no mail is sent, the delivery is `skipped` with reason, and the normal assignee breach mail is unaffected.
4. Given the user lost access to the ticket's team scope, Then skipped, no ticket data leaks.
5. Given SMTP is down, Then the admin list shows `queued` then `failed` after retries; it shows `sent` only after the transport accepted it.
6. Given a breach, Then team, assignee and priority are unchanged.
7. Given a paused instance, Then no escalation.

## Open decisions for the owner
- One contact per team, or one global contact plus team override?
- Should the escalation also go to the team lead when the ticket is unassigned (not proposed)?
- Warning level escalation (before breach): yes or breach only?
