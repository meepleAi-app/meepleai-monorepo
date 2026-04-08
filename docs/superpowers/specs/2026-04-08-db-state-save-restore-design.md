# DB State Save/Restore for Migration Consolidation — Design

**Status**: Draft
**Author**: Brainstormed with Claude
**Date**: 2026-04-08
**Scope**: One-shot developer tool for local dev database

## Goal

Provide a safe, three-step PowerShell workflow to:
1. Snapshot the current local PostgreSQL state (data + schema + PDF volume) before a migration consolidation,
2. Wipe migrations and rebuild the database from a fresh consolidated `Initial` migration,
3. Restore data into the fresh schema after verifying schema compatibility.

The primary use case is the one-off operation of collapsing the accumulated EF Core migration history into a single `Initial` migration without losing seeded/working data.

## Non-Goals

- Admin web UI (`/admin`) feature — out of scope.
- Remote/staging/production database support — hard-blocked via localhost check.
- Backup to R2/S3 — `infra/scripts/backup.sh` already covers this for staging DR.
- Scheduling or cron automation.
- Automatic rollback between EF CLI steps — documented manual procedure only.
- Multi-database support — only the local `meepleai` database.
- Data transformations for renamed/removed columns — the drift response is "fix the model code", not "patch the dump".

## User Decisions Recorded

| # | Decision | Choice |
|---|---|---|
| 1 | Scope | Local PowerShell, one-shot |
| 2 | Data preservation | Full dump by default, opt-in `-Sanitize` for sensitive tables; `__EFMigrationsHistory` always excluded from data dump |
| 3 | Schema drift handling | Fail-fast with visible diff; user decides between fix / rollback / force |
| 4 | Orchestration | Three separate scripts (save, reset, restore) with safety-net, localhost-only enforcement |
| 5 | Sanitize scope | Applies only to `data.dump`; `safety-net.dump` is always full |
| 6 | EF step rollback | Not automatic; documented manual rollback via safety-net |
| 7 | Git uncommitted changes | Warning only, non-blocking |
| 8 | Shared helper script | Yes (`db-snapshot-common.ps1`) |
| 9 | Makefile targets | Yes |
| 10 | Drift "minor" heuristic | Liberal: treat whitespace, ordering, and cosmetic diffs as "minor" and proceed; only structural changes trigger STOP |
| 11 | Sequence reset | Automatic, no flag |
| 12 | PDF volume handling | Include by default when local storage is detected |

## Architecture Overview

Three complementary PowerShell scripts in `infra/scripts/`, a shared helper module, and a gitignored snapshot directory tree:

```
infra/
├── scripts/
│   ├── db-snapshot-common.ps1    # Shared helpers (dot-sourced)
│   ├── db-save-state.ps1         # Step 1: snapshot
│   ├── db-reset-migrations.ps1   # Step 2: wipe migrations, fresh schema
│   └── db-restore-state.ps1      # Step 3: diff schema, restore data
└── db-snapshots/                 # gitignored
    └── 20260408-143522/
        ├── safety-net.dump       # pg_dump -Fc (schema+data, ALWAYS full)
        ├── data.dump             # pg_dump -Fc --data-only (respects -Sanitize)
        ├── schema-pre.sql        # pg_dump -s before reset
        ├── schema-post.sql       # pg_dump -s after reset (written by reset + restore)
        ├── schema-pre.normalized.sql
        ├── schema-post.normalized.sql
        ├── schema-diff.txt
        ├── rowcounts-pre.tsv
        ├── pdf_uploads.tar.gz    # optional, local storage only
        ├── migrations-backup/    # copy of Migrations/ before deletion
        ├── manifest.json         # metadata, hashes, phase tracking
        ├── README.md             # copy-pasteable commands for next steps
        ├── reset.log             # stdout+stderr from reset script
        └── restore.log           # stdout+stderr from restore script
```

### Operator Flow

```
1. pwsh infra/scripts/db-save-state.ps1 [-Sanitize] [-IncludePdfVolume:$false]
   → creates infra/db-snapshots/<ts>/ with safety-net, data, schema-pre, (pdf_uploads), manifest

2. pwsh infra/scripts/db-reset-migrations.ps1 -SnapshotPath infra/db-snapshots/<ts>
   → drops DB, backs up + deletes Migrations/, adds new Initial, applies it, writes schema-post

3. pwsh infra/scripts/db-restore-state.ps1 -SnapshotPath infra/db-snapshots/<ts>
   → normalizes + diffs schemas, restores data.dump, resets sequences, verifies row counts
```

### Principles

- **Safety-first**: localhost-only hard check in every script; any script that touches `POSTGRES_HOST != localhost/127.0.0.1` aborts before any destructive action.
- **Session isolation**: each run produces an immutable timestamped directory, never overwrites.
- **Stateless between scripts**: script 2 and 3 take only `-SnapshotPath`; no hidden global state.
- **No automagic orchestration across scripts**: user explicitly runs each step. Scripts 2 and 3 do orchestrate their internal phases.
- **Audit trail**: `manifest.json` tracks SHA256 hashes, phases completed, and timestamps for forensics.
- **Single-transaction restores**: every `pg_restore` runs in `--single-transaction` so failures leave no partial state.

## Component 1: `db-save-state.ps1`

### Purpose
Create a timestamped snapshot of the local DB (data + schema) with a full safety-net, before the operator performs a destructive migration reset.

### Parameters

| Param | Type | Default | Description |
|---|---|---|---|
| `-Sanitize` | switch | `$false` | Exclude sensitive tables (`Users`, `Sessions`, `RefreshTokens`, `ApiKeys`, `AuditLogs`) from `data.dump`. Never affects `safety-net.dump`. |
| `-IncludePdfVolume` | nullable bool | `$null` (auto: true if local storage) | Include the `meepleai_pdf_uploads` Docker volume as `pdf_uploads.tar.gz`. |
| `-SnapshotRoot` | string | `$PSScriptRoot/../db-snapshots` | Base directory for the snapshot. |
| `-SecretFile` | string | `$PSScriptRoot/../secrets/database.secret` | Credentials file. |
| `-StorageSecretFile` | string | `$PSScriptRoot/../secrets/storage.secret` | Read to detect storage provider. |

### Pre-flight checks (fail-fast, exit 1)

1. `pg_dump` and `psql` v16+ available on PATH.
2. `database.secret` exists and contains `POSTGRES_HOST/PORT/DB/USER/PASSWORD`.
3. `POSTGRES_HOST` is `localhost` or `127.0.0.1`. Otherwise: hard abort with error message naming the host.
4. PostgreSQL reachable (`pg_isready`).
5. `meepleai-postgres` container is running (via `docker inspect`), OR native PG is reachable — either is acceptable.
6. `SnapshotRoot` writable.
7. Free disk space > 2 × current DB size, computed via `pg_database_size('meepleai')`.
8. `storage.secret` read: detect `STORAGE_PROVIDER`. If `local`, inspect `meepleai_pdf_uploads` volume and record file count + size.

### Actions

1. Create `$SnapshotRoot/$(Get-Date -Format 'yyyyMMdd-HHmmss')/` → assign to `$snap`.
2. **Safety net dump** — `pg_dump -Fc --no-owner --no-acl -f $snap/safety-net.dump`. Always full schema + data + `__EFMigrationsHistory` + sensitive tables. **Fatal if this step fails.**
3. **Schema pre** — `pg_dump -s --no-owner --no-acl -f $snap/schema-pre.sql`.
4. **Data dump** — `pg_dump -Fc --data-only --no-owner --no-acl --exclude-table=*__EFMigrationsHistory* $(excludeSensitiveArgs) -f $snap/data.dump`.
5. **Row counts pre** — for every user table (enumerated via `information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema')`), run `SELECT COUNT(*) FROM "<schema>"."<table>"` and write `$snap/rowcounts-pre.tsv` with columns `schema TAB table TAB count`. Uses exact `COUNT(*)` rather than `pg_stat_user_tables.n_live_tup` to avoid false mismatches from stale statistics.
6. **PDF volume** (if `-IncludePdfVolume` resolves to `$true`):
   ```bash
   docker run --rm -v meepleai_pdf_uploads:/src -v $snap:/dst alpine \
     tar czf /dst/pdf_uploads.tar.gz -C /src .
   ```
   Record file count and hash in manifest.
7. **Manifest** — write `$snap/manifest.json`:
   ```json
   {
     "version": 1,
     "createdAt": "2026-04-08T14:35:22Z",
     "database": "meepleai",
     "host": "localhost",
     "dbSizeBytes": 123456789,
     "sanitize": false,
     "excludedTables": [],
     "storageProvider": "local",
     "pdfVolumeIncluded": true,
     "pdfFileCount": 42,
     "files": {
       "safetyNet": { "name": "safety-net.dump", "sha256": "...", "bytes": 0 },
       "dataDump":  { "name": "data.dump",       "sha256": "...", "bytes": 0 },
       "schemaPre": { "name": "schema-pre.sql",  "sha256": "...", "bytes": 0 },
       "pdfVolume": { "name": "pdf_uploads.tar.gz", "sha256": "...", "bytes": 0 }
     },
     "rowCountsPre": { "Administration.Users": 42, "GameManagement.Games": 101 }
   }
   ```
8. **README** — write `$snap/README.md` with exact commands for steps 2 and 3 (copy-pasteable, with snapshot path already interpolated).
9. **Summary to stdout** — paths, sizes, "next: `pwsh db-reset-migrations.ps1 -SnapshotPath $snap`".

### Error handling

- Any step fails → `throw`, partial snapshot directory removed, exit 1.
- Safety-net failure is fatal and no further dumps are attempted.
- If data dump fails but safety-net is OK, the safety-net is preserved and clearly reported.

## Component 2: `db-reset-migrations.ps1`

### Purpose
Perform the controlled wipe: drop DB, delete `Migrations/`, create and apply a fresh `Initial` migration. Dangerous steps gated behind an explicit confirmation prompt.

### Parameters

| Param | Type | Default | Description |
|---|---|---|---|
| `-SnapshotPath` | string | *required* | Path to snapshot from step 1. Validates existence of `manifest.json`, `safety-net.dump`, `data.dump`, `schema-pre.sql`. |
| `-InitialName` | string | `"Initial"` | Name for the new consolidated migration. |
| `-ApiProjectPath` | string | `$PSScriptRoot/../../apps/api/src/Api` | Path to the API `.csproj`. |
| `-MigrationsFolder` | string | `"$ApiProjectPath/Migrations"` | Folder to remove. |
| `-Force` | switch | `$false` | Skip confirmation prompt. |
| `-DryRun` | switch | `$false` | Print commands without executing. |

### Pre-flight checks

1. `SnapshotPath` contains the expected files; manifest hashes verify against actual files.
2. Manifest `createdAt` age check: warn if > 24h, do not block.
3. `POSTGRES_HOST` localhost enforcement.
4. `dotnet` and `dotnet ef` tools present.
5. API project found at `$ApiProjectPath`.
6. `git status` check inside the project: warn (non-blocking) if `Migrations/` has uncommitted changes.
7. Repo working directory cleanliness: warn if dirty, non-blocking.

### Confirmation prompt

Printed once, plain text:

```
This will:
  1. DROP database 'meepleai' on localhost:5432
  2. DELETE folder apps/api/src/Api/Migrations/ (N files)
  3. CREATE new migration 'Initial'
  4. APPLY it to a fresh database

Snapshot safety net: <SnapshotPath>/safety-net.dump (<size>)

To rollback if anything goes wrong:
  pwsh db-restore-state.ps1 -SnapshotPath <SnapshotPath> -UseSafetyNet

Type 'yes' to continue:
```

Only exact string `yes` proceeds. Any other input: clean abort (exit 0), snapshot remains valid.

### Actions

1. **Drop DB** — `dotnet ef database drop --force --project $ApiProjectPath`. Failure leaves DB intact; exit 1.
2. **Backup Migrations/** — copy `$MigrationsFolder/*` to `$SnapshotPath/migrations-backup/` before deletion, independently of git. Redundancy against git mishaps.
3. **Delete Migrations/** — `Remove-Item -Recurse -Force $MigrationsFolder`. Failure → exit 1 with instruction to restore from `migrations-backup/`.
4. **Add Initial migration** — `dotnet ef migrations add $InitialName --project $ApiProjectPath`. Failure (usually model compile errors) → exit 1 with instruction: restore from `migrations-backup/`, fix compile errors, retry; or rollback via safety-net.
5. **Database update** — `dotnet ef database update --project $ApiProjectPath`. Failure → exit 1 with diagnostic hints.
6. **Dump schema-post preliminary** — `pg_dump -s -f $SnapshotPath/schema-post.sql`. This is an early snapshot to enable the drift check as soon as possible; `db-restore-state.ps1` may re-dump it for freshness.
7. **Update manifest**:
   ```json
   {
     "reset": {
       "completedAt": "2026-04-08T14:38:01Z",
       "initialName": "Initial",
       "schemaPostHash": "<sha256>"
     }
   }
   ```
8. **Summary** — next-step command for `db-restore-state.ps1`.

### Error handling

- No automatic rollback between steps. Each failure prints:
  - What succeeded
  - What failed
  - Exact command to rollback manually via safety-net
  - Exact command to restore `Migrations/` from `migrations-backup/`
- All stdout+stderr mirrored to `$SnapshotPath/reset.log`.

## Component 3: `db-restore-state.ps1`

### Purpose
Verify the new schema is compatible with the saved data, restore the data, reset sequences, and verify integrity. Also supports full rollback from safety-net.

### Parameters

| Param | Type | Default | Description |
|---|---|---|---|
| `-SnapshotPath` | string | *required* | Snapshot path. |
| `-UseSafetyNet` | switch | `$false` | Use `safety-net.dump` instead of `data.dump`. Full DB restore (clean + recreate). |
| `-RestorePdfVolume` | nullable bool | `$null` (auto: true if `pdf_uploads.tar.gz` present) | Restore Docker PDF volume. |
| `-SkipSchemaCheck` | switch | `$false` | Bypass schema diff. Dangerous. Requires extra confirmation. |
| `-AllowDrift` | switch | `$false` | Accept non-identical schema, attempt best-effort restore. |
| `-Force` | switch | `$false` | Skip confirmations. |

### Pre-flight checks

1. `SnapshotPath` contains expected files; manifest hashes verified.
2. `POSTGRES_HOST` localhost enforcement.
3. Database reachable.
4. In normal mode (`-UseSafetyNet=$false`): target DB exists and user tables are empty. If not empty, warn and require confirmation.
5. In safety-net mode: DB state does not matter; script will drop and recreate.

### Mode A: standard restore (`-UseSafetyNet=$false`)

1. **Dump fresh schema-post** — overwrite `schema-post.sql` to ensure it matches the actual current DB state (in case anything changed between reset and restore).
2. **Normalize both schemas** — produce `schema-pre.normalized.sql` and `schema-post.normalized.sql` by:
   - Stripping SQL comments (`-- …`).
   - Stripping `SET …`, `SELECT pg_catalog.set_config(…)`, version headers.
   - Normalizing whitespace (collapse runs, trim trailing).
   - Sorting top-level statements by object name (stable ordering), preserving intra-statement order of columns.
   - Removing any timestamp/identifier of `__EFMigrationsHistory` row contents.
3. **Diff** — `git diff --no-index --exit-code schema-pre.normalized.sql schema-post.normalized.sql`, output saved to `schema-diff.txt`.
4. **Drift classification**:
   - **Zero diff** → `identical` → proceed automatically.
   - **Minor diff** (only default value normalization, trivial rewrites that are semantically equivalent per a small heuristic allow-list) → `minor` → proceed with a printed warning; log the minor diff.
   - **Significant diff** (tables added/removed/renamed, columns missing in post, type changes) → `significant` → **STOP** unless `-AllowDrift` is set.
5. **On STOP** — print a compact diff summary and three options:
   ```
   Options:
     a) Fix your model code and re-run: pwsh db-reset-migrations.ps1 -SnapshotPath <path>
     b) Rollback to safety net:         pwsh db-restore-state.ps1 -SnapshotPath <path> -UseSafetyNet
     c) Force restore (may fail):       re-run with -AllowDrift
   ```
6. **Restore data** — `pg_restore --data-only --no-owner --no-acl --disable-triggers --single-transaction $SnapshotPath/data.dump -d meepleai`.
7. **Reset sequences** — discover all identity/serial sequences via `pg_depend` joined to `pg_class` and `pg_attribute` (which gives the owning table and column for each sequence). For each discovered sequence, execute:
   ```sql
   SELECT setval(
     '"<sequence_schema>"."<sequence_name>"',
     (SELECT COALESCE(MAX("<column>"), 0) + 1 FROM "<table_schema>"."<table>"),
     false
   );
   ```
   `pg_depend` is preferred over `information_schema.sequences` because only `pg_depend` tells us which table/column owns each sequence.
8. **Verify row counts** — rerun the same exact `COUNT(*)` query used in step 5 of `db-save-state.ps1`, compare against `rowcounts-pre.tsv`. If `-Sanitize` was used, excluded tables must have count zero. All other tables must match exactly. Mismatch → warning; decision deferred to operator (do not auto-rollback).
9. **Restore PDF volume** (if applicable and `pdf_uploads.tar.gz` present):
   - Verify volume is empty or user-confirmed to overwrite.
   - Run:
     ```bash
     docker run --rm -v meepleai_pdf_uploads:/dst -v $SnapshotPath:/src alpine \
       sh -c "rm -rf /dst/* && tar xzf /src/pdf_uploads.tar.gz -C /dst"
     ```
   - Verify file count matches manifest.
10. **Update manifest** with `restore` section (completed timestamp, mode, schema drift classification, row count match result).
11. **Summary**:
    ```
    ✓ Restore complete.
      Schema: identical (no drift)
      Rows:   227/227 tables match
      Data:   98.2 MB restored in 14.3s
      PDFs:   42/42 files restored
    ```

### Mode B: safety-net rollback (`-UseSafetyNet=$true`)

Simpler path, bypasses diff and granular verify.

1. Confirmation prompt: `"This will DROP current DB and restore from safety-net.dump. Type 'yes':"`
2. Drop and recreate DB via `dotnet ef database drop --force` then `psql -c "CREATE DATABASE meepleai"`.
3. `pg_restore --clean --if-exists --no-owner --no-acl $SnapshotPath/safety-net.dump -d meepleai`.
4. Verify row counts against `rowcounts-pre.tsv`; all must match exactly.
5. **Important post-condition warning**: after safety-net rollback, the DB is in the pre-reset state, which is inconsistent with the code if `Migrations/` has already been replaced. Warn:
   ```
   ⚠ DB restored to pre-reset state.
     Your Migrations/ folder now conflicts with the DB state.
     To fully rollback, restore Migrations/ from <SnapshotPath>/migrations-backup/:
       Remove-Item -Recurse <ApiProjectPath>/Migrations
       Copy-Item -Recurse <SnapshotPath>/migrations-backup/* <ApiProjectPath>/Migrations/
   ```

### Error handling

- All `pg_restore` invocations use `--single-transaction` for atomicity.
- Restore failure leaves DB in its pre-restore state (fresh schema + empty, or unchanged for safety-net mode).
- Full stdout+stderr captured in `$SnapshotPath/restore.log`.

## Component 4: `db-snapshot-common.ps1`

Shared helper module, dot-sourced from the three scripts. Contains:

- `Get-PostgresConfig` — parses `database.secret`, returns object with `Host`, `Port`, `Db`, `User`, `Password`.
- `Assert-LocalhostOnly` — enforces host check, throws if not localhost/127.0.0.1.
- `Get-StorageProvider` — reads `storage.secret`, returns `"local"` or `"s3"`.
- `Invoke-PgDump` / `Invoke-PgRestore` / `Invoke-Psql` — wrappers that set `PGPASSWORD`, handle exit codes, and tee output to a log file.
- `Write-Timestamped` — `[HH:mm:ss] message` logging.
- `Get-FileSha256` — wrapper around `Get-FileHash`.
- `Read-Manifest` / `Write-Manifest` / `Update-Manifest` — JSON helpers with atomic file writes.
- `Test-DockerVolumeExists` — checks Docker volume presence.
- `Get-DockerVolumeStats` — returns file count and byte size.
- `Confirm-UserAction` — unified prompt helper, respects `-Force`.

## Component 5: Makefile targets

Added to `infra/Makefile`, grouped under a new `# === DB Snapshot (dev) ===` section, before the existing `# === Utilities ===`:

```makefile
db-snapshot: ## Create local DB snapshot before migration reset
	pwsh scripts/db-save-state.ps1

db-snapshot-sanitized: ## Create DB snapshot without sensitive tables
	pwsh scripts/db-save-state.ps1 -Sanitize

db-reset-migrations: ## Reset migrations using snapshot (requires SNAP=path)
	@if [ -z "$(SNAP)" ]; then echo "ERROR: SNAP=<snapshot-path> required" && exit 1; fi
	pwsh scripts/db-reset-migrations.ps1 -SnapshotPath $(SNAP)

db-restore-snapshot: ## Restore data from snapshot (requires SNAP=path)
	@if [ -z "$(SNAP)" ]; then echo "ERROR: SNAP=<snapshot-path> required" && exit 1; fi
	pwsh scripts/db-restore-state.ps1 -SnapshotPath $(SNAP)

db-rollback-safetynet: ## Rollback using snapshot safety-net (requires SNAP=path)
	@if [ -z "$(SNAP)" ]; then echo "ERROR: SNAP=<snapshot-path> required" && exit 1; fi
	pwsh scripts/db-restore-state.ps1 -SnapshotPath $(SNAP) -UseSafetyNet
```

All targets appended to the final `.PHONY` declaration.

## File Changes Summary

### New files

- `infra/scripts/db-snapshot-common.ps1`
- `infra/scripts/db-save-state.ps1`
- `infra/scripts/db-reset-migrations.ps1`
- `infra/scripts/db-restore-state.ps1`
- `infra/db-snapshots/.gitignore` — contains `*` and `!.gitignore`
- `docs/operations/migration-consolidation-runbook.md` — one-page runbook: when and how to use these scripts

### Modified files

- `infra/Makefile` — add DB Snapshot section and `.PHONY` entries
- `infra/scripts/README.md` — document the three new scripts alongside existing ones

## Error Matrix

| Failure point | Script | DB state | Operator action |
|---|---|---|---|
| `pg_dump` safety-net fails | save | intact | fix PG environment, retry |
| `pg_dump` data.dump fails | save | intact | safety-net preserved; investigate, retry |
| PDF volume tar fails | save | intact | rerun without `-IncludePdfVolume` or fix docker |
| Confirmation declined | reset | intact | clean abort, snapshot still valid |
| `ef database drop` fails | reset | intact | fix locks/permissions, retry |
| `Migrations/` removal fails | reset | dropped | restore from `migrations-backup/` |
| `ef migrations add` fails | reset | dropped | restore migrations from backup, fix models, retry — or rollback safety-net |
| `ef database update` fails | reset | dropped, no migrations applied | fix SQL in new Initial, retry |
| Schema diff "significant" | restore | fresh, empty | fix code / rollback / `-AllowDrift` |
| `pg_restore` data fails | restore | fresh, empty (txn rolled back) | options: `-UseSafetyNet`, `-AllowDrift`, fix and retry |
| Row count mismatch post-restore | restore | fresh, partially populated | warning + log; manual decision |
| PDF volume restore fails | restore | DB OK, volume untouched or partial | rerun PDF restore independently |

## Testing Strategy

Pragmatic, not exhaustive — this is a one-shot dev tool without CI.

### 1. Smoke tests (manual, after implementation)

- `pwsh db-save-state.ps1` on seeded dev DB → verify manifest, file hashes, sizes.
- `pwsh db-reset-migrations.ps1 -SnapshotPath $snap -DryRun` → verify printed commands.
- Real execution of reset → verify fresh schema, empty DB.
- `pwsh db-restore-state.ps1 -SnapshotPath $snap` → verify `identical` drift class, row counts match.
- Rollback path: save → reset → `-UseSafetyNet` → verify state identical to pre-reset.

### 2. Negative path tests (manual, documented in runbook)

- Staging host rejection: temporarily set `POSTGRES_HOST=staging.meepleai.app` in a copy of `database.secret`, verify all three scripts refuse.
- Corrupt snapshot detection: modify a hash in `manifest.json`, verify restore script fails pre-flight.
- Drift detection: deliberately add a column in a model before the reset, run restore → verify `significant` classification and STOP.
- Migration failure recovery: introduce a typo in a model attribute, run reset → verify `ef migrations add` fails, migrations-backup usable, safety-net rollback works.

### 3. No automated unit tests

Rationale: building a PostgreSQL testcontainer harness for these PowerShell scripts adds more complexity than the scripts themselves. Surface area is narrow, safety-net is the ultimate backstop. Coverage deferred to manual smoke tests during implementation PR.

## Security Considerations

- **Localhost enforcement** is the primary safeguard against accidental execution on staging/prod. Enforced in all three scripts via `Assert-LocalhostOnly`.
- **Secrets**: scripts read `database.secret` and `storage.secret` — same pattern as existing `db-dump.ps1`. No new secret files created.
- **Sanitize mode**: opt-in for sharing snapshots off-machine. Even when sanitized, the `safety-net.dump` always contains full data and stays on the local machine. Operators are warned not to copy `safety-net.dump` off-host.
- **Snapshot directory**: gitignored; never committed.
- **No network calls** — all operations are local; no R2, no HTTP.

## Open Questions

None at design time. All identified concerns have been resolved with explicit choices (localhost-only, sanitize scope, drift heuristic, PDF volume defaults, rollback philosophy).

## Out of Scope (explicit)

- Admin web UI
- Remote databases (staging, prod)
- R2/S3 backup integration
- Cron / scheduling
- Cross-environment restore (e.g., staging snapshot → local)
- Data transformation for schema drift (renames, type changes)
- Automated rollback across EF CLI steps
- Multi-database snapshotting (only `meepleai`)

## Success Criteria

The scripts are successful when an operator can:

1. Run `pwsh db-save-state.ps1` on a seeded local dev DB and obtain a snapshot with verified hashes.
2. Run `pwsh db-reset-migrations.ps1 -SnapshotPath <snap>`, drop the DB and Migrations folder, and create a fresh `Initial` migration without losing rollback ability.
3. Run `pwsh db-restore-state.ps1 -SnapshotPath <snap>` and see `identical` drift + 100% row count match + PDF files restored.
4. In the failure case (drift detected, or any error), rollback via `-UseSafetyNet` and return to the pre-reset state, including `Migrations/` restoration from `migrations-backup/`.
