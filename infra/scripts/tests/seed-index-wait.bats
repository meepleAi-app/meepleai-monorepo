#!/usr/bin/env bats
# Suite per infra/scripts/seed-index-wait.sh — #4363.
#
# Il bake dello snapshot attendeva la coda di processing_jobs per ore dopo che l'API, che è anche il
# worker della coda, era morta (exit 139 su maracaibo_rulebook.pdf): «queued=24 running=0» ogni 30
# secondi fino al timeout di 20000 s. E «running» era sempre 0 anche con un job vivo, perché lo
# script contava uno stato che il dominio non usa.
#
# Docker qui è finto: a «exec … psql» risponde con i conteggi preparati dal test, a «inspect» con lo
# stato del container dell'API, e registra le chiamate. Lo stub sta davanti nel PATH e lo script non
# antepone nulla; i test controllano comunque che a rispondere sia lui e non il docker del runner.

setup() {
    ROOT="$BATS_TEST_DIRNAME/../../.."
    SCRIPT="$ROOT/infra/scripts/seed-index-wait.sh"
    TMP="$(mktemp -d)"
    mkdir -p "$TMP/bin"
    export CALLS="$TMP/docker-calls.log"
    export FAKE_COUNTS="0 0 0 0 0 0"
    export FAKE_API_STATE="running 0"
    cat > "$TMP/bin/docker" <<'EOF'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$CALLS"
case "$1" in
    exec) printf '%s\n' "$FAKE_COUNTS" ;;
    inspect) printf '%s\n' "$FAKE_API_STATE" ;;
esac
exit 0
EOF
    chmod +x "$TMP/bin/docker"
    export PATH="$TMP/bin:$PATH"
    export SEED_INDEX_POLL=1
    export SEED_INDEX_TIMEOUT=3600
}

teardown() {
    rm -rf "$TMP"
}

@test "con l'API morta fallisce subito, dicendo stato ed exit code, invece di aspettare il timeout" {
    export FAKE_COUNTS="127 102 0 0 24 0"
    export FAKE_API_STATE="exited 139"

    # Il tetto di 15 s è del test: senza il controllo, lo script aspetterebbe un'ora.
    run timeout 15 bash "$SCRIPT"

    [ -f "$CALLS" ]                                   # ha risposto lo stub, non un docker vero
    [ "$status" -ne 0 ]
    [ "$status" -ne 124 ]                             # non è il timeout, né quello del test né il suo
    [[ "$output" == *"meepleai-api"* ]]
    [[ "$output" == *"exited"* ]]
    [[ "$output" == *"139"* ]]
}

@test "con l'API viva e la coda drenata finisce bene" {
    export FAKE_COUNTS="3 3 0 0 0 0"

    run timeout 15 bash "$SCRIPT"

    [ -f "$CALLS" ]
    [ "$status" -eq 0 ]
}

@test "conta gli stati che il dominio usa: Processing in corso, Cancelled terminale" {
    export FAKE_COUNTS="3 3 0 0 0 0"

    run timeout 15 bash "$SCRIPT"

    [ "$status" -eq 0 ]
    query="$(cat "$CALLS")"                       # la query va su più righe
    [[ "$query" == *"status='Processing'"* ]]
    [[ "$query" == *"status='Cancelled'"* ]]
    [[ "$query" != *"status='Running'"* ]]
    [[ "$query" != *"status='DeadLettered'"* ]]
}

@test "un job annullato è terminale, e conta fra i falliti" {
    # 3 job: 2 completati e 1 annullato. Il quarto conteggio è quello degli annullati (vedi la query
    # nel test precedente): chiudono la coda e contano nella soglia dei falliti.
    export FAKE_COUNTS="3 2 0 1 0 0"
    export SEED_INDEX_FAILURE_PCT=50

    run timeout 15 bash "$SCRIPT"

    [ "$status" -eq 0 ]
    [[ "$output" == *"1 falliti"* ]]
}
