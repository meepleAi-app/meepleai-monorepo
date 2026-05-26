#!/usr/bin/env bash
# seed-sp4.sh — Orchestrator for SP4 dataset seed.
#
# Usage:
#   ./seed-sp4.sh                        # all steps, local target
#   ./seed-sp4.sh --target staging       # all steps, staging (via tunnel)
#   ./seed-sp4.sh --step 30              # run only step 30-kb.sh
#   ./seed-sp4.sh --from 40              # run from step 40 onwards
#   ./seed-sp4.sh --to 60                # run up to step 60 (skip slow steps)
#   ./seed-sp4.sh --reset                # delete all seeded data via API
#   ./seed-sp4.sh --reset --target staging --confirm  # staging reset
#
# Steps:
#   00 bootstrap     ← admin login (required first)
#   10 users         ← 5 SP4 users
#   20 games         ← 8 shared games + publish
#   30 kb            ← 6 PDF uploads + indexing (slowest: 5-15min)
#   40 agents        ← 5 agents (multi-login)
#   50 toolkits      ← 4 toolkits + tools (multi-login)
#   60 library       ← per-user library entries
#   70 sessions      ← 6 live sessions
#   80 play-records  ← scaled play records for stats
#   90 events        ← 4 game nights
#   95 chats         ← 5 chat threads (empty)

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"

# Parse args
ACTION="run"
STEP_ONLY=""
FROM_STEP=""
TO_STEP=""
CONFIRM=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --target)  export TARGET="$2"; shift 2 ;;
    --step)    STEP_ONLY="$2"; shift 2 ;;
    --from)    FROM_STEP="$2"; shift 2 ;;
    --to)      TO_STEP="$2"; shift 2 ;;
    --reset)   ACTION="reset"; shift ;;
    --confirm) CONFIRM="--confirm"; shift ;;
    -h|--help) sed -n '2,/^$/p' "$0"; exit 0 ;;
    *) echo "unknown flag: $1" >&2; exit 1 ;;
  esac
done

export TARGET="${TARGET:-local}"

if [[ "$ACTION" == "reset" ]]; then
  exec bash "$SCRIPT_DIR/99-reset.sh" $CONFIRM
fi

# Enumerate step scripts (NN-name.sh; exclude 99-reset.sh — only runs via --reset)
mapfile -t STEPS < <(ls "$SCRIPT_DIR"/[0-9][0-9]-*.sh 2>/dev/null | grep -v '/99-reset\.sh$' | sort)

if [[ ${#STEPS[@]} -eq 0 ]]; then
  echo "no step scripts found in $SCRIPT_DIR" >&2
  exit 1
fi

# Filter
filtered=()
for s in "${STEPS[@]}"; do
  base=$(basename "$s")
  num="${base:0:2}"
  if [[ -n "$STEP_ONLY" && "$num" != "$STEP_ONLY" ]]; then continue; fi
  if [[ -n "$FROM_STEP" && "$num" < "$FROM_STEP" ]]; then continue; fi
  if [[ -n "$TO_STEP"   && "$num" > "$TO_STEP"   ]]; then continue; fi
  filtered+=("$s")
done

if [[ ${#filtered[@]} -eq 0 ]]; then
  echo "no steps match filter (step=$STEP_ONLY from=$FROM_STEP to=$TO_STEP)" >&2
  exit 1
fi

# Always include bootstrap unless explicitly excluded
if [[ -z "$STEP_ONLY" || "$STEP_ONLY" == "00" ]]; then : ; else
  has_boot=0
  for s in "${filtered[@]}"; do [[ "$(basename "$s")" == 00-bootstrap.sh ]] && has_boot=1; done
  [[ $has_boot -eq 0 ]] && filtered=("$SCRIPT_DIR/00-bootstrap.sh" "${filtered[@]}")
fi

echo "seed-sp4 [target=$TARGET] running ${#filtered[@]} steps:"
for s in "${filtered[@]}"; do echo "   . $(basename "$s")"; done
echo ""

started=$(date +%s)
for s in "${filtered[@]}"; do
  if ! bash "$s"; then
    echo ""
    echo "Step $(basename "$s") FAILED. Subsequent steps NOT run." >&2
    echo "   Fix and re-run from this step:" >&2
    echo "   $0 --from $(basename "$s" | cut -c1-2) --target $TARGET" >&2
    exit 1
  fi
done

elapsed=$(( $(date +%s) - started ))
echo ""
echo "seed-sp4 [target=$TARGET] complete in ${elapsed}s"
echo "   State: ${STATE_FILE:-/tmp/meepleai-sp4-${TARGET}-state.json}"
