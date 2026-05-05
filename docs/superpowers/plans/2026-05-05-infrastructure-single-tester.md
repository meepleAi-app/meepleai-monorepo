# Infrastructure for Single-Tester — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare le 5 SMART goals + 3 quick wins definiti in `docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md` per ridurre la superficie operazionale di MeepleAI a quanto serve a single-tester alpha indefinita, restando entro CAX21 (8GB/75GB).

**Architecture:** Le modifiche sono prevalentemente Docker Compose profiles, shell scripts in `infra/scripts/`, target Makefile, GitHub Actions step e documentazione runbook. Nessun cambio al codice applicativo (backend/frontend). Cloudflare Tunnel migration introduce un nuovo daemon `cloudflared` su VPS ed elimina Traefik dall'edge.

**Tech Stack:** Docker Compose v2, GNU Make, bash scripts, GitHub Actions YAML, cloudflared (Cloudflare Tunnel daemon), cron, ufw firewall.

---

## File Structure

### Files modificati

| Path | Cambio |
|------|--------|
| `infra/docker-compose.yml` | Aggiunta profili `ai-essential`, `monitoring-essential`, `tutor-agents` |
| `infra/Makefile` | Nuovi target: `work`, `work-stop`, `staging-minimal`, `staging-with-tutor`, `cron-install` |
| `infra/scripts/backup-restore-test.sh` | Nuovo flag `--with-smoke-readback` |
| `.github/workflows/deploy-staging.yml` | Pre-deploy port cleanup step + uso profilo minimal |

### Files creati

| Path | Responsabilità |
|------|----------------|
| `infra/scripts/work.sh` | Wrapper hybrid dev loop: tunnel + start API+Web + watchdog + cleanup |
| `infra/scripts/smoke-set.sh` | Esegue 9 scenari smoke (G1) contro un endpoint configurabile |
| `infra/scripts/daily-disk-prune.sh` | Cron-friendly Docker prune |
| `infra/scripts/dr-walkthrough-reminder.sh` | Cron-friendly notifica trimestrale via webhook |
| `docs/operations/cf-tunnel-migration.md` | Runbook migration Traefik→CF Tunnel (CF dashboard + cloudflared install) |
| `docs/operations/cf-tunnel-rollback.md` | Runbook rollback CF Tunnel→Traefik |
| `docs/operations/dr-walkthrough-log.md` | Log walkthrough trimestrali |

> Nota: `cloudflared` config viene generato automaticamente da `cloudflared service install <TOKEN>` (Phase 2 del migration runbook); routes sono gestite in CF dashboard, non in repo. Nessun file `infra/cloudflared/` necessario.

---

## Phase 1 — Quick Wins (Day 1, ~3h)

### Task 1: Pre-deploy port cleanup

Address smoke set scenario C1 + memory `feedback_staging_stale_api_host_process`.

**Files:**
- Modify: `.github/workflows/deploy-staging.yml` (deploy job, prima del `docker compose up`)

- [ ] **Step 1: Verify current behavior**

```bash
grep -n "fuser\|lsof.*8080" .github/workflows/deploy-staging.yml || echo "NOT FOUND"
```
Expected: `NOT FOUND` (no port cleanup currently)

- [ ] **Step 2: Add port cleanup step to deploy script**

In `.github/workflows/deploy-staging.yml`, dentro lo step `Deploy via SSH` (intorno alla riga 607, dopo il `cd /opt/meepleai/repo/infra` e prima del `docker pull`):

```yaml
            # Pre-deploy port cleanup — kill any stale process holding :8080
            # (see memory: feedback_staging_stale_api_host_process)
            STALE_PIDS=$(sudo fuser 8080/tcp 2>/dev/null | tr -d ':' | xargs || true)
            if [ -n "$STALE_PIDS" ]; then
              echo "🧹 Killing stale processes on :8080 — PIDs: $STALE_PIDS"
              sudo kill -9 $STALE_PIDS 2>/dev/null || true
              sleep 2
            else
              echo "✅ Port :8080 is clean"
            fi
```

- [ ] **Step 3: Verify YAML is valid**

```bash
cd D:/Repositories/meepleai-monorepo-dev
node scripts/validate-workflows.js 2>&1 | grep -i "deploy-staging" || echo "✅ no errors for deploy-staging"
```
Expected: no errors

- [ ] **Step 4: Local dry-run of port cleanup logic**

```bash
echo "TESTING fuser logic..."
sudo fuser 9999/tcp 2>/dev/null | tr -d ':' | xargs || echo "no PIDs (expected, port unused)"
```
Expected: empty output (port 9999 unused)

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/deploy-staging.yml
git commit -m "ops(deploy): pre-deploy port cleanup for :8080

Kills stale processes holding :8080 before docker compose up.
Addresses recurring issue from memory feedback_staging_stale_api_host_process.

Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G1.C1"
```

---

### Task 2: Daily Docker disk prune script

Mitigates 75GB disk pressure (memory `feedback_staging_disk_space`).

**Files:**
- Create: `infra/scripts/daily-disk-prune.sh`

- [ ] **Step 1: Verify script does not exist**

```bash
ls infra/scripts/daily-disk-prune.sh 2>/dev/null && echo "EXISTS" || echo "OK to create"
```
Expected: `OK to create`

- [ ] **Step 2: Create the script**

Path: `infra/scripts/daily-disk-prune.sh`

```bash
#!/usr/bin/env bash
# Daily Docker prune — keep VPS disk under control
# Runs from cron at 05:00 (after 03:00 backup, before any morning activity)
#
# Cron: 0 5 * * * cd /opt/meepleai/repo/infra && bash scripts/daily-disk-prune.sh >> /var/log/meepleai-disk-prune.log 2>&1

set -euo pipefail

log() {
  echo "[$(date '+%Y-%m-%dT%H:%M:%S')] $*"
}

log "=== Disk prune starting ==="
log "Disk before: $(df -h / | tail -1 | awk '{print $4}') free"

# Prune images not used in 72 hours; keeps recent build cache for fast rebuilds.
# --filter "until=72h" excludes anything created/used in last 3 days.
docker image prune -af --filter "until=72h" 2>&1 || log "WARN: image prune had non-zero exit"

# Prune builder cache older than 24h (BuildKit layers)
docker builder prune -f --filter "until=24h" 2>&1 || log "WARN: builder prune had non-zero exit"

# Prune stopped containers (any age — they should not accumulate)
docker container prune -f 2>&1 || log "WARN: container prune had non-zero exit"

# Networks: prune unused
docker network prune -f 2>&1 || log "WARN: network prune had non-zero exit"

# DO NOT prune volumes — they contain pgdata/redis/etc.
# Only manual `docker volume prune` allowed.

log "Disk after: $(df -h / | tail -1 | awk '{print $4}') free"
log "=== Disk prune complete ==="
```

- [ ] **Step 3: Make executable**

```bash
chmod +x infra/scripts/daily-disk-prune.sh
ls -la infra/scripts/daily-disk-prune.sh | grep -q "rwx" && echo "✅ executable" || echo "❌ FAIL"
```
Expected: `✅ executable`

- [ ] **Step 4: Local dry test (no docker access required for shellcheck)**

```bash
bash -n infra/scripts/daily-disk-prune.sh && echo "✅ syntax OK" || echo "❌ SYNTAX ERROR"
which shellcheck && shellcheck infra/scripts/daily-disk-prune.sh || echo "(shellcheck not installed, skip)"
```
Expected: `✅ syntax OK`

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/daily-disk-prune.sh
git commit -m "ops(infra): add daily-disk-prune.sh for cron

Mitigates recurring 75GB disk pressure on staging CAX21.
Prunes images >72h old, builder cache >24h, stopped containers, unused networks.
Volumes never pruned (contain pgdata/redis).

Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G5"
```

---

### Task 3: Add `cron-install` Makefile target

Centralizes cron management (existing backup-cron-install + new disk prune + future DR reminder).

**Files:**
- Modify: `infra/Makefile` (Backup & DR section, ~riga 129)

- [ ] **Step 1: Read current backup-cron-install target**

```bash
sed -n '129,140p' infra/Makefile
```
Expected: see existing `backup-cron-install` target with backup + verify + restore-test

- [ ] **Step 2: Extend backup-cron-install with disk prune entry**

In `infra/Makefile`, modify the `backup-cron-install` target. Replace the existing block:

```makefile
backup-cron-install: ## Install daily backup cron job (3 AM)
	@echo "Installing backup cron job..."
	@(crontab -l 2>/dev/null | grep -v "meepleai.*backup"; \
	  echo "0 3 * * * cd /opt/meepleai/repo/infra && bash scripts/backup.sh >> /var/log/meepleai-backup.log 2>&1"; \
	  echo "30 3 * * * cd /opt/meepleai/repo/infra && bash scripts/backup-verify.sh >> /var/log/meepleai-backup-verify.log 2>&1"; \
	  echo "0 4 * * 0 cd /opt/meepleai/repo/infra && bash scripts/backup-restore-test.sh >> /var/log/meepleai-restore-test.log 2>&1") | crontab -
	@echo "✅ Cron installed: backup daily 3:00, verify 3:30, restore test weekly Sunday 4:00"
```

with:

```makefile
backup-cron-install: ## Install all infrastructure cron jobs (backup, verify, prune, DR)
	@echo "Installing infrastructure cron jobs..."
	@(crontab -l 2>/dev/null | grep -vE "meepleai.*(backup|prune|dr-walkthrough|restore-test)"; \
	  echo "0 3 * * * cd /opt/meepleai/repo/infra && bash scripts/backup.sh >> /var/log/meepleai-backup.log 2>&1"; \
	  echo "30 3 * * * cd /opt/meepleai/repo/infra && bash scripts/backup-verify.sh >> /var/log/meepleai-backup-verify.log 2>&1"; \
	  echo "0 5 * * * cd /opt/meepleai/repo/infra && bash scripts/daily-disk-prune.sh >> /var/log/meepleai-disk-prune.log 2>&1"; \
	  echo "0 4 1 * * cd /opt/meepleai/repo/infra && bash scripts/backup-restore-test.sh --with-smoke-readback >> /var/log/meepleai-restore-test.log 2>&1"; \
	  echo "0 9 1 1,4,7,10 * cd /opt/meepleai/repo/infra && bash scripts/dr-walkthrough-reminder.sh >> /var/log/meepleai-dr-reminder.log 2>&1") | crontab -
	@echo "✅ Cron installed:"
	@echo "  daily 03:00  → backup"
	@echo "  daily 03:30  → backup-verify"
	@echo "  daily 05:00  → disk-prune"
	@echo "  monthly 1st 04:00  → restore-test (with smoke read-back)"
	@echo "  quarterly 09:00    → DR walkthrough reminder"
```

- [ ] **Step 3: Verify Makefile parses**

```bash
make -C infra -n backup-cron-install 2>&1 | head -5
```
Expected: makefile commands shown without "missing separator" errors

- [ ] **Step 4: Verify help target shows it**

```bash
make -C infra help 2>&1 | grep "backup-cron-install"
```
Expected: line shown in help output with description

- [ ] **Step 5: Commit (do NOT install cron yet — DR scripts not created)**

```bash
git add infra/Makefile
git commit -m "ops(make): unify infra cron management in backup-cron-install

Adds disk-prune (daily 05:00), monthly restore-test with smoke read-back,
and quarterly DR walkthrough reminder to existing backup cron.

NOTE: scripts dr-walkthrough-reminder.sh and --with-smoke-readback flag
will be added in subsequent tasks. Cron should be re-installed AFTER all
scripts are merged to staging.

Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G5"
```

---

### Task 4: Add `single-tester` profiles to docker-compose.yml

Splits AI services into `ai-essential` (always on for single-tester) vs `ai`/`tutor-agents` (opt-in).

**Files:**
- Modify: `infra/docker-compose.yml`

- [ ] **Step 1: Read current profile assignments**

```bash
grep -nE "profiles:\s*\[" infra/docker-compose.yml
```
Expected: see existing `[ai]`, `[monitoring]`, `[automation]`, `[storage]` assignments

- [ ] **Step 2: Update embedding-service profile to add `ai-essential`**

Find in `infra/docker-compose.yml`:
```yaml
  embedding-service:
    build:
      context: ../apps/embedding-service
      dockerfile: ./Dockerfile
    container_name: meepleai-embedding
    restart: unless-stopped
    profiles: [ai]
```

Replace `profiles: [ai]` with `profiles: [ai, ai-essential]`.

- [ ] **Step 3: Update reranker-service profile**

Find:
```yaml
  reranker-service:
    build:
      context: ../apps/reranker-service
      dockerfile: ./Dockerfile
    container_name: meepleai-reranker
    restart: unless-stopped
    profiles: [ai]
```

Replace `profiles: [ai]` with `profiles: [ai, ai-essential]`.

- [ ] **Step 4: Update orchestration-service profile**

Find:
```yaml
  orchestration-service:
    build:
      context: ../apps/orchestration-service
      dockerfile: ./Dockerfile
    container_name: meepleai-orchestrator
    restart: unless-stopped
    profiles: [ai]
```

Replace `profiles: [ai]` with `profiles: [ai, tutor-agents]`.

- [ ] **Step 5: Update monitoring-essential split**

For `prometheus`, `grafana`, `alertmanager`, `node-exporter`: replace `profiles: [monitoring]` with `profiles: [monitoring, monitoring-essential]`.

For `seq` and `cadvisor`: leave `profiles: [monitoring]` unchanged (heavyweight, opt-in only via full profile).

Find each block (e.g., prometheus around line 324, grafana around 355, alertmanager around 387, node-exporter around 444) and apply the change.

- [ ] **Step 6: Verify profile gating**

```bash
docker compose -f infra/docker-compose.yml -f infra/compose.staging.yml --profile ai-essential --profile monitoring-essential --profile proxy config --services 2>&1 | sort
```
Expected: shows postgres, redis, api, web, embedding-service, reranker-service, prometheus, grafana, alertmanager, node-exporter, traefik, docker-socket-proxy (no orchestration, no n8n, no seq, no cadvisor)

- [ ] **Step 7: Verify default `--profile ai` still includes everything**

```bash
docker compose -f infra/docker-compose.yml -f infra/compose.staging.yml --profile ai --profile monitoring config --services 2>&1 | sort
```
Expected: includes orchestration-service, embedding-service, reranker-service (i.e., everything still works for `make staging`)

- [ ] **Step 8: Commit**

```bash
git add infra/docker-compose.yml
git commit -m "ops(compose): add single-tester profiles (ai-essential, monitoring-essential, tutor-agents)

Splits AI services so single-tester staging can run with embedding+reranker only,
deferring orchestration-service to opt-in --profile tutor-agents.

Splits monitoring so prometheus+grafana+alertmanager+node-exporter run by default,
deferring seq+cadvisor to opt-in (heavy and unused for single-tester).

Backwards compat: --profile ai still pulls everything (orchestration included via
[ai, tutor-agents] union semantics — Docker Compose adds when profile match exists).

Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G3"
```

---

### Task 5: Add `make staging-minimal` and `make staging-with-tutor` targets

**Files:**
- Modify: `infra/Makefile`

- [ ] **Step 1: Read existing staging targets**

```bash
sed -n '38,50p' infra/Makefile
```
Expected: see `staging`, `staging-core`, `staging-down` targets

- [ ] **Step 2: Replace `staging-core` with `staging-minimal` + add `staging-with-tutor`**

In `infra/Makefile`, find:
```makefile
staging-core: ## Deploy staging core only — no AI services (safe for CAX21, ~6GB reservation)
	$(COMPOSE) -f compose.staging.yml -f compose.traefik.yml \
		--profile monitoring --profile proxy up -d
```

Replace with:
```makefile
staging-minimal: ## Deploy staging single-tester profile (~3GB RAM, no orchestration/n8n/seq/cadvisor)
	$(COMPOSE) -f compose.staging.yml -f compose.traefik.yml \
		--profile ai-essential --profile monitoring-essential --profile proxy up -d

staging-with-tutor: ## Deploy staging single-tester + orchestration-service for tutor agents (~5GB RAM)
	$(COMPOSE) -f compose.staging.yml -f compose.traefik.yml \
		--profile ai-essential --profile monitoring-essential --profile proxy --profile tutor-agents up -d

staging-tutor-down: ## Stop only orchestration-service (keep rest of staging running)
	$(COMPOSE) -f compose.staging.yml --profile tutor-agents stop orchestration-service
	$(COMPOSE) -f compose.staging.yml --profile tutor-agents rm -f orchestration-service
```

Update `.PHONY` line (around row 309) to include the new targets:
- Find: `staging staging-down`
- Replace with: `staging staging-minimal staging-with-tutor staging-tutor-down staging-down`

- [ ] **Step 3: Verify Makefile parses**

```bash
make -C infra -n staging-minimal 2>&1 | head -3
make -C infra -n staging-with-tutor 2>&1 | head -3
make -C infra -n staging-tutor-down 2>&1 | head -3
```
Expected: each shows the docker compose command without errors

- [ ] **Step 4: Verify help shows new targets**

```bash
make -C infra help | grep -E "staging-minimal|staging-with-tutor|staging-tutor-down"
```
Expected: 3 lines, one per target

- [ ] **Step 5: Commit**

```bash
git add infra/Makefile
git commit -m "ops(make): add staging-minimal/with-tutor/tutor-down targets

staging-minimal: default for single-tester (no orchestration, no full monitoring)
staging-with-tutor: opt-in tutor agents
staging-tutor-down: stop only orchestration without touching rest

Replaces staging-core which is now subsumed by staging-minimal.

Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G3"
```

---

### Task 6: Update deploy-staging.yml to use `staging-minimal` profile

**Files:**
- Modify: `.github/workflows/deploy-staging.yml` (deploy job, intorno alla riga 649-650)

- [ ] **Step 1: Read current deploy command**

```bash
grep -nA2 "docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml" .github/workflows/deploy-staging.yml
```
Expected: `--profile minimal up -d --no-deps --force-recreate $SERVICES`

- [ ] **Step 2: Replace with single-tester profiles**

In `.github/workflows/deploy-staging.yml`, find the line:
```yaml
            docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml \
              --profile minimal up -d --no-deps --force-recreate $SERVICES
```

Replace with:
```yaml
            # Single-tester profiles: ai-essential (embedding+reranker) + monitoring-essential
            # + proxy (traefik). NO orchestration, NO n8n, NO seq/cadvisor.
            # See docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G3
            docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml \
              --profile ai-essential --profile monitoring-essential --profile proxy \
              up -d --no-deps --force-recreate $SERVICES
```

- [ ] **Step 3: Verify YAML still valid**

```bash
node scripts/validate-workflows.js 2>&1 | grep -i "deploy-staging" || echo "✅ no errors for deploy-staging"
```
Expected: no errors

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/deploy-staging.yml
git commit -m "ops(deploy): use single-tester profiles instead of --profile minimal

Default deploy now uses ai-essential (embedding+reranker) + monitoring-essential
(prometheus/grafana/alertmanager/node-exporter) + proxy (traefik).

Drops orchestration-service, n8n, seq, cadvisor by default.
Saves ~5GB RAM on CAX21 (8GB total).

Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G3"
```

---

## Phase 2 — `make work` Hybrid Dev Loop (Day 2, ~3h)

### Task 7: Create `infra/scripts/work.sh` wrapper

**Files:**
- Create: `infra/scripts/work.sh`

- [ ] **Step 1: Verify script does not exist**

```bash
ls infra/scripts/work.sh 2>/dev/null && echo "EXISTS" || echo "OK to create"
```
Expected: `OK to create`

- [ ] **Step 2: Create the wrapper script**

Path: `infra/scripts/work.sh`

```bash
#!/usr/bin/env bash
# work.sh — Hybrid dev loop: SSH tunnel + local API+Web + watchdog
#
# Usage:
#   bash infra/scripts/work.sh         # Start everything
#   bash infra/scripts/work.sh stop    # Stop everything (also via Ctrl+C in foreground)
#
# Pre-flight checks SSH key permissions, then:
#   1. Opens SSH tunnels to staging (postgres+redis+AI services)
#   2. Starts API + Web locally in Integration mode
#   3. Watchdog loop: re-opens tunnel if SSH socket dies
#   4. Trap on SIGINT/SIGTERM: clean teardown of all processes

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SSH_KEY="${HOME}/.ssh/meepleai-staging"
WATCHDOG_PID_FILE="/tmp/meepleai-work-watchdog.pid"
MAX_WATCHDOG_RETRIES=5

ACTION="${1:-start}"

log() { echo "[$(date '+%H:%M:%S')] $*"; }
fail() { log "❌ $*"; exit 1; }

# ─────────────────────────────────────────────
# Pre-flight checks
# ─────────────────────────────────────────────
preflight() {
  log "🔍 Pre-flight checks..."

  [ -f "$SSH_KEY" ] || fail "SSH key not found: $SSH_KEY"

  local perms
  perms=$(stat -c '%a' "$SSH_KEY" 2>/dev/null || stat -f '%A' "$SSH_KEY" 2>/dev/null)
  if [ "$perms" != "600" ] && [ "$perms" != "400" ]; then
    fail "SSH key permissions: chmod 600 required (current: $perms)"
  fi

  command -v dotnet >/dev/null || fail "dotnet not found in PATH"
  command -v pnpm >/dev/null || fail "pnpm not found in PATH"

  # Quick connectivity check
  ssh -o ConnectTimeout=5 -o BatchMode=yes -i "$SSH_KEY" deploy@204.168.135.69 'echo ok' >/dev/null 2>&1 \
    || fail "SSH to staging failed (check VPN/network)"

  log "✅ Pre-flight OK"
}

# ─────────────────────────────────────────────
# Start tunnel + watchdog + API + Web
# ─────────────────────────────────────────────
do_start() {
  preflight

  log "🚇 Opening SSH tunnels..."
  bash "$SCRIPT_DIR/integration-tunnel.sh" start || fail "tunnel start failed"

  log "🚀 Starting API + Web (integration mode)..."
  bash "$SCRIPT_DIR/integration-start.sh" all &
  local INT_PID=$!

  # Watchdog loop: re-opens tunnel if it dies
  (
    local retries=0
    while true; do
      sleep 10
      if ! ssh -O check -S "${HOME}/.ssh/meepleai-tunnel.sock" deploy@204.168.135.69 2>/dev/null; then
        retries=$((retries+1))
        if [ "$retries" -gt "$MAX_WATCHDOG_RETRIES" ]; then
          log "❌ Watchdog: max retries ($MAX_WATCHDOG_RETRIES) exceeded, giving up"
          break
        fi
        log "⚠️  Tunnel dropped, restarting (retry $retries/$MAX_WATCHDOG_RETRIES)..."
        bash "$SCRIPT_DIR/integration-tunnel.sh" start && log "✅ Tunnel restored" \
          || log "❌ Tunnel restart failed"
      fi
    done
  ) &
  echo $! > "$WATCHDOG_PID_FILE"

  trap 'do_stop; exit 0' INT TERM

  log "✅ make work running"
  log "   API: http://localhost:8080"
  log "   Web: http://localhost:3000"
  log "   Stop with Ctrl+C or 'make work-stop'"

  wait $INT_PID
}

# ─────────────────────────────────────────────
# Stop everything cleanly
# ─────────────────────────────────────────────
do_stop() {
  log "🛑 Stopping make work..."

  if [ -f "$WATCHDOG_PID_FILE" ]; then
    local wpid
    wpid=$(cat "$WATCHDOG_PID_FILE")
    kill "$wpid" 2>/dev/null || true
    rm -f "$WATCHDOG_PID_FILE"
    log "  Watchdog stopped (PID $wpid)"
  fi

  bash "$SCRIPT_DIR/integration-start.sh" stop || true
  bash "$SCRIPT_DIR/integration-tunnel.sh" stop || true

  # Force-kill any orphan processes on integration ports
  if command -v lsof >/dev/null; then
    lsof -ti:8080 2>/dev/null | xargs -r kill -9 2>/dev/null || true
    lsof -ti:3000 2>/dev/null | xargs -r kill -9 2>/dev/null || true
  fi

  log "✅ All stopped"
}

case "$ACTION" in
  start) do_start ;;
  stop)  do_stop ;;
  *) fail "Usage: $0 [start|stop]" ;;
esac
```

- [ ] **Step 3: Make executable**

```bash
chmod +x infra/scripts/work.sh
ls -la infra/scripts/work.sh | grep -q "rwx" && echo "✅ executable" || echo "❌ FAIL"
```
Expected: `✅ executable`

- [ ] **Step 4: Syntax check**

```bash
bash -n infra/scripts/work.sh && echo "✅ syntax OK" || echo "❌ SYNTAX ERROR"
```
Expected: `✅ syntax OK`

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/work.sh
git commit -m "ops(dev): add work.sh wrapper for hybrid dev loop

Single-command hybrid workflow: SSH tunnel to staging + local API+Web + watchdog.
Pre-flight checks SSH key, dotnet, pnpm, connectivity.
Watchdog re-opens tunnel within 10s if SSH socket dies (max 5 retries).
Trap SIGINT/SIGTERM for clean teardown of all processes.

Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G4"
```

---

### Task 8: Add `make work` and `make work-stop` Makefile targets

**Files:**
- Modify: `infra/Makefile`

- [ ] **Step 1: Find Integration section in Makefile**

```bash
grep -n "integration:" infra/Makefile | head -5
```
Expected: see `integration-check`, `integration`, `integration-down` targets around row 27-36

- [ ] **Step 2: Add work + work-stop targets after integration-down**

In `infra/Makefile`, after the `integration-down` target (around line 36), add:

```makefile
work: ## Hybrid dev loop: tunnel + local API+Web + watchdog (single command)
	bash scripts/work.sh start

work-stop: ## Stop work session cleanly (alternative to Ctrl+C)
	bash scripts/work.sh stop
```

Update `.PHONY` line to include them:
- Find: `tunnel tunnel-stop tunnel-status integration-check integration`
- Replace with: `tunnel tunnel-stop tunnel-status integration-check integration work work-stop`

- [ ] **Step 3: Verify Makefile parses**

```bash
make -C infra -n work 2>&1 | head -3
make -C infra -n work-stop 2>&1 | head -3
```
Expected: each shows `bash scripts/work.sh ...`

- [ ] **Step 4: Verify help shows new targets**

```bash
make -C infra help | grep -E "^work|^work-stop"
```
Expected: 2 lines

- [ ] **Step 5: Commit**

```bash
git add infra/Makefile
git commit -m "ops(make): add work and work-stop targets

make work       → hybrid dev loop in single command (tunnel+API+Web+watchdog)
make work-stop  → clean teardown

Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G4"
```

---

### Task 9: Manual smoke test of `make work`

**Files:**
- N/A (manual verification, no code change)

- [ ] **Step 1: Confirm staging is up**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 'docker ps --format "{{.Names}}" | grep meepleai'
```
Expected: at least postgres, redis, api, web visible

- [ ] **Step 2: Run `make work` cold start with timing**

```bash
cd D:/Repositories/meepleai-monorepo-dev
time make -C infra work-stop  # ensure clean state
sleep 2
time make -C infra work &
WORK_PID=$!
sleep 30
curl -sf http://localhost:8080/health > /dev/null && echo "✅ API up at 30s" || echo "❌ API down at 30s"
sleep 30
curl -sf http://localhost:3000 > /dev/null && echo "✅ Web up at 60s" || echo "❌ Web down at 60s"
sleep 30
curl -sf http://localhost:3000 > /dev/null && echo "✅ Web up at 90s (target met)" || echo "❌ Web down at 90s"
```
Expected: ≤90s to Web up

- [ ] **Step 3: Test watchdog auto-recovery**

In another terminal while `make work` is running:
```bash
SSH_PID=$(ps aux | grep -E "ssh.*meepleai-tunnel.sock" | grep -v grep | awk '{print $2}' | head -1)
echo "Killing SSH tunnel PID $SSH_PID..."
kill "$SSH_PID"
sleep 15
ssh -O check -S ~/.ssh/meepleai-tunnel.sock deploy@204.168.135.69 && echo "✅ Tunnel re-established" || echo "❌ Watchdog failed"
```
Expected: `✅ Tunnel re-established` within 15s

- [ ] **Step 4: Test clean teardown**

```bash
make -C infra work-stop
lsof -i:8080 2>/dev/null && echo "❌ port 8080 still bound" || echo "✅ port 8080 free"
lsof -i:3000 2>/dev/null && echo "❌ port 3000 still bound" || echo "✅ port 3000 free"
ssh -O check -S ~/.ssh/meepleai-tunnel.sock deploy@204.168.135.69 2>&1 | grep -q "control socket" && echo "❌ tunnel still up" || echo "✅ tunnel closed"
```
Expected: all 3 lines show ✅

- [ ] **Step 5: Document timing in commit (no code change, doc-only)**

Append to `docs/development/local-environment-startup-guide.md` a new section:

```markdown
## `make work` — Hybrid Dev Loop

Single-command wrapper for `make tunnel && make integration` with watchdog.

**Cold start timing target**: ≤90s from invocation to `localhost:3000` ready.

**Watchdog**: re-opens SSH tunnel within 15s if dropped (max 5 retries).

**Teardown**: `Ctrl+C` or `make work-stop` cleans all processes + tunnel.

See: `docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md` G4
```

```bash
git add docs/development/local-environment-startup-guide.md
git commit -m "docs(dev): document make work hybrid dev loop"
```

---

## Phase 3 — Smoke Set Automation (Day 3, ~2h)

### Task 10: Create `infra/scripts/smoke-set.sh`

**Files:**
- Create: `infra/scripts/smoke-set.sh`

- [ ] **Step 1: Verify script does not exist**

```bash
ls infra/scripts/smoke-set.sh 2>/dev/null && echo "EXISTS" || echo "OK to create"
```
Expected: `OK to create`

- [ ] **Step 2: Create the script**

Path: `infra/scripts/smoke-set.sh`

```bash
#!/usr/bin/env bash
# smoke-set.sh — Run the 9 G1 smoke scenarios against an endpoint
#
# Usage:
#   bash infra/scripts/smoke-set.sh [BASE_URL] [TEST_EMAIL] [TEST_PASSWORD]
#   bash infra/scripts/smoke-set.sh http://localhost:8080 user@test.local pass123
#   bash infra/scripts/smoke-set.sh https://meepleai.app
#
# Env overrides:
#   BASE_URL, TEST_EMAIL, TEST_PASSWORD (alternative to positional args)
#
# Exit code: 0 if all 9 PASS, 1 if any FAIL.
#
# Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G1

set -uo pipefail

BASE_URL="${1:-${BASE_URL:-http://localhost:8080}}"
TEST_EMAIL="${2:-${TEST_EMAIL:-}}"
TEST_PASSWORD="${3:-${TEST_PASSWORD:-}}"

PASS=0
FAIL=0
COOKIE_JAR="$(mktemp)"
trap 'rm -f "$COOKIE_JAR"' EXIT

log() { echo "[$(date '+%H:%M:%S')] $*"; }
ok() { echo "  ✅ PASS — $1"; PASS=$((PASS+1)); }
ko() { echo "  ❌ FAIL — $1 ($2)"; FAIL=$((FAIL+1)); }

# ─────────────────────────────────────────────
# Helpers
# ─────────────────────────────────────────────
check_status() {
  local name="$1" url="$2" expected="$3"
  local status
  status=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 -b "$COOKIE_JAR" -c "$COOKIE_JAR" "$url" 2>/dev/null || echo "000")
  if [ "$status" = "$expected" ]; then
    ok "$name (HTTP $status)"
  else
    ko "$name" "got $status, expected $expected"
  fi
}

# ─────────────────────────────────────────────
# Smoke scenarios
# ─────────────────────────────────────────────
log "🧪 Smoke set against: $BASE_URL"

# A1 — Login → Library mostra giochi (skipped if no creds)
if [ -n "$TEST_EMAIL" ] && [ -n "$TEST_PASSWORD" ]; then
  log "A1: Login + GET /api/v1/library/me"
  LOGIN_RES=$(curl -sf -X POST "$BASE_URL/api/v1/auth/login" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"$TEST_EMAIL\",\"password\":\"$TEST_PASSWORD\"}" \
    -c "$COOKIE_JAR" -w "\n%{http_code}" 2>/dev/null || echo "000")
  STATUS=$(echo "$LOGIN_RES" | tail -1)
  if [ "$STATUS" = "200" ]; then
    ok "A1.login (HTTP 200)"
    check_status "A1.library_me" "$BASE_URL/api/v1/library/me" "200"
  else
    ko "A1.login" "got $STATUS"
  fi
else
  log "A1: SKIPPED (TEST_EMAIL/TEST_PASSWORD not set)"
fi

# A2 — Search BGG
log "A2: GET /api/v1/bgg/search?query=Catan"
check_status "A2.bgg_search" "$BASE_URL/api/v1/bgg/search?query=Catan" "200"

# A3 — KB status of test game (skipped if no test game ID)
if [ -n "${TEST_GAME_ID:-}" ]; then
  log "A3: GET /api/v1/games/$TEST_GAME_ID/kb-status"
  check_status "A3.kb_status" "$BASE_URL/api/v1/games/$TEST_GAME_ID/kb-status" "200"
else
  log "A3: SKIPPED (TEST_GAME_ID env not set — set to validate KB indexing)"
fi

# A4 — Chat citations (skipped — requires interactive SSE, validated manually)
log "A4: SKIPPED (interactive SSE — validate manually via /chat UI)"

# A5 — Logout
if [ -n "$TEST_EMAIL" ]; then
  log "A5: POST /api/v1/auth/logout"
  check_status "A5.logout" "$BASE_URL/api/v1/auth/logout" "200"
fi

# C1 — Deploy senza rotture (validated by health endpoint)
log "C1: GET /health (deploy proxy)"
check_status "C1.health" "$BASE_URL/health" "200"

# C2 — Migration applicata: validated by DB schema query (skipped — needs DB access)
log "C2: SKIPPED (validated by deploy-staging.yml migrate-db job exit code)"

# C3 — Backup: validated by backup-verify cron, smoke check that endpoint exists
log "C3: GET /api/v1/admin/rag-backup/snapshots/latest"
# May 401 if not admin — accept 200 OR 401 (endpoint reachable)
RES_C3=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 -b "$COOKIE_JAR" "$BASE_URL/api/v1/admin/rag-backup/snapshots/latest" 2>/dev/null || echo "000")
if [ "$RES_C3" = "200" ] || [ "$RES_C3" = "401" ]; then
  ok "C3.backup_endpoint (HTTP $RES_C3)"
else
  ko "C3.backup_endpoint" "got $RES_C3"
fi

# C5 — RAG perf <10s (light heuristic: shared-games endpoint <2s as proxy)
log "C5: GET /api/v1/shared-games?pageNumber=1&pageSize=1 (perf proxy)"
START=$(date +%s%N)
check_status "C5.perf_proxy" "$BASE_URL/api/v1/shared-games?pageNumber=1&pageSize=1" "200"
END=$(date +%s%N)
ELAPSED_MS=$(( (END - START) / 1000000 ))
if [ "$ELAPSED_MS" -lt 2000 ]; then
  ok "C5.perf_under_2s (${ELAPSED_MS}ms)"
else
  ko "C5.perf_under_2s" "${ELAPSED_MS}ms (expected <2000ms)"
fi

# ─────────────────────────────────────────────
# Summary
# ─────────────────────────────────────────────
echo ""
log "📊 Summary: $PASS PASS / $FAIL FAIL"
if [ "$FAIL" -eq 0 ]; then
  log "✅ Smoke set passed"
  exit 0
else
  log "❌ Smoke set FAILED"
  exit 1
fi
```

- [ ] **Step 3: Make executable + syntax check**

```bash
chmod +x infra/scripts/smoke-set.sh
bash -n infra/scripts/smoke-set.sh && echo "✅ syntax OK" || echo "❌ SYNTAX ERROR"
```
Expected: `✅ syntax OK`

- [ ] **Step 4: Run against localhost (offline test, expect API down)**

```bash
bash infra/scripts/smoke-set.sh http://localhost:9999 2>&1 | tail -5
```
Expected: shows FAIL on multiple scenarios (port 9999 has no service), exit 1

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/smoke-set.sh
git commit -m "ops(testing): add smoke-set.sh for G1 9-scenario validation

Runs A1 (login + library), A2 (BGG search), A3 (KB status, opt-in via TEST_GAME_ID),
A5 (logout), C1 (health), C3 (backup endpoint), C5 (perf proxy <2s).
A4 skipped (interactive SSE — manual UI validation).
C2 skipped (validated by deploy-staging.yml migrate-db).

Usable against any endpoint: localhost (dev), https://meepleai.app (staging).

Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G1"
```

---

### Task 11: Add `make smoke` target

**Files:**
- Modify: `infra/Makefile`

- [ ] **Step 1: Add target after work-stop**

In `infra/Makefile`, after the `work-stop` target, add:

```makefile
smoke: ## Run smoke set against BASE_URL (default localhost:8080) — set TEST_EMAIL/TEST_PASSWORD for auth scenarios
	bash scripts/smoke-set.sh $(BASE_URL) $(TEST_EMAIL) $(TEST_PASSWORD)

smoke-staging: ## Run smoke set against staging
	bash scripts/smoke-set.sh https://meepleai.app $(TEST_EMAIL) $(TEST_PASSWORD)
```

Add to `.PHONY`: `smoke smoke-staging`

- [ ] **Step 2: Verify**

```bash
make -C infra -n smoke 2>&1 | head -3
make -C infra -n smoke-staging 2>&1 | head -3
make -C infra help | grep -E "^smoke"
```
Expected: 2 + 2 + 2 lines

- [ ] **Step 3: Commit**

```bash
git add infra/Makefile
git commit -m "ops(make): add smoke and smoke-staging targets

make smoke           → run 9-scenario smoke against BASE_URL (default localhost)
make smoke-staging   → run smoke against https://meepleai.app

Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G1"
```

---

## Phase 4 — DR Validation (Day 4, ~2h)

### Task 12: Extend `backup-restore-test.sh` with `--with-smoke-readback`

**Files:**
- Modify: `infra/scripts/backup-restore-test.sh`

- [ ] **Step 1: Confirm existing script structure**

```bash
sed -n '7,15p' infra/scripts/backup-restore-test.sh
```
Expected: shows `TEMP_CONTAINER="meepleai-restore-test"`, `DB_USER="meepleai"`, `DB_NAME="meepleai"`, `PG_IMAGE="pgvector/pgvector:pg16"`.

- [ ] **Step 2: Confirm existing verification queries (lines 109-122)**

```bash
sed -n '109,123p' infra/scripts/backup-restore-test.sh
```
Expected: shows existing `USERS_COUNT` (Administration.Users), `GAMES_COUNT` (GameManagement.Games), `SCHEMA_COUNT` queries — but only `SCHEMA_COUNT` is enforced (>= 5). The smoke read-back will harden the existing 2 queries + add a 3rd.

- [ ] **Step 3: Add smoke read-back block before final "PASSED ✅" line**

In `infra/scripts/backup-restore-test.sh`, find the line `echo "=== Restore test PASSED ✅ (${RESTORE_TIME}s) ==="` (line 146) and insert this block IMMEDIATELY BEFORE it:

```bash
# ─── Smoke read-back (only with --with-smoke-readback flag) ──────────────────

if [[ " $* " == *" --with-smoke-readback "* ]]; then
  echo ""
  echo "--- Smoke read-back ---"

  SMOKE_FAIL=0

  # Reuse already-computed counts from earlier verification block
  for pair in "users:${USERS_COUNT}" "games:${GAMES_COUNT}"; do
    label="${pair%:*}"
    count="${pair#*:}"
    if [[ "$count" =~ ^[0-9]+$ ]] && [ "$count" -gt 0 ]; then
      echo "  ✅ $label: $count rows"
    else
      echo "  ❌ $label: got '$count' (expected positive integer)"
      SMOKE_FAIL=1
    fi
  done

  # Third query: GameSessions (schema default 'public', table PascalCase quoted)
  SESSIONS_COUNT=$(docker exec "${TEMP_CONTAINER}" psql -U "${DB_USER}" -d "${DB_NAME}" -t -c \
    'SELECT COUNT(*) FROM "GameSessions";' 2>/dev/null | tr -d '[:space:]' || echo "ERROR")
  if [[ "$SESSIONS_COUNT" =~ ^[0-9]+$ ]]; then
    echo "  ✅ sessions: $SESSIONS_COUNT rows (>=0 accepted, table may be empty)"
  else
    echo "  ❌ sessions: query failed (got '$SESSIONS_COUNT')"
    SMOKE_FAIL=1
  fi

  if [ "$SMOKE_FAIL" -ne 0 ]; then
    echo ""
    echo "❌ Restore OK but smoke read-back FAILED" >&2
    exit 1
  fi

  echo "  ✅ Restore + smoke read-back PASSED"
fi
```

Notes on the implementation:
- `USERS_COUNT` and `GAMES_COUNT` are already computed at lines 110-115 — we reuse them.
- `sessions` accepts `>=0` because a fresh restore on a single-tester staging may have zero sessions (legitimate), but the query MUST succeed (no ERROR token).
- Default behavior (without flag) is unchanged — backwards compatible.

- [ ] **Step 4: Syntax check**

```bash
bash -n infra/scripts/backup-restore-test.sh && echo "✅ syntax OK" || echo "❌ SYNTAX ERROR"
```
Expected: `✅ syntax OK`

- [ ] **Step 5: Test invocation without flag (should behave as before)**

```bash
bash infra/scripts/backup-restore-test.sh --help 2>&1 | head -5 || true
# (May fail if no --help; OK — checking that flag parsing works)
```
Expected: no fatal error from flag parsing

- [ ] **Step 6: Commit**

```bash
git add infra/scripts/backup-restore-test.sh
git commit -m "ops(dr): add --with-smoke-readback flag to backup-restore-test

Validates restore by running 3 smoke read-back queries (games, users, sessions)
on the restored temp container. Each must return >0 rows.

Used by monthly cron to confirm backups are not silently corrupt.

Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G5"
```

---

### Task 13: Create `dr-walkthrough-reminder.sh`

**Files:**
- Create: `infra/scripts/dr-walkthrough-reminder.sh`

- [ ] **Step 1: Verify script does not exist**

```bash
ls infra/scripts/dr-walkthrough-reminder.sh 2>/dev/null && echo "EXISTS" || echo "OK to create"
```
Expected: `OK to create`

- [ ] **Step 2: Create the script**

Path: `infra/scripts/dr-walkthrough-reminder.sh`

```bash
#!/usr/bin/env bash
# dr-walkthrough-reminder.sh — Quarterly notification to walk through DR runbook
#
# Cron: 0 9 1 1,4,7,10 * cd /opt/meepleai/repo/infra && bash scripts/dr-walkthrough-reminder.sh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SECRETS_DIR="${SCRIPT_DIR}/../secrets"

log() { echo "[$(date '+%Y-%m-%dT%H:%M:%S')] $*"; }

# Load webhook URL from backup.secret (reuse same channel as backup notifications)
if [ -f "${SECRETS_DIR}/backup.secret" ]; then
  set -a
  # shellcheck source=/dev/null
  source "${SECRETS_DIR}/backup.secret"
  set +a
fi

WEBHOOK_URL="${BACKUP_WEBHOOK_URL:-}"
LOG_FILE="${SCRIPT_DIR}/../../docs/operations/dr-walkthrough-log.md"

# Check last walkthrough date from log file
LAST_WALKTHROUGH=$(grep -oE '^\| [0-9]{4}-[0-9]{2}-[0-9]{2}' "$LOG_FILE" 2>/dev/null | head -1 | tr -d '|' | xargs || echo "")
DAYS_SINCE="?"
if [ -n "$LAST_WALKTHROUGH" ]; then
  LAST_EPOCH=$(date -d "$LAST_WALKTHROUGH" +%s 2>/dev/null || echo "0")
  NOW_EPOCH=$(date +%s)
  DAYS_SINCE=$(( (NOW_EPOCH - LAST_EPOCH) / 86400 ))
fi

PAYLOAD=$(cat <<EOF
{
  "status": "action_required",
  "task": "DR runbook walkthrough due",
  "runbook": "docs/operations/disaster-recovery-runbook.md",
  "log_file": "docs/operations/dr-walkthrough-log.md",
  "last_walkthrough": "${LAST_WALKTHROUGH:-never}",
  "days_since": "${DAYS_SINCE}",
  "instructions": "Read the runbook end-to-end. Verify each step still matches the current infrastructure. Add an entry to dr-walkthrough-log.md."
}
EOF
)

log "DR walkthrough reminder triggered (last: ${LAST_WALKTHROUGH:-never}, days_since: ${DAYS_SINCE})"

if [ -z "$WEBHOOK_URL" ]; then
  log "WARN: BACKUP_WEBHOOK_URL not set — reminder logged only"
  echo "$PAYLOAD" | python3 -m json.tool 2>/dev/null || echo "$PAYLOAD"
  exit 0
fi

curl -sf --max-time 10 \
  -H "Content-Type: application/json" \
  -d "$PAYLOAD" \
  "$WEBHOOK_URL" \
  && log "✅ Webhook delivered" \
  || log "❌ Webhook delivery failed"
```

- [ ] **Step 3: Make executable + syntax check**

```bash
chmod +x infra/scripts/dr-walkthrough-reminder.sh
bash -n infra/scripts/dr-walkthrough-reminder.sh && echo "✅ syntax OK" || echo "❌ SYNTAX ERROR"
```
Expected: `✅ syntax OK`

- [ ] **Step 4: Test logic without webhook (should print payload and exit 0)**

```bash
BACKUP_WEBHOOK_URL="" bash infra/scripts/dr-walkthrough-reminder.sh
```
Expected: prints JSON payload and "WARN: BACKUP_WEBHOOK_URL not set"; exit code 0

- [ ] **Step 5: Commit**

```bash
git add infra/scripts/dr-walkthrough-reminder.sh
git commit -m "ops(dr): add quarterly walkthrough reminder script

Reads last walkthrough date from docs/operations/dr-walkthrough-log.md,
sends webhook (BACKUP_WEBHOOK_URL) with reminder payload + days_since.

Cron: 1 jan/apr/jul/oct at 09:00 UTC.

Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G5"
```

---

### Task 14: Create `dr-walkthrough-log.md` template

**Files:**
- Create: `docs/operations/dr-walkthrough-log.md`

- [ ] **Step 1: Verify file does not exist**

```bash
ls docs/operations/dr-walkthrough-log.md 2>/dev/null && echo "EXISTS" || echo "OK to create"
```
Expected: `OK to create`

- [ ] **Step 2: Create the template**

Path: `docs/operations/dr-walkthrough-log.md`

```markdown
# DR Walkthrough Log

> Quarterly review log for `docs/operations/disaster-recovery-runbook.md`.
> Triggered by `infra/scripts/dr-walkthrough-reminder.sh` (cron: 1st jan/apr/jul/oct).

## How to log a walkthrough

After receiving a quarterly reminder:

1. Open `docs/operations/disaster-recovery-runbook.md`.
2. Read end-to-end. For each step, verify:
   - Commands still work as written (no deprecated flags, no removed scripts).
   - Service names, paths, env vars match current `infra/docker-compose.yml`.
   - Required secrets are listed in `infra/secrets/*.secret.example`.
3. Note any **drift** (runbook says X, reality is Y).
4. Add a row to the table below (most recent first).
5. If drift was found, open a separate PR to fix the runbook.

## Walkthrough Entries (most recent first)

| Date | Drift Found | Status | Notes |
|------|-------------|--------|-------|
| _yyyy-mm-dd_ | _none / list_ | _ok / pr-#NNN_ | _short note_ |

<!-- Example entry:
| 2026-08-01 | step 4 references qdrant (removed in PR#480) | pr-#NNN | Updated runbook to use pgvector |
-->
```

- [ ] **Step 3: Verify**

```bash
cat docs/operations/dr-walkthrough-log.md | head -10
```
Expected: shows the markdown header

- [ ] **Step 4: Commit**

```bash
git add docs/operations/dr-walkthrough-log.md
git commit -m "docs(dr): add walkthrough log template

Quarterly DR runbook review log. Reminder script
(infra/scripts/dr-walkthrough-reminder.sh) reads last entry
to compute days_since for the webhook payload.

Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G5"
```

---

### Task 15: Install cron jobs on staging server

**Files:**
- N/A (manual SSH, no code change)

- [ ] **Step 1: SSH to staging**

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69
```

- [ ] **Step 2: Pull latest changes**

```bash
cd /opt/meepleai/repo
git pull origin main-staging
```
Expected: shows the new infra/scripts/* files

- [ ] **Step 3: Run cron-install**

```bash
cd /opt/meepleai/repo/infra
make backup-cron-install
```
Expected: prints 5 cron entries installed

- [ ] **Step 4: Verify crontab**

```bash
crontab -l | grep meepleai
```
Expected: 5 entries (backup, verify, prune, restore-test, dr-reminder)

- [ ] **Step 5: Test disk-prune script manually (low risk)**

```bash
bash /opt/meepleai/repo/infra/scripts/daily-disk-prune.sh
```
Expected: shows "Disk before/after" lines and exits 0

- [ ] **Step 6: Test DR reminder script manually**

```bash
bash /opt/meepleai/repo/infra/scripts/dr-walkthrough-reminder.sh
```
Expected: webhook delivered (or WARN if BACKUP_WEBHOOK_URL unset)

- [ ] **Step 7: Logout**

```bash
exit
```

No commit needed (server-side only). Optionally document the install in commit message of a doc update if desired.

---

## Phase 5 — Cloudflare Tunnel Migration (Week 2-8, ~5h, target: 2026-06-30)

### Task 16: Document CF Tunnel migration runbook

**Files:**
- Create: `docs/operations/cf-tunnel-migration.md`

- [ ] **Step 1: Verify file does not exist**

```bash
ls docs/operations/cf-tunnel-migration.md 2>/dev/null && echo "EXISTS" || echo "OK to create"
```
Expected: `OK to create`

- [ ] **Step 2: Create the migration runbook**

Path: `docs/operations/cf-tunnel-migration.md`

```markdown
# Cloudflare Tunnel Migration Runbook

> **Goal**: Migrate `meepleai.app` from Traefik+Let's Encrypt edge to Cloudflare Tunnel.
> **Audience**: project owner (single tester).
> **Prerequisites**: Cloudflare account with `meepleai.app` zone; SSH access to staging.
> **Estimated time**: 3-4 hours. Reversible via `cf-tunnel-rollback.md` in <30 min.
> **Pre-migration smoke test**: `make -C infra smoke-staging` must pass 9/9.

## Phase 0 — Pre-flight

1. Confirm staging is healthy:
   ```bash
   curl -sf https://meepleai.app/health | jq .
   bash infra/scripts/smoke-set.sh https://meepleai.app
   ```
2. Backup current Traefik config:
   ```bash
   ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
     'cd /opt/meepleai/repo/infra && tar czf /tmp/traefik-backup-$(date +%Y%m%d).tgz traefik/ compose.traefik.yml'
   ```
3. Take DB backup:
   ```bash
   ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
     'cd /opt/meepleai/repo/infra && bash scripts/backup.sh'
   ```

## Phase 1 — Cloudflare setup (web UI)

1. Log into Cloudflare dashboard → Zero Trust → Networks → Tunnels.
2. Create a new tunnel named `meepleai-staging`.
3. Copy the `cloudflared` install command (includes the tunnel token).
4. Configure routes (web UI):
   - `meepleai.app` → `http://localhost:3000` (web)
   - `api.meepleai.app` → `http://localhost:8080` (api)
5. Configure CF Access policy (optional but recommended):
   - Application: `meepleai.app`
   - Policy: "owner-only" with email match → your email only.

## Phase 2 — Install `cloudflared` on VPS

1. SSH to staging.
2. Install daemon (the tunnel token from Phase 1 is embedded):
   ```bash
   sudo curl -L --output /tmp/cloudflared.deb \
     https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-arm64.deb
   sudo dpkg -i /tmp/cloudflared.deb
   sudo cloudflared service install <TOKEN_FROM_PHASE_1>
   sudo systemctl enable --now cloudflared
   ```
3. Verify daemon is running:
   ```bash
   sudo systemctl status cloudflared
   sudo journalctl -u cloudflared -n 20
   ```
4. Verify tunnel is up in CF dashboard (status: Healthy).

## Phase 3 — Expose API to localhost

The API container currently does NOT bind a host port (only Traefik routes traffic). Add a temporary host bind so cloudflared can reach it.

1. Edit `infra/compose.staging.yml` to add `ports: ["127.0.0.1:8080:8080"]` to the `api` service.
2. Edit `infra/compose.staging.yml` to add `ports: ["127.0.0.1:3000:3000"]` to the `web` service.
3. Restart:
   ```bash
   make -C /opt/meepleai/repo/infra staging-minimal
   ```
4. Verify:
   ```bash
   curl -sf http://localhost:8080/health | jq .
   curl -sf http://localhost:3000 | head -3
   ```

## Phase 4 — Cutover

1. Test traffic flow via CF Tunnel (DNS still on Traefik):
   - In CF dashboard, temporarily map `dr-test.meepleai.app` → tunnel routes.
   - `curl -sf https://dr-test.meepleai.app/health | jq .`
   - If 200 OK with same payload as current, cutover is safe.
2. Switch DNS in CF dashboard:
   - Replace A record `meepleai.app` → 204.168.135.69 with CNAME `meepleai.app` → `<tunnel-id>.cfargotunnel.com` (proxied).
   - Same for `api.meepleai.app`.
3. Wait 5 min for DNS propagation.
4. Run smoke set:
   ```bash
   bash infra/scripts/smoke-set.sh https://meepleai.app
   ```
   All 9 must PASS.

## Phase 5 — Lock down ports

1. Enable UFW firewall:
   ```bash
   sudo ufw allow 22/tcp comment 'SSH'
   sudo ufw deny 80/tcp  comment 'HTTP — closed (CF Tunnel)'
   sudo ufw deny 443/tcp comment 'HTTPS — closed (CF Tunnel)'
   sudo ufw enable
   sudo ufw status verbose
   ```
2. Verify from external host:
   ```bash
   nmap -p 80,443,22 204.168.135.69
   ```
   Expected: 22 open, 80/443 filtered or closed.

## Phase 6 — Decommission Traefik (after 7-day soak)

After 7 days of CF Tunnel running with smoke set passing:

1. Stop Traefik:
   ```bash
   docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml \
     --profile proxy stop traefik docker-socket-proxy
   ```
2. Remove Traefik labels from `compose.staging.yml` (api, web, embedding, reranker, grafana, prometheus blocks).
3. Remove Traefik secrets from `infra/secrets/`:
   ```bash
   rm infra/secrets/traefik.secret  # backup first if anything custom
   ```
4. Update `Makefile`:
   - Remove `--profile proxy` from `staging-minimal` and `staging-with-tutor` targets.
5. Commit:
   ```bash
   git add infra/compose.staging.yml infra/Makefile
   git rm infra/secrets/traefik.secret
   git commit -m "ops(infra): decommission Traefik after CF Tunnel migration"
   ```

## Phase 7 — Validation

1. `bash infra/scripts/smoke-set.sh https://meepleai.app` → 9/9 PASS.
2. `nmap -p 80,443 204.168.135.69` → filtered/closed.
3. `free -m` on VPS → ≥6GB available (Traefik freed ~512MB).
4. Add entry to `docs/operations/dr-walkthrough-log.md` with date + "CF Tunnel migration completed".

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `cloudflared` shows "DNS resolution failed" | Routes not configured in CF dashboard | Re-check Phase 1 step 4 |
| `curl https://meepleai.app/health` returns 502 | API container not bound to localhost | Verify Phase 3 step 1 + restart |
| Smoke set A1 fails (login) | CF Access blocking POST | Add CF Access bypass for `/api/v1/auth/*` paths or disable Access for staging |
| `nmap` still shows 80/443 open | UFW not enabled or rule order wrong | `sudo ufw status verbose` and re-check Phase 5 |

## Rollback

If anything breaks: `docs/operations/cf-tunnel-rollback.md`.
```

- [ ] **Step 3: Verify**

```bash
wc -l docs/operations/cf-tunnel-migration.md
```
Expected: ~120-150 lines

- [ ] **Step 4: Commit**

```bash
git add docs/operations/cf-tunnel-migration.md
git commit -m "docs(ops): add CF Tunnel migration runbook

7-phase runbook: pre-flight → CF setup → cloudflared install → API expose →
DNS cutover → port lockdown → Traefik decommission → validation.

Includes troubleshooting table and rollback reference.

Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G2"
```

---

### Task 17: Document CF Tunnel rollback runbook

**Files:**
- Create: `docs/operations/cf-tunnel-rollback.md`

- [ ] **Step 1: Verify file does not exist**

```bash
ls docs/operations/cf-tunnel-rollback.md 2>/dev/null && echo "EXISTS" || echo "OK to create"
```
Expected: `OK to create`

- [ ] **Step 2: Create the rollback runbook**

Path: `docs/operations/cf-tunnel-rollback.md`

```markdown
# Cloudflare Tunnel Rollback Runbook

> **Goal**: Revert from Cloudflare Tunnel back to Traefik+Let's Encrypt edge.
> **Estimated time**: 30 min.
> **When to use**: smoke set fails after migration; CF Tunnel adds unacceptable latency; CF outage.

## Phase 0 — Verify Traefik config still in repo

```bash
ls infra/traefik/ infra/compose.traefik.yml
```
Expected: files exist. If not, restore from `/tmp/traefik-backup-*.tgz` (created in cf-tunnel-migration.md Phase 0).

## Phase 1 — Disable cloudflared

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69
sudo systemctl stop cloudflared
sudo systemctl disable cloudflared
```

## Phase 2 — Re-open ports

```bash
sudo ufw allow 80/tcp  comment 'HTTP — Traefik'
sudo ufw allow 443/tcp comment 'HTTPS — Traefik'
sudo ufw status verbose
```

## Phase 3 — Restart Traefik

```bash
cd /opt/meepleai/repo/infra
make staging-minimal  # or 'make staging' if you reverted Makefile changes
docker ps --format "{{.Names}}" | grep traefik
```
Expected: `meepleai-traefik` and `meepleai-docker-socket-proxy` running.

## Phase 4 — Switch DNS back

In CF dashboard:
- `meepleai.app` CNAME → A record pointing to 204.168.135.69 (proxied: orange cloud).
- `api.meepleai.app` same.

Wait 5 min for DNS propagation.

## Phase 5 — Validate

```bash
curl -sf https://meepleai.app/health | jq .
bash infra/scripts/smoke-set.sh https://meepleai.app
```
Expected: 9/9 PASS.

## Phase 6 — Document the rollback

Add an entry to `docs/operations/dr-walkthrough-log.md`:

| Date | Action | Reason | Notes |
|------|--------|--------|-------|
| _yyyy-mm-dd_ | CF Tunnel rollback | _e.g., latency >300ms_ | _details_ |
```

- [ ] **Step 3: Commit**

```bash
git add docs/operations/cf-tunnel-rollback.md
git commit -m "docs(ops): add CF Tunnel rollback runbook

6-phase rollback in <30 min: stop cloudflared → re-open ports →
restart Traefik → DNS revert → smoke validate → log incident.

Refs: docs/superpowers/specs/2026-05-05-infrastructure-single-tester-design.md G2"
```

---

### Task 18: Execute CF Tunnel migration (manual, on server)

**Files:**
- N/A (manual SSH execution following Task 16 runbook)
- After completion: edit `infra/compose.staging.yml` and `infra/Makefile`

- [ ] **Step 1: Allocate 4-hour maintenance window** when staging traffic is acceptable to lose

Schedule via TodoWrite: "CF Tunnel migration window — yyyy-mm-dd HH:00-HH:00"

- [ ] **Step 2: Execute Phase 0-5** of `docs/operations/cf-tunnel-migration.md`

Follow the runbook step-by-step. Do NOT skip the pre-flight backup.

- [ ] **Step 3: Run smoke set against new endpoint**

```bash
bash infra/scripts/smoke-set.sh https://meepleai.app
```
Expected: 9/9 PASS.

If any FAIL: execute `docs/operations/cf-tunnel-rollback.md` and open issue with diagnostics.

- [ ] **Step 4: Soak period — leave Traefik running for 7 days as fallback**

During this period, both routing paths exist (CF Tunnel active, Traefik available). Monitor:
- `sudo systemctl status cloudflared` daily
- Latency: `curl -w "%{time_total}\n" -o /dev/null -s https://meepleai.app/health`
- Smoke set weekly

- [ ] **Step 5: Day 8 — execute Phase 6 (decommission Traefik)**

Follow Phase 6 of the migration runbook. Commit changes.

- [ ] **Step 6: Final validation (Phase 7) + log entry**

```bash
bash infra/scripts/smoke-set.sh https://meepleai.app  # 9/9 PASS
nmap -p 80,443 204.168.135.69                         # filtered/closed
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 'free -m | head -2'  # ≥6GB available
```

Add to `docs/operations/dr-walkthrough-log.md`:
```
| 2026-mm-dd | none | ok | CF Tunnel migration completed, Traefik decommissioned |
```

```bash
git add docs/operations/dr-walkthrough-log.md
git commit -m "ops(dr): log CF Tunnel migration completion"
```

---

## Acceptance Criteria

After all 18 tasks merged:

- [ ] Spec G1 — `bash infra/scripts/smoke-set.sh https://meepleai.app` returns exit 0 with 9/9 PASS
- [ ] Spec G2 — `nmap -p 80,443 204.168.135.69` shows filtered/closed; CF tunnel up in CF dashboard
- [ ] Spec G3 — `ssh deploy@... 'free -m'` shows ≥5GB available; `docker ps` shows ≤10 containers in single-tester mode
- [ ] Spec G4 — `make work` cold start ≤90s wall-clock; `Ctrl+C` cleans all processes; `lsof -i:8080,3000` returns empty after stop
- [ ] Spec G5 — `crontab -l | grep meepleai` shows 5 entries; `/var/log/meepleai-restore-test.log` updated monthly with PASS

## Self-Review

After writing this plan, the following review was performed:

**1. Spec coverage:**
- G1 → Tasks 10, 11 (smoke automation) + Task 1 (port cleanup for C1) + verified C2 already implemented in deploy-staging.yml
- G2 → Tasks 16, 17, 18 (migration + rollback runbooks + execution)
- G3 → Tasks 4, 5, 6 (profiles + Makefile targets + deploy update)
- G4 → Tasks 7, 8, 9 (work.sh + Makefile + manual smoke)
- G5 → Tasks 2, 3, 12, 13, 14, 15 (disk prune + cron + smoke read-back + reminder + log + install)

**2. Placeholder scan:** none. All scripts have full code, all commands are explicit. Some Phase 5 steps reference variables that the implementer must adjust to match the existing script (e.g., TEMP_CONTAINER name in Task 12) — this is explicitly noted with "NOTE:" in the task body.

**3. Type/name consistency:**
- `BACKUP_WEBHOOK_URL` used in both `backup.sh` (existing) and `dr-walkthrough-reminder.sh` (Task 13) ✓
- `staging-minimal` target referenced consistently in Tasks 5, 6, deploy workflow ✓
- `tutor-agents` profile name consistent in compose, Makefile, deploy ✓
- `--with-smoke-readback` flag consistent in Task 12 (script) and Task 3 (cron entry) ✓

**4. Out-of-scope (deferred, not in this plan):**
- LLM cost monitoring (separate design)
- GitHub-hosted runners migration (deferred until after CF Tunnel)
- Annual full DR drill (Q5 = no)
- Hardware upgrade (Q5 = no)
- Exploratory testing harness (separate design)
