#!/bin/bash
# view-logs.sh - View workflow logs with filtering and formatting

LOG_DIR="/logs"
FILTER="${1}"

echo "📋 GitHub Actions Simulator - Log Viewer"
echo "=========================================="
echo ""

if [ -z "${FILTER}" ]; then
    echo "📁 Available log files:"
    echo ""
    ls -lth "${LOG_DIR}"/*.log 2>/dev/null | head -20 || echo "No logs found"
    echo ""
    echo "Usage: view-logs.sh <log-file-or-pattern>"
    echo "Example: view-logs.sh ci-api"
    echo "Example: view-logs.sh 20241026"
    exit 0
fi

# Find matching logs
MATCHING_LOGS=$(ls -t "${LOG_DIR}"/*${FILTER}*.log 2>/dev/null)

if [ -z "${MATCHING_LOGS}" ]; then
    echo "❌ No logs matching '${FILTER}' found"
    exit 1
fi

# Show the most recent matching log
LATEST_LOG=$(echo "${MATCHING_LOGS}" | head -1)

echo "📝 Viewing: $(basename ${LATEST_LOG})"
echo "=========================================="
echo ""

# Check if we should show full log or summary
if [ "$2" == "--full" ]; then
    cat "${LATEST_LOG}"
else
    # Show summary: errors, warnings, and final status
    echo "🔍 Error Summary:"
    grep -i "error\|fail\|❌" "${LATEST_LOG}" | tail -20 || echo "  No errors found ✅"

    echo ""
    echo "⚠️  Warning Summary:"
    grep -i "warning\|⚠️" "${LATEST_LOG}" | tail -10 || echo "  No warnings ✅"

    echo ""
    echo "📊 Final Status:"
    tail -20 "${LATEST_LOG}"

    echo ""
    echo "💡 Tip: Use --full flag to see complete log"
    echo "   Example: view-logs.sh ${FILTER} --full"
fi

echo ""
echo "📄 Full log location: ${LATEST_LOG}"
