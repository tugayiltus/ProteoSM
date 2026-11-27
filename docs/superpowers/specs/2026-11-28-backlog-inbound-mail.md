# Backlog: Gelen posta (inbound mail)

Status: design only. No code. Date: 2026-11-28.

## Gap
- Only outgoing mail exists (ADR-0015, ADR-0045: SMTP, outbox, deliveries, templates). `proteone.md`/capabilities list no inbound path. Replies by email to a ticket mail are lost.
- Reference only (Odoo ITSM Pack): team e-mail aliases create incidents and acknowledgement mails.

## Proposal
- New Worker job `InboundMailReader`: polls one shared mailbox (IMAP over TLS, credentials as Data Protection ciphertext like ADR-0045; settings screen `/admin/gelen-posta` with test). Row lease like other jobs (ADR-0014); one reader at a time.
- Per message, in order:
  1. Duplicate delivery check: store `Message-ID` (and mailbox UID+UIDVALIDITY) in `inbound_messages` with a unique index; a repeat is ignored. Mark the mail processed in the mailbox only after the DB commit (at-least-once, made idempotent by the unique key).
  2. Sender check: the From address must equal an active user's normalized email. Unknown or inactive sender, or no SPF/DKIM pass in `Authentication-Results` when the relay provides it -> review queue, never a ticket. Auto-replies, bounces and mailing-list headers are dropped and counted.
  3. Matching: reply to a ticket mail is matched by the ticket number in the subject and by `In-Reply-To` to a stored outgoing `Message-ID`. A match adds a public comment (requester) only if the sender is the requester; any other sender goes to review.
  4. No match: v1 does not create a ticket; message goes to the review queue ("Eşleşmeyen e-postalar"). (Creating tickets from mail is an open decision.)
- Review queue (admin and desk): sender, subject, received time, reason (`unknown_sender`, `no_ticket`, `not_requester`, `suspicious`). Actions: attach to a ticket (by number), create ticket for a known user, discard with reason. Text only; HTML stripped; attachments pass the existing type/scan rules (ADR-0012) or are dropped with a note. Size cap.
- Quoted history and signatures are cut by a conservative rule; the original is kept in the inbound row for review, never shown to employees.
- Audit and a heartbeat for the job; `/admin/system` shows last read and queue size.

## Screen sketch
```
/admin/eslesmeyen-eposta   (İnceleme kuyruğu: 3)
 Gönderen              Konu                  Neden            İşlem
 x@firma.com           Re: INC-2026-000211   Kayıtlı değil    [Kayda ekle] [Reddet]
 ayse@firma.com        VPN çalışmıyor        Kayıt bulunamadı [Arıza aç]   [Reddet]
Ayarlar: Sunucu [ imap... ]  Kutu [ destek@ ]  Parola [Kayıtlı]  [Test et]
```

## Dependencies
- ADR-0015, ADR-0045 (settings pattern, `SmtpTargetPolicy`-like host check needed for IMAP), ADR-0012, ADR-0013, ADR-0014, ADR-0044/rating unaffected.
- Real mailbox for pilot; outgoing mails must carry `Message-ID` that the app stores (small change).
- New ADR, migration, security review (spoofing, mail loops, attachments). Operations: mailbox quota and credentials.

## Acceptance scenarios
1. Given a requester replies to the ticket mail, When the reader runs, Then one public comment appears on that ticket and the mail is marked processed.
2. Given the same message is delivered twice, Then one comment exists.
3. Given the sender is not an active user, Then the review queue gets it and nothing is added to any ticket.
4. Given a reply from a user who is not the requester, Then review queue, reason `not_requester`.
5. Given an auto-reply or bounce, Then dropped, counted, no loop with the outgoing mail.
6. Given the IMAP server is down, Then the job heartbeat shows the error and `/health` stays ok or degraded by the existing rule; no message is lost.
7. Given a crash after DB commit but before marking the mail, Then the next run finds the duplicate key and only marks it.

## Open decisions for the owner
- Create tickets from unmatched mail from known users (v2), or only review?
- IMAP, or Microsoft Graph for the Entra tenant?
- Shared mailbox address and who staffs the review queue?
