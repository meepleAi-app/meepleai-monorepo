/**
 * Standalone test script for escapeRoutePattern function
 *
 * Security tests for CWE-94, CWE-116 (incomplete regex sanitization)
 * Verifies that ALL regex metacharacters are properly escaped to prevent injection
 *
 * Run with: tsx e2e/fixtures/__tests__/escapeRoutePattern.test.ts
 *
 * @see https://codeql.github.com/codeql-query-help/javascript/js-incomplete-sanitization/
 */

/**
 * Copy of escapeRoutePattern from auth.ts for testing
 */
function escapeRoutePattern(routePattern: string): string {
  const DOUBLE_WILDCARD = '__DOUBLE_WILDCARD_PLACEHOLDER__';
  const SINGLE_WILDCARD = '__SINGLE_WILDCARD_PLACEHOLDER__';

  return routePattern
    .replace(/\*\*/g, DOUBLE_WILDCARD)
    .replace(/\*/g, SINGLE_WILDCARD)
    .replace(/[.+?()[\]{}|^$\\/]/g, '\\$&')
    .replace(new RegExp(DOUBLE_WILDCARD, 'g'), '.*')
    .replace(new RegExp(SINGLE_WILDCARD, 'g'), '[^/]*');
}

// Test helpers
let totalTests = 0;
let passedTests = 0;
let failedTests = 0;

function test(name: string, fn: () => void) {
  totalTests++;
  try {
    fn();
    passedTests++;
    console.log(`✅ ${name}`);
  } catch (error) {
    failedTests++;
    console.error(`❌ ${name}`);
    console.error(`   ${error instanceof Error ? error.message : String(error)}`);
  }
}

function assertEqual(actual: unknown, expected: unknown, message?: string) {
  if (actual !== expected) {
    throw new Error(
      message || `Expected ${JSON.stringify(expected)} but got ${JSON.stringify(actual)}`
    );
  }
}

function assertTrue(condition: boolean, message?: string) {
  if (!condition) {
    throw new Error(message || 'Expected true but got false');
  }
}

function assertFalse(condition: boolean, message?: string) {
  if (condition) {
    throw new Error(message || 'Expected false but got true');
  }
}

// Run tests
console.log('\n🔒 Security Tests for escapeRoutePattern\n');
console.log('Testing wildcard handling...\n');

test('should convert ** to .* (match any path)', () => {
  const result = escapeRoutePattern('/admin/**');
  assertEqual(result, '\\/admin\\/.*');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/admin/users'), 'Should match /admin/users');
  assertTrue(regex.test('/admin/users/123'), 'Should match /admin/users/123');
  assertTrue(regex.test('/admin/'), 'Should match /admin/');
});

test('should convert * to [^/]* (match within segment)', () => {
  const result = escapeRoutePattern('/user/*');
  assertEqual(result, '\\/user\\/[^/]*');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/user/123'), 'Should match /user/123');
  assertTrue(regex.test('/user/abc'), 'Should match /user/abc');
  assertFalse(regex.test('/user/123/profile'), 'Should NOT match nested paths');
});

test('should handle multiple wildcards in same pattern', () => {
  const result = escapeRoutePattern('/api/*/v1/**');
  assertEqual(result, '\\/api\\/[^/]*\\/v1\\/.*');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/api/users/v1/list'), 'Should match /api/users/v1/list');
  assertTrue(regex.test('/api/games/v1/detail/123'), 'Should match /api/games/v1/detail/123');
});

console.log('\n🛡️  Testing regex metacharacter escaping (SECURITY CRITICAL)...\n');

test('should escape dots to prevent matching any character', () => {
  const result = escapeRoutePattern('/api.v1');
  assertEqual(result, '\\/api\\.v1');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/api.v1'), 'Should match /api.v1');
  assertFalse(regex.test('/apixv1'), 'CRITICAL: dot should NOT match any char');
});

test('should escape question marks', () => {
  const result = escapeRoutePattern('/admin?');
  assertEqual(result, '\\/admin\\?');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/admin?'), 'Should match /admin?');
  assertFalse(regex.test('/admin'), '? should NOT make previous char optional');
});

test('should escape plus signs', () => {
  const result = escapeRoutePattern('/path+');
  assertEqual(result, '\\/path\\+');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/path+'), 'Should match /path+');
  assertFalse(regex.test('/pathhh'), '+ should NOT mean "one or more"');
});

test('should escape parentheses', () => {
  const result = escapeRoutePattern('/api(v1)');
  assertEqual(result, '\\/api\\(v1\\)');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/api(v1)'), 'Should match /api(v1)');
  assertFalse(regex.test('/api'), 'Parentheses should NOT create groups');
});

test('should escape square brackets', () => {
  const result = escapeRoutePattern('/api[v1]');
  assertEqual(result, '\\/api\\[v1\\]');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/api[v1]'), 'Should match /api[v1]');
  assertFalse(regex.test('/apiv'), 'Brackets should NOT match char class');
  assertFalse(regex.test('/api1'), 'Brackets should NOT match char class');
});

test('should escape curly braces', () => {
  const result = escapeRoutePattern('/path{1,3}');
  assertEqual(result, '\\/path\\{1,3\\}');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/path{1,3}'), 'Should match /path{1,3}');
  assertFalse(regex.test('/p'), 'Braces should NOT create quantifiers');
});

test('should escape pipe (alternation)', () => {
  const result = escapeRoutePattern('/admin|user');
  assertEqual(result, '\\/admin\\|user');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/admin|user'), 'Should match /admin|user');
  assertFalse(regex.test('/admin'), 'Pipe should NOT mean "or"');
  assertFalse(regex.test('/user'), 'Pipe should NOT mean "or"');
});

test('should escape backslashes', () => {
  const result = escapeRoutePattern('/path\\test');
  assertEqual(result, '\\/path\\\\test');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/path\\test'), 'Should match /path\\test');
});

console.log('\n🚨 Testing complex injection attack vectors...\n');

test('should prevent .* injection attack', () => {
  // Note: * is a wildcard, so /admin?.* becomes /admin?.[^/]* (dot + single-segment wildcard)
  const result = escapeRoutePattern('/admin?.*');
  assertEqual(result, '\\/admin\\?\\.[^/]*');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/admin?.foo'), 'Should match /admin?.foo (dot + wildcard)');
  assertTrue(regex.test('/admin?.123'), 'Should match /admin?.123 (dot + wildcard)');
  assertFalse(regex.test('/admin?foo'), 'Should NOT match without dot');
  assertFalse(regex.test('/admin'), 'Should NOT match /admin');
  assertFalse(regex.test('/admin?.foo/bar'), 'Should NOT match nested paths (single wildcard)');
});

test('should prevent character class injection', () => {
  const result = escapeRoutePattern('/api[v1]');
  assertEqual(result, '\\/api\\[v1\\]');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/api[v1]'), 'Should match /api[v1]');
  assertFalse(regex.test('/apiv'), 'CRITICAL: should NOT match char class');
  assertFalse(regex.test('/api1'), 'CRITICAL: should NOT match char class');
});

test('should prevent alternation injection', () => {
  const result = escapeRoutePattern('/admin|/user');
  assertEqual(result, '\\/admin\\|\\/user');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/admin|/user'), 'Should match /admin|/user');
  assertFalse(regex.test('/admin'), 'CRITICAL: should NOT match alternation');
  assertFalse(regex.test('/user'), 'CRITICAL: should NOT match alternation');
});

test('should prevent quantifier injection', () => {
  const result = escapeRoutePattern('/a+');
  assertEqual(result, '\\/a\\+');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/a+'), 'Should match /a+');
  assertFalse(regex.test('/aaaa'), 'CRITICAL: + should NOT mean "one or more"');
});

test('should handle complex mixed injection attempts', () => {
  // Note: * is treated as wildcard, so .* becomes .[^/]* (dot + single-segment wildcard)
  const result = escapeRoutePattern('/api.*(admin|user)?[0-9]+');
  assertEqual(result, '\\/api\\.[^/]*\\(admin\\|user\\)\\?\\[0-9\\]\\+');

  const regex = new RegExp(`^${result}$`);
  assertTrue(regex.test('/api.foo(admin|user)?[0-9]+'), 'Should match with wildcard expanded');
  assertTrue(regex.test('/api.123(admin|user)?[0-9]+'), 'Should match with wildcard expanded');
  // Verify that metacharacters OTHER than * are properly escaped
  assertFalse(regex.test('/apifoo(admin|user)?[0-9]+'), 'Dot should be literal (not match any char)');
  assertFalse(regex.test('/api.foo'), 'Parentheses should be literal (not grouping)');
  assertFalse(regex.test('/api.fooadmin'), 'Pipe should be literal (not alternation)');
});

console.log('\n📋 Testing edge cases...\n');

test('should handle empty string', () => {
  const result = escapeRoutePattern('');
  assertEqual(result, '');
});

test('should handle root path', () => {
  const result = escapeRoutePattern('/');
  assertEqual(result, '\\/');
});

test('should handle path with no special characters', () => {
  const result = escapeRoutePattern('/admin/users');
  assertEqual(result, '\\/admin\\/users');
});

console.log('\n🎯 Testing real-world RBAC patterns...\n');

test('should correctly handle /admin/** pattern', () => {
  const result = escapeRoutePattern('/admin/**');
  const regex = new RegExp(`^${result}$`);

  assertTrue(regex.test('/admin/users'), 'Should match /admin/users');
  assertTrue(regex.test('/admin/configuration'), 'Should match /admin/configuration');
  assertTrue(regex.test('/admin/analytics'), 'Should match /admin/analytics');
  assertFalse(regex.test('/user/admin'), 'Should NOT match /user/admin');
  assertFalse(regex.test('/admin'), 'Should NOT match /admin (no trailing slash)');
});

test('should correctly handle /editor/** pattern', () => {
  const result = escapeRoutePattern('/editor/**');
  const regex = new RegExp(`^${result}$`);

  assertTrue(regex.test('/editor/rules'), 'Should match /editor/rules');
  assertTrue(regex.test('/editor/versions'), 'Should match /editor/versions');
  assertFalse(regex.test('/editor'), 'Should NOT match /editor');
});

// Summary
console.log('\n' + '='.repeat(60));
console.log(`\n📊 Test Results: ${passedTests}/${totalTests} passed`);

if (failedTests > 0) {
  console.log(`\n❌ ${failedTests} test(s) failed\n`);
  process.exit(1);
} else {
  console.log('\n✅ All tests passed! Security fix verified.\n');
  process.exit(0);
}
