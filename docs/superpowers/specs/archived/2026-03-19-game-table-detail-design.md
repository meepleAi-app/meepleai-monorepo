# Game Table Detail Page — Feature Specification

**Date**: 2026-03-19
**Status**: Draft
**Scope**: Unified game detail page with board-game table metaphor, responsive zones, drawer overlay

---

## 1. Vision

The game detail page becomes a **board game table**: the MeepleCard sits at the center, surrounded by thematic zones (Tools, Knowledge, Sessions). Users interact with buttons on the card to open a contextual drawer overlay — like picking up a card from the table and bringing it to the foreground.

**Target**: Authenticated users viewing a game in their library.
**Public page**: Minimal preview with CTA to register/add to collection.

**Design language**: Extends PR #23 "Il Tavolo" — dark theme (#0d1117), MtG card overlay (mechanic icon + state badge), mana pips for entity connections, TavoloSection headers.

---

## 2. Layout Architecture

### 2.1 Desktop (>1024px) — Full Table

```
┌──────────────┬──────────────────┬──────────────┐
│  ZONA SX     │    MEEPLECARD    │  ZONA DX     │
│  Strumenti   │   Hero+Flip      │  Conoscenza  │
│              │   MtG overlay    │              │
│  - Toolkit   │   Mana pips      │  - KB        │
│  - Note      │   Action buttons │  - Chat AI   │
│  - Links     │                  │  - Agent     │
│  - Ownership │                  │              │
├──────────────┴──────────────────┴──────────────┤
│               ZONA BASSO                        │
│  Sessioni: ultima | stats | + nuova             │
└─────────────────────────────────────────────────┘
```

Grid: `grid-template-columns: 1.2fr 2fr 1.2fr; grid-template-rows: 1fr auto;`

### 2.2 Tablet (640px–1024px) — Compact Table

Same 3-column grid, zones show fewer details (labels only, expandable on tap). Card slightly smaller but still dominant center.

Grid: `grid-template-columns: 1fr 1.5fr 1fr; grid-template-rows: 1fr auto;`

### 2.3 Smartphone (<640px) — Card + Accordions

```
┌─────────────────────┐
│     MEEPLECARD      │  ← ~60% viewport (normal)
│   Hero + actions    │  ← ~85% viewport (focus)
├─────────────────────┤
│ ▼ Strumenti         │  ← collapsible accordion
│ ▼ Conoscenza        │     one open at a time
│ ▼ Sessioni          │
└─────────────────────┘
```

**Focus mode**: Tap on card → card expands to ~85% viewport via `framer-motion` layout animation. Zones collapse below. A "Torna al tavolo" button appears below the card to exit focus mode. Tap the button or tap the card again to return to table view. No swipe gestures required — simple toggle via tap/click.

---

## 3. Components

### 3.1 GameTableLayout

**Purpose**: Responsive layout container — 3-zone grid on desktop/tablet, card + accordions on mobile.

**Location**: `apps/web/src/components/library/game-table/GameTableLayout.tsx`

**Props**:
```typescript
interface GameTableLayoutProps {
  card: ReactNode;
  toolsZone: ReactNode;
  knowledgeZone: ReactNode;
  sessionsZone: ReactNode;
  drawer?: ReactNode;
  drawerOpen?: boolean;
  onDrawerClose?: () => void;
}
```

**Behavior**:
- Desktop/tablet: CSS grid with zones as direct children.
- Mobile: Card at top, zones rendered as accordion items via collapsible sections. One zone open at a time.
- Drawer overlay rendered as portal when `drawerOpen` is true.

**Reuses**: `TavoloSection` for zone headers, dark theme tokens from PR #23.

### 3.2 GameTablePage (Authenticated)

**Location**: `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx` (replaces current)

**Responsibility**: Fetches game data, assembles GameTableLayout with zone contents.

**Data**: `useLibraryGameDetail(gameId)` — existing hook, provides all game metadata, play statistics, state, notes.

**Structure**:
```tsx
<GameTableLayout
  card={<MeepleCard entity="game" variant="hero" flippable ... />}
  toolsZone={<GameTableZoneTools ... />}
  knowledgeZone={<GameTableZoneKnowledge ... />}
  sessionsZone={<GameTableZoneSessions ... />}
  drawer={<GameTableDrawer ... />}
  drawerOpen={drawerState.isOpen}
  onDrawerClose={drawerState.close}
/>
```

### 3.3 MeepleCard Hero Configuration

Uses existing MeepleCard with PR #23 features:

- `variant="hero"`, `flippable`
- `entity="game"`
- MtG overlay: `mechanicIcon` (bottom-left) + `stateLabel` (bottom-right, ownership state)
- **Mechanic icon selection**: Use the first entry from `gameDetail.mechanics` array. Pass `<MechanicIcon mechanic={gameDetail.mechanics?.[0]?.name} />` — the component handles mapping to SVG and has a `DefaultMechanic` fallback for unknown mechanics. If `mechanics` is empty, omit the icon.
- Mana pips via `ManaLinkFooter`: agent (amber), kb (teal), session (indigo), chatSession (blue) — active/inactive based on entity existence
- Action buttons on card: Chat, Stats, KB, Toolkit — each opens drawer with corresponding content
- Flip back: description, categories, mechanics, designers, publishers, complexity, min age

### 3.4 Zone Components

#### GameTableZoneTools

**Location**: `apps/web/src/components/library/game-table/GameTableZoneTools.tsx`

**Contents**:
| Element | Component | Data Source |
|---------|-----------|------------|
| Toolkit | Link to `/library/games/[id]/toolkit` | — |
| Notes | Inline collapsible textarea + save. Uses `EditNotesModal` (existing) triggered by an "Edit" icon button. | `gameDetail.notes` |
| Links | `RelatedEntitiesSection` (existing) | Entity links API |
| Ownership | `DeclareOwnershipButton` + `RagAccessBadge` (existing) | `gameDetail.currentState`, `gameDetail.hasRagAccess` |
| Remove game | Danger button at bottom of zone. Uses `RemoveGameDialog` (existing). On removal, navigates to `/library`. | — |

#### GameTableZoneKnowledge

**Location**: `apps/web/src/components/library/game-table/GameTableZoneKnowledge.tsx`

**Pre-requisite**: Extract `useAgentKbDocs` and `useAgentThreads` from their current locations (inline in `AgentCharacterSheet.tsx` and `AgentExtraMeepleCard.tsx`) into shared hooks at `apps/web/src/hooks/queries/useAgentData.ts`. This resolves the existing DRY violation and makes them importable here.

**Contents**:
| Element | Component | Data Source |
|---------|-----------|------------|
| KB docs | Doc list + `DocumentStatusBadge` (PR #23) | `useAgentKbDocs(gameId)` (extracted shared hook) |
| Chat AI | Last message preview + "Open" → drawer | `useAgentThreads(agentId)` (extracted shared hook) |
| Agent | `AgentStatusBadge` (PR #23) + config | `useAgentStatus(agentId)` (extracted shared hook) |

#### GameTableZoneSessions

**Location**: `apps/web/src/components/library/game-table/GameTableZoneSessions.tsx`

**Contents**:
| Element | Component | Data Source |
|---------|-----------|------------|
| Active/last session | `ActiveSessionCard` (PR #23) or summary | `gameDetail.recentSessions` (array of `LibraryGameSession` with sessionDate, duration, score, didWin, players, notes) |
| Statistics | Win rate, total games, last played, avg duration | `gameDetail.timesPlayed`, `gameDetail.winRate`, `gameDetail.lastPlayed`, `gameDetail.avgDuration` (flat fields on `GameDetailDto`) |
| + New session | Button → session creation | — |

### 3.5 GameTableDrawer

**Location**: `apps/web/src/components/library/game-table/GameTableDrawer.tsx`

**Purpose**: Full-screen overlay with contextual content. Renders above the table with dark backdrop.

**Triggers and content**:
| Trigger | Content | Component |
|---------|---------|-----------|
| 💬 Chat button on card | Chat AI with SSE | `ChatThreadView` (existing) |
| 📊 Stats button on card | Detailed stats + history | `GameStatsPanel` (new) |
| 📚 KB button on card | Document list + upload | Adapted from `GameDetailKbTab` logic |
| 🧰 Toolkit button on card | Timer, dice, scorer | Link/embed toolkit |
| Tap "Open chat" in Knowledge zone | Chat AI | `ChatThreadView` |
| Tap document in Knowledge zone | Document viewer | Document detail |
| Tap session in Sessions zone | Session detail | Session detail view |

**Layout**:
```
┌─────────────────────────────────┐
│ [Icon] Drawer Title         [✕] │  ← sticky header
├─────────────────────────────────┤
│                                 │
│  Scrollable content             │
│                                 │
└─────────────────────────────────┘
Backdrop: bg-black/60 backdrop-blur-sm
```

**Animation**: `framer-motion` — slide-up from bottom (mobile), fade-in centered (desktop).
**Dismiss**: Swipe down (mobile), click ✕ or backdrop (desktop), Escape key.

**Dimensions**:
- Mobile: 90vh height, full width, top border-radius
- Tablet: 80vh, 85% width, centered
- Desktop: 70vh, 60% width, centered

### 3.6 useGameTableDrawer (Zustand Store)

**Location**: `apps/web/src/lib/stores/gameTableDrawerStore.ts`

```typescript
type DrawerContent =
  | { type: 'chat'; agentId: string; threadId?: string }
  | { type: 'stats'; gameId: string }
  | { type: 'kb'; gameId: string }
  | { type: 'toolkit'; gameId: string }
  | { type: 'document'; documentId: string }
  | { type: 'session'; sessionId: string };

interface GameTableDrawerState {
  isOpen: boolean;
  content: DrawerContent | null;
  open: (content: DrawerContent) => void;
  close: () => void;
}
```

### 3.7 GameStatsPanel (New)

**Location**: `apps/web/src/components/library/game-table/GameStatsPanel.tsx`

**Purpose**: Detailed play statistics for the drawer. Replaces the placeholder "Statistiche Partite" card.

**Contents**: Win rate, total games played, last played date, average duration. Data from flat fields on `GameDetailDto`: `timesPlayed`, `winRate`, `lastPlayed`, `avgDuration`. Also displays `recentSessions` array as a history list (date, score, win/loss, players).

**Note**: Fields like "average score" and "player count distribution" are not available on the current DTO. The panel displays only what the API provides. If richer stats are needed in the future, a new backend query can be added as a separate enhancement.

### 3.8 Public Game Detail Page (Preview)

**Location**: `apps/web/src/app/(public)/games/[id]/page.tsx` (replaces current)

**Layout**: Simple centered layout — no zones, no drawer.

**Contents**:
- MeepleCard hero (read-only, not flippable, no action buttons)
- MtG overlay: mechanic icon + "Catalogo" state label (info variant)
- Mana pips: all inactive (opacity-30)
- Metadata: players, duration, complexity, rating
- CTA: "Registrati per il tavolo completo" (unauthenticated) or "Aggiungi alla collezione" (authenticated, not in library)

---

## 4. Legacy Code Removal

### 4.1 Files to Delete (12 files)

**Component directory** — delete entire `apps/web/src/components/library/game-detail/` (11 files):
1. `GameDetailHeroCard.tsx` — replaced by direct MeepleCard hero
2. `GameDetailHero.tsx` — already deprecated
3. `GameDetailOverviewTab.tsx` — replaced by zones
4. `GameDetailAgentTab.tsx` — replaced by Knowledge zone + drawer
5. `GameDetailKbTab.tsx` — replaced by Knowledge zone + drawer
6. `GameDetailSessionsTab.tsx` — replaced by Sessions zone + drawer
7. `CatalogDetailsSection.tsx` — content in flip card back
8. `UserActionSection.tsx` — actions on card buttons + zones
9. `index.ts` — barrel export
10. `__tests__/GameDetailHero.test.tsx` — test for deleted component
11. `__tests__/UserActionSection.test.tsx` — test for deleted component

**Page support** (1 file):
12. `apps/web/src/app/(authenticated)/library/games/[gameId]/loading.tsx` — replaced by new loading skeleton

**Verification**: After deletion, run `grep -r "game-detail" apps/web/src/` to confirm no dead imports remain. All 11 component files are exclusively imported by the authenticated library game detail page — confirmed zero cross-imports elsewhere.

### 4.2 Files to Replace (2 files)

- `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx` → GameTablePage
- `apps/web/src/app/(public)/games/[id]/page.tsx` → Public preview

### 4.3 Files NOT Touched

- `/giochi/[id]/` — independent Italian page, different components
- Sub-pages: `toolkit/`, `agent/` routes under `[gameId]/` — separate detail views
- `error.tsx` — keep, may need minor updates

---

## 5. Reuse Map

### From PR #23 "Il Tavolo"

| Component | Used For |
|-----------|----------|
| `TavoloSection` | Zone headers (icon + title + divider) |
| `ManaSymbol` / `ManaLinkFooter` | Entity connection pips on hero card |
| `MechanicIcon` | MtG overlay bottom-left on hero card |
| `EntityTypeIcon` | Icons in zone items |
| `ActiveSessionCard` | Active session in Sessions zone |
| `DocumentStatusBadge` | KB doc status in Knowledge zone |
| `AgentStatusBadge` | Agent status in Knowledge zone |
| `coverOverlayStyles` | MtG overlay styling |
| Dark theme tokens | `#0d1117`, `#30363d`, `#21262d` |

### Pre-existing

| Component | Used For |
|-----------|----------|
| `MeepleCard` (hero, flippable) | Center of table |
| `ChatThreadView` | Chat in drawer |
| `RelatedEntitiesSection` | Links in Tools zone |
| `DeclareOwnershipButton` | Ownership in Tools zone |
| `RagAccessBadge` | RAG status in Tools zone |
| `EditNotesModal` | Notes editing (triggered from Tools zone) |
| `RemoveGameDialog` | Game removal (triggered from Tools zone danger button) |
| `useLibraryGameDetail` | Primary data hook |

---

## 6. Technical Notes

### State Management
- `useGameTableDrawer` (Zustand) — drawer state only
- No new global state — zones read from existing React Query hooks
- Mobile accordion state: local `useState` in GameTableLayout

### Animation
- `framer-motion` for: drawer overlay (slide-up/fade-in), card focus mode (layout animation), accordion expand/collapse
- Already used in project — no new dependency

### Accessibility
- Drawer: focus trap, Escape to close, aria-modal
- Accordion: aria-expanded, aria-controls, keyboard navigation
- Action buttons on card: minimum 44x44px touch targets (WCAG 2.1 AA)
- Zone headers: semantic headings (h2/h3)

### Loading Skeleton
When `useLibraryGameDetail` is loading, render a skeleton matching the table layout:
- Desktop: 3-col grid with card skeleton (center, pulsing rectangle), zone skeletons (left/right, 3 placeholder rows each), session bar skeleton (bottom)
- Mobile: Card skeleton (~60% viewport) + 3 accordion header skeletons

### Performance
- Zones lazy-load content (React.lazy or conditional render)
- Drawer content mounts only when open
- MeepleCard hero: React.memo (already implemented)
- Mana pip counts: derived from existing query data, no extra API calls

### Relation to Alpha Plan
- GameTableLayout is self-contained — works with current UnifiedShell
- If Alpha implements CardDetailModal, GameTableLayout can be rendered inside it without changes
- Alpha plan should be updated to reference GameTableLayout as the game detail view

---

## 7. Pre-requisites

Before implementation, extract shared hooks that currently exist only as inline functions:

| Hook | Current Location(s) | New Location |
|------|---------------------|--------------|
| `useAgentKbDocs(gameId)` | Inline in `AgentCharacterSheet.tsx`, `AgentExtraMeepleCard.tsx` | `apps/web/src/hooks/queries/useAgentData.ts` |
| `useAgentThreads(agentId)` | Inline in `AgentCharacterSheet.tsx`, `AgentExtraMeepleCard.tsx` | `apps/web/src/hooks/queries/useAgentData.ts` |
| `useAgentStatus(agentId)` | Inline in `AgentCharacterSheet.tsx` | `apps/web/src/hooks/queries/useAgentData.ts` |

After extraction, update `AgentCharacterSheet.tsx` and `AgentExtraMeepleCard.tsx` to import from the shared module. This resolves an existing DRY violation.

---

## 8. File Structure

```
apps/web/src/components/library/game-table/
├── GameTableLayout.tsx          # Responsive 3-zone layout
├── GameTableDrawer.tsx          # Overlay drawer
├── GameTableZoneTools.tsx       # Tools zone content
├── GameTableZoneKnowledge.tsx   # Knowledge zone content
├── GameTableZoneSessions.tsx    # Sessions zone content
├── GameStatsPanel.tsx           # Stats panel for drawer
├── index.ts                     # Barrel export
└── __tests__/
    ├── GameTableLayout.test.tsx
    ├── GameTableDrawer.test.tsx
    ├── GameTableZoneTools.test.tsx
    ├── GameTableZoneKnowledge.test.tsx
    ├── GameTableZoneSessions.test.tsx
    └── GameStatsPanel.test.tsx

apps/web/src/lib/stores/
└── gameTableDrawerStore.ts      # Zustand drawer store

# Pages (replaced)
apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx
apps/web/src/app/(public)/games/[id]/page.tsx
```

---

## 9. Success Criteria

1. Authenticated users see the game table layout on `/library/games/[gameId]`
2. MeepleCard hero with MtG overlay, mana pips, flip, and action buttons renders correctly
3. Three zones display correct content on desktop (3-col grid)
4. Tablet shows compact 3-col grid
5. Mobile shows card (~60% viewport) + accordion zones (one open at a time)
6. Mobile focus: card expands to ~85%, zones collapse, tap to return
7. Drawer overlay opens/closes with correct content for each trigger
8. Drawer dismisses via ✕, backdrop click, Escape
9. Public page shows preview with CTA, no zones, no drawer
10. All 12 legacy files removed, no dead imports remain (`grep -r "game-detail" apps/web/src/` returns nothing)
11. Shared hooks extracted (`useAgentKbDocs`, `useAgentThreads`) into `hooks/queries/useAgentData.ts`
12. Game removal and notes editing preserved (via existing `RemoveGameDialog` and `EditNotesModal`)
13. Loading skeleton matches table layout structure
14. All new components have unit tests
15. TypeScript typecheck and lint pass
16. No new backend endpoints required
