#!/usr/bin/env bats
# La ripartizione in shard dei test di integrazione — #3742.
#
# I test Category=Integration sono divisi fra piu' job da un filtro per nome: ogni shard seleziona i
# test il cui nome completo contiene uno dei suoi token, e `Core` prende cio' che resta escludendo
# quei token uno per uno. Niente tiene insieme le due meta' della regola tranne chi scrive la
# matrice: togliere un token da uno shard lasciandolo escluso da `Core` fa sparire quei test da ogni
# shard, e nessun job cambia colore — sono test che non girano, non test che falliscono.
#
# Questi test leggono le matrici dai due workflow e ne verificano la forma. Costano secondi; la
# stessa svista, scoperta in dev-async, costa un'ora per run e si vede solo cercando un test nel
# .trx.

setup() {
    ROOT="$BATS_TEST_DIRNAME/../../.."
    DEV_ASYNC="$ROOT/.github/workflows/dev-async.yml"
    CI="$ROOT/.github/workflows/ci.yml"
    BASELINE="$ROOT/infra/fixtures/dev-async-shard-baseline.json"
}

# "nome<TAB>filtro" per ogni voce della matrice. Nei due workflow `filter_extra` compare solo li',
# e segue sempre la riga `- name: "..."` del suo shard.
shards() {
    awk '
      /^[[:space:]]*- name:[[:space:]]*"/        { name = $0; sub(/^[^"]*"/, "", name); sub(/".*$/, "", name); next }
      /^[[:space:]]*filter_extra:[[:space:]]*"/  { f = $0; sub(/^[^"]*"/, "", f); sub(/"[^"]*$/, "", f); print name "\t" f }
    ' "$1"
}

filter_of() { shards "$1" | awk -F'\t' -v n="$2" '$1 == n { print $2 }'; }
names_of() { shards "$1" | cut -f1 | LC_ALL=C sort; }

# I token che un filtro SELEZIONA (FullyQualifiedName~X) e quelli che ESCLUDE (FullyQualifiedName!~X).
selected() {
    printf '%s\n' "$1" | sed 's/FullyQualifiedName!~/ESCLUSO:/g' \
        | grep -oE 'FullyQualifiedName~[A-Za-z0-9_]+' | sed 's/^FullyQualifiedName~//' | LC_ALL=C sort -u
}
excluded() {
    printf '%s\n' "$1" | grep -oE 'FullyQualifiedName!~[A-Za-z0-9_]+' | sed 's/^FullyQualifiedName!~//' | LC_ALL=C sort -u
}

# L'unione dei token selezionati dagli shard diversi da Core.
selected_outside_core() {
    shards "$1" | awk -F'\t' '$1 != "Core" { print $2 }' | while IFS= read -r f; do selected "$f"; done | LC_ALL=C sort -u
}

# ─── la lettura delle matrici e' viva ────────────────────────────────────────────────────────────

@test "le due matrici si leggono: piu' shard, e uno si chiama Core" {
    # Controprova per tutti i test sotto: se il formato del YAML cambiasse e la lettura tornasse
    # vuota, i confronti fra insiemi vuoti passerebbero senza provare niente.
    for wf in "$DEV_ASYNC" "$CI"; do
        run names_of "$wf"
        [ "$status" -eq 0 ]
        [ "$(printf '%s\n' "$output" | wc -l)" -ge 3 ]
        [[ $'\n'"$output"$'\n' == *$'\n'"Core"$'\n'* ]]
        [ -n "$(selected_outside_core "$wf")" ]
    done
}

# ─── nessun test resta fuori da ogni shard ───────────────────────────────────────────────────────

@test "dev-async: Core esclude esattamente i token selezionati dagli altri shard" {
    [ "$(excluded "$(filter_of "$DEV_ASYNC" Core)")" = "$(selected_outside_core "$DEV_ASYNC")" ]
}

@test "ci.yml: Core esclude esattamente i token selezionati dagli altri shard" {
    [ "$(excluded "$(filter_of "$CI" Core)")" = "$(selected_outside_core "$CI")" ]
}

@test "dev-async: un token escluso da uno shard e' selezionato da un altro" {
    # Uno shard puo' escludere un token per non eseguire due volte i test di un altro shard. Ma se
    # nessuno lo seleziona, quei test non li prende nemmeno Core, che esclude i token degli altri.
    all="$(selected_outside_core "$DEV_ASYNC")"
    while IFS=$'\t' read -r name filter; do
        [ "$name" = "Core" ] && continue
        for token in $(excluded "$filter"); do
            printf '%s\n' "$all" | grep -qx "$token" || {
                echo "lo shard $name esclude $token, che nessuno shard seleziona"
                return 1
            }
        done
    done < <(shards "$DEV_ASYNC")
}

# ─── i due gate misurano lo stesso insieme ───────────────────────────────────────────────────────

@test "dev-async e ci.yml selezionano gli stessi test: stessi token, stesso Core" {
    # dev-async puo' avere una ripartizione piu' fine di ci.yml (#3742 divide Games in due), ma
    # l'unione dev'essere la stessa: un test che un solo gate esegue e' il difetto di #3622.
    [ "$(selected_outside_core "$DEV_ASYNC")" = "$(selected_outside_core "$CI")" ]
    [ "$(filter_of "$DEV_ASYNC" Core)" = "$(filter_of "$CI" Core)" ]
}

# ─── la divisione di Games (#3742) ───────────────────────────────────────────────────────────────

@test "dev-async: Catalog e GamesAdmin non eseguono gli stessi test" {
    # Sono le due meta' dello shard Games. GamesAdmin deve escludere cio' che Catalog seleziona,
    # altrimenti una classe col nome di entrambi i contesti girerebbe due volte.
    catalog="$(selected "$(filter_of "$DEV_ASYNC" Catalog)")"
    [ -n "$catalog" ]
    [ "$(excluded "$(filter_of "$DEV_ASYNC" GamesAdmin)")" = "$catalog" ]
}

# ─── la baseline segue la matrice ────────────────────────────────────────────────────────────────

@test "la baseline ha una voce per ogni shard di dev-async, e nessuna in piu'" {
    # Uno shard senza voce fa uscire 1 il comparatore al primo run; una voce senza shard e' una
    # dichiarazione che nessuno confronta piu'.
    [ "$(jq -r '.shards | keys[]' "$BASELINE" | tr -d '\r' | LC_ALL=C sort)" = "$(names_of "$DEV_ASYNC")" ]
    for shard in $(names_of "$DEV_ASYNC"); do
        run jq -e --arg s "$shard" '.shards[$s].failed != null and .shards[$s].skipped != null' "$BASELINE"
        [ "$status" -eq 0 ]
    done
}
