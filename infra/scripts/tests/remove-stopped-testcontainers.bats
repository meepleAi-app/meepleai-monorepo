#!/usr/bin/env bats
# Suite per tools/cleanup/remove-stopped-testcontainers.sh e per il suo uso in .husky/pre-push — #4310.
#
# Il pre-push rimuoveva OGNI contenitore con l'etichetta org.testcontainers=true, anche quelli in
# esecuzione. Con più checkout al lavoro sulla stessa macchina, il push di uno abbatteva il Postgres
# dei test di integrazione di tutti gli altri: decine di «Exception while reading from stream» nello
# stesso istante, che si leggono come un difetto del branch sotto test.
#
# Docker qui è finto: registra le chiamate e a «ps» risponde con gli id preparati dal test. Lo stub
# sta davanti nel PATH e lo script non antepone nulla; il primo test controlla comunque che a essere
# chiamato sia lui e non il docker vero del runner, che su ubuntu-latest esiste.

setup() {
    ROOT="$BATS_TEST_DIRNAME/../../.."
    SCRIPT="$ROOT/tools/cleanup/remove-stopped-testcontainers.sh"
    HOOK="$ROOT/.husky/pre-push"
    TMP="$(mktemp -d)"
    mkdir -p "$TMP/bin"
    export CALLS="$TMP/docker-calls.log"
    export FAKE_STOPPED_IDS=""
    cat > "$TMP/bin/docker" <<'EOF'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$CALLS"
if [ "$1" = "ps" ]; then
    printf '%s' "$FAKE_STOPPED_IDS"
fi
exit 0
EOF
    chmod +x "$TMP/bin/docker"
    export PATH="$TMP/bin:$PATH"
}

teardown() {
    rm -rf "$TMP"
}

# ─── cosa viene elencato ─────────────────────────────────────────────────────────────────────────

@test "elenca i contenitori Testcontainers FERMI: exited e dead, con l'etichetta" {
    run sh "$SCRIPT"
    [ "$status" -eq 0 ]
    [ -f "$CALLS" ]                                   # ha risposto lo stub, non un docker vero
    ps_call="$(grep '^ps ' "$CALLS")"
    [[ "$ps_call" == *"label=org.testcontainers=true"* ]]
    [[ "$ps_call" == *"status=exited"* ]]
    [[ "$ps_call" == *"status=dead"* ]]
}

@test "non elenca mai senza un filtro di stato: senza, comprenderebbe i contenitori in esecuzione" {
    export FAKE_STOPPED_IDS=$'aaa111\nbbb222\n'
    run sh "$SCRIPT"
    [ "$status" -eq 0 ]
    while IFS= read -r call; do
        case "$call" in
            ps*) [[ "$call" == *"status="* ]] ;;
        esac
    done < "$CALLS"
}

@test "non chiede i contenitori in esecuzione, e nemmeno quelli appena creati" {
    # «created» è lo stato di un contenitore fra la creazione e l'avvio: rimuoverlo abbatterebbe un
    # run che sta partendo.
    run sh "$SCRIPT"
    [ "$status" -eq 0 ]
    run grep -c '^ps ' "$CALLS"                       # l'elenco è stato chiesto: senza, il resto è vuoto
    [ "$output" -ge 1 ]
    run grep -E 'status=(running|created|restarting|paused)' "$CALLS"
    [ "$status" -ne 0 ]
}

# ─── cosa viene rimosso ──────────────────────────────────────────────────────────────────────────

@test "rimuove gli id che l'elenco filtrato restituisce, e lo dice" {
    export FAKE_STOPPED_IDS=$'aaa111\nbbb222\n'
    run sh "$SCRIPT"
    [ "$status" -eq 0 ]
    run grep -c '^rm aaa111 bbb222$' "$CALLS"
    [ "$output" -eq 1 ]
}

@test "non forza la rimozione: un contenitore ripartito nel frattempo non viene abbattuto" {
    export FAKE_STOPPED_IDS=$'aaa111\n'
    run sh "$SCRIPT"
    [ "$status" -eq 0 ]
    run grep -c '^rm ' "$CALLS"                       # la rimozione c'è stata: senza, il resto è vuoto
    [ "$output" -eq 1 ]
    run grep -E '^rm .*(-f|--force)' "$CALLS"
    [ "$status" -ne 0 ]
}

@test "nessun contenitore fermo: non chiama rm" {
    run sh "$SCRIPT"
    [ "$status" -eq 0 ]
    run grep -c '^rm' "$CALLS"
    [ "$output" -eq 0 ]
}

# ─── il pre-push ─────────────────────────────────────────────────────────────────────────────────

@test "il pre-push usa questo script" {
    run grep -c 'tools/cleanup/remove-stopped-testcontainers.sh' "$HOOK"
    [ "$status" -eq 0 ]
    [ "$output" -ge 1 ]
}

@test "il pre-push non rimuove piu ogni contenitore Testcontainers" {
    run grep -c 'docker rm -f' "$HOOK"
    [ "$output" -eq 0 ]
}

@test "il pre-push passa alla pulizia dei processi la radice del proprio checkout" {
    run grep -cE 'cleanup-test-processes\.ps1.*-RepoRoot' "$HOOK"
    [ "$status" -eq 0 ]
    [ "$output" -ge 1 ]
}
