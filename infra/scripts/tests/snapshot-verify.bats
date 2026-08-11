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

META_FILE() { echo "$TMPDIR/meepleai_seed_test.meta.json"; }

patch_meta() {
    jq "$1" "$(META_FILE)" > "$TMPDIR/meta.patched" && mv "$TMPDIR/meta.patched" "$(META_FILE)"
}

# infra/seed-schema.version is a counter bumped by any PR that renames a seeded
# table (#2126 D9). A fixture pinning the literal would go red on the next bump
# with nothing actually broken — the same "test rots until nobody reads it"
# trap this suite exists to catch (#3665). Read the live counter instead.
sync_seed_schema_version() {
    local live
    live=$(tr -d '[:space:]' <"$BATS_TEST_DIRNAME/../../seed-schema.version")
    patch_meta ".seed_table_schema_version = $live"
}

@test "exit 0 when all fields match" {
    install_fixture meta-good
    sync_seed_schema_version
    set_expected_env
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 0 ]
}

@test "exit 5 on seed-table schema-version drift" {
    install_fixture meta-good
    set_expected_env
    # A sidecar baked before the field existed — snapshot-verify.sh treats the
    # missing field as version 0, so it drifts from any bumped counter while
    # every other field still matches.
    #
    # This branch shipped in #2134 with no test at all: until #3665 the only
    # fixture that reached it was meta-good, which reached it by accident and
    # was asserting exit 0. Breaking the branch deliberately ran green.
    patch_meta 'del(.seed_table_schema_version)'
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 5 ]
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
