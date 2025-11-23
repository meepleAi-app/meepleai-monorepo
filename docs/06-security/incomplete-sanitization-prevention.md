# Incomplete Sanitization Prevention Guide

**Status**: ✅ RESOLVED
**Date**: 2025-11-23
**CWE**: CWE-116 (Incomplete String Escaping or Encoding)
**CodeQL Rule**: js/incomplete-sanitization

---

## Executive Summary

This document describes the **incomplete sanitization vulnerability** that was detected by CodeQL in our codebase and the comprehensive solution implemented to prevent it from recurring.

**Problem**: Using `.replace(/"/g, '\\"')` alone doesn't escape backslashes, allowing injection attacks.
**Solution**: Created `escapePrometheusLabelValue()` utility and replaced all vulnerable code.

---

## The Vulnerability

### What is Incomplete Sanitization?

When escaping strings for inclusion in structured formats (like Prometheus metrics, JSON, etc.), it's critical to escape special characters in the **correct order**. Failing to do so can allow attackers to bypass the sanitization.

### Attack Example

```typescript
// ❌ VULNERABLE CODE
const unsafe = (input: string) => {
  return input.replace(/"/g, '\\"');
};

// Attack scenario:
const malicious = 'test\\"attack';
const escaped = unsafe(malicious);
// Result: test\\"attack

// When parsed by Prometheus:
// The backslash escapes the next backslash, leaving the quote unescaped!
// Parsed as: test\"attack
// This allows the attacker to inject arbitrary metrics
```

### Why It's Dangerous

In Prometheus metrics format:
```
metric_name{label="value"} 123
```

An attacker can inject:
```typescript
const attack = 'value"\nmalicious_metric{} 999';
// After incomplete escaping: value\"\nmalicious_metric{} 999
// Injected metric: malicious_metric{} 999
```

---

## The Solution

### Correct Escaping Order

To prevent this vulnerability, special characters **must be escaped in the correct order**:

1. **Escape backslashes FIRST** (`\` → `\\`)
2. **Escape quotes SECOND** (`"` → `\"`)
3. **Escape newlines** (`\n` → `\\n`)
4. **Escape carriage returns** (`\r` → `\\r`)

### Utility Function

We've created a secure utility function in `src/lib/api/core/prometheusUtils.ts`:

```typescript
export function escapePrometheusLabelValue(value: string): string {
  return value
    .replace(/\\/g, '\\\\')  // 1. Escape backslashes FIRST
    .replace(/"/g, '\\"')     // 2. Escape quotes SECOND
    .replace(/\n/g, '\\n')    // 3. Escape newlines
    .replace(/\r/g, '\\r');   // 4. Escape carriage returns
}
```

### Correct Usage

```typescript
// ✅ SECURE CODE
import { escapePrometheusLabelValue } from './prometheusUtils';

const endpoint = '/api/test\\"malicious';
const escaped = escapePrometheusLabelValue(endpoint);
// Result: /api/test\\\\\\"malicious
// Properly escaped! No injection possible.

// Use in Prometheus metrics:
lines.push(`http_requests{endpoint="${escaped}"} 100`);
```

---

## Fixed Files

### 1. `src/lib/api/core/metrics.ts`

**Before** (Line 131):
```typescript
const escapedEndpoint = endpoint.replace(/"/g, '\\"');  // ❌ VULNERABLE
```

**After**:
```typescript
import { escapePrometheusLabelValue } from './prometheusUtils';

const escapedEndpoint = escapePrometheusLabelValue(endpoint);  // ✅ SECURE
```

### 2. `src/lib/api/core/circuitBreaker.ts`

**Before** (Lines 332, 340, 348):
```typescript
const escapedEndpoint = endpoint.replace(/"/g, '\\"');  // ❌ VULNERABLE (3 occurrences)
```

**After**:
```typescript
import { escapePrometheusLabelValue } from './prometheusUtils';

const escapedEndpoint = escapePrometheusLabelValue(endpoint);  // ✅ SECURE
```

**Total fixes**: 4 occurrences across 2 files

---

## Testing

### Comprehensive Test Suite

We've added 60+ test cases in `src/lib/api/core/__tests__/prometheusUtils.test.ts` covering:

1. **Basic functionality**: Safe strings, quotes, backslashes, newlines
2. **Security tests**: Injection attacks, backslash-quote combinations
3. **Edge cases**: Empty strings, Unicode, very long strings
4. **Real-world scenarios**: API endpoints, error messages, file paths

### Key Security Tests

```typescript
it('should prevent backslash-quote injection attacks', () => {
  // The critical test for this vulnerability
  const attack = 'test\\"';
  const escaped = escapePrometheusLabelValue(attack);

  expect(escaped).toBe('test\\\\\\"');  // Both properly escaped
});

it('should prevent metric injection via newlines', () => {
  const attack = 'value\nmalicious_metric{} 999';
  const escaped = escapePrometheusLabelValue(attack);

  expect(escaped).toContain('\\n');  // Newline escaped, injection prevented
});
```

---

## Prevention Strategy

### 1. Use Utility Functions (Primary)

**Always use the provided utility functions** instead of manual escaping:

```typescript
// For Prometheus metrics:
import { escapePrometheusLabelValue } from '@/lib/api/core/prometheusUtils';

// For HTML content:
import { sanitizeHtml } from '@/lib/security/sanitize';

// For URLs:
import { sanitizeUrl } from '@/lib/security/sanitize';
```

### 2. Code Review Checklist

When reviewing code that handles user input or generates structured formats:

- [ ] Are special characters being escaped?
- [ ] Is the escaping done in the correct order? (backslash → quotes → newlines)
- [ ] Is a utility function being used instead of manual `.replace()`?
- [ ] Are there tests for injection attacks?

### 3. CodeQL Scanning

Our CI pipeline includes CodeQL security scanning that detects:
- Incomplete sanitization (js/incomplete-sanitization)
- XSS vulnerabilities (js/xss)
- SQL injection (js/sql-injection)
- Path traversal (js/path-injection)

**GitHub Actions**: `.github/workflows/ci.yml` runs CodeQL on every PR.

### 4. Documentation

All utility functions include:
- JSDoc comments explaining the security implications
- Example attack scenarios
- References to CWE and OWASP guidelines

---

## When to Use Which Utility

| Context | Utility Function | Import From |
|---------|-----------------|-------------|
| **Prometheus metrics** | `escapePrometheusLabelValue()` | `@/lib/api/core/prometheusUtils` |
| **HTML content** | `sanitizeHtml()` | `@/lib/security/sanitize` |
| **User-generated content** | `sanitizeUserContent()` | `@/lib/security/sanitize` |
| **URLs** | `sanitizeUrl()` | `@/lib/security/sanitize` |
| **Plain text** | `htmlToPlainText()` | `@/lib/security/sanitize` |
| **Metric/label names** | `sanitizePrometheusName()` | `@/lib/api/core/prometheusUtils` |

---

## Additional Resources

### Internal Documentation

- [Frontend Security Best Practices](./frontend-security-best-practices.md)
- [Code Scanning Remediation Summary](./code-scanning-remediation-summary.md)
- [Security Testing Guide](../02-development/testing/test-writing-guide.md)

### External References

- [CWE-116: Improper Encoding or Escaping of Output](https://cwe.mitre.org/data/definitions/116.html)
- [OWASP: Injection Prevention](https://cheatsheetseries.owasp.org/cheatsheets/Injection_Prevention_Cheat_Sheet.html)
- [Prometheus Data Model](https://prometheus.io/docs/concepts/data_model/)
- [CodeQL: Incomplete Sanitization](https://codeql.github.com/codeql-query-help/javascript/js-incomplete-sanitization/)

---

## Verification

### Run Tests

```bash
# Run security-focused tests
pnpm test prometheusUtils.test.ts

# Run all frontend tests
pnpm test

# Run with coverage
pnpm test --coverage
```

### Expected Output

```
PASS  src/lib/api/core/__tests__/prometheusUtils.test.ts
  escapePrometheusLabelValue
    Basic functionality
      ✓ should not modify safe strings
      ✓ should escape double quotes
      ✓ should escape backslashes
      ✓ should escape newlines
    Security: Injection attack prevention (CWE-116)
      ✓ should prevent quote injection attacks
      ✓ should prevent backslash-quote injection attacks ⭐ (critical)
      ✓ should handle complex injection attempts
      ✓ should prevent metric injection via newlines

Test Suites: 1 passed, 1 total
Tests:       60 passed, 60 total
```

---

## Security Sign-Off

**Status**: ✅ **ALL VULNERABILITIES RESOLVED**

- ✅ Incomplete sanitization (CWE-116): **4 instances fixed**
- ✅ Utility function created with correct escaping order
- ✅ Comprehensive test suite (60+ tests) with security focus
- ✅ Documentation added for prevention
- ✅ CodeQL scanning enabled for continuous monitoring

**Ready for**: Production deployment

---

**Created**: 2025-11-23
**Last Updated**: 2025-11-23
**Owner**: Security Team
**Reviewers**: Engineering Lead, Security Architect

---

## Quick Reference

### ❌ DON'T DO THIS

```typescript
// WRONG: Incomplete escaping
const escaped = value.replace(/"/g, '\\"');

// WRONG: Escaping in wrong order
const escaped = value.replace(/"/g, '\\"').replace(/\\/g, '\\\\');
```

### ✅ DO THIS

```typescript
// RIGHT: Use utility function
import { escapePrometheusLabelValue } from '@/lib/api/core/prometheusUtils';

const escaped = escapePrometheusLabelValue(value);
```

### 🔍 How to Detect in Code Reviews

Look for these patterns:
```typescript
.replace(/"/g, '\\"')       // 🚨 RED FLAG
.replace(/'/g, "\\'")       // 🚨 RED FLAG
.replace(/`/g, '\\`')       // 🚨 RED FLAG
```

If you see these patterns, ask:
1. Is this escaping backslashes first?
2. Should we use a utility function instead?

---

**Remember**: Security is not optional. Always use the provided utilities for escaping and sanitization.
