# Backlog: Kritik kesinti (critical outage)

Status: design only. No code. Date: 2026-11-28.

## Gap
- Announcements (ADR-0042): title, body, level, start/end, no link to a service or records. Services detail (ADR-0036) shows assets, open records, changes and problems, but no outage state. P1 exists in the priority matrix.
- Monitoring (ADR-0041) opens incidents per alert; nothing groups them.
- Missing: one place that says "VPN is down, coordinator X, last update 14:20" for IT and a safe public line for employees; related incidents are not grouped under a parent.
- Reference: Odoo ITSM Pack marks P1 tickets as major incidents (idea taken). Parent/child incident grouping is a ProteoSM proposal, not in Odoo.

## Proposal
- New record "Kesinti" (`outages`): service (required), status `active` / `resolved`, started at, coordinator (active IT staff or admin), optional location, internal note, and a public text (employee line, optional). Updates are append-only entries (`outage_updates`, with a visibility: internal or public).
- Declare from a P1 incident ("Kritik kesinti ilan et") or from the service detail. One active outage per service (unique partial index).
- Link incidents to the outage as children: a ticket belongs to at most one outage; parent is the declaring incident or none. Linking never changes status, SLA, team or version rules of the ticket (like ADR-0034 links).
- Access: IT staff read all outages (like problems). Writes: coordinator, admin, owner-team members. Linked tickets outside the viewer's scope show number/status without link (`canOpen` false), as ADR-0034/0036.
- Employees: no outage routes (404). Only the public text appears, through the existing announcement banner: declaring an outage may create an announcement with the public text, in the owner's choice; ending the outage ends that announcement. The announcement carries no ticket numbers, names or internal notes.
- Coordinator and last update show on the service detail card and the İş listem.

## Screen sketch
```
/services/12  VPN
+- Aktif kesinti --------------------------------+
| Başladı 13:40   Koordinatör: Mehmet K.         |
| Lokasyon: Ankara merkez   Son güncelleme 14:20 |
| "Sertifika sunucusu yanıt vermiyor."           |
| Bağlı arızalar (8)  [Güncelleme ekle] [Bitir]  |
+------------------------------------------------+
Çalışan şeridi: "VPN erişiminde sorun var. Çalışıyoruz."
```

## Dependencies
- ADR-0036, ADR-0042 (announcements), ADR-0013, ADR-0034 (link pattern), item 6 (bulk complete of linked incidents works the same way), item 1 (service on tickets).
- New ADR, migration, event kinds on tickets, `TranslationCoverageTests`, mail template optional (not v1).
- Full CMDB and an extra portal remain out of scope.

## Acceptance scenarios
1. Given a P1 incident on VPN, When IT declares an outage, Then the service shows an active outage and the incident is its first linked record.
2. Given an active VPN outage, When IT declares another for VPN, Then 409.
3. Given 8 incidents linked, When the outage ends, Then linked tickets are unchanged (no auto close).
4. Given an employee, Then no outage endpoint answers (404) and the banner shows only the public text.
5. Given a linked ticket in another team's scope, Then an IT user sees number and status without a link.
6. Given an internal update, Then it never appears in the banner or any employee DTO.
7. Given the coordinator is deactivated, Then admin can reassign; the outage stays active.

## Open decisions for the owner
- Auto-create the announcement or keep it a manual second step?
- Need an email to affected requesters of linked tickets (ADR-0046 style, ticket variables only)?
- Parent/child: only grouping (proposed) or also propagate resolution?
