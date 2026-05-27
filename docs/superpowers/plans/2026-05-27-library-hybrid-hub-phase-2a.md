# Library Hybrid Hub — Phase 2a (Surface, games/sessions/chat) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the `/library` surface from the current games-only 3-tab view into the hybrid multi-entity hub for the 3 ready sources (games + sessions + chat), using the Phase 1 foundation (`useHybridHubItems` orchestration + `deriveHybridItems`), with a functional `CrossEntityFilters` STATO chip and game-scoped bulk selection — agents + kb stubbed to `[]` until BE-1 #1588 / BE-2 #1589 (Phase 2b).

**Architecture:** Brownfield refactor of `LibraryHub`. Two greenfield pieces land first as green commits (`useHybridHubItems` hook + `CrossEntityFilters` component); `LibraryTabs` is generalized to a generic key type (backward-compatible green commit); then **one atomic "flip" commit** swaps `LibraryHybridGrid` to `HybridHubItem[]` and rewires `LibraryHub` (6-tab via the existing `HybridHubTab`, hook orchestration, hero hybrid stats, game-scoped selection, partial-failure FSM) + rewrites the affected tests. The atomic flip is irreducible because the pre-commit `tsc --noEmit` gate rejects an intermediate state where the grid prop type and its `LibraryHub` consumer disagree.

**Key technical decisions (from the /sc:spec-panel resolutions on #1605):**
- **Tab state uses `HybridHubTab`** (`'all'|'games'|'agents'|'kb'|'sessions'|'chat'`, already exported from `lib/library/hybrid-hub.derive.ts`). `LibraryEntityKey` (`'all'|'kb'|'loaned'`) is **NOT** expanded — it stays the SSOT for the game **state filters** that the `CrossEntityFilters` STATO chip reuses via the existing `filterByEntity`. This keeps `library-filters.ts` + its 28 tests untouched.
- **FSM + partial-failure:** `error` only when all sources fail; `empty` = zero items across all sources; `filtered-empty` = sources have data but tab+query+filters eliminate all; per-source inline error otherwise.
- **Cardinality:** cap 20/source in the `all` tab inside `useHybridHubItems`; `useActiveSessions` raised to limit 20; `useRecentChatSessions` stays auth-gated; `useAgents`/`useUserKbDocs` stubbed to `[]`.
- **Selection mode is game-scoped:** `LibraryHub` forces `browse` when `tab !== 'games'`; `BulkSelectionBar` mounts only in `games`.

**Tech Stack:** TypeScript 5 · React 19 · TanStack Query · react-intl (`@/hooks/useTranslation`) · Vitest + Testing Library + jest-axe

---

## File Structure

| File | Responsibility | Status |
|---|---|---|
| `apps/web/src/lib/library/hybrid-hub.types.ts` | Add `hasKb: boolean` to `GameHubItem` (so the STATO with-KB chip has data) | Modify (Phase 1 enrich) |
| `apps/web/src/lib/library/hybrid-hub.mappers.ts` | `libraryEntryToHubItem` sets `hasKb` from `isKbEntry(entry)` | Modify (Phase 1 enrich) |
| `apps/web/src/hooks/queries/useHybridHubItems.ts` | Orchestrate 5 sources → map → `HybridHubSources`; expose `{ sources, isLoading, partialErrors, totalCounts }`; cap + auth-gating | Create |
| `apps/web/src/hooks/queries/__tests__/useHybridHubItems.test.tsx` | Hook unit tests (mocked source hooks) | Create |
| `apps/web/src/components/features/library/CrossEntityFilters.tsx` | Chip row (STATO/STATS/ORD + "Più filtri"); STATO functional for `games` (Owned/Wishlist/InPrestito + with-KB) | Create |
| `apps/web/src/components/features/library/__tests__/CrossEntityFilters.test.tsx` | Component tests | Create |
| `apps/web/src/components/features/library/LibraryTabs.tsx` | Generalize to `<K extends string>` (backward-compatible) | Modify |
| `apps/web/src/components/features/library/LibraryHybridGrid.tsx` | Accept `HybridHubItem[]` instead of `UserLibraryEntry[]`; render `MeepleCard entity={item.entity}` | Modify (flip) |
| `apps/web/src/components/features/library/index.ts` | Export `CrossEntityFilters` + its types | Modify |
| `apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx` | Rewire to hybrid orchestration | Modify (flip) |
| `apps/web/src/components/features/library/__tests__/LibraryHybridGrid.test.tsx` | Rewrite for `HybridHubItem[]` | Modify (flip) |
| `apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx` | Rewrite fixtures games-only → hybrid; 6-tab; partial-failure; selection game-scoped | Modify (flip) |

---

## Type reference (already on main-dev, Phase 1 — do NOT redefine)

From `lib/library/hybrid-hub.types.ts`: `HybridHubItem` (union), `GameHubItem`, `AgentHubItem`, `KbHubItem`, `SessionHubItem`, `ChatHubItem`, `HybridHubEntity`.
From `lib/library/hybrid-hub.mappers.ts`: `libraryEntryToHubItem`, `agentToHubItem`, `kbDocToHubItem`, `sessionToHubItem`, `chatToHubItem`, `KbDoc`.
From `lib/library/hybrid-hub.derive.ts`: `deriveHybridItems(sources, tab, query, sort)`, `HybridHubTab`, `HybridHubSources`.
From `lib/library/library-filters.ts`: `LibraryEntityKey` (`'all'|'kb'|'loaned'`), `LibrarySortKey`, `filterByEntity`, `isKbEntry`.

---

### Task 0: Enrich `GameHubItem` with `hasKb` (Phase 1 foundation touch-up)

The STATO with-KB chip (Task 2) needs to filter games that have a knowledge base. `GameHubItem` (Phase 1) doesn't carry that flag. Add `hasKb: boolean`, set from the existing `isKbEntry(entry)` helper. This is a small, non-breaking enrichment of the merged Phase 1 foundation.

**Files:**
- Modify: `apps/web/src/lib/library/hybrid-hub.types.ts` (add field to `GameHubItem`)
- Modify: `apps/web/src/lib/library/hybrid-hub.mappers.ts` (set it in `libraryEntryToHubItem`)
- Modify: `apps/web/src/lib/library/__tests__/hybrid-hub.mappers.test.ts` (assert the field)

- [ ] **Step 1: Update the mapper test (TDD — assert the new field first)**

In `hybrid-hub.mappers.test.ts`, in the `libraryEntryToHubItem` describe block, add a test (and the base fixture already has `hasKb`/`kbCardCount` fields — `isKbEntry` returns `hasKb || kbCardCount > 0`):

```ts
it('sets hasKb from isKbEntry (true when hasKb or kbCardCount>0)', () => {
  expect(libraryEntryToHubItem({ ...baseEntry, hasKb: true, kbCardCount: 0 }).hasKb).toBe(true);
  expect(libraryEntryToHubItem({ ...baseEntry, hasKb: false, kbCardCount: 2 }).hasKb).toBe(true);
  expect(libraryEntryToHubItem({ ...baseEntry, hasKb: false, kbCardCount: 0 }).hasKb).toBe(false);
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm exec vitest run hybrid-hub.mappers`
Expected: FAIL — `hasKb` is not a property of `GameHubItem` (type error) / undefined at runtime.

- [ ] **Step 3: Add the field + set it**

In `hybrid-hub.types.ts`, add to `GameHubItem` — **optional** so the existing Phase 1 `GameHubItem` literal factories (e.g. `gameItem()` in `hybrid-hub.derive.test.ts`) don't break; the mapper always sets it:
```ts
export interface GameHubItem extends HybridHubItemBase {
  readonly entity: 'game';
  readonly gameId: string;
  readonly rating?: number;
  readonly state?: GameStateType;
  readonly imageUrl?: string;
  /** True if the game has ≥1 KB doc (mirrors the retired `kb` tab). Optional in the
   *  type (so test literals stay valid) but always set by `libraryEntryToHubItem`. */
  readonly hasKb?: boolean;
}
```

In `hybrid-hub.mappers.ts`, add the `isKbEntry` import and set `hasKb`:
```ts
import { isKbEntry } from './library-filters';
// ... inside libraryEntryToHubItem return object:
    hasKb: isKbEntry(entry),
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm exec vitest run hybrid-hub.mappers && pnpm exec tsc --noEmit`
Expected: PASS — mapper tests green (including the 3 new assertions), typecheck clean.

- [ ] **Step 5: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend && git add apps/web/src/lib/library/hybrid-hub.types.ts apps/web/src/lib/library/hybrid-hub.mappers.ts apps/web/src/lib/library/__tests__/hybrid-hub.mappers.test.ts
git commit -m "feat(library): #1605 add GameHubItem.hasKb for the STATO with-KB filter (Phase 2a)"
```

---

### Task 1: `useHybridHubItems` orchestration hook (greenfield)

**Files:**
- Create: `apps/web/src/hooks/queries/useHybridHubItems.ts`
- Create: `apps/web/src/hooks/queries/__tests__/useHybridHubItems.test.tsx`

The hook calls the 3 ready source hooks (games/sessions/chat) + stubs agents/kb to `[]`, maps each to `HubItem[]`, caps to 20/source, and reports per-source errors. It returns `HybridHubSources` (the shape `deriveHybridItems` consumes) + loading/error state. It does NOT call `deriveHybridItems` itself — that stays in `LibraryHub` (the hook is the data layer; derivation is the view layer, keeping the hook testable without tab/query/sort).

- [ ] **Step 1: Write the failing test**

`apps/web/src/hooks/queries/__tests__/useHybridHubItems.test.tsx`:

```tsx
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';

import { useHybridHubItems } from '../useHybridHubItems';

// Mock the 3 source hooks + the 2 stub hooks
const mockUseLibrary = vi.fn();
const mockUseActiveSessions = vi.fn();
const mockUseRecentChatSessions = vi.fn();
const mockUseAgents = vi.fn();

vi.mock('../useLibrary', () => ({
  useLibrary: (...args: unknown[]) => mockUseLibrary(...args),
}));
vi.mock('../useActiveSessions', () => ({
  useActiveSessions: (...args: unknown[]) => mockUseActiveSessions(...args),
}));
vi.mock('../useChatSessions', () => ({
  useRecentChatSessions: (...args: unknown[]) => mockUseRecentChatSessions(...args),
}));
vi.mock('../useAgents', () => ({
  useAgents: (...args: unknown[]) => mockUseAgents(...args),
}));

function ok<T>(data: T) {
  return { data, isLoading: false, isError: false, error: null };
}
function loading() {
  return { data: undefined, isLoading: true, isError: false, error: null };
}
function failed(err: Error) {
  return { data: undefined, isLoading: false, isError: true, error: err };
}

const gameEntry = {
  id: 'g1', userId: 'u', gameId: 'game-1', gameTitle: 'Catan', gamePublisher: 'Kosmos',
  gameYearPublished: 1995, gameIconUrl: null, gameImageUrl: null, addedAt: '2026-01-01T00:00:00Z',
  notes: null, isFavorite: false, currentState: 'Owned' as const, stateChangedAt: null, stateNotes: null,
  hasKb: false, kbCardCount: 0, kbIndexedCount: 0, kbProcessingCount: 0, ownershipDeclaredAt: null,
  hasRagAccess: false, agentIsOwned: true, minPlayers: 3, maxPlayers: 4, playingTimeMinutes: 60,
  complexityRating: 2, averageRating: 7, privateGameId: null, isPrivateGame: false, canProposeToCatalog: false,
};
const sessionDto = {
  id: 's1', gameId: 'game-1', status: 'Completed', startedAt: '2026-02-01T00:00:00Z',
  completedAt: '2026-02-01T01:00:00Z', playerCount: 4, players: [], winnerName: 'Alice',
  notes: null, durationMinutes: 60,
};
const chatDto = {
  id: 'c1', userId: 'u', gameId: 'game-1', gameTitle: 'Catan', agentId: null, agentType: null,
  agentName: null, title: 'How to play?', messageCount: 3, lastMessagePreview: null,
  createdAt: '2026-03-01T00:00:00Z', lastMessageAt: '2026-03-01T00:10:00Z', isArchived: false,
};

beforeEach(() => {
  mockUseLibrary.mockReturnValue(ok({ items: [gameEntry], page: 1, pageSize: 50, totalCount: 1, totalPages: 1, hasNextPage: false, hasPreviousPage: false }));
  mockUseActiveSessions.mockReturnValue(ok({ sessions: [sessionDto], page: 1, pageSize: 20, totalCount: 1 }));
  mockUseRecentChatSessions.mockReturnValue(ok([chatDto]));
  mockUseAgents.mockReturnValue(ok([])); // 2a stub
});

describe('useHybridHubItems', () => {
  it('maps the 3 ready sources into HybridHubSources (game/session/chat), agents/kb empty', () => {
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.sources.games.map(i => i.entity)).toEqual(['game']);
    expect(result.current.sources.sessions.map(i => i.entity)).toEqual(['session']);
    expect(result.current.sources.chat.map(i => i.entity)).toEqual(['chat']);
    expect(result.current.sources.agents).toEqual([]);
    expect(result.current.sources.kb).toEqual([]);
  });

  it('unwraps .items / .sessions from paginated responses; chat passes through', () => {
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.sources.games[0]?.id).toBe('g1');
    expect(result.current.sources.sessions[0]?.id).toBe('s1');
    expect(result.current.sources.chat[0]?.id).toBe('c1');
  });

  it('caps each source to 20 items', () => {
    const many = Array.from({ length: 30 }, (_, i) => ({ ...gameEntry, id: `g${i}`, gameId: `game-${i}` }));
    mockUseLibrary.mockReturnValue(ok({ items: many, page: 1, pageSize: 50, totalCount: 30, totalPages: 1, hasNextPage: false, hasPreviousPage: false }));
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.sources.games).toHaveLength(20);
  });

  it('reports totalCounts per source (pre-cap)', () => {
    const many = Array.from({ length: 30 }, (_, i) => ({ ...gameEntry, id: `g${i}`, gameId: `game-${i}` }));
    mockUseLibrary.mockReturnValue(ok({ items: many, page: 1, pageSize: 50, totalCount: 30, totalPages: 1, hasNextPage: false, hasPreviousPage: false }));
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.totalCounts.games).toBe(30);
    expect(result.current.totalCounts.sessions).toBe(1);
    expect(result.current.totalCounts.chat).toBe(1);
  });

  it('isLoading=true while any ready source is loading', () => {
    mockUseActiveSessions.mockReturnValue(loading());
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.isLoading).toBe(true);
  });

  it('partialErrors records the failing source; others still map', () => {
    mockUseActiveSessions.mockReturnValue(failed(new Error('boom')));
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.partialErrors.sessions).toBeInstanceOf(Error);
    expect(result.current.partialErrors.games).toBeNull();
    expect(result.current.sources.games).toHaveLength(1); // games still mapped
    expect(result.current.sources.sessions).toEqual([]); // failed source → empty
  });

  it('allFailed=true only when every ready source errors', () => {
    mockUseLibrary.mockReturnValue(failed(new Error('a')));
    mockUseActiveSessions.mockReturnValue(failed(new Error('b')));
    mockUseRecentChatSessions.mockReturnValue(failed(new Error('c')));
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.allFailed).toBe(true);
  });

  it('allFailed=false when at least one ready source succeeds', () => {
    mockUseLibrary.mockReturnValue(failed(new Error('a')));
    mockUseActiveSessions.mockReturnValue(ok({ sessions: [sessionDto], page: 1, pageSize: 20, totalCount: 1 }));
    mockUseRecentChatSessions.mockReturnValue(failed(new Error('c')));
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.allFailed).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm exec vitest run useHybridHubItems`
Expected: FAIL — `Cannot find module '../useHybridHubItems'`.

- [ ] **Step 3: Write the implementation**

`apps/web/src/hooks/queries/useHybridHubItems.ts`:

```ts
/**
 * useHybridHubItems — Phase 2a (#1605) orchestration hook for the `/library`
 * hybrid hub. Calls the 3 ready entity sources (games / sessions / chat),
 * maps each DTO to a `HybridHubItem` via the Phase 1 mappers, caps each source
 * to `PER_SOURCE_CAP`, and reports per-source errors so the hub can degrade
 * gracefully (AC9.1). Agents + KB are stubbed to `[]` until BE-2 #1589 / BE-1
 * #1588 ship (Phase 2b).
 *
 * Returns `HybridHubSources` (the shape `deriveHybridItems` consumes) — the
 * hook is the data layer; tab/query/sort derivation stays in `LibraryHub`.
 */

import { useMemo } from 'react';

import {
  agentToHubItem,
  chatToHubItem,
  libraryEntryToHubItem,
  sessionToHubItem,
} from '@/lib/library/hybrid-hub.mappers';
import type { HybridHubItem } from '@/lib/library/hybrid-hub.types';
import type { HybridHubSources } from '@/lib/library/hybrid-hub.derive';

import { useActiveSessions } from './useActiveSessions';
import { useAgents } from './useAgents';
import { useRecentChatSessions } from './useChatSessions';
import { useLibrary } from './useLibrary';

export const PER_SOURCE_CAP = 20;

export type HybridHubSourceKey = keyof HybridHubSources; // 'games'|'agents'|'kb'|'sessions'|'chat'

export interface UseHybridHubItemsResult {
  readonly sources: HybridHubSources;
  readonly isLoading: boolean;
  readonly allFailed: boolean;
  readonly partialErrors: Record<HybridHubSourceKey, Error | null>;
  readonly totalCounts: Record<HybridHubSourceKey, number>;
}

export function useHybridHubItems(): UseHybridHubItemsResult {
  const libraryQuery = useLibrary({ page: 1, pageSize: 50, sortBy: 'addedAt', sortDescending: true });
  const sessionsQuery = useActiveSessions(PER_SOURCE_CAP);
  const chatQuery = useRecentChatSessions(50);
  const agentsQuery = useAgents({}); // 2a: result intentionally ignored (stub [])

  return useMemo(() => {
    const gameItems = (libraryQuery.data?.items ?? []).map(libraryEntryToHubItem);
    const sessionItems = (sessionsQuery.data?.sessions ?? []).map(sessionToHubItem);
    const chatItems = (chatQuery.data ?? []).map(chatToHubItem);

    const cap = (items: readonly HybridHubItem[]) => items.slice(0, PER_SOURCE_CAP);

    const sources: HybridHubSources = {
      games: libraryQuery.isError ? [] : cap(gameItems),
      sessions: sessionsQuery.isError ? [] : cap(sessionItems),
      chat: chatQuery.isError ? [] : cap(chatItems),
      agents: [], // Phase 2b: useAgents({ scope: 'my-library' }) → agentToHubItem
      kb: [], // Phase 2b: useUserKbDocs() → kbDocToHubItem
    };

    const partialErrors: Record<HybridHubSourceKey, Error | null> = {
      games: libraryQuery.isError ? (libraryQuery.error ?? new Error('library')) : null,
      sessions: sessionsQuery.isError ? (sessionsQuery.error ?? new Error('sessions')) : null,
      chat: chatQuery.isError ? (chatQuery.error ?? new Error('chat')) : null,
      agents: null,
      kb: null,
    };

    const totalCounts: Record<HybridHubSourceKey, number> = {
      games: gameItems.length,
      sessions: sessionItems.length,
      chat: chatItems.length,
      agents: 0,
      kb: 0,
    };

    const readyErrors = [libraryQuery.isError, sessionsQuery.isError, chatQuery.isError];
    const allFailed = readyErrors.every(Boolean);
    const isLoading =
      libraryQuery.isLoading || sessionsQuery.isLoading || chatQuery.isLoading;

    // agentsQuery is referenced to keep the hook call (cache warm for 2b) without using its data
    void agentsQuery;

    return { sources, isLoading, allFailed, partialErrors, totalCounts };
  }, [
    libraryQuery.data, libraryQuery.isError, libraryQuery.error, libraryQuery.isLoading,
    sessionsQuery.data, sessionsQuery.isError, sessionsQuery.error, sessionsQuery.isLoading,
    chatQuery.data, chatQuery.isError, chatQuery.error, chatQuery.isLoading,
    agentsQuery,
  ]);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm exec vitest run useHybridHubItems`
Expected: PASS — all 8 tests green.

- [ ] **Step 5: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend && git add apps/web/src/hooks/queries/useHybridHubItems.ts apps/web/src/hooks/queries/__tests__/useHybridHubItems.test.tsx
git commit -m "feat(library): #1605 add useHybridHubItems orchestration hook (Phase 2a)"
```

---

### Task 2: `CrossEntityFilters` component (greenfield, functional STATO chip)

**Files:**
- Create: `apps/web/src/components/features/library/CrossEntityFilters.tsx`
- Create: `apps/web/src/components/features/library/__tests__/CrossEntityFilters.test.tsx`
- Modify: `apps/web/src/components/features/library/index.ts`

Renders a chip row. The STATO chip is **functional for the `games` tab** (toggles game-state filters Owned/Wishlist/InPrestito + a with-KB toggle); for other tabs it renders nothing (search + sort globals live in the hub toolbar). The component is controlled — parent owns the active filter state.

- [ ] **Step 1: Write the failing test**

`apps/web/src/components/features/library/__tests__/CrossEntityFilters.test.tsx`:

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';

import { CrossEntityFilters, type GameStateFilter } from '../CrossEntityFilters';

const messages: Record<string, string> = {
  'pages.library.filters.stato.label': 'Stato',
  'pages.library.filters.stato.owned': 'Posseduti',
  'pages.library.filters.stato.wishlist': 'Wishlist',
  'pages.library.filters.stato.loaned': 'In prestito',
  'pages.library.filters.stato.withKb': 'Con Knowledge Base',
};
function renderWithIntl(ui: React.ReactElement) {
  return render(<IntlProvider locale="it" messages={messages}>{ui}</IntlProvider>);
}
const noop = () => {};
const emptyFilter: GameStateFilter = { states: [], withKb: false };

describe('CrossEntityFilters', () => {
  it('renders the STATO chip group only for the games tab', () => {
    const { rerender } = renderWithIntl(
      <CrossEntityFilters tab="games" gameStateFilter={emptyFilter} onGameStateFilterChange={noop} />
    );
    expect(screen.getByTestId('cross-entity-filters-stato')).toBeInTheDocument();

    rerender(
      <IntlProvider locale="it" messages={messages}>
        <CrossEntityFilters tab="sessions" gameStateFilter={emptyFilter} onGameStateFilterChange={noop} />
      </IntlProvider>
    );
    expect(screen.queryByTestId('cross-entity-filters-stato')).not.toBeInTheDocument();
  });

  it('renders nothing for the all tab (search+sort are global in toolbar)', () => {
    const { container } = renderWithIntl(
      <CrossEntityFilters tab="all" gameStateFilter={emptyFilter} onGameStateFilterChange={noop} />
    );
    expect(container.querySelector('[data-testid="cross-entity-filters-stato"]')).toBeNull();
  });

  it('toggling a state chip emits the updated filter', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    renderWithIntl(
      <CrossEntityFilters tab="games" gameStateFilter={emptyFilter} onGameStateFilterChange={onChange} />
    );
    await user.click(screen.getByRole('button', { name: /posseduti/i }));
    expect(onChange).toHaveBeenCalledWith({ states: ['Owned'], withKb: false });
  });

  it('active state chip reflects the filter (aria-pressed)', () => {
    renderWithIntl(
      <CrossEntityFilters tab="games" gameStateFilter={{ states: ['Wishlist'], withKb: false }} onGameStateFilterChange={noop} />
    );
    expect(screen.getByRole('button', { name: /wishlist/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /posseduti/i })).toHaveAttribute('aria-pressed', 'false');
  });

  it('with-KB toggle emits withKb=true', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    renderWithIntl(
      <CrossEntityFilters tab="games" gameStateFilter={emptyFilter} onGameStateFilterChange={onChange} />
    );
    await user.click(screen.getByRole('button', { name: /con knowledge base/i }));
    expect(onChange).toHaveBeenCalledWith({ states: [], withKb: true });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm exec vitest run CrossEntityFilters`
Expected: FAIL — `Cannot find module '../CrossEntityFilters'`.

- [ ] **Step 3: Write the implementation**

`apps/web/src/components/features/library/CrossEntityFilters.tsx`:

```tsx
/**
 * CrossEntityFilters — Phase 2a (#1605). Chip row above the hub grid.
 *
 * In the `games` tab the STATO chip group is FUNCTIONAL: it carries the
 * game-state filters that the retired `loaned`/`kb` tabs used to provide
 * (Owned / Wishlist / InPrestito + a with-KB toggle), so no access regresses
 * when the 3 game-state tabs collapse into one `games` tab. For all other tabs
 * the component renders nothing — search + sort live as globals in the hub
 * toolbar.
 *
 * Controlled component: the parent (`LibraryHub`) owns `gameStateFilter`.
 */

'use client';

import { type ReactElement } from 'react';

import clsx from 'clsx';

import { useTranslation } from '@/hooks/useTranslation';
import type { GameStateType } from '@/lib/api/schemas/library.schemas';
import type { HybridHubTab } from '@/lib/library/hybrid-hub.derive';

export interface GameStateFilter {
  readonly states: ReadonlyArray<GameStateType>;
  readonly withKb: boolean;
}

export interface CrossEntityFiltersProps {
  readonly tab: HybridHubTab;
  readonly gameStateFilter: GameStateFilter;
  readonly onGameStateFilterChange: (next: GameStateFilter) => void;
  readonly className?: string;
}

const STATE_CHIPS: ReadonlyArray<{ value: GameStateType; i18nKey: string }> = [
  { value: 'Owned', i18nKey: 'pages.library.filters.stato.owned' },
  { value: 'Wishlist', i18nKey: 'pages.library.filters.stato.wishlist' },
  { value: 'InPrestito', i18nKey: 'pages.library.filters.stato.loaned' },
];

export function CrossEntityFilters({
  tab,
  gameStateFilter,
  onGameStateFilterChange,
  className,
}: CrossEntityFiltersProps): ReactElement | null {
  const { t } = useTranslation();

  if (tab !== 'games') return null;

  const toggleState = (value: GameStateType) => {
    const has = gameStateFilter.states.includes(value);
    const states = has
      ? gameStateFilter.states.filter(s => s !== value)
      : [...gameStateFilter.states, value];
    onGameStateFilterChange({ ...gameStateFilter, states });
  };

  return (
    <div
      data-slot="cross-entity-filters"
      data-testid="cross-entity-filters-stato"
      className={clsx('flex flex-wrap items-center gap-2', className)}
    >
      <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
        {t('pages.library.filters.stato.label')}
      </span>
      {STATE_CHIPS.map(chip => {
        const active = gameStateFilter.states.includes(chip.value);
        return (
          <button
            key={chip.value}
            type="button"
            aria-pressed={active}
            onClick={() => toggleState(chip.value)}
            className={clsx(
              'rounded-full border px-3 py-1 text-sm font-medium transition-colors',
              active
                ? 'border-primary bg-primary/10 text-foreground'
                : 'border-border bg-background text-muted-foreground hover:text-foreground'
            )}
          >
            {t(chip.i18nKey)}
          </button>
        );
      })}
      <button
        type="button"
        aria-pressed={gameStateFilter.withKb}
        onClick={() => onGameStateFilterChange({ ...gameStateFilter, withKb: !gameStateFilter.withKb })}
        className={clsx(
          'rounded-full border px-3 py-1 text-sm font-medium transition-colors',
          gameStateFilter.withKb
            ? 'border-primary bg-primary/10 text-foreground'
            : 'border-border bg-background text-muted-foreground hover:text-foreground'
        )}
      >
        {t('pages.library.filters.stato.withKb')}
      </button>
    </div>
  );
}
```

- [ ] **Step 3.5: Add i18n keys to both catalogs**

In `apps/web/src/locales/it.json` under `pages.library.filters`, add:
```jsonc
"stato": {
  "label": "Stato",
  "owned": "Posseduti",
  "wishlist": "Wishlist",
  "loaned": "In prestito",
  "withKb": "Con Knowledge Base"
}
```
In `apps/web/src/locales/en.json` under `pages.library.filters`, add:
```jsonc
"stato": {
  "label": "State",
  "owned": "Owned",
  "wishlist": "Wishlist",
  "loaned": "Loaned",
  "withKb": "With Knowledge Base"
}
```
(Verify the `pages.library.filters` object already exists in both — it does, per the existing `pages.library.filters.search.*` keys used by the current toolbar. Insert `stato` as a sibling without disturbing existing keys.)

- [ ] **Step 4: Add barrel export**

In `apps/web/src/components/features/library/index.ts`, add:
```ts
export { CrossEntityFilters } from '@/components/features/library/CrossEntityFilters';
export type {
  CrossEntityFiltersProps,
  GameStateFilter,
} from '@/components/features/library/CrossEntityFilters';
```

- [ ] **Step 5: Run test to verify it passes**

Run: `cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm exec vitest run CrossEntityFilters`
Expected: PASS — all 5 tests green.

- [ ] **Step 6: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend && git add apps/web/src/components/features/library/CrossEntityFilters.tsx apps/web/src/components/features/library/__tests__/CrossEntityFilters.test.tsx apps/web/src/components/features/library/index.ts apps/web/src/locales/it.json apps/web/src/locales/en.json
git commit -m "feat(library): #1605 add CrossEntityFilters STATO chip (Phase 2a)"
```

---

### Task 3: Generalize `LibraryTabs` to a generic key type (backward-compatible)

**Files:**
- Modify: `apps/web/src/components/features/library/LibraryTabs.tsx`
- Modify (if needed): `apps/web/src/components/features/library/__tests__/LibraryTabs.test.tsx`

Today `LibraryTabs` is typed on `LibraryEntityKey` (3 keys). Generalize it to `<K extends string>` so `LibraryHub` can pass `HybridHubTab` (6 keys) in the flip — without breaking the existing `LibraryTabs.test.tsx` (which uses `LibraryEntityKey`, still a valid `K`).

- [ ] **Step 1: Confirm existing tests pass (baseline)**

Run: `cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm exec vitest run LibraryTabs`
Expected: PASS (baseline before change).

- [ ] **Step 2: Generalize the component**

In `LibraryTabs.tsx`, change the config + props + component to be generic on the key. Replace the `LibraryTabConfig` interface and `LibraryTabsProps` + the function signature:

```tsx
export interface LibraryTabConfig<K extends string = LibraryEntityKey> {
  readonly key: K;
  readonly label: string;
  readonly count: number;
}

export interface LibraryTabsProps<K extends string = LibraryEntityKey> {
  readonly tabs: ReadonlyArray<LibraryTabConfig<K>>;
  readonly active: K;
  readonly onChange: (next: K) => void;
  readonly className?: string;
}

export function LibraryTabs<K extends string = LibraryEntityKey>({
  tabs,
  active,
  onChange,
  className,
}: LibraryTabsProps<K>): ReactElement {
  const orderedKeys = useMemo<readonly K[]>(() => tabs.map(t => t.key), [tabs]);
  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<K>({ orderedKeys, onChange });
  // ... rest of the body unchanged (it already operates on `K` via orderedKeys/active)
}
```

Keep the `import type { LibraryEntityKey } from '@/lib/library/library-filters'` (used as the default type param) and the existing `export type { LibraryEntityKey }` re-export (Phase 1 SSOT consolidation — do not remove). The body of the component (the `tabs.map`, underline math, `useTablistKeyboardNav`) already works on generic keys — only the type annotations change.

Verify `useTablistKeyboardNav` is itself generic (`useTablistKeyboardNav<K>`) — it is (Phase 1 used `useTablistKeyboardNav<LibraryEntityKey>`). If its signature is not generic, leave it as-is and cast at the call site; do NOT modify that hook in this task.

- [ ] **Step 3: Run typecheck + tests**

Run: `cd /d/Repositories/meepleai-monorepo-frontend/apps/web && pnpm exec tsc --noEmit && pnpm exec vitest run LibraryTabs`
Expected: PASS — typecheck clean (the existing `LibraryHub` still passes `LibraryEntityKey`, a valid `K`), `LibraryTabs.test.tsx` green unchanged.

If `tsc` flags the `LibraryHub` call site (it should not — `LibraryEntityKey` satisfies `K extends string`), STOP and report; do not modify `LibraryHub` in this task.

- [ ] **Step 4: Commit**

```bash
cd /d/Repositories/meepleai-monorepo-frontend && git add apps/web/src/components/features/library/LibraryTabs.tsx
git commit -m "refactor(library): #1605 generalize LibraryTabs to generic key type (Phase 2a)"
```

---

### Task 4: Atomic flip — grid + LibraryHub wire + test rewrite

**Files:**
- Modify: `apps/web/src/components/features/library/LibraryHybridGrid.tsx` (accept `HybridHubItem[]`)
- Modify: `apps/web/src/components/features/library/__tests__/LibraryHybridGrid.test.tsx` (rewrite for hybrid items)
- Modify: `apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx` (rewire to hybrid)
- Modify: `apps/web/src/app/(authenticated)/library/_components/__tests__/LibraryHub.test.tsx` (rewrite fixtures + 6-tab + partial-failure + selection game-scoped)

> **⚠️ This is the atomic flip — one commit.** The pre-commit `tsc --noEmit` gate rejects an intermediate where the grid prop type and `LibraryHub` disagree, so the grid change + the hub rewire + the two test rewrites land together. This is the critical, highest-risk task. Implement carefully; run the FULL library test suite before committing.

#### 4a. `LibraryHybridGrid` → `HybridHubItem[]`

Replace the props + render so the grid renders any `HybridHubItem` via `MeepleCard entity={item.entity}`. The selection contract (browse/select, `aria-pressed`, check overlay) is preserved.

New `LibraryHybridGrid.tsx` (replace lines 40-138, keeping the file header + the `clsx`/`MeepleCard` imports):

```tsx
import type { MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { HybridHubItem } from '@/lib/library/hybrid-hub.types';

export type LibraryViewMode = 'grid' | 'list' | 'compact';
export type LibrarySelectionMode = 'browse' | 'select';

export interface LibraryHybridGridProps {
  readonly items: ReadonlyArray<HybridHubItem>;
  readonly view: LibraryViewMode;
  readonly selectionMode: LibrarySelectionMode;
  readonly selected: ReadonlySet<string>;
  readonly onCardClick: (itemId: string) => void;
  readonly onLongPressEnter?: (itemId: string) => void;
  readonly className?: string;
}

const VIEW_TO_VARIANT: Record<LibraryViewMode, MeepleCardVariant> = {
  grid: 'grid', list: 'list', compact: 'compact',
};
const VIEW_TO_LAYOUT: Record<LibraryViewMode, string> = {
  grid: 'grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4',
  list: 'flex flex-col gap-2',
  compact: 'grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-6',
};

function itemImageUrl(item: HybridHubItem): string | undefined {
  return item.entity === 'game' ? item.imageUrl : undefined;
}
function itemRating(item: HybridHubItem): number | undefined {
  return item.entity === 'game' ? item.rating : undefined;
}

export function LibraryHybridGrid({
  items, view, selectionMode, selected, onCardClick, className,
}: LibraryHybridGridProps): ReactElement {
  const variant = VIEW_TO_VARIANT[view];
  const layoutClass = VIEW_TO_LAYOUT[view];
  const isSelectMode = selectionMode === 'select';
  return (
    <div data-slot="library-hybrid-grid" data-view={view} data-selection-mode={selectionMode}
      className={clsx(layoutClass, className)}>
      {items.map(item => {
        const isSelected = selected.has(item.id);
        return (
          <button key={item.id} type="button" data-slot="library-grid-card"
            data-selection-mode={selectionMode} data-entry-id={item.id}
            aria-pressed={isSelectMode ? isSelected : undefined}
            onClick={() => onCardClick(item.id)}
            className={clsx('relative block w-full text-left',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-2xl',
              isSelectMode && isSelected && 'ring-2 ring-primary')}>
            <MeepleCard entity={item.entity} variant={variant} title={item.title}
              subtitle={item.subtitle} imageUrl={itemImageUrl(item)}
              rating={itemRating(item)} ratingMax={10} />
            {isSelectMode && isSelected ? (
              <span data-testid="library-grid-card-check" aria-hidden="true"
                className="pointer-events-none absolute right-3 top-3 inline-flex h-6 w-6 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-sm">
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="h-4 w-4">
                  <path fillRule="evenodd" d="M16.704 5.29a1 1 0 010 1.42l-7.5 7.5a1 1 0 01-1.42 0l-3.5-3.5a1 1 0 011.42-1.42L8.5 12.08l6.79-6.79a1 1 0 011.414 0z" clipRule="evenodd" />
                </svg>
              </span>
            ) : null}
          </button>
        );
      })}
    </div>
  );
}
```

(Remove the `UserLibraryEntry` import + the `entry.gameImageUrl ?? entry.gameIconUrl` logic + `onLongPressEnter` from the destructure if unused — keep it in the prop type as optional for the orchestrator's long-press, referenced via `void onLongPressEnter` is NOT needed since it's just an unused optional prop; ESLint allows unused destructured-omitted props.)

#### 4b. `LibraryHybridGrid.test.tsx` rewrite

Rewrite the grid test to use `HybridHubItem[]` fixtures (mix of game/session/chat) and assert `MeepleCard` renders per `entity`, the selection overlay works, and `onCardClick(item.id)` fires. Keep the existing test count/structure; swap `UserLibraryEntry` fixtures for:

```tsx
import type { HybridHubItem } from '@/lib/library/hybrid-hub.types';

const items: HybridHubItem[] = [
  { id: 'g1', entity: 'game', title: 'Catan', subtitle: 'Kosmos', updatedAt: '2026-01-01T00:00:00Z', href: '/library/game-1', gameId: 'game-1', rating: 7, state: 'Owned', imageUrl: 'https://x/c.jpg' },
  { id: 's1', entity: 'session', title: 'Session s1', subtitle: 'Alice', updatedAt: '2026-02-01T00:00:00Z', href: '/sessions/s1', status: 'Completed', playerCount: 4 },
  { id: 'c1', entity: 'chat', title: 'How to play?', subtitle: 'Catan', updatedAt: '2026-03-01T00:00:00Z', href: '/chats/c1', messageCount: 3 },
];
```
Assert: 3 cards render; in select mode each card has `aria-pressed`; clicking calls `onCardClick` with the item id; a non-game item (session/chat) renders without a rating/image (no crash).

#### 4c. `LibraryHub.tsx` rewire

Replace the data layer + render. The full new `LibraryHub.tsx`:

```tsx
/**
 * LibraryHub — Phase 2a (#1605): hybrid multi-entity hub orchestrator.
 *
 * Migrated from the Wave B.3 games-only view. Tab state is `HybridHubTab`
 * (6 tabs); the 3 ready sources (games/sessions/chat) are orchestrated by
 * `useHybridHubItems` and merged/filtered/sorted by `deriveHybridItems`.
 * Agents + KB are stubbed `[]` until BE-2 #1589 / BE-1 #1588 (Phase 2b).
 *
 * Game-state filters (ex-`loaned`/`kb` tabs) live in the `CrossEntityFilters`
 * STATO chip, applied to the games source before merge. Selection mode is
 * game-scoped: forced to `browse` outside the `games` tab. FSM degrades on
 * partial failure: `error` only when all ready sources fail.
 */

'use client';

import { useCallback, useEffect, useMemo, useState, type ReactElement } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import {
  BulkSelectionBar, CrossEntityFilters, EmptyLibrary, LibraryHeroDesktop,
  LibraryHybridGrid, LibraryTabs, RecentActivityRail,
  type ActivityItem, type ActivityKind, type BulkSelectionBarLabels,
  type EmptyLibraryLabels, type GameStateFilter, type LibraryHeroDesktopLabels,
  type LibraryHeroStat, type LibrarySelectionMode, type LibraryTabConfig,
  type LibraryViewMode,
} from '@/components/features/library';
import { useHybridHubItems } from '@/hooks/queries/useHybridHubItems';
import { useLibraryActivity, useRemoveGameFromLibrary } from '@/hooks/queries/useLibrary';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
import { useTranslation } from '@/hooks/useTranslation';
import { deriveHybridItems, type HybridHubTab, type HybridHubSources } from '@/lib/library/hybrid-hub.derive';
import type { HybridHubItem } from '@/lib/library/hybrid-hub.types';
import { isKbEntry, type LibrarySortKey } from '@/lib/library/library-filters';
import { useLibraryView } from '@/lib/library/use-library-view';
import { IS_VISUAL_TEST_BUILD } from '@/lib/library/visual-test-fixture';

const VALID_OVERRIDES = ['loading', 'empty', 'filtered-empty', 'error'] as const;
type StateOverride = (typeof VALID_OVERRIDES)[number];
const STATE_OVERRIDE_ENABLED = process.env.NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD;
function parseStateOverride(raw: string | null): StateOverride | null {
  if (!STATE_OVERRIDE_ENABLED || raw == null) return null;
  return (VALID_OVERRIDES as readonly string[]).includes(raw) ? (raw as StateOverride) : null;
}
type SurfaceKind = 'default' | 'loading' | 'empty' | 'filtered-empty' | 'error';

const HUB_TABS: readonly HybridHubTab[] = ['all', 'games', 'agents', 'kb', 'sessions', 'chat'];

export function LibraryHub(): ReactElement {
  const { t } = useTranslation();
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const [tab, setTab] = useState<HybridHubTab>('all');
  const [selectionMode, setSelectionMode] = useState<LibrarySelectionMode>('browse');
  const [selected, setSelected] = useState<ReadonlySet<string>>(() => new Set());
  const [query, setQuery] = useState('');
  const [sortKey, setSortKey] = useState<LibrarySortKey>('recent');
  const [gameStateFilter, setGameStateFilter] = useState<GameStateFilter>({ states: [], withKb: false });
  const { view, setView } = useLibraryView('grid');

  const stateOverride = parseStateOverride(searchParams.get('state'));

  const hub = useHybridHubItems();
  const removeMutation = useRemoveGameFromLibrary();
  const activityQuery = useLibraryActivity(20);

  // Selection mode is game-scoped — force browse when leaving the games tab.
  useEffect(() => {
    if (tab !== 'games' && selectionMode === 'select') {
      setSelectionMode('browse');
      setSelected(new Set());
    }
  }, [tab, selectionMode]);

  // Apply game-state filters (ex-loaned/kb tabs) to the games source before merge.
  const filteredSources = useMemo<HybridHubSources>(() => {
    const { states, withKb } = gameStateFilter;
    if (states.length === 0 && !withKb) return hub.sources;
    const games = hub.sources.games.filter(item => {
      if (item.entity !== 'game') return true;
      const stateOk = states.length === 0 || (item.state != null && states.includes(item.state));
      const kbOk = !withKb || item.hasKb === true; // Task 0 added hasKb to GameHubItem (optional)
      return stateOk && kbOk;
    });
    return { ...hub.sources, games };
  }, [hub.sources, gameStateFilter]);

  const merged = useMemo<HybridHubItem[]>(
    () => deriveHybridItems(filteredSources, tab, query, sortKey),
    [filteredSources, tab, query, sortKey]
  );

  // Hero stats: hybrid counts (games/agents/docs/chats) from pre-filter totals.
  const heroStats = useMemo(
    () => ({
      games: hub.totalCounts.games,
      agents: hub.totalCounts.agents,
      docs: hub.totalCounts.kb,
      chats: hub.totalCounts.chat,
    }),
    [hub.totalCounts]
  );

  const activityItems = useMemo<readonly ActivityItem[]>(() => {
    const raw = activityQuery.data ?? [];
    const mapped: ActivityItem[] = [];
    for (const event of raw) {
      let kind: ActivityKind | null;
      switch (event.type) {
        case 'added': kind = 'add'; break;
        case 'state-changed': kind = 'rating-changed'; break;
        case 'session-recorded': kind = 'play'; break;
        case 'removed': kind = 'removed'; break;
        default: kind = null; break;
      }
      if (!kind) continue;
      mapped.push({ id: `${event.id}:${event.type}`, kind, entityTitle: event.gameTitle, timestamp: event.timestamp });
    }
    return mapped;
  }, [activityQuery.data]);

  // ─── Labels ───
  const heroLabels = useMemo<LibraryHeroDesktopLabels>(() => ({
    title: t('pages.library.hero.title'),
    subtitle: t('pages.library.hero.subtitle'),
    ctaAdd: t('pages.library.hero.cta.add'),
  }), [t]);

  const heroStatRows = useMemo<readonly LibraryHeroStat[]>(() => [
    { key: 'totalGames', label: t('pages.library.hero.stats.totalGames'), value: heroStats.games },
    { key: 'kbReady', label: t('pages.library.hero.stats.agents'), value: heroStats.agents },
    { key: 'wishlist', label: t('pages.library.hero.stats.docs'), value: heroStats.docs },
    { key: 'loaned', label: t('pages.library.hero.stats.chats'), value: heroStats.chats },
  ], [t, heroStats]);

  const tabsConfig = useMemo<readonly LibraryTabConfig<HybridHubTab>[]>(() => {
    const countFor = (tk: HybridHubTab): number => {
      if (tk === 'all') return Object.values(hub.totalCounts).reduce((a, b) => a + b, 0);
      if (tk === 'games') return hub.totalCounts.games;
      if (tk === 'agents') return hub.totalCounts.agents;
      if (tk === 'kb') return hub.totalCounts.kb;
      if (tk === 'sessions') return hub.totalCounts.sessions;
      return hub.totalCounts.chat;
    };
    return HUB_TABS.map(tk => ({ key: tk, label: t(`pages.library.hubTabs.${tk}`), count: countFor(tk) }));
  }, [t, hub.totalCounts]);

  const emptyLabels = useMemo<EmptyLibraryLabels>(() => ({
    empty: { title: t('pages.library.emptyState.default.title'), subtitle: t('pages.library.emptyState.default.subtitle'), cta: t('pages.library.emptyState.default.cta') },
    filteredEmpty: { title: t('pages.library.emptyState.filteredEmpty.title'), subtitle: t('pages.library.emptyState.filteredEmpty.subtitle'), cta: t('pages.library.emptyState.filteredEmpty.cta') },
    error: { title: t('pages.library.emptyState.error.title'), subtitle: t('pages.library.emptyState.error.subtitle'), cta: t('pages.library.emptyState.error.cta') },
  }), [t]);

  const bulkLabels = useMemo<BulkSelectionBarLabels>(() => {
    const count = selected.size;
    return {
      regionLabel: t('pages.library.selectionMode.selectedCount', { count }),
      counter: t('pages.library.selectionMode.selectedCount', { count }),
      cancel: t('pages.library.selectionMode.exit'),
      archive: t('pages.library.bulk.actions.delete'),
      confirmTitle: t('pages.library.bulk.confirm.deleteTitle', { count }),
      confirmDescription: t('pages.library.bulk.confirm.deleteMessage'),
      confirmCta: t('pages.library.bulk.confirm.confirmCta'),
      cancelCta: t('pages.library.bulk.confirm.cancelCta'),
    };
  }, [t, selected]);

  // ─── FSM (partial-failure aware) ───
  const realKind = useMemo<SurfaceKind>(() => {
    if (hub.allFailed) return 'error';
    if (hub.isLoading) return 'loading';
    const totalAll = Object.values(hub.totalCounts).reduce((a, b) => a + b, 0);
    if (totalAll === 0) return 'empty';
    if (merged.length === 0) return 'filtered-empty';
    return 'default';
  }, [hub.allFailed, hub.isLoading, hub.totalCounts, merged.length]);

  const effectiveKind: SurfaceKind = stateOverride ?? realKind;

  // ─── Callbacks ───
  const handleAddGame = useCallback(() => router.push('/library?action=add'), [router]);

  const handleCardClick = useCallback((itemId: string) => {
    if (selectionMode === 'select') {
      setSelected(prev => { const n = new Set(prev); n.has(itemId) ? n.delete(itemId) : n.add(itemId); return n; });
      return;
    }
    const item = merged.find(i => i.id === itemId);
    if (item) router.push(item.href);
  }, [router, selectionMode, merged]);

  const handleEnterSelectMode = useCallback((itemId?: string) => {
    if (tab !== 'games') return; // game-scoped guard
    setSelectionMode('select');
    if (itemId) setSelected(prev => { const n = new Set(prev); n.add(itemId); return n; });
  }, [tab]);

  const handleExitSelectMode = useCallback(() => { setSelectionMode('browse'); setSelected(new Set()); }, []);

  const handleBulkDelete = useCallback(async () => {
    const ids = Array.from(selected);
    if (ids.length === 0) return;
    // ids are HybridHubItem ids in the games tab; the games source id IS the library entry id.
    await Promise.allSettled(ids.map(id => removeMutation.mutateAsync(id)));
    setSelected(new Set());
    setSelectionMode('browse');
  }, [selected, removeMutation]);

  const handleRetry = useCallback(() => { /* per-source refetch handled by TanStack; no-op surfaces retry CTA */ }, []);

  const handleClearFilters = useCallback(() => {
    setQuery('');
    setTab('all');
    setGameStateFilter({ states: [], withKb: false });
    if (stateOverride != null) router.push(pathname);
  }, [stateOverride, router, pathname]);

  // ─── MiniNav ───
  const miniNavConfig = useMemo(() => ({
    breadcrumb: 'Libreria · Hub',
    tabs: [
      { id: 'hub', label: 'Hub', href: '/library' },
      { id: 'wishlist', label: 'Wishlist', href: '/library/wishlist', count: 0 },
    ],
    activeTabId: 'hub',
    primaryAction: { label: t('pages.library.hero.cta.add'), icon: '＋', onClick: handleAddGame },
  }), [t, handleAddGame]);
  useMiniNavConfig(miniNavConfig);

  // ─── Render ───
  return (
    <div data-slot="library-hub-v2" data-state={effectiveKind} className="mx-auto flex max-w-[1440px] flex-col gap-6 p-6 pb-24 sm:p-7">
      <LibraryHeroDesktop labels={heroLabels} stats={heroStatRows} onAddGame={handleAddGame} />
      <div className="flex flex-col gap-4 lg:flex-row lg:items-start">
        <div className="flex flex-1 flex-col gap-4">
          <LibraryTabs<HybridHubTab> tabs={tabsConfig} active={tab} onChange={setTab} />
          <CrossEntityFilters tab={tab} gameStateFilter={gameStateFilter} onGameStateFilterChange={setGameStateFilter} />
          <div data-slot="library-toolbar" className="flex flex-wrap items-center gap-3 rounded-2xl border border-border bg-card p-3 shadow-sm">
            <input type="search" value={query} onChange={e => setQuery(e.target.value)}
              placeholder={t('pages.library.filters.search.placeholder')} aria-label={t('pages.library.filters.search.ariaLabel')}
              data-slot="library-search-input" className="min-w-[12rem] flex-1 rounded-full border border-input bg-background px-4 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" />
            <label className="flex items-center gap-2 text-sm text-muted-foreground">
              <span className="sr-only sm:not-sr-only">{t('pages.library.sort.label')}</span>
              <select value={sortKey} onChange={e => setSortKey(e.target.value as LibrarySortKey)}
                aria-label={t('pages.library.sort.ariaLabel')} data-slot="library-sort-select"
                className="rounded-full border border-input bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
                <option value="recent">{t('pages.library.sort.recent')}</option>
                <option value="title">{t('pages.library.sort.title')}</option>
                <option value="rating">{t('pages.library.sort.rating')}</option>
                <option value="state">{t('pages.library.sort.state')}</option>
              </select>
            </label>
            <div role="group" aria-label={t('pages.library.view.ariaLabel')} data-slot="library-view-toggle"
              className="inline-flex items-center gap-1 rounded-full border border-input bg-background p-1">
              {(['grid', 'list', 'compact'] as const).map(mode => (
                <button key={mode} type="button" onClick={() => setView(mode)} aria-pressed={view === mode} data-view-mode={mode}
                  className={`rounded-full px-3 py-1 text-xs font-medium transition-colors ${view === mode ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground'}`}>
                  {t(`pages.library.view.${mode}` as const)}
                </button>
              ))}
            </div>
            {tab === 'games' && selectionMode === 'browse' ? (
              <button type="button" onClick={() => handleEnterSelectMode()} aria-label={t('pages.library.selectionMode.enterAriaLabel')}
                data-slot="library-enter-select-mode" className="ml-auto rounded-full border border-input bg-background px-4 py-2 text-sm font-medium hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
                {t('pages.library.selectionMode.enter')}
              </button>
            ) : null}
          </div>
          {effectiveKind === 'default' ? (
            <LibraryHybridGrid items={merged} view={view as LibraryViewMode} selectionMode={selectionMode}
              selected={selected} onCardClick={handleCardClick} onLongPressEnter={handleEnterSelectMode} />
          ) : (
            <EmptyLibrary kind={effectiveKind} labels={emptyLabels} onAddGame={handleAddGame} onClearFilters={handleClearFilters} onRetry={handleRetry} />
          )}
        </div>
        <RecentActivityRail items={activityItems} />
      </div>
      {tab === 'games' && selectionMode === 'select' ? (
        <BulkSelectionBar selectedCount={selected.size} labels={bulkLabels} onExitSelectMode={handleExitSelectMode}
          onArchive={handleBulkDelete} disabled={selected.size === 0 || removeMutation.isPending} />
      ) : null}
    </div>
  );
}
```

Note: `isKbEntry` import is retained for a possible future use in the games with-KB filter; if ESLint flags it unused in 2a (the with-KB filter is stubbed per the comment), **remove the import** rather than suppressing — YAGNI. The implementer decides based on the lint result.

#### 4d. i18n keys for the new hub tab labels + hero stats

Add to `it.json` + `en.json` under `pages.library`:
- `hubTabs.all` / `.games` / `.agents` / `.kb` / `.sessions` / `.chat` (it: "Tutti"/"Giochi"/"Agenti"/"KB"/"Sessioni"/"Chat"; en: "All"/"Games"/"Agents"/"KB"/"Sessions"/"Chat")
- `hero.stats.agents` / `.docs` / `.chats` (it: "Agenti"/"Documenti"/"Chat"; en: "Agents"/"Documents"/"Chats") — reuses existing `hero.stats.totalGames` for the first chip key

(The hero stat `key` values stay `totalGames|kbReady|wishlist|loaned` because `LibraryHeroStatKey` is typed to those literals; only the **labels** + **values** change to agents/docs/chats. Do NOT change `LibraryHeroStatKey` in 2a — that's cosmetic-key churn; the visible label comes from `label`, not `key`.)

#### 4e. `LibraryHub.test.tsx` rewrite

Rewrite the test suite. **Preserve** these behaviors (adapt fixtures to hybrid + `useHybridHubItems` mock): FSM 5-state, `?state=` overrides, card-click navigate (now `item.href`), select-mode toggle (now only in games tab), bulk delete fan-out, hero CTA, clearFilters, mini-nav. **Remove** the `loaned`-related assertions. **Add**: 6-tab render, partial-failure (1 source errors → grid still renders + no error surface), selection-mode forced browse outside games.

Mock `useHybridHubItems` instead of `useLibrary`:
```tsx
vi.mock('@/hooks/queries/useHybridHubItems', () => ({
  useHybridHubItems: () => mockHub(),
  PER_SOURCE_CAP: 20,
}));
```
where `mockHub` returns `{ sources, isLoading, allFailed, partialErrors, totalCounts }`. Provide a `makeHub(overrides)` factory. Keep `useLibraryActivity` + `useRemoveGameFromLibrary` mocks as today.

Key new tests:
```tsx
it('renders 6 hub tabs (all/games/agents/kb/sessions/chat)', () => {
  renderHub(makeHub({ /* games:2, sessions:1, chat:1 */ }));
  const tabs = screen.getAllByRole('tab');
  expect(tabs.map(t => t.getAttribute('data-tab-key'))).toEqual(['all','games','agents','kb','sessions','chat']);
});

it('partial failure: sessions source errors but games+chat still render (no error surface)', () => {
  renderHub(makeHub({ partialErrors: { sessions: new Error('x'), games: null, chat: null, agents: null, kb: null }, allFailed: false }));
  expect(screen.getByTestId('library-hub-v2')).toHaveAttribute('data-state', 'default');
});

it('all sources fail → error surface', () => {
  renderHub(makeHub({ allFailed: true }));
  expect(screen.getByTestId('library-hub-v2')).toHaveAttribute('data-state', 'error');
});

it('select mode is forced to browse when switching away from games tab', async () => {
  const user = userEvent.setup();
  renderHub(makeHub({ /* games present */ }));
  // enter games tab, enter select mode, switch to sessions → BulkSelectionBar gone
  await user.click(screen.getByRole('tab', { name: /giochi/i }));
  await user.click(screen.getByTestId('library-enter-select-mode'));
  await user.click(screen.getByRole('tab', { name: /sessioni/i }));
  expect(screen.queryByTestId('library-bulk-selection-bar')).not.toBeInTheDocument();
});
```

#### Step-by-step for Task 4

- [ ] **Step 1: Rewrite `LibraryHybridGrid.tsx` (4a) + its test (4b)**
- [ ] **Step 2: Add i18n keys (4d) to it.json + en.json**
- [ ] **Step 3: Rewrite `LibraryHub.tsx` (4c)**
- [ ] **Step 4: Rewrite `LibraryHub.test.tsx` (4e)**
- [ ] **Step 5: Run the FULL library suite + typecheck + lint**

Run:
```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web
pnpm exec tsc --noEmit
pnpm exec vitest run LibraryHub LibraryHybridGrid LibraryTabs CrossEntityFilters useHybridHubItems library-filters
pnpm exec eslint src/app/(authenticated)/library src/components/features/library src/hooks/queries/useHybridHubItems.ts
```
Expected: typecheck clean; all suites green (library-filters unchanged = its 28 tests still pass); eslint clean.

- [ ] **Step 6: Commit (the atomic flip)**

```bash
cd /d/Repositories/meepleai-monorepo-frontend && git add apps/web/src/components/features/library/LibraryHybridGrid.tsx apps/web/src/components/features/library/__tests__/LibraryHybridGrid.test.tsx apps/web/src/app/\(authenticated\)/library/_components/LibraryHub.tsx apps/web/src/app/\(authenticated\)/library/_components/__tests__/LibraryHub.test.tsx apps/web/src/locales/it.json apps/web/src/locales/en.json
git commit -m "feat(library): #1605 flip LibraryHub to hybrid hub orchestration (Phase 2a)"
```

---

## Acceptance check for Phase 2a

After Task 4, verify against #1605 inline AC:
- **AC3** — Hero shows badge + subtitle + 4 stat chips (games/agents/docs/chats) + Add CTA → ✅ Task 4 (agents/docs at 0 in 2a)
- **AC4** — `CrossEntityFilters` STATO chip functional in games tab → ✅ Task 2 + wired Task 4
- **AC6** — 5-state FSM preserved → ✅ Task 4 (partial-failure-aware)
- **AC9** — bulk-selection only in games tab → ✅ Task 4 (useEffect guard + conditional mount)
- **AC9.1** — partial-failure: 1 source fails, others render → ✅ Task 1 (hook) + Task 4 (FSM)

Final full check:
```bash
cd /d/Repositories/meepleai-monorepo-frontend/apps/web
pnpm exec tsc --noEmit
pnpm exec vitest run library
pnpm exec eslint src/app/(authenticated)/library src/components/features/library src/hooks/queries
```

---

## Out of scope (Phase 2b #1592)

- `useUserKbDocs` (BE-1 #1588) → kb tab content
- `useAgents({ scope: 'my-library' })` (BE-2 #1589) → agents tab content
- with-KB game filter actual predicate (needs `GameHubItem.hasKb` enrichment)
- `AdvancedFiltersDrawer` wire-in (Phase 3b #1593) — the "Più filtri" entry point
- `RecentActivityRail` cross-entity population (Phase 3b, BE-3 #1590)
