# Backups directory

Local backup artifacts for database reset operations (issue #1320).

## Contents (all gitignored)

| Pattern | Source | Retention |
|---|---|---|
| `*.dump` | `pg_dump -Fc` snapshots | 30 days local; off-machine archive for prod |
| `*.csv` | Mapping exports for vector re-link | 30 days local |
| `*.txt` | Verification artefacts (vector counts, gate outputs) | 30 days local |

## Naming convention

`<YYYY-MM-DD>-<env>-<purpose>.<ext>`

Examples:
- `2026-05-19-dev-pre-game-reset.dump`
- `2026-05-19-dev-game-mapping.csv`
- `2026-05-19-dev-vector-count-pre.txt`

## NEVER commit dump files

Dumps may contain PII, secrets, or unreleased product data. The `.gitkeep` marker is the only committed file in this directory.
