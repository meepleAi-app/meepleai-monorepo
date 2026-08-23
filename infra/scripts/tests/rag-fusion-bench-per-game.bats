#!/usr/bin/env bats
# Unit tests per il modo --per-game di rag-fusion-bench.py (#3768).
#
# Run with: bats infra/scripts/tests/rag-fusion-bench-per-game.bats
# (requires bats-core + python3)
#
# Perché esistono: il banco serve a decidere se una modifica al ranking vale un deploy, quindi
# l'errore che deve essere impossibile è che stampi numeri plausibili quando la replica NON
# riproduce il codice reale. Il payload [RAG-TUNE-GAME] porta il punteggio vero (`s`) apposta: la
# replica lo ricostruisce dai fattori e si ferma se diverge. Questi test pinnano quella guardia —
# in entrambe le direzioni — e la capacità del banco di misurare l'inversione per cui esiste.
#
# Lo scenario è quello misurato su staging per `catan-setup-it`: dentro Catan, il chunk con le
# regole di setup è rango 1 del braccio vettoriale (cosine 0.80544) e perde contro il colophon
# `catan.com` (0.77997, rango 14), perché la heading di quest'ultimo contiene il nome del gioco e
# `headingBoost` (0.15) vale ~9 volte il massimo dell'intera somma RRF (1/61 = 0.0164).

setup() {
    TMP=$(mktemp -d)
    BENCH="$BATS_TEST_DIRNAME/../rag-fusion-bench.py"
    FIXTURES="$TMP/fixtures"
    mkdir -p "$TMP/pg" "$FIXTURES"

    # Una sola query canonica: il documento atteso è quello di Catan.
    cat > "$FIXTURES/rag-canonical-queries.json" <<'JSON'
{
  "endpoint": "/api/v1/knowledge-base/ask/global",
  "topK": 3,
  "queries": [
    {
      "queryId": "catan-setup-it",
      "language": "it",
      "query": "Come si prepara il tabellone in Catan?",
      "expectedDocument": "catan_en_rulebook.pdf"
    }
  ]
}
JSON

    python3 "$BATS_TEST_DIRNAME/fixtures/make-per-game-log.py" "$TMP/pg"
}

teardown() {
    rm -rf "$TMP"
}

run_bench() {
    run python3 "$BENCH" --per-game "$TMP/pg" --fixtures "$FIXTURES" \
        --per-game-limit 1 --doc-names "$TMP/pg/names.json" "$@"
}

@test "la validazione passa quando i fattori ricostruiscono il punteggio reale" {
    run_bench
    [ "$status" -eq 0 ]
    [[ "$output" == *"divergenze: 0"* ]]
}

@test "la validazione FERMA il banco quando un punteggio non torna" {
    # Un solo `s` corrotto: se il banco stampasse comunque i conteggi, una replica divergente
    # passerebbe per autorevole — è il modo esatto in cui questa indagine ha sbagliato due volte.
    python3 - "$TMP/pg/per-game.log" <<'PY'
import io, sys
path = sys.argv[1]
lines = io.open(path, encoding="utf-8").read().splitlines()
lines[0] = lines[0].replace('"s":', '"s":9.99,"_orig_s":', 1)
io.open(path, "w", encoding="utf-8").write("\n".join(lines) + "\n")
PY
    run_bench
    [ "$status" -eq 1 ]
    [[ "$output" == *"divergenze: 1"* ]]
    [[ "$output" == *"infondati"* ]]
    [[ "$output" != *"semantico"* ]]
}

@test "com'e' oggi il colophon vince dentro Catan e il documento atteso esce dal top-3" {
    run_bench
    [ "$status" -eq 0 ]
    [[ "$output" == *"com'e' oggi: semantico 0/1"* ]]
}

@test "escludendo il nome del gioco dai termini di heading il documento atteso rientra" {
    run_bench --game-titles "$TMP/pg/titles.json"
    [ "$status" -eq 0 ]
    [[ "$output" == *"com'e' oggi: semantico 0/1"* ]]
    [[ "$output" == *"senza il nome del gioco nei termini di heading: semantico 1/1"* ]]
}

@test "i giochi senza titolo nella mappa sono dichiarati, non ignorati in silenzio" {
    # Un titolo mancante lascia quel gioco invariato: se non venisse detto, il conteggio
    # sembrerebbe misurare la variante su tutto il corpus.
    python3 - "$TMP/pg/titles.json" <<'PY'
import io, json, sys
path = sys.argv[1]
titles = json.load(io.open(path, encoding="utf-8"))
titles.pop("g-catan", None)
json.dump(titles, io.open(path, "w", encoding="utf-8"))
PY
    run_bench --game-titles "$TMP/pg/titles.json"
    [ "$status" -eq 0 ]
    [[ "$output" == *"ATTENZIONE"* ]]
    [[ "$output" == *"giochi senza titolo, lasciati invariati: 1"* ]]
}
