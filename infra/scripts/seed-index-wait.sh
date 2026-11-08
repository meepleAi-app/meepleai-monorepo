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
# #4363: l'API è anche il worker della coda. Se muore, nessuno drena più processing_jobs.
API_CONTAINER="${SEED_INDEX_API_CONTAINER:-meepleai-api}"

log() { echo "[seed-index-wait] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

start=$(date +%s)
while :; do
    # Gli stati sono quelli di DocumentProcessing.Domain.Enums.JobStatus: Queued, Processing,
    # Completed, Failed, Cancelled. Lo script contava «Running» e «DeadLettered», che non esistono:
    # un job vivo risultava running=0, e uno annullato non chiudeva mai la coda (#4363).
    # Letti in una variabile: con «read < <(…)», un psql che non risponde dava EOF a read e set -e
    # fermava lo script senza un messaggio.
    counts=$(
        docker exec meepleai-postgres psql -U "$PG_USER" -d "$PG_DB" -At -F' ' -c \
        "SELECT
           COUNT(*),
           COUNT(*) FILTER (WHERE status='Completed'),
           COUNT(*) FILTER (WHERE status='Failed'),
           COUNT(*) FILTER (WHERE status='Cancelled'),
           COUNT(*) FILTER (WHERE status='Queued'),
           COUNT(*) FILTER (WHERE status='Processing')
         FROM processing_jobs;"
    ) || fail "la query su processing_jobs non risponde (meepleai-postgres): impossibile sapere lo stato della coda"
    read -r total completed failed cancelled queued processing <<< "$counts"
    terminal=$((completed + failed + cancelled))
    log "total=$total completed=$completed failed=$failed cancelled=$cancelled queued=$queued processing=$processing"

    if [ "$total" -gt 0 ] && [ "$terminal" -eq "$total" ]; then
        break
    fi

    # #4363: il 2026-10-14 l'API è morta (exit 139) e lo script ha continuato a interrogare la coda
    # per quattro ore, fino al timeout. Lo stato del container è l'unico segnale che lo distingue da
    # una coda lenta. L'exit code ha senso solo se il container non è in esecuzione.
    read -r api_status api_exit < <(
        docker inspect -f '{{.State.Status}} {{.State.ExitCode}}' "$API_CONTAINER" 2>/dev/null || echo "assente -"
    )
    if [ "$api_status" != "running" ]; then
        fail "il container $API_CONTAINER non è in esecuzione (stato $api_status, exit code $api_exit): nessuno drena più la coda, con $queued job in coda e $processing in elaborazione. Vedi i suoi log"
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

fail_count=$((failed + cancelled))
fail_pct=$(( fail_count * 100 / total ))
log "termine: $completed/$total OK, $fail_count falliti (${fail_pct}%)"

if [ "$fail_pct" -gt "$FAILURE_THRESHOLD_PCT" ] && [ "$ALLOW_PARTIAL" != "true" ]; then
    log "fail rate ${fail_pct}% > soglia ${FAILURE_THRESHOLD_PCT}%"
    # NB: pdf_documents.Id/FileName + processing_jobs.Id are PascalCase (EF default, must be quoted);
    # pdf_document_id/status/error_message/current_step are snake_case. The error text lives on
    # processing_jobs.error_message — processing_steps has NO error_message column, so the old
    # 4-column query failed with "column p.id does not exist" and silently swallowed the real cause.
    docker exec meepleai-postgres psql -U "$PG_USER" -d "$PG_DB" -c \
      "SELECT j.\"Id\", p.\"FileName\", j.current_step, j.error_message
       FROM processing_jobs j
       JOIN pdf_documents p ON p.\"Id\" = j.pdf_document_id
       WHERE j.status IN ('Failed','Cancelled')
       LIMIT 50;" >&2 || true
    fail "aborting — usa SEED_INDEX_ALLOW_PARTIAL=true per procedere comunque"
fi

log "OK"
