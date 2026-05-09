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

# Negative: --env without value -> exit 2 + error message
NEG1_OUTPUT=$("$RUN_SMOKE" --env 2>&1) || NEG1_EXIT=$?
NEG1_EXIT=${NEG1_EXIT:-0}
if [[ $NEG1_EXIT -ne 2 ]]; then
  echo "FAIL: --env with no value expected exit 2, got $NEG1_EXIT"
  echo "$NEG1_OUTPUT"
  exit 1
fi
if ! grep -q "requires a value" <<< "$NEG1_OUTPUT"; then
  echo "FAIL: --env with no value missing 'requires a value' in output"
  echo "$NEG1_OUTPUT"
  exit 1
fi

# Negative: missing --env flag entirely -> exit 2
NEG2_OUTPUT=$("$RUN_SMOKE" 2>&1) || NEG2_EXIT=$?
NEG2_EXIT=${NEG2_EXIT:-0}
if [[ $NEG2_EXIT -ne 2 ]]; then
  echo "FAIL: no args expected exit 2, got $NEG2_EXIT"
  echo "$NEG2_OUTPUT"
  exit 1
fi

# Negative: --env with non-existent env file -> exit 2 + error
NEG3_OUTPUT=$("$RUN_SMOKE" --env nonexistent_env_xyz 2>&1) || NEG3_EXIT=$?
NEG3_EXIT=${NEG3_EXIT:-0}
if [[ $NEG3_EXIT -ne 2 ]]; then
  echo "FAIL: nonexistent env expected exit 2, got $NEG3_EXIT"
  echo "$NEG3_OUTPUT"
  exit 1
fi
if ! grep -q "env file not found" <<< "$NEG3_OUTPUT"; then
  echo "FAIL: nonexistent env missing 'env file not found' in output"
  echo "$NEG3_OUTPUT"
  exit 1
fi

echo "PASS: run-smoke.sh negative-path contracts"
