# Backlog: Hizmet bazlı yönlendirme (service-based routing)

Status: design only. No code. Date: 2026-11-28.

## Gap
- Today `CreateTicket.cs` always sends a new ticket to the one active default intake team (`teams.is_default_intake`, `TicketNoIntakeTeam` if none). The employee never chooses a team.
- Services exist (ADR-0036, `services.owner_team_id`) but ADR-0036 states the service never routes a ticket, picks a team or changes an SLA. It is only a hint set by IT after creation.
- Monitoring sources (ADR-0041) already carry their own `TeamId` and `ServiceId`; QR incidents (ADR-0028) and the portal forms use the default intake team.
- Missing: the employee form has no service choice, so "VPN kaydı" cannot reach the Ağ Ekibi queue without a manual transfer by the desk.
- Reference only (Odoo ITSM Pack): services in a catalogue with a support team, tickets routed to that team. We take the idea, not the screens.

## Proposal
- Optional `service_id` on the incident and request create forms. The service decides the target: `services.owner_team_id`.
- Target rule, in order: service chosen and active and owner team active and has at least one active member -> owner team; otherwise the default intake team (karşılama kuyruğu). The ticket keeps the service either way.
- The server decides. The client sends only the service id; a client-sent team id is ignored and refused.
- Later label change: changing the service on an existing ticket (existing set-service action, ADR-0036) never moves the team. A transfer stays its own action with reason (existing rule). The UI may show "Hizmetin ekibi: Ağ Ekibi. Aktar?" as a hint.
- Employee sees only active services that are marked "çalışana açık" (see item 9). Without that flag the form shows no service list; routing then uses the default team as today.
- Contracts kept: portal create payload without `serviceId` behaves exactly as today; QR create (`assetId`) unchanged, with the asset's single active service as an optional suggestion only; monitoring webhook keeps its source team and service and is not rerouted.
- Audit: `Created` timeline event data gets `routedBy: service|default` and the fallback reason code (`service_missing`, `service_inactive`, `team_inactive`, `team_empty`).

## Screen sketch
```
Bir şey çalışmıyor
+---------------------------------------------+
| Konu            [ VPN bağlanmıyor         ] |
| Hizmet          [ VPN                    v] |
|   > "VPN kaydı Ağ Ekibi kuyruğuna gönderilecek" |
| Kategori        [ Ağ                     v] |
| Açıklama        [                          ] |
|                              [Gönder]       |
+---------------------------------------------+
Hizmet seçilmediyse: "Servis Masası kuyruğuna gönderilecek"
```
Ticket detail (IT): `Hizmet: VPN   Ekip: Ağ Ekibi   (Yönlendirme: hizmetten)`.

## Dependencies
- ADR-0036 (services), ADR-0013 (ticket scope), ADR-0028 (QR), ADR-0041 (webhook), `SerializationLocks.DefaultIntake`.
- Item 9 (which services employees may pick). Item 4 (service SLA) reads the same service id at creation.
- New ADR: reverses the ADR-0036 sentence "the service never routes". Write as a new ADR, supersede only that clause by amendment.
- Employee DTOs must not expose owner team or internal service data (ADR-0036 returns 404 to employees on service routes). Needs a separate employee-safe list endpoint.

## Acceptance scenarios
1. Given service VPN owned by an active Ağ Ekibi with members, When an employee files an incident with VPN, Then the ticket's team is Ağ Ekibi and the timeline says routed from service.
2. Given no service chosen, When the employee files, Then the team is the default intake team, as before.
3. Given VPN's owner team was deactivated, When filed, Then the ticket goes to the default intake team with reason `team_inactive`, and the form never shows an error to the employee.
4. Given a ticket routed to Ağ Ekibi, When IT changes its service to SAP, Then the team stays Ağ Ekibi and no transfer event is written.
5. Given a client sends `teamId` in the payload, Then the request is refused (400) or the field is ignored; the team is still decided by the server.
6. Given a QR incident for a laptop with one active service, Then the service is a suggestion only; saved only if the employee confirms.
7. Given a monitoring alert for a source with its own team, Then the ticket goes to the source team, not the service owner team.
8. Given an employee, When calling the IT service list, Then 404; the employee list shows only name and description of employee-visible services.

## Open decisions for the owner
- Fallback when the owner team has no active member: default team (proposed) or still the owner team?
- Should the desk see a "yönlendirme düzeltme" count in reports (tickets moved right after routing)?
- Is a per-category fallback needed when no service is picked (not proposed)?
