#!/bin/bash
#
# Fix remaining Vitest import and type issues (Issue #1503)
#
# Handles:
# - Import statements still containing 'jest'
# - MockedFunction → Mock type replacements
# - global.fetch as Mock<typeof fetch> → fetchSpy
# - Missing vi imports
#

DRY_RUN=false
if [[ "$1" == "--dry-run" ]]; then
  DRY_RUN=true
  echo "🔍 DRY RUN MODE"
fi

TEST_FILES=$(find src -type f \( -name "*.test.ts" -o -name "*.test.tsx" \) ! -path "*/node_modules/*")
COUNT=$(echo "$TEST_FILES" | wc -l)

echo "📊 Found $COUNT test files to fix"
echo ""

PROCESSED=0

for file in $TEST_FILES; do
  PROCESSED=$((PROCESSED + 1))

  # Check if file needs fixing
  if grep -q "jest, " "$file" || grep -q "MockedFunction" "$file" || grep -q "from '@jest/globals'" "$file"; then
    echo "[$PROCESSED/$COUNT] Fixing: $file"

    if [ "$DRY_RUN" = false ]; then
      # Fix import statements with jest
      sed -i "s/import { describe, it, expect, jest, beforeEach, afterEach } from 'vitest'/import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'/g" "$file"
      sed -i "s/import { describe, it, expect, jest } from 'vitest'/import { describe, it, expect, vi } from 'vitest'/g" "$file"
      sed -i "s/import { describe, it, expect, jest, beforeEach } from 'vitest'/import { describe, it, expect, beforeEach, vi } from 'vitest'/g" "$file"

      # Replace @jest/globals imports
      sed -i "s/from '@jest\/globals'/from 'vitest'/g" "$file"

      # Add type imports if MockedFunction exists
      if grep -q "MockedFunction" "$file" && ! grep -q "import type { Mock }" "$file"; then
        sed -i "/^import { describe/a import type { Mock } from 'vitest';" "$file"
      fi

      # Replace MockedFunction with Mock
      sed -i "s/MockedFunction<typeof fetch>/Mock<typeof fetch>/g" "$file"
      sed -i "s/MockedFunction/Mock/g" "$file"

      # Replace global.fetch as Mock patterns (if not already using fetchSpy)
      if ! grep -q "fetchSpy" "$file"; then
        sed -i "s/(global\.fetch as Mock<typeof fetch>)\.mockResolvedValueOnce/fetchSpy.mockResolvedValueOnce/g" "$file"
        sed -i "s/(global\.fetch as Mock<typeof fetch>)\.mockRejectedValueOnce/fetchSpy.mockRejectedValueOnce/g" "$file"
        sed -i "s/(global\.fetch as Mock<typeof fetch>)\.mockImplementation/fetchSpy.mockImplementation/g" "$file"
      fi
    fi
  fi
done

echo ""
echo "✅ Import fixes complete!"
