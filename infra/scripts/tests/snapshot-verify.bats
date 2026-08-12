#!/usr/bin/env bats
# Unit tests for snapshot-verify.sh compat gate logic.
#
# Run with: bats infra/scripts/tests/snapshot-verify.bats
# (requires bats-core: https://github.com/bats-core/bats-core)

setup() {
    SCRIPT_DIR="$BATS_TEST_DIRNAME/.."
    FIXTURES="$BATS_TEST_DIRNAME/fixtures"
    TMPDIR=$(mktemp -d)
    META="$TMPDIR/meepleai_seed_test.meta.json"
    export SEED_INDEX_OUT_DIR="$TMPDIR"

    # snapshot-verify.sh resolves infra/seed-schema.version RELATIVE TO CWD
    # (lines 126-131: infra/… → ../infra/… → seed-schema.version) and falls back
    # to 0 in silence when none matches. Run from infra/scripts/ the counter is
    # unreachable, and "exit 5 on seed-table schema-version drift" then fails in
    # the dangerous direction: expected 0 == current 0, the branch is never
    # taken, and the test asserting the branch is reachable stops reaching it.
    # Pin the CWD so the suite tests the script's real resolution path instead
    # of whichever directory the caller happened to stand in.
    cd "$BATS_TEST_DIRNAME/../../.." || return 1
}

teardown() {
    rm -rf "$TMPDIR"
}

install_fixture() {
    local name=$1
    cp "$FIXTURES/$name.json" "$META"
    echo "meepleai_seed_test" > "$TMPDIR/.latest"
}

set_expected_env() {
    export EXPECTED_EF_HEAD="20260401_AddSearchVector"
    export EXPECTED_EMBEDDING_MODEL="sentence-transformers/all-MiniLM-L6-v2"
    export EXPECTED_EMBEDDING_DIM=384
}

patch_meta() {
    # Scratch file lives outside SEED_INDEX_OUT_DIR: the script only reads
    # .latest and $BASENAME.meta.json today, but a future glob over the
    # snapshot dir shouldn't trip over our temporaries.
    jq "$1" "$META" > "$BATS_TEST_TMPDIR/meta.patched" && mv "$BATS_TEST_TMPDIR/meta.patched" "$META"
}

# infra/seed-schema.version is a counter bumped by any PR that renames a seeded
# table (#2126 D9). A fixture pinning the literal would go red on the next bump
# with nothing actually broken — the same "test rots until nobody reads it"
# trap this suite exists to catch (#3665). Read the live counter instead.
sync_seed_schema_version() {
    local live
    live=$(tr -d '[:space:]' <"$BATS_TEST_DIRNAME/../../seed-schema.version")
    # Without this the malformed value reaches jq as a filter fragment and the
    # failure surfaces as a parse error that names neither the file nor why.
    [[ "$live" =~ ^[0-9]+$ ]] || {
        echo "seed-schema.version is not a counter: '$live'" >&2
        return 1
    }
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
    # Assert the reason, not just the number: exit 5 must come from THIS gate.
    # Without it the test could go green off an unrelated future exit 5, or red
    # with no hint that the counter simply wasn't found.
    [[ "$output" == *"seed-table schema-version drift"* ]]
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
