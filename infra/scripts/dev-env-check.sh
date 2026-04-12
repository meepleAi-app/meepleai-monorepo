#!/usr/bin/env bash
# Verifica che .env.dev.local contenga tutte le chiavi del template .example.
# Se mancano chiavi, stampa un warning rosso con diff e istruzioni di fix.
# NFR-DX-1 enforcement.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INFRA_DIR="$(dirname "$SCRIPT_DIR")"
EXAMPLE="$INFRA_DIR/.env.dev.local.example"
LOCAL="$INFRA_DIR/.env.dev.local"

RED='\033[0;31m'
YELLOW='\033[0;33m'
GREEN='\033[0;32m'
NC='\033[0m'

if [ ! -f "$EXAMPLE" ]; then
  echo -e "${RED}ERROR: $EXAMPLE not found${NC}"
  exit 1
fi

if [ ! -f "$LOCAL" ]; then
  echo -e "${YELLOW}WARNING: $LOCAL not found. Creating from template...${NC}"
  cp "$EXAMPLE" "$LOCAL"
  echo -e "${GREEN}Created $LOCAL${NC}"
  exit 0
fi

# Extract keys (lines matching KEY=... or KEY="...")
get_keys() {
  grep -E '^[A-Z_][A-Z0-9_]*=' "$1" | cut -d= -f1 | sort -u
}

EXAMPLE_KEYS=$(get_keys "$EXAMPLE")
LOCAL_KEYS=$(get_keys "$LOCAL")

MISSING=$(comm -23 <(echo "$EXAMPLE_KEYS") <(echo "$LOCAL_KEYS") || true)

if [ -n "$MISSING" ]; then
  echo -e "${RED}\xe2\x9a\xa0  .env.dev.local is missing keys from the template:${NC}"
  echo "$MISSING" | while read -r key; do
    line=$(grep "^$key=" "$EXAMPLE" || echo "$key=")
    echo -e "  ${YELLOW}$line${NC}"
  done
  echo
  echo -e "${YELLOW}Fix: append the missing lines to $LOCAL or run:${NC}"
  echo "  cp $EXAMPLE $LOCAL  # (will overwrite your local changes)"
fi

EXTRA=$(comm -13 <(echo "$EXAMPLE_KEYS") <(echo "$LOCAL_KEYS") || true)
if [ -n "$EXTRA" ]; then
  echo -e "${YELLOW}\xe2\x84\xb9  .env.dev.local has keys not in template (probably custom):${NC}"
  echo "$EXTRA" | sed 's/^/  /'
fi
