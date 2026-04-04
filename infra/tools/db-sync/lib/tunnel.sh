#!/usr/bin/env bash
source "$(dirname "${BASH_SOURCE[0]}")/common.sh"

tunnel_status() {
    log_info "Checking tunnel status..."
    api_call GET /tunnel/status | python3 -m json.tool
}

tunnel_connect() {
    log_info "Opening SSH tunnel to staging..."
    api_call POST /tunnel/open | python3 -m json.tool
}

tunnel_disconnect() {
    log_info "Closing SSH tunnel..."
    api_call DELETE /tunnel/close | python3 -m json.tool
}

case "${1:-}" in
    status) tunnel_status ;;
    connect) tunnel_connect ;;
    disconnect) tunnel_disconnect ;;
    *) echo "Usage: db-sync.sh tunnel {status|connect|disconnect}" ;;
esac
