#!/usr/bin/env bash
# notify.sh — shared notification channel for the backup family (#3669).
#
# Sourced by backup.sh, backup-verify.sh and backup-restore-test.sh so the three
# resolve the channel identically. Before this, only backup.sh could notify at
# all, and it read a variable nobody had set: BACKUP_WEBHOOK_URL was empty on
# staging, so notify_webhook() returned 0 on its first line and the degraded /
# failure signal built by PR #3690 had no transport. The monthly restore test
# had meanwhile failed three times out of three, in silence, because it had no
# notification path whatsoever.
#
# Two transports, tried in order, because one of them is currently dead and that
# is exactly the point:
#
#   1. webhook — BACKUP_WEBHOOK_URL, else SLACK_WEBHOOK_URL from
#      monitoring.secret. The fallback matters: that variable already exists and
#      already feeds Alertmanager. Requiring a second copy of the same URL under
#      a second name is how the first one ended up empty.
#
#   2. email — SMTP credentials from email.secret. Added because the Slack
#      webhook on staging answers HTTP 404 "no_service": it was revoked at some
#      point, so the wire was connected to nothing. A configured-but-dead channel
#      is the same defect as an unconfigured one, one layer down, and a single
#      transport gives it nowhere to be caught.
#
# If neither delivers, that is stated out loud. A silent return is the defect
# this file exists to remove.

# ─────────────────────────────────────────────
# Channel resolution
# ─────────────────────────────────────────────

resolve_notify_url() {
  printf '%s' "${BACKUP_WEBHOOK_URL:-${SLACK_WEBHOOK_URL:-}}"
}

# Alerts go to BACKUP_ALERT_EMAIL_TO; defaulting to the sending address means a
# host with working SMTP is never left with no destination at all, without this
# file guessing at somebody's personal mailbox.
resolve_notify_email() {
  printf '%s' "${BACKUP_ALERT_EMAIL_TO:-${SMTP_FROM_EMAIL:-}}"
}

notify_email_configured() {
  [[ -n "${SMTP_HOST:-}" && -n "${SMTP_USER:-}" && -n "${SMTP_PASSWORD:-}" && -n "$(resolve_notify_email)" ]]
}

notify_has_channel() {
  [[ -n "$(resolve_notify_url)" ]] || notify_email_configured
}

_notify_log() {
  echo "[$(date '+%Y-%m-%dT%H:%M:%S')] [notify] $*"
}

# ─────────────────────────────────────────────
# Liveness probe
# ─────────────────────────────────────────────
#
# Answers "would a notification actually arrive?" without sending one. A Slack
# incoming webhook that still exists rejects a body with no "text" as HTTP 400
# invalid_payload; a revoked one answers 404 no_service regardless of the body.
# So an empty JSON object separates alive from dead at the cost of one request
# and zero messages in the channel — which is what makes it safe to run daily
# from backup-verify.sh. Only 404/410 are treated as dead: a timeout or a 5xx is
# Slack having a bad minute, not a revoked hook, and must not cry wolf.
#
# Returns 0 = channel looks deliverable, 1 = definitively dead, 2 = unknown.
notify_probe_channel() {
  local url
  url="$(resolve_notify_url)"

  [[ -z "${url}" ]] && return 2
  [[ -n "${BACKUP_NOTIFY_DISABLED:-}" ]] && return 0

  local code
  code=$(curl --silent --output /dev/null --max-time 10 \
           --write-out '%{http_code}' \
           -H "Content-Type: application/json" \
           -d '{}' "${url}" 2>/dev/null) || return 2

  case "${code}" in
    404|410) return 1 ;;
    000)     return 2 ;;
    *)       return 0 ;;
  esac
}

# ─────────────────────────────────────────────
# Delivery
# ─────────────────────────────────────────────

_notify_via_webhook() {
  local status="$1" message="$2" url="$3"

  # "text" is what Slack and Discord require: without it an incoming webhook
  # answers 400 and the notification is dropped. The structured fields are kept
  # alongside for any receiver that reads them.
  local payload
  payload=$(printf '{"text":"[%s] MeepleAI backup on %s: %s","status":"%s","message":"%s","timestamp":"%s","host":"%s"}' \
    "$status" "$(hostname)" "$message" \
    "$status" "$message" "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" "$(hostname)")

  # --fail so an HTTP 4xx/5xx is an error: without it curl exits 0 on a rejected
  # payload and the caller believes it delivered.
  curl --silent --fail --max-time 10 \
    -H "Content-Type: application/json" \
    -d "$payload" \
    "$url" >/dev/null 2>&1
}

_notify_via_email() {
  local status="$1" message="$2"
  local to from host port
  to="$(resolve_notify_email)"
  from="${SMTP_FROM_EMAIL:-${SMTP_USER}}"
  host="${SMTP_HOST}"
  port="${SMTP_PORT:-587}"

  local body
  body="$(printf 'From: MeepleAI Backup <%s>\nTo: %s\nSubject: [%s] MeepleAI backup on %s\n\n%s\n\nHost: %s\nTime: %s\n' \
    "$from" "$to" "$status" "$(hostname)" "$message" "$(hostname)" "$(date -u '+%Y-%m-%dT%H:%M:%SZ')")"

  # --ssl-reqd rather than smtps:// so the common submission port 587 with
  # STARTTLS works; refusing to fall back to plaintext keeps the credentials off
  # the wire if the server does not offer TLS.
  printf '%s' "$body" | curl --silent --show-error --max-time 30 \
    --url "smtp://${host}:${port}" --ssl-reqd \
    --mail-from "$from" --mail-rcpt "$to" \
    --user "${SMTP_USER}:${SMTP_PASSWORD}" \
    --upload-file - >/dev/null 2>&1
}

# notify_webhook <status> <message>
# Never fails the caller: a broken notification must not destroy a good backup.
# It must, however, say out loud when it did not deliver.
notify_webhook() {
  local status="$1"
  local message="$2"
  local url
  url="$(resolve_notify_url)"

  if ! notify_has_channel; then
    _notify_log "NOT DELIVERED — no channel configured (set BACKUP_WEBHOOK_URL, SLACK_WEBHOOK_URL, or SMTP + BACKUP_ALERT_EMAIL_TO): [${status}] ${message}"
    return 0
  fi

  # Unit tests and dry runs: resolve and report, send nothing.
  if [[ -n "${BACKUP_NOTIFY_DISABLED:-}" ]]; then
    _notify_log "suppressed via BACKUP_NOTIFY_DISABLED: [${status}] ${message}"
    return 0
  fi

  if [[ -n "${url}" ]] && _notify_via_webhook "$status" "$message" "$url"; then
    return 0
  fi

  [[ -n "${url}" ]] && _notify_log "webhook delivery failed — falling back to email"

  if notify_email_configured && _notify_via_email "$status" "$message"; then
    _notify_log "delivered by email to $(resolve_notify_email): [${status}] ${message}"
    return 0
  fi

  _notify_log "NOT DELIVERED by any transport — '${status}' was lost: ${message}"
  return 0
}
