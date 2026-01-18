#!/bin/bash
# Script to analyze test failures from dotnet test output
# Usage: ./scripts/analyze-test-failures.sh /tmp/full-test-results.log

LOG_FILE="${1:-/tmp/full-test-results.log}"

echo "📊 Test Failure Analysis"
echo "========================"
echo ""

# Wait for file to exist
if [ ! -f "$LOG_FILE" ]; then
    echo "⏳ Waiting for test results..."
    sleep 10
fi

# Extract summary
echo "## Test Summary"
grep -E "(Passed|Failed|Skipped|Total tests)" "$LOG_FILE" | tail -5
echo ""

# Count failures by type
echo "## Failures by Error Type"
grep -i "failed" "$LOG_FILE" | grep -oE "(TimeoutException|FileNotFoundException|InvalidOperationException|ArgumentException|NullReferenceException)" | sort | uniq -c | sort -rn
echo ""

# Failures by bounded context
echo "## Failures by Bounded Context"
grep "Failed" "$LOG_FILE" | grep -oE "BoundedContexts/[^/]+" | sort | uniq -c | sort -rn
echo ""

# Failed test names
echo "## Failed Test Names (First 20)"
grep -A 1 "Failed" "$LOG_FILE" | grep "Api.Tests" | head -20
echo ""

# Categorize by priority
echo "## Priority Categorization"
echo "### Critical (Auth, Games, Documents)"
grep "Failed" "$LOG_FILE" | grep -E "(Authentication|GameManagement|DocumentProcessing)" | wc -l | xargs echo "Critical failures:"

echo "### Important (KnowledgeBase, Search)"
grep "Failed" "$LOG_FILE" | grep -E "(KnowledgeBase)" | wc -l | xargs echo "Important failures:"

echo "### Other"
grep "Failed" "$LOG_FILE" | grep -vE "(Authentication|GameManagement|DocumentProcessing|KnowledgeBase)" | wc -l | xargs echo "Other failures:"
