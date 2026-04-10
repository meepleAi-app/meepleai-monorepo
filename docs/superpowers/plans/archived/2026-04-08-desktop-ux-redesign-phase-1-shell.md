# Desktop UX Redesign — Phase 1: Shell Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the new desktop shell (top bar 64px + contextual mini-nav 48px + persistent hand rail 76px) behind a feature flag, reusing existing infrastructure (`AppNavbar`, `CardRack`, `ContextMiniNav`, `useCardHand`, `ContextBarRegistrar`) and laying the foundation for the following phases (Dashboard, Library Hub, Chat panel).

**Architecture:** This phase **evolves** the existing shell, it does not replace it. A new `DesktopShell` component wraps the new layout (new `TopBar64`, new `DesktopHandRail` built on `CardRack`, and a global `ContextMiniNavSlot` driven by a new `useMiniNavConfig` hook + Zustand store). A `NEXT_PUBLIC_UX_REDESIGN` feature flag in `UserShellClient.tsx` toggles between the legacy `AppNavbar` layout and the new `DesktopShell`. No existing component is deleted in Phase 1.

**Tech Stack:** Next.js 16 App Router, React 19, TypeScript, Tailwind 4, Zustand, Vitest + Testing Library, Playwright (for E2E in Phase 5 only). All design tokens reused from `apps/web/src/styles/design-tokens.css`.

**Spec reference:** `docs/superpowers/specs/2026-04-08-desktop-ux-redesign-design.md` §4 (Shell Architecture)

---

## File Structure

### New files

| Path | Responsibility |
|---|---|
| `apps/web/src/lib/feature-flags.ts` | Central flag helper (`isUxRedesignEnabled()`) reading `NEXT_PUBLIC_UX_REDESIGN` at build time |
| `apps/web/src/lib/stores/mini-nav-config-store.ts` | Zustand store holding the current page's mini-nav config (tabs, active id, breadcrumb, primary action) |
| `apps/web/src/hooks/useMiniNavConfig.ts` | React hook for pages to register their mini-nav config; `{ setConfig, clear }` |
| `apps/web/src/components/layout/UserShell/v2/DesktopShell.tsx` | New shell composition: `<TopBar64>` + `<MiniNavSlot>` + `<DesktopHandRail>` + main content slot |
| `apps/web/src/components/layout/UserShell/v2/TopBar64.tsx` | New 64px top bar: logo + 3 nav links + search pill + chat icon + notification + avatar |
| `apps/web/src/components/layout/UserShell/v2/TopBarLogo.tsx` | Hex gradient logo "MeepleAi" wordmark, 32px hex + Quicksand wordmark |
| `apps/web/src/components/layout/UserShell/v2/TopBarNavLinks.tsx` | 3-link pill nav (Home / Libreria / Sessioni) with active state by pathname |
| `apps/web/src/components/layout/UserShell/v2/TopBarSearchPill.tsx` | Global search pill with ⌘K kbd + expand-on-focus behavior |
| `apps/web/src/components/layout/UserShell/v2/TopBarChatButton.tsx` | Chat trigger icon button (dispatches `openChatPanel()` — Phase 4; Phase 1 stub no-op + console.warn) |
| `apps/web/src/components/layout/UserShell/v2/MiniNavSlot.tsx` | Renders the current mini-nav from the `mini-nav-config-store`, hidden when empty |
| `apps/web/src/components/layout/UserShell/v2/DesktopHandRail.tsx` | New desktop hand rail (76px collapsed, 240px hover) that reads from `useCardHand` instead of hardcoded `NAV_ITEMS` |
| `apps/web/src/components/layout/UserShell/v2/HandRailItem.tsx` | Single hand card item 52×72 with accent edge, cover gradient, title, active/pinned state |
| `apps/web/src/components/layout/UserShell/v2/HandRailToolbar.tsx` | Bottom toolbar of the rail with Pin/Unpin and Expand buttons |
| `apps/web/src/components/layout/UserShell/v2/index.ts` | Public barrel exporting the above components |
| `apps/web/src/components/layout/UserShell/v2/__tests__/TopBar64.test.tsx` | Vitest tests for TopBar64 composition + nav link active state |
| `apps/web/src/components/layout/UserShell/v2/__tests__/DesktopHandRail.test.tsx` | Vitest tests for rail render from `useCardHand`, click nav, active state |
| `apps/web/src/components/layout/UserShell/v2/__tests__/MiniNavSlot.test.tsx` | Vitest tests for slot visibility based on store content |
| `apps/web/src/components/layout/UserShell/v2/__tests__/DesktopShell.test.tsx` | Vitest integration test: shell mounts, renders children, flag on/off |
| `apps/web/src/lib/__tests__/feature-flags.test.ts` | Vitest test for feature flag helper |
| `apps/web/src/lib/stores/__tests__/mini-nav-config-store.test.ts` | Vitest test for Zustand store set/clear |

### Modified files

| Path | Change |
|---|---|
| `apps/web/src/components/layout/UserShell/UserShellClient.tsx` | Add feature flag branch: if `isUxRedesignEnabled()` render `<DesktopShell>`, else legacy `<AppNavbar>` |
| `apps/web/.env.development.example` | Add `NEXT_PUBLIC_UX_REDESIGN=false` (commented default) |
| `apps/web/src/app/(authenticated)/layout.tsx` (if exists — else no change) | Ensure it uses `UserShell`, not directly `AppNavbar` |

### NOT touched in Phase 1

- `AppNavbar.tsx` — legacy path, unchanged
- `CardRack.tsx` — legacy path, unchanged (new rail is a separate file in `v2/`)
- `ContextMiniNav.tsx` — kept for pages that import it directly; new `MiniNavSlot` coexists
- `useCardHand` store — reused as-is, no changes
- Dashboard, Library, Chat pages — Phase 2-4

---

## Task 1: Feature flag helper

**Files:**
- Create: `apps/web/src/lib/feature-flags.ts`
- Test: `apps/web/src/lib/__tests__/feature-flags.test.ts`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/lib/__tests__/feature-flags.test.ts`:

```typescript
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { isUxRedesignEnabled } from '../feature-flags';

describe('feature-flags', () => {
  const originalEnv = process.env.NEXT_PUBLIC_UX_REDESIGN;

  afterEach(() => {
    if (originalEnv === undefined) delete process.env.NEXT_PUBLIC_UX_REDESIGN;
    else process.env.NEXT_PUBLIC_UX_REDESIGN = originalEnv;
  });

  describe('isUxRedesignEnabled', () => {
    it('returns false when env var is undefined', () => {
      delete process.env.NEXT_PUBLIC_UX_REDESIGN;
      expect(isUxRedesignEnabled()).toBe(false);
    });

    it('returns false when env var is "false"', () => {
      process.env.NEXT_PUBLIC_UX_REDESIGN = 'false';
      expect(isUxRedesignEnabled()).toBe(false);
    });

    it('returns true when env var is "true"', () => {
      process.env.NEXT_PUBLIC_UX_REDESIGN = 'true';
      expect(isUxRedesignEnabled()).toBe(true);
    });

    it('returns false for any other value', () => {
      process.env.NEXT_PUBLIC_UX_REDESIGN = '1';
      expect(isUxRedesignEnabled()).toBe(false);
    });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/lib/__tests__/feature-flags.test.ts`
Expected: FAIL — cannot resolve `../feature-flags`

- [ ] **Step 3: Create the feature flag helper**

Create `apps/web/src/lib/feature-flags.ts`:

```typescript
/**
 * Feature flag helpers.
 *
 * All flags read from build-time `NEXT_PUBLIC_*` env vars.
 * Changing a flag requires a rebuild (not a restart).
 */

/**
 * Desktop UX redesign (Phase 1 — shell, Phase 2 — dashboard, Phase 3 — library hub, Phase 4 — chat panel).
 *
 * Controlled by `NEXT_PUBLIC_UX_REDESIGN=true`. Defaults to false.
 * See: `docs/superpowers/specs/2026-04-08-desktop-ux-redesign-design.md`
 */
export function isUxRedesignEnabled(): boolean {
  return process.env.NEXT_PUBLIC_UX_REDESIGN === 'true';
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/lib/__tests__/feature-flags.test.ts`
Expected: PASS — 4 tests passing

- [ ] **Step 5: Add env var to example**

Append to `apps/web/.env.development.example`:

```
# Desktop UX redesign (Phase 1-5) — toggle new shell/dashboard/library/chat
NEXT_PUBLIC_UX_REDESIGN=false
```

- [ ] **Step 6: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-dev
git add apps/web/src/lib/feature-flags.ts apps/web/src/lib/__tests__/feature-flags.test.ts apps/web/.env.development.example
git commit -m "feat(web): add isUxRedesignEnabled feature flag"
```

---

## Task 2: Mini-nav config Zustand store

**Files:**
- Create: `apps/web/src/lib/stores/mini-nav-config-store.ts`
- Test: `apps/web/src/lib/stores/__tests__/mini-nav-config-store.test.ts`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/lib/stores/__tests__/mini-nav-config-store.test.ts`:

```typescript
import { describe, it, expect, beforeEach } from 'vitest';

import { useMiniNavConfigStore, type MiniNavConfig } from '../mini-nav-config-store';

describe('mini-nav-config-store', () => {
  beforeEach(() => {
    useMiniNavConfigStore.getState().clear();
  });

  it('starts with null config', () => {
    expect(useMiniNavConfigStore.getState().config).toBeNull();
  });

  it('setConfig stores the payload', () => {
    const config: MiniNavConfig = {
      breadcrumb: 'Libreria · Hub',
      tabs: [
        { id: 'hub', label: 'Hub', href: '/library' },
        { id: 'personal', label: 'Personal', href: '/library?tab=personal', count: 47 },
      ],
      activeTabId: 'hub',
    };
    useMiniNavConfigStore.getState().setConfig(config);
    expect(useMiniNavConfigStore.getState().config).toEqual(config);
  });

  it('clear resets to null', () => {
    useMiniNavConfigStore.getState().setConfig({
      breadcrumb: 'x',
      tabs: [{ id: 'a', label: 'A', href: '/a' }],
      activeTabId: 'a',
    });
    useMiniNavConfigStore.getState().clear();
    expect(useMiniNavConfigStore.getState().config).toBeNull();
  });

  it('supports optional primary action', () => {
    useMiniNavConfigStore.getState().setConfig({
      breadcrumb: 'Home',
      tabs: [{ id: 'a', label: 'A', href: '/' }],
      activeTabId: 'a',
      primaryAction: { label: '＋ Nuova partita', onClick: () => {} },
    });
    expect(useMiniNavConfigStore.getState().config?.primaryAction?.label).toBe('＋ Nuova partita');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/lib/stores/__tests__/mini-nav-config-store.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the store**

Create `apps/web/src/lib/stores/mini-nav-config-store.ts`:

```typescript
'use client';

import { create } from 'zustand';

export interface MiniNavTab {
  id: string;
  label: string;
  href: string;
  count?: number;
}

export interface MiniNavPrimaryAction {
  label: string;
  onClick: () => void;
  icon?: string;
}

export interface MiniNavConfig {
  breadcrumb: string;
  tabs: MiniNavTab[];
  activeTabId: string;
  primaryAction?: MiniNavPrimaryAction;
}

interface MiniNavConfigState {
  config: MiniNavConfig | null;
  setConfig: (config: MiniNavConfig) => void;
  clear: () => void;
}

export const useMiniNavConfigStore = create<MiniNavConfigState>(set => ({
  config: null,
  setConfig: config => set({ config }),
  clear: () => set({ config: null }),
}));
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/lib/stores/__tests__/mini-nav-config-store.test.ts`
Expected: PASS — 4 tests passing

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/stores/mini-nav-config-store.ts apps/web/src/lib/stores/__tests__/mini-nav-config-store.test.ts
git commit -m "feat(web): add mini-nav-config Zustand store"
```

---

## Task 3: useMiniNavConfig hook (page registration)

**Files:**
- Create: `apps/web/src/hooks/useMiniNavConfig.ts`
- Test: `apps/web/src/hooks/__tests__/useMiniNavConfig.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/hooks/__tests__/useMiniNavConfig.test.tsx`:

```typescript
import { renderHook } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';

import { useMiniNavConfigStore } from '@/lib/stores/mini-nav-config-store';

import { useMiniNavConfig } from '../useMiniNavConfig';

describe('useMiniNavConfig', () => {
  beforeEach(() => {
    useMiniNavConfigStore.getState().clear();
  });

  it('sets config on mount', () => {
    renderHook(() =>
      useMiniNavConfig({
        breadcrumb: 'Home',
        tabs: [{ id: 'a', label: 'A', href: '/' }],
        activeTabId: 'a',
      })
    );
    expect(useMiniNavConfigStore.getState().config?.breadcrumb).toBe('Home');
  });

  it('clears config on unmount', () => {
    const { unmount } = renderHook(() =>
      useMiniNavConfig({
        breadcrumb: 'Home',
        tabs: [{ id: 'a', label: 'A', href: '/' }],
        activeTabId: 'a',
      })
    );
    expect(useMiniNavConfigStore.getState().config).not.toBeNull();
    unmount();
    expect(useMiniNavConfigStore.getState().config).toBeNull();
  });

  it('updates config when props change', () => {
    const { rerender } = renderHook(
      ({ activeTabId }: { activeTabId: string }) =>
        useMiniNavConfig({
          breadcrumb: 'Home',
          tabs: [
            { id: 'a', label: 'A', href: '/a' },
            { id: 'b', label: 'B', href: '/b' },
          ],
          activeTabId,
        }),
      { initialProps: { activeTabId: 'a' } }
    );
    expect(useMiniNavConfigStore.getState().config?.activeTabId).toBe('a');
    rerender({ activeTabId: 'b' });
    expect(useMiniNavConfigStore.getState().config?.activeTabId).toBe('b');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/useMiniNavConfig.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the hook**

Create `apps/web/src/hooks/useMiniNavConfig.ts`:

```typescript
'use client';

import { useEffect } from 'react';

import { useMiniNavConfigStore, type MiniNavConfig } from '@/lib/stores/mini-nav-config-store';

/**
 * Pages call this hook to register their mini-nav config with the global shell.
 * The shell reads the store and renders MiniNavSlot automatically.
 * Config is cleared on unmount (so navigating away hides the mini-nav).
 */
export function useMiniNavConfig(config: MiniNavConfig): void {
  const setConfig = useMiniNavConfigStore(s => s.setConfig);
  const clear = useMiniNavConfigStore(s => s.clear);

  useEffect(() => {
    setConfig(config);
    return () => clear();
  }, [config, setConfig, clear]);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/useMiniNavConfig.test.tsx`
Expected: PASS — 3 tests passing

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/useMiniNavConfig.ts apps/web/src/hooks/__tests__/useMiniNavConfig.test.tsx
git commit -m "feat(web): add useMiniNavConfig hook for page registration"
```

---

## Task 4: TopBarLogo component

**Files:**
- Create: `apps/web/src/components/layout/UserShell/v2/TopBarLogo.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/layout/UserShell/v2/__tests__/TopBarLogo.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { TopBarLogo } from '../TopBarLogo';

describe('TopBarLogo', () => {
  it('renders the wordmark', () => {
    render(<TopBarLogo />);
    expect(screen.getByText('Meeple')).toBeInTheDocument();
    expect(screen.getByText('Ai')).toBeInTheDocument();
  });

  it('has accessible label', () => {
    render(<TopBarLogo />);
    expect(screen.getByRole('link', { name: /meepleai home/i })).toBeInTheDocument();
  });

  it('links to /', () => {
    render(<TopBarLogo />);
    const link = screen.getByRole('link', { name: /meepleai home/i });
    expect(link).toHaveAttribute('href', '/');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/TopBarLogo.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement TopBarLogo**

Create `apps/web/src/components/layout/UserShell/v2/TopBarLogo.tsx`:

```typescript
'use client';

import Link from 'next/link';

export function TopBarLogo() {
  return (
    <Link
      href="/"
      aria-label="MeepleAi home"
      className="flex items-center gap-2.5 font-[var(--font-quicksand)] font-extrabold text-[1.05rem] shrink-0"
    >
      <span
        aria-hidden
        className="flex h-8 w-8 items-center justify-center rounded-[10px] text-white font-extrabold text-sm"
        style={{
          background:
            'linear-gradient(135deg, hsl(25 95% 52%), hsl(38 92% 55%))',
          boxShadow: '0 2px 8px hsla(25, 95%, 45%, 0.35)',
        }}
      >
        ◆
      </span>
      <span className="text-[var(--nh-text-primary)]">Meeple</span>
      <span className="-ml-2.5" style={{ color: 'hsl(25 95% 42%)' }}>
        Ai
      </span>
    </Link>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/TopBarLogo.test.tsx`
Expected: PASS — 3 tests passing

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UserShell/v2/TopBarLogo.tsx apps/web/src/components/layout/UserShell/v2/__tests__/TopBarLogo.test.tsx
git commit -m "feat(web): add TopBarLogo component"
```

---

## Task 5: TopBarNavLinks component

**Files:**
- Create: `apps/web/src/components/layout/UserShell/v2/TopBarNavLinks.tsx`
- Test: `apps/web/src/components/layout/UserShell/v2/__tests__/TopBarNavLinks.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/layout/UserShell/v2/__tests__/TopBarNavLinks.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { TopBarNavLinks } from '../TopBarNavLinks';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
}));

import { usePathname } from 'next/navigation';

const mockUsePathname = usePathname as unknown as ReturnType<typeof vi.fn>;

describe('TopBarNavLinks', () => {
  it('renders all 3 links', () => {
    mockUsePathname.mockReturnValue('/');
    render(<TopBarNavLinks />);
    expect(screen.getByRole('link', { name: 'Home' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Libreria' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sessioni' })).toBeInTheDocument();
  });

  it('marks Home active when pathname is /', () => {
    mockUsePathname.mockReturnValue('/');
    render(<TopBarNavLinks />);
    expect(screen.getByRole('link', { name: 'Home' })).toHaveAttribute(
      'aria-current',
      'page'
    );
  });

  it('marks Home active when pathname is /dashboard', () => {
    mockUsePathname.mockReturnValue('/dashboard');
    render(<TopBarNavLinks />);
    expect(screen.getByRole('link', { name: 'Home' })).toHaveAttribute(
      'aria-current',
      'page'
    );
  });

  it('marks Libreria active when pathname starts with /library', () => {
    mockUsePathname.mockReturnValue('/library?tab=personal');
    render(<TopBarNavLinks />);
    expect(screen.getByRole('link', { name: 'Libreria' })).toHaveAttribute(
      'aria-current',
      'page'
    );
  });

  it('marks Sessioni active when pathname starts with /sessions', () => {
    mockUsePathname.mockReturnValue('/sessions/live/abc');
    render(<TopBarNavLinks />);
    expect(screen.getByRole('link', { name: 'Sessioni' })).toHaveAttribute(
      'aria-current',
      'page'
    );
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/TopBarNavLinks.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement TopBarNavLinks**

Create `apps/web/src/components/layout/UserShell/v2/TopBarNavLinks.tsx`:

```typescript
'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';

interface NavLink {
  href: string;
  label: string;
  isActive: (pathname: string) => boolean;
}

const LINKS: NavLink[] = [
  {
    href: '/',
    label: 'Home',
    isActive: p => p === '/' || p === '/dashboard' || p?.startsWith('/dashboard') || false,
  },
  {
    href: '/library',
    label: 'Libreria',
    isActive: p => p?.startsWith('/library') || false,
  },
  {
    href: '/sessions',
    label: 'Sessioni',
    isActive: p => p?.startsWith('/sessions') || false,
  },
];

export function TopBarNavLinks() {
  const pathname = usePathname() ?? '';

  return (
    <nav className="flex items-center gap-1 ml-3 shrink-0" aria-label="Primary">
      {LINKS.map(link => {
        const active = link.isActive(pathname);
        return (
          <Link
            key={link.href}
            href={link.href}
            aria-current={active ? 'page' : undefined}
            className={cn(
              'px-3.5 py-2 rounded-[10px] font-[var(--font-nunito)] font-bold text-[0.82rem] transition-colors',
              active
                ? 'text-[hsl(25_95%_38%)] shadow-[inset_0_0_0_1px_hsla(25,95%,45%,0.25)]'
                : 'text-[var(--nh-text-secondary)] hover:bg-[var(--nh-bg-surface)] hover:text-[var(--nh-text-primary)]'
            )}
            style={active ? { background: 'hsla(25, 95%, 45%, 0.1)' } : undefined}
          >
            {link.label}
          </Link>
        );
      })}
    </nav>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/TopBarNavLinks.test.tsx`
Expected: PASS — 5 tests passing

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UserShell/v2/TopBarNavLinks.tsx apps/web/src/components/layout/UserShell/v2/__tests__/TopBarNavLinks.test.tsx
git commit -m "feat(web): add TopBarNavLinks with pathname-based active state"
```

---

## Task 6: TopBarSearchPill component

**Files:**
- Create: `apps/web/src/components/layout/UserShell/v2/TopBarSearchPill.tsx`
- Test: `apps/web/src/components/layout/UserShell/v2/__tests__/TopBarSearchPill.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/layout/UserShell/v2/__tests__/TopBarSearchPill.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { TopBarSearchPill } from '../TopBarSearchPill';

describe('TopBarSearchPill', () => {
  it('shows placeholder text and ⌘K hint', () => {
    render(<TopBarSearchPill />);
    expect(screen.getByText(/cerca giochi/i)).toBeInTheDocument();
    expect(screen.getByText('⌘K')).toBeInTheDocument();
  });

  it('invokes onOpen when clicked', async () => {
    const onOpen = vi.fn();
    const user = userEvent.setup();
    render(<TopBarSearchPill onOpen={onOpen} />);
    await user.click(screen.getByRole('button', { name: /search/i }));
    expect(onOpen).toHaveBeenCalledOnce();
  });

  it('invokes onOpen on ⌘K keyboard shortcut', async () => {
    const onOpen = vi.fn();
    render(<TopBarSearchPill onOpen={onOpen} />);
    // Simulate Cmd+K
    window.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'k', metaKey: true, bubbles: true })
    );
    expect(onOpen).toHaveBeenCalledOnce();
  });

  it('invokes onOpen on Ctrl+K keyboard shortcut', async () => {
    const onOpen = vi.fn();
    render(<TopBarSearchPill onOpen={onOpen} />);
    window.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'k', ctrlKey: true, bubbles: true })
    );
    expect(onOpen).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/TopBarSearchPill.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement TopBarSearchPill**

Create `apps/web/src/components/layout/UserShell/v2/TopBarSearchPill.tsx`:

```typescript
'use client';

import { useEffect } from 'react';

interface TopBarSearchPillProps {
  /** Called when the pill is clicked or ⌘K/Ctrl+K is pressed */
  onOpen?: () => void;
  placeholder?: string;
}

export function TopBarSearchPill({
  onOpen,
  placeholder = 'Cerca giochi, sessioni, regole, giocatori…',
}: TopBarSearchPillProps) {
  useEffect(() => {
    if (!onOpen) return;

    const handler = (e: KeyboardEvent) => {
      if (e.key === 'k' && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        onOpen();
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [onOpen]);

  return (
    <div className="flex-1 min-w-0 px-2 max-w-[420px]">
      <button
        type="button"
        aria-label="Search"
        onClick={() => onOpen?.()}
        className="w-full flex items-center gap-3 px-5 py-[11px] rounded-xl border border-[var(--nh-border-default)] bg-[var(--nh-bg-surface)] text-left text-[0.82rem] text-[var(--nh-text-muted)] hover:bg-white hover:border-[rgba(160,120,60,0.2)] hover:shadow-[var(--shadow-warm-sm)] transition-all"
      >
        <span className="shrink-0 text-sm" aria-hidden>
          🔍
        </span>
        <span className="flex-1 truncate">{placeholder}</span>
        <span
          className="shrink-0 px-2 py-0.5 rounded-[5px] border border-[var(--nh-border-default)] bg-[rgba(160,120,60,0.08)] text-[10px] font-mono font-bold text-[var(--nh-text-secondary)]"
          aria-hidden
        >
          ⌘K
        </span>
      </button>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/TopBarSearchPill.test.tsx`
Expected: PASS — 4 tests passing

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UserShell/v2/TopBarSearchPill.tsx apps/web/src/components/layout/UserShell/v2/__tests__/TopBarSearchPill.test.tsx
git commit -m "feat(web): add TopBarSearchPill with ⌘K shortcut"
```

---

## Task 7: TopBarChatButton (stub)

**Files:**
- Create: `apps/web/src/components/layout/UserShell/v2/TopBarChatButton.tsx`
- Test: `apps/web/src/components/layout/UserShell/v2/__tests__/TopBarChatButton.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/layout/UserShell/v2/__tests__/TopBarChatButton.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { TopBarChatButton } from '../TopBarChatButton';

describe('TopBarChatButton', () => {
  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => {});
  });

  it('renders with accessible label', () => {
    render(<TopBarChatButton />);
    expect(screen.getByRole('button', { name: /chat agente/i })).toBeInTheDocument();
  });

  it('shows notification dot when hasUnread is true', () => {
    render(<TopBarChatButton hasUnread />);
    expect(screen.getByTestId('chat-unread-dot')).toBeInTheDocument();
  });

  it('hides notification dot when hasUnread is false', () => {
    render(<TopBarChatButton hasUnread={false} />);
    expect(screen.queryByTestId('chat-unread-dot')).not.toBeInTheDocument();
  });

  it('calls onOpen when clicked', async () => {
    const onOpen = vi.fn();
    const user = userEvent.setup();
    render(<TopBarChatButton onOpen={onOpen} />);
    await user.click(screen.getByRole('button', { name: /chat agente/i }));
    expect(onOpen).toHaveBeenCalledOnce();
  });

  it('warns if clicked without onOpen (Phase 1 stub)', async () => {
    const user = userEvent.setup();
    render(<TopBarChatButton />);
    await user.click(screen.getByRole('button', { name: /chat agente/i }));
    expect(console.warn).toHaveBeenCalledWith(
      expect.stringContaining('TopBarChatButton')
    );
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/TopBarChatButton.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement TopBarChatButton**

Create `apps/web/src/components/layout/UserShell/v2/TopBarChatButton.tsx`:

```typescript
'use client';

interface TopBarChatButtonProps {
  onOpen?: () => void;
  hasUnread?: boolean;
}

export function TopBarChatButton({ onOpen, hasUnread = false }: TopBarChatButtonProps) {
  const handleClick = () => {
    if (onOpen) {
      onOpen();
      return;
    }
    // Phase 1: chat panel not wired yet — Phase 4 will provide onOpen
    // eslint-disable-next-line no-console
    console.warn('[TopBarChatButton] onOpen handler not provided — chat panel not wired yet (Phase 4)');
  };

  return (
    <button
      type="button"
      aria-label="Chat agente"
      onClick={handleClick}
      className="relative flex h-10 w-10 items-center justify-center rounded-[10px] border border-[var(--nh-border-default)] bg-[var(--nh-bg-surface)] text-base shrink-0 hover:bg-white hover:shadow-[var(--shadow-warm-sm)] hover:-translate-y-px transition-all"
    >
      <span aria-hidden>💬</span>
      {hasUnread && (
        <span
          data-testid="chat-unread-dot"
          aria-label="Unread messages"
          className="absolute top-[7px] right-[7px] h-2 w-2 rounded-full"
          style={{
            background: 'hsl(350 89% 58%)',
            boxShadow: '0 0 0 2px var(--nh-bg-surface)',
          }}
        />
      )}
    </button>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/TopBarChatButton.test.tsx`
Expected: PASS — 5 tests passing

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UserShell/v2/TopBarChatButton.tsx apps/web/src/components/layout/UserShell/v2/__tests__/TopBarChatButton.test.tsx
git commit -m "feat(web): add TopBarChatButton stub (Phase 4 will wire onOpen)"
```

---

## Task 8: TopBar64 composition

**Files:**
- Create: `apps/web/src/components/layout/UserShell/v2/TopBar64.tsx`
- Test: `apps/web/src/components/layout/UserShell/v2/__tests__/TopBar64.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/layout/UserShell/v2/__tests__/TopBar64.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { TopBar64 } from '../TopBar64';

vi.mock('next/navigation', () => ({
  usePathname: () => '/',
}));

// Mock the notification bell (existing component with runtime deps)
vi.mock('@/components/notifications', () => ({
  NotificationBell: () => <button aria-label="Notifications">🔔</button>,
}));

vi.mock('@/components/layout/UserMenuDropdown', () => ({
  UserMenuDropdown: () => <button aria-label="User menu">MR</button>,
}));

describe('TopBar64', () => {
  it('renders logo, nav links, search, chat button, and user menu', () => {
    render(<TopBar64 />);
    expect(screen.getByRole('link', { name: /meepleai home/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Home' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /search/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /chat agente/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /notifications/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /user menu/i })).toBeInTheDocument();
  });

  it('is 64px tall and sticky', () => {
    const { container } = render(<TopBar64 />);
    const header = container.querySelector('header');
    expect(header).toHaveClass('h-16');
    expect(header).toHaveClass('sticky');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/TopBar64.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement TopBar64**

Create `apps/web/src/components/layout/UserShell/v2/TopBar64.tsx`:

```typescript
'use client';

import { NotificationBell } from '@/components/notifications';

import { TopBarChatButton } from './TopBarChatButton';
import { TopBarLogo } from './TopBarLogo';
import { TopBarNavLinks } from './TopBarNavLinks';
import { TopBarSearchPill } from './TopBarSearchPill';
import { UserMenuDropdown } from '../../UserMenuDropdown';

interface TopBar64Props {
  onOpenChat?: () => void;
  onOpenSearch?: () => void;
}

/**
 * New 64px top bar for the UX redesign (Phase 1).
 * Composes: Logo + NavLinks + SearchPill + ChatButton + Notifications + UserMenu
 * Sticky positioning, backdrop-blur, border-bottom.
 */
export function TopBar64({ onOpenChat, onOpenSearch }: TopBar64Props) {
  return (
    <header
      data-testid="top-bar-64"
      className="sticky top-0 z-40 h-16 flex items-center gap-4 px-6 border-b border-[var(--nh-border-default)] backdrop-blur-md"
      style={{
        background: 'rgba(255, 252, 248, 0.85)',
      }}
    >
      <TopBarLogo />
      <TopBarNavLinks />
      <TopBarSearchPill onOpen={onOpenSearch} />
      <div className="flex items-center gap-2.5 shrink-0">
        <TopBarChatButton onOpen={onOpenChat} />
        <NotificationBell />
        <UserMenuDropdown />
      </div>
    </header>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/TopBar64.test.tsx`
Expected: PASS — 2 tests passing

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UserShell/v2/TopBar64.tsx apps/web/src/components/layout/UserShell/v2/__tests__/TopBar64.test.tsx
git commit -m "feat(web): add TopBar64 composition"
```

---

## Task 9: MiniNavSlot (reads from store)

**Files:**
- Create: `apps/web/src/components/layout/UserShell/v2/MiniNavSlot.tsx`
- Test: `apps/web/src/components/layout/UserShell/v2/__tests__/MiniNavSlot.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/layout/UserShell/v2/__tests__/MiniNavSlot.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';

import { useMiniNavConfigStore } from '@/lib/stores/mini-nav-config-store';

import { MiniNavSlot } from '../MiniNavSlot';

describe('MiniNavSlot', () => {
  beforeEach(() => {
    useMiniNavConfigStore.getState().clear();
  });

  it('renders nothing when config is null', () => {
    const { container } = render(<MiniNavSlot />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders breadcrumb and tabs when config is set', () => {
    useMiniNavConfigStore.getState().setConfig({
      breadcrumb: 'Libreria · Hub',
      tabs: [
        { id: 'hub', label: 'Hub', href: '/library' },
        { id: 'personal', label: 'Personal', href: '/library?tab=personal', count: 47 },
      ],
      activeTabId: 'hub',
    });
    render(<MiniNavSlot />);
    expect(screen.getByText(/Libreria/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Hub/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Personal/i })).toBeInTheDocument();
    expect(screen.getByText('47')).toBeInTheDocument();
  });

  it('marks active tab with aria-current', () => {
    useMiniNavConfigStore.getState().setConfig({
      breadcrumb: 'Libreria',
      tabs: [
        { id: 'hub', label: 'Hub', href: '/library' },
        { id: 'personal', label: 'Personal', href: '/library?tab=personal' },
      ],
      activeTabId: 'personal',
    });
    render(<MiniNavSlot />);
    expect(screen.getByRole('link', { name: /Personal/i })).toHaveAttribute(
      'aria-current',
      'page'
    );
  });

  it('renders primary action button when provided', () => {
    useMiniNavConfigStore.getState().setConfig({
      breadcrumb: 'Home',
      tabs: [{ id: 'a', label: 'A', href: '/' }],
      activeTabId: 'a',
      primaryAction: { label: '＋ Nuova partita', onClick: () => {} },
    });
    render(<MiniNavSlot />);
    expect(
      screen.getByRole('button', { name: /Nuova partita/i })
    ).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/MiniNavSlot.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement MiniNavSlot**

Create `apps/web/src/components/layout/UserShell/v2/MiniNavSlot.tsx`:

```typescript
'use client';

import Link from 'next/link';

import { useMiniNavConfigStore } from '@/lib/stores/mini-nav-config-store';
import { cn } from '@/lib/utils';

/**
 * MiniNavSlot — renders the mini-nav registered by the current page.
 * Hidden when no config is set.
 */
export function MiniNavSlot() {
  const config = useMiniNavConfigStore(s => s.config);
  if (!config) return null;

  return (
    <div
      data-testid="mini-nav-slot"
      className="h-12 flex items-center gap-1 px-7 pl-[104px] border-b border-[var(--nh-border-default)] bg-[var(--nh-bg-base)]"
    >
      <div className="text-xs font-semibold text-[var(--nh-text-muted)] mr-5">
        <span aria-hidden>›</span> {config.breadcrumb}
      </div>
      {config.tabs.map(tab => {
        const active = tab.id === config.activeTabId;
        return (
          <Link
            key={tab.id}
            href={tab.href}
            aria-current={active ? 'page' : undefined}
            className={cn(
              'relative px-3.5 py-2 rounded-lg text-[0.78rem] font-bold flex items-center gap-1.5 transition-colors',
              active
                ? 'text-[var(--nh-text-primary)]'
                : 'text-[var(--nh-text-secondary)] hover:bg-[var(--nh-bg-surface)]'
            )}
          >
            {tab.label}
            {tab.count !== undefined && (
              <span className="px-1.5 py-0.5 rounded text-[10px] font-extrabold bg-[rgba(160,120,60,0.1)] text-[var(--nh-text-secondary)]">
                {tab.count}
              </span>
            )}
            {active && (
              <span
                aria-hidden
                className="absolute bottom-[-12px] left-3.5 right-3.5 h-0.5 rounded-t"
                style={{ background: 'hsl(25 95% 45%)' }}
              />
            )}
          </Link>
        );
      })}
      <div className="flex-1" />
      {config.primaryAction && (
        <button
          type="button"
          onClick={config.primaryAction.onClick}
          className="px-3.5 py-2 rounded-[10px] text-[0.78rem] font-bold text-white border-none cursor-pointer flex items-center gap-1.5 transition-all hover:-translate-y-px"
          style={{
            background: 'linear-gradient(135deg, hsl(25 95% 48%), hsl(25 95% 40%))',
            boxShadow: '0 2px 6px hsla(25, 95%, 45%, 0.3)',
          }}
        >
          {config.primaryAction.label}
        </button>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/MiniNavSlot.test.tsx`
Expected: PASS — 4 tests passing

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UserShell/v2/MiniNavSlot.tsx apps/web/src/components/layout/UserShell/v2/__tests__/MiniNavSlot.test.tsx
git commit -m "feat(web): add MiniNavSlot reading from mini-nav-config-store"
```

---

## Task 10: HandRailItem component

**Files:**
- Create: `apps/web/src/components/layout/UserShell/v2/HandRailItem.tsx`
- Test: `apps/web/src/components/layout/UserShell/v2/__tests__/HandRailItem.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/layout/UserShell/v2/__tests__/HandRailItem.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { HandRailItem } from '../HandRailItem';

describe('HandRailItem', () => {
  const baseCard = {
    id: 'azul',
    entity: 'game' as const,
    title: 'Azul',
    href: '/library/azul',
  };

  it('renders title in the card body', () => {
    render(<HandRailItem card={baseCard} isActive={false} />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
  });

  it('renders as a link to card.href', () => {
    render(<HandRailItem card={baseCard} isActive={false} />);
    expect(screen.getByRole('link')).toHaveAttribute('href', '/library/azul');
  });

  it('applies active state when isActive is true', () => {
    render(<HandRailItem card={baseCard} isActive />);
    expect(screen.getByRole('link')).toHaveAttribute('data-active', 'true');
  });

  it('uses entity data attribute for styling', () => {
    render(<HandRailItem card={baseCard} isActive={false} />);
    expect(screen.getByRole('link')).toHaveAttribute('data-entity', 'game');
  });

  it('applies different entity for session', () => {
    render(
      <HandRailItem
        card={{ ...baseCard, entity: 'session' }}
        isActive={false}
      />
    );
    expect(screen.getByRole('link')).toHaveAttribute('data-entity', 'session');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/HandRailItem.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement HandRailItem**

Create `apps/web/src/components/layout/UserShell/v2/HandRailItem.tsx`:

```typescript
'use client';

import Link from 'next/link';

import type { HandCard } from '@/stores/use-card-hand';

import { cn } from '@/lib/utils';

interface HandRailItemProps {
  card: HandCard;
  isActive: boolean;
}

const ENTITY_GRADIENTS: Record<string, string> = {
  game: 'linear-gradient(135deg, hsl(25 75% 78%), hsl(25 80% 55%))',
  session: 'linear-gradient(135deg, hsl(240 55% 75%), hsl(240 60% 55%))',
  chat: 'linear-gradient(135deg, hsl(220 72% 78%), hsl(220 80% 58%))',
  player: 'linear-gradient(135deg, hsl(262 78% 78%), hsl(262 83% 60%))',
  agent: 'linear-gradient(135deg, hsl(38 85% 75%), hsl(38 92% 55%))',
  kb: 'linear-gradient(135deg, hsl(210 55% 78%), hsl(210 40% 55%))',
  event: 'linear-gradient(135deg, hsl(350 75% 78%), hsl(350 89% 60%))',
  toolkit: 'linear-gradient(135deg, hsl(142 60% 78%), hsl(142 70% 45%))',
  tool: 'linear-gradient(135deg, hsl(195 70% 78%), hsl(195 80% 50%))',
};

const ENTITY_ACCENTS: Record<string, string> = {
  game: 'hsl(25 95% 45%)',
  session: 'hsl(240 60% 55%)',
  chat: 'hsl(220 80% 55%)',
  player: 'hsl(262 83% 58%)',
  agent: 'hsl(38 92% 50%)',
  kb: 'hsl(210 40% 55%)',
  event: 'hsl(350 89% 60%)',
  toolkit: 'hsl(142 70% 45%)',
  tool: 'hsl(195 80% 50%)',
};

const ENTITY_ICONS: Record<string, string> = {
  game: '🎲',
  session: '🎯',
  chat: '💬',
  player: '👤',
  agent: '🤖',
  kb: '📚',
  event: '📅',
  toolkit: '🧰',
  tool: '🔧',
};

export function HandRailItem({ card, isActive }: HandRailItemProps) {
  const gradient = ENTITY_GRADIENTS[card.entity] ?? ENTITY_GRADIENTS.game;
  const accent = ENTITY_ACCENTS[card.entity] ?? ENTITY_ACCENTS.game;
  const icon = ENTITY_ICONS[card.entity] ?? '🎲';

  return (
    <Link
      href={card.href}
      data-entity={card.entity}
      data-active={isActive}
      title={card.title}
      className={cn(
        'relative block w-[52px] h-[72px] rounded-[10px] bg-[var(--nh-bg-elevated)] border border-[var(--nh-border-default)] overflow-hidden shrink-0 transition-all duration-300 ease-out',
        'shadow-[var(--shadow-warm-sm)]',
        'hover:translate-x-[3px] hover:scale-105 hover:shadow-[var(--shadow-warm-md)]',
        isActive &&
          'translate-x-[6px] scale-110 shadow-[var(--shadow-warm-lg)]'
      )}
      style={
        isActive
          ? { outline: `2px solid ${accent}`, outlineOffset: '2px' }
          : undefined
      }
    >
      <span
        aria-hidden
        className="absolute left-0 top-0 bottom-0 w-[3px]"
        style={{ background: accent }}
      />
      <div
        className="w-full h-[42px] flex items-center justify-center text-lg"
        style={{ background: gradient }}
        aria-hidden
      >
        {card.imageUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={card.imageUrl}
            alt=""
            className="w-full h-full object-cover"
          />
        ) : (
          icon
        )}
      </div>
      <span className="absolute bottom-[3px] left-[4px] right-[4px] text-[7px] font-bold font-[var(--font-quicksand)] leading-[1.1] text-[var(--nh-text-primary)] whitespace-nowrap overflow-hidden text-ellipsis text-center">
        {card.title}
      </span>
    </Link>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/HandRailItem.test.tsx`
Expected: PASS — 5 tests passing

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UserShell/v2/HandRailItem.tsx apps/web/src/components/layout/UserShell/v2/__tests__/HandRailItem.test.tsx
git commit -m "feat(web): add HandRailItem with entity-aware styling"
```

---

## Task 11: HandRailToolbar component

**Files:**
- Create: `apps/web/src/components/layout/UserShell/v2/HandRailToolbar.tsx`
- Test: `apps/web/src/components/layout/UserShell/v2/__tests__/HandRailToolbar.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/layout/UserShell/v2/__tests__/HandRailToolbar.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { HandRailToolbar } from '../HandRailToolbar';

describe('HandRailToolbar', () => {
  it('renders pin and expand buttons', () => {
    render(<HandRailToolbar onTogglePin={() => {}} onToggleExpand={() => {}} />);
    expect(screen.getByRole('button', { name: /pin/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /expand/i })).toBeInTheDocument();
  });

  it('calls onTogglePin when pin clicked', async () => {
    const onTogglePin = vi.fn();
    const user = userEvent.setup();
    render(<HandRailToolbar onTogglePin={onTogglePin} onToggleExpand={() => {}} />);
    await user.click(screen.getByRole('button', { name: /pin/i }));
    expect(onTogglePin).toHaveBeenCalledOnce();
  });

  it('calls onToggleExpand when expand clicked', async () => {
    const onToggleExpand = vi.fn();
    const user = userEvent.setup();
    render(<HandRailToolbar onTogglePin={() => {}} onToggleExpand={onToggleExpand} />);
    await user.click(screen.getByRole('button', { name: /expand/i }));
    expect(onToggleExpand).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/HandRailToolbar.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement HandRailToolbar**

Create `apps/web/src/components/layout/UserShell/v2/HandRailToolbar.tsx`:

```typescript
'use client';

interface HandRailToolbarProps {
  onTogglePin: () => void;
  onToggleExpand: () => void;
  isPinned?: boolean;
  isExpanded?: boolean;
}

export function HandRailToolbar({
  onTogglePin,
  onToggleExpand,
  isPinned = false,
  isExpanded = false,
}: HandRailToolbarProps) {
  return (
    <div className="w-full border-t border-[var(--nh-border-default)] pt-2.5 mt-1.5 flex flex-col items-center gap-1.5">
      <button
        type="button"
        aria-label={isPinned ? 'Unpin current card' : 'Pin current card'}
        onClick={onTogglePin}
        className="flex h-[34px] w-[34px] items-center justify-center rounded-lg border border-[var(--nh-border-default)] bg-[var(--nh-bg-surface)] text-[13px] hover:bg-white hover:shadow-[var(--shadow-warm-sm)] transition-all"
      >
        📌
      </button>
      <button
        type="button"
        aria-label={isExpanded ? 'Collapse rail' : 'Expand rail'}
        onClick={onToggleExpand}
        className="flex h-[34px] w-[34px] items-center justify-center rounded-lg border border-[var(--nh-border-default)] bg-[var(--nh-bg-surface)] text-[13px] hover:bg-white hover:shadow-[var(--shadow-warm-sm)] transition-all"
      >
        ⇔
      </button>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/HandRailToolbar.test.tsx`
Expected: PASS — 3 tests passing

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UserShell/v2/HandRailToolbar.tsx apps/web/src/components/layout/UserShell/v2/__tests__/HandRailToolbar.test.tsx
git commit -m "feat(web): add HandRailToolbar with pin/expand buttons"
```

---

## Task 12: DesktopHandRail (reads from useCardHand)

**Files:**
- Create: `apps/web/src/components/layout/UserShell/v2/DesktopHandRail.tsx`
- Test: `apps/web/src/components/layout/UserShell/v2/__tests__/DesktopHandRail.test.tsx`

- [ ] **Step 1: Confirm useCardHand store API**

The store `apps/web/src/stores/use-card-hand.ts` exposes (verified):
- State: `cards: HandCard[]`, `pinnedIds: Set<string>`, `focusedIdx: number`, etc.
- Actions: `drawCard`, `discardCard`, `focusCard`, `focusByHref`, `pinCard(id)`, `unpinCard(id)` (separate, NOT `togglePin`), `clear`
- Usage: `useCardHand()` returns the full state, or with selector `useCardHand(s => s.cards)`
- `isPinned(id)` is NOT a helper — derive from `pinnedIds.has(id)`

- [ ] **Step 2: Write the failing test**

Create `apps/web/src/components/layout/UserShell/v2/__tests__/DesktopHandRail.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/stores/use-card-hand', () => {
  const cards = [
    { id: 'azul', entity: 'game', title: 'Azul', href: '/library/azul' },
    { id: 'wings', entity: 'game', title: 'Wingspan', href: '/library/wings' },
    { id: 'ses', entity: 'session', title: 'Serata', href: '/sessions/live/123' },
  ];
  const state = {
    cards,
    pinnedIds: new Set<string>(),
    pinCard: vi.fn(),
    unpinCard: vi.fn(),
  };
  return {
    useCardHand: (selector?: (s: typeof state) => unknown) =>
      selector ? selector(state) : state,
  };
});

vi.mock('next/navigation', () => ({
  usePathname: () => '/library/azul',
}));

import { DesktopHandRail } from '../DesktopHandRail';

describe('DesktopHandRail', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders hand label', () => {
    render(<DesktopHandRail />);
    expect(screen.getByText(/la tua mano/i)).toBeInTheDocument();
  });

  it('renders all cards from useCardHand', () => {
    render(<DesktopHandRail />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Serata')).toBeInTheDocument();
  });

  it('marks the card matching the current pathname as active', () => {
    render(<DesktopHandRail />);
    const azul = screen.getByText('Azul').closest('a');
    expect(azul).toHaveAttribute('data-active', 'true');
  });

  it('renders the toolbar with pin/expand buttons', () => {
    render(<DesktopHandRail />);
    expect(screen.getByRole('button', { name: /pin/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /expand/i })).toBeInTheDocument();
  });

  it('is 76px wide by default', () => {
    const { container } = render(<DesktopHandRail />);
    const rail = container.querySelector('[data-testid="desktop-hand-rail"]');
    expect(rail).toHaveClass('w-[76px]');
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/DesktopHandRail.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 4: Implement DesktopHandRail**

Create `apps/web/src/components/layout/UserShell/v2/DesktopHandRail.tsx`:

```typescript
'use client';

import { useState } from 'react';

import { usePathname } from 'next/navigation';

import { useCardHand } from '@/stores/use-card-hand';

import { HandRailItem } from './HandRailItem';
import { HandRailToolbar } from './HandRailToolbar';

/**
 * DesktopHandRail — persistent left rail reading from useCardHand store.
 * Replaces the hardcoded NAV_ITEMS behavior of the legacy CardRack.
 *
 * Active state is computed from the current pathname: the card whose
 * href matches the pathname (exact or prefix for game/session ids) is active.
 */
export function DesktopHandRail() {
  const cards = useCardHand(s => s.cards);
  const pinnedIds = useCardHand(s => s.pinnedIds);
  const pinCard = useCardHand(s => s.pinCard);
  const unpinCard = useCardHand(s => s.unpinCard);
  const pathname = usePathname() ?? '';
  const [isExpanded, setIsExpanded] = useState(false);

  const isCardActive = (href: string) => {
    if (href === '/') return pathname === '/';
    return pathname === href || pathname.startsWith(href + '/');
  };

  const activeCard = cards.find(c => isCardActive(c.href));
  const isActivePinned = activeCard ? pinnedIds.has(activeCard.id) : false;

  const handleTogglePin = () => {
    if (!activeCard) return;
    if (isActivePinned) unpinCard(activeCard.id);
    else pinCard(activeCard.id);
  };

  return (
    <aside
      data-testid="desktop-hand-rail"
      aria-label="Cards in hand"
      className="hidden md:flex flex-col w-[76px] shrink-0 border-r border-[var(--nh-border-default)] py-3.5 pb-3 gap-2"
      style={{
        background:
          'linear-gradient(180deg, rgba(255,252,248,0.6), rgba(255,252,248,0.2))',
      }}
    >
      <span
        className="text-[9px] font-extrabold font-[var(--font-quicksand)] uppercase tracking-[0.12em] text-[var(--nh-text-muted)] pb-2 self-center"
        style={{ writingMode: 'vertical-rl', transform: 'rotate(180deg)' }}
      >
        La tua mano
      </span>
      <div className="flex-1 flex flex-col items-center gap-2.5 w-full overflow-y-auto pt-1 px-1">
        {cards.map(card => (
          <HandRailItem
            key={card.id}
            card={card}
            isActive={isCardActive(card.href)}
          />
        ))}
      </div>
      <HandRailToolbar
        onTogglePin={handleTogglePin}
        onToggleExpand={() => setIsExpanded(!isExpanded)}
        isPinned={isActivePinned}
        isExpanded={isExpanded}
      />
    </aside>
  );
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/DesktopHandRail.test.tsx`
Expected: PASS — 5 tests passing

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/layout/UserShell/v2/DesktopHandRail.tsx apps/web/src/components/layout/UserShell/v2/__tests__/DesktopHandRail.test.tsx
git commit -m "feat(web): add DesktopHandRail reading from useCardHand"
```

---

## Task 13: DesktopShell composition

**Files:**
- Create: `apps/web/src/components/layout/UserShell/v2/DesktopShell.tsx`
- Test: `apps/web/src/components/layout/UserShell/v2/__tests__/DesktopShell.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/layout/UserShell/v2/__tests__/DesktopShell.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/',
}));

vi.mock('@/stores/use-card-hand', () => ({
  useCardHand: () => ({ cards: [], togglePin: vi.fn(), isPinned: () => false }),
}));

vi.mock('@/components/notifications', () => ({
  NotificationBell: () => <button aria-label="Notifications">🔔</button>,
}));

vi.mock('@/components/layout/UserMenuDropdown', () => ({
  UserMenuDropdown: () => <button aria-label="User menu">MR</button>,
}));

import { DesktopShell } from '../DesktopShell';

describe('DesktopShell', () => {
  it('renders top bar, mini-nav slot, hand rail and children', () => {
    render(
      <DesktopShell>
        <div data-testid="content">hello</div>
      </DesktopShell>
    );
    expect(screen.getByTestId('top-bar-64')).toBeInTheDocument();
    expect(screen.getByTestId('desktop-hand-rail')).toBeInTheDocument();
    expect(screen.getByTestId('content')).toBeInTheDocument();
  });

  it('wraps children in a main landmark', () => {
    render(
      <DesktopShell>
        <div>child</div>
      </DesktopShell>
    );
    expect(screen.getByRole('main')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/DesktopShell.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement DesktopShell**

Create `apps/web/src/components/layout/UserShell/v2/DesktopShell.tsx`:

```typescript
'use client';

import type { ReactNode } from 'react';

import { DesktopHandRail } from './DesktopHandRail';
import { MiniNavSlot } from './MiniNavSlot';
import { TopBar64 } from './TopBar64';

interface DesktopShellProps {
  children: ReactNode;
}

/**
 * DesktopShell — new Phase 1 layout composition.
 *
 * Layout:
 *   ┌───────────────────────────────┐
 *   │ TopBar64 (64px sticky)        │
 *   ├───────────────────────────────┤
 *   │ MiniNavSlot (48px, optional)  │
 *   ├────┬──────────────────────────┤
 *   │ HR │ main                     │
 *   │ 76 │                          │
 *   └────┴──────────────────────────┘
 */
export function DesktopShell({ children }: DesktopShellProps) {
  return (
    <div className="min-h-dvh flex flex-col bg-[var(--nh-bg-base)]">
      <TopBar64 />
      <MiniNavSlot />
      <div className="flex-1 flex min-h-0">
        <DesktopHandRail />
        <main className="flex-1 min-w-0 overflow-y-auto">{children}</main>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/v2/__tests__/DesktopShell.test.tsx`
Expected: PASS — 2 tests passing

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UserShell/v2/DesktopShell.tsx apps/web/src/components/layout/UserShell/v2/__tests__/DesktopShell.test.tsx
git commit -m "feat(web): add DesktopShell composition (TopBar + MiniNav + HandRail + main)"
```

---

## Task 14: Barrel export for v2

**Files:**
- Create: `apps/web/src/components/layout/UserShell/v2/index.ts`

- [ ] **Step 1: Create the barrel**

Create `apps/web/src/components/layout/UserShell/v2/index.ts`:

```typescript
export { DesktopShell } from './DesktopShell';
export { TopBar64 } from './TopBar64';
export { TopBarLogo } from './TopBarLogo';
export { TopBarNavLinks } from './TopBarNavLinks';
export { TopBarSearchPill } from './TopBarSearchPill';
export { TopBarChatButton } from './TopBarChatButton';
export { MiniNavSlot } from './MiniNavSlot';
export { DesktopHandRail } from './DesktopHandRail';
export { HandRailItem } from './HandRailItem';
export { HandRailToolbar } from './HandRailToolbar';
```

- [ ] **Step 2: Verify imports resolve**

Run: `cd apps/web && pnpm typecheck`
Expected: no errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/layout/UserShell/v2/index.ts
git commit -m "feat(web): add v2 barrel export"
```

---

## Task 15: Wire DesktopShell into UserShellClient behind flag

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/UserShellClient.tsx`

- [ ] **Step 1: Read the current file**

Run: `cd apps/web && cat src/components/layout/UserShell/UserShellClient.tsx`

- [ ] **Step 2: Write an integration test**

Create `apps/web/src/components/layout/UserShell/__tests__/UserShellClient.flag.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/',
}));

vi.mock('@/stores/use-card-hand', () => ({
  useCardHand: () => ({ cards: [], togglePin: vi.fn(), isPinned: () => false }),
}));

vi.mock('@/components/notifications', () => ({
  NotificationBell: () => <button aria-label="Notifications">🔔</button>,
}));

vi.mock('@/components/layout/UserMenuDropdown', () => ({
  UserMenuDropdown: () => <button aria-label="User menu">MR</button>,
}));

vi.mock('@/components/layout/AppNavbar', () => ({
  AppNavbar: () => <nav data-testid="legacy-navbar">Legacy Navbar</nav>,
}));

vi.mock('@/components/dashboard', () => ({
  DashboardEngineProvider: ({ children }: { children: React.ReactNode }) => (
    <>{children}</>
  ),
}));

vi.mock('@/components/session/BackToSessionFAB', () => ({
  BackToSessionFAB: () => null,
}));

const originalEnv = process.env.NEXT_PUBLIC_UX_REDESIGN;

describe('UserShellClient feature flag', () => {
  afterEach(() => {
    if (originalEnv === undefined) delete process.env.NEXT_PUBLIC_UX_REDESIGN;
    else process.env.NEXT_PUBLIC_UX_REDESIGN = originalEnv;
    vi.resetModules();
  });

  it('renders legacy AppNavbar when flag is off', async () => {
    process.env.NEXT_PUBLIC_UX_REDESIGN = 'false';
    const { UserShellClient } = await import('../UserShellClient');
    render(
      <UserShellClient>
        <div>child</div>
      </UserShellClient>
    );
    expect(screen.getByTestId('legacy-navbar')).toBeInTheDocument();
    expect(screen.queryByTestId('top-bar-64')).not.toBeInTheDocument();
  });

  it('renders DesktopShell when flag is on', async () => {
    process.env.NEXT_PUBLIC_UX_REDESIGN = 'true';
    vi.resetModules();
    const { UserShellClient } = await import('../UserShellClient');
    render(
      <UserShellClient>
        <div>child</div>
      </UserShellClient>
    );
    expect(screen.getByTestId('top-bar-64')).toBeInTheDocument();
    expect(screen.queryByTestId('legacy-navbar')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/__tests__/UserShellClient.flag.test.tsx`
Expected: FAIL — the flag branch doesn't exist yet

- [ ] **Step 4: Update UserShellClient to branch on the flag**

Replace the contents of `apps/web/src/components/layout/UserShell/UserShellClient.tsx` with:

```typescript
'use client';

import { Suspense, type ReactNode } from 'react';

import { DashboardEngineProvider } from '@/components/dashboard';
import { AppNavbar } from '@/components/layout/AppNavbar';
import { BackToSessionFAB } from '@/components/session/BackToSessionFAB';
import { isUxRedesignEnabled } from '@/lib/feature-flags';

import { DesktopShell } from './v2';

interface UserShellClientProps {
  children: ReactNode;
}

export function UserShellClient({ children }: UserShellClientProps) {
  const useNewShell = isUxRedesignEnabled();

  if (useNewShell) {
    return (
      <DesktopShell>
        <DashboardEngineProvider>{children}</DashboardEngineProvider>
        <Suspense>
          <BackToSessionFAB />
        </Suspense>
      </DesktopShell>
    );
  }

  return (
    <div className="min-h-dvh bg-background">
      <AppNavbar />
      <main className="flex-1 min-w-0">
        <DashboardEngineProvider>{children}</DashboardEngineProvider>
      </main>
      <Suspense>
        <BackToSessionFAB />
      </Suspense>
    </div>
  );
}
```

- [ ] **Step 5: Run the test again**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/__tests__/UserShellClient.flag.test.tsx`
Expected: PASS — 2 tests passing

- [ ] **Step 6: Run the full UserShell test suite for regressions**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell`
Expected: all existing tests still pass

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/layout/UserShell/UserShellClient.tsx apps/web/src/components/layout/UserShell/__tests__/UserShellClient.flag.test.tsx
git commit -m "feat(web): wire DesktopShell into UserShellClient behind feature flag"
```

---

## Task 16: Manual smoke test of the new shell

**Files:** none (manual QA)

- [ ] **Step 1: Start dev server with the flag on**

Run in Git Bash (NOT PowerShell):

```bash
cd D:/Repositories/meepleai-monorepo-dev/apps/web
NEXT_PUBLIC_UX_REDESIGN=true pnpm dev
```

Expected: dev server starts on `http://localhost:3000` without errors

- [ ] **Step 2: Visit the homepage**

Open `http://localhost:3000/` in browser.
Expected:
- New 64px top bar visible: MeepleAi logo + Home/Libreria/Sessioni links + search pill + chat button + notification + user menu
- No mini-nav visible (no page registered one yet)
- Empty hand rail on the left (76px, hand label vertical, no cards since store is empty)
- Main content area renders the current dashboard/landing

- [ ] **Step 3: Navigate to /library**

Expected:
- Libreria link in top bar is highlighted (orange tint + aria-current=page)
- Main area shows the current library page (unchanged)

- [ ] **Step 4: Click the chat button**

Expected: console warn message: `[TopBarChatButton] onOpen handler not provided — chat panel not wired yet (Phase 4)`. No crash.

- [ ] **Step 5: Disable the flag and verify rollback**

Stop dev server. Restart without the flag:

```bash
pnpm dev
```

Expected: legacy `AppNavbar` layout restored, no visual regression from pre-task state.

- [ ] **Step 6: Commit any findings**

If issues are found during manual smoke, file follow-up tasks in the plan before continuing. If all green, document the smoke test result in the next commit message.

```bash
# No code changes — just a marker commit with empty allow if needed, OR skip this step
git commit --allow-empty -m "chore(web): phase 1 shell manual smoke test passed"
```

---

## Task 17: Typecheck + lint + full test sweep

**Files:** none

- [ ] **Step 1: Typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: no errors

- [ ] **Step 2: Lint**

Run: `cd apps/web && pnpm lint`
Expected: no errors (warnings OK)

- [ ] **Step 3: Full unit test suite**

Run: `cd apps/web && pnpm test`
Expected: all tests pass, new tests included in count

- [ ] **Step 4: If anything fails**

Fix issues inline. Do NOT mark Phase 1 complete until all green.

- [ ] **Step 5: Final Phase 1 commit marker**

```bash
git commit --allow-empty -m "chore(web): phase 1 shell foundation complete

New desktop shell (TopBar64 + MiniNavSlot + DesktopHandRail) behind
NEXT_PUBLIC_UX_REDESIGN flag. All unit tests passing. Legacy shell
unchanged. Phase 2 (Dashboard restyle) to follow in a separate plan."
```

---

## Self-Review Checklist (run before marking Phase 1 complete)

- [ ] All 17 tasks marked complete with green tests
- [ ] `apps/web/src/lib/feature-flags.ts` exists and is imported in `UserShellClient`
- [ ] `.env.development.example` has the `NEXT_PUBLIC_UX_REDESIGN=false` line
- [ ] No `TODO` or `FIXME` comments left in new files
- [ ] All new components have at least one unit test
- [ ] `pnpm typecheck`, `pnpm lint`, `pnpm test` all pass
- [ ] Manual smoke test passed with flag on AND off
- [ ] Spec §4 (Shell Architecture) requirements covered:
  - [x] Top bar 64px with logo / 3 links / search / chat icon / notifications / avatar
  - [x] Mini-nav slot 48px driven by a store
  - [x] Hand rail 76px reading from `useCardHand`
  - [x] Feature flag for rollback

---

## Next Phases (separate plans, NOT in this plan)

Each subsequent phase will get its own plan file following the same TDD pattern:

| Phase | Scope | Plan file (TBD) |
|---|---|---|
| Phase 2 | Dashboard Gaming Hub restyle (widgets → MeepleCard variants) | `2026-XX-XX-desktop-ux-redesign-phase-2-dashboard.md` |
| Phase 3 | Library Hub (carousels, header, filter bar, wire tab routing) | `2026-XX-XX-desktop-ux-redesign-phase-3-library.md` |
| Phase 4 | Chat slide-over panel (panel, sidebar, ctx switcher, URL state) | `2026-XX-XX-desktop-ux-redesign-phase-4-chat.md` |
| Phase 5 | Cleanup (remove flag, delete legacy, add Playwright E2E, redirects) | `2026-XX-XX-desktop-ux-redesign-phase-5-cleanup.md` |

Each phase depends on the previous. Phase 2+ must not start until Phase 1 ships and smoke-test passes.

---

## References

- Spec: `docs/superpowers/specs/2026-04-08-desktop-ux-redesign-design.md`
- Mockups: `.superpowers/brainstorm/598863-1775669036/content/*.html` (shell-overview, dashboard-gaming-hub, library-hub, chat-panel)
- Design tokens: `apps/web/src/styles/design-tokens.css`
- Existing shell: `apps/web/src/components/layout/UserShell/`
- Existing hand store: `apps/web/src/stores/use-card-hand.ts`
- Existing mini-nav: `apps/web/src/components/layout/ContextMiniNav.tsx`
