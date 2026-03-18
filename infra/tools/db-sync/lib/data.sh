#!/usr/bin/env bash
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

data_tables() {
    log_info "Listing tables..."
    api_call GET /tables | python3 -m json.tool
}

data_diff() {
    local table="${1:?Table name required}"
    log_info "Comparing table '$table'..."
    api_call GET "/tables/$table/compare" | python3 -m json.tool
}

data_sync() {
    local table="${1:?Table name required}"
    shift
    local direction="${1:-LocalToStaging}"
    shift || true

    if [[ "${1:-}" != "--confirm" ]]; then
        log_warn "This will sync '$table'. Add --confirm and --direction to proceed."
        exit 1
    fi

    local target
    if [[ "$direction" == "LocalToStaging" ]]; then target="STAGING"; else target="LOCAL"; fi
    local confirmation="SYNC ${table} TO ${target}"

    log_warn "About to sync '$table' ($direction)."
    read -rp "Type '$confirmation' to confirm: " user_input

    if [[ "$user_input" != "$confirmation" ]]; then
        log_err "Confirmation mismatch. Aborted."
        exit 1
    fi

    api_call POST "/tables/$table/sync" -d "{\"direction\":\"$direction\",\"confirmation\":\"$confirmation\"}" | python3 -m json.tool
}

case "${1:-}" in
    tables) data_tables ;;
    diff) shift; data_diff "$@" ;;
    sync) shift; data_sync "$@" ;;
    *) echo "Usage: db-sync.sh data {tables|diff <table>|sync <table> <direction> --confirm}" ;;
esac
