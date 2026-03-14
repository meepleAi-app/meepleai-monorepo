# Mobile UX: Card Browser & Navigation — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Overhaul the mobile UX with a card-centric browsing experience: full-screen card overlay with carousel, deck-stack history, cross-domain bottom bar, and scroll reduction.

**Architecture:** Overlay-based "Card Browser" pattern — new `MeepleCardBrowser` overlay renders on top of any page, managing carousel navigation and card history. The existing `MeepleCard` component is decomposed from a 1322-line monolith into focused modules. Navigation uses a hybrid bottom tab bar (5 quick domains) + full hub page.

**Tech Stack:** Next.js 16, React 19, Tailwind 4, framer-motion (already in project), Zustand (already in project), Sheet component (already in project), Vitest + React Testing Library

**Spec:** `docs/specs/2026-03-14-mobile-ux-card-browser-design.md`

---

## File Structure

### New Files

```
apps/web/src/components/ui/data-display/
├── meeple-card/                              # Decomposed MeepleCard
│   ├── index.ts                              # Public re-exports (API preserved)
│   ├── MeepleCard.tsx                        # Main component: props + variant routing
│   ├── variants/
│   │   ├── MeepleCardGrid.tsx                # Grid variant extraction
│   │   ├── MeepleCardList.tsx                # List variant extraction
│   │   ├── MeepleCardCompact.tsx             # Compact variant extraction
│   │   ├── MeepleCardFeatured.tsx            # Featured variant extraction
│   │   ├── MeepleCardHero.tsx                # Hero variant extraction
│   │   └── MeepleCardExpanded.tsx            # NEW: full-screen focus variant
│   ├── parts/
│   │   ├── CardCover.tsx                     # Cover image + shimmer
│   │   ├── CardBadges.tsx                    # Status, rating, entity link badges
│   │   ├── CardMetadata.tsx                  # Metadata chips display
│   │   ├── CardActions.tsx                   # Quick actions + wishlist
│   │   ├── CardFlip.tsx                      # Existing 3D flip extraction
│   │   └── CardTagStrip.tsx                  # Tag strip horizontal/vertical
│   ├── hooks/
│   │   ├── useMobileInteraction.ts           # Tap detection, bottom sheet
│   │   └── useCardAnimation.ts               # Hover transforms, shimmer
│   ├── types.ts                              # All interfaces/types
│   └── constants.ts                          # Entity colors, sizing, animations
│
├── meeple-card-browser/                      # Card Browser overlay system
│   ├── index.ts                              # Public exports
│   ├── MeepleCardBrowser.tsx                 # Overlay + carousel engine
│   ├── CardBrowserContext.tsx                # React Context: state, history
│   ├── DeckStackDrawer.tsx                   # Right drawer: card history
│   ├── CardLinkBar.tsx                       # Tabbed related-card links
│   └── useMeepleCardBrowser.ts               # Hook: open/close/navigate

apps/web/src/components/layout/
├── MobileBottomBar/
│   ├── index.ts
│   └── MobileBottomBar.tsx                   # Fixed bottom tab bar (mobile only)
├── DomainHub/
│   ├── index.ts
│   └── DomainHub.tsx                         # Full domain grid page

apps/web/src/app/(authenticated)/hub/
│   └── page.tsx                              # DomainHub route
```

### Modified Files

```
apps/web/src/components/ui/data-display/meeple-card.tsx         # DELETE (replaced by meeple-card/)
apps/web/src/components/ui/data-display/meeple-card-styles.ts   # Add 'expanded' variant
apps/web/src/components/layout/FloatingActionBar/FloatingActionBar.tsx  # Add md:hidden on mobile
apps/web/src/app/(authenticated)/layout.tsx                     # Add MobileBottomBar + CardBrowserProvider
```

### Test Files

```
apps/web/src/components/ui/data-display/__tests__/
├── meeple-card-decomposition.test.tsx        # Verify decomposed card matches original
├── meeple-card-expanded.test.tsx             # Expanded variant unit tests
├── meeple-card-browser.test.tsx              # Browser overlay + carousel tests
├── card-browser-context.test.tsx             # Context state management tests
├── deck-stack-drawer.test.tsx                # History drawer tests
├── card-link-bar.test.tsx                    # Related links tabs tests

apps/web/src/components/layout/__tests__/
├── mobile-bottom-bar.test.tsx                # Bottom tab bar tests
├── domain-hub.test.tsx                       # Hub page tests
```

---

## Chunk 1: MeepleCard Decomposition

The foundation — extract the 1322-line monolith into focused modules without changing behavior.

### Task 1: Extract types and constants

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/types.ts`
- Create: `apps/web/src/components/ui/data-display/meeple-card/constants.ts`
- Read: `apps/web/src/components/ui/data-display/meeple-card.tsx:132-310` (types section)
- Read: `apps/web/src/components/ui/data-display/meeple-card-styles.ts` (entity colors, CVA)

- [ ] **Step 1: Read the full meeple-card.tsx to identify all type exports**

Read `apps/web/src/components/ui/data-display/meeple-card.tsx` lines 132-310 for all interfaces and types.
Read lines 1294-1323 for all type exports.

- [ ] **Step 2: Create types.ts with all extracted interfaces**

Create `apps/web/src/components/ui/data-display/meeple-card/types.ts`.
Move ALL interfaces from meeple-card.tsx: `MeepleCardMetadata`, `MeepleCardAction`, `MeepleCardProps`, and all feature-specific types.
Import `MeepleEntityType` and `MeepleCardVariant` from `../meeple-card-styles`.
Re-export everything.

- [ ] **Step 3: Create constants.ts with shared constants**

Create `apps/web/src/components/ui/data-display/meeple-card/constants.ts`.
Move any hardcoded values (animation durations, sizing constants) from the main component.
Re-export `entityColors` from `../meeple-card-styles`.

- [ ] **Step 4: Verify types compile**

Run: `cd apps/web && npx tsc --noEmit --pretty 2>&1 | head -20`
Expected: No errors from the new files (they're not imported yet)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/types.ts apps/web/src/components/ui/data-display/meeple-card/constants.ts
git commit -m "refactor(meeple-card): extract types and constants to dedicated modules"
```

### Task 2: Extract shared parts (CardCover, CardBadges, CardMetadata, CardActions, CardFlip, CardTagStrip)

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/CardCover.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/CardBadges.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/CardMetadata.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/CardActions.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/CardFlip.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/parts/CardTagStrip.tsx`
- Read: `apps/web/src/components/ui/data-display/meeple-card.tsx` (full file, identify render sections)
- Read: `apps/web/src/components/ui/data-display/meeple-card-parts.ts` (existing parts barrel)

- [ ] **Step 1: Read the existing meeple-card-parts.ts barrel file**

Read `apps/web/src/components/ui/data-display/meeple-card-parts.ts` to see what's already extracted vs inline.

- [ ] **Step 2: Read the render sections of meeple-card.tsx**

Read `apps/web/src/components/ui/data-display/meeple-card.tsx` lines 600-1000 to identify the cover image rendering, badges, metadata chips, actions, flip, and tag strip inline code.

- [ ] **Step 3: Extract CardCover.tsx**

Extract the cover image rendering logic (including shimmer effect, aspect ratio handling per variant, `object-fit: cover`, and the shimmer animation overlay) into `parts/CardCover.tsx`.
Props: `{ imageUrl, variant, entity, showShimmer, className }`.
Import from `../types` and `../../meeple-card-styles`.

- [ ] **Step 4: Extract CardBadges.tsx**

Extract status badge rendering, rating display, and entity link badge into `parts/CardBadges.tsx`.
Re-use existing `StatusBadge`, `RatingDisplay`, `EntityLinkBadge` components via import.
Props: `{ status, rating, ratingMax, showStatusIcon, entity, linkCount, firstLinkPreview, onLinksClick, variant }`.

- [ ] **Step 5: Extract CardMetadata.tsx**

Extract metadata chips rendering into `parts/CardMetadata.tsx`.
Re-use existing `MetadataChips` from `meeple-card-parts`.
Props: `{ metadata, variant, className }`.

- [ ] **Step 6: Extract CardActions.tsx**

Extract quick actions menu, wishlist button, and action buttons into `parts/CardActions.tsx`.
Re-use existing `QuickActionsMenu`, `WishlistButton`, `ActionButtons`.
Props: `{ quickActions, showWishlist, isWishlisted, onWishlistToggle, actions, variant, userRole, entityId }`.

- [ ] **Step 7: Extract CardFlip.tsx**

Extract the existing `FlipCard` wrapper and back-content rendering into `parts/CardFlip.tsx`.
This is extraction of existing code from `meeple-card.tsx` lines that wrap content in `FlipCard`.
Props: `{ flippable, flipData, entity, children }`.

- [ ] **Step 8: Extract CardTagStrip.tsx**

Extract tag strip rendering into `parts/CardTagStrip.tsx`.
Re-use existing `TagStrip`, `VerticalTagStack` components.
Props: `{ tags, maxVisibleTags, showTagStrip, variant }`.

- [ ] **Step 9: Verify types compile**

Run: `cd apps/web && npx tsc --noEmit --pretty 2>&1 | head -30`
Expected: No type errors

- [ ] **Step 10: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/
git commit -m "refactor(meeple-card): extract shared parts (cover, badges, metadata, actions, flip, tags)"
```

### Task 3: Extract hooks

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/hooks/useMobileInteraction.ts`
- Create: `apps/web/src/components/ui/data-display/meeple-card/hooks/useCardAnimation.ts`
- Read: `apps/web/src/components/ui/data-display/meeple-card.tsx:550-620` (mobile interaction)
- Read: `apps/web/src/components/ui/data-display/meeple-card.tsx:450-550` (animation logic)

- [ ] **Step 1: Read the mobile interaction code**

Read `apps/web/src/components/ui/data-display/meeple-card.tsx` lines 550-620 for the mobile tap handler, media query detection, and bottom sheet logic.

- [ ] **Step 2: Create useMobileInteraction.ts**

Extract:
- `window.matchMedia('(max-width: 768px)')` detection
- Two-tap navigation logic (1st tap: show actions, 2nd tap: navigate)
- `showMobileActions` state
- `handleMobileClick` function

Returns: `{ isMobile, showMobileActions, setShowMobileActions, handleMobileClick }`.

- [ ] **Step 3: Create useCardAnimation.ts**

Extract hover transform logic (translateY, scale) and shimmer animation trigger.
Returns: `{ isHovered, onMouseEnter, onMouseLeave, showShimmer }`.

- [ ] **Step 4: Verify types compile**

Run: `cd apps/web && npx tsc --noEmit --pretty 2>&1 | head -20`

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/hooks/
git commit -m "refactor(meeple-card): extract useMobileInteraction and useCardAnimation hooks"
```

### Task 4: Extract variant renderers

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardList.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardCompact.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardFeatured.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardHero.tsx`
- Read: `apps/web/src/components/ui/data-display/meeple-card.tsx:620-1292` (render logic)

- [ ] **Step 1: Read the full render logic of meeple-card.tsx**

Read lines 620-1292 to understand how variant-specific rendering is done. Identify the switch/conditional blocks that render different layouts per variant.

- [ ] **Step 2: Create MeepleCardGrid.tsx**

Extract the grid variant rendering: 7:10 cover aspect, `p-4` padding, hover translateY, entity glow ring.
Uses `CardCover`, `CardBadges`, `CardMetadata`, `CardActions`, `CardTagStrip` from `../parts/`.
Props: subset of `MeepleCardProps` relevant to grid.

- [ ] **Step 3: Create MeepleCardList.tsx**

Extract list variant: horizontal layout, 64x64 thumbnail, `p-3` padding.
Props: subset of `MeepleCardProps` relevant to list.

- [ ] **Step 4: Create MeepleCardCompact.tsx**

Extract compact variant: inline, 40x40 avatar, `p-2` padding, minimal info.
Props: subset of `MeepleCardProps` relevant to compact.

- [ ] **Step 5: Create MeepleCardFeatured.tsx**

Extract featured variant: 400px width, 16:9 cover, `px-5 py-4`.
Props: subset of `MeepleCardProps` relevant to featured.

- [ ] **Step 6: Create MeepleCardHero.tsx**

Extract hero variant: full-width, min-h-320px, absolute inset-0 cover.
Props: subset of `MeepleCardProps` relevant to hero.

- [ ] **Step 7: Verify types compile**

Run: `cd apps/web && npx tsc --noEmit --pretty 2>&1 | head -30`

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/
git commit -m "refactor(meeple-card): extract variant renderers (grid, list, compact, featured, hero)"
```

### Task 5: Create main MeepleCard.tsx + index.ts, delete old file

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card/index.ts`
- Delete: `apps/web/src/components/ui/data-display/meeple-card.tsx` (old monolith)

- [ ] **Step 1: Create MeepleCard.tsx — the router component**

This component:
1. Accepts all `MeepleCardProps`
2. Routes to the correct variant based on `variant` prop
3. Wraps with `React.memo`
4. Handles common logic (mobile interaction, flip wrapping)

```tsx
// Pseudo-structure:
const MeepleCard = React.memo(function MeepleCard(props: MeepleCardProps) {
  const { variant = 'grid', ...rest } = props;

  switch (variant) {
    case 'grid': return <MeepleCardGrid {...rest} />;
    case 'list': return <MeepleCardList {...rest} />;
    case 'compact': return <MeepleCardCompact {...rest} />;
    case 'featured': return <MeepleCardFeatured {...rest} />;
    case 'hero': return <MeepleCardHero {...rest} />;
    case 'expanded': return <MeepleCardExpanded {...rest} />;
    default: return <MeepleCardGrid {...rest} />;
  }
});
```

- [ ] **Step 2: Create index.ts with all re-exports**

Must re-export EXACTLY the same public API as the old `meeple-card.tsx` lines 1294-1323:

```typescript
export { MeepleCard } from './MeepleCard';
export { MeepleCardSkeleton } from '../meeple-card-parts';
export { entityColors } from '../meeple-card-styles';
export type { MeepleEntityType, MeepleCardVariant } from '../meeple-card-styles';
export type { MeepleCardMetadata as MeepleMetadata, MeepleCardAction as MeepleAction } from './types';
// ... all other type re-exports matching old file exactly
```

- [ ] **Step 3: Write the decomposition verification test**

Create `apps/web/src/components/ui/data-display/__tests__/meeple-card-decomposition.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { MeepleCard } from '../meeple-card';

// Verify each variant renders without errors
describe('MeepleCard decomposition — smoke tests', () => {
  const variants = ['grid', 'list', 'compact', 'featured', 'hero'] as const;
  const entities = ['game', 'player', 'session', 'agent', 'kb', 'chatSession', 'event', 'toolkit', 'custom'] as const;

  for (const variant of variants) {
    it(`renders ${variant} variant`, () => {
      render(<MeepleCard entity="game" variant={variant} title="Test" />);
      expect(screen.getByText('Test')).toBeInTheDocument();
    });
  }

  for (const entity of entities) {
    it(`renders ${entity} entity type`, () => {
      render(<MeepleCard entity={entity} variant="grid" title={`${entity} card`} />);
      expect(screen.getByText(`${entity} card`)).toBeInTheDocument();
    });
  }
});
```

- [ ] **Step 4: Run the decomposition test**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/__tests__/meeple-card-decomposition.test.tsx`
Expected: All tests pass

- [ ] **Step 5: Run ALL existing MeepleCard tests to verify no regressions**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/__tests__/meeple-card`
Expected: ALL existing tests pass (meeple-card.test.tsx, meeple-card-agent.test.tsx, meeple-card-chat.test.tsx, snapshot, a11y, permissions, compound, contexts, improvements, drawer)

- [ ] **Step 6: Delete old monolith file**

Delete `apps/web/src/components/ui/data-display/meeple-card.tsx`.
The `meeple-card/index.ts` now serves the same import path.

Note: TypeScript module resolution resolves `from '../meeple-card'` to either `meeple-card.tsx` (file) or `meeple-card/index.ts` (directory). With the file deleted, the directory takes over — same import path, no breaking changes.

- [ ] **Step 7: Full typecheck**

Run: `cd apps/web && npx tsc --noEmit --pretty 2>&1 | head -50`
Expected: Zero errors

- [ ] **Step 8: Run all existing tests one more time**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/__tests__/meeple-card`
Expected: ALL pass — decomposition complete with zero regressions

- [ ] **Step 9: Commit**

```bash
git add -A apps/web/src/components/ui/data-display/meeple-card/
git add apps/web/src/components/ui/data-display/__tests__/meeple-card-decomposition.test.tsx
git rm apps/web/src/components/ui/data-display/meeple-card.tsx
git commit -m "refactor(meeple-card): complete decomposition from 1322-line monolith to modular structure

Extracts MeepleCard into focused modules:
- variants/: Grid, List, Compact, Featured, Hero
- parts/: Cover, Badges, Metadata, Actions, Flip, TagStrip
- hooks/: useMobileInteraction, useCardAnimation
- types.ts + constants.ts
Public API unchanged — all existing imports work via index.ts re-exports."
```

---

## Chunk 2: CardBrowserContext + MeepleCardExpanded

The state layer and the new card variant — needed before the overlay.

### Task 6: Create CardBrowserContext

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card-browser/CardBrowserContext.tsx`
- Create: `apps/web/src/components/ui/data-display/__tests__/card-browser-context.test.tsx`

- [ ] **Step 1: Write the failing test for CardBrowserContext**

Create `apps/web/src/components/ui/data-display/__tests__/card-browser-context.test.tsx`:

```tsx
import { renderHook, act } from '@testing-library/react';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { CardBrowserProvider, useCardBrowser } from '../meeple-card-browser/CardBrowserContext';

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <CardBrowserProvider>{children}</CardBrowserProvider>
);

const mockCards = [
  { id: '1', entity: 'game' as const, title: 'Catan', color: '25 95% 45%' },
  { id: '2', entity: 'game' as const, title: 'Wingspan', color: '25 95% 45%' },
  { id: '3', entity: 'agent' as const, title: 'RulesBot', color: '38 92% 50%' },
];

describe('CardBrowserContext', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('starts closed with empty state', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    expect(result.current.isOpen).toBe(false);
    expect(result.current.cards).toEqual([]);
    expect(result.current.history).toEqual([]);
  });

  it('opens with cards and index', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    act(() => result.current.open(mockCards, 0));
    expect(result.current.isOpen).toBe(true);
    expect(result.current.cards).toEqual(mockCards);
    expect(result.current.currentIndex).toBe(0);
  });

  it('pushes to history on open, no consecutive duplicates', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    act(() => result.current.open(mockCards, 0));
    expect(result.current.history).toHaveLength(1);
    expect(result.current.history[0].id).toBe('1');

    // Navigate to same card — no duplicate
    act(() => result.current.navigateTo(mockCards[0]));
    expect(result.current.history).toHaveLength(1);

    // Navigate to different card
    act(() => result.current.navigateTo(mockCards[1]));
    expect(result.current.history).toHaveLength(2);
  });

  it('enforces max 50 history entries (FIFO)', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    act(() => result.current.open(mockCards, 0));
    for (let i = 0; i < 55; i++) {
      act(() => result.current.navigateTo({
        id: `card-${i}`, entity: 'game', title: `Game ${i}`, color: '25 95% 45%',
      }));
    }
    expect(result.current.history.length).toBeLessThanOrEqual(50);
  });

  it('closes and resets isOpen', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    act(() => result.current.open(mockCards, 0));
    act(() => result.current.close());
    expect(result.current.isOpen).toBe(false);
    // history persists after close
    expect(result.current.history.length).toBeGreaterThan(0);
  });

  it('clears history', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    act(() => result.current.open(mockCards, 0));
    act(() => result.current.navigateTo(mockCards[1]));
    act(() => result.current.clearHistory());
    expect(result.current.history).toEqual([]);
  });

  it('resets cards[] when opening from different list', () => {
    const { result } = renderHook(() => useCardBrowser(), { wrapper });
    act(() => result.current.open(mockCards, 0));
    const newCards = [{ id: '10', entity: 'agent' as const, title: 'NewBot', color: '38 92% 50%' }];
    act(() => result.current.open(newCards, 0));
    expect(result.current.cards).toEqual(newCards);
    // history still has entries from first list
    expect(result.current.history.length).toBeGreaterThan(0);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/__tests__/card-browser-context.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement CardBrowserContext.tsx**

Create `apps/web/src/components/ui/data-display/meeple-card-browser/CardBrowserContext.tsx`:

```tsx
'use client';

import React, { createContext, useContext, useCallback, useState, useEffect } from 'react';
import type { MeepleEntityType } from '../meeple-card-styles';

export interface CardRef {
  id: string;
  entity: MeepleEntityType;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  color: string;
}

interface CardBrowserState {
  isOpen: boolean;
  cards: CardRef[];
  currentIndex: number;
  history: CardRef[];
}

interface CardBrowserActions {
  open: (cards: CardRef[], index: number) => void;
  close: () => void;
  setIndex: (index: number) => void;
  navigateTo: (card: CardRef) => void;
  clearHistory: () => void;
}

type CardBrowserContextValue = CardBrowserState & CardBrowserActions;

const CardBrowserContext = createContext<CardBrowserContextValue | null>(null);

const MAX_HISTORY = 50;
const STORAGE_KEY = 'meeple-card-browser-history';

function loadHistory(): CardRef[] {
  if (typeof window === 'undefined') return [];
  try {
    const stored = sessionStorage.getItem(STORAGE_KEY);
    return stored ? JSON.parse(stored) : [];
  } catch {
    return [];
  }
}

function saveHistory(history: CardRef[]) {
  if (typeof window === 'undefined') return;
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(history));
  } catch { /* quota exceeded — silently ignore */ }
}

export function CardBrowserProvider({ children }: { children: React.ReactNode }) {
  const [state, setState] = useState<CardBrowserState>({
    isOpen: false,
    cards: [],
    currentIndex: 0,
    history: [],
  });

  // Load history from sessionStorage on mount
  useEffect(() => {
    setState(prev => ({ ...prev, history: loadHistory() }));
  }, []);

  // Persist history to sessionStorage on change
  useEffect(() => {
    saveHistory(state.history);
  }, [state.history]);

  const pushHistory = useCallback((card: CardRef) => {
    setState(prev => {
      const last = prev.history[prev.history.length - 1];
      if (last?.id === card.id) return prev; // no consecutive duplicates
      const newHistory = [...prev.history, card];
      if (newHistory.length > MAX_HISTORY) newHistory.shift(); // FIFO
      return { ...prev, history: newHistory };
    });
  }, []);

  const open = useCallback((cards: CardRef[], index: number) => {
    const card = cards[index];
    setState(prev => ({
      ...prev,
      isOpen: true,
      cards,
      currentIndex: index,
    }));
    if (card) pushHistory(card);
  }, [pushHistory]);

  const close = useCallback(() => {
    setState(prev => ({ ...prev, isOpen: false }));
  }, []);

  const setIndex = useCallback((index: number) => {
    setState(prev => {
      const card = prev.cards[index];
      if (card) pushHistory(card);
      return { ...prev, currentIndex: index };
    });
  }, [pushHistory]);

  const navigateTo = useCallback((card: CardRef) => {
    pushHistory(card);
    setState(prev => {
      const existingIndex = prev.cards.findIndex(c => c.id === card.id);
      if (existingIndex >= 0) {
        return { ...prev, currentIndex: existingIndex };
      }
      // Card from different context — add to current list
      const newCards = [...prev.cards, card];
      return { ...prev, cards: newCards, currentIndex: newCards.length - 1 };
    });
  }, [pushHistory]);

  const clearHistory = useCallback(() => {
    setState(prev => ({ ...prev, history: [] }));
  }, []);

  const value: CardBrowserContextValue = {
    ...state,
    open,
    close,
    setIndex,
    navigateTo,
    clearHistory,
  };

  return (
    <CardBrowserContext.Provider value={value}>
      {children}
    </CardBrowserContext.Provider>
  );
}

export function useCardBrowser(): CardBrowserContextValue {
  const context = useContext(CardBrowserContext);
  if (!context) {
    throw new Error('useCardBrowser must be used within CardBrowserProvider');
  }
  return context;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/__tests__/card-browser-context.test.tsx`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-browser/CardBrowserContext.tsx
git add apps/web/src/components/ui/data-display/__tests__/card-browser-context.test.tsx
git commit -m "feat(card-browser): add CardBrowserContext with history management and sessionStorage persistence"
```

### Task 7: Add 'expanded' variant to styles + create MeepleCardExpanded

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-styles.ts:25` (add 'expanded' to variant type)
- Modify: `apps/web/src/components/ui/data-display/meeple-card-styles.ts:58-101` (add expanded CVA variant)
- Create: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardExpanded.tsx`
- Create: `apps/web/src/components/ui/data-display/__tests__/meeple-card-expanded.test.tsx`

- [ ] **Step 1: Write the failing test for MeepleCardExpanded**

Create `apps/web/src/components/ui/data-display/__tests__/meeple-card-expanded.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { MeepleCard } from '../meeple-card';

vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    <img src={src} alt={alt} {...props} />
  ),
}));

describe('MeepleCardExpanded', () => {
  it('renders with expanded variant', () => {
    render(
      <MeepleCard
        entity="game"
        variant="expanded"
        title="Catan"
        subtitle="Klaus Teuber"
        imageUrl="/catan.jpg"
      />
    );
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Klaus Teuber')).toBeInTheDocument();
  });

  it('clamps title to 2 lines', () => {
    render(
      <MeepleCard entity="game" variant="expanded" title="Very Long Title" />
    );
    const title = screen.getByText('Very Long Title');
    expect(title.className).toContain('line-clamp-2');
  });

  it('renders metadata chips (max 4)', () => {
    render(
      <MeepleCard
        entity="game"
        variant="expanded"
        title="Catan"
        metadata={[
          { value: '2-4 players' },
          { value: '60min' },
          { value: 'Strategy' },
          { value: '2024' },
          { value: 'Extra' },  // should not be visible
        ]}
      />
    );
    expect(screen.getByText('2-4 players')).toBeInTheDocument();
    expect(screen.getByText('60min')).toBeInTheDocument();
    expect(screen.getByText('Strategy')).toBeInTheDocument();
    expect(screen.getByText('2024')).toBeInTheDocument();
    expect(screen.queryByText('Extra')).not.toBeInTheDocument();
  });

  it('renders "See all" action button', () => {
    const onClick = vi.fn();
    render(
      <MeepleCard
        entity="game"
        variant="expanded"
        title="Catan"
        onClick={onClick}
      />
    );
    // The "See all" link should be present
    expect(screen.getByRole('button', { name: /see all|vedi tutto/i })).toBeInTheDocument();
  });

  it('renders all 9 entity types without errors', () => {
    const entities = ['game', 'player', 'session', 'agent', 'kb', 'chatSession', 'event', 'toolkit', 'custom'] as const;
    for (const entity of entities) {
      const { unmount } = render(
        <MeepleCard entity={entity} variant="expanded" title={`${entity} card`} />
      );
      expect(screen.getByText(`${entity} card`)).toBeInTheDocument();
      unmount();
    }
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/__tests__/meeple-card-expanded.test.tsx`
Expected: FAIL — 'expanded' is not a valid variant

- [ ] **Step 3: Add 'expanded' to MeepleCardVariant type**

Edit `apps/web/src/components/ui/data-display/meeple-card-styles.ts` line 25:

```typescript
export type MeepleCardVariant = 'grid' | 'list' | 'compact' | 'featured' | 'hero' | 'expanded';
```

- [ ] **Step 4: Add expanded CVA variants**

Edit `meeple-card-styles.ts` to add `expanded` to `meepleCardVariants`, `coverVariants`, and `contentVariants`:

```typescript
// In meepleCardVariants:
expanded: [
  'flex flex-col rounded-2xl overflow-hidden',
  'bg-card border border-border/50',
  '[box-shadow:var(--shadow-warm-md)]',
],

// In coverVariants:
expanded: 'w-full rounded-t-2xl',

// In contentVariants:
expanded: 'flex-1 flex flex-col px-4 py-3 gap-2',
```

- [ ] **Step 5: Create MeepleCardExpanded.tsx**

Create `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardExpanded.tsx`.

Key layout rules:
- Cover: `max(40vh, 200px)` height, `object-fit: cover`
- Title: `text-lg line-clamp-2`
- Subtitle: `text-sm line-clamp-1 text-muted-foreground`
- Metadata: `flex flex-wrap gap-2`, max 4 chips visible (slice first 4)
- Tags: `flex overflow-x-auto gap-1.5`, single row, `flex-nowrap`
- Action bar: wishlist + "See all" button
- Status badges overlaid on cover image (absolute positioning)
- Entity color border-top on cover

- [ ] **Step 6: Add expanded case to MeepleCard router**

Edit `apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx` to add:

```tsx
case 'expanded': return <MeepleCardExpanded {...rest} />;
```

- [ ] **Step 7: Run test to verify it passes**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/__tests__/meeple-card-expanded.test.tsx`
Expected: ALL PASS

- [ ] **Step 8: Run all MeepleCard tests for regression**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/__tests__/meeple-card`
Expected: ALL PASS

- [ ] **Step 9: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-styles.ts
git add apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardExpanded.tsx
git add apps/web/src/components/ui/data-display/__tests__/meeple-card-expanded.test.tsx
git commit -m "feat(meeple-card): add expanded variant for full-screen card focus view"
```

---

## Chunk 3: MeepleCardBrowser Overlay + DeckStackDrawer

### Task 8: Create MeepleCardBrowser overlay with carousel

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card-browser/MeepleCardBrowser.tsx`
- Create: `apps/web/src/components/ui/data-display/meeple-card-browser/useMeepleCardBrowser.ts`
- Create: `apps/web/src/components/ui/data-display/meeple-card-browser/index.ts`
- Create: `apps/web/src/components/ui/data-display/__tests__/meeple-card-browser.test.tsx`

- [ ] **Step 1: Write the failing test for MeepleCardBrowser**

Create `apps/web/src/components/ui/data-display/__tests__/meeple-card-browser.test.tsx`:

```tsx
import { render, screen, fireEvent, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { CardBrowserProvider, useCardBrowser } from '../meeple-card-browser/CardBrowserContext';
import { MeepleCardBrowser } from '../meeple-card-browser';

vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    <img src={src} alt={alt} {...props} />
  ),
}));

vi.mock('framer-motion', () => ({
  motion: {
    div: React.forwardRef(({ children, ...props }: any, ref: any) => (
      <div ref={ref} {...props}>{children}</div>
    )),
  },
  AnimatePresence: ({ children }: any) => <>{children}</>,
}));

const mockCards = [
  { id: '1', entity: 'game' as const, title: 'Catan', color: '25 95% 45%' },
  { id: '2', entity: 'game' as const, title: 'Wingspan', color: '25 95% 45%' },
];

// Helper to open browser programmatically
function TestHarness() {
  const { open, isOpen } = useCardBrowser();
  return (
    <>
      <button onClick={() => open(mockCards, 0)}>Open</button>
      <MeepleCardBrowser />
      {isOpen && <span data-testid="is-open" />}
    </>
  );
}

describe('MeepleCardBrowser', () => {
  it('does not render when closed', () => {
    render(
      <CardBrowserProvider>
        <MeepleCardBrowser />
      </CardBrowserProvider>
    );
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders overlay when opened', () => {
    render(
      <CardBrowserProvider>
        <TestHarness />
      </CardBrowserProvider>
    );
    fireEvent.click(screen.getByText('Open'));
    expect(screen.getByTestId('is-open')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('shows carousel indicator n/total', () => {
    render(
      <CardBrowserProvider>
        <TestHarness />
      </CardBrowserProvider>
    );
    fireEvent.click(screen.getByText('Open'));
    expect(screen.getByText('1/2')).toBeInTheDocument();
  });

  it('closes on ESC key', () => {
    render(
      <CardBrowserProvider>
        <TestHarness />
      </CardBrowserProvider>
    );
    fireEvent.click(screen.getByText('Open'));
    expect(screen.getByTestId('is-open')).toBeInTheDocument();
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByTestId('is-open')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/__tests__/meeple-card-browser.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Create useMeepleCardBrowser.ts hook**

Hook for browser history integration (pushState/popstate):

```typescript
export function useBrowserHistoryIntegration(isOpen: boolean, onClose: () => void) {
  useEffect(() => {
    if (isOpen) {
      history.pushState({ cardBrowser: true }, '');
    }
    const handlePopState = (e: PopStateEvent) => {
      if (isOpen) onClose();
    };
    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, [isOpen, onClose]);
}
```

Also includes ESC key handler and swipe-down gesture detection.

- [ ] **Step 4: Create MeepleCardBrowser.tsx**

The overlay component:
- Renders only when `isOpen` from context
- `framer-motion` AnimatePresence for enter/exit animations
- Contains: MeepleCardExpanded (current card) + carousel container
- Carousel: `scroll-snap-type: x mandatory` container with cards
- Indicator: `currentIndex + 1 / cards.length`
- Close: ESC, backdrop tap, swipe down
- Accessibility: `role="dialog"`, `aria-modal="true"`, focus trap

- [ ] **Step 5: Create index.ts barrel export**

```typescript
export { MeepleCardBrowser } from './MeepleCardBrowser';
export { CardBrowserProvider, useCardBrowser } from './CardBrowserContext';
export type { CardRef } from './CardBrowserContext';
```

- [ ] **Step 6: Run test to verify it passes**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/__tests__/meeple-card-browser.test.tsx`
Expected: ALL PASS

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-browser/
git add apps/web/src/components/ui/data-display/__tests__/meeple-card-browser.test.tsx
git commit -m "feat(card-browser): add MeepleCardBrowser overlay with carousel, history integration, and keyboard controls"
```

### Task 9: Create DeckStackDrawer

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card-browser/DeckStackDrawer.tsx`
- Create: `apps/web/src/components/ui/data-display/__tests__/deck-stack-drawer.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/ui/data-display/__tests__/deck-stack-drawer.test.tsx`:

```tsx
import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { CardBrowserProvider, useCardBrowser } from '../meeple-card-browser/CardBrowserContext';
import { DeckStackDrawer } from '../meeple-card-browser/DeckStackDrawer';

vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    <img src={src} alt={alt} {...props} />
  ),
}));

const mockHistory = [
  { id: '1', entity: 'game' as const, title: 'Catan', color: '25 95% 45%' },
  { id: '2', entity: 'agent' as const, title: 'RulesBot', color: '38 92% 50%' },
  { id: '3', entity: 'game' as const, title: 'Wingspan', color: '25 95% 45%' },
];

// Helper: pre-populate history then render drawer
function TestHarness({ currentCardId }: { currentCardId: string }) {
  const { open, navigateTo } = useCardBrowser();
  React.useEffect(() => {
    open([mockHistory[0]], 0);
    mockHistory.slice(1).forEach(card => navigateTo(card));
  }, []);
  return <DeckStackDrawer isOpen currentCardId={currentCardId} onClose={vi.fn()} />;
}

describe('DeckStackDrawer', () => {
  it('renders history entries as compact cards', () => {
    render(
      <CardBrowserProvider>
        <TestHarness currentCardId="3" />
      </CardBrowserProvider>
    );
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('RulesBot')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('shows entry count in header', () => {
    render(
      <CardBrowserProvider>
        <TestHarness currentCardId="3" />
      </CardBrowserProvider>
    );
    expect(screen.getByText(/3/)).toBeInTheDocument(); // "History (3)"
  });

  it('renders clear history button', () => {
    render(
      <CardBrowserProvider>
        <TestHarness currentCardId="3" />
      </CardBrowserProvider>
    );
    expect(screen.getByRole('button', { name: /clear|cancella/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/__tests__/deck-stack-drawer.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement DeckStackDrawer.tsx**

Create `apps/web/src/components/ui/data-display/meeple-card-browser/DeckStackDrawer.tsx`:

Uses:
- `Sheet`, `SheetContent`, `SheetHeader`, `SheetTitle` from `@/components/ui/navigation/sheet` (side: `right`)
- `MeepleCard` compact variant for each history entry
- `useCardBrowser()` for history, navigateTo, clearHistory
- Width: `w-[80vw] max-w-[320px]`
- Current card highlighted: `border-2` with entity color, `bg-[hsl(color/0.05)]`
- Props: `{ isOpen: boolean; currentCardId: string; onClose: () => void }`
- Each entry is a button wrapping a compact MeepleCard — on click calls `navigateTo(card)` then `onClose()`
- Footer: "Clear history" button that calls `clearHistory()`
- Vertical scroll: `overflow-y-auto max-h-[calc(100vh-8rem)]`

Note: The Sheet component from shadcn does NOT support edge-swipe activation natively. The drawer is activated only via the stack icon button in the MeepleCardBrowser header. Edge-swipe from right screen border is deferred — it would require custom touch event handling with `touchstart`/`touchmove` threshold detection, which adds complexity without blocking the core UX. Can be added as a follow-up enhancement.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/__tests__/deck-stack-drawer.test.tsx`
Expected: ALL PASS

- [ ] **Step 5: Integrate DeckStackDrawer into MeepleCardBrowser**

Add a stack icon button (Layers icon from lucide-react) in the MeepleCardBrowser overlay header. Tapping it toggles the drawer open/closed via local state `showDeckStack`.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-browser/DeckStackDrawer.tsx
git add apps/web/src/components/ui/data-display/__tests__/deck-stack-drawer.test.tsx
git commit -m "feat(card-browser): add DeckStackDrawer with card history, compact card entries, and clear functionality"
```

### Task 10: Create CardLinkBar

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card-browser/CardLinkBar.tsx`
- Create: `apps/web/src/components/ui/data-display/__tests__/card-link-bar.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/ui/data-display/__tests__/card-link-bar.test.tsx`:

```tsx
import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { CardLinkBar } from '../meeple-card-browser/CardLinkBar';

vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    <img src={src} alt={alt} {...props} />
  ),
}));

const mockRelatedCards = [
  { id: 'r1', entity: 'game' as const, title: 'Catan Expansion', color: '25 95% 45%' },
  { id: 'r2', entity: 'game' as const, title: 'Catan Seafarers', color: '25 95% 45%' },
];

describe('CardLinkBar', () => {
  it('renders tabs for game entity type', () => {
    render(
      <CardLinkBar
        entityType="game"
        entityId="1"
        relatedCards={mockRelatedCards}
        onCardTap={vi.fn()}
      />
    );
    expect(screen.getByText(/related|correlati|expansions/i)).toBeInTheDocument();
  });

  it('does not render when no tabs have content', () => {
    const { container } = render(
      <CardLinkBar
        entityType="game"
        entityId="1"
        relatedCards={[]}
        onCardTap={vi.fn()}
      />
    );
    expect(container.firstChild).toBeNull();
  });

  it('renders compact cards in horizontal scroll', () => {
    render(
      <CardLinkBar
        entityType="game"
        entityId="1"
        relatedCards={mockRelatedCards}
        onCardTap={vi.fn()}
      />
    );
    expect(screen.getByText('Catan Expansion')).toBeInTheDocument();
    expect(screen.getByText('Catan Seafarers')).toBeInTheDocument();
  });

  it('calls onCardTap when compact card is tapped', () => {
    const onCardTap = vi.fn();
    render(
      <CardLinkBar
        entityType="game"
        entityId="1"
        relatedCards={mockRelatedCards}
        onCardTap={onCardTap}
      />
    );
    fireEvent.click(screen.getByText('Catan Expansion'));
    expect(onCardTap).toHaveBeenCalledWith(mockRelatedCards[0]);
  });

  it('renders correct tabs for agent entity', () => {
    render(
      <CardLinkBar
        entityType="agent"
        entityId="a1"
        relatedCards={[{ id: 'c1', entity: 'chatSession' as const, title: 'Chat 1', color: '220 80% 55%' }]}
        onCardTap={vi.fn()}
      />
    );
    expect(screen.getByText(/chat|recent/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/__tests__/card-link-bar.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement CardLinkBar.tsx**

Create `apps/web/src/components/ui/data-display/meeple-card-browser/CardLinkBar.tsx`:

- Tab header: `flex overflow-x-auto flex-nowrap gap-2` with tab buttons
- Tab content: compact MeepleCards in `flex overflow-x-auto gap-2 flex-nowrap`
- Tab configuration mapped per entity type (9-entity table from spec):

```typescript
const TAB_CONFIG: Record<MeepleEntityType, TabDefinition[]> = {
  game: [
    { key: 'related', label: 'Related' },
    { key: 'similar', label: 'Similar' },
    { key: 'collections', label: 'Collections' },
  ],
  agent: [
    { key: 'chats', label: 'Recent chats' },
    { key: 'games', label: 'Games' },
  ],
  // ... all 9 types
};
```

- Props: `{ entityType: MeepleEntityType; entityId: string; relatedCards: CardRef[]; similarCards?: CardRef[]; collectionCards?: CardRef[]; onCardTap: (card: CardRef) => void }`
- **Data fetching**: CardLinkBar is a **presentational** component. Data is fetched by the parent `MeepleCardBrowser`, which calls:
  - "Related" tab: existing `useEntityLinks(entityId)` hook → maps to CardRef[]
  - "Similar" tab: existing vector search endpoint `GET /api/v1/knowledge-base/search?similar_to={gameId}` (already used by RAG) → maps to CardRef[]
  - "Collections" tab: existing `useUserLibrary()` hook → filter by game presence
  - Data passed in as props; CardLinkBar only renders what it receives
- Returns `null` when all card arrays are empty (tab has no content)
- Active tab state managed locally with `useState`

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/__tests__/card-link-bar.test.tsx`
Expected: ALL PASS

- [ ] **Step 5: Integrate CardLinkBar into MeepleCardBrowser below the expanded card**

In `MeepleCardBrowser.tsx`, render `<CardLinkBar>` below `<MeepleCardExpanded>` inside the overlay. Pass data fetched via existing hooks. The `onCardTap` handler calls `navigateTo(card)` from the context.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-browser/CardLinkBar.tsx
git add apps/web/src/components/ui/data-display/__tests__/card-link-bar.test.tsx
git commit -m "feat(card-browser): add CardLinkBar with entity-specific tabbed related cards"
```

---

## Chunk 4: MobileBottomBar + DomainHub

### Task 11: Create MobileBottomBar

**Files:**
- Create: `apps/web/src/components/layout/MobileBottomBar/MobileBottomBar.tsx`
- Create: `apps/web/src/components/layout/MobileBottomBar/index.ts`
- Create: `apps/web/src/components/layout/__tests__/mobile-bottom-bar.test.tsx`
- Modify: `apps/web/src/components/layout/FloatingActionBar/FloatingActionBar.tsx` (add `md:hidden` logic)

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/layout/__tests__/mobile-bottom-bar.test.tsx`:

```tsx
import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { MobileBottomBar } from '../MobileBottomBar';

// Mock Next.js hooks
vi.mock('next/navigation', () => ({
  usePathname: () => '/agents',
  useRouter: () => ({ push: vi.fn() }),
}));

// Mock CardBrowserContext
vi.mock('@/components/ui/data-display/meeple-card-browser', () => ({
  useCardBrowser: () => ({ isOpen: false }),
}));

describe('MobileBottomBar', () => {
  it('renders 5 tab items', () => {
    render(<MobileBottomBar />);
    expect(screen.getByText('Home')).toBeInTheDocument();
    expect(screen.getByText('Games')).toBeInTheDocument();
    expect(screen.getByText('AI')).toBeInTheDocument();
    expect(screen.getByText('Library')).toBeInTheDocument();
    expect(screen.getByText('Settings')).toBeInTheDocument();
  });

  it('has accessible touch targets (min-h-[44px])', () => {
    render(<MobileBottomBar />);
    const buttons = screen.getAllByRole('link');
    buttons.forEach(button => {
      // Each tab link should have min-h class for WCAG touch target
      expect(button.className).toMatch(/min-h/);
    });
  });

  it('does not render when cardBrowser is open', () => {
    vi.mocked(require('@/components/ui/data-display/meeple-card-browser').useCardBrowser)
      .mockReturnValue({ isOpen: true });
    const { container } = render(<MobileBottomBar />);
    expect(container.firstChild).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && npx vitest run src/components/layout/__tests__/mobile-bottom-bar.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement MobileBottomBar.tsx**

Create `apps/web/src/components/layout/MobileBottomBar/MobileBottomBar.tsx`:

- Fixed bottom: `fixed bottom-0 left-0 right-0 z-40 md:hidden`
- Glassmorphism: `bg-card/90 backdrop-blur-md backdrop-saturate-150`
- Safe area: `pb-[env(safe-area-inset-bottom)]`
- Height: `h-14` (56px)
- 5 tabs layout: `flex items-center justify-around`
- Each tab: `<Link>` with icon + label, `min-h-[44px] flex flex-col items-center justify-center gap-0.5`
- Icons from lucide-react: LayoutGrid (Home), Dice5 (Games), MessageSquare (AI), BookOpen (Library), User (Settings)
- Active tab detection: `usePathname()` from `next/navigation`, match prefix (e.g., `/agents` → AI active)
- Active styling: icon gets entity color, label gets `font-bold text-foreground`
- Inactive styling: `text-muted-foreground`
- Returns `null` when `useCardBrowser().isOpen` is true
- Tab routes: Home→`/hub`, Games→`/discover`, AI→`/agents`, Library→`/library`, Settings→`/settings`

Create `apps/web/src/components/layout/MobileBottomBar/index.ts`:
```typescript
export { MobileBottomBar } from './MobileBottomBar';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && npx vitest run src/components/layout/__tests__/mobile-bottom-bar.test.tsx`
Expected: ALL PASS

- [ ] **Step 5: Modify FloatingActionBar to hide on mobile**

Edit `apps/web/src/components/layout/FloatingActionBar/FloatingActionBar.tsx`:
Add `hidden md:flex` to the outermost container className so it hides on mobile, but remains visible on desktop. Find the container with `fixed` positioning and add `hidden md:flex` to its className.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/layout/MobileBottomBar/
git add apps/web/src/components/layout/__tests__/mobile-bottom-bar.test.tsx
git add apps/web/src/components/layout/FloatingActionBar/FloatingActionBar.tsx
git commit -m "feat(navigation): add MobileBottomBar with 5-domain tabs, hide FloatingActionBar on mobile"
```

### Task 12: Create DomainHub page

**Files:**
- Create: `apps/web/src/components/layout/DomainHub/DomainHub.tsx`
- Create: `apps/web/src/components/layout/DomainHub/index.ts`
- Create: `apps/web/src/app/(authenticated)/hub/page.tsx`
- Create: `apps/web/src/components/layout/__tests__/domain-hub.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/layout/__tests__/domain-hub.test.tsx`:

```tsx
import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { DomainHub } from '../DomainHub';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

describe('DomainHub', () => {
  it('renders 8 domain tiles', () => {
    render(<DomainHub userName="Marco" />);
    const tiles = screen.getAllByRole('link');
    expect(tiles).toHaveLength(8);
  });

  it('renders all domain labels', () => {
    render(<DomainHub userName="Marco" />);
    expect(screen.getByText('Games')).toBeInTheDocument();
    expect(screen.getByText('Agents')).toBeInTheDocument();
    expect(screen.getByText('Chat')).toBeInTheDocument();
    expect(screen.getByText('Library')).toBeInTheDocument();
    expect(screen.getByText('Leaderboards')).toBeInTheDocument();
    expect(screen.getByText('Sessions')).toBeInTheDocument();
    expect(screen.getByText('Notifications')).toBeInTheDocument();
    expect(screen.getByText('Settings')).toBeInTheDocument();
  });

  it('renders 2-column grid', () => {
    const { container } = render(<DomainHub userName="Marco" />);
    const grid = container.querySelector('[class*="grid-cols-2"]');
    expect(grid).not.toBeNull();
  });

  it('shows greeting with user name', () => {
    render(<DomainHub userName="Marco" />);
    expect(screen.getByText(/Marco/)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && npx vitest run src/components/layout/__tests__/domain-hub.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement DomainHub.tsx**

Create `apps/web/src/components/layout/DomainHub/DomainHub.tsx`:

```tsx
'use client';

import Link from 'next/link';
import { Dice5, Bot, MessageSquare, BookOpen, Trophy, CalendarDays, Bell, Settings } from 'lucide-react';

const DOMAINS = [
  { label: 'Games', icon: Dice5, href: '/discover', color: '25 95% 45%' },
  { label: 'Agents', icon: Bot, href: '/agents', color: '38 92% 50%' },
  { label: 'Chat', icon: MessageSquare, href: '/ask', color: '220 80% 55%' },
  { label: 'Library', icon: BookOpen, href: '/library', color: '168 76% 42%' },
  { label: 'Leaderboards', icon: Trophy, href: '/badges', color: '262 83% 58%' },
  { label: 'Sessions', icon: CalendarDays, href: '/sessions', color: '240 60% 55%' },
  { label: 'Notifications', icon: Bell, href: '/notifications', color: '350 89% 60%' },
  { label: 'Settings', icon: Settings, href: '/settings', color: '220 70% 50%' },
] as const;

interface DomainHubProps {
  userName?: string;
}

export function DomainHub({ userName }: DomainHubProps) {
  return (
    <div className="flex flex-col gap-6 p-6">
      {userName && (
        <h1 className="text-xl font-heading font-bold">
          Welcome back, {userName}!
        </h1>
      )}
      <div className="grid grid-cols-2 gap-4">
        {DOMAINS.map(({ label, icon: Icon, href, color }) => (
          <Link
            key={label}
            href={href}
            className="aspect-square rounded-2xl flex flex-col items-center justify-center gap-2 transition-transform active:scale-95"
            style={{ backgroundColor: `hsl(${color} / 0.1)` }}
          >
            <Icon size={32} style={{ color: `hsl(${color})` }} />
            <span className="text-sm font-medium">{label}</span>
          </Link>
        ))}
      </div>
    </div>
  );
}
```

Create `apps/web/src/components/layout/DomainHub/index.ts`:
```typescript
export { DomainHub } from './DomainHub';
```

- [ ] **Step 4: Create hub route page**

Create `apps/web/src/app/(authenticated)/hub/page.tsx`:

```tsx
import { DomainHub } from '@/components/layout/DomainHub';

export default function HubPage() {
  // userName fetched from auth context or session in the actual implementation
  return <DomainHub userName="Player" />;
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && npx vitest run src/components/layout/__tests__/domain-hub.test.tsx`
Expected: ALL PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/layout/DomainHub/
git add apps/web/src/app/(authenticated)/hub/
git add apps/web/src/components/layout/__tests__/domain-hub.test.tsx
git commit -m "feat(navigation): add DomainHub page with 8-domain tile grid and user greeting"
```

---

## Chunk 5: Integration + Anti-Scroll Optimizations

### Task 13: Wire CardBrowserProvider + MobileBottomBar into layout

**Files:**
- Modify: `apps/web/src/app/(authenticated)/layout.tsx`

- [ ] **Step 1: Read the current authenticated layout**

Read `apps/web/src/app/(authenticated)/layout.tsx` to understand the current component tree.

- [ ] **Step 2: Add CardBrowserProvider and MobileBottomBar**

Wrap children with `CardBrowserProvider`.
Add `MobileBottomBar` and `MeepleCardBrowser` as siblings of the main content.

```tsx
import { CardBrowserProvider } from '@/components/ui/data-display/meeple-card-browser';
import { MeepleCardBrowser } from '@/components/ui/data-display/meeple-card-browser';
import { MobileBottomBar } from '@/components/layout/MobileBottomBar';

// Inside layout:
<CardBrowserProvider>
  {/* existing layout content */}
  {children}
  <MeepleCardBrowser />
  <MobileBottomBar />
</CardBrowserProvider>
```

- [ ] **Step 3: Add bottom padding for MobileBottomBar**

Add `pb-16 md:pb-0` to the main content container so content doesn't hide behind the bottom bar on mobile.

- [ ] **Step 4: Verify the app builds**

Run: `cd apps/web && npx next build 2>&1 | tail -20`
Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/(authenticated)/layout.tsx
git commit -m "feat(layout): integrate CardBrowserProvider, MeepleCardBrowser overlay, and MobileBottomBar into authenticated layout"
```

### Task 14: Anti-scroll — mobile grid 2 columns + card browser tap integration

**Files:**
- Modify: key pages with MeepleCard grids (agents/page.tsx, discover/page.tsx, library/page.tsx, sessions/page.tsx, etc.)

- [ ] **Step 1: Identify all pages with MeepleCard grids**

Run: `cd apps/web && grep -rl "grid-cols-1" src/app/ | head -20`
Cross-reference with pages importing `MeepleCard`.

- [ ] **Step 2: Update grid classes for mobile 2-columns**

For each identified page, change:
- `grid-cols-1 md:grid-cols-2` → `grid-cols-2 md:grid-cols-3`
- `gap-6` → `gap-3 md:gap-6`

This gives 2 columns on mobile with tighter gaps.

- [ ] **Step 3: Add "open in browser" onClick to MeepleCards in grids**

For each page with a card grid, build a `CardRef[]` from the page data and use `useCardBrowser().open()`:

```tsx
// Example for agents page:
'use client';
import { useCardBrowser, type CardRef } from '@/components/ui/data-display/meeple-card-browser';
import { entityColors } from '@/components/ui/data-display/meeple-card';

// Inside the component:
const { open } = useCardBrowser();

const cardRefs: CardRef[] = filteredAgents.map(agent => ({
  id: agent.id,
  entity: 'agent',
  title: agent.name,
  subtitle: `${agent.type} agent`,
  imageUrl: agent.imageUrl,
  color: entityColors.agent.hsl,
}));

// In the grid:
{filteredAgents.map((agent, index) => (
  <MeepleCard
    entity="agent"
    variant="grid"
    title={agent.name}
    onClick={() => open(cardRefs, index)}
    // ... other props
  />
))}
```

Repeat this pattern for each page (discover, library, sessions, etc.), adapting the `CardRef` construction to the page's data model.

- [ ] **Step 4: Verify mobile layout renders 2 columns**

Run: `cd apps/web && pnpm dev`
Open in browser at mobile viewport (375px) — verify 2-column grid.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/
git commit -m "feat(anti-scroll): mobile grid 2 columns, tighter gaps, card browser integration on tap"
```

### Task 15: Anti-scroll — view toggle (grid/list) + sticky quick filters

**Files:**
- Create: `apps/web/src/components/ui/data-display/ListPageHeader.tsx`

- [ ] **Step 1: Create ListPageHeader component**

A reusable header for list pages with view toggle and sticky filters:

```tsx
interface ListPageHeaderProps {
  title: string;
  count: number;
  viewMode: 'grid' | 'list';
  onViewModeChange: (mode: 'grid' | 'list') => void;
  filters?: { key: string; label: string }[];
  activeFilter?: string;
  onFilterChange?: (key: string) => void;
}
```

Layout:
```
┌──────────────────────────┐
│  Title (count)  [▦] [≡]  │  top row
├──────────────────────────┤
│  [All] [Owned] [Wishlist]│  sticky filter chips (optional)
└──────────────────────────┘
```

- Toggle: two icon buttons (LayoutGrid, List), active one highlighted
- Filters: `flex overflow-x-auto gap-2 flex-nowrap`, each chip is a button with `rounded-full px-3 py-1.5 text-sm`
- Active filter: `bg-primary text-primary-foreground`
- Sticky: `sticky top-14 z-30 bg-background/95 backdrop-blur-sm` (below TopNavbar at 56px)
- View preference saved in `localStorage` keyed by domain: `meeple-view-${domain}`

- [ ] **Step 2: Create useViewPreference hook**

```typescript
function useViewPreference(domain: string): [mode: 'grid' | 'list', setMode: (m: 'grid' | 'list') => void] {
  // localStorage: `meeple-view-${domain}`
  // Default: 'grid'
  // SSR-safe: typeof window check
}
```

- [ ] **Step 3: Integrate into one example page (agents)**

Add `ListPageHeader` to agents page. Conditionally render grid or list view based on `viewMode`.

- [ ] **Step 4: Verify toggle works in dev**

Run: `cd apps/web && pnpm dev`
Expected: Toggle switches between grid and list view, preference persists on refresh.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/ListPageHeader.tsx
git add apps/web/src/app/
git commit -m "feat(anti-scroll): add ListPageHeader with view toggle and sticky quick filters"
```

### Task 16: Anti-scroll — infinite scroll with IntersectionObserver

**Files:**
- Create: `apps/web/src/hooks/useInfiniteScroll.ts`

- [ ] **Step 1: Create useInfiniteScroll hook**

```typescript
interface UseInfiniteScrollOptions {
  hasMore: boolean;
  isLoading: boolean;
  onLoadMore: () => void;
  rootMargin?: string;
}

function useInfiniteScroll({ hasMore, isLoading, onLoadMore, rootMargin = '200px' }: UseInfiniteScrollOptions) {
  const sentinelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!hasMore || isLoading) return;
    const observer = new IntersectionObserver(
      ([entry]) => { if (entry.isIntersecting) onLoadMore(); },
      { rootMargin }
    );
    if (sentinelRef.current) observer.observe(sentinelRef.current);
    return () => observer.disconnect();
  }, [hasMore, isLoading, onLoadMore, rootMargin]);

  return sentinelRef;
}
```

Returns a ref to attach to a sentinel `<div>` at the bottom of the list.

- [ ] **Step 2: Create skeleton loader for card grids**

A row of 4-6 `MeepleCardSkeleton` components rendered below the grid when loading:

```tsx
function CardGridSkeletons({ count = 4 }: { count?: number }) {
  return (
    <div className="grid grid-cols-2 md:grid-cols-3 gap-3 md:gap-6">
      {Array.from({ length: count }).map((_, i) => (
        <MeepleCardSkeleton key={i} variant="grid" />
      ))}
    </div>
  );
}
```

- [ ] **Step 3: Integrate into one example page (agents or discover)**

Add `useInfiniteScroll` to agents page:
- Initial load: 20 items
- `onLoadMore`: fetch next page
- Sentinel div at bottom of grid
- Show `CardGridSkeletons` while loading

- [ ] **Step 4: Verify infinite scroll in dev**

Run: `cd apps/web && pnpm dev`
Expected: Scrolling near bottom triggers loading more items.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/useInfiniteScroll.ts
git add apps/web/src/app/
git commit -m "feat(anti-scroll): add useInfiniteScroll hook with IntersectionObserver and skeleton loaders"
```

### Task 17: Polish — origin-aware open animation + swipe-down close

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-browser/MeepleCardBrowser.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card-browser/useMeepleCardBrowser.ts`

- [ ] **Step 1: Add tap origin tracking**

In `CardBrowserContext`, extend `open()` to accept tap coordinates:

```typescript
open: (cards: CardRef[], index: number, origin?: { x: number; y: number }) => void;
```

Store `origin` in state. When MeepleCard is tapped, pass the click event's `clientX`/`clientY`.

- [ ] **Step 2: Apply origin-aware animation in MeepleCardBrowser**

In the framer-motion `motion.div` for the overlay:

```tsx
<motion.div
  initial={{
    scale: 0.8,
    opacity: 0,
    transformOrigin: origin ? `${origin.x}px ${origin.y}px` : 'center',
  }}
  animate={{ scale: 1, opacity: 1 }}
  exit={{ y: '100vh', opacity: 0 }}
  transition={{ duration: 0.3, ease: [0.4, 0, 0.2, 1] }}
>
```

- [ ] **Step 3: Add swipe-down close gesture**

In `useMeepleCardBrowser.ts`, add touch event handling:

```typescript
function useSwipeDownClose(onClose: () => void) {
  const startY = useRef(0);
  const currentY = useRef(0);

  const onTouchStart = (e: TouchEvent) => { startY.current = e.touches[0].clientY; };
  const onTouchMove = (e: TouchEvent) => { currentY.current = e.touches[0].clientY; };
  const onTouchEnd = () => {
    const delta = currentY.current - startY.current;
    if (delta > 100) onClose(); // swipe down threshold: 100px
  };

  return { onTouchStart, onTouchMove, onTouchEnd };
}
```

Attach to the overlay container div.

- [ ] **Step 4: Verify animations in dev**

Run: `cd apps/web && pnpm dev`
Expected: Card expands from tap point, swipe down closes.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-browser/
git commit -m "feat(card-browser): add origin-aware open animation and swipe-down close gesture"
```

### Task 18: Final integration test — full flow E2E

**Files:**
- Create: `apps/web/e2e/mobile-card-browser.spec.ts` (Playwright E2E)

- [ ] **Step 1: Write E2E test for the full mobile flow**

```typescript
import { test, expect } from '@playwright/test';

test.describe('Mobile Card Browser', () => {
  test.use({ viewport: { width: 375, height: 812 } }); // iPhone viewport

  test('full flow: grid → tap → overlay → carousel → deck → close', async ({ page }) => {
    await page.goto('/agents');

    // Verify 2-column grid
    const grid = page.locator('[data-testid="card-grid"]');
    // ... assertions

    // Tap first card → overlay opens
    await page.locator('[data-testid="meeple-card"]').first().click();
    await expect(page.locator('[role="dialog"]')).toBeVisible();

    // Carousel indicator visible
    await expect(page.getByText(/1\//)).toBeVisible();

    // Press ESC → overlay closes
    await page.keyboard.press('Escape');
    await expect(page.locator('[role="dialog"]')).not.toBeVisible();
  });

  test('bottom bar navigation between domains', async ({ page }) => {
    await page.goto('/hub');

    // Bottom bar visible
    const bottomBar = page.locator('[data-testid="mobile-bottom-bar"]');
    await expect(bottomBar).toBeVisible();

    // Tap Games → navigates
    await bottomBar.getByText('Games').click();
    await expect(page).toHaveURL(/games|discover/);
  });
});
```

- [ ] **Step 2: Run E2E tests**

Run: `cd apps/web && npx playwright test e2e/mobile-card-browser.spec.ts`
Expected: PASS (requires dev server running)

- [ ] **Step 3: Final full test suite**

Run: `cd apps/web && npx vitest run src/components/ui/data-display/__tests__/meeple-card`
Run: `cd apps/web && npx vitest run src/components/layout/__tests__/`
Expected: ALL PASS

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/mobile-card-browser.spec.ts
git commit -m "test(e2e): add mobile card browser full flow test — grid, overlay, carousel, bottom bar"
```

### Task 19: Final cleanup and typecheck

- [ ] **Step 1: Full typecheck**

Run: `cd apps/web && npx tsc --noEmit --pretty`
Expected: Zero errors

- [ ] **Step 2: Lint check**

Run: `cd apps/web && pnpm lint`
Expected: No errors

- [ ] **Step 3: Full test suite**

Run: `cd apps/web && pnpm test`
Expected: ALL PASS

- [ ] **Step 4: Final commit**

```bash
git commit -m "chore: final cleanup — typecheck, lint, all tests pass"
```
