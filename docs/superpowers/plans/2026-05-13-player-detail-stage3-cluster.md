# player-detail Stage 3 cluster Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor `PlayerDetailView` to adopt the `DetailPageLayout` composer (from PR #1112) and add a 6-pip ConnectionBar + 4 Tabs (`Sessions/Games/Toolkits/Achievements`) matching the `sp4-player-detail.jsx` mockup, while preserving Wave 3 components and existing subroutes.

**Architecture:** Route-private wrapper components live alongside `PlayerDetailView.tsx`. Wave 3 component primitives (`PlayerHero`, `PlayerStatsGrid`, `PlayerLeaderboardCard`, `FavoriteAgentCard`, `AchievementBadgeGrid`) are consumed unchanged. Tab state is read from `?tab=<key>` URL search param. ConnectionBar pips 3-6 (`event/agent/toolkit/chat`) render with `isEmpty: true` until backend exposes the counts.

**Tech Stack:** React 19, TypeScript, Tailwind 4, `clsx`, `lucide-react`. Vitest + React Testing Library for unit tests. Playwright for visual conformity. `useTablistKeyboardNav` hook for keyboard navigation.

**Spec:** `docs/superpowers/specs/2026-05-13-player-detail-stage3-cluster-design.md`

**Branch (already created):** `feature/issue-1113-stage3-player-detail-cluster` (parent: `main-dev`)

---

## File Structure

| File | Responsibility |
|---|---|
| `_components/PlayerTabs.tsx` | 4-tab tablist for player-detail (custom because `ui/detail-layout/tabs.tsx` is locked to shared-games keys) |
| `_components/PlayerConnectionBar.tsx` | Translates `PlayerProfileFixture` → 6 `ConnectionPip[]` and renders via canonical `ConnectionBar` primitive |
| `_components/PlayerOverviewRegion.tsx` | Composes `PlayerStatsGrid` + `PlayerLeaderboardCard` + `FavoriteAgentCard` in responsive layout |
| `_components/SessionsTabPanel.tsx` | Tab content for "Sessions" — gamePlayCounts ranked + CTA to `/players/[id]/sessions` |
| `_components/GamesTabPanel.tsx` | Tab content for "Games" — gamePlayCounts as MeepleCard grid + CTA to `/players/[id]/games` |
| `_components/ToolkitsTabPanel.tsx` | Tab content for "Toolkits" — placeholder empty state (backend not yet ready) |
| `_components/PlayerDetailView.tsx` | UPDATED — wrap default shell in `<DetailPageLayout>`; read tab from URL; replace flat layout with slot composition |
| `_components/__tests__/PlayerTabs.test.tsx` | Vitest unit tests for tab rendering, ARIA, keyboard nav |
| `_components/__tests__/PlayerConnectionBar.test.tsx` | Vitest unit tests for 6-pip layout, isEmpty fallbacks, aria-labels |
| `_components/__tests__/PlayerOverviewRegion.test.tsx` | Vitest unit tests for 3-card composition |
| `_components/__tests__/SessionsTabPanel.test.tsx` | Vitest unit tests for ranked list + CTA |
| `_components/__tests__/GamesTabPanel.test.tsx` | Vitest unit tests for grid + empty state |
| `_components/__tests__/ToolkitsTabPanel.test.tsx` | Vitest unit tests for placeholder |
| `_components/__tests__/PlayerDetailView.test.tsx` | UPDATED — extended for tab URL state + DetailPageLayout wrapping |
| `apps/web/e2e/visual-conformity/player-detail.spec.ts` | Playwright visual regression spec |
| `apps/web/e2e/state-coverage/state-matrix.json` | UPDATED — add `sp4-player-detail` entry |
| `docs/for-developers/specs/2026-05-11-design-system-deversioning.md` | UPDATED — §4 composability row for `/players/[id]` |

All `_components/...` paths are under `apps/web/src/app/(authenticated)/players/[id]/_components/`. The folder already exists from Wave 3 (PR #724).

---

## Preamble — Commit the plan itself

The spec was already committed in commit `4f72dc4bf` on the branch `feature/issue-1113-stage3-player-detail-cluster`. The plan you are reading needs to be committed too, before the implementation tasks start.

```bash
git add docs/superpowers/plans/2026-05-13-player-detail-stage3-cluster.md
git commit -m "docs(plans): player-detail Stage 3 cluster implementation plan (refs #1113)"
```

---

## Task 1 — PlayerTabs component

**Files:**
- Create: `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerTabs.tsx`
- Create: `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerTabs.test.tsx`

The canonical `Tabs` primitive at `apps/web/src/components/ui/detail-layout/tabs.tsx` has `TAB_KEYS` hardcoded to the shared-games detail tabs (`overview/toolkits/agents/knowledge/community`). Adopting it directly would force player-detail keys into that union. Instead, build a route-private `PlayerTabs` that reuses the generic `useTablistKeyboardNav` hook (already parameterized as `<T extends string>`) but defines its own key union.

- [ ] **Step 1.1: Write the failing test file**

Path: `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerTabs.test.tsx`

```tsx
/**
 * PlayerTabs unit tests — Stage 3 cluster (Issue #1113).
 *
 * Verifies ARIA tablist semantics, count rendering, and keyboard nav
 * delegation to the underlying useTablistKeyboardNav hook.
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { PlayerTabs, type PlayerTabKey } from '../PlayerTabs';

const labels = {
  tablistAriaLabel: 'Player sections',
  sessions: 'Partite',
  games: 'Giochi',
  toolkits: 'Toolkit',
  achievements: 'Achievement',
};

const counts: Record<PlayerTabKey, number> = {
  sessions: 23,
  games: 5,
  toolkits: 1,
  achievements: 4,
};

describe('PlayerTabs', () => {
  it('renders four tabs in canonical order with labels and counts', () => {
    render(<PlayerTabs activeTab="sessions" onChange={vi.fn()} counts={counts} labels={labels} />);

    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(4);
    expect(tabs[0]).toHaveTextContent('Partite');
    expect(tabs[0]).toHaveTextContent('23');
    expect(tabs[1]).toHaveTextContent('Giochi');
    expect(tabs[1]).toHaveTextContent('5');
    expect(tabs[2]).toHaveTextContent('Toolkit');
    expect(tabs[3]).toHaveTextContent('Achievement');
  });

  it('marks the active tab with aria-selected="true" and others with false', () => {
    render(<PlayerTabs activeTab="games" onChange={vi.fn()} counts={counts} labels={labels} />);

    const tabs = screen.getAllByRole('tab');
    expect(tabs[0]).toHaveAttribute('aria-selected', 'false');
    expect(tabs[1]).toHaveAttribute('aria-selected', 'true');
    expect(tabs[2]).toHaveAttribute('aria-selected', 'false');
    expect(tabs[3]).toHaveAttribute('aria-selected', 'false');
  });

  it('exposes a tablist with the provided aria-label', () => {
    render(<PlayerTabs activeTab="sessions" onChange={vi.fn()} counts={counts} labels={labels} />);
    expect(screen.getByRole('tablist', { name: /player sections/i })).toBeInTheDocument();
  });

  it('calls onChange with the clicked key', () => {
    const onChange = vi.fn();
    render(<PlayerTabs activeTab="sessions" onChange={onChange} counts={counts} labels={labels} />);

    fireEvent.click(screen.getByRole('tab', { name: /giochi/i }));
    expect(onChange).toHaveBeenCalledWith('games');
  });

  it('omits the count pill when the count is zero', () => {
    const zeroCounts: Record<PlayerTabKey, number> = { sessions: 0, games: 0, toolkits: 0, achievements: 0 };
    render(<PlayerTabs activeTab="sessions" onChange={vi.fn()} counts={zeroCounts} labels={labels} />);

    const tabs = screen.getAllByRole('tab');
    // labels still present
    expect(tabs[0]).toHaveTextContent('Partite');
    // no numeric count visible (we check the tab innerText doesn't contain a digit)
    expect(tabs[0].textContent).not.toMatch(/\d/);
  });

  it('moves focus to the next tab on ArrowRight (keyboard nav delegated)', () => {
    const onChange = vi.fn();
    render(<PlayerTabs activeTab="sessions" onChange={onChange} counts={counts} labels={labels} />);

    const tabs = screen.getAllByRole('tab');
    tabs[0].focus();
    fireEvent.keyDown(tabs[0], { key: 'ArrowRight' });
    expect(onChange).toHaveBeenCalledWith('games');
  });
});
```

- [ ] **Step 1.2: Run the test file and confirm it fails**

```
cd apps/web
pnpm test src/app/\(authenticated\)/players/\[id\]/_components/__tests__/PlayerTabs.test.tsx --run
```

Expected: tests fail with module-resolution error (`Cannot find module '../PlayerTabs'`).

- [ ] **Step 1.3: Create the PlayerTabs component**

Path: `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerTabs.tsx`

```tsx
/**
 * PlayerTabs — 4-tab tablist for /players/[id] Stage 3 cluster (Issue #1113).
 *
 * Route-private wrapper. Reuses useTablistKeyboardNav hook for WAI-ARIA APG
 * keyboard contract (ArrowLeft/Right/Home/End, roving tabindex). The canonical
 * `Tabs` primitive in ui/detail-layout cannot be used directly because its
 * `TabKey` union is locked to shared-games detail values.
 */

'use client';

import type { JSX } from 'react';
import { useMemo } from 'react';

import clsx from 'clsx';

import { useTablistKeyboardNav } from '@/hooks/useTablistKeyboardNav';

export const PLAYER_TAB_KEYS = ['sessions', 'games', 'toolkits', 'achievements'] as const;
export type PlayerTabKey = (typeof PLAYER_TAB_KEYS)[number];

export interface PlayerTabsLabels {
  readonly tablistAriaLabel: string;
  readonly sessions: string;
  readonly games: string;
  readonly toolkits: string;
  readonly achievements: string;
}

export interface PlayerTabsProps {
  readonly activeTab: PlayerTabKey;
  readonly onChange: (next: PlayerTabKey) => void;
  readonly counts: Record<PlayerTabKey, number>;
  readonly labels: PlayerTabsLabels;
  readonly className?: string;
}

const ID_BASE = 'player-detail';

export function PlayerTabs({
  activeTab,
  onChange,
  counts,
  labels,
  className,
}: PlayerTabsProps): JSX.Element {
  const orderedKeys = useMemo(() => [...PLAYER_TAB_KEYS], []);
  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<PlayerTabKey>({
    orderedKeys,
    onChange,
  });

  const labelOf = (key: PlayerTabKey): string => labels[key];

  return (
    <div
      data-slot="player-detail-tabs"
      role="tablist"
      aria-label={labels.tablistAriaLabel}
      className={clsx('flex items-center gap-1 overflow-x-auto', className)}
    >
      {PLAYER_TAB_KEYS.map((key, index) => {
        const isActive = key === activeTab;
        const count = counts[key];
        return (
          <button
            key={key}
            ref={(el) => {
              tabRefs.current[index] = el;
            }}
            type="button"
            role="tab"
            id={`tab-${ID_BASE}-${key}`}
            aria-selected={isActive}
            aria-controls={`tabpanel-${ID_BASE}-${key}`}
            tabIndex={isActive ? 0 : -1}
            onClick={() => onChange(key)}
            onKeyDown={(event) => handleKeyDown(event, index)}
            className={clsx(
              'flex items-center gap-2 whitespace-nowrap rounded-t-md border-b-2 px-3 py-2 text-sm font-semibold transition-colors',
              isActive
                ? 'border-primary text-foreground'
                : 'border-transparent text-muted-foreground hover:text-foreground',
            )}
          >
            <span>{labelOf(key)}</span>
            {count > 0 && (
              <span className="tabular-nums text-xs font-mono text-muted-foreground">
                {count}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 1.4: Run the tests again and confirm they pass**

```
pnpm test src/app/\(authenticated\)/players/\[id\]/_components/__tests__/PlayerTabs.test.tsx --run
```

Expected: 6 passing, 0 failing.

- [ ] **Step 1.5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/players/\[id\]/_components/PlayerTabs.tsx \
        apps/web/src/app/\(authenticated\)/players/\[id\]/_components/__tests__/PlayerTabs.test.tsx
git commit -m "feat(player-detail): PlayerTabs tablist component (refs #1113)"
```

---

## Task 2 — PlayerConnectionBar component

**Files:**
- Create: `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerConnectionBar.tsx`
- Create: `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerConnectionBar.test.tsx`

Translates the existing `PlayerProfileFixture` shape into 6 `ConnectionPip[]` entries and renders via the canonical `ConnectionBar` primitive. Pips 3-6 (`event/agent/toolkit/chat`) render with `isEmpty: true` because the backend `PlayerStatistics` does not expose those counts yet.

- [ ] **Step 2.1: Write the failing test file**

Path: `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerConnectionBar.test.tsx`

```tsx
/**
 * PlayerConnectionBar unit tests — Stage 3 cluster (Issue #1113).
 *
 * Verifies the 6-pip layout and isEmpty fallback contract for pips 3-6.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { PlayerProfileFixture } from '@/lib/player-detail/player-detail-visual-test-fixture';

import { PlayerConnectionBar } from '../PlayerConnectionBar';

const labels = {
  topGames: 'Top giochi',
  sessions: 'Partite',
  gameNights: 'Game Nights',
  agents: 'Agenti usati',
  toolkits: 'Toolkit',
  chats: 'Chat',
};

function makeProfile(overrides: Partial<PlayerProfileFixture> = {}): PlayerProfileFixture {
  return {
    playerId: 'p-test',
    displayName: 'Test Player',
    totalSessions: 23,
    totalWins: 12,
    winRate: 0.52,
    favoriteGameName: 'Azul',
    favoriteAgentName: null,
    achievementCount: 0,
    leaderboardRank: null,
    ...overrides,
  };
}

describe('PlayerConnectionBar', () => {
  it('renders six pips in canonical order', () => {
    render(<PlayerConnectionBar stats={makeProfile()} gameCount={5} labels={labels} />);

    expect(screen.getByTestId('connection-pip-game')).toBeInTheDocument();
    expect(screen.getByTestId('connection-pip-session')).toBeInTheDocument();
    expect(screen.getByTestId('connection-pip-event')).toBeInTheDocument();
    expect(screen.getByTestId('connection-pip-agent')).toBeInTheDocument();
    expect(screen.getByTestId('connection-pip-toolkit')).toBeInTheDocument();
    expect(screen.getByTestId('connection-pip-chat')).toBeInTheDocument();
  });

  it('renders game pip with real count from gameCount prop', () => {
    render(<PlayerConnectionBar stats={makeProfile()} gameCount={5} labels={labels} />);
    const pip = screen.getByTestId('connection-pip-game');
    expect(pip).toHaveAttribute('aria-label', 'Top giochi: 5');
  });

  it('renders session pip with real count from stats.totalSessions', () => {
    render(<PlayerConnectionBar stats={makeProfile({ totalSessions: 23 })} gameCount={5} labels={labels} />);
    const pip = screen.getByTestId('connection-pip-session');
    expect(pip).toHaveAttribute('aria-label', 'Partite: 23');
  });

  it('renders pip 3-6 with empty fallback aria-label (no count suffix)', () => {
    render(<PlayerConnectionBar stats={makeProfile()} gameCount={5} labels={labels} />);

    expect(screen.getByTestId('connection-pip-event')).toHaveAttribute('aria-label', 'Game Nights');
    expect(screen.getByTestId('connection-pip-agent')).toHaveAttribute('aria-label', 'Agenti usati');
    expect(screen.getByTestId('connection-pip-toolkit')).toHaveAttribute('aria-label', 'Toolkit');
    expect(screen.getByTestId('connection-pip-chat')).toHaveAttribute('aria-label', 'Chat');
  });

  it('renders the game pip as empty when gameCount is zero', () => {
    render(<PlayerConnectionBar stats={makeProfile()} gameCount={0} labels={labels} />);
    const pip = screen.getByTestId('connection-pip-game');
    expect(pip).toHaveAttribute('aria-label', 'Top giochi');
  });

  it('renders the session pip as empty when totalSessions is zero', () => {
    render(<PlayerConnectionBar stats={makeProfile({ totalSessions: 0 })} gameCount={5} labels={labels} />);
    const pip = screen.getByTestId('connection-pip-session');
    expect(pip).toHaveAttribute('aria-label', 'Partite');
  });
});
```

- [ ] **Step 2.2: Run the test file and confirm it fails**

```
pnpm test src/app/\(authenticated\)/players/\[id\]/_components/__tests__/PlayerConnectionBar.test.tsx --run
```

Expected: tests fail with module-resolution error.

- [ ] **Step 2.3: Create the PlayerConnectionBar component**

Path: `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerConnectionBar.tsx`

```tsx
/**
 * PlayerConnectionBar — 6-pip ConnectionBar for /players/[id] Stage 3 cluster (Issue #1113).
 *
 * Translates PlayerProfileFixture + gameCount into 6 canonical ConnectionPip
 * entries. Pips 3-6 (event/agent/toolkit/chat) render with isEmpty=true
 * pending backend schema extension (follow-up issue).
 */

'use client';

import type { JSX } from 'react';
import { useMemo } from 'react';

import {
  Bot,
  Calendar,
  Dices,
  MessageCircle,
  Target,
  Wrench,
} from 'lucide-react';

import { ConnectionBar } from '@/components/ui/data-display/connection-bar/ConnectionBar';
import type { ConnectionPip } from '@/components/ui/data-display/connection-bar/types';
import type { PlayerProfileFixture } from '@/lib/player-detail/player-detail-visual-test-fixture';

export interface PlayerConnectionBarLabels {
  readonly topGames: string;
  readonly sessions: string;
  readonly gameNights: string;
  readonly agents: string;
  readonly toolkits: string;
  readonly chats: string;
}

export interface PlayerConnectionBarProps {
  readonly stats: PlayerProfileFixture;
  /** Distinct game count derived from gamePlayCounts.length by the caller. */
  readonly gameCount: number;
  readonly labels: PlayerConnectionBarLabels;
  readonly className?: string;
}

export function PlayerConnectionBar({
  stats,
  gameCount,
  labels,
  className,
}: PlayerConnectionBarProps): JSX.Element {
  const connections = useMemo<ConnectionPip[]>(
    () => [
      {
        entityType: 'game',
        count: gameCount,
        label: labels.topGames,
        icon: Dices,
        isEmpty: gameCount === 0,
      },
      {
        entityType: 'session',
        count: stats.totalSessions,
        label: labels.sessions,
        icon: Target,
        isEmpty: stats.totalSessions === 0,
      },
      // Pips 3-6: backend doesn't yet expose these counts on PlayerStatistics.
      // Follow-up issue tracks the schema extension; until then we render
      // isEmpty=true so the visual layout matches the mockup exactly.
      {
        entityType: 'event',
        count: 0,
        label: labels.gameNights,
        icon: Calendar,
        isEmpty: true,
      },
      {
        entityType: 'agent',
        count: 0,
        label: labels.agents,
        icon: Bot,
        isEmpty: true,
      },
      {
        entityType: 'toolkit',
        count: 0,
        label: labels.toolkits,
        icon: Wrench,
        isEmpty: true,
      },
      {
        entityType: 'chat',
        count: 0,
        label: labels.chats,
        icon: MessageCircle,
        isEmpty: true,
      },
    ],
    [gameCount, stats.totalSessions, labels],
  );

  return (
    <ConnectionBar
      connections={connections}
      onPipClick={() => {
        /* no-op until backend exposes the data and follow-up wires navigation */
      }}
      className={className}
      data-testid="player-connection-bar"
    />
  );
}
```

- [ ] **Step 2.4: Run the tests again and confirm they pass**

```
pnpm test src/app/\(authenticated\)/players/\[id\]/_components/__tests__/PlayerConnectionBar.test.tsx --run
```

Expected: 6 passing, 0 failing.

- [ ] **Step 2.5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/players/\[id\]/_components/PlayerConnectionBar.tsx \
        apps/web/src/app/\(authenticated\)/players/\[id\]/_components/__tests__/PlayerConnectionBar.test.tsx
git commit -m "feat(player-detail): PlayerConnectionBar 6-pip wrapper (refs #1113)"
```

---

## Task 3 — PlayerOverviewRegion component

**Files:**
- Create: `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerOverviewRegion.tsx`
- Create: `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerOverviewRegion.test.tsx`

Compose the three Wave 3 cards (`PlayerStatsGrid`, `PlayerLeaderboardCard`, `FavoriteAgentCard`) in a responsive layout that sits between Hero and Tabs.

- [ ] **Step 3.1: Write the failing test**

Path: `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerOverviewRegion.test.tsx`

```tsx
/**
 * PlayerOverviewRegion unit tests — Stage 3 cluster (Issue #1113).
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { PlayerProfileFixture } from '@/lib/player-detail/player-detail-visual-test-fixture';

import { PlayerOverviewRegion } from '../PlayerOverviewRegion';

const labels = {
  stats: {
    totalSessions: 'Partite',
    totalWins: 'Vittorie',
    winRate: 'Win rate',
    achievementCount: 'Achievement',
  },
  leaderboard: {
    title: 'Leaderboard',
    unranked: 'Non classificato',
    rank: 'Rank',
  },
  favoriteAgent: {
    title: 'Agente preferito',
    none: 'Nessuno',
    forGame: 'per',
  },
};

const profile: PlayerProfileFixture = {
  playerId: 'p-test',
  displayName: 'Test Player',
  totalSessions: 23,
  totalWins: 12,
  winRate: 0.52,
  favoriteGameName: 'Azul',
  favoriteAgentName: 'Carcassonne Coach',
  achievementCount: 3,
  leaderboardRank: 7,
};

describe('PlayerOverviewRegion', () => {
  it('renders the stats grid, leaderboard, and favorite-agent regions', () => {
    const { container } = render(
      <PlayerOverviewRegion stats={profile} labels={labels} onFavoriteAgentClick={vi.fn()} />,
    );

    // data-slot attributes from Wave 3 components are the contract
    expect(container.querySelector('[data-slot="player-stats-grid"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="player-leaderboard-card"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="favorite-agent-card"]')).not.toBeNull();
  });

  it('exposes a region root with data-slot="player-overview-region"', () => {
    const { container } = render(
      <PlayerOverviewRegion stats={profile} labels={labels} onFavoriteAgentClick={vi.fn()} />,
    );
    expect(container.querySelector('[data-slot="player-overview-region"]')).not.toBeNull();
  });

  it('forwards onFavoriteAgentClick when the favorite agent has a name', () => {
    const handler = vi.fn();
    render(
      <PlayerOverviewRegion stats={profile} labels={labels} onFavoriteAgentClick={handler} />,
    );
    // FavoriteAgentCard renders a button when agent name is non-null
    const card = screen.getByText(/carcassonne coach/i);
    expect(card).toBeInTheDocument();
  });
});
```

- [ ] **Step 3.2: Run test, confirm it fails (module not found)**

```
pnpm test src/app/\(authenticated\)/players/\[id\]/_components/__tests__/PlayerOverviewRegion.test.tsx --run
```

- [ ] **Step 3.3: Create the PlayerOverviewRegion component**

Path: `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerOverviewRegion.tsx`

```tsx
/**
 * PlayerOverviewRegion — stats + leaderboard + favorite-agent composition for
 * /players/[id] Stage 3 cluster (Issue #1113).
 *
 * Sits between the PlayerHero and the PlayerTabs inside the DetailPageLayout
 * `hero` slot. Wave 3 components are consumed unchanged.
 */

'use client';

import type { JSX } from 'react';

import {
  FavoriteAgentCard,
  PlayerLeaderboardCard,
  PlayerStatsGrid,
  type FavoriteAgentCardLabels,
  type PlayerLeaderboardCardLabels,
  type PlayerStatsGridLabels,
} from '@/components/features/player-detail';
import type { PlayerProfileFixture } from '@/lib/player-detail/player-detail-visual-test-fixture';

export interface PlayerOverviewRegionLabels {
  readonly stats: PlayerStatsGridLabels;
  readonly leaderboard: PlayerLeaderboardCardLabels;
  readonly favoriteAgent: FavoriteAgentCardLabels;
}

export interface PlayerOverviewRegionProps {
  readonly stats: PlayerProfileFixture;
  readonly labels: PlayerOverviewRegionLabels;
  readonly onFavoriteAgentClick?: () => void;
}

export function PlayerOverviewRegion({
  stats,
  labels,
  onFavoriteAgentClick,
}: PlayerOverviewRegionProps): JSX.Element {
  return (
    <div
      data-slot="player-overview-region"
      className="mx-auto w-full max-w-4xl px-4 sm:px-8 flex flex-col gap-4"
    >
      <PlayerStatsGrid
        totalSessions={stats.totalSessions}
        totalWins={stats.totalWins}
        winRate={stats.winRate}
        achievementCount={stats.achievementCount}
        labels={labels.stats}
      />
      <div className="grid gap-4 sm:grid-cols-2">
        <PlayerLeaderboardCard rank={stats.leaderboardRank} labels={labels.leaderboard} />
        <FavoriteAgentCard
          agentName={stats.favoriteAgentName}
          gameName={stats.favoriteGameName}
          onClick={stats.favoriteAgentName != null ? onFavoriteAgentClick : undefined}
          labels={labels.favoriteAgent}
        />
      </div>
    </div>
  );
}
```

- [ ] **Step 3.4: Run tests, confirm 3 pass**

```
pnpm test src/app/\(authenticated\)/players/\[id\]/_components/__tests__/PlayerOverviewRegion.test.tsx --run
```

- [ ] **Step 3.5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/players/\[id\]/_components/PlayerOverviewRegion.tsx \
        apps/web/src/app/\(authenticated\)/players/\[id\]/_components/__tests__/PlayerOverviewRegion.test.tsx
git commit -m "feat(player-detail): PlayerOverviewRegion stats composition (refs #1113)"
```

---

## Task 4 — Three tab panel components

**Files:**
- Create: `apps/web/src/app/(authenticated)/players/[id]/_components/SessionsTabPanel.tsx`
- Create: `apps/web/src/app/(authenticated)/players/[id]/_components/GamesTabPanel.tsx`
- Create: `apps/web/src/app/(authenticated)/players/[id]/_components/ToolkitsTabPanel.tsx`
- Create: `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/SessionsTabPanel.test.tsx`
- Create: `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/GamesTabPanel.test.tsx`
- Create: `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/ToolkitsTabPanel.test.tsx`

Three small panels. The cluster scope is "adopt the layout"; tab content beyond summary + CTA is deferred to follow-ups.

- [ ] **Step 4.1: Write all three failing tests**

Path: `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/SessionsTabPanel.test.tsx`

```tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { PlayerProfileFixture } from '@/lib/player-detail/player-detail-visual-test-fixture';

import { SessionsTabPanel } from '../SessionsTabPanel';

const labels = {
  title: 'Sessioni recenti',
  viewAll: 'Vedi tutte',
  empty: 'Nessuna partita registrata',
  totalLabel: 'Totale partite',
};

const profile: PlayerProfileFixture = {
  playerId: 'p-test',
  displayName: 'Test Player',
  totalSessions: 23,
  totalWins: 12,
  winRate: 0.52,
  favoriteGameName: 'Azul',
  favoriteAgentName: null,
  achievementCount: 0,
  leaderboardRank: null,
};

describe('SessionsTabPanel', () => {
  it('renders the panel root with data-slot="sessions-tab-panel"', () => {
    const { container } = render(<SessionsTabPanel stats={profile} labels={labels} />);
    expect(container.querySelector('[data-slot="sessions-tab-panel"]')).not.toBeNull();
  });

  it('shows the total sessions count', () => {
    render(<SessionsTabPanel stats={profile} labels={labels} />);
    expect(screen.getByText('23')).toBeInTheDocument();
    expect(screen.getByText(/totale partite/i)).toBeInTheDocument();
  });

  it('renders a CTA link to /players/<id>/sessions', () => {
    render(<SessionsTabPanel stats={profile} labels={labels} />);
    const cta = screen.getByRole('link', { name: /vedi tutte/i });
    expect(cta).toHaveAttribute('href', '/players/p-test/sessions');
  });

  it('shows the empty label when totalSessions is zero', () => {
    render(
      <SessionsTabPanel
        stats={{ ...profile, totalSessions: 0 }}
        labels={labels}
      />,
    );
    expect(screen.getByText(/nessuna partita registrata/i)).toBeInTheDocument();
  });
});
```

Path: `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/GamesTabPanel.test.tsx`

```tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { GamesTabPanel } from '../GamesTabPanel';

const labels = {
  title: 'Giochi giocati',
  viewAll: 'Vedi tutti',
  empty: 'Nessun gioco giocato',
  playsSuffix: 'partite',
};

describe('GamesTabPanel', () => {
  const playerId = 'p-test';

  it('renders the panel root with data-slot="games-tab-panel"', () => {
    const { container } = render(
      <GamesTabPanel
        playerId={playerId}
        gamePlayCounts={{ Azul: 23, Wingspan: 17 }}
        labels={labels}
      />,
    );
    expect(container.querySelector('[data-slot="games-tab-panel"]')).not.toBeNull();
  });

  it('lists each game with its play count ranked descending', () => {
    render(
      <GamesTabPanel
        playerId={playerId}
        gamePlayCounts={{ Azul: 23, Wingspan: 17, Brass: 12 }}
        labels={labels}
      />,
    );
    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(3);
    expect(items[0]).toHaveTextContent('Azul');
    expect(items[0]).toHaveTextContent('23');
    expect(items[1]).toHaveTextContent('Wingspan');
    expect(items[2]).toHaveTextContent('Brass');
  });

  it('renders a CTA link to /players/<id>/games when non-empty', () => {
    render(
      <GamesTabPanel
        playerId={playerId}
        gamePlayCounts={{ Azul: 23 }}
        labels={labels}
      />,
    );
    const cta = screen.getByRole('link', { name: /vedi tutti/i });
    expect(cta).toHaveAttribute('href', '/players/p-test/games');
  });

  it('shows the empty label when gamePlayCounts is empty', () => {
    render(<GamesTabPanel playerId={playerId} gamePlayCounts={{}} labels={labels} />);
    expect(screen.getByText(/nessun gioco giocato/i)).toBeInTheDocument();
  });
});
```

Path: `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/ToolkitsTabPanel.test.tsx`

```tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ToolkitsTabPanel } from '../ToolkitsTabPanel';

const labels = {
  title: 'Toolkit',
  comingSoon: 'I toolkit del giocatore arriveranno presto',
};

describe('ToolkitsTabPanel', () => {
  it('renders the panel root with data-slot="toolkits-tab-panel"', () => {
    const { container } = render(<ToolkitsTabPanel labels={labels} />);
    expect(container.querySelector('[data-slot="toolkits-tab-panel"]')).not.toBeNull();
  });

  it('shows the coming-soon label', () => {
    render(<ToolkitsTabPanel labels={labels} />);
    expect(
      screen.getByText(/i toolkit del giocatore arriveranno presto/i),
    ).toBeInTheDocument();
  });
});
```

- [ ] **Step 4.2: Run all three test files; confirm they fail**

```
pnpm test src/app/\(authenticated\)/players/\[id\]/_components/__tests__/SessionsTabPanel.test.tsx src/app/\(authenticated\)/players/\[id\]/_components/__tests__/GamesTabPanel.test.tsx src/app/\(authenticated\)/players/\[id\]/_components/__tests__/ToolkitsTabPanel.test.tsx --run
```

- [ ] **Step 4.3: Create the three panel components**

Path: `apps/web/src/app/(authenticated)/players/[id]/_components/SessionsTabPanel.tsx`

```tsx
/**
 * SessionsTabPanel — content for the "Sessions" tab on /players/[id] (Issue #1113).
 *
 * MVP: shows total session count + CTA link to the existing subroute
 * /players/[id]/sessions which carries the full Wave 3 list view. Follow-up
 * issue will expand into a richer in-tab list once backend exposes a
 * per-player session feed.
 */

'use client';

import Link from 'next/link';
import type { JSX } from 'react';

import type { PlayerProfileFixture } from '@/lib/player-detail/player-detail-visual-test-fixture';

export interface SessionsTabPanelLabels {
  readonly title: string;
  readonly viewAll: string;
  readonly empty: string;
  readonly totalLabel: string;
}

export interface SessionsTabPanelProps {
  readonly stats: PlayerProfileFixture;
  readonly labels: SessionsTabPanelLabels;
}

export function SessionsTabPanel({ stats, labels }: SessionsTabPanelProps): JSX.Element {
  const isEmpty = stats.totalSessions === 0;
  return (
    <div
      data-slot="sessions-tab-panel"
      className="mx-auto w-full max-w-4xl px-4 sm:px-8 flex flex-col gap-4"
    >
      <h2 className="text-lg font-semibold">{labels.title}</h2>
      {isEmpty ? (
        <p className="text-muted-foreground">{labels.empty}</p>
      ) : (
        <>
          <div className="flex items-baseline gap-2">
            <span className="text-3xl font-bold tabular-nums">{stats.totalSessions}</span>
            <span className="text-muted-foreground">{labels.totalLabel}</span>
          </div>
          <Link
            href={`/players/${stats.playerId}/sessions`}
            className="text-sm font-semibold text-primary hover:underline"
          >
            {labels.viewAll}
          </Link>
        </>
      )}
    </div>
  );
}
```

Path: `apps/web/src/app/(authenticated)/players/[id]/_components/GamesTabPanel.tsx`

```tsx
/**
 * GamesTabPanel — content for the "Games" tab on /players/[id] (Issue #1113).
 *
 * MVP: ranked list of gamePlayCounts entries + CTA to /players/[id]/games.
 * Each entry is a simple <li> row; follow-up issue may upgrade to MeepleCard
 * grid once a richer per-game shape lands.
 */

'use client';

import Link from 'next/link';
import type { JSX } from 'react';
import { useMemo } from 'react';

export interface GamesTabPanelLabels {
  readonly title: string;
  readonly viewAll: string;
  readonly empty: string;
  readonly playsSuffix: string;
}

export interface GamesTabPanelProps {
  readonly playerId: string;
  readonly gamePlayCounts: Readonly<Record<string, number>>;
  readonly labels: GamesTabPanelLabels;
}

export function GamesTabPanel({
  playerId,
  gamePlayCounts,
  labels,
}: GamesTabPanelProps): JSX.Element {
  const ranked = useMemo(
    () =>
      Object.entries(gamePlayCounts)
        .filter(([, count]) => count > 0)
        .sort(([, a], [, b]) => b - a),
    [gamePlayCounts],
  );

  const isEmpty = ranked.length === 0;

  return (
    <div
      data-slot="games-tab-panel"
      className="mx-auto w-full max-w-4xl px-4 sm:px-8 flex flex-col gap-4"
    >
      <h2 className="text-lg font-semibold">{labels.title}</h2>
      {isEmpty ? (
        <p className="text-muted-foreground">{labels.empty}</p>
      ) : (
        <>
          <ul className="flex flex-col gap-2">
            {ranked.map(([name, count]) => (
              <li
                key={name}
                className="flex items-center justify-between rounded-md border border-border px-3 py-2"
              >
                <span className="font-medium">{name}</span>
                <span className="text-muted-foreground tabular-nums">
                  {count} {labels.playsSuffix}
                </span>
              </li>
            ))}
          </ul>
          <Link
            href={`/players/${playerId}/games`}
            className="text-sm font-semibold text-primary hover:underline"
          >
            {labels.viewAll}
          </Link>
        </>
      )}
    </div>
  );
}
```

Path: `apps/web/src/app/(authenticated)/players/[id]/_components/ToolkitsTabPanel.tsx`

```tsx
/**
 * ToolkitsTabPanel — content for the "Toolkits" tab on /players/[id] (Issue #1113).
 *
 * Placeholder. Backend does not yet expose per-player toolkit data on
 * PlayerStatistics. Tracked by follow-up issue for the toolkit-detail cluster
 * (parent spec §3, cluster #2).
 */

'use client';

import type { JSX } from 'react';

export interface ToolkitsTabPanelLabels {
  readonly title: string;
  readonly comingSoon: string;
}

export interface ToolkitsTabPanelProps {
  readonly labels: ToolkitsTabPanelLabels;
}

export function ToolkitsTabPanel({ labels }: ToolkitsTabPanelProps): JSX.Element {
  return (
    <div
      data-slot="toolkits-tab-panel"
      className="mx-auto w-full max-w-4xl px-4 sm:px-8 flex flex-col items-center gap-2 py-12"
    >
      <h2 className="text-lg font-semibold">{labels.title}</h2>
      <p className="text-muted-foreground text-center">{labels.comingSoon}</p>
    </div>
  );
}
```

- [ ] **Step 4.4: Run all three test files; confirm they pass (11 tests total)**

```
pnpm test src/app/\(authenticated\)/players/\[id\]/_components/__tests__/SessionsTabPanel.test.tsx src/app/\(authenticated\)/players/\[id\]/_components/__tests__/GamesTabPanel.test.tsx src/app/\(authenticated\)/players/\[id\]/_components/__tests__/ToolkitsTabPanel.test.tsx --run
```

Expected: 11 passing total (4 + 4 + 2 + 1; actual = 4 sessions + 4 games + 2 toolkits = 10; if numbers differ, that is fine — what matters is all pass).

- [ ] **Step 4.5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/players/\[id\]/_components/SessionsTabPanel.tsx \
        apps/web/src/app/\(authenticated\)/players/\[id\]/_components/GamesTabPanel.tsx \
        apps/web/src/app/\(authenticated\)/players/\[id\]/_components/ToolkitsTabPanel.tsx \
        apps/web/src/app/\(authenticated\)/players/\[id\]/_components/__tests__/SessionsTabPanel.test.tsx \
        apps/web/src/app/\(authenticated\)/players/\[id\]/_components/__tests__/GamesTabPanel.test.tsx \
        apps/web/src/app/\(authenticated\)/players/\[id\]/_components/__tests__/ToolkitsTabPanel.test.tsx
git commit -m "feat(player-detail): three tab panel components (refs #1113)"
```

---

## Task 5 — PlayerDetailView refactor

**Files:**
- Modify: `apps/web/src/app/(authenticated)/players/[id]/_components/PlayerDetailView.tsx`
- Modify: `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerDetailView.test.tsx`

The heart of the cluster. The `default` shell is rewritten to use `<DetailPageLayout>` + the new wrappers. Other shells (loading/error/not-found) are preserved as-is.

- [ ] **Step 5.1: Extend the existing test file with new test cases**

Open `apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerDetailView.test.tsx` and append the following inside the existing `describe('PlayerDetailView', ...)` block (or create a new sibling `describe` if structure differs). The exact location doesn't matter as long as the tests run.

```tsx
  it('wraps the default shell in DetailPageLayout', async () => {
    mockStatsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        totalSessions: 23,
        totalWins: 12,
        gamePlayCounts: { Azul: 23 },
      },
    });

    const { container } = render(<PlayerDetailView playerId="p-test" />);

    expect(container.querySelector('[data-slot="detail-page-layout"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="player-overview-region"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="player-detail-tabs"]')).not.toBeNull();
  });

  it('renders the Sessions tab panel by default (no ?tab=)', async () => {
    mockStatsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { totalSessions: 23, totalWins: 12, gamePlayCounts: { Azul: 23 } },
    });

    const { container } = render(<PlayerDetailView playerId="p-test" />);
    expect(container.querySelector('[data-slot="sessions-tab-panel"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="games-tab-panel"]')).toBeNull();
  });

  it('renders the Games tab panel when ?tab=games is in the URL', async () => {
    mockSearchParams.set('tab', 'games');
    mockStatsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { totalSessions: 23, totalWins: 12, gamePlayCounts: { Azul: 23 } },
    });

    const { container } = render(<PlayerDetailView playerId="p-test" />);
    expect(container.querySelector('[data-slot="games-tab-panel"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="sessions-tab-panel"]')).toBeNull();

    mockSearchParams.delete('tab'); // cleanup for sibling tests
  });

  it('falls back to the default tab when ?tab= is an unknown key', async () => {
    mockSearchParams.set('tab', 'bogus');
    mockStatsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { totalSessions: 23, totalWins: 12, gamePlayCounts: { Azul: 23 } },
    });

    const { container } = render(<PlayerDetailView playerId="p-test" />);
    expect(container.querySelector('[data-slot="sessions-tab-panel"]')).not.toBeNull();

    mockSearchParams.delete('tab');
  });

  it('renders the Toolkits tab panel when ?tab=toolkits', async () => {
    mockSearchParams.set('tab', 'toolkits');
    mockStatsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { totalSessions: 23, totalWins: 12, gamePlayCounts: { Azul: 23 } },
    });

    const { container } = render(<PlayerDetailView playerId="p-test" />);
    expect(container.querySelector('[data-slot="toolkits-tab-panel"]')).not.toBeNull();

    mockSearchParams.delete('tab');
  });

  it('renders the achievement grid when ?tab=achievements', async () => {
    mockSearchParams.set('tab', 'achievements');
    mockStatsQuery.mockReturnValue({
      isLoading: false,
      isError: false,
      data: { totalSessions: 23, totalWins: 12, gamePlayCounts: { Azul: 23 } },
    });

    const { container } = render(<PlayerDetailView playerId="p-test" />);
    // AchievementBadgeGrid carries data-slot="achievement-badge-grid" from Wave 3
    expect(container.querySelector('[data-slot="achievement-badge-grid"]')).not.toBeNull();

    mockSearchParams.delete('tab');
  });
```

Note: if the existing test file's `mockSearchParams` is declared `const`, the `.set()`/`.delete()` calls work because `URLSearchParams` is mutable. If your existing setup uses a fresh instance per test, adapt by setting it inside each new test (move `mockSearchParams = new URLSearchParams(...)` inside `beforeEach`).

- [ ] **Step 5.2: Run the test file and confirm new tests fail**

```
pnpm test src/app/\(authenticated\)/players/\[id\]/_components/__tests__/PlayerDetailView.test.tsx --run
```

Expected: ≥6 new tests fail. Existing tests still pass.

- [ ] **Step 5.3: Read the current PlayerDetailView.tsx to locate the default-shell render block**

```bash
grep -n "default shell\|safeProfile\|<div data-slot=\"player-detail-view\"" apps/web/src/app/\(authenticated\)/players/\[id\]/_components/PlayerDetailView.tsx
```

You should find a `return (...)` block at the bottom of the file that renders `<div data-slot="player-detail-view"> <PlayerHero/> ... <AchievementBadgeGrid/> </div>` (the "flat" layout). This is the only block that needs replacing in Step 5.5.

- [ ] **Step 5.4: Add imports + tab-key constants at the top of PlayerDetailView.tsx**

Find the existing import block in `PlayerDetailView.tsx` (around lines 30-60) and add at the bottom of it (after the last existing import):

```tsx
import { DetailPageLayout } from '@/components/ui/detail-layout';

import { PlayerConnectionBar, type PlayerConnectionBarLabels } from './PlayerConnectionBar';
import { PlayerOverviewRegion, type PlayerOverviewRegionLabels } from './PlayerOverviewRegion';
import { PlayerTabs, PLAYER_TAB_KEYS, type PlayerTabKey, type PlayerTabsLabels } from './PlayerTabs';
import { SessionsTabPanel, type SessionsTabPanelLabels } from './SessionsTabPanel';
import { GamesTabPanel, type GamesTabPanelLabels } from './GamesTabPanel';
import { ToolkitsTabPanel, type ToolkitsTabPanelLabels } from './ToolkitsTabPanel';
```

Add after the `StateOverride` block (after `parseStateOverride` function):

```tsx
// ─── Tab URL state ────────────────────────────────────────────────────────────

const DEFAULT_TAB: PlayerTabKey = 'sessions';

function readTabFromUrl(searchParams: ReturnType<typeof useSearchParams>): PlayerTabKey {
  const raw = searchParams?.get('tab');
  return (PLAYER_TAB_KEYS as readonly string[]).includes(raw ?? '')
    ? (raw as PlayerTabKey)
    : DEFAULT_TAB;
}
```

- [ ] **Step 5.5: Replace the default-shell render block**

Locate the JSX block that currently starts with `<div data-slot="player-detail-view" className="flex flex-col gap-6 pb-24">` near the bottom of `PlayerDetailView.tsx`. Replace the ENTIRE block (from that opening tag through the closing `</div>` that wraps the achievement grid) with:

```tsx
  const tab: PlayerTabKey = readTabFromUrl(searchParams);

  const onTabChange = (next: PlayerTabKey): void => {
    const params = new URLSearchParams(searchParams ?? '');
    if (next === DEFAULT_TAB) {
      params.delete('tab');
    } else {
      params.set('tab', next);
    }
    const qs = params.toString();
    router.replace(
      `/players/${safePlayerId}${qs ? `?${qs}` : ''}`,
      // @ts-expect-error — Next.js types: `scroll` is supported but missing on RouterReplace overload in this version.
      { scroll: false },
    );
  };

  const gameCount = Object.keys(stats?.gamePlayCounts ?? {}).length;

  const tabCounts: Record<PlayerTabKey, number> = {
    sessions: safeProfile.totalSessions,
    games: gameCount,
    toolkits: 0,
    achievements: safeProfile.achievementCount,
  };

  const connectionLabels: PlayerConnectionBarLabels = {
    topGames: t('players.detail.connections.topGames'),
    sessions: t('players.detail.connections.sessions'),
    gameNights: t('players.detail.connections.gameNights'),
    agents: t('players.detail.connections.agents'),
    toolkits: t('players.detail.connections.toolkits'),
    chats: t('players.detail.connections.chats'),
  };

  const tabLabels: PlayerTabsLabels = {
    tablistAriaLabel: t('players.detail.tabs.ariaLabel'),
    sessions: t('players.detail.tabs.sessions'),
    games: t('players.detail.tabs.games'),
    toolkits: t('players.detail.tabs.toolkits'),
    achievements: t('players.detail.tabs.achievements'),
  };

  const overviewLabels: PlayerOverviewRegionLabels = {
    stats: statsLabels,
    leaderboard: leaderboardLabels,
    favoriteAgent: favoriteAgentLabels,
  };

  const sessionsLabels: SessionsTabPanelLabels = {
    title: t('players.detail.tabs.sessions.title'),
    viewAll: t('players.detail.tabs.sessions.viewAll'),
    empty: t('players.detail.tabs.sessions.empty'),
    totalLabel: t('players.detail.tabs.sessions.totalLabel'),
  };

  const gamesLabels: GamesTabPanelLabels = {
    title: t('players.detail.tabs.games.title'),
    viewAll: t('players.detail.tabs.games.viewAll'),
    empty: t('players.detail.tabs.games.empty'),
    playsSuffix: t('players.detail.tabs.games.playsSuffix'),
  };

  const toolkitsLabels: ToolkitsTabPanelLabels = {
    title: t('players.detail.tabs.toolkits.title'),
    comingSoon: t('players.detail.tabs.toolkits.comingSoon'),
  };

  let tabPanel: ReactElement;
  switch (tab) {
    case 'games':
      tabPanel = (
        <GamesTabPanel
          playerId={safePlayerId}
          gamePlayCounts={stats?.gamePlayCounts ?? {}}
          labels={gamesLabels}
        />
      );
      break;
    case 'toolkits':
      tabPanel = <ToolkitsTabPanel labels={toolkitsLabels} />;
      break;
    case 'achievements':
      tabPanel = (
        <AchievementBadgeGrid
          count={safeProfile.achievementCount}
          viewAllHref={`/players/${safePlayerId}/achievements`}
          labels={achievementLabels}
        />
      );
      break;
    case 'sessions':
    default:
      tabPanel = <SessionsTabPanel stats={safeProfile} labels={sessionsLabels} />;
      break;
  }

  return (
    <DetailPageLayout
      hero={
        <>
          <PlayerHero
            displayName={safeProfile.displayName}
            totalSessions={safeProfile.totalSessions}
            totalWins={safeProfile.totalWins}
            winRate={safeProfile.winRate}
            onBack={() => router.push('/players')}
            labels={heroLabels}
          />
          <PlayerOverviewRegion
            stats={safeProfile}
            labels={overviewLabels}
            onFavoriteAgentClick={
              safeProfile.favoriteAgentName != null ? () => router.push('/agents') : undefined
            }
          />
        </>
      }
      connections={
        <PlayerConnectionBar
          stats={safeProfile}
          gameCount={gameCount}
          labels={connectionLabels}
        />
      }
      tabs={<PlayerTabs activeTab={tab} onChange={onTabChange} counts={tabCounts} labels={tabLabels} />}
    >
      {tabPanel}
    </DetailPageLayout>
  );
```

Notes:
- The `// @ts-expect-error` directive on `{ scroll: false }` exists because Next.js router type definitions vary by version. If `pnpm typecheck` passes without it, remove the directive — keep the file lint-clean.
- The label keys (`players.detail.tabs.*`, `players.detail.connections.*`) need to exist in the i18n translations file. The test mock returns the key as the value, so tests don't fail. For the live build, add the keys to whatever locale file backs `useTranslation` (usually `apps/web/src/lib/i18n/locales/it.json` or similar). The exact file is identified via `grep -r "players.detail.hero" apps/web/src/lib/i18n/locales/` after Wave 3.

- [ ] **Step 5.6: Add translation keys (locale files)**

```
grep -rln "players.detail.hero" apps/web/src/lib/i18n/locales/
```

This identifies the active locale files. For each file, add the following keys under the `players.detail` branch (next to existing `hero` and `stats` keys). Example for `it.json`:

```json
{
  "players": {
    "detail": {
      "connections": {
        "topGames": "Top giochi",
        "sessions": "Partite",
        "gameNights": "Game Nights",
        "agents": "Agenti usati",
        "toolkits": "Toolkit",
        "chats": "Chat"
      },
      "tabs": {
        "ariaLabel": "Sezioni del giocatore",
        "sessions": "Partite",
        "games": "Giochi",
        "toolkits": "Toolkit",
        "achievements": "Achievement",
        "sessions.title": "Partite recenti",
        "sessions.viewAll": "Vedi tutte",
        "sessions.empty": "Nessuna partita registrata",
        "sessions.totalLabel": "Totale partite",
        "games.title": "Giochi giocati",
        "games.viewAll": "Vedi tutti",
        "games.empty": "Nessun gioco giocato",
        "games.playsSuffix": "partite",
        "toolkits.title": "Toolkit",
        "toolkits.comingSoon": "I toolkit del giocatore arriveranno presto"
      }
    }
  }
}
```

If the locale file uses a different structure (dot-notation keys vs nested object), match the existing pattern verbatim. Inspect 5-10 lines of context around the existing `players.detail.hero` block before editing.

If you discover the i18n hook does not return `key` literally when a key is missing (i.e. it returns empty string or throws), the test mock will need updating. Check `apps/web/src/hooks/useTranslation.ts` for the missing-key behavior before debugging false test failures.

- [ ] **Step 5.7: Run the PlayerDetailView test file and confirm all tests pass**

```
pnpm test src/app/\(authenticated\)/players/\[id\]/_components/__tests__/PlayerDetailView.test.tsx --run
```

Expected: 100% pass. If any FSM-shell test (loading/error/not-found) regresses, the refactor broke the shell preservation — revisit Step 5.5 to confirm only the `default` shell was modified.

- [ ] **Step 5.8: Run typecheck + lint and confirm clean**

```
pnpm typecheck && pnpm lint
```

Expected: 0 errors. Pre-existing lint warnings are OK.

- [ ] **Step 5.9: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/players/\[id\]/_components/PlayerDetailView.tsx \
        apps/web/src/app/\(authenticated\)/players/\[id\]/_components/__tests__/PlayerDetailView.test.tsx \
        apps/web/src/lib/i18n/locales/
git commit -m "refactor(player-detail): adopt DetailPageLayout + URL tab state (refs #1113)

Wraps the default FSM shell of PlayerDetailView in <DetailPageLayout>:
  - hero slot = <PlayerHero /> + <PlayerOverviewRegion />
  - connections slot = <PlayerConnectionBar /> (6 pips)
  - tabs slot = <PlayerTabs /> (4 tabs)
  - children = active tab panel via switch on URL ?tab param

Wave 3 components (PlayerHero, PlayerStatsGrid, PlayerLeaderboardCard,
FavoriteAgentCard, AchievementBadgeGrid) consumed unchanged. FSM shells
(loading/error/not-found) preserved verbatim; only the default shell
body was restructured.

Adds locale keys for connections + tabs labels."
```

---

## Task 6 — Playwright visual conformity spec

**Files:**
- Create: `apps/web/e2e/visual-conformity/player-detail.spec.ts`

- [ ] **Step 6.1: Inspect existing visual-conformity specs for the established pattern**

```
ls apps/web/e2e/visual-conformity/
```

Pick one of the existing files (e.g., `bootstrap.spec.ts` or whichever cluster has the most similar shape) and read the first 50 lines to copy the import / fixture pattern.

- [ ] **Step 6.2: Create the player-detail visual spec**

Path: `apps/web/e2e/visual-conformity/player-detail.spec.ts`

```ts
/**
 * Visual conformity — /players/[id] (Issue #1113).
 *
 * Compares the rendered route against the canonical mockup
 * admin-mockups/design_files/sp4-player-detail.html at two viewports for
 * each of the 4 tabs. Threshold 2% per parent spec AC3.1.
 */

import { test, expect } from '@playwright/test';

const ROUTE_BASE = '/players/p-test-fixture';
const TABS = ['sessions', 'games', 'toolkits', 'achievements'] as const;

for (const tab of TABS) {
  test(`player-detail ${tab} tab matches sp4-player-detail mockup`, async ({ page }) => {
    const url = tab === 'sessions' ? ROUTE_BASE : `${ROUTE_BASE}?tab=${tab}`;
    await page.goto(url);
    // Wait for the DetailPageLayout root to be present + tab content rendered
    await page.waitForSelector('[data-slot="detail-page-layout"]');
    await page.waitForSelector(`[data-slot="${tab === 'achievements' ? 'achievement-badge-grid' : `${tab}-tab-panel`}"]`);

    await expect(page).toHaveScreenshot(`player-detail-${tab}.png`, {
      maxDiffPixelRatio: 0.02,
      fullPage: true,
    });
  });
}
```

If the existing visual specs use a different test factory (e.g., `test.use({ viewport })` or a custom `screenshot` helper), align with that pattern. The test names and screenshot file names above are the contract regardless of structure.

- [ ] **Step 6.3: Generate the baseline screenshots (snapshot-update run)**

```
pnpm exec playwright test apps/web/e2e/visual-conformity/player-detail.spec.ts --update-snapshots
```

This run produces the baseline PNGs. Inspect them visually before committing — if any diverge significantly from the mockup, do NOT commit; instead, reload the page-tree and verify the route renders correctly.

- [ ] **Step 6.4: Run again without `--update-snapshots` to confirm green**

```
pnpm exec playwright test apps/web/e2e/visual-conformity/player-detail.spec.ts
```

Expected: 4 passing (one per tab, single viewport unless multi-viewport is project-level config).

- [ ] **Step 6.5: Commit**

```bash
git add apps/web/e2e/visual-conformity/player-detail.spec.ts apps/web/e2e/visual-conformity/player-detail-*.png
git commit -m "test(player-detail): Playwright visual conformity vs sp4-player-detail mockup (refs #1113)"
```

---

## Task 7 — State coverage matrix update

**Files:**
- Modify: `apps/web/e2e/state-coverage/state-matrix.json`

- [ ] **Step 7.1: Read the current matrix file**

```
cat apps/web/e2e/state-coverage/state-matrix.json | head -30
```

Find the existing pattern for a route entry (probably keyed by mockup filename or route path).

- [ ] **Step 7.2: Add the `sp4-player-detail` entry**

Append (or merge into the existing JSON object) a row for the player-detail route. The exact shape depends on the matrix schema — match an existing entry like `sp4-shared-game-detail`. Conceptually:

```json
{
  "sp4-player-detail": {
    "route": "/players/[id]",
    "states": [
      "default",
      "tab-sessions",
      "tab-games",
      "tab-toolkits",
      "tab-achievements",
      "loading",
      "error",
      "empty",
      "not-found"
    ],
    "implemented": "yes",
    "lastUpdated": "2026-05-13"
  }
}
```

Adjust field names to match the schema. If `state-coverage-check.yml` has a JSON Schema sidecar (typical pattern from WS-D), run it via:

```
pnpm exec node -e "const Ajv = require('ajv'); /* ... */"
```

or whatever validation command the WS-D workflow uses. Failing validation = update the entry to match the schema.

- [ ] **Step 7.3: Validate the JSON locally**

```
pnpm exec node -e "JSON.parse(require('fs').readFileSync('apps/web/e2e/state-coverage/state-matrix.json', 'utf8'))"
```

Expected: no output (success). If parse error, the JSON is malformed.

- [ ] **Step 7.4: Commit**

```bash
git add apps/web/e2e/state-coverage/state-matrix.json
git commit -m "test(state-coverage): register sp4-player-detail row (refs #1113, #1070)"
```

---

## Task 8 — Parent spec §4 composability table update

**Files:**
- Modify: `docs/for-developers/specs/2026-05-11-design-system-deversioning.md`

- [ ] **Step 8.1: Locate the row to change**

```
grep -n "/players/\[id\]" docs/for-developers/specs/2026-05-11-design-system-deversioning.md
```

You should find a row in the §4 composability table currently reading something like:

```
| `/players/[id]` | authenticated | `PlayerHero` | game/session | — (flat) | — |
```

- [ ] **Step 8.2: Update the Tabs column**

Edit that row to replace `— (flat)` with the four tab keys:

```
| `/players/[id]` | authenticated | `PlayerHero` | game/session/event/agent/toolkit/chat | Sessions/Games/Toolkits/Achievements | — |
```

Note: also widened the Connections column from `game/session` to the full 6-pip list, matching what the cluster actually delivers.

- [ ] **Step 8.3: Commit**

```bash
git add docs/for-developers/specs/2026-05-11-design-system-deversioning.md
git commit -m "docs(specs): update §4 composability table for /players/[id] post-Stage-3 (refs #1113)"
```

---

## Task 9 — Final verification + PR

- [ ] **Step 9.1: Run the full quality gate**

```
cd apps/web
pnpm typecheck && pnpm lint && pnpm test --run
```

Expected: all three exit 0. Some pre-existing warnings (e.g. unhandled rejections in unrelated test files) may surface — they are tracked separately and not blockers for this PR.

- [ ] **Step 9.2: Run the Playwright visual spec once more**

```
pnpm exec playwright test apps/web/e2e/visual-conformity/player-detail.spec.ts
```

Expected: 4 passing.

- [ ] **Step 9.3: Confirm no out-of-scope file changes**

```
git diff --name-only main-dev
```

Expected output (each file must match this list; no others):

```
apps/web/e2e/state-coverage/state-matrix.json
apps/web/e2e/visual-conformity/player-detail.spec.ts
apps/web/e2e/visual-conformity/player-detail-achievements.png
apps/web/e2e/visual-conformity/player-detail-games.png
apps/web/e2e/visual-conformity/player-detail-sessions.png
apps/web/e2e/visual-conformity/player-detail-toolkits.png
apps/web/src/app/(authenticated)/players/[id]/_components/GamesTabPanel.tsx
apps/web/src/app/(authenticated)/players/[id]/_components/PlayerConnectionBar.tsx
apps/web/src/app/(authenticated)/players/[id]/_components/PlayerDetailView.tsx
apps/web/src/app/(authenticated)/players/[id]/_components/PlayerOverviewRegion.tsx
apps/web/src/app/(authenticated)/players/[id]/_components/PlayerTabs.tsx
apps/web/src/app/(authenticated)/players/[id]/_components/SessionsTabPanel.tsx
apps/web/src/app/(authenticated)/players/[id]/_components/ToolkitsTabPanel.tsx
apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/GamesTabPanel.test.tsx
apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerConnectionBar.test.tsx
apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerDetailView.test.tsx
apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerOverviewRegion.test.tsx
apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/PlayerTabs.test.tsx
apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/SessionsTabPanel.test.tsx
apps/web/src/app/(authenticated)/players/[id]/_components/__tests__/ToolkitsTabPanel.test.tsx
apps/web/src/lib/i18n/locales/<locale-file>.json
docs/for-developers/specs/2026-05-11-design-system-deversioning.md
docs/superpowers/plans/2026-05-13-player-detail-stage3-cluster.md
docs/superpowers/specs/2026-05-13-player-detail-stage3-cluster-design.md
```

`<locale-file>` is whichever JSON the i18n hook reads from (typically `it.json` and/or `en.json` — confirmed during Step 5.6).

Any extra file = scope creep. Revert it before pushing.

- [ ] **Step 9.4: Push the branch**

```
git push -u origin feature/issue-1113-stage3-player-detail-cluster
```

- [ ] **Step 9.5: Open the PR**

```
gh pr create --base main-dev \
  --title "feat(player-detail): adopt DetailPageLayout + Tabs + 6-pip ConnectionBar (Stage 3 cluster, closes #1113)" \
  --body "$(cat <<'EOF'
## Summary

First real consumer of the `DetailPageLayout` composer primitive (PR #1112). Refactors `PlayerDetailView.tsx` to wrap its default FSM shell in the composer and adds three new route-private components:

- `PlayerOverviewRegion` — composes the existing Wave 3 cards (Stats / Leaderboard / FavoriteAgent) into the region between Hero and Tabs
- `PlayerConnectionBar` — translates `PlayerStatistics` into 6 ConnectionPip entries (2 with real counts, 4 with `isEmpty: true` until backend schema extension)
- `PlayerTabs` — 4-tab tablist (Sessions / Games / Toolkits / Achievements), reuses `useTablistKeyboardNav` hook for WAI-ARIA APG keyboard contract

Plus three tab panel components (SessionsTabPanel / GamesTabPanel / ToolkitsTabPanel) — minimal content, deeper integration tracked in follow-ups.

Tab state lives in `?tab=<key>` URL search param with graceful fallback to `sessions`. Subroutes `/players/[id]/{stats,sessions,games,achievements}` remain unchanged per Wave 3 contract preservation.

## Decisions captured during brainstorming (see spec §3)

| Question | Decision |
|---|---|
| Tab list — mockup vs initial issue | Mockup wins (no Stats tab; Stats live in overview region) |
| Stats components placement | Overview region between Hero and Tabs, via fragment in hero slot |
| ConnectionBar 6-pip data | 2 real + 4 isEmpty placeholders |
| Tab state | URL `?tab=<key>` search param |
| Subroutes | Unchanged (Wave 3 contract) |
| Visual conformity | Playwright vs sp4-player-detail mockup, ≤ 2% threshold |

## Spec & plan

- Design spec: `docs/superpowers/specs/2026-05-13-player-detail-stage3-cluster-design.md`
- Implementation plan: `docs/superpowers/plans/2026-05-13-player-detail-stage3-cluster.md`
- Composer primitive (dependency): PR #1112
- Parent spec: `docs/for-developers/specs/2026-05-11-design-system-deversioning.md` §4

## Test plan

- [x] `pnpm typecheck` green
- [x] `pnpm lint` green (no new warnings)
- [x] `pnpm test --run` green — new Vitest tests pass, existing Wave 3 tests unchanged
- [x] `pnpm exec playwright test apps/web/e2e/visual-conformity/player-detail.spec.ts` green — 4 tabs visual diff ≤ 2% vs mockup baseline
- [x] State coverage matrix entry added for `sp4-player-detail`
- [x] Parent spec §4 composability row updated

## Out of scope (tracked separately)

- Backend `PlayerStatistics` schema extension for event/agent/toolkit/chat counts (unblocks pips 3-6 from `isEmpty`)
- Guest variant gating (current FE assumes authenticated)
- Subroutes deprecation
- Richer tab content (panels currently summary + CTA; deeper UIs as follow-up issues)

Refs: #1113, #1112, #1026, #1023
EOF
)"
```

- [ ] **Step 9.6: Link the PR from the parent issue**

```
gh issue comment 1113 --body "Cluster PR opened: see https://github.com/meepleAi-app/meepleai-monorepo/pulls — links DetailPageLayout (PR #1112) as the first real consumer. Visual conformity baselines committed."
```

---

## Self-Review Notes (from plan author)

**Spec coverage check**:
- G1 (DetailPageLayout wrap) → Task 5
- G2 (PlayerOverviewRegion) → Task 3
- G3 (PlayerConnectionBar) → Task 2
- G4 (PlayerTabs) → Task 1
- G5 (URL tab state) → Task 5 Step 5.4+5.5
- G6 (visual conformity) → Task 6
- G7 (FSM shells preserved) → Task 5 (only default shell modified; Step 5.7 verifies)
- G8 (parent spec §4 update) → Task 8
- AC1-AC11 → mapped: AC1→Task 5; AC2→Task 2; AC3→Task 1; AC4→Task 5; AC5→Task 5; AC6→Step 9.3 diff guard; AC7→all Vitest tests in Tasks 1-5; AC8→Task 6; AC9→Step 9.1; AC10→Task 8; AC11→Task 7.

**Placeholder scan**: no TBD/TODO. The phrase "follow-up issue" appears in component JSDocs as context, not as a placeholder for missing implementation. The `<locale-file>` in Step 9.3 is a placeholder for discovery, marked explicitly.

**Type consistency**:
- `PlayerTabKey` defined in Task 1 (`PlayerTabs.tsx`) → reused in Task 5 (`PlayerDetailView.tsx`) — same import.
- `PLAYER_TAB_KEYS` constant exported from Task 1 → consumed in Task 5 `readTabFromUrl`.
- `PlayerProfileFixture` is the Wave 3 shape from `@/lib/player-detail/player-detail-visual-test-fixture` — used by Tasks 2, 3, 5 with the same property set (`totalSessions`, `gamePlayCounts`-via-stats reference, etc.). `gamePlayCounts` lives on the underlying `PlayerStatistics`, not on `PlayerProfileFixture`; Task 5 reads it from `stats?.gamePlayCounts ?? {}` and passes the derived count + the raw map separately.
- `PlayerConnectionBarLabels`, `PlayerTabsLabels`, `PlayerOverviewRegionLabels`, `SessionsTabPanelLabels`, `GamesTabPanelLabels`, `ToolkitsTabPanelLabels` — each defined in its task's component file and imported in Task 5.

**Risk callouts**:
- Step 5.5's `// @ts-expect-error` directive is provisional. If typecheck passes without it, remove it.
- Task 6 Step 6.3 (snapshot-update) requires manual visual inspection of the generated PNGs before commit. If the mockup baseline differs significantly from the rendered route, this is the moment to catch it — DO NOT commit and proceed; instead surface the diff to the user.
- Task 5 Step 5.6's locale file path is discovery-based. If `players.detail.hero` isn't present in any locale file (Wave 3 may have used a different key naming), inspect a known-working translation key from the existing PlayerDetailView render output and copy its file location.
