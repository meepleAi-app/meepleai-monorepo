#!/usr/bin/env bash
# Daily Docker + runner _diag prune — keep VPS disk under control
# Runs from cron at 05:00 (after 03:00 backup, before any morning activity).
#
# Usage: daily-disk-prune.sh [--dry-run]
#   --dry-run    Log what would be removed without executing destructive ops.
#                Useful for testing the stale-staging detection logic.
#
# Cron: 0 5 * * * cd /opt/meepleai/repo/infra && bash scripts/daily-disk-prune.sh >> /var/log/meepleai-disk-prune.log 2>&1
#
# Exit codes:
#   0 — prune ran, disk pressure within bounds
#   2 — WARNING: prune ran but reclaimed_space < 100MB AND disk_free < 15GB
#       (cron output is captured; surface via log monitor / Slack)
#
# Scope (issue #1575):
# - Docker images > 72h (kept) + builder cache > 24h + stopped containers +
#   unused networks.
# - Stale staging-* images: removed regardless of mtime if NOT the
#   currently-deployed tag per DEPLOYMENT.json (with 3 safeguards to never
#   touch the running container's image — see prune_stale_staging_images).
# - Runner _diag rotation: top-level logs > 7d, blocks/pages > 1d
#   (the runner self-hosted on this VPS accumulated 14GB in _diag/blocks
#    because maintenance.sh hardcoded a wrong path — see #1575).
# - DOES NOT touch docker volumes (pgdata/redis/loki — never auto-prune).

set -uo pipefail  # NOTE: no `-e` — pruning failures should not abort the chain.

# Parse flags
DRY_RUN=0
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY_RUN=1 ;;
    *) echo "Unknown arg: $arg" >&2; exit 64 ;;
  esac
done

log() {
  echo "[$(date '+%Y-%m-%dT%H:%M:%S')] $*"
}

# Capture disk_free in MB (numeric) for end-of-run comparison.
disk_free_mb() {
  df -BM / | awk 'NR==2 {gsub(/M/,"",$4); print $4}'
}

# Remove staging-* images that are NOT currently deployed.
# Three safeguards (per spec-panel critique on #1575):
#   1. DEPLOYMENT.json must exist (skip otherwise — never blanket-prune).
#   2. api_image + web_image must be non-empty strings.
#   3. Running containers (meepleai-api, meepleai-web) must match the
#      DEPLOYMENT.json declarations (defends against a stale json file
#      from an aborted deploy, e.g. issue #1573).
# Image-aware retention bypasses the standard `--filter "until=72h"`
# because staging-{date}-{sha} tags are immutable: once superseded, they
# will never be referenced again, so 72h is unnecessary slack.
prune_stale_staging_images() {
  log "=== Stale staging-* image prune ==="
  local deployment_json="/opt/meepleai/repo/infra/DEPLOYMENT.json"

  if [ ! -f "$deployment_json" ]; then
    log "SKIP: $deployment_json not found (no deployment record — refusing to guess)"
    return 0
  fi

  # Prefer jq when available; python3 is the universal fallback.
  local current_api_image="" current_web_image=""
  if command -v jq >/dev/null 2>&1; then
    current_api_image=$(jq -r '.api_image // empty' "$deployment_json" 2>/dev/null)
    current_web_image=$(jq -r '.web_image // empty' "$deployment_json" 2>/dev/null)
  elif command -v python3 >/dev/null 2>&1; then
    current_api_image=$(python3 -c "import json,sys; print(json.load(open('$deployment_json')).get('api_image',''))" 2>/dev/null)
    current_web_image=$(python3 -c "import json,sys; print(json.load(open('$deployment_json')).get('web_image',''))" 2>/dev/null)
  else
    log "SKIP: neither jq nor python3 available — cannot parse DEPLOYMENT.json"
    return 0
  fi

  if [ -z "$current_api_image" ] || [ -z "$current_web_image" ]; then
    log "SKIP: DEPLOYMENT.json missing api_image or web_image"
    return 0
  fi

  # Paranoia check: the json must agree with what's actually running.
  local running_api running_web
  running_api=$(docker inspect meepleai-api --format '{{.Config.Image}}' 2>/dev/null || echo "")
  running_web=$(docker inspect meepleai-web --format '{{.Config.Image}}' 2>/dev/null || echo "")
  if [ "$running_api" != "$current_api_image" ] || [ "$running_web" != "$current_web_image" ]; then
    log "SKIP: DEPLOYMENT.json (api=$current_api_image, web=$current_web_image) does not match running containers (api=$running_api, web=$running_web)"
    log "      A deploy may be in progress, aborted, or DEPLOYMENT.json is stale — refusing to prune."
    return 0
  fi

  log "Currently deployed: api=$current_api_image, web=$current_web_image"

  local removed=0 considered=0
  while IFS= read -r img; do
    [ -z "$img" ] && continue
    considered=$((considered + 1))
    if [ "$img" = "$current_api_image" ] || [ "$img" = "$current_web_image" ]; then
      # Never touch the currently-deployed images.
      continue
    fi
    if [ "$DRY_RUN" = "1" ]; then
      log "DRY-RUN: would remove $img"
      removed=$((removed + 1))
    else
      if docker rmi "$img" >/dev/null 2>&1; then
        log "Removed stale: $img"
        removed=$((removed + 1))
      else
        log "WARN: could not remove $img (still referenced?)"
      fi
    fi
  done < <(docker images --format '{{.Repository}}:{{.Tag}}' \
            | grep -E ':staging-[0-9]{8}-[a-f0-9]+$' \
            | grep -v '<none>')

  log "Stale-staging prune complete: $removed removed / $considered considered (dry-run=$DRY_RUN)"
}

DISK_BEFORE_MB=$(disk_free_mb)
log "=== Disk prune starting ==="
log "Disk before: ${DISK_BEFORE_MB} MB free"

# --- Docker pruning ---------------------------------------------------------

# Prune images not used in 72 hours; keeps recent build cache for fast rebuilds.
# --filter "until=72h" excludes anything created/used in last 3 days.
docker image prune -af --filter "until=72h" 2>&1 || log "WARN: image prune had non-zero exit"

# Prune builder cache older than 24h (BuildKit layers)
docker builder prune -f --filter "until=24h" 2>&1 || log "WARN: builder prune had non-zero exit"

# Prune stopped containers (any age — they should not accumulate)
docker container prune -f 2>&1 || log "WARN: container prune had non-zero exit"

# Networks: prune unused
docker network prune -f 2>&1 || log "WARN: network prune had non-zero exit"

# Image-aware staging-* retention (issue #1575 deferred C).
# This runs AFTER `docker image prune --filter until=72h`, so it only catches
# superseded staging tags that the time filter skipped. With three safeguards
# (DEPLOYMENT.json present + non-empty tags + matches running containers),
# this never touches a currently-deployed image.
prune_stale_staging_images

# DO NOT prune volumes — they contain pgdata/redis/loki/grafana/prometheus.
# Only manual `docker volume prune` allowed.

# --- Runner _diag rotation (#1575) ------------------------------------------
# Auto-detect runner dir. The runner on meepleai-staging runs as `deploy`,
# but earlier scripts hardcoded `/home/ubuntu/...` and the directory grew
# unbounded to 14 GB before this fix.

RUNNER_DIAG=""
for candidate in /home/deploy/actions-runner/_diag /home/ubuntu/actions-runner/_diag /home/runner/actions-runner/_diag; do
  if [ -d "$candidate" ]; then
    RUNNER_DIAG="$candidate"
    break
  fi
done

if [ -n "$RUNNER_DIAG" ]; then
  log "Runner _diag found: $RUNNER_DIAG"
  # Top-level Runner_*.log / Worker_*.log: 7-day audit window
  TOP_LOGS_DELETED=$(find "$RUNNER_DIAG" -maxdepth 1 -name "*.log" -mtime +7 2>/dev/null | wc -l)
  find "$RUNNER_DIAG" -maxdepth 1 -name "*.log" -mtime +7 -delete 2>/dev/null || true
  # blocks/ + pages/ are per-job streaming buffers. Any entry > 1 day old
  # belongs to a completed job (typical job duration: minutes to an hour).
  BLOCKS_DELETED=$(find "$RUNNER_DIAG/blocks" -type f -mtime +1 2>/dev/null | wc -l)
  PAGES_DELETED=$(find "$RUNNER_DIAG/pages" -type f -mtime +1 2>/dev/null | wc -l)
  find "$RUNNER_DIAG/blocks" -type f -mtime +1 -delete 2>/dev/null || true
  find "$RUNNER_DIAG/pages" -type f -mtime +1 -delete 2>/dev/null || true
  log "Runner _diag pruned: top-level logs >7d=$TOP_LOGS_DELETED, blocks >1d=$BLOCKS_DELETED, pages >1d=$PAGES_DELETED"
else
  log "No runner _diag dir found (host without self-hosted runner — skipping)"
fi

# --- Reporting + alert gate -------------------------------------------------

DISK_AFTER_MB=$(disk_free_mb)
RECLAIMED_MB=$((DISK_AFTER_MB - DISK_BEFORE_MB))
log "Disk after: ${DISK_AFTER_MB} MB free (reclaimed: ${RECLAIMED_MB} MB)"
log "=== Disk prune complete ==="

# Alert gate: if we are below 15GB AND the prune barely moved the needle,
# something is using disk that our prune does not cover — surface it.
# 15 GB free = 80% used on a 75 GB disk (10% safety margin above the 90% red zone).
DISK_FREE_THRESHOLD_MB=15360   # 15 GB
RECLAIM_THRESHOLD_MB=100       # 100 MB
if [ "$DISK_AFTER_MB" -lt "$DISK_FREE_THRESHOLD_MB" ] && [ "$RECLAIMED_MB" -lt "$RECLAIM_THRESHOLD_MB" ]; then
  log "ALERT: disk_free=${DISK_AFTER_MB}MB < ${DISK_FREE_THRESHOLD_MB}MB AND reclaimed=${RECLAIMED_MB}MB < ${RECLAIM_THRESHOLD_MB}MB"
  log "ALERT: prune is no longer effective — investigate disk consumers (see #1575)"
  exit 2
fi

exit 0
