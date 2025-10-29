#!/bin/bash
# cleanup.sh - Clean up old logs and artifacts

echo "🧹 GitHub Actions Simulator - Cleanup"
echo "======================================"
echo ""

DAYS_TO_KEEP="${1:-7}"

echo "📅 Removing files older than ${DAYS_TO_KEEP} days..."
echo ""

# Cleanup logs
echo "🗑️  Cleaning logs..."
LOGS_DELETED=$(find /logs -name "*.log" -type f -mtime +${DAYS_TO_KEEP} -delete -print | wc -l)
echo "   Deleted ${LOGS_DELETED} log files"

# Cleanup artifacts
echo "🗑️  Cleaning artifacts..."
ARTIFACTS_DELETED=$(find /artifacts -type f -mtime +${DAYS_TO_KEEP} -delete -print | wc -l)
echo "   Deleted ${ARTIFACTS_DELETED} artifact files"

# Show disk usage
echo ""
echo "💾 Current disk usage:"
du -sh /logs /artifacts /cache 2>/dev/null || echo "   Unable to calculate"

echo ""
echo "✅ Cleanup complete!"
echo ""
echo "💡 Tip: Run 'cleanup.sh 3' to clean files older than 3 days"
