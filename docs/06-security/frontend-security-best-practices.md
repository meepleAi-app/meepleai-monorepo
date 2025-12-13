# Frontend Security Best Practices

**Date**: 2025-11-23
**Status**: ✅ ACTIVE
**Owner**: Engineering Team

---

## Executive Summary

This document outlines security best practices for the MeepleAI frontend to prevent common web vulnerabilities (XSS, injection, prototype pollution, etc.). Following these guidelines ensures that CodeQL and other security scanners will not flag security warnings.

**Key Achievement**: Systematic prevention of security vulnerabilities through:
- ESLint security plugins (automatic detection)
- Centralized sanitization utilities
- Pre-commit hooks (blocks unsafe code)
- TypeScript strict mode (type safety)

---

## Table of Contents

1. [XSS Prevention](#xss-prevention)
2. [Input Sanitization](#input-sanitization)
3. [URL Validation](#url-validation)
4. [TypeScript Type Safety](#typescript-type-safety)
5. [ESLint Security Rules](#eslint-security-rules)
6. [Pre-commit Hooks](#pre-commit-hooks)
7. [Common Pitfalls](#common-pitfalls)
8. [Appendix](#appendix)

---

## XSS Prevention

### ❌ NEVER Do This

```tsx
// BAD: Unsafe - allows XSS attacks
function UnsafeComponent({ userContent }: { userContent: string }) {
  return <div dangerouslySetInnerHTML={{ __html: userContent }} />;
}

// BAD: Unsafe - direct DOM manipulation
function UnsafeComponent2({ html }: { html: string }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (ref.current) {
      ref.current.innerHTML = html; // XSS vulnerability
    }
  }, [html]);
  return <div ref={ref} />;
}

// BAD: Unsafe - eval is a security risk
function UnsafeCode({ code }: { code: string }) {
  eval(code); // NEVER use eval
  return null;
}
```

### ✅ Always Do This

```tsx
import { createSafeMarkup, sanitizeHtml } from '@/lib/security';

// GOOD: Sanitized HTML
function SafeComponent({ userContent }: { userContent: string }) {
  return <div dangerouslySetInnerHTML={createSafeMarkup(userContent)} />;
}

// BEST: Use React children instead (no dangerouslySetInnerHTML)
function BestComponent({ text }: { text: string }) {
  return <div>{text}</div>;
}

// GOOD: Sanitize before using
function SafeComponent2({ html }: { html: string }) {
  const safeHtml = sanitizeHtml(html);
  return <div dangerouslySetInnerHTML={{ __html: safeHtml }} />;
}
```

---

## Input Sanitization

### Use Case: Board Game Rules (Trusted Content)

```tsx
import { sanitizeHtml } from '@/lib/security';

function GameRules({ rulesHtml }: { rulesHtml: string }) {
  // Rules from PDF extraction - sanitize with default config
  // Allows tables, lists, formatting
  return <div dangerouslySetInnerHTML={createSafeMarkup(rulesHtml)} />;
}
```

### Use Case: User Comments (Untrusted Content)

```tsx
import { sanitizeUserContent } from '@/lib/security';

function UserComment({ comment }: { comment: string }) {
  // User-generated content - use strict sanitization
  // Removes links, images, only allows basic formatting
  const safeComment = sanitizeUserContent(comment);
  return <div dangerouslySetInnerHTML={{ __html: safeComment }} />;
}
```

### Use Case: Plain Text Output

```tsx
import { htmlToPlainText } from '@/lib/security';

function MetaDescription({ html }: { html: string }) {
  // Convert HTML to plain text for meta tags
  const text = htmlToPlainText(html);
  return <meta name="description" content={text} />;
}
```

---

## URL Validation

### ❌ NEVER Do This

```tsx
// BAD: Unsafe - allows javascript: URLs
function UnsafeLink({ url, text }: { url: string; text: string }) {
  return <a href={url}>{text}</a>;
}

// Usage:
// <UnsafeLink url="javascript:alert('XSS')" text="Click me" />
// Result: XSS attack when user clicks
```

### ✅ Always Do This

```tsx
import { sanitizeUrl, isSafeUrl } from '@/lib/security';

// GOOD: Sanitized URL
function SafeLink({ url, text }: { url: string; text: string }) {
  return <a href={sanitizeUrl(url)}>{text}</a>;
}

// BETTER: Validate before rendering
function SaferLink({ url, text }: { url: string; text: string }) {
  if (!isSafeUrl(url)) {
    return <span title="Invalid URL">{text}</span>;
  }
  return <a href={url} target="_blank" rel="noopener noreferrer">{text}</a>;
}
```

---

## TypeScript Type Safety

### Strict Null Checks

TypeScript strict mode is already enabled (`tsconfig.json: "strict": true`). This prevents many common errors:

```tsx
// ❌ BAD: Nullable type without check
function UnsafeComponent({ user }: { user: User | null }) {
  return <div>{user.name}</div>; // TypeScript error: user might be null
}

// ✅ GOOD: Null check
function SafeComponent({ user }: { user: User | null }) {
  if (!user) return <div>No user</div>;
  return <div>{user.name}</div>;
}

// ✅ BEST: Optional chaining
function BestComponent({ user }: { user: User | null }) {
  return <div>{user?.name ?? 'Anonymous'}</div>;
}
```

### Avoid `any` Type

```tsx
// ❌ BAD: any type disables type checking
function unsafeFunction(data: any) {
  return data.property; // No type safety
}

// ✅ GOOD: Proper typing
interface GameData {
  name: string;
  players: number;
}

function safeFunction(data: GameData) {
  return data.name; // Type-safe
}

// ✅ BEST: Generic types
function bestFunction<T extends { name: string }>(data: T) {
  return data.name;
}
```

---

## ESLint Security Rules

### Enabled Rules

The following ESLint security rules are automatically enforced:

#### XSS Prevention
- `no-unsanitized/property`: Error - Prevents unsafe dangerouslySetInnerHTML
- `no-unsanitized/method`: Error - Prevents unsafe innerHTML assignments

#### Code Injection
- `no-eval`: Error - Prevents eval()
- `no-implied-eval`: Error - Prevents setTimeout/setInterval with strings
- `no-new-func`: Error - Prevents new Function()

#### Security Patterns (security plugin)
- `security/detect-eval-with-expression`: Error
- `security/detect-unsafe-regex`: Error - Prevents ReDoS
- `security/detect-pseudoRandomBytes`: Error - Requires crypto.randomBytes
- `security/detect-object-injection`: Warn - Prevents prototype pollution
- `security/detect-non-literal-regexp`: Warn
- `security/detect-possible-timing-attacks`: Warn

#### Prototype Pollution
- `no-proto`: Error - Prevents __proto__ usage
- `no-extend-native`: Error - Prevents extending native prototypes

#### TypeScript Safety
- `@typescript-eslint/no-explicit-any`: Error - Prevents any type
- `@typescript-eslint/no-non-null-assertion`: Warn - Prevents ! operator
- `@typescript-eslint/prefer-nullish-coalescing`: Warn
- `@typescript-eslint/prefer-optional-chain`: Warn

### Example Violations

```tsx
// ❌ Violation: no-unsanitized/property
<div dangerouslySetInnerHTML={{ __html: userInput }} />

// ✅ Fix: Use sanitization utility
<div dangerouslySetInnerHTML={createSafeMarkup(userInput)} />

// ❌ Violation: security/detect-unsafe-regex
const regex = new RegExp(userInput);

// ✅ Fix: Validate/sanitize regex pattern
const regex = new RegExp(escapeRegExp(userInput));

// ❌ Violation: @typescript-eslint/no-explicit-any
function process(data: any) { ... }

// ✅ Fix: Proper typing
function process(data: GameData) { ... }
```

---

## Pre-commit Hooks

Pre-commit hooks automatically run before each commit to prevent insecure code from being committed.

### What Gets Checked

1. **ESLint** (max-warnings=0): No security warnings allowed
2. **TypeScript**: No type errors
3. **Prettier**: Code formatting
4. **Staged Files Only**: Fast execution

### Bypassing Hooks (EMERGENCY ONLY)

```bash
# ⚠️ Only use in emergency situations (never for security warnings)
git commit --no-verify -m "Emergency fix"
```

**Rule**: NEVER bypass security warnings. Fix the issue instead.

---

## Common Pitfalls

### Pitfall #1: Rendering User Content Without Sanitization

```tsx
// ❌ WRONG
function Comment({ text }: { text: string }) {
  return <div dangerouslySetInnerHTML={{ __html: text }} />;
}

// ✅ CORRECT
import { sanitizeUserContent } from '@/lib/security';

function Comment({ text }: { text: string }) {
  const safe = sanitizeUserContent(text);
  return <div dangerouslySetInnerHTML={{ __html: safe }} />;
}
```

### Pitfall #2: Client-Side Redirects

```tsx
// ❌ WRONG: Open redirect vulnerability
function RedirectComponent() {
  const url = new URLSearchParams(window.location.search).get('redirect');
  window.location.href = url; // Unsafe: could redirect to evil.com
}

// ✅ CORRECT: Validate redirect URL
import { isSafeUrl } from '@/lib/security';

function RedirectComponent() {
  const url = new URLSearchParams(window.location.search).get('redirect');
  if (url && isSafeUrl(url)) {
    window.location.href = url;
  } else {
    console.error('Invalid redirect URL');
  }
}
```

### Pitfall #3: Dynamic Property Access

```tsx
// ❌ WRONG: Prototype pollution
function updateObject(obj: Record<string, any>, key: string, value: any) {
  obj[key] = value; // Unsafe if key = "__proto__"
}

// ✅ CORRECT: Validate key
function updateObject(obj: Record<string, any>, key: string, value: any) {
  if (key === '__proto__' || key === 'constructor' || key === 'prototype') {
    throw new Error('Invalid key');
  }
  obj[key] = value;
}

// ✅ BEST: Use Map instead
function updateObject(map: Map<string, any>, key: string, value: any) {
  map.set(key, value); // Safe: Map doesn't have prototype pollution issues
}
```

### Pitfall #4: Regular Expression Denial of Service (ReDoS)

```tsx
// ❌ WRONG: Catastrophic backtracking
const regex = /(a+)+$/;
const result = regex.test('aaaaaaaaaaaaaaaaaaaaaaa!'); // Hangs

// ✅ CORRECT: Use simple patterns or timeout
const regex = /a+$/; // No nested quantifiers

// ✅ BEST: Validate input length first
function validateInput(input: string): boolean {
  if (input.length > 1000) return false; // Limit input size
  return /^[a-zA-Z0-9]+$/.test(input);
}
```

---

## Sanitization API Reference

### `sanitizeHtml(html, config?)`

Sanitizes HTML with default configuration (allows tables, lists, formatting).

**Use for**: Board game rules, markdown rendering, trusted content

```tsx
import { sanitizeHtml } from '@/lib/security';

const clean = sanitizeHtml('<script>alert(1)</script><p>Safe</p>');
// Result: '<p>Safe</p>'
```

### `sanitizeUserContent(html)`

Sanitizes HTML with strict configuration (removes links, images).

**Use for**: User comments, forum posts, untrusted user input

```tsx
import { sanitizeUserContent } from '@/lib/security';

const clean = sanitizeUserContent('<a href="evil.com">Link</a><b>Text</b>');
// Result: '<b>Text</b>'
```

### `htmlToPlainText(html)`

Strips all HTML tags, returns plain text.

**Use for**: Meta descriptions, search indexing, notifications

```tsx
import { htmlToPlainText } from '@/lib/security';

const text = htmlToPlainText('<p>Hello <b>world</b>!</p>');
// Result: 'Hello world!'
```

### `createSafeMarkup(html, config?)`

Creates a sanitized object for `dangerouslySetInnerHTML`.

**Use for**: React components that need to render HTML

```tsx
import { createSafeMarkup } from '@/lib/security';

function Component({ content }: { content: string }) {
  return <div dangerouslySetInnerHTML={createSafeMarkup(content)} />;
}
```

### `isSafeUrl(url)`

Checks if a URL is safe (blocks javascript:, data:, vbscript:, file:).

**Use for**: Link validation before rendering

```tsx
import { isSafeUrl } from '@/lib/security';

if (isSafeUrl(userUrl)) {
  return <a href={userUrl}>Link</a>;
}
```

### `sanitizeUrl(url)`

Sanitizes a URL, returns '#' if unsafe.

**Use for**: href attributes with user input

```tsx
import { sanitizeUrl } from '@/lib/security';

<a href={sanitizeUrl(userUrl)}>Link</a>
```

---

## Testing

### Unit Tests

All sanitization functions have comprehensive unit tests:

```bash
cd apps/web
pnpm test src/lib/security/__tests__/sanitize.test.ts
```

Tests cover:
- XSS prevention (script tags, onclick handlers, javascript: URLs)
- Safe HTML tags (tables, lists, formatting)
- URL validation (safe vs dangerous protocols)
- Edge cases (mXSS, DOM clobbering, prototype pollution)

### Integration Testing

Use Playwright for E2E security testing:

```tsx
// e2e/security.spec.ts
test('should prevent XSS in user comments', async ({ page }) => {
  await page.goto('/game/123');
  await page.fill('[data-testid="comment-input"]', '<script>alert(1)</script>');
  await page.click('[data-testid="submit-comment"]');

  // Verify script tag is removed
  const comment = await page.textContent('[data-testid="comment-text"]');
  expect(comment).not.toContain('<script>');
});
```

---

## Appendix

### References

- **OWASP Top 10 (2021)**: https://owasp.org/Top10/
- **DOMPurify**: https://github.com/cure53/DOMPurify
- **ESLint Security Plugin**: https://github.com/eslint-community/eslint-plugin-security
- **ESLint No Unsanitized**: https://github.com/mozilla/eslint-plugin-no-unsanitized

### CWE Mappings

| Vulnerability | CWE | Prevention |
|---------------|-----|------------|
| XSS | CWE-79 | DOMPurify sanitization |
| Code Injection | CWE-94 | No eval, ESLint rules |
| SQL Injection | CWE-89 | Backend: EF Core parameterization |
| Path Traversal | CWE-22 | Backend: PathSecurity utilities |
| Prototype Pollution | CWE-1321 | ESLint no-proto, object validation |
| ReDoS | CWE-400 | ESLint detect-unsafe-regex |

### Security Contacts

- **Security Issues**: Create issue with `security` label
- **Critical Vulnerabilities**: Email security@meepleai.dev (when available)
- **CodeQL Alerts**: Visible at GitHub Security > Code Scanning

---

**Last Updated**: 2025-12-13T10:59:23.970Z
**Version**: 1.0
**Status**: ✅ ACTIVE

