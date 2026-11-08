#!/usr/bin/env bash
# shard-session-budget.sh — quanta parte del TestSessionTimeout ha usato la sessione di uno shard
# dei test di integrazione (#3742).
#
# PERCHE' ESISTE
#
# Quando una sessione supera il TestSessionTimeout viene abortita, e i test non ancora partiti non
# girano. Non succede all'improvviso: lo shard Games di dev-async e' stato per settimane a pochi
# minuti dal limite — 55-59 minuti su 75 sui runner veloci, verde — finche' i runner lenti, dove
# ogni test dura circa il 45% in piu', non lo hanno superato tre volte su sette in un giorno. Prima
# di allora nessun numero diceva quanto margine restava: lo si poteva solo ricavare dal log.
#
# Questo script lo dice a ogni run, e avvisa quando la sessione supera l'80% del limite. Non e' un
# gate: riporta. La troncatura vera la rileva e la rende rossa il passo Gate di dev-async.yml.
#
# Il limite lo legge dal runsettings, non da un numero ripetuto qui: e' gia' stato alzato una volta
# (da 45 a 75 minuti), e una quota calcolata contro il valore vecchio mentirebbe in silenzio.
#
# Uso:
#   bash infra/scripts/shard-session-budget.sh --shard Core \
#     --trx <file.trx> --runsettings apps/api/tests/Api.Tests/integration.runsettings
#
# Exit: 0 = misurato (con o senza avviso)
#       1 = non misurabile: argomenti mancanti, file assenti, .trx senza inizio e fine,
#           runsettings senza TestSessionTimeout
set -euo pipefail

WARN_PERCENT=80

SHARD=""
TRX=""
RUNSETTINGS=""
PENDING=""
for arg in "$@"; do
  case "$arg" in
    --shard=*) SHARD="${arg#*=}" ;;
    --trx=*) TRX="${arg#*=}" ;;
    --runsettings=*) RUNSETTINGS="${arg#*=}" ;;
    --shard|--trx|--runsettings) PENDING="$arg" ;;
    *)
      case "${PENDING:-}" in
        --shard) SHARD="$arg"; PENDING="" ;;
        --trx) TRX="$arg"; PENDING="" ;;
        --runsettings) RUNSETTINGS="$arg"; PENDING="" ;;
        *) echo "::error::argomento non riconosciuto: $arg" >&2; exit 1 ;;
      esac
      ;;
  esac
done

if [ -z "$SHARD" ] || [ -z "$TRX" ] || [ -z "$RUNSETTINGS" ]; then
  echo "::error::--shard, --trx e --runsettings sono obbligatori. Uso: $0 --shard Core --trx <file.trx> --runsettings <file.runsettings>" >&2
  exit 1
fi
for f in "$TRX" "$RUNSETTINGS"; do
  if [ ! -f "$f" ]; then
    echo "::error::file non trovato: $f" >&2
    exit 1
  fi
done

# --- il limite -----------------------------------------------------------------------------------
limit_ms=$(sed -n 's/.*<TestSessionTimeout>\([0-9]\{1,\}\)<\/TestSessionTimeout>.*/\1/p' "$RUNSETTINGS" | head -1)
if [ -z "$limit_ms" ] || [ "$limit_ms" -le 0 ]; then
  echo "::error::[$SHARD] nessun TestSessionTimeout in $RUNSETTINGS: il margine della sessione non e' calcolabile" >&2
  exit 1
fi

# --- inizio e fine della sessione ----------------------------------------------------------------
# Dal .trx: <Times creation=… queuing=… start=… finish=… />. `start` precede `creation`, perche' il
# file dei risultati nasce a sessione avviata; la durata che conta e' finish - start.
times=$(grep -o '<Times [^>]*>' "$TRX" | head -1 || true)
start=$(printf '%s' "$times" | sed -n 's/.* start="\([^"]*\)".*/\1/p')
finish=$(printf '%s' "$times" | sed -n 's/.* finish="\([^"]*\)".*/\1/p')
if [ -z "$start" ] || [ -z "$finish" ]; then
  echo "::error::[$SHARD] il .trx non porta inizio e fine della sessione (<Times start= finish=>): $TRX" >&2
  exit 1
fi

# Le frazioni di secondo hanno sette cifre e `date` non le accetta ovunque: non servono, si tolgono.
to_epoch() { date -u -d "$(printf '%s' "$1" | sed 's/\.[0-9]*//')" +%s; }
if ! start_s=$(to_epoch "$start") || ! finish_s=$(to_epoch "$finish"); then
  echo "::error::[$SHARD] date della sessione illeggibili nel .trx: start=\"$start\" finish=\"$finish\"" >&2
  exit 1
fi

elapsed_s=$((finish_s - start_s))
if [ "$elapsed_s" -lt 0 ]; then
  echo "::error::[$SHARD] la sessione finisce prima di cominciare: start=\"$start\" finish=\"$finish\"" >&2
  exit 1
fi

minutes=$((elapsed_s / 60))
limit_min=$((limit_ms / 60000))
percent=$((elapsed_s * 100000 / limit_ms))

echo "[$SHARD] sessione: ${minutes} min su ${limit_min} (${percent}% del TestSessionTimeout)"

if [ "$percent" -ge "$WARN_PERCENT" ]; then
  echo "::warning::[$SHARD] la sessione ha usato il ${percent}% del TestSessionTimeout (${minutes} min su ${limit_min}). Al 100% viene abortita e i test non ancora partiti non girano: e' il difetto di #3742. Su un runner lento ogni test dura circa il 45% in piu', quindi il margine vero e' minore di quello che questo run mostra. Lo shard va alleggerito o diviso prima che succeda."
fi
