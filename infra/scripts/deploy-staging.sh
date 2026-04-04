#!/usr/bin/env bash
# =============================================================================
# deploy-staging.sh — Deploy MeepleAI to staging with Slack + Email notifications
# =============================================================================
# Usage:
#   bash infra/scripts/deploy-staging.sh [--skip-build] [--skip-notify] [--dry-run]
#
# Prerequisites:
#   - SSH key: "D:\Repositories\SSH Keys\meepleai-staging" (or SSH_KEY env var)
#   - Secrets on server: /opt/meepleai/repo/infra/secrets/monitoring.secret (SLACK_WEBHOOK_URL)
#   - Secrets on server: /opt/meepleai/repo/infra/secrets/email.secret (SMTP_*)
#   - curl, ssh available in PATH
# =============================================================================

set -euo pipefail

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------
DEPLOY_SERVER="deploy@204.168.135.69"
SSH_KEY="${SSH_KEY:-$HOME/.ssh/meepleai-staging}"
REMOTE_DIR="/opt/meepleai/repo"
REMOTE_INFRA="${REMOTE_DIR}/infra"
DOMAIN="meepleai.app"
ADMIN_URL="https://${DOMAIN}/api/v1/admin"
GRAFANA_URL="https://${DOMAIN}/grafana"
HEALTH_URL="https://${DOMAIN}/api/health"
WEB_URL="https://${DOMAIN}"

# Compose command on server
COMPOSE_CMD="docker compose -f docker-compose.yml -f compose.staging.yml -f compose.traefik.yml"
COMPOSE_PROFILES="--profile ai --profile monitoring --profile proxy"

# Timeouts
HEALTH_TIMEOUT=120  # seconds to wait for health check
HEALTH_INTERVAL=5   # seconds between health checks

# Parse arguments
SKIP_BUILD=false
SKIP_NOTIFY=false
DRY_RUN=false
for arg in "$@"; do
  case "$arg" in
    --skip-build) SKIP_BUILD=true ;;
    --skip-notify) SKIP_NOTIFY=true ;;
    --dry-run) DRY_RUN=true ;;
    --help|-h)
      echo "Usage: deploy-staging.sh [--skip-build] [--skip-notify] [--dry-run]"
      echo "  --skip-build   Skip git pull and rebuild (just restart)"
      echo "  --skip-notify  Skip Slack and email notifications"
      echo "  --dry-run      Show what would be done without executing"
      exit 0
      ;;
    *) echo "Unknown argument: $arg"; exit 1 ;;
  esac
done

# ---------------------------------------------------------------------------
# Colors & Formatting
# ---------------------------------------------------------------------------
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color
BOLD='\033[1m'

log()    { echo -e "${BLUE}[$(date '+%H:%M:%S')]${NC} $*"; }
ok()     { echo -e "${GREEN}[$(date '+%H:%M:%S')] ✅${NC} $*"; }
warn()   { echo -e "${YELLOW}[$(date '+%H:%M:%S')] ⚠️${NC}  $*"; }
err()    { echo -e "${RED}[$(date '+%H:%M:%S')] ❌${NC} $*"; }
header() { echo -e "\n${BOLD}${CYAN}═══ $* ═══${NC}\n"; }

# ---------------------------------------------------------------------------
# SSH helper
# ---------------------------------------------------------------------------
ssh_cmd() {
  ssh -i "$SSH_KEY" -o ConnectTimeout=10 -o StrictHostKeyChecking=accept-new "$DEPLOY_SERVER" "$@"
}

# ---------------------------------------------------------------------------
# Slack notification
# ---------------------------------------------------------------------------
send_slack() {
  local status="$1"  # started | completed | failed | stopped
  local message="$2"
  local color

  if [[ "$SKIP_NOTIFY" == "true" ]]; then return 0; fi

  case "$status" in
    started)   color="#2196F3" ;;  # blue
    completed) color="#4CAF50" ;;  # green
    failed)    color="#F44336" ;;  # red
    stopped)   color="#FF9800" ;;  # orange
    *)         color="#9E9E9E" ;;  # grey
  esac

  # Read webhook URL from server's monitoring.secret
  local webhook_url
  webhook_url=$(ssh_cmd "grep -m1 '^SLACK_WEBHOOK_URL=' ${REMOTE_INFRA}/secrets/monitoring.secret 2>/dev/null | cut -d= -f2-" || true)

  if [[ -z "$webhook_url" || "$webhook_url" == *"YOUR"* ]]; then
    warn "Slack webhook not configured in monitoring.secret — skipping Slack"
    return 0
  fi

  local timestamp
  timestamp=$(date -u '+%Y-%m-%d %H:%M:%S UTC')
  local git_info
  git_info=$(ssh_cmd "cd ${REMOTE_DIR} && git log -1 --format='%h - %s (%an)' 2>/dev/null" || echo "unknown")

  local payload
  payload=$(cat <<SLACK_EOF
{
  "attachments": [{
    "color": "${color}",
    "blocks": [
      {
        "type": "header",
        "text": {"type": "plain_text", "text": "🎲 MeepleAI Staging Deploy — ${status^^}"}
      },
      {
        "type": "section",
        "fields": [
          {"type": "mrkdwn", "text": "*Status:*\n${status^^}"},
          {"type": "mrkdwn", "text": "*Time:*\n${timestamp}"}
        ]
      },
      {
        "type": "section",
        "text": {"type": "mrkdwn", "text": "*Details:*\n${message}"}
      },
      {
        "type": "section",
        "text": {"type": "mrkdwn", "text": "*Last Commit:*\n\`${git_info}\`"}
      },
      {
        "type": "section",
        "text": {"type": "mrkdwn", "text": "<${WEB_URL}|🌐 Site> | <${ADMIN_URL}|⚙️ Admin> | <${GRAFANA_URL}|📊 Grafana> | <${HEALTH_URL}|💚 Health>"}
      }
    ]
  }]
}
SLACK_EOF
  )

  if [[ "$DRY_RUN" == "true" ]]; then
    log "[DRY-RUN] Would send Slack: ${status}"
    return 0
  fi

  curl -sS -X POST "$webhook_url" \
    -H "Content-Type: application/json" \
    -d "$payload" > /dev/null 2>&1 && ok "Slack notification sent (${status})" || warn "Slack notification failed"
}

# ---------------------------------------------------------------------------
# Email notification (via server's SMTP using Python)
# ---------------------------------------------------------------------------
send_email() {
  local status="$1"  # completed | failed
  local services_status="$2"

  if [[ "$SKIP_NOTIFY" == "true" ]]; then return 0; fi
  if [[ "$DRY_RUN" == "true" ]]; then
    log "[DRY-RUN] Would send email report"
    return 0
  fi

  local timestamp
  timestamp=$(date -u '+%Y-%m-%d %H:%M:%S UTC')
  local git_info
  git_info=$(ssh_cmd "cd ${REMOTE_DIR} && git log -1 --format='%h - %s (%an, %ar)' 2>/dev/null" || echo "unknown")
  local git_branch
  git_branch=$(ssh_cmd "cd ${REMOTE_DIR} && git branch --show-current 2>/dev/null" || echo "unknown")

  local status_emoji="✅"
  local status_color="#4CAF50"
  if [[ "$status" == "failed" ]]; then
    status_emoji="❌"
    status_color="#F44336"
  fi

  # Build HTML email body
  local html_body
  html_body=$(cat <<HTML_EOF
<html>
<body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 700px; margin: 0 auto; padding: 20px; background: #f5f5f5;">
  <div style="background: white; border-radius: 12px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.1);">
    <!-- Header -->
    <div style="background: ${status_color}; padding: 24px; text-align: center;">
      <h1 style="color: white; margin: 0; font-size: 24px;">${status_emoji} MeepleAI Staging Deploy — ${status^^}</h1>
      <p style="color: rgba(255,255,255,0.9); margin: 8px 0 0;">${timestamp}</p>
    </div>

    <!-- Quick Links -->
    <div style="padding: 16px 24px; background: #f8f9fa; border-bottom: 1px solid #e0e0e0; text-align: center;">
      <a href="${WEB_URL}" style="display: inline-block; margin: 0 8px; padding: 8px 16px; background: #1a73e8; color: white; text-decoration: none; border-radius: 6px;">🌐 Site</a>
      <a href="${ADMIN_URL}" style="display: inline-block; margin: 0 8px; padding: 8px 16px; background: #e8710a; color: white; text-decoration: none; border-radius: 6px;">⚙️ Admin API</a>
      <a href="${GRAFANA_URL}" style="display: inline-block; margin: 0 8px; padding: 8px 16px; background: #6c47ff; color: white; text-decoration: none; border-radius: 6px;">📊 Grafana</a>
      <a href="${HEALTH_URL}" style="display: inline-block; margin: 0 8px; padding: 8px 16px; background: #0d904f; color: white; text-decoration: none; border-radius: 6px;">💚 Health</a>
    </div>

    <!-- Git Info -->
    <div style="padding: 16px 24px; border-bottom: 1px solid #e0e0e0;">
      <h3 style="margin: 0 0 8px; color: #333;">📦 Deployment Info</h3>
      <table style="width: 100%; border-collapse: collapse;">
        <tr><td style="padding: 4px 8px; color: #666; width: 120px;">Branch:</td><td style="padding: 4px 8px;"><code>${git_branch}</code></td></tr>
        <tr><td style="padding: 4px 8px; color: #666;">Commit:</td><td style="padding: 4px 8px;"><code>${git_info}</code></td></tr>
        <tr><td style="padding: 4px 8px; color: #666;">Server:</td><td style="padding: 4px 8px;">${DEPLOY_SERVER}</td></tr>
        <tr><td style="padding: 4px 8px; color: #666;">Domain:</td><td style="padding: 4px 8px;">${DOMAIN}</td></tr>
      </table>
    </div>

    <!-- Services Status -->
    <div style="padding: 16px 24px;">
      <h3 style="margin: 0 0 8px; color: #333;">🐳 Service Status</h3>
      <pre style="background: #1e1e1e; color: #d4d4d4; padding: 16px; border-radius: 8px; overflow-x: auto; font-size: 13px; line-height: 1.5;">${services_status}</pre>
    </div>
  </div>

  <p style="text-align: center; color: #999; font-size: 12px; margin-top: 16px;">
    MeepleAI Deploy Bot — ${timestamp}
  </p>
</body>
</html>
HTML_EOF
  )

  # Send email via Python on the server (using SMTP credentials from email.secret)
  ssh_cmd "bash -s" <<REMOTE_EMAIL_EOF
set -a
source ${REMOTE_INFRA}/secrets/email.secret 2>/dev/null || true
source ${REMOTE_INFRA}/secrets/monitoring.secret 2>/dev/null || true
set +a

# Read ALERT_EMAIL_TO from alertmanager.env or fall back
ALERT_EMAIL_TO=\${ALERT_EMAIL_TO:-\${SMTP_USER}}

python3 -c "
import smtplib, os
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText

host = os.environ.get('SMTP_HOST', '')
port = int(os.environ.get('SMTP_PORT', '587'))
user = os.environ.get('SMTP_USER', '')
password = os.environ.get('SMTP_PASSWORD', '')
from_email = os.environ.get('SMTP_FROM_EMAIL', user)
to_email = os.environ.get('ALERT_EMAIL_TO', user)

if not all([host, user, password]):
    print('SMTP not configured — skipping email')
    exit(0)

msg = MIMEMultipart('alternative')
msg['Subject'] = '${status_emoji} MeepleAI Staging Deploy — ${status^^} (${timestamp})'
msg['From'] = from_email
msg['To'] = to_email

html = '''$(echo "$html_body" | sed "s/'/\\\\'/g")'''
msg.attach(MIMEText(html, 'html'))

try:
    with smtplib.SMTP(host, port) as server:
        server.starttls()
        server.login(user, password)
        server.sendmail(from_email, [to_email], msg.as_string())
    print('Email sent to ' + to_email)
except Exception as e:
    print(f'Email failed: {e}')
" 2>&1
REMOTE_EMAIL_EOF

  local exit_code=$?
  if [[ $exit_code -eq 0 ]]; then
    ok "Email report sent"
  else
    warn "Email sending may have failed (exit code: $exit_code)"
  fi
}

# ---------------------------------------------------------------------------
# Health check with retry
# ---------------------------------------------------------------------------
wait_for_health() {
  local url="$1"
  local name="$2"
  local elapsed=0

  log "Waiting for ${name} to be healthy..."
  while [[ $elapsed -lt $HEALTH_TIMEOUT ]]; do
    local status_code
    status_code=$(ssh_cmd "curl -sS -o /dev/null -w '%{http_code}' --max-time 5 ${url}" 2>/dev/null || echo "000")
    if [[ "$status_code" == "200" ]]; then
      ok "${name} is healthy (${elapsed}s)"
      return 0
    fi
    sleep $HEALTH_INTERVAL
    elapsed=$((elapsed + HEALTH_INTERVAL))
  done

  err "${name} did not become healthy within ${HEALTH_TIMEOUT}s"
  return 1
}

# ---------------------------------------------------------------------------
# Get services status
# ---------------------------------------------------------------------------
get_services_status() {
  ssh_cmd "cd ${REMOTE_INFRA} && ${COMPOSE_CMD} ${COMPOSE_PROFILES} ps --format 'table {{.Name}}\t{{.Status}}\t{{.Ports}}' 2>/dev/null" || echo "Could not retrieve service status"
}

# ==========================================================================
# MAIN DEPLOY FLOW
# ==========================================================================

DEPLOY_START=$(date +%s)

header "MeepleAI Staging Deploy"
log "Server: ${DEPLOY_SERVER}"
log "Domain: ${DOMAIN}"
log "Skip build: ${SKIP_BUILD}"
log "Dry run: ${DRY_RUN}"
echo ""

# --- Step 0: Verify SSH connectivity ---
header "Step 0 — Verify SSH"
if [[ "$DRY_RUN" == "true" ]]; then
  log "[DRY-RUN] Would test SSH to ${DEPLOY_SERVER}"
else
  log "Testing SSH connection..."
  if ssh_cmd "echo 'SSH OK'" > /dev/null 2>&1; then
    ok "SSH connection verified"
  else
    err "Cannot connect to ${DEPLOY_SERVER}"
    err "Check SSH key: ${SSH_KEY}"
    exit 1
  fi
fi

# --- Step 1: Notify START ---
header "Step 1 — Notify Deploy Started"
send_slack "started" "Deploy initiated from local machine. Building and deploying..."

# --- Step 2: Git pull + rebuild ---
header "Step 2 — Git Pull & Build"
if [[ "$SKIP_BUILD" == "true" ]]; then
  log "Skipping git pull & build (--skip-build)"
elif [[ "$DRY_RUN" == "true" ]]; then
  log "[DRY-RUN] Would: git pull + docker compose build + up"
else
  log "Pulling latest code on server..."
  ssh_cmd "cd ${REMOTE_DIR} && git pull --ff-only" || {
    err "Git pull failed — check for conflicts on server"
    send_slack "failed" "Git pull failed on server. Manual intervention needed."
    exit 1
  }
  ok "Git pull complete"

  log "Loading secrets and starting build..."
  ssh_cmd "cd ${REMOTE_INFRA} && set -a && for f in secrets/*.secret; do source \"\$f\" 2>/dev/null; done && set +a && ${COMPOSE_CMD} ${COMPOSE_PROFILES} build --parallel" || {
    err "Docker build failed"
    send_slack "failed" "Docker build failed. Check server logs."
    exit 1
  }
  ok "Docker build complete"
fi

# --- Step 3: Deploy (compose up) ---
header "Step 3 — Deploy Services"
if [[ "$DRY_RUN" == "true" ]]; then
  log "[DRY-RUN] Would: docker compose up -d"
else
  log "Starting services..."
  ssh_cmd "cd ${REMOTE_INFRA} && set -a && for f in secrets/*.secret; do source \"\$f\" 2>/dev/null; done && set +a && ${COMPOSE_CMD} ${COMPOSE_PROFILES} up -d --remove-orphans" || {
    err "Docker compose up failed"
    send_slack "stopped" "Compose up failed. Services may be in inconsistent state."
    exit 1
  }
  ok "Services started"
fi

# --- Step 4: Health checks ---
header "Step 4 — Health Checks"
HEALTH_OK=true
if [[ "$DRY_RUN" == "true" ]]; then
  log "[DRY-RUN] Would check health endpoints"
else
  # Wait for API health
  if ! wait_for_health "http://localhost:8080/health" "API"; then
    HEALTH_OK=false
    warn "API health check failed"
  fi

  # Wait for Web
  if ! wait_for_health "http://localhost:3000" "Web"; then
    HEALTH_OK=false
    warn "Web health check failed"
  fi
fi

# --- Step 5: Collect service status ---
header "Step 5 — Service Status"
SERVICES_STATUS=""
if [[ "$DRY_RUN" == "true" ]]; then
  SERVICES_STATUS="[DRY-RUN] Service status would be collected here"
else
  SERVICES_STATUS=$(get_services_status)
  echo "$SERVICES_STATUS"
fi

# --- Step 6: Final notifications ---
header "Step 6 — Final Notifications"
DEPLOY_END=$(date +%s)
DEPLOY_DURATION=$(( DEPLOY_END - DEPLOY_START ))
DURATION_MIN=$(( DEPLOY_DURATION / 60 ))
DURATION_SEC=$(( DEPLOY_DURATION % 60 ))

if [[ "$HEALTH_OK" == "true" ]]; then
  FINAL_STATUS="completed"
  FINAL_MSG="Deploy completed successfully in ${DURATION_MIN}m ${DURATION_SEC}s. All health checks passed."
else
  FINAL_STATUS="stopped"
  FINAL_MSG="Deploy finished in ${DURATION_MIN}m ${DURATION_SEC}s but some health checks failed. Check services."
fi

send_slack "$FINAL_STATUS" "$FINAL_MSG"
send_email "$FINAL_STATUS" "$SERVICES_STATUS"

# --- Summary ---
header "Deploy Summary"
echo -e "${BOLD}Status:${NC}   ${FINAL_STATUS^^}"
echo -e "${BOLD}Duration:${NC} ${DURATION_MIN}m ${DURATION_SEC}s"
echo -e "${BOLD}Health:${NC}   $([ "$HEALTH_OK" == "true" ] && echo "✅ All OK" || echo "⚠️ Issues detected")"
echo ""
echo -e "${BOLD}Links:${NC}"
echo -e "  🌐 Site:    ${WEB_URL}"
echo -e "  ⚙️  Admin:   ${ADMIN_URL}"
echo -e "  📊 Grafana: ${GRAFANA_URL}"
echo -e "  💚 Health:  ${HEALTH_URL}"
echo ""

if [[ "$HEALTH_OK" == "true" ]]; then
  ok "Deploy staging complete! 🎲"
  exit 0
else
  warn "Deploy finished with issues — check service status above"
  exit 1
fi
