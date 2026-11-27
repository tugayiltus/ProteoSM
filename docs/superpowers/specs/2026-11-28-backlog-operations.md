# Backlog: Operasyon (operations before go-live)

Status: design only. No code. Date: 2026-11-28. Four independent work packages.

## A. Real Entra tenant and AD pilot

Gap
- ADR-0030 sign-in is tested against `mock-oauth2-server` and OpenLDAP (`tools/directory-contract-test.sh`, `docs/release/directory-contract-test.md`). capabilities.md says a real Entra tenant and a real Active Directory are still needed. Mock fidelity and OpenLDAP-for-AD are named open gaps.

Plan
- Pilot checklist with a real tenant and a real AD: app registration, redirect URI on the final host, tenant and domain allow-list, guest refusal, `sub`/object id link by stored id, then same email, new user created without password.
- Local emergency access: with the method set to Entra or LDAP, an admin signs in with a local password. Test: directory unreachable (503 `auth.directory_unavailable`), tenant misconfigured, expired client secret. Prove at least two admins have a local password before switching.
- Disabled user: disabled in the directory -> next sign-in refused; an open session ends only when the app learns of it (document the delay; ADR-0030 decides whether a periodic re-check exists).
- Mobile sign-in: phone browser, in-app browsers and password managers; redirect works on iOS Safari and Android Chrome; cookie `__Host-proteosm` survives the round trip.
- Evidence file `docs/release/directory-real-pilot.md` with date, tenant type, results; secrets never written.

Screen sketch (admin test result)
```
/admin/login-method  Entra
 Bağlantı testi: Başarılı (tenant ... )   Yerel yönetici: 2 hesap hazır
 Kontrol listesi:  [x] Konuk reddi  [x] Devre dışı kullanıcı  [ ] Mobil giriş
```

Dependencies: tenant admin consent from the owner/IT; real HTTPS host and cert; ADR-0030.

Acceptance
1. Given a tenant member, When signing in with Entra, Then the user is linked by id and gets the employee role.
2. Given a guest account, Then refused.
3. Given the directory is disabled for the user, Then sign-in is refused; admin sees the audit row.
4. Given Entra is down, Then an admin signs in locally; employees see a clear Turkish message.
5. Given an iPhone and an Android phone, Then sign-in completes and returns to the page asked for.

Open decisions: which real tenant and domain; session end rule for disabled users (re-check interval); is AD pilot LDAPS or Entra-only.

## B. CI runs the built image; health before "active"; rollback drill

Gap
- `.github/workflows/ci.yml` runs lint, test, E2E and `docker build` of API and Worker, but never starts the images. `tools/deploy.sh` waits up to 180 s for `/api/v1/health` through nginx (capabilities, ADR-0024). `docs/operations.md` Rollback: no migration -> pin the old tag; migrations applied -> restore the pre-update archive (data written after is lost). No automated check that the old image works with the new schema or the reverse.

Plan
- New job (script `tools/ci-image-smoke.sh`, saved once, then reused): start Postgres service, run `migrate` from the built image, start API and Worker images, wait until `/api/v1/health` is `ok`, check Worker heartbeats (`/admin/system/workers` or the DB) and one authenticated API call after `setup-admin`. Measure and print: migration time, time to healthy, image sizes.
- "Active mark": the deploy script writes `deploy/.version` only after the health check passed; until then the previous tag stays the known-good tag. A failed health leaves the old tag running (or a printed rollback command).
- Rollback drill in CI or on a schedule: build N-1 and N images; start N-1, seed, migrate with N, and verify (1) N-1 image against the N schema (is the migration expand-only?), (2) restore the pre-update archive with N-1. A migration that breaks N-1 is flagged: such migrations need the "restore" rollback class in release notes.
- Migration compatibility rule (proposed): migrations are expand/contract; destructive steps ship one release after the code stops using the column.

Dependencies: ADR-0024, ADR-0025, `tools/restore.sh`, `tools/restore-drill.sh`, Docker in CI (runner has it), `tools/` rule: script, not ad hoc commands.

Acceptance
1. Given a PR, When CI runs, Then the images start, migrate, report healthy and the job prints the timings.
2. Given a broken migration, Then the job fails before any deploy and names the migration.
3. Given a deploy whose health check fails, Then `deploy/.version` still names the previous tag.
4. Given a release with a column drop, Then the rollback drill reports "old image incompatible" and CI notes restore-only rollback.
5. Given the drill passes, Then `docs/release/rollback-drill.md` records versions and times.

Open decisions: expand/contract rule mandatory or advisory; run the drill on every PR or nightly/release only (cost); real nginx in CI or direct API.

## C. Off-server backup, restore elsewhere, RPO/RTO

Gap
- ADR-0025: nightly `backup` service at `BACKUP_TIME`, archives with manifest and hashes, retention, `restore.sh`, `restore-drill.sh` on throw-away projects with seeded data (`docs/release/restore-drill.md`). operations.md says "copy the archive off the host" by hand. No automatic off-server copy, no restore on a second real server, no measured RPO/RTO.

Plan
- Off-server copy: after a successful archive, copy to a second location (SFTP/rsync target or object store). Needs the credential outside git and a checksum check on arrival; failure raises a visible alert (`/admin/system`, mail). Encryption of the archive at rest before it leaves the host.
- Restore on another server: fresh host, only `deploy/.env`, certs and the archive; run `tools/restore.sh`; check manifest counts, health, a login, files (attachments in `proteosm_files`), Data Protection keys (without them protected secrets and sessions fail; keys must be in the archive or separate escrow).
- Use realistic data: `seed-pilot --scale` large plus real-shaped attachments; time dump, copy, restore.
- Measure: RPO = time since the last good off-server archive at the worst moment (nightly = up to 24 h; options: more frequent dumps); RTO = clock time from "server lost" to "healthy and signed in" on the other host, measured by a stopwatch script and written to `docs/release/dr-drill.md`.

Screen sketch
```
/admin/system   Yedekleme
 Son yedek: 2026-11-28 02:00  Boyut 412 MB  Sunucu dışı kopya: Tamam (02:07)
 Hedef: RPO 24 sa  Ölçülen RTO: 38 dk (2026-11-28 tatbikatı)
```

Dependencies: ADR-0025, ADR-0024, a second server or VM, storage target, owner decision on retention and encryption key custody.

Acceptance
1. Given a successful nightly backup, Then an encrypted copy exists off-server with a matching SHA-256 within an agreed time.
2. Given the off-server copy fails, Then the admin sees an alert; the local archive is kept.
3. Given a new server with only `.env`, certs and the archive, When running the restore, Then counts match the manifest, health is ok and a user signs in.
4. Given the drill, Then RPO and RTO numbers and the data volume are recorded in the file.
5. Given lost Data Protection keys, Then the drill documents which secrets must be re-entered.

Open decisions: target storage and cost; encryption key custody; RPO target (24 h or less); who runs the drill and how often.

## D. Pilot capacity measurement

Gap
- `tools/perf-measure.sh` times single requests one by one on a seeded M1 machine (2,000 tickets, 500 assets); `docs/release/performance.md` states "no numeric targets (criterion 18)". No concurrency test, no worker load test, no queue depth measurement on the target server.

Plan
- Script `tools/load-pilot.sh` (k6 or a small .NET/Node runner, owner choice) on the real server size: N virtual users (pilot size from the owner) doing a realistic mix: sign in, list, create incident, comment, search, workspace queue.
- Measure: p50/p95/errors at 1x/2x/5x pilot users; concurrent ticket creation (number counter locks, SLA starts); outbox and delivery backlog and time-to-send; SLA monitor cycle time with a large open set; ExportRunner with 100,000 rows; Excel export and CSV commit of 5,000 rows while others work; DB connections and memory of API/Worker.
- Pass criteria agreed first, then written to `docs/release/capacity.md` with machine specs and data volume.

Dependencies: ADR-0014/0015/0020, pilot headcount, target server, seeded data at scale (`seed-pilot --scale`).

Acceptance
1. Given pilot-size users, Then p95 of create/list/search stays under the agreed limit with zero 5xx.
2. Given a burst of tickets, Then every number is unique and every ticket has its two SLA instances.
3. Given 5,000 queued notifications, Then the backlog drains within the agreed time and nothing is sent twice.
4. Given an Excel export during load, Then other requests stay within limits.
5. Result file lists machine, data volume, dates and first bottleneck.

Open decisions: pilot headcount and daily ticket volume; acceptable p95; which tool.
