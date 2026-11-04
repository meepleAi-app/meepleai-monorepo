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
#
# Nota per chi aggiunge fixture: .gitignore esclude *.log, quindi le fixture di log in
# scripts/tests/fixtures/ usano l estensione .logfixture — altrimenti non vengono committate e
# la suite bats fallisce in CI per file mancanti, non per un difetto dello script.
#   bash scripts/shard-skip-assert.sh --shard Core --trx <file> --update-baseline
#
# 🔴 I NOMI, NON SOLO I CONTEGGI (#4298)
#
# Un conteggio non distingue «gli stessi due fallimenti di ieri» da «uno e' guarito e ne e' comparso
# un altro»: il numero resta due. Finche' il job di dev-async era rosso comunque, il buco non
# costava nulla. Da quando il colore del job lo decide QUESTO script, e' il verde a dover escludere
# lo scambio — altrimenti una regressione passa dentro una baseline rispettata.
#
# Per questo la baseline porta, accanto a `failed` e `skipped`, i nomi: `failedTests` e
# `skippedTests`, come `classe.test`. La classe serve: nel .trx `testName` e' il solo nome del
# metodo, e lo stesso nome ricorre in classi diverse. Un fallito o un saltato che non e' nella lista
# e' un errore anche a conteggio invariato; uno in lista che non fallisce piu' e' un avviso.
# Uno shard senza nomi viene confrontato sui soli conteggi, e lo script lo DICE.
#
# Exit: 0 = entro la baseline (o baseline aggiornata)
#       1 = REGRESSIONE o input inutilizzabile: fallito o saltato non dichiarato, aumento dei
#           conteggi, fonti discordanti, baseline incoerente, .trx illeggibile, shard assente
#       2 = NON MISURABILE: la run e' stata troncata dal TestSessionTimeout, quindi i conteggi sono
#           parziali e un confronto con la baseline non significherebbe nulla
#
# 🔴 Perche' 2 e non 1 (#4036). «Non ho potuto misurare» e «ho misurato una regressione» sono due
# cose diverse, e confonderle rende il segnale inutile: lo shard Games sfora il TestSessionTimeout
# in modo cronico (causa documentata in #3742: GroupA vuota e GroupB con una classe sola, quindi una
# catena seriale di 113 classi in GroupC), quindi con un solo codice d'uscita resterebbe rosso per
# sempre per una ragione che NON e' un salto in piu' — e chi legge imparerebbe a ignorarlo.
# La troncatura ha gia' il suo guardiano in dev-async.yml, che fa fallire il job: questo script la
# segnala e si ferma, senza rivendicare un confronto che non ha fatto.
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

# --- nomi dal .trx -------------------------------------------------------------------------------
# Stampa `classe.testName`, uno per riga e ordinati, dei test con l'esito chiesto.
#
# Nel .trx il risultato e la sua classe stanno in due punti: <UnitTestResult testId testName
# outcome> sotto <Results>, e sotto <TestDefinitions> un <UnitTest id> seguito dal suo
# <TestMethod className>. Li unisce il testId. Le entita' XML vengono decodificate, cosi' la riga
# di una Theory si scrive nella baseline con le virgolette e non con &quot;.
names_with_outcome() {
  awk -v want="$1" '
    function attr(line, name,    re, v) {
      re = " " name "=\"[^\"]*\""
      if (!match(line, re)) return ""
      v = substr(line, RSTART, RLENGTH)
      sub("^ " name "=\"", "", v)
      sub("\"$", "", v)
      return v
    }
    /<UnitTestResult / {
      if (attr($0, "outcome") == want) { n++; rid[n] = attr($0, "testId"); rname[n] = attr($0, "testName") }
      next
    }
    /<UnitTest /   { cur = attr($0, "id"); next }
    /<TestMethod / { if (cur != "") { cls[cur] = attr($0, "className"); cur = "" } next }
    END {
      for (i = 1; i <= n; i++) {
        c = cls[rid[i]]
        print (c == "" ? rname[i] : c "." rname[i])
      }
    }' "$TRX" \
    | sed -e 's/&quot;/"/g' -e "s/&apos;/'/g" -e 's/&lt;/</g' -e 's/&gt;/>/g' -e 's/&amp;/\&/g' \
    | LC_ALL=C sort
}

FAILED_NAMES="$(names_with_outcome Failed)"
SKIPPED_NAMES="$(names_with_outcome NotExecuted)"

# I nomi e i conteggi vengono dallo stesso file per due strade. Se non tornano, l'estrazione dei
# nomi ha perso qualcosa (un .trx di forma diversa?) e una lista piu' corta farebbe passare per
# «guarito» cio' che non e' stato letto: meglio fermarsi che confrontare una lista monca.
count_lines() { if [ -z "$1" ]; then echo 0; else printf '%s\n' "$1" | wc -l | tr -d ' '; fi; }
if [ "$(count_lines "$FAILED_NAMES")" != "$FAILED" ] || [ "$(count_lines "$SKIPPED_NAMES")" != "$SKIPPED" ]; then
  echo "::error::[$SHARD] nomi e conteggi del .trx non tornano: failed=$FAILED ma $(count_lines "$FAILED_NAMES") nomi, skipped=$SKIPPED ma $(count_lines "$SKIPPED_NAMES") nomi. L'estrazione dei nomi non ha letto tutto: non confronto una lista monca." >&2
  exit 1
fi

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
    # Exit 2 = NON MISURABILE, non «regressione»: vedi la nota in testa. Il fallimento del job per
    # la troncatura lo decide dev-async.yml, che ha il suo guardiano (#3742); qui ci si ferma prima
    # di confrontare, perche' confrontare conteggi parziali con una baseline e' l'errore vero.
    echo "::warning::[$SHARD] run TRONCATA dal TestSessionTimeout: i conteggi sono PARZIALI, quindi il confronto con la baseline NON e' stato eseguito. Non e' una regressione dei salti: e' l'impossibilita' di misurarli (#3742). Finche' dura, una regressione nei salti di questo shard puo' restare invisibile, perche' il test che la rivelerebbe puo' non essere stato eseguito." >&2
    exit 2
  fi
else
  # 🔴 Senza il log la troncatura NON e' rilevabile: il .trx di una run abortita ha la stessa forma
  # di quello di una run completa, solo con meno elementi. Verificato sullo shard Games del run
  # 36997819474, dove l'invocazione col solo .trx dice «conteggi entro la baseline» su una run che
  # si era fermata a 773 test su timeout. Dirlo, invece di far credere che il confronto sia completo.
  echo "::warning::[$SHARD] nessun --log: la troncatura della run non e' verificabile, quindi questo confronto NON garantisce di aver misurato la selezione intera. In CI passa sempre --log."
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
  # `tr -d '\r'`: su Windows jq scrive CRLF, e un nome con un \r in coda non e' piu' lo stesso nome.
  failed_json="$(printf '%s' "$FAILED_NAMES" | jq -R . | jq -s . | tr -d '\r')"
  skipped_json="$(printf '%s' "$SKIPPED_NAMES" | jq -R . | jq -s . | tr -d '\r')"
  # `+` e non `=`: lo shard tiene cio' che aveva — la nota `_comment`, che dice da dove vengono i
  # numeri — e cambiano solo le quattro chiavi misurate. Con `=` la nota spariva a ogni ricattura.
  jq --arg s "$SHARD" --argjson f "$FAILED" --argjson k "$SKIPPED" \
     --argjson fn "$failed_json" --argjson kn "$skipped_json" \
     '.shards[$s] = ((.shards[$s] // {}) + {failed: $f, skipped: $k, failedTests: $fn, skippedTests: $kn})' \
     "$BASELINE" > "$tmp" && mv "$tmp" "$BASELINE"
  echo "[$SHARD] baseline aggiornata: failed=$FAILED skipped=$SKIPPED, con i nomi"
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

# --- il confronto per nome (#4298) ---------------------------------------------------------------
# $1 = chiave nella baseline, $2 = nomi misurati, $3 = conteggio dichiarato,
# $4 = etichetta per i non dichiarati, $5 = frase per i dichiarati che non lo sono piu'.
compare_names() {
  local key="$1" actual="$2" expected_count="$3" label="$4" gone_phrase="$5"

  if [ "$(jq -r --arg s "$SHARD" --arg k "$key" '.shards[$s] | has($k)' "$BASELINE" | tr -d '\r')" != "true" ]; then
    echo "::warning::[$SHARD] baseline senza nomi ($key): confronto solo i conteggi, e uno scambio — un test dichiarato che guarisce e uno nuovo che prende il suo posto — NON lo vedrei. Ricattura con --update-baseline."
    return 0
  fi

  local declared declared_count
  declared="$(jq -r --arg s "$SHARD" --arg k "$key" '.shards[$s][$k][]' "$BASELINE" | tr -d '\r' | LC_ALL=C sort)"
  declared_count="$(count_lines "$declared")"
  if [ "$declared_count" != "$expected_count" ]; then
    echo "::error::[$SHARD] baseline incoerente: il conteggio dichiarato e' $expected_count ma $key ha $declared_count nomi. Una delle due cose e' falsa: ricattura lo shard con --update-baseline." >&2
    return 1
  fi

  local undeclared gone rc=0
  undeclared="$(comm -13 <(printf '%s\n' "$declared") <(printf '%s\n' "$actual") | sed '/^$/d')"
  gone="$(comm -23 <(printf '%s\n' "$declared") <(printf '%s\n' "$actual") | sed '/^$/d')"

  if [ -n "$undeclared" ]; then
    echo "::error::[$SHARD] $label non dichiarati nella baseline: $(printf '%s' "$undeclared" | paste -sd ';' - | sed 's/;/; /g'). Vale anche a conteggio invariato: un numero uguale non dice che i test siano gli stessi. Correggi la causa, oppure dichiarali nella baseline SPIEGANDO perche' nella stessa PR." >&2
    rc=1
  fi
  if [ -n "$gone" ]; then
    echo "::notice::[$SHARD] dichiarati nella baseline ma $gone_phrase: $(printf '%s' "$gone" | paste -sd ';' - | sed 's/;/; /g'). Toglili con --update-baseline, altrimenti il loro posto resta libero per una regressione."
  fi
  return "$rc"
}

compare_names failedTests "$FAILED_NAMES" "$EXP_FAILED" "FALLIMENTI" "non falliscono piu'" || STATUS=1
compare_names skippedTests "$SKIPPED_NAMES" "$EXP_SKIPPED" "SALTI" "ora vengono eseguiti" || STATUS=1

[ "$STATUS" -eq 0 ] && echo "[$SHARD] entro la baseline ✓"
exit "$STATUS"
