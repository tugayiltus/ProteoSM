# Backlog: Bilinen hata önerisi (known error suggestion)

Status: design only. No code. Date: 2026-11-28.

## Gap
- Problems (ADR-0034) have `known_error` status with root cause and workaround; "Makale oluştur" makes an internal draft article. The incident detail shows the linked problem only after IT links it by hand ("Probleme bağla").
- Services (ADR-0036) tag tickets and problems with one `service_id`. Nothing connects the two when an incident arrives.
- Missing: the agent does not see "this service already has a known error" while reading a new incident.
- Reference only (Odoo ITSM Pack): known errors that agents apply on matching incidents.

## Proposal
- On the incident detail (IT staff), when the ticket has a service: card "Bilinen hatalar" listing problems with the same `service_id`, status `known_error`, that the viewer may read (problems are readable by all IT staff, ADR-0034). Max 5, newest first, with a "Tümünü göster" link to `/problems?service=…&status=known_error`.
- Per row: number, title, impact, "Geçici çözüm" summary (truncated).
- Actions: "Probleme bağla" (existing link rules: one incident, one problem; closed problems refuse), and "Geçici çözümü yanıta ekle".
- "Yanıta ekle" inserts only a published text: the linked KB article if it is `published` with `employees` visibility (link via existing reply picker), else nothing. The raw `workaround` field is internal and is never copied to a public reply. The existing server guard (`knowledge.internal_article_in_public_reply`) stays the final check.
- Internal notes (root cause, problem comments) never leave: no field of the problem except an employee-visible published article reaches the requester. Mail templates keep the ADR-0046 rule (no problem variables to employees).
- No matching by text; service match only in v1.

## Screen sketch
```
Arıza INC-2026-000123  Hizmet: VPN
+- Bilinen hatalar (VPN) -----------------------------+
| PRB-2026-000041  Orta  İstemci sertifikası süresi   |
|   Geçici çözüm: sertifikayı yenileyin...            |
|   [Probleme bağla]  [Yanıta ekle: KB-… "VPN sertifika"] |
| PRB-2026-000037  Yüksek ...                         |
|                              Tümünü göster (3)      |
+-----------------------------------------------------+
Yanıta ekle pasif: "Yayımlanmış çalışan makalesi yok"
```

## Dependencies
- ADR-0034, ADR-0036, ADR-0019 (KB visibility, public-reply guard), ADR-0013.
- Item 1 helps (more tickets carry a service). Item 6 (bulk complete) reuses the same link.
- No new table; one query by `service_id` + status; trigram not needed. Index on `problems(service_id, status)` check.

## Acceptance scenarios
1. Given incident with service VPN and a known-error problem on VPN, When IT opens it, Then the card lists that problem.
2. Given no service on the ticket, Then no card, and a hint "Hizmet seçin".
3. Given the problem has an internal draft article only, When "Yanıta ekle" is pressed, Then it is disabled; nothing is inserted.
4. Given a published employees article is linked, Then the reply gets the article link only, and the requester can open it.
5. Given an IT user types the workaround text into a public reply by hand, Then the existing rules apply (no automatic block of free text; owner decision below).
6. Given the incident already links another problem, Then "Probleme bağla" is replaced by the current link.
7. Given an employee, Then the card and its endpoint answer 404.

## Open decisions for the owner
- Also match by category when the ticket has no service?
- Should publishing a known-error article be prompted when a problem becomes `known_error`?
- Block free-text paste of `workaround` into a public reply (hard to detect) or leave to the agent?
