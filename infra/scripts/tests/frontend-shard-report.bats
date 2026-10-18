#!/usr/bin/env bats
# Suite per infra/scripts/frontend-shard-report.sh — #4117.
#
# Lo script dice, per ogni shard di Frontend Tests, l'esito, la causa di un `cancelled` e quanta
# parte del budget ha usato. Serve perche' uno shard chiuso per timeout conclude `cancelled`, e
# `gh pr checks` lo mostra come `fail`: per settimane le diagnosi sono partite da un test rotto che
# non c'era.
#
# Le fixture timeout, sostituito, fallito e verde sono risposte vere dell'API (run 38772184870,
# 38638306599, 38851277548, 38859015099), ridotte ai campi che lo script legge. Le altre sono
# costruite a mano, e lo dicono i test che le usano.

setup() {
    SCRIPT="$BATS_TEST_DIRNAME/../frontend-shard-report.sh"
    FIX="$BATS_TEST_DIRNAME/fixtures/frontend-shard-report"
    NO_ANN="$BATS_TEST_TMPDIR/senza-annotazioni"
    mkdir -p "$NO_ANN"
    WORKFLOW="$BATS_TEST_DIRNAME/../../../.github/workflows/dev-fast.yml"
}

report() { # fixture, cartella annotazioni, budget
    run bash "$SCRIPT" --jobs "$FIX/$1.jobs.json" --annotations "$2" --budget-minutes "$3"
}

# ─── la causa di un cancelled ────────────────────────────────────────────────────────────────────

@test "un timeout e' nominato come tale, con lo shard, e non come un test fallito" {
    report timeout "$FIX/timeout.annotations" 10
    [ "$status" -eq 0 ]
    [[ "$output" == *"Frontend Tests (shard 1/4): TIMEOUT — 10m 12s, 102% del budget di 10 min"* ]]
    [[ "$output" == *"NON e' un test fallito"* ]]
    [[ "$output" == *"::error title=Frontend Tests (shard 1/4)%3A timeout%2C non un test fallito::"* ]]
    # Gli altri tre shard del run erano verdi, e restano tali.
    [ "$(grep -c ': success — ' <<<"$output")" -eq 3 ]
}

@test "uno shard sostituito da un run piu' recente non e' un timeout ne' un errore" {
    # Tre shard annullati dal cancel-in-progress, a 7-8 minuti: sotto il budget, e la causa e'
    # un'altra. Chiamarli timeout farebbe alzare un budget che non c'entra.
    report sostituito "$FIX/sostituito.annotations" 10
    [ "$status" -eq 0 ]
    [ "$(grep -c "l'ha sostituito (concurrency)" <<<"$output")" -eq 3 ]
    [[ "$output" != *"TIMEOUT"* ]]
    [[ "$output" != *"::error"* ]]
    [[ "$output" == *"::notice title=Frontend Tests (shard 2/4)%3A sostituito::"* ]]
}

@test "la causa viene dalle annotazioni, non dalla durata: un sostituito vicino al budget resta sostituito" {
    # Lo shard 1/4 di quel run e' durato 7m 48s. Con un budget di 8 minuti la durata sarebbe al 97%,
    # e una regola basata sui tempi lo scambierebbe per un timeout.
    report sostituito "$FIX/sostituito.annotations" 8
    [ "$status" -eq 0 ]
    [[ "$output" == *"Frontend Tests (shard 1/4): annullato — 7m 48s, 97% del budget di 8 min. Un run piu' recente"* ]]
    [[ "$output" != *"TIMEOUT"* ]]
}

@test "un'altra causa di annullamento viene riportata con il messaggio di GitHub (fixture costruita)" {
    # Il messaggio dell'annullamento manuale e' scritto a mano: il test fissa che lo script stampi
    # la causa che trova, scartando la riga generica che accompagna ogni annullamento.
    report annullato-altro "$FIX/annullato-altro.annotations" 10
    [ "$status" -eq 0 ]
    [[ "$output" == *"Frontend Tests (shard 1/2): annullato — 2m 00s, 20% del budget di 10 min. Causa riportata da GitHub: The run was canceled by @maintainer."* ]]
    [[ "$output" != *"::error"* ]]
}

@test "senza annotazioni, una durata che raggiunge il budget e' un probabile timeout" {
    report timeout "$NO_ANN" 10
    [ "$status" -eq 0 ]
    [[ "$output" == *"Frontend Tests (shard 1/4): PROBABILE TIMEOUT — 10m 12s"* ]]
    [[ "$output" == *"::error title=Frontend Tests (shard 1/4)%3A probabile timeout::"* ]]
}

@test "senza annotazioni e sotto il budget, la causa resta non determinata e non accusa nessuno" {
    report timeout "$NO_ANN" 15
    [ "$status" -eq 0 ]
    [[ "$output" == *"Frontend Tests (shard 1/4): annullato — 10m 12s, 68% del budget di 15 min. Causa non determinata"* ]]
    [[ "$output" != *"TIMEOUT"* ]]
    [[ "$output" != *"::error"* ]]
}

@test "uno shard fallito resta un fallimento, e lo script rimanda al suo log" {
    report fallito "$NO_ANN" 10
    [ "$status" -eq 0 ]
    [[ "$output" == *"Frontend Tests (shard 3/4): FALLITO — 5m 41s"* ]]
    [[ "$output" == *"la causa e' nel suo log"* ]]
    [[ "$output" != *"TIMEOUT"* ]]
}

@test "i titoli delle annotazioni non contengono virgole, due punti o percentuali in chiaro" {
    # Nei comandi di workflow una virgola in chiaro chiude la proprieta' title: nel run di prova
    # 39079980679 il titolo «…: timeout, non un test fallito» e' arrivato come «…: timeout».
    report timeout "$FIX/timeout.annotations" 10
    [ "$status" -eq 0 ]
    titles=$(grep -oE '^::(error|warning|notice) title=[^:]*::' <<<"$output" | sed -E 's/^::[a-z]+ title=//; s/::$//')
    [ "$(wc -l <<<"$titles")" -eq 2 ]   # il timeout dello shard 1/4 e l'avviso sullo shard 4/4
    # Niente `! grep`: in bats una pipeline negata non fa fallire il test.
    stripped=$(sed 's/%[0-9A-F][0-9A-F]//g' <<<"$titles")
    [[ "$stripped" != *","* ]]
    [[ "$stripped" != *"%"* ]]
    # E il messaggio non porta un % che GitHub leggerebbe come inizio di una codifica.
    warn=$(grep -E '^::warning' <<<"$output" | sed 's/%[0-9A-F][0-9A-F]//g')
    [ -n "$warn" ]
    [[ "$warn" != *"%"* ]]
}

# ─── la quota del budget ─────────────────────────────────────────────────────────────────────────

@test "dice la quota di ogni shard e quella dello shard verde piu' lento" {
    report verde "$NO_ANN" 10
    [ "$status" -eq 0 ]
    [[ "$output" == *"Frontend Tests (shard 3/4): success — 7m 16s, 72% del budget di 10 min"* ]]
    [[ "$output" == *"Shard verde piu' lento: Frontend Tests (shard 3/4), 7m 16s = 72% del budget di 10 min"* ]]
    [[ "$output" != *"::warning"* ]]
}

@test "il budget viene dall'argomento, non da un numero nello script" {
    # Gli stessi 7m 16s contro 15 minuti sono il 48%. Se lo script ripetesse un budget, la quota
    # resterebbe quella di prima il giorno in cui timeout-minutes cambia in dev-fast.yml.
    report verde "$NO_ANN" 15
    [ "$status" -eq 0 ]
    [[ "$output" == *"7m 16s = 48% del budget di 15 min"* ]]
}

@test "il job dell'aggregatore ancora in corso e i job che non sono shard non entrano nel riepilogo" {
    report verde "$NO_ANN" 10
    [ "$status" -eq 0 ]
    [ "$(grep -c '^Frontend Tests (shard ' <<<"$output")" -eq 4 ]
    [[ "$output" != *"Frontend Fast"* ]]
    [[ "$output" != *"Frontend Static"* ]]
}

@test "all'80% esatto avvisa (fixture costruita: 8m 00s su 10 min)" {
    report soglia-80 "$NO_ANN" 10
    [ "$status" -eq 0 ]
    [[ "$output" == *"::warning title=Frontend Tests%3A margine sotto il 20%25::Frontend Tests (shard 1/2) ha usato il 80%25"* ]]
}

@test "sotto l'80% non avvisa (fixture costruita: 7m 59s su 10 min sono il 79%)" {
    report soglia-79 "$NO_ANN" 10
    [ "$status" -eq 0 ]
    [[ "$output" == *"7m 59s = 79% del budget"* ]]
    [[ "$output" != *"::warning"* ]]
}

@test "un verde al 91% avvisa anche se il run ha un timeout altrove: e' il caso del run 38772184870" {
    report timeout "$FIX/timeout.annotations" 10
    [ "$status" -eq 0 ]
    [[ "$output" == *"::warning title=Frontend Tests%3A margine sotto il 20%25::Frontend Tests (shard 4/4) ha usato il 91%25"* ]]
}

@test "con GITHUB_STEP_SUMMARY scrive la tabella, con la riga del timeout" {
    export GITHUB_STEP_SUMMARY="$BATS_TEST_TMPDIR/summary.md"
    report timeout "$FIX/timeout.annotations" 10
    [ "$status" -eq 0 ]
    grep -qF '| shard | esito | durata | quota del budget di 10 min |' "$GITHUB_STEP_SUMMARY"
    grep -qF '| 1/4 | TIMEOUT | 10m 12s | 102% |' "$GITHUB_STEP_SUMMARY"
    grep -qF '| 4/4 | success | 9m 11s | 91% |' "$GITHUB_STEP_SUMMARY"
    grep -qF 'Timeout, non test falliti' "$GITHUB_STEP_SUMMARY"
}

# ─── cio' che non si puo' riportare va detto ─────────────────────────────────────────────────────

@test "nessuno shard nel run: esce 1 e dice che il job puo' essere stato rinominato" {
    report senza-shard "$NO_ANN" 10
    [ "$status" -eq 1 ]
    [[ "$output" == *"::error::nessuno shard concluso"* ]]
    [[ "$output" == *"rinominato in dev-fast.yml"* ]]
}

@test "una risposta che non e' JSON: esce 1 e lo dice" {
    report non-json "$NO_ANN" 10
    [ "$status" -eq 1 ]
    [[ "$output" == *"::error::"*"non e' la risposta dell'API dei job"* ]]
}

@test "argomenti mancanti o budget non intero: esce 1" {
    run bash "$SCRIPT" --jobs "$FIX/verde.jobs.json" --annotations "$NO_ANN"
    [ "$status" -eq 1 ]
    [[ "$output" == *"obbligatori"* ]]
    report verde "$NO_ANN" 10m
    [ "$status" -eq 1 ]
    [[ "$output" == *"intero positivo"* ]]
}

# ─── il cablaggio in dev-fast.yml ────────────────────────────────────────────────────────────────

@test "dev-fast.yml: il budget degli shard e quello del riepilogo sono lo stesso valore" {
    # Un numero ripetuto in due punti diverge al primo cambio: la quota verrebbe calcolata contro il
    # budget vecchio. L'ancora YAML lo tiene in un posto solo.
    grep -qE '^    timeout-minutes: &frontend-tests-budget [0-9]+$' "$WORKFLOW"
    grep -qE '^          BUDGET_MINUTES: \*frontend-tests-budget$' "$WORKFLOW"
}

@test "dev-fast.yml: il nome del job degli shard comincia con il prefisso che lo script cerca" {
    # Rinominare il job senza toccare lo script lascerebbe il riepilogo senza shard: esce 1, e
    # l'aggregatore lo riduce a un avviso. Meglio accorgersene prima del merge.
    prefix=$(sed -n "s/^NAME_PREFIX='\(.*\)'$/\1/p" "$SCRIPT")
    [ -n "$prefix" ]
    grep -qF "    name: ${prefix}\${{ matrix.shard }}/4)" "$WORKFLOW"
}

@test "dev-fast.yml: l'aggregatore puo' leggere i job e le annotazioni del run" {
    # Senza actions: read l'elenco dei job non si legge; senza checks: read le annotazioni, che sono
    # l'unico posto in cui GitHub distingue un timeout da una sostituzione.
    block=$(awk '/^  frontend-fast:$/ {on=1; next} on && /^  [a-z]/ {exit} on' "$WORKFLOW")
    grep -qE '^      actions: read$' <<<"$block"
    grep -qE '^      checks: read$' <<<"$block"
    grep -qF 'infra/scripts/frontend-shard-report.sh' <<<"$block"
}
