#!/usr/bin/env bash
# Phase 1 / Step 0 of spec: pre-flight backup before game entity reset.
# Produces:
#   - <date>-<env>-pre-game-reset.dump  (pg_dump -Fc full snapshot)
#   - <date>-<env>-vector-count-pre.txt (baseline vector count for verification gate)
#
# Usage: ./01-backup.sh <env-file> [--i-mean-it]

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

load_env "${1:-}"
guard_prod "${2:-}"

dump_file=$(artefact_path "pre-game-reset" "dump")
count_file=$(artefact_path "vector-count-pre" "txt")

if [[ -f "$dump_file" ]]; then
  log_error "Refusing to overwrite existing dump: $dump_file"
  log_error "Move or delete the old file first."
  exit 73  # EX_CANTCREAT
fi

log_info "Dumping $DATABASE_NAME → $dump_file"
pg_dump --dbname="$DATABASE_URL" -Fc --no-owner --no-acl -f "$dump_file"

dump_size=$(stat -c '%s' "$dump_file" 2>/dev/null || stat -f '%z' "$dump_file")
if [[ "$dump_size" -lt 1024 ]]; then
  log_error "Dump file is suspiciously small ($dump_size bytes). Aborting."
  exit 70  # EX_SOFTWARE
fi
log_ok "Dump created: $dump_file ($(numfmt --to=iec --suffix=B "$dump_size" 2>/dev/null || echo "${dump_size}B"))"

log_info "Recording baseline vector count → $count_file"
psql --dbname="$DATABASE_URL" -t -A -c "SELECT COUNT(*) FROM pgvector_embeddings;" > "$count_file"

count=$(cat "$count_file")
log_ok "Baseline vector count: $count"

log_info "Backup complete. Artefacts:"
log_info "  $dump_file"
log_info "  $count_file"
