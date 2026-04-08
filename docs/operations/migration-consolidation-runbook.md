# Migration Consolidation Runbook

> **When to use:** You have an EF Core project with many accumulated migrations and want to collapse them into a single new `Initial` migration without losing local dev data. Local dev only — never on staging or production.

## TL;DR

```bash
# 1. Save current state
pwsh infra/scripts/db-save-state.ps1

# 2. Note the snapshot path printed at the end, then reset migrations
pwsh infra/scripts/db-reset-migrations.ps1 -SnapshotPath infra/db-snapshots/<timestamp>

# 3. Restore data into the fresh schema
pwsh infra/scripts/db-restore-state.ps1 -SnapshotPath infra/db-snapshots/<timestamp>
```

If anything goes wrong at any step:

```bash
pwsh infra/scripts/db-restore-state.ps1 -SnapshotPath infra/db-snapshots/<timestamp> -UseSafetyNet
# Then restore Migrations/ from <snapshot>/migrations-backup/ if needed
```

## Preconditions

| # | Check | How to verify |
|---|-------|--------------|
| P1 | `Migrations/` is committed in git | `git status apps/api/src/Api/Migrations` |
| P2 | No pending unapplied migrations | `dotnet ef migrations list --project apps/api/src/Api` |
| P3 | API project compiles | `dotnet build apps/api/src/Api` |
| P4 | API container is stopped (or expect a warning) | `docker ps | grep meepleai-api` |
| P5 | No long-running queries on the dev DB | check active connections |

## Detailed steps

### Step 1 — Save state

```bash
pwsh infra/scripts/db-save-state.ps1
```

This creates `infra/db-snapshots/<timestamp>/` containing:

- `safety-net.dump` — full schema+data (your insurance policy)
- `data.dump` — data only, used for restore into the new schema
- `schema-pre.sql` — schema before reset, used for drift detection
- `rowcounts-pre.tsv` — exact row counts per table
- `pdf_uploads.tar.gz` — PDF volume backup (if local storage)
- `manifest.json` — immutable metadata with SHA256 hashes

Pass `-Sanitize` if you plan to share the snapshot off-machine (excludes `Users`, `Sessions`, `RefreshTokens`, `ApiKeys`, `AuditLogs` from `data.dump` only — `safety-net.dump` is always full).

### Step 2 — Reset migrations

```bash
pwsh infra/scripts/db-reset-migrations.ps1 -SnapshotPath infra/db-snapshots/<timestamp>
```

This wraps:

1. `dotnet ef database drop --force`
2. Backup `Migrations/` to `<snapshot>/migrations-backup/`
3. Delete `Migrations/`
4. `dotnet ef migrations add Initial`
5. `dotnet ef database update`
6. Dump `schema-post.sql`, write `reset-result.json`

A pre-flight check runs `dotnet build` first and refuses to proceed if it fails (because you'd end up with a dropped DB and a deleted Migrations folder if the build is broken).

You will be asked to type `yes` to confirm.

### Step 3 — Restore data

```bash
pwsh infra/scripts/db-restore-state.ps1 -SnapshotPath infra/db-snapshots/<timestamp>
```

This:

1. Re-dumps `schema-post.sql` from the live DB
2. Normalizes both `schema-pre` and `schema-post`
3. Diffs them and classifies as `identical`, `minor`, or `significant`
4. If `significant` → STOP, prints diff, exits with code 2
5. Otherwise restores `data.dump` via `pg_restore --data-only --disable-triggers --single-transaction`
6. Resets all sequences via `pg_depend`
7. Verifies row counts against `rowcounts-pre.tsv`
8. Restores PDF volume (if present)
9. Writes `restore-result.json`

## Failure recovery

### Build fails before drop

Pre-flight check rejects the operation. Fix compile errors and retry — DB and Migrations are unchanged.

### `dotnet ef migrations add` fails after drop

DB is dropped, Migrations/ is deleted. The script prints recovery instructions:

```bash
# Restore migrations from backup
Copy-Item -Recurse <snapshot>/migrations-backup <api>/Migrations
# Fix model code, retry
pwsh db-reset-migrations.ps1 -SnapshotPath <snapshot>
```

Or full rollback via safety net:

```bash
pwsh db-restore-state.ps1 -SnapshotPath <snapshot> -UseSafetyNet
```

### Schema drift = significant

Your model code generates a different schema than the saved snapshot. Options:

- **(a)** Fix the model code so the new schema matches the old one, re-run reset
- **(b)** Rollback via safety net
- **(c)** Force restore with `-AllowDrift` (will likely fail on missing/renamed columns)

### `pg_restore` data fails

Single-transaction restore: nothing is committed. DB stays in fresh empty state. Investigate the error, fix, retry — or rollback via safety net.

## Cleanup

Snapshots are stored in `infra/db-snapshots/<timestamp>/` and gitignored. To free space:

```bash
# Delete snapshots older than 30 days
Get-ChildItem infra/db-snapshots -Directory | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | Remove-Item -Recurse -Force
```

## Hard guarantees

- **Localhost only**: every script aborts if `POSTGRES_HOST` is anything other than `localhost` or `127.0.0.1`.
- **Atomic restores**: `pg_restore --single-transaction` means a failed restore leaves the DB in its pre-restore state, never partial.
- **Hash verification**: dump file SHA256 hashes are written to `manifest.json` at save time and re-verified before every restore operation.
- **Build sanity check**: `db-reset-migrations` refuses to drop the DB if `dotnet build` fails.
