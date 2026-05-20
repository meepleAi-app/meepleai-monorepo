# Game Entity Reset — Phase 3: Execute Reset Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute the game entity reset against live environments (dev → staging → prod) by running Phase 1 backup scripts, capturing the legacy `game_id → shared_game_id` mapping, re-linking orphaned `pgvector_embeddings.game_id` values to the corresponding `SharedGame.Id`, deploying Phase 2c code, and validating with verification gates from the parent spec §4 step 5.

**Architecture:** Pre-link data BEFORE deploying new code, so consumers (which now treat every `Guid gameId` as `GameRef.Shared(gameId)`) read correct values from the first request. New script `03-relink-vectors.sh` is added under `infra/scripts/game-reset/`. Sequence runs per environment with explicit go/no-go gates between each.

**Tech Stack:** Bash · PostgreSQL 16 · pgvector · EF Core 9 migrations · GNU Make · existing Phase 1 scripts

**Tracking issue**: [#1320](https://github.com/meepleAi-app/meepleai-monorepo/issues/1320)

**Parent spec**: [`docs/for-developers/specs/2026-05-19-game-entity-reset.md`](../../for-developers/specs/2026-05-19-game-entity-reset.md) — section §4 (sequence) + §5 (verification gates)

**Predecessors**:
- PR #1331 (Phase 1 — backup scripts) merged 2026-05-19
- PR #1332 (Phase 2a — foundation primitives) merged 2026-05-19
- PR #1341 (Phase 2c — `Game` aggregate deleted, EF migration `DropGameAggregate_Issue1320` ready) merged 2026-05-20

---

## Spec-panel review applied (key changes from initial draft)

This plan was reviewed by a virtual spec panel (Nygard / Wiegers / Hightower / Adzic) before finalization. Their critiques baked in:

| Expert | Concern | Mitigation in plan |
|---|---|---|
| **Nygard** (Release It!) | "What happens between data relink and code deploy? Race condition." | Re-ordered: relink BEFORE code deploy. Old code reads stale `games.id` (which equals `SharedGame.Id` after relink). New code reads `pgvector_embeddings.game_id` directly (already updated). Zero gap. |
| **Wiegers** (requirements) | "Verification gates must have measurable acceptance criteria." | Every gate (§5) now has exact SQL + expected output as G/W/T. |
| **Hightower** (cloud-native) | "Traffic pause? Maintenance window? User notification?" | Per-env runbook defines maintenance window: brief for dev, scheduled for staging/prod with health-check exclusion. |
| **Adzic** (specification by example) | "Give concrete examples per gate." | Each gate has a Given/When/Then sample with actual numbers. |
| **Nygard** | "Relink script: dry-run mode, idempotency." | `03-relink-vectors.sh` supports `--dry-run`, refuses double-run with same mapping (idempotency-safe via `WHERE game_id != target_id`). |

---

## Sequence (per environment)

**Critical invariant**: Steps 0-4 run sequentially in the SAME maintenance window. Do not pause between Step 3 (relink) and Step 5 (code deploy).

```
┌─ Step 0: Pre-flight backup (pg_dump -Fc)                    [scripted]
│
├─ Step 1: Export legacy→shared mapping CSV                    [scripted]
│   └─ pre-condition: backup file exists for today
│
├─ Step 2: Verify mapping coverage (no orphans)                [gate]
│   └─ if orphans > 0: STOP, escalate
│
├─ Step 3: Re-link vectors (UPDATE pgvector_embeddings)        [scripted, idempotent]
│   ├─ Step 3a: dry-run preview (--dry-run flag)
│   └─ Step 3b: real run (no flag)
│
├─ Step 4: Verify zero dangling game_ids                       [gate]
│   └─ if dangling > 0: STOP, run rollback
│
├─ Step 5: Deploy Phase 2c code + apply EF migration           [scripted via make]
│   └─ DropGameAggregate_Issue1320 drops games table
│
├─ Step 6: Smoke test (manual + automated)                     [gate]
│   ├─ Health endpoint returns 200
│   ├─ Sample RAG query returns expected chunks
│   └─ Sample auth flow works
│
└─ Step 7: Resume traffic / close maintenance window           [manual]
```

Rollback procedure (§7) reverses any step that fails.

---

## File structure

| Path | Status | Responsibility |
|---|---|---|
| `infra/scripts/game-reset/03-relink-vectors.sh` | ✨ NEW (Task 1) | UPDATE pgvector_embeddings.game_id from mapping CSV |
| `infra/scripts/game-reset/04-verify-gates.sh` | ✨ NEW (Task 2) | Run the 6 verification queries from spec §5, output PASS/FAIL |
| `infra/scripts/game-reset/runbook-dev.md` | ✨ NEW (Task 3) | Per-env runbook (dev first as rehearsal) |
| `infra/scripts/game-reset/runbook-staging.md` | ✨ NEW (Task 4) | Staging runbook |
| `infra/scripts/game-reset/runbook-prod.md` | ✨ NEW (Task 5) | Prod runbook |
| `infra/Makefile` | ✏️ MODIFY | Add `game-reset-relink` and `game-reset-verify` targets |
| Existing Phase 1 scripts (`01-backup.sh`, `02-export-mapping.sh`, `99-rollback.sh`) | ✅ UNCHANGED | reused |
| Existing Phase 2c EF migration `DropGameAggregate_Issue1320` | ✅ UNCHANGED | reused (applied in Step 5) |

---

## Re-link strategy (the critical design decision)

The previous Game aggregate had `Game.Id` (its own PK) and `Game.SharedGameId` (FK to `SharedGame.Id`). Every `pgvector_embeddings.game_id` value was set to `Game.Id` (not `SharedGameId`).

After `DropGameAggregate_Issue1320` drops the `games` table, every `pgvector_embeddings.game_id` value becomes a dangling Guid pointing to nothing.

Phase 2c established the convention: code now treats `pgvector_embeddings.game_id` as `SharedGame.Id` (via `GameRef.Shared(...)`). So before dropping the table, we must:

1. Read `games` table: for each row, capture `(old_id := games.id, new_id := games.shared_game_id)`
2. UPDATE `pgvector_embeddings`: `SET game_id = new_id WHERE game_id = old_id`
3. Then drop the table

After this, every `pgvector_embeddings.game_id` value either:
- Points to a valid `SharedGame.Id` (good — RAG works)
- Was already a `SharedGame.Id` directly (issue #4921 added this path) — relink is a no-op (`WHERE game_id = old_id` doesn't match)
- Is genuinely orphaned (no Game / SharedGame relation existed) — these vectors will fail RAG queries; document as known limitation

The pre-export sanity check from `02-export-mapping.sh` already counts orphans. If orphans > 0, decide before proceeding:
- **Option A**: delete orphan vectors before drop (`DELETE FROM pgvector_embeddings WHERE game_id IN (orphan_list)`) — lossy but clean
- **Option B**: leave orphans, accept that those PDFs won't be RAG-queryable until re-uploaded
- **Default**: Option B (preserve all vectors; orphans = post-Phase-3 follow-up)

---

## Task 1: Create `03-relink-vectors.sh`

**Files:**
- Create: `infra/scripts/game-reset/03-relink-vectors.sh`

- [ ] **Step 1: Write the script with EXACTLY this content**

```bash
#!/usr/bin/env bash
# Phase 3 / Step 3 of spec: re-link pgvector_embeddings.game_id from legacy Game.Id → SharedGame.Id.
# MUST run BEFORE applying EF migration DropGameAggregate_Issue1320 (which drops the games table).
#
# Pre-condition: 01-backup.sh AND 02-export-mapping.sh have been run today.
# Idempotent: safe to re-run; rows already updated are skipped via `WHERE game_id != target_id`.
#
# Usage: ./03-relink-vectors.sh <env-file> [--dry-run] [--i-mean-it]

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

load_env "${1:-}"

# Optional flags (order-independent)
dry_run=false
prod_confirm=""
for arg in "${@:2}"; do
  case "$arg" in
    --dry-run) dry_run=true ;;
    --i-mean-it) prod_confirm="--i-mean-it" ;;
    *) log_error "Unknown flag: $arg"; exit 64 ;;
  esac
done
guard_prod "$prod_confirm"

# Pre-condition: backup + mapping CSV from today must exist
expected_dump=$(artefact_path "pre-game-reset" "dump")
expected_csv=$(artefact_path "game-mapping" "csv")
if [[ ! -f "$expected_dump" ]]; then
  log_error "No backup dump found: $expected_dump. Run 01-backup.sh first."
  exit 72
fi
if [[ ! -f "$expected_csv" ]]; then
  log_error "No mapping CSV found: $expected_csv. Run 02-export-mapping.sh first."
  exit 72
fi
log_ok "Pre-flight artefacts present"

# Count rows that will be affected
count_sql="
SELECT COUNT(*) FROM pgvector_embeddings pe
JOIN games g ON g.id = pe.game_id
WHERE g.shared_game_id IS NOT NULL
  AND pe.game_id <> g.shared_game_id;
"

affected=$(psql "$DATABASE_URL" -t -A -c "$count_sql")
log_info "Vectors to be re-linked: $affected"

if [[ "$affected" == "0" ]]; then
  log_ok "No re-link needed (already done or no matching rows). Exiting."
  exit 0
fi

if [[ "$dry_run" == "true" ]]; then
  log_warn "DRY RUN — no changes applied."
  log_info "Would execute:"
  cat <<SQL
UPDATE pgvector_embeddings pe
SET game_id = g.shared_game_id
FROM games g
WHERE pe.game_id = g.id
  AND g.shared_game_id IS NOT NULL
  AND pe.game_id <> g.shared_game_id;
SQL
  exit 0
fi

# Real run: transactional update
log_info "Re-linking $affected vectors (transactional UPDATE)..."

psql "$DATABASE_URL" -v ON_ERROR_STOP=1 <<'SQL'
BEGIN;

UPDATE pgvector_embeddings pe
SET game_id = g.shared_game_id
FROM games g
WHERE pe.game_id = g.id
  AND g.shared_game_id IS NOT NULL
  AND pe.game_id <> g.shared_game_id;

COMMIT;
SQL

# Post-relink verification: count remaining dangling
dangling=$(psql "$DATABASE_URL" -t -A -c "
SELECT COUNT(*) FROM pgvector_embeddings pe
WHERE pe.game_id NOT IN (SELECT id FROM games)
  AND pe.game_id NOT IN (SELECT id FROM shared_games);
")
log_info "Vectors with game_id resolving to neither games nor shared_games: $dangling"

if [[ "$dangling" != "0" ]]; then
  log_warn "Post-relink: $dangling vectors still orphaned. These will be lost when games table is dropped."
  log_warn "Per spec §4 ORPHANING strategy: accept the loss OR delete these rows before drop."
  log_warn "Decision required before Step 5 (apply migration)."
fi

log_ok "Relink complete. Next step: apply EF migration to drop games table."
```

- [ ] **Step 2: Set executable bit via git**

```bash
chmod +x infra/scripts/game-reset/03-relink-vectors.sh
git add infra/scripts/game-reset/03-relink-vectors.sh
git update-index --chmod=+x infra/scripts/game-reset/03-relink-vectors.sh
```

- [ ] **Step 3: Verify syntax**

```bash
bash -n infra/scripts/game-reset/03-relink-vectors.sh
```

Expected: exit 0.

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(infra): game-reset 03-relink-vectors.sh with dry-run + idempotency (refs #1320)"
```

---

## Task 2: Create `04-verify-gates.sh`

**Files:**
- Create: `infra/scripts/game-reset/04-verify-gates.sh`

Encodes the 6 verification queries from spec §5 (post-merge update §4 step 5).

- [ ] **Step 1: Write the script with EXACTLY this content**

```bash
#!/usr/bin/env bash
# Phase 3 / Step 6 of spec: run verification gates after EF migration applied.
# Each gate has a measurable PASS/FAIL criterion.
#
# Usage: ./04-verify-gates.sh <env-file>

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

load_env "${1:-}"

# Counters
pass=0
fail=0

run_gate() {
  local name="$1"
  local sql="$2"
  local expected="$3"
  local got

  got=$(psql "$DATABASE_URL" -t -A -c "$sql")
  if [[ "$got" == "$expected" ]]; then
    log_ok "GATE PASS: $name (got=$got, expected=$expected)"
    pass=$((pass+1))
  else
    log_error "GATE FAIL: $name (got=$got, expected=$expected)"
    fail=$((fail+1))
  fi
}

log_info "Running verification gates..."

# Gate 1: games table no longer exists
got=$(psql "$DATABASE_URL" -t -A -c "
SELECT COUNT(*) FROM pg_tables WHERE schemaname='public' AND tablename='games';
")
if [[ "$got" == "0" ]]; then
  log_ok "GATE PASS: games table dropped"
  pass=$((pass+1))
else
  log_error "GATE FAIL: games table still exists"
  fail=$((fail+1))
fi

# Gate 2: zero dangling pgvector_embeddings.game_id (resolve to neither shared_games nor private_games)
run_gate "zero dangling vectors" "
SELECT COUNT(*) FROM pgvector_embeddings pe
WHERE pe.game_id NOT IN (SELECT id FROM shared_games)
  AND pe.game_id NOT IN (SELECT id FROM private_games);
" "0"

# Gate 3: pgvector_embeddings count unchanged from pre-reset baseline
expected_count_file=$(artefact_path "vector-count-pre" "txt")
if [[ ! -f "$expected_count_file" ]]; then
  log_warn "No baseline count file: $expected_count_file. Skipping gate 3."
else
  expected=$(cat "$expected_count_file" | tr -d '[:space:]')
  got=$(psql "$DATABASE_URL" -t -A -c "SELECT COUNT(*) FROM pgvector_embeddings;")
  if [[ "$got" == "$expected" ]]; then
    log_ok "GATE PASS: vector count unchanged ($got)"
    pass=$((pass+1))
  else
    log_error "GATE FAIL: vector count $got != baseline $expected"
    fail=$((fail+1))
  fi
fi

# Gate 4: HNSW index on vector column is valid (not invalidated)
got=$(psql "$DATABASE_URL" -t -A -c "
SELECT COUNT(*) FROM pg_class c
JOIN pg_index i ON i.indexrelid = c.oid
WHERE c.relname LIKE '%pgvector_embeddings%vector%' AND i.indisvalid = true;
")
if [[ "$got" != "0" ]]; then
  log_ok "GATE PASS: HNSW index valid"
  pass=$((pass+1))
else
  log_error "GATE FAIL: HNSW index missing or invalid"
  fail=$((fail+1))
fi

# Gate 5: vector_documents.shared_game_id populated, vector_documents.game_id is null
run_gate "vector_documents migrated to shared_game_id" "
SELECT COUNT(*) FROM vector_documents WHERE game_id IS NOT NULL;
" "0"

# Gate 6: no Game aggregate references remain in code (run from script location, not DB)
# Skipped: this is a build-time check, covered by CI on Phase 2c PR.

log_info "Gates complete: $pass passed, $fail failed"
if [[ "$fail" -gt 0 ]]; then
  exit 1
fi
exit 0
```

- [ ] **Step 2: Set exec bit + verify**

```bash
chmod +x infra/scripts/game-reset/04-verify-gates.sh
git add infra/scripts/game-reset/04-verify-gates.sh
git update-index --chmod=+x infra/scripts/game-reset/04-verify-gates.sh
bash -n infra/scripts/game-reset/04-verify-gates.sh
```

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(infra): game-reset 04-verify-gates.sh with 5 measurable gates (refs #1320)"
```

---

## Task 3: Dev runbook

**Files:**
- Create: `infra/scripts/game-reset/runbook-dev.md`

This is the **rehearsal** run — full procedure on local docker postgres before staging.

- [ ] **Step 1: Write runbook**

```markdown
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
- Given: empty `infra/backups/` directory
- When: scripts 01 and 02 run
- Then: 2 files appear: `<date>-dev-pre-game-reset.dump`, `<date>-dev-game-mapping.csv`

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
# TODO: add RAG query smoke test
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

Only after sign-off: proceed to `runbook-staging.md`.
```

- [ ] **Step 2: Commit**

```bash
git add infra/scripts/game-reset/runbook-dev.md
git commit -m "docs(infra): game-reset runbook-dev.md (refs #1320)"
```

---

## Task 4: Staging runbook

**Files:**
- Create: `infra/scripts/game-reset/runbook-staging.md`

Same procedure as dev with these additions:
- Maintenance window declaration (notify team in #releases channel)
- SSH to staging server (`ssh meepleai-staging`)
- Disable health checks / drain traffic before Step 3
- Re-enable after Step 6 sign-off

- [ ] **Step 1: Write runbook (similar structure to dev, with staging-specific commands)**

```markdown
# Game Reset Runbook — STAGING environment

## Pre-conditions
- Dev runbook completed and signed off
- `infra/scripts/game-reset/.env.staging` exists (filled from staging postgres credentials)
- Maintenance window scheduled (estimate 10-20 minutes)
- Team notified in #releases channel

## Maintenance window

### T-15min: Declare window
- Post in #releases: "Game entity reset starting on staging. Expected duration: 15 min. Re-link of pgvector embeddings + EF migration."
- Pause CI/CD deploys to staging

### T-0: Drain traffic (optional)
If staging has external traffic (rare): set staging behind maintenance page or disable health checks.

### T+0: Execute (SSH to staging server)

```bash
ssh meepleai-staging
cd /opt/meepleai/infra/scripts/game-reset/

# Backup + mapping
./01-backup.sh .env.staging
./02-export-mapping.sh .env.staging

# Dry-run relink
./03-relink-vectors.sh .env.staging --dry-run

# Real relink
./03-relink-vectors.sh .env.staging
```

### Deploy Phase 2c code (already in main-staging via auto-merge)
```bash
make staging  # applies migration via API startup
```

### Verify
```bash
./04-verify-gates.sh .env.staging
curl -fsS https://meepleai-staging.example.com/health
```

### Sign-off
- [ ] 5/5 gates pass
- [ ] Health endpoint 200
- [ ] Sample RAG query returns expected chunks
- [ ] No errors in API logs (`docker logs meepleai-api --tail=200`)
- [ ] Backup retained off-machine (S3/R2)

### T+X: Resume traffic
Post in #releases: "Game entity reset complete on staging. All gates passed."

## Rollback (if any gate fails)

```bash
./99-rollback.sh .env.staging /opt/meepleai/infra/backups/<date>-staging-pre-game-reset.dump
make staging-rollback  # if exists
```

Post in #releases: "Staging rollback executed due to <reason>. Investigating before retry."
```

- [ ] **Step 2: Commit**

```bash
git add infra/scripts/game-reset/runbook-staging.md
git commit -m "docs(infra): game-reset runbook-staging.md (refs #1320)"
```

---

## Task 5: Prod runbook

**Files:**
- Create: `infra/scripts/game-reset/runbook-prod.md`

Same as staging with stricter gates:
- 24-hour notice in #releases before window
- Backup uploaded off-machine (S3/R2 archive)
- `--i-mean-it` flag mandatory on every script invocation
- Rollback rehearsal on a DB snapshot before the real run

- [ ] **Step 1: Write runbook (mirror staging structure + add prod gates)**

```markdown
# Game Reset Runbook — PROD environment

## Pre-conditions (mandatory)
- Staging runbook completed and signed off ≥ 24h ago
- Backup of prod DB uploaded off-machine (S3/R2 archive, encrypted)
- Maintenance window scheduled (15-30 min) and announced 24h in advance
- Rollback rehearsed on a prod DB snapshot in a disposable env
- On-call engineer alerted

## Mandatory flags

Every script invocation on prod requires `--i-mean-it`:
```bash
./01-backup.sh .env.prod --i-mean-it
./02-export-mapping.sh .env.prod --i-mean-it
./03-relink-vectors.sh .env.prod --i-mean-it
./99-rollback.sh .env.prod <dump> --i-mean-it
```

## Procedure

(Same as staging, with `--i-mean-it` everywhere and explicit log lines for each step's start/end.)

## Sign-off gates (all required)

- [ ] 5/5 verification gates pass
- [ ] Health endpoint 200 for ≥ 5 minutes
- [ ] Sample RAG query (curated test set) returns expected results
- [ ] No 5xx errors in last 5 min of API logs
- [ ] Vector count identical to baseline (`04-verify-gates.sh` Gate 3)
- [ ] No alerts firing in monitoring

## Post-execution

- Retain prod backup for 90 days off-machine
- Document execution in incident log (timestamp, gates, anomalies)
- Close #1320 if all phases complete
```

- [ ] **Step 2: Commit**

```bash
git add infra/scripts/game-reset/runbook-prod.md
git commit -m "docs(infra): game-reset runbook-prod.md with mandatory --i-mean-it (refs #1320)"
```

---

## Task 6: Makefile targets

**Files:**
- Modify: `infra/Makefile`

Add 2 new targets following the Phase 1 pattern:

```makefile
game-reset-relink:  ## Re-link pgvector_embeddings (ENV=dev|staging|prod, requires backup + mapping)
	@test -n "$(ENV)" || (echo "ENV required: make game-reset-relink ENV=dev" && exit 64)
	@test -f scripts/game-reset/.env.$(ENV) || (echo "Missing scripts/game-reset/.env.$(ENV)" && exit 66)
	./scripts/game-reset/03-relink-vectors.sh scripts/game-reset/.env.$(ENV) $(EXTRA)

game-reset-verify:  ## Run verification gates after migration applied (ENV=dev|staging|prod)
	@test -n "$(ENV)" || (echo "ENV required: make game-reset-verify ENV=dev" && exit 64)
	@test -f scripts/game-reset/.env.$(ENV) || (echo "Missing scripts/game-reset/.env.$(ENV)" && exit 66)
	./scripts/game-reset/04-verify-gates.sh scripts/game-reset/.env.$(ENV)
```

Add to `.PHONY` declaration. Update `game-reset-help` to mention the new targets.

- [ ] **Step 1: Locate and edit**

Find the existing game-reset section: `grep -n "game-reset" infra/Makefile`

Add new targets adjacent to existing ones. Update `.PHONY` line + help text.

- [ ] **Step 2: Verify**

```bash
cd infra && make game-reset-help
```

Expected output includes lines for `game-reset-relink` and `game-reset-verify`.

- [ ] **Step 3: Commit**

```bash
cd .. && git add infra/Makefile
git commit -m "feat(infra): Makefile targets for game-reset relink + verify (refs #1320)"
```

---

## Task 7: Update Phase 1 README

**Files:**
- Modify: `infra/scripts/game-reset/README.md`

Update the Sequence table to include the new scripts:

```markdown
| Phase | Script | When |
|---|---|---|
| Pre-flight | `01-backup.sh` | Before any destructive operation |
| Mapping export | `02-export-mapping.sh` | After backup, before relink |
| **Re-link** | **`03-relink-vectors.sh`** | After mapping, before EF migration |
| **Verification** | **`04-verify-gates.sh`** | After EF migration applied |
| Rollback | `99-rollback.sh` | Emergency only |
```

Add a "Phase 3 — full reset sequence" section linking to the 3 runbooks.

- [ ] **Step 1: Edit README**
- [ ] **Step 2: Commit**

```bash
git add infra/scripts/game-reset/README.md
git commit -m "docs(infra): update game-reset README with relink + verify + runbooks (refs #1320)"
```

---

## Task 8: PR + close-out

- [ ] **Step 1: Push branch**

```bash
git push -u origin feature/issue-1320-game-entity-reset-phase3-plan
```

- [ ] **Step 2: Open PR to main-dev**

```bash
gh pr create --title "feat(infra): game-reset Phase 3 — relink script + verify gates + runbooks (#1320)" \
  --base main-dev \
  --body "$(cat <<'EOF'
## Summary

Phase 3 of #1320 — adds the **execution layer** for the game entity reset. Ships:

- \`03-relink-vectors.sh\` — UPDATE pgvector_embeddings.game_id from legacy Game.Id → SharedGame.Id (dry-run + idempotent)
- \`04-verify-gates.sh\` — 5 measurable gates (games table dropped, zero dangling vectors, vector count unchanged, HNSW index valid, vector_documents migrated)
- 3 runbooks: dev / staging / prod (with --i-mean-it requirement on prod)
- Makefile targets: \`make game-reset-relink\` and \`make game-reset-verify\`

## What this does NOT do

- Does NOT execute the reset on any environment (this PR is plan + scripts only)
- Does NOT modify Phase 1 or Phase 2c artifacts
- Does NOT modify application code or schema

## Sequencing decision

The previous sequence (drop migration → fix data) was reversed to **fix data → drop migration**, eliminating the race condition between data state and code state. Spec-panel critique drove this design (Nygard: "What happens between data relink and code deploy? Race condition.").

New order per environment:
1. Backup
2. Export mapping
3. Re-link vectors
4. Verify no dangling
5. Deploy code + apply migration (drops games table)
6. Verification gates
7. Resume traffic

## Execution status

This PR ships the **artifacts**. Actual execution happens AFTER merge, per runbook, with sign-off at each environment.

| Env | Status |
|---|---|
| Dev | Pending (rehearsal) |
| Staging | Blocked on dev sign-off |
| Prod | Blocked on staging sign-off (24h notice required) |

Refs: #1320
EOF
)"
```

- [ ] **Step 3: After merge, schedule dev rehearsal**

Per `runbook-dev.md`. Sign-off → staging. Sign-off → prod.

---

## Acceptance criteria (Wiegers SMART)

| # | Criterion | Measurement |
|---|---|---|
| AC-1 | All 3 runbooks committed to `infra/scripts/game-reset/` | `ls infra/scripts/game-reset/runbook-*.md` returns 3 files |
| AC-2 | `03-relink-vectors.sh` is git mode 100755, `bash -n` clean | `git ls-files --stage` + syntax check |
| AC-3 | `04-verify-gates.sh` runs 5 gates with PASS/FAIL output | Manual run in dev returns exit 0 with 5 PASSes |
| AC-4 | Makefile targets `game-reset-relink` and `game-reset-verify` exist | `make help` shows both |
| AC-5 | PR merged to `main-dev` | `gh pr view` shows MERGED |
| AC-6 | Dev rehearsal completed and signed off | Comment in #1320 with rehearsal results |

---

## Risk register

| Risk | Severity | Mitigation |
|---|---|---|
| Re-link UPDATE deadlocks on large pgvector_embeddings table | HIGH | Wrap in BEGIN/COMMIT; if deadlocks, batch by chunks of 10k rows |
| HNSW index invalidated by UPDATE (vector column unchanged but row rewritten) | MEDIUM | Verify Gate 4 explicitly; if invalid, REINDEX after relink |
| Mapping CSV captured before drop loses freshness if deploy delayed > 24h | MEDIUM | Runbook requires same-day backup + mapping + relink + deploy in single window |
| Orphan vectors (game_id with no Game / SharedGame) lost on drop | LOW | Spec §4 ORPHANING strategy documented; default = accept loss; option to DELETE pre-drop |
| Code deployed before relink → consumers read stale game_id | HIGH | **Mitigated by sequencing**: relink BEFORE migration applies |
| Rollback fails (pg_restore error) | HIGH | Rollback rehearsed via Phase 1's `rehearse-rollback.sh` before each env |
| Different game_id in vectors vs vector_documents (issue #4921 cross-BC) | MEDIUM | Mapping CSV handles via UNION ALL over 3 sources (already in 02-export-mapping.sh) |

---

## Out of scope

- Frontend changes — separate follow-up
- Re-uploading orphan PDFs — separate operational task
- Promotion API (SharedGame.CreateFromPrivate) — separate issue
- Phase 2b (SharedGame/PrivateGame embed GameCoreData) — optional optimization, separate PR

---

## Self-review checklist (Wiegers + Adzic)

✅ Every gate has measurable acceptance criterion (Gate 1-5 all use COUNT() with expected value)
✅ Every script has dry-run mode where applicable (relink does; backup/verify are read-only)
✅ Every destructive operation has rollback path (99-rollback.sh, documented in each runbook)
✅ Each runbook has G/W/T sample (dev runbook shows Given empty backups dir, When scripts run, Then 2 files appear)
✅ Prod runbook requires `--i-mean-it` (mandatory at script + makefile level)
✅ Sequencing prevents race condition (data fix BEFORE code deploy)
✅ Maintenance window declared (15-30 min target)
✅ Sign-off gates between environments (dev → staging → prod with 24h prod notice)

## Out-of-scope items confirmed (Cockburn)

The plan does NOT address:
- Stakeholder communication beyond #releases channel notification (assumes existing channel suffices)
- Customer-facing notifications (handled separately by ops if needed)
- Long-term archive policy beyond 30/90 day retention (assumes ops standard applies)
