#!/usr/bin/env bash
# infra/scripts/wait-for-healthy.sh
# Attende che un container Docker raggiunga stato healthy.
# Usage: bash wait-for-healthy.sh <service-name> [timeout-seconds=120]
#
# Variabili opzionali:
#   HEALTH_LOG_LINES   righe di `docker logs` nel dump di fallimento (default 2000)
set -euo pipefail

SERVICE=${1:?service name required (es. api, postgres)}
TIMEOUT=${2:-120}
CONTAINER="meepleai-${SERVICE}"

# #3998 — tutto cio' che serve a distinguere «avvio lento» da «riparte in loop» da «morto».
# Per cinque settimane il bake e' fallito stampando trenta righe dell'ULTIMO tentativo di
# boot, e il crash stava in quello precedente: nessuno dei log permetteva di dire quale dei
# tre casi fosse.
diagnose() {
    echo "[wait-for-healthy] stato del container:" >&2
    docker inspect -f 'restarts={{.RestartCount}} status={{.State.Status}} started={{.State.StartedAt}} finished={{.State.FinishedAt}} exit={{.State.ExitCode}} oom={{.State.OOMKilled}} health={{if .State.Health}}{{.State.Health.Status}} streak={{.State.Health.FailingStreak}} probes={{len .State.Health.Log}}{{else}}(nessun healthcheck){{end}}' \
        "$CONTAINER" >&2 2>/dev/null || echo "  (docker inspect non disponibile)" >&2

    # Le sonde: Docker conserva output ed exit code di ognuna. Un exit 7 di curl dice
    # «niente in ascolto», 28 «timeout scaduto», 22 «HTTP >= 400» — tre diagnosi che il
    # solo stato aggregato non distingue. Ne conserva cinque e non e' configurabile, quindi
    # si stampano tutte: nessun `tail`, perche' `.Output` di curl e' multi-riga e tagliare
    # per righe fisiche spezzerebbe una sonda a meta'.
    echo "[wait-for-healthy] sonde di health conservate da Docker:" >&2
    docker inspect -f '{{if .State.Health}}{{range .State.Health.Log}}{{println "  --" .Start "exit=" .ExitCode}}{{println .Output}}{{end}}{{end}}' \
        "$CONTAINER" >&2 2>/dev/null || true

    # Finestra ampia: se il container riparte, il boot crashato precede l'ultimo e una coda
    # corta non lo contiene mai.
    echo "[wait-for-healthy] log del container (le ultime ${HEALTH_LOG_LINES:-2000} righe):" >&2
    docker logs "$CONTAINER" --tail "${HEALTH_LOG_LINES:-2000}" >&2 2>/dev/null || true

    # #3998 — un `exit=139` e' un SIGSEGV, e i log non bastano a localizzarlo: in `Program.cs`
    # il tratto fra la fine del seeding e `RunAsync()` non logga nulla per costruzione, quindi
    # l'ultima riga scritta non dice dove l'esecuzione sia arrivata.
    #
    # Con `DOTNET_EnableCrashReport=1` (vedi infra/compose.bake.yml) il runtime scrive accanto
    # al dump un `.crashreport.json` con lo stack di ogni thread in chiaro. Il container e'
    # `exited`, quindi `docker exec` non lo raggiunge ma `docker cp` si'.
    echo "[wait-for-healthy] crash report del runtime .NET:" >&2
    crashdir=$(mktemp -d)
    if docker cp "$CONTAINER:/tmp/." "$crashdir" >/dev/null 2>&1; then
        found=0
        for report in "$crashdir"/*.crashreport.json; do
            [ -f "$report" ] || continue
            found=1
            echo "  -- $(basename "$report")" >&2
            # jq c'e' sui runner GitHub; in locale puo' mancare, e allora si stampa il json
            # grezzo invece di non stampare niente.
            jq -r '.payload.threads[]? | "  thread \(.native_thread_id // "?"):",
                   (.stack_frames[]? | "    \(.module_name // "?")!\(.method_name // "?")")' \
                "$report" 2>/dev/null >&2 || cat "$report" >&2
        done
        if [ "$found" = 0 ]; then
            echo "  (nessun crash report: il processo non e' stato terminato da un segnale," >&2
            echo "   oppure createdump non ha potuto scrivere — serve cap_add: SYS_PTRACE)" >&2
        fi
    else
        echo "  (docker cp non ha potuto leggere /tmp dal container)" >&2
    fi
    rm -rf "$crashdir"
}

start=$(date +%s)
while :; do
    # #3998 — stato del CONTAINER e stato dell'HEALTHCHECK, letti separatamente.
    # La forma precedente era `{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}`:
    # quando l'healthcheck esiste restituisce l'health e **nasconde** lo stato del container,
    # quindi un container `exited` appariva col suo ultimo valore di health. E' il motivo per
    # cui il bake riportava `status=starting` mentre il container ripartiva tredici volte.
    state=$(docker inspect -f '{{.State.Status}}' "$CONTAINER" 2>/dev/null || echo "missing")
    health=$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$CONTAINER" 2>/dev/null || echo "none")

    case "$state" in
        running)
            if [ "$health" = "healthy" ]; then
                echo "[wait-for-healthy] $CONTAINER: healthy" >&2
                exit 0
            fi
            if [ "$health" = "none" ]; then
                # Container senza healthcheck definito — considera OK dopo 5s di stabilita'
                if [ $(( $(date +%s) - start )) -gt 5 ]; then
                    echo "[wait-for-healthy] $CONTAINER: running (no healthcheck defined)" >&2
                    exit 0
                fi
            fi
            ;;
        exited | dead)
            # Fail-fast: il container e' morto e non sta ripartendo. Aspettare il timeout non
            # aggiunge informazione, la ritarda — e qui `ExitCode`, `FinishedAt` e `OOMKilled`
            # sono finalmente significativi, mentre su un container `running` descrivono una
            # terminazione passata e non il processo vivo.
            echo "::error:: $CONTAINER e' in stato '$state' e non ripartira': non diventera' healthy" >&2
            diagnose
            exit 1
            ;;
        missing)
            echo "[wait-for-healthy] $CONTAINER non esiste" >&2
            ;;
    esac

    if [ $(( $(date +%s) - start )) -gt "$TIMEOUT" ]; then
        echo "::error:: $CONTAINER non healthy dopo ${TIMEOUT}s (container=$state health=$health)" >&2
        diagnose
        exit 1
    fi

    sleep 2
done
