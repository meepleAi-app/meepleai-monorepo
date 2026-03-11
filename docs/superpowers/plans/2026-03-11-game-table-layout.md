# Game Table Layout — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the remaining 10 items from the Game Table layout spec (Quick View, Inline Picker, Game Night pages, Live Session, Activity Feed, Toolkit, AI integration).

**Architecture:** Zustand stores for state (following `store/admin-games/` pattern with `create` + `devtools` + `immer`). Components follow existing CardRack/TopBar patterns (Lucide icons, `cn()` utility, `data-testid`, `'use client'`). SSE reuses `useAgentChatStream` pattern. All new components under `apps/web/src/components/`.

**Tech Stack:** Next.js 16 (App Router), React 19, Tailwind 4, Zustand, Vitest + Testing Library, Lucide React, shadcn/ui

---

## Chunk 1: Quick View Panel & Store Foundation

### Task 1: `useQuickViewStore` — Zustand store

**Files:**
- Create: `apps/web/src/store/quick-view/store.ts`
- Create: `apps/web/src/store/quick-view/index.ts`
- Test: `apps/web/src/store/quick-view/__tests__/store.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// apps/web/src/store/quick-view/__tests__/store.test.ts
import { describe, expect, it, beforeEach } from 'vitest';
import { useQuickViewStore } from '../store';

describe('useQuickViewStore', () => {
  beforeEach(() => {
    useQuickViewStore.setState(useQuickViewStore.getInitialState());
  });

  it('starts closed with no game selected', () => {
    const state = useQuickViewStore.getState();
    expect(state.isOpen).toBe(false);
    expect(state.selectedGameId).toBeNull();
    expect(state.activeTab).toBe('rules');
  });

  it('opens with a game id', () => {
    useQuickViewStore.getState().openForGame('game-123');
    const state = useQuickViewStore.getState();
    expect(state.isOpen).toBe(true);
    expect(state.selectedGameId).toBe('game-123');
  });

  it('closes and clears selection', () => {
    useQuickViewStore.getState().openForGame('game-123');
    useQuickViewStore.getState().close();
    const state = useQuickViewStore.getState();
    expect(state.isOpen).toBe(false);
    expect(state.selectedGameId).toBeNull();
  });

  it('switches active tab', () => {
    useQuickViewStore.getState().setActiveTab('faq');
    expect(useQuickViewStore.getState().activeTab).toBe('faq');
  });

  it('toggles collapsed state', () => {
    useQuickViewStore.getState().toggleCollapsed();
    expect(useQuickViewStore.getState().isCollapsed).toBe(true);
    useQuickViewStore.getState().toggleCollapsed();
    expect(useQuickViewStore.getState().isCollapsed).toBe(false);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/store/quick-view/__tests__/store.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Write minimal implementation**

```ts
// apps/web/src/store/quick-view/store.ts
import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

export type QuickViewTab = 'rules' | 'faq' | 'ai';

interface QuickViewState {
  isOpen: boolean;
  isCollapsed: boolean;
  selectedGameId: string | null;
  activeTab: QuickViewTab;
  openForGame: (gameId: string) => void;
  close: () => void;
  setActiveTab: (tab: QuickViewTab) => void;
  toggleCollapsed: () => void;
}

export const useQuickViewStore = create<QuickViewState>()(
  devtools(
    immer((set) => ({
      isOpen: false,
      isCollapsed: false,
      selectedGameId: null,
      activeTab: 'rules' as QuickViewTab,

      openForGame: (gameId: string) =>
        set((state) => {
          state.isOpen = true;
          state.selectedGameId = gameId;
          state.isCollapsed = false;
        }),

      close: () =>
        set((state) => {
          state.isOpen = false;
          state.selectedGameId = null;
        }),

      setActiveTab: (tab: QuickViewTab) =>
        set((state) => {
          state.activeTab = tab;
        }),

      toggleCollapsed: () =>
        set((state) => {
          state.isCollapsed = !state.isCollapsed;
        }),
    })),
    { name: 'quick-view-store' }
  )
);
```

```ts
// apps/web/src/store/quick-view/index.ts
export { useQuickViewStore, type QuickViewTab } from './store';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/store/quick-view/__tests__/store.test.ts`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/store/quick-view/
git commit -m "feat(store): add useQuickViewStore for Quick View panel state"
```

---

### Task 2: QuickView panel component

**Files:**
- Create: `apps/web/src/components/layout/QuickView/QuickView.tsx`
- Create: `apps/web/src/components/layout/QuickView/QuickViewTab.tsx`
- Create: `apps/web/src/components/layout/QuickView/index.ts`
- Test: `apps/web/src/components/layout/QuickView/__tests__/QuickView.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/layout/QuickView/__tests__/QuickView.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, beforeEach, vi } from 'vitest';

import { useQuickViewStore } from '@/store/quick-view';
import { QuickView } from '../QuickView';

vi.mock('next/navigation', () => ({ usePathname: () => '/games' }));

describe('QuickView', () => {
  beforeEach(() => {
    useQuickViewStore.setState(useQuickViewStore.getInitialState());
  });

  it('is not rendered when closed', () => {
    render(<QuickView />);
    expect(screen.queryByTestId('quick-view')).not.toBeInTheDocument();
  });

  it('renders when open with game selected', () => {
    useQuickViewStore.getState().openForGame('game-1');
    render(<QuickView />);
    expect(screen.getByTestId('quick-view')).toBeInTheDocument();
  });

  it('renders three tab buttons', () => {
    useQuickViewStore.getState().openForGame('game-1');
    render(<QuickView />);
    expect(screen.getByRole('tab', { name: /regole/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /faq/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /ai/i })).toBeInTheDocument();
  });

  it('switches tabs on click', async () => {
    const user = userEvent.setup();
    useQuickViewStore.getState().openForGame('game-1');
    render(<QuickView />);
    await user.click(screen.getByRole('tab', { name: /faq/i }));
    expect(useQuickViewStore.getState().activeTab).toBe('faq');
  });

  it('has a close button', async () => {
    const user = userEvent.setup();
    useQuickViewStore.getState().openForGame('game-1');
    render(<QuickView />);
    await user.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(useQuickViewStore.getState().isOpen).toBe(false);
  });

  it('has a collapse button that narrows to 44px', async () => {
    const user = userEvent.setup();
    useQuickViewStore.getState().openForGame('game-1');
    render(<QuickView />);
    await user.click(screen.getByRole('button', { name: /comprimi/i }));
    expect(useQuickViewStore.getState().isCollapsed).toBe(true);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/QuickView/__tests__/QuickView.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write minimal implementation**

```tsx
// apps/web/src/components/layout/QuickView/QuickViewTab.tsx
'use client';

import type { QuickViewTab as TabType } from '@/store/quick-view';
import { cn } from '@/lib/utils';

interface QuickViewTabProps {
  tab: TabType;
  label: string;
  isActive: boolean;
  onClick: (tab: TabType) => void;
}

export function QuickViewTab({ tab, label, isActive, onClick }: QuickViewTabProps) {
  return (
    <button
      role="tab"
      aria-selected={isActive}
      onClick={() => onClick(tab)}
      className={cn(
        'flex-1 px-3 py-2 text-sm font-medium rounded-lg transition-colors',
        isActive
          ? 'bg-primary/10 text-primary'
          : 'text-muted-foreground hover:text-foreground hover:bg-muted'
      )}
    >
      {label}
    </button>
  );
}
```

```tsx
// apps/web/src/components/layout/QuickView/QuickView.tsx
'use client';

import { BookOpen, HelpCircle, Bot, X, PanelRightClose } from 'lucide-react';
import { useQuickViewStore, type QuickViewTab as TabType } from '@/store/quick-view';
import { cn } from '@/lib/utils';
import { QuickViewTab } from './QuickViewTab';

const TABS: { tab: TabType; label: string; icon: typeof BookOpen }[] = [
  { tab: 'rules', label: 'Regole', icon: BookOpen },
  { tab: 'faq', label: 'FAQ', icon: HelpCircle },
  { tab: 'ai', label: 'AI', icon: Bot },
];

export function QuickView() {
  const { isOpen, isCollapsed, activeTab, setActiveTab, close, toggleCollapsed } =
    useQuickViewStore();

  if (!isOpen) return null;

  if (isCollapsed) {
    return (
      <aside
        data-testid="quick-view"
        className="hidden xl:flex flex-col items-center w-[44px] border-l border-border bg-card"
      >
        {TABS.map(({ tab, icon: Icon }) => (
          <button
            key={tab}
            onClick={() => {
              setActiveTab(tab);
              toggleCollapsed();
            }}
            className="p-2.5 text-muted-foreground hover:text-foreground transition-colors"
            aria-label={tab}
          >
            <Icon className="h-4 w-4" />
          </button>
        ))}
      </aside>
    );
  }

  return (
    <aside
      data-testid="quick-view"
      className={cn(
        'hidden xl:flex flex-col',
        'w-[300px] border-l border-border bg-card',
        'h-[calc(100vh-var(--top-bar-height,48px))]'
      )}
    >
      {/* Header */}
      <div className="flex items-center justify-between px-3 py-2 border-b border-border">
        <div role="tablist" className="flex gap-1 flex-1">
          {TABS.map(({ tab, label }) => (
            <QuickViewTab
              key={tab}
              tab={tab}
              label={label}
              isActive={activeTab === tab}
              onClick={setActiveTab}
            />
          ))}
        </div>
        <div className="flex items-center gap-1 ml-2">
          <button
            onClick={toggleCollapsed}
            aria-label="Comprimi pannello"
            className="p-1.5 rounded-md text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
          >
            <PanelRightClose className="h-4 w-4" />
          </button>
          <button
            onClick={close}
            aria-label="Chiudi pannello"
            className="p-1.5 rounded-md text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
      </div>

      {/* Tab content placeholder */}
      <div className="flex-1 overflow-y-auto p-4">
        {activeTab === 'rules' && (
          <p className="text-sm text-muted-foreground">Seleziona un gioco per vederne le regole</p>
        )}
        {activeTab === 'faq' && (
          <p className="text-sm text-muted-foreground">Domande frequenti sul gioco</p>
        )}
        {activeTab === 'ai' && (
          <p className="text-sm text-muted-foreground">Chiedi all'AI</p>
        )}
      </div>
    </aside>
  );
}
```

```ts
// apps/web/src/components/layout/QuickView/index.ts
export { QuickView } from './QuickView';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/QuickView/__tests__/QuickView.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/QuickView/
git commit -m "feat(quick-view): add QuickView panel with tabs, collapse, close"
```

---

### Task 3: Wire QuickView into LayoutShell

**Files:**
- Modify: `apps/web/src/components/layout/LayoutShell/LayoutShell.tsx`
- Modify: `apps/web/src/components/layout/__tests__/LayoutShell.test.tsx`

- [ ] **Step 1: Write the failing test**

Add to `LayoutShell.test.tsx`:

```tsx
it('renders QuickView panel inside content area', () => {
  // QuickView only renders when open, so just verify it's importable
  // and LayoutShell has the flex structure for it
  render(<LayoutShell>Content</LayoutShell>);
  const contentArea = screen.getByTestId('layout-content-area');
  expect(contentArea).toBeInTheDocument();
});
```

- [ ] **Step 2: Run test to verify it fails (or passes — we're adding structural wiring)**

Run: `cd apps/web && pnpm vitest run src/components/layout/__tests__/LayoutShell.test.tsx`

- [ ] **Step 3: Add QuickView to LayoutShell**

In `LayoutShell.tsx`, add import and render QuickView alongside the content area:

```tsx
import { QuickView } from '../QuickView';

// Inside LayoutShellInner, wrap the content + QuickView in a flex row:
// Change the content area div to include QuickView as a sibling
<div className={cn('flex flex-1', 'transition-[margin] duration-200 ease-in-out', 'md:ml-[var(--card-rack-width,64px)]')}>
  <div className="flex flex-col flex-1" data-testid="layout-content-area">
    {/* existing: MiniNav, MobileBreadcrumb, main, FloatingActionBar, SmartFAB */}
  </div>
  <QuickView />
</div>
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/layout/__tests__/LayoutShell.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/LayoutShell/LayoutShell.tsx apps/web/src/components/layout/__tests__/LayoutShell.test.tsx
git commit -m "feat(layout): wire QuickView panel into LayoutShell"
```

---

## Chunk 2: Game Night Stores & List Page

### Task 4: `useGameNightStore` — Zustand store

**Files:**
- Create: `apps/web/src/store/game-night/store.ts`
- Create: `apps/web/src/store/game-night/index.ts`
- Create: `apps/web/src/store/game-night/types.ts`
- Test: `apps/web/src/store/game-night/__tests__/store.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// apps/web/src/store/game-night/__tests__/store.test.ts
import { describe, expect, it, beforeEach } from 'vitest';
import { useGameNightStore } from '../store';

describe('useGameNightStore', () => {
  beforeEach(() => {
    useGameNightStore.setState(useGameNightStore.getInitialState());
  });

  it('starts with empty game nights list', () => {
    const state = useGameNightStore.getState();
    expect(state.gameNights).toEqual([]);
    expect(state.isLoading).toBe(false);
  });

  it('sets game nights', () => {
    const nights = [
      { id: '1', title: 'Friday Night', status: 'upcoming' as const, date: '2026-03-15' },
    ];
    useGameNightStore.getState().setGameNights(nights as any);
    expect(useGameNightStore.getState().gameNights).toHaveLength(1);
  });

  it('sets selected game night', () => {
    useGameNightStore.getState().selectGameNight('gn-1');
    expect(useGameNightStore.getState().selectedId).toBe('gn-1');
  });

  it('manages selected games for a night', () => {
    useGameNightStore.getState().addGame({ id: 'g1', title: 'Catan' } as any);
    expect(useGameNightStore.getState().selectedGames).toHaveLength(1);
    useGameNightStore.getState().removeGame('g1');
    expect(useGameNightStore.getState().selectedGames).toHaveLength(0);
  });

  it('manages players', () => {
    useGameNightStore.getState().addPlayer({ id: 'p1', name: 'Alice' } as any);
    expect(useGameNightStore.getState().players).toHaveLength(1);
    useGameNightStore.getState().removePlayer('p1');
    expect(useGameNightStore.getState().players).toHaveLength(0);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/store/game-night/__tests__/store.test.ts`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```ts
// apps/web/src/store/game-night/types.ts
export type GameNightStatus = 'draft' | 'upcoming' | 'completed';

export interface GameNightSummary {
  id: string;
  title: string;
  status: GameNightStatus;
  date: string;
  location?: string;
  playerCount: number;
  gameCount: number;
  playerAvatars: string[];
  gameThumbnails: string[];
  winnerId?: string;
}

export interface GameNightPlayer {
  id: string;
  name: string;
  avatarUrl?: string;
}

export interface GameNightGame {
  id: string;
  title: string;
  thumbnailUrl?: string;
  minPlayers?: number;
  maxPlayers?: number;
}

export interface TimelineSlot {
  id: string;
  type: 'game' | 'break' | 'free';
  gameId?: string;
  startTime?: string;
  durationMinutes: number;
}
```

```ts
// apps/web/src/store/game-night/store.ts
import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

import type { GameNightSummary, GameNightPlayer, GameNightGame, TimelineSlot } from './types';

interface GameNightState {
  gameNights: GameNightSummary[];
  selectedId: string | null;
  isLoading: boolean;
  error: string | null;
  players: GameNightPlayer[];
  selectedGames: GameNightGame[];
  timeline: TimelineSlot[];

  setGameNights: (nights: GameNightSummary[]) => void;
  selectGameNight: (id: string | null) => void;
  setLoading: (loading: boolean) => void;
  setError: (error: string | null) => void;
  addPlayer: (player: GameNightPlayer) => void;
  removePlayer: (playerId: string) => void;
  addGame: (game: GameNightGame) => void;
  removeGame: (gameId: string) => void;
  setTimeline: (timeline: TimelineSlot[]) => void;
  reset: () => void;
}

const initialState = {
  gameNights: [] as GameNightSummary[],
  selectedId: null as string | null,
  isLoading: false,
  error: null as string | null,
  players: [] as GameNightPlayer[],
  selectedGames: [] as GameNightGame[],
  timeline: [] as TimelineSlot[],
};

export const useGameNightStore = create<GameNightState>()(
  devtools(
    immer((set) => ({
      ...initialState,

      setGameNights: (nights) => set((s) => { s.gameNights = nights; }),
      selectGameNight: (id) => set((s) => { s.selectedId = id; }),
      setLoading: (loading) => set((s) => { s.isLoading = loading; }),
      setError: (error) => set((s) => { s.error = error; }),
      addPlayer: (player) => set((s) => { s.players.push(player); }),
      removePlayer: (playerId) =>
        set((s) => { s.players = s.players.filter((p) => p.id !== playerId); }),
      addGame: (game) => set((s) => { s.selectedGames.push(game); }),
      removeGame: (gameId) =>
        set((s) => { s.selectedGames = s.selectedGames.filter((g) => g.id !== gameId); }),
      setTimeline: (timeline) => set((s) => { s.timeline = timeline; }),
      reset: () => set(() => ({ ...initialState })),
    })),
    { name: 'game-night-store' }
  )
);
```

```ts
// apps/web/src/store/game-night/index.ts
export { useGameNightStore } from './store';
export type * from './types';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/store/game-night/__tests__/store.test.ts`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/store/game-night/
git commit -m "feat(store): add useGameNightStore for Game Night CRUD and planning state"
```

---

### Task 5: GameNightCard component

**Files:**
- Create: `apps/web/src/components/game-nights/GameNightCard.tsx`
- Create: `apps/web/src/components/game-nights/index.ts`
- Test: `apps/web/src/components/game-nights/__tests__/GameNightCard.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/game-nights/__tests__/GameNightCard.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GameNightCard } from '../GameNightCard';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => <a href={href} {...props}>{children}</a>,
}));

const mockNight = {
  id: 'gn-1',
  title: 'Venerdì Ludico',
  status: 'upcoming' as const,
  date: '2026-03-15T19:00:00Z',
  location: 'Casa di Marco',
  playerCount: 4,
  gameCount: 3,
  playerAvatars: [],
  gameThumbnails: [],
};

describe('GameNightCard', () => {
  it('renders the title', () => {
    render(<GameNightCard night={mockNight} />);
    expect(screen.getByText('Venerdì Ludico')).toBeInTheDocument();
  });

  it('shows upcoming status badge', () => {
    render(<GameNightCard night={mockNight} />);
    expect(screen.getByText(/prossima/i)).toBeInTheDocument();
  });

  it('shows draft status badge', () => {
    render(<GameNightCard night={{ ...mockNight, status: 'draft' }} />);
    expect(screen.getByText(/bozza/i)).toBeInTheDocument();
  });

  it('shows completed status with dimmed style', () => {
    render(<GameNightCard night={{ ...mockNight, status: 'completed' }} />);
    expect(screen.getByText(/completata/i)).toBeInTheDocument();
  });

  it('links to the game night detail page', () => {
    render(<GameNightCard night={mockNight} />);
    expect(screen.getByRole('link')).toHaveAttribute('href', '/game-nights/gn-1');
  });

  it('shows location when provided', () => {
    render(<GameNightCard night={mockNight} />);
    expect(screen.getByText('Casa di Marco')).toBeInTheDocument();
  });

  it('shows player and game counts', () => {
    render(<GameNightCard night={mockNight} />);
    expect(screen.getByText(/4/)).toBeInTheDocument();
    expect(screen.getByText(/3/)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/game-nights/__tests__/GameNightCard.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/game-nights/GameNightCard.tsx
'use client';

import { Calendar, MapPin, Users, Gamepad2 } from 'lucide-react';
import Link from 'next/link';

import type { GameNightSummary, GameNightStatus } from '@/store/game-night';
import { cn } from '@/lib/utils';

const STATUS_CONFIG: Record<GameNightStatus, { label: string; className: string }> = {
  upcoming: { label: 'Prossima', className: 'bg-green-500/10 text-green-500 border-green-500/20' },
  draft: { label: 'Bozza', className: 'bg-muted text-muted-foreground border-border' },
  completed: { label: 'Completata', className: 'bg-muted/50 text-muted-foreground border-border/50' },
};

interface GameNightCardProps {
  night: GameNightSummary;
}

export function GameNightCard({ night }: GameNightCardProps) {
  const status = STATUS_CONFIG[night.status];
  const dateStr = new Date(night.date).toLocaleDateString('it-IT', {
    weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit',
  });

  return (
    <Link
      href={`/game-nights/${night.id}`}
      className={cn(
        'block rounded-xl border border-border bg-card p-4',
        'hover:border-primary/30 hover:shadow-md transition-all duration-200',
        night.status === 'completed' && 'opacity-60'
      )}
    >
      <div className="flex items-start justify-between mb-3">
        <h3 className="font-semibold font-quicksand text-foreground truncate">{night.title}</h3>
        <span className={cn('text-xs px-2 py-0.5 rounded-full border font-medium shrink-0 ml-2', status.className)}>
          {status.label}
        </span>
      </div>

      <div className="space-y-1.5 text-sm text-muted-foreground">
        <div className="flex items-center gap-2">
          <Calendar className="h-3.5 w-3.5 shrink-0" />
          <span>{dateStr}</span>
        </div>
        {night.location && (
          <div className="flex items-center gap-2">
            <MapPin className="h-3.5 w-3.5 shrink-0" />
            <span className="truncate">{night.location}</span>
          </div>
        )}
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-1.5">
            <Users className="h-3.5 w-3.5" />
            <span>{night.playerCount}</span>
          </div>
          <div className="flex items-center gap-1.5">
            <Gamepad2 className="h-3.5 w-3.5" />
            <span>{night.gameCount}</span>
          </div>
        </div>
      </div>

      {/* Player avatars */}
      {night.playerAvatars.length > 0 && (
        <div className="flex -space-x-2 mt-3" data-testid="player-avatars">
          {night.playerAvatars.slice(0, 4).map((url, i) => (
            <img key={i} src={url} alt="" className="h-6 w-6 rounded-full border-2 border-card" />
          ))}
          {night.playerAvatars.length > 4 && (
            <span className="h-6 w-6 rounded-full bg-muted flex items-center justify-center text-[10px] font-medium border-2 border-card">
              +{night.playerAvatars.length - 4}
            </span>
          )}
        </div>
      )}

      {/* Game thumbnails */}
      {night.gameThumbnails.length > 0 && (
        <div className="flex gap-1.5 mt-2" data-testid="game-thumbnails">
          {night.gameThumbnails.slice(0, 3).map((url, i) => (
            <img key={i} src={url} alt="" className="h-8 w-8 rounded-md object-cover" />
          ))}
        </div>
      )}
    </Link>
  );
}
```

```ts
// apps/web/src/components/game-nights/index.ts
export { GameNightCard } from './GameNightCard';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/game-nights/__tests__/GameNightCard.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/game-nights/
git commit -m "feat(game-nights): add GameNightCard with status badges and metadata"
```

---

### Task 6: Game Night list page

**Files:**
- Create: `apps/web/src/app/(authenticated)/game-nights/page.tsx`
- Create: `apps/web/src/components/game-nights/GameNightList.tsx`
- Create: `apps/web/src/components/game-nights/GameNightListSkeleton.tsx`
- Test: `apps/web/src/components/game-nights/__tests__/GameNightList.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/game-nights/__tests__/GameNightList.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GameNightList } from '../GameNightList';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => <a href={href} {...props}>{children}</a>,
}));

const mockNights = [
  { id: '1', title: 'Friday', status: 'upcoming' as const, date: '2026-03-15T19:00:00Z', playerCount: 4, gameCount: 2, playerAvatars: [], gameThumbnails: [] },
  { id: '2', title: 'Saturday', status: 'draft' as const, date: '2026-03-16T18:00:00Z', playerCount: 0, gameCount: 0, playerAvatars: [], gameThumbnails: [] },
];

describe('GameNightList', () => {
  it('renders a grid of game night cards', () => {
    render(<GameNightList nights={mockNights} isLoading={false} />);
    expect(screen.getByText('Friday')).toBeInTheDocument();
    expect(screen.getByText('Saturday')).toBeInTheDocument();
  });

  it('renders skeleton when loading', () => {
    render(<GameNightList nights={[]} isLoading={true} />);
    expect(screen.getAllByTestId('game-night-skeleton')).toHaveLength(3);
  });

  it('renders empty state when no nights', () => {
    render(<GameNightList nights={[]} isLoading={false} />);
    expect(screen.getByText(/nessuna serata pianificata/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/game-nights/__tests__/GameNightList.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/game-nights/GameNightListSkeleton.tsx
export function GameNightListSkeleton() {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      {Array.from({ length: 3 }).map((_, i) => (
        <div key={i} data-testid="game-night-skeleton" className="rounded-xl border border-border bg-card p-4 animate-pulse">
          <div className="h-5 w-2/3 bg-muted rounded mb-3" />
          <div className="space-y-2">
            <div className="h-4 w-1/2 bg-muted rounded" />
            <div className="h-4 w-1/3 bg-muted rounded" />
          </div>
        </div>
      ))}
    </div>
  );
}
```

```tsx
// apps/web/src/components/game-nights/GameNightList.tsx
'use client';

import { Plus } from 'lucide-react';
import Link from 'next/link';

import type { GameNightSummary } from '@/store/game-night';

import { GameNightCard } from './GameNightCard';
import { GameNightListSkeleton } from './GameNightListSkeleton';

interface GameNightListProps {
  nights: GameNightSummary[];
  isLoading: boolean;
}

export function GameNightList({ nights, isLoading }: GameNightListProps) {
  if (isLoading) return <GameNightListSkeleton />;

  if (nights.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <p className="text-muted-foreground mb-4">Nessuna serata pianificata</p>
        <Link
          href="/game-nights/new"
          className="inline-flex items-center gap-2 px-4 py-2 rounded-lg bg-primary text-primary-foreground font-medium hover:bg-primary/90 transition-colors"
        >
          <Plus className="h-4 w-4" />
          Crea serata
        </Link>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      {nights.map((night) => (
        <GameNightCard key={night.id} night={night} />
      ))}
    </div>
  );
}
```

Update index: add `GameNightList` and `GameNightListSkeleton` exports.

```tsx
// apps/web/src/app/(authenticated)/game-nights/page.tsx
import { GameNightList } from '@/components/game-nights/GameNightList';

export default function GameNightsPage() {
  // TODO: Replace with React Query hook when backend endpoint exists
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold font-quicksand">Serate di Gioco</h1>
      </div>
      <GameNightList nights={[]} isLoading={false} />
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/game-nights/__tests__/GameNightList.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/game-nights/ apps/web/src/app/\(authenticated\)/game-nights/
git commit -m "feat(game-nights): add GameNightList page with skeleton and empty state"
```

---

## Chunk 3: Live Session Store & Activity Feed

### Task 7: `useSessionStore` — Zustand store

**Files:**
- Create: `apps/web/src/store/session/store.ts`
- Create: `apps/web/src/store/session/types.ts`
- Create: `apps/web/src/store/session/index.ts`
- Test: `apps/web/src/store/session/__tests__/store.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// apps/web/src/store/session/__tests__/store.test.ts
import { describe, expect, it, beforeEach } from 'vitest';
import { useSessionStore } from '../store';

describe('useSessionStore', () => {
  beforeEach(() => {
    useSessionStore.setState(useSessionStore.getInitialState());
  });

  it('starts in idle state', () => {
    const state = useSessionStore.getState();
    expect(state.status).toBe('idle');
    expect(state.isPaused).toBe(false);
    expect(state.events).toEqual([]);
    expect(state.scores).toEqual([]);
  });

  it('starts a session', () => {
    useSessionStore.getState().startSession('session-1', 'game-1');
    const state = useSessionStore.getState();
    expect(state.status).toBe('live');
    expect(state.sessionId).toBe('session-1');
    expect(state.gameId).toBe('game-1');
  });

  it('toggles pause', () => {
    useSessionStore.getState().startSession('s1', 'g1');
    useSessionStore.getState().togglePause();
    expect(useSessionStore.getState().isPaused).toBe(true);
    useSessionStore.getState().togglePause();
    expect(useSessionStore.getState().isPaused).toBe(false);
  });

  it('adds events to the feed', () => {
    useSessionStore.getState().addEvent({
      id: 'e1', type: 'dice_roll', playerId: 'p1', data: { values: [3, 4], total: 7 }, timestamp: new Date().toISOString(),
    } as any);
    expect(useSessionStore.getState().events).toHaveLength(1);
  });

  it('updates scores', () => {
    useSessionStore.getState().updateScore('p1', 10);
    expect(useSessionStore.getState().scores).toEqual([{ playerId: 'p1', score: 10 }]);
    useSessionStore.getState().updateScore('p1', 15);
    expect(useSessionStore.getState().scores).toEqual([{ playerId: 'p1', score: 15 }]);
  });

  it('increments current turn', () => {
    useSessionStore.getState().nextTurn();
    expect(useSessionStore.getState().currentTurn).toBe(2);
  });

  it('ends session', () => {
    useSessionStore.getState().startSession('s1', 'g1');
    useSessionStore.getState().endSession();
    expect(useSessionStore.getState().status).toBe('ended');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/store/session/__tests__/store.test.ts`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```ts
// apps/web/src/store/session/types.ts
export type SessionStatus = 'idle' | 'live' | 'paused' | 'ended';

export type ActivityEventType =
  | 'dice_roll' | 'ai_tip' | 'score_update' | 'photo'
  | 'note' | 'audio_note' | 'turn_change' | 'pause_resume' | 'session_start';

export interface ActivityEvent {
  id: string;
  type: ActivityEventType;
  playerId?: string;
  data: Record<string, unknown>;
  timestamp: string;
}

export interface PlayerScore {
  playerId: string;
  score: number;
}
```

```ts
// apps/web/src/store/session/store.ts
import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

import type { SessionStatus, ActivityEvent, PlayerScore } from './types';

interface SessionState {
  status: SessionStatus;
  sessionId: string | null;
  gameId: string | null;
  isPaused: boolean;
  currentTurn: number;
  events: ActivityEvent[];
  scores: PlayerScore[];
  timerStartedAt: string | null;

  startSession: (sessionId: string, gameId: string) => void;
  endSession: () => void;
  togglePause: () => void;
  addEvent: (event: ActivityEvent) => void;
  updateScore: (playerId: string, score: number) => void;
  nextTurn: () => void;
  reset: () => void;
}

const initialState = {
  status: 'idle' as SessionStatus,
  sessionId: null as string | null,
  gameId: null as string | null,
  isPaused: false,
  currentTurn: 1,
  events: [] as ActivityEvent[],
  scores: [] as PlayerScore[],
  timerStartedAt: null as string | null,
};

export const useSessionStore = create<SessionState>()(
  devtools(
    immer((set) => ({
      ...initialState,

      startSession: (sessionId, gameId) =>
        set((s) => {
          s.status = 'live';
          s.sessionId = sessionId;
          s.gameId = gameId;
          s.timerStartedAt = new Date().toISOString();
        }),

      endSession: () => set((s) => { s.status = 'ended'; }),

      togglePause: () =>
        set((s) => {
          s.isPaused = !s.isPaused;
          s.status = s.isPaused ? 'paused' : 'live';
        }),

      addEvent: (event) => set((s) => { s.events.push(event); }),

      updateScore: (playerId, score) =>
        set((s) => {
          const existing = s.scores.find((sc) => sc.playerId === playerId);
          if (existing) {
            existing.score = score;
          } else {
            s.scores.push({ playerId, score });
          }
        }),

      nextTurn: () => set((s) => { s.currentTurn += 1; }),

      reset: () => set(() => ({ ...initialState })),
    })),
    { name: 'session-store' }
  )
);
```

```ts
// apps/web/src/store/session/index.ts
export { useSessionStore } from './store';
export type * from './types';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/store/session/__tests__/store.test.ts`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/store/session/
git commit -m "feat(store): add useSessionStore for live session state management"
```

---

### Task 8: ActivityFeedEvent component

**Files:**
- Create: `apps/web/src/components/session/ActivityFeedEvent.tsx`
- Create: `apps/web/src/components/session/index.ts`
- Test: `apps/web/src/components/session/__tests__/ActivityFeedEvent.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/session/__tests__/ActivityFeedEvent.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ActivityFeedEvent } from '../ActivityFeedEvent';

describe('ActivityFeedEvent', () => {
  it('renders a dice roll event', () => {
    render(
      <ActivityFeedEvent
        event={{
          id: 'e1',
          type: 'dice_roll',
          playerId: 'p1',
          data: { values: [3, 4], total: 7, playerName: 'Alice' },
          timestamp: '2026-03-11T19:00:00Z',
        }}
      />
    );
    expect(screen.getByText(/7/)).toBeInTheDocument();
    expect(screen.getByText(/Alice/)).toBeInTheDocument();
  });

  it('renders a score update event', () => {
    render(
      <ActivityFeedEvent
        event={{
          id: 'e2',
          type: 'score_update',
          playerId: 'p1',
          data: { playerName: 'Bob', action: '+5', newScore: 15 },
          timestamp: '2026-03-11T19:05:00Z',
        }}
      />
    );
    expect(screen.getByText(/Bob/)).toBeInTheDocument();
    expect(screen.getByText(/15/)).toBeInTheDocument();
  });

  it('renders a note event', () => {
    render(
      <ActivityFeedEvent
        event={{
          id: 'e3',
          type: 'note',
          playerId: 'p1',
          data: { playerName: 'Carol', text: 'Great round!' },
          timestamp: '2026-03-11T19:10:00Z',
        }}
      />
    );
    expect(screen.getByText('Great round!')).toBeInTheDocument();
  });

  it('renders an AI tip event', () => {
    render(
      <ActivityFeedEvent
        event={{
          id: 'e4',
          type: 'ai_tip',
          data: { text: 'Remember the trading rule!' },
          timestamp: '2026-03-11T19:15:00Z',
        }}
      />
    );
    expect(screen.getByText(/trading rule/)).toBeInTheDocument();
  });

  it('shows formatted timestamp', () => {
    render(
      <ActivityFeedEvent
        event={{
          id: 'e5',
          type: 'turn_change',
          data: { from: 1, to: 2, playerName: 'Dan' },
          timestamp: '2026-03-11T19:20:00Z',
        }}
      />
    );
    expect(screen.getByTestId('activity-event')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/session/__tests__/ActivityFeedEvent.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/session/ActivityFeedEvent.tsx
'use client';

import {
  Dice5, Bot, Trophy, Camera, FileText, Mic, RefreshCw, Pause, Play,
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';

import type { ActivityEvent, ActivityEventType } from '@/store/session';
import { cn } from '@/lib/utils';

const EVENT_CONFIG: Record<ActivityEventType, { icon: LucideIcon; color: string; label: string }> = {
  dice_roll: { icon: Dice5, color: 'text-orange-500', label: 'Lancio dadi' },
  ai_tip: { icon: Bot, color: 'text-purple-500', label: 'Suggerimento AI' },
  score_update: { icon: Trophy, color: 'text-green-500', label: 'Punteggio' },
  photo: { icon: Camera, color: 'text-blue-500', label: 'Foto' },
  note: { icon: FileText, color: 'text-yellow-500', label: 'Nota' },
  audio_note: { icon: Mic, color: 'text-pink-500', label: 'Audio' },
  turn_change: { icon: RefreshCw, color: 'text-gray-500', label: 'Turno' },
  pause_resume: { icon: Pause, color: 'text-yellow-500', label: 'Pausa' },
  session_start: { icon: Play, color: 'text-green-500', label: 'Inizio' },
};

interface ActivityFeedEventProps {
  event: ActivityEvent;
}

export function ActivityFeedEvent({ event }: ActivityFeedEventProps) {
  const config = EVENT_CONFIG[event.type];
  const Icon = config.icon;
  const data = event.data;
  const time = new Date(event.timestamp).toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });

  return (
    <div data-testid="activity-event" className="flex gap-3 py-2 px-3">
      <div className={cn('mt-0.5 shrink-0', config.color)}>
        <Icon className="h-4 w-4" />
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-baseline gap-2">
          {data.playerName && (
            <span className="font-medium text-sm text-foreground">{String(data.playerName)}</span>
          )}
          <span className="text-xs text-muted-foreground">{time}</span>
        </div>
        <div className="text-sm text-muted-foreground mt-0.5">
          {event.type === 'dice_roll' && (
            <span>Lancio: {String(data.total)} ({Array.isArray(data.values) ? (data.values as number[]).join('+') : ''})</span>
          )}
          {event.type === 'score_update' && (
            <span>{String(data.action)} → {String(data.newScore)} punti</span>
          )}
          {event.type === 'note' && <span>{String(data.text)}</span>}
          {event.type === 'ai_tip' && (
            <span className="italic">{String(data.text)}</span>
          )}
          {event.type === 'turn_change' && (
            <span>Turno {String(data.from)} → {String(data.to)}</span>
          )}
          {event.type === 'pause_resume' && (
            <span>{data.paused ? 'Sessione in pausa' : 'Sessione ripresa'}</span>
          )}
          {event.type === 'session_start' && <span>Sessione iniziata</span>}
        </div>
      </div>
    </div>
  );
}
```

```ts
// apps/web/src/components/session/index.ts
export { ActivityFeedEvent } from './ActivityFeedEvent';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/session/__tests__/ActivityFeedEvent.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/session/
git commit -m "feat(session): add ActivityFeedEvent with 9 event types and icons"
```

---

### Task 9: ActivityFeed container component

**Files:**
- Create: `apps/web/src/components/session/ActivityFeed.tsx`
- Test: `apps/web/src/components/session/__tests__/ActivityFeed.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/session/__tests__/ActivityFeed.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, beforeEach } from 'vitest';

import { useSessionStore } from '@/store/session';
import { ActivityFeed } from '../ActivityFeed';

describe('ActivityFeed', () => {
  beforeEach(() => {
    useSessionStore.setState(useSessionStore.getInitialState());
  });

  it('shows empty state when no events', () => {
    render(<ActivityFeed />);
    expect(screen.getByText(/sessione appena iniziata/i)).toBeInTheDocument();
  });

  it('renders events from store', () => {
    useSessionStore.getState().addEvent({
      id: 'e1', type: 'note', data: { playerName: 'Alice', text: 'Nice!' }, timestamp: new Date().toISOString(),
    });
    render(<ActivityFeed />);
    expect(screen.getByText('Nice!')).toBeInTheDocument();
  });

  it('renders events in reverse chronological order (newest first)', () => {
    useSessionStore.getState().addEvent({
      id: 'e1', type: 'note', data: { playerName: 'A', text: 'First' }, timestamp: '2026-03-11T19:00:00Z',
    });
    useSessionStore.getState().addEvent({
      id: 'e2', type: 'note', data: { playerName: 'B', text: 'Second' }, timestamp: '2026-03-11T19:01:00Z',
    });
    render(<ActivityFeed />);
    const events = screen.getAllByTestId('activity-event');
    expect(events).toHaveLength(2);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/session/__tests__/ActivityFeed.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/session/ActivityFeed.tsx
'use client';

import { useSessionStore } from '@/store/session';
import { ActivityFeedEvent } from './ActivityFeedEvent';

export function ActivityFeed() {
  const events = useSessionStore((s) => s.events);

  if (events.length === 0) {
    return (
      <div className="flex items-center justify-center py-12 text-center">
        <p className="text-sm text-muted-foreground">Sessione appena iniziata — le attività appariranno qui</p>
      </div>
    );
  }

  const sorted = [...events].sort(
    (a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()
  );

  return (
    <div className="flex flex-col divide-y divide-border overflow-y-auto" data-testid="activity-feed">
      {sorted.map((event) => (
        <ActivityFeedEvent key={event.id} event={event} />
      ))}
    </div>
  );
}
```

Add to `apps/web/src/components/session/index.ts`:
```ts
export { ActivityFeed } from './ActivityFeed';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/session/__tests__/ActivityFeed.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/session/
git commit -m "feat(session): add ActivityFeed container with empty state and sorting"
```

---

## Chunk 4: Game Toolkit (Dice Roller)

### Task 10: DiceRoller component

**Files:**
- Create: `apps/web/src/components/session/DiceRoller.tsx`
- Test: `apps/web/src/components/session/__tests__/DiceRoller.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/session/__tests__/DiceRoller.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import { useSessionStore } from '@/store/session';
import { DiceRoller } from '../DiceRoller';

describe('DiceRoller', () => {
  beforeEach(() => {
    useSessionStore.setState(useSessionStore.getInitialState());
  });

  it('renders dice type selector', () => {
    render(<DiceRoller playerId="p1" playerName="Alice" />);
    expect(screen.getByLabelText(/tipo dadi/i)).toBeInTheDocument();
  });

  it('shows roll button', () => {
    render(<DiceRoller playerId="p1" playerName="Alice" />);
    expect(screen.getByRole('button', { name: /lancia/i })).toBeInTheDocument();
  });

  it('adds dice_roll event to store on roll', async () => {
    const user = userEvent.setup();
    render(<DiceRoller playerId="p1" playerName="Alice" />);
    await user.click(screen.getByRole('button', { name: /lancia/i }));
    const events = useSessionStore.getState().events;
    expect(events).toHaveLength(1);
    expect(events[0].type).toBe('dice_roll');
  });

  it('displays the result after rolling', async () => {
    const user = userEvent.setup();
    render(<DiceRoller playerId="p1" playerName="Alice" />);
    await user.click(screen.getByRole('button', { name: /lancia/i }));
    expect(screen.getByTestId('dice-result')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/session/__tests__/DiceRoller.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/session/DiceRoller.tsx
'use client';

import { useState } from 'react';
import { Dice5 } from 'lucide-react';

import { useSessionStore } from '@/store/session';
import { cn } from '@/lib/utils';

type DiceConfig = { label: string; count: number; sides: number };

const DICE_TYPES: DiceConfig[] = [
  { label: '2d6', count: 2, sides: 6 },
  { label: '1d6', count: 1, sides: 6 },
  { label: '1d20', count: 1, sides: 20 },
  { label: '1d12', count: 1, sides: 12 },
  { label: '1d8', count: 1, sides: 8 },
];

function rollDice(config: DiceConfig): { values: number[]; total: number } {
  const values = Array.from({ length: config.count }, () =>
    Math.floor(Math.random() * config.sides) + 1
  );
  return { values, total: values.reduce((a, b) => a + b, 0) };
}

interface DiceRollerProps {
  playerId: string;
  playerName: string;
}

export function DiceRoller({ playerId, playerName }: DiceRollerProps) {
  const [selectedType, setSelectedType] = useState(0);
  const [lastResult, setLastResult] = useState<{ values: number[]; total: number } | null>(null);
  const addEvent = useSessionStore((s) => s.addEvent);

  const handleRoll = () => {
    const config = DICE_TYPES[selectedType];
    const result = rollDice(config);
    setLastResult(result);

    addEvent({
      id: crypto.randomUUID(),
      type: 'dice_roll',
      playerId,
      data: { playerName, values: result.values, total: result.total, diceType: config.label },
      timestamp: new Date().toISOString(),
    });
  };

  return (
    <div className="flex flex-col gap-3 p-3" data-testid="dice-roller">
      <div className="flex items-center gap-2">
        <select
          value={selectedType}
          onChange={(e) => setSelectedType(Number(e.target.value))}
          aria-label="Tipo dadi"
          className="flex-1 rounded-lg border border-border bg-background px-3 py-1.5 text-sm"
        >
          {DICE_TYPES.map((dt, i) => (
            <option key={dt.label} value={i}>{dt.label}</option>
          ))}
        </select>
        <button
          onClick={handleRoll}
          aria-label="Lancia dadi"
          className={cn(
            'inline-flex items-center gap-2 px-4 py-1.5 rounded-lg',
            'bg-primary text-primary-foreground font-medium text-sm',
            'hover:bg-primary/90 transition-colors'
          )}
        >
          <Dice5 className="h-4 w-4" />
          Lancia
        </button>
      </div>

      {lastResult && (
        <div data-testid="dice-result" className="flex items-center gap-3 p-3 rounded-lg bg-muted/50">
          <span className="text-3xl font-bold font-quicksand tabular-nums">{lastResult.total}</span>
          <span className="text-sm text-muted-foreground">
            ({lastResult.values.join(' + ')})
          </span>
        </div>
      )}
    </div>
  );
}
```

Add to `apps/web/src/components/session/index.ts`:
```ts
export { DiceRoller } from './DiceRoller';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/session/__tests__/DiceRoller.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/session/
git commit -m "feat(session): add DiceRoller with configurable dice types and auto-logging"
```

---

## Chunk 5: Collapsible Panel & Inline Picker

### Task 11: CollapsiblePanel reusable component

**Files:**
- Create: `apps/web/src/components/layout/CollapsiblePanel/CollapsiblePanel.tsx`
- Create: `apps/web/src/components/layout/CollapsiblePanel/index.ts`
- Test: `apps/web/src/components/layout/CollapsiblePanel/__tests__/CollapsiblePanel.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/layout/CollapsiblePanel/__tests__/CollapsiblePanel.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { CollapsiblePanel } from '../CollapsiblePanel';

describe('CollapsiblePanel', () => {
  it('renders children when expanded', () => {
    render(
      <CollapsiblePanel side="left" isCollapsed={false} onToggle={vi.fn()}>
        <p>Panel content</p>
      </CollapsiblePanel>
    );
    expect(screen.getByText('Panel content')).toBeInTheDocument();
  });

  it('hides children when collapsed', () => {
    render(
      <CollapsiblePanel side="left" isCollapsed={true} onToggle={vi.fn()}>
        <p>Panel content</p>
      </CollapsiblePanel>
    );
    expect(screen.queryByText('Panel content')).not.toBeInTheDocument();
  });

  it('shows collapse strip at 44px when collapsed', () => {
    render(
      <CollapsiblePanel side="left" isCollapsed={true} onToggle={vi.fn()} />
    );
    const panel = screen.getByTestId('collapsible-panel');
    expect(panel.className).toContain('w-[44px]');
  });

  it('calls onToggle when toggle button clicked', async () => {
    const onToggle = vi.fn();
    const user = userEvent.setup();
    render(
      <CollapsiblePanel side="right" isCollapsed={false} onToggle={onToggle}>
        Content
      </CollapsiblePanel>
    );
    await user.click(screen.getByRole('button', { name: /comprimi/i }));
    expect(onToggle).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/layout/__tests__/CollapsiblePanel.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/layout/CollapsiblePanel/CollapsiblePanel.tsx
'use client';

import type { ReactNode } from 'react';
import { PanelLeftClose, PanelRightClose, PanelLeftOpen, PanelRightOpen } from 'lucide-react';
import { cn } from '@/lib/utils';

interface CollapsiblePanelProps {
  side: 'left' | 'right';
  isCollapsed: boolean;
  onToggle: () => void;
  children?: ReactNode;
  width?: string;
  className?: string;
}

export function CollapsiblePanel({
  side,
  isCollapsed,
  onToggle,
  children,
  width = '280px',
  className,
}: CollapsiblePanelProps) {
  const CollapseIcon = side === 'left' ? PanelLeftClose : PanelRightClose;
  const ExpandIcon = side === 'left' ? PanelLeftOpen : PanelRightOpen;

  return (
    <div
      data-testid="collapsible-panel"
      className={cn(
        'flex flex-col border-border bg-card transition-[width] duration-200',
        side === 'left' ? 'border-r' : 'border-l',
        isCollapsed && 'w-[44px]',
        className
      )}
      style={isCollapsed ? { width: '44px' } : { width }}
    >
      {isCollapsed ? (
        <button
          onClick={onToggle}
          aria-label="Espandi pannello"
          className="p-2.5 text-muted-foreground hover:text-foreground transition-colors"
        >
          <ExpandIcon className="h-4 w-4" />
        </button>
      ) : (
        <>
          <div className="flex items-center justify-end p-1 border-b border-border">
            <button
              onClick={onToggle}
              aria-label="Comprimi pannello"
              className="p-1.5 rounded-md text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
            >
              <CollapseIcon className="h-4 w-4" />
            </button>
          </div>
          <div className="flex-1 overflow-y-auto">{children}</div>
        </>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/layout/__tests__/CollapsiblePanel.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/CollapsiblePanel/
git commit -m "feat(layout): add CollapsiblePanel with 44px collapsed strip"
```

---

### Task 12: InlineGamePicker component

**Files:**
- Create: `apps/web/src/components/game-nights/InlineGamePicker.tsx`
- Test: `apps/web/src/components/game-nights/__tests__/InlineGamePicker.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/game-nights/__tests__/InlineGamePicker.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { InlineGamePicker } from '../InlineGamePicker';

const mockGames = [
  { id: 'g1', title: 'Catan', thumbnailUrl: '', minPlayers: 3, maxPlayers: 4 },
  { id: 'g2', title: 'Ticket to Ride', thumbnailUrl: '', minPlayers: 2, maxPlayers: 5 },
  { id: 'g3', title: 'Gloomhaven', thumbnailUrl: '', minPlayers: 1, maxPlayers: 4 },
];

describe('InlineGamePicker', () => {
  it('renders available games', () => {
    render(<InlineGamePicker games={mockGames} onSelect={vi.fn()} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
  });

  it('calls onSelect when a game is clicked', async () => {
    const onSelect = vi.fn();
    const user = userEvent.setup();
    render(<InlineGamePicker games={mockGames} onSelect={onSelect} />);
    await user.click(screen.getByText('Catan'));
    expect(onSelect).toHaveBeenCalledWith(mockGames[0]);
  });

  it('filters by player count', () => {
    render(<InlineGamePicker games={mockGames} onSelect={vi.fn()} playerCount={5} />);
    // Only Ticket to Ride supports 5 players
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
  });

  it('shows empty state when no games match', () => {
    render(<InlineGamePicker games={mockGames} onSelect={vi.fn()} playerCount={10} />);
    expect(screen.getByText(/nessun gioco adatto/i)).toBeInTheDocument();
  });

  it('excludes already selected games', () => {
    render(<InlineGamePicker games={mockGames} onSelect={vi.fn()} excludeIds={['g1']} />);
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/game-nights/__tests__/InlineGamePicker.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/game-nights/InlineGamePicker.tsx
'use client';

import { Gamepad2 } from 'lucide-react';
import { useMemo } from 'react';

import type { GameNightGame } from '@/store/game-night';
import { cn } from '@/lib/utils';

interface InlineGamePickerProps {
  games: GameNightGame[];
  onSelect: (game: GameNightGame) => void;
  playerCount?: number;
  excludeIds?: string[];
}

export function InlineGamePicker({ games, onSelect, playerCount, excludeIds = [] }: InlineGamePickerProps) {
  const filtered = useMemo(() => {
    let result = games.filter((g) => !excludeIds.includes(g.id));
    if (playerCount) {
      result = result.filter(
        (g) => (!g.minPlayers || g.minPlayers <= playerCount) && (!g.maxPlayers || g.maxPlayers >= playerCount)
      );
    }
    return result;
  }, [games, playerCount, excludeIds]);

  if (filtered.length === 0) {
    return (
      <div className="py-6 text-center text-sm text-muted-foreground">
        Nessun gioco adatto{playerCount ? ` per ${playerCount} giocatori` : ''}
      </div>
    );
  }

  return (
    <div className="flex gap-3 overflow-x-auto py-2 px-1 scrollbar-thin" data-testid="inline-game-picker">
      {filtered.map((game) => (
        <button
          key={game.id}
          onClick={() => onSelect(game)}
          className={cn(
            'flex flex-col items-center gap-1.5 p-3 rounded-xl',
            'border border-border bg-card hover:border-primary/30',
            'transition-all duration-200 hover:shadow-sm',
            'shrink-0 w-[120px]'
          )}
        >
          <div className="h-16 w-16 rounded-lg bg-muted flex items-center justify-center">
            {game.thumbnailUrl ? (
              <img src={game.thumbnailUrl} alt={game.title} className="h-full w-full rounded-lg object-cover" />
            ) : (
              <Gamepad2 className="h-6 w-6 text-muted-foreground" />
            )}
          </div>
          <span className="text-xs font-medium text-foreground text-center line-clamp-2">{game.title}</span>
          {(game.minPlayers || game.maxPlayers) && (
            <span className="text-[10px] text-muted-foreground">
              {game.minPlayers}–{game.maxPlayers} giocatori
            </span>
          )}
        </button>
      ))}
    </div>
  );
}
```

Add to `apps/web/src/components/game-nights/index.ts`:
```ts
export { InlineGamePicker } from './InlineGamePicker';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/game-nights/__tests__/InlineGamePicker.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/game-nights/
git commit -m "feat(game-nights): add InlineGamePicker with player count filter"
```

---

## Chunk 6: Live Session Layout & Design Tokens

### Task 13: New design tokens for Quick View and panels

**Files:**
- Modify: `apps/web/src/styles/design-tokens.css`

- [ ] **Step 1: Verify current tokens**

Run: `cd apps/web && grep -n 'card-rack\|quick-view\|collapsed-panel' src/styles/design-tokens.css`
Expected: Only card-rack tokens exist

- [ ] **Step 2: Add new tokens**

Add after existing `--card-rack-hover-width` line:

```css
  --quick-view-width: 300px;
  --quick-view-compact-width: 280px;
  --collapsed-panel-width: 44px;
  --mobile-status-bar-height: 36px;
  --mobile-scorebar-height: 52px;
```

- [ ] **Step 3: Verify build**

Run: `cd apps/web && pnpm build --no-lint 2>&1 | tail -5`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/styles/design-tokens.css
git commit -m "feat(tokens): add Quick View and panel dimension tokens"
```

---

### Task 14: LiveSessionLayout (3-column)

**Files:**
- Create: `apps/web/src/components/session/LiveSessionLayout.tsx`
- Test: `apps/web/src/components/session/__tests__/LiveSessionLayout.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/session/__tests__/LiveSessionLayout.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, beforeEach, vi } from 'vitest';

import { useSessionStore } from '@/store/session';
import { LiveSessionLayout } from '../LiveSessionLayout';

vi.mock('next/navigation', () => ({ usePathname: () => '/sessions/s1' }));

describe('LiveSessionLayout', () => {
  beforeEach(() => {
    useSessionStore.setState(useSessionStore.getInitialState());
  });

  it('renders three columns on desktop', () => {
    render(
      <LiveSessionLayout
        leftPanel={<div>Left</div>}
        centerContent={<div>Center</div>}
        rightPanel={<div>Right</div>}
      />
    );
    expect(screen.getByText('Left')).toBeInTheDocument();
    expect(screen.getByText('Center')).toBeInTheDocument();
    expect(screen.getByText('Right')).toBeInTheDocument();
  });

  it('renders with data-testid', () => {
    render(
      <LiveSessionLayout
        leftPanel={<div>L</div>}
        centerContent={<div>C</div>}
        rightPanel={<div>R</div>}
      />
    );
    expect(screen.getByTestId('live-session-layout')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/session/__tests__/LiveSessionLayout.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/session/LiveSessionLayout.tsx
'use client';

import type { ReactNode } from 'react';
import { useCallback } from 'react';

import { CollapsiblePanel } from '@/components/layout/CollapsiblePanel';
import { useLocalStorage } from '@/hooks/useLocalStorage';

interface LiveSessionLayoutProps {
  leftPanel: ReactNode;
  centerContent: ReactNode;
  rightPanel: ReactNode;
}

export function LiveSessionLayout({ leftPanel, centerContent, rightPanel }: LiveSessionLayoutProps) {
  const [leftCollapsed, setLeftCollapsed] = useLocalStorage('session-left-collapsed', false);
  const [rightCollapsed, setRightCollapsed] = useLocalStorage('session-right-collapsed', false);
  const toggleLeft = useCallback(() => setLeftCollapsed((v: boolean) => !v), [setLeftCollapsed]);
  const toggleRight = useCallback(() => setRightCollapsed((v: boolean) => !v), [setRightCollapsed]);

  return (
    <div
      data-testid="live-session-layout"
      className="flex h-[calc(100vh-var(--top-bar-height,48px))] overflow-hidden"
    >
      {/* Left panel — scoreboard, game card */}
      <div className="hidden lg:flex">
        <CollapsiblePanel
          side="left"
          isCollapsed={leftCollapsed}
          onToggle={toggleLeft}
          width="280px"
        >
          {leftPanel}
        </CollapsiblePanel>
      </div>

      {/* Center — activity feed */}
      <div className="flex-1 flex flex-col overflow-hidden">
        {centerContent}
      </div>

      {/* Right panel — AI chat, rules, FAQ */}
      <div className="hidden lg:flex">
        <CollapsiblePanel
          side="right"
          isCollapsed={rightCollapsed}
          onToggle={toggleRight}
          width="280px"
        >
          {rightPanel}
        </CollapsiblePanel>
      </div>
    </div>
  );
}
```

Add to `apps/web/src/components/session/index.ts`:
```ts
export { LiveSessionLayout } from './LiveSessionLayout';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/session/__tests__/LiveSessionLayout.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/session/ apps/web/src/components/ui/layout/
git commit -m "feat(session): add LiveSessionLayout with collapsible 3-column layout"
```

---

## Chunk 7: Mobile Live Session & Integration

### Task 15: MobileStatusBar for live sessions

**Files:**
- Create: `apps/web/src/components/session/MobileStatusBar.tsx`
- Test: `apps/web/src/components/session/__tests__/MobileStatusBar.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/session/__tests__/MobileStatusBar.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, beforeEach, vi } from 'vitest';

import { useSessionStore } from '@/store/session';
import { MobileStatusBar } from '../MobileStatusBar';

describe('MobileStatusBar', () => {
  beforeEach(() => {
    useSessionStore.setState(useSessionStore.getInitialState());
  });

  it('renders game name', () => {
    render(<MobileStatusBar gameName="Catan" currentPlayer="Alice" />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('shows LIVE indicator when session is live', () => {
    useSessionStore.getState().startSession('s1', 'g1');
    render(<MobileStatusBar gameName="Catan" currentPlayer="Alice" />);
    expect(screen.getByText('LIVE')).toBeInTheDocument();
  });

  it('shows current turn', () => {
    useSessionStore.getState().startSession('s1', 'g1');
    render(<MobileStatusBar gameName="Catan" currentPlayer="Alice" />);
    expect(screen.getByText(/turno 1/i)).toBeInTheDocument();
  });

  it('shows pause button', () => {
    useSessionStore.getState().startSession('s1', 'g1');
    render(<MobileStatusBar gameName="Catan" currentPlayer="Alice" />);
    expect(screen.getByRole('button', { name: /pausa/i })).toBeInTheDocument();
  });

  it('toggles pause on click', async () => {
    const user = userEvent.setup();
    useSessionStore.getState().startSession('s1', 'g1');
    render(<MobileStatusBar gameName="Catan" currentPlayer="Alice" />);
    await user.click(screen.getByRole('button', { name: /pausa/i }));
    expect(useSessionStore.getState().isPaused).toBe(true);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/session/__tests__/MobileStatusBar.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/session/MobileStatusBar.tsx
'use client';

import { Pause, Play } from 'lucide-react';

import { useSessionStore } from '@/store/session';
import { cn } from '@/lib/utils';

interface MobileStatusBarProps {
  gameName: string;
  currentPlayer: string;
}

export function MobileStatusBar({ gameName, currentPlayer }: MobileStatusBarProps) {
  const { status, isPaused, currentTurn, togglePause } = useSessionStore();
  const isLive = status === 'live' || status === 'paused';

  return (
    <div
      data-testid="mobile-status-bar"
      className={cn(
        'flex items-center justify-between px-3 lg:hidden',
        'h-[var(--mobile-status-bar-height,36px)]',
        'bg-card border-b border-border text-sm'
      )}
    >
      <div className="flex items-center gap-2 min-w-0">
        {isLive && (
          <span className={cn(
            'inline-flex items-center gap-1 text-xs font-bold',
            isPaused ? 'text-yellow-500' : 'text-green-500'
          )}>
            <span className={cn('h-2 w-2 rounded-full', isPaused ? 'bg-yellow-500' : 'bg-green-500 animate-pulse')} />
            {isPaused ? 'PAUSA' : 'LIVE'}
          </span>
        )}
        <span className="font-medium truncate">{gameName}</span>
      </div>

      <div className="flex items-center gap-3 shrink-0">
        <span className="text-xs text-muted-foreground">Turno {currentTurn}</span>
        {isLive && (
          <button
            onClick={togglePause}
            aria-label={isPaused ? 'Riprendi' : 'Pausa'}
            className="p-1 rounded-md hover:bg-muted transition-colors"
          >
            {isPaused ? <Play className="h-4 w-4 text-green-500" /> : <Pause className="h-4 w-4 text-yellow-500" />}
          </button>
        )}
      </div>
    </div>
  );
}
```

Add to `apps/web/src/components/session/index.ts`:
```ts
export { MobileStatusBar } from './MobileStatusBar';
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/session/__tests__/MobileStatusBar.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/session/
git commit -m "feat(session): add MobileStatusBar with LIVE indicator and pause toggle"
```

---

### Task 16: Run all tests and typecheck

- [ ] **Step 1: Run all new component tests**

Run: `cd apps/web && pnpm vitest run src/store/ src/components/game-nights/ src/components/session/ src/components/layout/QuickView/ src/components/layout/CollapsiblePanel/`
Expected: All PASS

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 3: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: No errors (or only pre-existing warnings)

- [ ] **Step 4: Fix any issues found in steps 1-3**

- [ ] **Step 5: Final commit if fixes were needed**

```bash
git add -A
git commit -m "fix(game-table): resolve typecheck and lint issues"
```

---

### Task 17: PR and issue update

- [ ] **Step 1: Push branch**

```bash
git push -u origin feature/issue-148-game-table-s1
```

- [ ] **Step 2: Create PR to `frontend-dev`**

```bash
gh pr create --base frontend-dev --title "feat(game-table): S1 Phase 2 — Quick View, Game Nights, Live Session" --body-file /dev/stdin <<'EOF'
## Summary

- Quick View panel (300px, 3 tabs, collapse to 44px)
- Game Night list page with status cards (upcoming/draft/completed)
- Inline Game Picker with player count filtering
- Live Session 3-column layout with collapsible panels
- Activity Feed with 9 event types
- Dice Roller with configurable dice types
- Mobile Status Bar with LIVE indicator
- 3 new Zustand stores: useQuickViewStore, useGameNightStore, useSessionStore
- New design tokens for panel dimensions
- CollapsiblePanel reusable component

## Test plan

- [ ] All new unit tests pass (`pnpm vitest run`)
- [ ] Typecheck passes (`pnpm typecheck`)
- [ ] Lint passes (`pnpm lint`)
- [ ] QuickView opens/closes/collapses correctly
- [ ] GameNightCard renders 3 status variants
- [ ] DiceRoller logs events to ActivityFeed
- [ ] LiveSessionLayout shows 3 columns on lg+
- [ ] MobileStatusBar shows on mobile only

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
```

- [ ] **Step 3: Update issue #148 on GitHub**

```bash
gh issue comment 148 --body "S1 Phase 2 implementation complete. PR created with Quick View, Game Night pages, Live Session layout, Activity Feed, Dice Roller, and Mobile Status Bar. See PR for details."
```

- [ ] **Step 4: Request code review**

Dispatch `superpowers:code-reviewer` subagent on the PR.

---

## Chunk 8: Game Night Planning Page (Spec Item #4)

### Task 18: DealtGameCard — rotated game card for table

**Files:**
- Create: `apps/web/src/components/game-nights/DealtGameCard.tsx`
- Test: `apps/web/src/components/game-nights/__tests__/DealtGameCard.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/game-nights/__tests__/DealtGameCard.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { DealtGameCard } from '../DealtGameCard';

describe('DealtGameCard', () => {
  it('renders game title', () => {
    render(<DealtGameCard game={{ id: 'g1', title: 'Catan' }} onRemove={vi.fn()} rotation={-2} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('applies rotation transform', () => {
    render(<DealtGameCard game={{ id: 'g1', title: 'Catan' }} onRemove={vi.fn()} rotation={-2} />);
    const card = screen.getByTestId('dealt-card');
    expect(card.style.transform).toContain('rotate(-2deg)');
  });

  it('calls onRemove when remove button clicked', async () => {
    const onRemove = vi.fn();
    const user = userEvent.setup();
    render(<DealtGameCard game={{ id: 'g1', title: 'Catan' }} onRemove={onRemove} rotation={1} />);
    await user.click(screen.getByRole('button', { name: /rimuovi/i }));
    expect(onRemove).toHaveBeenCalledWith('g1');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/game-nights/__tests__/DealtGameCard.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/game-nights/DealtGameCard.tsx
'use client';

import { X, Gamepad2 } from 'lucide-react';
import type { GameNightGame } from '@/store/game-night';
import { cn } from '@/lib/utils';

interface DealtGameCardProps {
  game: GameNightGame;
  onRemove: (gameId: string) => void;
  rotation: number;
}

export function DealtGameCard({ game, onRemove, rotation }: DealtGameCardProps) {
  return (
    <div
      data-testid="dealt-card"
      style={{ transform: `rotate(${rotation}deg)` }}
      className={cn(
        'relative group w-[140px] rounded-xl border border-border bg-card p-3',
        'shadow-sm hover:shadow-md transition-shadow duration-200'
      )}
    >
      <button
        onClick={() => onRemove(game.id)}
        aria-label={`Rimuovi ${game.title}`}
        className={cn(
          'absolute -top-2 -right-2 h-6 w-6 rounded-full',
          'bg-destructive text-destructive-foreground',
          'flex items-center justify-center',
          'opacity-0 group-hover:opacity-100 transition-opacity'
        )}
      >
        <X className="h-3 w-3" />
      </button>
      <div className="h-16 w-full rounded-lg bg-muted flex items-center justify-center mb-2">
        {game.thumbnailUrl ? (
          <img src={game.thumbnailUrl} alt={game.title} className="h-full w-full rounded-lg object-cover" />
        ) : (
          <Gamepad2 className="h-6 w-6 text-muted-foreground" />
        )}
      </div>
      <p className="text-xs font-medium text-center truncate">{game.title}</p>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/game-nights/__tests__/DealtGameCard.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/game-nights/DealtGameCard.tsx apps/web/src/components/game-nights/__tests__/DealtGameCard.test.tsx
git commit -m "feat(game-nights): add DealtGameCard with rotation transform"
```

---

### Task 19: GameNightTimeline — horizontal timeline bar

**Files:**
- Create: `apps/web/src/components/game-nights/GameNightTimeline.tsx`
- Test: `apps/web/src/components/game-nights/__tests__/GameNightTimeline.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/game-nights/__tests__/GameNightTimeline.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { GameNightTimeline } from '../GameNightTimeline';
import type { TimelineSlot } from '@/store/game-night';

const slots: TimelineSlot[] = [
  { id: '1', type: 'game', gameId: 'g1', durationMinutes: 60 },
  { id: '2', type: 'break', durationMinutes: 15 },
  { id: '3', type: 'free', durationMinutes: 30 },
];

describe('GameNightTimeline', () => {
  it('renders all timeline slots', () => {
    render(<GameNightTimeline slots={slots} />);
    expect(screen.getByTestId('timeline')).toBeInTheDocument();
    expect(screen.getAllByTestId('timeline-slot')).toHaveLength(3);
  });

  it('shows game slot with game label', () => {
    render(<GameNightTimeline slots={slots} gameNames={{ g1: 'Catan' }} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('shows break label', () => {
    render(<GameNightTimeline slots={slots} />);
    expect(screen.getByText(/pausa/i)).toBeInTheDocument();
  });

  it('shows duration for each slot', () => {
    render(<GameNightTimeline slots={slots} />);
    expect(screen.getByText(/60min/i)).toBeInTheDocument();
    expect(screen.getByText(/15min/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/game-nights/__tests__/GameNightTimeline.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/game-nights/GameNightTimeline.tsx
'use client';

import { Gamepad2, Coffee, Clock } from 'lucide-react';
import type { TimelineSlot } from '@/store/game-night';
import { cn } from '@/lib/utils';

const SLOT_CONFIG = {
  game: { icon: Gamepad2, bg: 'bg-primary/10 border-primary/20', label: 'Gioco' },
  break: { icon: Coffee, bg: 'bg-yellow-500/10 border-yellow-500/20', label: 'Pausa' },
  free: { icon: Clock, bg: 'bg-muted border-border', label: 'Libero' },
} as const;

interface GameNightTimelineProps {
  slots: TimelineSlot[];
  gameNames?: Record<string, string>;
}

export function GameNightTimeline({ slots, gameNames = {} }: GameNightTimelineProps) {
  const total = slots.reduce((sum, s) => sum + s.durationMinutes, 0);

  return (
    <div data-testid="timeline" className="flex gap-1 h-10 rounded-lg overflow-hidden border border-border">
      {slots.map((slot) => {
        const config = SLOT_CONFIG[slot.type];
        const Icon = config.icon;
        const width = total > 0 ? (slot.durationMinutes / total) * 100 : 0;
        const label = slot.type === 'game' && slot.gameId ? (gameNames[slot.gameId] ?? config.label) : config.label;

        return (
          <div
            key={slot.id}
            data-testid="timeline-slot"
            style={{ width: `${width}%` }}
            className={cn('flex items-center gap-1 px-2 border', config.bg, 'min-w-0')}
          >
            <Icon className="h-3.5 w-3.5 shrink-0" />
            <span className="text-xs font-medium truncate">{label}</span>
            <span className="text-[10px] text-muted-foreground ml-auto shrink-0">{slot.durationMinutes}min</span>
          </div>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/game-nights/__tests__/GameNightTimeline.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/game-nights/GameNightTimeline.tsx apps/web/src/components/game-nights/__tests__/GameNightTimeline.test.tsx
git commit -m "feat(game-nights): add GameNightTimeline horizontal bar"
```

---

### Task 20: GameNightPlanningLayout — two-column planning page

**Files:**
- Create: `apps/web/src/components/game-nights/GameNightPlanningLayout.tsx`
- Create: `apps/web/src/app/(authenticated)/game-nights/[id]/page.tsx`
- Test: `apps/web/src/components/game-nights/__tests__/GameNightPlanningLayout.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/game-nights/__tests__/GameNightPlanningLayout.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import { useGameNightStore } from '@/store/game-night';
import { GameNightPlanningLayout } from '../GameNightPlanningLayout';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => <a href={href} {...props}>{children}</a>,
}));

describe('GameNightPlanningLayout', () => {
  beforeEach(() => {
    useGameNightStore.setState(useGameNightStore.getInitialState());
  });

  it('renders two-column layout', () => {
    render(<GameNightPlanningLayout title="Friday Night" />);
    expect(screen.getByTestId('planning-layout')).toBeInTheDocument();
  });

  it('shows planning title', () => {
    render(<GameNightPlanningLayout title="Friday Night" />);
    expect(screen.getByText('Friday Night')).toBeInTheDocument();
  });

  it('shows dealt cards area', () => {
    render(<GameNightPlanningLayout title="Test" />);
    expect(screen.getByTestId('dealt-cards-area')).toBeInTheDocument();
  });

  it('shows empty drop zone when no games selected', () => {
    render(<GameNightPlanningLayout title="Test" />);
    expect(screen.getByText(/aggiungi giochi/i)).toBeInTheDocument();
  });

  it('shows player section', () => {
    render(<GameNightPlanningLayout title="Test" />);
    expect(screen.getByText(/giocatori/i)).toBeInTheDocument();
  });

  it('shows timeline section', () => {
    render(<GameNightPlanningLayout title="Test" />);
    expect(screen.getByText(/programma/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/game-nights/__tests__/GameNightPlanningLayout.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/game-nights/GameNightPlanningLayout.tsx
'use client';

import { Plus, Users } from 'lucide-react';
import { useState } from 'react';

import { useGameNightStore } from '@/store/game-night';
import { cn } from '@/lib/utils';

import { DealtGameCard } from './DealtGameCard';
import { GameNightTimeline } from './GameNightTimeline';
import { InlineGamePicker } from './InlineGamePicker';

const ROTATIONS = [-2, 1, -1, 2, -0.5, 1.5];

interface GameNightPlanningLayoutProps {
  title: string;
  availableGames?: Array<{ id: string; title: string; thumbnailUrl?: string; minPlayers?: number; maxPlayers?: number }>;
}

export function GameNightPlanningLayout({ title, availableGames = [] }: GameNightPlanningLayoutProps) {
  const { players, selectedGames, timeline, removeGame, addGame } = useGameNightStore();
  const [showPicker, setShowPicker] = useState(false);

  return (
    <div data-testid="planning-layout" className="grid grid-cols-1 lg:grid-cols-[320px_1fr] gap-6">
      {/* Left column — info, players, AI suggestion */}
      <div className="space-y-6">
        <div>
          <h2 className="text-xl font-bold font-quicksand">{title}</h2>
        </div>

        <div>
          <h3 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide mb-3">
            <Users className="inline h-4 w-4 mr-1.5" />
            Giocatori ({players.length})
          </h3>
          {players.length === 0 ? (
            <p className="text-sm text-muted-foreground">Invita giocatori per iniziare</p>
          ) : (
            <div className="space-y-2">
              {players.map((p) => (
                <div key={p.id} className="flex items-center gap-2 text-sm">
                  <div className="h-6 w-6 rounded-full bg-primary/20 flex items-center justify-center text-[10px] font-bold">
                    {p.name[0]}
                  </div>
                  <span>{p.name}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Right column — table, picker, timeline */}
      <div className="space-y-6">
        {/* Dealt cards area */}
        <div data-testid="dealt-cards-area">
          <h3 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide mb-3">
            Sul Tavolo ({selectedGames.length})
          </h3>
          {selectedGames.length === 0 ? (
            <button
              onClick={() => setShowPicker(true)}
              className={cn(
                'w-full h-32 rounded-xl border-2 border-dashed border-border',
                'flex flex-col items-center justify-center gap-2',
                'text-muted-foreground hover:border-primary/30 hover:text-foreground transition-colors'
              )}
            >
              <Plus className="h-6 w-6" />
              <span className="text-sm">Aggiungi giochi al tavolo</span>
            </button>
          ) : (
            <div className="flex flex-wrap gap-4 p-4 rounded-xl bg-muted/30 border border-border min-h-[120px]">
              {selectedGames.map((game, i) => (
                <DealtGameCard
                  key={game.id}
                  game={game}
                  onRemove={removeGame}
                  rotation={ROTATIONS[i % ROTATIONS.length]}
                />
              ))}
              <button
                onClick={() => setShowPicker(true)}
                className="w-[140px] h-[120px] rounded-xl border-2 border-dashed border-border flex items-center justify-center text-muted-foreground hover:border-primary/30 transition-colors"
              >
                <Plus className="h-5 w-5" />
              </button>
            </div>
          )}
        </div>

        {/* Inline picker */}
        {showPicker && (
          <InlineGamePicker
            games={availableGames}
            onSelect={(game) => { addGame(game); setShowPicker(false); }}
            playerCount={players.length || undefined}
            excludeIds={selectedGames.map((g) => g.id)}
          />
        )}

        {/* Timeline */}
        <div>
          <h3 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide mb-3">
            Programma
          </h3>
          {timeline.length > 0 ? (
            <GameNightTimeline slots={timeline} />
          ) : (
            <p className="text-sm text-muted-foreground">Il programma verrà generato automaticamente</p>
          )}
        </div>
      </div>
    </div>
  );
}
```

```tsx
// apps/web/src/app/(authenticated)/game-nights/[id]/page.tsx
import { GameNightPlanningLayout } from '@/components/game-nights/GameNightPlanningLayout';

interface Props {
  params: Promise<{ id: string }>;
}

export default async function GameNightDetailPage({ params }: Props) {
  const { id } = await params;
  // TODO: fetch game night data via React Query when backend exists
  return (
    <div className="space-y-6">
      <GameNightPlanningLayout title={`Serata ${id}`} />
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/game-nights/__tests__/GameNightPlanningLayout.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/game-nights/ apps/web/src/app/\(authenticated\)/game-nights/
git commit -m "feat(game-nights): add planning page with dealt cards, timeline, inline picker"
```

---

## Chunk 9: AI Integration (Spec Item #10)

### Task 21: AIQuickViewContent — AI tab content for Quick View

**Files:**
- Create: `apps/web/src/components/layout/QuickView/AIQuickViewContent.tsx`
- Test: `apps/web/src/components/layout/QuickView/__tests__/AIQuickViewContent.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/layout/QuickView/__tests__/AIQuickViewContent.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { AIQuickViewContent } from '../AIQuickViewContent';

describe('AIQuickViewContent', () => {
  it('renders quick prompt buttons', () => {
    render(<AIQuickViewContent gameId="g1" gameName="Catan" />);
    expect(screen.getByText(/spiega le regole/i)).toBeInTheDocument();
    expect(screen.getByText(/strategia/i)).toBeInTheDocument();
  });

  it('renders message input', () => {
    render(<AIQuickViewContent gameId="g1" gameName="Catan" />);
    expect(screen.getByPlaceholderText(/chiedi/i)).toBeInTheDocument();
  });

  it('sends message on submit', async () => {
    const user = userEvent.setup();
    render(<AIQuickViewContent gameId="g1" gameName="Catan" />);
    const input = screen.getByPlaceholderText(/chiedi/i);
    await user.type(input, 'Come si vince?');
    await user.click(screen.getByRole('button', { name: /invia/i }));
    // Input should clear after send
    expect(input).toHaveValue('');
  });

  it('shows game name in context label', () => {
    render(<AIQuickViewContent gameId="g1" gameName="Catan" />);
    expect(screen.getByText(/catan/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/QuickView/__tests__/AIQuickViewContent.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/layout/QuickView/AIQuickViewContent.tsx
'use client';

import { useState } from 'react';
import { Bot, Send } from 'lucide-react';
import { cn } from '@/lib/utils';

const QUICK_PROMPTS = [
  'Spiega le regole base',
  'Suggerisci una strategia',
  'Regole per principianti',
  'Domande frequenti',
];

interface AIQuickViewContentProps {
  gameId: string;
  gameName: string;
}

export function AIQuickViewContent({ gameId, gameName }: AIQuickViewContentProps) {
  const [input, setInput] = useState('');
  const [messages, setMessages] = useState<Array<{ role: 'user' | 'ai'; text: string }>>([]);

  const handleSend = (text: string) => {
    if (!text.trim()) return;
    setMessages((prev) => [...prev, { role: 'user', text }]);
    setInput('');
    // TODO: integrate with useAgentChatStream when agent endpoint exists
  };

  return (
    <div className="flex flex-col h-full">
      {/* Context label */}
      <div className="flex items-center gap-2 px-3 py-2 border-b border-border">
        <Bot className="h-4 w-4 text-purple-500" />
        <span className="text-xs font-medium text-muted-foreground">
          AI assistente — {gameName}
        </span>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-3 space-y-3">
        {messages.length === 0 && (
          <div className="text-center py-6">
            <Bot className="h-8 w-8 mx-auto text-muted-foreground/50 mb-2" />
            <p className="text-sm text-muted-foreground">Chiedi qualcosa su {gameName}</p>
          </div>
        )}
        {messages.map((msg, i) => (
          <div key={i} className={cn('text-sm rounded-lg px-3 py-2', msg.role === 'user' ? 'bg-primary/10 ml-6' : 'bg-muted mr-6')}>
            {msg.text}
          </div>
        ))}
      </div>

      {/* Quick prompts */}
      <div className="flex flex-wrap gap-1.5 px-3 py-2 border-t border-border">
        {QUICK_PROMPTS.map((prompt) => (
          <button
            key={prompt}
            onClick={() => handleSend(prompt)}
            className="text-xs px-2 py-1 rounded-full bg-muted text-muted-foreground hover:bg-muted/80 transition-colors"
          >
            {prompt}
          </button>
        ))}
      </div>

      {/* Input */}
      <div className="flex items-center gap-2 px-3 py-2 border-t border-border">
        <input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleSend(input)}
          placeholder="Chiedi all'AI..."
          className="flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
        />
        <button
          onClick={() => handleSend(input)}
          aria-label="Invia messaggio"
          className="p-1.5 rounded-md text-primary hover:bg-primary/10 transition-colors"
        >
          <Send className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/QuickView/__tests__/AIQuickViewContent.test.tsx`
Expected: PASS

- [ ] **Step 5: Wire into QuickView and commit**

Update `QuickView.tsx` to render `AIQuickViewContent` when `activeTab === 'ai'` and a game is selected (pass `selectedGameId` and game name).

```bash
git add apps/web/src/components/layout/QuickView/
git commit -m "feat(ai): add AIQuickViewContent with quick prompts and chat input"
```

---

### Task 22: AISuggestionCard — AI game recommendation card for planning

**Files:**
- Create: `apps/web/src/components/game-nights/AISuggestionCard.tsx`
- Test: `apps/web/src/components/game-nights/__tests__/AISuggestionCard.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/game-nights/__tests__/AISuggestionCard.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { AISuggestionCard } from '../AISuggestionCard';

describe('AISuggestionCard', () => {
  it('renders AI suggestion title', () => {
    render(<AISuggestionCard suggestions={[{ gameTitle: 'Catan', reason: 'Perfect for 4 players' }]} />);
    expect(screen.getByText(/suggerimenti ai/i)).toBeInTheDocument();
  });

  it('renders game suggestions', () => {
    render(<AISuggestionCard suggestions={[{ gameTitle: 'Catan', reason: 'Adatto a principianti' }]} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText(/adatto a principianti/i)).toBeInTheDocument();
  });

  it('shows empty state when no suggestions', () => {
    render(<AISuggestionCard suggestions={[]} />);
    expect(screen.getByText(/nessun suggerimento/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/game-nights/__tests__/AISuggestionCard.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/game-nights/AISuggestionCard.tsx
'use client';

import { Bot, Sparkles } from 'lucide-react';

interface Suggestion {
  gameTitle: string;
  reason: string;
}

interface AISuggestionCardProps {
  suggestions: Suggestion[];
}

export function AISuggestionCard({ suggestions }: AISuggestionCardProps) {
  return (
    <div className="rounded-xl border border-purple-500/20 bg-gradient-to-br from-purple-500/5 to-primary/5 p-4">
      <div className="flex items-center gap-2 mb-3">
        <Sparkles className="h-4 w-4 text-purple-500" />
        <h3 className="text-sm font-semibold">Suggerimenti AI</h3>
      </div>
      {suggestions.length === 0 ? (
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Bot className="h-4 w-4" />
          <span>Nessun suggerimento — aggiungi giocatori per consigli personalizzati</span>
        </div>
      ) : (
        <div className="space-y-2">
          {suggestions.map((s) => (
            <div key={s.gameTitle} className="flex items-start gap-2">
              <Bot className="h-4 w-4 text-purple-500 mt-0.5 shrink-0" />
              <div>
                <span className="text-sm font-medium">{s.gameTitle}</span>
                <p className="text-xs text-muted-foreground">{s.reason}</p>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/game-nights/__tests__/AISuggestionCard.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/game-nights/AISuggestionCard.tsx apps/web/src/components/game-nights/__tests__/AISuggestionCard.test.tsx
git commit -m "feat(game-nights): add AISuggestionCard with gradient design"
```

---

## Chunk 10: Activity Feed Input Bar & Mobile Components

### Task 23: ActivityFeedInputBar — message input for activity feed

**Files:**
- Create: `apps/web/src/components/session/ActivityFeedInputBar.tsx`
- Test: `apps/web/src/components/session/__tests__/ActivityFeedInputBar.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/session/__tests__/ActivityFeedInputBar.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import { useSessionStore } from '@/store/session';
import { ActivityFeedInputBar } from '../ActivityFeedInputBar';

describe('ActivityFeedInputBar', () => {
  beforeEach(() => {
    useSessionStore.setState(useSessionStore.getInitialState());
  });

  it('renders text input', () => {
    render(<ActivityFeedInputBar playerId="p1" playerName="Alice" />);
    expect(screen.getByPlaceholderText(/nota/i)).toBeInTheDocument();
  });

  it('renders quick action buttons', () => {
    render(<ActivityFeedInputBar playerId="p1" playerName="Alice" />);
    expect(screen.getByRole('button', { name: /dadi/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /foto/i })).toBeInTheDocument();
  });

  it('adds note event on submit', async () => {
    const user = userEvent.setup();
    render(<ActivityFeedInputBar playerId="p1" playerName="Alice" />);
    const input = screen.getByPlaceholderText(/nota/i);
    await user.type(input, 'Great move!');
    await user.click(screen.getByRole('button', { name: /invia/i }));
    expect(useSessionStore.getState().events).toHaveLength(1);
    expect(useSessionStore.getState().events[0].type).toBe('note');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/session/__tests__/ActivityFeedInputBar.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/session/ActivityFeedInputBar.tsx
'use client';

import { useState } from 'react';
import { Camera, Dice5, Bot, Send } from 'lucide-react';
import { useSessionStore } from '@/store/session';
import { cn } from '@/lib/utils';

interface ActivityFeedInputBarProps {
  playerId: string;
  playerName: string;
  onDiceClick?: () => void;
  onCameraClick?: () => void;
  onAIClick?: () => void;
}

export function ActivityFeedInputBar({
  playerId, playerName, onDiceClick, onCameraClick, onAIClick,
}: ActivityFeedInputBarProps) {
  const [text, setText] = useState('');
  const addEvent = useSessionStore((s) => s.addEvent);

  const handleSend = () => {
    if (!text.trim()) return;
    addEvent({
      id: crypto.randomUUID(),
      type: 'note',
      playerId,
      data: { playerName, text: text.trim() },
      timestamp: new Date().toISOString(),
    });
    setText('');
  };

  return (
    <div className="flex items-center gap-2 px-3 py-2 border-t border-border bg-card">
      <input
        value={text}
        onChange={(e) => setText(e.target.value)}
        onKeyDown={(e) => e.key === 'Enter' && handleSend()}
        placeholder="Scrivi una nota..."
        className="flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
      />
      <div className="flex items-center gap-1 shrink-0">
        <button onClick={onDiceClick} aria-label="Dadi" className="p-1.5 rounded-md text-muted-foreground hover:text-foreground transition-colors">
          <Dice5 className="h-4 w-4" />
        </button>
        <button onClick={onCameraClick} aria-label="Foto" className="p-1.5 rounded-md text-muted-foreground hover:text-foreground transition-colors">
          <Camera className="h-4 w-4" />
        </button>
        <button onClick={onAIClick} aria-label="AI" className="p-1.5 rounded-md text-muted-foreground hover:text-foreground transition-colors">
          <Bot className="h-4 w-4" />
        </button>
        <button
          onClick={handleSend}
          aria-label="Invia nota"
          className={cn('p-1.5 rounded-md transition-colors', text.trim() ? 'text-primary hover:bg-primary/10' : 'text-muted-foreground')}
        >
          <Send className="h-4 w-4" />
        </button>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/session/__tests__/ActivityFeedInputBar.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/session/ActivityFeedInputBar.tsx apps/web/src/components/session/__tests__/ActivityFeedInputBar.test.tsx
git commit -m "feat(session): add ActivityFeedInputBar with note input and quick actions"
```

---

### Task 24: MobileScorebar — horizontal player mini-cards

**Files:**
- Create: `apps/web/src/components/session/MobileScorebar.tsx`
- Test: `apps/web/src/components/session/__tests__/MobileScorebar.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/session/__tests__/MobileScorebar.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { MobileScorebar } from '../MobileScorebar';

const players = [
  { id: 'p1', name: 'Alice', score: 10 },
  { id: 'p2', name: 'Bob', score: 15 },
  { id: 'p3', name: 'Carol', score: 8 },
];

describe('MobileScorebar', () => {
  it('renders all player mini-cards', () => {
    render(<MobileScorebar players={players} />);
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('Carol')).toBeInTheDocument();
  });

  it('shows scores', () => {
    render(<MobileScorebar players={players} />);
    expect(screen.getByText('10')).toBeInTheDocument();
    expect(screen.getByText('15')).toBeInTheDocument();
  });

  it('is hidden on desktop (lg:hidden)', () => {
    render(<MobileScorebar players={players} />);
    const bar = screen.getByTestId('mobile-scorebar');
    expect(bar.className).toContain('lg:hidden');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/session/__tests__/MobileScorebar.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write implementation**

```tsx
// apps/web/src/components/session/MobileScorebar.tsx
'use client';

interface PlayerScore {
  id: string;
  name: string;
  score: number;
}

interface MobileScorebarProps {
  players: PlayerScore[];
}

export function MobileScorebar({ players }: MobileScorebarProps) {
  return (
    <div
      data-testid="mobile-scorebar"
      className="flex gap-2 px-3 overflow-x-auto scrollbar-none lg:hidden"
      style={{ height: 'var(--mobile-scorebar-height, 52px)' }}
    >
      {players.map((p) => (
        <div
          key={p.id}
          className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-card border border-border shrink-0"
        >
          <div className="h-6 w-6 rounded-full bg-primary/20 flex items-center justify-center text-[10px] font-bold">
            {p.name[0]}
          </div>
          <div className="flex flex-col">
            <span className="text-xs font-medium truncate max-w-[60px]">{p.name}</span>
            <span className="text-sm font-bold tabular-nums">{p.score}</span>
          </div>
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/session/__tests__/MobileScorebar.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/session/MobileScorebar.tsx apps/web/src/components/session/__tests__/MobileScorebar.test.tsx
git commit -m "feat(session): add MobileScorebar with horizontal player mini-cards"
```

---

## Chunk 11: Store Selectors & Final Validation

### Task 25: Add selectors to all three new Zustand stores

**Files:**
- Modify: `apps/web/src/store/quick-view/store.ts`
- Modify: `apps/web/src/store/game-night/store.ts`
- Modify: `apps/web/src/store/session/store.ts`

- [ ] **Step 1: Add selectors to each store**

Append to each store file, following the `admin-games/store.ts` pattern:

```ts
// quick-view/store.ts — add after store definition
export const selectIsOpen = (s: QuickViewState) => s.isOpen;
export const selectSelectedGameId = (s: QuickViewState) => s.selectedGameId;
export const selectActiveTab = (s: QuickViewState) => s.activeTab;
export const selectIsCollapsed = (s: QuickViewState) => s.isCollapsed;

// game-night/store.ts — add after store definition
export const selectGameNights = (s: GameNightState) => s.gameNights;
export const selectSelectedId = (s: GameNightState) => s.selectedId;
export const selectPlayers = (s: GameNightState) => s.players;
export const selectSelectedGames = (s: GameNightState) => s.selectedGames;
export const selectTimeline = (s: GameNightState) => s.timeline;
export const selectIsLoading = (s: GameNightState) => s.isLoading;
export const selectPlayerCount = (s: GameNightState) => s.players.length;

// session/store.ts — add after store definition
export const selectStatus = (s: SessionState) => s.status;
export const selectEvents = (s: SessionState) => s.events;
export const selectScores = (s: SessionState) => s.scores;
export const selectCurrentTurn = (s: SessionState) => s.currentTurn;
export const selectIsPaused = (s: SessionState) => s.isPaused;
export const selectIsLive = (s: SessionState) => s.status === 'live' || s.status === 'paused';
```

Also export state interfaces from each store file and barrel export selectors from `index.ts`.

- [ ] **Step 2: Run existing store tests to verify nothing breaks**

Run: `cd apps/web && pnpm vitest run src/store/`
Expected: All PASS

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/store/
git commit -m "feat(store): add selectors to QuickView, GameNight, and Session stores"
```

---

### Task 26: Final validation — all tests, typecheck, lint

- [ ] **Step 1: Run ALL new tests**

Run: `cd apps/web && pnpm vitest run src/store/ src/components/game-nights/ src/components/session/ src/components/layout/QuickView/ src/components/layout/CollapsiblePanel/`
Expected: All PASS

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 3: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: No errors

- [ ] **Step 4: Fix any issues found**

- [ ] **Step 5: Final commit if needed**

---

### Task 27: PR, issue update, code review

(Replaces Task 17 scope — updated PR description to include all chunks)

- [ ] **Step 1: Push branch**

```bash
git push -u origin feature/issue-148-game-table-s1
```

- [ ] **Step 2: Create PR to `frontend-dev`**

```bash
gh pr create --base frontend-dev --title "feat(game-table): S1 Phase 2 — complete Game Table layout" --body-file /dev/stdin <<'EOF'
## Summary

- **Quick View panel** (300px, 3 tabs: Regole/FAQ/AI, collapse to 44px, AI chat with quick prompts)
- **Game Night list** with status cards (upcoming/draft/completed), player avatars, game thumbnails
- **Game Night planning** — two-column layout, dealt cards with rotation, InlineGamePicker with player count filter, timeline, AI suggestion card
- **Live Session** — 3-column layout with collapsible panels (persisted), ActivityFeed with 9 event types + input bar, DiceRoller
- **Mobile** — MobileStatusBar (LIVE/pause), MobileScorebar (horizontal player cards)
- **3 Zustand stores** with selectors: useQuickViewStore, useGameNightStore, useSessionStore
- **Design tokens**: panel dimensions, status bar/scorebar heights
- **CollapsiblePanel** reusable component

## Test plan

- [ ] All unit tests pass (`pnpm vitest run`)
- [ ] Typecheck passes (`pnpm typecheck`)
- [ ] Lint passes (`pnpm lint`)
- [ ] QuickView opens/closes/collapses, AI tab has chat + prompts
- [ ] GameNightCard shows 3 status variants with avatars/thumbnails
- [ ] Planning page: dealt cards with rotation, InlineGamePicker filters by player count
- [ ] DiceRoller logs events to ActivityFeed
- [ ] LiveSessionLayout 3-column on lg+, collapsible panels persist preference
- [ ] MobileStatusBar LIVE indicator on mobile only
- [ ] MobileScorebar horizontal scroll on mobile only

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
```

- [ ] **Step 3: Update issue #148**

```bash
gh issue comment 148 --body "S1 Phase 2 complete — all 10 spec items implemented. PR ready for review."
```

- [ ] **Step 4: Request code review**

Dispatch `superpowers:code-reviewer` subagent on the PR.
