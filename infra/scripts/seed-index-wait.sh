#!/usr/bin/env bash
# infra/scripts/seed-index-wait.sh
# Polla processing_jobs finché tutti terminal, con timeout e fail-fast.
set -euo pipefail

TIMEOUT_SECONDS=${SEED_INDEX_TIMEOUT:-10800}        # 3h
POLL_INTERVAL=${SEED_INDEX_POLL:-15}                 # 15s
FAILURE_THRESHOLD_PCT=${SEED_INDEX_FAILURE_PCT:-15}  # 15%
ALLOW_PARTIAL=${SEED_INDEX_ALLOW_PARTIAL:-false}
PG_USER="${POSTGRES_USER:-meepleai}"
PG_DB="${POSTGRES_DB:-meepleai_staging}"

log() { echo "[seed-index-wait] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

start=$(date +%s)
while :; do
    read -r total completed failed dlq queued running < <(
        docker exec meepleai-postgres psql -U "$PG_USER" -d "$PG_DB" -At -F' ' -c \
        "SELECT
           COUNT(*),
           COUNT(*) FILTER (WHERE status='Completed'),
           COUNT(*) FILTER (WHERE status='Failed'),
           COUNT(*) FILTER (WHERE status='DeadLettered'),
           COUNT(*) FILTER (WHERE status='Queued'),
           COUNT(*) FILTER (WHERE status='Running')
         FROM processing_jobs;"
    )
    terminal=$((completed + failed + dlq))
    log "total=$total completed=$completed failed=$failed dlq=$dlq queued=$queued running=$running"

    if [ "$total" -gt 0 ] && [ "$terminal" -eq "$total" ]; then
        break
    fi

    if [ $(( $(date +%s) - start )) -gt "$TIMEOUT_SECONDS" ]; then
        echo "::error:: timeout dopo ${TIMEOUT_SECONDS}s — processing_jobs non drenata" >&2
        exit 124
    fi

    sleep "$POLL_INTERVAL"
done

if [ "$total" -eq 0 ]; then
    log "WARNING: processing_jobs vuota — nessun PDF processato"
    exit 0
fi

fail_count=$((failed + dlq))
fail_pct=$(( fail_count * 100 / total ))
log "termine: $completed/$total OK, $fail_count falliti (${fail_pct}%)"

if [ "$fail_pct" -gt "$FAILURE_THRESHOLD_PCT" ] && [ "$ALLOW_PARTIAL" != "true" ]; then
    log "fail rate ${fail_pct}% > soglia ${FAILURE_THRESHOLD_PCT}%"
    docker exec meepleai-postgres psql -U "$PG_USER" -d "$PG_DB" -c \
      "SELECT j.id, p.file_name, s.error_message
       FROM processing_jobs j
       JOIN pdf_documents p ON p.id = j.pdf_document_id
       LEFT JOIN processing_steps s ON s.processing_job_id = j.id AND s.status='Failed'
       WHERE j.status IN ('Failed','DeadLettered')
       LIMIT 50;" >&2 || true
    fail "aborting — usa SEED_INDEX_ALLOW_PARTIAL=true per procedere comunque"
fi

log "OK"
