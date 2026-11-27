# Backlog: Değişiklik şablonu ve kapanış değerlendirmesi

Status: design only. No code. Date: 2026-11-28.

## Gap
- ADR-0031/0032: changes have risk, impact, window, three plans (implementation, verification, backout), tasks per phase, single-stage approval by an eligible admin, plan fingerprint, close outcomes (`successful`, `rolled_back`, `failed`) with verification note, actual start/end and closing note. Releases group changes (ADR-0040).
- Missing: every change starts empty (the same VPN certificate renewal is retyped); closing captures only free notes, so "what really happened, what was outside the plan, what follows" is not structured or reportable.
- CAB and emergency change: deliberately absent (single-stage approval). Reference only (Odoo ITSM Pack): standard change models, CAB, post-implementation review.

## Proposal
Part A, template ("Şablondan değişiklik"):
- Admin-managed `change_templates`: name, owner team, default risk, impact, affected service, the three plans, a list of task titles per phase, active flag, version.
- "Yeni değişiklik > Şablondan" copies fields and creates the tasks as ordinary tasks in `draft`. After copying there is no link that changes the change; template edits never touch existing changes. The record keeps `template_id` and template version only for reporting.
- A template never skips approval. Pre-approved standard change is a separate decision (below).
Part B, closing review (when closing):
- Structured fields beside the existing notes: `actual_result` (as planned / with deviations / not achieved), `unplanned_events` text (required when deviations), `follow_up_actions` (list: text + optional new task or problem link).
- Follow-up creates a normal task or lets IT link/open a problem; nothing is created silently.
- Part of the close dialog; Başarılı/Geri alındı/Başarısız rules stay as in ADR-0032. Review fields are not in `ChangePlanFingerprint` (the change is already closing) and never void approval.
- A "Kapanış değerlendirmesi" report figure: closed changes by outcome and by "planned vs deviated".

## Screen sketch
```
Yeni değişiklik
  ( ) Boş değişiklik   (x) Şablondan: [ VPN sertifika yenileme  v]
  Uygulama planı  [ ön dolu ]  Doğrulama [ ön dolu ]  Geri dönüş [ ön dolu ]
  Görevler: Hazırlık 1, Uygulama 2, Doğrulama 1, Geri dönüş 1   (taslak)
Onay ayrı: "Onaya gönder" adımı değişmez.

Kapat > Değerlendirme
  Sonuç        (x) Plana uygun  ( ) Sapmalı  ( ) Hedefe ulaşılamadı
  Plan dışı durum [ ........ ]   (sapmada zorunlu)
  Takip aksiyonları  [+ Görev ekle] [+ Problem bağla]
```

## Dependencies
- ADR-0031, ADR-0032, ADR-0033, ADR-0034, ADR-0040, ADR-0036 (service), reports (ADR-0020). New ADR; migration; `TranslationCoverageTests`; admin screen `/admin/change-templates`.
- Change plan gating (tasks per phase) is unchanged.

## Acceptance scenarios
1. Given template "VPN sertifika yenileme", When creating a change from it, Then plans, risk, service and 5 draft tasks are filled and the change is `draft`.
2. Given a later template edit, Then existing changes keep their text.
3. Given the change is submitted, Then approval works exactly as before, including author/editor exclusion.
4. Given result "sapmalı" without unplanned-event text, When closing, Then validation fails with a Turkish message.
5. Given follow-up action "task", Then an ordinary task is created in the owner team and linked, in the same save as the close.
6. Given a closed change, Then the review fields are read-only.
7. Given an inactive template, Then it is not offered; changes made from it stay.

## Open decisions for the owner
- CAB (several approvers) and emergency change: separate decision; keep single admin approval for now?
- Pre-approved "standard change" (template skips approval)? Not proposed in this design.
- Who edits templates: admin only (proposed) or owner-team leads?
