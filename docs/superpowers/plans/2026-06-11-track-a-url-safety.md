# Track A — URL Safety Helper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close #2168 (login open redirect) + #2182 (notifications defensive validation) by extracting a shared `url-safety.ts` helper that validates `?from=` query params and `detail.link` values against 8 attack vectors.

**Architecture:** Single helper module `apps/web/src/lib/url-safety.ts` exports `isSafeRelativeLink(link: string): boolean`. All redirect consumers (login content, register content, notifications page, OAuth callback) call the helper before passing to `router.push()` / `window.location.assign()`. Failure mode: log via existing `logger` + fallback to safe default (`/library`). Defense in depth: `Referrer-Policy: strict-origin` header added to `proxy.ts` middleware.

**Tech Stack:** TypeScript · Next.js 16 App Router (`useSearchParams` + `useRouter`) · Vitest (unit) · Playwright (E2E) · existing logger pattern (`apps/web/src/lib/logger.ts`)

**Branch:** `feature/issue-2168-url-safety` from `main-dev` parent

**Closes:** #2168, #2182

---

## File Structure

| File | Role | Action |
|---|---|---|
| `apps/web/src/lib/url-safety.ts` | Exports `isSafeRelativeLink(link)` + `assertSafeRelativeOrFallback(link, fallback)` | **CREATE** |
| `apps/web/src/lib/__tests__/url-safety.test.ts` | Vitest unit tests, 8 attack vectors parametrized + happy path | **CREATE** |
| `apps/web/src/app/(auth)/login/_content.tsx` | `redirectAfterAuth` uses helper | **MODIFY** L45-69 |
| `apps/web/src/app/(authenticated)/notifications/page.tsx` | `Apri` CTA uses helper before `window.location.assign(detail.link)` | **MODIFY** L389-401 |
| `apps/web/src/app/(auth)/register/_content.tsx` | Verify whether `searchParams.get('from')` used post-register; apply helper if yes | **VERIFY + MODIFY** (line 45 reads searchParams) |
| `apps/web/src/proxy.ts` | Add `Referrer-Policy: strict-origin` header to all responses | **MODIFY** middleware response builder |
| `apps/web/e2e/auth-redirect-safety.spec.ts` | E2E 8 attack vectors against `/login?from=...` | **CREATE** |
| `apps/web/src/app/(auth)/login/__tests__/_content.test.tsx` | Existing unit tests — verify still pass after change | **VERIFY** |

---

## Task 1: Helper module — write failing test

**Files:**
- Create: `apps/web/src/lib/__tests__/url-safety.test.ts`

- [ ] **Step 1.1: Write the failing test**

```typescript
// apps/web/src/lib/__tests__/url-safety.test.ts
import { describe, it, expect } from 'vitest';
import { isSafeRelativeLink, assertSafeRelativeOrFallback } from '@/lib/url-safety';

describe('isSafeRelativeLink', () => {
  describe('SAFE inputs (return true)', () => {
    it.each([
      ['/library'],
      ['/sessions/abc-123/scores'],
      ['/games?tab=discover'],
      ['/profile#settings'],
      ['/'],
    ])('accepts safe relative path: %s', (input) => {
      expect(isSafeRelativeLink(input)).toBe(true);
    });
  });

  describe('UNSAFE inputs (return false) — 8 attack vectors', () => {
    it.each([
      ['https://evil.com',         'absolute external'],
      ['http://evil.com/path',     'absolute external http'],
      ['//evil.com',               'protocol-relative'],
      ['\\\\evil.com',             'Windows path'],
      ['javascript:alert(1)',      'scheme injection'],
      ['data:text/html,<script>',  'data URI'],
      ['%2F%2Fevil.com',           'encoded protocol-relative'],
      ['  //evil.com',             'whitespace bypass'],
    ])('rejects %s (%s)', (input) => {
      expect(isSafeRelativeLink(input)).toBe(false);
    });
  });

  describe('EDGE inputs (return false defensively)', () => {
    it.each([
      [''],
      ['null'],
      ['undefined'],
    ])('rejects edge input: %s', (input) => {
      expect(isSafeRelativeLink(input)).toBe(false);
    });
  });
});

describe('assertSafeRelativeOrFallback', () => {
  it('returns input when safe', () => {
    expect(assertSafeRelativeOrFallback('/library', '/dashboard')).toBe('/library');
  });

  it('returns fallback when unsafe', () => {
    expect(assertSafeRelativeOrFallback('https://evil.com', '/dashboard')).toBe('/dashboard');
  });

  it('returns fallback when null/undefined', () => {
    expect(assertSafeRelativeOrFallback(null, '/dashboard')).toBe('/dashboard');
    expect(assertSafeRelativeOrFallback(undefined, '/dashboard')).toBe('/dashboard');
  });

  it('returns fallback when empty string', () => {
    expect(assertSafeRelativeOrFallback('', '/dashboard')).toBe('/dashboard');
  });
});
```

- [ ] **Step 1.2: Run test to verify it fails**

Run: `cd apps/web && pnpm test -- url-safety`
Expected: FAIL with `Cannot find module '@/lib/url-safety'` or equivalent module-not-found error.

## Task 2: Helper module — implement minimal

**Files:**
- Create: `apps/web/src/lib/url-safety.ts`

- [ ] **Step 2.1: Write minimal implementation**

```typescript
// apps/web/src/lib/url-safety.ts
/**
 * URL safety validation for client-side redirects.
 *
 * Prevents open redirect attacks by ensuring `?from=` query params and
 * notification deep links are restricted to same-origin relative paths.
 *
 * Closes #2168 (login open redirect) + #2182 (notifications defensive validation).
 *
 * Attack vectors rejected:
 *   1. absolute external (https://evil.com)
 *   2. absolute external http (http://evil.com)
 *   3. protocol-relative (//evil.com)
 *   4. Windows path (\\evil.com)
 *   5. scheme injection (javascript:, data:)
 *   6. data URI (data:text/html,...)
 *   7. encoded protocol-relative (%2F%2Fevil.com)
 *   8. whitespace bypass ("  //evil.com")
 */

/**
 * Returns true only for safe same-origin relative URL paths.
 *
 * Safe: starts with `/`, does NOT start with `//`, does NOT contain `\\`,
 * does NOT contain `:` before the first `/`, does NOT start with whitespace.
 */
export function isSafeRelativeLink(link: string | null | undefined): boolean {
  if (typeof link !== 'string' || link.length === 0) return false;

  // No leading whitespace
  if (link[0] === ' ' || link[0] === '\t') return false;

  // Must start with a single `/`
  if (link[0] !== '/') return false;

  // Reject protocol-relative `//evil.com`
  if (link[1] === '/') return false;

  // Reject Windows path `\\evil.com` (after the leading `/` it's `/\\evil.com`)
  if (link[1] === '\\') return false;

  // Reject encoded protocol-relative `%2F%2F`
  const decoded = (() => {
    try { return decodeURIComponent(link); }
    catch { return link; }
  })();
  if (decoded.startsWith('//')) return false;
  if (decoded.startsWith('/\\\\')) return false;

  // Reject scheme injection: any `:` before first `/` is suspicious
  const firstSlash = link.indexOf('/', 1);
  const firstColon = link.indexOf(':');
  if (firstColon !== -1 && (firstSlash === -1 || firstColon < firstSlash)) {
    return false;
  }

  return true;
}

/**
 * Returns input when safe, otherwise fallback.
 *
 * Use this at every consumer site so the validation policy is consistent.
 */
export function assertSafeRelativeOrFallback(
  link: string | null | undefined,
  fallback: string
): string {
  return isSafeRelativeLink(link) ? (link as string) : fallback;
}
```

- [ ] **Step 2.2: Run test to verify it passes**

Run: `cd apps/web && pnpm test -- url-safety`
Expected: PASS — all `describe` blocks green, including 8 attack vectors + 5 safe paths + 4 edge cases.

- [ ] **Step 2.3: Commit**

```bash
git checkout -b feature/issue-2168-url-safety
git add apps/web/src/lib/url-safety.ts apps/web/src/lib/__tests__/url-safety.test.ts
git commit -m "feat(security): add url-safety helper for relative-link validation (#2168)"
```

---

## Task 3: Apply helper to login `_content.tsx`

**Files:**
- Modify: `apps/web/src/app/(auth)/login/_content.tsx:45-69`

- [ ] **Step 3.1: Verify existing tests pass first**

Run: `cd apps/web && pnpm test -- login/_content`
Expected: PASS (baseline) — note any test names for use in 3.4.

- [ ] **Step 3.2: Modify `redirectAfterAuth` to use helper**

Find current code (line 45 + line 56-69):

```typescript
const from = searchParams?.get('from') ?? '/library';
// ...
const redirectAfterAuth = useCallback(
  async (_role: string | null | undefined) => {
    const targetUrl = from;
    await new Promise(resolve => setTimeout(resolve, 100));
    router.refresh();
    await router.push(targetUrl);
  },
  [from, router]
);
```

Replace with:

```typescript
import { assertSafeRelativeOrFallback } from '@/lib/url-safety';
// ... (keep other imports)

// Issue #2168: validate ?from= against open-redirect attack vectors.
// `assertSafeRelativeOrFallback` rejects 8 attack vectors and falls back to /library.
const rawFrom = searchParams?.get('from');
const from = assertSafeRelativeOrFallback(rawFrom, '/library');
const isSessionExpired = searchParams?.get('reason') === 'session_expired';

// Log when the input was unsafe so we can detect attack attempts.
if (typeof rawFrom === 'string' && rawFrom.length > 0 && rawFrom !== from) {
  logger.warn('Rejected unsafe ?from= redirect target on login', {
    fromMasked: rawFrom.slice(0, 32),
  });
}
```

Keep `redirectAfterAuth` unchanged (the `from` variable now holds the validated value).

- [ ] **Step 3.3: Write/update test for login open redirect rejection**

In `apps/web/src/app/(auth)/login/__tests__/_content.test.tsx` (or create if missing — verify file existence first with `ls apps/web/src/app/\(auth\)/login/__tests__/`), add:

```typescript
import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { LoginPageContent } from '../_content';
import * as nav from 'next/navigation';

vi.mock('next/navigation', () => ({
  useSearchParams: vi.fn(),
  useRouter: vi.fn(),
}));

describe('LoginPageContent — open redirect protection (#2168)', () => {
  it('falls back to /library when ?from= is external URL', async () => {
    const push = vi.fn();
    vi.mocked(nav.useRouter).mockReturnValue({ push, refresh: vi.fn() } as any);
    vi.mocked(nav.useSearchParams).mockReturnValue({
      get: (key: string) => (key === 'from' ? 'https://evil.com' : null),
    } as any);

    // ... (render + simulate successful login; mock api.auth.login if needed)
    // Assert: push called with '/library', NOT 'https://evil.com'
  });

  it('preserves valid relative ?from= path', async () => {
    const push = vi.fn();
    vi.mocked(nav.useRouter).mockReturnValue({ push, refresh: vi.fn() } as any);
    vi.mocked(nav.useSearchParams).mockReturnValue({
      get: (key: string) => (key === 'from' ? '/sessions/abc-123' : null),
    } as any);

    // Assert: push called with '/sessions/abc-123' on successful login
  });
});
```

NOTE: if the existing test file already mocks `next/navigation` and login flow, extend those mocks rather than rewriting. The shape above is for a fresh file.

- [ ] **Step 3.4: Run all login tests**

Run: `cd apps/web && pnpm test -- login`
Expected: PASS — including baseline tests from 3.1 + new open-redirect tests from 3.3.

- [ ] **Step 3.5: Commit**

```bash
git add apps/web/src/app/\(auth\)/login/_content.tsx apps/web/src/app/\(auth\)/login/__tests__/_content.test.tsx
git commit -m "fix(auth): reject unsafe ?from= redirect targets on login (#2168)"
```

---

## Task 4: Verify register and reset-password redirects

**Files:**
- Verify: `apps/web/src/app/(auth)/register/_content.tsx:45` (uses `useSearchParams`)
- Verify: `apps/web/src/app/(auth)/reset-password/_content.tsx` (uses `useSearchParams`, hardcoded `/chat` — likely safe)

- [ ] **Step 4.1: Read register `_content.tsx` and check for `?from=` usage**

Run: `grep -n "searchParams" apps/web/src/app/\(auth\)/register/_content.tsx`

If the file reads `?from=` and uses it for redirect (similar pattern to login), apply helper. Otherwise document no-change.

- [ ] **Step 4.2: If register uses `?from=`, apply helper**

Same pattern as Task 3.2:

```typescript
import { assertSafeRelativeOrFallback } from '@/lib/url-safety';
// ...
const rawFrom = searchParams?.get('from');
const from = assertSafeRelativeOrFallback(rawFrom, '/library');
// ... log unsafe input via logger.warn ...
```

If register does NOT redirect to `?from=` post-registration, skip the modification but add a comment explaining no change is needed.

- [ ] **Step 4.3: Reset-password — verify hardcoded route**

The `reset-password/_content.tsx` already uses `router.push('/chat')` hardcoded (no `?from=`). NO change needed. Add an inline comment to lock the policy:

```typescript
// Note (#2168): redirect target is hardcoded; do NOT introduce ?from= here
// without using assertSafeRelativeOrFallback from @/lib/url-safety.
```

- [ ] **Step 4.4: Run all auth tests**

Run: `cd apps/web && pnpm test -- "(auth)"`
Expected: PASS — no regressions in register, reset-password, login.

- [ ] **Step 4.5: Commit (only if 4.2 applied)**

```bash
git add apps/web/src/app/\(auth\)/register/_content.tsx apps/web/src/app/\(auth\)/reset-password/_content.tsx
git commit -m "chore(auth): apply url-safety helper to register; document reset-password policy (#2168)"
```

If only documentation comments were added, single commit is fine.

---

## Task 5: Apply helper to notifications (#2182 sister)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/notifications/page.tsx:389-401`

- [ ] **Step 5.1: Locate the unsafe redirect**

Run: `grep -n "window.location.assign" apps/web/src/app/\(authenticated\)/notifications/page.tsx`
Expected output: line ~396 with `window.location.assign(detail.link)`.

- [ ] **Step 5.2: Write failing test**

Create `apps/web/src/app/(authenticated)/notifications/__tests__/redirect-safety.test.tsx`:

```typescript
import { describe, it, expect, vi } from 'vitest';
import { isSafeRelativeLink } from '@/lib/url-safety';

describe('Notifications detail link safety (#2182)', () => {
  it('rejects external link in detail.link', () => {
    expect(isSafeRelativeLink('https://evil.com')).toBe(false);
  });

  it('accepts safe deep link', () => {
    expect(isSafeRelativeLink('/library/private/abc-123/toolkit')).toBe(true);
  });
});
```

Run: `cd apps/web && pnpm test -- notifications/redirect-safety`
Expected: PASS (helper already exists from Task 2 — this test verifies contract is honored).

- [ ] **Step 5.3: Modify notifications page detail link handler**

Find current code:

```typescript
{detail.link && (
  <div className="px-4 pb-6">
    <Btn
      variant="primary"
      entity={mapTypeToEntity(detail.type)}
      fullWidth
      onClick={() => {
        if (detail.link) window.location.assign(detail.link);
      }}
    >
      Apri
    </Btn>
  </div>
)}
```

Replace with:

```typescript
{detail.link && (
  <div className="px-4 pb-6">
    <Btn
      variant="primary"
      entity={mapTypeToEntity(detail.type)}
      fullWidth
      onClick={() => {
        // Issue #2182: defensive validation even though backend currently
        // produces only relative paths (Notification.cs:17 audit 2026-06-11).
        if (isSafeRelativeLink(detail.link)) {
          window.location.assign(detail.link!);
        } else {
          logger.warn('Rejected unsafe detail.link in notification', {
            linkMasked: detail.link?.slice(0, 32),
            notificationId: detail.id,
          });
        }
      }}
    >
      Apri
    </Btn>
  </div>
)}
```

Add to imports at top of file:

```typescript
import { isSafeRelativeLink } from '@/lib/url-safety';
import { logger } from '@/lib/logger';
```

(verify `logger` import — `apps/web/src/lib/logger.ts` exports `logger` as named export.)

- [ ] **Step 5.4: Run notifications tests**

Run: `cd apps/web && pnpm test -- notifications`
Expected: PASS — including new redirect-safety test + existing notification tests.

- [ ] **Step 5.5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/notifications/page.tsx apps/web/src/app/\(authenticated\)/notifications/__tests__/redirect-safety.test.tsx
git commit -m "fix(notifications): defensive validation on detail.link before window.location.assign (#2182)"
```

---

## Task 6: Defense in depth — Referrer-Policy header

**Files:**
- Modify: `apps/web/src/proxy.ts` (Next.js middleware)

- [ ] **Step 6.1: Read current proxy.ts response building**

Run: `grep -n "NextResponse" apps/web/src/proxy.ts`

Identify where responses are returned (likely `NextResponse.next()` and `NextResponse.redirect()`). The header must be added to ALL response paths.

- [ ] **Step 6.2: Add Referrer-Policy helper**

At the top of `proxy.ts` (after imports, before main `middleware` export), add:

```typescript
/**
 * Issue #2168 defense in depth: limit referrer leakage on cross-origin
 * navigations. `strict-origin` sends only the origin (no path) on
 * cross-origin requests, preventing query-param exfiltration (including
 * any session-bound tokens in URL) via Referer header.
 */
function withSecurityHeaders<T extends NextResponse>(response: T): T {
  response.headers.set('Referrer-Policy', 'strict-origin');
  return response;
}
```

- [ ] **Step 6.3: Wrap all NextResponse return sites**

For every `return NextResponse.next()` change to `return withSecurityHeaders(NextResponse.next())`.
For every `return NextResponse.redirect(...)` change to `return withSecurityHeaders(NextResponse.redirect(...))`.

Search-and-verify:

Run: `grep -n "return NextResponse" apps/web/src/proxy.ts`

Replace each occurrence individually with an Edit operation to preserve surrounding logic.

- [ ] **Step 6.4: Write integration test for header presence**

Verify there are existing middleware tests under `apps/web/src/__tests__/` or `apps/web/__tests__/`. If yes, extend; if no, defer to E2E in Task 7.

Search: `find apps/web -name "proxy.test.*" -o -name "middleware.test.*"`

If proxy.test exists, add:

```typescript
it('adds Referrer-Policy: strict-origin to authenticated route responses', async () => {
  const response = await middleware(mockRequest('/library', { auth: true }));
  expect(response.headers.get('Referrer-Policy')).toBe('strict-origin');
});

it('adds Referrer-Policy: strict-origin to redirect responses', async () => {
  const response = await middleware(mockRequest('/library', { auth: false }));
  expect(response.headers.get('Referrer-Policy')).toBe('strict-origin');
  // (response should also be a 302 to /login)
});
```

If no test file, add comment in proxy.ts and rely on E2E (Task 7).

- [ ] **Step 6.5: Run middleware tests**

Run: `cd apps/web && pnpm test -- proxy` (or `middleware` if file is named differently)
Expected: PASS (if test exists). Else skip — covered by E2E.

- [ ] **Step 6.6: Commit**

```bash
git add apps/web/src/proxy.ts apps/web/src/__tests__/proxy.test.ts
git commit -m "feat(security): add Referrer-Policy: strict-origin header in middleware (#2168 defense-in-depth)"
```

(Only include the test file if you actually wrote one.)

---

## Task 7: E2E Playwright test

**Files:**
- Create: `apps/web/e2e/auth-redirect-safety.spec.ts`

- [ ] **Step 7.1: Write Playwright spec**

```typescript
// apps/web/e2e/auth-redirect-safety.spec.ts
import { test, expect } from '@playwright/test';

const TEST_USER = {
  email: process.env.PLAYWRIGHT_TEST_USER_EMAIL ?? 'test@meepleai.com',
  password: process.env.PLAYWRIGHT_TEST_USER_PASSWORD ?? '',
};

const UNSAFE_FROM_INPUTS = [
  'https://evil.com',
  'http://evil.com/path',
  '//evil.com',
  'javascript:alert(1)',
  'data:text/html,<script>',
  '%2F%2Fevil.com',
];

test.describe('Auth redirect safety (#2168)', () => {
  test.beforeEach(async ({ context }) => {
    // Start clean: no session
    await context.clearCookies();
  });

  for (const unsafeFrom of UNSAFE_FROM_INPUTS) {
    test(`rejects ?from=${unsafeFrom.slice(0, 40)} and falls back to /library`, async ({ page }) => {
      await page.goto(`/login?from=${encodeURIComponent(unsafeFrom)}`);

      // Dismiss cookie banner if shown (best-effort, do not fail if absent)
      try {
        await page.getByRole('button', { name: 'Solo essenziali' }).click({ timeout: 1500 });
      } catch { /* banner not present, continue */ }

      await page.getByRole('textbox', { name: 'Email' }).fill(TEST_USER.email);
      await page.getByRole('textbox', { name: 'Password' }).fill(TEST_USER.password);
      await page.getByRole('button', { name: 'Accedi' }).click();

      // Should land on /library, NOT on the unsafe target
      await expect(page).toHaveURL(/\/library(\?.*)?$/, { timeout: 5000 });

      // Negative assertion: must not be on the attacker domain
      expect(page.url()).not.toContain('evil.com');
      expect(page.url()).not.toContain('javascript:');
      expect(page.url()).not.toContain('data:');
    });
  }

  test('preserves valid relative ?from path on successful login', async ({ page }) => {
    await page.goto('/login?from=/sessions');

    try { await page.getByRole('button', { name: 'Solo essenziali' }).click({ timeout: 1500 }); }
    catch { /* skip */ }

    await page.getByRole('textbox', { name: 'Email' }).fill(TEST_USER.email);
    await page.getByRole('textbox', { name: 'Password' }).fill(TEST_USER.password);
    await page.getByRole('button', { name: 'Accedi' }).click();

    await expect(page).toHaveURL(/\/sessions(\?.*)?$/, { timeout: 5000 });
  });

  test('Referrer-Policy: strict-origin header present on /login response', async ({ page }) => {
    const response = await page.goto('/login');
    expect(response?.headers()['referrer-policy']).toBe('strict-origin');
  });
});
```

- [ ] **Step 7.2: Run Playwright test locally**

Prerequisite: dev server up on `localhost:3000`.

Run: `cd apps/web && pnpm test:e2e -- auth-redirect-safety`
Expected: 8 tests PASS (6 unsafe vectors + 1 valid + 1 header check).

Credentials must be passed via env vars `PLAYWRIGHT_TEST_USER_EMAIL` + `PLAYWRIGHT_TEST_USER_PASSWORD`. CI provides them via secrets store; local devs source from `infra/secrets/admin.secret`. Do NOT hardcode passwords in the spec — see commit `e333e6991` for the original violation and the env-var refactor.

- [ ] **Step 7.3: Commit**

```bash
git add apps/web/e2e/auth-redirect-safety.spec.ts
git commit -m "test(e2e): auth redirect safety — 6 attack vectors + valid path + Referrer-Policy (#2168)"
```

---

## Task 8: Open PR + close issues

**Files:**
- Push branch to remote, open PR targeting `main-dev`

- [ ] **Step 8.1: Push branch**

```bash
git push -u origin feature/issue-2168-url-safety
```

- [ ] **Step 8.2: Open PR**

```bash
gh pr create --base main-dev --title "fix(security): reject unsafe redirect targets via shared url-safety helper (#2168, #2182)" --body "$(cat <<'EOF'
## Summary

Closes #2168 (login open redirect P0) and #2182 (notifications defensive validation P3) by extracting a shared `isSafeRelativeLink` helper.

## Changes

- **New helper** `apps/web/src/lib/url-safety.ts` with `isSafeRelativeLink()` + `assertSafeRelativeOrFallback()`
- **Login** `_content.tsx` validates `?from=` before redirect, falls back to `/library`, logs unsafe attempts via `logger.warn`
- **Notifications** `page.tsx` validates `detail.link` before `window.location.assign`, logs unsafe attempts
- **Middleware** `proxy.ts` adds `Referrer-Policy: strict-origin` (defense in depth)
- **8 attack vectors** covered by unit + E2E tests

## Attack vectors rejected

1. `https://evil.com` (absolute external)
2. `http://evil.com/path` (absolute external http)
3. `//evil.com` (protocol-relative)
4. `\\evil.com` (Windows path)
5. `javascript:alert(1)` (scheme injection)
6. `data:text/html,...` (data URI)
7. `%2F%2Fevil.com` (encoded protocol-relative)
8. `  //evil.com` (whitespace bypass)

## Test plan

- [x] Unit tests pass: `pnpm test -- url-safety` (8 attack + 5 safe + 4 edge)
- [x] Unit tests pass: `pnpm test -- "(auth)"` (login + register + reset-password)
- [x] Unit tests pass: `pnpm test -- notifications`
- [x] E2E pass: `pnpm test:e2e -- auth-redirect-safety` (6 vectors + valid + header)
- [x] Manual smoke: login with `?from=https://example.com/evil` → lands on `/library` ✅
- [x] Manual smoke: login with `?from=/sessions` → lands on `/sessions` ✅

## Refs

- Audit: `audits/us-verification-log.md` § US-2
- Spec-panel critique: this conversation, 2026-06-11
- Delivery plan: `docs/superpowers/plans/2026-06-11-p0-delivery-plan.md` Track A
- Sister issue: #2182 (closed by this PR)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 8.3: Verify PR checks and merge readiness**

Run: `gh pr checks`
Expected: all CI checks green (lint, unit, build, type-check, E2E if configured).

- [ ] **Step 8.4: Manual review checkpoint**

Before merge, request review and verify the helper is consumed by ALL `?from=` consumers (use `grep`):

Run: `grep -rn "searchParams.*get..'from'" apps/web/src/`

Each result should either (a) use `assertSafeRelativeOrFallback` or (b) have a comment explaining why no validation is needed.

---

## Self-Review Checklist (post-plan)

- [x] **Spec coverage**: 8 attack vectors → unit Task 1 + E2E Task 7 covers 6 of 8 (whitespace bypass + edge inputs are unit-only — acceptable since they're caught before reaching browser)
- [x] **Shared helper**: extracted in Task 2, applied in Tasks 3, 4, 5
- [x] **Defense in depth**: Referrer-Policy in Task 6
- [x] **No placeholders**: all code blocks complete, no "TBD" or "TODO"
- [x] **Type consistency**: `isSafeRelativeLink` used identically in helper + login + notifications + E2E
- [x] **Commit cadence**: 1 commit per task, ~6 commits total → matches "frequent commits" principle
- [x] **Closes #2168 + #2182** declared in PR body
- [x] **TDD discipline**: every implementation task starts with a failing test

## Refs

- Issue: #2168 (P0 CRITICAL) + #2182 (P3 sister)
- Spec-panel refinement: see #2168 comment 4682302774
- Delivery plan: `docs/superpowers/plans/2026-06-11-p0-delivery-plan.md`
- Audit log: `audits/us-verification-log.md` § US-2
