# Security Utilities

**Location**: `apps/web/src/lib/security/`
**Status**: ✅ ACTIVE

---

## Quick Start

```tsx
import { sanitizeHtml, createSafeMarkup, sanitizeUrl } from '@/lib/security';

// Sanitize HTML for rendering
const clean = sanitizeHtml(userInput);

// For dangerouslySetInnerHTML
<div dangerouslySetInnerHTML={createSafeMarkup(userContent)} />

// Sanitize URLs
<a href={sanitizeUrl(userUrl)}>Link</a>
```

---

## Files

- **sanitize.ts**: XSS prevention utilities (DOMPurify)
- **index.ts**: Barrel exports
- **__tests__/sanitize.test.ts**: Unit tests (100% coverage)

---

## When to Use

### ✅ Always Sanitize When:

1. Rendering user-generated content
2. Using `dangerouslySetInnerHTML`
3. Displaying HTML from external sources (PDFs, APIs)
4. Creating links from user input

### ❌ Never:

1. Trust user input without sanitization
2. Use `eval()` or `new Function()`
3. Bypass ESLint security warnings
4. Use `innerHTML` directly

---

## Functions

### `sanitizeHtml(html, config?)`

General-purpose sanitization. Allows safe HTML tags (tables, lists, formatting).

**Use for**: Board game rules, markdown content

```tsx
const safe = sanitizeHtml('<script>alert(1)</script><p>Text</p>');
// Result: '<p>Text</p>'
```

### `sanitizeUserContent(html)`

Strict sanitization. Removes links and images.

**Use for**: User comments, forum posts

```tsx
const safe = sanitizeUserContent('<a href="evil.com">Click</a><b>Bold</b>');
// Result: '<b>Bold</b>'
```

### `htmlToPlainText(html)`

Strips all HTML tags.

**Use for**: Meta descriptions, search indexing

```tsx
const text = htmlToPlainText('<p>Hello <b>world</b>!</p>');
// Result: 'Hello world!'
```

### `createSafeMarkup(html, config?)`

Creates sanitized markup for React's `dangerouslySetInnerHTML`.

**Use for**: React components

```tsx
<div dangerouslySetInnerHTML={createSafeMarkup(content)} />
```

### `isSafeUrl(url)`

Validates URL safety. Blocks javascript:, data:, vbscript:, file:

**Use for**: URL validation

```tsx
if (isSafeUrl(url)) {
  return <a href={url}>Link</a>;
}
```

### `sanitizeUrl(url)`

Returns sanitized URL or '#' if unsafe.

**Use for**: href attributes

```tsx
<a href={sanitizeUrl(userUrl)}>Link</a>
```

---

## Testing

```bash
# Run unit tests
pnpm test src/lib/security

# With coverage
pnpm test:coverage src/lib/security
```

Tests cover:
- XSS attacks (script tags, event handlers, javascript: URLs)
- Safe HTML rendering
- URL validation
- Edge cases (mXSS, DOM clobbering, prototype pollution)

---

## Documentation

See: `docs/06-security/frontend-security-best-practices.md`

---

**Version**: 1.0
**Last Updated**: 2025-11-23
