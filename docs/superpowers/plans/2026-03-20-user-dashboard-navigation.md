# User Dashboard & Navigation System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign MeepleAI's dashboard and navigation — navbar (48px) + dynamic context bar (44px), card-based dashboard with Tavolo/Hub dual styling, overlay hybrid (bottom sheet/side panel), CardDeck swipeable component, and live session fast navigation.

**Architecture:** Layered build — foundation (CSS tokens, Zustand stores) -> shell migration (retire sidebar/tabbar, add ContextBar) -> reusable primitives (OverlayHybrid, CardDeck) -> page integration (dashboard zones, context bars, live session) -> polish (onboarding, skeletons).

**Tech Stack:** Next.js 16 App Router, React 19, Tailwind 4, Zustand, TypeScript, Vitest

**Spec:** `docs/superpowers/specs/2026-03-20-user-dashboard-navigation-design.md`

---

## File Structure

### New Files

```
apps/web/src/
├── lib/stores/
│   ├── context-bar-store.ts          # Zustand store for context bar content + options
│   └── overlay-store.ts              # Zustand store for overlay state + URL sync
├── hooks/
│   └── useNavigation.ts              # MODIFY existing: add breadcrumb logic (already exists at this path)
├── components/layout/ContextBar/
│   ├── ContextBar.tsx                # Shell: reads store, handles scroll hide, renders content
│   ├── ContextBarRegistrar.tsx       # Client component: registers content in store on mount
│   └── index.ts                      # Barrel export
├── components/ui/overlays/
│   ├── OverlayHybrid.tsx             # Bottom sheet (mobile) / side panel (desktop)
│   ├── OverlayHeader.tsx             # Shared header: entity icon, title, close button
│   ├── OverlayFooter.tsx             # Shared footer: deck nav dots, expand button
│   └── index.ts
├── components/ui/data-display/card-deck/
│   ├── CardDeck.tsx                  # Swipeable card navigator
│   ├── MiniCardDeck.tsx              # Inline stacked cover preview
│   ├── useCardDeckGestures.ts        # Touch/swipe gesture handler
│   └── index.ts
├── components/dashboard/
│   ├── TavoloZone.tsx                # Warm Tavolo-styled container
│   ├── zones/
│   │   ├── ActiveSessionZone.tsx     # Zona 2: session cards
│   │   ├── GameNightZone.tsx         # Zona 3: event cards
│   │   ├── AgentZone.tsx             # Zona 4: agent cards
│   │   ├── StatsZone.tsx             # Zona 5: StatCard grid
│   │   ├── FeedZone.tsx              # Zona 6: mixed entity feed
│   │   └── SuggestedZone.tsx         # Zona 7: AI suggestions carousel
│   ├── DashboardContextBar.tsx       # Quick actions + global search
│   └── OnboardingFlow.tsx            # First-visit onboarding cards (SSR-safe)
├── components/library/
│   └── LibraryContextBar.tsx         # Filters, sort, view toggle, search
├── components/discovery/
│   └── DiscoveryContextBar.tsx       # Categories, trending, filters, search
├── components/game-night/
│   └── GameNightContextBar.tsx       # Event info, countdown, edit
├── components/chat/
│   └── ChatContextBar.tsx            # Agent, context, thread switcher
├── components/session/
│   ├── LiveSessionContextBar.tsx     # Game, timer, turn, players, score
│   └── BackToSessionFAB.tsx          # "Back to session" floating button
└── styles/
    └── (modify design-tokens.css)    # Add .env-tavolo, .env-hub
```

### Modified Files

```
apps/web/src/
├── components/layout/UserShell/
│   ├── UserShellClient.tsx           # Remove sidebar, tabbar, CardRack, SwipeableContainer; add ContextBar
│   └── UserTopNav.tsx                # Reduce to 48px, replace sectionTitle with breadcrumb logic
├── lib/stores/navStore.ts            # (leave as-is)
├── hooks/useNavigation.ts            # MODIFY existing: add breadcrumb computation
├── components/ui/data-display/meeple-card/
│   ├── MeepleCard.tsx                # Add env-aware shadow/lift via CSS vars
│   └── variants/                     # Add skeleton state to each variant
├── components/dashboard/
│   └── DashboardRenderer.tsx         # Complete rewrite with zone-based layout
├── app/(authenticated)/dashboard/layout.tsx     # Add ContextBarRegistrar
├── app/(authenticated)/library/layout.tsx       # Add ContextBarRegistrar
├── app/(authenticated)/discover/layout.tsx      # Add ContextBarRegistrar
├── app/(authenticated)/game-nights/[id]/layout.tsx  # Add ContextBarRegistrar
├── app/(authenticated)/sessions/live/[sessionId]/layout.tsx  # Add ContextBarRegistrar
└── app/(chat)/layout.tsx             # Add ContextBarRegistrar
```

---

## Phase 1: Foundation (Stores + CSS Tokens)

### Task 1: Context Bar Zustand Store

**Files:**
- Create: `apps/web/src/lib/stores/context-bar-store.ts`
- Test: `apps/web/src/__tests__/stores/context-bar-store.test.ts`
- Ref: `apps/web/src/store/quick-view/store.ts` (pattern to follow)

- [ ] **Step 1: Write failing test for context-bar-store**

```ts
// apps/web/src/__tests__/stores/context-bar-store.test.ts
import { describe, it, expect, beforeEach } from 'vitest';
import { useContextBarStore } from '@/lib/stores/context-bar-store';

describe('useContextBarStore', () => {
  beforeEach(() => {
    // Follow project pattern from quick-view store tests
    useContextBarStore.setState({ content: null, options: { alwaysVisible: false } });
  });

  it('starts with null content and default options', () => {
    const state = useContextBarStore.getState();
    expect(state.content).toBeNull();
    expect(state.options).toEqual({ alwaysVisible: false });
  });

  it('setContent updates content', () => {
    useContextBarStore.getState().setContent('test-content');
    expect(useContextBarStore.getState().content).toBe('test-content');
  });

  it('setOptions merges with existing options', () => {
    useContextBarStore.getState().setOptions({ alwaysVisible: true });
    expect(useContextBarStore.getState().options.alwaysVisible).toBe(true);
  });

  it('clear resets content and options', () => {
    useContextBarStore.getState().setContent('test');
    useContextBarStore.getState().setOptions({ alwaysVisible: true });
    useContextBarStore.getState().clear();
    expect(useContextBarStore.getState().content).toBeNull();
    expect(useContextBarStore.getState().options).toEqual({ alwaysVisible: false });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/__tests__/stores/context-bar-store.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Implement context-bar-store**

```ts
// apps/web/src/lib/stores/context-bar-store.ts
import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import type { ReactNode } from 'react';

interface ContextBarOptions {
  alwaysVisible: boolean;
}

interface ContextBarState {
  content: ReactNode | null;
  options: ContextBarOptions;
  setContent: (content: ReactNode | null) => void;
  setOptions: (options: Partial<ContextBarOptions>) => void;
  clear: () => void;
}

const DEFAULT_OPTIONS: ContextBarOptions = { alwaysVisible: false };

export const useContextBarStore = create<ContextBarState>()(
  devtools(
    (set) => ({
      content: null,
      options: DEFAULT_OPTIONS,
      setContent: (content) => set({ content }),
      setOptions: (options) =>
        set((state) => ({ options: { ...state.options, ...options } })),
      clear: () => set({ content: null, options: DEFAULT_OPTIONS }),
    }),
    { name: 'context-bar-store' }
  )
);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/__tests__/stores/context-bar-store.test.ts`
Expected: PASS — all 4 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/stores/context-bar-store.ts apps/web/src/__tests__/stores/context-bar-store.test.ts
git commit -m "feat(store): add context-bar Zustand store"
```

---

### Task 2: Overlay Zustand Store

**Files:**
- Create: `apps/web/src/lib/stores/overlay-store.ts`
- Test: `apps/web/src/__tests__/stores/overlay-store.test.ts`

- [ ] **Step 1: Write failing test for overlay-store**

```ts
// apps/web/src/__tests__/stores/overlay-store.test.ts
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useOverlayStore } from '@/lib/stores/overlay-store';

describe('useOverlayStore', () => {
  beforeEach(() => {
    useOverlayStore.setState({ isOpen: false, entityType: null, entityId: null, deckItems: null, deckIndex: 0 });
  });

  it('starts closed with no entity', () => {
    const state = useOverlayStore.getState();
    expect(state.isOpen).toBe(false);
    expect(state.entityType).toBeNull();
    expect(state.entityId).toBeNull();
  });

  it('open sets entity and opens', () => {
    useOverlayStore.getState().open('player', 'mario-id');
    const state = useOverlayStore.getState();
    expect(state.isOpen).toBe(true);
    expect(state.entityType).toBe('player');
    expect(state.entityId).toBe('mario-id');
  });

  it('close resets state', () => {
    useOverlayStore.getState().open('game', 'catan-id');
    useOverlayStore.getState().close();
    const state = useOverlayStore.getState();
    expect(state.isOpen).toBe(false);
    expect(state.entityType).toBeNull();
  });

  it('replace swaps entity without closing', () => {
    useOverlayStore.getState().open('player', 'mario-id');
    useOverlayStore.getState().open('game', 'catan-id');
    const state = useOverlayStore.getState();
    expect(state.isOpen).toBe(true);
    expect(state.entityType).toBe('game');
    expect(state.entityId).toBe('catan-id');
  });

  it('serializes to URL param format', () => {
    useOverlayStore.getState().open('player', 'mario-id');
    expect(useOverlayStore.getState().toUrlParam()).toBe('player:mario-id');
  });

  it('deserializes from URL param format', () => {
    useOverlayStore.getState().fromUrlParam('game:catan-id');
    const state = useOverlayStore.getState();
    expect(state.isOpen).toBe(true);
    expect(state.entityType).toBe('game');
    expect(state.entityId).toBe('catan-id');
  });

  it('fromUrlParam with null closes', () => {
    useOverlayStore.getState().open('game', 'x');
    useOverlayStore.getState().fromUrlParam(null);
    expect(useOverlayStore.getState().isOpen).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/__tests__/stores/overlay-store.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Implement overlay-store**

```ts
// apps/web/src/lib/stores/overlay-store.ts
import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
// NOTE: MeepleEntityType is defined in meeple-card-styles.ts and re-exported via meeple-card/types.ts
// Import from the types barrel to avoid deep component-layer coupling
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card/types';

interface OverlayState {
  isOpen: boolean;
  entityType: MeepleEntityType | null;
  entityId: string | null;
  deckItems: Array<{ entityType: MeepleEntityType; entityId: string }> | null;
  deckIndex: number;
  open: (entityType: MeepleEntityType, entityId: string) => void;
  openDeck: (items: Array<{ entityType: MeepleEntityType; entityId: string }>, startIndex?: number) => void;
  setDeckIndex: (index: number) => void;
  close: () => void;
  toUrlParam: () => string | null;
  fromUrlParam: (param: string | null) => void;
}

export const useOverlayStore = create<OverlayState>()(
  devtools(
    (set, get) => ({
      isOpen: false,
      entityType: null,
      entityId: null,
      deckItems: null,
      deckIndex: 0,
      open: (entityType, entityId) =>
        set({ isOpen: true, entityType, entityId, deckItems: null, deckIndex: 0 }),
      openDeck: (items, startIndex = 0) =>
        set({
          isOpen: true,
          entityType: items[startIndex]?.entityType ?? null,
          entityId: items[startIndex]?.entityId ?? null,
          deckItems: items,
          deckIndex: startIndex,
        }),
      setDeckIndex: (index) => {
        const { deckItems } = get();
        if (!deckItems || index < 0 || index >= deckItems.length) return;
        set({
          deckIndex: index,
          entityType: deckItems[index].entityType,
          entityId: deckItems[index].entityId,
        });
      },
      close: () =>
        set({ isOpen: false, entityType: null, entityId: null, deckItems: null, deckIndex: 0 }),
      toUrlParam: () => {
        const { isOpen, entityType, entityId } = get();
        if (!isOpen || !entityType || !entityId) return null;
        return `${entityType}:${entityId}`;
      },
      fromUrlParam: (param) => {
        if (!param) {
          set({ isOpen: false, entityType: null, entityId: null, deckItems: null, deckIndex: 0 });
          return;
        }
        const [entityType, entityId] = param.split(':') as [MeepleEntityType, string];
        if (entityType && entityId) {
          set({ isOpen: true, entityType, entityId, deckItems: null, deckIndex: 0 });
        }
      },
    }),
    { name: 'overlay-store' }
  )
);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/__tests__/stores/overlay-store.test.ts`
Expected: PASS — all 7 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/stores/overlay-store.ts apps/web/src/__tests__/stores/overlay-store.test.ts
git commit -m "feat(store): add overlay Zustand store with URL param sync"
```

---

### Task 3: CSS Environment Tokens (Tavolo + Hub)

**Files:**
- Modify: `apps/web/src/styles/design-tokens.css`
- Create: `public/textures/` directory (texture asset deferred to polish phase)

- [ ] **Step 1: Read current design-tokens.css to find insertion point**

Read: `apps/web/src/styles/design-tokens.css` — find the section after entity colors (around line 344+) where env utilities should go. Also check for any existing `@layer utilities` blocks.

- [ ] **Step 2: Add environment CSS variables to design-tokens.css**

Append after the entity color section in `design-tokens.css`:

```css
/* ===== Environment Themes ===== */

@layer utilities {
  .env-tavolo {
    --env-bg: hsl(25, 30%, 15%);
    --env-bg-dark: hsl(25, 20%, 10%);
    --env-shadow-offset: 6px;
    --env-shadow-color: hsl(25 40% 8% / 0.4);
    --env-card-lift: -4px;
    --env-texture: url('/textures/felt-subtle.webp');
    --env-texture-opacity: 0.04;
  }

  .env-hub {
    --env-bg: hsl(0, 0%, 98%);
    --env-bg-dark: hsl(0, 0%, 8%);
    --env-shadow-offset: 2px;
    --env-shadow-color: hsl(0 0% 0% / 0.08);
    --env-card-lift: -2px;
    --env-texture: none;
    --env-texture-opacity: 0;
  }
}
```

- [ ] **Step 3: Verify CSS is valid**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds (CSS is parsed without errors)

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/styles/design-tokens.css
git commit -m "feat(tokens): add Tavolo and Hub environment CSS variables"
```

---

### Task 4: Verify useScrollDirection Hook Exists

**Files:**
- Existing: `apps/web/src/hooks/useScrollDirection.ts` (already exists in codebase)

- [ ] **Step 1: Verify existing hook at `apps/web/src/hooks/useScrollDirection.ts`**

Read the file. It returns `ScrollDirection = 'up' | 'down' | null` and accepts `{ threshold }` object param. The ContextBar (Task 5) will import from `@/hooks/useScrollDirection` and handle the `null` case (treat as `'up'`).

No code changes needed. This task is verification only.

---

## Phase 2: Shell Migration

### Task 5: ContextBar Shell Component

**Files:**
- Create: `apps/web/src/components/layout/ContextBar/ContextBar.tsx`
- Create: `apps/web/src/components/layout/ContextBar/ContextBarRegistrar.tsx`
- Create: `apps/web/src/components/layout/ContextBar/index.ts`
- Test: `apps/web/src/__tests__/components/ContextBar.test.tsx`

- [ ] **Step 1: Write failing test for ContextBar**

```tsx
// apps/web/src/__tests__/components/ContextBar.test.tsx
import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ContextBar } from '@/components/layout/ContextBar';
import { useContextBarStore } from '@/lib/stores/context-bar-store';

describe('ContextBar', () => {
  beforeEach(() => {
    useContextBarStore.setState({ content: null, options: { alwaysVisible: false } });
  });

  it('renders nothing when content is null', () => {
    render(<ContextBar />);
    // ContextBar returns null when no content — element should not exist
    expect(screen.queryByTestId('context-bar')).toBeNull();
  });

  it('renders content from store', () => {
    useContextBarStore.getState().setContent(<div>Test Content</div>);
    render(<ContextBar />);
    expect(screen.getByText('Test Content')).toBeDefined();
  });

  it('has correct height class', () => {
    useContextBarStore.getState().setContent(<div>Content</div>);
    render(<ContextBar />);
    const bar = screen.getByTestId('context-bar');
    expect(bar.className).toContain('h-11'); // 44px
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/ContextBar.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement ContextBar, ContextBarRegistrar, index**

```tsx
// apps/web/src/components/layout/ContextBar/ContextBar.tsx
'use client';

import { useContextBarStore } from '@/lib/stores/context-bar-store';
import { useScrollDirection } from '@/hooks/useScrollDirection';
import { cn } from '@/lib/utils';

export function ContextBar() {
  const { content, options } = useContextBarStore();
  const scrollDirection = useScrollDirection({ threshold: 50 });

  // Return null when no content — element should not exist in DOM
  if (!content) return null;

  const isHidden = !options.alwaysVisible && scrollDirection === 'down';

  return (
    <div
      data-testid="context-bar"
      className={cn(
        'h-11 border-b border-border/50 bg-background/80 backdrop-blur-md',
        'flex items-center px-4 gap-2 overflow-x-auto',
        'transition-all duration-150 motion-reduce:transition-none',
        isHidden && '-translate-y-full opacity-0 pointer-events-none h-0 border-0'
      )}
    >
      {content}
    </div>
  );
}
```

```tsx
// apps/web/src/components/layout/ContextBar/ContextBarRegistrar.tsx
'use client';

import { useEffect, type ReactNode } from 'react';
import { useContextBarStore } from '@/lib/stores/context-bar-store';

interface ContextBarRegistrarProps {
  children: ReactNode;
  alwaysVisible?: boolean;
}

export function ContextBarRegistrar({ children, alwaysVisible = false }: ContextBarRegistrarProps) {
  const { setContent, setOptions, clear } = useContextBarStore();

  useEffect(() => {
    setContent(children);
    if (alwaysVisible) setOptions({ alwaysVisible: true });
    return () => clear();
  }, [children, alwaysVisible, setContent, setOptions, clear]);

  return null; // Renders nothing — pushes content to store
}
```

```ts
// apps/web/src/components/layout/ContextBar/index.ts
export { ContextBar } from './ContextBar';
export { ContextBarRegistrar } from './ContextBarRegistrar';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/ContextBar.test.tsx`
Expected: PASS — all 3 tests

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/ContextBar/
git add apps/web/src/__tests__/components/ContextBar.test.tsx
git commit -m "feat(layout): add ContextBar shell with Zustand store integration"
```

---

### Task 6: Simplify UserShellClient — Remove Retired Components

> **DEPENDENCY**: This task MUST be executed AFTER Task 15 (DashboardRenderer rewrite) because the current `UserShellClient` renders `HomeFeed`, `LibraryPanel`, `PlayPanel`, `ChatPanel` via `SwipeableContainer`. Removing these before the dashboard zones exist will break the app. The new dashboard zones (Task 14-15) replace `HomeFeed` functionality.

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/UserShellClient.tsx`
- Ref: Current file uses `UserDesktopSidebar`, `UserTabBar`, `CardRack`, `SwipeableContainer`, `HomeFeed`, `LibraryPanel`, `PlayPanel`, `ChatPanel`

- [ ] **Step 1: Read current UserShellClient.tsx**

Read: `apps/web/src/components/layout/UserShell/UserShellClient.tsx`
Understand: Which imports and JSX references to `UserDesktopSidebar`, `UserTabBar`, `CardRack`, `SwipeableContainer` must be removed.

- [ ] **Step 2: Rewrite UserShellClient with new layout**

Replace the entire component body with the new flex-column layout from the spec:

```tsx
// Key changes:
// - Remove: UserDesktopSidebar, UserTabBar, CardRack, SwipeableContainer imports + JSX
// - Add: ContextBar import + JSX between UserTopNav and main
// - Change layout: from sidebar grid to simple flex-col
// - Keep: DashboardEngineProvider, any error boundaries, session warnings

import { ContextBar } from '@/components/layout/ContextBar';

// New layout:
<div className="flex flex-col h-dvh bg-background">
  <UserTopNav />
  <ContextBar />
  <main className="flex-1 overflow-y-auto">
    <DashboardEngineProvider>
      {children}
    </DashboardEngineProvider>
  </main>
</div>
```

- [ ] **Step 3: Verify app still compiles**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds. Pages render without sidebar or bottom tab bar.

- [ ] **Step 4: Run existing tests to check for breakage**

Run: `cd apps/web && pnpm vitest run --reporter=verbose 2>&1 | head -50`
Expected: No new failures from shell changes (some tests may reference removed components — note failures for fixing in next step)

- [ ] **Step 5: Fix any broken test imports**

If tests import `UserDesktopSidebar`, `UserTabBar`, etc., update or remove those test files since the components are retired.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/layout/UserShell/UserShellClient.tsx
git add -u  # any test fixes
git commit -m "refactor(shell): remove sidebar, tab bar, CardRack, SwipeableContainer; add ContextBar"
```

---

### Task 7: Slim UserTopNav to 48px + Breadcrumb

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/UserTopNav.tsx`
- Modify: `apps/web/src/hooks/useNavigation.ts` (existing file — currently a re-export of `useNavStore`)
- Ref: `apps/web/src/lib/stores/navStore.ts` (existing store, do not modify)

- [ ] **Step 1: Modify existing useNavigation hook to add breadcrumbs**

The file already exists at `apps/web/src/hooks/useNavigation.ts` and currently re-exports `useNavStore`. Transform it into a wrapper hook:

```ts
// apps/web/src/hooks/useNavigation.ts
'use client';

import { usePathname } from 'next/navigation';
import { useNavStore } from '@/lib/stores/navStore';

interface Breadcrumb {
  label: string;
  href: string;
}

const SEGMENT_LABELS: Record<string, string> = {
  dashboard: 'Dashboard',
  sessions: 'Sessioni',
  live: 'Live',
  library: 'Libreria',
  discover: 'Scopri',
  'game-nights': 'Serate',
  chat: 'Chat',
  settings: 'Impostazioni',
  scoreboard: 'Punteggi',
  players: 'Giocatori',
};

export function useNavigation() {
  const store = useNavStore();
  const pathname = usePathname();

  const segments = pathname.split('/').filter(Boolean);
  // Remove route groups like (authenticated)
  const cleanSegments = segments.filter((s) => !s.startsWith('('));

  const breadcrumbs: Breadcrumb[] = cleanSegments.map((segment, index) => ({
    label: SEGMENT_LABELS[segment] || segment,
    href: '/' + cleanSegments.slice(0, index + 1).join('/'),
  }));

  const showBreadcrumb = cleanSegments.length >= 3;

  return {
    ...store,
    breadcrumbs,
    showBreadcrumb,
  };
}
```

- [ ] **Step 2: Update UserTopNav — height to h-12, replace sectionTitle with breadcrumb**

Key changes in `UserTopNav.tsx`:
- Change `h-14` to `h-12` (48px)
- Replace `sectionTitle` center content with: if `showBreadcrumb`, render breadcrumb links; else render `sectionTitle`
- Import `useNavigation` from new hook instead of `navStore`

- [ ] **Step 3: Verify build and visual**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds, navbar is 48px.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/hooks/useNavigation.ts
git add apps/web/src/components/layout/UserShell/UserTopNav.tsx
git commit -m "refactor(nav): slim UserTopNav to 48px, add breadcrumb navigation"
```

---

## Phase 3: Reusable Primitives

### Task 8: OverlayHybrid Component

**Files:**
- Create: `apps/web/src/components/ui/overlays/OverlayHybrid.tsx`
- Create: `apps/web/src/components/ui/overlays/OverlayHeader.tsx`
- Create: `apps/web/src/components/ui/overlays/OverlayFooter.tsx`
- Create: `apps/web/src/components/ui/overlays/index.ts`
- Test: `apps/web/src/__tests__/components/OverlayHybrid.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/__tests__/components/OverlayHybrid.test.tsx
import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { OverlayHybrid } from '@/components/ui/overlays';
import { useOverlayStore } from '@/lib/stores/overlay-store';

describe('OverlayHybrid', () => {
  beforeEach(() => {
    useOverlayStore.getState().close();
  });

  it('is not visible when store is closed', () => {
    render(<OverlayHybrid>{() => <div>Content</div>}</OverlayHybrid>);
    expect(screen.queryByTestId('overlay-hybrid')).toBeNull();
  });

  it('renders when store is open', () => {
    useOverlayStore.getState().open('game', 'catan');
    render(
      <OverlayHybrid>
        {({ entityType, entityId }) => <div>Showing {entityType}:{entityId}</div>}
      </OverlayHybrid>
    );
    expect(screen.getByText('Showing game:catan')).toBeDefined();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/OverlayHybrid.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement OverlayHybrid**

The component reads from `useOverlayStore` and renders a bottom sheet (mobile) or side panel (desktop). Key implementation points:

- Uses `useMediaQuery` or Tailwind responsive classes to switch between bottom sheet / side panel
- Bottom sheet: `fixed bottom-0 inset-x-0 h-[60vh]` with swipe-down-to-close
- Side panel: `fixed right-0 top-0 bottom-0 w-[400px]`
- Backdrop: semi-transparent, click to close
- Close on Escape (desktop), back button (mobile via popstate)
- `children` is a render function receiving `{ entityType, entityId, onClose }`
- Transitions: 250ms ease-out open, 200ms close, `motion-reduce:transition-none`

- [ ] **Step 4: Implement OverlayHeader and OverlayFooter**

`OverlayHeader`: Entity icon (colored), title, subtitle, close X button, drag handle (mobile).
`OverlayFooter`: Deck navigation dots (if `deckItems`), "Espandi pagina completa" link.

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/OverlayHybrid.test.tsx`
Expected: PASS

- [ ] **Step 6: Add URL sync for deep linking**

In `OverlayHybrid`, add `useEffect` that:
- On open: calls `window.history.pushState({}, '', currentPath + '?overlay=' + store.toUrlParam())`
- On `popstate` event: calls `store.close()`
- On mount: reads `searchParams` from URL and calls `store.fromUrlParam()` to restore state
- Only active when a `enableDeepLink` prop is true (dashboard + live session pass it)

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/ui/overlays/
git add apps/web/src/__tests__/components/OverlayHybrid.test.tsx
git commit -m "feat(ui): add OverlayHybrid — bottom sheet (mobile) / side panel (desktop)"
```

---

### Task 9: CardDeck Swipeable Component

**Files:**
- Create: `apps/web/src/components/ui/data-display/card-deck/CardDeck.tsx`
- Create: `apps/web/src/components/ui/data-display/card-deck/useCardDeckGestures.ts`
- Create: `apps/web/src/components/ui/data-display/card-deck/MiniCardDeck.tsx`
- Create: `apps/web/src/components/ui/data-display/card-deck/index.ts`
- Test: `apps/web/src/__tests__/components/CardDeck.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
// apps/web/src/__tests__/components/CardDeck.test.tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CardDeck } from '@/components/ui/data-display/card-deck';

const mockItems = [
  { id: '1', title: 'Card 1' },
  { id: '2', title: 'Card 2' },
  { id: '3', title: 'Card 3' },
];

describe('CardDeck', () => {
  it('renders the active item', () => {
    render(
      <CardDeck
        items={mockItems}
        activeIndex={0}
        onIndexChange={() => {}}
        renderItem={(item) => <div>{item.title}</div>}
      />
    );
    expect(screen.getByText('Card 1')).toBeDefined();
  });

  it('shows dot indicators matching item count', () => {
    render(
      <CardDeck
        items={mockItems}
        activeIndex={1}
        onIndexChange={() => {}}
        renderItem={(item) => <div>{item.title}</div>}
        showIndicators
      />
    );
    const dots = screen.getAllByRole('tab');
    expect(dots).toHaveLength(3);
  });

  it('renders empty state when no items', () => {
    render(
      <CardDeck
        items={[]}
        activeIndex={0}
        onIndexChange={() => {}}
        renderItem={(item) => <div>{item.title}</div>}
        emptyState={<div>No items</div>}
      />
    );
    expect(screen.getByText('No items')).toBeDefined();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/CardDeck.test.tsx`

- [ ] **Step 3: Implement useCardDeckGestures hook**

Touch gesture handler with:
- `onTouchStart`, `onTouchMove`, `onTouchEnd` handlers
- Tracks horizontal delta, applies `snapThreshold` (30%)
- Returns `{ gestureHandlers, offset, isDragging }`
- `touch-action: pan-y` on container

- [ ] **Step 4: Implement CardDeck component**

Key implementation:
- Container with `role="tablist"`, `overflow: hidden`
- Renders 3 items max (active + 2 adjacent) for virtualization
- Active item centered, adjacent items peeking 8px
- Dot indicators with `role="tab"` and `aria-selected`
- Arrow buttons (desktop, visible on hover)
- Keyboard: ArrowLeft/Right, Home/End, Escape
- `prefers-reduced-motion`: no animations on transition, instant snap

- [ ] **Step 5: Implement MiniCardDeck**

Inline stacked preview: 3-4 overlapping thumbnails with `offset: 8px`, count badge if >3. `onClick` with `e.stopPropagation()`.

- [ ] **Step 6: Run tests, verify pass**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/CardDeck.test.tsx`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/ui/data-display/card-deck/
git add apps/web/src/__tests__/components/CardDeck.test.tsx
git commit -m "feat(ui): add CardDeck swipeable component and MiniCardDeck inline preview"
```

---

### Task 10: MeepleCard Skeleton States

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/*.tsx` (all 6 variants)
- Modify: `apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx`
- Test: `apps/web/src/__tests__/components/MeepleCardSkeleton.test.tsx`

- [ ] **Step 1: Read existing skeleton implementation**

**IMPORTANT**: A `MeepleCardSkeleton` component already exists in `meeple-card-parts` and is imported by some variants (e.g., `MeepleCardGrid.tsx` imports it). Read:
1. `apps/web/src/components/ui/data-display/meeple-card-parts/` — find the existing skeleton
2. Each variant in `variants/` — check which ones already use `MeepleCardSkeleton` when `loading` is true
3. Determine which variants need skeleton support added vs which already have it

Only implement what's missing. Do NOT duplicate existing skeleton functionality.

- [ ] **Step 2: Write failing test**

```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';

describe('MeepleCard skeleton', () => {
  const variants = ['grid', 'list', 'compact', 'featured', 'hero', 'expanded'] as const;

  variants.forEach((variant) => {
    it(`renders skeleton for variant="${variant}"`, () => {
      render(<MeepleCard entity="game" variant={variant} loading title="" />);
      expect(screen.getByTestId(`meeple-card-skeleton-${variant}`)).toBeDefined();
    });
  });
});
```

- [ ] **Step 3: Implement skeleton state in each variant**

Each variant returns a shimmer skeleton when `loading` is true:
- Same dimensions as the real card variant
- Placeholder boxes with `animate-pulse bg-muted rounded` matching title, image, metadata positions
- `data-testid="meeple-card-skeleton-{variant}"`

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/MeepleCardSkeleton.test.tsx`
Expected: PASS — all 6 variants

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/
git add apps/web/src/__tests__/components/MeepleCardSkeleton.test.tsx
git commit -m "feat(card): implement skeleton loading state for all 6 MeepleCard variants"
```

---

### Task 11: TavoloZone Container

**Files:**
- Create: `apps/web/src/components/dashboard/TavoloZone.tsx`
- Test: `apps/web/src/__tests__/components/TavoloZone.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { TavoloZone } from '@/components/dashboard/TavoloZone';

describe('TavoloZone', () => {
  it('renders children with env-tavolo class', () => {
    render(<TavoloZone><div>Inside tavolo</div></TavoloZone>);
    const zone = screen.getByTestId('tavolo-zone');
    expect(zone.className).toContain('env-tavolo');
    expect(screen.getByText('Inside tavolo')).toBeDefined();
  });

  it('collapses when no children have content', () => {
    render(<TavoloZone isEmpty><div>Fallback</div></TavoloZone>);
    expect(screen.queryByTestId('tavolo-zone')).toBeNull();
  });
});
```

- [ ] **Step 2: Implement TavoloZone**

```tsx
// apps/web/src/components/dashboard/TavoloZone.tsx
'use client';

import { cn } from '@/lib/utils';
import type { ReactNode } from 'react';

interface TavoloZoneProps {
  children: ReactNode;
  isEmpty?: boolean;
  className?: string;
}

export function TavoloZone({ children, isEmpty, className }: TavoloZoneProps) {
  if (isEmpty) return null;

  return (
    <section
      data-testid="tavolo-zone"
      className={cn(
        'env-tavolo relative rounded-2xl p-6 overflow-hidden',
        'bg-[var(--env-bg)] dark:bg-[var(--env-bg-dark)]',
        'shadow-lg shadow-[var(--env-shadow-color)]',
        className
      )}
    >
      {/* Subtle texture overlay — reads --env-texture from CSS env class */}
      <div
        className="absolute inset-0 pointer-events-none bg-repeat opacity-[var(--env-texture-opacity)]"
        style={{ backgroundImage: 'var(--env-texture)' }}
        aria-hidden="true"
      />
      <div className="relative z-10">{children}</div>
    </section>
  );
}
```

- [ ] **Step 3: Run test**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/TavoloZone.test.tsx`

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/dashboard/TavoloZone.tsx
git add apps/web/src/__tests__/components/TavoloZone.test.tsx
git commit -m "feat(dashboard): add TavoloZone warm environment container"
```

---

## Phase 4: Context Bars

### Task 12: DashboardContextBar + Route Registration

**Files:**
- Create: `apps/web/src/components/dashboard/DashboardContextBar.tsx`
- Modify: `apps/web/src/app/(authenticated)/dashboard/layout.tsx`

- [ ] **Step 1: Create DashboardContextBar**

```tsx
// apps/web/src/components/dashboard/DashboardContextBar.tsx
'use client';

import Link from 'next/link';
import { Dice5, Search, Moon } from 'lucide-react';
import { useState } from 'react';
import { cn } from '@/lib/utils';

export function DashboardContextBar() {
  const [searchFocused, setSearchFocused] = useState(false);

  return (
    <div className="flex items-center gap-2 w-full">
      {!searchFocused && (
        <>
          <Link
            href="/sessions/new"
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-primary/10 text-primary text-sm font-medium hover:bg-primary/20 transition-colors whitespace-nowrap"
          >
            <Dice5 className="w-4 h-4" />
            Nuova Partita
          </Link>
          <Link
            href="/discover"
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-muted text-muted-foreground text-sm font-medium hover:bg-muted/80 transition-colors whitespace-nowrap"
          >
            <Search className="w-4 h-4" />
            Cerca Gioco
          </Link>
          <Link
            href="/game-nights/new"
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-muted text-muted-foreground text-sm font-medium hover:bg-muted/80 transition-colors whitespace-nowrap"
          >
            <Moon className="w-4 h-4" />
            Game Night
          </Link>
        </>
      )}
      <div className={cn('relative', searchFocused ? 'flex-1' : 'ml-auto')}>
        <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
        <input
          type="search"
          placeholder="Cerca..."
          className={cn(
            'pl-8 pr-3 py-1.5 rounded-full bg-muted/50 border border-border/50 text-sm',
            'focus:outline-none focus:ring-2 focus:ring-primary/30 transition-all',
            searchFocused ? 'w-full' : 'w-36'
          )}
          onFocus={() => setSearchFocused(true)}
          onBlur={() => setSearchFocused(false)}
        />
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Register in dashboard layout**

```tsx
// apps/web/src/app/(authenticated)/dashboard/layout.tsx
import { ContextBarRegistrar } from '@/components/layout/ContextBar';
import { DashboardContextBar } from '@/components/dashboard/DashboardContextBar';

export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <ContextBarRegistrar>
        <DashboardContextBar />
      </ContextBarRegistrar>
      {children}
    </>
  );
}
```

- [ ] **Step 3: Verify in browser or build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds. Dashboard route renders ContextBar with quick action pills.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/dashboard/DashboardContextBar.tsx
git add apps/web/src/app/\(authenticated\)/dashboard/layout.tsx
git commit -m "feat(dashboard): add DashboardContextBar with quick actions and search"
```

---

### Task 13: Remaining 5 Context Bars + Route Registration

**Files:**
- Create: `apps/web/src/components/library/LibraryContextBar.tsx`
- Create: `apps/web/src/components/discovery/DiscoveryContextBar.tsx`
- Create: `apps/web/src/components/game-night/GameNightContextBar.tsx`
- Create: `apps/web/src/components/chat/ChatContextBar.tsx`
- Create: `apps/web/src/components/session/LiveSessionContextBar.tsx`
- Modify: 5 route layouts to add `ContextBarRegistrar`

- [ ] **Step 1: Create LibraryContextBar**

Filter toggle group (Tutti/Posseduti/Wishlist/Prestati), sort dropdown, grid/list toggle, search. Read existing library page to understand what filter state hooks already exist.

- [ ] **Step 2: Create DiscoveryContextBar**

Categories dropdown, trending/nuovi/top toggle, filters button, search input.

- [ ] **Step 3: Create GameNightContextBar**

Read event data from route params. Show name, countdown, invitees avatar stack, games count, edit button.

- [ ] **Step 4: Create ChatContextBar**

Agent avatar + name, context game badge, thread dropdown.

- [ ] **Step 5: Create LiveSessionContextBar**

This is the most complex one. Read session data from live-session-store. Show: game cover (28px circle), game name, timer (monospace), turn "T{n}", avatar stack (max 4), leader score. Each element is a button that calls `useOverlayStore().open(entityType, entityId)`. Set `alwaysVisible: true`.

- [ ] **Step 6: Register all 5 in their route layouts**

**NOTE**: `app/(authenticated)/game-nights/[id]/layout.tsx` does NOT exist yet — CREATE it (not modify). The other layouts exist and should be modified.

```tsx
// Example for library:
// apps/web/src/app/(authenticated)/library/layout.tsx
import { ContextBarRegistrar } from '@/components/layout/ContextBar';
import { LibraryContextBar } from '@/components/library/LibraryContextBar';

export default function LibraryLayout({ children }) {
  return (
    <>
      <ContextBarRegistrar><LibraryContextBar /></ContextBarRegistrar>
      {children}
    </>
  );
}

// For live session: add alwaysVisible prop
<ContextBarRegistrar alwaysVisible>
  <LiveSessionContextBar sessionId={sessionId} />
</ContextBarRegistrar>
```

- [ ] **Step 7: Verify build**

Run: `cd apps/web && pnpm build`

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/components/library/LibraryContextBar.tsx
git add apps/web/src/components/discovery/DiscoveryContextBar.tsx
git add apps/web/src/components/game-night/GameNightContextBar.tsx
git add apps/web/src/components/chat/ChatContextBar.tsx
git add apps/web/src/components/session/LiveSessionContextBar.tsx
git add apps/web/src/app/  # all modified layouts
git commit -m "feat(contextbar): add 5 context bars (library, discovery, game-night, chat, live-session)"
```

---

## Phase 5: Dashboard Redesign

### Task 14: Dashboard Zone Components

**Files:**
- Create: `apps/web/src/components/dashboard/zones/ActiveSessionZone.tsx`
- Create: `apps/web/src/components/dashboard/zones/GameNightZone.tsx`
- Create: `apps/web/src/components/dashboard/zones/AgentZone.tsx`
- Create: `apps/web/src/components/dashboard/zones/StatsZone.tsx`
- Create: `apps/web/src/components/dashboard/zones/FeedZone.tsx`
- Create: `apps/web/src/components/dashboard/zones/SuggestedZone.tsx`

Each zone is a self-contained component that:
- Fetches its own data via React Query hooks
- Renders `MeepleCard` components with correct entity type + variant
- Handles its own loading (skeleton), empty, and error states
- Is independently testable

- [ ] **Step 1: Create ActiveSessionZone**

Uses `useActiveSessions()`. Renders `MeepleCard entity="session" variant="featured"` for single session, or `variant="list"` row for multiple. Returns `null` if no active sessions (zona collassa).

- [ ] **Step 2: Create GameNightZone**

Uses a game-night hook (check existing hooks). Renders `MeepleCard entity="event" variant="featured"` with `MiniCardDeck` for selected games. Empty state: suggestion card with CTA.

- [ ] **Step 3: Create AgentZone**

Uses `useAgents()`. Horizontal scrollable row of `MeepleCard entity="agent" variant="compact"`. Each card tap calls `useOverlayStore().open('agent', agentId)`. Empty: single CTA card.

- [ ] **Step 4: Create StatsZone**

Uses `useDashboardStore().stats`. Grid of `StatCard`. 2x2 mobile, 4-col desktop. Shows "—" values if no data.

- [ ] **Step 5: Create FeedZone**

Uses a feed/activity hook. Mixed `MeepleCard variant="list"` with dynamic entity types. Each tap opens overlay. "Vedi tutto" link.

- [ ] **Step 6: Create SuggestedZone**

Uses `useRecentlyAddedGames()` or a suggestions hook. Horizontal `CardDeck` (standalone variant) of `MeepleCard entity="game" variant="compact"`. Only rendered if user has >= 3 games.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/dashboard/zones/
git commit -m "feat(dashboard): add 6 dashboard zone components with card-based rendering"
```

---

### Task 15: Rewrite DashboardRenderer

**Files:**
- Modify: `apps/web/src/components/dashboard/DashboardRenderer.tsx`
- Test: `apps/web/src/__tests__/components/DashboardRenderer.test.tsx`

- [ ] **Step 1: Read current DashboardRenderer**

Read: `apps/web/src/components/dashboard/DashboardRenderer.tsx`
Understand current structure and which imports/hooks to preserve vs replace.

- [ ] **Step 2: Rewrite DashboardRenderer with zone layout**

```tsx
// apps/web/src/components/dashboard/DashboardRenderer.tsx
'use client';

import { TavoloZone } from './TavoloZone';
import { ActiveSessionZone } from './zones/ActiveSessionZone';
import { GameNightZone } from './zones/GameNightZone';
import { AgentZone } from './zones/AgentZone';
import { StatsZone } from './zones/StatsZone';
import { FeedZone } from './zones/FeedZone';
import { SuggestedZone } from './zones/SuggestedZone';
import { OnboardingFlow } from './OnboardingFlow';
import { OverlayHybrid } from '@/components/ui/overlays';
import { useActiveSessions } from '@/lib/hooks/useActiveSessions';

export function DashboardRenderer() {
  const { data: sessions } = useActiveSessions();
  const hasActiveSessions = sessions && sessions.length > 0;
  // TODO: check game night data too for TavoloZone isEmpty

  return (
    <div className="env-hub space-y-6 p-4 md:p-6">
      <OnboardingFlow />

      <TavoloZone isEmpty={!hasActiveSessions}>
        <div className="space-y-4 md:flex md:gap-4 md:space-y-0">
          <ActiveSessionZone />
          <GameNightZone />
        </div>
      </TavoloZone>

      <AgentZone />
      <StatsZone />

      <div className="md:flex md:gap-6">
        <FeedZone />
        <SuggestedZone />
      </div>

      <OverlayHybrid enableDeepLink>
        {({ entityType, entityId }) => (
          /* Render expanded MeepleCard based on entityType + entityId */
          <div>Overlay: {entityType}:{entityId}</div>
        )}
      </OverlayHybrid>
    </div>
  );
}
```

- [ ] **Step 3: Write basic test**

```tsx
import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';
import { DashboardRenderer } from '@/components/dashboard/DashboardRenderer';

// Mock all zone components + hooks (use absolute import paths, not relative)
vi.mock('@/components/dashboard/zones/ActiveSessionZone', () => ({ ActiveSessionZone: () => <div>sessions</div> }));
// ... mock other zones with @/ paths

describe('DashboardRenderer', () => {
  it('renders without crashing', () => {
    const { container } = render(<DashboardRenderer />);
    expect(container).toBeDefined();
  });
});
```

- [ ] **Step 4: Verify build + visual check**

Run: `cd apps/web && pnpm build`

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/dashboard/DashboardRenderer.tsx
git add apps/web/src/__tests__/components/DashboardRenderer.test.tsx
git commit -m "refactor(dashboard): rewrite DashboardRenderer with zone-based card layout"
```

---

## Phase 6: Live Session Integration

### Task 16: BackToSessionFAB

**Files:**
- Create: `apps/web/src/components/session/BackToSessionFAB.tsx`

- [ ] **Step 1: Implement BackToSessionFAB**

```tsx
// apps/web/src/components/session/BackToSessionFAB.tsx
'use client';

import Link from 'next/link';
import { Dice5 } from 'lucide-react';
import { useActiveSessions } from '@/lib/hooks/useActiveSessions';
import { usePathname } from 'next/navigation';

export function BackToSessionFAB() {
  const { data: sessions } = useActiveSessions();
  const pathname = usePathname();

  // Only show if there's an active session AND user is NOT on a live session page
  const activeSession = sessions?.[0];
  const isOnLiveSession = pathname.includes('/sessions/live/');

  if (!activeSession || isOnLiveSession) return null;

  return (
    <Link
      href={`/sessions/live/${activeSession.id}`}
      className="fixed bottom-6 right-6 z-50 flex items-center gap-2 px-4 py-3 rounded-full bg-primary text-primary-foreground shadow-lg hover:shadow-xl transition-all motion-reduce:transition-none"
    >
      <Dice5 className="w-5 h-5" />
      <span className="text-sm font-medium">Torna alla partita</span>
    </Link>
  );
}
```

- [ ] **Step 2: Add to UserShellClient**

Import and render `<BackToSessionFAB />` inside the shell, after `<main>`.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/session/BackToSessionFAB.tsx
git add apps/web/src/components/layout/UserShell/UserShellClient.tsx
git commit -m "feat(session): add BackToSessionFAB for quick return to active game"
```

---

### Task 17: Live Session Overlay Integration

**Files:**
- Modify: Live session page to wire overlay to context bar taps
- Already done: `LiveSessionContextBar` (Task 13) and `OverlayHybrid` (Task 8)

- [ ] **Step 1: Wire LiveSessionContextBar taps to overlay store**

Each tappable element in `LiveSessionContextBar` calls `useOverlayStore().open()`:
- Game cover tap → `open('game', gameId)`
- Avatar stack tap → `openDeck(playerItems, 0)`
- Turn tap → `openDeck(turnItems, 0)`
- Score tap → `open('session', sessionId)` (scoreboard view)

- [ ] **Step 2: Add OverlayHybrid to live session page**

In the live session layout or page, add `<OverlayHybrid enableDeepLink>` that renders the appropriate content based on `entityType`:
- `game` → expanded game card with rules/FAQ
- `player` → player card with session stats + CardDeck for all players
- `kb` → rules/FAQ card deck
- `session` → scoreboard view

- [ ] **Step 3: Verify build**

Run: `cd apps/web && pnpm build`

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/session/
git add apps/web/src/app/\(authenticated\)/sessions/live/
git commit -m "feat(session): wire live session context bar to overlay system"
```

---

## Phase 7: Onboarding

### Task 18: OnboardingFlow Component

**Files:**
- Create: `apps/web/src/components/dashboard/OnboardingFlow.tsx`
- Test: `apps/web/src/__tests__/components/OnboardingFlow.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { OnboardingFlow } from '@/components/dashboard/OnboardingFlow';

// Mock localStorage
const mockStorage: Record<string, string> = {};
vi.stubGlobal('localStorage', {
  getItem: (key: string) => mockStorage[key] ?? null,
  setItem: (key: string, value: string) => { mockStorage[key] = value; },
});

describe('OnboardingFlow', () => {
  beforeEach(() => {
    delete mockStorage['meepleai-onboarding-complete'];
  });

  it('does not render on server (SSR default)', () => {
    // On first render (before useEffect), shows nothing
    const { container } = render(<OnboardingFlow />);
    // Before effect runs, no onboarding visible (matches server output)
    expect(screen.queryByText('Benvenuto su MeepleAI!')).toBeNull();
  });

  it('does not render when onboarding is complete', () => {
    mockStorage['meepleai-onboarding-complete'] = 'true';
    render(<OnboardingFlow />);
    expect(screen.queryByText('Benvenuto su MeepleAI!')).toBeNull();
  });
});
```

- [ ] **Step 2: Implement OnboardingFlow**

SSR-safe: `useState(false)` default, read localStorage in `useEffect`. Shows welcome card + 3 step cards. Skip button sets localStorage. Completed steps tracked in localStorage JSON.

- [ ] **Step 3: Run tests**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/OnboardingFlow.test.tsx`

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/dashboard/OnboardingFlow.tsx
git add apps/web/src/__tests__/components/OnboardingFlow.test.tsx
git commit -m "feat(dashboard): add SSR-safe onboarding flow for new users"
```

---

## Phase 8: Polish & Verification

### Task 19: MeepleCard Environment-Aware Styling

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx`

- [ ] **Step 1: Add CSS variable reading for shadow and lift**

In the card's outer wrapper, use `var(--env-shadow-offset)` and `var(--env-card-lift)` for hover effects. This makes the card automatically adapt to Tavolo vs Hub environment without any conditional logic.

```tsx
// In the card wrapper style:
style={{
  '--card-shadow-offset': 'var(--env-shadow-offset, 2px)',
  '--card-shadow-color': 'var(--env-shadow-color, hsl(0 0% 0% / 0.08))',
  '--card-lift': 'var(--env-card-lift, -2px)',
}}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx
git commit -m "feat(card): make MeepleCard environment-aware via CSS variables"
```

---

### Task 20: Felt Texture Asset

**Files:**
- Create: `apps/web/public/textures/felt-subtle.webp`

- [ ] **Step 1: Create placeholder texture**

Generate or source a tileable felt/wood texture. Requirements:
- ~10KB max (webp compressed)
- Subtle, natural feel
- Tileable (seamless when repeated)
- Save to `public/textures/felt-subtle.webp`

If no texture is available, create a 1px transparent placeholder and add a TODO comment in `TavoloZone.tsx` noting the asset needs to be replaced with a real texture.

- [ ] **Step 2: Commit**

```bash
git add apps/web/public/textures/
git commit -m "feat(assets): add felt texture for Tavolo environment"
```

---

### Task 21: Final Verification

- [ ] **Step 1: Run full test suite**

Run: `cd apps/web && pnpm vitest run`
Expected: All tests pass, no regressions.

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No type errors.

- [ ] **Step 3: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: No lint errors.

- [ ] **Step 4: Build check**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds.

- [ ] **Step 5: Commit any fixes, then create final commit**

```bash
git add -u
git commit -m "chore: fix lint/type issues from dashboard navigation feature"
```

---

## Dependency Graph

```
Phase 1 (Foundation)
  Task 1: context-bar-store ─┐
  Task 2: overlay-store ─────┤
  Task 3: CSS env tokens ────┤
  Task 4: useScrollDirection ─┘
                               │
Phase 2 (Shell Migration)      ▼
  Task 5: ContextBar shell ←── Task 1, 4
  Task 6: UserShellClient ←─── Task 5, Task 15 (MUST wait for dashboard zones!)
  Task 7: UserTopNav slim ←─── (independent)
                               │
Phase 3 (Primitives)           ▼
  Task 8: OverlayHybrid ←───── Task 2
  Task 9: CardDeck ────────── (independent)
  Task 10: MeepleCard skeleton (independent)
  Task 11: TavoloZone ←─────── Task 3
                               │
Phase 4 (Context Bars)         ▼
  Task 12: DashboardContextBar ←── Task 5
  Task 13: 5 Context Bars ←────── Task 5, 2
                               │
Phase 5 (Dashboard)            ▼
  Task 14: Zone components ←─── Task 8, 9, 10, 11
  Task 15: DashboardRenderer ←─ Task 14, 12
                               │
Phase 6 (Live Session)         ▼
  Task 16: BackToSessionFAB ──── (independent)
  Task 17: Live overlay wiring ← Task 8, 13
                               │
Phase 7 (Onboarding)           ▼
  Task 18: OnboardingFlow ←──── Task 9
                               │
Phase 8 (Polish)               ▼
  Task 19: MeepleCard env ←──── Task 3
  Task 20: Texture asset ────── (independent)
  Task 21: Final verification
```

**Parallelizable tasks** (can be dispatched to independent agents):
- Tasks 1, 2, 3 (all foundation, no deps between them; Task 4 is verification only)
- Tasks 7, 9, 10 (independent of each other)
- Tasks 16, 18, 19, 20 (independent of each other)
