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
    # (its seed-schema.version lookup: infra/… → ../infra/… → seed-schema.version) and falls back
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
    [[ "$output" == *"snapshot disallineato con le migrations del working tree"* ]]
}

# #4489: con l'uguaglianza ogni migration mergiata dopo il bake settimanale
# fermava il gate RAG su tutti i branch. Uno snapshot piu' vecchio del working
# tree si carica allo schema del bake, e snapshot-restore.sh applica dopo le
# migrations successive: qui si controlla che il gate lo lasci passare e dica
# quali applichera'.
@test "exit 0 with the pending migrations when the snapshot is older than the working tree" {
    install_fixture meta-good
    sync_seed_schema_version
    set_expected_env
    export EXPECTED_EF_MIGRATIONS=$'20260301_Init\n20260401_AddSearchVector\n20260410_AddLater\n20260420_AddLatest'
    export EXPECTED_EF_HEAD="20260420_AddLatest"
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 0 ]
    [[ "$output" == *"snapshot piu' vecchio del working tree"* ]]
    [[ "$output" == *"dei dati 2 migration"* ]]
    [[ "$output" == *"[verify]   20260410_AddLater"* ]]
    [[ "$output" == *"[verify]   20260420_AddLatest"* ]]
    # La migration del bake e quelle prima sono gia' nello snapshot.
    [[ "$output" != *"[verify]   20260401_AddSearchVector"* ]]
    [[ "$output" != *"[verify]   20260301_Init"* ]]
    # meta-good e' un sidecar di prima di #4489, senza l'elenco delle migrations.
    [[ "$output" == *"non elenca le migrations del bake"* ]]
}

# Il caso che l'uguaglianza proteggeva davvero: uno snapshot che il working tree
# non conosce (piu' nuovo del checkout, o di un altro branch) resta exit 2.
@test "exit 2 when the snapshot migration is unknown to the working tree" {
    install_fixture meta-good
    sync_seed_schema_version
    set_expected_env
    export EXPECTED_EF_MIGRATIONS=$'20260301_Init\n20260320_Before'
    export EXPECTED_EF_HEAD="20260320_Before"
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 2 ]
    [[ "$output" == *"non e' fra quelle del working tree"* ]]
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

# Uno snapshot di un altro branch con una migration che il working tree non ha,
# anche se la sua data e' anteriore all'ultima del working tree: un controllo
# basato solo sull'ordine (head dello snapshot <= head del working tree) lo
# lascerebbe passare.
@test "exit 2 when an unknown snapshot migration sorts before the working tree head" {
    install_fixture meta-good
    sync_seed_schema_version
    set_expected_env
    unset EXPECTED_EF_HEAD
    export EXPECTED_EF_MIGRATIONS=$'20260301_Init\n20260320_Before\n20260420_AddLatest'
    patch_meta '.ef_migration_head = "20260310_OtherBranch"'
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 2 ]
    [[ "$output" == *"non e' fra quelle del working tree"* ]]
}

# Senza EXPECTED_EF_HEAD la head del working tree e' l'ultima migration in ordine,
# qualunque sia l'ordine in cui arrivano: in produzione l'elenco viene da find.
@test "the working tree head is the latest migration whatever the input order" {
    install_fixture meta-good
    sync_seed_schema_version
    set_expected_env
    unset EXPECTED_EF_HEAD
    export EXPECTED_EF_MIGRATIONS=$'20260420_AddLatest\n20260301_Init\n20260401_AddSearchVector'
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 0 ]
    [[ "$output" == *"dei dati 1 migration"* ]]
    [[ "$output" == *"[verify]   20260420_AddLatest"* ]]

    patch_meta '.ef_migration_head = "20260420_AddLatest"'
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 0 ]
    [[ "$output" != *"snapshot piu' vecchio"* ]]
}

# Dai bake di #4489 il sidecar elenca tutte le migrations del bake: quelle da
# applicare sono la differenza fra i due elenchi, e manca l'avviso sul controllo
# impossibile.
@test "with the bake's migration list the pending ones are the set difference" {
    install_fixture meta-good
    sync_seed_schema_version
    set_expected_env
    export EXPECTED_EF_MIGRATIONS=$'20260301_Init\n20260401_AddSearchVector\n20260410_AddLater\n20260420_AddLatest'
    export EXPECTED_EF_HEAD="20260420_AddLatest"
    patch_meta '.ef_migrations = ["20260301_Init", "20260401_AddSearchVector"]'
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 0 ]
    [[ "$output" == *"dei dati 2 migration"* ]]
    [[ "$output" == *"[verify]   20260410_AddLater"* ]]
    [[ "$output" == *"[verify]   20260420_AddLatest"* ]]
    [[ "$output" != *"non elenca le migrations del bake"* ]]
}

@test "without the bake's migration list a warning says the late-migration check is impossible" {
    install_fixture meta-good
    sync_seed_schema_version
    set_expected_env
    export EXPECTED_EF_MIGRATIONS=$'20260301_Init\n20260401_AddSearchVector\n20260420_AddLatest'
    export EXPECTED_EF_HEAD="20260420_AddLatest"
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 0 ]
    [[ "$output" == *"non elenca le migrations del bake"* ]]
}

# Il caso trovato dalla review di #4491: una migration arrivata nel working tree
# dopo il bake, ma con una data anteriore a quella dello snapshot (un branch
# mergiato dopo). Nel ripristino cadrebbe nel primo tempo, su tabelle vuote.
@test "exit 2 on a working tree migration older than the snapshot but missing from the bake" {
    install_fixture meta-good
    sync_seed_schema_version
    set_expected_env
    export EXPECTED_EF_MIGRATIONS=$'20260301_Init\n20260315_FromABranch\n20260401_AddSearchVector\n20260420_AddLatest'
    export EXPECTED_EF_HEAD="20260420_AddLatest"
    patch_meta '.ef_migrations = ["20260301_Init", "20260401_AddSearchVector"]'
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 2 ]
    [[ "$output" == *"data ANTERIORE"* ]]
    [[ "$output" == *"  20260315_FromABranch"* ]]
    [[ "$output" != *"  20260420_AddLatest"$'\n'* ]]
}

# Lo stesso difetto c'era gia' con le head uguali, e l'uguaglianza lo lasciava passare.
@test "exit 2 on a late migration also when the heads are equal" {
    install_fixture meta-good
    sync_seed_schema_version
    set_expected_env
    export EXPECTED_EF_MIGRATIONS=$'20260301_Init\n20260315_FromABranch\n20260401_AddSearchVector'
    patch_meta '.ef_migrations = ["20260301_Init", "20260401_AddSearchVector"]'
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 2 ]
    [[ "$output" == *"  20260315_FromABranch"* ]]
}

@test "exit 2 when the bake has a migration the working tree lacks" {
    install_fixture meta-good
    sync_seed_schema_version
    set_expected_env
    export EXPECTED_EF_MIGRATIONS=$'20260301_Init\n20260401_AddSearchVector'
    patch_meta '.ef_migrations = ["20260301_Init", "20260305_OtherBranch", "20260401_AddSearchVector"]'
    run bash "$SCRIPT_DIR/snapshot-verify.sh"
    [ "$status" -eq 2 ]
    [[ "$output" == *"migrations che il working tree non ha"* ]]
    [[ "$output" == *"  20260305_OtherBranch"* ]]
}
