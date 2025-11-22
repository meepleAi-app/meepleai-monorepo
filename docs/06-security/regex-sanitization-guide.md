# RegEx Sanitization Security Guide

**Status**: ✅ **ACTIVE**
**Created**: 2025-11-22
**Last Updated**: 2025-11-22
**Severity**: HIGH (CWE-94, CWE-116)
**CodeQL Rule**: `js/incomplete-sanitization`

---

## Executive Summary

This guide addresses **incomplete regex sanitization** vulnerabilities detected by CodeQL. When constructing regular expressions dynamically from user input or variable data, **all special characters must be properly escaped** to prevent injection attacks and unexpected behavior.

### Fixed Warning

**File**: `apps/web/e2e/fixtures/auth.ts:257`
**Issue**: Backslash characters were not escaped before regex construction
**Impact**: Potential regex injection allowing unauthorized route access
**Fix**: Created `escapeRoutePattern()` utility with proper character escaping order

---

## The Vulnerability

### ❌ Vulnerable Pattern

```typescript
// VULNERABLE: Backslashes are NOT escaped
const regexPattern = routePattern
  .replace(/\*\*/g, '.*')      // Replace ** with .*
  .replace(/\*/g, '[^/]*')     // Replace * with [^/]*
  .replace(/\//g, '\\/');      // Escape forward slashes

await page.route(new RegExp(regexPattern), handler);
```

**Problem**: If `routePattern` contains backslashes (e.g., `/api\test`), they are **not escaped** and will be interpreted as regex escape sequences, potentially breaking the pattern or allowing injection.

### Why This is Dangerous

1. **Unexpected Matching**: `\t` becomes tab, `\n` becomes newline
2. **Pattern Bypass**: Attackers can craft inputs to bypass route restrictions
3. **Injection Risk**: Malicious patterns can match unintended routes
4. **Silent Failures**: Tests may pass but production behaves differently

---

## The Solution

### ✅ Secure Pattern

```typescript
/**
 * Safely converts a route pattern to a RegExp pattern string
 * Escapes all special regex characters to prevent injection attacks
 *
 * @security Addresses CodeQL warning js/incomplete-sanitization
 * @param routePattern - Route pattern with wildcards (e.g., '/admin/**')
 * @returns Escaped regex pattern string safe for use in new RegExp()
 */
function escapeRoutePattern(routePattern: string): string {
  // CRITICAL: Escape backslashes FIRST to prevent injection
  // Order matters! Backslashes must be escaped before other characters
  return routePattern
    .replace(/\\/g, '\\\\')     // 1. Escape backslashes FIRST (security fix)
    .replace(/\*\*/g, '.*')     // 2. Replace ** with .* (match any path)
    .replace(/\*/g, '[^/]*')    // 3. Replace * with [^/]* (match within segment)
    .replace(/\//g, '\\/');     // 4. Escape forward slashes last
}

// Usage
const regexPattern = escapeRoutePattern(forbiddenRoute);
await page.route(new RegExp(regexPattern), handler);
```

### Key Principles

1. **Escape backslashes FIRST**: This prevents them from escaping subsequent replacements
2. **Order matters**: Process backslashes before any other character substitutions
3. **Document security**: Add `@security` JSDoc tags explaining the fix
4. **Test edge cases**: Include tests with backslashes, wildcards, and special chars

---

## Standard Regex Escaping Utility

For general-purpose regex escaping (not route patterns), use this standard pattern:

```typescript
/**
 * Escapes all special regex characters in a string
 * Use this when you need to match a string literally in a regex
 *
 * @param str - String to escape
 * @returns Escaped string safe for use in RegExp
 *
 * @example
 * escapeRegex('Hello. (World)*') // => 'Hello\\. \\(World\\)\\*'
 */
function escapeRegex(str: string): string {
  // Escapes: . * + ? ^ $ { } ( ) | [ ] \
  return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
```

This pattern is already used correctly in:
- `apps/web/src/__tests__/pages/admin-prompts-version-detail.test.tsx:148`
- `apps/web/e2e/fixtures/i18n.ts:143-146`

---

## Common Mistakes to Avoid

### ❌ Don't Use String Interpolation with Unescaped Input

```typescript
// WRONG: User input not escaped
const userPattern = getUserInput();
const regex = new RegExp(`prefix${userPattern}suffix`);
```

### ❌ Don't Forget Backslash Escaping

```typescript
// WRONG: Only escapes forward slashes
const pattern = input.replace(/\//g, '\\/');
```

### ❌ Don't Escape After Substitutions

```typescript
// WRONG: Backslash escaping done too late
const pattern = input
  .replace(/\*/g, '.*')
  .replace(/\\/g, '\\\\');  // Too late! Wildcards already inserted
```

### ✅ Do Escape Before Substitutions

```typescript
// CORRECT: Backslash escaping done first
const pattern = input
  .replace(/\\/g, '\\\\')   // First!
  .replace(/\*/g, '.*');    // Then wildcards
```

---

## Testing Requirements

When implementing regex escaping, include these test cases:

```typescript
describe('escapeRoutePattern', () => {
  it('should escape backslashes', () => {
    expect(escapeRoutePattern('/api\\test')).toBe('\\/api\\\\\\\\test');
  });

  it('should handle wildcards', () => {
    expect(escapeRoutePattern('/admin/**')).toBe('\\/admin\\/.*');
    expect(escapeRoutePattern('/users/*')).toBe('\\/users\\/[^/]*');
  });

  it('should handle combined patterns', () => {
    expect(escapeRoutePattern('/api\\v1/admin/**')).toBe('\\/api\\\\\\\\v1\\/admin\\/.*');
  });

  it('should escape forward slashes', () => {
    expect(escapeRoutePattern('/path/to/route')).toBe('\\/path\\/to\\/route');
  });
});
```

---

## Verification Checklist

Before committing regex construction code:

- [ ] Backslashes are escaped **first** in the chain
- [ ] All special regex characters are properly escaped
- [ ] Function includes `@security` JSDoc comment
- [ ] Tests cover edge cases (backslashes, wildcards, special chars)
- [ ] CodeQL scan passes without `js/incomplete-sanitization` warnings
- [ ] Manual testing confirms expected behavior

---

## Related Security Issues

### Fixed Issues

✅ **auth.ts:257** - Route pattern escaping (2025-11-22)
- Added `escapeRoutePattern()` utility
- Properly escapes backslashes before wildcards
- Documented security considerations

### Related Vulnerabilities (CWE)

- **CWE-94**: Improper Control of Generation of Code ('Code Injection')
- **CWE-116**: Improper Encoding or Escaping of Output
- **CWE-185**: Incorrect Regular Expression
- **CWE-625**: Permissive Regular Expression

---

## References

### CodeQL Documentation

- [Incomplete sanitization](https://codeql.github.com/codeql-query-help/javascript/js-incomplete-sanitization/)
- [Regular expression injection](https://codeql.github.com/codeql-query-help/javascript/js-regex-injection/)

### OWASP

- [Input Validation Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html)
- [Injection Prevention](https://owasp.org/www-community/Injection_Prevention_Cheat_Sheet)

### MDN

- [Regular Expressions - Escaping](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Regular_Expressions#escaping)

---

## Implementation Examples

### Playwright Route Mocking (E2E Tests)

```typescript
// ✅ SECURE: Using escapeRoutePattern utility
import { escapeRoutePattern } from './utils/regex';

const forbiddenRoutes = ['/admin/**', '/api/sensitive/*'];
for (const routePattern of forbiddenRoutes) {
  const regexPattern = escapeRoutePattern(routePattern);
  await page.route(new RegExp(regexPattern), async (route) => {
    await route.fulfill({ status: 403, body: 'Forbidden' });
  });
}
```

### URL Path Matching

```typescript
// ✅ SECURE: Escape user input before regex
function matchesPath(url: string, pattern: string): boolean {
  const escapedPattern = escapeRoutePattern(pattern);
  return new RegExp(`^${escapedPattern}$`).test(url);
}
```

### String Literal Matching

```typescript
// ✅ SECURE: Use standard escape for literal matching
function containsText(haystack: string, needle: string): boolean {
  const escapedNeedle = needle.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  return new RegExp(escapedNeedle).test(haystack);
}
```

---

## Enforcement

### CI/CD Pipeline

The following security checks are enforced in CI:

1. **CodeQL SAST**: Scans for `js/incomplete-sanitization` on every PR
2. **Manual Review**: Security-sensitive files require code review
3. **Test Coverage**: Regex utilities must have 90%+ test coverage

### Pre-Commit Hooks (Recommended)

Add this to `.husky/pre-commit`:

```bash
# Check for unsafe regex patterns
git diff --cached --name-only | grep -E '\.(ts|tsx|js|jsx)$' | while read file; do
  if git diff --cached "$file" | grep -E 'new RegExp.*\.replace' | grep -v 'escapeRegex\|escapeRoutePattern'; then
    echo "⚠️  WARNING: Potential unsafe regex construction in $file"
    echo "    Use escapeRegex() or escapeRoutePattern() utilities"
    exit 1
  fi
done
```

---

## Contact

For security concerns or questions:

- **Security Team**: Create private security advisory on GitHub
- **Code Review**: Tag `@security-reviewers` on PRs with regex changes
- **Documentation**: Update this guide when new patterns are discovered

---

**Version**: 1.0
**Maintainer**: Engineering Security Team
**Next Review**: 2026-02-22

---

**END OF GUIDE**
