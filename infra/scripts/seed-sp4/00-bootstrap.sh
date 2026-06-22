#!/usr/bin/env bash
# 00-bootstrap.sh — Bootstrap admin login + sanity check the stack is ready.
#
# Idempotent: re-running just re-logs in (no side effects).
# Prereq: $REPO_ROOT/infra/secrets/admin.secret with INITIAL_ADMIN_EMAIL/PASSWORD.

set -euo pipefail
source "$(dirname "$(readlink -f "$0")")/lib/common.sh"

banner "00 — Bootstrap"

log "Target: $TARGET  ($API_BASE)"
log "Cookie dir: $COOKIE_DIR"
log "State file: $STATE_FILE"

admin_login
ok "Bootstrap OK — admin session ready, state file initialised"
