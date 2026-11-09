#!/usr/bin/env bats
# Unit tests for rag-smoke-assert.sh — riconoscimento della baseline scaduta (#3645).
#
# Run with: bats infra/scripts/tests/rag-smoke-assert.bats
# (requires bats-core: https://github.com/bats-core/bats-core)
#
# Perché esistono: il gate settimanale è stato rosso dal 2026-07-20 al 2026-08-10 con
# `0 passed, 11 failed`, e l'issue che apriva da solo diceva «retrieval drift». Non lo era:
# la baseline era stata catturata sullo snapshot 20260729T070620Z e il run girava su
# 20260809T060634Z. L'harness scriveva `snapshot` nella baseline senza mai rileggerlo, quindi
# non poteva distinguere un re-bake legittimo da una regressione — e chiamava il primo col
# nome della seconda.
#
# I test montano una copia dello script in una gerarchia temporanea (infra/scripts + infra/fixtures
# + data/snapshots) così da esercitare il percorso reale di risoluzione dei path senza toccare
# le fixture del repo.

setup() {
    TMP=$(mktemp -d)
    mkdir -p "$TMP/infra/scripts" "$TMP/infra/fixtures" "$TMP/data/snapshots"
    cp "$BATS_TEST_DIRNAME/../rag-smoke-assert.sh" "$TMP/infra/scripts/rag-smoke-assert.sh"
    SCRIPT="$TMP/infra/scripts/rag-smoke-assert.sh"

    cat > "$TMP/infra/fixtures/rag-canonical-queries.json" <<'JSON'
{
  "endpoint": "/api/v1/knowledge-base/ask/global",
  "topK": 3,
  "language": "en",
  "queries": [
    { "queryId": "catan-setup", "query": "How do you set up Catan?" }
  ]
}
JSON

    # Porta chiusa: se lo script supera la guardia arriva alla curl e fallisce subito,
    # senza attese di rete. È così che distinguiamo "bloccato dalla guardia" da "proseguito".
    export API_BASE_URL="http://127.0.0.1:9"
}

teardown() {
    rm -rf "$TMP"
}

# $1 = snapshot registrato nella baseline (stringa vuota = campo assente)
# Formato v2 (#3666): le entry sono {document,page}. La chiave è il NOME del documento, che
# sopravvive al re-bake, non `pdf_documents.Id`, che è un Guid.NewGuid() rigenerato a ogni ingest.
write_baseline() {
    if [ -z "$1" ]; then
        cat > "$TMP/infra/fixtures/rag-golden-baseline.json" <<'JSON'
{ "schemaVersion": 2, "baseline": { "catan-setup": [ { "document": "catan_rulebook.pdf", "page": 1 } ] } }
JSON
    else
        cat > "$TMP/infra/fixtures/rag-golden-baseline.json" <<JSON
{ "schemaVersion": 2, "snapshot": "$1",
  "baseline": { "catan-setup": [ { "document": "catan_rulebook.pdf", "page": 1 } ] } }
JSON
    fi
}

# Baseline nel vecchio formato: chiave fisica `source` = pdf_documents.Id.
write_v1_baseline() {
    cat > "$TMP/infra/fixtures/rag-golden-baseline.json" <<'JSON'
{ "schemaVersion": 1, "snapshot": "meepleai_seed_20260729T070620Z_intfloat_multilingual-e5-base_9101176e9",
  "baseline": { "catan-setup": [ { "source": "8d0a2f6f-aa45-4d01-982e-218d47badd79", "page": 1 } ] } }
JSON
}

# $1 = snapshot attualmente caricato (nessuna chiamata = .latest assente)
write_latest() {
    echo "$1" > "$TMP/data/snapshots/.latest"
}

@test "v2: uno snapshot diverso non blocca più il confronto" {
    write_baseline "meepleai_seed_20260729T070620Z_intfloat_multilingual-e5-base_9101176e9"
    write_latest   "meepleai_seed_20260809T060634Z_intfloat_multilingual-e5-base_dc83e1a4e"

    run bash "$SCRIPT"

    # Il cuore di #3666: con la chiave stabile un re-bake non invalida più la baseline, quindi
    # la guardia exit 3 di #3645 non ha più ragione di scattare. Lo script prosegue fino alle
    # query e fallisce sulla rete (nessuna API in ascolto) → exit 1, non 3.
    [ "$status" -eq 1 ]
}

@test "v2: lo snapshot diverso è segnalato, non taciuto" {
    write_baseline "meepleai_seed_20260729T070620Z_intfloat_multilingual-e5-base_9101176e9"
    write_latest   "meepleai_seed_20260809T060634Z_intfloat_multilingual-e5-base_dc83e1a4e"

    run bash "$SCRIPT"

    # Non blocca, ma chi legge un fallimento deve sapere su quale corpus era stata catturata
    # la baseline: senza questo, il notice varrebbe quanto il silenzio.
    [[ "$output" == *"20260729T070620Z"* ]]
    [[ "$output" == *"20260809T060634Z"* ]]
    [[ "$output" == *"::notice::"* ]]
}

@test "v1: la baseline nel vecchio formato esce 3 e non esegue le query" {
    write_v1_baseline
    write_latest "meepleai_seed_20260809T060634Z_intfloat_multilingual-e5-base_dc83e1a4e"

    run bash "$SCRIPT"

    # Una baseline v1 pinna pdf_documents.Id: confrontarla non produce informazione. Va
    # rigenerata UNA volta (non a ogni bake, che era il difetto), e il gate deve dirlo invece
    # di riportare undici FAIL come faceva prima di #3645.
    [ "$status" -eq 3 ]
    [[ "$output" != *"FAIL  catan-setup"* ]]
}

@test "v1: il messaggio spiega che la rigenerazione è una tantum" {
    write_v1_baseline

    run bash "$SCRIPT"

    # Chi lo legge deve capire che non è la toil ricorrente di prima.
    [[ "$output" == *"v1"* ]]
    [[ "$output" == *"--update-baseline"* ]]
}

@test "snapshot coincidente: la guardia non blocca" {
    write_baseline "meepleai_seed_20260809T060634Z_intfloat_multilingual-e5-base_dc83e1a4e"
    write_latest   "meepleai_seed_20260809T060634Z_intfloat_multilingual-e5-base_dc83e1a4e"

    run bash "$SCRIPT"

    # Prosegue fino alle query (che falliscono: nessuna API in ascolto) → exit 1, non 3.
    [ "$status" -eq 1 ]
}

@test ".latest assente: la guardia non blocca" {
    write_baseline "meepleai_seed_20260729T070620Z_intfloat_multilingual-e5-base_9101176e9"

    run bash "$SCRIPT"

    # Eseguire contro un'API remota senza snapshot locale è legittimo: uno stato non
    # conoscibile non va trasformato in un fallimento.
    [ "$status" -eq 1 ]
}

@test "baseline senza campo snapshot: la guardia non blocca" {
    write_baseline ""
    write_latest   "meepleai_seed_20260809T060634Z_intfloat_multilingual-e5-base_dc83e1a4e"

    run bash "$SCRIPT"

    # Baseline anteriori all'introduzione del campo non devono diventare rosse.
    [ "$status" -eq 1 ]
}

# --- criterio semantico (#3740) -----------------------------------------------------------
# Non esercitano lo script (servirebbe un'API viva) ma l'integrita' del fixture da cui il
# criterio dipende. Servono perche' il modo in cui questo meccanismo muore in silenzio e'
# preciso: si aggiunge una query senza `expectedDocument`, il conteggio smette di coprirla,
# e il pavimento continua a passare su una popolazione piu' piccola. E' la stessa forma di
# difetto del cluster dei gate che non esaminano niente.

@test "semantico: ogni query canonica dichiara expectedDocument" {
    fixture="$BATS_TEST_DIRNAME/../../fixtures/rag-canonical-queries.json"
    missing=$(jq -r '[.queries[] | select(has("expectedDocument") | not) | .queryId] | join(", ")' "$fixture")
    [ -z "$missing" ] || {
        echo "query senza expectedDocument: $missing" >&2
        return 1
    }
}

@test "semantico: il pavimento esiste e non supera il numero di query" {
    fixture="$BATS_TEST_DIRNAME/../../fixtures/rag-canonical-queries.json"
    floor=$(jq -r '.semanticFloor // empty' "$fixture")
    [ -n "$floor" ]
    total=$(jq -r '[.queries[] | select(.expectedDocument)] | length' "$fixture")
    [ "$floor" -le "$total" ]
    [ "$floor" -ge 0 ]
}

@test "semantico: expectedDocument non punta a un file di un altro gioco" {
    # 7-wonders-duel e wingspan-asia sono giochi DIVERSI da 7 Wonders e Wingspan: il criterio
    # largo li accettava per via del prefisso comune, ed e' cosi' che seven-wonders-military
    # risultava corretta pur recuperando tre volte su tre il manuale di un altro gioco.
    fixture="$BATS_TEST_DIRNAME/../../fixtures/rag-canonical-queries.json"
    bad=$(jq -r '[.queries[] | select(.expectedDocument | test("-duel|-asia|-prosperity")) | .queryId] | join(", ")' "$fixture")
    [ -z "$bad" ] || {
        echo "expectedDocument punta a un'espansione o a un gioco diverso: $bad" >&2
        return 1
    }
}

# --- cattura e criterio semantico, script vero (#4965) -------------------------------------
# Questi arrivano al ciclo delle query: una curl finta nel PATH risponde alla mappa documenti
# (GET /admin/pdfs) e a ogni domanda con una citazione SSE del documento in FAKE_CITED. Servono
# perché il difetto stava proprio lì: in cattura lo script usciva con `exit 0` prima del criterio
# semantico, quindi un dispatch con update_baseline=true era verde a qualunque conteggio — i due
# di #4959 non avevano una riga `semantico:` nel log.

write_semantic_fixture() {
    cat > "$TMP/infra/fixtures/rag-canonical-queries.json" <<'JSON'
{
  "endpoint": "/api/v1/knowledge-base/ask/global",
  "topK": 3,
  "language": "en",
  "semanticFloor": 1,
  "queries": [
    { "queryId": "catan-setup", "query": "How do you set up Catan?", "expectedDocument": "catan_rulebook.pdf" }
  ]
}
JSON
}

# $1 = documento che la baseline pinna per catan-setup
write_named_baseline() {
    cat > "$TMP/infra/fixtures/rag-golden-baseline.json" <<JSON
{ "schemaVersion": 2, "snapshot": "",
  "baseline": { "catan-setup": [ { "document": "$1", "page": 1 } ] } }
JSON
}

install_fake_curl() {
    mkdir -p "$TMP/bin"
    cat > "$TMP/bin/curl" <<'SH'
#!/usr/bin/env bash
for arg in "$@"; do
  case "$arg" in
    *admin/pdfs*)
      echo '{"items":[{"id":"doc-catan","fileName":"catan_rulebook.pdf"},{"id":"doc-other","fileName":"other_rulebook.pdf"}]}'
      exit 0 ;;
  esac
done
printf 'data: {"type":1,"data":{"citations":[{"source":"%s","page":1,"score":0.9}]}}\n' "$FAKE_CITED"
SH
    chmod +x "$TMP/bin/curl"
    export PATH="$TMP/bin:$PATH"
    unset SMOKE_EMAIL SMOKE_PASSWORD TEST_EMAIL TEST_PASSWORD
}

@test "cattura: il criterio semantico compare nel log e la baseline viene scritta (#4965)" {
    write_semantic_fixture
    write_named_baseline "previous_rulebook.pdf"
    install_fake_curl
    export FAKE_CITED=doc-catan

    run bash "$SCRIPT" --update-baseline

    [ "$status" -eq 0 ]
    [[ "$output" == *"semantico: 1/1"* ]]
    [ "$(jq -r '.baseline["catan-setup"][0].document' "$TMP/infra/fixtures/rag-golden-baseline.json")" = "catan_rulebook.pdf" ]
}

@test "cattura sotto il pavimento: uscita 1 e la baseline resta quella di prima (#4965)" {
    write_semantic_fixture
    write_named_baseline "previous_rulebook.pdf"
    cp "$TMP/infra/fixtures/rag-golden-baseline.json" "$TMP/before.json"
    install_fake_curl
    export FAKE_CITED=doc-other

    run bash "$SCRIPT" --update-baseline

    [ "$status" -eq 1 ]
    [[ "$output" == *"semantico: 0/1"* ]]
    [[ "$output" == *"baseline NOT written"* ]]
    cmp "$TMP/before.json" "$TMP/infra/fixtures/rag-golden-baseline.json"
}

@test "assert: stesso esito e stessa riga semantica di prima (#4965)" {
    write_semantic_fixture
    write_named_baseline "catan_rulebook.pdf"
    install_fake_curl
    export FAKE_CITED=doc-catan

    run bash "$SCRIPT"

    [ "$status" -eq 0 ]
    [[ "$output" == *"PASS  catan-setup"* ]]
    [[ "$output" == *"result: 1 passed, 0 failed"* ]]
    [[ "$output" == *"semantico: 1/1"* ]]
}

@test "assert: sotto il pavimento resta rosso anche se la baseline combacia (#4965)" {
    write_semantic_fixture
    write_named_baseline "other_rulebook.pdf"
    install_fake_curl
    export FAKE_CITED=doc-other

    run bash "$SCRIPT"

    [ "$status" -eq 1 ]
    [[ "$output" == *"PASS  catan-setup"* ]]
    [[ "$output" == *"Criterio semantico sceso a 0/1"* ]]
}
