# MeepleCard Drawer + Navigation Redesign — Design Spec

**Date**: 2026-04-10
**Status**: Draft
**Scope**: Shell layout, drawer tabs, cascade navigation, ManaPip, session mode

## Summary

Redesign the MeepleAI navigation and drawer system around a **Page-first** flow. The user lands on an entity page and explores related entities via drawers — without leaving the page. Removes the CardHand left rail, transforms MyHand into lightweight recents, and introduces a ConnectionBar, conditional action footer, and mobile Session Mode.

## Decisions Log

| Question | Decision | Rationale |
|----------|----------|-----------|
| Primary flow | **Page-first** | User lands on entity page, drawer for related entities |
| CardHand (left rail) | **Remove** | Redundant with Page-first + recents |
| MyHand (4 fixed slots) | **Transform** → generic recents (A) + session-context (D) | Fixed slots too rigid; recents + session mode covers both needs |
| Cascade: ManaPip click | **Smart**: 1 → drawer direct, 2+ → DeckStack (B) | Saves clicks when choice is obvious |
| Cascade: NavFooter click | **Always drawer direct** (D) | NavFooter = intentional action; drawer shows picker if 2+ |
| Drawer interactivity | **Read + quick actions** (B) | Light actions in footer; complex editing on full page |
| Layout | **MiniNav potenziato, no sidebars** (C) | Full-width content, recents in MiniNav, session mode in bottom bar |
| ManaPip | **Dual role** (D) | Small indicators on cards in lists; interactive ConnectionBar on pages |

## 1. Shell Layout

### What Gets Removed

| Component | File | Reason |
|-----------|------|--------|
| `DesktopHandRail` | `components/layout/UserShell/DesktopHandRail.tsx` | Replaced by MiniNav recents |
| `use-card-hand.ts` store | `stores/use-card-hand.ts` | No longer needed |
| `MyHandSidebar` | `components/layout/MyHand/MyHandSidebar.tsx` | Replaced by recents + session mode |
| `MyHandBottomBar` | `components/layout/MyHand/MyHandBottomBar.tsx` | Replaced by MobileTabBar + session mode |
| `MyHandSlot`, `MyHandSlotPicker` | `components/layout/MyHand/` | No more fixed slots |
| `my-hand/` store + API | `stores/my-hand/`, `lib/api/my-hand.ts` | Backend API can stay for backward compat, frontend stops using it |

### New DesktopShell

```
┌──────────────────────────────────────────────┐
│ TopBar (64px)   [Logo] [Search] [Notif] [Av] │
├──────────────────────────────────────────────┤
│ MiniNav (48px)                               │
│ [← Breadcrumb] [Tab1] [Tab2]    [R1][R2][R3]│
├──────────────────────────────────────────────┤
│ SessionBanner (32px, only when active)       │
├──────────────────────────────────────────────┤
│                                              │
│         main content (full width)            │
│                                              │
└──────────────────────────────────────────────┘
```

```tsx
// New DesktopShell composition
function DesktopShell({ children }) {
  return (
    <div className="min-h-dvh flex flex-col">
      <TopBar />
      <MiniNavSlot />        {/* existing, enhanced with recents */}
      <SessionBanner />      {/* NEW: only when activeSessionId */}
      <main className="flex-1 min-w-0 overflow-y-auto">
        {children}
      </main>
      <div className="md:hidden">
        <MobileBottomBar />  {/* NEW: replaces MyHandBottomBar */}
      </div>
      <ChatSlideOverPanel />
    </div>
  );
}
```

## 2. MiniNav Recents

### Store

```typescript
interface RecentItem {
  id: string;
  entity: MeepleEntityType;
  title: string;
  href: string;
  visitedAt: number;
}

interface RecentsStore {
  items: RecentItem[];        // max 4, FIFO
  push: (item: Omit<RecentItem, 'visitedAt'>) => void;
  remove: (id: string) => void;
  clear: () => void;
}
```

- Persisted in `sessionStorage` (resets on tab close)
- Current page excluded from display
- Max 3 visible (4 on screens > 1280px)

### Rendering

- 28px circle, entity-colored background alpha 10%, entity icon (Lucide, 14px)
- Hover: tooltip "Azul (Game)", glow ring
- Click: `router.push(href)`
- New pill enters fade+slide from right, oldest exits left
- **Mobile**: not shown in MiniNav (insufficient width)

## 3. ConnectionBar (ManaPip Evolved)

### Dual Role

**On cards in lists** (ShelfCard, MeepleCard grid): ManaPip stays as-is — small 4-8px colored dots indicating related entities exist. Not clickable on the card itself.

**On entity pages**: ConnectionBar appears below title/meta as interactive pills.

### ConnectionBar Component

```typescript
interface ConnectionPip {
  entityType: MeepleEntityType;
  count: number;
  label: string;
  icon: LucideIcon;
  isEmpty: boolean;             // count === 0: show "+" to create
}

interface ConnectionBarProps {
  connections: ConnectionPip[];
  onPipClick: (pip: ConnectionPip, anchorRect: DOMRect) => void;
}
```

### Click Behavior

| Trigger | Count | Action |
|---------|-------|--------|
| ConnectionBar pip | 0 | "Create" action (e.g., "Crea Agente per Azul") |
| ConnectionBar pip | 1 | Drawer opens directly |
| ConnectionBar pip | 2+ | DeckStack fan → click → Drawer |
| NavFooter on MeepleCard | any | Drawer opens directly. If 2+ entities of that type exist, drawer shows a compact picker list as first view (entity mini-cards, click one to load full detail in same drawer). |

### Connections per Entity Type

| Entity Page | Shows connections to |
|-------------|---------------------|
| game | Agent, KB, Chat, Sessions |
| player | Sessions, Favorite Games |
| session | Game, Players, Tools, Agent |
| agent | Game, KB, Chat |
| kb | Game, Agent |
| chat | Agent, Game |
| event | Participants, Games, Sessions |
| toolkit | Game, Tools, Sessions |
| tool | Toolkit |

### Rendering

- Horizontal pills, entity-colored (bg alpha 10%, text entity color)
- Icon (14px) + count + label
- Hover: glow ring entity-colored
- `isEmpty`: dashed border, "+" icon, dimmed
- Mobile: horizontal scroll if needed, ~36px height

## 4. Drawer System

### Architecture (Unchanged Pattern)

```
ExtraMeepleCardDrawer (Sheet wrapper)
  └── DrawerEntityRouter (switch on entityType)
       └── {Entity}DrawerContent (fetches data, renders tabs)
            ├── Tab content area (scrollable)
            └── ActionFooter (fixed bottom, conditional actions)
```

Drawer opens `side="right"` on desktop, `side="bottom"` on mobile (future enhancement).

### Drawer Action Footer

Fixed bar at bottom of drawer. Actions are **conditional** — hidden or disabled based on state.

```typescript
interface DrawerAction {
  icon: LucideIcon;
  label: string;
  onClick: () => void;
  variant: 'primary' | 'secondary' | 'danger';
  /** If false, action is hidden entirely (not just disabled) */
  enabled: boolean;
}
```

Every drawer includes `[↗ Apri pagina]` as the last action — the escape hatch to the full page.

### Drawer Stack Navigation

When clicking a linked entity inside a drawer (e.g., a session row in Game Storico), the current drawer is pushed to a stack and the new drawer opens. A back button in the drawer header returns to the previous drawer.

```typescript
interface DrawerStackEntry {
  entityType: DrawerEntityType;
  entityId: string;
  activeTabId?: string;
  scrollPosition?: number;
}
```

Max stack depth: 3. Beyond that, the oldest entry is discarded.

### Tab Definitions per Entity

#### GAME — 3 tabs (1 if zero plays)

| Tab | ID | Icon | Badge | Content |
|-----|----|------|-------|---------|
| Info | `info` | `Info` | — | Publisher, year, players, duration, complexity, description |
| Stats | `stats` | `BarChart3` | `totalPlays` | Plays, win rate, avg score, trend. **Hidden if user has 0 plays.** |
| Storico | `history` | `History` | — | Last plays: date, players, result, score. Each row links to session drawer. **Hidden if 0 plays.** |

Footer: `[▶️ Gioca]` always · `[🤖 Chiedi AI]` if agent exists and active · `[↗ Apri]` always

#### PLAYER — 3 tabs

> **Migration note**: The existing `PlayerExtraMeepleCard.tsx` has Profile/Achievements/History tabs. `PlayerDrawerContent.tsx` replaces it with Profilo/Stats/Storico — achievements folded into Stats. The old component is removed.

| Tab | ID | Icon | Badge | Content |
|-----|----|------|-------|---------|
| Profilo | `profile` | `User` | — | Avatar, name, member since, favorite game, groups |
| Stats | `stats` | `Trophy` | `totalWins` | Wins, plays, win rate, top 3 games |
| Storico | `history` | `History` | `totalSessions` | Last sessions: game, date, result, score. Each row links to session + game drawer. |

Footer: `[📊 Confronta]` if common games exist · `[↗ Apri]` always

#### SESSION — 3 tabs (2 if no toolkit)

> **Migration note**: The existing `ExtraMeepleCard.tsx` is a session-specific component with 6 tabs (Overview, Toolkit, Scoreboard, History, Media, AI). `SessionDrawerContent.tsx` replaces it with a simplified 3-tab drawer version (Live, Toolkit, Timeline). The old component is removed; any standalone usage (e.g., session detail page) should use the page's own tab system, not the drawer component.

| Tab | ID | Icon | Badge | Content |
|-----|----|------|-------|---------|
| Live | `live` | `Layers` | — | Status, turn, duration, player ranking with scores. Each player links to player drawer. |
| Toolkit | `toolkit` | `Wrench` | `toolCount` | Active tools with state (timer running, counter values). **Hidden if no toolkit.** |
| Timeline | `timeline` | `Clock` | — | Chrono feed: events + notes + inline photos |

Footer: `[▶️ Riprendi]` if in-progress/paused · `[📊 Risultati]` if completed · `[📤 Condividi]` if completed · `[↗ Apri]` always

#### AGENT — 3 tabs (2 if never invoked)

| Tab | ID | Icon | Badge | Content |
|-----|----|------|-------|---------|
| Overview | `overview` | `Bot` | — | Name, type, strategy, model, status, linked game, KB doc count |
| Storico | `history` | `History` | `invocationCount` | Last invocations: date, query type, latency. Each row links to chat thread. **Hidden if 0 invocations.** |
| Config | `config` | `Settings` | — | Read-only parameters: temperature, maxTokens, topK, strategy, rerank |

Footer: `[💬 Chat]` if agent active and KB populated · `[🔄 Riavvia]` if agent in error/idle · `[↗ Apri]` always

#### KB — 3 tabs

| Tab | ID | Icon | Badge | Content |
|-----|----|------|-------|---------|
| Overview | `overview` | `FileText` | — | Filename, size, pages, indexing status, dates, game, embedding model, chunk count |
| Anteprima | `preview` | `Eye` | — | First ~500 words of extracted content |
| Citazioni | `citations` | `MessageCircle` | citationCount | Chat threads that cited this document: thread title, date, cited passage with **page/paragraph reference**. Each row links to chat drawer. Empty state: "No citations yet — start a chat to use this document." |

Footer: `[🔄 Reindex]` if status = indexed or failed (not during processing) · `[⬇️ Download]` always · `[↗ Apri]` always

#### CHAT — 2 tabs

| Tab | ID | Icon | Badge | Content |
|-----|----|------|-------|---------|
| Messaggi | `messages` | `MessageSquare` | `messageCount` | Compact header (agent badge + game badge), last 6-8 messages bubble UI. Agent responses with citations show clickable pills `[📄 p.12 §3]` that open KB drawer at cited position. |
| Fonti | `sources` | `FileText` | — | KB documents cited in this chat: doc name, citation count, referenced pages/paragraphs. Each row links to KB drawer. |

Footer: `[💬 Continua]` if chat active · `[📦 Archivia]` if chat active · `[🔓 Riapri]` if chat archived · `[↗ Apri]` always

#### EVENT — 2 tabs

| Tab | ID | Icon | Badge | Content |
|-----|----|------|-------|---------|
| Overview | `overview` | `Calendar` | — | Date, time, location, organizer, notes, participant list with status (confirmed/pending/declined) |
| Programma | `program` | `Dices` | `gameCount` | Games with status (completed/in-progress/planned) + diary timeline if event is past/ongoing |

Footer: `[✅ Conferma]` if user hasn't confirmed · `[❌ Declina]` if user confirmed (toggle) · `[📤 Invita]` if user is organizer · `[↗ Apri]` always

#### TOOLKIT — 3 tabs

| Tab | ID | Icon | Badge | Content |
|-----|----|------|-------|---------|
| Overview | `overview` | `Wrench` | — | Name, version, published status, game, tool list with type and config summary |
| Template | `template` | `Layout` | — | Scoring dimensions, turn phases |
| Storico | `history` | `History` | `useCount` | Sessions that used this toolkit: date, players, duration. Each row links to session drawer. |

Footer: `[▶️ Usa in sessione]` if published · `[✏️ Modifica]` if user is owner · `[↗ Apri]` always

#### TOOL — 2 tabs

| Tab | ID | Icon | Badge | Content |
|-----|----|------|-------|---------|
| Dettaglio | `detail` | `Wrench` | — | Name, type, full config, parent toolkit, color |
| Preview | `preview` | `Play` | — | Interactive: timer countdown / dice roll / card draw |

Footer: `[▶️ Usa]` if in active session with this toolkit · `[✏️ Modifica]` if user is owner of parent toolkit · `[↗ Apri]` always

## 5. Cross-Entity Links in Drawers

Drawer content contains clickable references to other entities. Clicking pushes the current drawer onto a stack and opens the target drawer.

### Link Graph

```
Game ←→ Session   (Game.Storico rows / Session.Live game link)
Game ←→ Agent     (Game ConnectionBar / Agent.Overview game link)
Game ←→ KB        (Game ConnectionBar / KB.Overview game link)
Player ←→ Session (Player.Storico rows / Session.Live player rows)
Agent ←→ Chat     (Agent.Storico rows / Chat.Messaggi agent badge)
KB ←→ Chat        (KB.Citazioni rows / Chat.Fonti rows + message citation pills)
Session ←→ Toolkit (Session.Toolkit tab / Toolkit.Storico rows)
Session ←→ Player  (Session.Live player rows / Player.Storico session rows)
Toolkit ←→ Session (Toolkit.Storico rows / Session.Toolkit)
Event ←→ Game     (Event.Programma game rows)
Event ←→ Player   (Event.Overview participant rows)
```

### KB ↔ Chat Citation Detail

KB documents store chunk metadata with page/paragraph references. When an agent response cites a chunk:

1. **In Chat.Messaggi**: citation appears as an inline pill: `[📄 azul-regole.pdf p.12 §3]`
   - Click → opens KB drawer, scrolls Anteprima tab to approximate position
2. **In Chat.Fonti tab**: aggregated by document, listing all pages/paragraphs cited
3. **In KB.Citazioni tab**: lists all chat threads that referenced this doc, with cited passages

## 6. Session Mode

### Activation

Session Mode activates when `useDashboardMode().isGameMode === true` and `activeSessionId` is present.

### Mobile Bottom Bar

**Normal Mode** (no active session):
```
[🏠 Home] [🔍 Cerca] [📚 Libreria] [💬 Chat] [👤 Profilo]
```

**Session Mode** (active session):
```
[◀ Back] [📊 Classifica] [🧰 Toolkit*] [💬 AI*] [⋯ Altro]
```

| Slot | Action | Condition |
|------|--------|-----------|
| ◀ Back | Navigate back (does not close session) | Always |
| 📊 Classifica | Opens session drawer, tab "Live" | Always |
| 🧰 Toolkit | Opens session drawer, tab "Toolkit" | Only if session has toolkit |
| 💬 AI | Opens agent/chat drawer for session's agent | Only if game has active agent |
| ⋯ Altro | Popup menu: Share, Notes, Photos, Exit session | Always |

- Transition: crossfade 300ms between modes
- Session indicator: subtle indigo top border, slow pulse

### Desktop Session Banner

32px bar between MiniNav and content. Only appears when session is active.

```
┌──────────────────────────────────────────────────────────┐
│ 🟣 Sessione attiva: Serata Azul · Turno 3/5             │
│                    [📊 Classifica] [🧰 Toolkit] [💬 AI] [✕]│
└──────────────────────────────────────────────────────────┘
```

- Background: indigo alpha 8%, border-bottom indigo alpha 20%
- Buttons open session-related drawers
- `[✕]` exits session (with confirmation dialog)
- On unrelated pages: compact mode — just text + "Torna alla sessione" link

### Session Context in Drawers

When a drawer is opened from Session Mode:
- Drawer header shows subtle indigo top accent (2px)
- Footer includes extra action: `[↩ Torna a sessione]` → navigates to `/sessions/[id]/play`

## 7. Cascade Navigation Store (Updated)

The existing `cascade-navigation-store.ts` is updated to support the drawer stack:

```typescript
interface CascadeNavigationState {
  state: 'closed' | 'deckStack' | 'drawer';
  
  // Current drawer
  activeEntityType: MeepleEntityType | null;
  activeEntityId: string | null;
  activeTabId: string | null;
  
  // DeckStack
  sourceEntityId: string | null;
  anchorRect: DOMRect | null;
  
  // Drawer stack (max 3)
  drawerStack: DrawerStackEntry[];
  
  // Actions
  openDeckStack: (entityType, sourceEntityId, anchor?) => void;
  openDrawer: (entityType, entityId, tabId?) => void;
  pushDrawer: (entityType, entityId) => void;   // NEW: push current, open new
  popDrawer: () => void;                         // NEW: back to previous
  closeDrawer: () => void;
  closeCascade: () => void;
}

interface DrawerStackEntry {
  entityType: MeepleEntityType;
  entityId: string;
  activeTabId?: string;
  scrollPosition?: number;
}
```

## 8. Files Affected

### Remove
- `components/layout/UserShell/DesktopHandRail.tsx`
- `components/layout/UserShell/HandRailItem.tsx`
- `components/layout/UserShell/HandRailToolbar.tsx`
- `stores/use-card-hand.ts`
- `components/layout/MyHand/MyHandSidebar.tsx`
- `components/layout/MyHand/MyHandBottomBar.tsx`
- `components/layout/MyHand/MyHandSlot.tsx`
- `components/layout/MyHand/MyHandSlotPicker.tsx`
- `components/layout/MyHand/MyHandProvider.tsx`
- `stores/my-hand/` (entire directory)
- `components/ui/data-display/extra-meeple-card/ExtraMeepleCard.tsx` (superseded by SessionDrawerContent)
- `components/ui/data-display/extra-meeple-card/entities/PlayerExtraMeepleCard.tsx` (superseded by PlayerDrawerContent)

### Create
- `stores/use-recents.ts` — RecentsStore
- `components/layout/UserShell/RecentsBar.tsx` — pill bar for MiniNav
- `components/ui/data-display/connection-bar/ConnectionBar.tsx`
- `components/ui/data-display/connection-bar/types.ts`
- `components/layout/UserShell/SessionBanner.tsx`
- `components/layout/MobileBottomBar.tsx` — unified mobile nav with session mode
- `components/ui/data-display/extra-meeple-card/DrawerActionFooter.tsx`
- `components/ui/data-display/extra-meeple-card/entities/EventExtraMeepleCard.tsx`
- `components/ui/data-display/extra-meeple-card/entities/ToolkitExtraMeepleCard.tsx`
- `components/ui/data-display/extra-meeple-card/entities/ToolExtraMeepleCard.tsx`
- `components/ui/data-display/extra-meeple-card/entities/SessionDrawerContent.tsx`
- `components/ui/data-display/extra-meeple-card/entities/PlayerDrawerContent.tsx`

### Modify
- `components/layout/UserShell/DesktopShell.tsx` — remove HandRail + MyHand imports, add SessionBanner + MobileBottomBar
- `components/layout/UserShell/index.ts` — remove exports for DesktopHandRail, HandRailItem, HandRailToolbar
- `components/layout/MyHand/index.ts` — remove export for MyHandProvider (or delete barrel if empty)
- `components/layout/UserShell/MiniNavSlot.tsx` — add RecentsBar slot on the right side
- `lib/stores/cascade-navigation-store.ts` — add `activeTabId: string | null` to state, update `openDrawer` signature to accept optional `tabId`, add `DrawerStackEntry` type, `drawerStack: DrawerStackEntry[]`, `pushDrawer`, `popDrawer` actions
- `components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer.tsx` — update DrawerEntityRouter for all 9 entities (remove DrawerComingSoon branches), add stack nav back button, integrate DrawerActionFooter
- `components/ui/data-display/extra-meeple-card/drawer-states.tsx` — remove DrawerComingSoon component (all entities now have dedicated content)
- `components/ui/data-display/extra-meeple-card/drawer-test-ids.ts` — remove COMING_SOON factory, add test-ids for new drawer content components
- `components/ui/data-display/extra-meeple-card/EntityExtraMeepleCard.tsx` — remove re-export of PlayerExtraMeepleCard, update exports for new entity components
- `components/dashboard/SessionPanel.tsx` — use ConnectionBar instead of inline mana pips
- `config/component-registry.ts` — remove entries for HandRailItem, HandRailToolbar, DesktopHandRail

### Consumer Migration (use-card-hand.ts removal)

~31 files import `useCardHand` or `drawCard()`. These must be migrated:
- **Page-level `drawCard()` calls** (DashboardClient, LibraryHub, sessions/_content, agents/page, etc.): replace with `useRecents().push()` — same entity data, different store
- **Components reading `cards[]` for display** (DeckTrackerSync, quick-cards-carousel, session-quick-actions): remove or replace with recents-based logic
- **Test files** (~20): update mocks from useCardHand to useRecents
- Full consumer list to be generated during implementation via `grep -r "useCardHand\|use-card-hand" apps/web/src/`

## 9. Out of Scope

- Drawer `side="bottom"` on mobile (future enhancement, current right-side works)
- Backend API changes for MyHand (keep for backward compat, just stop using)
- ShelfCard ManaPip visual redesign (stays as-is)
- DeckStack component redesign (stays as-is, just wired differently)
- Full chat inline in drawer (drawer shows messages read-only; "Continua" navigates to full chat page)
