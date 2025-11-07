#!/bin/bash
echo "=== OAuth E2E Test Fix Verification ==="
echo ""
echo "1. Checking test file exists..."
if [ -f "e2e/auth-oauth-buttons.spec.ts" ]; then
    echo "   ✅ Test file found"
else
    echo "   ❌ Test file missing"
    exit 1
fi

echo ""
echo "2. Checking backup exists..."
if [ -f "e2e/auth-oauth-buttons.spec.ts.backup" ]; then
    echo "   ✅ Backup file found"
else
    echo "   ⚠️  Backup file not found (optional)"
fi

echo ""
echo "3. Checking i18n import..."
if grep -q "import.*getTextMatcher.*from.*fixtures/i18n" e2e/auth-oauth-buttons.spec.ts; then
    echo "   ✅ i18n import present"
else
    echo "   ❌ Missing i18n import"
    exit 1
fi

echo ""
echo "4. Checking mocking setup..."
mock_count=$(grep -c "page.route.*oauth.*login" e2e/auth-oauth-buttons.spec.ts)
if [ "$mock_count" -ge 3 ]; then
    echo "   ✅ OAuth endpoint mocking present (found $mock_count routes)"
else
    echo "   ❌ Missing OAuth endpoint mocks"
    exit 1
fi

echo ""
echo "5. Checking test.skip removed..."
if grep -q "test.describe.skip" e2e/auth-oauth-buttons.spec.ts; then
    echo "   ❌ Test suite still skipped"
    exit 1
else
    echo "   ✅ Test suite enabled (no .skip)"
fi

echo ""
echo "6. Counting tests..."
test_count=$(grep -c "^  test(" e2e/auth-oauth-buttons.spec.ts)
echo "   ℹ️  Found $test_count individual tests"
if [ "$test_count" -eq 16 ]; then
    echo "   ✅ Expected 16 tests (19 total including nested)"
else
    echo "   ⚠️  Test count: $test_count (verify manually)"
fi

echo ""
echo "7. Checking waitForURL usage..."
wait_count=$(grep -c "waitForURL" e2e/auth-oauth-buttons.spec.ts)
if [ "$wait_count" -ge 5 ]; then
    echo "   ✅ waitForURL used for timing ($wait_count occurrences)"
else
    echo "   ❌ Missing waitForURL timing fixes"
    exit 1
fi

echo ""
echo "=== All checks passed! ✅ ==="
echo ""
echo "Next steps:"
echo "  1. Run tests: pnpm test:e2e auth-oauth-buttons.spec.ts"
echo "  2. Verify all 19 tests pass"
echo "  3. Check CI compatibility"
