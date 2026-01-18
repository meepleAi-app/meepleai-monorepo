#!/bin/bash
# Quick cleanup for orphaned Testcontainers and test processes
# Usage: ./tools/cleanup/cleanup-testcontainers.sh

echo "🧹 Cleaning up Testcontainers and test processes..."

# Count orphaned containers
ORPHANED_COUNT=$(docker ps -a --filter "label=org.testcontainers=true" -q 2>/dev/null | wc -l)

if [ "$ORPHANED_COUNT" -gt 0 ]; then
    echo "   Found $ORPHANED_COUNT orphaned Testcontainers"
    docker ps -a --filter "label=org.testcontainers=true" -q | xargs docker rm -f 2>/dev/null
    echo "   ✅ Removed orphaned containers"
else
    echo "   ✅ No orphaned Testcontainers found"
fi

# Cleanup testhost processes on Windows
if [ -f "tools/cleanup/cleanup-test-processes.ps1" ] && command -v powershell &> /dev/null; then
    echo "   Cleaning up testhost processes..."
    powershell -ExecutionPolicy Bypass -File "tools/cleanup/cleanup-test-processes.ps1" -TestHostOnly 2>/dev/null || true
fi

echo ""
echo "✅ Cleanup complete!"
echo ""
echo "Current Docker containers:"
docker ps --format "table {{.ID}}\t{{.Image}}\t{{.Status}}" 2>/dev/null | head -10
