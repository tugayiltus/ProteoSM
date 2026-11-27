# Performance measurement

Run by `tools/perf-measure.sh` on 2026-11-24T23:16:24Z at commit `7110322`. Spec 5.2; acceptance criterion 18 of `proteosm.md` section 20.

- Target: local Release API on http://localhost:5313, database proteone_pilot.
- Machine: Apple M1, 8 logical cores, 16 GiB RAM, macOS 26.7.
- Data volume before the run (tickets and assets as the IT staff account sees them, changes and tasks as the admin sees them, problems as the IT staff account sees them): tickets=2000 assets=500 changes=60 tasks=234 problems=50.
- Runs per operation: 5; times are wall-clock milliseconds from request to the last byte of the body, over HTTP on the same machine (no network, no browser rendering).
- The CSV import commit writes 5,000 assets, so each run grows the database by that many; it is timed once per run (one sample), the preview 5 times.
- No numeric targets are set (criterion 18); outliers are reported to the owner, not tuned.

| Operation | Runs | Median (ms) | p95 (ms) |
|---|---:|---:|---:|
| Ticket list (page of 50) | 5 | 13 | 14 |
| Ticket search | 5 | 12 | 12 |
| Central search (title word) | 5 | 12 | 14 |
| Central search (full ticket number) | 5 | 13 | 13 |
| Problem list (page of 50) | 5 | 8 | 9 |
| Central search (problem title word) | 5 | 12 | 18 |
| Workspace queue (unassigned) | 5 | 13 | 31 |
| Asset list (page of 50) | 5 | 9 | 9 |
| Asset search (brand name) | 5 | 12 | 13 |
| Change calendar (62 days) | 5 | 7 | 8 |
| Report: incidents | 5 | 54 | 65 |
| Report: requests | 5 | 31 | 35 |
| Report: operations | 5 | 28 | 34 |
| Report: transfers | 5 | 7 | 8 |
| Report: assets | 5 | 8 | 8 |
| Report: warranty | 5 | 4 | 6 |
| Report: changes | 5 | 8 | 8 |
| Report: tasks | 5 | 7 | 8 |
| Report: problems | 5 | 17 | 18 |
| Report: licences | 5 | 5 | 8 |
| Drill-down page (incidents, mttr) | 5 | 10 | 12 |
| Synchronous Excel export (incidents, mttr) | 5 | 89 | 124 |
| CSV import preview (5000 rows) | 5 | 142 | 162 |
| CSV import commit (5000 rows, once per run) | 1 | 4274 | 4274 |
