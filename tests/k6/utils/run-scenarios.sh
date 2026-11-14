#!/usr/bin/env bash
# Runs the k6 scenarios of test-performance.yml and fails if any of them fails (#4978).
#
# Each scenario used to run as `k6 run … || true`, so no result of k6 could turn the job red: on #4897 and on the
# nightly run of 2026-11-09 every login answered 400 and nobody saw it. This script keeps the exit code of each
# scenario and writes one row per scenario, with its duration, to $GITHUB_STEP_SUMMARY.
#
# Usage (from tests/k6):
#   utils/run-scenarios.sh <test_type> <scenario|all>
#
# Environment: K6_BIN (default `k6`), GITHUB_STEP_SUMMARY (optional), and what the scenarios read
# (API_BASE_URL, TEST_USER_*, TEST_ADMIN_*, HTTP_DEBUG).
#
# k6 exit codes worth knowing: 99 = a threshold was crossed (in smoke only auth_errors and checks are thresholds,
# see thresholdsFor in shared-config.js); 107 = an exception in the init code or in setup()/teardown().
#
# An exception inside an iteration does NOT change k6's exit code: k6 logs it (`source=stacktrace`) and starts the
# next iteration. Until #4978 every authenticated GET threw `URLSearchParams is not defined`, and the scenarios
# exited 0 without calling one endpoint. So a scenario whose output has a stack trace fails here, whatever k6 says.
# Each scenario's output is kept in reports/<scenario>.log.
#
# Exit codes: 0 = every scenario passed; 1 = at least one failed, or the arguments are wrong.

set -uo pipefail

TEST_TYPE="${1:?usage: run-scenarios.sh <test_type> <scenario|all>}"
SCENARIO="${2:?usage: run-scenarios.sh <test_type> <scenario|all>}"
K6_BIN="${K6_BIN:-k6}"

declare -A FILES=(
  [admin-polling]=scenarios/admin-polling.js
  [user-dashboard]=scenarios/user-dashboard-polling.js
  [library-browsing]=scenarios/library-browsing.js
  [catalog-search]=scenarios/catalog-search.js
  [admin-concurrent]=scenarios/admin-concurrent-actions.js
  [rag-isolated]=scenarios/rag-pipeline-isolated.js
  [rag-e2e]=scenarios/rag-e2e-realistic.js
)

# The scenarios of `all`, in this order. The two RAG scenarios are left out: they need an agent with indexed
# documents and an LLM, and the job has neither (its OpenRouter key is a dummy), so setup() would throw on every
# run. They stay available one at a time through the k6_scenario input of the dispatch.
ALL=(admin-polling user-dashboard library-browsing catalog-search admin-concurrent)

if [ "$SCENARIO" = "all" ]; then
  NAMES=("${ALL[@]}")
elif [ -n "${FILES[$SCENARIO]:-}" ]; then
  NAMES=("$SCENARIO")
else
  echo "::error::unknown k6 scenario: $SCENARIO (known: ${!FILES[*]})"
  exit 1
fi

mkdir -p reports

ROWS=()
FAILED=()
for name in "${NAMES[@]}"; do
  file="${FILES[$name]}"
  if [ ! -f "$file" ]; then
    echo "::error::k6 scenario file not found: $file"
    ROWS+=("| \`$name\` | – | – | – | file not found |")
    FAILED+=("$name")
    continue
  fi

  log="reports/${name}.log"
  echo "::group::k6 $name ($file, TEST_TYPE=$TEST_TYPE)"
  start=$(date +%s)
  "$K6_BIN" run --env TEST_TYPE="$TEST_TYPE" "$file" 2>&1 | tee "$log"
  rc=${PIPESTATUS[0]}
  elapsed=$(( $(date +%s) - start ))
  echo "::endgroup::"
  exceptions=$(grep -c 'source=stacktrace' "$log" || true)

  if [ "$rc" -eq 0 ] && [ "$exceptions" -gt 0 ]; then
    verdict="script threw"
  else
    case "$rc" in
      0)  verdict="passed" ;;
      99) verdict="threshold crossed" ;;
      *)  verdict="k6 failed" ;;
    esac
  fi
  echo "k6 $name: exit $rc, $exceptions stack trace(s) ($verdict) in ${elapsed}s"
  ROWS+=("| \`$name\` | ${elapsed}s | $rc | $exceptions | $verdict |")
  if [ "$verdict" != "passed" ]; then
    echo "::error::k6 $name: exit $rc, $exceptions stack trace(s) ($verdict); output in tests/k6/$log"
    FAILED+=("$name")
  fi
done

{
  echo "### k6 scenarios (TEST_TYPE=$TEST_TYPE)"
  echo ""
  echo "| scenario | duration | exit | stack traces | result |"
  echo "|---|---|---|---|---|"
  printf '%s\n' "${ROWS[@]}"
} >> "${GITHUB_STEP_SUMMARY:-/dev/stdout}"

if [ "${#FAILED[@]}" -gt 0 ]; then
  echo "Failed scenarios: ${FAILED[*]}"
  exit 1
fi
echo "All k6 scenarios passed"
