#!/usr/bin/env bash
# frontend-shard-report.sh — esito, causa e quota del budget di ogni shard di Frontend Tests (#4117).
#
# PERCHE' ESISTE
#
# Uno shard che esaurisce `timeout-minutes` viene chiuso da GitHub come `cancelled`, non `failure`, e
# `gh pr checks` rende le due cose con la stessa parola: `fail`. La lettura naturale e' «un test e'
# rotto», e per settimane le diagnosi sono partite da li': rerun, test sospettati di essere flaky e poi
# verdi in locale, mentre la causa era il budget. L'aggregatore «Frontend Fast» diceva soltanto
# `result=cancelled`, senza la parola timeout e senza quale shard.
#
# `cancelled` ha almeno due cause che non c'entrano col codice, e GitHub le scrive nelle annotazioni
# del job:
#   - «The job has exceeded the maximum execution time of 10m0s»     → timeout del budget
#   - «Canceling since a higher priority waiting request … exists»   → un run piu' recente dello stesso
#                                                                      ramo o PR l'ha sostituito
# Questo script le legge e le nomina. Poi dice quanta parte del budget ha usato ogni shard e avvisa
# dall'80%: un margine assoluto invecchia in silenzio mentre la suite cresce, una quota stampata a ogni
# run no. Non e' un gate: il verdetto resta all'aggregatore.
#
# Il budget arriva come argomento dall'ancora YAML di `timeout-minutes` in dev-fast.yml, non da un
# numero ripetuto qui: e' gia' stato cambiato una volta, e una quota calcolata contro il valore
# vecchio mentirebbe.
#
# Uso:
#   bash infra/scripts/frontend-shard-report.sh --jobs <jobs.json> --annotations <cartella> \
#     --budget-minutes 15
#
#   --jobs          la risposta di GET /repos/{o}/{r}/actions/runs/{id}/attempts/{n}/jobs
#   --annotations   cartella con <job_id>.json = GET /repos/{o}/{r}/check-runs/{job_id}/annotations,
#                   per gli shard annullati. Un file assente non e' un errore: la causa resta «non
#                   determinata» e la durata dice se era probabilmente il budget.
#
# Se e' definita GITHUB_STEP_SUMMARY, ci scrive anche una tabella.
#
# Exit: 0 = riepilogo prodotto, qualunque sia l'esito degli shard
#       1 = non misurabile: argomenti mancanti, JSON illeggibile, nessuno shard nel run
set -euo pipefail

WARN_PERCENT=80
NAME_PREFIX='Frontend Tests (shard '

JOBS=""
ANN_DIR=""
BUDGET_MIN=""
PENDING=""
for arg in "$@"; do
  case "$arg" in
    --jobs=*) JOBS="${arg#*=}" ;;
    --annotations=*) ANN_DIR="${arg#*=}" ;;
    --budget-minutes=*) BUDGET_MIN="${arg#*=}" ;;
    --jobs|--annotations|--budget-minutes) PENDING="$arg" ;;
    *)
      case "${PENDING:-}" in
        --jobs) JOBS="$arg"; PENDING="" ;;
        --annotations) ANN_DIR="$arg"; PENDING="" ;;
        --budget-minutes) BUDGET_MIN="$arg"; PENDING="" ;;
        *) echo "::error::argomento non riconosciuto: $arg" >&2; exit 1 ;;
      esac
      ;;
  esac
done

if [ -z "$JOBS" ] || [ -z "$ANN_DIR" ] || [ -z "$BUDGET_MIN" ]; then
  echo "::error::--jobs, --annotations e --budget-minutes sono obbligatori. Uso: $0 --jobs <jobs.json> --annotations <cartella> --budget-minutes 15" >&2
  exit 1
fi
if ! [[ "$BUDGET_MIN" =~ ^[1-9][0-9]*$ ]]; then
  echo "::error::--budget-minutes deve essere un intero positivo, non \"$BUDGET_MIN\"" >&2
  exit 1
fi
if [ ! -f "$JOBS" ]; then
  echo "::error::file non trovato: $JOBS" >&2
  exit 1
fi
budget_s=$((BUDGET_MIN * 60))

# Una riga per shard concluso: id, nome, conclusione, durata in secondi (vuota se mancano gli istanti).
# Gli shard `skipped` non hanno durata ne' esito da riportare.
if ! rows=$(jq -r --arg p "$NAME_PREFIX" '
    .jobs[]
    | select(.name | startswith($p))
    | select(.conclusion != null and .conclusion != "skipped")
    | [ (.id | tostring), .name, .conclusion,
        (if .started_at != null and .completed_at != null
           then ((.completed_at | fromdate) - (.started_at | fromdate)) | tostring
           else "" end) ]
    | @tsv' "$JOBS" 2>/dev/null); then
  echo "::error::$JOBS non e' la risposta dell'API dei job di un run: JSON non valido, senza .jobs[], o con istanti non ISO 8601" >&2
  exit 1
fi
if [ -z "$rows" ]; then
  echo "::error::nessuno shard concluso con nome «${NAME_PREFIX}…» in $JOBS: o il job e' stato rinominato in dev-fast.yml, o il riepilogo e' girato prima della fine degli shard" >&2
  exit 1
fi
# Il jq di Windows chiude le righe con \r: senza toglierlo la durata non e' un numero.
rows=$(printf '%s\n' "$rows" | tr -d '\r' | sort -t$'\t' -k2,2)
expected=$(printf '%s\n' "$rows" | wc -l | tr -d ' ')

fmt() { printf '%dm %02ds' $(($1 / 60)) $(($1 % 60)); }

# Comandi di workflow (`::error title=…::messaggio`): nel messaggio vanno codificati %, \r e \n; nelle
# proprieta' anche : e , — una virgola in chiaro chiude il titolo. Visto nel primo run di prova
# (39079980679), dove «…: timeout, non un test fallito» e' arrivato come «…: timeout».
esc_data() {
  local s=${1//'%'/%25}
  s=${s//$'\r'/%0D}
  s=${s//$'\n'/%0A}
  printf '%s' "$s"
}
esc_prop() {
  local s
  s=$(esc_data "$1")
  s=${s//:/%3A}
  s=${s//,/%2C}
  printf '%s' "$s"
}
annotate() { # livello, titolo, messaggio
  printf '::%s title=%s::%s\n' "$1" "$(esc_prop "$2")" "$(esc_data "$3")"
}

# Causa di un `cancelled`, dalle annotazioni del job: timeout | sostituito | altro:<messaggio> | ignota
cause_of() {
  local f="$ANN_DIR/$1.json" msgs first
  [ -f "$f" ] || { echo ignota; return; }
  msgs=$(jq -r '.[] | select(.annotation_level == "failure") | .message' "$f" 2>/dev/null | tr -d '\r') || { echo ignota; return; }
  if grep -q 'exceeded the maximum execution time' <<<"$msgs"; then
    echo timeout
  elif grep -q 'higher priority waiting request' <<<"$msgs"; then
    echo sostituito
  else
    # «The operation was canceled.» accompagna ogni annullamento e non dice perche'.
    first=$(grep -v '^The operation was canceled\.$' <<<"$msgs" | head -1 || true)
    echo "altro:${first}"
  fi
}

summary_rows=""
reported=0
timeouts=0
slowest_pct=-1
slowest_name=""
slowest_s=0

while IFS=$'\t' read -r id name conclusion dur; do
  short="${name#"$NAME_PREFIX"}"
  short="${short%)}"
  if [ -n "$dur" ] && ! [[ "$dur" =~ ^[0-9]+$ ]]; then
    echo "::error::$name: durata non numerica («$dur») in $JOBS" >&2
    exit 1
  fi
  if [ -n "$dur" ]; then
    pct=$((dur * 100 / budget_s))
    timing="$(fmt "$dur"), ${pct}% del budget di ${BUDGET_MIN} min"
    cell_dur="$(fmt "$dur")"
    cell_pct="${pct}%"
  else
    pct=""
    timing="durata non disponibile"
    cell_dur="—"
    cell_pct="—"
  fi
  ann_level=""

  case "$conclusion" in
    success)
      label="success"
      line="$name: success — $timing"
      # Sui secondi, non sulla quota: due shard possono arrotondare alla stessa percentuale.
      if [ -n "$pct" ] && { [ "$slowest_pct" -lt 0 ] || [ "$dur" -gt "$slowest_s" ]; }; then
        slowest_pct=$pct; slowest_name=$name; slowest_s=$dur
      fi
      ;;
    failure)
      label="fallito"
      line="$name: FALLITO — $timing. Un test o un errore dentro lo shard: la causa e' nel suo log, non qui."
      ;;
    cancelled|timed_out)
      if [ "$conclusion" = timed_out ]; then cause=timeout; else cause=$(cause_of "$id"); fi
      case "$cause" in
        timeout)
          label="TIMEOUT"
          timeouts=$((timeouts + 1))
          line="$name: TIMEOUT — $timing. Ha esaurito timeout-minutes: NON e' un test fallito."
          ann_level=error
          ann_title="${name}: timeout, non un test fallito"
          ann_msg="Lo shard ha esaurito il budget di ${BUDGET_MIN} min (timeout-minutes in dev-fast.yml) ed e' stato chiuso come «cancelled», che gh pr checks mostra come «fail». Non e' il diff: un test rotto conclude «failure». Se gli altri shard sono verdi, rilancia il run; se succede spesso, il budget va rivisto (#4117)."
          ;;
        sostituito)
          label="sostituito"
          line="$name: annullato — $timing. Un run piu' recente dello stesso ramo o PR l'ha sostituito (concurrency): non e' un esito del codice."
          ann_level=notice
          ann_title="${name}: sostituito"
          ann_msg="Annullato da un run piu' recente dello stesso ramo o PR (cancel-in-progress). Conta l'esito di quel run."
          ;;
        altro:*)
          label="annullato"
          detail="${cause#altro:}"
          line="$name: annullato — $timing. Causa riportata da GitHub: ${detail:-nessuna oltre a «The operation was canceled.»}"
          ;;
        *)
          if [ -n "$dur" ] && [ "$dur" -ge "$budget_s" ]; then
            label="probabile timeout"
            timeouts=$((timeouts + 1))
            line="$name: PROBABILE TIMEOUT — $timing. Le annotazioni non sono leggibili, ma la durata ha raggiunto il budget: non e' un test fallito."
            ann_level=error
            ann_title="${name}: probabile timeout"
            ann_msg="Annotazioni del job non leggibili, ma la durata ha raggiunto il budget di ${BUDGET_MIN} min. Un test rotto conclude «failure», non «cancelled» (#4117)."
          else
            label="annullato"
            line="$name: annullato — $timing. Causa non determinata: annotazioni del job non leggibili, e la durata e' sotto il budget."
          fi
          ;;
      esac
      ;;
    *)
      label="$conclusion"
      line="$name: $conclusion — $timing"
      ;;
  esac

  echo "$line"
  if [ -n "$ann_level" ]; then annotate "$ann_level" "$ann_title" "$ann_msg"; fi
  summary_rows+="| ${short} | ${label} | ${cell_dur} | ${cell_pct} |"$'\n'
  reported=$((reported + 1))
done <<<"$rows"

# Un errore di espansione dentro il ciclo lo interrompe e bash prosegue dopo `done`, anche con set -e:
# il riepilogo uscirebbe parziale con exit 0. Visto alla prima stesura, con una durata che finiva in \r.
if [ "$reported" -ne "$expected" ]; then
  echo "::error::riepilogo incompleto: riportati $reported shard su $expected" >&2
  exit 1
fi

if [ "$slowest_pct" -ge 0 ]; then
  echo "Shard verde piu' lento: ${slowest_name}, $(fmt "$slowest_s") = ${slowest_pct}% del budget di ${BUDGET_MIN} min (avviso dall'${WARN_PERCENT}%)."
  if [ "$slowest_pct" -ge "$WARN_PERCENT" ]; then
    annotate warning "Frontend Tests: margine sotto il $((100 - WARN_PERCENT))%" \
      "${slowest_name} ha usato il ${slowest_pct}% del budget di ${BUDGET_MIN} min ed e' passato. Con il runner carico la stessa suite dura 2-3 minuti in piu' per shard (#4117): al 100% lo shard viene chiuso come «cancelled». Il budget va rivisto, o il costo per file ridotto, prima che succeda."
  fi
fi

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "### Frontend Tests — esito degli shard (#4117)"
    echo
    echo "| shard | esito | durata | quota del budget di ${BUDGET_MIN} min |"
    echo "|---|---|---|---|"
    printf '%s' "$summary_rows"
    echo
    if [ "$timeouts" -gt 0 ]; then
      echo "**Timeout, non test falliti**: uno shard chiuso per budget esaurito risulta «cancelled», e \`gh pr checks\` lo mostra come «fail»."
      echo
    fi
    if [ "$slowest_pct" -ge 0 ]; then
      echo "Shard verde piu' lento: ${slowest_pct}% del budget (avviso dall'${WARN_PERCENT}%)."
    fi
  } >> "$GITHUB_STEP_SUMMARY"
fi
