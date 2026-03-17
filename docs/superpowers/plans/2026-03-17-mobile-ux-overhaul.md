# Mobile UX Overhaul Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce mobile navigation chrome from 288px (43%) to ~64px (10%) by consolidating 6 navigation layers into a unified TabBar with morphing center tab and contextual bottom sheet.

**Architecture:** Sticky TopNav hides on scroll-down via transform+negative-margin (preserving flex shell). ContextualBottomNav, HandDrawer, MobileBreadcrumb, and SmartFAB are removed on mobile. Their functionality moves into a new ContextualBottomSheet triggered by the TabBar's morphing center tab or swipe-up gesture.

**Tech Stack:** Next.js 16, React 19, Tailwind 4, framer-motion 12, Zustand, Vitest, Playwright

**Spec:** `docs/superpowers/specs/2026-03-17-mobile-ux-overhaul-design.md`

---

## File Structure

### New Files
| File | Responsibility |
|------|---------------|
| `apps/web/src/hooks/useScrollHideNav.ts` | Scroll-direction detection on `<main>` container, returns `isNavVisible` |
| `apps/web/src/hooks/__tests__/useScrollHideNav.test.ts` | Unit tests for scroll hide/reveal logic |
| `apps/web/src/hooks/useContextualEntity.ts` | Derives entity type/title/icon/color from route + card hand |
| `apps/web/src/hooks/__tests__/useContextualEntity.test.ts` | Unit tests for entity detection per route |
| `apps/web/src/hooks/useContextualSheetActions.ts` | Route-based action definitions for bottom sheet |
| `apps/web/src/hooks/__tests__/useContextualSheetActions.test.ts` | Unit tests for action mapping |
| `apps/web/src/components/layout/ContextualBottomSheet/ContextualBottomSheet.tsx` | Bottom sheet overlay with cards + actions grid |
| `apps/web/src/components/layout/ContextualBottomSheet/index.ts` | Barrel export |
| `apps/web/src/components/layout/ContextualBottomSheet/__tests__/ContextualBottomSheet.test.tsx` | Unit tests |

### Modified Files
| File | Change |
|------|--------|
| `apps/web/src/components/layout/UnifiedShell/UnifiedTopNav.tsx` | Add `isNavVisible` prop, transform+margin classes, hide center title on mobile |
| `apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx` | Add `mainRef`, integrate `useScrollHideNav`, pass `isNavVisible` to TopNav, render ContextualBottomSheet, hide HandDrawer + ContextualBottomNav on mobile |
| `apps/web/src/components/layout/MobileTabBar/MobileTabBar.tsx` | Morphing center tab, swipe indicator, guest handling, bottom sheet trigger |
| `apps/web/src/components/layout/UnifiedShell/HandDrawer.tsx` | Add `hidden md:flex` to hide on mobile |
| `apps/web/src/components/layout/UnifiedShell/ContextualBottomNav.tsx` | Add `hidden md:flex` to hide on mobile |

### Deleted Files
| File | Reason |
|------|--------|
| `apps/web/src/components/layout/SmartFAB/` (entire directory) | Replaced by morphing center tab |
| `apps/web/src/components/layout/MobileBreadcrumb/` (entire directory) | Replaced by inline breadcrumb in content |

### Important Route Note
The actual filesystem route is `/sessions/[id]` (plural), matching `apps/web/src/app/(authenticated)/sessions/[id]/`. The existing `useRouteContext.ts` uses singular `/session/` — this is a pre-existing inconsistency. All new hooks use the correct plural `/sessions/` form.

---

## Chunk 1: New Hooks (Phase 1)

### Task 1: `useScrollHideNav` hook

**Files:**
- Create: `apps/web/src/hooks/useScrollHideNav.ts`
- Create: `apps/web/src/hooks/__tests__/useScrollHideNav.test.ts`
- Reference: `apps/web/src/hooks/useScrollDirection.ts` (existing pattern)

- [ ] **Step 1: Write the test file**

```typescript
// apps/web/src/hooks/__tests__/useScrollHideNav.test.ts
import { renderHook, act } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';

import { useScrollHideNav } from '../useScrollHideNav';

describe('useScrollHideNav', () => {
  let mockElement: HTMLDivElement;
  const listeners: Map<string, EventListener> = new Map();

  beforeEach(() => {
    mockElement = document.createElement('div');
    // Mock scrollTop property
    let _scrollTop = 0;
    Object.defineProperty(mockElement, 'scrollTop', {
      get: () => _scrollTop,
      set: (v: number) => { _scrollTop = v; },
      configurable: true,
    });

    vi.spyOn(mockElement, 'addEventListener').mockImplementation((event, handler) => {
      listeners.set(event as string, handler as EventListener);
    });
    vi.spyOn(mockElement, 'removeEventListener').mockImplementation((event) => {
      listeners.delete(event as string);
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    listeners.clear();
  });

  function simulateScroll(newScrollTop: number) {
    mockElement.scrollTop = newScrollTop;
    const handler = listeners.get('scroll');
    if (handler) {
      act(() => { handler(new Event('scroll')); });
    }
  }

  it('returns isNavVisible=true initially', () => {
    const ref = { current: mockElement };
    const { result } = renderHook(() => useScrollHideNav({ scrollContainerRef: ref }));
    expect(result.current.isNavVisible).toBe(true);
    expect(result.current.scrollDirection).toBeNull();
  });

  it('hides nav on scroll down past threshold', () => {
    const ref = { current: mockElement };
    const { result } = renderHook(() => useScrollHideNav({ scrollContainerRef: ref, threshold: 10 }));

    simulateScroll(20);
    expect(result.current.isNavVisible).toBe(false);
    expect(result.current.scrollDirection).toBe('down');
  });

  it('shows nav on scroll up past threshold', () => {
    const ref = { current: mockElement };
    const { result } = renderHook(() => useScrollHideNav({ scrollContainerRef: ref, threshold: 10 }));

    simulateScroll(100); // scroll down
    simulateScroll(80);  // scroll up
    expect(result.current.isNavVisible).toBe(true);
    expect(result.current.scrollDirection).toBe('up');
  });

  it('ignores scroll below threshold', () => {
    const ref = { current: mockElement };
    const { result } = renderHook(() => useScrollHideNav({ scrollContainerRef: ref, threshold: 10 }));

    simulateScroll(5); // below threshold
    expect(result.current.isNavVisible).toBe(true);
    expect(result.current.scrollDirection).toBeNull();
  });

  it('respects disabled option', () => {
    const ref = { current: mockElement };
    const { result } = renderHook(() =>
      useScrollHideNav({ scrollContainerRef: ref, disabled: true })
    );

    simulateScroll(100);
    expect(result.current.isNavVisible).toBe(true);
  });

  it('attaches listener to scrollContainerRef, not window', () => {
    const windowSpy = vi.spyOn(window, 'addEventListener');
    const ref = { current: mockElement };
    renderHook(() => useScrollHideNav({ scrollContainerRef: ref }));

    expect(mockElement.addEventListener).toHaveBeenCalledWith('scroll', expect.any(Function), { passive: true });
    expect(windowSpy).not.toHaveBeenCalledWith('scroll', expect.any(Function), expect.anything());
    windowSpy.mockRestore();
  });

  it('cleans up listener on unmount', () => {
    const ref = { current: mockElement };
    const { unmount } = renderHook(() => useScrollHideNav({ scrollContainerRef: ref }));

    unmount();
    expect(mockElement.removeEventListener).toHaveBeenCalledWith('scroll', expect.any(Function));
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/useScrollHideNav.test.ts`
Expected: FAIL — module `../useScrollHideNav` not found

- [ ] **Step 3: Write the implementation**

```typescript
// apps/web/src/hooks/useScrollHideNav.ts
/**
 * useScrollHideNav - Hide/reveal nav based on scroll direction in a container element.
 *
 * Unlike useScrollDirection (which listens to window.scrollY), this hook
 * attaches to a specific scroll container ref (e.g., <main>).
 *
 * @see docs/superpowers/specs/2026-03-17-mobile-ux-overhaul-design.md
 */

'use client';

import { useState, useEffect, useRef, useCallback, type RefObject } from 'react';

export type ScrollDirection = 'up' | 'down' | null;

interface UseScrollHideNavOptions {
  /** The scrollable container element ref (NOT window) */
  scrollContainerRef: RefObject<HTMLElement | null>;
  /** Minimum scroll delta (px) before direction change registers. Default: 10 */
  threshold?: number;
  /** Disable the hook (nav stays visible). Default: false */
  disabled?: boolean;
}

interface UseScrollHideNavReturn {
  isNavVisible: boolean;
  scrollDirection: ScrollDirection;
}

export function useScrollHideNav({
  scrollContainerRef,
  threshold = 10,
  disabled = false,
}: UseScrollHideNavOptions): UseScrollHideNavReturn {
  const [isNavVisible, setIsNavVisible] = useState(true);
  const [scrollDirection, setScrollDirection] = useState<ScrollDirection>(null);
  const lastScrollTop = useRef(0);

  const handleScroll = useCallback(() => {
    const el = scrollContainerRef.current;
    if (!el) return;

    const currentScrollTop = el.scrollTop;
    const diff = currentScrollTop - lastScrollTop.current;

    if (Math.abs(diff) < threshold) return;

    const newDirection: ScrollDirection = diff > 0 ? 'down' : 'up';
    setScrollDirection(newDirection);
    setIsNavVisible(newDirection === 'up');

    lastScrollTop.current = currentScrollTop;
  }, [scrollContainerRef, threshold]);

  useEffect(() => {
    if (disabled) return;

    const el = scrollContainerRef.current;
    if (!el) return;

    lastScrollTop.current = el.scrollTop;
    el.addEventListener('scroll', handleScroll, { passive: true });
    return () => el.removeEventListener('scroll', handleScroll);
  }, [scrollContainerRef, handleScroll, disabled]);

  if (disabled) {
    return { isNavVisible: true, scrollDirection: null };
  }

  return { isNavVisible, scrollDirection };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/useScrollHideNav.test.ts`
Expected: All 7 tests PASS

- [ ] **Step 5: Commit**

```bash
cd apps/web
git add src/hooks/useScrollHideNav.ts src/hooks/__tests__/useScrollHideNav.test.ts
git commit -m "feat(mobile-ux): add useScrollHideNav hook for container-based scroll detection"
```

---

### Task 2: `useContextualEntity` hook

**Files:**
- Create: `apps/web/src/hooks/useContextualEntity.ts`
- Create: `apps/web/src/hooks/__tests__/useContextualEntity.test.ts`
- Reference: `apps/web/src/hooks/useRouteContext.ts` (route patterns)
- Reference: `apps/web/src/stores/use-card-hand.ts` (card hand state)
- Reference: `apps/web/src/components/ui/data-display/meeple-card-features/navigation-icons.tsx` (entity icons)
- Reference: `apps/web/src/components/ui/data-display/meeple-card-styles.ts` (entity colors)

- [ ] **Step 1: Write the test file**

```typescript
// apps/web/src/hooks/__tests__/useContextualEntity.test.ts
import { renderHook } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
}));

// Mock card hand store
vi.mock('@/stores/use-card-hand', () => ({
  useCardHand: vi.fn(),
}));

import { usePathname } from 'next/navigation';
import { useCardHand } from '@/stores/use-card-hand';
import { useContextualEntity } from '../useContextualEntity';

const mockUsePathname = vi.mocked(usePathname);
const mockUseCardHand = vi.mocked(useCardHand);

describe('useContextualEntity', () => {
  beforeEach(() => {
    mockUseCardHand.mockReturnValue({
      cards: [],
      focusedIdx: -1,
    } as ReturnType<typeof useCardHand>);
  });

  it('returns null on dashboard (no context)', () => {
    mockUsePathname.mockReturnValue('/dashboard');
    const { result } = renderHook(() => useContextualEntity());
    expect(result.current).toBeNull();
  });

  it('returns game entity on game detail page', () => {
    mockUsePathname.mockReturnValue('/library/games/catan-123');
    mockUseCardHand.mockReturnValue({
      cards: [{ id: 'catan-123', entity: 'game', title: 'Catan', href: '/library/games/catan-123' }],
      focusedIdx: 0,
    } as ReturnType<typeof useCardHand>);

    const { result } = renderHook(() => useContextualEntity());
    expect(result.current).toEqual(expect.objectContaining({
      type: 'game',
      id: 'catan-123',
      title: 'Catan',
      color: '25 95% 45%',
    }));
    expect(result.current?.icon).toBeDefined();
  });

  it('returns session entity on session page', () => {
    mockUsePathname.mockReturnValue('/sessions/abc-456');
    mockUseCardHand.mockReturnValue({
      cards: [{ id: 'abc-456', entity: 'session', title: 'Game Night', href: '/sessions/abc-456' }],
      focusedIdx: 0,
    } as ReturnType<typeof useCardHand>);

    const { result } = renderHook(() => useContextualEntity());
    expect(result.current).toEqual(expect.objectContaining({
      type: 'session',
      title: 'Game Night',
    }));
  });

  it('returns chat entity on chat page', () => {
    mockUsePathname.mockReturnValue('/chat/conv-789');
    const { result } = renderHook(() => useContextualEntity());
    expect(result.current).toEqual(expect.objectContaining({
      type: 'chatSession',
    }));
  });

  it('returns null on library list page', () => {
    mockUsePathname.mockReturnValue('/library');
    const { result } = renderHook(() => useContextualEntity());
    expect(result.current).toBeNull();
  });

  it('returns null on games catalog', () => {
    mockUsePathname.mockReturnValue('/games');
    const { result } = renderHook(() => useContextualEntity());
    expect(result.current).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/useContextualEntity.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Write the implementation**

```typescript
// apps/web/src/hooks/useContextualEntity.ts
/**
 * useContextualEntity - Derives the current contextual entity from route + card hand.
 *
 * Returns entity info (type, title, icon, color) when the user is on a
 * detail page (game, session, chat). Returns null on list/dashboard pages.
 *
 * @see docs/superpowers/specs/2026-03-17-mobile-ux-overhaul-design.md
 */

'use client';

import { useMemo } from 'react';
import { usePathname } from 'next/navigation';

import { ENTITY_NAV_ICONS } from '@/components/ui/data-display/meeple-card-features/navigation-icons';
import { entityColors } from '@/components/ui/data-display/meeple-card-styles';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import { useCardHand } from '@/stores/use-card-hand';

export interface ContextualEntity {
  type: MeepleEntityType;
  id: string | null;
  title: string;
  icon: React.ComponentType<{ className?: string }>;
  color: string; // HSL value
}

/** Route patterns that have a contextual entity */
const ENTITY_ROUTE_PATTERNS: Array<{
  pattern: RegExp;
  entityType: MeepleEntityType;
  extractId: (pathname: string) => string | null;
}> = [
  {
    pattern: /^\/library\/games\/([^/]+)$/,
    entityType: 'game',
    extractId: (p) => p.match(/^\/library\/games\/([^/]+)$/)?.[1] ?? null,
  },
  {
    pattern: /^\/games\/([^/]+)$/,
    entityType: 'game',
    extractId: (p) => p.match(/^\/games\/([^/]+)$/)?.[1] ?? null,
  },
  {
    pattern: /^\/sessions\/([^/]+)/,
    entityType: 'session',
    extractId: (p) => p.match(/^\/sessions\/([^/]+)/)?.[1] ?? null,
  },
  {
    pattern: /^\/chat\/([^/]+)$/,
    entityType: 'chatSession',
    extractId: (p) => {
      const id = p.match(/^\/chat\/([^/]+)$/)?.[1];
      return id === 'new' ? null : id ?? null;
    },
  },
];

export function useContextualEntity(): ContextualEntity | null {
  const pathname = usePathname();
  const { cards, focusedIdx } = useCardHand();
  const focusedCard = focusedIdx >= 0 && focusedIdx < cards.length ? cards[focusedIdx] : null;

  return useMemo(() => {
    if (!pathname) return null;

    for (const { pattern, entityType, extractId } of ENTITY_ROUTE_PATTERNS) {
      if (pattern.test(pathname)) {
        const id = extractId(pathname);
        if (!id) return null; // e.g., /chat/new

        // Try to get title from focused card
        const title = focusedCard?.id === id
          ? focusedCard.title
          : cards.find(c => c.id === id)?.title ?? entityType;

        return {
          type: entityType,
          id,
          title,
          icon: ENTITY_NAV_ICONS[entityType] ?? ENTITY_NAV_ICONS.game,
          color: entityColors[entityType]?.hsl ?? '220 70% 50%',
        };
      }
    }

    return null;
  }, [pathname, focusedCard, cards]);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/useContextualEntity.test.ts`
Expected: All 6 tests PASS

- [ ] **Step 5: Commit**

```bash
cd apps/web
git add src/hooks/useContextualEntity.ts src/hooks/__tests__/useContextualEntity.test.ts
git commit -m "feat(mobile-ux): add useContextualEntity hook for route-based entity detection"
```

---

### Task 3: `useContextualSheetActions` hook

**Files:**
- Create: `apps/web/src/hooks/useContextualSheetActions.ts`
- Create: `apps/web/src/hooks/__tests__/useContextualSheetActions.test.ts`
- Reference: `apps/web/src/hooks/useRouteContext.ts` (route context detection)
- Reference: `apps/web/src/types/layout.ts:20-39` (LayoutContext type)

- [ ] **Step 1: Write the test file**

```typescript
// apps/web/src/hooks/__tests__/useContextualSheetActions.test.ts
import { renderHook } from '@testing-library/react';
import { vi, describe, it, expect } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/components/layout/LayoutProvider', () => ({
  useLayout: vi.fn(() => ({ context: 'default', setContext: vi.fn() })),
}));

import { usePathname } from 'next/navigation';
import { useContextualSheetActions } from '../useContextualSheetActions';

const mockUsePathname = vi.mocked(usePathname);

describe('useContextualSheetActions', () => {
  it('returns game actions on game detail page', () => {
    mockUsePathname.mockReturnValue('/library/games/catan-123');
    const { result } = renderHook(() => useContextualSheetActions());

    expect(result.current.length).toBeGreaterThan(0);
    const ids = result.current.map(a => a.id);
    expect(ids).toContain('faq');
    expect(ids).toContain('rules');
    expect(ids).toContain('reviews');
    expect(ids).toContain('strategy');
    expect(ids).toContain('new-session');
  });

  it('returns session actions on session page', () => {
    mockUsePathname.mockReturnValue('/sessions/abc-123');
    const { result } = renderHook(() => useContextualSheetActions());

    const ids = result.current.map(a => a.id);
    expect(ids).toContain('add-note');
    expect(ids).toContain('scoreboard');
  });

  it('returns dashboard actions on /dashboard', () => {
    mockUsePathname.mockReturnValue('/dashboard');
    const { result } = renderHook(() => useContextualSheetActions());
    const ids = result.current.map(a => a.id);
    expect(ids).toContain('quick-start');
    expect(ids).toContain('recent-games');
  });

  it('returns dashboard actions on root', () => {
    mockUsePathname.mockReturnValue('/');
    const { result } = renderHook(() => useContextualSheetActions());
    const ids = result.current.map(a => a.id);
    expect(ids).toContain('quick-start');
  });

  it('returns chat actions with settings', () => {
    mockUsePathname.mockReturnValue('/chat/conv-789');
    const { result } = renderHook(() => useContextualSheetActions());
    const ids = result.current.map(a => a.id);
    expect(ids).toContain('new-topic');
    expect(ids).toContain('history');
    expect(ids).toContain('settings');
  });

  it('returns library actions on library page', () => {
    mockUsePathname.mockReturnValue('/library');
    const { result } = renderHook(() => useContextualSheetActions());
    const ids = result.current.map(a => a.id);
    expect(ids).toContain('add-game');
  });

  it('each action has required fields', () => {
    mockUsePathname.mockReturnValue('/library/games/catan-123');
    const { result } = renderHook(() => useContextualSheetActions());

    for (const action of result.current) {
      expect(action).toHaveProperty('id');
      expect(action).toHaveProperty('label');
      expect(action).toHaveProperty('icon');
      expect(action).toHaveProperty('onClick');
    }
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/useContextualSheetActions.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Write the implementation**

```typescript
// apps/web/src/hooks/useContextualSheetActions.ts
/**
 * useContextualSheetActions - Route-based action definitions for ContextualBottomSheet.
 *
 * NOT the same as useBottomNavActions (which only handles session quick actions).
 * This hook provides a full action map based on the current route context.
 *
 * @see docs/superpowers/specs/2026-03-17-mobile-ux-overhaul-design.md
 */

'use client';

import { useMemo } from 'react';
import { usePathname, useRouter } from 'next/navigation';
import type { LucideIcon } from 'lucide-react';
import {
  HelpCircle, BookOpen, MessageSquare, Target, Gamepad2, Bot,
  StickyNote, Trophy, Square, Share2,
  Plus, Upload, Filter,
  Zap, Clock,
} from 'lucide-react';

export interface SheetAction {
  id: string;
  label: string;
  icon: LucideIcon;
  onClick: () => void;
  variant?: 'primary' | 'secondary';
}

export function useContextualSheetActions(): SheetAction[] {
  const pathname = usePathname();
  const router = useRouter();

  return useMemo(() => {
    if (!pathname) return [];

    // Game detail: /library/games/[id] or /games/[id]
    const gameMatch = pathname.match(/^\/(library\/)?games\/([^/]+)$/);
    if (gameMatch) {
      const gameId = gameMatch[2];
      return [
        { id: 'faq', label: 'FAQ', icon: HelpCircle, onClick: () => router.push(`${pathname}?tab=faq`) },
        { id: 'rules', label: 'Rules', icon: BookOpen, onClick: () => router.push(`${pathname}?tab=rules`) },
        { id: 'reviews', label: 'Reviews', icon: MessageSquare, onClick: () => router.push(`${pathname}?tab=reviews`) },
        { id: 'strategy', label: 'Strategy', icon: Target, onClick: () => router.push(`${pathname}?tab=strategy`) },
        { id: 'ask-ai', label: 'Ask AI', icon: Bot, onClick: () => router.push(`/chat/new?game=${gameId}`) },
        { id: 'new-session', label: 'New Session', icon: Gamepad2, onClick: () => router.push(`/sessions/new?game=${gameId}`), variant: 'primary' as const },
      ];
    }

    // Session: /session/[id]
    if (/^\/sessions\/[^/]+/.test(pathname)) {
      return [
        { id: 'add-note', label: 'Add Note', icon: StickyNote, onClick: () => {} },
        { id: 'scoreboard', label: 'Scoreboard', icon: Trophy, onClick: () => {} },
        { id: 'end-session', label: 'End Session', icon: Square, onClick: () => {} },
        { id: 'share', label: 'Share', icon: Share2, onClick: () => {} },
      ];
    }

    // Chat: /chat/[id]
    if (/^\/chat\/[^/]+$/.test(pathname) && !pathname.endsWith('/new')) {
      return [
        { id: 'new-topic', label: 'New Topic', icon: Plus, onClick: () => router.push('/chat/new') },
        { id: 'history', label: 'History', icon: Clock, onClick: () => {} },
        { id: 'settings', label: 'Settings', icon: Filter, onClick: () => router.push('/settings') },
      ];
    }

    // Library list
    if (pathname === '/library') {
      return [
        { id: 'add-game', label: 'Add Game', icon: Plus, onClick: () => {}, variant: 'primary' as const },
        { id: 'import', label: 'Import', icon: Upload, onClick: () => {} },
        { id: 'filter', label: 'Filter', icon: Filter, onClick: () => {} },
      ];
    }

    // Dashboard
    if (pathname === '/dashboard' || pathname === '/') {
      return [
        { id: 'quick-start', label: 'Quick Start', icon: Zap, onClick: () => router.push('/games'), variant: 'primary' as const },
        { id: 'recent-games', label: 'Recent Games', icon: Clock, onClick: () => router.push('/library') },
      ];
    }

    // Catalog, default: no actions
    return [];
  }, [pathname, router]);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/useContextualSheetActions.test.ts`
Expected: All 6 tests PASS

- [ ] **Step 5: Commit**

```bash
cd apps/web
git add src/hooks/useContextualSheetActions.ts src/hooks/__tests__/useContextualSheetActions.test.ts
git commit -m "feat(mobile-ux): add useContextualSheetActions hook for route-based bottom sheet actions"
```

---

## Chunk 2: ContextualBottomSheet Component (Phase 1)

### Task 4: ContextualBottomSheet component

**Files:**
- Create: `apps/web/src/components/layout/ContextualBottomSheet/ContextualBottomSheet.tsx`
- Create: `apps/web/src/components/layout/ContextualBottomSheet/index.ts`
- Create: `apps/web/src/components/layout/ContextualBottomSheet/__tests__/ContextualBottomSheet.test.tsx`
- Reference: `apps/web/src/stores/use-card-hand.ts` (card hand for open cards)
- Reference: `apps/web/src/hooks/useContextualSheetActions.ts` (actions)
- Reference: `apps/web/src/hooks/useContextualEntity.ts` (entity header)

- [ ] **Step 1: Write the test file**

```typescript
// apps/web/src/components/layout/ContextualBottomSheet/__tests__/ContextualBottomSheet.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(() => '/library/games/catan-123'),
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/components/layout/LayoutProvider', () => ({
  useLayout: vi.fn(() => ({ context: 'game_detail', setContext: vi.fn() })),
}));

vi.mock('@/stores/use-card-hand', () => ({
  useCardHand: vi.fn(() => ({
    cards: [
      { id: 'catan-123', entity: 'game', title: 'Catan', href: '/library/games/catan-123' },
      { id: 'risk-456', entity: 'game', title: 'Risk', href: '/library/games/risk-456' },
    ],
    focusedIdx: 0,
  })),
}));

vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => {
      const { initial, animate, exit, transition, drag, dragConstraints, onDragEnd, ...rest } = props;
      return <div {...rest}>{children}</div>;
    },
  },
  AnimatePresence: ({ children }: React.PropsWithChildren) => <>{children}</>,
}));

import { ContextualBottomSheet } from '../ContextualBottomSheet';

describe('ContextualBottomSheet', () => {
  it('renders nothing when closed', () => {
    render(<ContextualBottomSheet isOpen={false} onClose={() => {}} />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders dialog when open', () => {
    render(<ContextualBottomSheet isOpen={true} onClose={() => {}} />);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('shows entity header with title', () => {
    render(<ContextualBottomSheet isOpen={true} onClose={() => {}} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('shows open cards section', () => {
    render(<ContextualBottomSheet isOpen={true} onClose={() => {}} />);
    expect(screen.getByText('Risk')).toBeInTheDocument();
  });

  it('shows contextual actions', () => {
    render(<ContextualBottomSheet isOpen={true} onClose={() => {}} />);
    expect(screen.getByText('FAQ')).toBeInTheDocument();
    expect(screen.getByText('Rules')).toBeInTheDocument();
  });

  it('calls onClose when backdrop clicked', () => {
    const onClose = vi.fn();
    render(<ContextualBottomSheet isOpen={true} onClose={onClose} />);
    fireEvent.click(screen.getByTestId('bottom-sheet-backdrop'));
    expect(onClose).toHaveBeenCalled();
  });

  it('has correct a11y attributes', () => {
    render(<ContextualBottomSheet isOpen={true} onClose={() => {}} />);
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/ContextualBottomSheet/__tests__/ContextualBottomSheet.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Install focus-trap-react (if not already present)**

Run: `cd apps/web && pnpm add focus-trap-react`
This provides WCAG-compliant focus trapping inside the bottom sheet dialog.

- [ ] **Step 5: Write the barrel export**

```typescript
// apps/web/src/components/layout/ContextualBottomSheet/index.ts
export { ContextualBottomSheet } from './ContextualBottomSheet';
```

- [ ] **Step 6: Write the component**

```tsx
// apps/web/src/components/layout/ContextualBottomSheet/ContextualBottomSheet.tsx
/**
 * ContextualBottomSheet - Bottom sheet overlay with entity cards + contextual actions.
 *
 * Triggered by morphed center tab tap or swipe-up from TabBar.
 * Replaces ContextualBottomNav, HandDrawer, and SmartFAB on mobile.
 *
 * @see docs/superpowers/specs/2026-03-17-mobile-ux-overhaul-design.md
 */

'use client';

import { useEffect, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import FocusTrap from 'focus-trap-react';
import Link from 'next/link';

import { ENTITY_NAV_ICONS } from '@/components/ui/data-display/meeple-card-features/navigation-icons';
import { entityColors } from '@/components/ui/data-display/meeple-card-styles';
import { cn } from '@/lib/utils';
import { useCardHand } from '@/stores/use-card-hand';
import { useContextualEntity } from '@/hooks/useContextualEntity';
import { useContextualSheetActions, type SheetAction } from '@/hooks/useContextualSheetActions';

interface ContextualBottomSheetProps {
  isOpen: boolean;
  onClose: () => void;
}

export function ContextualBottomSheet({ isOpen, onClose }: ContextualBottomSheetProps) {
  const entity = useContextualEntity();
  const actions = useContextualSheetActions();
  const { cards } = useCardHand();
  const sheetRef = useRef<HTMLDivElement>(null);

  // Focus trap: close on Escape
  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  // Focus the sheet when opened
  useEffect(() => {
    if (isOpen && sheetRef.current) {
      sheetRef.current.focus();
    }
  }, [isOpen]);

  const hasCards = cards.length > 0;
  const hasActions = actions.length > 0;

  return (
    <AnimatePresence>
      {isOpen && (
        <>
          {/* Backdrop */}
          <motion.div
            data-testid="bottom-sheet-backdrop"
            className="fixed inset-0 z-50 bg-black/50"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            onClick={onClose}
          />

          {/* Sheet with focus trap */}
          <FocusTrap active={isOpen} focusTrapOptions={{ escapeDeactivates: true, onDeactivate: onClose, allowOutsideClick: true }}>
          <motion.div
            ref={sheetRef}
            role="dialog"
            aria-modal="true"
            aria-label={entity ? `${entity.title} actions` : 'Quick actions'}
            tabIndex={-1}
            className={cn(
              'fixed bottom-0 left-0 right-0 z-50',
              'bg-card/95 backdrop-blur-xl rounded-t-2xl',
              'border-t border-border/40',
              'max-h-[80vh] overflow-y-auto',
              'pb-[env(safe-area-inset-bottom)]',
              'focus:outline-none'
            )}
            initial={{ y: '100%' }}
            animate={{ y: 0 }}
            exit={{ y: '100%' }}
            transition={{ type: 'spring', damping: 25, stiffness: 300 }}
            drag="y"
            dragConstraints={{ top: 0 }}
            onDragEnd={(_, info) => {
              if (info.offset.y > 100) onClose();
            }}
          >
            {/* Drag handle */}
            <div className="flex justify-center pt-3 pb-2">
              <div className="w-10 h-1 bg-muted-foreground/20 rounded-full" />
            </div>

            {/* Entity header */}
            {entity && (
              <div className="px-5 pb-3 border-b border-border/30">
                <div className="flex items-center gap-3">
                  <div
                    className="flex items-center justify-center w-10 h-10 rounded-xl"
                    style={{ background: `hsl(${entity.color} / 0.12)` }}
                  >
                    <entity.icon
                      className="w-5 h-5"
                      style={{ color: `hsl(${entity.color})` } as React.CSSProperties}
                    />
                  </div>
                  <div>
                    <div className="text-sm font-semibold font-quicksand">{entity.title}</div>
                    <div className="text-xs text-muted-foreground">Quick actions</div>
                  </div>
                </div>
              </div>
            )}

            {/* Open cards carousel */}
            {hasCards && (
              <div className="px-5 py-3 border-b border-border/30">
                <div className="text-xs font-medium text-muted-foreground mb-2">
                  Open Cards ({cards.length})
                </div>
                <div className="flex gap-2 overflow-x-auto pb-1">
                  {cards.map((card) => {
                    const CardIcon = ENTITY_NAV_ICONS[card.entity] ?? ENTITY_NAV_ICONS.game;
                    const cardColor = entityColors[card.entity]?.hsl ?? '220 70% 50%';
                    return (
                      <Link
                        key={card.id}
                        href={card.href}
                        onClick={onClose}
                        className={cn(
                          'flex items-center gap-2 px-3 py-2 rounded-lg shrink-0',
                          'border transition-colors',
                          'hover:bg-muted/50'
                        )}
                        style={{
                          borderColor: `hsl(${cardColor} / 0.25)`,
                          background: `hsl(${cardColor} / 0.06)`,
                        }}
                      >
                        <CardIcon className="w-3.5 h-3.5" style={{ color: `hsl(${cardColor})` } as React.CSSProperties} />
                        <span className="text-xs font-medium truncate max-w-[100px]">{card.title}</span>
                      </Link>
                    );
                  })}
                </div>
              </div>
            )}

            {/* Actions grid */}
            {hasActions && (
              <div className="px-5 py-4">
                <div className="grid grid-cols-2 gap-2.5">
                  {actions.map((action) => (
                    <ActionButton
                      key={action.id}
                      action={action}
                      onClose={onClose}
                    />
                  ))}
                </div>
              </div>
            )}
          </motion.div>
          </FocusTrap>
        </>
      )}
    </AnimatePresence>
  );
}

function ActionButton({ action, onClose }: { action: SheetAction; onClose: () => void }) {
  const Icon = action.icon;
  const isPrimary = action.variant === 'primary';

  return (
    <button
      type="button"
      onClick={() => {
        action.onClick();
        onClose();
      }}
      className={cn(
        'flex flex-col items-center gap-1.5 p-4 rounded-xl',
        'border transition-colors',
        'min-h-[72px]',
        isPrimary
          ? 'bg-primary/10 border-primary/25 text-primary col-span-2'
          : 'bg-muted/30 border-border/50 hover:bg-muted/50'
      )}
    >
      <Icon className={cn('w-5 h-5', isPrimary && 'text-primary')} />
      <span className={cn('text-xs font-medium', isPrimary && 'font-semibold')}>{action.label}</span>
    </button>
  );
}
```

- [ ] **Step 7: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/ContextualBottomSheet/__tests__/ContextualBottomSheet.test.tsx`
Expected: All 7 tests PASS

- [ ] **Step 8: Commit**

```bash
cd apps/web
git add src/components/layout/ContextualBottomSheet/ package.json pnpm-lock.yaml
git commit -m "feat(mobile-ux): add ContextualBottomSheet component with cards, actions grid, and focus trap"
```

---

## Chunk 3: Modify Existing Components (Phase 2)

### Task 5: Update UnifiedTopNav with hide/reveal

**Files:**
- Modify: `apps/web/src/components/layout/UnifiedShell/UnifiedTopNav.tsx`
- Reference: `apps/web/src/hooks/useScrollHideNav.ts` (isNavVisible)

- [ ] **Step 1: Read current UnifiedTopNav.tsx**

Run: `cat apps/web/src/components/layout/UnifiedShell/UnifiedTopNav.tsx`
Verify the current structure matches what we explored (sticky top-0 z-40 h-14).

- [ ] **Step 2: Add `isNavVisible` prop and conditional classes**

In `UnifiedTopNav.tsx`, add prop to interface:

```typescript
interface UnifiedTopNavProps {
  isAdmin: boolean;
  userMenu?: React.ReactNode;
  notificationBell?: React.ReactNode;
  searchTrigger?: React.ReactNode;
  miniCards?: React.ReactNode;
  /** Whether nav is visible (false = hidden via scroll-down). Mobile only. */
  isNavVisible?: boolean;
}
```

Update the `<header>` element className — replace the existing `className={cn(...)}` with:

```tsx
className={cn(
  'sticky top-0 z-40 h-14',
  'flex items-center justify-between px-4',
  'bg-background/95 backdrop-blur-xl',
  'border-b border-border/40',
  // Hide/reveal on mobile scroll
  'transition-[transform,margin-top] duration-200 ease-out',
  isNavVisible === false && 'md:transform-none md:mt-0 -translate-y-full -mt-14'
)}
```

Also hide the center title section on mobile by adding `hidden md:flex` to the center div:

```tsx
{/* Center: Focused card title — hidden on mobile (morphed tab handles this) */}
<div className="hidden md:flex items-center gap-2 min-w-0 mx-4">
```

- [ ] **Step 3: Verify build compiles**

Run: `cd apps/web && pnpm build --no-lint 2>&1 | head -20`
Expected: No TypeScript errors related to UnifiedTopNav

- [ ] **Step 4: Commit**

```bash
cd apps/web
git add src/components/layout/UnifiedShell/UnifiedTopNav.tsx
git commit -m "feat(mobile-ux): add hide/reveal behavior to UnifiedTopNav on mobile scroll"
```

---

### Task 6: Update UnifiedShellClient to integrate scroll hook and bottom sheet

**Files:**
- Modify: `apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx`

- [ ] **Step 1: Read current UnifiedShellClient.tsx**

Run: Read the full file to understand current structure and imports.

- [ ] **Step 2: Add imports and mainRef**

Add to imports:
```typescript
import { useRef } from 'react';
import { useScrollHideNav } from '@/hooks/useScrollHideNav';
import { useContextualEntity } from '@/hooks/useContextualEntity';
import { useContextualSheetActions } from '@/hooks/useContextualSheetActions';
import { ContextualBottomSheet } from '@/components/layout/ContextualBottomSheet';
```

Add inside the component function, near the top:
```typescript
const mainRef = useRef<HTMLMainElement>(null);
const { isNavVisible } = useScrollHideNav({ scrollContainerRef: mainRef });
const contextualEntity = useContextualEntity();
const sheetActions = useContextualSheetActions();
const [isBottomSheetOpen, setIsBottomSheetOpen] = useState(false);
const hasSheetContent = sheetActions.length > 0 || cards.length > 0;
```

- [ ] **Step 3: Pass `isNavVisible` to UnifiedTopNav**

Find the `<UnifiedTopNav` JSX and add the prop:
```tsx
<UnifiedTopNav
  isAdmin={isAdmin}
  userMenu={...}
  notificationBell={...}
  searchTrigger={...}
  miniCards={...}
  isNavVisible={isNavVisible}
/>
```

- [ ] **Step 4: Add ref to `<main>` element**

Find `<main id="main-content"` and add the ref:
```tsx
<main
  ref={mainRef}
  id="main-content"
  className="flex-1 overflow-y-auto"
  tabIndex={-1}
>
```

- [ ] **Step 5: Hide HandDrawer and ContextualBottomNav on mobile**

Wrap `<HandDrawer />` render with mobile hide:
```tsx
{/* HandDrawer: hidden on mobile, visible on desktop */}
{!isDesktop && !isAdminContext ? null : <HandDrawer />}
```

Actually, per spec, HandDrawer should show on desktop but hide on mobile. The current condition is `!isDesktop && !isAdminContext`. Change to only show on desktop:
```tsx
{isDesktop && !isAdminContext && <HandDrawer />}
```

For `<ContextualBottomNav />`, wrap with desktop-only:
```tsx
<div className="hidden md:block">
  <ContextualBottomNav />
</div>
```

- [ ] **Step 6: Add ContextualBottomSheet to the JSX**

After the `<MobileTabBar />` (or before the closing `</div>` of the shell), add:
```tsx
<ContextualBottomSheet
  isOpen={isBottomSheetOpen}
  onClose={() => setIsBottomSheetOpen(false)}
/>
```

- [ ] **Step 7: Pass bottom sheet controls to MobileTabBar**

The MobileTabBar needs to know about the entity and sheet trigger. We'll pass them via props in the next task, but for now ensure the state is available. Add to the MobileTabBar render:
```tsx
<MobileTabBar
  contextualEntity={contextualEntity}
  hasSheetContent={hasSheetContent}
  onCenterTabPress={() => setIsBottomSheetOpen(true)}
/>
```

- [ ] **Step 8: Verify build compiles**

Run: `cd apps/web && pnpm build --no-lint 2>&1 | head -20`
Expected: May have type errors for MobileTabBar props (added in next task). That's OK — we'll fix in Task 7.

- [ ] **Step 9: Commit**

```bash
cd apps/web
git add src/components/layout/UnifiedShell/UnifiedShellClient.tsx
git commit -m "feat(mobile-ux): integrate scroll-hide, bottom sheet, and mobile-only visibility in shell"
```

---

### Task 7: Update MobileTabBar with morphing center tab

**Files:**
- Modify: `apps/web/src/components/layout/MobileTabBar/MobileTabBar.tsx`
- Reference: `apps/web/src/hooks/useContextualEntity.ts` (ContextualEntity type)
- Reference: `apps/web/src/components/ui/data-display/meeple-card-features/navigation-icons.tsx` (entity icons)
- Reference: `apps/web/src/components/ui/data-display/meeple-card-styles.ts` (entity colors)

- [ ] **Step 1: Read current MobileTabBar.tsx**

Run: Read the full file (already read above — 167 lines).

- [ ] **Step 2: Add new props interface**

Update the component to accept props:

```typescript
import type { ContextualEntity } from '@/hooks/useContextualEntity';

interface MobileTabBarProps {
  /** Current contextual entity from route (morphs center tab) */
  contextualEntity?: ContextualEntity | null;
  /** Whether the bottom sheet has content to show */
  hasSheetContent?: boolean;
  /** Callback when center tab is pressed in morphed state */
  onCenterTabPress?: () => void;
}
```

- [ ] **Step 3: Rewrite the component with morphing center**

Replace the `MobileTabBar` function:

```tsx
export function MobileTabBar({
  contextualEntity,
  hasSheetContent = false,
  onCenterTabPress,
}: MobileTabBarProps) {
  const pathname = usePathname();
  const { data: user } = useCurrentUser();
  const isAuthenticated = !!user;

  // Guest: 2 tabs only, no morphing
  const visibleTabs = TAB_ITEMS.filter(tab => !tab.authOnly || isAuthenticated);
  const hasMorphedCenter = isAuthenticated && contextualEntity != null;

  // Find the center index (Discover tab position)
  const centerIdx = visibleTabs.findIndex(t => t.href === '/games');

  return (
    <nav
      className={cn(
        'md:hidden',
        'fixed bottom-0 left-0 right-0 z-40',
        'pb-[env(safe-area-inset-bottom)]',
        'bg-card/90 backdrop-blur-md backdrop-saturate-150',
        'border-t border-border/50',
        'shadow-lg shadow-black/5',
        'dark:bg-card/95 dark:border-border/30 dark:shadow-black/20'
      )}
      aria-label="Primary navigation"
      data-testid="mobile-tab-bar"
    >
      {/* Swipe-up indicator */}
      {hasSheetContent && hasMorphedCenter && (
        <div className="absolute -top-2 left-1/2 -translate-x-1/2 w-9 h-1 bg-white/15 rounded-full" />
      )}

      <div className="flex items-center justify-around h-16 px-2">
        {visibleTabs.map((tab, idx) => {
          // Morphed center tab
          if (idx === centerIdx && hasMorphedCenter) {
            return (
              <MorphedCenterTab
                key="morphed-center"
                entity={contextualEntity!}
                onPress={onCenterTabPress}
              />
            );
          }

          const active = isTabActive(tab.href, pathname);
          return <TabLink key={tab.href} tab={tab} active={active} />;
        })}
      </div>
    </nav>
  );
}
```

- [ ] **Step 4: Add the MorphedCenterTab sub-component**

```tsx
function MorphedCenterTab({
  entity,
  onPress,
}: {
  entity: ContextualEntity;
  onPress?: () => void;
}) {
  const Icon = entity.icon;

  return (
    <button
      type="button"
      onClick={onPress}
      aria-label={`${entity.title} actions`}
      data-testid="mobile-tab-morphed-center"
      className={cn(
        'flex flex-col items-center justify-center gap-0.5',
        'min-w-[44px] min-h-[44px]',
        'px-2 py-1',
        'rounded-lg',
        'transition-all duration-300',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2'
      )}
    >
      <div
        className="flex items-center justify-center w-11 h-11 rounded-xl border-2 transition-colors"
        style={{
          borderColor: `hsl(${entity.color} / 0.4)`,
          background: `hsl(${entity.color} / 0.12)`,
        }}
      >
        <Icon
          className="w-5 h-5"
          style={{ color: `hsl(${entity.color})` } as React.CSSProperties}
        />
      </div>
      <span
        className="text-[10px] font-nunito font-semibold leading-tight truncate max-w-[60px]"
        style={{ color: `hsl(${entity.color})` }}
      >
        {entity.title}
      </span>
    </button>
  );
}
```

- [ ] **Step 5: Verify build compiles**

Run: `cd apps/web && pnpm build --no-lint 2>&1 | head -30`
Expected: Build succeeds

- [ ] **Step 6: Commit**

```bash
cd apps/web
git add src/components/layout/MobileTabBar/MobileTabBar.tsx
git commit -m "feat(mobile-ux): add morphing center tab and swipe indicator to MobileTabBar"
```

---

## Chunk 4: Cleanup & Removal (Phase 3)

### Task 8: Remove SmartFAB

**Files:**
- Delete: `apps/web/src/components/layout/SmartFAB/` (entire directory)
- Modify: any files importing SmartFAB

- [ ] **Step 1: Find all SmartFAB references**

Run: `cd apps/web && grep -r "SmartFAB\|smart-fab\|smartFab" src/ --include="*.tsx" --include="*.ts" -l`
List all files that import or reference SmartFAB.

- [ ] **Step 2: Remove SmartFAB import and usage from each file**

For each file found in step 1 (likely `UnifiedShellClient.tsx`), remove the import line and JSX usage of `<SmartFAB />`.

- [ ] **Step 3: Delete SmartFAB directory**

```bash
rm -rf apps/web/src/components/layout/SmartFAB/
```

- [ ] **Step 4: Verify build compiles**

Run: `cd apps/web && pnpm build --no-lint 2>&1 | head -20`
Expected: No import errors

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(mobile-ux): remove SmartFAB component (replaced by morphing center tab)"
```

---

### Task 9: Remove MobileBreadcrumb

**Files:**
- Delete: `apps/web/src/components/layout/MobileBreadcrumb/` (entire directory)
- Modify: any files importing MobileBreadcrumb

- [ ] **Step 1: Audit all MobileBreadcrumb references**

Run: `cd apps/web && grep -r "MobileBreadcrumb\|mobile-breadcrumb" src/ --include="*.tsx" --include="*.ts" -l`

- [ ] **Step 2: Remove MobileBreadcrumb import and usage from each file**

For each file found, remove the import and JSX usage. If the component was rendered in `UnifiedShellClient.tsx` or a layout, remove it there.

- [ ] **Step 3: Delete MobileBreadcrumb directory**

```bash
rm -rf apps/web/src/components/layout/MobileBreadcrumb/
```

- [ ] **Step 4: Verify build compiles**

Run: `cd apps/web && pnpm build --no-lint 2>&1 | head -20`
Expected: No import errors

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(mobile-ux): remove MobileBreadcrumb component (replaced by inline breadcrumb)"
```

---

### Task 10: Hide ContextualBottomNav on mobile

**Files:**
- Modify: `apps/web/src/components/layout/UnifiedShell/ContextualBottomNav.tsx`

- [ ] **Step 1: Read ContextualBottomNav.tsx**

Already read above (37 lines). The `<nav>` element needs `hidden md:flex` added.

- [ ] **Step 2: Add mobile-hide class**

Change the className in the `<nav>`:

```tsx
className={cn(
  'hidden md:flex',  // ← ADD: hide on mobile
  'sticky bottom-0 z-30',
  'items-center justify-around px-2 py-1',
  // ...rest unchanged
)}
```

Note: change `'flex items-center'` to `'items-center'` since `md:flex` handles the display.

- [ ] **Step 3: Verify build compiles**

Run: `cd apps/web && pnpm build --no-lint 2>&1 | head -20`

- [ ] **Step 4: Commit**

```bash
cd apps/web
git add src/components/layout/UnifiedShell/ContextualBottomNav.tsx
git commit -m "refactor(mobile-ux): hide ContextualBottomNav on mobile (actions moved to bottom sheet)"
```

---

### Task 11: Update E2E tests

**Files:**
- Modify: `apps/web/e2e/bottom-nav.spec.ts`
- Modify: `apps/web/e2e/mobile-card-browser.spec.ts`

- [ ] **Step 1: Read existing E2E tests**

Run: Read both files to understand current test assertions.

- [ ] **Step 2: Update bottom-nav.spec.ts**

Update assertions:
- Remove any assertions checking for `SmartFAB` presence on mobile
- Remove checks for `ContextualBottomNav` visibility on mobile viewport
- Add assertion that `[data-testid="mobile-tab-bar"]` is visible on mobile
- Add assertion that `[data-testid="mobile-tab-morphed-center"]` appears on game detail page
- Add assertion that bottom sheet opens on center tab click

- [ ] **Step 3: Update mobile-card-browser.spec.ts**

Update assertions:
- Remove `HandDrawer` visibility checks on mobile (it's now hidden)
- If test checks card carousel, update to check bottom sheet instead

- [ ] **Step 4: Run E2E tests**

Run: `cd apps/web && pnpm test:e2e --grep "bottom-nav|mobile-card" 2>&1 | tail -20`
Expected: Tests pass with updated assertions

- [ ] **Step 5: Commit**

```bash
cd apps/web
git add e2e/bottom-nav.spec.ts e2e/mobile-card-browser.spec.ts
git commit -m "test(mobile-ux): update E2E tests for new mobile navigation layout"
```

---

## Chunk 5: Final Verification

### Task 12: Run full test suite and build

- [ ] **Step 1: Run unit tests**

Run: `cd apps/web && pnpm vitest run 2>&1 | tail -30`
Expected: All tests pass. If any fail, investigate and fix.

- [ ] **Step 2: Run linter**

Run: `cd apps/web && pnpm lint 2>&1 | tail -20`
Expected: No new lint errors.

- [ ] **Step 3: Run typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -20`
Expected: No type errors.

- [ ] **Step 4: Run production build**

Run: `cd apps/web && pnpm build 2>&1 | tail -20`
Expected: Build succeeds.

- [ ] **Step 5: Final commit if any fixes were needed**

```bash
git add -A
git commit -m "fix(mobile-ux): address test/lint/type issues from mobile UX overhaul"
```

- [ ] **Step 6: Create PR**

```bash
# Ensure parent branch is tracked
git config branch.$(git branch --show-current).parent frontend-dev

# Push and create PR
git push -u origin $(git branch --show-current)
gh pr create \
  --base frontend-dev \
  --title "feat(mobile-ux): unified navigation with collapsing header and contextual bottom sheet" \
  --body "$(cat <<'EOF'
## Summary
- Consolidate 6 mobile navigation layers into unified TabBar + bottom sheet
- Add hide/reveal TopNav on scroll (Chrome mobile pattern)
- Add morphing center tab that shows current entity context
- Add contextual bottom sheet with cards carousel + action grid (swipe-up or tap)
- Remove SmartFAB, MobileBreadcrumb, hide HandDrawer + ContextualBottomNav on mobile
- Content area increases from 57% to 85-90% on iPhone SE

## Changes
- **New hooks**: useScrollHideNav, useContextualEntity, useContextualSheetActions
- **New component**: ContextualBottomSheet
- **Modified**: UnifiedTopNav (hide/reveal), MobileTabBar (morphing center), UnifiedShellClient (integration)
- **Removed**: SmartFAB, MobileBreadcrumb
- **Hidden on mobile**: HandDrawer, ContextualBottomNav

## Test plan
- [ ] Unit tests for all 3 new hooks
- [ ] Unit tests for ContextualBottomSheet component
- [ ] E2E: TopNav hides on scroll-down, reveals on scroll-up
- [ ] E2E: Center tab morphs on game/session detail pages
- [ ] E2E: Bottom sheet opens via tap/swipe, shows cards + actions
- [ ] E2E: Desktop layout unchanged
- [ ] Manual: Test on iPhone SE, iPhone 14 Pro, Android Chrome

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
