#!/usr/bin/env bats
# Regression test for the MediatR license fatal in every pipeline log (#3998).
#
# Run with: bats infra/scripts/tests/mediatr-secret-template.bats
#
# The chain that produced the fatal:
#   1. `make secrets-setup` → secrets/setup-secrets.ps1 does `Copy-Item` of each
#      *.secret.example to *.secret, VERBATIM. Whatever the template holds becomes the value.
#   2. compose loads *.secret into the api container through `env_file`.
#   3. Program.cs sets `cfg.LicenseKey` only when the variable is not null/whitespace.
#
# So an EMPTY value does what the template's own "OPTIONAL — startup continues without this key"
# line promises: community edition, no error. A TEXTUAL PLACEHOLDER instead arrives as a real
# value, is accepted as a licence, and fails validation:
#
#   [FTL] Error validating the Lucky Penny software license key
#
# which appeared on every boot of every pipeline running on generated secrets.
#
# Deliberately scoped to mediatr. Four other OPTIONAL templates (e2e, email, oauth, storage) also
# hold textual placeholders, and they must NOT be zeroed by analogy: for storage.secret the
# placeholder is load-bearing — the bake workflow relies on it so that seed-index-preflight.sh
# logs "no seed bucket". Each one needs checking against its own consumer, which is why this suite
# asserts the one case whose consumer is known.

setup() {
    SECRETS_DIR="$BATS_TEST_DIRNAME/../../secrets"
    TEMPLATE="$SECRETS_DIR/mediatr.secret.example"
}

@test "il template di mediatr esiste (e' la sola fonte di mediatr.secret)" {
    [ -f "$TEMPLATE" ]
}

@test "MEDIATR_LICENSE_KEY e' dichiarata nel template" {
    run grep -cE '^MEDIATR_LICENSE_KEY=' "$TEMPLATE"
    [ "$status" -eq 0 ]
    [ "$output" = "1" ]
}

@test "il valore e' VUOTO: un placeholder diventerebbe la licenza e fallirebbe la validazione" {
    value=$(grep -E '^MEDIATR_LICENSE_KEY=' "$TEMPLATE" | head -1 | cut -d= -f2-)
    # Niente trailing comment, niente testo: la riga finisce dopo l'uguale.
    [ -z "$value" ]
}

@test "nessun placeholder testuale nel valore" {
    run grep -nE '^MEDIATR_LICENSE_KEY=.*(your_|_here|CHANGEME|placeholder|xxx|<)' "$TEMPLATE"
    [ "$status" -ne 0 ]
}

@test "il template dichiara ancora che la chiave e' opzionale" {
    # Se qualcuno rende la licenza obbligatoria, il valore vuoto non e' piu' la scelta giusta e
    # questo test deve cadere insieme a quella decisione, non sopravviverle in silenzio.
    run grep -ci 'OPTIONAL' "$TEMPLATE"
    [ "$status" -eq 0 ]
}

@test "il .secret reale non e' tracciato da git" {
    # La chiave commerciale sta in mediatr.secret, che deve restare ignorato: il template e' la
    # sola cosa versionata.
    cd "$BATS_TEST_DIRNAME/../../.."
    run git check-ignore -q infra/secrets/mediatr.secret
    [ "$status" -eq 0 ]
}
