# Storage Layout Migration Runbook — Issue #1314 PR 2

**Status**: Phase 0 ready (PR 2 deployed, flags default-safe). Phase 1+ to be triggered by operator.
**Owner**: Backend dev + DevOps
**Estimated downtime**: 0 (zero-downtime rollout)
**Estimated duration**: ~30min staging / 2-4h prod (Phase 1)

---

## Context

Issue #1314 migrates S3 blob storage from legacy single-folder layout
`pdf_uploads/{resourceKey}/{fileId}_{filename}` to a categorized layout
`{category}/{resourceKey}/{fileId}_{filename}` where `{category}` is one of:

| Category | Folder |
|----------|--------|
| `Pdf` | `pdfs/` |
| `SessionPhoto` | `session-photos/` |
| `GameImage` | `game-images/` |
| `VisionSnapshot` | `vision-snapshots/` |
| `GamebookPhoto` | `gamebook-photos/` |
| `PhotoBatch` | `photo-batches/` |

The migration uses an **Outbox pattern** (mirror of `EmailOutboxBackgroundService`):
each object move is enqueued as a row in `storage_operation_outbox`, and a
background drainer (`StorageOperationOutboxBackgroundService`) executes the
move with retry + exponential backoff `[1m, 5m, 30m, 2h, 6h]` × MaxAttempts 5.

---

## Phase 0 — Deploy infra (no behavior change)

**Goal**: PR 2 merged + deployed. Flags default-safe. No user-visible change.

**Configuration** (default values, no override needed):

```yaml
StorageLayout:
  WriteMode: Legacy        # New uploads → pdf_uploads/...
  ReadMode: Dual           # Reads probe new prefix first, fall back to legacy
  MigrationEnabled: false  # Drainer service starts but no-ops
```

**Verification**:

```bash
# 1. Health endpoint reports legacy layout version
curl https://meepleai.app/health/storage | jq '.data.layout_version'
# Expected: "v1-gameId"

# 2. Drainer container logs the disabled-mode startup message (SSH to VPS)
ssh meepleai-staging \
  'docker compose -f /opt/meepleai/repo/infra/compose.staging.yml logs api --tail 200' \
  | grep "StorageOperationOutbox"
# Expected: "started but disabled (StorageLayout:MigrationEnabled=false)"

# 3. Outbox table empty
psql -c "SELECT COUNT(*) FROM storage_operation_outbox;"
# Expected: 0
```

**Rollback (Phase 0)**: revert PR 2. No data migration was started.

---

## Phase 1 — Background migration (drainer ON)

**Goal**: existing objects under legacy layout are moved to categorized layout
in the background. New uploads still land at legacy layout.

**Pre-requisites**:
- ⬜ R2 backup snapshot of current state (T-zero snapshot for rollback)
- ⬜ Phase 0 verified for ≥ 24h (no anomalies)
- ⬜ Migration script ready to enumerate existing objects + enqueue outbox rows

### Step 1.1 — Pre-migration audit

```bash
# Count objects under legacy prefix
aws s3 ls s3://${BUCKET}/pdf_uploads/ --recursive --summarize | tail -2
# Expected: ~150 objects staging, more in prod

# Save manifest for rollback safety
aws s3 ls s3://${BUCKET}/pdf_uploads/ --recursive > /tmp/manifest-pre-${BUCKET}.tsv
```

### Step 1.2 — Enable drainer flag

Staging runs on **VPS Hetzner** (not Kubernetes). Feature flags are read by
the API from env vars loaded via `env_file: [./secrets/storage.secret]` in
`infra/compose.staging.yml`. The `.secret` files are NOT synced by the
deploy workflow — they live on the VPS at `/opt/meepleai/secrets/` and must
be edited via SSH.

```bash
# 1. SSH to the staging VPS (operator account)
ssh meepleai-staging

# 2. Append the migration flag to storage.secret (heredoc avoids quoting bugs)
cd /opt/meepleai
sudo tee -a secrets/storage.secret <<'EOF'

# Issue #1314 Phase 1: enable storage-layout-migration drainer.
# Default (absent var) = false. Remove this block after Phase 4 cleanup.
StorageLayout__MigrationEnabled=true
EOF

# 3. Restart api container (env_file is re-read on container restart)
docker compose -f repo/infra/compose.staging.yml restart api

# 4. Verify the flag is loaded
docker compose -f repo/infra/compose.staging.yml exec api \
  printenv StorageLayout__MigrationEnabled
# Expected: true
```

Allowed env var values (defined in `apps/api/src/Api/Services/Pdf/StorageLayoutOptions.cs`):

| Variable | Values | Default if absent |
|---|---|---|
| `StorageLayout__MigrationEnabled` | `true` / `false` | `false` (drainer dormant) |
| `StorageLayout__WriteMode` | `Legacy` / `Dual` / `New` | `Legacy` (no behavior change) |
| `StorageLayout__ReadMode` | `Legacy` / `Dual` / `New` | `Dual` (probe new first, fallback legacy) |

> **Note:** the `appsettings.json` section is `StorageLayout`. The env-var
> convention uses double underscore (`__`) per .NET IConfiguration. See
> `infra/secrets/storage.secret.example` for the full template + phase
> progression comments.

**Rollback Phase 1 (drainer ON → OFF):** SSH back, remove the line from
`storage.secret`, restart api. Pending rows stay Pending; Sent rows stay
Sent. No state corruption — the drainer is a no-op when the flag is false.

### Step 1.3 — Enqueue migration rows

Implemented in issue #1333 (admin endpoint `POST /api/v1/admin/storage/migration/enqueue`
+ `scripts/enqueue-storage-migration.sh`). The script enumerates S3 objects
under `--prefix` and inserts `storage_operation_outbox` rows for the drainer.

Idempotent — re-runs with the same `--migration-id` produce 0 enqueued, N skipped.

```bash
export MEEPLEAI_ADMIN_TOKEN="<admin-bearer-token>"
MIGRATION_ID=$(uuidgen)

# Dry-run first — counts objects without inserting rows
./scripts/enqueue-storage-migration.sh \
  --bucket ${BUCKET} \
  --migration-id $MIGRATION_ID \
  --prefix pdf_uploads/ \
  --category Pdf \
  --dry-run

# Apply
./scripts/enqueue-storage-migration.sh \
  --bucket ${BUCKET} \
  --migration-id $MIGRATION_ID \
  --prefix pdf_uploads/ \
  --category Pdf

# Output:
# {
#   "migrationId": "<UUID>",
#   "totalObjects": 113,
#   "enqueued": 113,
#   "skipped": 0,
#   "failed": 0,
#   ...
# }
```

For non-PDF prefixes (session photos, vision snapshots, etc.) re-invoke with
the matching `--category` value and prefix:

```bash
./scripts/enqueue-storage-migration.sh --bucket ${BUCKET} --migration-id $MIGRATION_ID \
  --prefix pdf_uploads/session-photos- --category SessionPhoto
```

(One invocation per category — the enumerator is single-prefix; cross-category
batches must be split.)

### Step 1.4 — Monitor progress

```bash
# Prometheus / Grafana — storage migration dashboard
open https://grafana.meepleai.app/d/storage-migration

# Or via psql
psql -c "SELECT status, COUNT(*) FROM storage_operation_outbox GROUP BY status;"
# Expected progression: Pending decreasing, Sent increasing.
```

**SLO**: `pending_rows` reaches 0 within 30 min (staging) / 2-4h (prod).

**Alert**: any FailedPermanent row → halt migration, investigate, do NOT
proceed to Phase 2.

### Step 1.5 — Verify all rows Sent

```bash
psql -c "
  SELECT COUNT(*) FILTER (WHERE status = 'Sent')          AS sent,
         COUNT(*) FILTER (WHERE status = 'Pending')       AS pending,
         COUNT(*) FILTER (WHERE status = 'FailedPermanent') AS failed
  FROM storage_operation_outbox
  WHERE migration_id = '<UUID-from-1.3>';
"
# Expected: sent = N, pending = 0, failed = 0
```

**Rollback (Phase 1)**: set `STORAGE_MIGRATION_ENABLED=false`, restart pods.
Already-moved objects exist under BOTH layouts (CopyObject is followed by
DeleteObject, but if DeleteObject failed the legacy copy survives — the
outbox row stays Pending). Restore from T-zero snapshot if needed.

---

## Phase 2 — Switch writes to new layout

**Goal**: new uploads land at the new categorized layout. Reads still dual.

**Pre-requisites**:
- ✅ Phase 1 complete (all rows Sent, 0 Pending, 0 FailedPermanent)
- ⬜ Visual verification in R2 console: new prefixes (`pdfs/`, `session-photos/`, etc.) populated, `pdf_uploads/` empty (or close to it)

### Step 2.1 — Flip WriteMode

```bash
ssh meepleai-staging
cd /opt/meepleai
sudo tee -a secrets/storage.secret <<'EOF'
StorageLayout__WriteMode=New
StorageLayout__ReadMode=Dual
EOF
docker compose -f repo/infra/compose.staging.yml restart api
```

### Step 2.2 — Verification

```bash
# Health endpoint reports the new layout
curl https://meepleai.app/health/storage | jq '.data.layout_version'
# Expected: "v2-categorized"

# Upload a test PDF + verify landing prefix
curl -X POST -F "file=@test.pdf" https://meepleai.app/api/v1/pdfs/upload
aws s3 ls s3://${BUCKET}/pdfs/ --recursive | grep test.pdf
# Expected: object exists under pdfs/{resourceKey}/...

# Negative check: no new objects under pdf_uploads/
aws s3 ls s3://${BUCKET}/pdf_uploads/ --recursive | wc -l
# Expected: stable or decreasing (NO new objects appearing)
```

**Rollback (Phase 2)**: set `STORAGE_WRITE_MODE=Legacy`, restart. Any
objects written under new layout during Phase 2 remain reachable via
ReadMode=Dual.

---

## Phase 3 — Deprecation legacy reads

**Goal**: presigned URLs emitted pre-cutover have expired (TTL_max = 3600s
+ buffer = 1h). All reads now target new layout only.

**Pre-requisites**:
- ✅ Phase 2 complete + verified for ≥ 24h (grace window for presigned URLs)
- ✅ All new uploads observed at new prefix

### Step 3.1 — Flip ReadMode

```bash
ssh meepleai-staging
cd /opt/meepleai
# Update the existing StorageLayout__ReadMode=Dual line to =New
sudo sed -i 's/^StorageLayout__ReadMode=.*/StorageLayout__ReadMode=New/' secrets/storage.secret
docker compose -f repo/infra/compose.staging.yml restart api
```

### Step 3.2 — Monitor for legacy-read attempts

```bash
# Counter should stay at 0
curl https://meepleai.app/metrics | grep storage_legacy_reads_total
# Expected: 0 or absent
```

**Rollback (Phase 3)**: set `STORAGE_READ_MODE=Dual`, restart. Re-enables
legacy fallback immediately.

---

## Phase 4 — Cleanup

**Goal**: legacy `pdf_uploads/` folder removed from S3, feature flags removed
from codebase.

**Pre-requisites**:
- ✅ Phase 3 complete + monitored for ≥ 7 days (no legacy-read attempts)

### Step 4.1 — Move legacy folder to `_trash/`

```bash
# Reuse the #520 cleanup script pattern — move under a dated _trash/ for
# safety, NOT outright delete.
DATE=$(date +%F)
aws s3 mv s3://${BUCKET}/pdf_uploads/ \
          s3://${BUCKET}/_trash/${DATE}/pdf_uploads/ --recursive
```

### Step 4.2 — Drop feature flags from codebase

Follow-up PR removes `StorageLayoutOptions`, the WriteMode/ReadMode branching
in `S3BlobStorageService`/`BlobStorageService`, and the
`StorageOperationOutboxBackgroundService` (or keeps it for future migrations).

### Step 4.3 — Drop `_trash/` after 7d retention

```bash
aws s3 rm s3://${BUCKET}/_trash/${DATE}/pdf_uploads/ --recursive
```

---

## Backout — Full reverse-migration

**When**: Phase 2 detected as broken AND Phase 1 already complete (new objects
exist at categorized prefixes).

Implemented in issue #1333 (admin endpoint `POST /api/v1/admin/storage/migration/reverse`
+ `scripts/reverse-storage-migration.sh`).

### Step B.1 — Halt new uploads at new layout

```bash
ssh meepleai-staging
cd /opt/meepleai
sudo sed -i 's/^StorageLayout__WriteMode=.*/StorageLayout__WriteMode=Legacy/' secrets/storage.secret
docker compose -f repo/infra/compose.staging.yml restart api
```

### Step B.2 — Reverse-move objects (script + admin endpoint)

```bash
export MEEPLEAI_ADMIN_TOKEN="<admin-bearer-token>"

# Dry-run — counts rows that would be reversed, no S3 writes
./scripts/reverse-storage-migration.sh \
  --migration-id <UUID-from-step-1.3> \
  --dry-run

# Apply (interactive — requires typing 'reverse' to confirm)
./scripts/reverse-storage-migration.sh \
  --migration-id <UUID-from-step-1.3>

# Output:
# {
#   "migrationId": "<UUID>",
#   "totalRows": 113,
#   "reversedFromSent": 100,
#   "cancelledFromPending": 13,
#   "skipped": 0,
#   "failed": 0,
#   ...
# }
```

For each `Sent` row the script copies `NewKey → LegacyKey` then deletes `NewKey`;
for each `Pending` row it transitions the outbox `Status` to `Reverted` so the
drainer skips the row. `FailedPermanent` rows are NOT auto-reverted — investigate
manually.

### Step B.3 — Verify health endpoint

```bash
curl https://meepleai.app/health/storage | jq '.data.layout_version'
# Expected: "v1-gameId" (back to Phase 0 state)
```

### Step B.4 — Audit trail

```bash
psql -c "SELECT status, COUNT(*) FROM storage_operation_outbox \
         WHERE migration_id = '<UUID>' GROUP BY status;"
# Expected after full reverse:
#   Reverted: <total>
#   FailedPermanent: 0 (else: manual investigation)
```

---

## Observability

### Prometheus metrics (PR 2 adds these)

| Metric | Type | Tags | Description |
|--------|------|------|-------------|
| `meepleai_storage_migration_objects_migrated_total` | Counter | `status`, `category` | Per-object move outcomes (success / failed / failed_permanent) |
| `meepleai_storage_migration_duration` | Histogram | `category` | Per-object move duration (CopyObject + DeleteObject) |
| `meepleai_storage_layout_version_announcements_total` | Counter | `layout_version` | Drainer startup announcements (1 per pod restart) |

### Grafana dashboard

To be added at `infra/grafana/dashboards/storage-migration.json`. Panels:
1. Progress: `sum(rate(meepleai_storage_migration_objects_migrated_total{status="success"}[5m]))`
2. Error rate: `sum(rate(meepleai_storage_migration_objects_migrated_total{status="failed"}[5m]))`
3. P99 duration: `histogram_quantile(0.99, rate(meepleai_storage_migration_duration_bucket[5m]))`
4. Layout version: `meepleai_storage_layout_version_announcements_total`

### Structured logs

All drainer log entries are tagged with `MigrationId` (UUID per Step 1.3 batch)
and `LegacyKey`/`NewKey`. Query in Loki:

```logql
{app="api"} |= "Storage outbox" | json | migration_id="<UUID>"
```

---

## References

- Issue #1314 — Refactor IBlobStorageService PDF storage layout
- Issue #520 — R2 orphan PDF cleanup (predecessor cleanup script pattern)
- PR #1322 — PR 1 signature refactor (behavior-preserving)
- This PR — PR 2 layout migration (this runbook)
- `EmailOutboxBackgroundService` — outbox pattern template
- `apps/api/src/Api/Services/Pdf/StorageLayoutOptions.cs` — feature flags
- `apps/api/src/Api/Infrastructure/BackgroundServices/StorageOperationOutboxBackgroundService.cs` — drainer
