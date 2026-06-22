#!/usr/bin/env bats
# Unit tests for seed-nanolith-demo.sh pre-flight failure paths.
#
# Live API integration is out of scope here — these tests cover only
# the local pre-flight gate (missing PDFs, missing admin credentials,
# invalid TARGET argument) so the script fails fast with helpful errors.
#
# Run with: bats infra/scripts/tests/seed-nanolith-demo.bats
# Requires: bats-core (https://github.com/bats-core/bats-core)

setup() {
    SCRIPT="$BATS_TEST_DIRNAME/../seed-nanolith-demo.sh"
    TMPDIR=$(mktemp -d)
    # Isolate the script from the real repo: stub a tree it will look at.
    export FAKE_REPO="$TMPDIR/repo"
    mkdir -p "$FAKE_REPO/data/rulebook/nanolith_datasource"
    mkdir -p "$FAKE_REPO/infra/secrets"
    mkdir -p "$FAKE_REPO/infra/scripts"
    cp "$SCRIPT" "$FAKE_REPO/infra/scripts/seed-nanolith-demo.sh"
    SCRIPT_IN_FAKE="$FAKE_REPO/infra/scripts/seed-nanolith-demo.sh"

    # Sterile env: clear inherited admin secrets so the suite is reproducible.
    unset INITIAL_ADMIN_EMAIL INITIAL_ADMIN_PASSWORD ADMIN_PASSWORD
}

teardown() {
    rm -rf "$TMPDIR"
}

@test "exit 1 on invalid TARGET argument" {
    run bash "$SCRIPT_IN_FAKE" not-a-target
    [ "$status" -eq 1 ]
    [[ "$output" == *"TARGET must be local|staging"* ]]
}

@test "fail when Nanolith Rules PDF is missing" {
    # Provide only Press Start, omit Rules
    touch "$FAKE_REPO/data/rulebook/nanolith_datasource/Nanolith Press Start ENG.pdf"
    export INITIAL_ADMIN_EMAIL="admin@example.com"
    export INITIAL_ADMIN_PASSWORD="StrongAdmin1234"
    run bash "$SCRIPT_IN_FAKE" local
    [ "$status" -ne 0 ]
    [[ "$output" == *"PDF missing"* ]]
    [[ "$output" == *"Nanolith Rules ENG.pdf"* ]]
}

@test "fail when Nanolith Press Start PDF is missing" {
    touch "$FAKE_REPO/data/rulebook/nanolith_datasource/Nanolith Rules ENG.pdf"
    export INITIAL_ADMIN_EMAIL="admin@example.com"
    export INITIAL_ADMIN_PASSWORD="StrongAdmin1234"
    run bash "$SCRIPT_IN_FAKE" local
    [ "$status" -ne 0 ]
    [[ "$output" == *"Nanolith Press Start ENG.pdf"* ]]
}

@test "fail when admin credentials are not configured" {
    touch "$FAKE_REPO/data/rulebook/nanolith_datasource/Nanolith Rules ENG.pdf"
    touch "$FAKE_REPO/data/rulebook/nanolith_datasource/Nanolith Press Start ENG.pdf"
    # No env, no admin.secret in $FAKE_REPO/infra/secrets/
    run bash "$SCRIPT_IN_FAKE" local
    [ "$status" -ne 0 ]
    [[ "$output" == *"INITIAL_ADMIN_EMAIL"* ]]
    [[ "$output" == *"INITIAL_ADMIN_PASSWORD"* ]]
}

@test "credentials sourced from infra/secrets/admin.secret when env vars empty" {
    touch "$FAKE_REPO/data/rulebook/nanolith_datasource/Nanolith Rules ENG.pdf"
    touch "$FAKE_REPO/data/rulebook/nanolith_datasource/Nanolith Press Start ENG.pdf"

    cat > "$FAKE_REPO/infra/secrets/admin.secret" <<EOF
INITIAL_ADMIN_EMAIL=admin@example.com
INITIAL_ADMIN_PASSWORD=StrongAdmin1234
EOF

    # The script reaches the login curl call against http://localhost:8080
    # and fails there (no API). We assert the pre-flight passed by checking
    # that the secrets-resolution log line was emitted.
    run bash "$SCRIPT_IN_FAKE" local
    [[ "$output" == *"Bootstrap admin credentials resolved"* ]]
    # Exit code is non-zero because no live API, but pre-flight succeeded.
}

@test "syntax: bash -n parses the script" {
    run bash -n "$SCRIPT_IN_FAKE"
    [ "$status" -eq 0 ]
}
