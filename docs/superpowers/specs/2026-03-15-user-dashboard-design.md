# User Dashboard Design Spec

**Date**: 2026-03-15
**Status**: Draft
**Epic**: Dashboard Utente — Context-Aware Gaming Hub

## Overview

The MeepleAI user dashboard is a context-aware hub that automatically switches between two modes:
- **Exploration mode** (default): library overview, recent activity, stats, agents
- **Game mode**: live session control center with real-time scoreboard, quick navigation to rules/FAQ/agents

The system is built on a **Dashboard Engine** (XState state machine) that orchestrates zone rendering and animated transitions, with a **cascade navigation** pattern (Mana Pip → DeckStack → Drawer) for fast access during game sessions.

## Design Decisions

| # | Decision | Choice | Rationale |
|---|----------|--------|-----------|
| 1 | Context model | Auto-switch (exploration vs game mode) | Dashboard adapts to active session; user can force switch |
| 2 | Navbar behavior | Fixed zone + dynamic contextual slot | Stability in navigation, flexibility in content |
| 3 | In-session navigation | Mana Pip → DeckStack → Drawer cascade | 3-step progressive disclosure using existing card system |
| 4 | Contextual slot layout | Expandable panel below CardStack | Non-intrusive, collapsible, rich content when expanded |
| 5 | Exploration layout | Hybrid hub: hero + card sections + stat widgets | Balanced information density, familiar pattern |
| 6 | Context transition | Animated morph (hero → session bar) | Cinematic UX, clear spatial continuity |
| 7 | Architecture | Full rewrite with Dashboard Engine | XState + View Transitions API (Framer Motion fallback) |
| 8 | Browser compat | Progressive enhancement | View Transitions where supported, Framer Motion elsewhere |

## Architecture

### Dashboard Engine (XState v5)

State machine that manages dashboard UI lifecycle, zone visibility, and transition orchestration. The engine owns **only UI transition state** — session data comes from the existing `sessionStore` (Zustand) via reactive subscription.

**Boundary with existing stores:**
- `sessionStore.ts` (Zustand): **source of truth** for active session data (players, scores, status)
- `useCardHand` (Zustand): **source of truth** for shell navigation context (`'user' | 'admin'`)
- `DashboardEngine` (XState): **owns** UI mode (`exploration | gameMode | transitioning`), transition type, animation state. **Reads** session data reactively from `sessionStore`, never duplicates it.

**States:**

```
idle
├── exploration (default — no active session)
│   └── Events: SESSION_DETECTED → transitioning
├── transitioning (animation in progress — buffers events)
│   ├── Events: TRANSITION_COMPLETE → gameMode or exploration (based on target)
│   └── Events: SESSION_COMPLETED, SESSION_DISMISSED → buffered, processed after TRANSITION_COMPLETE
└── gameMode (active session detected)
    ├── Events: SESSION_COMPLETED, SESSION_DISMISSED → transitioning
    └── gameModeExpanded
        └── Events: COLLAPSE → gameMode
```

The `transitioning` state buffers `SESSION_COMPLETED` / `SESSION_DISMISSED` events that arrive during an animation. When `TRANSITION_COMPLETE` fires, the engine processes the buffer: if a termination event was buffered, it immediately begins the reverse transition instead of settling into the target state.

**Context (machine data):**

```typescript
interface DashboardEngineContext {
  /** Read reactively from sessionStore — NOT duplicated */
  activeSessionId: string | null;
  /** UI-only state */
  transitionType: 'morph' | 'fade' | 'slide' | 'none';
  transitionTarget: 'exploration' | 'gameMode' | null;
  previousState: 'exploration' | 'gameMode' | null;
  /** Events buffered during transitioning state */
  bufferedEvents: DashboardEvent[];
}
```

**Session data access:** Components that need session data (SessionBar, SessionPanel, ScoreboardZone) read directly from `sessionStore` via `useSessionStore()`, NOT from the engine context. The engine only signals *which zones to render*.

**Files:**
- `DashboardEngine.ts` — XState v5 machine definition
- `DashboardEngineProvider.tsx` — React context provider wrapping the machine
- `useDashboardMode()` — Hook to read current UI state and send events

**Dependencies:** `xstate@5`, `@xstate/react`

**Dependency justification:** XState is chosen over a Zustand-based approach because the dashboard lifecycle has non-trivial state transitions (exploration ↔ transitioning ↔ gameMode, event buffering during transitions, guard conditions) that are error-prone to implement as imperative Zustand actions. XState's declarative state chart makes transitions explicit and testable. Bundle impact: ~15KB gzipped. If the team decides against adding XState, a Zustand-based alternative with a `useReducer`-style transition function is viable but requires manual event buffering logic.

### Session Detection

The DashboardEngine detects active sessions through three mechanisms:

1. **On mount (initial load):** `DashboardEngineProvider` calls `sessionStore.getState().activeSession` on mount. If non-null, sends `SESSION_DETECTED` immediately.

2. **Reactive subscription (same tab):** `DashboardEngineProvider` subscribes to `sessionStore` via `useStore(sessionStore, s => s.activeSession)`. When `activeSession` transitions from `null → session`, sends `SESSION_DETECTED`. When `session → null`, sends `SESSION_COMPLETED`.

3. **Cross-tab (multi-tab scenario):** The existing `sessionStore` does NOT sync across tabs. For v1, cross-tab detection is **out of scope**. If the user starts a session in Tab A, Tab B will not switch to game mode until the next `GET /api/v1/dashboard` refetch (5min staleTime). Future enhancement: add `BroadcastChannel` to `sessionStore` for instant cross-tab sync.

### Component Tree

```
app/(authenticated)/layout.tsx
└── UnifiedShellClient.tsx
    ├── DashboardEngineProvider          ← NEW: wraps entire shell
    │   ├── UnifiedTopNav.tsx
    │   ├── CardStack.tsx                ← MODIFIED: + DynamicSlot
    │   │   └── SessionPanel.tsx         ← NEW: rendered inside slot
    │   ├── main content area
    │   │   └── app/(authenticated)/dashboard/page.tsx
    │   │       └── DashboardRenderer.tsx  ← NEW: replaces gaming-hub-client.tsx
    │   │           └── zones/*
    │   └── ContextualBottomNav.tsx
    └── HandDrawer.tsx                   ← MODIFIED: + SessionPanel as first item
```

The `DashboardEngineProvider` wraps the entire `UnifiedShellClient` so that both the main content area (where `DashboardRenderer` renders zones) and the CardStack sidebar (where `SessionPanel` renders) can access the engine state.

### Zone System

The dashboard is composed of independent zones, each a self-contained React component with its own data fetching and Suspense boundary.

#### Exploration Mode Zones

| Zone | Component | Content | Data Source |
|------|-----------|---------|-------------|
| HeroZone | `zones/HeroZone.tsx` | Contextual banner: next game night, game suggestion, welcome onboarding | `GET /api/v1/dashboard` |
| StatsZone | `zones/StatsZone.tsx` | Compact KPI bar: 4 stats (library count, games last 30d, active chats, streak) | `GET /api/v1/dashboard` (shared) |
| CardsZone | `zones/CardsZone.tsx` | 3 horizontal scroll sections: Recent Games (entity=game), Recent Sessions (entity=session), Active Chats (entity=chatSession). Each card is a `MeepleCard variant="grid"` | Dedicated endpoints per section |
| AgentsSidebar | `zones/AgentsSidebar.tsx` | Agent cards in right sidebar (desktop) or section below (mobile) | `GET /api/v1/agents` |

#### Game Mode Zones

| Zone | Component | Content | Data Source |
|------|-----------|---------|-------------|
| SessionBar | `zones/SessionBar.tsx` | Floating glassmorphism bar: game icon + name + live dot + leader score + mana pip row. Morphs from HeroZone. | `useSessionStore()` + SSE via `useSessionSync()` |
| SessionPanel | `SessionPanel.tsx` | In CardStack dynamic slot: mini-scoreboard, current turn, mana pip row, quick actions (Pause, Chat Agent) | `useSessionStore()` + SSE via `useSessionSync()` |
| ScoreboardZone | `zones/ScoreboardZone.tsx` | Main content: full scoreboard with rounds, scores, player avatars | `useSessionStore()` + SSE via `useSessionSync()` |

**SSE endpoint:** The existing `GET /api/v1/game-sessions/{sessionId}/stream` (defined in `SessionTrackingEndpoints.cs:438`) provides score and turn update events. The `useSessionSync()` hook (already exists) connects to this endpoint. SessionPanel and SessionBar read the same hook instance — no new SSE endpoint is needed.

**Additional data needed by SessionPanel/Bar but not in SSE stream:**
- `gameName`, `gameImageUrl`: fetched once via `GET /api/v1/games/{gameId}` (React Query, 5min cache) when session becomes active
- `players` list: available from `useSessionStore().players` (populated on session join)

#### Dashboard Renderer

`DashboardRenderer.tsx` — Layout grid that positions zones based on DashboardEngine state. Replaces `gaming-hub-client.tsx` as the default export of `app/(authenticated)/dashboard/page.tsx`.

```tsx
// Simplified rendering logic
function DashboardRenderer() {
  const { state } = useDashboardMode();

  return (
    <AnimatePresence mode="wait">
      {state === 'exploration' && (
        <>
          <motion.div layoutId="hero"><HeroZone /></motion.div>
          <StatsZone />
          <CardsZone />
          <AgentsSidebar />
        </>
      )}
      {state === 'gameMode' && (
        <>
          <motion.div layoutId="hero"><SessionBar /></motion.div>
          <ScoreboardZone />
        </>
      )}
    </AnimatePresence>
  );
}
```

### Transitions

#### View Transitions API (Progressive Enhancement)

When `document.startViewTransition` is available (Chrome/Edge):

```css
/* Hero → SessionBar morph */
::view-transition-old(hero-to-session) {
  animation: morph-out 300ms ease-out;
}
::view-transition-new(hero-to-session) {
  animation: morph-in 300ms ease-out;
}
```

#### Framer Motion Fallback

When View Transitions are not available:

- `layoutId="hero"` on HeroZone and SessionBar for automatic morph
- `AnimatePresence` with `mode="wait"` for zone enter/exit
- `motion.div` with `layout` prop on CardStack items for auto-reflow

#### Transition Sequence (Exploration → Game Mode)

Total duration: ~750ms. The `transitioning` state spans the entire sequence.

1. HeroZone morphs → SessionBar (`viewTransitionName` or `layoutId`) — 300ms
2. StatsZone + CardsZone fade-out simultaneously (Framer `AnimatePresence`) — 200ms
3. SessionPanel slides into CardStack slot (`transform: translateY`) — 300ms ease-out
4. ScoreboardZone fades in as main content — 200ms delay + 250ms duration
5. Engine receives `TRANSITION_COMPLETE`, exits `transitioning` state
6. If buffered events exist (e.g., session ended during animation), process immediately

#### Reverse Transition (Game Mode → Exploration)

Same sequence reversed. SessionBar morphs → HeroZone, ScoreboardZone fades out, CardsZone fades in. Engine enters `transitioning` state for the duration.

## Cascade Navigation (Mana Pip → DeckStack → Drawer)

### Level 1 — Mana Pip (Entry Point)

Colored circular pips appear in 3 locations:
- **SessionPanel** (CardStack) — horizontal row below mini-scoreboard
- **SessionBar** (floating) — right side
- **MeepleCard footer** — existing `ManaLinkFooter.tsx`

Tap on pip → opens DeckStack filtered by entity type for the current game.

### Level 2 — DeckStack (Selection)

Existing component (`DeckStack.tsx`) with modifications:

- New prop: `presentation: 'popover' | 'bottomSheet'` — replaces the proposed `triggerSource`. Positioning for `popover` mode uses the existing `anchorRect` prop. `bottomSheet` mode renders a fixed bottom sheet (mobile).
- The caller decides the presentation mode based on viewport: `useResponsive()` hook returns `isMobile`, caller passes `presentation={isMobile ? 'bottomSheet' : 'popover'}`.
- Shows linked cards filtered by entity type: all KB docs for the game, all players in session, etc.
- Each card in deck is `MeepleCard variant="compact"`

### Level 3 — Drawer (Detail)

Tap on DeckStack card → opens `ExtraMeepleCardDrawer` with full content.
- Desktop: DeckStack stays visible behind drawer
- Mobile: DeckStack closes, drawer opens full-screen

### Data Flow

```
SessionPanel.manaPip[KB]
  → onClick → DeckStack({ entityType: 'kb', sourceEntityId: session.gameId })
    → fetch GET /api/v1/knowledge-base/{gameId}/documents  ← NEW ENDPOINT (see Backend Work)
    → render DeckStackCard[] (compact MeepleCards)
      → onCardClick → ExtraMeepleCardDrawer({ entityType: 'kb', entityId: docId })
        → fetch GET /api/v1/knowledge-base/documents/{docId}
        → render full document content
```

Note: Each entity type uses its bounded context endpoint:
- KB documents: `GET /api/v1/knowledge-base/{gameId}/documents` — **NEW** (see Backend Work)
- Players: `GET /api/v1/game-sessions/{sessionId}/players`
- Agents: `GET /api/v1/agents?gameId={gameId}`

### Shortcuts

- **Single item skip**: If DeckStack has only 1 item, skip directly to Drawer
- **Long-press**: Long-press on mana pip → open Drawer for first item directly

### Hook

`useCascadeNavigation.ts` — Orchestrates the 3-level flow. Implemented as a **Zustand store** (not a local hook) so that cascade state is shared between SessionPanel and SessionBar pip rows.

```typescript
interface CascadeNavigationState {
  state: 'closed' | 'deckStack' | 'drawer';
  activeEntityType: MeepleEntityType | null;
  activeEntityId: string | null;
  deckStackSkipped: boolean;
  openDeckStack: (entityType: MeepleEntityType, sourceEntityId: string, anchor?: DOMRect) => void;
  openDrawer: (entityType: MeepleEntityType, entityId: string) => void;
  closeCascade: () => void;
  closeDrawer: () => void; // Returns to deckStack if not skipped, or closes entirely
}
```

**Back-navigation behavior:**
- Drawer close when `deckStackSkipped === false` → return to `deckStack` state (DeckStack is still visible)
- Drawer close when `deckStackSkipped === true` → return to `closed` state (DeckStack was never shown)
- DeckStack close → return to `closed` state

## CardStack Slot + SessionPanel

### CardStack Architecture Clarification

The existing `CardStack.tsx` (`UnifiedShell/CardStack.tsx`) renders `HandCard` entries in two sections: dynamic cards (grow upward) and pinned cards (bottom). The "nav items" shown in the layout diagram (Dashboard, Library, etc.) are `HandCard` entries configured in `useCardHand`, NOT literal nav links.

The **dynamic slot** is inserted between the dynamic cards section and the pinned cards section of the existing `CardStack`. It renders `SessionPanel` when the DashboardEngine is in `gameMode`, and renders nothing in `exploration`.

```
┌─────────────────────┐
│  HandCard entries    │  ← Dynamic section (useCardHand)
│  (Dashboard, Library,│
│   Discover, etc.)    │
├─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤
│  ┌─────────────────┐│  ← DYNAMIC SLOT (DashboardEngine controlled)
│  │ 🟢 Catan - R5   ││     Only visible in gameMode
│  │ Marco 8  Sara 7 ││
│  │ Luca 6          ││
│  │ ○📜 ○⚡ ○♟      ││  ← Mana pip row
│  │ [⏸ Pause][💬 AI]││  ← Quick actions
│  └─────────────────┘│
├─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤
│  Pinned HandCards    │  ← Pinned section (Agents, Badges)
└─────────────────────┘
```

### SessionPanel States

**Collapsed** (CardStack closed, 56px width): Session icon with pulsing live dot. Tap → expand CardStack + show panel.

**Expanded** (CardStack open, 180px width):
- Header: game name + live dot + duration
- Mini-scoreboard: top 3 players with scores (ranked)
- Mana pip row: KB, Agent, Players (filtered for current game)
- Quick actions: 2 buttons (Pause/Resume + Chat Agent)
- Footer: "Go to scoreboard →" link

### Animation

- Slot appears: `height: 0 → auto` + `opacity: 0 → 1` (300ms ease-out)
- Items below slot reflow via Framer Motion `layout` prop
- Slot disappears: reverse animation

### Files

- `SessionPanel.tsx` — Expanded panel content
- `SessionPanelCollapsed.tsx` — 56px icon version
- `useSessionSlot.ts` — Connects DashboardEngine UI state + sessionStore data → SessionPanel props
- Modified: `CardStack.tsx` (+ `<DynamicSlot>` between dynamic and pinned sections)
- Modified: `HandDrawer.tsx` (+ SessionPanel as first item on mobile)

## Responsive Design

Breakpoints aligned with Carte in Mano navigation spec:

### Desktop (>=1280px)

- CardStack sidebar (180px expanded) with SessionPanel in slot
- SessionBar floating above content (game mode)
- DeckStack as popover anchored to mana pip (`presentation="popover"`)
- Drawer as right side sheet
- 2-column layout: main content + AgentsSidebar

### Tablet (768px-1279px)

- CardStack collapsed (56px) with SessionPanelCollapsed
- Tap → expand CardStack + full SessionPanel
- SessionBar floating (slightly more compact)
- DeckStack as popover (`presentation="popover"`)
- Drawer as bottom half-screen sheet
- 1-column layout, AgentsSidebar becomes section below

### Mobile (<768px)

- No CardStack — replaced by HandDrawer + ContextualBottomNav
- SessionPanel as first item in HandDrawer
- SessionBar compact: game icon + name + live dot + 3 mana pips. Tap → expand to full info.
- DeckStack as bottom sheet (`presentation="bottomSheet"`)
- Drawer as full-screen sheet
- 1-column stacked layout
- SmartFAB in game mode: icon changes to ⏸ (pause) as primary action

### Cross-Breakpoint Behavior

- Window resize during game mode: zones re-adapt without losing session state
- DashboardEngine does not reset on breakpoint change — only rendering changes
- Safe areas: SessionBar respects `env(safe-area-inset-top)`, BottomNav respects `env(safe-area-inset-bottom)`

## Data Flow & Performance

### Fetching Strategy

| Zone | Method | Cache (React Query) | Refresh |
|------|--------|---------------------|---------|
| HeroZone | `GET /api/v1/dashboard` | 5min staleTime | On window focus |
| StatsZone | Same dashboard endpoint | Shared query key | On window focus |
| CardsZone (games) | `GET /api/v1/library?pageSize=8&sortBy=addedAt&sortDescending=true` | 2min staleTime | On window focus |
| CardsZone (sessions) | `GET /api/v1/game-sessions/history` (recent completed) + `GET /api/v1/live-sessions/active` (in-progress) | 1min staleTime | On window focus |
| CardsZone (chats) | `GET /api/v1/knowledge-base/my-chats?skip=0&take=6` (lightweight dashboard variant) | 2min staleTime | On window focus |
| AgentsSidebar | `GET /api/v1/agents` | 5min staleTime | On window focus |
| SessionPanel/Bar | `useSessionStore()` + SSE via existing `useSessionSync()` on `GET /api/v1/game-sessions/{sessionId}/stream` | No cache, real-time | Continuous |
| SessionPanel/Bar (game info) | `GET /api/v1/games/{gameId}` | 5min staleTime | Once on session detect |
| DeckStack items (KB) | `GET /api/v1/knowledge-base/{gameId}/documents` — **NEW ENDPOINT** (see Backend Work) | 1min staleTime | On pip click |
| DeckStack items (players) | `GET /api/v1/game-sessions/{sessionId}/players` | 1min staleTime | On pip click |
| DeckStack items (agents) | `GET /api/v1/agents?gameId={gameId}` | 1min staleTime | On pip click |
| Drawer content | Entity-specific endpoint (per bounded context) | 5min staleTime | On open |

### Performance Targets

| Metric | Target | Constraint |
|--------|--------|------------|
| Dashboard initial load (LCP) | <1.5s | Cold cache, 4G throttled. Reducible to <800ms with SSR HeroZone + prefetch on layout mount. |
| Context switch animation | <300ms @ 60fps | Framer Motion path; View Transitions path may be faster |
| Mana pip → DeckStack (perceived) | <200ms | Assumes React Query cache hit; cold fetch adds network latency |
| DeckStack → Drawer (perceived) | <150ms | Drawer component already loaded if DeckStack was shown |
| SSE reconnect after disconnect | <2s | Using existing `useSessionSync()` reconnect logic |

### Optimizations

- **Zone lazy loading**: `React.lazy` per zone, individual Suspense boundaries with skeletons
- **Prefetch on session detect**: When DashboardEngine detects active session, prefetch game mode zone data (`queryClient.prefetchQuery`) before transition starts
- **SSE multiplexing**: Single SSE stream per session via existing `useSessionSync()`. SessionPanel and SessionBar both read from `useSessionStore()` which is updated by the SSE hook.
- **DeckStack prefetch**: On hover/focus of mana pip, prefetch DeckStack data (desktop). On mobile, fetch on tap.
- **Image optimization**: MeepleCard covers with `next/image` + blur placeholder (already in use)

### Error Handling

- **SSE disconnected**: SessionBar shows "Reconnecting..." spinner, `useSessionSync()` fallback to polling `GET /api/v1/game-sessions/{sessionId}` every 5s
- **Zone fetch failure**: Shows skeleton with retry button, does not block other zones
- **DashboardEngine invalid state**: Reset to `exploration` with error toast
- **Session terminated mid-transition**: `transitioning` state buffers the `SESSION_COMPLETED` event. On `TRANSITION_COMPLETE`, engine processes the buffer and immediately starts the reverse transition back to exploration.

## Testing Strategy

| Level | Scope | Tool |
|-------|-------|------|
| Unit | XState machine transitions, event buffering, guards (pure logic) | Vitest |
| Unit | Zone components with mock data | Vitest + React Testing Library |
| Unit | `useCascadeNavigation` state transitions | Vitest |
| Integration | Zone rendering + data fetching with MSW | Vitest + MSW |
| Integration | SessionPanel + SessionBar shared state via sessionStore | Vitest + RTL |
| E2E | Full flow: exploration → game mode → cascade → drawer | Playwright |
| Visual | Transition screenshot comparison | Playwright + Chromatic |

## File Inventory

### New Files

| File | Location | Purpose |
|------|----------|---------|
| `DashboardEngine.ts` | `components/dashboard/` | XState v5 state machine definition |
| `DashboardEngineProvider.tsx` | `components/dashboard/` | React context provider for engine |
| `useDashboardMode.ts` | `components/dashboard/` | Hook: read UI state, send events |
| `DashboardRenderer.tsx` | `components/dashboard/` | Layout grid, zone positioning |
| `zones/HeroZone.tsx` | `components/dashboard/zones/` | Contextual hero banner |
| `zones/StatsZone.tsx` | `components/dashboard/zones/` | Compact KPI bar |
| `zones/CardsZone.tsx` | `components/dashboard/zones/` | Horizontal card scroll sections |
| `zones/AgentsSidebar.tsx` | `components/dashboard/zones/` | Agent cards sidebar |
| `zones/SessionBar.tsx` | `components/dashboard/zones/` | Floating glassmorphism session bar |
| `zones/ScoreboardZone.tsx` | `components/dashboard/zones/` | Full scoreboard (game mode) |
| `SessionPanel.tsx` | `components/dashboard/` | CardStack slot: mini-scoreboard + pips + actions |
| `SessionPanelCollapsed.tsx` | `components/dashboard/` | 56px icon version for collapsed CardStack |
| `useSessionSlot.ts` | `components/dashboard/` | Bridges engine UI state + sessionStore → panel props |
| `useCascadeNavigation.ts` | `lib/stores/` | Zustand store: pip → stack → drawer flow |
| `dashboard-transitions.css` | `components/dashboard/` | View Transition rules + fallback keyframes |

### Modified Files

| File | Changes |
|------|---------|
| `CardStack.tsx` | + `<DynamicSlot>` between dynamic HandCard section and pinned section |
| `HandDrawer.tsx` | + SessionPanel as first item (mobile) |
| `DeckStack.tsx` | + `presentation: 'popover' \| 'bottomSheet'` prop replacing portal-only rendering |
| `ManaLinkFooter.tsx` | + cascade onClick handler via `useCascadeNavigation` |
| `ExtraMeepleCardDrawer.tsx` | + session context awareness (current game highlight) |
| `UnifiedShellClient.tsx` | + `DashboardEngineProvider` wrapper around shell content |
| `dashboard/page.tsx` | Replace `gaming-hub-client.tsx` import with `DashboardRenderer` |

### Dependencies

| Package | Version | Purpose | Bundle impact |
|---------|---------|---------|---------------|
| `xstate` | `^5.x` | State machine engine | ~15KB gzipped |
| `@xstate/react` | `^4.x` | React bindings for XState | ~3KB gzipped |

## Backend Work Required

### New Endpoint: KB Documents List for Game

The cascade navigation needs a user-facing endpoint to list KB documents linked to a game. Currently only admin endpoints expose document lists.

**Endpoint:** `GET /api/v1/knowledge-base/{gameId}/documents`
**Bounded Context:** KnowledgeBase
**Auth:** Requires authenticated user
**Response:** List of documents with `id`, `title`, `status` (indexed/processing/failed), `pageCount`, `createdAt`
**Routing file:** `KnowledgeBaseEndpoints.cs`
**Handler:** New query `GetGameDocumentsQuery` → returns `VectorDocument` entries filtered by `gameId` where user has access (owns the game or game is in shared catalog)

This is a lightweight read-only endpoint. No new domain logic required — it's a filtered projection of existing `VectorDocument` entities.

## Open Questions / Future Work

- **Cross-tab session sync**: Currently out of scope. Future: add `BroadcastChannel` to `sessionStore` for instant cross-tab game mode activation.
- **View Transitions API browser support**: As of March 2026, Chrome/Edge have full support, Firefox partial, Safari experimental. The Framer Motion fallback ensures no degraded experience.
- **Multiple concurrent sessions**: The current design assumes at most one active session. If multi-session support is added, the SessionPanel would need a session selector.
