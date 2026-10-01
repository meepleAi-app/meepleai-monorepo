#!/usr/bin/env bats
# Regression test for the inert `${VAR:-default}` in infra/Makefile (#3998).
#
# Run with: bats infra/scripts/tests/makefile-env-expansion.bats
#
# In una ricetta make `$` apre una variabile MAKE. Quindi `${SEED_INDEX_HEALTH_TIMEOUT:-180}` fa
# cercare a make una variabile chiamata `SEED_INDEX_HEALTH_TIMEOUT:-180`, che non esiste, e
# espande a **stringa vuota** — anche quando la variabile d'ambiente e' impostata. Serve `$$`,
# che passa `${...}` alla shell.
#
# Cosa costava, finche' era a dollaro singolo:
#   - `SEED_INDEX_HEALTH_TIMEOUT` non aveva effetto sul target `seed-index`;
#   - il timeout reale era 120s (il `${2:-120}` dentro wait-for-healthy.sh), non i 180 promessi
#     dal Makefile;
#   - e il bake FULL, che imposta `SEED_INDEX_HEALTH_TIMEOUT: '600'`, girava anch'esso a 120s.
#
# Trovato dalla riga di misura che wait-for-healthy.sh stampa su ogni successo: diceva
# `timeout 120s` dove il commento del Makefile prometteva 180. E' il motivo per cui quella riga
# esiste.

setup() {
    MAKEFILE="$BATS_TEST_DIRNAME/../../Makefile"
    TAB=$'\t'
}

@test "il Makefile esiste" {
    [ -f "$MAKEFILE" ]
}

@test "nessuna ricetta usa \${VAR:-default} a dollaro singolo (sarebbe inerte)" {
    # Il `[^$]` prima del `$` esclude i `$$` corretti. Un match qui e' una variabile d'ambiente
    # che il target crede di leggere e non legge.
    run grep -nE "^${TAB}.*[^$]\\\$\{[A-Za-z_][A-Za-z0-9_]*:-" "$MAKEFILE"
    [ "$status" -ne 0 ]
}

@test "il timeout dell'health del bake passa dalla shell, non da make" {
    run grep -cE "^${TAB}.*wait-for-healthy\.sh api \\\$\\\$\{SEED_INDEX_HEALTH_TIMEOUT:-" "$MAKEFILE"
    [ "$status" -eq 0 ]
    [ "$output" = "1" ]
}

@test "l'espansione a dollaro doppio consegna davvero il valore d'ambiente" {
    # Prova end-to-end su un Makefile usa-e-getta con la stessa forma: e' la proprieta' che conta,
    # e asserirla sulla forma del testo non basterebbe a provarla.
    tmp=$(mktemp -d)
    printf 'p:\n\t@echo "[$${SEED_INDEX_HEALTH_TIMEOUT:-180}]"\n' > "$tmp/Makefile"

    run make -s -C "$tmp" p
    [ "$output" = "[180]" ]

    SEED_INDEX_HEALTH_TIMEOUT=600 run make -s -C "$tmp" p
    [ "$output" = "[600]" ]

    rm -rf "$tmp"
}

@test "a dollaro SINGOLO il valore d'ambiente viene perso: e' il difetto, fissato" {
    # Il controesempio. Se un domani make cambiasse comportamento, questo test cadrebbe e la
    # correzione diventerebbe superflua — invece di restare in giro come superstizione.
    tmp=$(mktemp -d)
    printf 'p:\n\t@echo "[${SEED_INDEX_HEALTH_TIMEOUT:-180}]"\n' > "$tmp/Makefile"

    SEED_INDEX_HEALTH_TIMEOUT=600 run make -s -C "$tmp" p
    [ "$output" = "[]" ]

    rm -rf "$tmp"
}

@test "wait-for-healthy.sh stampa il tempo e il margine sul successo" {
    # La misura che ha scoperto il difetto sopra deve restare: senza di lei il timeout reale
    # tornerebbe a essere invisibile.
    run grep -cE "healthy in \\\$\{elapsed\}s \(timeout \\\$\{TIMEOUT\}s\)" "$BATS_TEST_DIRNAME/../wait-for-healthy.sh"
    [ "$status" -eq 0 ]
    [ "$output" = "1" ]
}
