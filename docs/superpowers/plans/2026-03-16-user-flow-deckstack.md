# User Flow & Deckstack Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the existing CardHand system with 4 placeholder action cards and new search/creation flows (agent search with KB selection, game search, session management, generic toolkit).

**Architecture:** Extend `useCardHand` Zustand store + `CardStack`/`HandDrawer` UI components with placeholder card support. New sheet components for each action flow. Backend requires one new user-facing endpoint for shared game documents.

**Tech Stack:** Next.js 16, React 19, Zustand, Tailwind 4, shadcn/ui, Vitest, React Query

**Spec:** `docs/superpowers/specs/2026-03-16-user-flow-deckstack-design.md`

---

## Chunk 1: Placeholder Card Infrastructure

Foundation layer: extend the store, config, and card components to support placeholder action cards.

### Task 1: Extend HandCard interface

**Files:**
- Modify: `apps/web/src/stores/use-card-hand.ts`
- Test: `apps/web/src/__tests__/stores/use-card-hand-placeholder.test.ts`

- [x] **Step 1: Write failing test for placeholder card in store**

```typescript
// apps/web/src/__tests__/stores/use-card-hand-placeholder.test.ts
import { describe, it, expect, beforeEach } from 'vitest';
import { useCardHand } from '@/stores/use-card-hand';

describe('useCardHand - placeholder cards', () => {
  beforeEach(() => {
    useCardHand.getState().clear();
  });

  it('should accept a card with isPlaceholder=true', () => {
    const { drawCard } = useCardHand.getState();
    drawCard({
      id: 'action-test',
      entity: 'agent',
      title: 'Test Action',
      href: '#action-test',
      isPlaceholder: true,
      placeholderAction: 'search-agent',
    });
    const cards = useCardHand.getState().cards;
    expect(cards).toHaveLength(1);
    expect(cards[0].isPlaceholder).toBe(true);
    expect(cards[0].placeholderAction).toBe('search-agent');
  });

  it('should not discard placeholder cards during FIFO eviction', () => {
    const { drawCard } = useCardHand.getState();
    // Draw placeholder first
    drawCard({
      id: 'action-placeholder',
      entity: 'agent',
      title: 'Placeholder',
      href: '#action',
      isPlaceholder: true,
      placeholderAction: 'search-agent',
    });
    // Fill to max with regular cards
    for (let i = 0; i < 10; i++) {
      drawCard({
        id: `card-${i}`,
        entity: 'game',
        title: `Game ${i}`,
        href: `/games/${i}`,
      });
    }
    const cards = useCardHand.getState().cards;
    const placeholder = cards.find(c => c.id === 'action-placeholder');
    expect(placeholder).toBeDefined();
  });
});
```

- [x] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/__tests__/stores/use-card-hand-placeholder.test.ts`
Expected: FAIL — `isPlaceholder` and `placeholderAction` not in `HandCard` type

- [x] **Step 3: Extend HandCard interface**

Add optional fields to `HandCard` in `apps/web/src/stores/use-card-hand.ts`:

```typescript
export interface HandCard {
  id: string;
  entity: MeepleEntityType;
  title: string;
  href: string;
  subtitle?: string;
  imageUrl?: string;
  isPlaceholder?: boolean;
  placeholderAction?: string;
}
```

Also update FIFO eviction logic in `drawCard` to skip cards where `isPlaceholder === true` (treat like pinned/protected during eviction).

- [x] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/__tests__/stores/use-card-hand-placeholder.test.ts`
Expected: PASS

- [x] **Step 5: Run existing store tests to check no regressions**

Run: `cd apps/web && pnpm vitest run src/__tests__/stores/`
Expected: All existing tests PASS

- [x] **Step 6: Commit**

```bash
git add apps/web/src/stores/use-card-hand.ts apps/web/src/__tests__/stores/use-card-hand-placeholder.test.ts
git commit -m "feat(card-hand): extend HandCard with isPlaceholder and placeholderAction fields"
```

---

### Task 2: Add PLACEHOLDER_ACTION_CARDS config

**Files:**
- Modify: `apps/web/src/config/entity-actions.ts`
- Test: `apps/web/src/__tests__/config/placeholder-action-cards.test.ts`

- [x] **Step 1: Write failing test for placeholder cards config**

```typescript
// apps/web/src/__tests__/config/placeholder-action-cards.test.ts
import { describe, it, expect } from 'vitest';
import { PLACEHOLDER_ACTION_CARDS, ALL_DEFAULT_CARDS, DEFAULT_PINNED_CARDS } from '@/config/entity-actions';

describe('PLACEHOLDER_ACTION_CARDS', () => {
  it('should define exactly 4 placeholder cards', () => {
    expect(PLACEHOLDER_ACTION_CARDS).toHaveLength(4);
  });

  it('each placeholder should have isPlaceholder=true and a placeholderAction', () => {
    for (const card of PLACEHOLDER_ACTION_CARDS) {
      expect(card.isPlaceholder).toBe(true);
      expect(card.placeholderAction).toBeDefined();
      expect(card.href).toMatch(/^#action-/);
    }
  });

  it('should have correct action types', () => {
    const actions = PLACEHOLDER_ACTION_CARDS.map(c => c.placeholderAction);
    expect(actions).toContain('search-agent');
    expect(actions).toContain('search-game');
    expect(actions).toContain('start-session');
    expect(actions).toContain('toolkit');
  });

  it('ALL_DEFAULT_CARDS combines DEFAULT_PINNED_CARDS + PLACEHOLDER_ACTION_CARDS', () => {
    expect(ALL_DEFAULT_CARDS.length).toBe(
      DEFAULT_PINNED_CARDS.length + PLACEHOLDER_ACTION_CARDS.length
    );
  });
});
```

- [x] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/__tests__/config/placeholder-action-cards.test.ts`
Expected: FAIL — exports not found

- [x] **Step 3: Add PLACEHOLDER_ACTION_CARDS and ALL_DEFAULT_CARDS to entity-actions.ts**

At the bottom of `apps/web/src/config/entity-actions.ts`, add:

```typescript
import type { HandCard } from '@/stores/use-card-hand';

export const PLACEHOLDER_ACTION_CARDS: HandCard[] = [
  {
    id: 'action-search-agent',
    entity: 'agent',
    title: 'Cerca Agente',
    href: '#action-search-agent',
    isPlaceholder: true,
    placeholderAction: 'search-agent',
  },
  {
    id: 'action-search-game',
    entity: 'game',
    title: 'Cerca Gioco',
    href: '#action-search-game',
    isPlaceholder: true,
    placeholderAction: 'search-game',
  },
  {
    id: 'action-start-session',
    entity: 'session',
    title: 'Avvia Sessione',
    href: '#action-start-session',
    isPlaceholder: true,
    placeholderAction: 'start-session',
  },
  {
    id: 'action-toolkit',
    entity: 'toolkit',
    title: 'Toolkit',
    href: '#action-toolkit',
    isPlaceholder: true,
    placeholderAction: 'toolkit',
  },
];

export const ALL_DEFAULT_CARDS: HandCard[] = [
  ...DEFAULT_PINNED_CARDS,
  ...PLACEHOLDER_ACTION_CARDS,
];
```

- [x] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/__tests__/config/placeholder-action-cards.test.ts`
Expected: PASS

- [x] **Step 5: Commit**

```bash
git add apps/web/src/config/entity-actions.ts apps/web/src/__tests__/config/placeholder-action-cards.test.ts
git commit -m "feat(config): add PLACEHOLDER_ACTION_CARDS and ALL_DEFAULT_CARDS constants"
```

---

### Task 3: Create usePlaceholderActions hook

**Files:**
- Create: `apps/web/src/hooks/usePlaceholderActions.ts`
- Test: `apps/web/src/__tests__/hooks/usePlaceholderActions.test.ts`

- [x] **Step 1: Write failing test**

```typescript
// apps/web/src/__tests__/hooks/usePlaceholderActions.test.ts
import { describe, it, expect } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { usePlaceholderActions } from '@/hooks/usePlaceholderActions';
import type { HandCard } from '@/stores/use-card-hand';

const makePlaceholder = (action: string): HandCard => ({
  id: `action-${action}`,
  entity: 'agent',
  title: 'Test',
  href: `#action-${action}`,
  isPlaceholder: true,
  placeholderAction: action,
});

describe('usePlaceholderActions', () => {
  it('should return null activeSheet initially', () => {
    const { result } = renderHook(() => usePlaceholderActions());
    expect(result.current.activeSheet).toBeNull();
  });

  it('handleCardClick returns true and sets activeSheet for placeholder', () => {
    const { result } = renderHook(() => usePlaceholderActions());
    let handled: boolean;
    act(() => {
      handled = result.current.handleCardClick(makePlaceholder('search-agent'));
    });
    expect(handled!).toBe(true);
    expect(result.current.activeSheet).toBe('search-agent');
  });

  it('handleCardClick returns false for non-placeholder card', () => {
    const { result } = renderHook(() => usePlaceholderActions());
    let handled: boolean;
    act(() => {
      handled = result.current.handleCardClick({
        id: 'regular',
        entity: 'game',
        title: 'Game',
        href: '/games/1',
      });
    });
    expect(handled!).toBe(false);
    expect(result.current.activeSheet).toBeNull();
  });

  it('closeSheet resets activeSheet to null', () => {
    const { result } = renderHook(() => usePlaceholderActions());
    act(() => {
      result.current.handleCardClick(makePlaceholder('toolkit'));
    });
    expect(result.current.activeSheet).toBe('toolkit');
    act(() => {
      result.current.closeSheet();
    });
    expect(result.current.activeSheet).toBeNull();
  });
});
```

- [x] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/__tests__/hooks/usePlaceholderActions.test.ts`
Expected: FAIL — module not found

- [x] **Step 3: Implement usePlaceholderActions**

```typescript
// apps/web/src/hooks/usePlaceholderActions.ts
'use client';

import { useState, useCallback } from 'react';
import type { HandCard } from '@/stores/use-card-hand';

export type PlaceholderActionType = 'search-agent' | 'search-game' | 'start-session' | 'toolkit';

export function usePlaceholderActions() {
  const [activeSheet, setActiveSheet] = useState<PlaceholderActionType | null>(null);

  const handleCardClick = useCallback((card: HandCard): boolean => {
    if (card.isPlaceholder && card.placeholderAction) {
      setActiveSheet(card.placeholderAction as PlaceholderActionType);
      return true;
    }
    return false;
  }, []);

  const closeSheet = useCallback(() => {
    setActiveSheet(null);
  }, []);

  return { handleCardClick, activeSheet, closeSheet };
}
```

- [x] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/__tests__/hooks/usePlaceholderActions.test.ts`
Expected: PASS

- [x] **Step 5: Commit**

```bash
git add apps/web/src/hooks/usePlaceholderActions.ts apps/web/src/__tests__/hooks/usePlaceholderActions.test.ts
git commit -m "feat(hooks): add usePlaceholderActions hook for action card click handling"
```

---

### Task 4: Update CardStackItem for placeholder rendering

**Files:**
- Modify: `apps/web/src/components/layout/UnifiedShell/CardStackItem.tsx`

- [x] **Step 1: Read the current CardStackItem implementation**

Read `apps/web/src/components/layout/UnifiedShell/CardStackItem.tsx` to understand the existing render structure, especially how the Link/button root element works and where to add conditional logic.

- [x] **Step 2: Add placeholder-specific rendering**

Modify `CardStackItem.tsx`:

1. Accept a new prop `onPlaceholderClick?: (card: HandCard) => void`
2. For `card.isPlaceholder === true`:
   - Root element: `<button onClick={() => onPlaceholderClick?.(card)}>` instead of `<Link href={card.href}>`
   - Add `aria-label={`Azione: ${card.title}`}`
   - Style: `border-dashed border-2` instead of solid border
   - Hide the discard (X) button
   - In mini mode: add a subtle pulse animation on the icon
   - In card mode: prefix title with "+" (e.g., "+ Cerca Agente")
3. For regular cards: no changes (keep `<Link>`)

- [x] **Step 3: Verify visually**

Run: `cd apps/web && pnpm dev`
Navigate to dashboard. CardStack should render placeholder cards with dashed borders. Click should not navigate.

- [x] **Step 4: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/CardStackItem.tsx
git commit -m "feat(card-stack): render placeholder cards with dashed style and button root"
```

---

### Task 5: Update HandDrawerCard for placeholder rendering

**Files:**
- Modify: `apps/web/src/components/layout/UnifiedShell/HandDrawerCard.tsx`

- [x] **Step 1: Read the current HandDrawerCard implementation**

Read `apps/web/src/components/layout/UnifiedShell/HandDrawerCard.tsx` to understand the existing `<Link>` root.

- [x] **Step 2: Add placeholder-specific rendering**

Modify `HandDrawerCard.tsx`:

1. Accept a new prop `onPlaceholderClick?: (card: HandCard) => void`
2. For `card.isPlaceholder === true`:
   - Root element: `<button onClick={() => onPlaceholderClick?.(card)}>` instead of `<Link href={card.href}>`
   - Add `aria-label={`Azione: ${card.title}`}`
   - Style: dashed border, full opacity (not 45% like unfocused)
   - Add "+" overlay on the icon
3. For regular cards: no changes

- [x] **Step 3: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/HandDrawerCard.tsx
git commit -m "feat(hand-drawer): render placeholder cards with button root and dashed style"
```

---

### Task 6: Wire placeholder actions in UnifiedShellClient + migration

**Files:**
- Modify: `apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx`

- [x] **Step 1: Read UnifiedShellClient to understand seeding logic**

Read `apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx`. Find the `cards.length === 0` seeding block and the CardStack/HandDrawer render calls.

- [x] **Step 2: Add placeholder migration for returning users**

After the existing `useEffect` seeding block, add:

```typescript
import { PLACEHOLDER_ACTION_CARDS } from '@/config/entity-actions';

// Migration: inject placeholder cards for returning users
useEffect(() => {
  const hasPlaceholders = cards.some(c => c.isPlaceholder);
  if (!hasPlaceholders) {
    PLACEHOLDER_ACTION_CARDS.forEach(card => {
      drawCard(card);
      pinCard(card.id);
    });
  }
}, []); // eslint-disable-line react-hooks/exhaustive-deps
```

- [x] **Step 3: Wire usePlaceholderActions to CardStack and HandDrawer**

In the `UnifiedShellClient` component:

```typescript
import { usePlaceholderActions } from '@/hooks/usePlaceholderActions';

// Inside component:
const { handleCardClick, activeSheet, closeSheet } = usePlaceholderActions();

// Pass to CardStack:
<CardStack onPlaceholderClick={handleCardClick} />

// Pass to HandDrawer:
<HandDrawer onPlaceholderClick={handleCardClick} />

// Render sheets based on activeSheet (placeholder for now):
{activeSheet === 'search-agent' && <div>SearchAgentSheet placeholder</div>}
{activeSheet === 'search-game' && <div>SearchGameSheet placeholder</div>}
{activeSheet === 'start-session' && <div>SessionSheet placeholder</div>}
{activeSheet === 'toolkit' && <div>ToolkitSheet placeholder</div>}
```

Note: Sheet components will be built in later tasks. Use placeholder `<div>` for now to verify wiring works.

- [x] **Step 4: Update initial seeding to use ALL_DEFAULT_CARDS**

In the existing `cards.length === 0` block, replace `DEFAULT_PINNED_CARDS` with `ALL_DEFAULT_CARDS` so new users get placeholder cards on first load.

- [x] **Step 5: Pass onPlaceholderClick through CardStack and HandDrawer**

`CardStack.tsx` needs to accept `onPlaceholderClick` prop and forward it to each `CardStackItem`.
`HandDrawer.tsx` needs to accept `onPlaceholderClick` prop and forward it to each `HandDrawerCard`.

- [x] **Step 6: Verify visually**

Run: `cd apps/web && pnpm dev`
Clear localStorage/sessionStorage (to test new user flow). Navigate to dashboard. 4 placeholder cards should appear pinned in CardStack with dashed borders. Clicking them should log the action (or show placeholder div).

- [x] **Step 7: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx apps/web/src/components/layout/UnifiedShell/CardStack.tsx apps/web/src/components/layout/UnifiedShell/HandDrawer.tsx
git commit -m "feat(unified-shell): wire placeholder actions + migration for returning users"
```

---

### Task 7: Create DeckTrackerSync client component

**Files:**
- Create: `apps/web/src/components/layout/DeckTrackerSync.tsx`
- Test: `apps/web/src/__tests__/components/DeckTrackerSync.test.tsx`

- [x] **Step 1: Write failing test**

```typescript
// apps/web/src/__tests__/components/DeckTrackerSync.test.tsx
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render } from '@testing-library/react';
import { DeckTrackerSync } from '@/components/layout/DeckTrackerSync';
import { useCardHand } from '@/stores/use-card-hand';

describe('DeckTrackerSync', () => {
  beforeEach(() => {
    useCardHand.getState().clear();
  });

  it('should call drawCard on mount', () => {
    render(
      <DeckTrackerSync
        entity="agent"
        id="agent-123"
        title="Test Agent"
        href="/agents/agent-123"
      />
    );
    const cards = useCardHand.getState().cards;
    expect(cards).toHaveLength(1);
    expect(cards[0].id).toBe('agent-123');
    expect(cards[0].entity).toBe('agent');
  });

  it('should render nothing (null)', () => {
    const { container } = render(
      <DeckTrackerSync
        entity="game"
        id="game-1"
        title="Game"
        href="/games/1"
      />
    );
    expect(container.innerHTML).toBe('');
  });
});
```

- [x] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/DeckTrackerSync.test.tsx`
Expected: FAIL — module not found

- [x] **Step 3: Implement DeckTrackerSync**

```typescript
// apps/web/src/components/layout/DeckTrackerSync.tsx
'use client';

import { useEffect } from 'react';
import { useCardHand } from '@/stores/use-card-hand';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card-styles';

interface DeckTrackerSyncProps {
  entity: MeepleEntityType;
  id: string;
  title: string;
  href: string;
  subtitle?: string;
  imageUrl?: string;
}

export function DeckTrackerSync({ entity, id, title, href, subtitle, imageUrl }: DeckTrackerSyncProps) {
  const drawCard = useCardHand((s) => s.drawCard);

  useEffect(() => {
    drawCard({ id, entity, title, href, subtitle, imageUrl });
  }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  return null;
}
```

- [x] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/DeckTrackerSync.test.tsx`
Expected: PASS

- [x] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/DeckTrackerSync.tsx apps/web/src/__tests__/components/DeckTrackerSync.test.tsx
git commit -m "feat(layout): add DeckTrackerSync client component for RSC page integration"
```

---

### Task 8: Add DeckTrackerSync to detail pages

**Files:**
- Modify: `apps/web/src/app/(authenticated)/agents/[id]/page.tsx` (RSC — use DeckTrackerSync)
- Modify: `apps/web/src/app/(authenticated)/knowledge-base/[id]/page.tsx` (Client — use drawCard directly)

- [x] **Step 1: Read agents/[id]/page.tsx to understand structure**

Read the file. Identify if it's async (RSC) and where to insert `<DeckTrackerSync>`.

- [x] **Step 2: Add DeckTrackerSync to agent detail page**

In `agents/[id]/page.tsx`, after loading agent data, add:

```tsx
import { DeckTrackerSync } from '@/components/layout/DeckTrackerSync';

// Inside the return:
<DeckTrackerSync
  entity="agent"
  id={agent.id}
  title={agent.name}
  href={`/agents/${agent.id}`}
  subtitle={agent.typology}
  imageUrl={agent.imageUrl}
/>
```

- [x] **Step 3: Read knowledge-base/[id]/page.tsx to understand structure**

Read the file. Check whether it has `'use client'` directive at the top.

- [x] **Step 4: Add card tracking to KB detail page**

**If Client Component** (`'use client'` present): import and use `useCardHand` directly:
**If Server Component** (async, no `'use client'`): use `<DeckTrackerSync>` instead (same as agent page).

Example for Client Component approach:

```tsx
import { useCardHand } from '@/stores/use-card-hand';

// Inside the component:
const drawCard = useCardHand((s) => s.drawCard);

useEffect(() => {
  if (documentData) {
    drawCard({
      id: documentData.id,
      entity: 'kb',
      title: documentData.fileName || documentData.title,
      href: `/knowledge-base/${documentData.id}`,
    });
  }
}, [documentData?.id]); // eslint-disable-line react-hooks/exhaustive-deps
```

- [x] **Step 5: Verify typecheck passes**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [x] **Step 6: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/agents/\[id\]/page.tsx apps/web/src/app/\(authenticated\)/knowledge-base/\[id\]/page.tsx
git commit -m "feat(pages): add DeckTrackerSync to agent and KB detail pages"
```

---

### Task 9: Remove AgentsSidebar from DashboardRenderer

**Files:**
- Modify: `apps/web/src/components/dashboard/DashboardRenderer.tsx`
- Delete: `apps/web/src/components/dashboard/zones/AgentsSidebar.tsx` (if separate file)
- Delete: Related test file (if exists)

- [x] **Step 1: Read DashboardRenderer to find AgentsSidebar usage**

Read `apps/web/src/components/dashboard/DashboardRenderer.tsx`. Find where `AgentsSidebar` is imported and rendered.

- [x] **Step 2: Remove AgentsSidebar from layout**

Remove the import and render of `AgentsSidebar`. Adjust the flex layout so `CardsZone` takes full width.

- [x] **Step 3: Remove AgentsSidebar component file and tests**

Delete the component file and any related test file.

- [x] **Step 4: Verify typecheck and existing tests**

Run: `cd apps/web && pnpm typecheck && pnpm vitest run src/__tests__/`
Expected: No type errors. Tests pass (some may need updating if they reference AgentsSidebar).

- [x] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor(dashboard): remove AgentsSidebar (functionality absorbed by CardStack)"
```

---

## Chunk 2: Search Agent Flow + KB Selection

The core new feature: search for a game, select knowledge bases, then create an agent.

### Task 10: Backend endpoint — user-facing shared game documents

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetGameDocumentsForUserQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetGameDocumentsForUserHandler.cs`
- Modify: `apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs` (or equivalent routing file)

- [x] **Step 1: Explore existing admin endpoint**

Read the admin documents endpoint to understand the query pattern, DTOs, and response format. Search for `GetSharedGameDocumentsQuery` or similar in the backend.

- [x] **Step 2: Create GetGameDocumentsForUserQuery**

Follow CQRS pattern. The query should:
- Accept `GameId` and `UserId`
- Validate: user has game in library OR game is `IsRagPublic` OR user has declared ownership
- Return only `isActive=true` documents
- Response: `SharedGameDocument[]` (same DTO as admin)

- [x] **Step 3: Add endpoint routing**

Add `GET /api/v1/shared-games/{gameId}/documents` mapped to the new query. Require `[Authorize]` (authenticated user, not admin).

- [x] **Step 4: Write integration test**

Test the endpoint with different access scenarios: user with game in library (allowed), user without (forbidden), public game (allowed).

- [x] **Step 5: Commit**

```bash
git add apps/api/
git commit -m "feat(api): add user-facing GET /shared-games/{gameId}/documents endpoint"
```

---

### Task 11: Frontend API client + React Query hook for shared game documents

**Files:**
- Modify: `apps/web/src/lib/api/clients/sharedGamesClient.ts`
- Create: `apps/web/src/hooks/queries/useSharedGameDocuments.ts`
- Test: `apps/web/src/__tests__/hooks/useSharedGameDocuments.test.ts`

- [x] **Step 1: Read sharedGamesClient.ts to understand pattern**

Read `apps/web/src/lib/api/clients/sharedGamesClient.ts`. Find how other methods are structured.

- [x] **Step 2: Add getDocuments method to sharedGamesClient**

```typescript
// In sharedGamesClient.ts
async getDocuments(gameId: string): Promise<SharedGameDocument[]> {
  const response = await this.client.get(`/api/v1/shared-games/${gameId}/documents`);
  return response.data;
}
```

- [x] **Step 3: Write failing test for useSharedGameDocuments hook**

```typescript
// apps/web/src/__tests__/hooks/useSharedGameDocuments.test.ts
import { describe, it, expect } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useSharedGameDocuments } from '@/hooks/queries/useSharedGameDocuments';
// ... wrapper with QueryClientProvider, MSW handler for /api/v1/shared-games/game-1/documents
```

- [x] **Step 4: Implement useSharedGameDocuments hook**

```typescript
// apps/web/src/hooks/queries/useSharedGameDocuments.ts
'use client';

import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export function useSharedGameDocuments(gameId: string | null) {
  return useQuery({
    queryKey: ['shared-game-documents', gameId],
    queryFn: () => api.sharedGames.getDocuments(gameId!),
    enabled: !!gameId,
  });
}
```

- [x] **Step 5: Run test**

Run: `cd apps/web && pnpm vitest run src/__tests__/hooks/useSharedGameDocuments.test.ts`
Expected: PASS

- [x] **Step 6: Commit**

```bash
git add apps/web/src/lib/api/clients/sharedGamesClient.ts apps/web/src/hooks/queries/useSharedGameDocuments.ts apps/web/src/__tests__/hooks/useSharedGameDocuments.test.ts
git commit -m "feat(api): add useSharedGameDocuments hook for user-facing KB documents"
```

---

### Task 12: KBSelectionPanel component with auto-select

**Files:**
- Create: `apps/web/src/components/sheets/KBSelectionPanel.tsx`
- Test: `apps/web/src/__tests__/components/sheets/KBSelectionPanel.test.tsx`

- [x] **Step 1: Write failing test for auto-selection algorithm**

Test: given a list of documents, auto-selects latest Rulebook + latest Errata. Test edge cases: no errata, multiple rulebook versions, single KB only.

- [x] **Step 2: Implement KBSelectionPanel**

Component receives `gameId`, fetches documents via `useSharedGameDocuments`, applies auto-selection, and renders:
- Auto-selected section (green border, checked)
- Optional expansions/homerules section (unchecked)
- "Crea Agente con N KB" button
- Back button

Props:
```typescript
interface KBSelectionPanelProps {
  gameId: string;
  gameTitle: string;
  onBack: () => void;
  onConfirm: (selectedDocumentIds: string[], summary: string) => void;
}
```

- [x] **Step 3: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/sheets/KBSelectionPanel.test.tsx`
Expected: PASS

- [x] **Step 4: Commit**

```bash
git add apps/web/src/components/sheets/KBSelectionPanel.tsx apps/web/src/__tests__/components/sheets/KBSelectionPanel.test.tsx
git commit -m "feat(sheets): add KBSelectionPanel with smart auto-selection algorithm"
```

---

### Task 13: SearchAgentSheet component

**Files:**
- Create: `apps/web/src/components/sheets/SearchAgentSheet.tsx`
- Test: `apps/web/src/__tests__/components/sheets/SearchAgentSheet.test.tsx`

- [x] **Step 1: Write failing test for SearchAgentSheet**

Test: renders search input, shows games from library + shared, filters by KB availability, clicking game navigates to KB selection panel.

- [x] **Step 2: Implement SearchAgentSheet**

Two-step sheet:
1. Game search (input + scope filters + results list)
2. KB selection (renders `KBSelectionPanel` inline)

Props:
```typescript
interface SearchAgentSheetProps {
  isOpen: boolean;
  onClose: () => void;
  onCreateAgent: (gameId: string, gameTitle: string, documentIds: string[], documentSummary: string) => void;
}
```

Uses bottom sheet (mobile) / right drawer (desktop) pattern matching `AgentCreationSheet`.

- [x] **Step 3: Run test to verify it passes**

- [x] **Step 4: Commit**

```bash
git add apps/web/src/components/sheets/SearchAgentSheet.tsx apps/web/src/__tests__/components/sheets/SearchAgentSheet.test.tsx
git commit -m "feat(sheets): add SearchAgentSheet with game search and KB selection flow"
```

---

### Task 14: Extend AgentCreationSheet with pre-fill props

**Files:**
- Modify: `apps/web/src/components/agent/config/AgentCreationSheet.tsx`
- Modify: `apps/web/src/hooks/queries/useCreateAgentFlow.ts`
- Modify: `apps/web/src/lib/api/clients/agentsClient.ts`

- [x] **Step 1: Read AgentCreationSheet to understand step rendering**

Read `apps/web/src/components/agent/config/AgentCreationSheet.tsx`. Identify the step/section rendering logic and where `GameSelector` and PDF upload are rendered.

- [x] **Step 2: Add new props to AgentCreationSheet**

```typescript
interface AgentCreationSheetProps {
  isOpen: boolean;
  onClose: () => void;
  initialGameId?: string;
  initialGameTitle?: string;
  initialDocumentIds?: string[];
  initialDocumentSummary?: string;
  skipGameSelection?: boolean;
  skipKBUpload?: boolean;
}
```

Conditional rendering:
- If `skipGameSelection`: don't render GameSelector section, show read-only game badge
- If `skipKBUpload` + `initialDocumentIds`: don't render PDF upload, show read-only KB summary with "Modifica" link

- [x] **Step 3: Extend useCreateAgentFlow to accept documentIds**

In `apps/web/src/hooks/queries/useCreateAgentFlow.ts`, add `documentIds?: string[]` to the input type and forward it in the mutation payload.

- [x] **Step 4: Update agentsClient.createWithSetup to forward documentIds**

In `apps/web/src/lib/api/clients/agentsClient.ts`, ensure the `createWithSetup` method includes `documentIds` in the API call body.

- [x] **Step 5: Verify typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [x] **Step 6: Commit**

```bash
git add apps/web/src/components/agent/config/AgentCreationSheet.tsx apps/web/src/hooks/queries/useCreateAgentFlow.ts apps/web/src/lib/api/clients/agentsClient.ts
git commit -m "feat(agent-wizard): add pre-fill props for game and KB document selection"
```

---

### Task 15: Wire SearchAgentSheet into UnifiedShellClient

**Files:**
- Modify: `apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx`

- [x] **Step 1: Replace placeholder div with SearchAgentSheet**

```typescript
import { SearchAgentSheet } from '@/components/sheets/SearchAgentSheet';
import { AgentCreationSheet } from '@/components/agent/config/AgentCreationSheet';

// State for wizard handoff
const [agentWizardState, setAgentWizardState] = useState<{
  gameId: string; gameTitle: string; documentIds: string[]; documentSummary: string;
} | null>(null);

// In render:
<SearchAgentSheet
  isOpen={activeSheet === 'search-agent'}
  onClose={closeSheet}
  onCreateAgent={(gameId, gameTitle, documentIds, documentSummary) => {
    closeSheet();
    setAgentWizardState({ gameId, gameTitle, documentIds, documentSummary });
  }}
/>

{agentWizardState && (
  <AgentCreationSheet
    isOpen={!!agentWizardState}
    onClose={() => setAgentWizardState(null)}
    initialGameId={agentWizardState.gameId}
    initialGameTitle={agentWizardState.gameTitle}
    initialDocumentIds={agentWizardState.documentIds}
    initialDocumentSummary={agentWizardState.documentSummary}
    skipGameSelection
    skipKBUpload
  />
)}
```

- [x] **Step 2: Verify end-to-end flow visually**

Run: `cd apps/web && pnpm dev`
Click "Cerca Agente" placeholder → SearchAgentSheet opens → search game → select KB → wizard opens with pre-fill.

- [x] **Step 3: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx
git commit -m "feat(shell): wire SearchAgentSheet + AgentCreationSheet handoff"
```

---

## Chunk 3: Search Game + Session Sheets

### Task 16: SearchGameSheet

**Files:**
- Create: `apps/web/src/components/sheets/SearchGameSheet.tsx`
- Test: `apps/web/src/__tests__/components/sheets/SearchGameSheet.test.tsx`

- [x] **Step 1: Write failing test**

Test: renders search, shows games from library + shared (no KB filter), clicking navigates to game page.

- [x] **Step 2: Implement SearchGameSheet**

Similar structure to SearchAgentSheet step 1, but:
- No KB filter — show all games
- Click navigates to `/games/{id}` (or appropriate game detail route)
- Uses `useRouter().push()` for navigation

Props:
```typescript
interface SearchGameSheetProps {
  isOpen: boolean;
  onClose: () => void;
}
```

- [x] **Step 3: Run test, verify passes**
- [x] **Step 4: Wire into UnifiedShellClient (replace placeholder div)**
- [x] **Step 5: Commit**

```bash
git add apps/web/src/components/sheets/SearchGameSheet.tsx apps/web/src/__tests__/components/sheets/SearchGameSheet.test.tsx apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx
git commit -m "feat(sheets): add SearchGameSheet for game search quick action"
```

---

### Task 17: SessionSheet

**Files:**
- Create: `apps/web/src/components/sheets/SessionSheet.tsx`
- Test: `apps/web/src/__tests__/components/sheets/SessionSheet.test.tsx`

- [x] **Step 1: Write failing test**

Test two states: (a) with active sessions → renders list, (b) no sessions → renders creation form.

- [x] **Step 2: Implement SessionSheet**

Two states:
- Active/paused sessions exist: list with status badges, click navigates to session
- No sessions: creation form (reuses `GameSelector` for game selection, participant input, submit)

Props:
```typescript
interface SessionSheetProps {
  isOpen: boolean;
  onClose: () => void;
}
```

- [x] **Step 3: Run test, verify passes**
- [x] **Step 4: Wire into UnifiedShellClient**
- [x] **Step 5: Commit**

```bash
git add apps/web/src/components/sheets/SessionSheet.tsx apps/web/src/__tests__/components/sheets/SessionSheet.test.tsx apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx
git commit -m "feat(sheets): add SessionSheet for session management quick action"
```

---

## Chunk 4: Toolkit

### Task 18: DiceRoller component

**Files:**
- Create: `apps/web/src/components/toolkit/DiceRoller.tsx`
- Test: `apps/web/src/__tests__/components/toolkit/DiceRoller.test.tsx`

- [x] **Step 1: Write failing test**

Test: renders dice type selector (d4-d20), count selector (1-10), roll button, displays results.

- [x] **Step 2: Implement DiceRoller**

Stateless component with local state for: dice type, count, results array. Uses `Math.random()` for rolls (client-only, no SSR concern since it's inside a sheet).

- [x] **Step 3: Run test, verify passes**
- [x] **Step 4: Commit**

```bash
git add apps/web/src/components/toolkit/DiceRoller.tsx apps/web/src/__tests__/components/toolkit/DiceRoller.test.tsx
git commit -m "feat(toolkit): add DiceRoller component"
```

---

### Task 19: Timer component

**Files:**
- Create: `apps/web/src/components/toolkit/Timer.tsx`
- Test: `apps/web/src/__tests__/components/toolkit/Timer.test.tsx`

- [x] **Step 1: Write failing test**

Test: countdown mode (set time, start, pause, reset), stopwatch mode (start, lap, stop).

- [x] **Step 2: Implement Timer**

Local state with `useRef` for interval. Two modes: countdown (configurable minutes/seconds) and stopwatch.

- [x] **Step 3: Run test, verify passes**
- [x] **Step 4: Commit**

```bash
git add apps/web/src/components/toolkit/Timer.tsx apps/web/src/__tests__/components/toolkit/Timer.test.tsx
git commit -m "feat(toolkit): add Timer component with countdown and stopwatch modes"
```

---

### Task 20: Counter component

**Files:**
- Create: `apps/web/src/components/toolkit/Counter.tsx`
- Test: `apps/web/src/__tests__/components/toolkit/Counter.test.tsx`

- [x] **Step 1: Write failing test**

Test: increment, decrement, reset, multiple named counters.

- [x] **Step 2: Implement Counter**

Multiple counters with labels. Each has +/- buttons and reset. "Add counter" button.

- [x] **Step 3: Run test, verify passes**
- [x] **Step 4: Commit**

```bash
git add apps/web/src/components/toolkit/Counter.tsx apps/web/src/__tests__/components/toolkit/Counter.test.tsx
git commit -m "feat(toolkit): add Counter component with multiple named counters"
```

---

### Task 21: Scoreboard component

**Files:**
- Create: `apps/web/src/components/toolkit/Scoreboard.tsx`
- Test: `apps/web/src/__tests__/components/toolkit/Scoreboard.test.tsx`

- [x] **Step 1: Write failing test**

Test: add player, update score, sort by score, remove player.

- [x] **Step 2: Implement Scoreboard**

Table with player name + score. Add player form. +/- buttons per player. Sort toggle.

- [x] **Step 3: Run test, verify passes**
- [x] **Step 4: Commit**

```bash
git add apps/web/src/components/toolkit/Scoreboard.tsx apps/web/src/__tests__/components/toolkit/Scoreboard.test.tsx
git commit -m "feat(toolkit): add Scoreboard component for N-player scoring"
```

---

### Task 22: Randomizer component

**Files:**
- Create: `apps/web/src/components/toolkit/Randomizer.tsx`
- Test: `apps/web/src/__tests__/components/toolkit/Randomizer.test.tsx`

- [x] **Step 1: Write failing test**

Test: add items to list, pick random, display result, clear list.

- [x] **Step 2: Implement Randomizer**

Input for adding items, list display, "Pick random" button with result highlight.

- [x] **Step 3: Run test, verify passes**
- [x] **Step 4: Commit**

```bash
git add apps/web/src/components/toolkit/Randomizer.tsx apps/web/src/__tests__/components/toolkit/Randomizer.test.tsx
git commit -m "feat(toolkit): add Randomizer component for random selection from list"
```

---

### Task 23: ToolkitSheet (orchestrator)

**Files:**
- Create: `apps/web/src/components/sheets/ToolkitSheet.tsx`
- Test: `apps/web/src/__tests__/components/sheets/ToolkitSheet.test.tsx`

- [x] **Step 1: Write failing test**

Test: renders grid of 5 tools, clicking a tool renders it inline, back button returns to grid.

- [x] **Step 2: Implement ToolkitSheet**

Grid of tool cards. Click opens tool inline (replaces grid). Back button returns to grid.

```typescript
interface ToolkitSheetProps {
  isOpen: boolean;
  onClose: () => void;
}
```

- [x] **Step 3: Run test, verify passes**
- [x] **Step 4: Wire into UnifiedShellClient (replace placeholder div)**
- [x] **Step 5: Commit**

```bash
git add apps/web/src/components/sheets/ToolkitSheet.tsx apps/web/src/__tests__/components/sheets/ToolkitSheet.test.tsx apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx
git commit -m "feat(sheets): add ToolkitSheet with 5 board game tools"
```

---

## Chunk 5: Final Integration + Cleanup

### Task 24: Integration test — full placeholder flow

**Files:**
- Create: `apps/web/src/__tests__/integration/placeholder-cards-flow.test.tsx`

- [x] **Step 1: Write integration test**

Test the full flow: render UnifiedShellClient → placeholder cards visible → click "Cerca Agente" → SearchAgentSheet opens → select game → KB panel → confirm → AgentCreationSheet opens with pre-fill.

- [x] **Step 2: Run test**

Run: `cd apps/web && pnpm vitest run src/__tests__/integration/placeholder-cards-flow.test.tsx`
Expected: PASS

- [x] **Step 3: Commit**

```bash
git add apps/web/src/__tests__/integration/placeholder-cards-flow.test.tsx
git commit -m "test(integration): add full placeholder cards flow test"
```

---

### Task 25: Typecheck + lint + full test suite

- [x] **Step 1: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [x] **Step 2: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: No errors (fix any if found)

- [x] **Step 3: Run full test suite**

Run: `cd apps/web && pnpm test`
Expected: All tests pass

- [x] **Step 4: Fix any failures and commit**

```bash
git add -A
git commit -m "chore: fix lint and test issues from deckstack implementation"
```

---

### Task 26: Final review + PR

- [x] **Step 1: Review all changes**

Run: `git diff main...HEAD --stat` to see all changed files.

- [x] **Step 2: Create PR**

PR to parent branch (track with `git config branch.<feature>.parent`). Title: "feat: add deckstack placeholder actions + search agent flow + toolkit"

Include in PR body:
- Summary of changes (placeholder cards, 4 action flows, toolkit, KB selection)
- Link to spec: `docs/superpowers/specs/2026-03-16-user-flow-deckstack-design.md`
- Test plan checklist

---

## Task Dependency Graph

```
Task 1 (HandCard interface)
  → Task 2 (PLACEHOLDER_ACTION_CARDS config)
  → Task 3 (usePlaceholderActions hook)
    → Task 4 (CardStackItem placeholder rendering) ─┐
    → Task 5 (HandDrawerCard placeholder rendering) ─┤
                                                      → Task 6 (UnifiedShellClient wiring + migration)
        → Task 15 (Wire SearchAgentSheet)
        → Task 16 (SearchGameSheet) → Task 16.4 (wire)
        → Task 17 (SessionSheet) → Task 17.4 (wire)
        → Task 23 (ToolkitSheet) → Task 23.4 (wire)

Task 7 (DeckTrackerSync)
  → Task 8 (Add to detail pages)

Task 9 (Remove AgentsSidebar) — independent

Task 10 (Backend endpoint) → Task 11 (Frontend API + hook) → Task 12 (KBSelectionPanel)
  → Task 13 (SearchAgentSheet) → Task 14 (AgentCreationSheet pre-fill) → Task 15 (wire)

Task 18-22 (Toolkit components) → Task 23 (ToolkitSheet)

Task 24-26 (Integration + cleanup) — after all above
```

**Parallelizable groups:**
- Group A: Tasks 1-6 (placeholder infrastructure) — sequential internally
- Group B: Tasks 7-8 (DeckTrackerSync) — independent of Group A
- Group C: Task 9 (AgentsSidebar removal) — independent
- Group D1: Tasks 10-11 (backend endpoint + API client) — **independent, can start immediately**
- Group D2: Tasks 12-15 (KB panel + search agent + wizard + wiring) — depends on Task 6 AND Task 11
- Group E: Tasks 18-23 (toolkit) — depends on Task 6
- Group F: Tasks 16-17 (game search + session) — depends on Task 6
- Final: Tasks 24-26 (integration) — after all groups

**Note**: `CardStack.tsx` and `HandDrawer.tsx` are modified in Task 6 Step 5 (adding `onPlaceholderClick` prop forwarding) — this is an implicit modification not listed in the spec's "Files to Modify" table but required for the placeholder wiring to work.
