#!/usr/bin/env bash
# Runner Maintenance Script
# Epic #2967: Zero-Cost CI/CD Infrastructure
#
# Usage: ./maintenance.sh [--install-cron]
#   --install-cron  Install cron jobs for automated maintenance

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
LOG_FILE="/var/log/runner-maintenance.log"

log() {
  echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] $*" | sudo tee -a "$LOG_FILE"
}

# Docker cleanup (remove unused images, containers, volumes)
docker_cleanup() {
  log "=== Docker Cleanup ==="
  local before=$(docker system df --format '{{.Size}}' 2>/dev/null | head -1)

  docker container prune -f --filter "until=24h" 2>/dev/null || true
  docker image prune -af --filter "until=48h" 2>/dev/null || true
  docker volume prune -f 2>/dev/null || true
  docker builder prune -af --keep-storage 5GB 2>/dev/null || true

  local after=$(docker system df --format '{{.Size}}' 2>/dev/null | head -1)
  log "Docker cleanup: $before -> $after"
}

# System package updates (security patches only)
system_update() {
  log "=== System Updates ==="
  sudo apt-get update -qq
  sudo apt-get upgrade -y -qq --only-upgrade
  log "System packages updated"
}

# Runner software update check
runner_update_check() {
  log "=== Runner Update Check ==="
  local runner_dir="/home/ubuntu/actions-runner"
  if [ -f "$runner_dir/bin/Runner.Listener" ]; then
    local current=$("$runner_dir/bin/Runner.Listener" --version 2>/dev/null || echo "unknown")
    local latest=$(curl -s https://api.github.com/repos/actions/runner/releases/latest | jq -r '.tag_name' | sed 's/^v//')
    if [ "$current" != "$latest" ] && [ -n "$latest" ]; then
      log "Runner update available: $current -> $latest"
      echo "UPDATE_AVAILABLE: $current -> $latest"
    else
      log "Runner is up to date: $current"
    fi
  else
    log "Runner not found at $runner_dir"
  fi
}

# Log rotation
rotate_logs() {
  log "=== Log Rotation ==="
  if [ -f "$LOG_FILE" ] && [ "$(stat -c%s "$LOG_FILE" 2>/dev/null || stat -f%z "$LOG_FILE")" -gt 10485760 ]; then
    sudo mv "$LOG_FILE" "${LOG_FILE}.old"
    log "Rotated maintenance log"
  fi

  # Rotate runner logs — auto-detect runner_dir
  # (legacy hardcode was /home/ubuntu/actions-runner; on meepleai-staging the
  # runner runs as user `deploy`, so the legacy path silently matched nothing
  # and 14 GB of _diag accumulated unnoticed — see issue #1575).
  local runner_dir=""
  for candidate in /home/deploy/actions-runner /home/ubuntu/actions-runner /home/runner/actions-runner; do
    if [ -d "$candidate/_diag" ]; then
      runner_dir="$candidate"
      break
    fi
  done
  if [ -n "$runner_dir" ]; then
    # Top-level Runner_*.log / Worker_*.log: keep 7 days (audit window).
    local logs_before
    logs_before=$(find "$runner_dir/_diag" -maxdepth 1 -name "*.log" -mtime +7 2>/dev/null | wc -l)
    find "$runner_dir/_diag" -maxdepth 1 -name "*.log" -mtime +7 -delete 2>/dev/null || true
    # blocks/ + pages/ are per-job streaming buffers — every entry > 1 day old
    # belongs to a long-finished job (typical job duration: minutes to an hour).
    # This was the main offender in #1575 (14 GB across 6810 block files).
    local blocks_before pages_before
    blocks_before=$(find "$runner_dir/_diag/blocks" -type f -mtime +1 2>/dev/null | wc -l)
    pages_before=$(find "$runner_dir/_diag/pages" -type f -mtime +1 2>/dev/null | wc -l)
    find "$runner_dir/_diag/blocks" -type f -mtime +1 -delete 2>/dev/null || true
    find "$runner_dir/_diag/pages" -type f -mtime +1 -delete 2>/dev/null || true
    log "Cleaned runner _diag: $runner_dir (top-level logs >7d: $logs_before, blocks >1d: $blocks_before, pages >1d: $pages_before)"
  else
    log "WARN: no runner _diag directory found under /home/{deploy,ubuntu,runner}/actions-runner"
  fi
}

# Disk space check
check_disk() {
  log "=== Disk Check ==="
  local usage=$(df -h / | awk 'NR==2{print $5}' | tr -d '%')
  if [ "$usage" -gt 85 ]; then
    log "WARNING: Disk usage at ${usage}%"
    # Emergency cleanup
    docker system prune -af 2>/dev/null || true
    sudo apt-get autoremove -y -qq 2>/dev/null || true
    sudo apt-get clean 2>/dev/null || true
    log "Emergency cleanup performed"
  else
    log "Disk usage: ${usage}%"
  fi
}

# Install cron jobs
install_cron() {
  local cron_content="# MeepleAI Runner Maintenance (Epic #2967)
# Daily: Docker cleanup + disk check (3:00 AM UTC)
0 3 * * * ${SCRIPT_DIR}/maintenance.sh daily >> /var/log/runner-maintenance-cron.log 2>&1
# Weekly: System updates (Sunday 4:00 AM UTC)
0 4 * * 0 ${SCRIPT_DIR}/maintenance.sh weekly >> /var/log/runner-maintenance-cron.log 2>&1
# Monthly: Full maintenance (1st of month, 5:00 AM UTC)
0 5 1 * * ${SCRIPT_DIR}/maintenance.sh monthly >> /var/log/runner-maintenance-cron.log 2>&1
# Every 5 min: Health check
*/5 * * * * ${SCRIPT_DIR}/monitor.sh --check >> /dev/null 2>&1 || echo \"[ALERT] Runner unhealthy at \$(date)\" >> /var/log/runner-alerts.log
"
  # Install cron under the user that actually runs the runner.
  # Legacy code targeted `ubuntu`; meepleai-staging uses `deploy`.
  local cron_user=""
  for u in deploy ubuntu runner; do
    if id "$u" >/dev/null 2>&1 && [ -d "/home/$u/actions-runner" ]; then
      cron_user="$u"
      break
    fi
  done
  if [ -z "$cron_user" ]; then
    echo "ERROR: no runner user found (looked for deploy, ubuntu, runner)"
    exit 1
  fi
  echo "$cron_content" | sudo crontab -u "$cron_user" -
  echo "Cron jobs installed for user '$cron_user'. Verify with: sudo crontab -u $cron_user -l"
}

# Main dispatcher
case "${1:-full}" in
  daily)
    docker_cleanup
    check_disk
    rotate_logs
    ;;
  weekly)
    docker_cleanup
    system_update
    check_disk
    rotate_logs
    ;;
  monthly)
    docker_cleanup
    system_update
    runner_update_check
    check_disk
    rotate_logs
    ;;
  full)
    docker_cleanup
    system_update
    runner_update_check
    check_disk
    rotate_logs
    ;;
  --install-cron)
    install_cron
    ;;
  *)
    echo "Usage: $0 [daily|weekly|monthly|full|--install-cron]"
    exit 1
    ;;
esac

log "=== Maintenance Complete ==="
