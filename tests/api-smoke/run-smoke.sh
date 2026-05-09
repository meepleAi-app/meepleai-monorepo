#!/usr/bin/env bash
# meepleai-api-smoke runner (Linux/macOS/CI)
#
# Invokes Bruno CLI against the bruno-collection/ directory with the chosen env.
# Prints a JSON summary + table.
#
# Usage:
#   tests/api-smoke/run-smoke.sh --env local
#   tests/api-smoke/run-smoke.sh --env staging --collection private-game
#   tests/api-smoke/run-smoke.sh --help
#
# Exit codes:
#   0  = all scenarios passed
#   1  = one or more scenarios failed
#   2  = invalid arguments / Bruno CLI not found

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COLLECTION_ROOT="$SCRIPT_DIR/bruno-collection"
BRUNO_VERSION="$(cat "$SCRIPT_DIR/.bruno-version" 2>/dev/null || echo "2.15.1")"

ENV_NAME=""
SUB_COLLECTION=""

usage() {
  cat <<EOF
Usage: run-smoke.sh [OPTIONS]

Run meepleai API smoke tests via Bruno CLI.

Options:
  --env <name>          Environment name (local|staging). REQUIRED.
  --collection <name>   Run only one sub-collection (private-game|kb|agents|sessions).
                        If omitted, runs all 4.
  --help                Show this help and exit.

Examples:
  run-smoke.sh --env local
  run-smoke.sh --env staging --collection private-game

Exit codes:
  0  all scenarios passed
  1  one or more scenarios failed
  2  invalid arguments / Bruno CLI not found
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --env)
      ENV_NAME="${2:-}"
      shift 2
      ;;
    --collection)
      SUB_COLLECTION="${2:-}"
      shift 2
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -z "$ENV_NAME" ]]; then
  echo "Error: --env is required" >&2
  usage >&2
  exit 2
fi

ENV_FILE="$COLLECTION_ROOT/environments/${ENV_NAME}.bru"
if [[ ! -f "$ENV_FILE" ]]; then
  echo "Error: env file not found: $ENV_FILE" >&2
  exit 2
fi

TARGET="$COLLECTION_ROOT"
if [[ -n "$SUB_COLLECTION" ]]; then
  TARGET="$COLLECTION_ROOT/$SUB_COLLECTION"
  if [[ ! -d "$TARGET" ]]; then
    echo "Error: sub-collection not found: $TARGET" >&2
    exit 2
  fi
fi

echo "Running Bruno (v${BRUNO_VERSION}) — env=${ENV_NAME}, target=${TARGET#$SCRIPT_DIR/}"
npx -y "@usebruno/cli@${BRUNO_VERSION}" run --env "$ENV_NAME" "$TARGET"
