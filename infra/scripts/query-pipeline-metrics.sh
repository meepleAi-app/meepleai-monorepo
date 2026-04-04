#!/usr/bin/env bash
# =============================================================================
# MeepleAI Pipeline Metrics Query — Quick staging metrics check
# =============================================================================
# Queries existing admin endpoints to show current pipeline performance.
#
# Usage:
#   ADMIN_COOKIE='session=...' ./query-pipeline-metrics.sh
#   STAGING_USER=admin STAGING_PASS=xxx ADMIN_COOKIE='session=...' ./query-pipeline-metrics.sh
# =============================================================================

set -euo pipefail

STAGING_BASE="https://meepleai.app"
API_BASE="${STAGING_BASE}/api/v1"
EMBEDDING_BASE="${STAGING_BASE}/services/embedding"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

log()  { echo -e "${BLUE}[INFO]${NC} $*"; }
ok()   { echo -e "${GREEN}[ OK ]${NC} $*"; }
warn() { echo -e "${YELLOW}[WARN]${NC} $*"; }
header() { echo -e "\n${CYAN}━━━ $* ━━━${NC}"; }

# --- Auth ---
if [[ -z "${STAGING_USER:-}" ]] || [[ -z "${STAGING_PASS:-}" ]]; then
    echo -n "Basic auth username: "; read -r STAGING_USER
    echo -n "Basic auth password: "; read -rs STAGING_PASS; echo
fi
BASIC_AUTH="${STAGING_USER}:${STAGING_PASS}"

if [[ -z "${ADMIN_COOKIE:-}" ]]; then
    warn "No ADMIN_COOKIE set — API admin endpoints will be skipped."
    warn "Login to staging, copy session cookie, then:"
    warn "  ADMIN_COOKIE='session=abc123' ./query-pipeline-metrics.sh"
fi

# === 1. Embedding Service Health ===
header "EMBEDDING SERVICE"

embed_health=$(curl -sf -u "$BASIC_AUTH" --max-time 5 \
    "${EMBEDDING_BASE}/health" 2>/dev/null || echo '{"status":"unreachable"}')

echo "$embed_health" | jq -r '
    "  Status:     \(.status)",
    "  Model:      \(.model // "unknown")",
    "  Device:     \(.device // "unknown")",
    "  Languages:  \(.supported_languages // [] | join(", "))"
' 2>/dev/null || echo "  Service unreachable"

# === 2. Embedding Prometheus Metrics ===
header "EMBEDDING METRICS (Prometheus)"

embed_metrics=$(curl -sf -u "$BASIC_AUTH" --max-time 5 \
    "${EMBEDDING_BASE}/metrics" 2>/dev/null || echo "")

if [[ -n "$embed_metrics" ]]; then
    requests=$(echo "$embed_metrics" | grep '^embed_requests_total' | awk '{print $2}')
    failures=$(echo "$embed_metrics" | grep '^embed_failures_total' | awk '{print $2}')
    duration_sum=$(echo "$embed_metrics" | grep '^embed_duration_ms_sum' | awk '{print $2}')
    chars_sum=$(echo "$embed_metrics" | grep '^embed_total_chars_sum' | awk '{print $2}')

    requests=${requests:-0}
    failures=${failures:-0}
    duration_sum=${duration_sum:-0}
    chars_sum=${chars_sum:-0}

    if [[ "$requests" != "0" ]] && [[ "$requests" != "0.0" ]]; then
        avg_ms=$(echo "scale=1; $duration_sum / $requests" | bc)
        avg_chars=$(echo "scale=0; $chars_sum / $requests" | bc)
        failure_rate=$(echo "scale=1; $failures * 100 / $requests" | bc)
        chars_per_sec=$(echo "scale=0; $chars_sum / ($duration_sum / 1000)" | bc 2>/dev/null || echo "N/A")
        kb_per_sec=$(echo "scale=2; ($chars_sum / 1024) / ($duration_sum / 1000)" | bc 2>/dev/null || echo "N/A")

        echo "  Total requests:     $requests"
        echo "  Total failures:     $failures (${failure_rate}%)"
        echo "  Avg duration:       ${avg_ms} ms/request"
        echo "  Avg chars/request:  $avg_chars"
        echo "  Cumulative chars:   $chars_sum"
        echo "  ─────────────────────────────"
        echo -e "  ${GREEN}Throughput:         ${chars_per_sec} chars/s | ${kb_per_sec} KB/s${NC}"
    else
        echo "  No requests recorded yet (service just started?)"
    fi
else
    echo "  Metrics endpoint unreachable"
fi

# === 3. Pipeline Health (Admin) ===
if [[ -n "${ADMIN_COOKIE:-}" ]]; then
    header "PIPELINE HEALTH"

    pipeline_health=$(curl -sf \
        -H "Cookie: ${ADMIN_COOKIE}" \
        --max-time 10 \
        "${API_BASE}/admin/kb/pipeline/health" 2>/dev/null || echo '{}')

    if echo "$pipeline_health" | jq -e '.stages' &>/dev/null; then
        echo "$pipeline_health" | jq -r '
            .stages[] |
            "  \(.name | ljust(12)) \(
                if .status == "healthy" then "✅ healthy"
                elif .status == "warning" then "⚠️  warning"
                else "❌ error"
                end
            ) \(
                if .metrics.avgDurationMs then "  avg=\(.metrics.avgDurationMs)ms p95=\(.metrics.p95Ms // "?")ms (n=\(.metrics.sampleSize))"
                elif .metrics.requestsTotal then "  requests=\(.metrics.requestsTotal) avg=\(.metrics.avgDurationMs)ms fail=\(.metrics.failureRate)%"
                elif .metrics.activeJobs != null then "  active=\(.metrics.activeJobs) queued=\(.metrics.queuedJobs) failed=\(.metrics.failedJobs)"
                else ""
                end
            )"
        ' 2>/dev/null || echo "  Could not parse pipeline health"

        # Distribution
        echo ""
        echo "$pipeline_health" | jq -r '
            .distribution |
            "  Documents: \(.totalDocuments)  Chunks: \(.totalChunks)  Vectors: \(.vectorCount)  Files: \(.totalFiles)  Storage: \(.storageSizeFormatted)"
        ' 2>/dev/null || true

        # Recent activity
        header "RECENT PROCESSING ACTIVITY"
        echo "$pipeline_health" | jq -r '
            .recentActivity[]? |
            "  \(.fileName | .[0:40] | ljust(40))  \(.status | ljust(10))  \(
                if .durationMs then "\(.durationMs / 1000 | tostring | .[0:6])s"
                else "N/A"
                end
            )"
        ' 2>/dev/null || echo "  No recent activity"
    else
        warn "Pipeline health unavailable (check admin cookie)"
    fi

    # === 4. Processing Step Metrics ===
    header "PROCESSING STEP METRICS (P50/P95/P99)"

    step_metrics=$(curl -sf \
        -H "Cookie: ${ADMIN_COOKIE}" \
        --max-time 10 \
        "${API_BASE}/admin/pdfs/metrics/processing" 2>/dev/null || echo '{}')

    if echo "$step_metrics" | jq -e '.averages' &>/dev/null; then
        echo "$step_metrics" | jq -r '
            .averages | to_entries[] |
            "  \(.key | ljust(15))  avg=\(.value.avgDuration | tostring | .[0:6])s  (n=\(.value.sampleSize))"
        ' 2>/dev/null || echo "  No step metrics available"

        echo ""
        echo "$step_metrics" | jq -r '
            .percentiles | to_entries[] |
            "  \(.key | ljust(15))  P50=\(.value.p50 | tostring | .[0:6])s  P95=\(.value.p95 | tostring | .[0:6])s  P99=\(.value.p99 | tostring | .[0:6])s"
        ' 2>/dev/null || true
    else
        warn "No processing metrics yet (process some PDFs first)"
    fi
fi

# === Summary ===
header "THROUGHPUT SUMMARY"
echo ""
if [[ -n "${kb_per_sec:-}" ]] && [[ "$kb_per_sec" != "N/A" ]]; then
    echo -e "  Embedding service:     ${GREEN}${kb_per_sec} KB/s${NC} (cumulative average)"
    echo -e "  Embedding service:     ${GREEN}${chars_per_sec} chars/s${NC}"
fi
echo "  Pipeline estimate:     ~25 KB/s per worker (based on 12-page/2min reference)"
echo "  Pipeline (3 workers):  ~43 KB/s (sub-linear scaling on 4 vCPU)"
echo ""
echo "  Server: Hetzner CAX21 ARM64 (4 vCPU, 8GB RAM)"
echo "  Model:  $(echo "$embed_health" | jq -r '.model // "unknown"') on $(echo "$embed_health" | jq -r '.device // "unknown"')"
echo ""
