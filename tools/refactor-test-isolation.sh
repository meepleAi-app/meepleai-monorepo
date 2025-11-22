#!/bin/bash
# Script to help identify test files that need refactoring
# Usage: ./tools/refactor-test-isolation.sh

set -e

echo "=== Test Isolation Refactoring Helper ==="
echo ""

# Find all test files with shared DbContext pattern
echo "Files with 'private readonly.*DbContext' pattern:"
echo "=================================================="
cd "$(dirname "$0")/.."
grep -r "private readonly.*DbContext" apps/api/tests/Api.Tests/BoundedContexts --include="*.cs" -l | while read -r file; do
    test_count=$(grep -c "\[Fact\]" "$file" 2>/dev/null || echo "0")
    relative_path=$(echo "$file" | sed 's|apps/api/tests/Api.Tests/||')
    echo "- $relative_path ($test_count tests)"
done

echo ""
echo "=== Summary ==="
total_files=$(grep -r "private readonly.*DbContext" apps/api/tests/Api.Tests/BoundedContexts --include="*.cs" -l | wc -l)
echo "Total files to refactor: $total_files"
