#!/bin/bash
#
# Migrate Jest tests to Vitest (Issue #1503)
#
# This script performs bulk find-replace transformations to convert
# Jest test syntax to Vitest across all test files.
#
# Usage: bash scripts/migrate-jest-to-vitest.sh [--dry-run]
#

DRY_RUN=false
if [[ "$1" == "--dry-run" ]]; then
  DRY_RUN=true
  echo "🔍 DRY RUN MODE - No files will be modified"
  echo ""
fi

# Find all test files in src directory
TEST_FILES=$(find src -type f \( -name "*.test.ts" -o -name "*.test.tsx" -o -name "*.spec.ts" -o -name "*.spec.tsx" \) ! -path "*/node_modules/*")

COUNT=$(echo "$TEST_FILES" | wc -l)
echo "📊 Found $COUNT test files to migrate"
echo ""

if [ "$DRY_RUN" = true ]; then
  echo "Would process these files:"
  echo "$TEST_FILES" | head -20
  echo "... and $(($COUNT - 20)) more"
  exit 0
fi

# Counter for progress
PROCESSED=0

for file in $TEST_FILES; do
  PROCESSED=$((PROCESSED + 1))
  echo "[$PROCESSED/$COUNT] Processing: $file"

  # 1. Replace Jest imports with Vitest imports
  sed -i "s/from '@jest\/globals'/from 'vitest'/g" "$file"
  sed -i "s/import { describe, it, expect, jest, beforeEach, afterEach } from '@jest\/globals'/import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'/g" "$file"

  # 2. Replace jest.* APIs with vi.*
  sed -i 's/jest\.fn(/vi.fn(/g' "$file"
  sed -i 's/jest\.spyOn(/vi.spyOn(/g' "$file"
  sed -i 's/jest\.mock(/vi.mock(/g' "$file"
  sed -i 's/jest\.unmock(/vi.unmock(/g' "$file"
  sed -i 's/jest\.clearAllMocks(/vi.clearAllMocks(/g' "$file"
  sed -i 's/jest\.resetAllMocks(/vi.resetAllMocks(/g' "$file"
  sed -i 's/jest\.restoreAllMocks(/vi.restoreAllMocks(/g' "$file"
  sed -i 's/jest\.useFakeTimers(/vi.useFakeTimers(/g' "$file"
  sed -i 's/jest\.useRealTimers(/vi.useRealTimers(/g' "$file"
  sed -i 's/jest\.advanceTimersByTime(/vi.advanceTimersByTime(/g' "$file"
  sed -i 's/jest\.runOnlyPendingTimers(/vi.runOnlyPendingTimers(/g' "$file"
  sed -i 's/jest\.runAllTimers(/vi.runAllTimers(/g' "$file"
  sed -i 's/jest\.clearAllTimers(/vi.clearAllTimers(/g' "$file"

  # 3. Replace type references
  sed -i 's/jest\.Mock/Mock/g' "$file"
  sed -i 's/jest\.MockedFunction/Mock/g' "$file"

  # 4. Replace global.fetch mock assignments
  sed -i 's/^global\.fetch = jest\.fn()/\/\/ Removed: global.fetch mock (now using MSW)/g' "$file"

done

echo ""
echo "✅ Migration complete!"
echo "📝 Next steps:"
echo "   1. Run: pnpm test"
echo "   2. Fix any remaining issues (fetch spies, type imports)"
echo "   3. Review changes: git diff"
