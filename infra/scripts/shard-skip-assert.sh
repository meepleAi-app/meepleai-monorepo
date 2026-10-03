#!/usr/bin/env bash
# infra/scripts/shard-skip-assert.sh
# Guardia sui conteggi per shard di dev-async (#4024, spec R2/A2).
#
# I salti e i fallimenti dei test SONO GIA' stampati a ogni run: dev-async.yml grepa il trailer
# `Passed!  -  Failed: N, Passed: N, Skipped: N` e lo scrive in $GITHUB_STEP_SUMMARY per shard.
# Nessuno li confronta con un valore atteso. E' il motivo per cui lo skip di #3978 e' durato un mese:
# era osservabile e non osservato. Stampare non e' misurare.
#
# Questo script confronta i conteggi di uno shard con la baseline committata in
# fixtures/dev-async-shard-baseline.json e FALLISCE su un aumento non dichiarato, di
# fallimenti o di salti.
#
# 🔴 LA FONTE DEL CONTEGGIO, E LA TRAPPOLA CHE CONTIENE
#
# Nel .trx i salti hanno DUE rappresentazioni e una e' falsa. Misurato sui tre shard del run
# 36997819474 del 2026-10-02:
#
#   Counters/@notExecuted                         0 ·  0 · 0   <- FALSO
#   numero di <UnitTestResult outcome="NotExecuted">  45 · 11 · 8   <- VERO
#   trailer del log `Skipped: N`                     45 · 11 · 8
#
# Un gate scritto col campo apparentemente ovvio leggerebbe SEMPRE ZERO: sarebbe il gate verde e
# vuoto, costruito dentro il lavoro che dovrebbe chiuderne la famiglia. Qui il conteggio dei salti
# viene dagli ELEMENTI, e `Counters/@notExecuted` non viene nemmeno letto.
#
# Il trailer del log, quando fornito con --log, serve da CONTROPROVA: se le due fonti divergono lo
# script fallisce dicendo «fonti discordanti» invece di scegliere quale credere.
#
# Uso:
#   bash scripts/shard-skip-assert.sh --shard Core --trx path/to/integration-test-results.trx
#   bash scripts/shard-skip-assert.sh --shard Core --trx <file> --log integration-Core.log
#   bash scripts/shard-skip-assert.sh --shard Core --trx <file> --update-baseline
#
# Exit: 0 = conteggi entro la baseline (o baseline aggiornata); 1 = aumento non dichiarato,
#       fonti discordanti, o input illeggibile.
set -uo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"   # infra/
BASELINE="${SHARD_BASELINE:-$DIR/fixtures/dev-async-shard-baseline.json}"

SHARD=""
TRX=""
LOG=""
UPDATE_BASELINE=false

for arg in "$@"; do
  case "$arg" in
    --shard=*) SHARD="${arg#*=}" ;;
    --trx=*) TRX="${arg#*=}" ;;
    --log=*) LOG="${arg#*=}" ;;
    --baseline=*) BASELINE="${arg#*=}" ;;
    --update-baseline) UPDATE_BASELINE=true ;;
    --shard|--trx|--log|--baseline) PENDING="$arg" ;;
    *)
      case "${PENDING:-}" in
        --shard) SHARD="$arg"; PENDING="" ;;
        --trx) TRX="$arg"; PENDING="" ;;
        --log) LOG="$arg"; PENDING="" ;;
        --baseline) BASELINE="$arg"; PENDING="" ;;
        *) echo "::error::argomento non riconosciuto: $arg" >&2; exit 1 ;;
      esac
      ;;
  esac
done

if [ -z "$SHARD" ] || [ -z "$TRX" ]; then
  echo "::error::--shard e --trx sono obbligatori. Uso: $0 --shard Core --trx <file.trx> [--log <file.log>] [--update-baseline]" >&2
  exit 1
fi

if [ ! -f "$TRX" ]; then
  echo "::error::.trx non trovato: $TRX" >&2
  exit 1
fi

# --- conteggi dal .trx -------------------------------------------------------------------------
# `failed` dal contatore, che per i fallimenti e' corretto (verificato identico al trailer).
FAILED=$(grep -o 'failed="[0-9]*"' "$TRX" | head -1 | grep -o '[0-9]*' || true)
# `skipped` dagli ELEMENTI, mai da Counters/@notExecuted — vedi la trappola nell'header.
SKIPPED=$(grep -c 'outcome="NotExecuted"' "$TRX" || true)
FAILED="${FAILED:-}"
SKIPPED="${SKIPPED:-0}"

if [ -z "$FAILED" ]; then
  echo "::error::nessun contatore 'failed' nel .trx: $TRX — il file e' troncato o non e' un trx" >&2
  exit 1
fi

echo "[$SHARD] dal .trx: failed=$FAILED skipped=$SKIPPED"

# --- controprova dal trailer del log ------------------------------------------------------------
if [ -n "$LOG" ] && [ -f "$LOG" ]; then
  # Il trailer compare anche su una run troncata, dopo `Test Run Aborted.`; prendiamo l'ultimo.
  TRAILER=$(grep -aE '^(Passed|Failed)!  -' "$LOG" | tail -1 || true)

  if [ -z "$TRAILER" ]; then
    # Nessun trailer: test host crashato, oppure dotnet test mai partito. Non e' una discordanza,
    # e' un'assenza — e il .trx resta l'unica fonte. Lo diciamo invece di tacere.
    echo "::warning::[$SHARD] il log non ha un trailer Passed!/Failed!: controprova non disponibile (test host crashato?)"
  else
    LOG_FAILED=$(echo "$TRAILER" | grep -oE 'Failed: *[0-9]+' | grep -oE '[0-9]+' || true)
    LOG_SKIPPED=$(echo "$TRAILER" | grep -oE 'Skipped: *[0-9]+' | grep -oE '[0-9]+' || true)

    if [ -n "$LOG_FAILED" ] && [ "$LOG_FAILED" != "$FAILED" ]; then
      echo "::error::[$SHARD] fonti discordanti sui FALLIMENTI: .trx dice $FAILED, il trailer dice $LOG_FAILED. Non scelgo quale credere: va capito perche' divergono." >&2
      exit 1
    fi
    if [ -n "$LOG_SKIPPED" ] && [ "$LOG_SKIPPED" != "$SKIPPED" ]; then
      echo "::error::[$SHARD] fonti discordanti sui SALTI: .trx dice $SKIPPED, il trailer dice $LOG_SKIPPED. Non scelgo quale credere: va capito perche' divergono." >&2
      exit 1
    fi
    echo "[$SHARD] controprova dal trailer: coerente"
  fi

  if grep -qa 'Aborting test run: test run timeout' "$LOG"; then
    echo "::error::[$SHARD] run TRONCATA dal TestSessionTimeout: i conteggi sono parziali e non confrontabili con la baseline (#3633, guard di #3742)." >&2
    exit 1
  fi
fi

# --- baseline ------------------------------------------------------------------------------------
if [ "$UPDATE_BASELINE" = true ]; then
  if ! command -v jq >/dev/null 2>&1; then
    echo "::error::jq e' richiesto per --update-baseline" >&2
    exit 1
  fi
  mkdir -p "$(dirname "$BASELINE")"
  [ -f "$BASELINE" ] || echo '{"capturedAt":"","runId":"","shards":{}}' > "$BASELINE"
  tmp="$(mktemp)"
  jq --arg s "$SHARD" --argjson f "$FAILED" --argjson k "$SKIPPED" \
     '.shards[$s] = {failed: $f, skipped: $k}' "$BASELINE" > "$tmp" && mv "$tmp" "$BASELINE"
  echo "[$SHARD] baseline aggiornata: failed=$FAILED skipped=$SKIPPED"
  echo "::notice::ricorda di aggiornare anche runId e capturedAt in $BASELINE, altrimenti la baseline non dice da dove viene"
  exit 0
fi

if [ ! -f "$BASELINE" ]; then
  echo "::error::baseline non trovata: $BASELINE — creala con --update-baseline" >&2
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "::error::jq e' richiesto per leggere la baseline" >&2
  exit 1
fi

EXP_FAILED=$(jq -r --arg s "$SHARD" '.shards[$s].failed // empty' "$BASELINE")
EXP_SKIPPED=$(jq -r --arg s "$SHARD" '.shards[$s].skipped // empty' "$BASELINE")

if [ -z "$EXP_FAILED" ] || [ -z "$EXP_SKIPPED" ]; then
  echo "::error::lo shard '$SHARD' non e' nella baseline $BASELINE. Aggiungilo con --update-baseline, dichiarando da quale run viene." >&2
  exit 1
fi

echo "[$SHARD] baseline: failed=$EXP_FAILED skipped=$EXP_SKIPPED"

STATUS=0

if [ "$FAILED" -gt "$EXP_FAILED" ]; then
  echo "::error::[$SHARD] i FALLIMENTI sono aumentati: $EXP_FAILED -> $FAILED. Correggi la causa, oppure dichiara il nuovo valore nella baseline SPIEGANDO perche' nella stessa PR." >&2
  STATUS=1
fi

if [ "$SKIPPED" -gt "$EXP_SKIPPED" ]; then
  echo "::error::[$SHARD] i SALTI sono aumentati: $EXP_SKIPPED -> $SKIPPED. Un salto in piu' e' un test che non viene piu' eseguito: va dichiarato come i fallimenti. Se e' il rimedio a un fallimento, il test si e' SPOSTATO da una colonna all'altra e vanno aggiornati entrambi i valori." >&2
  STATUS=1
fi

if [ "$FAILED" -lt "$EXP_FAILED" ] || [ "$SKIPPED" -lt "$EXP_SKIPPED" ]; then
  echo "::notice::[$SHARD] i conteggi sono SCESI (failed $EXP_FAILED->$FAILED, skipped $EXP_SKIPPED->$SKIPPED): aggiorna la baseline con --update-baseline, altrimenti la prossima regressione passera' inosservata dentro il margine."
fi

[ "$STATUS" -eq 0 ] && echo "[$SHARD] conteggi entro la baseline ✓"
exit "$STATUS"
