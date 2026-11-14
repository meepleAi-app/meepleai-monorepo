#!/usr/bin/env bats
# Suite per tests/k6/utils/run-scenarios.sh — #4978.
#
# Lo script esegue gli scenari k6 del job `K6 Performance Tests` e decide l'esito. Prima ogni scenario girava
# con `k6 run … || true`, quindi nessun esito di k6 poteva rendere rosso il job. E k6 da solo non basta: un
# errore dentro un'iterazione non cambia il suo exit code. Fino a #4978 ogni GET autenticata lanciava
# `URLSearchParams is not defined`, e gli scenari uscivano con 0 senza aver chiamato un endpoint.
#
# Qui k6 è un doppio: uno script che scrive cosa gli è stato chiesto e si comporta come dice FAKE_K6_PLAN.

setup() {
    SCRIPT="$BATS_TEST_DIRNAME/../../../tests/k6/utils/run-scenarios.sh"
    WORK="$BATS_TEST_TMPDIR/k6"
    mkdir -p "$WORK/scenarios"
    for f in admin-polling user-dashboard-polling library-browsing catalog-search admin-concurrent-actions \
             rag-pipeline-isolated rag-e2e-realistic; do
        : > "$WORK/scenarios/$f.js"
    done

    # Il doppio: `k6 run --env TEST_TYPE=<tipo> <file>`. FAKE_K6_PLAN è una riga per file,
    # «<file> <exit> <stacktrace|clean>»; un file assente dal piano esce 0 senza errori.
    export K6_BIN="$BATS_TEST_TMPDIR/k6-fake"
    export FAKE_K6_CALLS="$BATS_TEST_TMPDIR/calls"
    export FAKE_K6_PLAN="$BATS_TEST_TMPDIR/plan"
    : > "$FAKE_K6_PLAN"
    cat > "$K6_BIN" <<'STUB'
#!/usr/bin/env bash
file="${@: -1}"
echo "$file $3" >> "$FAKE_K6_CALLS"
line=$(grep -F "$file " "$FAKE_K6_PLAN" || true)
code=$(echo "$line" | awk '{print $2}')
kind=$(echo "$line" | awk '{print $3}')
echo "running $file"
if [ "$kind" = "stacktrace" ]; then
  echo 'time="2026-11-14T10:10:53+01:00" level=error msg="ReferenceError: URLSearchParams is not defined" source=stacktrace'
fi
exit "${code:-0}"
STUB
    chmod +x "$K6_BIN"
    export GITHUB_STEP_SUMMARY="$BATS_TEST_TMPDIR/summary.md"
    cd "$WORK"
}

# ─── l'esito ──────────────────────────────────────────────────────────────────────────────────────

@test "tutti gli scenari escono 0 senza errori: esce 0" {
    run bash "$SCRIPT" smoke all
    [ "$status" -eq 0 ]
    [[ "$output" == *"All k6 scenarios passed"* ]]
}

@test "uno scenario che lancia un'eccezione fallisce anche se k6 esce 0" {
    # Il caso di #4978: k6 registra l'errore con source=stacktrace e passa all'iterazione successiva.
    echo "scenarios/library-browsing.js 0 stacktrace" > "$FAKE_K6_PLAN"
    run bash "$SCRIPT" smoke all
    [ "$status" -eq 1 ]
    [[ "$output" == *"::error::k6 library-browsing: exit 0, 1 stack trace(s) (script threw)"* ]]
    grep -qF '| `library-browsing` |' "$GITHUB_STEP_SUMMARY"
    grep -qF '| 0 | 1 | script threw |' "$GITHUB_STEP_SUMMARY"
}

@test "una soglia superata (exit 99) fallisce lo scenario, e gli altri girano lo stesso" {
    echo "scenarios/admin-polling.js 99 clean" > "$FAKE_K6_PLAN"
    run bash "$SCRIPT" smoke all
    [ "$status" -eq 1 ]
    [[ "$output" == *"::error::k6 admin-polling: exit 99, 0 stack trace(s) (threshold crossed)"* ]]
    [ "$(wc -l < "$FAKE_K6_CALLS")" -eq 5 ]
}

@test "un errore di k6 fuori dalle soglie (exit 107, setup) fallisce lo scenario" {
    echo "scenarios/catalog-search.js 107 clean" > "$FAKE_K6_PLAN"
    run bash "$SCRIPT" smoke all
    [ "$status" -eq 1 ]
    [[ "$output" == *"(k6 failed)"* ]]
}

# ─── cosa gira ────────────────────────────────────────────────────────────────────────────────────

@test "all esegue i cinque scenari senza i due RAG, nell'ordine, con il TEST_TYPE dato" {
    run bash "$SCRIPT" smoke all
    [ "$status" -eq 0 ]
    expected="scenarios/admin-polling.js TEST_TYPE=smoke
scenarios/user-dashboard-polling.js TEST_TYPE=smoke
scenarios/library-browsing.js TEST_TYPE=smoke
scenarios/catalog-search.js TEST_TYPE=smoke
scenarios/admin-concurrent-actions.js TEST_TYPE=smoke"
    [ "$(cat "$FAKE_K6_CALLS")" = "$expected" ]
}

@test "uno scenario RAG si può ancora chiedere per nome" {
    run bash "$SCRIPT" load rag-isolated
    [ "$status" -eq 0 ]
    [ "$(cat "$FAKE_K6_CALLS")" = "scenarios/rag-pipeline-isolated.js TEST_TYPE=load" ]
}

@test "uno scenario sconosciuto esce 1 senza chiamare k6" {
    run bash "$SCRIPT" smoke non-esiste
    [ "$status" -eq 1 ]
    [[ "$output" == *"::error::unknown k6 scenario: non-esiste"* ]]
    [ ! -e "$FAKE_K6_CALLS" ]
}

# ─── il riepilogo ─────────────────────────────────────────────────────────────────────────────────

@test "il riepilogo del passo ha una riga per scenario con la durata, e l'output resta in reports/" {
    run bash "$SCRIPT" smoke all
    [ "$status" -eq 0 ]
    [ "$(grep -c '^| `' "$GITHUB_STEP_SUMMARY")" -eq 5 ]
    grep -qE '^\| `admin-polling` \| [0-9]+s \| 0 \| 0 \| passed \|$' "$GITHUB_STEP_SUMMARY"
    [ -f reports/admin-polling.log ]
    grep -qF "running scenarios/admin-polling.js" reports/admin-polling.log
}
