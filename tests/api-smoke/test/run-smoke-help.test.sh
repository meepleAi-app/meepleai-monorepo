#!/usr/bin/env bash
# Contract test for run-smoke.sh.
# Verifica che --help: (a) exit 0, (b) print "Usage:" line, (c) menziona --env.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUN_SMOKE="$SCRIPT_DIR/run-smoke.sh"

if [[ ! -x "$RUN_SMOKE" ]]; then
  echo "FAIL: $RUN_SMOKE does not exist or is not executable"
  exit 1
fi

OUTPUT="$("$RUN_SMOKE" --help 2>&1)"
EXIT_CODE=$?

if [[ $EXIT_CODE -ne 0 ]]; then
  echo "FAIL: --help exited with $EXIT_CODE (expected 0)"
  echo "$OUTPUT"
  exit 1
fi

if ! grep -q "Usage:" <<< "$OUTPUT"; then
  echo "FAIL: --help output missing 'Usage:'"
  echo "$OUTPUT"
  exit 1
fi

if ! grep -q -- "--env" <<< "$OUTPUT"; then
  echo "FAIL: --help output missing '--env' flag"
  echo "$OUTPUT"
  exit 1
fi

echo "PASS: run-smoke.sh --help contract"
