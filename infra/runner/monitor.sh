#!/usr/bin/env bash
# Runner Health Monitor
# Epic #2967: Zero-Cost CI/CD Infrastructure
#
# Usage: ./monitor.sh [--json] [--check]
#   --json   Output as JSON (for alerting)
#   --check  Exit 1 if unhealthy

set -euo pipefail

JSON_OUTPUT=false
CHECK_MODE=false

while [[ $# -gt 0 ]]; do
  case $1 in
    --json) JSON_OUTPUT=true; shift ;;
    --check) CHECK_MODE=true; shift ;;
    *) shift ;;
  esac
done

# Collect metrics
RUNNER_SERVICE=$(systemctl list-units --type=service --no-legend | grep actions.runner | awk '{print $1}' || echo "")
RUNNER_STATUS="unknown"
if [ -n "$RUNNER_SERVICE" ]; then
  RUNNER_STATUS=$(systemctl is-active "$RUNNER_SERVICE" 2>/dev/null || echo "inactive")
fi

UPTIME=$(awk '{print int($1/86400)"d "int($1%86400/3600)"h"}' /proc/uptime)
CPU_USAGE=$(top -bn1 | grep "Cpu(s)" | awk '{print $2}' | cut -d'%' -f1)
MEM_TOTAL=$(free -m | awk '/Mem:/{print $2}')
MEM_USED=$(free -m | awk '/Mem:/{print $3}')
MEM_PCT=$(awk "BEGIN{printf \"%.1f\", $MEM_USED/$MEM_TOTAL*100}")
DISK_PCT=$(df -h / | awk 'NR==2{print $5}' | tr -d '%')
DOCKER_STATUS=$(systemctl is-active docker 2>/dev/null || echo "inactive")
DOCKER_IMAGES=$(docker images -q 2>/dev/null | wc -l)
DOCKER_CONTAINERS=$(docker ps -q 2>/dev/null | wc -l)
LOAD_AVG=$(cat /proc/loadavg | awk '{print $1}')

# Health assessment
HEALTHY=true
ISSUES=()

if [ "$RUNNER_STATUS" != "active" ]; then
  HEALTHY=false
  ISSUES+=("Runner service is $RUNNER_STATUS")
fi

if [ "$DOCKER_STATUS" != "active" ]; then
  HEALTHY=false
  ISSUES+=("Docker is $DOCKER_STATUS")
fi

if [ "$DISK_PCT" -gt 85 ]; then
  HEALTHY=false
  ISSUES+=("Disk usage at ${DISK_PCT}%")
fi

if [ "$JSON_OUTPUT" = true ]; then
  cat <<JSON
{
  "healthy": $HEALTHY,
  "timestamp": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "uptime": "$UPTIME",
  "runner": {
    "service": "$RUNNER_SERVICE",
    "status": "$RUNNER_STATUS"
  },
  "system": {
    "cpu_usage": $CPU_USAGE,
    "memory_used_mb": $MEM_USED,
    "memory_total_mb": $MEM_TOTAL,
    "memory_pct": $MEM_PCT,
    "disk_pct": $DISK_PCT,
    "load_avg": $LOAD_AVG
  },
  "docker": {
    "status": "$DOCKER_STATUS",
    "images": $DOCKER_IMAGES,
    "running_containers": $DOCKER_CONTAINERS
  },
  "issues": [$([ ${#ISSUES[@]} -gt 0 ] && printf '"%s",' "${ISSUES[@]}" | sed 's/,$//' || true)]
}
JSON
else
  echo "=== MeepleAI Runner Health ==="
  echo "Time:    $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "Uptime:  $UPTIME"
  echo ""
  echo "Runner:  $RUNNER_STATUS ($RUNNER_SERVICE)"
  echo "Docker:  $DOCKER_STATUS ($DOCKER_CONTAINERS containers, $DOCKER_IMAGES images)"
  echo ""
  echo "CPU:     ${CPU_USAGE}%"
  echo "Memory:  ${MEM_USED}/${MEM_TOTAL} MB (${MEM_PCT}%)"
  echo "Disk:    ${DISK_PCT}%"
  echo "Load:    $LOAD_AVG"
  echo ""
  if [ "$HEALTHY" = true ]; then
    echo "Status:  HEALTHY"
  else
    echo "Status:  UNHEALTHY"
    for issue in "${ISSUES[@]}"; do
      echo "  - $issue"
    done
  fi
fi

if [ "$CHECK_MODE" = true ] && [ "$HEALTHY" = false ]; then
  exit 1
fi
