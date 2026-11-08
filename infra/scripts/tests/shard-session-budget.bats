#!/usr/bin/env bats
# Suite per infra/scripts/shard-session-budget.sh — #3742.
#
# Lo script dice quanta parte del TestSessionTimeout ha usato la sessione di uno shard, e avvisa
# prima che il limite venga raggiunto. Serve perche' la troncatura non arriva all'improvviso: lo
# shard Games e' stato per settimane a pochi minuti dal limite, verde sui runner veloci, e nessun
# numero lo diceva finche' un runner lento non lo ha superato.

setup() {
    SCRIPT="$BATS_TEST_DIRNAME/../shard-session-budget.sh"
    FIX="$BATS_TEST_DIRNAME/fixtures/shard-budget"
    LIMIT="$FIX/limite-75.runsettings"
}

# ─── la misura ───────────────────────────────────────────────────────────────────────────────────

@test "dice durata, limite e quota: 36 minuti su 75 sono il 48%" {
    run bash "$SCRIPT" --shard Catalog --trx "$FIX/36-minuti.trx" --runsettings "$LIMIT"
    [ "$status" -eq 0 ]
    [[ "$output" == *"[Catalog] sessione: 36 min su 75 (48% del TestSessionTimeout)"* ]]
    [[ "$output" != *"::warning::"* ]]
}

@test "il limite viene dal runsettings, non da un numero nello script" {
    # Gli stessi 36 minuti contro un limite di 60 sono il 60%. Se lo script ripetesse 75, questo
    # test resterebbe a 48: e il giorno in cui il limite cambia nel runsettings la quota mentirebbe.
    run bash "$SCRIPT" --shard Catalog --trx "$FIX/36-minuti.trx" --runsettings "$FIX/limite-60.runsettings"
    [ "$status" -eq 0 ]
    [[ "$output" == *"36 min su 60 (60% del TestSessionTimeout)"* ]]
}

@test "una sessione a cavallo della mezzanotte si misura lo stesso" {
    run bash "$SCRIPT" --shard Core --trx "$FIX/61-minuti.trx" --runsettings "$LIMIT"
    [ "$status" -eq 0 ]
    [[ "$output" == *"sessione: 61 min su 75 (81%"* ]]
}

# ─── l'avviso ────────────────────────────────────────────────────────────────────────────────────

@test "sotto l'80% non avvisa: 59 minuti e 59 secondi sono il 79%" {
    run bash "$SCRIPT" --shard KnowledgeBase --trx "$FIX/59-minuti-59.trx" --runsettings "$LIMIT"
    [ "$status" -eq 0 ]
    [[ "$output" == *"(79% del TestSessionTimeout)"* ]]
    [[ "$output" != *"::warning::"* ]]
}

@test "all'80% esatto avvisa" {
    run bash "$SCRIPT" --shard KnowledgeBase --trx "$FIX/60-minuti.trx" --runsettings "$LIMIT"
    [ "$status" -eq 0 ]
    [[ "$output" == *"::warning::[KnowledgeBase]"* ]]
    [[ "$output" == *"80%"* ]]
}

@test "oltre l'80% avvisa, nominando lo shard e la issue" {
    run bash "$SCRIPT" --shard KnowledgeBase --trx "$FIX/61-minuti.trx" --runsettings "$LIMIT"
    [ "$status" -eq 0 ]
    [[ "$output" == *"::warning::[KnowledgeBase]"* ]]
    [[ "$output" == *"81%"* ]]
    [[ "$output" == *"#3742"* ]]
}

@test "una sessione troncata e' al 100% e avvisa, ma non e' lo script a far fallire il job" {
    # La troncatura la segnala e la rende rossa il passo Gate. Questo script riporta soltanto.
    run bash "$SCRIPT" --shard Games --trx "$FIX/75-minuti.trx" --runsettings "$LIMIT"
    [ "$status" -eq 0 ]
    [[ "$output" == *"(100% del TestSessionTimeout)"* ]]
    [[ "$output" == *"::warning::"* ]]
}

# ─── cio' che non si puo' misurare va detto ──────────────────────────────────────────────────────

@test "un .trx senza inizio e fine: esce 1 e lo dice" {
    run bash "$SCRIPT" --shard Core --trx "$FIX/senza-times.trx" --runsettings "$LIMIT"
    [ "$status" -eq 1 ]
    [[ "$output" == *"::error::"* ]]
}

@test "un runsettings senza TestSessionTimeout: esce 1 e lo dice" {
    run bash "$SCRIPT" --shard Core --trx "$FIX/36-minuti.trx" --runsettings "$FIX/senza-limite.runsettings"
    [ "$status" -eq 1 ]
    [[ "$output" == *"TestSessionTimeout"* ]]
}

@test "senza argomenti obbligatori: esce 1" {
    run bash "$SCRIPT" --shard Core
    [ "$status" -eq 1 ]
}

@test "file inesistenti: esce 1" {
    run bash "$SCRIPT" --shard Core --trx "$FIX/non-esiste.trx" --runsettings "$LIMIT"
    [ "$status" -eq 1 ]
    run bash "$SCRIPT" --shard Core --trx "$FIX/36-minuti.trx" --runsettings "$FIX/non-esiste.runsettings"
    [ "$status" -eq 1 ]
}

# ─── il file vero ────────────────────────────────────────────────────────────────────────────────

@test "il runsettings vero dei test di integrazione porta un limite che lo script legge" {
    # Fissa il contratto col file che il workflow passa davvero: se TestSessionTimeout cambiasse
    # forma o sparisse da li', in dev-async il margine diventerebbe «non calcolabile» in silenzio.
    real="$BATS_TEST_DIRNAME/../../../apps/api/tests/Api.Tests/integration.runsettings"
    run bash "$SCRIPT" --shard Core --trx "$FIX/36-minuti.trx" --runsettings "$real"
    [ "$status" -eq 0 ]
    [[ "$output" == *"sessione: 36 min su "* ]]
}
