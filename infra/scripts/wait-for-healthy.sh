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

        # #3998 — `status=starting` oltre lo start_period non e' spiegabile da un
        # healthcheck che fallisce: quello porta a `unhealthy`. Lo stato resta
        # `starting` solo se si AZZERA, cioe' se il container riparte — e `api` porta
        # `restart: unless-stopped`. Distinguere «riparte» da «avvio lento» richiede
        # RestartCount, non i log: per cinque settimane il bake ha fallito senza che si
        # potesse dire quale dei due fosse, perche' qui c'erano trenta righe del solo
        # ULTIMO tentativo di boot — e il crash sta in quello precedente.
        echo "[wait-for-healthy] stato del container:" >&2
        docker inspect -f 'restarts={{.RestartCount}} status={{.State.Status}} started={{.State.StartedAt}} exit={{.State.ExitCode}} oom={{.State.OOMKilled}} health={{if .State.Health}}{{.State.Health.Status}} streak={{.State.Health.FailingStreak}} probes={{len .State.Health.Log}}{{else}}(nessun healthcheck){{end}}' \
            "$CONTAINER" >&2 2>/dev/null || echo "  (docker inspect non disponibile)" >&2

        # Le sonde: Docker conserva output ed exit code di ognuna. Un exit 7 di curl dice
        # «niente in ascolto», 28 «timeout scaduto», 22 «HTTP >= 400» — tre diagnosi che
        # il solo stato aggregato non distingue.
        # Docker conserva le ultime cinque sonde e non e' configurabile: si stampano
        # tutte. Nessun `tail`, perche' `.Output` di curl e' multi-riga e tagliare per
        # righe fisiche spezzerebbe la sonda a meta'.
        echo "[wait-for-healthy] sonde di health conservate da Docker:" >&2
        docker inspect -f '{{if .State.Health}}{{range .State.Health.Log}}{{println "  --" .Start "exit=" .ExitCode}}{{println .Output}}{{end}}{{end}}' \
            "$CONTAINER" >&2 2>/dev/null || true

        # Finestra ampia: se il container riparte, il boot crashato precede l'ultimo e
        # una coda corta non lo contiene mai.
        echo "[wait-for-healthy] log del container (le ultime ${HEALTH_LOG_LINES:-2000} righe):" >&2
        docker logs "$CONTAINER" --tail "${HEALTH_LOG_LINES:-2000}" >&2 2>/dev/null || true
        exit 1
    fi

    sleep 2
done
