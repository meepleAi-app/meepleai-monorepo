# User Dashboard & Session Navigation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the user dashboard with dynamic exploration/game mode switching, navbar transformation during sessions, bottom sheet sub-context navigation, and card link graph navigation within sheets.

**Architecture:** Extend the existing xstate DashboardEngine with new `tavolo`/`sheetOpen` sub-states under `gameMode`. The navbar transforms conditionally via `SessionNavBar` inside `UserTopNav`. Bottom sheets use framer-motion spring animations with drag-to-resize. Card links navigate within the same sheet using a breadcrumb stack.

**Tech Stack:** Next.js 16, React 19, TypeScript, Tailwind 4, framer-motion, xstate, Vitest, React Testing Library

**Spec:** `docs/superpowers/specs/2026-03-22-user-dashboard-navigation-design.md`

---

## File Map

### New Files

| File | Responsibility |
|------|---------------|
| `apps/web/src/components/dashboard/session-nav/SessionNavBar.tsx` | Navbar content for gameMode (exit, game name, timer, sub-context icons) |
| `apps/web/src/components/dashboard/session-nav/SubContextIcon.tsx` | Individual sub-context icon button with active state |
| `apps/web/src/components/dashboard/session-nav/LiveTimer.tsx` | Real-time session timer display |
| `apps/web/src/components/dashboard/sheet/SessionSheet.tsx` | Bottom sheet container with drag, overlay, spring animations |
| `apps/web/src/components/dashboard/sheet/SheetBreadcrumb.tsx` | Breadcrumb navigation inside sheet |
| `apps/web/src/components/dashboard/sheet/CardLinkChip.tsx` | Clickable pill for contextual card link navigation |
| `apps/web/src/components/dashboard/sheet/SheetContent.tsx` | Router component dispatching to context-specific content |
| `apps/web/src/components/dashboard/sheet/contents/ScoresContent.tsx` | Sheet content: live scoreboard with categories |
| `apps/web/src/components/dashboard/sheet/contents/RulesAiContent.tsx` | Sheet content: AI chat with RAG |
| `apps/web/src/components/dashboard/sheet/contents/TimerContent.tsx` | Sheet content: turn timer, stopwatch, history |
| `apps/web/src/components/dashboard/sheet/contents/PhotosContent.tsx` | Sheet content: session photo grid |
| `apps/web/src/components/dashboard/sheet/contents/PlayersContent.tsx` | Sheet content: participant list, turn order |
| `apps/web/src/components/dashboard/tavolo/TavoloView.tsx` | Game mode main view: scoreboard, turn, actions, log |
| `apps/web/src/components/dashboard/tavolo/ScoreboardCompact.tsx` | Compact scoreboard with avatars and scores |
| `apps/web/src/components/dashboard/tavolo/TurnIndicator.tsx` | Current turn display with timer |
| `apps/web/src/components/dashboard/tavolo/QuickActions.tsx` | Quick action buttons (add score, ask AI) |
| `apps/web/src/components/dashboard/tavolo/EventLog.tsx` | Recent session events list |
| `apps/web/src/components/dashboard/exploration/ExplorationView.tsx` | Exploration layout: hero + banner + carousel sections |
| `apps/web/src/components/dashboard/exploration/HeroCompact.tsx` | Greeting + inline stats |
| `apps/web/src/components/dashboard/exploration/ActiveSessionBanner.tsx` | Resume active session banner |
| `apps/web/src/components/dashboard/exploration/CarouselSection.tsx` | Reusable carousel section with title + "See all" |

### Modified Files

| File | Change |
|------|--------|
| `apps/web/src/components/dashboard/DashboardEngine.ts` | Replace `default`/`expanded` sub-states with `tavolo`/`sheetOpen`, add sheet/cardlink events |
| `apps/web/src/components/dashboard/useDashboardMode.ts` | Replace `isExpanded` with `activeSheet`, `breadcrumb`, `openSheet()`, `closeSheet()`, `navigateCardLink()`, `backCardLink()` |
| `apps/web/src/components/dashboard/DashboardRenderer.tsx` | Render `ExplorationView` or `TavoloView` + `SessionSheet` based on state |
| `apps/web/src/components/layout/UserShell/UserTopNav.tsx` | Conditional render: `SessionNavBar` when `isGameMode` |
| `apps/web/src/components/dashboard/__tests__/DashboardEngine.test.ts` | Update tests for new states/events |
| `apps/web/src/components/dashboard/__tests__/useDashboardMode.test.tsx` | Update tests for new hook API |

### Test Files (New)

| File | Tests For |
|------|-----------|
| `apps/web/src/components/dashboard/__tests__/DashboardEngine.test.ts` | Extended xstate machine (existing file, new tests added) |
| `apps/web/src/components/dashboard/__tests__/useDashboardMode.test.tsx` | Extended hook API (existing file, new tests added) |
| `apps/web/src/components/dashboard/__tests__/SessionNavBar.test.tsx` | Navbar transformation |
| `apps/web/src/components/dashboard/__tests__/SessionSheet.test.tsx` | Sheet open/close/drag |
| `apps/web/src/components/dashboard/__tests__/CardLinkChip.test.tsx` | Card link navigation |
| `apps/web/src/components/dashboard/__tests__/SheetBreadcrumb.test.tsx` | Breadcrumb rendering and back navigation |
| `apps/web/src/components/dashboard/__tests__/TavoloView.test.tsx` | Tavolo layout rendering |
| `apps/web/src/components/dashboard/__tests__/ExplorationView.test.tsx` | Exploration layout with carousels |

---

## Task 1: Extend DashboardEngine xstate Machine

**Files:**
- Modify: `apps/web/src/components/dashboard/DashboardEngine.ts` (lines 7-13 events, lines 138-162 gameMode states)
- Modify: `apps/web/src/components/dashboard/__tests__/DashboardEngine.test.ts`

- [ ] **Step 1: Write failing tests for new events and states**

Add to `apps/web/src/components/dashboard/__tests__/DashboardEngine.test.ts`:

```typescript
import { createActor } from 'xstate';
import { dashboardMachine } from '../DashboardEngine';

describe('DashboardEngine — sheet navigation', () => {
  it('should transition from tavolo to sheetOpen on OPEN_SHEET', () => {
    const actor = createActor(dashboardMachine).start();
    // Get to gameMode.tavolo first
    actor.send({ type: 'SESSION_DETECTED', sessionId: 'test-1' });
    actor.send({ type: 'TRANSITION_COMPLETE' });

    actor.send({ type: 'OPEN_SHEET', context: 'scores' });

    const snapshot = actor.getSnapshot();
    expect(snapshot.value).toEqual({ gameMode: 'sheetOpen' });
    expect(snapshot.context.activeSheet).toBe('scores');
    expect(snapshot.context.breadcrumb).toEqual([{ context: 'scores', label: 'Punteggi' }]);
  });

  it('should transition from sheetOpen to tavolo on CLOSE_SHEET', () => {
    const actor = createActor(dashboardMachine).start();
    actor.send({ type: 'SESSION_DETECTED', sessionId: 'test-1' });
    actor.send({ type: 'TRANSITION_COMPLETE' });
    actor.send({ type: 'OPEN_SHEET', context: 'scores' });

    actor.send({ type: 'CLOSE_SHEET' });

    const snapshot = actor.getSnapshot();
    expect(snapshot.value).toEqual({ gameMode: 'tavolo' });
    expect(snapshot.context.activeSheet).toBeNull();
    expect(snapshot.context.breadcrumb).toEqual([]);
  });

  it('should push breadcrumb on NAVIGATE_CARD_LINK', () => {
    const actor = createActor(dashboardMachine).start();
    actor.send({ type: 'SESSION_DETECTED', sessionId: 'test-1' });
    actor.send({ type: 'TRANSITION_COMPLETE' });
    actor.send({ type: 'OPEN_SHEET', context: 'scores' });

    actor.send({ type: 'NAVIGATE_CARD_LINK', target: 'rules-ai', label: 'Regole punteggio' });

    const snapshot = actor.getSnapshot();
    expect(snapshot.context.activeSheet).toBe('rules-ai');
    expect(snapshot.context.breadcrumb).toEqual([
      { context: 'scores', label: 'Punteggi' },
      { context: 'rules-ai', label: 'Regole punteggio' },
    ]);
  });

  it('should pop breadcrumb on BACK_CARD_LINK', () => {
    const actor = createActor(dashboardMachine).start();
    actor.send({ type: 'SESSION_DETECTED', sessionId: 'test-1' });
    actor.send({ type: 'TRANSITION_COMPLETE' });
    actor.send({ type: 'OPEN_SHEET', context: 'scores' });
    actor.send({ type: 'NAVIGATE_CARD_LINK', target: 'rules-ai', label: 'Regole punteggio' });

    actor.send({ type: 'BACK_CARD_LINK' });

    const snapshot = actor.getSnapshot();
    expect(snapshot.context.activeSheet).toBe('scores');
    expect(snapshot.context.breadcrumb).toEqual([
      { context: 'scores', label: 'Punteggi' },
    ]);
  });

  it('should cap breadcrumb at max depth 3', () => {
    const actor = createActor(dashboardMachine).start();
    actor.send({ type: 'SESSION_DETECTED', sessionId: 'test-1' });
    actor.send({ type: 'TRANSITION_COMPLETE' });
    actor.send({ type: 'OPEN_SHEET', context: 'scores' });
    actor.send({ type: 'NAVIGATE_CARD_LINK', target: 'rules-ai', label: 'Regole' });
    actor.send({ type: 'NAVIGATE_CARD_LINK', target: 'players', label: 'Players' });

    // This should NOT add a 4th entry
    actor.send({ type: 'NAVIGATE_CARD_LINK', target: 'timer', label: 'Timer' });

    const snapshot = actor.getSnapshot();
    expect(snapshot.context.breadcrumb.length).toBeLessThanOrEqual(3);
  });

  it('should switch sheet context when OPEN_SHEET sent while sheetOpen', () => {
    const actor = createActor(dashboardMachine).start();
    actor.send({ type: 'SESSION_DETECTED', sessionId: 'test-1' });
    actor.send({ type: 'TRANSITION_COMPLETE' });
    actor.send({ type: 'OPEN_SHEET', context: 'scores' });

    actor.send({ type: 'OPEN_SHEET', context: 'timer' });

    const snapshot = actor.getSnapshot();
    expect(snapshot.context.activeSheet).toBe('timer');
    expect(snapshot.context.breadcrumb).toEqual([{ context: 'timer', label: 'Timer' }]);
  });

  it('should clear sheet state on SESSION_COMPLETED', () => {
    const actor = createActor(dashboardMachine).start();
    actor.send({ type: 'SESSION_DETECTED', sessionId: 'test-1' });
    actor.send({ type: 'TRANSITION_COMPLETE' });
    actor.send({ type: 'OPEN_SHEET', context: 'scores' });

    actor.send({ type: 'SESSION_COMPLETED' });
    actor.send({ type: 'TRANSITION_COMPLETE' });

    const snapshot = actor.getSnapshot();
    expect(snapshot.context.activeSheet).toBeNull();
    expect(snapshot.context.breadcrumb).toEqual([]);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/DashboardEngine.test.ts`
Expected: FAIL — `OPEN_SHEET` event not recognized, `activeSheet`/`breadcrumb` not in context

- [ ] **Step 3: Update DashboardEngine types and context**

In `apps/web/src/components/dashboard/DashboardEngine.ts`, update the event types (around line 7) and context (around line 15):

```typescript
// --- Types ---
export type SheetContext = 'scores' | 'rules-ai' | 'timer' | 'photos' | 'players';

export interface BreadcrumbEntry {
  context: SheetContext;
  label: string;
}

const SHEET_LABELS: Record<SheetContext, string> = {
  scores: 'Punteggi',
  'rules-ai': 'Regole AI',
  timer: 'Timer',
  photos: 'Foto',
  players: 'Giocatori',
};

const MAX_BREADCRUMB_DEPTH = 3;

// --- Events (replace existing) ---
export type DashboardEvent =
  | { type: 'SESSION_DETECTED'; sessionId: string }
  | { type: 'TRANSITION_COMPLETE' }
  | { type: 'SESSION_COMPLETED' }
  | { type: 'SESSION_DISMISSED' }
  | { type: 'OPEN_SHEET'; context: SheetContext }
  | { type: 'CLOSE_SHEET' }
  | { type: 'NAVIGATE_CARD_LINK'; target: SheetContext; label: string }
  | { type: 'BACK_CARD_LINK' };

// --- Context (add to existing) ---
// Add these fields to the context type:
//   activeSheet: SheetContext | null;
//   breadcrumb: BreadcrumbEntry[];
// Add to initial context:
//   activeSheet: null,
//   breadcrumb: [],
```

- [ ] **Step 4: Replace gameMode sub-states**

Replace the `gameMode` state definition (lines 138-162) with:

```typescript
gameMode: {
  initial: 'tavolo',
  on: {
    SESSION_COMPLETED: {
      target: '#dashboard.transitioning',
      actions: ['beginExitTransition'],
    },
    SESSION_DISMISSED: {
      target: '#dashboard.transitioning',
      actions: ['beginExitTransition'],
    },
  },
  states: {
    tavolo: {
      entry: ['clearSheet'],
      on: {
        OPEN_SHEET: {
          target: 'sheetOpen',
          actions: ['setSheet'],
        },
      },
    },
    sheetOpen: {
      on: {
        CLOSE_SHEET: {
          target: 'tavolo',
        },
        OPEN_SHEET: {
          actions: ['setSheet'],
        },
        NAVIGATE_CARD_LINK: {
          actions: ['pushBreadcrumb'],
          guard: 'breadcrumbNotFull',
        },
        BACK_CARD_LINK: {
          actions: ['popBreadcrumb'],
          guard: 'hasBreadcrumbHistory',
        },
      },
    },
  },
},
```

- [ ] **Step 5: Add actions and guards**

Add to the machine's `actions` (after existing actions around line 80):

```typescript
setSheet: assign({
  activeSheet: ({ event }) => {
    if (event.type !== 'OPEN_SHEET') return null;
    return event.context;
  },
  breadcrumb: ({ event }) => {
    if (event.type !== 'OPEN_SHEET') return [];
    return [{ context: event.context, label: SHEET_LABELS[event.context] }];
  },
}),
clearSheet: assign({
  activeSheet: () => null,
  breadcrumb: () => [],
}),
pushBreadcrumb: assign({
  activeSheet: ({ event }) => {
    if (event.type !== 'NAVIGATE_CARD_LINK') return null;
    return event.target;
  },
  breadcrumb: ({ context, event }) => {
    if (event.type !== 'NAVIGATE_CARD_LINK') return context.breadcrumb;
    return [...context.breadcrumb, { context: event.target, label: event.label }];
  },
}),
popBreadcrumb: assign({
  activeSheet: ({ context }) => {
    const prev = context.breadcrumb[context.breadcrumb.length - 2];
    return prev?.context ?? null;
  },
  breadcrumb: ({ context }) => context.breadcrumb.slice(0, -1),
}),
```

Add to the machine's `guards`:

```typescript
breadcrumbNotFull: ({ context }) => context.breadcrumb.length < MAX_BREADCRUMB_DEPTH,
hasBreadcrumbHistory: ({ context }) => context.breadcrumb.length > 1,
```

Also update `clearSession` action to include sheet cleanup:

```typescript
clearSession: assign({
  activeSessionId: () => null,
  transitionType: () => null,
  transitionTarget: () => null,
  previousState: () => null,
  activeSheet: () => null,
  breadcrumb: () => [],
}),
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/DashboardEngine.test.ts`
Expected: ALL PASS

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/dashboard/DashboardEngine.ts apps/web/src/components/dashboard/__tests__/DashboardEngine.test.ts
git commit -m "feat(dashboard): extend DashboardEngine with tavolo/sheetOpen states and card link navigation"
```

---

## Task 2: Update useDashboardMode Hook

**Files:**
- Modify: `apps/web/src/components/dashboard/useDashboardMode.ts`
- Modify: `apps/web/src/components/dashboard/__tests__/useDashboardMode.test.tsx`

- [ ] **Step 1: Write failing tests for new hook API**

Add to `apps/web/src/components/dashboard/__tests__/useDashboardMode.test.tsx`:

```typescript
describe('useDashboardMode — sheet API', () => {
  it('should expose openSheet function', () => {
    const { result } = renderHook(() => useDashboardMode(), { wrapper });
    expect(result.current.openSheet).toBeDefined();
    expect(typeof result.current.openSheet).toBe('function');
  });

  it('should expose closeSheet function', () => {
    const { result } = renderHook(() => useDashboardMode(), { wrapper });
    expect(result.current.closeSheet).toBeDefined();
  });

  it('should return activeSheet as null initially', () => {
    const { result } = renderHook(() => useDashboardMode(), { wrapper });
    expect(result.current.activeSheet).toBeNull();
  });

  it('should return empty breadcrumb initially', () => {
    const { result } = renderHook(() => useDashboardMode(), { wrapper });
    expect(result.current.breadcrumb).toEqual([]);
  });

  it('should not expose isExpanded', () => {
    const { result } = renderHook(() => useDashboardMode(), { wrapper });
    expect(result.current).not.toHaveProperty('isExpanded');
  });

  it('should expose navigateCardLink and backCardLink', () => {
    const { result } = renderHook(() => useDashboardMode(), { wrapper });
    expect(typeof result.current.navigateCardLink).toBe('function');
    expect(typeof result.current.backCardLink).toBe('function');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/useDashboardMode.test.tsx`
Expected: FAIL — `openSheet`, `closeSheet`, `activeSheet`, `breadcrumb` not in return value

- [ ] **Step 3: Update hook implementation**

Replace `apps/web/src/components/dashboard/useDashboardMode.ts` content:

```typescript
'use client';

import { useCallback } from 'react';
import { useSelector } from '@xstate/react';
import { DashboardEngineContext } from './DashboardEngineProvider';
import { useContext } from 'react';
import type { SheetContext, BreadcrumbEntry } from './DashboardEngine';

export function useDashboardMode() {
  const actorRef = useContext(DashboardEngineContext);
  const snapshot = useSelector(actorRef, (s) => s);

  const stateValue = snapshot.value;
  const state =
    typeof stateValue === 'string'
      ? stateValue
      : Object.keys(stateValue)[0];

  const send = snapshot ? actorRef.send : () => {};

  const openSheet = useCallback(
    (context: SheetContext) => send({ type: 'OPEN_SHEET', context }),
    [send]
  );

  const closeSheet = useCallback(
    () => send({ type: 'CLOSE_SHEET' }),
    [send]
  );

  const navigateCardLink = useCallback(
    (target: SheetContext, label: string) =>
      send({ type: 'NAVIGATE_CARD_LINK', target, label }),
    [send]
  );

  const backCardLink = useCallback(
    () => send({ type: 'BACK_CARD_LINK' }),
    [send]
  );

  return {
    state: state as 'exploration' | 'transitioning' | 'gameMode',
    isExploration: state === 'exploration',
    isGameMode: state === 'gameMode',
    isTransitioning: state === 'transitioning',
    activeSessionId: snapshot.context.activeSessionId,
    transitionTarget: snapshot.context.transitionTarget,
    activeSheet: snapshot.context.activeSheet as SheetContext | null,
    breadcrumb: snapshot.context.breadcrumb as BreadcrumbEntry[],
    send,
    openSheet,
    closeSheet,
    navigateCardLink,
    backCardLink,
  };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/useDashboardMode.test.tsx`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/dashboard/useDashboardMode.ts apps/web/src/components/dashboard/__tests__/useDashboardMode.test.tsx
git commit -m "feat(dashboard): update useDashboardMode with sheet navigation API"
```

---

## Task 3: ExplorationView Components

**Files:**
- Create: `apps/web/src/components/dashboard/exploration/HeroCompact.tsx`
- Create: `apps/web/src/components/dashboard/exploration/ActiveSessionBanner.tsx`
- Create: `apps/web/src/components/dashboard/exploration/CarouselSection.tsx`
- Create: `apps/web/src/components/dashboard/exploration/QuickStats.tsx`
- Create: `apps/web/src/components/dashboard/exploration/ExplorationView.tsx`
- Create: `apps/web/src/components/dashboard/__tests__/ExplorationView.test.tsx`

- [ ] **Step 1: Write failing test for ExplorationView**

Create `apps/web/src/components/dashboard/__tests__/ExplorationView.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ExplorationView } from '../exploration/ExplorationView';

// Mock child components
vi.mock('../exploration/HeroCompact', () => ({
  HeroCompact: () => <div data-testid="hero-compact">HeroCompact</div>,
}));
vi.mock('../exploration/ActiveSessionBanner', () => ({
  ActiveSessionBanner: () => <div data-testid="active-session-banner">Banner</div>,
}));
vi.mock('../exploration/CarouselSection', () => ({
  CarouselSection: ({ title }: { title: string }) => (
    <div data-testid={`carousel-${title}`}>{title}</div>
  ),
}));

vi.mock('@/lib/stores/sessionStore', () => ({
  useSessionStore: () => ({ activeSessions: [] }),
}));

describe('ExplorationView', () => {
  it('should render HeroCompact', () => {
    render(<ExplorationView />);
    expect(screen.getByTestId('hero-compact')).toBeInTheDocument();
  });

  it('should render carousel sections', () => {
    render(<ExplorationView />);
    expect(screen.getByTestId('carousel-Giochi Recenti')).toBeInTheDocument();
    expect(screen.getByTestId('carousel-Suggeriti per te')).toBeInTheDocument();
    expect(screen.getByTestId('carousel-Sessioni Recenti')).toBeInTheDocument();
  });

  it('should not render ActiveSessionBanner when no active session', () => {
    render(<ExplorationView />);
    expect(screen.queryByTestId('active-session-banner')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/ExplorationView.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Create HeroCompact**

Create `apps/web/src/components/dashboard/exploration/HeroCompact.tsx`:

```tsx
'use client';

interface HeroCompactProps {
  userName?: string;
  gamesThisWeek?: number;
  hoursPlayed?: number;
  avgRating?: number;
}

export function HeroCompact({
  userName = 'Giocatore',
  gamesThisWeek = 0,
  hoursPlayed = 0,
  avgRating = 0,
}: HeroCompactProps) {
  return (
    <div className="px-4 py-4 bg-gradient-to-br from-card to-background">
      <h1 className="text-lg font-semibold font-quicksand text-foreground">
        Ciao {userName} 👋
      </h1>
      <p className="text-sm text-muted-foreground mt-1">
        {gamesThisWeek} giochi questa settimana · {hoursPlayed}h giocate · rating medio{' '}
        {avgRating.toFixed(1)}
      </p>
    </div>
  );
}
```

- [ ] **Step 4: Create ActiveSessionBanner**

Create `apps/web/src/components/dashboard/exploration/ActiveSessionBanner.tsx`:

```tsx
'use client';

import Link from 'next/link';

interface ActiveSessionBannerProps {
  gameName: string;
  elapsed: string;
  sessionId: string;
}

export function ActiveSessionBanner({ gameName, elapsed, sessionId }: ActiveSessionBannerProps) {
  return (
    <Link
      href={`/sessions/live/${sessionId}`}
      className="mx-4 my-3 p-3 rounded-xl border border-emerald-500/30 bg-gradient-to-r from-emerald-500/15 to-emerald-500/5 flex items-center justify-between hover:border-emerald-500/50 transition-colors"
    >
      <div>
        <p className="text-xs font-semibold text-emerald-500 uppercase tracking-wide">
          ⏱ Sessione in corso
        </p>
        <p className="text-sm font-semibold text-foreground mt-0.5">
          {gameName} · {elapsed}
        </p>
      </div>
      <span className="text-sm font-semibold text-white bg-emerald-500 px-3 py-1.5 rounded-md">
        Riprendi →
      </span>
    </Link>
  );
}
```

- [ ] **Step 5: Create CarouselSection**

Create `apps/web/src/components/dashboard/exploration/CarouselSection.tsx`:

```tsx
'use client';

import Link from 'next/link';
import type { ReactNode } from 'react';

interface CarouselSectionProps {
  title: string;
  icon?: string;
  seeAllHref?: string;
  seeAllLabel?: string;
  accentColor?: string;
  children: ReactNode;
}

export function CarouselSection({
  title,
  icon,
  seeAllHref,
  seeAllLabel = 'Vedi tutti →',
  accentColor = 'text-primary',
  children,
}: CarouselSectionProps) {
  return (
    <section className="px-4 py-2">
      <div className="flex items-center justify-between mb-2">
        <h2 className="text-sm font-semibold text-foreground">
          {icon && <span className="mr-1">{icon}</span>}
          {title}
        </h2>
        {seeAllHref && (
          <Link href={seeAllHref} className={`text-xs ${accentColor} hover:underline`}>
            {seeAllLabel}
          </Link>
        )}
      </div>
      <div className="flex gap-3 overflow-x-auto pb-2 scrollbar-none snap-x snap-mandatory">
        {children}
      </div>
    </section>
  );
}
```

- [ ] **Step 6: Create QuickStats**

Create `apps/web/src/components/dashboard/exploration/QuickStats.tsx`:

```tsx
'use client';

interface QuickStatsProps {
  totalGames?: number;
  totalSessions?: number;
  avgRating?: number;
}

export function QuickStats({ totalGames = 0, totalSessions = 0, avgRating = 0 }: QuickStatsProps) {
  const stats = [
    { value: totalGames, label: 'Giochi', color: 'text-primary' },
    { value: totalSessions, label: 'Sessioni', color: 'text-emerald-500' },
    { value: avgRating.toFixed(1), label: 'Rating medio', color: 'text-violet-500' },
  ];

  return (
    <div className="px-4 py-2 grid grid-cols-3 gap-2">
      {stats.map((stat) => (
        <div
          key={stat.label}
          className="bg-card rounded-lg border border-border p-2.5 text-center"
        >
          <p className={`text-lg font-bold ${stat.color}`}>{stat.value}</p>
          <p className="text-[10px] text-muted-foreground">{stat.label}</p>
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 7: Create ExplorationView**

Create `apps/web/src/components/dashboard/exploration/ExplorationView.tsx`:

```tsx
'use client';

import { HeroCompact } from './HeroCompact';
import { ActiveSessionBanner } from './ActiveSessionBanner';
import { CarouselSection } from './CarouselSection';
import { QuickStats } from './QuickStats';

interface ExplorationViewProps {
  userName?: string;
  activeSession?: {
    gameName: string;
    elapsed: string;
    sessionId: string;
  } | null;
}

export function ExplorationView({ userName, activeSession = null }: ExplorationViewProps) {
  return (
    <div className="flex flex-col gap-0">
      <HeroCompact userName={userName} />

      {activeSession && (
        <ActiveSessionBanner
          gameName={activeSession.gameName}
          elapsed={activeSession.elapsed}
          sessionId={activeSession.sessionId}
        />
      )}

      <CarouselSection
        title="Giochi Recenti"
        icon="🎲"
        seeAllHref="/library"
        accentColor="text-primary"
      >
        {/* Populated by parent with MeepleCard items */}
        <div />
      </CarouselSection>

      <CarouselSection
        title="Suggeriti per te"
        icon="🤖"
        seeAllHref="/discover"
        accentColor="text-cyan-500"
      >
        <div />
      </CarouselSection>

      <CarouselSection
        title="Sessioni Recenti"
        icon="📊"
        seeAllHref="/sessions"
        accentColor="text-emerald-500"
      >
        <div />
      </CarouselSection>

      <QuickStats />
    </div>
  );
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/ExplorationView.test.tsx`
Expected: ALL PASS

- [ ] **Step 9: Commit**

```bash
git add apps/web/src/components/dashboard/exploration/
git add apps/web/src/components/dashboard/__tests__/ExplorationView.test.tsx
git commit -m "feat(dashboard): add ExplorationView with HeroCompact, ActiveSessionBanner, CarouselSection, QuickStats"
```

---

## Task 4: SessionNavBar Component

**Files:**
- Create: `apps/web/src/components/dashboard/session-nav/SessionNavBar.tsx`
- Create: `apps/web/src/components/dashboard/session-nav/SubContextIcon.tsx`
- Create: `apps/web/src/components/dashboard/session-nav/LiveTimer.tsx`
- Modify: `apps/web/src/components/layout/UserShell/UserTopNav.tsx`
- Create: `apps/web/src/components/dashboard/__tests__/SessionNavBar.test.tsx`

- [ ] **Step 1: Write failing test for SessionNavBar**

Create `apps/web/src/components/dashboard/__tests__/SessionNavBar.test.tsx`:

```typescript
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SessionNavBar } from '../session-nav/SessionNavBar';

const mockOpenSheet = vi.fn();
const mockOnExit = vi.fn();

const defaultProps = {
  gameName: 'Catan',
  activeSheet: null as string | null,
  onOpenSheet: mockOpenSheet,
  onExit: mockOnExit,
};

describe('SessionNavBar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render game name', () => {
    render(<SessionNavBar {...defaultProps} />);
    expect(screen.getByText(/Catan/)).toBeInTheDocument();
  });

  it('should render exit button', () => {
    render(<SessionNavBar {...defaultProps} />);
    const exitBtn = screen.getByRole('button', { name: /esci/i });
    expect(exitBtn).toBeInTheDocument();
  });

  it('should call onExit when exit button clicked', () => {
    render(<SessionNavBar {...defaultProps} />);
    fireEvent.click(screen.getByRole('button', { name: /esci/i }));
    expect(mockOnExit).toHaveBeenCalledOnce();
  });

  it('should render all 5 sub-context icons', () => {
    render(<SessionNavBar {...defaultProps} />);
    expect(screen.getByRole('button', { name: /punteggi/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /regole ai/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /timer/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /foto/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /giocatori/i })).toBeInTheDocument();
  });

  it('should call onOpenSheet with context when icon clicked', () => {
    render(<SessionNavBar {...defaultProps} />);
    fireEvent.click(screen.getByRole('button', { name: /punteggi/i }));
    expect(mockOpenSheet).toHaveBeenCalledWith('scores');
  });

  it('should highlight active sheet icon', () => {
    render(<SessionNavBar {...defaultProps} activeSheet="scores" />);
    const scoresBtn = screen.getByRole('button', { name: /punteggi/i });
    expect(scoresBtn.className).toContain('bg-primary');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/SessionNavBar.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Create SubContextIcon**

Create `apps/web/src/components/dashboard/session-nav/SubContextIcon.tsx`:

```tsx
'use client';

import type { SheetContext } from '../DashboardEngine';

const CONTEXT_CONFIG: Record<SheetContext, { icon: string; label: string }> = {
  scores: { icon: '🏆', label: 'Punteggi' },
  'rules-ai': { icon: '🤖', label: 'Regole AI' },
  timer: { icon: '⏱', label: 'Timer' },
  photos: { icon: '📸', label: 'Foto' },
  players: { icon: '👥', label: 'Giocatori' },
};

interface SubContextIconProps {
  context: SheetContext;
  isActive: boolean;
  onClick: () => void;
}

export function SubContextIcon({ context, isActive, onClick }: SubContextIconProps) {
  const config = CONTEXT_CONFIG[context];

  return (
    <button
      onClick={onClick}
      aria-label={config.label}
      aria-pressed={isActive}
      className={`w-7 h-7 rounded-md flex items-center justify-center text-sm transition-all ${
        isActive
          ? 'bg-primary shadow-[0_0_8px_hsl(25,95%,38%/0.4)]'
          : 'bg-card border border-border hover:bg-accent'
      }`}
    >
      {config.icon}
    </button>
  );
}
```

- [ ] **Step 4: Create LiveTimer**

Create `apps/web/src/components/dashboard/session-nav/LiveTimer.tsx`:

```tsx
'use client';

import { useState, useEffect, useRef } from 'react';

interface LiveTimerProps {
  startedAt: Date;
  isPaused?: boolean;
  onClick?: () => void;
}

function formatElapsed(ms: number): string {
  const totalSeconds = Math.floor(ms / 1000);
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}

export function LiveTimer({ startedAt, isPaused = false, onClick }: LiveTimerProps) {
  const [elapsed, setElapsed] = useState(() => Date.now() - startedAt.getTime());
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    if (isPaused) {
      if (intervalRef.current) clearInterval(intervalRef.current);
      return;
    }
    intervalRef.current = setInterval(() => {
      setElapsed(Date.now() - startedAt.getTime());
    }, 1000);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [startedAt, isPaused]);

  return (
    <button
      onClick={onClick}
      className="text-xs font-mono text-emerald-500 hover:text-emerald-400 transition-colors"
      aria-label={`Timer sessione: ${formatElapsed(elapsed)}`}
    >
      ⏱ {formatElapsed(elapsed)}
    </button>
  );
}
```

- [ ] **Step 5: Create SessionNavBar**

Create `apps/web/src/components/dashboard/session-nav/SessionNavBar.tsx`:

```tsx
'use client';

import type { SheetContext } from '../DashboardEngine';
import { SubContextIcon } from './SubContextIcon';
import { LiveTimer } from './LiveTimer';

const SUB_CONTEXTS: SheetContext[] = ['scores', 'rules-ai', 'timer', 'photos', 'players'];

interface SessionNavBarProps {
  gameName: string;
  sessionStartedAt?: Date;
  isPaused?: boolean;
  activeSheet: SheetContext | null;
  onOpenSheet: (context: SheetContext) => void;
  onExit: () => void;
}

export function SessionNavBar({
  gameName,
  sessionStartedAt,
  isPaused,
  activeSheet,
  onOpenSheet,
  onExit,
}: SessionNavBarProps) {
  return (
    <div className="flex items-center justify-between w-full">
      {/* Left: Exit + Game name + Timer */}
      <div className="flex items-center gap-2 min-w-0">
        <button
          onClick={onExit}
          aria-label="Esci dalla sessione"
          className="text-xs text-muted-foreground hover:text-foreground transition-colors shrink-0"
        >
          ← Esci
        </button>
        <span className="text-xs font-semibold text-primary truncate max-w-[120px] sm:max-w-[200px]">
          🎲 {gameName}
        </span>
        {sessionStartedAt && (
          <LiveTimer
            startedAt={sessionStartedAt}
            isPaused={isPaused}
            onClick={() => onOpenSheet('timer')}
          />
        )}
      </div>

      {/* Right: Sub-context icons */}
      <div className="flex items-center gap-1.5">
        {SUB_CONTEXTS.map((ctx) => (
          <SubContextIcon
            key={ctx}
            context={ctx}
            isActive={activeSheet === ctx}
            onClick={() => onOpenSheet(ctx)}
          />
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/SessionNavBar.test.tsx`
Expected: ALL PASS

- [ ] **Step 7: Modify UserTopNav for conditional rendering**

In `apps/web/src/components/layout/UserShell/UserTopNav.tsx`, add the conditional branch. Wrap the existing content in an `{!isGameMode && ...}` block and add the `SessionNavBar` branch:

```tsx
// Add imports at top:
import { useDashboardMode } from '@/components/dashboard/useDashboardMode';
import { SessionNavBar } from '@/components/dashboard/session-nav/SessionNavBar';

// Inside the header element, replace the content with:
// const { isGameMode, activeSheet, openSheet } = useDashboardMode();

// {isGameMode ? (
//   <SessionNavBar
//     gameName={...}
//     activeSheet={activeSheet}
//     onOpenSheet={openSheet}
//     onExit={() => send({ type: 'SESSION_DISMISSED' })}
//   />
// ) : (
//   /* existing nav content */
// )}
```

Refer to `UserTopNav.tsx` lines 21-90 for the existing content to wrap.

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/components/dashboard/session-nav/
git add apps/web/src/components/dashboard/__tests__/SessionNavBar.test.tsx
git add apps/web/src/components/layout/UserShell/UserTopNav.tsx
git commit -m "feat(dashboard): add SessionNavBar with sub-context icons and navbar transformation"
```

---

## Task 5: TavoloView Components

**Files:**
- Create: `apps/web/src/components/dashboard/tavolo/ScoreboardCompact.tsx`
- Create: `apps/web/src/components/dashboard/tavolo/TurnIndicator.tsx`
- Create: `apps/web/src/components/dashboard/tavolo/QuickActions.tsx`
- Create: `apps/web/src/components/dashboard/tavolo/EventLog.tsx`
- Create: `apps/web/src/components/dashboard/tavolo/TavoloView.tsx`
- Create: `apps/web/src/components/dashboard/__tests__/TavoloView.test.tsx`

- [ ] **Step 1: Write failing test for TavoloView**

Create `apps/web/src/components/dashboard/__tests__/TavoloView.test.tsx`:

```typescript
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { TavoloView } from '../tavolo/TavoloView';

vi.mock('../tavolo/ScoreboardCompact', () => ({
  ScoreboardCompact: () => <div data-testid="scoreboard-compact" />,
}));
vi.mock('../tavolo/TurnIndicator', () => ({
  TurnIndicator: () => <div data-testid="turn-indicator" />,
}));
vi.mock('../tavolo/QuickActions', () => ({
  QuickActions: () => <div data-testid="quick-actions" />,
}));
vi.mock('../tavolo/EventLog', () => ({
  EventLog: () => <div data-testid="event-log" />,
}));

describe('TavoloView', () => {
  it('should render all tavolo sections', () => {
    render(<TavoloView sessionId="test-1" />);
    expect(screen.getByTestId('scoreboard-compact')).toBeInTheDocument();
    expect(screen.getByTestId('turn-indicator')).toBeInTheDocument();
    expect(screen.getByTestId('quick-actions')).toBeInTheDocument();
    expect(screen.getByTestId('event-log')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/TavoloView.test.tsx`
Expected: FAIL

- [ ] **Step 3: Create ScoreboardCompact**

Create `apps/web/src/components/dashboard/tavolo/ScoreboardCompact.tsx`:

```tsx
'use client';

interface PlayerScore {
  id: string;
  name: string;
  initial: string;
  score: number;
  rank: number;
  color: string;
}

interface ScoreboardCompactProps {
  players: PlayerScore[];
}

export function ScoreboardCompact({ players = [] }: ScoreboardCompactProps) {
  const sorted = [...players].sort((a, b) => b.score - a.score);

  return (
    <div className="bg-card rounded-xl border border-border p-3">
      <p className="text-[10px] text-muted-foreground uppercase tracking-wider mb-2">
        Classifica Live
      </p>
      <div className="flex justify-around">
        {sorted.map((player, i) => (
          <div key={player.id} className="text-center">
            <div
              className="w-8 h-8 rounded-full mx-auto mb-1 flex items-center justify-center text-xs font-semibold text-white"
              style={{ backgroundColor: player.color }}
            >
              {player.initial}
            </div>
            <p className="text-sm font-bold text-foreground">{player.score}</p>
            <p className="text-[9px] text-muted-foreground">
              {i === 0 ? '🏆 1st' : `${i + 1}${i === 1 ? 'nd' : i === 2 ? 'rd' : 'th'}`}
            </p>
          </div>
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Create TurnIndicator, QuickActions, EventLog**

Create `apps/web/src/components/dashboard/tavolo/TurnIndicator.tsx`:

```tsx
'use client';

interface TurnIndicatorProps {
  playerName: string;
  playerColor: string;
  turnElapsed?: string;
}

export function TurnIndicator({ playerName, playerColor, turnElapsed }: TurnIndicatorProps) {
  return (
    <div
      className="rounded-xl border p-3 flex items-center justify-between"
      style={{
        borderColor: `${playerColor}4D`,
        background: `linear-gradient(90deg, ${playerColor}26, ${playerColor}0D)`,
      }}
    >
      <div>
        <p className="text-[10px] uppercase tracking-wider" style={{ color: playerColor }}>
          Turno di
        </p>
        <p className="text-sm font-semibold text-foreground">{playerName}</p>
      </div>
      {turnElapsed && (
        <span className="text-xs font-mono" style={{ color: playerColor }}>
          ⏱ {turnElapsed}
        </span>
      )}
    </div>
  );
}
```

Create `apps/web/src/components/dashboard/tavolo/QuickActions.tsx`:

```tsx
'use client';

interface QuickActionsProps {
  onAddScore: () => void;
  onAskAi: () => void;
}

export function QuickActions({ onAddScore, onAskAi }: QuickActionsProps) {
  return (
    <div className="flex gap-2">
      <button
        onClick={onAddScore}
        className="flex-1 bg-primary text-primary-foreground rounded-lg py-2.5 text-sm font-semibold hover:bg-primary/90 transition-colors"
      >
        + Aggiungi Punteggio
      </button>
      <button
        onClick={onAskAi}
        className="w-11 bg-card border border-border rounded-lg flex items-center justify-center text-lg hover:bg-accent transition-colors"
        aria-label="Chiedi all'AI"
      >
        🤖
      </button>
    </div>
  );
}
```

Create `apps/web/src/components/dashboard/tavolo/EventLog.tsx`:

```tsx
'use client';

interface SessionEvent {
  id: string;
  message: string;
  isRecent: boolean;
}

interface EventLogProps {
  events: SessionEvent[];
}

export function EventLog({ events = [] }: EventLogProps) {
  return (
    <div className="bg-card rounded-xl border border-border p-3">
      <p className="text-[10px] text-muted-foreground uppercase tracking-wider mb-1.5">
        Ultimi eventi
      </p>
      <div className="space-y-0.5">
        {events.slice(0, 5).map((event) => (
          <p
            key={event.id}
            className={`text-xs ${event.isRecent ? 'text-foreground' : 'text-muted-foreground'}`}
          >
            {event.message}
          </p>
        ))}
        {events.length === 0 && (
          <p className="text-xs text-muted-foreground italic">Nessun evento</p>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Create TavoloView**

Create `apps/web/src/components/dashboard/tavolo/TavoloView.tsx`:

```tsx
'use client';

import { ScoreboardCompact } from './ScoreboardCompact';
import { TurnIndicator } from './TurnIndicator';
import { QuickActions } from './QuickActions';
import { EventLog } from './EventLog';
import { useDashboardMode } from '../useDashboardMode';

interface TavoloViewProps {
  sessionId: string;
}

export function TavoloView({ sessionId }: TavoloViewProps) {
  const { openSheet } = useDashboardMode();

  return (
    <div className="flex flex-col gap-3 p-4">
      <ScoreboardCompact players={[]} />
      <TurnIndicator playerName="" playerColor="#8b5cf6" />
      <QuickActions
        onAddScore={() => openSheet('scores')}
        onAskAi={() => openSheet('rules-ai')}
      />
      <EventLog events={[]} />
    </div>
  );
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/TavoloView.test.tsx`
Expected: ALL PASS

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/dashboard/tavolo/
git add apps/web/src/components/dashboard/__tests__/TavoloView.test.tsx
git commit -m "feat(dashboard): add TavoloView with ScoreboardCompact, TurnIndicator, QuickActions, EventLog"
```

---

## Task 6: SessionSheet Component

**Files:**
- Create: `apps/web/src/components/dashboard/sheet/SessionSheet.tsx`
- Create: `apps/web/src/components/dashboard/sheet/SheetBreadcrumb.tsx`
- Create: `apps/web/src/components/dashboard/sheet/CardLinkChip.tsx`
- Create: `apps/web/src/components/dashboard/__tests__/SessionSheet.test.tsx`
- Create: `apps/web/src/components/dashboard/__tests__/CardLinkChip.test.tsx`
- Create: `apps/web/src/components/dashboard/__tests__/SheetBreadcrumb.test.tsx`

- [ ] **Step 1: Write failing tests for SessionSheet**

Create `apps/web/src/components/dashboard/__tests__/SessionSheet.test.tsx`:

```typescript
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SessionSheet } from '../sheet/SessionSheet';

const mockOnClose = vi.fn();

describe('SessionSheet', () => {
  beforeEach(() => vi.clearAllMocks());

  it('should render children when open', () => {
    render(
      <SessionSheet isOpen onClose={mockOnClose}>
        <div data-testid="sheet-content">Content</div>
      </SessionSheet>
    );
    expect(screen.getByTestId('sheet-content')).toBeInTheDocument();
  });

  it('should not render when closed', () => {
    render(
      <SessionSheet isOpen={false} onClose={mockOnClose}>
        <div data-testid="sheet-content">Content</div>
      </SessionSheet>
    );
    expect(screen.queryByTestId('sheet-content')).not.toBeInTheDocument();
  });

  it('should render overlay when open', () => {
    render(
      <SessionSheet isOpen onClose={mockOnClose}>
        <div>Content</div>
      </SessionSheet>
    );
    expect(screen.getByTestId('sheet-overlay')).toBeInTheDocument();
  });

  it('should call onClose when overlay clicked', () => {
    render(
      <SessionSheet isOpen onClose={mockOnClose}>
        <div>Content</div>
      </SessionSheet>
    );
    fireEvent.click(screen.getByTestId('sheet-overlay'));
    expect(mockOnClose).toHaveBeenCalledOnce();
  });

  it('should render drag handle', () => {
    render(
      <SessionSheet isOpen onClose={mockOnClose}>
        <div>Content</div>
      </SessionSheet>
    );
    expect(screen.getByTestId('drag-handle')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Write failing tests for CardLinkChip and SheetBreadcrumb**

Create `apps/web/src/components/dashboard/__tests__/CardLinkChip.test.tsx`:

```typescript
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { CardLinkChip } from '../sheet/CardLinkChip';

describe('CardLinkChip', () => {
  it('should render label and icon', () => {
    render(<CardLinkChip icon="🤖" label="Come si punteggia?" onClick={() => {}} />);
    expect(screen.getByText('Come si punteggia?')).toBeInTheDocument();
    expect(screen.getByText('🤖')).toBeInTheDocument();
  });

  it('should call onClick when clicked', () => {
    const onClick = vi.fn();
    render(<CardLinkChip icon="🤖" label="Test" onClick={onClick} />);
    fireEvent.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('should render description when provided', () => {
    render(
      <CardLinkChip icon="🤖" label="Test" description="Sub text" onClick={() => {}} />
    );
    expect(screen.getByText('Sub text')).toBeInTheDocument();
  });
});
```

Create `apps/web/src/components/dashboard/__tests__/SheetBreadcrumb.test.tsx`:

```typescript
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SheetBreadcrumb } from '../sheet/SheetBreadcrumb';

describe('SheetBreadcrumb', () => {
  it('should render breadcrumb entries', () => {
    render(
      <SheetBreadcrumb
        entries={[
          { context: 'scores', label: 'Punteggi' },
          { context: 'rules-ai', label: 'Regole AI' },
        ]}
        onNavigate={() => {}}
      />
    );
    expect(screen.getByText('Punteggi')).toBeInTheDocument();
    expect(screen.getByText('Regole AI')).toBeInTheDocument();
  });

  it('should call onNavigate with index when non-last entry clicked', () => {
    const onNavigate = vi.fn();
    render(
      <SheetBreadcrumb
        entries={[
          { context: 'scores', label: 'Punteggi' },
          { context: 'rules-ai', label: 'Regole AI' },
        ]}
        onNavigate={onNavigate}
      />
    );
    fireEvent.click(screen.getByText('Punteggi'));
    expect(onNavigate).toHaveBeenCalledWith(0);
  });

  it('should not make last entry clickable', () => {
    const onNavigate = vi.fn();
    render(
      <SheetBreadcrumb
        entries={[
          { context: 'scores', label: 'Punteggi' },
          { context: 'rules-ai', label: 'Regole AI' },
        ]}
        onNavigate={onNavigate}
      />
    );
    fireEvent.click(screen.getByText('Regole AI'));
    expect(onNavigate).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/SessionSheet.test.tsx src/components/dashboard/__tests__/CardLinkChip.test.tsx src/components/dashboard/__tests__/SheetBreadcrumb.test.tsx`
Expected: FAIL — modules not found

- [ ] **Step 4: Create CardLinkChip**

Create `apps/web/src/components/dashboard/sheet/CardLinkChip.tsx`:

```tsx
'use client';

interface CardLinkChipProps {
  icon: string;
  label: string;
  description?: string;
  accentColor?: string;
  onClick: () => void;
}

export function CardLinkChip({
  icon,
  label,
  description,
  accentColor = 'cyan',
  onClick,
}: CardLinkChipProps) {
  const colorMap: Record<string, string> = {
    cyan: 'border-cyan-500/30 bg-gradient-to-r from-cyan-500/10 to-cyan-500/5',
    emerald: 'border-emerald-500/30 bg-gradient-to-r from-emerald-500/10 to-emerald-500/5',
    violet: 'border-violet-500/30 bg-gradient-to-r from-violet-500/10 to-violet-500/5',
    primary: 'border-primary/30 bg-gradient-to-r from-primary/10 to-primary/5',
  };

  return (
    <button
      onClick={onClick}
      className={`w-full rounded-lg border p-2.5 flex items-center justify-between hover:opacity-80 transition-opacity ${colorMap[accentColor] ?? colorMap.cyan}`}
    >
      <div className="flex items-center gap-2">
        <span className="text-base">{icon}</span>
        <div className="text-left">
          <p className="text-xs font-semibold text-foreground">{label}</p>
          {description && (
            <p className="text-[10px] text-muted-foreground">{description}</p>
          )}
        </div>
      </div>
      <span className="text-sm text-muted-foreground">→</span>
    </button>
  );
}
```

- [ ] **Step 5: Create SheetBreadcrumb**

Create `apps/web/src/components/dashboard/sheet/SheetBreadcrumb.tsx`:

```tsx
'use client';

import type { BreadcrumbEntry } from '../DashboardEngine';

interface SheetBreadcrumbProps {
  entries: BreadcrumbEntry[];
  onNavigate: (index: number) => void;
}

export function SheetBreadcrumb({ entries, onNavigate }: SheetBreadcrumbProps) {
  if (entries.length <= 1) return null;

  return (
    <nav className="flex items-center gap-1 text-xs px-4 pb-2" aria-label="Breadcrumb">
      {entries.map((entry, i) => {
        const isLast = i === entries.length - 1;
        return (
          <span key={`${entry.context}-${i}`} className="flex items-center gap-1">
            {i > 0 && <span className="text-muted-foreground">›</span>}
            {isLast ? (
              <span className="text-foreground font-medium">{entry.label}</span>
            ) : (
              <button
                onClick={() => onNavigate(i)}
                className="text-muted-foreground hover:text-foreground transition-colors"
              >
                {entry.label}
              </button>
            )}
          </span>
        );
      })}
    </nav>
  );
}
```

- [ ] **Step 6: Create SessionSheet**

Create `apps/web/src/components/dashboard/sheet/SessionSheet.tsx`:

```tsx
'use client';

import { type ReactNode } from 'react';
import { motion, AnimatePresence } from 'framer-motion';

interface SessionSheetProps {
  isOpen: boolean;
  onClose: () => void;
  children: ReactNode;
}

export function SessionSheet({ isOpen, onClose, children }: SessionSheetProps) {
  return (
    <AnimatePresence>
      {isOpen && (
        <>
          {/* Overlay */}
          <motion.div
            data-testid="sheet-overlay"
            className="fixed inset-0 bg-black/30 z-40"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={onClose}
          />

          {/* Sheet */}
          <motion.div
            className="fixed bottom-0 left-0 right-0 z-50 bg-card rounded-t-2xl shadow-[0_-4px_20px_rgba(0,0,0,0.5)] flex flex-col"
            style={{ height: '75vh' }}
            initial={{ y: '100%' }}
            animate={{ y: 0 }}
            exit={{ y: '100%' }}
            transition={{ type: 'spring', damping: 25, stiffness: 300 }}
            drag="y"
            dragConstraints={{ top: 0, bottom: 0 }}
            dragElastic={0.2}
            onDragEnd={(_, info) => {
              if (info.offset.y > 100) onClose();
            }}
          >
            {/* Drag handle */}
            <div className="flex justify-center py-2" data-testid="drag-handle">
              <div className="w-10 h-1 bg-muted-foreground/30 rounded-full" />
            </div>

            {/* Content */}
            <div className="flex-1 overflow-y-auto">{children}</div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/SessionSheet.test.tsx src/components/dashboard/__tests__/CardLinkChip.test.tsx src/components/dashboard/__tests__/SheetBreadcrumb.test.tsx`
Expected: ALL PASS

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/components/dashboard/sheet/
git add apps/web/src/components/dashboard/__tests__/SessionSheet.test.tsx
git add apps/web/src/components/dashboard/__tests__/CardLinkChip.test.tsx
git add apps/web/src/components/dashboard/__tests__/SheetBreadcrumb.test.tsx
git commit -m "feat(dashboard): add SessionSheet with CardLinkChip and SheetBreadcrumb"
```

---

## Task 7: SheetContent Router and Content Modules

**Files:**
- Create: `apps/web/src/components/dashboard/sheet/SheetContent.tsx`
- Create: `apps/web/src/components/dashboard/sheet/contents/ScoresContent.tsx`
- Create: `apps/web/src/components/dashboard/sheet/contents/RulesAiContent.tsx`
- Create: `apps/web/src/components/dashboard/sheet/contents/TimerContent.tsx`
- Create: `apps/web/src/components/dashboard/sheet/contents/PhotosContent.tsx`
- Create: `apps/web/src/components/dashboard/sheet/contents/PlayersContent.tsx`

- [ ] **Step 1: Create SheetContent router**

Create `apps/web/src/components/dashboard/sheet/SheetContent.tsx`:

```tsx
'use client';

import type { SheetContext } from '../DashboardEngine';
import { ScoresContent } from './contents/ScoresContent';
import { RulesAiContent } from './contents/RulesAiContent';
import { TimerContent } from './contents/TimerContent';
import { PhotosContent } from './contents/PhotosContent';
import { PlayersContent } from './contents/PlayersContent';

interface SheetContentProps {
  context: SheetContext;
  sessionId: string;
}

const CONTENT_MAP: Record<SheetContext, React.ComponentType<{ sessionId: string }>> = {
  scores: ScoresContent,
  'rules-ai': RulesAiContent,
  timer: TimerContent,
  photos: PhotosContent,
  players: PlayersContent,
};

export function SheetContent({ context, sessionId }: SheetContentProps) {
  const Component = CONTENT_MAP[context];
  if (!Component) return null;
  return <Component sessionId={sessionId} />;
}
```

- [ ] **Step 2: Create placeholder content modules**

Each content module is a scaffold that will be populated with real data integration later. Create all 5 files:

Create `apps/web/src/components/dashboard/sheet/contents/ScoresContent.tsx`:

```tsx
'use client';

import { CardLinkChip } from '../CardLinkChip';
import { useDashboardMode } from '../../useDashboardMode';

interface ScoresContentProps {
  sessionId: string;
}

export function ScoresContent({ sessionId }: ScoresContentProps) {
  const { navigateCardLink } = useDashboardMode();

  return (
    <div className="px-4 space-y-3">
      <div>
        <p className="text-[10px] text-muted-foreground uppercase tracking-wider">Punteggi</p>
        <h3 className="text-base font-semibold text-foreground">🏆 Classifica Dettagliata</h3>
      </div>

      {/* Score table will be populated with session data */}
      <div className="bg-background rounded-lg p-3">
        <p className="text-sm text-muted-foreground">Caricamento punteggi...</p>
      </div>

      <CardLinkChip
        icon="🤖"
        label="Come si punteggia?"
        description="Chiedi all'AI le regole del punteggio"
        accentColor="cyan"
        onClick={() => navigateCardLink('rules-ai', 'Regole punteggio')}
      />

      <CardLinkChip
        icon="👥"
        label="Dettaglio per giocatore"
        description="Storico punteggi di ogni player"
        accentColor="emerald"
        onClick={() => navigateCardLink('players', 'Punteggi giocatore')}
      />
    </div>
  );
}
```

Create `apps/web/src/components/dashboard/sheet/contents/RulesAiContent.tsx`:

```tsx
'use client';

import { CardLinkChip } from '../CardLinkChip';
import { useDashboardMode } from '../../useDashboardMode';

interface RulesAiContentProps {
  sessionId: string;
}

export function RulesAiContent({ sessionId }: RulesAiContentProps) {
  const { navigateCardLink } = useDashboardMode();

  return (
    <div className="px-4 space-y-3">
      <div>
        <p className="text-[10px] text-muted-foreground uppercase tracking-wider">Regole AI</p>
        <h3 className="text-base font-semibold text-foreground">🤖 Assistente Regole</h3>
      </div>

      {/* Chat interface will be integrated here */}
      <div className="bg-background rounded-lg p-3 min-h-[200px]">
        <p className="text-sm text-muted-foreground">Chat AI in arrivo...</p>
      </div>

      <CardLinkChip
        icon="🏆"
        label="Vai ai punteggi"
        description="Torna alla classifica"
        accentColor="primary"
        onClick={() => navigateCardLink('scores', 'Punteggi')}
      />
    </div>
  );
}
```

Create `apps/web/src/components/dashboard/sheet/contents/TimerContent.tsx`:

```tsx
'use client';

import { CardLinkChip } from '../CardLinkChip';
import { useDashboardMode } from '../../useDashboardMode';

interface TimerContentProps {
  sessionId: string;
}

export function TimerContent({ sessionId }: TimerContentProps) {
  const { navigateCardLink } = useDashboardMode();

  return (
    <div className="px-4 space-y-3">
      <div>
        <p className="text-[10px] text-muted-foreground uppercase tracking-wider">Timer</p>
        <h3 className="text-base font-semibold text-foreground">⏱ Timer Sessione</h3>
      </div>

      <div className="bg-background rounded-lg p-3">
        <p className="text-sm text-muted-foreground">Timer in arrivo...</p>
      </div>

      <CardLinkChip
        icon="👥"
        label="Di chi e il turno?"
        description="Vedi ordine giocatori"
        accentColor="emerald"
        onClick={() => navigateCardLink('players', 'Giocatori')}
      />
    </div>
  );
}
```

Create `apps/web/src/components/dashboard/sheet/contents/PhotosContent.tsx`:

```tsx
'use client';

interface PhotosContentProps {
  sessionId: string;
}

export function PhotosContent({ sessionId }: PhotosContentProps) {
  return (
    <div className="px-4 space-y-3">
      <div>
        <p className="text-[10px] text-muted-foreground uppercase tracking-wider">Foto</p>
        <h3 className="text-base font-semibold text-foreground">📸 Foto Sessione</h3>
      </div>

      <div className="bg-background rounded-lg p-3">
        <p className="text-sm text-muted-foreground">Galleria foto in arrivo...</p>
      </div>
    </div>
  );
}
```

Create `apps/web/src/components/dashboard/sheet/contents/PlayersContent.tsx`:

```tsx
'use client';

import { CardLinkChip } from '../CardLinkChip';
import { useDashboardMode } from '../../useDashboardMode';

interface PlayersContentProps {
  sessionId: string;
}

export function PlayersContent({ sessionId }: PlayersContentProps) {
  const { navigateCardLink } = useDashboardMode();

  return (
    <div className="px-4 space-y-3">
      <div>
        <p className="text-[10px] text-muted-foreground uppercase tracking-wider">Giocatori</p>
        <h3 className="text-base font-semibold text-foreground">👥 Partecipanti</h3>
      </div>

      <div className="bg-background rounded-lg p-3">
        <p className="text-sm text-muted-foreground">Lista giocatori in arrivo...</p>
      </div>

      <CardLinkChip
        icon="🏆"
        label="Punteggi giocatore"
        description="Vedi i punteggi dettagliati"
        accentColor="primary"
        onClick={() => navigateCardLink('scores', 'Punteggi')}
      />
    </div>
  );
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/dashboard/sheet/SheetContent.tsx
git add apps/web/src/components/dashboard/sheet/contents/
git commit -m "feat(dashboard): add SheetContent router and content modules with card links"
```

---

## Task 8: Wire DashboardRenderer

**Files:**
- Modify: `apps/web/src/components/dashboard/DashboardRenderer.tsx` (lines 37-69 ExplorationView, lines 105-136 AnimatePresence)

- [ ] **Step 1: Update DashboardRenderer imports and rendering**

In `apps/web/src/components/dashboard/DashboardRenderer.tsx`:

Replace the existing zone imports (lines 16-26) and inline `ExplorationView` (lines 37-69) with imports of the new components:

```tsx
// Replace zone imports with:
import { ExplorationView } from './exploration/ExplorationView';
import { TavoloView } from './tavolo/TavoloView';
import { SessionSheet } from './sheet/SessionSheet';
import { SheetContent } from './sheet/SheetContent';
import { SheetBreadcrumb } from './sheet/SheetBreadcrumb';
import { useDashboardMode } from './useDashboardMode';
```

Replace the AnimatePresence content (around lines 105-136) with:

```tsx
const {
  isExploration,
  isGameMode,
  isTransitioning,
  state,
  activeSheet,
  activeSessionId,
  breadcrumb,
  closeSheet,
  backCardLink,
} = useDashboardMode();

// In the JSX AnimatePresence:
// Exploration branch: render <ExplorationView />
// GameMode branch: render <TavoloView sessionId={activeSessionId} /> + <SessionSheet>
```

The `SessionSheet` wraps `SheetBreadcrumb` + `SheetContent`:

```tsx
{isGameMode && activeSessionId && (
  <>
    <TavoloView sessionId={activeSessionId} />
    <SessionSheet isOpen={activeSheet !== null} onClose={closeSheet}>
      <SheetBreadcrumb entries={breadcrumb} onNavigate={(i) => {
        // Navigate back to breadcrumb[i] by calling backCardLink enough times
        const stepsBack = breadcrumb.length - 1 - i;
        for (let s = 0; s < stepsBack; s++) backCardLink();
      }} />
      {activeSheet && (
        <SheetContent context={activeSheet} sessionId={activeSessionId} />
      )}
    </SessionSheet>
  </>
)}
```

- [ ] **Step 2: Run existing DashboardRenderer tests**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/DashboardRenderer.test.tsx`
Expected: May need mock updates for new imports. Fix any broken mocks.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/dashboard/DashboardRenderer.tsx
git commit -m "feat(dashboard): wire DashboardRenderer with ExplorationView, TavoloView, SessionSheet"
```

---

## Task 9: Mobile Responsive — Icon Overflow

**Files:**
- Modify: `apps/web/src/components/dashboard/session-nav/SessionNavBar.tsx`

- [ ] **Step 1: Add icon overflow for mobile**

In `SessionNavBar.tsx`, wrap the sub-context icons in a responsive container that collapses extra icons into a `•••` menu on mobile:

```tsx
// Add state for overflow menu
const [overflowOpen, setOverflowOpen] = useState(false);
const MAX_VISIBLE_MOBILE = 4;

// In JSX, split icons into visible and overflow:
<div className="flex items-center gap-1.5">
  {SUB_CONTEXTS.slice(0, MAX_VISIBLE_MOBILE).map((ctx) => (
    <SubContextIcon key={ctx} context={ctx} isActive={activeSheet === ctx} onClick={() => onOpenSheet(ctx)} />
  ))}
  {SUB_CONTEXTS.length > MAX_VISIBLE_MOBILE && (
    <div className="relative sm:hidden">
      <button onClick={() => setOverflowOpen(!overflowOpen)} className="w-7 h-7 rounded-md bg-card border border-border flex items-center justify-center text-xs text-muted-foreground">
        •••
      </button>
      {overflowOpen && (
        <div className="absolute right-0 top-full mt-1 bg-card border border-border rounded-lg shadow-lg p-1 z-50">
          {SUB_CONTEXTS.slice(MAX_VISIBLE_MOBILE).map((ctx) => (
            <SubContextIcon key={ctx} context={ctx} isActive={activeSheet === ctx} onClick={() => { onOpenSheet(ctx); setOverflowOpen(false); }} />
          ))}
        </div>
      )}
    </div>
  )}
  {/* Show all on desktop */}
  <div className="hidden sm:flex items-center gap-1.5">
    {SUB_CONTEXTS.slice(MAX_VISIBLE_MOBILE).map((ctx) => (
      <SubContextIcon key={ctx} context={ctx} isActive={activeSheet === ctx} onClick={() => onOpenSheet(ctx)} />
    ))}
  </div>
</div>
```

- [ ] **Step 2: Update mobile navbar to hide notifications/avatar**

In `UserTopNav.tsx`, when in gameMode on mobile, hide the notification bell and avatar (they go to hamburger):

```tsx
{/* Only show on desktop or in exploration mode */}
<div className={`flex items-center gap-2 ${isGameMode ? 'hidden sm:flex' : 'flex'}`}>
  <NotificationBell />
  <UserMenuDropdown />
</div>
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/dashboard/session-nav/SessionNavBar.tsx
git add apps/web/src/components/layout/UserShell/UserTopNav.tsx
git commit -m "feat(dashboard): add mobile responsive icon overflow and navbar adaptation"
```

---

## Task 10: Integration Smoke Test

**Files:**
- No new files — verify all components work together

- [ ] **Step 1: Run all dashboard tests**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/`
Expected: ALL PASS

- [ ] **Step 2: Run full frontend test suite**

Run: `cd apps/web && pnpm test`
Expected: ALL PASS (no regressions from changed components)

- [ ] **Step 3: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No type errors

- [ ] **Step 4: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: No lint errors

- [ ] **Step 5: Final commit if any fixes needed**

```bash
git add -u
git commit -m "fix(dashboard): resolve integration issues from dashboard navigation implementation"
```

- [ ] **Step 6: Create PR**

```bash
gh pr create --base frontend-dev --title "feat(dashboard): user dashboard & session navigation redesign" --body "$(cat <<'EOF'
## Summary
- Extend DashboardEngine xstate with tavolo/sheetOpen sub-states and card link navigation
- Add navbar transformation during active game sessions (SessionNavBar)
- Add bottom sheet system for sub-context navigation (scores, AI rules, timer, photos, players)
- Add card link graph navigation within sheets with breadcrumb
- Replace dashboard zones with carousel-based ExplorationView
- Add TavoloView for game mode with compact scoreboard, turn indicator, quick actions
- Mobile responsive with icon overflow menu

## Spec
docs/superpowers/specs/2026-03-22-user-dashboard-navigation-design.md

## Test plan
- [ ] DashboardEngine state machine transitions (unit)
- [ ] useDashboardMode hook API (unit)
- [ ] SessionNavBar rendering and interaction (unit)
- [ ] SessionSheet open/close/drag (unit)
- [ ] CardLinkChip and SheetBreadcrumb navigation (unit)
- [ ] ExplorationView and TavoloView layout (unit)
- [ ] Full dashboard test suite passes
- [ ] Typecheck and lint pass
- [ ] Manual test: exploration → game mode → sheet → card link → breadcrumb back

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
