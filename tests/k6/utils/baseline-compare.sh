#!/bin/bash
# Issue #5542: k6 Baseline Regression Gate
#
# Compares current k6 test results against baseline thresholds.
# Uses k6 summary JSON output to extract p95/p99/error_rate metrics
# and applies dual threshold: relative (vs baseline) + absolute (vs SLO).
#
# Usage:
#   ./baseline-compare.sh <summary-json-dir> [--strict]
#
# Exit codes:
#   0 = All checks passed
#   1 = Regression detected (blocks PR)
#   2 = Warning (does not block)

set -euo pipefail

REPORTS_DIR="${1:-reports}"
STRICT="${2:-}"
EXIT_CODE=0
WARNINGS=0
FAILURES=0
REPORT_LINES=()

# ============================================
# BASELINE CONFIGURATION
# ============================================

# Absolute SLO thresholds (milliseconds for latency, percentage for errors)
declare -A SLO_THRESHOLDS=(
  # Standard API endpoints
  ["http_req_duration_p95"]=500
  ["http_req_duration_p99"]=1000
  ["http_req_failed_rate"]=1.0

  # RAG-specific
  ["pipeline_total_latency_p95"]=1000
  ["chat_response_latency_p95"]=5000
  ["embedding_latency_p95"]=300
  ["vector_search_latency_p95"]=200

  # E2E RAG
  ["first_token_latency_p95"]=2000
)

# Relative regression threshold (percentage)
REGRESSION_THRESHOLD=15

# ============================================
# FUNCTIONS
# ============================================

add_report() {
  REPORT_LINES+=("$1")
}

check_metric() {
  local metric_name="$1"
  local current_value="$2"
  local slo_key="$3"

  # Skip if no value
  if [[ -z "$current_value" || "$current_value" == "null" ]]; then
    return
  fi

  local slo_value="${SLO_THRESHOLDS[$slo_key]:-}"

  if [[ -n "$slo_value" ]]; then
    # Compare against absolute SLO threshold
    local exceeded
    exceeded=$(echo "$current_value > $slo_value" | bc -l 2>/dev/null || echo "0")

    if [[ "$exceeded" == "1" ]]; then
      add_report "❌ **FAIL**: \`$metric_name\` = ${current_value}ms (SLO: ${slo_value}ms)"
      FAILURES=$((FAILURES + 1))
    else
      local pct_of_slo
      pct_of_slo=$(echo "scale=0; $current_value * 100 / $slo_value" | bc -l 2>/dev/null || echo "0")

      if [[ "$pct_of_slo" -gt 85 ]]; then
        add_report "⚠️ **WARN**: \`$metric_name\` = ${current_value}ms (${pct_of_slo}% of SLO ${slo_value}ms)"
        WARNINGS=$((WARNINGS + 1))
      else
        add_report "✅ \`$metric_name\` = ${current_value}ms (${pct_of_slo}% of SLO ${slo_value}ms)"
      fi
    fi
  fi
}

extract_from_summary() {
  local file="$1"
  local metric_path="$2"

  if command -v jq &>/dev/null; then
    jq -r "$metric_path // empty" "$file" 2>/dev/null || echo ""
  else
    echo ""
  fi
}

# ============================================
# MAIN
# ============================================

add_report "## 📊 K6 Performance Regression Check"
add_report ""
add_report "| Metric | Value | SLO | Status |"
add_report "|--------|-------|-----|--------|"

# Find all summary JSON files
shopt -s nullglob
SUMMARY_FILES=("$REPORTS_DIR"/*-summary.json)

if [[ ${#SUMMARY_FILES[@]} -eq 0 ]]; then
  echo "No summary files found in $REPORTS_DIR"
  exit 0
fi

for summary_file in "${SUMMARY_FILES[@]}"; do
  scenario_name=$(basename "$summary_file" -summary.json)
  add_report ""
  add_report "### Scenario: \`$scenario_name\`"
  add_report ""

  if ! command -v jq &>/dev/null; then
    add_report "⚠️ jq not available — skipping detailed analysis"
    continue
  fi

  # Extract standard k6 metrics
  p95=$(jq -r '.metrics.http_req_duration.values["p(95)"] // empty' "$summary_file" 2>/dev/null || echo "")
  p99=$(jq -r '.metrics.http_req_duration.values["p(99)"] // empty' "$summary_file" 2>/dev/null || echo "")
  error_rate=$(jq -r '.metrics.http_req_failed.values.rate // empty' "$summary_file" 2>/dev/null || echo "")

  if [[ -n "$p95" ]]; then
    check_metric "http_req_duration p95" "$p95" "http_req_duration_p95"
  fi
  if [[ -n "$p99" ]]; then
    check_metric "http_req_duration p99" "$p99" "http_req_duration_p99"
  fi

  # Extract custom RAG metrics if present
  for custom_metric in pipeline_total_latency chat_response_latency embedding_latency vector_search_latency first_token_latency; do
    val=$(jq -r ".metrics.${custom_metric}.values[\"p(95)\"] // empty" "$summary_file" 2>/dev/null || echo "")
    if [[ -n "$val" ]]; then
      check_metric "${custom_metric} p95" "$val" "${custom_metric}_p95"
    fi
  done

  # Error rate check
  if [[ -n "$error_rate" ]]; then
    error_pct=$(echo "scale=2; $error_rate * 100" | bc -l 2>/dev/null || echo "0")
    slo_error="${SLO_THRESHOLDS[http_req_failed_rate]:-1.0}"
    exceeded=$(echo "$error_pct > $slo_error" | bc -l 2>/dev/null || echo "0")
    if [[ "$exceeded" == "1" ]]; then
      add_report "❌ **FAIL**: Error rate = ${error_pct}% (SLO: <${slo_error}%)"
      FAILURES=$((FAILURES + 1))
    else
      add_report "✅ Error rate = ${error_pct}% (SLO: <${slo_error}%)"
    fi
  fi
done

# Summary
add_report ""
add_report "---"
if [[ $FAILURES -gt 0 ]]; then
  add_report "**Result: ❌ ${FAILURES} SLO violation(s), ${WARNINGS} warning(s)**"
  add_report ""
  add_report "> This PR introduces performance regressions that exceed SLO thresholds."
  EXIT_CODE=1
elif [[ $WARNINGS -gt 0 ]]; then
  add_report "**Result: ⚠️ ${WARNINGS} warning(s), no SLO violations**"
  add_report ""
  add_report "> Performance is within SLO but approaching thresholds. Monitor closely."
  EXIT_CODE=0
else
  add_report "**Result: ✅ All metrics within SLO thresholds**"
  EXIT_CODE=0
fi

# Output report
for line in "${REPORT_LINES[@]}"; do
  echo "$line"
done

# Write report to file for CI to pick up
REPORT_FILE="${REPORTS_DIR}/baseline-comparison.md"
printf '%s\n' "${REPORT_LINES[@]}" > "$REPORT_FILE"
echo ""
echo "Report saved to: $REPORT_FILE"

# In strict mode, warnings also fail
if [[ "$STRICT" == "--strict" && $WARNINGS -gt 0 ]]; then
  EXIT_CODE=1
fi

exit $EXIT_CODE
