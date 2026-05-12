# Rollback Runbook — Production & Staging

> **Last verified**: 2026-05-12 (DR drill cadence: every 90 days — see [§13](#13-quarterly-dr-drill-cadence))
> **Audience**: On-call engineer (single operator during beta phase)
> **Maintenance owner**: @badsworm (founder)
> **Scope**: Manual revert via image tag for `meepleai.app` (staging) and `meepleai.com` (production, TBD)
> **Sibling docs**: [deploy-staging-runbook.md](./deploy-staging-runbook.md) · [operations-manual.md §17 Incident Response](./operations-manual.md#17-incident-response) · [operations-manual.md §18 DR Procedures](./operations-manual.md#18-maintenance--disaster-recovery) · [ADR-054 Multi-Branch Strategy](../../for-claude/architecture/adr/adr-054-devops-multi-branch-strategy.md)

> **Status of `meepleai.com`**: production host is **not yet provisioned** ([epic #842](https://github.com/meepleAi-app/meepleai-monorepo/issues/842)). This runbook covers the operational model for both environments; staging procedures are validated and prod sections become enforceable when the prod host is provisioned. Until then, treat prod sections as the **target template** to validate on the first prod deploy.

---

## Table of Contents

1. [TL;DR — Emergency Quick Card](#1-tldr--emergency-quick-card)
2. [Decision tree — when to rollback](#2-decision-tree--when-to-rollback)
3. [Prerequisites](#3-prerequisites)
4. [Pre-rollback safety checks](#4-pre-rollback-safety-checks)
5. [Procedure — happy path](#5-procedure--happy-path)
6. [Three rollback scenarios](#6-three-rollback-scenarios)
7. [Multi-service rollback ordering](#7-multi-service-rollback-ordering)
8. [Database backward-compatibility policy](#8-database-backward-compatibility-policy)
9. [Post-rollback validation](#9-post-rollback-validation)
10. [RACI + SLA](#10-raci--sla)
11. [Image tag retention](#11-image-tag-retention)
12. [Communication templates](#12-communication-templates)
13. [Quarterly DR drill cadence](#13-quarterly-dr-drill-cadence)
14. [Post-mortem & follow-up](#14-post-mortem--follow-up)
15. [Future upgrade path](#15-future-upgrade-path)

---

## 1. TL;DR — Emergency Quick Card

```
┌─────────────────────────────────────────────────────────────────┐
│  ROLLBACK IN 4 STEPS (≤ 10 min target RTO)                      │
├─────────────────────────────────────────────────────────────────┤
│  1. IDENTIFY previous-good tag                                   │
│     gh api /orgs/meepleAi-app/packages/container/\               │
│        meepleai-monorepo%2Fapi/versions \                        │
│        --jq '.[1].metadata.container.tags[0]'                    │
│                                                                  │
│  2. (Optional) DRAIN Hangfire if migrations were applied         │
│     ssh staging "docker exec meepleai-api dotnet hangfire-stop"  │
│                                                                  │
│  3. TRIGGER rollback workflow                                    │
│     gh workflow run rollback.yml \                               │
│        -f environment=production \                               │
│        -f image_tag=<previous-good-tag> \                        │
│        -f restore_db=false                                       │
│                                                                  │
│     OR via wrapper: bash infra/scripts/rollback-trigger.sh \     │
│                       production <previous-good-tag>             │
│                                                                  │
│  4. VALIDATE (5 min budget)                                      │
│     - Health: curl -sf https://meepleai.app/health               │
│     - Smoke:  3 critical paths (login + library + chat)          │
│     - Error rate: Grafana < baseline + 50% sustained 5min        │
└─────────────────────────────────────────────────────────────────┘
```

If any step fails, **stop and escalate to founder** (see [§10 RACI](#10-raci--sla)).

---

## 2. Decision tree — when to rollback

Not every incident calls for a rollback. Pick the **least invasive** action that resolves user impact within the SLA.

```
Incident detected
│
├─ Severity P1 (service down, > 5% error rate, data corruption suspected)
│   │
│   ├─ Caused by NEW deploy in last 60 min?
│   │   ├─ YES + schema unchanged → IMAGE ROLLBACK (this runbook §5)
│   │   ├─ YES + schema changed   → IMAGE ROLLBACK + DB RESTORE (§6.2)
│   │   └─ NO                      → Containment first (operations-manual §17)
│   │
│   └─ Suspected data corruption → STOP WRITES, see operations-manual §18
│
├─ Severity P2 (degraded but functional)
│   │
│   ├─ Hotfix available within 30 min? → FORWARD FIX (deploy-staging-runbook)
│   ├─ Hotfix would take > 1 hour     → IMAGE ROLLBACK (this runbook §5)
│   └─ Performance regression only    → CANARY ROLLBACK + analyse (§6.3)
│
└─ Severity P3/P4 → No rollback; backlog hotfix
```

**Heuristic** — *"If the post-deploy diff is small and the fix is obvious, forward-fix. If the diff is large or the failure mode is unknown, rollback first and diagnose offline."*

---

## 3. Prerequisites

### 3.1 Reused existing assets (no duplication)

| Asset | Purpose | Path |
|---|---|---|
| Manual Rollback workflow | Pulls image tag + recreates API/Web containers + healthcheck | `.github/workflows/rollback.yml` |
| Backup automation | Pre-migration `pg_dumpall` + R2 sync (daily 03:00) | `infra/scripts/backup.sh` |
| Backup verify | Integrity check before restore | `infra/scripts/backup-verify.sh` |
| Restore test | Restore to temp pgvector container + schema verify | `infra/scripts/backup-restore-test.sh` |
| DR procedures | Full VPS recovery, DB corruption recovery | `operations-manual.md` §18 |
| Incident response | P1/P2 severity gates, communication template | `operations-manual.md` §17 |
| Deploy runbook | Reference for promote main-dev → staging flow | `deploy-staging-runbook.md` |
| CF Access secrets | Service token for smoke checks behind Cloudflare Access | `infra/secrets/cf-access.secret` |

### 3.2 What you need before triggering rollback

- [ ] `gh` CLI authenticated (`gh auth status`)
- [ ] Repo write access (you can trigger `workflow_dispatch`)
- [ ] SSH key for target host loaded (`~/.ssh/meepleai-staging` or prod equivalent)
- [ ] CF Access service token in `infra/secrets/cf-access.secret` (for post-rollback smoke)
- [ ] Knowledge of the **previous-good image tag** — see [§1 step 1](#1-tldr--emergency-quick-card)
- [ ] Decision on `restore_db`: **default `false`**. Only `true` if a migration ran *and* it caused the incident *and* the migration was backward-incompatible (this should be impossible per [§8](#8-database-backward-compatibility-policy), but guarded as escape hatch).

### 3.3 Image tag conventions (must hold for rollback to work)

The `rollback.yml` workflow expects images at `ghcr.io/meepleai-app/meepleai-monorepo/api:<tag>` and `…/web:<tag>`. Tag formats currently in use:

| Source | Tag format | Example |
|---|---|---|
| Auto-deploy to staging | `staging-YYYYMMDD-<sha7>` | `staging-20260512-a1b2c3d` |
| Auto-deploy to staging | `latest` (mutable — **do not use for rollback**) | `latest` |
| Promoted to prod (future) | `vYYYY.MM.DD-<sha7>` | `v2026.05.12-a1b2c3d` |

**Rule** — never rollback to `latest`. Always use an immutable SHA-suffixed tag.

---

## 4. Pre-rollback safety checks

Run these in the **2-3 minutes** before triggering rollback. Do not skip on a real incident — they make the difference between a clean rollback and amplifying the outage.

### 4.1 Identify the previous-good tag

```bash
# Latest 5 API image versions (most recent first)
gh api \
  /orgs/meepleAi-app/packages/container/meepleai-monorepo%2Fapi/versions \
  --jq '.[].metadata.container.tags' | head -5

# Or: inspect the running staging container to see the *current* tag
ssh meepleai-staging "docker inspect meepleai-api --format '{{.Config.Image}}'"
```

Pick the tag **immediately before** the broken deploy. Confirm with the `Deploy to Staging` workflow log to map tag → commit SHA.

### 4.2 Drain Hangfire jobs (if recent migration ran)

If the broken deploy applied an EF migration, in-flight Hangfire jobs may write data using the *new* schema. Rolling back the schema mid-flight = data corruption.

```bash
# Count pending jobs
ssh meepleai-staging "docker exec meepleai-postgres psql -U meepleai -d meepleai_staging \
  -c \"SELECT count(*) FROM hangfire.job WHERE statename IN ('Enqueued', 'Processing');\""

# If count > 0: pause schedulers (graceful)
ssh meepleai-staging "docker exec meepleai-api curl -sf -X POST http://localhost:8080/admin/hangfire/pause"
# (Requires admin auth — use bearer token from secrets)

# Wait up to 60s for Processing jobs to finish
ssh meepleai-staging "for i in {1..30}; do
  CNT=\$(docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -t -c \"SELECT count(*) FROM hangfire.job WHERE statename='Processing';\" | xargs)
  if [ \"\$CNT\" = \"0\" ]; then echo 'drained'; break; fi
  echo \"\$i: \$CNT processing\"; sleep 2
done"
```

If jobs do not drain within 60 s, **dump pending job state** for forensic analysis and proceed (data loss accepted in P1 — log the loss in post-mortem):

```bash
ssh meepleai-staging "docker exec meepleai-postgres pg_dump -U meepleai -d meepleai_staging \
  -t hangfire.job -t hangfire.state -t hangfire.jobparameter \
  > /backups/hangfire-pre-rollback-\$(date +%Y%m%d-%H%M%S).sql"
```

### 4.3 Capture baseline metrics (for post-rollback comparison)

```bash
# Error rate baseline (last 15 min before incident detection)
curl -sf "http://meepleai-staging:9090/api/v1/query" \
  --data-urlencode 'query=rate(http_requests_total{status=~"5.."}[15m])' \
  -H "CF-Access-Client-Id: ${CF_ACCESS_CLIENT_ID}" \
  -H "CF-Access-Client-Secret: ${CF_ACCESS_CLIENT_SECRET}" \
  | jq '.data.result[0].value[1]' > /tmp/error-rate-baseline.txt

# Save current container state for forensic
ssh meepleai-staging "docker ps --format json > /tmp/pre-rollback-state-\$(date +%Y%m%d-%H%M%S).json"
```

### 4.4 Confirm backup exists (only if rollback may require DB restore)

```bash
ssh meepleai-staging "ls -lhrt /backups/meepleai/ | tail -3"
# Expect a backup directory dated < 24 h ago (per backup.sh cron)

# Verify integrity before relying on it
ssh meepleai-staging "bash /opt/meepleai/repo/infra/scripts/backup-verify.sh"
```

---

## 5. Procedure — happy path

### 5.1 Option A — via wrapper script (recommended)

```bash
# From your local machine, with gh CLI authenticated
bash infra/scripts/rollback-trigger.sh production v2026.05.11-deadbee
# Or staging:
bash infra/scripts/rollback-trigger.sh staging staging-20260511-deadbee
```

The wrapper validates environment, tag format, GH auth, and asks for **explicit operator confirmation** before calling `gh workflow run rollback.yml`.

### 5.2 Option B — direct workflow_dispatch

```bash
gh workflow run rollback.yml \
  -f environment=production \
  -f image_tag=v2026.05.11-deadbee \
  -f restore_db=false

# Watch progress
gh run watch $(gh run list --workflow rollback.yml --limit 1 --json databaseId --jq '.[0].databaseId')
```

### 5.3 What the workflow does

The `.github/workflows/rollback.yml` workflow performs (per `rollback-staging` or `rollback-production` job):

1. `docker login` to GHCR
2. SSH to target host
3. `docker pull` API + Web images at the requested tag
4. `docker compose up -d --no-deps api web` with the override `IMAGE` env vars
5. (Prod only, opt-in) `psql … < pre-migration-backup.sql` to restore DB if `restore_db=true`
6. 12 × 5 s health check loop on `http://localhost:8080/health` (60 s total budget)
7. Slack notification on completion or failure (critical channel for prod)

**Healthcheck timeout = 60 s** is intentional. If the rollback image fails to come healthy in 60 s, the workflow fails and the previous (broken) containers are still down — proceed to [§9.5 escalation](#95-escalation-on-rollback-failure).

---

## 6. Three rollback scenarios

### 6.1 Scenario A — bad image (no schema change)

> **Example**: deploy introduced a regression in API logic; no migration ran in this release.

**Indicators**: error rate spike, no new files in `apps/api/src/Api/Migrations/` between previous-good and broken tag.

**Steps**:

1. Identify previous-good tag (§4.1)
2. Skip Hangfire drain (§4.2 not needed — schema unchanged)
3. Capture baseline (§4.3)
4. Trigger rollback **without** `restore_db` (§5)
5. Validate (§9)

**Expected RTO**: ≤ 5 min (no DB restore overhead)

**Simulation cadence**: Every quarterly DR drill (§13).

### 6.2 Scenario B — bad migration (schema change applied)

> **Example**: migration `AddRequiredColumnX` deployed; existing rows fail constraint; API throws on every write.

**Indicators**: error logs mention column / constraint / FK names from the new migration; `dotnet ef migrations list` on staging shows the broken migration as latest applied.

**Steps**:

1. Identify previous-good tag (§4.1)
2. **Run §4.2 Hangfire drain** — mandatory for schema rollback
3. Run §4.4 backup verify
4. Trigger rollback **with** `restore_db=true` — workflow restores from latest `pre-migration-*.sql` backup
5. Validate (§9)
6. **Post-restore migration audit**: confirm `__EFMigrationsHistory` no longer contains the broken migration:
   ```bash
   ssh meepleai-staging "docker exec meepleai-postgres psql -U meepleai -d meepleai_staging \
     -c 'SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 5;'"
   ```

**Expected RTO**: 8-12 min (includes `psql` restore + healthcheck)

**RPO**: Last `backup.sh` run before the broken deploy. If deploy happened > 24 h after last backup, **data loss for that window** is accepted in P1.

> **Migration safety note**: per [§8](#8-database-backward-compatibility-policy), migrations should be backward-compatible. Scenario B should be *rare* and only triggered when [§8](#8-database-backward-compatibility-policy) was violated.

**Simulation cadence**: Every quarterly DR drill (§13).

### 6.3 Scenario C — slow leak / performance regression

> **Example**: P95 latency doubles over 30 min after deploy; no errors but UX degraded.

**Indicators**: Grafana P95 dashboard delta > 100% vs baseline; error rate unchanged; user complaints.

**Steps**:

1. Identify previous-good tag (§4.1)
2. **Decide forward-fix vs rollback** — if hotfix < 30 min, prefer forward-fix
3. If rollback chosen:
   - No Hangfire drain needed (no schema change)
   - Capture extended baseline metrics: P50, P95, P99, RPS
   - Trigger rollback (§5)
4. **Post-rollback root-cause investigation is mandatory** — slow leaks indicate observability gap

**Expected RTO**: ≤ 5 min for the rollback itself.

**Caveat**: rollback hides the symptom; you still need to find the cause. Schedule post-mortem (§14) within 48 h.

**Simulation cadence**: Annually (or after the first real-world slow-leak incident).

---

## 7. Multi-service rollback ordering

When API + Web + Embedding + Reranker are all part of a release, rollback order matters because of contract coupling.

| Change type | Rollback order | Rationale |
|---|---|---|
| API + Web coupled (e.g. new endpoint + client) | **Web first, then API** | Client falls back to old contract before server reverts |
| DB schema change (backward-compat per §8) | **No order constraint** — rollback API+Web together | Schema can serve both versions |
| DB schema change (breaking, violated §8) | **DB restore first, then API+Web** | Avoid API hitting impossible schema state |
| Embedding / Reranker only | **Independent** — rollback last | Decoupled from API contract via internal HTTP |
| Multiple services + DB | **DB → API → Web** | Schema is foundation, API depends on schema, Web depends on API |

> The current `rollback.yml` rolls API+Web together in a single `docker compose up`. This is acceptable for the beta phase (contract coupling rare, blast radius small). When the service surface grows, split into per-service workflow inputs.

---

## 8. Database backward-compatibility policy

**Rule** — every EF migration deployed to production MUST be **rollback-safe**, meaning the *previous* code version can still run against the new schema.

### 8.1 Allowed in a single migration

- `ADD COLUMN … NULL` (with default if NOT NULL needed temporarily)
- `CREATE TABLE` (new tables)
- `CREATE INDEX CONCURRENTLY` (if using raw SQL escape — EF doesn't support concurrent natively)
- `ADD CONSTRAINT … NOT VALID` then validate in follow-up
- Data backfills behind feature flag

### 8.2 Forbidden in a single migration

- `DROP COLUMN` of column still read by previous code
- `DROP TABLE` of table still referenced
- `ALTER COLUMN TYPE` to a narrower or incompatible type
- `RENAME COLUMN` / `RENAME TABLE` without a temporary alias
- `ADD COLUMN NOT NULL` without default (fails on existing rows)

### 8.3 Pattern — expand → migrate → contract (two-deploy pattern)

For breaking schema changes, split into two deploys. Example: rename `users.email` → `users.email_address`.

**Deploy N (expand)**:
```csharp
public partial class AddEmailAddressColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "email_address",
            table: "users",
            type: "varchar(320)",
            nullable: true);

        migrationBuilder.Sql(@"UPDATE users SET email_address = email WHERE email_address IS NULL;");
    }
}
```
- API code: dual-write (writes both `email` and `email_address`); reads from `email_address` with fallback to `email`.
- **Soak time**: ≥ 7 days in staging + prod before next deploy.
- **Rollback-safe**: yes — previous code still reads `email`.

**Deploy N+1 (contract)**:
```csharp
public partial class DropEmailColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "email", table: "users");
    }
}
```
- API code: reads/writes `email_address` only.
- **Pre-deploy gate**: confirm zero reads of `email` in last 7 days (log query or feature-flag telemetry).
- **Rollback-safe within N+1**: still yes — restoring to Deploy N code requires DB restore from §6.2, which is the documented escape hatch.

### 8.4 Enforcement (current state vs target)

**Current**: policy enforced by code review only.
**Target** (tracked in [#1087](https://github.com/meepleAi-app/meepleai-monorepo/issues/1087)): CI gate in `staging.yml` that parses migration `.sql` artifacts and fails on `DROP COLUMN|DROP TABLE|ALTER COLUMN.*TYPE` without an explicit `-- safe: deferred-contract` directive.

---

## 9. Post-rollback validation

### 9.1 Required success criteria (must all pass before declaring rollback complete)

| Criterion | Check | Pass threshold |
|---|---|---|
| **Health endpoint** | `curl -sf https://meepleai.app/health` | HTTP 200, < 500 ms |
| **API smoke — login** | POST `/api/v1/auth/login` with test account | HTTP 200 + session cookie |
| **API smoke — library** | GET `/api/v1/library` authenticated | HTTP 200 + non-empty JSON |
| **API smoke — chat** | POST `/api/v1/chat-threads/<id>/messages` | HTTP 200 + SSE stream starts |
| **Error rate** | Grafana `rate(http_requests_total{status=~"5.."}[5m])` | < baseline + 50% (from §4.3) |
| **Hangfire** | `SELECT count(*) FROM hangfire.job WHERE statename='Failed'` | No new Failed jobs in last 5 min |
| **DB row counts** (if DB restored) | Spot-check 3 critical tables: `users`, `games`, `chat_threads` | Counts within ±5 % of pre-rollback snapshot |

### 9.2 Smoke validation script (through CF Access)

```bash
set -a; source infra/secrets/cf-access.secret; set +a

# Health
curl -sf -w "%{http_code}\n" \
  -H "CF-Access-Client-Id: ${CF_ACCESS_CLIENT_ID}" \
  -H "CF-Access-Client-Secret: ${CF_ACCESS_CLIENT_SECRET}" \
  https://meepleai.app/health

# Library (use a smoke account session token from secrets)
curl -sf -w "%{http_code}\n" \
  -H "CF-Access-Client-Id: ${CF_ACCESS_CLIENT_ID}" \
  -H "CF-Access-Client-Secret: ${CF_ACCESS_CLIENT_SECRET}" \
  -H "Cookie: meepleai_session=${SMOKE_SESSION_TOKEN}" \
  https://meepleai.app/api/v1/library \
  | jq '. | length'

unset CF_ACCESS_CLIENT_ID CF_ACCESS_CLIENT_SECRET
```

### 9.3 Validation budget

- **Soft target**: all checks pass within 5 min of rollback completion
- **Hard limit**: 15 min — if smoke still failing at 15 min, treat as **failed rollback** and escalate

### 9.4 Observability during validation

Watch in parallel:
- Grafana `Production Overview` dashboard (P50/P95 latency + error rate)
- `docker logs meepleai-api -f --tail=20` for ERROR-level entries
- Sentry incoming events (if integrated)

### 9.5 Escalation on rollback failure

If health/smoke fails after rollback:

1. **Do not retry the same rollback** — it will fail the same way
2. Capture: workflow run URL + `docker logs` + `docker ps` state
3. **Notify founder** (badsworm@libero.it) via Slack `#critical` and email
4. Consider **emergency forward-fix** (small patch on `main-staging`, fast-track promotion)
5. Worst case: **full DR procedure** (operations-manual §18) — VPS rebuild

---

## 10. RACI + SLA

### 10.1 Roles during beta phase

| Role | Person | Responsibility |
|---|---|---|
| **Responsible** | On-call engineer (currently = founder) | Executes rollback steps |
| **Accountable** | @badsworm (founder) | Authorises rollback decision, signs post-mortem |
| **Consulted** | None during beta | (Will add team leads when ≥ 3 engineers) |
| **Informed** | Active users via status banner (when implemented) + Slack `#critical` | Knows there is an incident, gets ETA |

### 10.2 SLA — decision time

| Phase | SLA | Notes |
|---|---|---|
| **Detection → Decision** | ≤ 15 min | From P1 alert to "we're rolling back" — see §2 decision tree |
| **Decision → Workflow trigger** | ≤ 5 min | Includes §4 safety checks |
| **Workflow trigger → Healthy state** | ≤ 10 min | Per `rollback.yml` healthcheck budget |
| **Healthy state → User-facing confirmation** | ≤ 5 min | Smoke validation §9 |
| **Total — incident detection → service restored** | **≤ 35 min** | Soft target during beta; aim for tighter as team grows |

### 10.3 Authorisation matrix

| Action | Requires |
|---|---|
| Rollback staging (image only) | Self-authorise (operator) |
| Rollback staging (with DB restore) | Self-authorise + Slack `#critical` notification |
| Rollback prod (image only) | Self-authorise (during beta, single operator) |
| Rollback prod (with DB restore) | Self-authorise + post-incident written record in [§14 post-mortem](#14-post-mortem--follow-up) |
| **Full DR (VPS rebuild)** | Founder approval (operations-manual §18) |

> **Future expansion** — when the team reaches 3+ engineers, prod DB restore requires a second engineer approval (4-eye rule). Tracked in epic [#842](https://github.com/meepleAi-app/meepleai-monorepo/issues/842) close-out.

---

## 11. Image tag retention

### 11.1 Current state

GHCR has **no automatic retention policy** configured on `meepleai-monorepo/api` or `meepleai-monorepo/web` packages. Untagged versions are pruned by GHCR after 30 days by default; tagged versions persist indefinitely.

### 11.2 Policy

| Image source | Tag pattern | Retention |
|---|---|---|
| Staging deploys | `staging-YYYYMMDD-<sha7>` | **Keep last 30 builds** (rolling — ~30 days at current cadence) |
| Prod releases (future) | `vYYYY.MM.DD-<sha7>` | **Keep last 10 releases** + `latest` |
| Mutable tags | `latest`, `staging` | Always overwritten — never used for rollback |

### 11.3 Enforcement

Manual prune via GitHub Packages UI for now. To automate (future work):

```yaml
# .github/workflows/ghcr-retention.yml (not yet implemented)
- uses: actions/delete-package-versions@v5
  with:
    package-name: 'meepleai-monorepo/api'
    package-type: 'container'
    min-versions-to-keep: 30
    delete-only-untagged-versions: false
```

Tracked as part of [#850 CI cost optimization](https://github.com/meepleAi-app/meepleai-monorepo/issues/850).

---

## 12. Communication templates

### 12.1 Internal — Slack `#critical` (started)

```
:rotating_light: ROLLBACK INITIATED — production
Trigger: <one-line description of incident>
Severity: P1
Rolling back from <broken-tag> → <previous-good-tag>
DB restore: <yes/no>
ETA service restored: 10 min
Runbook: https://github.com/meepleAi-app/meepleai-monorepo/blob/main-dev/docs/for-developers/operations/rollback-runbook.md
```

### 12.2 Internal — Slack `#critical` (complete)

```
:white_check_mark: ROLLBACK COMPLETE — production
Restored to: <previous-good-tag>
Total downtime: <X min>
Smoke validation: PASSED
Hangfire failed jobs in window: <N>
Post-mortem: <link to doc, created within 48h>
```

### 12.3 External — status banner (in-app, when implemented)

> Beta-phase placeholder. Status banner component is tracked as follow-up [§14.2](#142-follow-up-issues-spawned-by-848). Until then, manually post in product chat / email active users.

**During incident** (≤ 240 chars):
```
We are investigating a service disruption affecting <feature>.
Service may be briefly unavailable while we apply a fix.
Updates: <link to status page or email>
```

**After resolution**:
```
The service disruption affecting <feature> has been resolved.
Total impact window: <X> minutes. We are reviewing the root cause
and will share a post-mortem within 48 hours.
```

### 12.4 External — Email to active beta users

```
Subject: MeepleAI — service incident resolved (DD MMM YYYY)

Hi <name>,

Today between HH:MM and HH:MM (UTC), MeepleAI experienced a service
disruption affecting <feature>. The issue has been fully resolved.

What happened: <one-paragraph factual summary, no jargon>
What we did: <reverted the change, restored service>
What we are doing next: <root-cause analysis + prevention measures>

We will share a public post-mortem within 48 hours at <link>.

Thank you for being part of the beta — your feedback helps us
build a more reliable service.

— The MeepleAI team
```

---

## 13. Quarterly DR drill cadence

Every 90 days, the on-call engineer must:

1. Read this runbook end-to-end and confirm each command still matches infrastructure
2. Execute **one of the three scenarios** ([§6](#6-three-rollback-scenarios)) on **staging only** as a dry run:
   - Q1: Scenario A (bad image)
   - Q2: Scenario B (bad migration)
   - Q3: Scenario A again (most-likely real case)
   - Q4: Scenario C (slow leak)
3. Update the **Last verified** header of this file (line 3)
4. Add an entry to `docs/for-developers/operations/dr-walkthrough-log.md` (created on first drill)
5. File issues for any drift discovered

### 13.1 Drill reminder automation

The `infra/scripts/dr-walkthrough-reminder.sh` script runs via cron (`0 9 1 1,4,7,10 *`) and posts to Slack. The script was created in [PR #259](https://github.com/meepleAi-app/meepleai-monorepo/pull/259) but referenced obsolete paths (`docs/operations/disaster-recovery-runbook.md` was moved/inlined). Path corrections shipped with this runbook — see [`dr-walkthrough-reminder.sh`](../../../infra/scripts/dr-walkthrough-reminder.sh).

### 13.2 Quarterly checklist (copy-paste template)

```
## Q<N> YYYY DR Drill

- [ ] Date: YYYY-MM-DD
- [ ] Operator: <name>
- [ ] Scenario: <A | B | C>
- [ ] Pre-drill staging baseline captured: <link to Grafana snapshot>
- [ ] Rollback executed: <gh run URL>
- [ ] All §9.1 success criteria met: <yes/no>
- [ ] RTO actual: <X min> (target ≤ 10 min)
- [ ] Drift discovered: <list of stale commands, paths, secrets>
- [ ] Follow-up issues filed: #..., #...
- [ ] Last verified header updated: <commit SHA>
```

---

## 14. Post-mortem & follow-up

### 14.1 When required

| Trigger | Post-mortem required? | Timeline |
|---|---|---|
| Real prod rollback executed | **Yes** | Within 48 h |
| Real staging rollback executed | Recommended (1-page) | Within 1 week |
| DR drill in §13 | Optional unless drift found | — |
| Scenario C (slow leak) | **Always** — required to find root cause | Within 48 h |
| Failed rollback (escalated per §9.5) | **Yes, with founder review** | Within 24 h |

### 14.2 Follow-up issues spawned by #848

This runbook intentionally references work that is **out-of-scope** but adjacent. Tracked as follow-up issues:

- [ ] [#1087](https://github.com/meepleAi-app/meepleai-monorepo/issues/1087) — **CI migration safety gate**: parse SQL artifacts in `staging.yml`, fail on forbidden patterns from §8.2
- [ ] [#1088](https://github.com/meepleAi-app/meepleai-monorepo/issues/1088) — **Post-mortem template doc**: structured Markdown template at `docs/for-developers/operations/post-mortem-template.md`
- [ ] [#1089](https://github.com/meepleAi-app/meepleai-monorepo/issues/1089) — **Status banner FE component**: implement in-app banner (refs §12.3) with admin controls

### 14.3 Post-mortem skeleton (until full template exists)

```markdown
# Post-mortem — <Incident title> (YYYY-MM-DD)

## Summary
One paragraph: what happened, impact, how we fixed it.

## Timeline (UTC)
- HH:MM — Detection
- HH:MM — Decision to rollback
- HH:MM — Workflow triggered
- HH:MM — Service healthy
- HH:MM — Smoke validation passed

## Root cause
What allowed the broken code to reach prod / staging? Where did the
review / CI / monitoring gap occur?

## Resolution
What action restored service. Reference §6 scenario.

## What went well
Concrete: list 2-3 things.

## What went poorly
Concrete: list 2-3 things. No blame.

## Action items
- [ ] <Concrete, dated, owner-assigned action>
- [ ] ...
```

---

## 15. Future upgrade path

The runbook assumes a **single VPS, manual revert** model. Triggers for upgrading to a more sophisticated model:

| Current model | Upgrade target | Trigger |
|---|---|---|
| Manual `workflow_dispatch` rollback | Automated canary + auto-rollback | DAU ≥ 100 sustained 30 days |
| Single VPS | Blue/green deploy (active + idle pair) | Paying customers OR RTO violations ≥ 2/quarter |
| Sample-based smoke validation | Synthetic monitoring (24/7 probes) | Active users ≥ 500 |
| Image-tag rollback only | Feature-flag-based gradual rollout | Multiple concurrent features in flight |

See [ADR-054 §Alternatives](../../for-claude/architecture/adr/adr-054-devops-multi-branch-strategy.md#alternatives-considered) for the decision context.

---

## Appendix — quick links

- [`.github/workflows/rollback.yml`](../../../.github/workflows/rollback.yml) — the workflow this runbook drives
- [`infra/scripts/rollback-trigger.sh`](../../../infra/scripts/rollback-trigger.sh) — local wrapper
- [`infra/scripts/backup.sh`](../../../infra/scripts/backup.sh) — backup automation
- [`infra/scripts/backup-restore-test.sh`](../../../infra/scripts/backup-restore-test.sh) — DB restore verification
- [`infra/scripts/dr-walkthrough-reminder.sh`](../../../infra/scripts/dr-walkthrough-reminder.sh) — quarterly drill reminder
- [operations-manual.md §17](./operations-manual.md#17-incident-response) — incident response procedure
- [operations-manual.md §18](./operations-manual.md#18-maintenance--disaster-recovery) — full DR procedures
- [deploy-staging-runbook.md](./deploy-staging-runbook.md) — staging deploy flow
- [ADR-054 Multi-Branch Strategy](../../for-claude/architecture/adr/adr-054-devops-multi-branch-strategy.md) — the broader DevOps context
- [Epic #842](https://github.com/meepleAi-app/meepleai-monorepo/issues/842) — DevOps multi-branch tracker
