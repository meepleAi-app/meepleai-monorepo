#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Parse global flags
while [[ $# -gt 0 ]]; do
    case "$1" in
        --token)
            export MEEPLEAI_ADMIN_TOKEN="$2"
            shift 2
            ;;
        --api-url)
            export API_URL="$2"
            shift 2
            ;;
        *)
            break
            ;;
    esac
done

domain="${1:-help}"
shift || true

case "$domain" in
    tunnel)  bash "$SCRIPT_DIR/lib/tunnel.sh" "$@" ;;
    schema)  bash "$SCRIPT_DIR/lib/schema.sh" "$@" ;;
    data)    bash "$SCRIPT_DIR/lib/data.sh" "$@" ;;
    history) bash "$SCRIPT_DIR/lib/history.sh" "$@" ;;
    help|--help|-h)
        echo "Database Sync CLI"
        echo ""
        echo "Usage: db-sync.sh [--token JWT] [--api-url URL] <command>"
        echo ""
        echo "Commands:"
        echo "  tunnel status          Check SSH tunnel status"
        echo "  tunnel connect         Open SSH tunnel to staging"
        echo "  tunnel disconnect      Close SSH tunnel"
        echo "  schema compare         Compare migrations local vs staging"
        echo "  schema preview         Preview SQL for pending migrations"
        echo "  schema apply --confirm Apply migrations to staging"
        echo "  data tables            List tables with row counts"
        echo "  data diff <table>      Compare table data"
        echo "  data sync <table> <direction> --confirm  Sync table data"
        echo "  history [limit]        Show sync operation history"
        ;;
    *)
        echo "Unknown command: $domain"
        echo "Run 'db-sync.sh help' for usage."
        exit 1
        ;;
esac
