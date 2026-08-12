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
write_baseline() {
    if [ -z "$1" ]; then
        cat > "$TMP/infra/fixtures/rag-golden-baseline.json" <<'JSON'
{ "schemaVersion": 1, "baseline": { "catan-setup": [ { "source": "catan.pdf", "page": 1 } ] } }
JSON
    else
        cat > "$TMP/infra/fixtures/rag-golden-baseline.json" <<JSON
{ "schemaVersion": 1, "snapshot": "$1",
  "baseline": { "catan-setup": [ { "source": "catan.pdf", "page": 1 } ] } }
JSON
    fi
}

# $1 = snapshot attualmente caricato (nessuna chiamata = .latest assente)
write_latest() {
    echo "$1" > "$TMP/data/snapshots/.latest"
}

@test "exit 3 quando la baseline è stata catturata su un altro snapshot" {
    write_baseline "meepleai_seed_20260729T070620Z_intfloat_multilingual-e5-base_9101176e9"
    write_latest   "meepleai_seed_20260809T060634Z_intfloat_multilingual-e5-base_dc83e1a4e"

    run bash "$SCRIPT"

    [ "$status" -eq 3 ]
}

@test "il messaggio di baseline scaduta nomina entrambi gli snapshot" {
    write_baseline "meepleai_seed_20260729T070620Z_intfloat_multilingual-e5-base_9101176e9"
    write_latest   "meepleai_seed_20260809T060634Z_intfloat_multilingual-e5-base_dc83e1a4e"

    run bash "$SCRIPT"

    # Chi legge deve poter decidere senza aprire i log: quale baseline, quale snapshot.
    [[ "$output" == *"20260729T070620Z"* ]]
    [[ "$output" == *"20260809T060634Z"* ]]
}

@test "una baseline scaduta non esegue le query" {
    write_baseline "meepleai_seed_20260729T070620Z_intfloat_multilingual-e5-base_9101176e9"
    write_latest   "meepleai_seed_20260809T060634Z_intfloat_multilingual-e5-base_dc83e1a4e"

    run bash "$SCRIPT"

    # Undici FAIL su undici erano il sintomo che ha sviato la diagnosi: confrontare
    # retrieval contro un corpus diverso non produce informazione, solo rumore.
    [[ "$output" != *"FAIL  catan-setup"* ]]
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
