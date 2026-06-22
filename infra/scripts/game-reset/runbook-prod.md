# Game Reset Runbook — PROD environment

**Purpose**: Execute the game entity reset on production after staging sign-off + 24h soak.

## Pre-conditions (mandatory — do not proceed without all)

- [Staging runbook](./runbook-staging.md) completed and signed off ≥ 24 hours ago
- Backup of prod DB uploaded off-machine (S3/R2 archive, encrypted)
- Maintenance window scheduled (15-30 min target) and announced **24 hours in advance**
- Rollback rehearsed on a prod DB snapshot in a disposable env
- On-call engineer alerted and available during window
- Customer status page updated with maintenance notice (if applicable)

## Mandatory production flag

Every script invocation on prod requires `--i-mean-it`. Without it, scripts abort with exit 77.

```bash
./01-backup.sh .env.prod --i-mean-it
./02-export-mapping.sh .env.prod --i-mean-it
./03-relink-vectors.sh .env.prod --i-mean-it
./04-verify-gates.sh .env.prod
./99-rollback.sh .env.prod <dump> --i-mean-it
```

(`04-verify-gates.sh` is read-only — no `--i-mean-it` needed.)

## Maintenance window

### T-24h: Announcement

- Post in #releases:
  > **24h notice**: Game entity reset scheduled for prod on <YYYY-MM-DD> at <HH:MM> UTC. Expected duration: 30 min. Tracking: #1320.
- Update status page (if applicable)
- Pause non-critical deploys to prod for 24h preceding window

### T-1h: Final pre-flight

- Verify staging environment is healthy (no regressions since staging sign-off)
- Confirm on-call engineer is online
- Confirm off-machine backup is uploaded and verified (`aws s3 ls` or equivalent)
- Re-rehearse rollback on disposable env if not done in last 24h

### T-15min: Declare window

- Post in #releases:
  > **STARTING NOW**: prod game entity reset. Maintenance window begins. Expected resume: T+30min.

### T-5min: Drain traffic

- Set load balancer to maintenance page
- Disable autoscaler scale-up events
- Wait for in-flight requests to drain (typically < 60s)

### T+0: Execute (SSH to prod server)

```bash
ssh meepleai-prod
cd /opt/meepleai/infra/scripts/game-reset/

# Backup (with off-machine upload as part of the runbook, NOT the script)
./01-backup.sh .env.prod --i-mean-it
aws s3 cp /opt/meepleai/infra/backups/<date>-prod-pre-game-reset.dump s3://meepleai-prod-archive/game-reset/

# Mapping export
./02-export-mapping.sh .env.prod --i-mean-it

# Dry-run relink (mandatory on prod)
./03-relink-vectors.sh .env.prod --dry-run --i-mean-it

# Real relink
./03-relink-vectors.sh .env.prod --i-mean-it
```

### Step 5: Apply EF migration via service deploy

Phase 2c code reaches prod via `main` branch deploy. Trigger:

```bash
make production  # or whatever invokes the prod deploy
```

Monitor API startup logs:

```bash
docker logs -f meepleai-api 2>&1 | tee /tmp/migration-log.txt
```

Expected: "Applying migration: DropGameAggregate_Issue1320" + "Done." within 30 seconds.

### Step 6: Verification gates

```bash
./04-verify-gates.sh .env.prod
```

Expected: `Gates complete: 5 passed, 0 failed`, exit 0.

### Step 6b: Smoke test (extended for prod)

```bash
# Health
curl -fsS https://api.meepleai.com/health
# Expected: 200 OK

# Multi-game RAG smoke test (use curated test set of 5 known games)
for game_id in <id1> <id2> <id3> <id4> <id5>; do
  curl -X POST https://api.meepleai.com/api/v1/games/$game_id/chat \
    -H 'Content-Type: application/json' \
    -d '{"message": "How do I win?"}' \
    -fsS -o /dev/null -w "$game_id: %{http_code}\n"
done
# Expected: all 200
```

### Sign-off (all required, no exceptions)

- [ ] 5/5 verification gates pass
- [ ] Health endpoint 200 for ≥ 5 minutes
- [ ] Sample RAG queries (curated test set of 5) all return 200
- [ ] No 5xx errors in last 5 min of API logs
- [ ] Vector count identical to baseline (`04-verify-gates.sh` Gate 3)
- [ ] No alerts firing in monitoring (Grafana, Sentry, etc.)
- [ ] Off-machine backup confirmed in S3/R2 (file size > 0)

### T+X: Resume traffic

- Remove load balancer maintenance page
- Re-enable autoscaler
- Resume CI/CD auto-deploys to prod
- Post in #releases:
  > **COMPLETE**: prod game entity reset done. All gates passed. Vector count unchanged. RAG smoke tests green. Closing maintenance window.
- Update status page (clear maintenance notice)

## Rollback (if any gate fails)

```bash
./99-rollback.sh .env.prod /opt/meepleai/infra/backups/<date>-prod-pre-game-reset.dump --i-mean-it

# Re-deploy previous code via rollback deploy
make production-rollback  # or revert PR + re-deploy
```

Post in #releases:
> **ROLLBACK EXECUTED**: prod game entity reset rolled back due to: <reason>. Status page updated. Investigating root cause before retry.

Update status page with degraded status until investigation complete.

## Post-execution

- Retain prod backup for **90 days** off-machine
- Document execution in incident log:
  - Timestamp (start, end, each step)
  - All gate results
  - Any anomalies or warnings observed
- Close #1320 if all phases complete and prod stable for 7 days
- Generate post-mortem if rollback was executed

## Escalation contacts

(Fill in with actual on-call contacts at execution time)

- Primary on-call: <name / Slack handle>
- Secondary on-call: <name / Slack handle>
- DB ops lead: <name / Slack handle>
- Engineering manager: <name / Slack handle>
