# User Dashboard Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a context-aware dashboard that auto-switches between exploration mode and game mode, with cascade navigation (Mana Pip → DeckStack → Drawer) for fast in-session access.

**Architecture:** XState v5 state machine (DashboardEngine) orchestrates zone rendering and animated transitions. Zones are independent lazy-loaded React components. Session data comes from existing sessionStore (Zustand). Animations use View Transitions API with Framer Motion fallback.

**Tech Stack:** XState v5, Framer Motion, React Query, Zustand, Vitest, Playwright

**Spec:** `docs/superpowers/specs/2026-03-15-user-dashboard-design.md`

---

## Chunk 1: Foundation (Engine + Dependencies)

### Task 1: Install XState Dependencies

**Files:**
- Modify: `apps/web/package.json`

- [ ] **Step 1: Install xstate and React bindings**

Run: `cd apps/web && pnpm add xstate@^5 @xstate/react@^4`

- [ ] **Step 2: Verify installation**

Run: `cd apps/web && pnpm list xstate @xstate/react`
Expected: Both packages listed with versions 5.x and 4.x

- [ ] **Step 3: Commit**

```bash
git add apps/web/package.json apps/web/pnpm-lock.yaml
git commit -m "chore: add xstate v5 for dashboard engine"
```

---

### Task 2: Dashboard Engine State Machine

**Files:**
- Create: `apps/web/src/components/dashboard/DashboardEngine.ts`
- Test: `apps/web/src/components/dashboard/__tests__/DashboardEngine.test.ts`

The engine owns only UI transition state. Session data is read from sessionStore reactively.

- [ ] **Step 1: Write failing tests for state machine transitions**

```typescript
// apps/web/src/components/dashboard/__tests__/DashboardEngine.test.ts
import { describe, it, expect } from 'vitest';
import { createActor } from 'xstate';
import { dashboardMachine } from '../DashboardEngine';

describe('DashboardEngine', () => {
  it('starts in exploration state', () => {
    const actor = createActor(dashboardMachine);
    actor.start();
    expect(actor.getSnapshot().value).toBe('exploration');
    actor.stop();
  });

  it('transitions to transitioning on SESSION_DETECTED', () => {
    const actor = createActor(dashboardMachine);
    actor.start();
    actor.send({ type: 'SESSION_DETECTED', sessionId: 'sess-1' });
    expect(actor.getSnapshot().value).toBe('transitioning');
    expect(actor.getSnapshot().context.activeSessionId).toBe('sess-1');
    expect(actor.getSnapshot().context.transitionTarget).toBe('gameMode');
    actor.stop();
  });

  it('transitions to gameMode on TRANSITION_COMPLETE', () => {
    const actor = createActor(dashboardMachine);
    actor.start();
    actor.send({ type: 'SESSION_DETECTED', sessionId: 'sess-1' });
    actor.send({ type: 'TRANSITION_COMPLETE' });
    expect(actor.getSnapshot().value).toBe('gameMode');
    actor.stop();
  });

  it('transitions gameMode → transitioning on SESSION_COMPLETED', () => {
    const actor = createActor(dashboardMachine);
    actor.start();
    actor.send({ type: 'SESSION_DETECTED', sessionId: 'sess-1' });
    actor.send({ type: 'TRANSITION_COMPLETE' });
    actor.send({ type: 'SESSION_COMPLETED' });
    expect(actor.getSnapshot().value).toBe('transitioning');
    expect(actor.getSnapshot().context.transitionTarget).toBe('exploration');
    actor.stop();
  });

  it('buffers SESSION_COMPLETED during transitioning', () => {
    const actor = createActor(dashboardMachine);
    actor.start();
    // Start transition to gameMode
    actor.send({ type: 'SESSION_DETECTED', sessionId: 'sess-1' });
    expect(actor.getSnapshot().value).toBe('transitioning');
    // Session ends during transition
    actor.send({ type: 'SESSION_COMPLETED' });
    // Still transitioning (buffered)
    expect(actor.getSnapshot().value).toBe('transitioning');
    expect(actor.getSnapshot().context.bufferedEvents).toHaveLength(1);
    // Complete transition — should process buffer and reverse
    actor.send({ type: 'TRANSITION_COMPLETE' });
    // Engine should be transitioning back to exploration
    expect(actor.getSnapshot().value).toBe('transitioning');
    expect(actor.getSnapshot().context.transitionTarget).toBe('exploration');
    actor.stop();
  });

  it('handles gameModeExpanded sub-state', () => {
    const actor = createActor(dashboardMachine);
    actor.start();
    actor.send({ type: 'SESSION_DETECTED', sessionId: 'sess-1' });
    actor.send({ type: 'TRANSITION_COMPLETE' });
    actor.send({ type: 'EXPAND' });
    expect(actor.getSnapshot().value).toEqual({ gameMode: 'expanded' });
    actor.send({ type: 'COLLAPSE' });
    expect(actor.getSnapshot().value).toEqual({ gameMode: 'default' });
    actor.stop();
  });

  it('clears activeSessionId on exploration entry', () => {
    const actor = createActor(dashboardMachine);
    actor.start();
    actor.send({ type: 'SESSION_DETECTED', sessionId: 'sess-1' });
    actor.send({ type: 'TRANSITION_COMPLETE' });
    actor.send({ type: 'SESSION_COMPLETED' });
    actor.send({ type: 'TRANSITION_COMPLETE' });
    expect(actor.getSnapshot().value).toBe('exploration');
    expect(actor.getSnapshot().context.activeSessionId).toBeNull();
    actor.stop();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/DashboardEngine.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the state machine**

```typescript
// apps/web/src/components/dashboard/DashboardEngine.ts
import { setup, assign } from 'xstate';

export type DashboardEvent =
  | { type: 'SESSION_DETECTED'; sessionId: string }
  | { type: 'SESSION_COMPLETED' }
  | { type: 'SESSION_DISMISSED' }
  | { type: 'TRANSITION_COMPLETE' }
  | { type: 'EXPAND' }
  | { type: 'COLLAPSE' };

export interface DashboardEngineContext {
  activeSessionId: string | null;
  transitionType: 'morph' | 'fade' | 'slide' | 'none';
  transitionTarget: 'exploration' | 'gameMode' | null;
  previousState: 'exploration' | 'gameMode' | null;
  bufferedEvents: DashboardEvent[];
}

export const dashboardMachine = setup({
  types: {
    context: {} as DashboardEngineContext,
    events: {} as DashboardEvent,
  },
  actions: {
    // XState v5 parameterized action: (_, params) form inside setup()
    setSessionDetected: assign((_, params: { sessionId: string }) => ({
      activeSessionId: params.sessionId,
      transitionTarget: 'gameMode' as const,
      transitionType: 'morph' as const,
      previousState: 'exploration' as const,
      bufferedEvents: [] as DashboardEvent[],
    })),
    setSessionEnding: assign(() => ({
      transitionTarget: 'exploration' as const,
      transitionType: 'morph' as const,
      previousState: 'gameMode' as const,
      bufferedEvents: [] as DashboardEvent[],
    })),
    bufferEvent: assign(({ context, event }) => ({
      bufferedEvents: [...context.bufferedEvents, event],
    })),
    clearSession: assign(() => ({
      activeSessionId: null,
      transitionTarget: null,
      previousState: null,
      bufferedEvents: [] as DashboardEvent[],
    })),
    clearTransition: assign(() => ({
      transitionTarget: null,
      bufferedEvents: [] as DashboardEvent[],
    })),
    processBuffer: assign(({ context }) => {
      const hasTermination = context.bufferedEvents.some(
        e => e.type === 'SESSION_COMPLETED' || e.type === 'SESSION_DISMISSED'
      );
      return {
        transitionTarget: hasTermination ? ('exploration' as const) : null,
        previousState: hasTermination ? context.transitionTarget : null,
        bufferedEvents: [] as DashboardEvent[],
      };
    }),
  },
  guards: {
    hasBufferedTermination: ({ context }) =>
      context.bufferedEvents.some(
        e => e.type === 'SESSION_COMPLETED' || e.type === 'SESSION_DISMISSED'
      ),
  },
}).createMachine({
  id: 'dashboard',
  initial: 'exploration',
  context: {
    activeSessionId: null,
    transitionType: 'none',
    transitionTarget: null,
    previousState: null,
    bufferedEvents: [],
  },
  states: {
    exploration: {
      on: {
        SESSION_DETECTED: {
          target: 'transitioning',
          actions: {
            type: 'setSessionDetected',
            params: ({ event }) => ({ sessionId: event.sessionId }),
          },
        },
      },
    },
    transitioning: {
      on: {
        SESSION_COMPLETED: { actions: 'bufferEvent' },
        SESSION_DISMISSED: { actions: 'bufferEvent' },
        TRANSITION_COMPLETE: [
          {
            target: 'transitioning',
            reenter: true, // Force re-entry to restart animation cycle
            guard: 'hasBufferedTermination',
            actions: 'processBuffer',
          },
          {
            target: 'gameMode',
            guard: ({ context }) => context.transitionTarget === 'gameMode',
            actions: 'clearTransition',
          },
          {
            target: 'exploration',
            actions: 'clearSession',
          },
        ],
      },
    },
    gameMode: {
      initial: 'default',
      on: {
        SESSION_COMPLETED: {
          target: 'transitioning',
          actions: 'setSessionEnding',
        },
        SESSION_DISMISSED: {
          target: 'transitioning',
          actions: 'setSessionEnding',
        },
      },
      states: {
        default: {
          on: { EXPAND: 'expanded' },
        },
        expanded: {
          on: { COLLAPSE: 'default' },
        },
      },
    },
  },
});
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/DashboardEngine.test.ts`
Expected: All 7 tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/dashboard/DashboardEngine.ts apps/web/src/components/dashboard/__tests__/DashboardEngine.test.ts
git commit -m "feat(dashboard): add XState v5 state machine for context switching"
```

---

### Task 3: Dashboard Engine Provider + useDashboardMode Hook

**Files:**
- Create: `apps/web/src/components/dashboard/DashboardEngineProvider.tsx`
- Create: `apps/web/src/components/dashboard/useDashboardMode.ts`
- Test: `apps/web/src/components/dashboard/__tests__/useDashboardMode.test.tsx`

- [ ] **Step 1: Write failing test for the hook**

```typescript
// apps/web/src/components/dashboard/__tests__/useDashboardMode.test.tsx
import { describe, it, expect } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { DashboardEngineProvider } from '../DashboardEngineProvider';
import { useDashboardMode } from '../useDashboardMode';

function wrapper({ children }: { children: React.ReactNode }) {
  return <DashboardEngineProvider>{children}</DashboardEngineProvider>;
}

describe('useDashboardMode', () => {
  it('returns exploration as initial state', () => {
    const { result } = renderHook(() => useDashboardMode(), { wrapper });
    expect(result.current.state).toBe('exploration');
    expect(result.current.isExploration).toBe(true);
    expect(result.current.isGameMode).toBe(false);
  });

  it('sends SESSION_DETECTED and transitions', () => {
    const { result } = renderHook(() => useDashboardMode(), { wrapper });
    act(() => {
      result.current.send({ type: 'SESSION_DETECTED', sessionId: 'sess-1' });
    });
    expect(result.current.isTransitioning).toBe(true);
  });

  it('provides activeSessionId from context', () => {
    const { result } = renderHook(() => useDashboardMode(), { wrapper });
    act(() => {
      result.current.send({ type: 'SESSION_DETECTED', sessionId: 'sess-1' });
    });
    expect(result.current.activeSessionId).toBe('sess-1');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/useDashboardMode.test.tsx`
Expected: FAIL — modules not found

- [ ] **Step 3: Implement Provider and hook**

```typescript
// apps/web/src/components/dashboard/DashboardEngineProvider.tsx
'use client';

import { createContext, useEffect, useRef } from 'react';
import { createActor, type ActorRefFrom } from 'xstate';
import { useActorRef } from '@xstate/react';

import { useSessionStore } from '@/lib/stores/sessionStore';

import { dashboardMachine } from './DashboardEngine';

export type DashboardActorRef = ActorRefFrom<typeof dashboardMachine>;

export const DashboardEngineContext = createContext<DashboardActorRef | null>(null);

export function DashboardEngineProvider({ children }: { children: React.ReactNode }) {
  const actorRef = useActorRef(dashboardMachine);

  // Session detection: track null→non-null and non-null→null transitions only
  const activeSession = useSessionStore(s => s.activeSession);
  const prevSessionRef = useRef(activeSession);

  useEffect(() => {
    const prev = prevSessionRef.current;
    prevSessionRef.current = activeSession;

    if (!prev && activeSession) {
      // null → session: new session detected
      actorRef.send({ type: 'SESSION_DETECTED', sessionId: activeSession.id });
    } else if (prev && !activeSession) {
      // session → null: session ended
      actorRef.send({ type: 'SESSION_COMPLETED' });
    }
  }, [activeSession, actorRef]);

  // Handle initial mount: if session already active, detect immediately
  useEffect(() => {
    const session = useSessionStore.getState().activeSession;
    if (session) {
      actorRef.send({ type: 'SESSION_DETECTED', sessionId: session.id });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <DashboardEngineContext.Provider value={actorRef}>
      {children}
    </DashboardEngineContext.Provider>
  );
}
```

```typescript
// apps/web/src/components/dashboard/useDashboardMode.ts
'use client';

import { useContext } from 'react';
import { useSelector } from '@xstate/react';

import { DashboardEngineContext, type DashboardActorRef } from './DashboardEngineProvider';
import type { DashboardEvent } from './DashboardEngine';

export function useDashboardMode() {
  const actorRef = useContext(DashboardEngineContext);

  if (!actorRef) {
    // Fallback for components outside DashboardEngineProvider
    return {
      state: 'exploration' as const,
      isExploration: true,
      isGameMode: false,
      isTransitioning: false,
      activeSessionId: null,
      transitionTarget: null,
      send: (() => {}) as (event: DashboardEvent) => void,
    };
  }

  const snapshot = useSelector(actorRef, s => s);
  const stateValue = snapshot.value;

  // Normalize nested state (gameMode.default, gameMode.expanded)
  const topState = typeof stateValue === 'string' ? stateValue : 'gameMode';

  return {
    state: topState as 'exploration' | 'transitioning' | 'gameMode',
    isExploration: topState === 'exploration',
    isGameMode: topState === 'gameMode',
    isTransitioning: topState === 'transitioning',
    isExpanded: typeof stateValue === 'object' && 'gameMode' in stateValue && stateValue.gameMode === 'expanded',
    activeSessionId: snapshot.context.activeSessionId,
    transitionTarget: snapshot.context.transitionTarget,
    send: actorRef.send,
  };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/useDashboardMode.test.tsx`
Expected: All 3 tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/dashboard/DashboardEngineProvider.tsx apps/web/src/components/dashboard/useDashboardMode.ts apps/web/src/components/dashboard/__tests__/useDashboardMode.test.tsx
git commit -m "feat(dashboard): add DashboardEngineProvider and useDashboardMode hook"
```

---

### Task 4: Cascade Navigation Store (Zustand)

**Files:**
- Create: `apps/web/src/lib/stores/cascadeNavigationStore.ts`
- Test: `apps/web/src/lib/stores/__tests__/cascadeNavigationStore.test.ts`

- [ ] **Step 1: Write failing tests**

```typescript
// apps/web/src/lib/stores/__tests__/cascadeNavigationStore.test.ts
import { describe, it, expect, beforeEach } from 'vitest';
import { useCascadeNavigation } from '../cascadeNavigationStore';

describe('cascadeNavigationStore', () => {
  beforeEach(() => {
    useCascadeNavigation.getState().closeCascade();
  });

  it('starts in closed state', () => {
    const state = useCascadeNavigation.getState();
    expect(state.state).toBe('closed');
    expect(state.activeEntityType).toBeNull();
    expect(state.activeEntityId).toBeNull();
  });

  it('opens deck stack', () => {
    const { openDeckStack } = useCascadeNavigation.getState();
    openDeckStack('kb', 'game-1');
    const state = useCascadeNavigation.getState();
    expect(state.state).toBe('deckStack');
    expect(state.activeEntityType).toBe('kb');
    expect(state.deckStackSkipped).toBe(false);
  });

  it('opens drawer from deck stack', () => {
    const store = useCascadeNavigation.getState();
    store.openDeckStack('kb', 'game-1');
    store.openDrawer('kb', 'doc-1');
    const state = useCascadeNavigation.getState();
    expect(state.state).toBe('drawer');
    expect(state.activeEntityId).toBe('doc-1');
    expect(state.deckStackSkipped).toBe(false);
  });

  it('closeDrawer returns to deckStack when not skipped', () => {
    const store = useCascadeNavigation.getState();
    store.openDeckStack('kb', 'game-1');
    store.openDrawer('kb', 'doc-1');
    store.closeDrawer();
    expect(useCascadeNavigation.getState().state).toBe('deckStack');
  });

  it('closeDrawer returns to closed when skipped', () => {
    const store = useCascadeNavigation.getState();
    // Direct drawer open (single item skip)
    store.openDrawer('kb', 'doc-1');
    expect(useCascadeNavigation.getState().deckStackSkipped).toBe(true);
    store.closeDrawer();
    expect(useCascadeNavigation.getState().state).toBe('closed');
  });

  it('closeCascade resets everything', () => {
    const store = useCascadeNavigation.getState();
    store.openDeckStack('kb', 'game-1');
    store.openDrawer('kb', 'doc-1');
    store.closeCascade();
    const state = useCascadeNavigation.getState();
    expect(state.state).toBe('closed');
    expect(state.activeEntityType).toBeNull();
    expect(state.activeEntityId).toBeNull();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/lib/stores/__tests__/cascadeNavigationStore.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the store**

```typescript
// apps/web/src/lib/stores/cascadeNavigationStore.ts
import { create } from 'zustand';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card-styles';

interface CascadeNavigationState {
  state: 'closed' | 'deckStack' | 'drawer';
  activeEntityType: MeepleEntityType | null;
  activeEntityId: string | null;
  sourceEntityId: string | null;
  anchorRect: DOMRect | null;
  deckStackSkipped: boolean;
  openDeckStack: (entityType: MeepleEntityType, sourceEntityId: string, anchor?: DOMRect) => void;
  openDrawer: (entityType: MeepleEntityType, entityId: string) => void;
  closeDrawer: () => void;
  closeCascade: () => void;
}

export const useCascadeNavigation = create<CascadeNavigationState>((set, get) => ({
  state: 'closed',
  activeEntityType: null,
  activeEntityId: null,
  sourceEntityId: null,
  anchorRect: null,
  deckStackSkipped: false,

  openDeckStack: (entityType, sourceEntityId, anchor) =>
    set({
      state: 'deckStack',
      activeEntityType: entityType,
      activeEntityId: null,
      sourceEntityId,
      anchorRect: anchor ?? null,
      deckStackSkipped: false,
    }),

  openDrawer: (entityType, entityId) => {
    const current = get();
    set({
      state: 'drawer',
      activeEntityType: entityType,
      activeEntityId: entityId,
      // If opening drawer without going through deckStack first, mark as skipped
      deckStackSkipped: current.state !== 'deckStack',
    });
  },

  closeDrawer: () => {
    const { deckStackSkipped } = get();
    if (deckStackSkipped) {
      // DeckStack was never shown — close entirely
      set({
        state: 'closed',
        activeEntityType: null,
        activeEntityId: null,
        sourceEntityId: null,
        anchorRect: null,
        deckStackSkipped: false,
      });
    } else {
      // Return to deckStack
      set(prev => ({
        state: 'deckStack',
        activeEntityId: null,
      }));
    }
  },

  closeCascade: () =>
    set({
      state: 'closed',
      activeEntityType: null,
      activeEntityId: null,
      sourceEntityId: null,
      anchorRect: null,
      deckStackSkipped: false,
    }),
}));
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/lib/stores/__tests__/cascadeNavigationStore.test.ts`
Expected: All 6 tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/stores/cascadeNavigationStore.ts apps/web/src/lib/stores/__tests__/cascadeNavigationStore.test.ts
git commit -m "feat(dashboard): add cascade navigation Zustand store (pip→stack→drawer)"
```

---

### Task 5: Dashboard Transitions CSS

**Files:**
- Create: `apps/web/src/components/dashboard/dashboard-transitions.css`

- [ ] **Step 1: Create transition definitions**

```css
/* apps/web/src/components/dashboard/dashboard-transitions.css */

/* View Transitions API — Progressive Enhancement (Chrome/Edge) */
@supports (view-transition-name: hero-to-session) {
  .dashboard-hero {
    view-transition-name: hero-to-session;
  }

  ::view-transition-old(hero-to-session) {
    animation: dashboard-morph-out 300ms ease-out forwards;
  }

  ::view-transition-new(hero-to-session) {
    animation: dashboard-morph-in 300ms ease-out forwards;
  }
}

/* Fallback keyframes (used by Framer Motion when View Transitions unavailable) */
@keyframes dashboard-morph-out {
  from { opacity: 1; transform: scale(1); }
  to { opacity: 0; transform: scale(0.95); }
}

@keyframes dashboard-morph-in {
  from { opacity: 0; transform: scale(1.05); }
  to { opacity: 1; transform: scale(1); }
}

@keyframes dashboard-zone-fade-in {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}

@keyframes dashboard-zone-fade-out {
  from { opacity: 1; transform: translateY(0); }
  to { opacity: 0; transform: translateY(-8px); }
}

@keyframes dashboard-panel-slide-in {
  from { opacity: 0; transform: translateY(20px); max-height: 0; }
  to { opacity: 1; transform: translateY(0); max-height: 400px; }
}

@keyframes dashboard-panel-slide-out {
  from { opacity: 1; transform: translateY(0); max-height: 400px; }
  to { opacity: 0; transform: translateY(20px); max-height: 0; }
}

/* Session panel slot animation */
.session-panel-enter {
  animation: dashboard-panel-slide-in 300ms ease-out forwards;
}

.session-panel-exit {
  animation: dashboard-panel-slide-out 300ms ease-out forwards;
}

/* Live dot pulse */
@keyframes live-pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.4; }
}

.live-dot {
  animation: live-pulse 2s ease-in-out infinite;
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/dashboard/dashboard-transitions.css
git commit -m "feat(dashboard): add View Transitions CSS with Framer Motion fallback keyframes"
```

---

### Task 6: Dashboard barrel export

**Files:**
- Create: `apps/web/src/components/dashboard/index.ts`

- [ ] **Step 1: Create barrel export**

```typescript
// apps/web/src/components/dashboard/index.ts
export { dashboardMachine } from './DashboardEngine';
export type { DashboardEvent, DashboardEngineContext } from './DashboardEngine';
export { DashboardEngineProvider } from './DashboardEngineProvider';
export { useDashboardMode } from './useDashboardMode';
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/dashboard/index.ts
git commit -m "feat(dashboard): add barrel export for dashboard module"
```

---

## Chunk 2: Exploration Mode Zones

### Task 7: HeroZone Component

**Files:**
- Create: `apps/web/src/components/dashboard/zones/HeroZone.tsx`
- Test: `apps/web/src/components/dashboard/zones/__tests__/HeroZone.test.tsx`

Reference existing: `apps/web/src/components/dashboard-v2/hero-zone.tsx` for data patterns.

- [ ] **Step 1: Write failing test**

```typescript
// apps/web/src/components/dashboard/zones/__tests__/HeroZone.test.tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClientProvider, QueryClient } from '@tanstack/react-query';
import { HeroZone } from '../HeroZone';

// Mock the dashboard API hook
vi.mock('@/hooks/queries/useDashboardData', () => ({
  useDashboardData: () => ({
    data: {
      user: { displayName: 'Marco' },
      stats: { libraryCount: 12, playedLast30Days: 5, chatCount: 3, currentStreak: 4 },
    },
    isLoading: false,
  }),
}));

const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

function renderWithProviders(ui: React.ReactElement) {
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe('HeroZone', () => {
  it('renders greeting with user name', () => {
    renderWithProviders(<HeroZone />);
    expect(screen.getByText(/Marco/)).toBeInTheDocument();
  });

  it('renders skeleton when loading', async () => {
    // Override mock for this test via vi.mock factory reset
    const { useDashboardData } = await import('@/hooks/queries/useDashboardData');
    vi.mocked(useDashboardData).mockReturnValueOnce({
      data: null,
      isLoading: true,
    });
    renderWithProviders(<HeroZone />);
    expect(screen.getByTestId('hero-zone-skeleton')).toBeInTheDocument();
  });

  it('has view-transition-name for morph animation', () => {
    renderWithProviders(<HeroZone />);
    const hero = screen.getByTestId('hero-zone');
    expect(hero.className).toContain('dashboard-hero');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/zones/__tests__/HeroZone.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement HeroZone**

Port logic from existing `apps/web/src/components/dashboard-v2/hero-zone.tsx`, adapting it as a self-contained zone with its own data fetching via React Query. Include `className="dashboard-hero"` for View Transitions and `data-testid="hero-zone"`. Show skeleton via `data-testid="hero-zone-skeleton"` when loading.

The component should render:
- Greeting with user name and time-based message
- Next game night banner (if upcoming)
- Welcome onboarding prompt (if new user)

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/zones/__tests__/HeroZone.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/dashboard/zones/HeroZone.tsx apps/web/src/components/dashboard/zones/__tests__/HeroZone.test.tsx
git commit -m "feat(dashboard): add HeroZone component with greeting and contextual banner"
```

---

### Task 8: StatsZone Component

**Files:**
- Create: `apps/web/src/components/dashboard/zones/StatsZone.tsx`
- Test: `apps/web/src/components/dashboard/zones/__tests__/StatsZone.test.tsx`

Reference existing: `apps/web/src/components/dashboard-v2/quick-stats.tsx`

- [ ] **Step 1: Write failing test**

Test that it renders 4 KPI stat cards (library count, games last 30d, active chats, streak) with correct values from the dashboard API. Test skeleton state.

- [ ] **Step 2: Run test to verify it fails**
- [ ] **Step 3: Implement StatsZone**

Compact horizontal KPI bar. Share the same React Query key as HeroZone (`['dashboard']`) so data is fetched once. Render 4 `stat-card` items with icons and values.

- [ ] **Step 4: Run test to verify it passes**
- [ ] **Step 5: Commit**

```bash
git commit -m "feat(dashboard): add StatsZone KPI bar component"
```

---

### Task 9: CardsZone Component

**Files:**
- Create: `apps/web/src/components/dashboard/zones/CardsZone.tsx`
- Test: `apps/web/src/components/dashboard/zones/__tests__/CardsZone.test.tsx`

Reference existing: `apps/web/src/components/dashboard-v2/recent-games-section.tsx`, `recent-sessions.tsx`, `recent-chats-section.tsx`

- [ ] **Step 1: Write failing test**

Test that it renders 3 horizontal scroll sections (Recent Games, Recent Sessions, Active Chats), each containing `MeepleCard variant="grid"` components. Use MSW or vi.mock for API calls:
- Games: `GET /api/v1/library?pageSize=8&sortBy=addedAt&sortDescending=true`
- Sessions: `GET /api/v1/game-sessions/history` + `GET /api/v1/live-sessions/active`
- Chats: `GET /api/v1/knowledge-base/my-chats?skip=0&take=6`

- [ ] **Step 2: Run test to verify it fails**
- [ ] **Step 3: Implement CardsZone**

Three horizontal scroll sections, each with its own React Query hook. Use `MeepleCard` with entity-appropriate props. Add empty states per section.

- [ ] **Step 4: Run test to verify it passes**
- [ ] **Step 5: Commit**

```bash
git commit -m "feat(dashboard): add CardsZone with horizontal scroll sections"
```

---

### Task 10: AgentsSidebar Component

**Files:**
- Create: `apps/web/src/components/dashboard/zones/AgentsSidebar.tsx`
- Test: `apps/web/src/components/dashboard/zones/__tests__/AgentsSidebar.test.tsx`

Reference existing: `apps/web/src/components/dashboard-v2/agents-section.tsx`

- [ ] **Step 1: Write failing test**

Test rendering agent cards from `GET /api/v1/agents`, desktop sidebar layout vs mobile stacked section.

- [ ] **Step 2-5: Implement, test, commit**

```bash
git commit -m "feat(dashboard): add AgentsSidebar zone component"
```

---

## Chunk 3: Game Mode Zones

### Task 11: SessionBar Component (Floating Glassmorphism Bar)

**Files:**
- Create: `apps/web/src/components/dashboard/zones/SessionBar.tsx`
- Test: `apps/web/src/components/dashboard/zones/__tests__/SessionBar.test.tsx`

- [ ] **Step 1: Write failing test**

Test that SessionBar renders game name, live dot, leader scores, and mana pip row. Test that it reads from `useSessionStore()` not from DashboardEngine context. Test `className="dashboard-hero"` for morph animation.

- [ ] **Step 2-4: Implement, test, verify**

Floating bar with glassmorphism: `bg-background/85 backdrop-blur-xl border border-white/10 shadow-lg rounded-2xl`. Include mana pip row that triggers `useCascadeNavigation.openDeckStack()`.

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(dashboard): add SessionBar floating glassmorphism zone"
```

---

### Task 12: ScoreboardZone Component

**Files:**
- Create: `apps/web/src/components/dashboard/zones/ScoreboardZone.tsx`
- Test: `apps/web/src/components/dashboard/zones/__tests__/ScoreboardZone.test.tsx`

Reference existing: `apps/web/src/components/session/Scoreboard.tsx`

- [ ] **Step 1-5: Test → Implement → Verify → Commit**

Wrap existing `Scoreboard` component with zone-specific layout and Suspense boundary. Read from `useSessionStore()`.

```bash
git commit -m "feat(dashboard): add ScoreboardZone wrapping existing Scoreboard component"
```

---

### Task 13: Zones barrel export

**Files:**
- Create: `apps/web/src/components/dashboard/zones/index.ts`

- [ ] **Step 1: Create lazy exports**

```typescript
// apps/web/src/components/dashboard/zones/index.ts
import { lazy } from 'react';

export const HeroZone = lazy(() => import('./HeroZone').then(m => ({ default: m.HeroZone })));
export const StatsZone = lazy(() => import('./StatsZone').then(m => ({ default: m.StatsZone })));
export const CardsZone = lazy(() => import('./CardsZone').then(m => ({ default: m.CardsZone })));
export const AgentsSidebar = lazy(() => import('./AgentsSidebar').then(m => ({ default: m.AgentsSidebar })));
export const SessionBar = lazy(() => import('./SessionBar').then(m => ({ default: m.SessionBar })));
export const ScoreboardZone = lazy(() => import('./ScoreboardZone').then(m => ({ default: m.ScoreboardZone })));
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/dashboard/zones/index.ts
git commit -m "feat(dashboard): add lazy-loaded zone barrel exports"
```

---

## Chunk 4: Dashboard Renderer + Integration

### Task 14: DashboardRenderer Component

**Files:**
- Create: `apps/web/src/components/dashboard/DashboardRenderer.tsx`
- Test: `apps/web/src/components/dashboard/__tests__/DashboardRenderer.test.tsx`

- [ ] **Step 1: Write failing test**

Test that DashboardRenderer renders exploration zones when state is `exploration` and game mode zones when state is `gameMode`. Mock `useDashboardMode`.

- [ ] **Step 2-4: Implement, test, verify**

Use `AnimatePresence` from framer-motion with `mode="wait"`. Wrap HeroZone and SessionBar with `motion.div layoutId="hero"` for morph animation. Each zone wrapped in `Suspense` with skeleton fallback. Import CSS transitions file.

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(dashboard): add DashboardRenderer with animated zone switching"
```

---

### Task 15: Wire DashboardEngineProvider into UnifiedShell

**Files:**
- Modify: `apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx`

- [ ] **Step 1: Read the existing file**

Run: Read `apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx` to understand current structure.

- [ ] **Step 2: Wrap shell content with DashboardEngineProvider**

Add import for `DashboardEngineProvider` from `@/components/dashboard`. Wrap the children/content area with `<DashboardEngineProvider>`.

- [ ] **Step 3: Verify the app still builds**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds with no errors

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(dashboard): wire DashboardEngineProvider into UnifiedShell"
```

---

### Task 16: Replace gaming-hub-client with DashboardRenderer

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/page.tsx`

- [ ] **Step 1: Read current page file**

Run: Read `apps/web/src/app/(authenticated)/dashboard/page.tsx`

- [ ] **Step 2: Replace import**

Change the import from `gaming-hub-client` to `DashboardRenderer` from `@/components/dashboard/DashboardRenderer`. Keep existing server-side data fetching and props passing.

- [ ] **Step 3: Verify build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(dashboard): replace gaming-hub-client with DashboardRenderer"
```

---

## Chunk 5: Session Panel + CardStack Integration

### Task 17: SessionPanel Component

**Files:**
- Create: `apps/web/src/components/dashboard/SessionPanel.tsx`
- Create: `apps/web/src/components/dashboard/SessionPanelCollapsed.tsx`
- Create: `apps/web/src/components/dashboard/useSessionSlot.ts`
- Test: `apps/web/src/components/dashboard/__tests__/SessionPanel.test.tsx`

- [ ] **Step 1: Write failing test for SessionPanel**

Test mini-scoreboard rendering, mana pip row (KB, Agent, Players), quick actions (Pause/Resume, Chat Agent), live dot, game name. Mock `useSessionStore` and `useDashboardMode`.

- [ ] **Step 2-4: Implement SessionPanel, SessionPanelCollapsed, useSessionSlot**

`SessionPanel.tsx`: Full expanded version (180px CardStack). Includes mini-scoreboard (top 3 players), mana pip row triggering `useCascadeNavigation.openDeckStack()`, quick action buttons, "Go to scoreboard →" link.

`SessionPanelCollapsed.tsx`: 56px version with session icon + pulsing `.live-dot`.

`useSessionSlot.ts`: Bridge hook that reads `useDashboardMode().isGameMode` + `useSessionStore()` data → returns SessionPanel props.

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(dashboard): add SessionPanel with mini-scoreboard and mana pip row"
```

---

### Task 18: CardStack Dynamic Slot Integration

**Files:**
- Modify: `apps/web/src/components/layout/UnifiedShell/CardStack.tsx`

- [ ] **Step 1: Read existing CardStack**

Run: Read `apps/web/src/components/layout/UnifiedShell/CardStack.tsx`

- [ ] **Step 2: Add DynamicSlot between dynamic and pinned sections**

Insert a `<DynamicSlot>` wrapper between the dynamic `HandCard` entries and the pinned section. When `useDashboardMode().isGameMode` is true, render `<SessionPanel />` (expanded) or `<SessionPanelCollapsed />` (collapsed based on CardStack expand state). Animate with framer-motion `layout` prop for smooth reflow.

- [ ] **Step 3: Verify CardStack still works without active session**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/`
Expected: Existing CardStack tests still pass

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(dashboard): add dynamic session slot to CardStack"
```

---

### Task 19: HandDrawer SessionPanel Integration (Mobile)

**Files:**
- Modify: `apps/web/src/components/layout/UnifiedShell/HandDrawer.tsx`

- [ ] **Step 1: Read existing HandDrawer**
- [ ] **Step 2: Add SessionPanel as first item when gameMode active**

When `useDashboardMode().isGameMode` is true, prepend `<SessionPanel />` as the first card in the HandDrawer before the existing HandCard entries.

- [ ] **Step 3: Run existing HandDrawer tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/HandDrawer`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(dashboard): add SessionPanel to HandDrawer for mobile game mode"
```

---

## Chunk 6: DeckStack + Cascade Navigation

### Task 20: DeckStack Presentation Prop

**Files:**
- Modify: `apps/web/src/components/ui/data-display/deck-stack/DeckStack.tsx`
- Modify: `apps/web/src/components/ui/data-display/deck-stack/deck-stack-types.ts`
- Test: `apps/web/src/components/ui/data-display/deck-stack/__tests__/DeckStack.test.tsx`

- [ ] **Step 1: Add `presentation` prop to DeckStack types**

Add `presentation?: 'popover' | 'bottomSheet'` to `DeckStackProps` in `deck-stack-types.ts`. Default to `'popover'` for backward compatibility.

- [ ] **Step 2: Implement bottomSheet rendering path in DeckStack**

When `presentation === 'bottomSheet'`, render as a fixed bottom sheet instead of portal-anchored popover. Use existing Sheet component from `@/components/ui/navigation/sheet`.

- [ ] **Step 3: Run existing DeckStack tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/deck-stack/__tests__/`
Expected: PASS (existing tests use default popover behavior)

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(deck-stack): add presentation prop for popover/bottomSheet modes"
```

---

### Task 21: ManaLinkFooter Cascade Integration

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/ManaLinkFooter.tsx`

- [ ] **Step 1: Read existing ManaLinkFooter**
- [ ] **Step 2: Add cascade onClick to mana pips**

When `onPipClick` is not provided by consumer, use `useCascadeNavigation().openDeckStack()` as default handler. Pass the clicked pip's DOM rect as anchor for popover positioning.

- [ ] **Step 3: Run existing ManaLinkFooter tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/meeple-card-features/__tests__/ManaLinkFooter`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(mana-footer): integrate cascade navigation on pip click"
```

---

### Task 21b: ExtraMeepleCardDrawer Session Context

**Files:**
- Modify: `apps/web/src/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer.tsx`

The spec requires the drawer to be "session context aware" — when opened during game mode, it should highlight relevance to the current game (e.g., show "Currently playing this game" badge, prioritize session-relevant content).

- [ ] **Step 1: Read existing ExtraMeepleCardDrawer**

Run: Read `apps/web/src/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer.tsx`

- [ ] **Step 2: Add session context awareness**

Add `useDashboardMode()` hook. When `isGameMode && activeSessionId`, and the drawer's entity is linked to the current session's game, show a subtle indicator (e.g., indigo border glow or "In sessione" badge).

- [ ] **Step 3: Run existing drawer tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/extra-meeple-card/__tests__/ExtraMeepleCardDrawer`
Expected: PASS — no regression

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(drawer): add session context awareness to ExtraMeepleCardDrawer"
```

---

## Chunk 7: Backend Endpoint

### Task 22: KB Documents List Endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGameDocuments/GetGameDocumentsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGameDocuments/GetGameDocumentsHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGameDocuments/GameDocumentDto.cs`
- Modify: `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetGameDocumentsHandlerTests.cs`

Follow CQRS pattern: Query → Handler → DTO. Use `IMediator.Send()` in endpoint.

- [ ] **Step 1: Write failing test**

Test that `GetGameDocumentsHandler` returns documents filtered by `gameId` where user has access. Test empty result for unknown game. Test that only non-deleted documents are returned.

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api && dotnet test --filter "GetGameDocuments"`
Expected: FAIL — classes not found

- [ ] **Step 3: Implement Query, Handler, DTO**

```csharp
// GameDocumentDto.cs
public record GameDocumentDto(
    Guid Id,
    string Title,
    string Status,  // "indexed" | "processing" | "failed"
    int PageCount,
    DateTime CreatedAt
);

// GetGameDocumentsQuery.cs
public record GetGameDocumentsQuery(Guid GameId, Guid UserId) : IRequest<IReadOnlyList<GameDocumentDto>>;

// GetGameDocumentsHandler.cs
// Query VectorDocument entities filtered by GameId, project to DTO
```

- [ ] **Step 4: Add endpoint to routing**

Add to `KnowledgeBaseEndpoints.cs`:
```csharp
group.MapGet("/knowledge-base/{gameId:guid}/documents", HandleGetGameDocuments)
    .RequireAuthenticatedUser()
    .WithName("GetGameDocuments");
```

Handler: `async (Guid gameId, IMediator mediator, ClaimsPrincipal user) => ...`

- [ ] **Step 5: Run tests**

Run: `cd apps/api && dotnet test --filter "GetGameDocuments"`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(kb): add GET /knowledge-base/{gameId}/documents endpoint for cascade navigation"
```

---

## Chunk 8: E2E Testing + Polish

### Task 23: E2E Test — Exploration → Game Mode Transition

**Files:**
- Create: `apps/web/e2e/flows/dashboard-context-switch.spec.ts`

- [ ] **Step 1: Write E2E test**

Test the full flow on staging:
1. Navigate to `/dashboard` → verify exploration mode (HeroZone, StatsZone, CardsZone visible)
2. Start a session (or mock SSE) → verify transition animation plays
3. Verify game mode (SessionBar, ScoreboardZone visible)
4. Verify SessionPanel appears in CardStack
5. Click mana pip → verify DeckStack opens
6. Click DeckStack card → verify Drawer opens
7. Close drawer → verify return to DeckStack
8. End session → verify return to exploration mode

- [ ] **Step 2: Run E2E**

Run: `cd apps/web && pnpm playwright test e2e/flows/dashboard-context-switch.spec.ts`

- [ ] **Step 3: Commit**

```bash
git commit -m "test(e2e): add dashboard context switch and cascade navigation flow"
```

---

### Task 24: Final Integration Verification

- [ ] **Step 1: Run full frontend test suite**

Run: `cd apps/web && pnpm test`
Expected: All tests pass, no regressions

- [ ] **Step 2: Run type check**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 3: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: No errors

- [ ] **Step 4: Run backend tests**

Run: `cd apps/api/src/Api && dotnet test`
Expected: All tests pass

- [ ] **Step 5: Final commit with any fixes**

```bash
git commit -m "chore(dashboard): fix any remaining lint/type issues from dashboard integration"
```

---

## Task Dependency Graph

```
Task 1 (deps) → Task 2 (engine) → Task 3 (provider/hook) → Task 6 (barrel)
                                                          ↓
Task 4 (cascade store) ─────────────────────────────────→ Task 21 (mana footer)
Task 5 (CSS) ─────────────────────────────────────────→ Task 14 (renderer)
                                                          ↓
Tasks 7-10 (exploration zones) ──→ Task 13 (lazy barrel) → Task 14 (renderer)
Tasks 11-12 (game mode zones) ──↗                         ↓
                                                       Task 15 (wire shell)
                                                          ↓
                                                       Task 16 (replace page)
                                                          ↓
Task 17 (session panel) → Task 18 (cardstack slot) → Task 19 (hand drawer)
Task 20 (deck-stack prop) → Task 21 (mana footer)
Task 22 (backend endpoint) — independent, can be done in parallel
Task 23 (E2E) — after all above
Task 24 (verification) — after all above
```

**Parallelizable groups:**
- Tasks 7, 8, 9, 10 (exploration zones) — independent of each other
- Tasks 11, 12 (game mode zones) — independent of each other
- Task 22 (backend) — independent of all frontend tasks
- Task 4 (cascade store) — independent of Tasks 2, 3
- Task 5 (CSS) — independent of all logic tasks
