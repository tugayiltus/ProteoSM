# Backlog: Çalışana açık hizmet seçenekleri

Status: design only. No code. Date: 2026-11-28.

## Gap
- Employees file "Bir şey çalışmıyor" (incident) or "Bir şey istiyorum" (request with a request type; `requires_approval`, categories with `scope`). ADR-0036 services are internal: employees get 404 on service routes and no service field in DTOs.
- Missing: nothing ties friendly choices ("VPN erişimi", "Yazılım kurulumu", "Yeni çalışan hazırlığı") to the request types and the owning service/team. Employees guess the category.
- Not a new portal and not a full CMDB (separate decisions).

## Proposal
- New `service_options` ("hizmet seçeneği"): employee-facing title and short text (Turkish), kind (`request` or `incident`), optional link to one request type (requests), optional service, `is_active`, sort order. Admin-managed under the hizmet kataloğu.
- Choosing an option on `/tickets/new/*` preselects the request type (and service for item 1) and may show a hint. All existing create rules, approvals and validations still run on the server; the option is only a shortcut and cannot create something the request type forbids.
- Employee endpoint returns only: id, title, short text, kind, request type id/name. It never returns the owner team, member names, assets, internal service description or SLA.
- Internal catalogue (teams, owners, assets) stays admin/IT only. Employee DTOs have no team field.
- Option links to item 1 (service routing) and item 4 (service SLA) by the service id on the option; without a service, the option only prefills the request type.
- Reports can count requests by option (optional).

## Screen sketch
```
Bir şey istiyorum
 Ne lazım?
  ( ) VPN erişimi            -> onay gerekir
  ( ) Yazılım kurulumu
  ( ) Yeni çalışan hazırlığı
  ( ) Diğer (talep tipini kendim seçeyim)
 [ Devam ]
/admin/hizmet-secenekleri
 Başlık  | Tür   | Talep tipi        | Hizmet | Durum
 VPN erişimi | Talep | VPN erişim talebi | VPN    | Aktif
```

## Dependencies
- Request types and categories (ADR-0009 area), ADR-0036, ADR-0013, ADR-0006 (Turkish UI text in locale files; titles stored as admin data).
- Item 1 and item 4 (optional). New ADR small; migration; `TranslationCoverageTests` for new codes.

## Acceptance scenarios
1. Given option "VPN erişimi" linked to request type X, When an employee chooses it, Then the form opens with type X selected and the approval hint if required.
2. Given the request type was deactivated, Then the option is hidden from employees and flagged in admin.
3. Given an employee, When requesting the option list, Then the payload holds no team, owner or asset data.
4. Given an employee calls the internal service routes, Then 404 as before.
5. Given an option with a service and item 1 active, Then the ticket is routed by the service rule.
6. Given an inactive option, Then it is hidden but existing tickets are unchanged.

## Open decisions for the owner
- Free admin-written options (proposed) or generated from request types automatically?
- Do options also cover incidents ("VPN bağlanmıyor"), or requests only in v1?
- Show expected duration (from SLA)? Not proposed: it leaks SLA design.
