#!/usr/bin/env bash
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

schema_compare() {
    log_info "Comparing schema (migrations)..."
    api_call GET /schema/compare | python3 -m json.tool
}

schema_preview() {
    log_info "Generating SQL preview for pending migrations..."
    api_call POST /schema/preview-sql
}

schema_apply() {
    if [[ "${1:-}" != "--confirm" ]]; then
        log_warn "This will apply migrations to staging. Add --confirm to proceed."
        log_info "First, run 'db-sync.sh schema compare' to see what will be applied."
        exit 1
    fi

    # Get diff to build confirmation text
    local diff
    diff=$(api_call GET /schema/compare)
    local count
    count=$(echo "$diff" | python3 -c "import sys,json; print(len(json.load(sys.stdin)['localOnly']))")

    if [[ "$count" == "0" ]]; then
        log_ok "No migrations to apply."
        exit 0
    fi

    local confirmation="APPLY ${count} MIGRATIONS TO STAGING"
    log_warn "About to apply $count migrations to staging."
    read -rp "Type '$confirmation' to confirm: " user_input

    if [[ "$user_input" != "$confirmation" ]]; then
        log_err "Confirmation mismatch. Aborted."
        exit 1
    fi

    api_call POST /schema/apply -d "{\"direction\":\"LocalToStaging\",\"confirmation\":\"$confirmation\"}" | python3 -m json.tool
}

case "${1:-}" in
    compare) schema_compare ;;
    preview) schema_preview ;;
    apply) shift; schema_apply "$@" ;;
    *) echo "Usage: db-sync.sh schema {compare|preview|apply [--confirm]}" ;;
esac
