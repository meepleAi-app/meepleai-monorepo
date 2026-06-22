# Game Reset Runbook — STAGING environment

**Purpose**: Execute the game entity reset on staging after dev rehearsal sign-off.

## Pre-conditions

- [Dev runbook](./runbook-dev.md) completed and signed off
- `infra/scripts/game-reset/.env.staging` exists (filled from staging postgres credentials, SSH access verified)
- Maintenance window scheduled (estimate 15-20 minutes)
- Team notified in #releases channel

## Maintenance window

### T-15min: Declare window

- Post in #releases:
  > Game entity reset starting on staging. Expected duration: 15 min. Re-link of pgvector embeddings + EF migration `DropGameAggregate_Issue1320`. Tracking: #1320.
- Pause CI/CD auto-deploys to staging

### T-0: Drain traffic (optional)

If staging has external traffic: set staging behind maintenance page or disable health checks via load balancer.

### T+0: Execute (SSH to staging server)

```bash
ssh meepleai-staging
cd /opt/meepleai/infra/scripts/game-reset/

# Backup + mapping
./01-backup.sh .env.staging
./02-export-mapping.sh .env.staging
```

**Gate** (Adzic G/W/T):
- **Given**: clean staging DB at pre-reset state
- **When**: 01-backup.sh + 02-export-mapping.sh run
- **Then**: `<date>-staging-pre-game-reset.dump` + `<date>-staging-game-mapping.csv` exist in `infra/backups/`

### Step 3: Dry-run + real relink

```bash
# Dry-run first
./03-relink-vectors.sh .env.staging --dry-run

# Real run
./03-relink-vectors.sh .env.staging
```

Verify no warnings about dangling vectors. If warnings: stop, escalate.

### Step 4: Verify no dangling pre-drop

```bash
psql "$(grep DATABASE_URL .env.staging | cut -d= -f2-)" -c "
SELECT COUNT(*) FROM pgvector_embeddings pe
WHERE pe.game_id NOT IN (SELECT id FROM games)
  AND pe.game_id NOT IN (SELECT id FROM shared_games);
"
```

Expected: 0 (or accept loss per spec ORPHANING strategy).

### Step 5: Apply EF migration via service deploy

Phase 2c code is already in `main-staging` via auto-merge. Trigger deploy:

```bash
make staging
```

This restarts the API container; on startup, EF applies `DropGameAggregate_Issue1320` which drops the games table.

**Gate**:
- **Given**: relink complete + pre-drop verification 0
- **When**: `make staging` triggers API restart
- **Then**: API logs show "Applying migration: DropGameAggregate_Issue1320" + no errors

### Step 6: Verification gates

```bash
./04-verify-gates.sh .env.staging
```

Expected: `Gates complete: 5 passed, 0 failed`, exit 0.

### Step 6b: Smoke test

```bash
# Health endpoint
curl -fsS https://meepleai-staging.example.com/health
# Expected: 200 OK

# Sample RAG query against a known game (replace <game_id> with a known SharedGame.Id)
curl -X POST https://meepleai-staging.example.com/api/v1/games/<game_id>/chat \
  -H 'Content-Type: application/json' \
  -d '{"message": "How do I win?"}' \
  -fsS | head -50
# Expected: streaming response with relevant chunks
```

### Sign-off (all required)

- [ ] 5/5 gates pass
- [ ] Health endpoint 200 for ≥ 3 minutes
- [ ] Sample RAG query returns expected chunks
- [ ] No errors in API logs (`docker logs meepleai-api --tail=200`)
- [ ] Backup retained off-machine (S3/R2 archive)
- [ ] Mapping CSV retained for audit

### T+X: Resume traffic

- Re-enable load balancer health checks (if disabled)
- Resume CI/CD auto-deploys
- Post in #releases:
  > Game entity reset complete on staging. All gates passed. PR #1320 phase 3 staging done. Proceeding to prod scheduling (24h notice required).

## Rollback (if any gate fails)

```bash
./99-rollback.sh .env.staging /opt/meepleai/infra/backups/<date>-staging-pre-game-reset.dump
# Confirm DB restored
./04-verify-gates.sh .env.staging  # all gates SHOULD fail again (table back)
```

If rollback also fails: escalate to on-call. Restore from S3/R2 archive.

Post in #releases:
> Staging rollback executed due to: <reason>. Investigating before retry.

## Next step

After sign-off + 24h soak: proceed to [`runbook-prod.md`](./runbook-prod.md).
