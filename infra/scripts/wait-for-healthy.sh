#!/usr/bin/env bash
# infra/scripts/wait-for-healthy.sh
# Attende che un container Docker raggiunga stato healthy.
# Usage: bash wait-for-healthy.sh <service-name> [timeout-seconds=120]
set -euo pipefail

SERVICE=${1:?service name required (es. api, postgres)}
TIMEOUT=${2:-120}
CONTAINER="meepleai-${SERVICE}"

start=$(date +%s)
while :; do
    status=$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$CONTAINER" 2>/dev/null || echo "missing")

    case "$status" in
        healthy)
            echo "[wait-for-healthy] $CONTAINER: healthy" >&2
            exit 0
            ;;
        running)
            # Container senza healthcheck definito — considera OK dopo 5s di stabilità
            if [ $(( $(date +%s) - start )) -gt 5 ]; then
                echo "[wait-for-healthy] $CONTAINER: running (no healthcheck defined)" >&2
                exit 0
            fi
            ;;
        missing)
            echo "[wait-for-healthy] $CONTAINER non esiste" >&2
            ;;
    esac

    if [ $(( $(date +%s) - start )) -gt "$TIMEOUT" ]; then
        echo "::error:: $CONTAINER non healthy dopo ${TIMEOUT}s (status=$status)" >&2
        docker logs "$CONTAINER" --tail 30 >&2 2>/dev/null || true
        exit 1
    fi

    sleep 2
done
