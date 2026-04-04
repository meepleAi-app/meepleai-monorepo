#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Load config
if [[ -f "$SCRIPT_DIR/.env" ]]; then
    source "$SCRIPT_DIR/.env"
fi

# Override with CLI flags
TOKEN="${MEEPLEAI_ADMIN_TOKEN:-}"
API="${API_URL:-http://localhost:8080/api/v1/admin/database-sync}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() { echo -e "${BLUE}[INFO]${NC} $*"; }
log_ok() { echo -e "${GREEN}[OK]${NC} $*"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $*"; }
log_err() { echo -e "${RED}[ERROR]${NC} $*" >&2; }

require_token() {
    if [[ -z "$TOKEN" ]]; then
        log_err "No auth token. Set MEEPLEAI_ADMIN_TOKEN in .env or pass --token <jwt>"
        exit 1
    fi
}

api_call() {
    local method="$1"
    local path="$2"
    shift 2
    local url="${API}${path}"

    require_token

    local response
    response=$(curl -s -w "\n%{http_code}" -X "$method" "$url" \
        -H "Authorization: Bearer $TOKEN" \
        -H "Content-Type: application/json" \
        "$@" 2>/dev/null) || {
        log_err "Failed to connect to API at $url"
        exit 1
    }

    local http_code
    http_code=$(echo "$response" | tail -1)
    local body
    body=$(echo "$response" | sed '$d')

    if [[ "$http_code" -ge 400 ]]; then
        log_err "API returned HTTP $http_code"
        echo "$body" | python3 -m json.tool 2>/dev/null || echo "$body"
        exit 1
    fi

    echo "$body"
}
