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
#
# Un token puo' contenerne un altro (#4484): `Integration.Authentication` seleziona un sottoinsieme
# di cio' che seleziona `Authentication`, perche' ogni nome che contiene il primo contiene anche il
# secondo. Uno shard che prende quel sottoinsieme RAFFINA la ripartizione senza cambiare l'unione,
# e `Core` resta coperto escludendo il token piu' largo. Per questo i confronti fra insiemi di token
# si fanno sui token massimali (vedi `maximal`), non sull'elenco grezzo.

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
# Il punto fa parte del token: senza, `Integration.Authentication` si leggerebbe `Integration`.
selected() {
    printf '%s\n' "$1" | sed 's/FullyQualifiedName!~/ESCLUSO:/g' \
        | grep -oE 'FullyQualifiedName~[A-Za-z0-9_.]+' | sed 's/^FullyQualifiedName~//' | LC_ALL=C sort -u
}
excluded() {
    printf '%s\n' "$1" | grep -oE 'FullyQualifiedName!~[A-Za-z0-9_.]+' | sed 's/^FullyQualifiedName!~//' | LC_ALL=C sort -u
}

# L'unione dei token selezionati dagli shard diversi da Core.
selected_outside_core() {
    shards "$1" | awk -F'\t' '$1 != "Core" { print $2 }' | while IFS= read -r f; do selected "$f"; done | LC_ALL=C sort -u
}

# Da un elenco di token (uno per riga, su stdin), quelli che non contengono un altro token
# dell'elenco. Un token che ne contiene un altro seleziona un sottoinsieme dei suoi test, quindi non
# aggiunge niente all'unione.
maximal() {
    awk '{ t[NR] = $0 } END {
        for (i = 1; i <= NR; i++) {
            keep = 1
            for (j = 1; j <= NR; j++) if (i != j && t[j] != t[i] && index(t[i], t[j]) > 0) keep = 0
            if (keep) print t[i]
        }
    }' | LC_ALL=C sort -u
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

@test "maximal toglie solo i token che ne contengono un altro" {
    # Controprova per i confronti sotto: un `maximal` che restituisse niente renderebbe uguali due
    # insiemi qualsiasi; uno che restituisse tutto rifiuterebbe ogni raffinamento.
    run maximal <<< $'Authentication\nIntegration.Authentication\nKnowledgeBase\nKnowledgeBase'
    [ "$status" -eq 0 ]
    [ "$output" = $'Authentication\nKnowledgeBase' ]
    run maximal <<< $'GameManagement\nSharedGameCatalog'
    [ "$output" = $'GameManagement\nSharedGameCatalog' ]
}

# ─── nessun test resta fuori da ogni shard ───────────────────────────────────────────────────────

@test "dev-async: Core esclude esattamente i token massimali selezionati dagli altri shard" {
    [ "$(excluded "$(filter_of "$DEV_ASYNC" Core)")" = "$(selected_outside_core "$DEV_ASYNC" | maximal)" ]
}

@test "ci.yml: Core esclude esattamente i token massimali selezionati dagli altri shard" {
    [ "$(excluded "$(filter_of "$CI" Core)")" = "$(selected_outside_core "$CI" | maximal)" ]
}

@test "dev-async: un token escluso da uno shard e' selezionato da un altro" {
    # Uno shard puo' escludere un token per non eseguire due volte i test di un altro shard. Ma se
    # nessuno lo seleziona, quei test non li prende nemmeno Core, che esclude i token degli altri.
    all="$(selected_outside_core "$DEV_ASYNC")"
    while IFS=$'\t' read -r name filter; do
        [ "$name" = "Core" ] && continue
        for token in $(excluded "$filter"); do
            printf '%s\n' "$all" | grep -qxF "$token" || {
                echo "lo shard $name esclude $token, che nessuno shard seleziona"
                return 1
            }
        done
    done < <(shards "$DEV_ASYNC")
}

# ─── i due gate misurano lo stesso insieme ───────────────────────────────────────────────────────

@test "dev-async e ci.yml applicano lo stesso filtro di base" {
    # Il filtro di base e' la parte che precede `${{ matrix.shard.filter_extra }}` nel comando
    # `dotnet test`: la selezione per categoria e le esclusioni per nome, uguali per tutti gli shard.
    # I confronti sotto guardano solo `filter_extra`, quindi un'esclusione tolta da un workflow e
    # lasciata nell'altro passerebbe senza che niente cambi colore (#4518: FrontendSdk era escluso
    # da entrambi e non girava da nessuna parte; toglierlo da uno solo avrebbe fatto misurare ai
    # due gate insiemi diversi, il difetto di #3622).
    base_filter() {
        grep -oE 'Category=Integration[^$"\\]*\$\{\{ matrix\.shard\.filter_extra' "$1" \
            | sed 's/\${{ matrix\.shard\.filter_extra$//' | LC_ALL=C sort -u
    }
    dev="$(base_filter "$DEV_ASYNC")"
    [ -n "$dev" ]
    [ "$(printf '%s\n' "$dev" | wc -l)" -eq 1 ]
    [ "$dev" = "$(base_filter "$CI")" ]
}

@test "dev-async e ci.yml selezionano gli stessi test: stessi token, stesso Core" {
    # dev-async puo' avere una ripartizione piu' fine di ci.yml (#3742 divide Games in due, #4484
    # stacca AuthIntegration da KnowledgeBase), ma l'unione dev'essere la stessa: un test che un solo
    # gate esegue e' il difetto di #3622.
    dev="$(selected_outside_core "$DEV_ASYNC" | maximal)"
    [ -n "$dev" ]
    [ "$dev" = "$(selected_outside_core "$CI" | maximal)" ]
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

# ─── AuthIntegration staccato da KnowledgeBase (#4484) ───────────────────────────────────────────

@test "dev-async: KnowledgeBase e AuthIntegration non eseguono gli stessi test" {
    # AuthIntegration prende un pezzo di cio' che KnowledgeBase selezionerebbe: KnowledgeBase deve
    # escluderlo, altrimenti quei test girerebbero due volte.
    auth="$(selected "$(filter_of "$DEV_ASYNC" AuthIntegration)")"
    [ -n "$auth" ]
    [ "$(excluded "$(filter_of "$DEV_ASYNC" KnowledgeBase)")" = "$auth" ]
}

@test "dev-async: AuthIntegration seleziona solo dentro il dominio di KnowledgeBase" {
    # Ogni suo token deve contenere un token che KnowledgeBase seleziona. Le regole sopra non lo
    # vedono quando il token cade nel dominio di un ALTRO shard: `SharedGameCatalog.Foo` sparisce in
    # `maximal`, quindi Core e ci.yml tornano uguali, ma quei test girerebbero sia in AuthIntegration
    # sia in Catalog, e KnowledgeBase escluderebbe un token che non e' mai stato suo.
    kb="$(selected "$(filter_of "$DEV_ASYNC" KnowledgeBase)")"
    [ -n "$kb" ]
    for token in $(selected "$(filter_of "$DEV_ASYNC" AuthIntegration)"); do
        inside=0
        for k in $kb; do [[ "$token" == *"$k"* ]] && inside=1; done
        [ "$inside" -eq 1 ] || {
            echo "AuthIntegration seleziona $token, che nessun token di KnowledgeBase contiene"
            return 1
        }
    done
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
