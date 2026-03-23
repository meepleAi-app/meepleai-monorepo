# User Dashboard & Session Navigation Design

**Date:** 2026-03-22
**Status:** Approved
**Approach:** Evoluzione Incrementale (extend existing DashboardEngine xstate)

## Overview

Redesign of the user dashboard and in-session navigation for MeepleAI. The dashboard dynamically adapts based on user context: exploration mode (browsing, library, suggestions) vs game mode (active session with sub-context navigation). The design introduces a 3-level zoom hierarchy: Carousel (exploration) → Tavolo + Dock (session) → Card Graph (contextual links within sheets).

## Target User

Mix of player and collector. The game session ("tavolo virtuale") is the heart of the app, but the user also lives outside sessions — browsing library, exploring games, interacting with AI agents. The dashboard must serve both contexts fluidly.

## Architecture: DashboardEngine States

Extend the existing xstate machine with new sub-states:

```
exploration
  ├── idle              # Dashboard with carousels
  └── searching         # Active search

transitioning           # Morph animation between modes

gameMode
  ├── tavolo            # Dock visible, no sheet open
  └── sheetOpen
      ├── context: "scores" | "rules-ai" | "timer" | "photos" | "players"
      └── breadcrumb: CardLink[]   # Navigation stack inside sheet
```

### New Events

| Event | Description |
|-------|-------------|
| `OPEN_SHEET(context)` | Opens bottom sheet with the specified sub-context |
| `CLOSE_SHEET` | Closes sheet, returns to tavolo |
| `NAVIGATE_CARD_LINK(target, label)` | Replaces sheet content, pushes to breadcrumb |
| `BACK_CARD_LINK` | Pops breadcrumb, returns to previous content |

### Changes from Current

- `gameMode` sub-states change from `default`/`expanded` to `tavolo`/`sheetOpen`
- `sheetOpen` maintains active context and breadcrumb stack internally
- Navbar reads `state.context` to determine what to render

## Navbar Transformation

`UserTopNav` renders conditionally based on dashboard state. No separate component in the hierarchy — `SessionNavBar` is a render branch inside `UserTopNav`.

### Exploration Mode (unchanged)

```
[Logo] [Dashboard] [Library] [Games] [Agents]    [🔔] [Avatar]
```

### Game Mode — Tavolo (no sheet open)

```
[← Esci] [🎲 GameName] [⏱ HH:MM:SS]    [🏆] [🤖] [⏱] [📸] [👥]    [🔔] [Avatar]
```

### Game Mode — Sheet Open (e.g. scores)

```
[← Esci] [🎲 GameName] [⏱ HH:MM:SS]    [🏆●] [🤖] [⏱] [📸] [👥]    [🔔] [Avatar]
```

Active sub-context icon is highlighted (orange background, glow). Tapping another icon closes current sheet and opens the new one.

### Navbar Elements in Session

| Element | Position | Behavior |
|---------|----------|----------|
| `← Esci` | Left | Returns to exploration (confirmation dialog if session active) |
| `🎲 Game Name` | Left-center | Non-interactive label, truncated if long |
| `⏱ Timer` | Center | Live timer display. Tap opens the timer sheet (same as tapping ⏱ icon). No separate inline expand — the sheet is the single source of timer interaction |
| Sub-context icons | Center-right | Tap to open/switch sheet |
| `🔔` + `Avatar` | Right | Unchanged |

### Mobile (<640px)

```
[←] [🎲 GameName] [⏱ HH:MM]    [🏆] [🤖] [⏱] [📸] [👥]
```

Notifications and avatar move to hamburger menu. If >4 sub-context icons, overflow into `•••` menu.

## Sheet System

Bottom sheet panel triggered by tapping sub-context icons in the navbar.

### Behavior

- Slides up from bottom with spring animation (framer-motion, `type: "spring"`, 300ms)
- Default height: **75vh**, drag handle for resize (snaps to 50vh, 75vh, 95vh)
- Tavolo/dashboard remains visible underneath, darkened with overlay `bg-black/30`
- Tapping overlay closes sheet
- Swipe down on drag handle closes sheet

### Sheet Content by Sub-Context

| Context | Content | Card Links |
|---------|---------|------------|
| `scores` | Live leaderboard, score input, categories | → `rules-ai` ("How is scoring done?") |
| `rules-ai` | AI chat with RAG on rulebook | → `scores` ("Go to scores") |
| `timer` | Turn timer, session stopwatch, turn history | → `players` ("Whose turn?") |
| `photos` | Session photo grid, camera, upload | — |
| `players` | Participant list, turn order, live stats | → `scores` ("Player scores") |

### Card Link Navigation

Card Links are contextual clickable chip/pills within sheet content.

**Interaction flow:**
1. Sheet content slides out to the left
2. New content (target) slides in from the right
3. Breadcrumb updates: `Scores > Scoring Rules`
4. Tapping "Scores" in breadcrumb reverses the animation

**No stacking** — content replaces in the same sheet. Breadcrumb provides back navigation. Simple and predictable. **Max breadcrumb depth: 3** — given the current cross-link map (each context links to at most 1-2 others), deeper navigation is unlikely but capped for safety.

### Sheet Components

| Component | Responsibility |
|-----------|---------------|
| `SessionSheet` | Container with drag handle, overlay, spring animations |
| `SheetContent` | Conditional render per context (scores, rules-ai, etc.) |
| `CardLinkChip` | Clickable pill with icon + label + arrow |
| `SheetBreadcrumb` | Internal navigation breadcrumb |

## Dashboard Exploration Mode

When no session is active (or session is in background), the dashboard shows carousel-based exploration.

### Layout (top to bottom, scrollable)

1. **HeroCompact** — Greeting + inline stats ("3 games this week · 12h played · avg rating 7.8")
2. **ActiveSessionBanner** — Conditional. Shows only if active session exists. Prominent "Resume →" CTA
3. **Carousel "Recent Games"** — User's library games sorted by last played. MeepleCard entity=game
4. **Carousel "Suggested for You"** — AI recommendations with reason ("Because you like TI4"). MeepleCard with cyan accent
5. **Carousel "Recent Sessions"** — Completed sessions with result, duration, participant avatars
6. **QuickStats** — 3-column grid (total games, total sessions, avg rating)

### Carousel Implementation

- **Recent Games / Suggested**: Use `GameCarousel` (3D perspective) on desktop, `QuickCardsCarousel` on mobile
- **Recent Sessions**: Use `QuickCardsCarousel` (lighter format, no cover image needed)
- Each carousel has "See all →" link to full page

## Component Map

### New Components

| Component | Responsibility | Depends On |
|-----------|---------------|------------|
| `SessionNavBar` | Conditional render inside UserTopNav in gameMode | DashboardEngine state |
| `SessionSheet` | Sheet container with drag, overlay, spring animations | framer-motion |
| `SheetContent` | Render per context (scores, rules-ai, timer, photos, players) | SessionSheet |
| `SheetBreadcrumb` | Internal navigation breadcrumb | DashboardEngine breadcrumb[] |
| `CardLinkChip` | Clickable pill with icon + label + arrow | NAVIGATE_CARD_LINK event |
| `TavoloView` | Tavolo layout: compact scoreboard, turn, log, quick actions | Session SSE data |
| `ExplorationView` | Exploration layout: hero + banner + carousel sections | GameCarousel, QuickCardsCarousel |

### Modified Components

| Component | Change |
|-----------|--------|
| `DashboardEngine.ts` | New sub-states `tavolo`, `sheetOpen` + sheet/cardlink events |
| `DashboardRenderer.tsx` | Render `TavoloView` or `ExplorationView` based on state |
| `UserTopNav.tsx` | Conditional render: `SessionNavBar` when `isGameMode` |
| `useDashboardMode.ts` | Expose `activeSheet`, `breadcrumb`, `openSheet()`, `closeSheet()`. Replaces current `isExpanded` (removed with `expanded` sub-state). `isGameMode` and `isExploration` remain |

### Unchanged Components

MeepleCard, GameCarousel, QuickCardsCarousel, LayoutProvider, AdminShell — remain as-is.

### Render Hierarchy

```
UserShell
  └── UserTopNav
        ├── [exploration] Logo, Nav links, Notifications, Avatar
        └── [gameMode] SessionNavBar
              ├── ExitButton (← Esci)
              ├── GameLabel (🎲 Name)
              ├── LiveTimer (⏱ HH:MM:SS)
              └── SubContextIcons (🏆 🤖 ⏱ 📸 👥)
  └── DashboardRenderer
        ├── [exploration] ExplorationView
        │     ├── HeroCompact
        │     ├── ActiveSessionBanner (conditional)
        │     ├── CarouselSection "Recent Games"
        │     ├── CarouselSection "Suggested"
        │     └── CarouselSection "Recent Sessions"
        └── [gameMode] TavoloView
              ├── ScoreboardCompact
              ├── TurnIndicator
              ├── QuickActions
              ├── EventLog
              └── SessionSheet (conditional, overlay)
                    ├── SheetBreadcrumb
                    ├── SheetContent[context]
                    └── CardLinkChip[]
```

## Data Flow & Backend Integration

### Exploration Mode — Carousel Data

| Carousel | Endpoint | Query Hook |
|----------|----------|------------|
| Recent Games | `GET /api/v1/user-library?sort=lastPlayed&limit=10` | `useUserLibrary()` |
| Suggested | `GET /api/v1/games/suggestions` **(new endpoint — requires backend work)** | `useSuggestedGames()` |
| Recent Sessions | `GET /api/v1/sessions?status=completed&limit=10` | `useRecentSessions()` |
| Active Session Banner | `GET /api/v1/sessions?status=active` | `useActiveSessions()` (exists) |

### Game Mode — Tavolo & Sheet Data

| Component | Data Source | Real-time |
|-----------|-----------|-----------|
| ScoreboardCompact | SSE `/api/v1/sessions/{id}/stream` | `ScoreUpdatedEvent` |
| TurnIndicator | SSE | `TurnChangedEvent` (new) |
| EventLog | SSE | All events |
| Sheet Scores | Query `GET /sessions/{id}/scores` + SSE | Yes |
| Sheet Rules AI | Chat SSE `POST /agents/{id}/chat` | Yes |
| Sheet Timer | Client-side + SSE sync (pause/resume) | Yes |
| Sheet Photos | Query `GET /sessions/{id}/photos` | No |
| Sheet Players | Query `GET /sessions/{id}/players` + SSE | Yes |

### New Endpoints (potential)

| Endpoint | Reason |
|----------|--------|
| `GET /api/v1/games/suggestions` | AI recommendations (if not existing) |
| `TurnChangedEvent` via SSE | Turn change notification |

No critical new endpoints required. Most data comes from existing endpoints and SSE streams. Card links navigate between already-loaded sheet content — no additional API calls needed.

## Implementation Order (incremental delivery)

1. **DashboardEngine extension** — new states, events, breadcrumb management
2. **ExplorationView** — carousel sections replacing current zone layout
3. **SessionNavBar** — navbar transformation in UserTopNav
4. **TavoloView** — game mode main view
5. **SessionSheet** — bottom sheet with drag, overlay, animations
6. **SheetContent modules** — scores, rules-ai, timer, photos, players
7. **CardLinkChip + SheetBreadcrumb** — contextual navigation within sheets
8. **Mobile responsive** — icon overflow, compact layouts

Each step is independently deployable and testable.
