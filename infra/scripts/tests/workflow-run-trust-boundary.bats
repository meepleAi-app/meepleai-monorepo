#!/usr/bin/env bats
# Ogni workflow attivato da `workflow_run` controlla che la testa sia di QUESTO repository (#4969).
#
# Run with: bats infra/scripts/tests/workflow-run-trust-boundary.bats
#
# Perché esiste: `workflow_run` gira con il token e i permessi del repository anche quando il run
# che lo ha attivato veniva da una PR di un fork. Il filtro `branches:` guarda solo il NOME del
# ramo di testa, che un fork sceglie liberamente, e la PR del fork può ridefinire nel proprio ramo
# i workflow che la attivano (nomi ed esiti dei check li sceglie lei). `release-gate-comment.yml`
# accettava ogni `workflow_run` con `pull-requests`, `issues` e `checks` in scrittura: segnalato
# dalla revisione di sicurezza dopo #4937, corretto in #4969. Il controllo che si cerca è
#
#     github.event.workflow_run.head_repository.full_name == github.repository
#
# Il rilevatore ha due test suoi, uno per direzione: senza, un verde qui potrebbe voler dire
# soltanto che non ha riconosciuto nessun trigger.

# Stampa i workflow di $1 attivati da workflow_run che non contengono il controllo.
unguarded_workflow_run() {
    local dir="$1" f
    for f in "$dir"/*.yml "$dir"/*.yaml; do
        [ -f "$f" ] || continue
        grep -qE '^[[:space:]]+workflow_run:[[:space:]]*$|^on:.*workflow_run' "$f" || continue
        grep -qF 'github.event.workflow_run.head_repository.full_name == github.repository' "$f" \
            || echo "$f"
    done
}

setup() {
    TMP=$(mktemp -d)
}

teardown() {
    rm -rf "$TMP"
}

@test "ogni workflow su workflow_run controlla il repository della testa (#4969)" {
    workflows="$BATS_TEST_DIRNAME/../../../.github/workflows"
    # Una cartella sbagliata darebbe un verde per niente.
    ls "$workflows"/*.yml >/dev/null

    run unguarded_workflow_run "$workflows"

    [ "$status" -eq 0 ]
    [ -z "$output" ] || {
        echo "workflow_run senza il controllo sul repository della testa:" >&2
        echo "$output" >&2
        return 1
    }
}

@test "il rilevatore segnala un workflow_run senza controllo (#4969)" {
    cat > "$TMP/unguarded.yml" <<'YAML'
on:
  workflow_run:
    workflows: ["CI"]
    types: [completed]
jobs:
  bot:
    if: github.event_name == 'workflow_run'
    runs-on: ubuntu-latest
YAML
    cat > "$TMP/short.yml" <<'YAML'
on: workflow_run
jobs:
  bot:
    runs-on: ubuntu-latest
YAML

    run unguarded_workflow_run "$TMP"

    [[ "$output" == *"unguarded.yml"* ]]
    [[ "$output" == *"short.yml"* ]]
}

@test "il rilevatore accetta il controllo, e ignora i workflow senza workflow_run (#4969)" {
    cat > "$TMP/guarded.yml" <<'YAML'
on:
  workflow_run:
    workflows: ["CI"]
    types: [completed]
jobs:
  bot:
    if: github.event.workflow_run.head_repository.full_name == github.repository
    runs-on: ubuntu-latest
YAML
    cat > "$TMP/push-only.yml" <<'YAML'
on:
  push:
    branches: [main-dev]
jobs:
  build:
    runs-on: ubuntu-latest
YAML

    run unguarded_workflow_run "$TMP"

    [ "$status" -eq 0 ]
    [ -z "$output" ]
}
