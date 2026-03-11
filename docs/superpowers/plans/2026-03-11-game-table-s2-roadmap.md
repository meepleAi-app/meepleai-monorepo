# Game Table S2-S5 Roadmap — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete Epic #148 Sprints 2-5 — wire scaffolded Game Table UI to real backend APIs, add SSE live session streaming, collapsible panels, mobile bottom sheets, and AI integration.

**Architecture:** All Game Night backend (CRUD, RSVP, status lifecycle) is production-ready. LiveSessions API client (24 endpoints) and Zod schemas exist. Frontend scaffolding (80+ session components, stores, pages) is in place. The work is **integration** — connecting existing UI stubs to real data — plus new features (collapsible panels, bottom sheets, SSE activity feed).

**Tech Stack:** Next.js 16 (App Router), React 19, Tailwind 4, Zustand + Immer, React Query, Vitest + Testing Library, Lucide React, shadcn/ui, SSE (EventSource)

**Branch:** `feature/issue-148-game-table-s2` (parent: `main-dev`)

---

## Existing Infrastructure (Do NOT Rebuild)

| Layer | What Exists | Location |
|-------|-------------|----------|
| **Backend: Game Nights** | Full CRUD + RSVP + publish/cancel (10 endpoints) | `Routing/GameNightEndpoints.cs` |
| **Backend: Live Sessions** | Full lifecycle + scoring + turns + tools (24 endpoints) | `Routing/LiveSessionEndpoints.cs` |
| **Frontend: API Clients** | `gameNightsClient.ts`, `liveSessionsClient.ts` | `lib/api/clients/` |
| **Frontend: React Query Hooks** | `useGameNights.ts` hooks (9 hooks), live session hooks | `hooks/queries/` |
| **Frontend: Zod Schemas** | `game-nights.schemas.ts`, `live-sessions.schemas.ts` | `lib/api/schemas/` |
| **Frontend: Stores** | `useGameNightStore`, `useSessionStore`, `useQuickViewStore` | `store/` |
| **Frontend: Components** | GameNightCard, List, Planning, Timeline, Picker, LiveSessionView, Scoreboard, ActivityFeed, 10+ tools | `components/` |
| **Frontend: Pages** | `/game-nights`, `/game-nights/[id]`, `/sessions/[id]/play` | `app/(authenticated)/` |

---

## Chunk 1: Game Night Pages — Wire to Backend (Sprint 2)

> **Focus:** Connect existing Game Night UI to real API. Zero new components — just wiring.

### Task 1: Game Night List Page — Connect to API

**Files:**
- Modify: `apps/web/src/app/(authenticated)/game-nights/_content.tsx`
- Test: `apps/web/src/app/(authenticated)/game-nights/__tests__/_content.test.tsx` (if exists, else create)

**Context:** The page already uses `RequireRole` + Suspense + `GameNightsContent`. The `_content.tsx` component likely fetches from the API. Verify it works end-to-end.

- [ ] **Step 1: Read `_content.tsx` and verify it uses `useMyGameNights()` or `useUpcomingGameNights()` hook**

Run: `cat apps/web/src/app/\(authenticated\)/game-nights/_content.tsx`

- [ ] **Step 2: If it uses mock data, replace with real hook**

Replace any mock/placeholder data with:
```tsx
import { useMyGameNights } from '@/hooks/queries/useGameNights';

export function GameNightsContent() {
  const { data: nights, isLoading, error } = useMyGameNights();
  // ... render GameNightList with real data
}
```

- [ ] **Step 3: Run the page locally and verify API call works**

Run: `cd apps/web && pnpm dev` → navigate to `/game-nights`
Expected: Either shows real data from API or empty state (no mock data)

- [ ] **Step 4: Commit (if changes were needed)**

```bash
git add apps/web/src/app/\(authenticated\)/game-nights/
git commit -m "feat(game-nights): wire list page to useMyGameNights API hook"
```

---

### Task 2: Game Night Detail Page — Verify RSVP Integration

**Files:**
- Read: `apps/web/src/app/(authenticated)/game-nights/[id]/page.tsx`

**Context:** The detail page already uses `useGameNight(id)`, `useGameNightRsvps(id)`, RSVP mutations. It was resolved from main-dev in the merge. Verify it compiles and renders.

- [ ] **Step 1: Verify page compiles with no TypeScript errors**

Run: `cd apps/web && pnpm typecheck`

- [ ] **Step 2: Verify page renders at `/game-nights/{id}`**

Run: `cd apps/web && pnpm dev` → navigate to `/game-nights/some-id`
Expected: Either shows game night data or "Serata non trovata" error (if ID doesn't exist)

- [ ] **Step 3: No commit needed unless fixes required**

---

### Task 3: Game Night Creation Flow — Verify Wizard Integration

**Files:**
- Read: `apps/web/src/app/(authenticated)/game-nights/new/` (check if page exists)
- Read: `apps/web/src/components/game-nights/GameNightWizard.tsx`

- [ ] **Step 1: Verify creation page exists and uses `useCreateGameNight()` hook**

Run: `ls apps/web/src/app/\(authenticated\)/game-nights/new/`

- [ ] **Step 2: If creation page doesn't exist, create minimal one**

```tsx
// apps/web/src/app/(authenticated)/game-nights/new/page.tsx
import { GameNightWizard } from '@/components/game-nights/GameNightWizard';

export default function NewGameNightPage() {
  return <GameNightWizard />;
}
```

- [ ] **Step 3: Commit (if changes were needed)**

```bash
git add apps/web/src/app/\(authenticated\)/game-nights/
git commit -m "feat(game-nights): add creation page with GameNightWizard"
```

---

### Task 4: Game Night Planning Layout — Connect Real Data

**Files:**
- Modify: `apps/web/src/components/game-nights/GameNightPlanningLayout.tsx`
- Modify: `apps/web/src/components/game-nights/InlineGamePicker.tsx`

**Context:** These components are UI-only scaffolds. They need to fetch real game night data and user's game collection.

- [ ] **Step 1: Read current GameNightPlanningLayout to understand props/data flow**

Run: `cat apps/web/src/components/game-nights/GameNightPlanningLayout.tsx`

- [ ] **Step 2: Wire GameNightPlanningLayout to use `useGameNight(id)` for real data**

The layout should receive a `gameNightId` prop and fetch data:
```tsx
import { useGameNight } from '@/hooks/queries/useGameNights';

interface Props {
  gameNightId: string;
}

export function GameNightPlanningLayout({ gameNightId }: Props) {
  const { data: gameNight, isLoading } = useGameNight(gameNightId);
  // ... render with real data
}
```

- [ ] **Step 3: Wire InlineGamePicker to filter by player count from real data**

The picker should use the `useGameNightStore` player count and the user's library to filter games.

- [ ] **Step 4: Write integration test**

```tsx
// apps/web/src/components/game-nights/__tests__/GameNightPlanningLayout.test.tsx
// Test that layout renders with mock API data
// Test that InlineGamePicker filters by player count
```

- [ ] **Step 5: Run tests**

Run: `cd apps/web && pnpm vitest run src/components/game-nights/__tests__/GameNightPlanningLayout.test.tsx`

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/game-nights/
git commit -m "feat(game-nights): wire planning layout and picker to real API data"
```

---

## Chunk 2: QuickView Context Modes (Sprint 4 — moved up, small scope)

> **Focus:** Add `mode` discriminator to QuickView store. Connect Rules/FAQ tabs to backend KB. Wire AI tab to agent chat.

### Task 5: Add `mode` to `useQuickViewStore`

**Files:**
- Modify: `apps/web/src/store/quick-view/store.ts`
- Modify: `apps/web/src/store/quick-view/__tests__/store.test.ts`

- [ ] **Step 1: Write failing tests for mode discriminator**

```ts
// Add to existing store.test.ts
it('defaults to idle mode', () => {
  expect(useQuickViewStore.getState().mode).toBe('idle');
});

it('sets game mode when opening for game', () => {
  useQuickViewStore.getState().openForGame('game-123');
  expect(useQuickViewStore.getState().mode).toBe('game');
});

it('sets session mode when opening for session', () => {
  useQuickViewStore.getState().openForSession('session-456', 'game-789');
  const state = useQuickViewStore.getState();
  expect(state.mode).toBe('session');
  expect(state.sessionId).toBe('session-456');
  expect(state.selectedGameId).toBe('game-789');
});

it('resets to idle on close', () => {
  useQuickViewStore.getState().openForGame('game-123');
  useQuickViewStore.getState().close();
  expect(useQuickViewStore.getState().mode).toBe('idle');
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/store/quick-view/__tests__/store.test.ts`
Expected: FAIL — `mode`, `openForSession`, `sessionId` not defined

- [ ] **Step 3: Implement mode discriminator**

```ts
// In store.ts, add to interface:
export type QuickViewMode = 'idle' | 'game' | 'session';

// Add to state:
mode: 'idle' as QuickViewMode,
sessionId: null as string | null,

// Add openForSession action:
openForSession: (sessionId: string, gameId: string) =>
  set((state) => {
    state.isOpen = true;
    state.mode = 'session';
    state.sessionId = sessionId;
    state.selectedGameId = gameId;
    state.isCollapsed = false;
  }),

// Update close to reset mode:
close: () =>
  set((state) => {
    state.isOpen = false;
    state.mode = 'idle';
    state.selectedGameId = null;
    state.sessionId = null;
  }),

// Update openForGame to set mode:
openForGame: (gameId: string) =>
  set((state) => {
    state.isOpen = true;
    state.mode = 'game';
    state.selectedGameId = gameId;
    state.isCollapsed = false;
  }),
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/store/quick-view/__tests__/store.test.ts`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/store/quick-view/
git commit -m "feat(store): add mode discriminator to useQuickViewStore (idle/game/session)"
```

---

### Task 6: QuickView — Wire Rules/FAQ Tabs to KB Data

**Files:**
- Modify: `apps/web/src/components/layout/QuickView/QuickView.tsx`
- Create: `apps/web/src/components/layout/QuickView/RulesContent.tsx`
- Create: `apps/web/src/components/layout/QuickView/FaqContent.tsx`
- Test: `apps/web/src/components/layout/QuickView/__tests__/RulesContent.test.tsx`

- [ ] **Step 1: Write test for RulesContent**

```tsx
// RulesContent.test.tsx
describe('RulesContent', () => {
  it('shows placeholder when no gameId', () => {
    render(<RulesContent gameId={null} />);
    expect(screen.getByText(/seleziona un gioco/i)).toBeInTheDocument();
  });

  it('shows loading state when fetching', () => {
    // Mock useQuery to return isLoading
    render(<RulesContent gameId="game-1" />);
    expect(screen.getByTestId('rules-skeleton')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement RulesContent and FaqContent**

```tsx
// RulesContent.tsx
'use client';

import { Skeleton } from '@/components/ui/feedback/skeleton';

interface RulesContentProps {
  gameId: string | null;
}

export function RulesContent({ gameId }: RulesContentProps) {
  if (!gameId) {
    return <p className="text-sm text-muted-foreground">Seleziona un gioco per vederne le regole</p>;
  }

  // TODO: Wire to GET /api/v1/games/{gameId}/rules once endpoint available
  // For now, show a placeholder that indicates data fetching will be added
  return (
    <div data-testid="rules-content">
      <p className="text-sm text-muted-foreground">
        Regole per il gioco selezionato saranno caricate dalla Knowledge Base.
      </p>
    </div>
  );
}
```

- [ ] **Step 4: Update QuickView to use RulesContent and FaqContent with store mode**

```tsx
// In QuickView.tsx tab content section:
const { mode, selectedGameId } = useQuickViewStore();

{activeTab === 'rules' && <RulesContent gameId={selectedGameId} />}
{activeTab === 'faq' && <FaqContent gameId={selectedGameId} />}
{activeTab === 'ai' && <AIQuickViewContent gameId={selectedGameId ?? ''} gameName="Gioco" />}
```

- [ ] **Step 5: Run tests**

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/layout/QuickView/
git commit -m "feat(quick-view): add RulesContent/FaqContent with mode-aware rendering"
```

---

## Chunk 3: Live Session Desktop Layout (Sprint 3)

> **Focus:** Transform LiveSessionView into a responsive 3-column desktop layout with collapsible panels.

### Task 7: Live Session 3-Column Desktop Layout

**Files:**
- Modify: `apps/web/src/components/game-night/LiveSessionView.tsx`
- Create: `apps/web/src/components/session/DesktopSessionLayout.tsx`
- Test: `apps/web/src/components/session/__tests__/DesktopSessionLayout.test.tsx`

- [ ] **Step 1: Read current LiveSessionView**

Run: `cat apps/web/src/components/game-night/LiveSessionView.tsx`

- [ ] **Step 2: Write test for DesktopSessionLayout**

```tsx
describe('DesktopSessionLayout', () => {
  it('renders 3 columns on desktop', () => {
    render(
      <DesktopSessionLayout
        leftPanel={<div data-testid="left">Left</div>}
        centerContent={<div data-testid="center">Center</div>}
        rightPanel={<div data-testid="right">Right</div>}
      />
    );
    expect(screen.getByTestId('left')).toBeInTheDocument();
    expect(screen.getByTestId('center')).toBeInTheDocument();
    expect(screen.getByTestId('right')).toBeInTheDocument();
  });

  it('hides side panels on mobile', () => {
    // Test responsive classes are applied
    const { container } = render(
      <DesktopSessionLayout
        leftPanel={<div>Left</div>}
        centerContent={<div>Center</div>}
        rightPanel={<div>Right</div>}
      />
    );
    const wrapper = container.querySelector('[data-testid="session-layout"]');
    expect(wrapper?.className).toContain('lg:grid-cols-[280px_1fr_280px]');
  });
});
```

- [ ] **Step 3: Run tests to verify they fail**

- [ ] **Step 4: Implement DesktopSessionLayout**

```tsx
// DesktopSessionLayout.tsx
'use client';

import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

interface DesktopSessionLayoutProps {
  leftPanel: ReactNode;
  centerContent: ReactNode;
  rightPanel: ReactNode;
  isLeftCollapsed?: boolean;
  isRightCollapsed?: boolean;
}

export function DesktopSessionLayout({
  leftPanel,
  centerContent,
  rightPanel,
  isLeftCollapsed = false,
  isRightCollapsed = false,
}: DesktopSessionLayoutProps) {
  return (
    <div
      data-testid="session-layout"
      className={cn(
        'grid h-full',
        'grid-cols-1',
        !isLeftCollapsed && !isRightCollapsed && 'lg:grid-cols-[280px_1fr_280px]',
        isLeftCollapsed && !isRightCollapsed && 'lg:grid-cols-[44px_1fr_280px]',
        !isLeftCollapsed && isRightCollapsed && 'lg:grid-cols-[280px_1fr_44px]',
        isLeftCollapsed && isRightCollapsed && 'lg:grid-cols-[44px_1fr_44px]'
      )}
    >
      <aside className="hidden lg:flex flex-col border-r border-border bg-card overflow-y-auto">
        {leftPanel}
      </aside>
      <main className="flex flex-col flex-1 overflow-y-auto">
        {centerContent}
      </main>
      <aside className="hidden lg:flex flex-col border-l border-border bg-card overflow-y-auto">
        {rightPanel}
      </aside>
    </div>
  );
}
```

- [ ] **Step 5: Run tests**

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/session/DesktopSessionLayout.tsx apps/web/src/components/session/__tests__/
git commit -m "feat(session): add DesktopSessionLayout with responsive 3-column grid"
```

---

### Task 8: Collapsible Panel Component

**Files:**
- Create: `apps/web/src/components/session/CollapsibleSessionPanel.tsx`
- Create: `apps/web/src/hooks/useCollapsiblePanel.ts`
- Test: `apps/web/src/components/session/__tests__/CollapsibleSessionPanel.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
describe('CollapsibleSessionPanel', () => {
  it('renders full content when expanded', () => {
    render(
      <CollapsibleSessionPanel
        side="left"
        isCollapsed={false}
        onToggle={() => {}}
        expandedContent={<div>Full</div>}
        collapsedContent={<div>Mini</div>}
      />
    );
    expect(screen.getByText('Full')).toBeInTheDocument();
  });

  it('renders collapsed content when collapsed', () => {
    render(
      <CollapsibleSessionPanel
        side="left"
        isCollapsed={true}
        onToggle={() => {}}
        expandedContent={<div>Full</div>}
        collapsedContent={<div>Mini</div>}
      />
    );
    expect(screen.getByText('Mini')).toBeInTheDocument();
  });

  it('calls onToggle when collapse button clicked', async () => {
    const onToggle = vi.fn();
    const user = userEvent.setup();
    render(
      <CollapsibleSessionPanel
        side="left"
        isCollapsed={false}
        onToggle={onToggle}
        expandedContent={<div>Full</div>}
        collapsedContent={<div>Mini</div>}
      />
    );
    await user.click(screen.getByRole('button', { name: /comprimi/i }));
    expect(onToggle).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement**

```tsx
// CollapsibleSessionPanel.tsx
'use client';

import { PanelLeftClose, PanelRightClose, PanelLeftOpen, PanelRightOpen } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { ReactNode } from 'react';

interface CollapsibleSessionPanelProps {
  side: 'left' | 'right';
  isCollapsed: boolean;
  onToggle: () => void;
  expandedContent: ReactNode;
  collapsedContent: ReactNode;
}

export function CollapsibleSessionPanel({
  side,
  isCollapsed,
  onToggle,
  expandedContent,
  collapsedContent,
}: CollapsibleSessionPanelProps) {
  const CollapseIcon = side === 'left' ? PanelLeftClose : PanelRightClose;
  const ExpandIcon = side === 'left' ? PanelLeftOpen : PanelRightOpen;

  return (
    <div className={cn('flex flex-col h-full', isCollapsed ? 'w-[44px]' : 'w-full')}>
      <div className={cn('flex items-center p-1', side === 'left' ? 'justify-end' : 'justify-start')}>
        <button
          onClick={onToggle}
          aria-label={isCollapsed ? 'Espandi pannello' : 'Comprimi pannello'}
          className="p-1.5 rounded-md text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
        >
          {isCollapsed ? <ExpandIcon className="h-4 w-4" /> : <CollapseIcon className="h-4 w-4" />}
        </button>
      </div>
      <div className="flex-1 overflow-y-auto">
        {isCollapsed ? collapsedContent : expandedContent}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Create `useCollapsiblePanel` hook with localStorage persistence**

```ts
// useCollapsiblePanel.ts
import { useState, useCallback, useEffect } from 'react';

export function useCollapsiblePanel(key: string, defaultCollapsed = false) {
  const [isCollapsed, setIsCollapsed] = useState(defaultCollapsed);

  useEffect(() => {
    if (typeof localStorage === 'undefined') return;
    const stored = localStorage.getItem(`panel-${key}`);
    if (stored !== null) setIsCollapsed(stored === 'true');
  }, [key]);

  const toggle = useCallback(() => {
    setIsCollapsed(prev => {
      const next = !prev;
      if (typeof localStorage !== 'undefined') {
        localStorage.setItem(`panel-${key}`, String(next));
      }
      return next;
    });
  }, [key]);

  return { isCollapsed, toggle };
}
```

- [ ] **Step 5: Run tests**

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/session/CollapsibleSessionPanel.tsx apps/web/src/hooks/useCollapsiblePanel.ts apps/web/src/components/session/__tests__/
git commit -m "feat(session): add CollapsibleSessionPanel with localStorage persistence"
```

---

### Task 9: Wire LiveSessionView to Desktop Layout

**Files:**
- Modify: `apps/web/src/components/game-night/LiveSessionView.tsx`
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/play/page.tsx`

- [ ] **Step 1: Read current LiveSessionView and play page**

- [ ] **Step 2: Refactor LiveSessionView to use DesktopSessionLayout on lg+ screens**

The component should:
- On mobile: Keep current stacked layout (Header → Scoreboard → Actions → Chat)
- On desktop (lg+): Use DesktopSessionLayout with:
  - Left: Scoreboard + Game Toolkit
  - Center: Activity Feed + Input Bar
  - Right: AI Chat / QuickView

- [ ] **Step 3: Add responsive breakpoint detection**

```tsx
import { useMediaQuery } from '@/hooks/useResponsive';

const isDesktop = useMediaQuery('(min-width: 1024px)');

if (isDesktop) {
  return <DesktopSessionLayout ... />;
} else {
  return <MobileSessionLayout ... />;
}
```

- [ ] **Step 4: Wire QuickView to auto-open in session mode when live session starts**

```tsx
// In LiveSessionView or parent page:
useEffect(() => {
  if (sessionData?.gameId) {
    useQuickViewStore.getState().openForSession(sessionId, sessionData.gameId);
  }
  return () => useQuickViewStore.getState().close();
}, [sessionId, sessionData?.gameId]);
```

- [ ] **Step 5: Run typecheck and verify**

Run: `cd apps/web && pnpm typecheck`

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/game-night/ apps/web/src/components/session/ apps/web/src/app/\(authenticated\)/sessions/
git commit -m "feat(session): wire LiveSessionView to responsive desktop 3-column layout"
```

---

## Chunk 4: Activity Feed SSE Integration (Sprint 3)

> **Focus:** Connect ActivityFeed to real-time SSE stream. Most complex chunk — involves EventSource, reconnection, dedup.

### Task 10: `useSessionSSE` Hook

**Files:**
- Create: `apps/web/src/hooks/useSessionSSE.ts`
- Test: `apps/web/src/hooks/__tests__/useSessionSSE.test.ts`

- [ ] **Step 1: Write failing test**

```ts
describe('useSessionSSE', () => {
  it('creates EventSource with correct URL', () => {
    // Mock EventSource
    const mockEventSource = vi.fn();
    vi.stubGlobal('EventSource', mockEventSource);

    renderHook(() => useSessionSSE('session-123'));
    expect(mockEventSource).toHaveBeenCalledWith(
      expect.stringContaining('/sessions/session-123/events/stream')
    );
  });

  it('adds events to store on message', () => {
    // Verify that incoming SSE messages are dispatched to useSessionStore
  });

  it('cleans up EventSource on unmount', () => {
    const closeFn = vi.fn();
    vi.stubGlobal('EventSource', vi.fn(() => ({ close: closeFn, addEventListener: vi.fn() })));

    const { unmount } = renderHook(() => useSessionSSE('session-123'));
    unmount();
    expect(closeFn).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement useSessionSSE**

```ts
// useSessionSSE.ts
'use client';

import { useEffect, useRef, useCallback } from 'react';
import { useSessionStore } from '@/store/session';

const MAX_RETRY_DELAY = 16000;
const MAX_OFFLINE_DURATION = 5 * 60 * 1000; // 5 minutes

export function useSessionSSE(sessionId: string | null) {
  const esRef = useRef<EventSource | null>(null);
  const retryDelayRef = useRef(1000);
  const disconnectedAtRef = useRef<number | null>(null);
  const seenIdsRef = useRef(new Set<string>());
  const addEvent = useSessionStore(s => s.addEvent);

  const connect = useCallback(() => {
    if (!sessionId) return;

    const url = `/api/v1/sessions/${sessionId}/events/stream`;
    const es = new EventSource(url);
    esRef.current = es;

    es.onopen = () => {
      retryDelayRef.current = 1000;
      disconnectedAtRef.current = null;
    };

    es.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data);
        if (data.id && seenIdsRef.current.has(data.id)) return; // dedup
        if (data.id) seenIdsRef.current.add(data.id);
        addEvent(data);
      } catch {
        console.warn('[SSE] Malformed event:', event.data);
      }
    };

    es.onerror = () => {
      es.close();
      esRef.current = null;

      if (!disconnectedAtRef.current) {
        disconnectedAtRef.current = Date.now();
      }

      const offlineDuration = Date.now() - disconnectedAtRef.current;
      if (offlineDuration > MAX_OFFLINE_DURATION) {
        // Signal session expired — handled by UI
        return;
      }

      // Exponential backoff
      const delay = retryDelayRef.current;
      retryDelayRef.current = Math.min(delay * 2, MAX_RETRY_DELAY);
      setTimeout(connect, delay);
    };
  }, [sessionId, addEvent]);

  useEffect(() => {
    connect();
    return () => {
      esRef.current?.close();
      esRef.current = null;
    };
  }, [connect]);
}
```

- [ ] **Step 4: Run tests**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/useSessionSSE.ts apps/web/src/hooks/__tests__/
git commit -m "feat(session): add useSessionSSE hook with reconnection and dedup"
```

---

### Task 11: Wire ActivityFeed to SSE Events

**Files:**
- Modify: `apps/web/src/components/session/ActivityFeed.tsx`
- Modify: `apps/web/src/components/session/ActivityFeedInputBar.tsx`

- [ ] **Step 1: Read current ActivityFeed implementation**

Run: `cat apps/web/src/components/session/ActivityFeed.tsx`

- [ ] **Step 2: Wire ActivityFeed to read from `useSessionStore.events`**

```tsx
import { useSessionStore } from '@/store/session';
import { selectEvents } from '@/store/session/selectors';

export function ActivityFeed() {
  const events = useSessionStore(selectEvents);
  // Render events as timeline items
}
```

- [ ] **Step 3: Wire ActivityFeedInputBar to POST events via API**

```tsx
import { liveSessionsClient } from '@/lib/api/clients/liveSessionsClient';

async function handleSubmitNote(text: string) {
  await liveSessionsClient.createEvent(sessionId, {
    type: 'note',
    data: { text },
  });
}
```

- [ ] **Step 4: Wire dice roller to auto-log to feed**

When SimpleDiceRoller produces a result, POST it as a `dice_roll` event.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/session/
git commit -m "feat(session): wire ActivityFeed to SSE events and input bar to API"
```

---

## Chunk 5: Mobile Live Session (Sprint 5)

> **Focus:** Bottom sheets, safe area, MobileScorebar integration.

### Task 12: Bottom Sheet Component

**Files:**
- Create: `apps/web/src/components/session/BottomSheet.tsx`
- Test: `apps/web/src/components/session/__tests__/BottomSheet.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
describe('BottomSheet', () => {
  it('renders content when open', () => {
    render(<BottomSheet isOpen onClose={() => {}}><p>Content</p></BottomSheet>);
    expect(screen.getByText('Content')).toBeInTheDocument();
  });

  it('does not render when closed', () => {
    render(<BottomSheet isOpen={false} onClose={() => {}}><p>Content</p></BottomSheet>);
    expect(screen.queryByText('Content')).not.toBeInTheDocument();
  });

  it('closes on Escape key', async () => {
    const onClose = vi.fn();
    render(<BottomSheet isOpen onClose={onClose}><p>Content</p></BottomSheet>);
    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Implement using shadcn Sheet (already available)**

```tsx
// BottomSheet.tsx — wraps shadcn Sheet with mobile-optimized defaults
'use client';

import type { ReactNode } from 'react';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/navigation/sheet';

interface BottomSheetProps {
  isOpen: boolean;
  onClose: () => void;
  title?: string;
  height?: string; // e.g. '420px', '60vh'
  children: ReactNode;
}

export function BottomSheet({ isOpen, onClose, title, height = '60vh', children }: BottomSheetProps) {
  return (
    <Sheet open={isOpen} onOpenChange={(open) => { if (!open) onClose(); }}>
      <SheetContent
        side="bottom"
        className="rounded-t-[20px] pb-[env(safe-area-inset-bottom,20px)]"
        style={{ maxHeight: height }}
      >
        {title && (
          <SheetHeader>
            <SheetTitle>{title}</SheetTitle>
          </SheetHeader>
        )}
        <div className="flex-1 overflow-y-auto">{children}</div>
      </SheetContent>
    </Sheet>
  );
}
```

- [ ] **Step 3: Run tests**

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/session/BottomSheet.tsx apps/web/src/components/session/__tests__/
git commit -m "feat(session): add BottomSheet wrapper with safe area and rounded corners"
```

---

### Task 13: Mobile Live Session Layout

**Files:**
- Create: `apps/web/src/components/session/MobileSessionLayout.tsx`
- Modify: `apps/web/src/components/session/MobileScorebar.tsx` (verify integration)
- Modify: `apps/web/src/components/session/MobileStatusBar.tsx` (verify integration)

- [ ] **Step 1: Write test for MobileSessionLayout**

```tsx
describe('MobileSessionLayout', () => {
  it('renders status bar, scorebar, feed, and action bar', () => {
    render(<MobileSessionLayout sessionId="s1" />);
    expect(screen.getByTestId('mobile-status-bar')).toBeInTheDocument();
    expect(screen.getByTestId('mobile-scorebar')).toBeInTheDocument();
    expect(screen.getByTestId('activity-feed')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Implement MobileSessionLayout**

Stacks: StatusBar (36px) → Scorebar (52px) → Feed (flex) → ActionBar (bottom)

- [ ] **Step 3: Wire bottom sheet triggers for dice, AI, camera**

Quick action buttons in the action bar open BottomSheet with the relevant tool.

- [ ] **Step 4: Run tests**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/session/
git commit -m "feat(session): add MobileSessionLayout with bottom sheets for tools"
```

---

## Chunk 6: Spec Improvements + PR (Cleanup)

> **Focus:** Apply spec-panel findings, update spec, create PR.

### Task 14: Update Spec with Panel Findings

**Files:**
- Modify: `docs/superpowers/specs/2026-03-11-game-table-layout-design.md`

- [ ] **Step 1: Add NFRs section (performance budgets, memory ceiling)**
- [ ] **Step 2: Add Gherkin scenarios for Live Session (score, dice, SSE reconnect)**
- [ ] **Step 3: Add SSE event envelope contract for Sprint 3**
- [ ] **Step 4: Add Sprint-to-Item mapping table**
- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/specs/
git commit -m "docs(spec): add NFRs, live session Gherkin, SSE contracts per spec-panel review"
```

---

### Task 15: Create PR to main-dev

- [ ] **Step 1: Run full test suite**

```bash
cd apps/web && pnpm test && pnpm typecheck && pnpm lint
```

- [ ] **Step 2: Push branch and create PR**

```bash
git push -u origin feature/issue-148-game-table-s2
gh pr create --base main-dev --title "feat: Game Table S2-S5 — API integration, SSE, collapsible panels, mobile" --body "..."
```

- [ ] **Step 3: Update Epic #148 issue with sprint progress**

---

## Dependency Graph

```
Chunk 1 (Game Night wiring) ──→ independent, start first
Chunk 2 (QuickView modes)  ──→ independent, can parallel with Chunk 1
Chunk 3 (Desktop layout)   ──→ depends on Chunk 2 (QuickView session mode)
Chunk 4 (SSE + feed)       ──→ depends on Chunk 3 (layout to render feed in)
Chunk 5 (Mobile)           ──→ depends on Chunk 3 + 4
Chunk 6 (Spec + PR)        ──→ after all chunks
```

**Parallelizable:** Chunks 1 + 2 can run simultaneously.
**Sequential:** 3 → 4 → 5 → 6.
