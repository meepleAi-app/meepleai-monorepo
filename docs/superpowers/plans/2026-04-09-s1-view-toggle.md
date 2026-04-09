# S1 — Admin↔User View Toggle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a one-click cookie-based view mode toggle to the top navbar that lets admin users flip between the admin shell and the user shell while preserving their authenticated session.

**Architecture:** A new `meepleai_view_mode` cookie (client-writable, SameSite=Lax) stores the current mode (`'admin' | 'user'`). A new `ViewModeToggle` client component renders an icon switch next to the avatar in `UserTopNav`. The guard in `app/admin/(dashboard)/layout.tsx` reads the cookie server-side via `cookies()` from `next/headers`. Role check (`isAdminRole`) is authoritative — a non-admin with `view_mode=admin` is always treated as user. Logout clears the cookie alongside auth cookies.

**Tech Stack:** Next.js 16 App Router, React 19, Tailwind, shadcn/ui, Vitest for unit tests, Playwright for E2E, `cookies()` from `next/headers` for server-side reads, `document.cookie` for client writes.

**Reference spec:** `docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md` §4.1
**Epic branch:** `epic/library-to-game` (create from `main-dev` before task 1)
**Feature branch:** `feature/s1-view-toggle` (from epic branch)

---

## File Structure (created or modified by this plan)

**New files:**
- `apps/web/src/lib/view-mode/cookie.ts` — cookie read/write helpers (client-safe)
- `apps/web/src/lib/view-mode/server.ts` — server-side cookie reader (uses `next/headers`)
- `apps/web/src/lib/view-mode/constants.ts` — cookie name + type exports
- `apps/web/src/hooks/useViewMode.ts` — client hook: `{ viewMode, toggle }`
- `apps/web/src/components/layout/ViewModeToggle.tsx` — the icon switch component
- `apps/web/src/lib/view-mode/__tests__/cookie.test.ts` — unit tests for cookie helpers
- `apps/web/src/hooks/__tests__/useViewMode.test.ts` — unit tests for the hook
- `apps/web/src/components/layout/__tests__/ViewModeToggle.test.tsx` — component tests
- `apps/web/e2e/sub-features/s1-admin-toggle.spec.ts` — Playwright E2E (smoke only in S1; S6a extends)

**Modified files:**
- `apps/web/src/components/layout/UserShell/UserTopNav.tsx` — render `<ViewModeToggle>` when `isAdminRole(user.role)` (reads user via `useCurrentUser` hook)
- `apps/web/src/app/admin/(dashboard)/layout.tsx` — add server-side guard: if cookie `view_mode === 'user'`, redirect to `/`
- `apps/web/src/actions/auth.ts` — `logoutAction` must also clear `meepleai_view_mode` cookie

**Total estimate:** ~9 new files + 3 modifications. ~250 lines of production code + ~300 lines of tests.

---

## Preconditions

Before starting Task 1, verify:

- [ ] **Epic branch exists**
  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  git fetch origin
  git checkout main-dev
  git pull origin main-dev
  git checkout -b epic/library-to-game
  git config branch.epic/library-to-game.parent main-dev
  git push -u origin epic/library-to-game
  ```
  Expected: branch `epic/library-to-game` pushed to remote.

- [ ] **Feature branch created from epic**
  ```bash
  git checkout -b feature/s1-view-toggle
  git config branch.feature/s1-view-toggle.parent epic/library-to-game
  ```
  Expected: HEAD on `feature/s1-view-toggle`.

- [ ] **Frontend workspace clean**
  ```bash
  cd apps/web
  pnpm install
  pnpm typecheck
  pnpm lint
  ```
  Expected: 0 errors. Any pre-existing errors should be noted BEFORE starting to avoid false blame.

---

## Task 1 — Cookie constants and types

**Files:**
- Create: `apps/web/src/lib/view-mode/constants.ts`
- Create: `apps/web/src/lib/view-mode/__tests__/cookie.test.ts` (test file used across tasks 1-2)

- [ ] **Step 1.1: Create the constants file**

  Write `apps/web/src/lib/view-mode/constants.ts`:
  ```typescript
  /**
   * View Mode — Cookie constants
   *
   * The `meepleai_view_mode` cookie stores the admin user's preferred shell.
   * Client-writable (not HttpOnly). Read server-side via `cookies()` from `next/headers`
   * in layout.tsx guards for SSR-consistent rendering.
   *
   * Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.1
   */

  /** Cookie name used across client and server to persist view mode */
  export const VIEW_MODE_COOKIE = 'meepleai_view_mode';

  /** The two valid view mode values */
  export type ViewMode = 'admin' | 'user';

  /** All valid view modes (for runtime validation) */
  export const VIEW_MODES: readonly ViewMode[] = ['admin', 'user'] as const;

  /** Cookie max-age: undefined = session cookie (cleared when browser closes) */
  export const VIEW_MODE_COOKIE_MAX_AGE: number | undefined = undefined;

  /** SameSite attribute — lax allows top-level navigation redirects */
  export const VIEW_MODE_COOKIE_SAMESITE = 'lax' as const;

  /** Path scope — whole app */
  export const VIEW_MODE_COOKIE_PATH = '/' as const;
  ```

- [ ] **Step 1.2: Commit**

  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  git add apps/web/src/lib/view-mode/constants.ts
  git commit -m "feat(s1): add view mode cookie constants and types"
  ```

---

## Task 2 — Client-side cookie helpers (TDD)

**Files:**
- Create: `apps/web/src/lib/view-mode/cookie.ts`
- Create: `apps/web/src/lib/view-mode/__tests__/cookie.test.ts`

- [ ] **Step 2.1: Write failing tests first**

  Write `apps/web/src/lib/view-mode/__tests__/cookie.test.ts`:
  ```typescript
  /**
   * Tests for client-side view mode cookie helpers.
   */
  import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

  import { readViewModeCookie, writeViewModeCookie, clearViewModeCookie } from '../cookie';
  import { VIEW_MODE_COOKIE } from '../constants';

  describe('view-mode cookie helpers', () => {
    beforeEach(() => {
      // Reset document.cookie before each test
      Object.defineProperty(document, 'cookie', {
        writable: true,
        value: '',
      });
    });

    afterEach(() => {
      vi.restoreAllMocks();
    });

    describe('readViewModeCookie', () => {
      it('returns null when cookie is absent', () => {
        document.cookie = '';
        expect(readViewModeCookie()).toBeNull();
      });

      it('returns "admin" when cookie value is admin', () => {
        document.cookie = `${VIEW_MODE_COOKIE}=admin`;
        expect(readViewModeCookie()).toBe('admin');
      });

      it('returns "user" when cookie value is user', () => {
        document.cookie = `${VIEW_MODE_COOKIE}=user`;
        expect(readViewModeCookie()).toBe('user');
      });

      it('returns null when cookie value is invalid', () => {
        document.cookie = `${VIEW_MODE_COOKIE}=malicious`;
        expect(readViewModeCookie()).toBeNull();
      });

      it('correctly extracts cookie when multiple cookies present', () => {
        document.cookie = `other_cookie=foo; ${VIEW_MODE_COOKIE}=admin; third=bar`;
        expect(readViewModeCookie()).toBe('admin');
      });
    });

    describe('writeViewModeCookie', () => {
      it('writes admin value to document.cookie', () => {
        writeViewModeCookie('admin');
        expect(document.cookie).toContain(`${VIEW_MODE_COOKIE}=admin`);
      });

      it('writes user value to document.cookie', () => {
        writeViewModeCookie('user');
        expect(document.cookie).toContain(`${VIEW_MODE_COOKIE}=user`);
      });

      it('includes path=/ in the cookie string', () => {
        const spy = vi.spyOn(document, 'cookie', 'set');
        writeViewModeCookie('admin');
        expect(spy).toHaveBeenCalledWith(expect.stringContaining('path=/'));
      });

      it('includes SameSite=Lax in the cookie string', () => {
        const spy = vi.spyOn(document, 'cookie', 'set');
        writeViewModeCookie('admin');
        expect(spy).toHaveBeenCalledWith(expect.stringContaining('SameSite=Lax'));
      });
    });

    describe('clearViewModeCookie', () => {
      it('clears the cookie by setting expired date', () => {
        const spy = vi.spyOn(document, 'cookie', 'set');
        clearViewModeCookie();
        expect(spy).toHaveBeenCalledWith(
          expect.stringContaining('Max-Age=0')
        );
      });
    });
  });
  ```

- [ ] **Step 2.2: Run tests and verify they fail**

  ```bash
  cd apps/web
  pnpm vitest run src/lib/view-mode/__tests__/cookie.test.ts
  ```
  Expected: FAIL with "Cannot find module '../cookie'" (or similar — the file doesn't exist yet).

- [ ] **Step 2.3: Implement the cookie helpers**

  Write `apps/web/src/lib/view-mode/cookie.ts`:
  ```typescript
  /**
   * Client-side view mode cookie helpers.
   *
   * These functions are safe to call only in the browser. For server-side
   * cookie reading, use `./server.ts` which uses `next/headers`.
   */
  import {
    VIEW_MODE_COOKIE,
    VIEW_MODE_COOKIE_PATH,
    VIEW_MODE_COOKIE_SAMESITE,
    VIEW_MODES,
    type ViewMode,
  } from './constants';

  /**
   * Read the view mode cookie from `document.cookie`.
   * Returns null if the cookie is absent or contains an invalid value.
   *
   * Safe to call only in browser environments (no-op in SSR).
   */
  export function readViewModeCookie(): ViewMode | null {
    if (typeof document === 'undefined') return null;

    const match = document.cookie
      .split(';')
      .map((c) => c.trim())
      .find((c) => c.startsWith(`${VIEW_MODE_COOKIE}=`));

    if (!match) return null;

    const value = match.slice(VIEW_MODE_COOKIE.length + 1);
    return (VIEW_MODES as readonly string[]).includes(value) ? (value as ViewMode) : null;
  }

  /**
   * Write the view mode cookie to `document.cookie`.
   * Session-scoped (no Max-Age), SameSite=Lax, client-readable.
   *
   * Safe to call only in browser environments (no-op in SSR).
   */
  export function writeViewModeCookie(mode: ViewMode): void {
    if (typeof document === 'undefined') return;

    const parts = [
      `${VIEW_MODE_COOKIE}=${mode}`,
      `path=${VIEW_MODE_COOKIE_PATH}`,
      `SameSite=${VIEW_MODE_COOKIE_SAMESITE === 'lax' ? 'Lax' : VIEW_MODE_COOKIE_SAMESITE}`,
    ];

    // Secure flag in https contexts only (cookie helpers run in browser)
    if (typeof window !== 'undefined' && window.location.protocol === 'https:') {
      parts.push('Secure');
    }

    document.cookie = parts.join('; ');
  }

  /**
   * Clear the view mode cookie by setting an expired date.
   * Safe to call only in browser environments (no-op in SSR).
   */
  export function clearViewModeCookie(): void {
    if (typeof document === 'undefined') return;

    const parts = [
      `${VIEW_MODE_COOKIE}=`,
      `path=${VIEW_MODE_COOKIE_PATH}`,
      'Max-Age=0',
      `SameSite=${VIEW_MODE_COOKIE_SAMESITE === 'lax' ? 'Lax' : VIEW_MODE_COOKIE_SAMESITE}`,
    ];

    document.cookie = parts.join('; ');
  }
  ```

- [ ] **Step 2.4: Run tests and verify they pass**

  ```bash
  cd apps/web
  pnpm vitest run src/lib/view-mode/__tests__/cookie.test.ts
  ```
  Expected: 10 tests PASS (5 read + 4 write + 1 clear).

- [ ] **Step 2.5: Commit**

  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  git add apps/web/src/lib/view-mode/cookie.ts apps/web/src/lib/view-mode/__tests__/cookie.test.ts
  git commit -m "feat(s1): add client-side view mode cookie helpers with tests"
  ```

---

## Task 3 — Server-side cookie reader (TDD)

**Files:**
- Create: `apps/web/src/lib/view-mode/server.ts`
- Create: `apps/web/src/lib/view-mode/__tests__/server.test.ts`

- [ ] **Step 3.1: Write failing test**

  Write `apps/web/src/lib/view-mode/__tests__/server.test.ts`:
  ```typescript
  /**
   * Tests for server-side view mode cookie reader.
   * Mocks `next/headers` cookies() API.
   */
  import { describe, it, expect, vi, beforeEach } from 'vitest';

  import { readViewModeCookieServer } from '../server';
  import { VIEW_MODE_COOKIE } from '../constants';

  // Mock next/headers
  const mockGet = vi.fn();
  vi.mock('next/headers', () => ({
    cookies: () => ({
      get: mockGet,
    }),
  }));

  describe('readViewModeCookieServer', () => {
    beforeEach(() => {
      mockGet.mockReset();
    });

    it('returns null when cookie is absent', async () => {
      mockGet.mockReturnValue(undefined);
      const result = await readViewModeCookieServer();
      expect(mockGet).toHaveBeenCalledWith(VIEW_MODE_COOKIE);
      expect(result).toBeNull();
    });

    it('returns "admin" when cookie has admin value', async () => {
      mockGet.mockReturnValue({ name: VIEW_MODE_COOKIE, value: 'admin' });
      const result = await readViewModeCookieServer();
      expect(result).toBe('admin');
    });

    it('returns "user" when cookie has user value', async () => {
      mockGet.mockReturnValue({ name: VIEW_MODE_COOKIE, value: 'user' });
      const result = await readViewModeCookieServer();
      expect(result).toBe('user');
    });

    it('returns null when cookie has invalid value', async () => {
      mockGet.mockReturnValue({ name: VIEW_MODE_COOKIE, value: 'malicious' });
      const result = await readViewModeCookieServer();
      expect(result).toBeNull();
    });
  });
  ```

- [ ] **Step 3.2: Run test and verify it fails**

  ```bash
  cd apps/web
  pnpm vitest run src/lib/view-mode/__tests__/server.test.ts
  ```
  Expected: FAIL with "Cannot find module '../server'".

- [ ] **Step 3.3: Implement the server reader**

  Write `apps/web/src/lib/view-mode/server.ts`:
  ```typescript
  /**
   * Server-side view mode cookie reader.
   *
   * Uses Next.js 16 `cookies()` from `next/headers` to read the
   * `meepleai_view_mode` cookie from Server Components, Route Handlers,
   * and Server Actions. This enables SSR-consistent rendering of the
   * correct shell (admin vs user) on first paint.
   *
   * @see https://nextjs.org/docs/app/api-reference/functions/cookies
   */
  import { cookies } from 'next/headers';

  import { VIEW_MODE_COOKIE, VIEW_MODES, type ViewMode } from './constants';

  /**
   * Read the view mode cookie from the current request's cookies.
   * Returns null if absent or invalid.
   *
   * MUST be called from Server Components, Route Handlers, or Server Actions.
   * Throws if called from client code.
   */
  export async function readViewModeCookieServer(): Promise<ViewMode | null> {
    const cookieStore = await cookies();
    const cookie = cookieStore.get(VIEW_MODE_COOKIE);

    if (!cookie) return null;

    const value = cookie.value;
    return (VIEW_MODES as readonly string[]).includes(value) ? (value as ViewMode) : null;
  }
  ```

- [ ] **Step 3.4: Run tests and verify they pass**

  ```bash
  cd apps/web
  pnpm vitest run src/lib/view-mode/__tests__/server.test.ts
  ```
  Expected: 4 tests PASS.

- [ ] **Step 3.5: Commit**

  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  git add apps/web/src/lib/view-mode/server.ts apps/web/src/lib/view-mode/__tests__/server.test.ts
  git commit -m "feat(s1): add server-side view mode cookie reader"
  ```

---

## Task 4 — `useViewMode` hook (TDD)

**Files:**
- Create: `apps/web/src/hooks/useViewMode.ts`
- Create: `apps/web/src/hooks/__tests__/useViewMode.test.ts`

- [ ] **Step 4.1: Write failing tests**

  Write `apps/web/src/hooks/__tests__/useViewMode.test.ts`:
  ```typescript
  /**
   * Tests for useViewMode client hook.
   */
  import { act, renderHook } from '@testing-library/react';
  import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

  import { useViewMode } from '../useViewMode';
  import { VIEW_MODE_COOKIE } from '@/lib/view-mode/constants';

  // Mock next/navigation
  const mockPush = vi.fn();
  vi.mock('next/navigation', () => ({
    useRouter: () => ({ push: mockPush }),
    usePathname: () => '/admin/overview',
  }));

  describe('useViewMode', () => {
    beforeEach(() => {
      Object.defineProperty(document, 'cookie', { writable: true, value: '' });
      mockPush.mockReset();
    });

    afterEach(() => {
      vi.restoreAllMocks();
    });

    it('defaults to "admin" when cookie absent and on /admin path', () => {
      const { result } = renderHook(() => useViewMode());
      expect(result.current.viewMode).toBe('admin');
    });

    it('reads initial value from cookie when present', () => {
      document.cookie = `${VIEW_MODE_COOKIE}=user`;
      const { result } = renderHook(() => useViewMode());
      expect(result.current.viewMode).toBe('user');
    });

    it('toggle flips admin → user and writes cookie', () => {
      document.cookie = `${VIEW_MODE_COOKIE}=admin`;
      const { result } = renderHook(() => useViewMode());
      act(() => {
        result.current.toggle();
      });
      expect(result.current.viewMode).toBe('user');
      expect(document.cookie).toContain(`${VIEW_MODE_COOKIE}=user`);
    });

    it('toggle flips user → admin', () => {
      document.cookie = `${VIEW_MODE_COOKIE}=user`;
      const { result } = renderHook(() => useViewMode());
      act(() => {
        result.current.toggle();
      });
      expect(result.current.viewMode).toBe('admin');
    });

    it('toggle navigates to / when switching to user mode', () => {
      document.cookie = `${VIEW_MODE_COOKIE}=admin`;
      const { result } = renderHook(() => useViewMode());
      act(() => {
        result.current.toggle();
      });
      expect(mockPush).toHaveBeenCalledWith('/');
    });

    it('toggle navigates to /admin/overview when switching to admin mode', () => {
      document.cookie = `${VIEW_MODE_COOKIE}=user`;
      const { result } = renderHook(() => useViewMode());
      act(() => {
        result.current.toggle();
      });
      expect(mockPush).toHaveBeenCalledWith('/admin/overview');
    });
  });
  ```

- [ ] **Step 4.2: Run tests and verify they fail**

  ```bash
  cd apps/web
  pnpm vitest run src/hooks/__tests__/useViewMode.test.ts
  ```
  Expected: FAIL with "Cannot find module '../useViewMode'".

- [ ] **Step 4.3: Implement the hook**

  Write `apps/web/src/hooks/useViewMode.ts`:
  ```typescript
  /**
   * useViewMode — client hook for view mode toggle state.
   *
   * Reads the `meepleai_view_mode` cookie on mount, exposes `viewMode` + `toggle`.
   * On toggle: flips the value, writes the cookie, navigates to the opposite shell.
   *
   * Path-based default: if no cookie, defaults to 'admin' on /admin/* paths,
   * 'user' elsewhere. This matches the shell the user is currently viewing.
   */
  'use client';

  import { useCallback, useEffect, useState } from 'react';

  import { usePathname, useRouter } from 'next/navigation';

  import { readViewModeCookie, writeViewModeCookie } from '@/lib/view-mode/cookie';
  import type { ViewMode } from '@/lib/view-mode/constants';

  interface UseViewModeResult {
    viewMode: ViewMode;
    toggle: () => void;
  }

  /**
   * Default view mode inferred from URL path.
   */
  function defaultViewMode(pathname: string): ViewMode {
    return pathname.startsWith('/admin') ? 'admin' : 'user';
  }

  export function useViewMode(): UseViewModeResult {
    const router = useRouter();
    const pathname = usePathname();

    // Initial state reads cookie synchronously (matches SSR when cookie exists)
    const [viewMode, setViewMode] = useState<ViewMode>(() => {
      if (typeof document === 'undefined') return defaultViewMode(pathname);
      return readViewModeCookie() ?? defaultViewMode(pathname);
    });

    // Re-sync if cookie changed while component is mounted (e.g., another tab)
    useEffect(() => {
      const current = readViewModeCookie();
      if (current && current !== viewMode) {
        setViewMode(current);
      }
      // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const toggle = useCallback(() => {
      const next: ViewMode = viewMode === 'admin' ? 'user' : 'admin';
      writeViewModeCookie(next);
      setViewMode(next);
      router.push(next === 'admin' ? '/admin/overview' : '/');
    }, [viewMode, router]);

    return { viewMode, toggle };
  }
  ```

- [ ] **Step 4.4: Run tests and verify they pass**

  ```bash
  cd apps/web
  pnpm vitest run src/hooks/__tests__/useViewMode.test.ts
  ```
  Expected: 6 tests PASS.

- [ ] **Step 4.5: Commit**

  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  git add apps/web/src/hooks/useViewMode.ts apps/web/src/hooks/__tests__/useViewMode.test.ts
  git commit -m "feat(s1): add useViewMode client hook with cookie persistence"
  ```

---

## Task 5 — `ViewModeToggle` component (TDD)

**Files:**
- Create: `apps/web/src/components/layout/ViewModeToggle.tsx`
- Create: `apps/web/src/components/layout/__tests__/ViewModeToggle.test.tsx`

- [ ] **Step 5.1: Write failing tests**

  Write `apps/web/src/components/layout/__tests__/ViewModeToggle.test.tsx`:
  ```typescript
  /**
   * Tests for ViewModeToggle component.
   */
  import { render, screen, fireEvent } from '@testing-library/react';
  import { describe, it, expect, vi } from 'vitest';

  import { ViewModeToggle } from '../ViewModeToggle';

  // Hoisted mocks
  const mockToggle = vi.fn();
  let mockViewMode: 'admin' | 'user' = 'admin';
  vi.mock('@/hooks/useViewMode', () => ({
    useViewMode: () => ({ viewMode: mockViewMode, toggle: mockToggle }),
  }));

  describe('ViewModeToggle', () => {
    beforeEach(() => {
      mockToggle.mockReset();
      mockViewMode = 'admin';
    });

    it('renders a switch with aria-checked reflecting admin mode', () => {
      mockViewMode = 'admin';
      render(<ViewModeToggle />);
      const toggle = screen.getByRole('switch', { name: /cambia vista/i });
      expect(toggle).toBeInTheDocument();
      expect(toggle).toHaveAttribute('aria-checked', 'true');
    });

    it('renders aria-checked=false when in user mode', () => {
      mockViewMode = 'user';
      render(<ViewModeToggle />);
      const toggle = screen.getByRole('switch', { name: /cambia vista/i });
      expect(toggle).toHaveAttribute('aria-checked', 'false');
    });

    it('calls toggle() when clicked', () => {
      render(<ViewModeToggle />);
      const toggle = screen.getByRole('switch', { name: /cambia vista/i });
      fireEvent.click(toggle);
      expect(mockToggle).toHaveBeenCalledOnce();
    });

    it('is keyboard-operable via Enter key', () => {
      render(<ViewModeToggle />);
      const toggle = screen.getByRole('switch', { name: /cambia vista/i });
      toggle.focus();
      fireEvent.keyDown(toggle, { key: 'Enter' });
      expect(mockToggle).toHaveBeenCalledOnce();
    });

    it('is keyboard-operable via Space key', () => {
      render(<ViewModeToggle />);
      const toggle = screen.getByRole('switch', { name: /cambia vista/i });
      toggle.focus();
      fireEvent.keyDown(toggle, { key: ' ' });
      expect(mockToggle).toHaveBeenCalledOnce();
    });

    it('has data-testid for E2E selection', () => {
      render(<ViewModeToggle />);
      expect(screen.getByTestId('view-mode-toggle')).toBeInTheDocument();
    });
  });
  ```

- [ ] **Step 5.2: Run tests and verify they fail**

  ```bash
  cd apps/web
  pnpm vitest run src/components/layout/__tests__/ViewModeToggle.test.tsx
  ```
  Expected: FAIL with "Cannot find module '../ViewModeToggle'".

- [ ] **Step 5.3: Implement the component**

  Write `apps/web/src/components/layout/ViewModeToggle.tsx`:
  ```typescript
  /**
   * ViewModeToggle — admin↔user view switcher for the top navbar.
   *
   * Icon pill switch (44×26) that flips between admin and user shells.
   * Only rendered when the current user has an admin role — the parent
   * component is responsible for gating visibility.
   *
   * Accessibility: role="switch" with aria-checked, keyboard-operable.
   */
  'use client';

  import { useCallback, type KeyboardEvent } from 'react';

  import { useViewMode } from '@/hooks/useViewMode';
  import { cn } from '@/lib/utils';

  export function ViewModeToggle() {
    const { viewMode, toggle } = useViewMode();
    const isAdmin = viewMode === 'admin';

    const handleKeyDown = useCallback(
      (e: KeyboardEvent<HTMLButtonElement>) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          toggle();
        }
      },
      [toggle]
    );

    return (
      <button
        type="button"
        role="switch"
        aria-checked={isAdmin}
        aria-label="Cambia vista tra admin e utente"
        onClick={toggle}
        onKeyDown={handleKeyDown}
        data-testid="view-mode-toggle"
        className={cn(
          'relative inline-flex h-[26px] w-[44px] shrink-0 items-center rounded-full',
          'bg-gradient-to-br from-purple-500 to-orange-500',
          'border-2 border-white shadow-sm',
          'cursor-pointer transition-colors',
          'focus:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
        )}
      >
        {/* Left indicator: user icon */}
        <span
          className={cn(
            'absolute left-1 text-[9px] transition-opacity',
            isAdmin ? 'opacity-70' : 'opacity-30'
          )}
          aria-hidden="true"
        >
          👤
        </span>
        {/* Right indicator: admin gear */}
        <span
          className={cn(
            'absolute right-1 text-[9px] transition-opacity',
            isAdmin ? 'opacity-30' : 'opacity-70'
          )}
          aria-hidden="true"
        >
          ⚙️
        </span>
        {/* Knob */}
        <span
          className={cn(
            'pointer-events-none absolute top-[1px] h-5 w-5 rounded-full bg-white shadow-sm',
            'flex items-center justify-center text-[11px]',
            'transition-transform duration-200',
            isAdmin ? 'translate-x-[18px]' : 'translate-x-[1px]'
          )}
          aria-hidden="true"
        >
          {isAdmin ? '⚙️' : '👤'}
        </span>
      </button>
    );
  }
  ```

- [ ] **Step 5.4: Run tests and verify they pass**

  ```bash
  cd apps/web
  pnpm vitest run src/components/layout/__tests__/ViewModeToggle.test.tsx
  ```
  Expected: 6 tests PASS.

- [ ] **Step 5.5: Commit**

  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  git add apps/web/src/components/layout/ViewModeToggle.tsx apps/web/src/components/layout/__tests__/ViewModeToggle.test.tsx
  git commit -m "feat(s1): add ViewModeToggle icon switch component"
  ```

---

## Task 6 — Integrate ViewModeToggle into UserTopNav

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/UserTopNav.tsx`

- [ ] **Step 6.1: Read the current UserTopNav file state**

  ```bash
  cd apps/web
  cat src/components/layout/UserShell/UserTopNav.tsx | head -50
  ```

  Confirm the imports section and the utility actions `<div className="flex items-center gap-2 shrink-0">` block (around line 134).

- [ ] **Step 6.2: Add ViewModeToggle import and conditional render**

  Edit `apps/web/src/components/layout/UserShell/UserTopNav.tsx`:

  Change the imports section (around lines 1-18) to add two new imports:
  ```typescript
  import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
  import { isAdminRole } from '@/lib/utils/roles';

  import { ViewModeToggle } from '../ViewModeToggle';
  ```

  Change the component body to read the current user and conditionally render the toggle. Add inside the `UserTopNav` function body, right after the existing hooks:
  ```typescript
  const { data: currentUser } = useCurrentUser();
  const canToggleView = isAdminRole(currentUser?.role);
  ```

  Change the "Right: utility actions" block (around line 134) from:
  ```tsx
  <div className="flex items-center gap-2 shrink-0">
    <NotificationBell />
    <UserMenuDropdown />
  </div>
  ```
  to:
  ```tsx
  <div className="flex items-center gap-2 shrink-0">
    {canToggleView && <ViewModeToggle />}
    <NotificationBell />
    <UserMenuDropdown />
  </div>
  ```

- [ ] **Step 6.3: Verify typecheck passes**

  ```bash
  cd apps/web
  pnpm typecheck
  ```
  Expected: 0 errors.

- [ ] **Step 6.4: Run existing UserTopNav tests to ensure no regression**

  ```bash
  cd apps/web
  pnpm vitest run src/components/layout/UserShell
  ```
  Expected: all existing tests PASS. If any fail due to the new `useCurrentUser` dependency, add the mock to the test setup (hoist mock at top).

- [ ] **Step 6.5: Commit**

  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  git add apps/web/src/components/layout/UserShell/UserTopNav.tsx
  git commit -m "feat(s1): render ViewModeToggle in UserTopNav for admin users"
  ```

---

## Task 7 — Server-side guard in admin layout

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/layout.tsx`

- [ ] **Step 7.1: Read the current admin layout file**

  Current file content (already read):
  ```typescript
  import { type ReactNode } from 'react';
  import { type Metadata } from 'next';

  import { PdfProcessingNotifier } from '@/components/admin/layout/PdfProcessingNotifier';
  import { RequireRole } from '@/components/auth/RequireRole';
  import { AdminShell } from '@/components/layout/AdminShell';

  export const metadata: Metadata = { ... };

  export default function DashboardLayout({ children }: { children: ReactNode }) {
    return (
      <RequireRole allowedRoles={['Admin']}>
        <PdfProcessingNotifier />
        <AdminShell>{children}</AdminShell>
      </RequireRole>
    );
  }
  ```

- [ ] **Step 7.2: Convert to async function and add server-side redirect**

  Edit `apps/web/src/app/admin/(dashboard)/layout.tsx` to become:
  ```typescript
  import { type ReactNode } from 'react';

  import { type Metadata } from 'next';
  import { redirect } from 'next/navigation';

  import { PdfProcessingNotifier } from '@/components/admin/layout/PdfProcessingNotifier';
  import { RequireRole } from '@/components/auth/RequireRole';
  import { AdminShell } from '@/components/layout/AdminShell';
  import { readViewModeCookieServer } from '@/lib/view-mode/server';

  export const metadata: Metadata = {
    title: {
      template: '%s | MeepleAI Admin',
      default: 'Admin Dashboard | MeepleAI',
    },
    description: 'MeepleAI Administration Dashboard',
  };

  /**
   * Dashboard route group layout.
   *
   * Guard chain:
   *  1. Server-side: if `meepleai_view_mode` cookie === 'user', redirect to '/' (no admin flash).
   *  2. Client-side: RequireRole enforces admin role (still the authoritative check).
   *
   * Applies the AdminShell to all pages under /admin/(dashboard)/.
   */
  export default async function DashboardLayout({ children }: { children: ReactNode }) {
    const viewMode = await readViewModeCookieServer();
    if (viewMode === 'user') {
      redirect('/');
    }

    return (
      <RequireRole allowedRoles={['Admin']}>
        <PdfProcessingNotifier />
        <AdminShell>{children}</AdminShell>
      </RequireRole>
    );
  }
  ```

- [ ] **Step 7.3: Verify typecheck passes**

  ```bash
  cd apps/web
  pnpm typecheck
  ```
  Expected: 0 errors.

- [ ] **Step 7.4: Commit**

  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  git add apps/web/src/app/admin/\(dashboard\)/layout.tsx
  git commit -m "feat(s1): server-side redirect admin dashboard to / when view_mode=user"
  ```

---

## Task 8 — Clear view mode cookie on logout

**Files:**
- Modify: `apps/web/src/actions/auth.ts`

- [ ] **Step 8.1: Locate the logoutAction function**

  ```bash
  cd apps/web
  grep -n "export async function logoutAction" src/actions/auth.ts
  ```
  Expected: line number of the function (should be around line 227).

- [ ] **Step 8.2: Read the current implementation of logoutAction**

  Read the function body starting from the identified line. It calls `api.auth.logout()` and clears auth cookies on the server side. We need to also clear `meepleai_view_mode`.

  Check if the server uses `cookies().delete()` from `next/headers`. If the logout API call deletes HttpOnly cookies server-side but `meepleai_view_mode` is client-readable, we need to clear it either via:
  1. Adding `cookies().delete(VIEW_MODE_COOKIE)` in the server action (preferred — server is the source of truth)
  2. Or adding `clearViewModeCookie()` call at the logout button level (fallback)

  Use approach 1: delete server-side in `logoutAction`.

- [ ] **Step 8.3: Add cookie deletion to logoutAction**

  Edit `apps/web/src/actions/auth.ts`. Find the `logoutAction` function and add the cookie deletion right after `await api.auth.logout();`:

  Add import at top of file (near other `next/headers` imports, if any; otherwise add a new import block):
  ```typescript
  import { cookies } from 'next/headers';

  import { VIEW_MODE_COOKIE } from '@/lib/view-mode/constants';
  ```

  In the `logoutAction` function body, right after the `api.auth.logout()` call, add:
  ```typescript
  // Clear view mode cookie so the next session starts fresh
  const cookieStore = await cookies();
  cookieStore.delete(VIEW_MODE_COOKIE);
  ```

- [ ] **Step 8.4: Verify typecheck passes**

  ```bash
  cd apps/web
  pnpm typecheck
  ```
  Expected: 0 errors.

- [ ] **Step 8.5: Run existing auth tests**

  ```bash
  cd apps/web
  pnpm vitest run src/actions/__tests__/auth.test.ts 2>/dev/null || pnpm vitest run src/actions -- --testNamePattern=logout
  ```
  Expected: existing tests still PASS. If any test mocks `cookies()`, it may need updating; if so, update the mock to include a no-op `delete` method.

- [ ] **Step 8.6: Commit**

  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  git add apps/web/src/actions/auth.ts
  git commit -m "feat(s1): clear view mode cookie on logout"
  ```

---

## Task 9 — Playwright E2E smoke test

**Files:**
- Create: `apps/web/e2e/sub-features/s1-admin-toggle.spec.ts`

- [ ] **Step 9.1: Check if sub-features/ folder exists**

  ```bash
  cd apps/web
  ls e2e/sub-features 2>/dev/null || echo "MISSING"
  ```
  If MISSING → create: `mkdir -p e2e/sub-features`

- [ ] **Step 9.2: Write the E2E smoke test**

  Write `apps/web/e2e/sub-features/s1-admin-toggle.spec.ts`:
  ```typescript
  /**
   * S1 — Admin↔User view mode toggle E2E smoke tests.
   *
   * Validates the toggle is rendered for admin users, clicking it flips the
   * view mode cookie, and the redirect happens as expected.
   *
   * Related spec: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.1
   */
  import { test, expect } from '@playwright/test';

  import { loginAsAdmin } from '../fixtures/auth';

  test.describe('S1 · Admin↔User view mode toggle', () => {
    test.beforeEach(async ({ context }) => {
      // Ensure no stale view mode cookie from previous tests
      await context.clearCookies({ name: 'meepleai_view_mode' });
    });

    test('toggle is visible for admin users', async ({ page }) => {
      await loginAsAdmin(page);
      await page.goto('/admin/overview');

      const toggle = page.getByTestId('view-mode-toggle');
      await expect(toggle).toBeVisible();
      await expect(toggle).toHaveAttribute('role', 'switch');
      await expect(toggle).toHaveAttribute('aria-checked', 'true');
    });

    test('clicking toggle redirects admin to user shell', async ({ page }) => {
      await loginAsAdmin(page);
      await page.goto('/admin/overview');

      const toggle = page.getByTestId('view-mode-toggle');
      await toggle.click();

      // Should navigate away from /admin/*
      await page.waitForURL((url) => !url.pathname.startsWith('/admin'), { timeout: 5000 });
      expect(page.url()).not.toContain('/admin');
    });

    test('cookie is set after clicking toggle', async ({ page, context }) => {
      await loginAsAdmin(page);
      await page.goto('/admin/overview');

      await page.getByTestId('view-mode-toggle').click();
      await page.waitForLoadState('networkidle');

      const cookies = await context.cookies();
      const viewModeCookie = cookies.find((c) => c.name === 'meepleai_view_mode');
      expect(viewModeCookie?.value).toBe('user');
    });

    test('SSR redirects admin layout to / when cookie is user (no flash)', async ({
      page,
      context,
    }) => {
      await loginAsAdmin(page);
      // Pre-set the cookie before navigating to /admin
      await context.addCookies([
        {
          name: 'meepleai_view_mode',
          value: 'user',
          url: page.url() || 'http://localhost:3000',
          path: '/',
          sameSite: 'Lax',
        },
      ]);

      const response = await page.goto('/admin/overview');
      // Server-side redirect returns 307 or the final / URL
      expect(page.url()).not.toContain('/admin/overview');
    });

    test('toggle returns to admin when clicked from user shell', async ({ page }) => {
      await loginAsAdmin(page);
      await page.goto('/');

      const toggle = page.getByTestId('view-mode-toggle');
      await expect(toggle).toBeVisible();
      await expect(toggle).toHaveAttribute('aria-checked', 'false');

      await toggle.click();
      await page.waitForURL((url) => url.pathname.startsWith('/admin'), { timeout: 5000 });
      expect(page.url()).toContain('/admin');
    });
  });
  ```

- [ ] **Step 9.3: Check if loginAsAdmin fixture exists**

  ```bash
  cd apps/web
  ls e2e/fixtures/auth.ts 2>/dev/null && grep -l "loginAsAdmin" e2e/fixtures/auth.ts
  ```
  If fixture doesn't exist or doesn't export `loginAsAdmin`, fall back to using an existing fixture (grep for `login` in `e2e/fixtures/`) or inline the admin login steps.

- [ ] **Step 9.4: Run the E2E test locally**

  ```bash
  cd apps/web
  pnpm test:e2e --project=mobile-chrome e2e/sub-features/s1-admin-toggle.spec.ts
  ```
  Expected: 5 tests PASS on mobile-chrome. If `loginAsAdmin` fixture is missing, update the test to use whatever login fixture is conventional in the project.

- [ ] **Step 9.5: Commit**

  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  git add apps/web/e2e/sub-features/s1-admin-toggle.spec.ts
  git commit -m "test(s1): add Playwright E2E smoke tests for view mode toggle"
  ```

---

## Task 10 — Final validation and PR

- [ ] **Step 10.1: Run full frontend test suite**

  ```bash
  cd apps/web
  pnpm typecheck
  pnpm lint
  pnpm test
  ```
  Expected: all PASS. Any pre-existing failures that are unrelated to S1 must be noted, not "fixed" under S1 scope.

- [ ] **Step 10.2: Verify coverage on new files ≥ 85%**

  ```bash
  cd apps/web
  pnpm test:coverage -- src/lib/view-mode src/hooks/useViewMode.ts src/components/layout/ViewModeToggle.tsx
  ```
  Expected: ≥ 85% line coverage on each new file. If below, add edge-case tests.

- [ ] **Step 10.3: Push the feature branch**

  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  git push -u origin feature/s1-view-toggle
  ```

- [ ] **Step 10.4: Create the PR into the epic branch**

  ```bash
  cd D:/Repositories/meepleai-monorepo-backend
  gh pr create \
    --base epic/library-to-game \
    --head feature/s1-view-toggle \
    --title "feat(s1): admin↔user view mode toggle in navbar" \
    --body "$(cat <<'EOF'
## Summary

Implements S1 of the `library-to-game` epic: a one-click view mode toggle in the top navbar that flips admin users between the admin shell and the user shell while preserving their authenticated session.

## Changes

- New cookie `meepleai_view_mode` (client-writable, SameSite=Lax, session-scoped)
- New utilities: `lib/view-mode/{constants,cookie,server}.ts` for client + server cookie access
- New hook: `useViewMode` with toggle + router navigation
- New component: `ViewModeToggle` (icon pill switch, role="switch", keyboard-operable)
- `UserTopNav` conditionally renders the toggle when `isAdminRole(user.role)`
- `app/admin/(dashboard)/layout.tsx` gets a server-side guard that redirects to `/` if cookie = `user` (no first-paint flash)
- `logoutAction` now clears the view mode cookie

## Reference

- Spec: `docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md` §4.1
- Plan: `docs/superpowers/plans/2026-04-09-s1-view-toggle.md`

## Test plan

- [x] Unit tests: `src/lib/view-mode/__tests__/cookie.test.ts` (10 tests)
- [x] Unit tests: `src/lib/view-mode/__tests__/server.test.ts` (4 tests)
- [x] Unit tests: `src/hooks/__tests__/useViewMode.test.ts` (6 tests)
- [x] Component tests: `src/components/layout/__tests__/ViewModeToggle.test.tsx` (6 tests)
- [x] E2E smoke: `e2e/sub-features/s1-admin-toggle.spec.ts` (5 tests)
- [x] Coverage ≥ 85% on new files
- [x] `pnpm typecheck` and `pnpm lint` pass

## Security

- Role check remains authoritative: `RequireRole` in admin layout still enforces admin-only access regardless of cookie
- Non-admin users with forged `view_mode=admin` cookie still get blocked by `RequireRole`
- Cookie is `SameSite=Lax` to prevent CSRF on the toggle action

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
  ```
  Expected: PR URL printed. Open in browser for review.

- [ ] **Step 10.5: Wait for review and merge**

  This step is manual. The plan ends here. After merge into `epic/library-to-game`, the next sub-feature plan (S6a scaffold) can be generated.

---

## Rollback plan

If something goes wrong after merge and needs to be rolled back:

```bash
git checkout epic/library-to-game
git revert <merge-commit-sha>
git push
```

All files added by S1 are new or cleanly additive; no existing data, migrations, or APIs are modified, so revert is safe.

---

## Known limitations of S1 (deferred to later sub-features)

- **E2E test does not yet check the happy path end-to-end.** S6a (next sub-feature in the epic order) creates the happy path skeleton; S6b fills it with all steps.
- **No visual regression screenshot.** S6 uses smoke + a11y, not pixel diff.
- **Toggle is desktop-and-mobile identical.** Future UX iteration may want a different mobile treatment (part of a potential S1b, not in scope now).
- **Admin layout guard is the only server-side check.** There is no equivalent guard in `app/(authenticated)/layout.tsx` to redirect admin view mode → `/admin/overview` because a non-admin user can still use the app normally (we don't want to force them away from `/`). Only the admin side needs a guard.

---

## Plan self-review checklist

- [x] Every step has a concrete command or code block — no placeholders.
- [x] Type consistency: `ViewMode` = `'admin' | 'user'` used uniformly across all files.
- [x] Function names consistent: `readViewModeCookie`, `writeViewModeCookie`, `clearViewModeCookie`, `readViewModeCookieServer`, `useViewMode`, `toggle`.
- [x] Cookie name constant `VIEW_MODE_COOKIE = 'meepleai_view_mode'` referenced from constants module in every consumer.
- [x] Spec §4.1 requirements covered: cookie-based (not sessionStorage), SSR-consistent, role check authoritative, logout clears cookie, icon switch in navbar, `role="switch"` + `aria-checked` + keyboard-operable.
- [x] Every task ends with a commit — frequent commits.
- [x] Tests precede implementation — TDD.
- [x] Preconditions include branch setup per CLAUDE.md rules (parent branch tracking).
- [x] PR targets epic branch, not main, per CLAUDE.md rule.

**Plan length:** 10 tasks, ~60 steps, estimated 4-6 hours of focused work.
