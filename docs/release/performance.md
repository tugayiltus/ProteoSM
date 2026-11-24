# Performance measurement

Run by `tools/perf-measure.sh` on 2026-11-24T13:47:38Z at commit `970abea`. Spec 5.2; acceptance criterion 18 of `proteone.md` section 20.

- Target: local Release API on http://localhost:5313, database proteone_pilot.
- Machine: Apple M1, 8 logical cores, 16 GiB RAM, macOS 26.7.
- Data volume before the run (tickets and assets as the IT staff account sees them, changes and tasks as the admin sees them, problems as the IT staff account sees them): tickets=2000 assets=500 changes=60 tasks=234 problems=50.
- Runs per operation: 5; times are wall-clock milliseconds from request to the last byte of the body, over HTTP on the same machine (no network, no browser rendering).
- The CSV import commit writes 5,000 assets, so each run grows the database by that many; it is timed once per run (one sample), the preview 5 times.
- No numeric targets are set (criterion 18); outliers are reported to the owner, not tuned.

| Operation | Runs | Median (ms) | p95 (ms) |
|---|---:|---:|---:|
| Ticket list (page of 50) | 5 | 12 | 14 |
| Ticket search | 5 | 14 | 15 |
| Central search (title word) | 5 | 11 | 13 |
| Central search (full ticket number) | 5 | 11 | 21 |
| Problem list (page of 50) | 5 | 8 | 14 |
| Central search (problem title word) | 5 | 10 | 11 |
| Workspace queue (unassigned) | 5 | 12 | 24 |
| Asset list (page of 50) | 5 | 7 | 8 |
| Asset search (brand name) | 5 | 11 | 16 |
| Change calendar (62 days) | 5 | 39 | 67 |
| Report: incidents | 5 | 79 | 181 |
| Report: requests | 5 | 48 | 75 |
| Report: operations | 5 | 30 | 33 |
| Report: transfers | 5 | 8 | 9 |
| Report: assets | 5 | 8 | 9 |
| Report: warranty | 5 | 6 | 6 |
| Report: changes | 5 | 9 | 18 |
| Report: tasks | 5 | 7 | 8 |
| Report: problems | 5 | 13 | 14 |
| Drill-down page (incidents, mttr) | 5 | 9 | 10 |
| Synchronous Excel export (incidents, mttr) | 5 | 117 | 174 |
| CSV import preview (5000 rows) | 5 | 169 | 223 |
| CSV import commit (5000 rows, once per run) | 1 | 7624 | 7624 |
