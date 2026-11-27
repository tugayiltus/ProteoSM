# Backlog: Hizmete özel SLA (service-specific SLA)

Status: design only. No code. Date: 2026-11-28.

## Gap
- ADR-0018: one company calendar, immutable policy versions; `sla_targets` per ticket kind, priority, optional request type and metric. A request-type target overrides the priority target. ADR-0036: a service never changes an SLA.
- Missing: "VPN P2: first response 30 min, resolution 4 h, 24/7" cannot be expressed. There is no per-service calendar either; the single business calendar applies.
- Reference only (Odoo ITSM Pack): SLA policies per type and priority, 24x7 or business hours, optional service-specific SLA.

## Proposal
- Add an optional `service_id` to `sla_targets` (same immutable policy version model) and a `calendar_mode` per target: `company` (default) or `always` (7/24). No per-service holiday calendars in v1.
- Target selection at ticket creation and at priority change, most specific first: (service + priority + metric) > (request type + metric) > (priority + metric, default). Explicit and tested order.
- Conflict with request type: a request ticket having both a request-type target and a service target uses the request-type target (the request type is the stronger business commitment). Admin sees this rule as text on the policy screen. Owner may flip it.
- Default policy return: the target exists only while the service is active and the policy version holds it; removing the row in the next version sends future tickets back to the default target.
- Running timers keep the version they started on (ADR-0018). A service change on an existing ticket does not re-target running timers in v1 (only priority change does, as today). Closed or stopped timers never change.
- Report and badge show which rule applied: "Hizmet SLA: VPN P2".

## Screen sketch
```
/admin/sla/policy
  Varsayılan hedefler        (mevcut matris)
  Hizmete özel hedefler      [+ Hedef ekle]
  +--------+-----+-----------+--------------+--------+
  | Hizmet | Öncelik | İlk yanıt | Çözüm    | Takvim |
  | VPN    | P2  | 30 dk     | 4 sa         | 7/24   |
  +--------+-----+-----------+--------------+--------+
  Kayıt: yeni sürüm olarak kaydedilir; çalışan sayaçlar eski sürümde kalır.
Kayıt detayı: SLA rozeti  "İlk yanıt 22 dk kaldı  (VPN P2)"
```

## Dependencies
- ADR-0018 (policy versions, instances, `SlaTracker`), ADR-0036, ADR-0046 (problem timers untouched).
- Item 1 sets `service_id` at creation, so the right target starts from minute zero. Without item 1, only IT-set service after creation applies on the next priority change.
- Migration on `sla_targets`; `BusinessTimeCalculator` needs an "always open" rule; unique key of targets widens.
- Reports: SLA figures stay correct because each instance keeps its own target.

## Acceptance scenarios
1. Given VPN P2 target 30 min / 4 h 24x7, When a P2 incident with service VPN is created at 23:50, Then first response is due 00:20.
2. Given the same incident with service SAP (no target), Then the default P2 target applies.
3. Given a request with a request-type target and service VPN with a target, Then the request-type target applies and the badge says so.
4. Given a running VPN timer, When the admin saves a new policy version without the VPN row, Then the running timer keeps its target; new VPN tickets use the default.
5. Given priority changes from P3 to P2 on a VPN ticket, Then the instances take the VPN P2 target from the version they run on.
6. Given IT changes the service after creation, Then running timers are not re-targeted (documented limit).
7. Given a deactivated service, Then its row stays in history and new tickets cannot select the service.

## Open decisions for the owner
- Request type vs service precedence (proposed: request type wins).
- Re-target running timers when the service changes? (not proposed)
- Per-service business hours beyond "7/24"?
