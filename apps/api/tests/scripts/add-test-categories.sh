#!/bin/bash
# Add Test Categories to Files Without Them - Issue #1820
# Usage: bash add-test-categories.sh [--dry-run]

set -e

DRY_RUN=false
if [[ "$1" == "--dry-run" ]]; then
    DRY_RUN=true
    echo "🔍 DRY RUN MODE - No files will be modified"
fi

TEST_ROOT="apps/api/tests/Api.Tests"
STATS_MODIFIED=0
STATS_SKIPPED=0
STATS_TOTAL=0

echo "📂 Adding test categories to files without [Trait] attributes..."

# Find all test files without [Trait("Category"
while IFS= read -r file; do
    ((STATS_TOTAL++))

    # Check if file already has Category trait
    if grep -q '\[Trait("Category"' "$file"; then
        ((STATS_SKIPPED++))
        continue
    fi

    # Determine category based on filename
    CATEGORY="Unit"
    if [[ "$file" == *"IntegrationTests.cs" ]] || [[ "$file" == *"E2ETests.cs" ]] || [[ "$file" == *"CrossContext"* ]]; then
        CATEGORY="Integration"
    elif [[ "$file" == *"SecurityTests.cs" ]] || [[ "$file" == *"PenetrationTests.cs" ]]; then
        CATEGORY="Security"
    elif [[ "$file" == *"PerformanceTests.cs" ]] || [[ "$file" == *"BenchmarkTests.cs" ]]; then
        CATEGORY="Performance"
    fi

    if [[ "$DRY_RUN" == true ]]; then
        echo "  [DRY RUN] Would add [$CATEGORY] to: $(basename "$file")"
        ((STATS_MODIFIED++))
    else
        # Add using statement if not present
        if ! grep -q "using Api.Tests.Constants;" "$file"; then
            sed -i '/using Xunit;/a using Api.Tests.Constants;' "$file"
        fi

        # Add [Trait] before public class declaration
        sed -i "/^public \(sealed \)\?class/i [Trait(\"Category\", TestCategories.$CATEGORY)]" "$file"

        ((STATS_MODIFIED++))
        echo "  ✅ Added [$CATEGORY] to: $(basename "$file")"
    fi

done < <(find "$TEST_ROOT" -name "*Tests.cs" -type f ! -path "*/obj/*" ! -path "*/bin/*")

echo ""
echo "📊 Summary:"
echo "  Total files: $STATS_TOTAL"
echo "  Modified: $STATS_MODIFIED"
echo "  Skipped: $STATS_SKIPPED"

if [[ "$DRY_RUN" == true ]]; then
    echo ""
    echo "⚠️  DRY RUN - Run without --dry-run to apply changes"
fi
