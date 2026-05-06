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
