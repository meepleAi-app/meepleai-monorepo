# Carte in Mano — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace AppShell + AdminShell with a unified card-based navigation shell ("Carte in Mano") across 5 PRs.

**Architecture:** Single `UnifiedShell` with left card stack (user context) or tabbed sidebar (admin context), contextual bottom nav, and minimal top nav. Card-based navigation: visiting a page = drawing a card. Zustand store (`useCardHand`) manages hand state.

**Tech Stack:** Next.js 16 App Router, React 19, Zustand, Tailwind CSS 4, shadcn/ui, Lucide icons

**Spec:** `docs/superpowers/specs/2026-03-14-carte-in-mano-navigation-design.md`

---

## File Structure

### New Files to Create

```
apps/web/src/
├── stores/
│   └── use-card-hand.ts                          # Zustand store (merges useHandContext + useNavigationTrail)
├── hooks/
│   └── use-bottom-nav-actions.ts                 # Derived hook for contextual bottom nav actions
├── config/
│   └── entity-actions.ts                         # Centralized entity action registry + default actions
├── components/
│   └── layout/
│       └── UnifiedShell/
│           ├── UnifiedShell.tsx                   # RSC wrapper (reads cookies, session)
│           ├── UnifiedShellClient.tsx             # Client component (providers, banners, error boundaries)
│           ├── UnifiedTopNav.tsx                  # Minimal top nav (logo, card title, admin toggle, utilities)
│           ├── CardStack.tsx                      # Left panel: card stack (user context)
│           ├── CardStackItem.tsx                  # Single card in stack (Mini + Card levels)
│           ├── AdminTabSidebar.tsx                # Left panel: tabbed sidebar (admin context)
│           ├── AdminToggle.tsx                    # User ↔ Admin toggle button
│           ├── ContextualBottomNav.tsx            # Bottom nav with contextual actions
│           ├── ContextualBottomNavItem.tsx        # Single action item in bottom nav
│           └── __tests__/
│               ├── use-card-hand.test.ts
│               ├── use-bottom-nav-actions.test.ts
│               ├── CardStack.test.tsx
│               ├── CardStackItem.test.tsx
│               ├── UnifiedTopNav.test.tsx
│               ├── AdminTabSidebar.test.tsx
│               ├── AdminToggle.test.tsx
│               ├── ContextualBottomNav.test.tsx
│               └── UnifiedShellClient.test.tsx
```

### Files to Modify

```
apps/web/src/
├── app/
│   ├── (authenticated)/layout.tsx                # PR2: Switch to UnifiedShell
│   ├── (authenticated)/library/layout.tsx        # PR2: Remove NavConfig import
│   ├── (authenticated)/agents/layout.tsx         # PR2: Remove NavConfig import
│   ├── (authenticated)/dashboard/layout.tsx      # PR2: Remove NavConfig import
│   ├── (authenticated)/profile/layout.tsx        # PR2: Remove NavConfig import
│   ├── (authenticated)/game-nights/layout.tsx    # PR2: Remove NavConfig import
│   ├── (chat)/layout.tsx                         # PR2: Switch to UnifiedShell
│   ├── (chat)/chat/layout.tsx                    # PR2: Remove NavConfig import
│   └── admin/(dashboard)/layout.tsx              # PR3: Switch to UnifiedShell
├── config/
│   └── admin-dashboard-navigation.ts             # PR3: Add actions field per section
```

### Files to Delete (PR4)

```
# Layout shells
components/layout/AppShell/AppShellClient.tsx
components/layout/AppShell/AppShell.tsx (if exists as separate RSC)
components/layout/LayoutShell/LayoutShell.tsx

# Sidebar
components/layout/Sidebar/Sidebar.tsx
components/layout/Sidebar/SidebarNav.tsx
components/layout/Sidebar/SidebarContextNav.tsx
components/layout/Sidebar/SidebarToggle.tsx
components/layout/Sidebar/SidebarUser.tsx

# Action bars (entire directories)
components/layout/ActionBar/           # All files: NavActionBar, ActionBarButton, ActionBarOverflow, ActionBar, ActionBarItem, OverflowMenu, MultiSelectBar, UnifiedActionBar, index, tests
components/layout/FloatingActionBar/   # Entire directory
components/layout/AdaptiveBottomBar/   # Entire directory

# Mobile nav
components/layout/MobileNavDrawer.tsx

# Breadcrumbs (entire directory)
components/layout/Breadcrumb/          # DesktopBreadcrumb.tsx, Breadcrumb.tsx, tests

# TopNavbar (entire directory — replaced by UnifiedTopNav)
components/layout/TopNavbar/           # TopNavbar.tsx, Logo.tsx, index.ts

# Old card stack (right-side)
components/ui/navigation/card-stack-panel.tsx

# Admin layout
components/admin/layout/AdminShell.tsx
components/admin/layout/AdminTopNav.tsx
components/admin/layout/AdminContextualSidebar.tsx
components/admin/layout/AdminMobileNav.tsx
components/admin/layout/AdminMobileTabBar.tsx

# NavConfig files (16 total)
app/(authenticated)/agents/NavConfig.tsx
app/(authenticated)/dashboard/NavConfig.tsx
app/(authenticated)/game-nights/NavConfig.tsx
app/(authenticated)/library/NavConfig.tsx
app/(authenticated)/profile/NavConfig.tsx
app/(chat)/chat/NavConfig.tsx
app/admin/(dashboard)/agents/NavConfig.tsx
app/admin/(dashboard)/ai/NavConfig.tsx
app/admin/(dashboard)/analytics/NavConfig.tsx
app/admin/(dashboard)/config/NavConfig.tsx
app/admin/(dashboard)/content/NavConfig.tsx
app/admin/(dashboard)/monitor/NavConfig.tsx
app/admin/(dashboard)/monitor/containers/NavConfig.tsx
app/admin/(dashboard)/monitor/logs/NavConfig.tsx
app/admin/(dashboard)/monitor/operations/NavConfig.tsx
app/admin/(dashboard)/monitor/services/NavConfig.tsx

# Old hooks
hooks/use-navigation-trail.ts
hooks/use-hand-context.ts (if not already at stores/)
hooks/useSetNavConfig.ts
hooks/useSidebarState.ts
hooks/__tests__/useSidebarState.test.ts

# Old tests
components/layout/__tests__/AppShellClient.test.tsx
components/layout/__tests__/LayoutShell.test.tsx
components/layout/__tests__/LayoutShell.responsive.test.tsx
components/layout/__tests__/MobileNavDrawer.test.tsx
components/layout/__tests__/AdaptiveBottomBar.test.tsx
components/layout/ActionBar/__tests__/NavActionBar.test.tsx
components/layout/Sidebar/__tests__/Sidebar.test.tsx
components/layout/Sidebar/__tests__/SidebarContextNavTabs.test.tsx
components/layout/FloatingActionBar/__tests__/FloatingActionBar.test.tsx
components/admin/__tests__/AdminLayout.test.tsx
components/admin/__tests__/AdminSidebar.test.tsx
components/admin/__tests__/AdminHeader.test.tsx
components/admin/layout/__tests__/AdminMobileTabBar.test.tsx
components/ui/navigation/__tests__/card-focus-layout.test.tsx
components/ui/navigation/__tests__/hand-stack.test.tsx
```

---

## Chunk 1: PR 1 — Foundation (New Components + Store)

**Branch:** `feature/carte-in-mano-foundation`
**Parent:** `main-dev`
**Goal:** Build all new components and hooks. Old code untouched — both systems coexist.

### Task 1.1: `useCardHand` Zustand Store

**Files:**
- Create: `apps/web/src/stores/use-card-hand.ts`
- Create: `apps/web/src/components/layout/UnifiedShell/__tests__/use-card-hand.test.ts`

**Reference:** Existing `apps/web/src/hooks/use-hand-context.ts` (Zustand pattern), `apps/web/src/hooks/use-navigation-trail.ts` (sessionStorage + useSyncExternalStore pattern)

- [ ] **Step 1: Write failing tests for core card actions**

```typescript
// apps/web/src/components/layout/UnifiedShell/__tests__/use-card-hand.test.ts
import { act, renderHook } from '@testing-library/react';
import { useCardHand } from '@/stores/use-card-hand';
import type { HandCard } from '@/stores/use-card-hand';

const makeCard = (id: string, entity = 'game' as const): HandCard => ({
  id,
  entity,
  title: `Card ${id}`,
  href: `/test/${id}`,
});

describe('useCardHand', () => {
  beforeEach(() => {
    // Reset Zustand store between tests
    const { result } = renderHook(() => useCardHand());
    act(() => result.current.clear());
    sessionStorage.clear();
    localStorage.clear();
  });

  describe('drawCard', () => {
    it('adds a card and focuses it', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => result.current.drawCard(makeCard('g1')));
      expect(result.current.cards).toHaveLength(1);
      expect(result.current.cards[0].id).toBe('g1');
      expect(result.current.focusedIdx).toBe(0);
    });

    it('focuses existing card instead of duplicating', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => {
        result.current.drawCard(makeCard('g1'));
        result.current.drawCard(makeCard('g2'));
        result.current.drawCard(makeCard('g1')); // duplicate
      });
      expect(result.current.cards).toHaveLength(2);
      expect(result.current.focusedIdx).toBe(0); // focused back to g1
    });

    it('evicts oldest non-pinned card when hand is full (FIFO)', () => {
      const { result } = renderHook(() => useCardHand());
      // Fill hand to max (10)
      for (let i = 0; i < 10; i++) {
        act(() => result.current.drawCard(makeCard(`g${i}`)));
      }
      expect(result.current.cards).toHaveLength(10);
      // Draw one more — g0 should be evicted
      act(() => result.current.drawCard(makeCard('g10')));
      expect(result.current.cards).toHaveLength(10);
      expect(result.current.cards.find(c => c.id === 'g0')).toBeUndefined();
      expect(result.current.cards.find(c => c.id === 'g10')).toBeDefined();
    });

    it('skips pinned cards during FIFO eviction', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => {
        result.current.drawCard(makeCard('g0'));
        result.current.pinCard('g0'); // pin first card
      });
      // Fill remaining slots
      for (let i = 1; i < 10; i++) {
        act(() => result.current.drawCard(makeCard(`g${i}`)));
      }
      // Draw one more — g1 (first non-pinned) should be evicted, not g0
      act(() => result.current.drawCard(makeCard('g10')));
      expect(result.current.cards.find(c => c.id === 'g0')).toBeDefined(); // pinned survives
      expect(result.current.cards.find(c => c.id === 'g1')).toBeUndefined(); // evicted
    });
  });

  describe('discardCard', () => {
    it('removes card and adjusts focus', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => {
        result.current.drawCard(makeCard('g1'));
        result.current.drawCard(makeCard('g2'));
        result.current.drawCard(makeCard('g3'));
        result.current.focusCard(1); // focus g2
      });
      act(() => result.current.discardCard('g2'));
      expect(result.current.cards).toHaveLength(2);
      // Focus should move to next card (g3, now at index 1)
      expect(result.current.focusedIdx).toBe(1);
    });
  });

  describe('pinCard / unpinCard', () => {
    it('pins and unpins a card', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => result.current.drawCard(makeCard('g1')));
      act(() => result.current.pinCard('g1'));
      expect(result.current.pinnedIds.has('g1')).toBe(true);
      act(() => result.current.unpinCard('g1'));
      expect(result.current.pinnedIds.has('g1')).toBe(false);
    });
  });

  describe('swipeNext / swipePrev', () => {
    it('navigates between cards', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => {
        result.current.drawCard(makeCard('g1'));
        result.current.drawCard(makeCard('g2'));
        result.current.drawCard(makeCard('g3'));
        result.current.focusCard(0);
      });
      act(() => result.current.swipeNext());
      expect(result.current.focusedIdx).toBe(1);
      act(() => result.current.swipePrev());
      expect(result.current.focusedIdx).toBe(0);
    });

    it('does not go below 0 or above length', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => result.current.drawCard(makeCard('g1')));
      act(() => result.current.swipePrev());
      expect(result.current.focusedIdx).toBe(0);
      act(() => result.current.swipeNext());
      expect(result.current.focusedIdx).toBe(0); // only 1 card
    });
  });

  describe('focusByHref', () => {
    it('focuses card matching href', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => {
        result.current.drawCard(makeCard('g1'));
        result.current.drawCard(makeCard('g2'));
      });
      act(() => result.current.focusByHref('/test/g1'));
      expect(result.current.focusedIdx).toBe(0);
    });
  });

  describe('context toggle', () => {
    it('toggles between user and admin', () => {
      const { result } = renderHook(() => useCardHand());
      expect(result.current.context).toBe('user');
      act(() => result.current.toggleContext());
      expect(result.current.context).toBe('admin');
      act(() => result.current.toggleContext());
      expect(result.current.context).toBe('user');
    });
  });

  describe('expandedStack', () => {
    it('toggles stack expansion', () => {
      const { result } = renderHook(() => useCardHand());
      expect(result.current.expandedStack).toBe(false);
      act(() => result.current.toggleExpandStack());
      expect(result.current.expandedStack).toBe(true);
    });
  });

  describe('clear', () => {
    it('removes non-pinned cards only', () => {
      const { result } = renderHook(() => useCardHand());
      act(() => {
        result.current.drawCard(makeCard('g1'));
        result.current.drawCard(makeCard('g2'));
        result.current.pinCard('g1');
      });
      act(() => result.current.clear());
      expect(result.current.cards).toHaveLength(1);
      expect(result.current.cards[0].id).toBe('g1');
    });
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/use-card-hand.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Implement `useCardHand` store**

```typescript
// apps/web/src/stores/use-card-hand.ts
'use client';

import { create } from 'zustand';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

export interface HandCard {
  id: string;
  entity: MeepleEntityType;
  title: string;
  href: string;
  subtitle?: string;
  imageUrl?: string;
}

const MAX_HAND_SIZE = 10;
const STORAGE_KEY = 'meeple-card-hand';
const PINS_KEY = 'meeple-card-pins';
const EXPANDED_KEY = 'meeple-card-stack-expanded';

// ---------------------------------------------------------------------------
// sessionStorage helpers
// ---------------------------------------------------------------------------

function readCards(): HandCard[] {
  if (typeof window === 'undefined') return [];
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as HandCard[]) : [];
  } catch {
    return [];
  }
}

function writeCards(cards: HandCard[]): void {
  if (typeof window === 'undefined') return;
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(cards));
  } catch { /* full or unavailable */ }
  window.dispatchEvent(new Event('card-hand-change'));
}

function readPins(): Set<string> {
  if (typeof window === 'undefined') return new Set();
  try {
    const raw = localStorage.getItem(PINS_KEY);
    return raw ? new Set(JSON.parse(raw) as string[]) : new Set();
  } catch {
    return new Set();
  }
}

function writePins(pins: Set<string>): void {
  if (typeof window === 'undefined') return;
  try {
    localStorage.setItem(PINS_KEY, JSON.stringify([...pins]));
  } catch { /* ignore */ }
}

function readExpanded(): boolean {
  if (typeof window === 'undefined') return false;
  return localStorage.getItem(EXPANDED_KEY) === 'true';
}

function writeExpanded(expanded: boolean): void {
  if (typeof window === 'undefined') return;
  localStorage.setItem(EXPANDED_KEY, String(expanded));
}

// ---------------------------------------------------------------------------
// Store
// ---------------------------------------------------------------------------

interface CardHandState {
  cards: HandCard[];
  focusedIdx: number;
  pinnedIds: Set<string>;
  maxHandSize: number;
  context: 'user' | 'admin';
  expandedStack: boolean;
  highlightEntity: MeepleEntityType | null;

  drawCard: (card: HandCard) => void;
  discardCard: (id: string) => void;
  focusCard: (index: number) => void;
  focusByHref: (href: string) => void;
  pinCard: (id: string) => void;
  unpinCard: (id: string) => void;
  swipeNext: () => void;
  swipePrev: () => void;
  toggleContext: () => void;
  toggleExpandStack: () => void;
  setHighlightEntity: (entity: MeepleEntityType | null) => void;
  clear: () => void;
}

export const useCardHand = create<CardHandState>((set, get) => ({
  cards: readCards(),
  focusedIdx: -1,
  pinnedIds: readPins(),
  maxHandSize: MAX_HAND_SIZE,
  context: 'user',
  expandedStack: readExpanded(),
  highlightEntity: null,

  drawCard: (card) => {
    const { cards, pinnedIds, maxHandSize } = get();

    // Duplicate check — focus existing card
    const existingIdx = cards.findIndex((c) => c.id === card.id);
    if (existingIdx >= 0) {
      set({ focusedIdx: existingIdx });
      return;
    }

    let next = [...cards, card];

    // FIFO eviction if over limit
    if (next.length > maxHandSize) {
      const evictIdx = next.findIndex((c) => !pinnedIds.has(c.id));
      if (evictIdx >= 0) {
        next = [...next.slice(0, evictIdx), ...next.slice(evictIdx + 1)];
      } else {
        // All cards pinned — drop the oldest pinned (edge case)
        next = next.slice(1);
      }
    }

    writeCards(next);
    set({ cards: next, focusedIdx: next.length - 1 });
  },

  discardCard: (id) => {
    const { cards, focusedIdx, pinnedIds } = get();
    const idx = cards.findIndex((c) => c.id === id);
    if (idx === -1) return;

    const next = cards.filter((c) => c.id !== id);
    const newPins = new Set(pinnedIds);
    newPins.delete(id);

    let newFocused: number;
    if (next.length === 0) {
      newFocused = -1;
    } else if (focusedIdx >= next.length) {
      newFocused = next.length - 1;
    } else if (focusedIdx > idx) {
      newFocused = focusedIdx - 1;
    } else if (focusedIdx === idx) {
      // Discarding focused card — move to next (or previous if last)
      newFocused = Math.min(focusedIdx, next.length - 1);
    } else {
      newFocused = focusedIdx;
    }

    writeCards(next);
    writePins(newPins);
    set({ cards: next, focusedIdx: newFocused, pinnedIds: newPins });
  },

  focusCard: (index) => {
    const { cards } = get();
    if (index >= 0 && index < cards.length) {
      set({ focusedIdx: index });
    }
  },

  focusByHref: (href) => {
    const { cards } = get();
    const idx = cards.findIndex((c) => c.href === href);
    if (idx >= 0) {
      set({ focusedIdx: idx });
    }
  },

  pinCard: (id) => {
    const { pinnedIds } = get();
    const next = new Set(pinnedIds);
    next.add(id);
    writePins(next);
    set({ pinnedIds: next });
  },

  unpinCard: (id) => {
    const { pinnedIds } = get();
    const next = new Set(pinnedIds);
    next.delete(id);
    writePins(next);
    set({ pinnedIds: next });
  },

  swipeNext: () => {
    const { focusedIdx, cards } = get();
    if (focusedIdx < cards.length - 1) {
      set({ focusedIdx: focusedIdx + 1 });
    }
  },

  swipePrev: () => {
    const { focusedIdx } = get();
    if (focusedIdx > 0) {
      set({ focusedIdx: focusedIdx - 1 });
    }
  },

  toggleContext: () => {
    const { context } = get();
    set({ context: context === 'user' ? 'admin' : 'user' });
  },

  toggleExpandStack: () => {
    const { expandedStack } = get();
    writeExpanded(!expandedStack);
    set({ expandedStack: !expandedStack });
  },

  setHighlightEntity: (entity) => set({ highlightEntity: entity }),

  clear: () => {
    const { cards, pinnedIds } = get();
    const pinned = cards.filter((c) => pinnedIds.has(c.id));
    writeCards(pinned);
    set({
      cards: pinned,
      focusedIdx: pinned.length > 0 ? pinned.length - 1 : -1,
    });
  },
}));
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/use-card-hand.test.ts`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/stores/use-card-hand.ts apps/web/src/components/layout/UnifiedShell/__tests__/use-card-hand.test.ts
git commit -m "feat(nav): add useCardHand Zustand store with FIFO eviction and pin support"
```

---

### Task 1.2: Entity Action Registry + `useBottomNavActions`

**Files:**
- Create: `apps/web/src/config/entity-actions.ts`
- Create: `apps/web/src/hooks/use-bottom-nav-actions.ts`
- Create: `apps/web/src/components/layout/UnifiedShell/__tests__/use-bottom-nav-actions.test.ts`

**Reference:** Existing NavConfig files at `apps/web/src/app/(authenticated)/*/NavConfig.tsx` for current action patterns

- [ ] **Step 1: Create entity action registry**

```typescript
// apps/web/src/config/entity-actions.ts
import {
  BookOpen, Compass, MessageSquare, ClipboardList,
  Gamepad2, Star, Bot, FileText,
  StickyNote, Trophy, StopCircle,
  Pencil, Database, RefreshCw,
  Users, Mail, BarChart3,
  ArrowRight, Share2,
  Wrench, Link2,
  CalendarDays, UserCheck,
  Eye,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

export interface BottomNavActionDef {
  id: string;
  label: string;
  icon: LucideIcon;
  variant?: 'primary' | 'secondary' | 'ghost' | 'destructive';
  /** If set, this action draws a card instead of triggering onClick */
  drawCard?: { entity: MeepleEntityType; href: string };
  /** Route to navigate to (relative to current card's href) */
  route?: string;
}

/** Actions shown when a card of this entity type is focused */
export const ENTITY_ACTIONS: Record<MeepleEntityType, BottomNavActionDef[]> = {
  game: [
    { id: 'new-session', label: 'Nuova Sessione', icon: Gamepad2, variant: 'primary' },
    { id: 'wishlist', label: 'Wishlist', icon: Star },
    { id: 'chat-ai', label: 'Chat AI', icon: Bot },
    { id: 'upload-pdf', label: 'Carica PDF', icon: FileText },
  ],
  session: [
    { id: 'add-notes', label: 'Note', icon: StickyNote, variant: 'primary' },
    { id: 'score', label: 'Punteggi', icon: Trophy },
    { id: 'end-session', label: 'Termina', icon: StopCircle, variant: 'destructive' },
  ],
  agent: [
    { id: 'chat', label: 'Chat', icon: MessageSquare, variant: 'primary' },
    { id: 'edit', label: 'Modifica', icon: Pencil },
    { id: 'view-kb', label: 'KB Cards', icon: Database },
  ],
  kb: [
    { id: 'documents', label: 'Documenti', icon: FileText, variant: 'primary' },
    { id: 'reindex', label: 'Reindicizza', icon: RefreshCw },
    { id: 'edit', label: 'Modifica', icon: Pencil },
  ],
  player: [
    { id: 'sessions', label: 'Sessioni', icon: ClipboardList, variant: 'primary' },
    { id: 'invite', label: 'Invita', icon: Mail },
    { id: 'stats', label: 'Statistiche', icon: BarChart3 },
  ],
  chatSession: [
    { id: 'continue', label: 'Continua', icon: ArrowRight, variant: 'primary' },
    { id: 'export', label: 'Esporta', icon: FileText },
    { id: 'share', label: 'Condividi', icon: Share2 },
  ],
  toolkit: [
    { id: 'view-tools', label: 'Strumenti', icon: Wrench, variant: 'primary' },
    { id: 'link-games', label: 'Collega Giochi', icon: Link2 },
  ],
  event: [
    { id: 'details', label: 'Dettagli', icon: Eye, variant: 'primary' },
    { id: 'rsvp', label: 'RSVP', icon: UserCheck },
    { id: 'share', label: 'Condividi', icon: Share2 },
  ],
  custom: [
    { id: 'details', label: 'Dettagli', icon: Eye },
  ],
};

/** Actions shown when no card is focused — "draw card" actions */
export const DEFAULT_ACTIONS: BottomNavActionDef[] = [
  { id: 'library', label: 'Library', icon: BookOpen, drawCard: { entity: 'game', href: '/library' } },
  { id: 'discover', label: 'Discover', icon: Compass, drawCard: { entity: 'game', href: '/discover' } },
  { id: 'chat', label: 'Chat', icon: MessageSquare, drawCard: { entity: 'chatSession', href: '/chat' } },
  { id: 'sessions', label: 'Sessions', icon: ClipboardList, drawCard: { entity: 'session', href: '/sessions' } },
];

/** Default pinned card definitions */
export const DEFAULT_PINNED_CARDS = DEFAULT_ACTIONS
  .filter((a) => a.drawCard)
  .map((a) => ({
    id: `section-${a.id}`,
    entity: a.drawCard!.entity,
    title: a.label,
    href: a.drawCard!.href,
  }));
```

- [ ] **Step 2: Write failing tests for `useBottomNavActions`**

```typescript
// apps/web/src/components/layout/UnifiedShell/__tests__/use-bottom-nav-actions.test.ts
import { act, renderHook } from '@testing-library/react';
import { useBottomNavActions } from '@/hooks/use-bottom-nav-actions';
import { useCardHand } from '@/stores/use-card-hand';

describe('useBottomNavActions', () => {
  beforeEach(() => {
    const { result } = renderHook(() => useCardHand());
    act(() => result.current.clear());
  });

  it('returns default actions when no card is focused', () => {
    const { result } = renderHook(() => useBottomNavActions());
    expect(result.current.map(a => a.id)).toEqual(['library', 'discover', 'chat', 'sessions']);
  });

  it('returns entity-specific actions when a game card is focused', () => {
    const { result: hand } = renderHook(() => useCardHand());
    act(() => {
      hand.current.drawCard({ id: 'g1', entity: 'game', title: 'Catan', href: '/library/g1' });
    });

    const { result } = renderHook(() => useBottomNavActions());
    expect(result.current.map(a => a.id)).toEqual(['new-session', 'wishlist', 'chat-ai', 'upload-pdf']);
  });

  it('returns entity-specific actions for session entity', () => {
    const { result: hand } = renderHook(() => useCardHand());
    act(() => {
      hand.current.drawCard({ id: 's1', entity: 'session', title: 'Game Night', href: '/sessions/s1' });
    });

    const { result } = renderHook(() => useBottomNavActions());
    expect(result.current.map(a => a.id)).toEqual(['add-notes', 'score', 'end-session']);
  });

  it('returns default actions when focused card index is -1', () => {
    const { result } = renderHook(() => useBottomNavActions());
    expect(result.current).toHaveLength(4);
  });
});
```

- [ ] **Step 3: Implement `useBottomNavActions`**

```typescript
// apps/web/src/hooks/use-bottom-nav-actions.ts
'use client';

import { useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useCardHand } from '@/stores/use-card-hand';
import { ENTITY_ACTIONS, DEFAULT_ACTIONS, type BottomNavActionDef } from '@/config/entity-actions';
import type { LucideIcon } from 'lucide-react';

export interface BottomNavAction {
  id: string;
  label: string;
  icon: LucideIcon;
  onClick: () => void;
  variant?: 'primary' | 'secondary' | 'ghost' | 'destructive';
  badge?: number | string;
  hidden?: boolean;
  disabled?: boolean;
}

export function useBottomNavActions(): BottomNavAction[] {
  const { cards, focusedIdx, drawCard } = useCardHand();
  const router = useRouter();

  const focusedCard = focusedIdx >= 0 && focusedIdx < cards.length
    ? cards[focusedIdx]
    : null;

  return useMemo(() => {
    if (!focusedCard) {
      // No card focused — show default "draw card" actions
      return DEFAULT_ACTIONS.map((def) => ({
        id: def.id,
        label: def.label,
        icon: def.icon,
        variant: def.variant,
        onClick: () => {
          if (def.drawCard) {
            drawCard({
              id: `section-${def.id}`,
              entity: def.drawCard.entity,
              title: def.label,
              href: def.drawCard.href,
            });
            router.push(def.drawCard.href);
          }
        },
      }));
    }

    // Card focused — show entity-specific actions
    const entityActions = ENTITY_ACTIONS[focusedCard.entity] ?? ENTITY_ACTIONS.custom;
    return entityActions.map((def) => ({
      id: def.id,
      label: def.label,
      icon: def.icon,
      variant: def.variant,
      onClick: () => {
        if (def.route) {
          router.push(`${focusedCard.href}${def.route}`);
        }
        // Action-specific handlers will be provided by page components
        // via a registry pattern in Phase 2
      },
    }));
  }, [focusedCard, drawCard, router]);
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/use-bottom-nav-actions.test.ts`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/config/entity-actions.ts apps/web/src/hooks/use-bottom-nav-actions.ts apps/web/src/components/layout/UnifiedShell/__tests__/use-bottom-nav-actions.test.ts
git commit -m "feat(nav): add entity action registry and useBottomNavActions hook"
```

---

### Task 1.3: `CardStackItem` Component

**Files:**
- Create: `apps/web/src/components/layout/UnifiedShell/CardStackItem.tsx`
- Create: `apps/web/src/components/layout/UnifiedShell/__tests__/CardStackItem.test.tsx`

**Reference:** Existing `apps/web/src/components/ui/navigation/card-stack-panel.tsx` (CardStackItem inner component), entity colors from `apps/web/src/components/ui/data-display/meeple-card-styles.ts`

- [ ] **Step 1: Write failing tests**

```typescript
// apps/web/src/components/layout/UnifiedShell/__tests__/CardStackItem.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { CardStackItem } from '../CardStackItem';
import type { HandCard } from '@/stores/use-card-hand';

const card: HandCard = {
  id: 'g1',
  entity: 'game',
  title: 'Catan',
  href: '/library/g1',
  subtitle: 'Klaus Teuber',
};

describe('CardStackItem', () => {
  it('renders Mini level with icon only', () => {
    render(
      <CardStackItem card={card} level="mini" index={0} isFocused={false} isPinned={false}
        onFocus={vi.fn()} onDiscard={vi.fn()} />
    );
    expect(screen.getByTestId('card-stack-item-g1')).toBeInTheDocument();
    // Mini level should NOT show title text
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
  });

  it('renders Card level with title and subtitle', () => {
    render(
      <CardStackItem card={card} level="card" index={0} isFocused={false} isPinned={false}
        onFocus={vi.fn()} onDiscard={vi.fn()} />
    );
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Klaus Teuber')).toBeInTheDocument();
  });

  it('highlights when focused', () => {
    render(
      <CardStackItem card={card} level="card" index={0} isFocused={true} isPinned={false}
        onFocus={vi.fn()} onDiscard={vi.fn()} />
    );
    const el = screen.getByTestId('card-stack-item-g1');
    expect(el.dataset.focused).toBe('true');
  });

  it('shows pin indicator when pinned', () => {
    render(
      <CardStackItem card={card} level="card" index={0} isFocused={false} isPinned={true}
        onFocus={vi.fn()} onDiscard={vi.fn()} />
    );
    expect(screen.getByLabelText('Pinned')).toBeInTheDocument();
  });

  it('calls onFocus when clicked', () => {
    const onFocus = vi.fn();
    render(
      <CardStackItem card={card} level="card" index={2} isFocused={false} isPinned={false}
        onFocus={onFocus} onDiscard={vi.fn()} />
    );
    fireEvent.click(screen.getByTestId('card-stack-item-g1'));
    expect(onFocus).toHaveBeenCalledWith(2);
  });

  it('calls onDiscard when X clicked', () => {
    const onDiscard = vi.fn();
    render(
      <CardStackItem card={card} level="card" index={0} isFocused={false} isPinned={false}
        onFocus={vi.fn()} onDiscard={onDiscard} />
    );
    fireEvent.click(screen.getByLabelText('Discard card'));
    expect(onDiscard).toHaveBeenCalledWith('g1');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/CardStackItem.test.tsx`

- [ ] **Step 3: Implement `CardStackItem`**

```tsx
// apps/web/src/components/layout/UnifiedShell/CardStackItem.tsx
'use client';

import { useCallback } from 'react';
import { X, Pin } from 'lucide-react';
import { ENTITY_NAV_ICONS } from '@/components/ui/data-display/meeple-card-features/navigation-icons';
import { entityColors } from '@/components/ui/data-display/meeple-card-styles';
import type { HandCard } from '@/stores/use-card-hand';
import { cn } from '@/lib/utils';

type CardLevel = 'mini' | 'card';

interface CardStackItemProps {
  card: HandCard;
  level: CardLevel;
  index: number;
  isFocused: boolean;
  isPinned: boolean;
  onFocus: (index: number) => void;
  onDiscard: (id: string) => void;
}

export function CardStackItem({
  card,
  level,
  index,
  isFocused,
  isPinned,
  onFocus,
  onDiscard,
}: CardStackItemProps) {
  const Icon = ENTITY_NAV_ICONS[card.entity] ?? ENTITY_NAV_ICONS.game;
  const hsl = entityColors[card.entity]?.hsl ?? '220 70% 50%';

  const handleClick = useCallback(() => onFocus(index), [onFocus, index]);

  const handleDiscard = useCallback(
    (e: React.MouseEvent) => {
      e.stopPropagation();
      onDiscard(card.id);
    },
    [onDiscard, card.id]
  );

  if (level === 'mini') {
    return (
      <button
        type="button"
        onClick={handleClick}
        className={cn(
          'flex flex-col items-center gap-0.5 p-1.5 rounded-lg',
          'transition-all duration-200 cursor-pointer border',
          isFocused
            ? 'bg-[hsl(var(--card-hsl)/0.12)] border-[hsl(var(--card-hsl)/0.4)] scale-105'
            : 'bg-card/60 border-border/30 hover:bg-card/80'
        )}
        style={{ '--card-hsl': hsl } as React.CSSProperties}
        data-testid={`card-stack-item-${card.id}`}
        data-focused={isFocused ? 'true' : 'false'}
        title={card.title}
      >
        <Icon
          className={cn(
            'w-4 h-4',
            isFocused ? 'text-[hsl(var(--card-hsl))]' : 'text-muted-foreground'
          )}
        />
        {isPinned && (
          <Pin className="w-2 h-2 text-muted-foreground/50" aria-label="Pinned" />
        )}
      </button>
    );
  }

  // Card level
  return (
    <button
      type="button"
      onClick={handleClick}
      className={cn(
        'group flex items-center gap-3 px-3 py-2.5 rounded-lg w-full text-left',
        'transition-all duration-200 cursor-pointer border',
        isFocused
          ? 'bg-[hsl(var(--card-hsl)/0.1)] border-[hsl(var(--card-hsl)/0.25)] shadow-sm'
          : 'bg-card/50 border-border/30 hover:bg-[hsl(var(--card-hsl)/0.06)] hover:border-[hsl(var(--card-hsl)/0.2)]'
      )}
      style={{ '--card-hsl': hsl } as React.CSSProperties}
      data-testid={`card-stack-item-${card.id}`}
      data-focused={isFocused ? 'true' : 'false'}
    >
      {/* Entity icon */}
      <div
        className={cn(
          'flex items-center justify-center w-8 h-8 rounded-md shrink-0',
          'bg-[hsl(var(--card-hsl)/0.12)]'
        )}
      >
        <Icon className="w-4 h-4 text-[hsl(var(--card-hsl))]" />
      </div>

      {/* Label + subtitle */}
      <div className="flex-1 min-w-0">
        <p
          className={cn(
            'text-sm font-medium truncate font-quicksand',
            isFocused ? 'text-[hsl(var(--card-hsl))]' : 'text-foreground'
          )}
        >
          {card.title}
        </p>
        {card.subtitle && (
          <p className="text-[10px] text-muted-foreground truncate font-nunito">
            {card.subtitle}
          </p>
        )}
      </div>

      {/* Pin indicator */}
      {isPinned && (
        <Pin className="w-3 h-3 text-muted-foreground/50 shrink-0" aria-label="Pinned" />
      )}

      {/* Discard button (visible on hover) */}
      <button
        type="button"
        onClick={handleDiscard}
        className={cn(
          'flex items-center justify-center w-5 h-5 rounded shrink-0',
          'text-muted-foreground/0 group-hover:text-muted-foreground/70',
          'hover:!text-destructive hover:bg-destructive/10',
          'transition-colors'
        )}
        aria-label="Discard card"
      >
        <X className="w-3 h-3" />
      </button>
    </button>
  );
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/CardStackItem.test.tsx`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/CardStackItem.tsx apps/web/src/components/layout/UnifiedShell/__tests__/CardStackItem.test.tsx
git commit -m "feat(nav): add CardStackItem component with Mini and Card detail levels"
```

---

### Task 1.4: `CardStack` Component

**Files:**
- Create: `apps/web/src/components/layout/UnifiedShell/CardStack.tsx`
- Create: `apps/web/src/components/layout/UnifiedShell/__tests__/CardStack.test.tsx`

- [ ] **Step 1: Write failing tests**

```typescript
// apps/web/src/components/layout/UnifiedShell/__tests__/CardStack.test.tsx
import { render, screen } from '@testing-library/react';
import { act, renderHook } from '@testing-library/react';
import { CardStack } from '../CardStack';
import { useCardHand } from '@/stores/use-card-hand';

describe('CardStack', () => {
  beforeEach(() => {
    const { result } = renderHook(() => useCardHand());
    act(() => result.current.clear());
  });

  it('renders nothing when no cards', () => {
    const { container } = render(<CardStack />);
    expect(container.querySelector('[data-testid="card-stack"]')).toBeInTheDocument();
  });

  it('renders pinned cards in a separate section from dynamic cards', () => {
    const { result } = renderHook(() => useCardHand());
    act(() => {
      result.current.drawCard({ id: 'p1', entity: 'game', title: 'Library', href: '/library' });
      result.current.pinCard('p1');
      result.current.drawCard({ id: 'd1', entity: 'game', title: 'Catan', href: '/library/catan' });
    });

    render(<CardStack />);
    expect(screen.getByTestId('card-stack-pinned')).toBeInTheDocument();
    expect(screen.getByTestId('card-stack-dynamic')).toBeInTheDocument();
  });

  it('renders cards with correct level based on expandedStack', () => {
    const { result } = renderHook(() => useCardHand());
    act(() => {
      result.current.drawCard({ id: 'd1', entity: 'game', title: 'Catan', href: '/library/catan' });
    });

    // Default: collapsed → mini level (no title visible)
    const { rerender } = render(<CardStack />);
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();

    // Expand
    act(() => result.current.toggleExpandStack());
    rerender(<CardStack />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement `CardStack`**

```tsx
// apps/web/src/components/layout/UnifiedShell/CardStack.tsx
'use client';

import { useCardHand } from '@/stores/use-card-hand';
import { CardStackItem } from './CardStackItem';
import { ChevronRight, ChevronLeft } from 'lucide-react';
import { cn } from '@/lib/utils';

export function CardStack() {
  const {
    cards, focusedIdx, pinnedIds, expandedStack,
    focusCard, discardCard, toggleExpandStack,
  } = useCardHand();

  const level = expandedStack ? 'card' as const : 'mini' as const;
  const pinnedCards = cards.filter((c) => pinnedIds.has(c.id));
  const dynamicCards = cards.filter((c) => !pinnedIds.has(c.id));

  return (
    <div
      className={cn(
        'flex flex-col h-full border-r border-border/30 bg-background/50 backdrop-blur-sm',
        'transition-all duration-200',
        expandedStack ? 'w-[180px]' : 'w-14'
      )}
      data-testid="card-stack"
    >
      {/* Expand/collapse toggle */}
      <button
        type="button"
        onClick={toggleExpandStack}
        className={cn(
          'flex items-center justify-center py-2',
          'text-muted-foreground/50 hover:text-muted-foreground',
          'transition-colors'
        )}
        aria-label={expandedStack ? 'Collapse card stack' : 'Expand card stack'}
      >
        {expandedStack ? (
          <ChevronLeft className="w-4 h-4" />
        ) : (
          <ChevronRight className="w-4 h-4" />
        )}
      </button>

      {/* Dynamic cards (grow upward — flex-end) */}
      <div
        className="flex-1 flex flex-col justify-end gap-1.5 px-1.5 overflow-y-auto scrollbar-none"
        data-testid="card-stack-dynamic"
      >
        {dynamicCards.map((card) => {
          const globalIdx = cards.indexOf(card);
          return (
            <CardStackItem
              key={card.id}
              card={card}
              level={level}
              index={globalIdx}
              isFocused={globalIdx === focusedIdx}
              isPinned={false}
              onFocus={focusCard}
              onDiscard={discardCard}
            />
          );
        })}
      </div>

      {/* Separator */}
      {pinnedCards.length > 0 && dynamicCards.length > 0 && (
        <div className="mx-2 my-1 border-t border-border/20" />
      )}

      {/* Pinned cards (always at bottom) */}
      <div
        className="flex flex-col gap-1.5 px-1.5 pb-2 pt-1"
        data-testid="card-stack-pinned"
      >
        {pinnedCards.map((card) => {
          const globalIdx = cards.indexOf(card);
          return (
            <CardStackItem
              key={card.id}
              card={card}
              level={level}
              index={globalIdx}
              isFocused={globalIdx === focusedIdx}
              isPinned={true}
              onFocus={focusCard}
              onDiscard={discardCard}
            />
          );
        })}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/CardStack.test.tsx`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/CardStack.tsx apps/web/src/components/layout/UnifiedShell/__tests__/CardStack.test.tsx
git commit -m "feat(nav): add CardStack component with pinned/dynamic sections and expand toggle"
```

---

### Task 1.5: `UnifiedTopNav` Component

**Files:**
- Create: `apps/web/src/components/layout/UnifiedShell/UnifiedTopNav.tsx`
- Create: `apps/web/src/components/layout/UnifiedShell/__tests__/UnifiedTopNav.test.tsx`

**Reference:** Existing `apps/web/src/components/layout/TopNavbar/TopNavbar.tsx` for structure, `apps/web/src/components/admin/layout/AdminTopNav.tsx` for admin patterns

- [ ] **Step 1: Write failing tests**

```typescript
// apps/web/src/components/layout/UnifiedShell/__tests__/UnifiedTopNav.test.tsx
import { render, screen } from '@testing-library/react';
import { act, renderHook } from '@testing-library/react';
import { UnifiedTopNav } from '../UnifiedTopNav';
import { useCardHand } from '@/stores/use-card-hand';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/library',
}));

describe('UnifiedTopNav', () => {
  beforeEach(() => {
    const { result } = renderHook(() => useCardHand());
    act(() => result.current.clear());
  });

  it('renders logo', () => {
    render(<UnifiedTopNav isAdmin={false} />);
    expect(screen.getByTestId('unified-top-nav')).toBeInTheDocument();
  });

  it('shows app name when no card focused', () => {
    render(<UnifiedTopNav isAdmin={false} />);
    expect(screen.getByText('MeepleAI')).toBeInTheDocument();
  });

  it('shows focused card title when card is focused', () => {
    const { result } = renderHook(() => useCardHand());
    act(() => {
      result.current.drawCard({ id: 'g1', entity: 'game', title: 'Catan', href: '/library/g1' });
    });

    render(<UnifiedTopNav isAdmin={false} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('hides admin toggle for non-admin users', () => {
    render(<UnifiedTopNav isAdmin={false} />);
    expect(screen.queryByTestId('admin-toggle')).not.toBeInTheDocument();
  });

  it('shows admin toggle for admin users', () => {
    render(<UnifiedTopNav isAdmin={true} />);
    expect(screen.getByTestId('admin-toggle')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement `AdminToggle`**

```tsx
// apps/web/src/components/layout/UnifiedShell/AdminToggle.tsx
'use client';

import { Shield, Gamepad2 } from 'lucide-react';
import { useCardHand } from '@/stores/use-card-hand';
import { cn } from '@/lib/utils';

export function AdminToggle() {
  const { context, toggleContext } = useCardHand();
  const isAdmin = context === 'admin';

  return (
    <button
      type="button"
      onClick={toggleContext}
      className={cn(
        'flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-xs font-medium',
        'transition-all duration-200 border',
        isAdmin
          ? 'bg-destructive/10 text-destructive border-destructive/30'
          : 'bg-muted/50 text-muted-foreground border-border/30 hover:bg-muted'
      )}
      role="switch"
      aria-checked={isAdmin}
      aria-label={isAdmin ? 'Switch to user mode' : 'Switch to admin mode'}
      data-testid="admin-toggle"
    >
      {isAdmin ? (
        <>
          <Shield className="w-3.5 h-3.5" />
          <span className="hidden sm:inline">Admin</span>
        </>
      ) : (
        <>
          <Gamepad2 className="w-3.5 h-3.5" />
          <span className="hidden sm:inline">User</span>
        </>
      )}
    </button>
  );
}
```

- [ ] **Step 4: Implement `UnifiedTopNav`**

```tsx
// apps/web/src/components/layout/UnifiedShell/UnifiedTopNav.tsx
'use client';

import { Search, Bell } from 'lucide-react';
import Link from 'next/link';
import { ENTITY_NAV_ICONS } from '@/components/ui/data-display/meeple-card-features/navigation-icons';
import { entityColors } from '@/components/ui/data-display/meeple-card-styles';
import { useCardHand } from '@/stores/use-card-hand';
import { AdminToggle } from './AdminToggle';
import { cn } from '@/lib/utils';

interface UnifiedTopNavProps {
  isAdmin: boolean;
  /** Slot for user menu (passed from shell) */
  userMenu?: React.ReactNode;
  /** Slot for notification bell (passed from shell) */
  notificationBell?: React.ReactNode;
  /** Slot for command palette trigger */
  searchTrigger?: React.ReactNode;
}

export function UnifiedTopNav({
  isAdmin,
  userMenu,
  notificationBell,
  searchTrigger,
}: UnifiedTopNavProps) {
  const { cards, focusedIdx } = useCardHand();
  const focusedCard = focusedIdx >= 0 && focusedIdx < cards.length
    ? cards[focusedIdx]
    : null;

  const Icon = focusedCard
    ? (ENTITY_NAV_ICONS[focusedCard.entity] ?? ENTITY_NAV_ICONS.game)
    : null;
  const hsl = focusedCard
    ? (entityColors[focusedCard.entity]?.hsl ?? '220 70% 50%')
    : null;

  return (
    <header
      className={cn(
        'sticky top-0 z-40 h-14',
        'flex items-center justify-between px-4',
        'bg-background/95 backdrop-blur-xl',
        'border-b border-border/40'
      )}
      data-testid="unified-top-nav"
    >
      {/* Left: Logo */}
      <Link
        href="/"
        className="flex items-center gap-2 shrink-0"
      >
        <span className="text-lg font-bold font-quicksand">MeepleAI</span>
      </Link>

      {/* Center: Focused card title */}
      <div className="flex items-center gap-2 min-w-0 mx-4">
        {focusedCard && Icon ? (
          <>
            <div
              className="flex items-center justify-center w-6 h-6 rounded shrink-0 bg-[hsl(var(--card-hsl)/0.12)]"
              style={{ '--card-hsl': hsl } as React.CSSProperties}
            >
              <Icon className="w-3.5 h-3.5 text-[hsl(var(--card-hsl))]" />
            </div>
            <span className="text-sm font-medium truncate font-quicksand">
              {focusedCard.title}
            </span>
          </>
        ) : (
          <span className="text-sm font-medium text-muted-foreground font-quicksand">
            MeepleAI
          </span>
        )}
      </div>

      {/* Right: Admin toggle + utilities */}
      <div className="flex items-center gap-2 shrink-0">
        {isAdmin && <AdminToggle />}
        {searchTrigger ?? (
          <button
            type="button"
            className="flex items-center justify-center w-9 h-9 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors"
            aria-label="Search"
          >
            <Search className="w-4 h-4" />
          </button>
        )}
        {notificationBell ?? (
          <button
            type="button"
            className="flex items-center justify-center w-9 h-9 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors"
            aria-label="Notifications"
          >
            <Bell className="w-4 h-4" />
          </button>
        )}
        {userMenu}
      </div>
    </header>
  );
}
```

- [ ] **Step 5: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/UnifiedTopNav.test.tsx`
Expected: ALL PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/AdminToggle.tsx apps/web/src/components/layout/UnifiedShell/UnifiedTopNav.tsx apps/web/src/components/layout/UnifiedShell/__tests__/UnifiedTopNav.test.tsx
git commit -m "feat(nav): add UnifiedTopNav with focused card title and AdminToggle"
```

---

### Task 1.6: `ContextualBottomNav` Component

**Files:**
- Create: `apps/web/src/components/layout/UnifiedShell/ContextualBottomNav.tsx`
- Create: `apps/web/src/components/layout/UnifiedShell/ContextualBottomNavItem.tsx`
- Create: `apps/web/src/components/layout/UnifiedShell/__tests__/ContextualBottomNav.test.tsx`

- [ ] **Step 1: Write failing tests**

```typescript
// apps/web/src/components/layout/UnifiedShell/__tests__/ContextualBottomNav.test.tsx
import { render, screen } from '@testing-library/react';
import { act, renderHook } from '@testing-library/react';
import { ContextualBottomNav } from '../ContextualBottomNav';
import { useCardHand } from '@/stores/use-card-hand';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/',
}));

describe('ContextualBottomNav', () => {
  beforeEach(() => {
    const { result } = renderHook(() => useCardHand());
    act(() => result.current.clear());
  });

  it('shows default actions when no card focused', () => {
    render(<ContextualBottomNav />);
    expect(screen.getByText('Library')).toBeInTheDocument();
    expect(screen.getByText('Discover')).toBeInTheDocument();
    expect(screen.getByText('Chat')).toBeInTheDocument();
    expect(screen.getByText('Sessions')).toBeInTheDocument();
  });

  it('shows entity actions when game card focused', () => {
    const { result } = renderHook(() => useCardHand());
    act(() => {
      result.current.drawCard({ id: 'g1', entity: 'game', title: 'Catan', href: '/library/g1' });
    });

    render(<ContextualBottomNav />);
    expect(screen.getByText('Nuova Sessione')).toBeInTheDocument();
    expect(screen.getByText('Wishlist')).toBeInTheDocument();
    expect(screen.queryByText('Library')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement `ContextualBottomNavItem` + `ContextualBottomNav`**

```tsx
// apps/web/src/components/layout/UnifiedShell/ContextualBottomNavItem.tsx
'use client';

import type { LucideIcon } from 'lucide-react';
import { cn } from '@/lib/utils';

interface ContextualBottomNavItemProps {
  id: string;
  label: string;
  icon: LucideIcon;
  variant?: 'primary' | 'secondary' | 'ghost' | 'destructive';
  onClick: () => void;
}

export function ContextualBottomNavItem({
  label,
  icon: Icon,
  variant,
  onClick,
}: ContextualBottomNavItemProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'flex flex-col items-center gap-1 px-3 py-2 rounded-lg flex-1',
        'text-xs font-medium font-nunito',
        'transition-colors',
        variant === 'primary' && 'text-primary',
        variant === 'destructive' && 'text-destructive',
        (!variant || variant === 'secondary' || variant === 'ghost') && 'text-muted-foreground',
        'hover:bg-muted/50'
      )}
    >
      <Icon className="w-5 h-5" />
      <span className="truncate max-w-[80px]">{label}</span>
    </button>
  );
}
```

```tsx
// apps/web/src/components/layout/UnifiedShell/ContextualBottomNav.tsx
'use client';

import { useBottomNavActions } from '@/hooks/use-bottom-nav-actions';
import { ContextualBottomNavItem } from './ContextualBottomNavItem';
import { cn } from '@/lib/utils';

export function ContextualBottomNav() {
  const actions = useBottomNavActions();

  return (
    <nav
      className={cn(
        'sticky bottom-0 z-30',
        'flex items-center justify-around px-2 py-1',
        'bg-background/95 backdrop-blur-xl',
        'border-t border-border/40',
        'pb-[env(safe-area-inset-bottom)]'
      )}
      role="toolbar"
      aria-label="Page actions"
      data-testid="contextual-bottom-nav"
    >
      {actions.map((action) => (
        <ContextualBottomNavItem
          key={action.id}
          id={action.id}
          label={action.label}
          icon={action.icon}
          variant={action.variant}
          onClick={action.onClick}
        />
      ))}
    </nav>
  );
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/ContextualBottomNav.test.tsx`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/ContextualBottomNav.tsx apps/web/src/components/layout/UnifiedShell/ContextualBottomNavItem.tsx apps/web/src/components/layout/UnifiedShell/__tests__/ContextualBottomNav.test.tsx
git commit -m "feat(nav): add ContextualBottomNav with entity-specific actions"
```

---

### Task 1.7: `AdminTabSidebar` Component

**Files:**
- Create: `apps/web/src/components/layout/UnifiedShell/AdminTabSidebar.tsx`
- Create: `apps/web/src/components/layout/UnifiedShell/__tests__/AdminTabSidebar.test.tsx`

**Reference:** `apps/web/src/config/admin-dashboard-navigation.ts` for section/item structure, `apps/web/src/components/admin/layout/AdminContextualSidebar.tsx` for existing patterns

- [ ] **Step 1: Write failing tests**

```typescript
// apps/web/src/components/layout/UnifiedShell/__tests__/AdminTabSidebar.test.tsx
import { render, screen } from '@testing-library/react';
import { AdminTabSidebar } from '../AdminTabSidebar';

vi.mock('next/navigation', () => ({
  usePathname: () => '/admin/overview',
}));

describe('AdminTabSidebar', () => {
  it('renders all 6 admin sections', () => {
    render(<AdminTabSidebar />);
    expect(screen.getByText('Overview')).toBeInTheDocument();
    expect(screen.getByText('Content')).toBeInTheDocument();
    expect(screen.getByText('AI')).toBeInTheDocument();
    expect(screen.getByText('Users')).toBeInTheDocument();
    expect(screen.getByText('System')).toBeInTheDocument();
    expect(screen.getByText('Analytics')).toBeInTheDocument();
  });

  it('highlights the active section based on pathname', () => {
    render(<AdminTabSidebar />);
    const overviewTab = screen.getByTestId('admin-tab-overview');
    expect(overviewTab.dataset.active).toBe('true');
  });

  it('shows sub-items for the active section', () => {
    render(<AdminTabSidebar />);
    // Overview section should show its sidebar items
    expect(screen.getByText('Dashboard')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement `AdminTabSidebar`**

```tsx
// apps/web/src/components/layout/UnifiedShell/AdminTabSidebar.tsx
'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { DASHBOARD_SECTIONS, getActiveSection, isSidebarItemActive } from '@/config/admin-dashboard-navigation';
import { cn } from '@/lib/utils';

export function AdminTabSidebar() {
  const pathname = usePathname();
  const activeSection = getActiveSection(pathname);

  return (
    <div
      className={cn(
        'flex flex-col h-full w-[200px] border-r border-border/30',
        'bg-background/50 backdrop-blur-sm'
      )}
      data-testid="admin-tab-sidebar"
    >
      {/* Section tabs */}
      <div className="flex flex-col">
        {DASHBOARD_SECTIONS.map((section) => {
          const isActive = activeSection?.id === section.id;
          const Icon = section.icon;

          return (
            <div key={section.id}>
              <Link
                href={section.baseRoute}
                className={cn(
                  'flex items-center gap-2 px-3 py-2.5 text-sm font-medium',
                  'transition-colors border-l-2',
                  isActive
                    ? 'bg-primary/5 border-primary text-primary'
                    : 'border-transparent text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                )}
                data-testid={`admin-tab-${section.id}`}
                data-active={isActive ? 'true' : 'false'}
              >
                <Icon className="w-4 h-4 shrink-0" />
                <span className="font-quicksand">{section.label}</span>
              </Link>

              {/* Sub-items for active section */}
              {isActive && section.sidebarItems.length > 0 && (
                <div className="ml-4 border-l border-border/20 py-1">
                  {section.sidebarItems.map((item) => {
                    const ItemIcon = item.icon;
                    const isItemActive = isSidebarItemActive(item, pathname);

                    return (
                      <Link
                        key={item.href}
                        href={item.href}
                        className={cn(
                          'flex items-center gap-2 px-3 py-1.5 text-xs',
                          'transition-colors',
                          isItemActive
                            ? 'text-primary font-medium'
                            : 'text-muted-foreground hover:text-foreground'
                        )}
                      >
                        <ItemIcon className="w-3.5 h-3.5 shrink-0" />
                        <span className="font-nunito">{item.label}</span>
                      </Link>
                    );
                  })}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/AdminTabSidebar.test.tsx`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/AdminTabSidebar.tsx apps/web/src/components/layout/UnifiedShell/__tests__/AdminTabSidebar.test.tsx
git commit -m "feat(nav): add AdminTabSidebar with section tabs and sub-items"
```

---

### Task 1.8: `UnifiedShell` + `UnifiedShellClient`

**Files:**
- Create: `apps/web/src/components/layout/UnifiedShell/UnifiedShell.tsx`
- Create: `apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx`
- Create: `apps/web/src/components/layout/UnifiedShell/index.ts`
- Create: `apps/web/src/components/layout/UnifiedShell/__tests__/UnifiedShellClient.test.tsx`

**Reference:** `apps/web/src/components/layout/AppShell/AppShellClient.tsx` for provider wiring, banner integration, error boundaries

- [ ] **Step 1: Write failing tests**

```typescript
// apps/web/src/components/layout/UnifiedShell/__tests__/UnifiedShellClient.test.tsx
import { render, screen } from '@testing-library/react';
import { UnifiedShellClient } from '../UnifiedShellClient';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/library',
}));

vi.mock('@/stores/use-card-hand', () => ({
  useCardHand: () => ({
    cards: [],
    focusedIdx: -1,
    pinnedIds: new Set(),
    context: 'user',
    expandedStack: false,
    highlightEntity: null,
    drawCard: vi.fn(),
    discardCard: vi.fn(),
    focusCard: vi.fn(),
    focusByHref: vi.fn(),
    pinCard: vi.fn(),
    unpinCard: vi.fn(),
    swipeNext: vi.fn(),
    swipePrev: vi.fn(),
    toggleContext: vi.fn(),
    toggleExpandStack: vi.fn(),
    setHighlightEntity: vi.fn(),
    clear: vi.fn(),
    maxHandSize: 10,
  }),
}));

describe('UnifiedShellClient', () => {
  it('renders top nav, left panel, main content, and bottom nav', () => {
    render(
      <UnifiedShellClient isAdmin={false}>
        <div data-testid="page-content">Hello</div>
      </UnifiedShellClient>
    );

    expect(screen.getByTestId('unified-top-nav')).toBeInTheDocument();
    expect(screen.getByTestId('card-stack')).toBeInTheDocument();
    expect(screen.getByTestId('page-content')).toBeInTheDocument();
    expect(screen.getByTestId('contextual-bottom-nav')).toBeInTheDocument();
  });

  it('renders admin tab sidebar when context is admin', () => {
    vi.mocked(require('@/stores/use-card-hand').useCardHand).mockReturnValue({
      ...require('@/stores/use-card-hand').useCardHand(),
      context: 'admin',
    });

    render(
      <UnifiedShellClient isAdmin={true}>
        <div>Admin content</div>
      </UnifiedShellClient>
    );

    expect(screen.getByTestId('admin-tab-sidebar')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement `UnifiedShellClient`**

```tsx
// apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx
'use client';

import { type ReactNode } from 'react';
import { ErrorBoundary } from '@/components/errors/ErrorBoundary';
import { useCardHand } from '@/stores/use-card-hand';
import { UnifiedTopNav } from './UnifiedTopNav';
import { CardStack } from './CardStack';
import { AdminTabSidebar } from './AdminTabSidebar';
import { ContextualBottomNav } from './ContextualBottomNav';
import { cn } from '@/lib/utils';

interface UnifiedShellClientProps {
  children: ReactNode;
  isAdmin: boolean;
  /** Slot for ImpersonationBanner */
  impersonationBanner?: ReactNode;
  /** Slot for OnboardingReminderBanner */
  onboardingBanner?: ReactNode;
  /** Slot for user menu */
  userMenu?: ReactNode;
  /** Slot for notification bell */
  notificationBell?: ReactNode;
  /** Slot for command palette trigger */
  searchTrigger?: ReactNode;
}

export function UnifiedShellClient({
  children,
  isAdmin,
  impersonationBanner,
  onboardingBanner,
  userMenu,
  notificationBell,
  searchTrigger,
}: UnifiedShellClientProps) {
  const { context } = useCardHand();
  const isAdminContext = context === 'admin';

  return (
    <div className="flex flex-col h-screen">
      {/* Impersonation banner (above everything) */}
      {impersonationBanner}

      {/* Top Nav */}
      <ErrorBoundary fallback={null} componentName="UnifiedTopNav">
        <UnifiedTopNav
          isAdmin={isAdmin}
          userMenu={userMenu}
          notificationBell={notificationBell}
          searchTrigger={searchTrigger}
        />
      </ErrorBoundary>

      {/* Onboarding banner */}
      {onboardingBanner}

      {/* Main body: left panel + content */}
      <div className="flex flex-1 overflow-hidden">
        {/* Left panel: CardStack (user) or AdminTabSidebar (admin) */}
        <ErrorBoundary fallback={null} componentName="LeftPanel">
          {isAdminContext ? (
            <AdminTabSidebar />
          ) : (
            <CardStack />
          )}
        </ErrorBoundary>

        {/* Main content area */}
        <main
          id="main-content"
          className={cn(
            'flex-1 overflow-y-auto',
            'focus:outline-none'
          )}
          tabIndex={-1}
        >
          <ErrorBoundary componentName="PageContent">
            {children}
          </ErrorBoundary>
        </main>
      </div>

      {/* Bottom Nav */}
      <ErrorBoundary fallback={null} componentName="ContextualBottomNav">
        <ContextualBottomNav />
      </ErrorBoundary>
    </div>
  );
}
```

- [ ] **Step 4: Implement RSC wrapper `UnifiedShell`**

```tsx
// apps/web/src/components/layout/UnifiedShell/UnifiedShell.tsx
import { type ReactNode } from 'react';
import { cookies } from 'next/headers';
import { UnifiedShellClient } from './UnifiedShellClient';

interface UnifiedShellProps {
  children: ReactNode;
  isAdmin?: boolean;
  impersonationBanner?: ReactNode;
  onboardingBanner?: ReactNode;
  userMenu?: ReactNode;
  notificationBell?: ReactNode;
  searchTrigger?: ReactNode;
}

export async function UnifiedShell({
  children,
  isAdmin = false,
  ...clientProps
}: UnifiedShellProps) {
  // Read server-side cookie for initial stack expansion state (prevents flash)
  const cookieStore = await cookies();
  const initialStackExpanded = cookieStore.get('card-stack-expanded')?.value === 'true';

  return (
    <UnifiedShellClient
      isAdmin={isAdmin}
      initialStackExpanded={initialStackExpanded}
      {...clientProps}
    >
      {children}
    </UnifiedShellClient>
  );
}
```

- [ ] **Step 5: Create barrel export**

```typescript
// apps/web/src/components/layout/UnifiedShell/index.ts
export { UnifiedShell } from './UnifiedShell';
export { UnifiedShellClient } from './UnifiedShellClient';
export { CardStack } from './CardStack';
export { CardStackItem } from './CardStackItem';
export { UnifiedTopNav } from './UnifiedTopNav';
export { AdminTabSidebar } from './AdminTabSidebar';
export { AdminToggle } from './AdminToggle';
export { ContextualBottomNav } from './ContextualBottomNav';
```

- [ ] **Step 6: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/UnifiedShellClient.test.tsx`
Expected: ALL PASS

- [ ] **Step 7: Run all UnifiedShell tests together**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/`
Expected: ALL PASS

- [ ] **Step 8: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 9: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/
git commit -m "feat(nav): add UnifiedShell with CardStack, AdminTabSidebar, and ContextualBottomNav"
```

- [ ] **Step 10: Create PR**

```bash
git push -u origin feature/carte-in-mano-foundation
gh pr create --title "feat(nav): Carte in Mano foundation — new shell components" --body "$(cat <<'EOF'
## Summary
- New `useCardHand` Zustand store (merges useHandContext + useNavigationTrail)
- Entity action registry (`config/entity-actions.ts`) with per-entity bottom nav actions
- `useBottomNavActions` hook for contextual bottom nav
- `CardStack` + `CardStackItem` — left panel with Mini/Card levels, pinned/dynamic sections
- `UnifiedTopNav` — minimal top nav with focused card title + admin toggle
- `AdminTabSidebar` — 6-section tabbed sidebar for admin context
- `ContextualBottomNav` — entity-specific actions bottom bar
- `UnifiedShell` RSC + `UnifiedShellClient` — unified layout replacing AppShell + AdminShell

Both old and new layout systems coexist — no breaking changes.

## Test plan
- [ ] All new unit tests pass (`pnpm vitest run src/components/layout/UnifiedShell/__tests__/`)
- [ ] TypeScript typecheck passes
- [ ] No regressions in existing tests

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)" --base main-dev
```

---

## Chunk 2: PR 2 — User Layout Swap

**Branch:** `feature/carte-in-mano-user-swap`
**Parent:** `feature/carte-in-mano-foundation` (or `main-dev` after PR 1 merges)
**Goal:** Replace authenticated + chat layouts with UnifiedShell. Delete user NavConfigs.

### Task 2.1: Swap Authenticated Layout

**Files:**
- Modify: `apps/web/src/app/(authenticated)/layout.tsx`

- [ ] **Step 1: Read current authenticated layout**

Run: Read `apps/web/src/app/(authenticated)/layout.tsx` to understand current AppShell usage and props

- [ ] **Step 2: Replace with UnifiedShell**

Replace the existing layout content. Keep any existing server-side logic (session checks, redirects) but swap AppShell → UnifiedShell. Pass `isAdmin` based on user role from session.

- [ ] **Step 3: Run typecheck**

Run: `cd apps/web && pnpm typecheck`

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/(authenticated)/layout.tsx
git commit -m "feat(nav): swap authenticated layout to UnifiedShell"
```

### Task 2.2: Swap Chat Layout

**Files:**
- Modify: `apps/web/src/app/(chat)/layout.tsx`

- [ ] **Step 1: Read current chat layout**
- [ ] **Step 2: Replace with UnifiedShell**
- [ ] **Step 3: Run typecheck**
- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/(chat)/layout.tsx
git commit -m "feat(nav): swap chat layout to UnifiedShell"
```

### Task 2.3: Add `drawCard` Calls to User Pages

**Files to modify** (add `drawCard` useEffect to each page/client component):
- `apps/web/src/app/(authenticated)/library/page.tsx` or client component
- `apps/web/src/app/(authenticated)/library/[gameId]/page.tsx` or client component
- `apps/web/src/app/(authenticated)/dashboard/page.tsx` or client component
- `apps/web/src/app/(authenticated)/agents/page.tsx` or client component
- `apps/web/src/app/(authenticated)/sessions/page.tsx` or client component
- `apps/web/src/app/(authenticated)/sessions/[id]/page.tsx` or client component
- `apps/web/src/app/(authenticated)/profile/page.tsx` or client component
- `apps/web/src/app/(authenticated)/discover/page.tsx` or client component
- `apps/web/src/app/(authenticated)/game-nights/page.tsx` or client component
- `apps/web/src/app/(chat)/chat/page.tsx` or client component

**Pattern for each page:**

- [ ] **Step 1: Read the page to find client component and available entity data**
- [ ] **Step 2: Add drawCard call**

```typescript
// Example for a game detail page
import { useCardHand } from '@/stores/use-card-hand';

// Inside the client component:
const { drawCard } = useCardHand();

useEffect(() => {
  drawCard({
    id: game.id,
    entity: 'game',
    title: game.title,
    href: `/library/${game.id}`,
    subtitle: game.publisher,
  });
}, [game.id, drawCard]);
```

For section index pages (library, discover, sessions, etc.):
```typescript
useEffect(() => {
  drawCard({
    id: 'section-library',
    entity: 'game',
    title: 'Library',
    href: '/library',
  });
}, [drawCard]);
```

- [ ] **Step 3: Run typecheck after all pages updated**

Run: `cd apps/web && pnpm typecheck`

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/ apps/web/src/app/\(chat\)/
git commit -m "feat(nav): add drawCard calls to all user page components"
```

### Task 2.4: Delete User NavConfig Components

**Files to delete:**
- `apps/web/src/app/(authenticated)/agents/NavConfig.tsx`
- `apps/web/src/app/(authenticated)/dashboard/NavConfig.tsx`
- `apps/web/src/app/(authenticated)/game-nights/NavConfig.tsx`
- `apps/web/src/app/(authenticated)/library/NavConfig.tsx`
- `apps/web/src/app/(authenticated)/profile/NavConfig.tsx`
- `apps/web/src/app/(chat)/chat/NavConfig.tsx`

**Files to modify** (remove NavConfig import/usage from layouts):
- `apps/web/src/app/(authenticated)/agents/layout.tsx`
- `apps/web/src/app/(authenticated)/dashboard/layout.tsx`
- `apps/web/src/app/(authenticated)/game-nights/layout.tsx`
- `apps/web/src/app/(authenticated)/library/layout.tsx`
- `apps/web/src/app/(authenticated)/profile/layout.tsx`
- `apps/web/src/app/(chat)/chat/layout.tsx`

- [ ] **Step 1: Delete NavConfig files**

```bash
rm apps/web/src/app/\(authenticated\)/agents/NavConfig.tsx
rm apps/web/src/app/\(authenticated\)/dashboard/NavConfig.tsx
rm apps/web/src/app/\(authenticated\)/game-nights/NavConfig.tsx
rm apps/web/src/app/\(authenticated\)/library/NavConfig.tsx
rm apps/web/src/app/\(authenticated\)/profile/NavConfig.tsx
rm apps/web/src/app/\(chat\)/chat/NavConfig.tsx
```

- [ ] **Step 2: Remove NavConfig imports from each section layout**

For each layout.tsx, remove the `<XxxNavConfig />` component rendering and its import.

- [ ] **Step 3: Run typecheck**

Run: `cd apps/web && pnpm typecheck`

- [ ] **Step 4: Run existing tests to check for breakage**

Run: `cd apps/web && pnpm vitest run`

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(nav): remove user NavConfig components (replaced by entity action registry)"
```

### Task 2.5: Initialize Default Pinned Cards

**Files:**
- Modify: `apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx`

- [ ] **Step 1: Add pinned card initialization on mount**

In `UnifiedShellClient`, add a `useEffect` that draws and pins the default section cards on first load (if hand is empty):

```typescript
import { DEFAULT_PINNED_CARDS } from '@/config/entity-actions';

// Inside UnifiedShellClient:
const { cards, drawCard, pinCard } = useCardHand();

useEffect(() => {
  if (cards.length === 0) {
    DEFAULT_PINNED_CARDS.forEach((card) => {
      drawCard(card);
      pinCard(card.id);
    });
  }
  // Only run on mount
  // eslint-disable-next-line react-hooks/exhaustive-deps
}, []);
```

- [ ] **Step 2: Run typecheck + tests**
- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx
git commit -m "feat(nav): initialize default pinned cards on first load"
```

- [ ] **Step 4: Create PR**

```bash
git push -u origin feature/carte-in-mano-user-swap
gh pr create --title "feat(nav): swap user layouts to UnifiedShell" --body "$(cat <<'EOF'
## Summary
- Authenticated layout now uses UnifiedShell
- Chat layout now uses UnifiedShell
- All user pages draw cards on mount via useCardHand
- 6 NavConfig files deleted (replaced by centralized entity-actions.ts)
- Default pinned cards (Library, Discover, Chat, Sessions) initialized on first load

## Test plan
- [ ] All user routes render correctly with new shell
- [ ] Card stack shows pinned cards on first load
- [ ] Clicking a game draws a card and focuses it
- [ ] Bottom nav shows entity-specific actions when card focused
- [ ] Mobile: mini-stack visible, expand works
- [ ] No regressions in existing tests

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)" --base main-dev
```

---

## Chunk 3: PR 3 — Admin Layout Swap

**Branch:** `feature/carte-in-mano-admin-swap`
**Parent:** `main-dev` (after PR 2 merges)
**Goal:** Replace admin dashboard layout with UnifiedShell in admin context. Delete admin NavConfigs.

### Task 3.1: Swap Admin Dashboard Layout

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/layout.tsx`

- [ ] **Step 1: Read current admin layout**
- [ ] **Step 2: Replace AdminShell with UnifiedShell, passing `isAdmin={true}`**

The layout should force admin context on mount:
```typescript
const { toggleContext, context } = useCardHand();
useEffect(() => {
  if (context !== 'admin') toggleContext();
}, []);
```

- [ ] **Step 3: Run typecheck**
- [ ] **Step 4: Commit**

### Task 3.2: Add Admin Section Actions to Navigation Config

**Files:**
- Modify: `apps/web/src/config/admin-dashboard-navigation.ts`

- [ ] **Step 1: Add `actions` field to `DashboardSection` interface**

```typescript
interface DashboardSection {
  // ... existing fields
  actions?: BottomNavActionDef[];  // NEW: contextual bottom nav actions for this section
}
```

- [ ] **Step 2: Add actions per section** (based on existing admin NavConfig definitions)
- [ ] **Step 3: Commit**

### Task 3.3: Delete Admin NavConfig Components

**Files to delete (10 total):**
- `apps/web/src/app/admin/(dashboard)/agents/NavConfig.tsx`
- `apps/web/src/app/admin/(dashboard)/ai/NavConfig.tsx`
- `apps/web/src/app/admin/(dashboard)/analytics/NavConfig.tsx`
- `apps/web/src/app/admin/(dashboard)/config/NavConfig.tsx`
- `apps/web/src/app/admin/(dashboard)/content/NavConfig.tsx`
- `apps/web/src/app/admin/(dashboard)/monitor/NavConfig.tsx`
- `apps/web/src/app/admin/(dashboard)/monitor/containers/NavConfig.tsx`
- `apps/web/src/app/admin/(dashboard)/monitor/logs/NavConfig.tsx`
- `apps/web/src/app/admin/(dashboard)/monitor/operations/NavConfig.tsx`
- `apps/web/src/app/admin/(dashboard)/monitor/services/NavConfig.tsx`

**Files to modify** (remove NavConfig from admin section layouts):
- `apps/web/src/app/admin/(dashboard)/overview/layout.tsx` (if it uses NavConfig)
- Any admin layout importing NavConfig

- [ ] **Step 1: Delete all admin NavConfig files**
- [ ] **Step 2: Remove NavConfig imports from admin layouts**
- [ ] **Step 3: Run typecheck + tests**
- [ ] **Step 4: Commit + Create PR**

```bash
gh pr create --title "feat(nav): swap admin layout to UnifiedShell" --body "$(cat <<'EOF'
## Summary
- Admin dashboard layout now uses UnifiedShell with admin context
- AdminTabSidebar renders 6 sections with sub-items
- Admin section actions migrated to admin-dashboard-navigation.ts
- 10 admin NavConfig files deleted

## Test plan
- [ ] Admin routes render correctly with new shell
- [ ] Admin tab sidebar shows all 6 sections
- [ ] Active section highlighted based on URL
- [ ] Sub-items visible for active section
- [ ] Admin toggle switches context
- [ ] Bottom nav shows admin section-specific actions
- [ ] No regressions in existing admin tests

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)" --base main-dev
```

---

## Chunk 4: PR 4 — Cleanup

**Branch:** `feature/carte-in-mano-cleanup`
**Parent:** `main-dev` (after PR 3 merges)
**Goal:** Delete all old layout components, hooks, and tests. Zero dead code.

### Task 4.1: Delete Old Layout Components

- [ ] **Step 1: Delete old shell components**

```bash
rm -rf apps/web/src/components/layout/AppShell/
rm -rf apps/web/src/components/layout/LayoutShell/
rm -rf apps/web/src/components/layout/Sidebar/
rm -rf apps/web/src/components/layout/ActionBar/
rm -rf apps/web/src/components/layout/FloatingActionBar/
rm -rf apps/web/src/components/layout/AdaptiveBottomBar/
rm -rf apps/web/src/components/layout/Breadcrumb/
rm -rf apps/web/src/components/layout/TopNavbar/
rm apps/web/src/components/layout/MobileNavDrawer.tsx
```

> **Note:** TopNavbar/ is deleted because UnifiedTopNav replaces it. The Logo component should be extracted into UnifiedTopNav or a shared location if needed by public layouts.

- [ ] **Step 2: Delete old admin layout components**

```bash
rm apps/web/src/components/admin/layout/AdminShell.tsx
rm apps/web/src/components/admin/layout/AdminTopNav.tsx
rm apps/web/src/components/admin/layout/AdminContextualSidebar.tsx
rm apps/web/src/components/admin/layout/AdminMobileNav.tsx
rm apps/web/src/components/admin/layout/AdminMobileTabBar.tsx
```

- [ ] **Step 3: Delete old card stack panel (right-side)**

```bash
rm apps/web/src/components/ui/navigation/card-stack-panel.tsx
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor(nav): delete old layout shell components"
```

### Task 4.2: Delete Old Hooks

- [ ] **Step 1: Remove useSetNavConfig from admin pages that use it directly**

These admin pages import `useSetNavConfig` outside of NavConfig components:
- `apps/web/src/app/admin/(dashboard)/users/page.tsx`
- `apps/web/src/app/admin/(dashboard)/users/invitations/page.tsx`

Read each file, remove the `useSetNavConfig` import and `useEffect` call. These pages' actions are now handled by the admin section action config in `admin-dashboard-navigation.ts`.

Also update the test: `apps/web/src/app/admin/(dashboard)/users/invitations/__tests__/page.test.tsx` — remove the `useSetNavConfig` mock.

- [ ] **Step 2: Delete hooks**

```bash
rm apps/web/src/hooks/use-navigation-trail.ts
rm apps/web/src/hooks/use-hand-context.ts
rm apps/web/src/hooks/useSetNavConfig.ts
rm apps/web/src/hooks/useSidebarState.ts
rm apps/web/src/hooks/__tests__/useSidebarState.test.ts
```

Note: Check if `useSidebarState` is still imported anywhere before deleting (it's used by PublicLayout). Run:
```bash
cd apps/web && grep -r "useSidebarState" src/ --include="*.ts" --include="*.tsx"
```
If PublicLayout imports it, remove that import and any sidebar collapse logic from the public layout.

- [ ] **Step 3: Clean NavigationContext**

Read `apps/web/src/context/NavigationContext.tsx` — remove `useSetNavConfig` export and related types/state if no longer needed. Also delete `apps/web/src/context/__tests__/NavigationContext.test.tsx` if it only tests the old NavConfig pattern.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "refactor(nav): delete old navigation hooks (useSetNavConfig, useNavigationTrail, useHandContext)"
```

### Task 4.3: Delete Old Tests

- [ ] **Step 1: Delete test files for removed components**

```bash
rm apps/web/src/components/layout/__tests__/AppShellClient.test.tsx
rm apps/web/src/components/layout/__tests__/LayoutShell.test.tsx
rm apps/web/src/components/layout/__tests__/LayoutShell.responsive.test.tsx
rm apps/web/src/components/layout/__tests__/MobileNavDrawer.test.tsx
rm apps/web/src/components/layout/__tests__/AdaptiveBottomBar.test.tsx
rm apps/web/src/components/layout/ActionBar/__tests__/NavActionBar.test.tsx
rm apps/web/src/components/layout/Sidebar/__tests__/Sidebar.test.tsx
rm apps/web/src/components/layout/Sidebar/__tests__/SidebarContextNavTabs.test.tsx
rm apps/web/src/components/layout/FloatingActionBar/__tests__/FloatingActionBar.test.tsx
rm apps/web/src/components/admin/__tests__/AdminLayout.test.tsx
rm apps/web/src/components/admin/__tests__/AdminSidebar.test.tsx
rm apps/web/src/components/admin/__tests__/AdminHeader.test.tsx
rm apps/web/src/components/admin/layout/__tests__/AdminMobileTabBar.test.tsx
rm apps/web/src/components/ui/navigation/__tests__/card-focus-layout.test.tsx
rm apps/web/src/components/ui/navigation/__tests__/hand-stack.test.tsx
```

- [ ] **Step 2: Commit**

```bash
git add -A
git commit -m "refactor(nav): delete old layout and navigation test files"
```

### Task 4.4: Fix Remaining Imports and Dead Code

- [ ] **Step 1: Run typecheck to find broken imports**

Run: `cd apps/web && pnpm typecheck`

Fix any remaining imports referencing deleted components/hooks.

- [ ] **Step 2: Search for dead imports**

```bash
cd apps/web && grep -r "AppShell\|AdminShell\|SidebarContextNav\|MobileNavDrawer\|FloatingActionBar\|AdaptiveBottomBar\|NavActionBar\|useSetNavConfig\|useNavigationTrail\|useHandContext\|CardStackPanel" src/ --include="*.ts" --include="*.tsx" -l
```

Fix any found references.

- [ ] **Step 3: Run full test suite**

Run: `cd apps/web && pnpm vitest run`

- [ ] **Step 4: Commit + Create PR**

```bash
git add -A
git commit -m "refactor(nav): fix remaining imports after old layout removal"
gh pr create --title "refactor(nav): remove old layout components and hooks" --body "$(cat <<'EOF'
## Summary
- Deleted 20+ old layout components (AppShell, Sidebar, AdminShell, ActionBars, etc.)
- Deleted 3 old hooks (useSetNavConfig, useNavigationTrail, useHandContext)
- Deleted 15 old test files
- Fixed all remaining imports

## Test plan
- [ ] TypeScript typecheck passes with zero errors
- [ ] Full test suite passes
- [ ] No dead imports remain (grep verification)
- [ ] App builds successfully (`pnpm build`)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)" --base main-dev
```

---

## Chunk 5: PR 5 — Test Suite

**Branch:** `feature/carte-in-mano-tests`
**Parent:** `main-dev` (after PR 4 merges)
**Goal:** Comprehensive E2E and integration tests for the new navigation system.

### Task 5.1: E2E Test — Card Drawing and Focus

**Files:**
- Create: `apps/web/e2e/carte-in-mano/card-navigation.spec.ts`

- [ ] **Step 1: Write E2E tests**

```typescript
// apps/web/e2e/carte-in-mano/card-navigation.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Carte in Mano — Card Navigation', () => {
  test.beforeEach(async ({ page }) => {
    // Mock auth via bypass
    await page.context().route('**/api/v1/auth/session', async (route) => {
      await route.fulfill({
        status: 200,
        json: { user: { id: 'u1', displayName: 'Test User', role: 'User' }, isAuthenticated: true },
      });
    });
  });

  test('shows default pinned cards on first load', async ({ page }) => {
    await page.goto('/library');
    const cardStack = page.locator('[data-testid="card-stack"]');
    await expect(cardStack).toBeVisible();

    const pinnedSection = page.locator('[data-testid="card-stack-pinned"]');
    await expect(pinnedSection.locator('button')).toHaveCount(4); // Library, Discover, Chat, Sessions
  });

  test('draws a card when navigating to a game', async ({ page }) => {
    await page.goto('/library');
    // Navigate to a game (mock game data)
    await page.context().route('**/api/v1/games/*', async (route) => {
      await route.fulfill({
        status: 200,
        json: { id: 'g1', title: 'Catan', publisher: 'Kosmos' },
      });
    });

    // Card should appear in dynamic section
    const dynamicSection = page.locator('[data-testid="card-stack-dynamic"]');
    await expect(dynamicSection.locator('button').first()).toBeVisible();
  });

  test('bottom nav shows entity actions when card focused', async ({ page }) => {
    await page.goto('/library');
    const bottomNav = page.locator('[data-testid="contextual-bottom-nav"]');

    // Default: section actions
    await expect(bottomNav.getByText('Library')).toBeVisible();

    // After focusing a game card: entity actions
    // (depends on actual page drawing card)
  });

  test('clicking card in stack focuses it', async ({ page }) => {
    await page.goto('/library');
    // Verify focus changes when clicking stack items
  });
});
```

- [ ] **Step 2: Commit**

### Task 5.2: E2E Test — Admin Toggle

**Files:**
- Create: `apps/web/e2e/carte-in-mano/admin-toggle.spec.ts`

- [ ] **Step 1: Write E2E tests for admin context switching**

```typescript
test.describe('Carte in Mano — Admin Toggle', () => {
  test.beforeEach(async ({ page }) => {
    await page.context().route('**/api/v1/auth/session', async (route) => {
      await route.fulfill({
        status: 200,
        json: { user: { id: 'u1', displayName: 'Admin', role: 'Admin' }, isAuthenticated: true },
      });
    });
  });

  test('admin toggle visible for admin users', async ({ page }) => {
    await page.goto('/library');
    await expect(page.locator('[data-testid="admin-toggle"]')).toBeVisible();
  });

  test('switching to admin shows tabbed sidebar', async ({ page }) => {
    await page.goto('/library');
    await page.locator('[data-testid="admin-toggle"]').click();
    await expect(page.locator('[data-testid="admin-tab-sidebar"]')).toBeVisible();
    await expect(page.locator('[data-testid="card-stack"]')).not.toBeVisible();
  });

  test('admin toggle hidden for non-admin users', async ({ page }) => {
    await page.context().route('**/api/v1/auth/session', async (route) => {
      await route.fulfill({
        status: 200,
        json: { user: { id: 'u2', displayName: 'User', role: 'User' }, isAuthenticated: true },
      });
    });
    await page.goto('/library');
    await expect(page.locator('[data-testid="admin-toggle"]')).not.toBeVisible();
  });
});
```

- [ ] **Step 2: Commit**

### Task 5.3: E2E Test — Mobile Responsive

**Files:**
- Create: `apps/web/e2e/carte-in-mano/mobile-responsive.spec.ts`

- [ ] **Step 1: Write mobile viewport tests**

```typescript
test.describe('Carte in Mano — Mobile', () => {
  test.use({ viewport: { width: 375, height: 812 } }); // iPhone viewport

  test('card stack shows mini level (56px)', async ({ page }) => {
    await page.goto('/library');
    const stack = page.locator('[data-testid="card-stack"]');
    const box = await stack.boundingBox();
    expect(box?.width).toBeLessThanOrEqual(60);
  });

  test('expand button widens card stack', async ({ page }) => {
    await page.goto('/library');
    await page.locator('[aria-label="Expand card stack"]').click();
    const stack = page.locator('[data-testid="card-stack"]');
    const box = await stack.boundingBox();
    expect(box?.width).toBeGreaterThanOrEqual(170);
  });

  test('bottom nav is visible and contextual', async ({ page }) => {
    await page.goto('/library');
    await expect(page.locator('[data-testid="contextual-bottom-nav"]')).toBeVisible();
  });
});
```

- [ ] **Step 2: Commit**

### Task 5.4: Accessibility Tests

**Files:**
- Create: `apps/web/e2e/carte-in-mano/accessibility.spec.ts`

- [ ] **Step 1: Write keyboard and ARIA tests**

```typescript
test.describe('Carte in Mano — Accessibility', () => {
  test('card stack has navigation role', async ({ page }) => {
    await page.goto('/library');
    const stack = page.locator('[data-testid="card-stack"]');
    // Check ARIA attributes exist on parent navigation wrapper
  });

  test('admin toggle has switch role', async ({ page }) => {
    // Mock admin user
    await page.goto('/library');
    const toggle = page.locator('[data-testid="admin-toggle"]');
    await expect(toggle).toHaveAttribute('role', 'switch');
  });

  test('bottom nav has toolbar role', async ({ page }) => {
    await page.goto('/library');
    const nav = page.locator('[data-testid="contextual-bottom-nav"]');
    await expect(nav).toHaveAttribute('role', 'toolbar');
  });
});
```

- [ ] **Step 2: Run all E2E tests**

Run: `cd apps/web && pnpm test:e2e --grep "carte-in-mano"`

- [ ] **Step 3: Commit + Create PR**

```bash
git add -A
git commit -m "test(nav): add E2E tests for Carte in Mano navigation system"
gh pr create --title "test(nav): comprehensive E2E tests for Carte in Mano" --body "$(cat <<'EOF'
## Summary
- E2E tests for card drawing, focusing, and bottom nav context switching
- E2E tests for admin toggle (show/hide, context switch)
- Mobile responsive tests (mini stack, expand, bottom nav)
- Accessibility tests (ARIA roles, keyboard navigation)

## Test plan
- [ ] All E2E tests pass in Chromium
- [ ] Tests cover mobile and desktop viewports
- [ ] Accessibility assertions verified

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)" --base main-dev
```

---

## Summary

| PR | Branch | Tasks | Est. Files Changed |
|----|--------|-------|--------------------|
| **PR 1: Foundation** | `feature/carte-in-mano-foundation` | 8 tasks | ~15 new files |
| **PR 2: User Swap** | `feature/carte-in-mano-user-swap` | 5 tasks | ~20 modified, 6 deleted |
| **PR 3: Admin Swap** | `feature/carte-in-mano-admin-swap` | 3 tasks | ~12 modified, 10 deleted |
| **PR 4: Cleanup** | `feature/carte-in-mano-cleanup` | 4 tasks | ~40 deleted |
| **PR 5: Tests** | `feature/carte-in-mano-tests` | 4 tasks | ~4 new E2E files |

**Total**: 24 tasks, ~15 new + 32 modified + 56 deleted files

**Execution order**: PR 1 → PR 2 → PR 3 → PR 4 → PR 5 (sequential, each depends on previous)

## Known Gaps (to address during implementation)

1. **Keyboard navigation shortcuts** (`Alt+1..9`, `Alt+Up/Down`, `Escape`) — spec defines these but plan omits implementation. Add a `useCardHandKeyboard` hook during PR1 Task 1.8 and E2E keyboard tests in PR5.
2. **`aria-live` region for announcements** — spec requires screen reader announcements for card draw/discard/focus/eviction. Add an `aria-live="polite"` region to `UnifiedShellClient` and announce card state changes. Implement during PR1 Task 1.8.
3. **Entity action onClick handlers** — `useBottomNavActions` returns entity actions but non-route actions (e.g. "Nuova Sessione", "Wishlist") have no `onClick` in PR1. These require page-level action registration in PR2 (e.g. a callback registry in the `useCardHand` store or a context provider). Note: this is a known limitation of the centralized registry — some actions need page-specific handlers.
4. **`initialStackExpanded` prop** — `UnifiedShell` RSC reads cookie and passes to client. The `UnifiedShellClient` interface needs this prop added, and `useCardHand` should accept it as initial state to prevent hydration mismatch.
5. **Logo component extraction** — `TopNavbar/Logo.tsx` is deleted with the directory. If the public layout or auth layout uses Logo, extract it to a shared location (e.g. `components/ui/brand/Logo.tsx`) before deletion in PR4.
