# Game Reset Runbook — DEV environment

**Purpose**: Rehearse the full Phase 3 procedure on local docker postgres before touching staging.

**Pre-conditions**:
- Local dev stack running (`make dev-core` from `infra/`)
- `infra/scripts/game-reset/.env.dev` exists and points to local docker postgres
- Phase 2c migration `DropGameAggregate_Issue1320` exists in code (already merged to main-dev)
- Current code does NOT have the migration applied yet (`dotnet ef database update` not yet run on dev)

## Procedure

### Pre-flight (no traffic to pause for dev)

```bash
cd infra/scripts/game-reset/
./01-backup.sh .env.dev
./02-export-mapping.sh .env.dev
```

**Gate** (Adzic G/W/T):
- **Given**: empty `infra/backups/` directory
- **When**: scripts 01 and 02 run
- **Then**: 2 files appear: `<date>-dev-pre-game-reset.dump`, `<date>-dev-game-mapping.csv`

### Sanity check

Inspect the mapping CSV:

```bash
wc -l infra/backups/*-dev-game-mapping.csv
head -3 infra/backups/*-dev-game-mapping.csv
```

Expected: > 0 lines (header + at least one row if dev has any games). If 0 rows: dev is empty, skip the rest.

### Step 3a: Dry-run relink

```bash
./03-relink-vectors.sh .env.dev --dry-run
```

Expected output:
```
[INFO] Vectors to be re-linked: <N>
[WARN] DRY RUN — no changes applied.
[INFO] Would execute: <SQL>
```

### Step 3b: Real relink

```bash
./03-relink-vectors.sh .env.dev
```

Expected: `[OK] Relink complete.` exit 0. If warning about dangling vectors: decide per spec ORPHANING strategy.

### Step 5: Apply EF migration

```bash
cd ../../../apps/api/src/Api
dotnet ef database update
```

Expected: migration `DropGameAggregate_Issue1320` applied without error. Output contains "Applying migration".

### Step 6: Verification gates

```bash
cd ../../../infra/scripts/game-reset/
./04-verify-gates.sh .env.dev
```

Expected: `Gates complete: 5 passed, 0 failed`, exit 0.

### Step 6b: Smoke test

```bash
cd ../../../
make dev-down && make dev-core
sleep 10
curl -fsS http://localhost:8080/health || (echo "FAIL: health endpoint not 200"; exit 1)
# TODO: add RAG query smoke test against a known game
```

### Rollback (if any gate fails)

```bash
cd infra/scripts/game-reset/
./99-rollback.sh .env.dev infra/backups/<date>-dev-pre-game-reset.dump
```

Confirm DB restored: `make dev-down && make dev-core`, then `curl /health`.

## Sign-off

- [ ] All gates passed on dev
- [ ] Smoke test passed on dev
- [ ] Backup file retained for 30 days (`infra/backups/`)
- [ ] Mapping CSV retained for audit

Only after sign-off: proceed to [`runbook-staging.md`](./runbook-staging.md).
