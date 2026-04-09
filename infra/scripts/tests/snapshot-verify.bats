#!/usr/bin/env bats
# Unit tests for snapshot-verify.sh compat gate logic.
#
# Run with: bats infra/scripts/tests/snapshot-verify.bats
# (requires bats-core: https://github.com/bats-core/bats-core)

setup() {
    SCRIPT_DIR="$BATS_TEST_DIRNAME/.."
    FIXTURES="$BATS_TEST_DIRNAME/fixtures"
    TMPDIR=$(mktemp -d)
    export SEED_INDEX_OUT_DIR="$TMPDIR"
}

teardown() {
    rm -rf "$TMPDIR"
}

install_fixture() {
    local name=$1
    cp "$FIXTURES/$name.json" "$TMPDIR/meepleai_seed_test.meta.json"
    echo "meepleai_seed_test" > "$TMPDIR/.latest"
}

set_expected_env() {
    export EXPECTED_EF_HEAD="20260401_AddSearchVector"
    export EXPECTED_EMBEDDING_MODEL="sentence-transformers/all-MiniLM-L6-v2"
    export EXPECTED_EMBEDDING_DIM=384
}

@test "exit 0 when all fields match" {
    install_fixture meta-good
    set_expected_env
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 0 ]
}

@test "exit 2 on migration drift" {
    install_fixture meta-migration-drift
    set_expected_env
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 2 ]
}

@test "exit 3 on model drift" {
    install_fixture meta-model-drift
    set_expected_env
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 3 ]
}

@test "exit 4 on dim drift" {
    install_fixture meta-dim-drift
    set_expected_env
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 4 ]
}

@test "exit 1 on missing .latest" {
    set_expected_env
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 1 ]
}

@test "exit 1 on missing meta.json" {
    set_expected_env
    echo "nonexistent_snapshot" > "$TMPDIR/.latest"
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 1 ]
}
