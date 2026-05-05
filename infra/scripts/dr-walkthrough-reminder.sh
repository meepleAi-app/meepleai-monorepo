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
