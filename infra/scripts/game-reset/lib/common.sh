#!/usr/bin/env bash
# Shared helpers for game-reset scripts.
# Source this file at the top of every script: source "$(dirname "$0")/lib/common.sh"

set -euo pipefail

# Logging helpers
log_info()  { printf '\033[0;36m[INFO]\033[0m  %s\n' "$*" >&2; }
log_warn()  { printf '\033[1;33m[WARN]\033[0m  %s\n' "$*" >&2; }
log_error() { printf '\033[0;31m[ERROR]\033[0m %s\n' "$*" >&2; }
log_ok()    { printf '\033[0;32m[OK]\033[0m    %s\n' "$*" >&2; }

# Load env file passed as first argument; exit if missing
load_env() {
  local env_file="${1:-}"
  if [[ -z "$env_file" ]]; then
    log_error "Usage: $0 <path-to-env-file>"
    log_error "Example: $0 infra/scripts/game-reset/.env.dev"
    exit 64  # EX_USAGE
  fi
  if [[ ! -f "$env_file" ]]; then
    log_error "Env file not found: $env_file"
    exit 66  # EX_NOINPUT
  fi
  # shellcheck disable=SC1090
  source "$env_file"

  : "${ENV_NAME:?ENV_NAME must be set in $env_file}"
  : "${DATABASE_URL:?DATABASE_URL must be set in $env_file}"
  : "${DATABASE_NAME:?DATABASE_NAME must be set in $env_file}"
  : "${BACKUP_DIR:?BACKUP_DIR must be set in $env_file}"

  mkdir -p "$BACKUP_DIR"
  log_info "Loaded env: ENV_NAME=$ENV_NAME DATABASE=$DATABASE_NAME"
}

# Refuse to run against production unless --i-mean-it is passed
guard_prod() {
  if [[ "${ENV_NAME:-}" == "prod" ]]; then
    if [[ "${1:-}" != "--i-mean-it" ]]; then
      log_error "ENV_NAME=prod detected. Refusing to run without --i-mean-it flag."
      log_error "Re-run as: $0 <env-file> --i-mean-it"
      exit 77  # EX_NOPERM
    fi
    log_warn "Running against PROD with --i-mean-it. Last chance to ctrl-c (sleeping 5s)..."
    sleep 5
  fi
}

# Standard timestamped artefact path
artefact_path() {
  local purpose="$1"  # e.g. pre-game-reset
  local ext="$2"      # e.g. dump
  local date_str
  date_str="$(date +%Y-%m-%d)"
  echo "$BACKUP_DIR/${date_str}-${ENV_NAME}-${purpose}.${ext}"
}
