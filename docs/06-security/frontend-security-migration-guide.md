# Frontend Security Migration Guide

**Date**: 2025-11-23
**Purpose**: Guide for migrating existing code to use security utilities
**Status**: ✅ ACTIVE

---

## Overview

This guide helps migrate existing frontend code to use the new security utilities from `@/lib/security`. Following this guide will eliminate CodeQL security warnings and prevent XSS vulnerabilities.

---

## Migration Checklist

- [ ] Replace all `dangerouslySetInnerHTML` with `createSafeMarkup`
- [ ] Replace all `.innerHTML` assignments with sanitization
- [ ] Validate all `href` attributes with user input
- [ ] Remove all `eval()` usage
- [ ] Fix TypeScript `any` types in security-sensitive code
- [ ] Run ESLint and fix all security warnings
- [ ] Add unit tests for sanitized components

---

## Pattern Migrations

### 1. dangerouslySetInnerHTML

#### Before (Unsafe)

```tsx
function GameRules({ content }: { content: string }) {
  return <div dangerouslySetInnerHTML={{ __html: content }} />;
}
```

#### After (Safe)

```tsx
import { createSafeMarkup } from '@/lib/security';

function GameRules({ content }: { content: string }) {
  return <div dangerouslySetInnerHTML={createSafeMarkup(content)} />;
}
```

**Why**: Prevents XSS by sanitizing HTML before rendering.

---

### 2. Direct innerHTML Assignment

#### Before (Unsafe)

```tsx
function MarkdownPreview({ html }: { html: string }) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (ref.current) {
      ref.current.innerHTML = html; // ❌ XSS vulnerability
    }
  }, [html]);

  return <div ref={ref} />;
}
```

#### After (Safe)

```tsx
import { createSafeMarkup } from '@/lib/security';

function MarkdownPreview({ html }: { html: string }) {
  return <div dangerouslySetInnerHTML={createSafeMarkup(html)} />;
}
```

**Alternative (Best)**:

```tsx
import ReactMarkdown from 'react-markdown';

function MarkdownPreview({ markdown }: { markdown: string }) {
  return <ReactMarkdown>{markdown}</ReactMarkdown>;
}
```

**Why**: Use React libraries when possible; sanitize when using HTML directly.

---

### 3. User-Generated Content

#### Before (Unsafe)

```tsx
function UserComment({ comment }: { comment: string }) {
  return <div>{comment}</div>; // Renders as text, but if using innerHTML...
}

// Or worse:
function UserComment({ comment }: { comment: string }) {
  return <div dangerouslySetInnerHTML={{ __html: comment }} />;
}
```

#### After (Safe)

```tsx
import { sanitizeUserContent } from '@/lib/security';

function UserComment({ comment }: { comment: string }) {
  const safeComment = sanitizeUserContent(comment);
  return <div dangerouslySetInnerHTML={{ __html: safeComment }} />;
}
```

**Why**: `sanitizeUserContent` uses strict rules (no links, images) for untrusted input.

---

### 4. URL Validation

#### Before (Unsafe)

```tsx
function ExternalLink({ url, text }: { url: string; text: string }) {
  return <a href={url} target="_blank">{text}</a>;
}

// Usage:
// <ExternalLink url="javascript:alert('XSS')" text="Click" />
```

#### After (Safe)

```tsx
import { sanitizeUrl } from '@/lib/security';

function ExternalLink({ url, text }: { url: string; text: string }) {
  return (
    <a href={sanitizeUrl(url)} target="_blank" rel="noopener noreferrer">
      {text}
    </a>
  );
}
```

**Alternative (Better Validation)**:

```tsx
import { isSafeUrl } from '@/lib/security';

function ExternalLink({ url, text }: { url: string; text: string }) {
  if (!isSafeUrl(url)) {
    return <span title="Invalid URL" className="text-gray-500">{text}</span>;
  }

  return (
    <a href={url} target="_blank" rel="noopener noreferrer" className="text-blue-600">
      {text}
    </a>
  );
}
```

**Why**: Prevents javascript:, data:, and other malicious URL schemes.

---

### 5. Dynamic Redirects

#### Before (Unsafe)

```tsx
function RedirectPage() {
  const searchParams = useSearchParams();
  const redirectUrl = searchParams.get('redirect');

  useEffect(() => {
    if (redirectUrl) {
      window.location.href = redirectUrl; // ❌ Open redirect vulnerability
    }
  }, [redirectUrl]);

  return <div>Redirecting...</div>;
}
```

#### After (Safe)

```tsx
import { isSafeUrl } from '@/lib/security';

function RedirectPage() {
  const searchParams = useSearchParams();
  const redirectUrl = searchParams.get('redirect');
  const router = useRouter();

  useEffect(() => {
    if (redirectUrl && isSafeUrl(redirectUrl)) {
      // Only allow relative URLs or same-origin
      if (redirectUrl.startsWith('/')) {
        router.push(redirectUrl);
      } else {
        console.warn('External redirects not allowed');
      }
    }
  }, [redirectUrl, router]);

  return <div>Redirecting...</div>;
}
```

**Why**: Prevents attackers from redirecting users to phishing sites.

---

### 6. eval() Usage

#### Before (Unsafe)

```tsx
function DynamicCalculator({ expression }: { expression: string }) {
  try {
    const result = eval(expression); // ❌ Code injection vulnerability
    return <div>Result: {result}</div>;
  } catch {
    return <div>Invalid expression</div>;
  }
}
```

#### After (Safe)

```tsx
import { evaluate } from 'mathjs'; // Use a safe math library

function DynamicCalculator({ expression }: { expression: string }) {
  try {
    // mathjs safely evaluates math expressions without code execution
    const result = evaluate(expression);
    return <div>Result: {result}</div>;
  } catch {
    return <div>Invalid expression</div>;
  }
}
```

**Alternative (Manual Parsing)**:

```tsx
function SimpleCalculator({ num1, num2, operator }: Props) {
  const calculate = () => {
    switch (operator) {
      case '+': return num1 + num2;
      case '-': return num1 - num2;
      case '*': return num1 * num2;
      case '/': return num2 !== 0 ? num1 / num2 : 'Error';
      default: return 'Invalid operator';
    }
  };

  return <div>Result: {calculate()}</div>;
}
```

**Why**: `eval()` executes arbitrary code and is a major security risk.

---

### 7. TypeScript `any` Types

#### Before (Type-Unsafe)

```tsx
function ProcessGameData(data: any) {
  return data.players.map((player: any) => player.name);
}
```

#### After (Type-Safe)

```tsx
interface Player {
  name: string;
  score: number;
}

interface GameData {
  players: Player[];
  round: number;
}

function ProcessGameData(data: GameData) {
  return data.players.map(player => player.name);
}
```

**Why**: Type safety prevents runtime errors and makes code more maintainable.

---

## Automated Migration

### Step 1: Find Unsafe Patterns

```bash
cd apps/web

# Find dangerouslySetInnerHTML without sanitization
grep -r "dangerouslySetInnerHTML" src/ --include="*.tsx"

# Find innerHTML assignments
grep -r "\.innerHTML\s*=" src/ --include="*.ts" --include="*.tsx"

# Find eval usage
grep -r "eval(" src/ --include="*.ts" --include="*.tsx"

# Find any types
grep -r ": any" src/ --include="*.ts" --include="*.tsx"
```

### Step 2: Run ESLint

```bash
# See all security warnings
pnpm lint

# Fix auto-fixable issues
pnpm lint:fix
```

### Step 3: Fix Remaining Issues

Use this guide to manually fix issues that ESLint can't auto-fix.

### Step 4: Add Tests

```tsx
import { render } from '@testing-library/react';
import { sanitizeHtml } from '@/lib/security';

describe('GameRules Component', () => {
  it('should sanitize malicious HTML', () => {
    const malicious = '<script>alert("XSS")</script><p>Safe content</p>';
    const { container } = render(<GameRules content={malicious} />);

    expect(container.innerHTML).not.toContain('<script>');
    expect(container.innerHTML).toContain('<p>Safe content</p>');
  });
});
```

---

## Component Examples

### Example 1: Rich Text Editor

```tsx
import { useState } from 'react';
import { createSafeMarkup } from '@/lib/security';

function RichTextEditor() {
  const [content, setContent] = useState('');
  const [preview, setPreview] = useState('');

  const handleChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setContent(e.target.value);
  };

  const handlePreview = () => {
    setPreview(content);
  };

  return (
    <div>
      <textarea value={content} onChange={handleChange} />
      <button onClick={handlePreview}>Preview</button>

      {preview && (
        <div
          className="preview"
          dangerouslySetInnerHTML={createSafeMarkup(preview)}
        />
      )}
    </div>
  );
}
```

### Example 2: User Profile with Bio

```tsx
import { sanitizeUserContent } from '@/lib/security';

interface UserProfileProps {
  user: {
    name: string;
    bio: string; // May contain HTML from rich text editor
  };
}

function UserProfile({ user }: UserProfileProps) {
  const safeBio = sanitizeUserContent(user.bio);

  return (
    <div>
      <h1>{user.name}</h1>
      <div
        className="user-bio"
        dangerouslySetInnerHTML={{ __html: safeBio }}
      />
    </div>
  );
}
```

### Example 3: External Resource Viewer

```tsx
import { useState, useEffect } from 'react';
import { sanitizeHtml } from '@/lib/security';

function ExternalContent({ url }: { url: string }) {
  const [content, setContent] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetch(url)
      .then(res => res.text())
      .then(html => {
        const safe = sanitizeHtml(html);
        setContent(safe);
        setLoading(false);
      })
      .catch(err => {
        console.error('Failed to load content:', err);
        setLoading(false);
      });
  }, [url]);

  if (loading) return <div>Loading...</div>;

  return <div dangerouslySetInnerHTML={{ __html: content }} />;
}
```

---

## Testing

### Unit Tests

```tsx
import { render, screen } from '@testing-library/react';
import { GameRules } from './GameRules';

describe('GameRules Security', () => {
  it('should prevent XSS via script tags', () => {
    const malicious = '<script>alert("XSS")</script><p>Rules</p>';
    const { container } = render(<GameRules content={malicious} />);

    expect(container.querySelector('script')).toBeNull();
    expect(screen.getByText('Rules')).toBeInTheDocument();
  });

  it('should prevent XSS via event handlers', () => {
    const malicious = '<div onclick="alert(1)">Click me</div>';
    const { container } = render(<GameRules content={malicious} />);

    expect(container.innerHTML).not.toContain('onclick');
  });

  it('should prevent XSS via javascript: URLs', () => {
    const malicious = '<a href="javascript:alert(1)">Link</a>';
    const { container } = render(<GameRules content={malicious} />);

    expect(container.innerHTML).not.toContain('javascript:');
  });
});
```

### E2E Tests

```tsx
// e2e/security.spec.ts
import { test, expect } from '@playwright/test';

test('should prevent XSS in comment form', async ({ page }) => {
  await page.goto('/games/catan');

  // Try to submit XSS payload
  await page.fill('[data-testid="comment-input"]', '<script>alert(1)</script>');
  await page.click('[data-testid="submit-comment"]');

  // Wait for comment to appear
  await page.waitForSelector('[data-testid="comment-list"]');

  // Verify script tag is not in DOM
  const pageContent = await page.content();
  expect(pageContent).not.toContain('<script>alert(1)</script>');

  // Verify comment text is still visible (tags stripped)
  const comment = await page.textContent('[data-testid="comment-list"]');
  expect(comment).toBeTruthy();
});
```

---

## Rollout Plan

### Phase 1: High-Risk Components (Week 1)

- [ ] User comment sections
- [ ] Rich text editors
- [ ] External content viewers
- [ ] URL handling components

### Phase 2: Medium-Risk Components (Week 2)

- [ ] Game rules display
- [ ] Markdown renderers
- [ ] Search results
- [ ] Notifications

### Phase 3: Low-Risk Components (Week 3)

- [ ] Static content
- [ ] Admin panels
- [ ] Internal tools

### Phase 4: Verification (Week 4)

- [ ] Run full ESLint scan (0 warnings)
- [ ] Run E2E security tests
- [ ] Review CodeQL results
- [ ] Update documentation

---

## Support

### Questions?

1. Check `docs/06-security/frontend-security-best-practices.md`
2. Review unit tests in `apps/web/src/lib/security/__tests__/`
3. Ask in #engineering Slack channel
4. Create an issue with `security` label

### Report Security Issues

If you find a security vulnerability:
1. DO NOT create a public issue
2. Email: security@meepleai.dev (when available)
3. Or create a private security advisory on GitHub

---

**Version**: 1.0
**Last Updated**: 2025-12-13T10:59:23.970Z
**Status**: ✅ ACTIVE

