#!/bin/bash
# Issue #184: Deploy Performance Regression Comparison
#
# Compares current K6 deploy-smoke results against previous deployment baseline.
# Uses aggregate http_req_duration p95/p99 and http_req_failed rate.
#
# Usage:
#   ./deploy-baseline-compare.sh <current-k6-summary.json> [previous-baseline.json]
#
# Environment variables:
#   PERF_REGRESSION_MODE     - "warn" (default) or "fail"
#   REGRESSION_THRESHOLD_PCT - percentage increase to flag (default: 20)
#
# Exit codes:
#   0 = OK (or warn mode with regression)
#   1 = Regression detected in fail mode

set -euo pipefail

CURRENT_SUMMARY="${1:?Usage: deploy-baseline-compare.sh <current.json> [previous.json]}"
PREVIOUS_BASELINE="${2:-}"
MODE="${PERF_REGRESSION_MODE:-warn}"
THRESHOLD="${REGRESSION_THRESHOLD_PCT:-20}"

# Portable float comparison using awk
float_gt() { awk "BEGIN { exit !($1 > $2) }"; }
float_pct_change() { awk "BEGIN { printf \"%.1f\", (($1 - $2) / $2) * 100 }"; }
float_fmt() { awk "BEGIN { printf \"%.1f\", $1 }"; }

# Extract metrics from K6 summary JSON
extract_metric() {
  local file="$1" path="$2"
  jq -r "$path // empty" "$file" 2>/dev/null || echo ""
}

# ============================================
# EXTRACT CURRENT METRICS
# ============================================

if [ ! -f "$CURRENT_SUMMARY" ]; then
  echo "::error::K6 summary file not found: $CURRENT_SUMMARY"
  exit 1
fi

CURR_P95=$(extract_metric "$CURRENT_SUMMARY" '.metrics.http_req_duration.values["p(95)"]')
CURR_P99=$(extract_metric "$CURRENT_SUMMARY" '.metrics.http_req_duration.values["p(99)"]')
CURR_AVG=$(extract_metric "$CURRENT_SUMMARY" '.metrics.http_req_duration.values.avg')
CURR_ERR=$(extract_metric "$CURRENT_SUMMARY" '.metrics.http_req_failed.values.rate')

if [ -z "$CURR_P95" ]; then
  echo "::error::Could not extract p95 from K6 summary"
  exit 1
fi

CURR_P95_FMT=$(float_fmt "$CURR_P95")
CURR_P99_FMT=$(float_fmt "$CURR_P99")
CURR_AVG_FMT=$(float_fmt "$CURR_AVG")
CURR_ERR_PCT=$(awk "BEGIN { printf \"%.2f\", ${CURR_ERR:-0} * 100 }")

# ============================================
# BUILD REPORT
# ============================================

REPORT=""
add() { REPORT="${REPORT}${1}\n"; }

add "## ⚡ Deploy Performance Baseline"
add ""
add "| Metric | Current | Previous | Change | Status |"
add "|--------|---------|----------|--------|--------|"

REGRESSION_FOUND=0

if [ -z "$PREVIOUS_BASELINE" ] || [ ! -f "$PREVIOUS_BASELINE" ] || [ ! -s "$PREVIOUS_BASELINE" ]; then
  # First deploy — no comparison, just record
  add "| p95 | ${CURR_P95_FMT}ms | _first deploy_ | — | Baseline |"
  add "| p99 | ${CURR_P99_FMT}ms | _first deploy_ | — | Baseline |"
  add "| avg | ${CURR_AVG_FMT}ms | _first deploy_ | — | Baseline |"
  add "| error rate | ${CURR_ERR_PCT}% | _first deploy_ | — | Baseline |"
  add ""
  add "**First deployment — baseline recorded for future comparisons.**"
else
  # Compare against previous
  PREV_P95=$(jq -r '.aggregate.p95_ms // empty' "$PREVIOUS_BASELINE" 2>/dev/null || echo "")
  PREV_P99=$(jq -r '.aggregate.p99_ms // empty' "$PREVIOUS_BASELINE" 2>/dev/null || echo "")
  PREV_AVG=$(jq -r '.aggregate.avg_ms // empty' "$PREVIOUS_BASELINE" 2>/dev/null || echo "")
  PREV_ERR=$(jq -r '.aggregate.error_rate // empty' "$PREVIOUS_BASELINE" 2>/dev/null || echo "")

  compare_metric() {
    local name="$1" curr="$2" prev="$3" unit="$4"
    if [ -z "$prev" ] || [ "$prev" = "null" ] || [ "$prev" = "0" ]; then
      add "| $name | ${curr}${unit} | — | — | New |"
      return
    fi

    local change
    change=$(float_pct_change "$curr" "$prev")
    local status="OK"

    if float_gt "$change" "$THRESHOLD"; then
      status="Regression (+${change}%)"
      REGRESSION_FOUND=1
    elif float_gt "$change" "0"; then
      status="+${change}%"
    else
      status="${change}%"
    fi

    add "| $name | ${curr}${unit} | ${prev}${unit} | ${change}% | $status |"
  }

  compare_metric "p95" "$CURR_P95_FMT" "$PREV_P95" "ms"
  compare_metric "p99" "$CURR_P99_FMT" "$PREV_P99" "ms"
  compare_metric "avg" "$CURR_AVG_FMT" "$PREV_AVG" "ms"
  compare_metric "error rate" "$CURR_ERR_PCT" "$(awk "BEGIN { printf \"%.2f\", ${PREV_ERR:-0} * 100 }")" "%"
fi

add ""
add "---"
add ""
add "**Mode**: \`$MODE\` | **Regression threshold**: ${THRESHOLD}%"

if [ "$REGRESSION_FOUND" -eq 1 ]; then
  add ""
  if [ "$MODE" = "fail" ]; then
    add "**REGRESSION DETECTED** — deploy pipeline will fail."
  else
    add "**REGRESSION DETECTED** — warning only (mode=warn)."
  fi
fi

# Output report
echo -e "$REPORT"

# Write to GitHub step summary if available
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  echo -e "$REPORT" >> "$GITHUB_STEP_SUMMARY"
fi

# Output current metrics as JSON for the update step to consume
METRICS_JSON=$(cat <<METRICSEOF
{
  "measured_at": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "aggregate": {
    "avg_ms": $CURR_AVG_FMT,
    "p95_ms": $CURR_P95_FMT,
    "p99_ms": $CURR_P99_FMT,
    "error_rate": ${CURR_ERR:-0}
  }
}
METRICSEOF
)
echo "$METRICS_JSON" > "${CURRENT_SUMMARY%.json}-metrics.json"
echo "Metrics JSON saved to: ${CURRENT_SUMMARY%.json}-metrics.json"

# Exit based on mode
if [ "$REGRESSION_FOUND" -eq 1 ] && [ "$MODE" = "fail" ]; then
  echo "::error::Performance regression detected (p95/p99 increased >${THRESHOLD}%)"
  exit 1
fi

if [ "$REGRESSION_FOUND" -eq 1 ]; then
  echo "::warning::Performance regression detected (p95/p99 increased >${THRESHOLD}%) — mode=$MODE, continuing"
fi

exit 0
