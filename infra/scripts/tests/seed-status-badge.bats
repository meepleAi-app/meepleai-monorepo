#!/usr/bin/env bats
# Unit tests for seed-status.sh --badge mode (D5 of #2126).
#
# Run with: bats infra/scripts/tests/seed-status-badge.bats
# (requires bats-core: https://github.com/bats-core/bats-core)
#
# The --badge flag must:
#   - emit exactly ONE line to stdout
#   - contain a Markdown fragment with HTML snapshot-badge markers
#   - show green / ⚠️ yellow / 🔴 red colour according to age thresholds
#     (≤7d green, 7-30d yellow, ≥30d red)
#   - emit nothing to stdout when no snapshot is present (only stderr)

setup() {
    SCRIPT_DIR="$BATS_TEST_DIRNAME/.."
    TMPDIR=$(mktemp -d)
    export SNAPSHOTS_DIR="$TMPDIR/snapshots"
    mkdir -p "$SNAPSHOTS_DIR"
}

teardown() {
    rm -rf "$TMPDIR"
}

# ── helpers ───────────────────────────────────────────────────────────────────

# Write a meta.json with a created_at N days ago from today.
install_meta_age_days() {
    local days=$1
    # Portable: use GNU date on Linux, BSD date on macOS (CI and dev)
    if date --version >/dev/null 2>&1; then
        # GNU date
        local ts
        ts=$(date -d "$days days ago" -u +%Y-%m-%dT%H:%M:%SZ)
    else
        # BSD date (macOS)
        local ts
        ts=$(date -u -v-"${days}d" +%Y-%m-%dT%H:%M:%SZ)
    fi
    cat > "$SNAPSHOTS_DIR/meepleai_seed_test.meta.json" <<JSON
{
  "schema_version": "20260401_AddSearchVector",
  "ef_migration_head": "20260401_AddSearchVector",
  "embedding_model": "sentence-transformers/all-MiniLM-L6-v2",
  "embedding_dim": 384,
  "app_commit": "test0000",
  "created_at": "$ts",
  "pdf_count": 130,
  "chunk_count": 18000,
  "embedding_count": 18000
}
JSON
}

run_badge() {
    run bash "$SCRIPT_DIR/seed-status.sh" --badge
}

# ── tests ─────────────────────────────────────────────────────────────────────

@test "--badge: fresh snapshot (2 days) emits one stdout line with green marker" {
    install_meta_age_days 2
    run_badge
    [ "$status" -eq 0 ]
    # Exactly one line on stdout
    [ "$(echo "$output" | wc -l)" -eq 1 ]
    # Contains the badge markers
    [[ "$output" == *"snapshot-badge:start"* ]]
    [[ "$output" == *"snapshot-badge:end"* ]]
    # Fresh = green = checkmark emoji
    [[ "$output" == *"✅"* ]]
    # Contains the age
    [[ "$output" == *"2 day"* ]] || [[ "$output" == *"2d"* ]]
}

@test "--badge: warning snapshot (10 days) emits one stdout line with yellow/warning marker" {
    install_meta_age_days 10
    run_badge
    [ "$status" -eq 0 ]
    [ "$(echo "$output" | wc -l)" -eq 1 ]
    [[ "$output" == *"snapshot-badge:start"* ]]
    [[ "$output" == *"snapshot-badge:end"* ]]
    # Warning = orange/yellow = warning emoji
    [[ "$output" == *"⚠️"* ]]
}

@test "--badge: stale snapshot (35 days) emits one stdout line with red marker" {
    install_meta_age_days 35
    run_badge
    [ "$status" -eq 0 ]
    [ "$(echo "$output" | wc -l)" -eq 1 ]
    [[ "$output" == *"snapshot-badge:start"* ]]
    [[ "$output" == *"snapshot-badge:end"* ]]
    # Stale = red = cross emoji
    [[ "$output" == *"🔴"* ]]
}

@test "--badge: missing snapshot emits one stdout line with missing marker" {
    # No files in SNAPSHOTS_DIR
    run_badge
    [ "$status" -eq 0 ]
    [ "$(echo "$output" | wc -l)" -eq 1 ]
    [[ "$output" == *"snapshot-badge:start"* ]]
    [[ "$output" == *"snapshot-badge:end"* ]]
    [[ "$output" == *"missing"* ]] || [[ "$output" == *"🔴"* ]]
}

@test "--badge: stdout is pure Markdown (no ANSI escape codes)" {
    install_meta_age_days 3
    run_badge
    [ "$status" -eq 0 ]
    # No ESC character (0x1B) in stdout
    [[ "$output" != *$'\033'* ]]
}

@test "--badge: compatible with --strict (no interaction)" {
    install_meta_age_days 3
    run bash "$SCRIPT_DIR/seed-status.sh" --badge --strict
    [ "$status" -eq 0 ]
}

@test "--badge: boundary: exactly 7 days old is still green" {
    install_meta_age_days 7
    run_badge
    [ "$status" -eq 0 ]
    [[ "$output" == *"✅"* ]]
}

@test "--badge: boundary: 8 days old is yellow warning" {
    install_meta_age_days 8
    run_badge
    [ "$status" -eq 0 ]
    [[ "$output" == *"⚠️"* ]]
}
