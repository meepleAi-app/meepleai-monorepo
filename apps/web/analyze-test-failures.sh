#!/bin/bash
# Analyze test failures and categorize by pattern

echo "=== Test Failure Pattern Analysis ===" > test-failure-analysis.txt
echo "" >> test-failure-analysis.txt

# Run tests and capture output
pnpm test --run 2>&1 > test-full-output.txt

# Pattern 1: Multiple elements found
echo "## Pattern 1: Multiple Elements Found Errors" >> test-failure-analysis.txt
grep -i "found multiple elements" test-full-output.txt | wc -l >> test-failure-analysis.txt
echo "" >> test-failure-analysis.txt

# Pattern 2: Unable to find element (async issues)
echo "## Pattern 2: Unable to Find Element (Async)" >> test-failure-analysis.txt
grep -i "unable to find" test-full-output.txt | wc -l >> test-failure-analysis.txt
echo "" >> test-failure-analysis.txt

# Pattern 3: TestingLibraryElementError
echo "## Pattern 3: TestingLibraryElementError" >> test-failure-analysis.txt
grep -i "TestingLibraryElementError" test-full-output.txt | wc -l >> test-failure-analysis.txt
echo "" >> test-failure-analysis.txt

# Pattern 4: Dialog accessibility warnings
echo "## Pattern 4: Dialog Accessibility Issues" >> test-failure-analysis.txt
grep -i "DialogContent.*requires.*DialogTitle" test-full-output.txt | wc -l >> test-failure-analysis.txt
echo "" >> test-failure-analysis.txt

# Pattern 5: Auth context issues
echo "## Pattern 5: Auth Context Missing" >> test-failure-analysis.txt
grep -i "useAuth must be used within" test-full-output.txt | wc -l >> test-failure-analysis.txt
echo "" >> test-failure-analysis.txt

# Summary
echo "## Summary" >> test-failure-analysis.txt
grep -E "Test Files.*failed" test-full-output.txt | tail -1 >> test-failure-analysis.txt

cat test-failure-analysis.txt
