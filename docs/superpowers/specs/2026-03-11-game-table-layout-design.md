# Game Table Layout — Design Specification

**Issue**: #148 — Game Table S1
**Branch**: `feature/issue-148-game-table-s1` (parent: `frontend-dev`)
**Date**: 2026-03-11
**Status**: Approved (brainstorm session completed)

## Overview

"The Game Table" is a layout redesign that reimagines MeepleAI's UX as a board game table. The metaphor: a slim sidebar is your card rack, the main content area is the table where you play, and a contextual side panel is the flipped card showing details.

This extends the previous layout system (Epic #5033) by replacing TopNavbar and Sidebar with **CardRack + TopBar**, while keeping MiniNav, FloatingActionBar, SmartFAB, MobileTabBar, and MobileBreadcrumb as coexisting components that will be gradually migrated or removed.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Layout concept | C: "The Game Table" | Best balance: slim sidebar (64px) maximizes content, board game metaphor fits domain |
| Game selection | C: Inline picker (no persistent hand) | Zero overhead, contextual smart filters, clean layout. Like Notion inline DB |
| Collapsible panels | Independent left/right collapse | Desktop flexibility: full (both open) → focus (both collapsed to 44px strips) |
| Mobile pattern | Touch-first with bottom sheets | One-hand operation at the game table, native-feeling interactions |

## Responsive Breakpoints

Uses Tailwind breakpoints from `design-tokens.css`:

| Tier | Breakpoint | Layout |
|------|-----------|--------|
| Mobile | `<768px` (below `md`) | No CardRack, MobileTabBar + MobileNavDrawer, bottom sheets |
| Tablet | `768px–1023px` (`md`–`lg`) | CardRack visible (64px), no Quick View panel, single-column content |
| Desktop | `1024px–1279px` (`lg`–`xl`) | Full layout: CardRack + content + optional Quick View |
| Wide | `≥1280px` (`xl`+) | Full layout with both panels open by default |

## Architecture

### Layout Structure (Desktop ≥1024px)

```
┌──────────────────────────────────────────────────────────┐
│ CardRack │ TopBar (h-12/48px): breadcrumb + ⌘K + avatar  │
│  (64px)  ├────────────────────────────────────────────────┤
│  slim    │ MiniNav (context-aware, auto-hides)            │
│  sidebar ├──────────────────────┬─────────────────────────┤
│  hover→  │ Content Area         │ Quick View (300px)      │
│  240px   │ (flex, adaptive)     │ Regole/FAQ/AI tabs      │
│          │                      │ toggle open/close       │
│          │ [FloatingActionBar]  │                         │
└──────────┴──────────────────────┴─────────────────────────┘
```

### Components

#### 1. CardRack (Left Sidebar)
- **Collapsed**: `var(--card-rack-width, 64px)` with icon-only Lucide icons
- **Expanded**: `var(--card-rack-hover-width, 240px)` on hover with labels
- **Nav items** (top): Dashboard, Libreria, Scopri, Chat AI, Sessioni, Serate
- **Bottom items**: Agenti, Badge
- **Visibility**: `hidden md:flex` — hidden below 768px
- **Position**: Fixed left, below TopBar (`top: var(--top-bar-height, 48px)`)
- **Active state**: Orange highlight on current route, `startsWith()` matching

#### 2. TopBar (Top Navigation)
- **Height**: `h-12` (48px via `var(--top-bar-height)`)
- **Position**: Sticky top, z-40, blur backdrop
- **Left (mobile)**: Hamburger (MobileNavDrawer) + Logo
- **Left (desktop)**: DesktopBreadcrumb
- **Center (desktop)**: SearchTrigger — `min-w-[200px] max-w-[280px]`, triggers CommandPalette
- **Right**: NotificationBell + UserMenu (avatar, profile, settings, theme toggle, logout)
- **Scroll shadow**: `shadow-sm` appears after 4px scroll
- **Accessibility**: Skip-to-content link (WCAG 2.4.1)
- **Live mode variant** (future): Green tint, LIVE pulse, timer, turn, Pause/Stop buttons

#### 3. Quick View (Right Side Panel) — PARTIALLY IMPLEMENTED (PR #200)
- **Width**: 300px (desktop), 280px (compact)
- **Tabs**: Regole / FAQ / AI
- **Context modes** (store needs `mode` discriminator):
  - **Game context** (`mode: 'game'`): Rules/FAQ for `selectedGameId`, AI scoped to game KB
  - **Session context** (`mode: 'session'`): Live session AI, session-specific quick prompts
- **Collapsible**: Toggle to 44px strip with icon buttons
- **Opens on**: Click on a game card (game mode) or auto-open during live sessions (session mode)
- **Status**: Shell + store + tabs implemented. Content stubs for rules/FAQ. AIQuickViewContent wired but not connected to backend.

#### 4. Inline Picker (Game Selection) — NOT YET IMPLEMENTED
- **Trigger**: Click "+" on drop zone in Game Night planning
- **Content**: Horizontal carousel of collection games, filterable
- **Smart filter**: Auto-applies "adatti a N giocatori" based on attendees
- **No persistent hand**: Appears contextually, disappears after selection

### Legacy Components (Coexisting)

These components from Epic #5033 remain in LayoutShell and will be migrated or removed incrementally:

| Component | Status | Plan |
|-----------|--------|------|
| `MiniNav` | Active | Keep — provides context-aware tabs per route |
| `FloatingActionBar` | Active | Keep — provides floating primary actions |
| `SmartFAB` | Active | Keep — mobile context-aware FAB |
| `MobileTabBar` | Active | Keep — mobile bottom navigation |
| `MobileBreadcrumb` | Active | Keep — mobile breadcrumb below MiniNav |
| `CardStackPanel` | Active | Keep — "Carte in Mano" panel |
| `ImpersonationBanner` | Active | Keep — admin impersonation UI |
| `Sidebar` | Removed | Replaced by CardRack in commit `9e948f72e` |
| `TopNavbar` | Removed | Replaced by TopBar in commit `9e948f72e` |

### Page-Specific Layouts

#### Game Night List
- Grid of event cards (3 columns on desktop)
- States: PROSSIMA (green), BOZZA (neutral), COMPLETATA (dimmed)
- Each card shows: date, title, location, player avatars, game thumbnails, winner badge

#### Game Night Planning
- **Two-column grid**: Left (320px info + players + AI suggestion) | Right (table + picker + timeline)
- **Table area**: "Dealt cards" with slight rotation (-2°/+1°), drop zone for adding games
- **Inline picker**: Below table, horizontal scroll, filtered by player count
- **Timeline**: Horizontal bar with time slots (game → break → game → free slot)
- **AI Suggestion**: Gradient card with recommendations based on player levels

#### Live Session (3-column)
- **Left (280px)**: Current game card, scoreboard (ranked, with PV breakdown), game toolkit
- **Center (flex)**: Activity feed timeline (vertical), tabs (Cronologia/Media/Note/Statistiche), input bar
- **Right (280px)**: AI chat panel, quick prompts, tab switch (AI/Regole/FAQ/Stats)
- **Collapsible panels**: Left collapses to 44px (mini avatars + scores + dice button), Right to 44px (icons)

#### Live Session — Pause State
- Full overlay: pause icon, game state snapshot (all player scores), resume/photo/note actions

### Mobile Layout (<768px)

#### Navigation
- No CardRack — `MobileNavDrawer` via hamburger in TopBar, `MobileTabBar` at bottom
- Status bar (36px) replaces TopBar in live sessions: LIVE indicator, game name, turn, timer, pause

#### Live Session Mobile
- **Status bar**: 36px — Live pulse, game, turn, timer, pause
- **Scorebar**: 52px — Horizontal scroll of player mini-cards, tap to expand
- **Feed**: Full-width vertical timeline
- **Action bar** (bottom): Text input + Send + quick actions (🎲📸🎥🎙️🤖)
- **Safe area**: iOS bottom padding (20px)

#### Bottom Sheets
- **Dice roller**: Large dice, type selector (2d6/1d6/1d20/card), roll history
- **AI chat**: Tall sheet (420px) with messages, quick prompts, tab switch
- **Camera/recorder**: Native device APIs
- **Player detail**: Score breakdown on tap
- **Pause**: Full-screen overlay

### Tablet Layout (768px–1023px)

- CardRack visible at 64px (no auto-expand, click to expand)
- No Quick View panel — content is single-column full width
- MiniNav and FloatingActionBar visible as on desktop
- Bottom sheets for toolkit and AI (same as mobile pattern)

## Data Flow & State Management

### State Architecture (Zustand)

| Store | Purpose | Key State |
|-------|---------|-----------|
| `useCardRackState` | Sidebar hover expand/collapse | `isExpanded`, `onMouseEnter`, `onMouseLeave` |
| `useNavigation` (context) | Per-route nav config | `tabs`, `actions` via `useSetNavConfig()` |
| `useCommandPalette` | Search palette open/close | `isOpen`, `toggle()` |
| `useGameNightStore` (new) | Game Night CRUD + live state | `gameNight`, `players`, `selectedGames`, `timeline` |
| `useSessionStore` (new) | Live session state | `scores`, `currentTurn`, `events`, `isPaused`, `timer` |
| `useQuickViewStore` (new) | Right panel state | `isOpen`, `selectedGameId`, `activeTab` |

### Real-Time Data (Live Sessions)

- **Transport**: SSE (Server-Sent Events) — same pattern as existing chat (`useAgentChatStream`)
- **Events**: Score updates, dice rolls, turn changes, AI tips → activity feed
- **Pause/Resume**: REST endpoint, client-side timer management
- **Reconnection**: Auto-reconnect on SSE drop, replay missed events from server

### API Endpoints (Required)

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/v1/game-nights` | GET/POST | List/create game nights |
| `/api/v1/game-nights/{id}` | GET/PATCH/DELETE | CRUD single game night |
| `/api/v1/game-nights/{id}/players` | GET/POST/DELETE | Manage players |
| `/api/v1/game-nights/{id}/games` | GET/POST/DELETE | Manage selected games |
| `/api/v1/game-nights/{id}/sessions` | POST | Start live session |
| `/api/v1/game-nights/{id}/sessions/{sid}/events` | GET/POST | Activity feed CRUD |
| `/api/v1/game-nights/{id}/sessions/{sid}/events/stream` | GET (SSE) | Live event stream |
| `/api/v1/game-nights/{id}/sessions/{sid}/scores` | GET/PATCH | Scoreboard |
| `/api/v1/game-nights/{id}/sessions/{sid}/pause` | POST | Pause/resume |
| `/api/v1/game-nights/{id}/ai/suggest` | POST | AI game suggestions |

### API Contracts (Sprint 2 — Game Night CRUD)

#### `GET /api/v1/game-nights`
```json
// Response 200
{
  "items": [{
    "id": "uuid",
    "title": "string",
    "date": "2026-03-15T20:00:00Z",
    "location": "string | null",
    "status": "draft | upcoming | completed",
    "playerCount": 4,
    "gameCount": 2,
    "playerAvatars": ["url1", "url2"],
    "gameThumbnails": ["url1"],
    "hostId": "uuid"
  }],
  "totalCount": 12,
  "page": 1,
  "pageSize": 20
}
```

#### `POST /api/v1/game-nights`
```json
// Request
{ "title": "string (required, 3-100 chars)", "date": "ISO 8601", "location": "string | null" }
// Response 201
{ "id": "uuid", "title": "...", "date": "...", "status": "draft", ... }
// Error 422: Validation errors
```

#### `GET /api/v1/game-nights/{id}`
```json
// Response 200 — Full game night with players and games
{
  "id": "uuid", "title": "string", "date": "ISO 8601",
  "location": "string | null", "status": "draft | upcoming | completed",
  "players": [{ "id": "uuid", "name": "string", "avatarUrl": "string | null" }],
  "games": [{ "id": "uuid", "title": "string", "thumbnailUrl": "string | null", "minPlayers": "int | null", "maxPlayers": "int | null" }],
  "timeline": [{ "slotIndex": 0, "gameId": "uuid | null", "type": "game | break | free", "durationMinutes": 60 }]
}
// Error 404: GameNightNotFoundException
```

### SSE Operational Requirements

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| Retry backoff | 1s → 2s → 4s → 8s → 16s (max) | Exponential with cap to avoid thundering herd |
| Max offline duration | 5 minutes | Beyond this, prompt full reload instead of replay |
| Event replay on reconnect | Server sends missed events since `Last-Event-ID` | SSE spec built-in; requires server-side event log |
| Max events/second (client) | 20 | Throttle renders via `requestAnimationFrame` batch |
| Feed memory limit | Last 500 events in client state | Older events paginated from server on scroll-up |
| Deduplication | By `event.id` (server-generated UUID) | Client maintains `Set<string>` of seen IDs |

**Client reconnection behavior:**
1. SSE connection drops → show "Riconnessione..." banner (yellow)
2. Retry with exponential backoff: 1s, 2s, 4s, 8s, 16s
3. On reconnect: send `Last-Event-ID` header → server replays missed events
4. If offline > 5 minutes: show "Sessione scaduta — ricarica" with reload button
5. During offline: queue local events (dice, notes) in `pendingEvents[]`, flush on reconnect

**Conflict resolution for offline events:**
- Server is authoritative for scores and turn state
- Client-queued events get server timestamps on flush (not client timestamps)
- Duplicate detection by idempotency key (`clientEventId` field)
- Score conflicts: server rejects stale updates, client re-fetches current scores

### Activity Feed Event Types

| Type | Icon | Color | Content |
|------|------|-------|---------|
| Dice roll | 🎲 | Orange | Player, dice values, total, special rules triggered |
| AI tip | 🤖 | Purple | Contextual advice, rule reminders for beginners |
| Score update | 🏆 | Green | Player, action, new score |
| Photo | 📸 | Blue | Player, photo preview |
| Note | 📝 | Yellow | Player, text content |
| Audio note | 🎙️ | Pink | Player, waveform player |
| Turn change | 🔄 | Gray | Turn N → Turn N+1, player name |
| Pause/Resume | ⏸/▶ | Yellow/Green | Duration, state saved timestamp |
| Session start | ▶ | Green | Game, player count, timestamp |

### Game Toolkit

- **Dice roller**: Configurable (2d6, 1d20, custom), animated result, automatic feed entry
- **Card draw**: Deck types configurable per game (from KB)
- **Random generators**: Timer, random player, random color
- All results logged to activity feed automatically

## Design Tokens

### Colors (from `design-tokens.css`)

| Token | CSS Variable | Value |
|-------|-------------|-------|
| Brand primary | `--color-meeple-orange` | `25 85% 45%` (~#D2691E) |
| Entity game | `--color-entity-game` | `25 95% 45%` (used for game cards) |
| Brand purple | `--color-meeple-purple` | `262 83% 62%` (#8b5cf6) |
| Player colors | Tailwind utilities | purple-500, blue-500, yellow-500, pink-500 |
| Status green | Tailwind `green-500` | #22c55e (confirmed/live) |
| Status yellow | Tailwind `yellow-500` | #eab308 (pending/pause) |
| Status red | Tailwind `red-500` | #ef4444 (stop/remove) |

**Dark theme surfaces** (brainstorm prototypes used raw values — future implementation should define these as CSS custom properties):
- Background: `#0d1117` → `#161b22` gradient
- Surface: `rgba(255,255,255,0.03)` cards
- Panel bg: `rgba(0,0,0,0.15)`
- Borders: `rgba(255,255,255,0.06)` subtle, `rgba(255,255,255,0.08)` normal

### Dimensions

| Token | CSS Variable | Value |
|-------|-------------|-------|
| CardRack collapsed | `--card-rack-width` | 64px |
| CardRack expanded | `--card-rack-hover-width` | 240px |
| TopBar height | `--top-bar-height` | 48px (h-12) |
| Quick View width | (new token needed) | 300px / 280px compact |
| Collapsed panel | (new token needed) | 44px |
| Mobile status bar | — | 36px |
| Mobile scorebar | — | 52px |
| Bottom sheet radius | — | 20px top corners |
| Card border-radius | — | 14px (large), 10px (medium), 8px (small) |

### Typography (from `design-tokens.css`)

| Role | Font | Weight | CSS Variable |
|------|------|--------|-------------|
| Headings | Quicksand | 700-800 | `--font-heading: var(--font-quicksand)` |
| Body/Nav | Nunito | 400-600 | `--font-body: var(--font-nunito)` |
| Labels | Nunito | 600-700 | 10-11px, uppercase, letter-spacing 0.5px |
| Scores | System (tabular-nums) | 800 | `font-variant-numeric: tabular-nums` |

## Error & Loading States

| Component | Loading | Error | Empty |
|-----------|---------|-------|-------|
| Game Night list | Skeleton grid (3 cards) | Error card with retry | "Nessuna serata pianificata" + CTA |
| Planning: Players | Skeleton list | Inline error toast | "Invita giocatori" prompt |
| Planning: Table | Skeleton cards | Error toast | Empty drop zone with prompt |
| Live: Scoreboard | Pulsing placeholders | "Impossibile caricare" + retry | N/A (always has players) |
| Live: Feed | Skeleton events | "Connessione persa" banner + auto-retry | "Sessione appena iniziata" |
| Live: AI panel | Typing indicator | "AI non disponibile" fallback to rules tab | Quick prompt suggestions |
| Quick View | Skeleton content | "Regole non disponibili" | "Seleziona un gioco" |
| Offline (live) | — | Persistent banner "Offline — riconnessione..." | Queue events locally, sync on reconnect |

## Implementation Status

### Completed (S1 — PR #200)

| Component | Status | Notes |
|-----------|--------|-------|
| `breadcrumb-utils.ts` | Done | `lib/breadcrumb-utils.ts`, tests |
| `CardRack` + `useCardRackState` | Done | Sidebar 64→240px hover, tests |
| `TopBar` | Done | Breadcrumb, SearchTrigger, UserMenu, skip-link, tests |
| `LayoutShell` wiring | Done | Replaced Sidebar/TopNavbar with CardRack/TopBar |
| `useQuickViewStore` | Done | Store + tests |
| `QuickView` shell | Done | 300px panel, 3 tabs, collapse, integrated in LayoutShell |
| `useGameNightStore` | Done | Store + selectors + tests |
| `useSessionStore` | Done | Store + selectors + tests |
| Design tokens | Done | Card Rack + panel dimension CSS variables |

### Scaffolded (S1 — PR #200, UI only, no backend integration)

| Component | Status | Missing |
|-----------|--------|---------|
| `GameNightCard` | UI only | Backend: `GET /game-nights` |
| `GameNightPlanningLayout` | UI only | Backend: `GET /game-nights/{id}`, real data fetching |
| `InlineGamePicker` | UI only | Real collection data, backend integration |
| `DealtGameCard` | UI only | Drag-and-drop, real game data |
| `GameNightTimeline` | UI only | Timeline CRUD, backend sync |
| `LiveSessionLayout` | UI only | SSE, real session data |
| `ActivityFeed` + `ActivityFeedInputBar` | UI only | SSE stream, event creation API |
| `MobileScorebar` + `MobileStatusBar` | UI only | Real session data |
| `SimpleDiceRoller` | UI only | Auto-log to activity feed |
| `AIQuickViewContent` | UI only | Backend AI endpoint, gameId scoping |

### Remaining (S2+)

| # | Item | Dependencies | Acceptance Criteria |
|---|------|-------------|-------------------|
| 1 | **Quick View panel** | None (standalone component) | 300px right panel, 3 tabs (Regole/FAQ/AI), collapse to 44px, `useQuickViewStore` |
| 2 | **Inline game picker** | Game Night planning page | Horizontal carousel from user collection, auto-filter by player count, click → add to table |
| 3 | **Game Night list page** | Backend: `GET /game-nights` | Grid layout, 3 status states, player avatars, game thumbnails |
| 4 | **Game Night planning page** | #2, #3, Quick View (#1) | Two-column grid, dealt cards with rotation, timeline, AI suggestion card |
| 5 | **Live session layout** | #1, #8 (activity feed), #9 (toolkit) | 3-column layout, collapsible panels, live status bar |
| 6 | **Collapsible panels** | #1, #5 | Independent L/R collapse to 44px strips, persist preference |
| 7 | **Mobile responsive** | #5 | Status bar, scorebar, bottom sheets (dice, AI), safe area padding |
| 8 | **Activity feed** | Backend: SSE endpoint | 9 event types, vertical timeline, media attachments, input bar |
| 9 | **Game toolkit** | #8 (auto-logs to feed) | Dice roller (2d6/1d20/custom), card draw, random generators |
| 10 | **AI integration** | Backend: AI suggest endpoint, existing agent chat | Quick View AI tab, contextual suggestions, quick prompts |

**Dependency graph**: 3 → 4 → 5 → 7 (main chain); 1 → 4,5,6; 8 → 5,9; 9 → 5

## Behavioral Examples (Specification by Example)

### CardRack Active State

```gherkin
Scenario: CardRack highlights active nav item based on route
  Given the user is on "/library/my-games"
  Then the "Libreria" item has an orange active indicator
  And all other items have default styling

Scenario: CardRack matches nested routes
  Given the user is on "/game-nights/abc-123/session"
  Then the "Serate" item has an orange active indicator

Scenario: CardRack hover expand timing
  Given the CardRack is in collapsed state (64px)
  When the user hovers over the CardRack
  Then after 200ms the CardRack expands to 240px with labels
  When the user moves the cursor away
  Then after 300ms the CardRack collapses to 64px
  When the user moves cursor away and back within 300ms
  Then the collapse is cancelled and the CardRack stays expanded
```

### Inline Picker Filtering

```gherkin
Scenario: Picker filters games by player count
  Given 3 players in the planning session
  And the collection contains:
    | Title      | MinPlayers | MaxPlayers |
    | Catan      | 3          | 4          |
    | Chess      | 2          | 2          |
    | Party Game | 4          | 10         |
    | Uno        | 2          | 10         |
  When the InlineGamePicker opens
  Then visible games are: "Catan", "Uno"
  And hidden games are: "Chess" (max < 3), "Party Game" (min > 3)

Scenario: Picker shows all games when no players added
  Given 0 players in the planning session
  When the InlineGamePicker opens
  Then all games are visible (no player count filter applied)
  And a hint reads "Aggiungi giocatori per filtrare"

Scenario: Games with null player counts always visible
  Given 3 players in the planning session
  And game "Custom Game" has minPlayers=null, maxPlayers=null
  When the InlineGamePicker opens
  Then "Custom Game" is visible (null bypasses filter)
```

### Collapsible Panels

```gherkin
Scenario: Independent panel collapse on desktop
  Given the viewport is >=1280px (wide)
  And both left and right panels are open
  When the user collapses the left panel
  Then the left panel shrinks to 44px strip with mini-icons
  And the content area expands to fill the freed space
  And the right panel remains at 300px unchanged

Scenario: Panel state on breakpoint change
  Given the viewport is >=1280px with both panels open
  When the viewport resizes to 768px (tablet)
  Then the right panel is hidden entirely (not rendered)
  And the left panel collapses to 64px (CardRack tablet mode)
  When the viewport returns to >=1280px
  Then panels restore their previous open/collapsed state
```

### QuickView Context Modes

```gherkin
Scenario: QuickView opens in game-context mode
  Given no live session is active
  When the user clicks a game card in the planning table
  Then QuickView opens with selectedGameId set
  And the "Regole" tab shows rules for that game
  And the "AI" tab scopes queries to that game's knowledge base

Scenario: QuickView in session-context mode
  Given a live session is active for game "Catan"
  When QuickView opens
  Then selectedGameId is set to the current session's game
  And the "AI" tab provides session-aware suggestions
  And quick prompts include session-specific options ("Suggerisci strategia per il turno")
```

## Testing Strategy

### Unit Tests
- Each component isolated (CardRack, TopBar, QuickView, ActivityFeed, etc.)
- Store logic tested independently with `getInitialState()` reset

### Integration Tests
- LayoutShell renders correct layout based on route and breakpoint
- QuickView integration with game/session context

### Responsive Test Matrix

| Component | Mobile (<768) | Tablet (768-1023) | Desktop (1024-1279) | Wide (>=1280) |
|-----------|--------------|-------------------|--------------------|--------------|
| CardRack | Hidden | Visible 64px, click-expand | Visible 64px, hover-expand | Same as desktop |
| QuickView | Hidden (bottom sheet) | Hidden | Optional (toggle) | Open by default |
| MiniNav | Visible | Visible | Visible | Visible |
| MobileTabBar | Visible | Hidden | Hidden | Hidden |
| FloatingActionBar | Visible | Visible | Visible | Visible |
| Live panels | Bottom sheets | Single column | 3-col, collapsible | 3-col, both open |

### Accessibility Scenarios (WCAG 2.1 AA)
- **2.1.1 Keyboard**: Tab through all CardRack items, Enter/Space to activate
- **2.4.1 Skip link**: Skip-to-content bypasses CardRack and TopBar
- **4.1.3 Status messages**: Activity feed updates announced via `aria-live="polite"`
- **Focus management**: Focus moves to QuickView content when opened, returns to trigger on close
- **Bottom sheets**: Focus trapped inside when open, Escape to dismiss

### E2E Scenarios (Playwright)
1. **Game Night CRUD**: Create → appears in list → edit → delete
2. **Planning flow**: Add players → pick games (filter verifies) → arrange timeline
3. **Session start**: Start from planning → live layout renders → feed shows "Session start"
4. **Session interaction**: Roll dice → score update → note → pause → resume
5. **Session end**: End session → summary view → status becomes "completed"

### Real-Time Testing
- SSE reconnection: Simulate network drop → verify banner → verify replay on reconnect
- Event ordering: Send 10 events rapidly → verify rendered in server timestamp order
- Malformed event: Send invalid JSON → verify client doesn't crash, logs warning
- Timer accuracy: Pause → wait 5s → resume → verify elapsed time ± 100ms

## Non-Functional Requirements (NFRs)

### Performance Budgets

| Metric | Target | Measurement |
|--------|--------|-------------|
| Layout LCP (Largest Contentful Paint) | < 1.5s | Lighthouse on 4G throttle |
| CardRack hover→expand animation | < 200ms | CSS transition timing |
| QuickView tab switch | < 100ms | React profiler |
| Activity feed render (100 events) | < 16ms | React profiler, 60fps |
| SSE reconnect (first attempt) | < 2s | Network timing |
| BottomSheet open animation | < 300ms | CSS transition timing |

### Memory Ceiling

| Component | Max Memory | Condition |
|-----------|-----------|-----------|
| ActivityFeed events store | 500 events | Oldest pruned on overflow |
| SSE dedup Set | 1000 IDs | Cleared on session change |
| Score history | 200 entries per player | Per session lifetime |
| Image thumbnails (lazy) | 50 loaded at once | Intersection Observer |

### Bundle Size

| Chunk | Max Size (gzipped) | Includes |
|-------|-------------------|----------|
| Layout shell | 25KB | CardRack, TopBar, MiniNav |
| Session view | 40KB | LiveSessionView, ActivityFeed, Scoreboard |
| QuickView | 15KB | Rules, FAQ, AI tabs |
| Mobile session | 20KB | MobileSessionLayout, BottomSheet, MobileScorebar |

## Live Session Gherkin Scenarios

### Score Recording

```gherkin
Scenario: Player records a score during live session
  Given a live session is active with players "Alice" and "Bob"
  When Alice opens the score input sheet
  And selects player "Bob"
  And enters score value "15" for round 2
  And taps "Conferma"
  Then the score is saved via API
  And the scoreboard updates Bob's total
  And an activity feed event "score_update" appears
  And SSE broadcasts the score to all connected clients

Scenario: Score input validates before submission
  Given the score input sheet is open
  When the user submits without selecting a player
  Then an inline error "Seleziona un giocatore" appears
  And the form is not submitted
```

### Dice Rolling

```gherkin
Scenario: Player rolls dice during session
  Given a live session is active
  When the user taps the dice button in ActivityFeedInputBar
  Then a BottomSheet opens with the dice roller
  When the user rolls 2d6
  Then the result appears in the activity feed as "dice_roll" event
  And the event shows individual values and total
```

### SSE Reconnection

```gherkin
Scenario: SSE reconnects after network interruption
  Given a live session is connected via SSE
  When the network connection drops
  Then the connection indicator shows "Disconnesso" (red)
  And useSessionSSE begins exponential backoff (1s, 2s, 4s, 8s, 16s)
  When the network returns within 5 minutes
  Then the EventSource reconnects
  And the connection indicator returns to "Live" (green)
  And missed events are replayed (dedup by event ID)

Scenario: SSE gives up after 5-minute offline threshold
  Given a live session lost SSE connection
  When 5 minutes pass without successful reconnection
  Then useSessionSSE stops reconnection attempts
  And the UI shows a "session expired" indicator

Scenario: SSE deduplicates replayed events
  Given a live session reconnects after brief disconnection
  When the server replays events that were already received
  Then events with previously seen IDs are silently dropped
  And the activity feed shows no duplicates
```

### Pause/Resume Flow

```gherkin
Scenario: Host pauses the session
  Given a live session is active
  When the host taps the pause button
  Then the SaveCompleteDialog opens
  When the host confirms save
  Then the session status changes to "Paused"
  And the connection indicator shows "PAUSA"
  And SSE broadcasts SessionPausedEvent

Scenario: Host resumes a paused session
  Given a live session is paused
  When the host taps the resume button
  Then the session status changes to "InProgress"
  And the connection indicator returns to "Live"
  And SSE broadcasts SessionResumedEvent
```

## SSE Event Envelope Contract (Sprint 3)

### Endpoint

```
GET /api/v1/sessions/{sessionId}/events/stream
Content-Type: text/event-stream
```

### Event Envelope

```typescript
interface SSEActivityEvent {
  id: string;           // UUID — unique per event, used for dedup
  type: ActivityEventType;
  playerId: string;     // UUID of the acting player
  data: Record<string, unknown>;
  timestamp: string;    // ISO 8601
}

type ActivityEventType =
  | 'dice_roll'       // data: { values: number[], total: number, playerName: string }
  | 'ai_tip'          // data: { text: string }
  | 'score_update'    // data: { action: string, newScore: number, playerName: string }
  | 'photo'           // data: { url: string, playerName: string }
  | 'note'            // data: { text: string, playerName: string }
  | 'audio_note'      // data: { url: string, duration: number, playerName: string }
  | 'turn_change'     // data: { from: string, to: string }
  | 'pause_resume'    // data: { paused: boolean }
  | 'session_start';  // data: {}
```

### POST Endpoint (Client → Server)

```
POST /api/v1/sessions/{sessionId}/events
Content-Type: application/json

{
  "type": "note",
  "data": { "playerName": "Alice", "text": "Great round!" }
}

Response: 201 Created
{
  "id": "uuid-assigned-by-server",
  "type": "note",
  ...
}
```

**Important**: The server SHOULD echo the client-provided event ID (if supplied) in its SSE broadcast to enable client-side deduplication of optimistic updates.

## Sprint-to-Item Mapping

| Sprint | Items | Status |
|--------|-------|--------|
| **S1** (Layout Foundation) | CardRack, TopBar, MiniNav, FloatingActionBar, LayoutShell, design tokens | ✅ Complete |
| **S2** (Game Night Pages) | GameNightPlanningLayout, inline picker, timeline, RSVP wiring | ✅ Complete |
| **S3** (QuickView + Desktop Session) | QuickView modes (game/session), LiveSessionLayout, CollapsiblePanel, responsive LiveSessionView | ✅ Complete |
| **S4** (Activity Feed + SSE) | useSessionSSE hook (dedup, backoff, 5min threshold), ActivityFeed wiring, ActivityFeedInputBar API POST | ✅ Complete |
| **S5** (Mobile Session) | BottomSheet wrapper, MobileSessionLayout, MobileStatusBar, MobileScorebar integration | ✅ Complete |

## References

- Brainstorm prototypes: `.superpowers/brainstorm/785115-1773216855/`
- Layout concepts archive: `docs/frontend/layout-concepts/`
- Epic #5033 layout foundation: `memory/epic-5033-layout.md`
- Design tokens: `apps/web/src/styles/design-tokens.css`
- CardRack implementation: `apps/web/src/components/layout/CardRack/`
- TopBar implementation: `apps/web/src/components/layout/TopBar/`
- LayoutShell: `apps/web/src/components/layout/LayoutShell/LayoutShell.tsx`
