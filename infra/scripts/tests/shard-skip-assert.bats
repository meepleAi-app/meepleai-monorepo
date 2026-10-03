#!/usr/bin/env bats
# Suite per infra/scripts/shard-skip-assert.sh — #4024, spec R2/A2.
#
# Perché questi test esistono qui e non in dev-async: osservare il gate dentro dev-async costa
# 61–110 minuti per run, il job è `failure` in ogni run che lo esegue (quindi «aggiungi uno skip e
# vedi il rosso» è già soddisfatto a codice invariato), e `cancel-in-progress: false` fa sì che solo
# l'ultimo merge ottenga un verdetto. Qui il job *Infra Scripts* di dev-fast gira su ogni PR verso
# main-dev e la prova costa secondi.

setup() {
    SCRIPT="$BATS_TEST_DIRNAME/../shard-skip-assert.sh"
    FIX="$BATS_TEST_DIRNAME/fixtures/shard-skip"
    BASELINE="$FIX/baseline.json"
}

# ─── la trappola del .trx ───────────────────────────────────────────────────────────────────────

@test "conta i salti dagli ELEMENTI, non da Counters/@notExecuted che vale 0" {
    # Il caso che un gate scritto col campo ovvio sbaglierebbe: notExecuted="0" e tre
    # <UnitTestResult outcome="NotExecuted">. La risposta giusta è 3.
    run bash "$SCRIPT" --shard Probe --trx "$FIX/trappola.trx" --baseline "$BASELINE"
    [ "$status" -eq 0 ]
    [[ "$output" == *"skipped=3"* ]]
}

@test "il campo notExecuted del .trx dice davvero 0, cioe la trappola e reale" {
    # Fissa il presupposto del test precedente: se la fixture smettesse di contenere il campo a 0,
    # quel test passerebbe senza provare niente.
    run grep -c 'notExecuted="0"' "$FIX/trappola.trx"
    [ "$status" -eq 0 ]
    [ "$output" -eq 1 ]
}

# ─── il confronto con la baseline ────────────────────────────────────────────────────────────────

@test "conteggi entro la baseline: esce 0" {
    run bash "$SCRIPT" --shard Probe --trx "$FIX/trappola.trx" --baseline "$BASELINE"
    [ "$status" -eq 0 ]
    [[ "$output" == *"entro la baseline"* ]]
}

@test "un salto in piu della baseline: FALLISCE dicendo che un test non viene piu eseguito" {
    run bash "$SCRIPT" --shard Probe --trx "$FIX/un-salto-in-piu.trx" --baseline "$BASELINE"
    [ "$status" -eq 1 ]
    [[ "$output" == *"SALTI sono aumentati"* ]]
    [[ "$output" == *"3 -> 4"* ]]
}

@test "uno shard assente dalla baseline: FALLISCE invece di passare sul vuoto" {
    run bash "$SCRIPT" --shard ShardCheNonEsiste --trx "$FIX/trappola.trx" --baseline "$BASELINE"
    [ "$status" -eq 1 ]
    [[ "$output" == *"non e' nella baseline"* ]]
}

# ─── la controprova dal trailer del log ──────────────────────────────────────────────────────────

@test "trailer coerente col .trx: la controprova passa" {
    run bash "$SCRIPT" --shard Probe --trx "$FIX/trappola.trx" --log "$FIX/coerente.log" --baseline "$BASELINE"
    [ "$status" -eq 0 ]
    [[ "$output" == *"controprova dal trailer: coerente"* ]]
}

@test "trailer discordante dal .trx: FALLISCE senza scegliere quale fonte credere" {
    run bash "$SCRIPT" --shard Probe --trx "$FIX/trappola.trx" --log "$FIX/discordante.log" --baseline "$BASELINE"
    [ "$status" -eq 1 ]
    [[ "$output" == *"fonti discordanti"* ]]
}

@test "log senza trailer: avverte che la controprova manca, e non la inventa" {
    # Test host crashato o dotnet test mai partito. Il .trx resta l'unica fonte: lo si dice.
    run bash "$SCRIPT" --shard Probe --trx "$FIX/trappola.trx" --log "$FIX/senza-trailer.log" --baseline "$BASELINE"
    [ "$status" -eq 0 ]
    [[ "$output" == *"controprova non disponibile"* ]]
}

@test "run troncata dal TestSessionTimeout: FALLISCE perche i conteggi sono parziali" {
    run bash "$SCRIPT" --shard Probe --trx "$FIX/trappola.trx" --log "$FIX/troncato.log" --baseline "$BASELINE"
    [ "$status" -eq 1 ]
    [[ "$output" == *"TRONCATA"* ]]
}

# ─── input illeggibile ───────────────────────────────────────────────────────────────────────────

@test "trx inesistente: FALLISCE invece di contare zero" {
    run bash "$SCRIPT" --shard Probe --trx "$FIX/non-esiste.trx" --baseline "$BASELINE"
    [ "$status" -eq 1 ]
    [[ "$output" == *"non trovato"* ]]
}

@test "senza --shard o --trx: FALLISCE spiegando l uso" {
    run bash "$SCRIPT" --trx "$FIX/trappola.trx" --baseline "$BASELINE"
    [ "$status" -eq 1 ]
    [[ "$output" == *"obbligatori"* ]]
}

# ─── la baseline reale committata ────────────────────────────────────────────────────────────────

@test "la baseline committata porta runId e capturedAt" {
    # Una baseline che non dice da dove viene non e' verificabile: e' il difetto della riga di prosa
    # che questo lavoro rimpiazza.
    real="$BATS_TEST_DIRNAME/../../fixtures/dev-async-shard-baseline.json"
    run jq -e '.runId != "" and .capturedAt != ""' "$real"
    [ "$status" -eq 0 ]
}

@test "la baseline committata ha i tre shard di dev-async" {
    real="$BATS_TEST_DIRNAME/../../fixtures/dev-async-shard-baseline.json"
    for shard in Core KnowledgeBase Games; do
        run jq -e --arg s "$shard" '.shards[$s].failed != null and .shards[$s].skipped != null' "$real"
        [ "$status" -eq 0 ]
    done
}
