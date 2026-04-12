# S4 — Game Page Desktop (Split + Vertical Rail + 5 Tabs) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:executing-plans`. TDD steps with checkbox syntax.

**Goal:** Redesign the game detail page at `/library/games/[gameId]` to use `SplitViewLayout` with a `MeepleCard variant="hero"` on the left and a new `GameTabsPanel` (vertical rail + 5 tabs) on the right. Establishes the shared tab component contract (§4.4.1) used by S5.

**Architecture:** Frontend-only change. Deprecates `GameTableLayout` and its zone components. Reuses `useLibraryGameDetail` hook and the existing `GetUserPlayHistoryQuery` (which already supports `GameId?` filter — no backend work).

**Tech Stack:** Next.js 16 App Router, React 19, Tailwind, Vitest, Playwright.

**Reference spec:** `docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md` §4.4
**Branch:** `feature/s4-game-desktop` (from `epic/library-to-game`)

## Scope corrections from the spec

Three spec claims were invalidated by codebase exploration and are corrected here:

1. **`SplitViewLayout` is NOT resizable.** The canonical file at `apps/web/src/components/layout/SplitViewLayout/SplitViewLayout.tsx` uses preset `listRatio` values (`narrow`/`balanced`/`wide`) mapped to Tailwind width classes. No localStorage, no drag-to-resize. S4 uses `listRatio="wide"` (left=1/2, right=1/2) to approximate the 50/50 spec without adding resize logic. Deferred to a future polish PR if users request.
2. **No new backend query needed for the Partite tab.** `GetUserPlayHistoryQuery` at `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/PlayRecords/GetUserPlayHistoryQuery.cs` already exposes a `GameId? = null` parameter. The Partite tab passes the current `gameId` to filter play records for the current game. S4 is **frontend-only**.
3. **No `MeepleCardHero` wrapper needed.** `MeepleCard variant="hero"` is already exported via `HeroCard` variant. Use directly.

---

## File Structure

**New files:**
- `apps/web/src/components/game-detail/tabs/types.ts` — Shared tab component contract (§4.4.1)
- `apps/web/src/components/game-detail/tabs/GameInfoTab.tsx`
- `apps/web/src/components/game-detail/tabs/GameAiChatTab.tsx`
- `apps/web/src/components/game-detail/tabs/GameToolboxTab.tsx`
- `apps/web/src/components/game-detail/tabs/GameHouseRulesTab.tsx`
- `apps/web/src/components/game-detail/tabs/GamePartiteTab.tsx`
- `apps/web/src/components/game-detail/tabs/index.ts` — barrel export
- `apps/web/src/components/game-detail/GameTabsPanel.tsx` — vertical rail + content area wrapper
- `apps/web/src/components/game-detail/GameDetailDesktop.tsx` — top-level desktop wrapper using SplitViewLayout
- `apps/web/src/components/game-detail/__tests__/GameTabsPanel.test.tsx`
- `apps/web/src/components/game-detail/__tests__/GameDetailDesktop.test.tsx`
- `apps/web/src/components/game-detail/tabs/__tests__/GameInfoTab.test.tsx` (minimal smoke)

**Modified files:**
- `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx` — replaces `GameTableLayout` path with `GameDetailDesktop`
- `apps/web/src/app/(authenticated)/library/games/[gameId]/agent/page.tsx` — server-side redirect to `?tab=aiChat`
- `apps/web/src/app/(authenticated)/library/games/[gameId]/toolbox/page.tsx` — server-side redirect to `?tab=toolbox`

**Deleted files** (deprecation):
- None in S4 — `GameTableLayout` + zone components stay in the codebase during the epic. A follow-up cleanup PR removes them after all epic branches land. This keeps S4 scope small and avoids cascading test failures in components still referencing zones.

**Total estimate:** ~9 new + 3 modified frontend files, ~600 LOC production + ~300 LOC tests.

---

## Task 1 — Shared tab component contract (§4.4.1)

**Files:**
- Create: `apps/web/src/components/game-detail/tabs/types.ts`

- [ ] **Step 1.1: Write the types file**

  ```typescript
  /**
   * Shared tab component contract for the library-to-game epic.
   *
   * Both S4 (desktop `GameTabsPanel`) and S5 (mobile `GameDetailsDrawer`)
   * import the same 5 tab components and pass `variant` to control layout.
   * This avoids duplication and keeps the two viewports in sync.
   *
   * Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.4.1
   */

  export type GameTabId = 'info' | 'aiChat' | 'toolbox' | 'houseRules' | 'partite';

  export type GameTabVariant = 'desktop' | 'mobile';

  /**
   * Props shared by all game detail tab components.
   *
   * Contract:
   * - Each tab owns its data fetching via React Query (no data props).
   * - Each tab must render correctly in both `desktop` and `mobile` containers.
   * - No `position: sticky` on direct children; the container manages scroll.
   * - Each tab root exposes `role="tabpanel"` and `aria-labelledby={tabId}`.
   * - Each tab wraps its content in a local `<ErrorBoundary>` with retry.
   */
  export interface GameTabProps {
    /** The SharedGameId OR PrivateGameId for which to render content. */
    gameId: string;

    /** Layout hint — affects padding, font scale, touch targets. */
    variant: GameTabVariant;

    /** True if gameId refers to a private game upload (not a shared catalog entry). */
    isPrivateGame?: boolean;

    /** True if the user does NOT have this game in their library. Locks non-Info tabs with a CTA. */
    isNotInLibrary?: boolean;
  }

  /**
   * Metadata for a single tab in the rail.
   */
  export interface GameTabDescriptor {
    id: GameTabId;
    label: string;
    icon: string; // emoji for now, lucide-react icon in a follow-up
  }

  export const GAME_TABS: readonly GameTabDescriptor[] = [
    { id: 'info', label: 'Info', icon: '📖' },
    { id: 'aiChat', label: 'AI Chat', icon: '🤖' },
    { id: 'toolbox', label: 'Toolbox', icon: '🧰' },
    { id: 'houseRules', label: 'House Rules', icon: '🏠' },
    { id: 'partite', label: 'Partite', icon: '🎲' },
  ] as const;
  ```

- [ ] **Step 1.2: Commit**

  ```bash
  git add apps/web/src/components/game-detail/tabs/types.ts
  git commit -m "feat(s4): add shared tab component contract for game detail"
  ```

---

## Task 2 — Five tab components (minimal implementations)

Each tab is a thin wrapper around either an existing component (AI Chat, Toolbox) or an inline placeholder (House Rules, Partite — can be expanded in follow-ups). The key goal is establishing the contract and making sure S5 can reuse them.

**Files:**
- Create: `apps/web/src/components/game-detail/tabs/GameInfoTab.tsx`
- Create: `apps/web/src/components/game-detail/tabs/GameAiChatTab.tsx`
- Create: `apps/web/src/components/game-detail/tabs/GameToolboxTab.tsx`
- Create: `apps/web/src/components/game-detail/tabs/GameHouseRulesTab.tsx`
- Create: `apps/web/src/components/game-detail/tabs/GamePartiteTab.tsx`
- Create: `apps/web/src/components/game-detail/tabs/index.ts`

- [ ] **Step 2.1: Write `GameInfoTab`**

  ```typescript
  'use client';

  import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
  import { cn } from '@/lib/utils';

  import type { GameTabProps } from './types';

  export function GameInfoTab({ gameId, variant, isNotInLibrary }: GameTabProps) {
    const { data: game, isLoading, isError } = useLibraryGameDetail(gameId, !isNotInLibrary);

    if (isNotInLibrary) {
      return (
        <div
          role="tabpanel"
          aria-labelledby="game-tab-info"
          className={cn('flex flex-col gap-3', variant === 'desktop' ? 'p-6' : 'p-4')}
        >
          <p className="text-sm text-muted-foreground">
            Aggiungi questo gioco alla tua libreria per vedere tutti i dettagli.
          </p>
        </div>
      );
    }

    if (isLoading) return <div className="p-4 text-sm text-muted-foreground">Caricamento…</div>;
    if (isError || !game) return <div className="p-4 text-sm text-destructive">Errore nel caricamento.</div>;

    return (
      <div
        role="tabpanel"
        aria-labelledby="game-tab-info"
        className={cn(
          'flex flex-col',
          variant === 'desktop' ? 'gap-4 p-6' : 'gap-3 p-4'
        )}
      >
        <h3 className={cn('font-heading font-bold', variant === 'desktop' ? 'text-lg' : 'text-base')}>
          Informazioni
        </h3>
        <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-2 text-sm">
          {game.gamePublisher && (
            <>
              <dt className="text-muted-foreground">Editore</dt>
              <dd className="font-medium">{game.gamePublisher}</dd>
            </>
          )}
          {game.gameYearPublished && (
            <>
              <dt className="text-muted-foreground">Anno</dt>
              <dd className="font-medium">{game.gameYearPublished}</dd>
            </>
          )}
          {(game.minPlayers || game.maxPlayers) && (
            <>
              <dt className="text-muted-foreground">Giocatori</dt>
              <dd className="font-medium">
                {game.minPlayers === game.maxPlayers ? game.minPlayers : `${game.minPlayers}–${game.maxPlayers}`}
              </dd>
            </>
          )}
          {game.addedAt && (
            <>
              <dt className="text-muted-foreground">In libreria dal</dt>
              <dd className="font-medium">{new Date(game.addedAt).toLocaleDateString('it-IT')}</dd>
            </>
          )}
        </dl>
        {game.description && (
          <p className="text-sm text-muted-foreground leading-relaxed whitespace-pre-wrap">
            {game.description}
          </p>
        )}
      </div>
    );
  }
  ```

- [ ] **Step 2.2: Write `GameAiChatTab` (placeholder linking to existing chat)**

  ```typescript
  'use client';

  import Link from 'next/link';

  import { Button } from '@/components/ui/primitives/button';
  import { cn } from '@/lib/utils';

  import type { GameTabProps } from './types';

  export function GameAiChatTab({ gameId, variant, isNotInLibrary }: GameTabProps) {
    if (isNotInLibrary) {
      return (
        <div
          role="tabpanel"
          aria-labelledby="game-tab-aiChat"
          className={cn('flex flex-col gap-3', variant === 'desktop' ? 'p-6' : 'p-4')}
        >
          <p className="text-sm text-muted-foreground">
            Aggiungi il gioco alla libreria per chattare con l'AI.
          </p>
        </div>
      );
    }

    return (
      <div
        role="tabpanel"
        aria-labelledby="game-tab-aiChat"
        className={cn(
          'flex flex-col gap-4',
          variant === 'desktop' ? 'p-6' : 'p-4'
        )}
      >
        <h3 className={cn('font-heading font-bold', variant === 'desktop' ? 'text-lg' : 'text-base')}>
          AI Chat
        </h3>
        <p className="text-sm text-muted-foreground">
          Chiedi qualsiasi cosa sulle regole di questo gioco. L'AI ti risponderà in base alla knowledge base.
        </p>
        <Link href={`/library/games/${gameId}/agent`}>
          <Button size={variant === 'mobile' ? 'sm' : 'default'}>Apri chat completa</Button>
        </Link>
      </div>
    );
  }
  ```

- [ ] **Step 2.3: Write `GameToolboxTab` (placeholder linking to existing toolbox)**

  Same pattern as AiChat: handles isNotInLibrary, renders heading + description + "Apri Toolbox completo" button linking to `/library/games/${gameId}/toolbox`. (Note: the old routes will be redirected via `redirect()` calls in S4 Task 6, but the Link is still useful for now because the redirect points back to the tab.)

  Actually to avoid a redirect loop, the "Apri Toolbox completo" button should NOT exist if we're redirecting that route. Replace with a placeholder: *"Toolbox funzionality available in the tab. Full-screen view coming in a future release."*

  ```typescript
  'use client';

  import { cn } from '@/lib/utils';

  import type { GameTabProps } from './types';

  export function GameToolboxTab({ gameId, variant, isNotInLibrary }: GameTabProps) {
    if (isNotInLibrary) {
      return (
        <div
          role="tabpanel"
          aria-labelledby="game-tab-toolbox"
          className={cn('flex flex-col gap-3', variant === 'desktop' ? 'p-6' : 'p-4')}
        >
          <p className="text-sm text-muted-foreground">
            Aggiungi il gioco alla libreria per usare il toolbox.
          </p>
        </div>
      );
    }

    return (
      <div
        role="tabpanel"
        aria-labelledby="game-tab-toolbox"
        className={cn(
          'flex flex-col gap-4',
          variant === 'desktop' ? 'p-6' : 'p-4'
        )}
      >
        <h3 className={cn('font-heading font-bold', variant === 'desktop' ? 'text-lg' : 'text-base')}>
          Toolbox
        </h3>
        <p className="text-sm text-muted-foreground">
          Strumenti rapidi per il gioco: dadi, timer, punteggi, note. In arrivo: integrazione completa del toolbox.
        </p>
        <div className="text-xs text-muted-foreground italic">gameId: {gameId}</div>
      </div>
    );
  }
  ```

- [ ] **Step 2.4: Write `GameHouseRulesTab` (placeholder)**

  ```typescript
  'use client';

  import { cn } from '@/lib/utils';

  import type { GameTabProps } from './types';

  export function GameHouseRulesTab({ gameId, variant, isNotInLibrary }: GameTabProps) {
    if (isNotInLibrary) {
      return (
        <div
          role="tabpanel"
          aria-labelledby="game-tab-houseRules"
          className={cn('flex flex-col gap-3', variant === 'desktop' ? 'p-6' : 'p-4')}
        >
          <p className="text-sm text-muted-foreground">
            Aggiungi il gioco alla libreria per gestire le regole della casa.
          </p>
        </div>
      );
    }

    return (
      <div
        role="tabpanel"
        aria-labelledby="game-tab-houseRules"
        className={cn(
          'flex flex-col gap-4',
          variant === 'desktop' ? 'p-6' : 'p-4'
        )}
      >
        <h3 className={cn('font-heading font-bold', variant === 'desktop' ? 'text-lg' : 'text-base')}>
          Regole della casa
        </h3>
        <p className="text-sm text-muted-foreground italic">
          Le regole personalizzate di questo gioco appariranno qui. In arrivo.
        </p>
      </div>
    );
  }
  ```

- [ ] **Step 2.5: Write `GamePartiteTab` (uses GetUserPlayHistoryQuery via existing hook)**

  First check if there's an existing React Query hook that wraps `GetUserPlayHistoryQuery` accepting a `gameId` filter. Look for:
  ```bash
  grep -rn "useUserPlayHistory\|usePlayHistory\|usePlayRecords" apps/web/src/hooks
  ```
  If a hook exists → wrap it. If not → defer the tab to a follow-up by rendering a placeholder: *"Cronologia partite — in arrivo"*.

  Minimal placeholder version (ship in S4):
  ```typescript
  'use client';

  import { cn } from '@/lib/utils';

  import type { GameTabProps } from './types';

  export function GamePartiteTab({ gameId, variant, isNotInLibrary }: GameTabProps) {
    if (isNotInLibrary) {
      return (
        <div
          role="tabpanel"
          aria-labelledby="game-tab-partite"
          className={cn('flex flex-col gap-3', variant === 'desktop' ? 'p-6' : 'p-4')}
        >
          <p className="text-sm text-muted-foreground">
            Aggiungi il gioco alla libreria per vedere lo storico delle partite.
          </p>
        </div>
      );
    }

    return (
      <div
        role="tabpanel"
        aria-labelledby="game-tab-partite"
        className={cn(
          'flex flex-col gap-4',
          variant === 'desktop' ? 'p-6' : 'p-4'
        )}
      >
        <h3 className={cn('font-heading font-bold', variant === 'desktop' ? 'text-lg' : 'text-base')}>
          Partite
        </h3>
        <p className="text-sm text-muted-foreground italic">
          Lo storico delle partite giocate per questo gioco apparirà qui. Integrazione con `GetUserPlayHistoryQuery` (gameId={gameId}) in arrivo.
        </p>
      </div>
    );
  }
  ```

- [ ] **Step 2.6: Write barrel export**

  ```typescript
  // apps/web/src/components/game-detail/tabs/index.ts
  export { GameInfoTab } from './GameInfoTab';
  export { GameAiChatTab } from './GameAiChatTab';
  export { GameToolboxTab } from './GameToolboxTab';
  export { GameHouseRulesTab } from './GameHouseRulesTab';
  export { GamePartiteTab } from './GamePartiteTab';
  export type { GameTabId, GameTabVariant, GameTabProps, GameTabDescriptor } from './types';
  export { GAME_TABS } from './types';
  ```

- [ ] **Step 2.7: Typecheck + commit**

  ```bash
  cd apps/web && pnpm typecheck
  cd D:/Repositories/meepleai-monorepo-backend
  git add apps/web/src/components/game-detail/tabs/
  git commit -m "feat(s4): add 5 game detail tab components with shared contract"
  ```

---

## Task 3 — `GameTabsPanel` (vertical rail + content area)

**Files:**
- Create: `apps/web/src/components/game-detail/GameTabsPanel.tsx`
- Create: `apps/web/src/components/game-detail/__tests__/GameTabsPanel.test.tsx`

- [ ] **Step 3.1: Write failing test**

  ```typescript
  // apps/web/src/components/game-detail/__tests__/GameTabsPanel.test.tsx
  import { render, screen, fireEvent } from '@testing-library/react';
  import { describe, it, expect, vi } from 'vitest';

  import { GameTabsPanel } from '../GameTabsPanel';

  vi.mock('@/hooks/queries/useLibrary', () => ({
    useLibraryGameDetail: () => ({ data: null, isLoading: false, isError: false }),
  }));

  describe('GameTabsPanel', () => {
    it('renders a tablist with 5 tabs', () => {
      render(<GameTabsPanel gameId="test-game-id" />);
      const tablist = screen.getByRole('tablist', { name: /dettagli gioco/i });
      expect(tablist).toBeInTheDocument();
      expect(screen.getAllByRole('tab')).toHaveLength(5);
    });

    it('defaults to Info tab as selected', () => {
      render(<GameTabsPanel gameId="test-game-id" />);
      const infoTab = screen.getByRole('tab', { name: /info/i });
      expect(infoTab).toHaveAttribute('aria-selected', 'true');
    });

    it('switches tabs when clicked', () => {
      render(<GameTabsPanel gameId="test-game-id" />);
      const aiChatTab = screen.getByRole('tab', { name: /ai chat/i });
      fireEvent.click(aiChatTab);
      expect(aiChatTab).toHaveAttribute('aria-selected', 'true');
    });

    it('accepts initialTab prop', () => {
      render(<GameTabsPanel gameId="test-game-id" initialTab="toolbox" />);
      expect(screen.getByRole('tab', { name: /toolbox/i })).toHaveAttribute('aria-selected', 'true');
    });

    it('calls onTabChange when tab switches', () => {
      const onTabChange = vi.fn();
      render(<GameTabsPanel gameId="test-game-id" onTabChange={onTabChange} />);
      fireEvent.click(screen.getByRole('tab', { name: /partite/i }));
      expect(onTabChange).toHaveBeenCalledWith('partite');
    });
  });
  ```

- [ ] **Step 3.2: Run test (red)**

  ```bash
  cd apps/web && pnpm vitest run src/components/game-detail/__tests__/GameTabsPanel.test.tsx
  ```

- [ ] **Step 3.3: Implement `GameTabsPanel`**

  ```typescript
  'use client';

  import { useState } from 'react';

  import { cn } from '@/lib/utils';

  import {
    GAME_TABS,
    GameAiChatTab,
    GameHouseRulesTab,
    GameInfoTab,
    GamePartiteTab,
    GameToolboxTab,
    type GameTabId,
  } from './tabs';

  interface GameTabsPanelProps {
    gameId: string;
    initialTab?: GameTabId;
    onTabChange?: (tab: GameTabId) => void;
    isPrivateGame?: boolean;
    isNotInLibrary?: boolean;
  }

  /**
   * Desktop right-panel of the game detail page.
   * Vertical rail on the left (74px) + scrollable content area on the right.
   *
   * Pattern: VSCode sidebar.
   */
  export function GameTabsPanel({
    gameId,
    initialTab = 'info',
    onTabChange,
    isPrivateGame,
    isNotInLibrary,
  }: GameTabsPanelProps) {
    const [activeTab, setActiveTab] = useState<GameTabId>(initialTab);

    const handleSelect = (tab: GameTabId) => {
      setActiveTab(tab);
      onTabChange?.(tab);
    };

    const tabProps = {
      gameId,
      variant: 'desktop' as const,
      isPrivateGame,
      isNotInLibrary,
    };

    return (
      <div className="flex h-full overflow-hidden rounded-2xl border border-border bg-card shadow-sm">
        {/* Vertical rail */}
        <div
          role="tablist"
          aria-orientation="vertical"
          aria-label="Dettagli gioco"
          className="flex w-[74px] flex-col gap-1 border-r border-border bg-muted/30 p-2"
        >
          {GAME_TABS.map(tab => {
            const isActive = tab.id === activeTab;
            return (
              <button
                key={tab.id}
                type="button"
                role="tab"
                id={`game-tab-${tab.id}`}
                aria-selected={isActive}
                aria-controls={`game-tabpanel-${tab.id}`}
                onClick={() => handleSelect(tab.id)}
                className={cn(
                  'flex flex-col items-center gap-1 rounded-lg border px-2 py-3 transition-colors',
                  'focus:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  isActive
                    ? 'border-primary/30 bg-primary/10 text-primary'
                    : 'border-transparent text-muted-foreground hover:bg-muted/60 hover:text-foreground'
                )}
                data-testid={`game-tab-${tab.id}`}
              >
                <span className="text-lg" aria-hidden="true">
                  {tab.icon}
                </span>
                <span className="text-[9px] font-bold uppercase tracking-wide">{tab.label}</span>
              </button>
            );
          })}
        </div>

        {/* Content area */}
        <div id={`game-tabpanel-${activeTab}`} className="flex-1 overflow-y-auto">
          {activeTab === 'info' && <GameInfoTab {...tabProps} />}
          {activeTab === 'aiChat' && <GameAiChatTab {...tabProps} />}
          {activeTab === 'toolbox' && <GameToolboxTab {...tabProps} />}
          {activeTab === 'houseRules' && <GameHouseRulesTab {...tabProps} />}
          {activeTab === 'partite' && <GamePartiteTab {...tabProps} />}
        </div>
      </div>
    );
  }
  ```

- [ ] **Step 3.4: Run test (green)**

  ```bash
  cd apps/web && pnpm vitest run src/components/game-detail/__tests__/GameTabsPanel.test.tsx
  ```
  Expected: 5 tests PASS.

- [ ] **Step 3.5: Commit**

  ```bash
  git add apps/web/src/components/game-detail/GameTabsPanel.tsx apps/web/src/components/game-detail/__tests__/GameTabsPanel.test.tsx
  git commit -m "feat(s4): add GameTabsPanel with vertical rail and 5 tabs"
  ```

---

## Task 4 — `GameDetailDesktop` wrapper

**Files:**
- Create: `apps/web/src/components/game-detail/GameDetailDesktop.tsx`
- Create: `apps/web/src/components/game-detail/__tests__/GameDetailDesktop.test.tsx`

- [ ] **Step 4.1: Write minimal test**

  ```typescript
  import { render, screen } from '@testing-library/react';
  import { describe, it, expect, vi } from 'vitest';

  import { GameDetailDesktop } from '../GameDetailDesktop';

  vi.mock('@/hooks/queries/useLibrary', () => ({
    useLibraryGameDetail: () => ({
      data: {
        gameId: 'test-id',
        gameTitle: 'Catan',
        gamePublisher: 'Kosmos',
        gameYearPublished: 1995,
        minPlayers: 3,
        maxPlayers: 4,
        playingTimeMinutes: 90,
        description: 'Build, trade, settle',
        gameImageUrl: null,
        averageRating: 7.2,
        addedAt: '2025-01-01T00:00:00Z',
        currentState: 'owned',
        isFavorite: false,
      },
      isLoading: false,
      isError: false,
    }),
  }));

  describe('GameDetailDesktop', () => {
    it('renders SplitViewLayout with MeepleCard hero and tabs panel', () => {
      render(<GameDetailDesktop gameId="test-id" />);
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByRole('tablist', { name: /dettagli gioco/i })).toBeInTheDocument();
    });

    it('renders loading state', () => {
      // Override mock for this test
      vi.doMock('@/hooks/queries/useLibrary', () => ({
        useLibraryGameDetail: () => ({ data: null, isLoading: true, isError: false }),
      }));
      // (Test assertion — may need re-import or use a different mock strategy)
    });
  });
  ```

- [ ] **Step 4.2: Implement `GameDetailDesktop`**

  ```typescript
  'use client';

  import { SplitViewLayout } from '@/components/layout/SplitViewLayout/SplitViewLayout';
  import { MeepleCard } from '@/components/ui/data-display/meeple-card/MeepleCard';
  import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';

  import { GameTabsPanel } from './GameTabsPanel';
  import type { GameTabId } from './tabs';

  interface GameDetailDesktopProps {
    gameId: string;
    initialTab?: GameTabId;
    onTabChange?: (tab: GameTabId) => void;
  }

  /**
   * Desktop variant of the game detail page.
   * Uses the existing SplitViewLayout with:
   *  - list (left):   MeepleCard hero showing the game's main card
   *  - detail (right): GameTabsPanel with 5 tabs (Info / AI Chat / Toolbox / House Rules / Partite)
   *
   * Note: SplitViewLayout is not resizable in the canonical implementation
   * (uses preset `listRatio`). listRatio="wide" gives a ~50/50 split.
   */
  export function GameDetailDesktop({ gameId, initialTab, onTabChange }: GameDetailDesktopProps) {
    const { data: game, isLoading, isError } = useLibraryGameDetail(gameId);

    if (isLoading) {
      return <div className="p-6 text-sm text-muted-foreground">Caricamento…</div>;
    }
    if (isError || !game) {
      return <div className="p-6 text-sm text-destructive">Impossibile caricare il gioco.</div>;
    }

    const heroCard = (
      <MeepleCard
        entity="game"
        variant="hero"
        title={game.gameTitle}
        subtitle={game.gamePublisher ?? undefined}
        imageUrl={game.gameImageUrl ?? undefined}
        rating={game.averageRating ?? undefined}
        metadata={[
          game.gameYearPublished ? { label: String(game.gameYearPublished) } : null,
          game.minPlayers && game.maxPlayers
            ? { label: `${game.minPlayers}-${game.maxPlayers} giocatori` }
            : null,
          game.playingTimeMinutes ? { label: `${game.playingTimeMinutes} min` } : null,
        ].filter((m): m is { label: string } => m !== null)}
      />
    );

    const tabsPanel = (
      <GameTabsPanel gameId={gameId} initialTab={initialTab} onTabChange={onTabChange} />
    );

    return (
      <SplitViewLayout
        list={heroCard}
        detail={tabsPanel}
        listRatio="wide"
        listLabel="Carta del gioco"
        detailLabel="Strumenti e informazioni"
      />
    );
  }
  ```

  Note: the exact `MeepleCard` prop shape needs verification against `MeepleCardProps` — especially the `metadata` field format. Read `apps/web/src/components/ui/data-display/meeple-card/types.ts` before writing this exact code; adapt as needed.

- [ ] **Step 4.3: Typecheck + test**

  ```bash
  cd apps/web && pnpm typecheck && pnpm vitest run src/components/game-detail/__tests__/GameDetailDesktop.test.tsx
  ```

- [ ] **Step 4.4: Commit**

  ```bash
  git add apps/web/src/components/game-detail/GameDetailDesktop.tsx apps/web/src/components/game-detail/__tests__/GameDetailDesktop.test.tsx
  git commit -m "feat(s4): add GameDetailDesktop wrapping SplitViewLayout"
  ```

---

## Task 5 — Wire the page route

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx`

- [ ] **Step 5.1: Read current page.tsx**

  It currently uses `GameTableLayout` + `GameDetailMobile`. Replace the desktop path (`lg+`) with `<GameDetailDesktop gameId={gameId} />`. Keep `GameDetailMobile` for mobile (S5 will replace it).

- [ ] **Step 5.2: Parse `?tab=` query param**

  Use `useSearchParams()` to extract `tab` and pass as `initialTab` prop. Validate against `GameTabId` type:

  ```typescript
  const searchParams = useSearchParams();
  const tabParam = searchParams?.get('tab');
  const validTabs: GameTabId[] = ['info', 'aiChat', 'toolbox', 'houseRules', 'partite'];
  const initialTab = tabParam && validTabs.includes(tabParam as GameTabId) ? (tabParam as GameTabId) : 'info';
  ```

- [ ] **Step 5.3: Sync active tab back to URL**

  Pass `onTabChange` that updates `?tab=` via `router.replace()`.

- [ ] **Step 5.4: Typecheck + commit**

  ```bash
  cd apps/web && pnpm typecheck
  cd D:/Repositories/meepleai-monorepo-backend
  git add "apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx"
  git commit -m "feat(s4): wire GameDetailDesktop into /library/games/[gameId] page"
  ```

---

## Task 6 — Server-side redirects for old routes

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/games/[gameId]/agent/page.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/games/[gameId]/toolbox/page.tsx`

- [ ] **Step 6.1: Replace agent/page.tsx body with server-side redirect**

  ```typescript
  import { redirect } from 'next/navigation';

  export default async function AgentRedirect({ params }: { params: Promise<{ gameId: string }> }) {
    const { gameId } = await params;
    redirect(`/library/games/${gameId}?tab=aiChat`);
  }
  ```

  (Verify `params` is a Promise in Next.js 16 — if not, destructure directly.)

- [ ] **Step 6.2: Same for toolbox/page.tsx**

  ```typescript
  import { redirect } from 'next/navigation';

  export default async function ToolboxRedirect({ params }: { params: Promise<{ gameId: string }> }) {
    const { gameId } = await params;
    redirect(`/library/games/${gameId}?tab=toolbox`);
  }
  ```

  **Do NOT touch** `toolkit/page.tsx` — it's a different feature.

- [ ] **Step 6.3: Typecheck + commit**

  ```bash
  cd apps/web && pnpm typecheck
  cd D:/Repositories/meepleai-monorepo-backend
  git add "apps/web/src/app/(authenticated)/library/games/[gameId]/agent/page.tsx" \
          "apps/web/src/app/(authenticated)/library/games/[gameId]/toolbox/page.tsx"
  git commit -m "feat(s4): server-side redirect old /agent and /toolbox routes to tab params"
  ```

---

## Task 7 — Regression test for old GameTableLayout tests

The existing tests under `src/components/library/game-table/__tests__/` still reference zone components. They will keep passing as long as we don't delete the zone files. Verify no test breakage:

```bash
cd apps/web && pnpm vitest run src/components/library/game-table 2>&1 | tail -20
```

If all pass → no action. If any fail due to refactors elsewhere → investigate and fix in place.

---

## Task 8 — Final validation + PR

- [ ] **Step 8.1: Full typecheck + lint**

  ```bash
  cd apps/web && pnpm typecheck && pnpm lint
  ```

- [ ] **Step 8.2: Run S4 tests + regression**

  ```bash
  pnpm vitest run src/components/game-detail src/components/library/game-table "src/app/(authenticated)/library/games"
  ```

- [ ] **Step 8.3: Push + PR**

  ```bash
  git push -u origin feature/s4-game-desktop
  gh pr create --base epic/library-to-game --head feature/s4-game-desktop \
    --title "feat(s4): game page desktop redesign (split + rail + 5 tabs)" \
    --body-file <(printf '%s\n' '## Summary' \
      '' \
      'Implements S4 of the library-to-game epic: ...')
  ```

---

## Scope corrections from the spec (summary)

- `SplitViewLayout` is NOT resizable → use `listRatio="wide"`
- `GetUserPlayHistoryQuery` already supports GameId filter → no new backend query
- `MeepleCard variant="hero"` exists → no new wrapper
- `GameTableLayout` + zones are NOT deleted in S4 → follow-up cleanup PR
- Partite tab ships as placeholder (integration with PlayRecords deferred)

## Known limitations of the S4 ship

- Partite tab is a placeholder until a dedicated follow-up wires `GetUserPlayHistoryQuery` via a new React Query hook.
- House Rules tab is a placeholder (no existing component to wrap).
- Toolbox tab is a placeholder because the old `/toolbox` route is being redirected (breaking the "Open full toolbox" link); the actual toolbox components will be wrapped in a follow-up.
- Desktop layout uses `listRatio="wide"` (no resize). Polish/resize is deferred.

These shortcuts keep S4 frontend-only and shippable. The shared tab contract (§4.4.1) is the key deliverable that unblocks S5.
