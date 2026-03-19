#!/usr/bin/env bash
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

limit="${1:-20}"
log_info "Fetching last $limit sync operations..."
api_call GET "/operations/history?limit=$limit" | python3 -m json.tool
