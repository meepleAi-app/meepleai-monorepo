# MeepleCard Consumers Completion — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Update all 66 MeepleCard consumers to pass `navItems`, `actions`, and `status` props (with real data via batch hooks); create 3 missing entity adapters; delete all legacy/deprecated documentation; and ensure visualization pages (dev showcase, storybook, admin UI library) demonstrate the complete feature set.

**Architecture:**
- **Adapter-first**: All entity rendering goes through `Meeple{Entity}Card.tsx` adapters. Direct `<MeepleCard>` usage in pages/feeds is forbidden after this plan.
- **Smart adapters**: Each adapter fetches its own counts via dedicated hooks (`useGameNavCounts`, `usePlayerNavCounts`, etc.) and derives `navItems` automatically.
- **Batch endpoints**: For grids that render N entities, a new backend endpoint `POST /api/v1/games/batch-nav-counts` returns `{[gameId]: {kbCount, agentCount, chatCount, sessionCount}}` in one call to avoid N+1.
- **Drawer integration**: Adapters that already have drawer sheets (KbDrawerSheet, AgentDrawerSheet, ChatDrawerSheet, SessionDrawerSheet) wire their `onClick` handlers from navItems.
- **No backwards-compat**: All deprecated markdown deleted, all direct `<MeepleCard>` consumers refactored, no migration shims.

**Tech Stack:** React 19, TypeScript, Tailwind 4, TanStack Query v5, Zod schemas, Next.js 16 App Router, Lucide icons (replacing emoji where present).

**Build Note:** This plan keeps the branch compilable at every task boundary. Task ordering ensures: foundation → adapters → consumers → visualization → cleanup. Tests added incrementally per component.

**Spec:** `docs/superpowers/specs/2026-04-07-meeplecard-rewrite-from-mockups-design.md` Section 6.7 (NavFooter), 11 (Public API)

---

## NavItems Contracts per Entity Type

These contracts define the canonical `navItems` structure each adapter must produce. Implementation hooks (`useGameNavItems`, etc.) created in Phase 1 wrap these contracts.

### Game (`entity: 'game'`)

| Slot | Icon | Label | Count source | Click target | Plus action |
|------|------|-------|--------------|--------------|-------------|
| 0 | `BookOpen` | KB | `kbCardCount` from `UserLibraryEntry` OR `useKbGameDocuments(gameId).length` | Open `KbDrawerSheet` | Open upload PDF wizard if `count === 0` |
| 1 | `Bot` | Agent | `useGameAgents({ gameId }).length` | Open `AgentDrawerSheet` | Open agent creation wizard if `count === 0` |
| 2 | `MessageCircle` | Chat | `chatSessionsCount` from associatedData OR new batch endpoint | Open `ChatDrawerSheet` | Start new chat if `count === 0` |
| 3 | `Dices` | Sessioni | `gameSessionsCount` from associatedData OR `useGameSessions(gameId).length` | Open `SessionDrawerSheet` | Start new session if `count === 0` |

### Player (`entity: 'player'`)

| Slot | Icon | Label | Count source | Click target |
|------|------|-------|--------------|--------------|
| 0 | `Trophy` | Vittorie | `PlayerStatistics.totalWins` | Navigate to `/players/{id}/wins` |
| 1 | `Dices` | Partite | `PlayerStatistics.totalSessions` | Navigate to `/players/{id}/history` |
| 2 | `Star` | Preferiti | NOT IMPLEMENTED in v1 — slot disabled (no `useFavoriteGames` hook exists) | — |
| 3 | `Award` | Achievement | NOT IMPLEMENTED in v1 — slot disabled (no achievement system) | — |

**Note:** Player adapter shows only 2 active slots in v1. Slots 2 and 3 are rendered as disabled placeholders to keep the visual layout consistent with other entities (4 slots).

### Session (`entity: 'session'`)

| Slot | Icon | Label | Count source | Click target |
|------|------|-------|--------------|--------------|
| 0 | `Users` | Giocatori | `LiveSessionDto.playerCount` (already in DTO) | Open players panel via internal modal |
| 1 | `FileText` | Note | `LiveSessionDto.notes && notes.length > 0 ? 1 : undefined` (presence indicator) | Open notes editor |
| 2 | `Wrench` | Tools | `SessionToolsDto.customTools.length` (custom tools only; base tools always present) | Open tools panel |
| 3 | `Camera` | Foto | `getSnapshots(sessionId).reduce((sum, s) => sum + s.attachmentCount, 0)` via existing `api.sessionTracking.getSnapshots` | Open gallery modal |

### Agent (`entity: 'agent'`)

| Slot | Icon | Label | Count source | Click target |
|------|------|-------|--------------|--------------|
| 0 | `MessageCircle` | Chat | `useChatSessions({ agentId }).sessions.length` (existing hook, filter client-side) | Navigate to `/chat?agentId={id}` |
| 1 | `BookOpen` | KB | `AgentStatus.documentCount` from `api.agents.getStatus(id)` | Open KB sources drawer |
| 2 | `Brain` | Memorie | NOT IMPLEMENTED in v1 — slot disabled | — |
| 3 | `Settings` | Config | no count, action button | Navigate to `/agents/{id}/settings` |

### KB (`entity: 'kb'`)

| Slot | Icon | Label | Count source | Click target |
|------|------|-------|--------------|--------------|
| 0 | `Layers` | Chunks | `AdminKbDocument.chunkCount` | Show chunks viewer |
| 1 | `RefreshCw` | Reindex | no count, action button | Trigger reindex mutation |
| 2 | `Eye` | Anteprima | no count, action | Open PDF viewer |
| 3 | `Download` | Download | no count, action | Trigger download |

### Chat (`entity: 'chat'`)

| Slot | Icon | Label | Count source | Click target |
|------|------|-------|--------------|--------------|
| 0 | `MessageSquare` | Messaggi | `ChatSessionSummaryDto.messageCount` | Continue chat |
| 1 | `BookOpen` | Sources | NOT IMPLEMENTED — slot disabled | — |
| 2 | `Bot` | Agente | no count, link | Navigate to source agent |
| 3 | `Archive` | Archivia | no count, action | Archive mutation |

### Event (`entity: 'event'`, game nights)

| Slot | Icon | Label | Count source | Click target |
|------|------|-------|--------------|--------------|
| 0 | `Users` | Partecipanti | `GameNight.participants.length` | Open RSVP panel |
| 1 | `MapPin` | Luogo | no count, link | Open location |
| 2 | `Dices` | Giochi | `GameNight.proposedGames.length` | Open game proposals |
| 3 | `Calendar` | Data | no count, link | Open calendar |

### Toolkit (`entity: 'toolkit'`) and Tool (`entity: 'tool'`)

Toolkit and tool entities are simpler — only show count of items they contain or sessions where they were used.

| Entity | Slot 0 | Slot 1 | Slot 2 | Slot 3 |
|--------|--------|--------|--------|--------|
| toolkit | `Wrench` Tools | `Layout` Decks | `Clock` Phases | `History` Usi |
| tool | `Play` Usa | `Edit` Modifica | `Copy` Duplica | `History` Storico |

---

## File Structure

```
apps/web/src/
├── components/
│   └── ui/
│       └── data-display/
│           └── meeple-card/
│               ├── nav-items/                      # NEW: contract helpers
│               │   ├── buildGameNavItems.ts        # NEW
│               │   ├── buildPlayerNavItems.ts      # NEW
│               │   ├── buildSessionNavItems.ts     # NEW
│               │   ├── buildAgentNavItems.ts       # NEW
│               │   ├── buildKbNavItems.ts          # NEW
│               │   ├── buildChatNavItems.ts        # NEW
│               │   ├── buildEventNavItems.ts       # NEW
│               │   ├── buildToolkitNavItems.ts     # NEW
│               │   ├── buildToolNavItems.ts        # NEW
│               │   ├── icons.ts                    # NEW: Lucide icon→string mapping
│               │   ├── index.ts                    # NEW: barrel
│               │   └── __tests__/
│               │       └── buildGameNavItems.test.ts  # NEW
│               ├── parts/
│               │   └── NavFooter.tsx               # MODIFY: support React.ReactNode icons
│               └── types.ts                        # MODIFY: NavFooterItem.icon = ReactNode
│
├── hooks/
│   └── queries/
│       ├── useBatchGameNavCounts.ts                # NEW: batch hook
│       ├── useGameNavCounts.ts                     # NEW: single-game hook
│       ├── usePlayerNavCounts.ts                   # NEW
│       ├── useSessionNavCounts.ts                  # NEW
│       ├── useAgentNavCounts.ts                    # NEW
│       ├── useKbNavCounts.ts                       # NEW
│       ├── useChatNavCounts.ts                     # NEW
│       ├── useEventNavCounts.ts                    # NEW
│       └── __tests__/
│           └── useBatchGameNavCounts.test.tsx      # NEW
│
├── lib/
│   └── api/
│       ├── clients/
│       │   └── games-client.ts                     # MODIFY: add getBatchNavCounts
│       └── schemas/
│           └── games.schemas.ts                    # MODIFY: add BatchNavCountsResponse
│
└── components/
    ├── games/
    │   ├── MeepleGameCard.tsx                      # MODIFY: pass navItems
    │   └── BggGameCard.tsx                         # MODIFY: pass navItems
    │   └── BggSearchPanel.tsx                      # MODIFY: pass navItems
    ├── bgg/
    │   ├── BggGameCard.tsx                         # MODIFY
    │   └── BggPreviewCard.tsx                      # MODIFY
    ├── library/
    │   ├── MeepleLibraryGameCard.tsx               # MODIFY
    │   ├── MeepleUserLibraryCard.tsx               # MODIFY: wire drawers to navItems
    │   ├── PersonalLibraryPage.tsx                 # MODIFY: replace direct MeepleCard
    │   ├── PublicLibraryPage.tsx                   # MODIFY
    │   ├── LibraryGameGrid.tsx                     # MODIFY
    │   └── RecentLibraryCard.tsx                   # MODIFY
    ├── catalog/
    │   └── MeepleGameCatalogCard.tsx               # MODIFY
    ├── catalog/                                    # NEW: shared catalog adapter
    ├── players/
    │   └── MeeplePlayerCard.tsx                    # MODIFY
    ├── session/
    │   ├── MeepleSessionCard.tsx                   # MODIFY
    │   ├── MeepleResumeSessionCard.tsx             # MODIFY
    │   └── MeepleParticipantCard.tsx               # MODIFY
    ├── chat-unified/
    │   ├── MeepleChatCard.tsx                      # NEW: chat adapter
    │   └── NewChatView.tsx                         # MODIFY
    ├── agents/                                     # NEW dir
    │   └── MeepleAgentCard.tsx                     # NEW
    ├── game-night/
    │   ├── MeepleGameNightCard.tsx                 # MODIFY
    │   ├── MeepleEventCard.tsx                     # NEW
    │   ├── MeepleDealtGameCard.tsx                 # MODIFY
    │   └── MeepleAISuggestionCard.tsx              # MODIFY
    ├── documents/
    │   └── MeepleKbCard.tsx                        # MODIFY
    ├── library/private-game-detail/
    │   └── MeeplePausedSessionCard.tsx             # MODIFY
    ├── playlists/
    │   └── MeeplePlaylistCard.tsx                  # MODIFY
    ├── wishlist/
    │   └── MeepleWishlistCard.tsx                  # MODIFY
    ├── shared-games/
    │   └── MeepleContributorCard.tsx               # MODIFY
    ├── toolbox/
    │   └── ToolboxKitCard.tsx                      # MODIFY
    ├── game-detail/
    │   └── MeeplePdfReferenceCard.tsx              # MODIFY
    ├── contributions/
    │   └── ShareRequestCard.tsx                    # MODIFY
    ├── admin/
    │   ├── shared-games/
    │   │   └── game-catalog-grid.tsx               # MODIFY
    │   ├── knowledge-base/
    │   │   └── vector-game-card.tsx                # MODIFY
    │   └── ui-library/
    │       ├── component-map.ts                    # MODIFY: register new adapters
    │       └── scenes/
    │           └── EntityCardsScene.tsx            # MODIFY: showcase navItems
    ├── dashboard/
    │   ├── RecentGamesRow.tsx                      # MODIFY
    │   └── v2/
    │       ├── RecentGames.tsx                     # MODIFY
    │       └── YourAgents.tsx                      # MODIFY
    ├── collection/
    │   └── CollectionGameGrid.tsx                  # MODIFY
    ├── play-records/
    │   └── PlayHistory.tsx                         # MODIFY
    ├── features/
    │   ├── home/HomeFeed.tsx                       # MODIFY
    │   ├── library/LibraryPanel.tsx                # MODIFY
    │   ├── play/PlayPanel.tsx                      # MODIFY
    │   └── chat/ChatPanel.tsx                      # MODIFY
    └── showcase/stories/
        └── meeple-card.story.tsx                   # MODIFY: showcase navItems
│
└── app/
    ├── (public)/
    │   ├── dev/meeple-card/page.tsx                # MODIFY: navItems showcase section
    │   ├── shared-games/[id]/page.tsx              # MODIFY
    │   └── library/shared/[token]/page.tsx         # MODIFY
    ├── (chat)/chat/page.tsx                        # MODIFY
    ├── (authenticated)/
    │   ├── agents/page.tsx                         # MODIFY
    │   ├── library/games/[gameId]/page.tsx         # MODIFY
    │   ├── library/library-mobile.tsx              # MODIFY
    │   ├── library/private/PrivateGamesClient.tsx  # MODIFY
    │   ├── knowledge-base/[id]/page.tsx            # MODIFY
    │   ├── players/[id]/page.tsx                   # MODIFY
    │   └── sessions/_content.tsx                   # MODIFY
    └── admin/(dashboard)/shared-games/import/steps/
        ├── Step2MetadataReview.tsx                 # MODIFY
        └── Step3PreviewConfirm.tsx                 # MODIFY

apps/api/src/Api/
└── BoundedContexts/GameManagement/
    ├── Application/Queries/
    │   └── BatchGameNavCountsQuery.cs              # NEW
    ├── Application/Handlers/
    │   └── BatchGameNavCountsHandler.cs            # NEW
    └── Routing/Games/
        └── GamesNavCountsEndpoint.cs               # NEW

docs/
├── frontend/components/
│   ├── meeple-card.md                              # DELETE
│   └── meeple-card-migration.md                    # DELETE
├── superpowers/plans/archived/
│   ├── 2026-04-03-meeplecard-adapter-migration.md  # DELETE
│   └── 2026-03-28-user-pages-phase3-library.md     # KEEP (different scope)
└── superpowers/specs/
    └── 2026-04-03-meeplecard-adapter-migration-design.md  # DELETE

apps/web/MIGRATION_GAMECARD_TO_MEEPLECARD.md        # DELETE (if exists)
apps/web/src/components/ui/data-display/MEEPLE_CARD_USAGE.md  # DELETE
```

---

## Phase 0: Branch Setup

### Task 0: Create feature branch and worktree

**Files:** None — branch operation only

- [ ] **Step 1: Create feature branch from current main-dev**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git fetch origin
git checkout main-dev
git pull
git checkout -b feature/meeplecard-consumers-completion
git config branch.feature/meeplecard-consumers-completion.parent main-dev
```

- [ ] **Step 2: Verify clean working tree**

```bash
git status
```

Expected: `nothing to commit, working tree clean`

---

## Phase 1: Foundation — NavItems Builders + Hooks

### Task 1: Update NavFooterItem type to support React icons

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/NavFooter.tsx`

- [ ] **Step 1: Read current types.ts**

```bash
cat apps/web/src/components/ui/data-display/meeple-card/types.ts
```

- [ ] **Step 2: Update NavFooterItem interface**

In `types.ts`, replace `icon: string` with `icon: ReactNode`:

```typescript
import type { ReactNode } from 'react';

export interface NavFooterItem {
  icon: ReactNode;          // CHANGED from string
  label: string;
  entity: MeepleEntityType;
  count?: number;
  showPlus?: boolean;
  disabled?: boolean;
  onClick?: () => void;
  onPlusClick?: () => void;  // NEW: separate click for plus button
}
```

- [ ] **Step 3: Update NavFooter.tsx to render ReactNode icons**

Replace line 45 in `NavFooter.tsx`:

```tsx
<span className="pointer-events-none">{item.icon}</span>
```

(no change needed in JSX — ReactNode renders correctly. But ensure the icon container has correct sizing for SVG icons:)

```tsx
<span className="pointer-events-none flex h-3.5 w-3.5 items-center justify-center [&>svg]:h-full [&>svg]:w-full">
  {item.icon}
</span>
```

- [ ] **Step 4: Add onPlusClick handler**

In NavFooter.tsx, change the plus button to be clickable (currently it's just a span):

```tsx
{item.showPlus && (
  <button
    type="button"
    onClick={e => {
      e.stopPropagation();
      item.onPlusClick?.();
    }}
    className="absolute -bottom-0.5 -right-0.5 flex h-3.5 w-3.5 items-center justify-center rounded-full text-[8px] font-extrabold text-white"
    style={{ background: color }}
    aria-label={`Aggiungi ${item.label}`}
  >
    +
  </button>
)}
```

- [ ] **Step 5: Run typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: PASS (existing string icons in tests will need update in Task 19)

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/types.ts apps/web/src/components/ui/data-display/meeple-card/parts/NavFooter.tsx
git commit -m "feat(meeple-card): support ReactNode icons and onPlusClick in NavFooter"
```

---

### Task 2: Create nav-items directory structure and icons mapping

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/nav-items/icons.ts`
- Create: `apps/web/src/components/ui/data-display/meeple-card/nav-items/index.ts`

- [ ] **Step 1: Write icons.ts with Lucide icon registry**

```typescript
// apps/web/src/components/ui/data-display/meeple-card/nav-items/icons.ts
import {
  BookOpen,
  Bot,
  MessageCircle,
  Dices,
  Trophy,
  Star,
  Award,
  Users,
  FileText,
  Wrench,
  Camera,
  Brain,
  Settings,
  Layers,
  RefreshCw,
  Eye,
  Download,
  MessageSquare,
  Archive,
  MapPin,
  Calendar,
  Layout,
  Clock,
  History,
  Play,
  Edit,
  Copy,
} from 'lucide-react';
import type { ReactElement } from 'react';

const ICON_SIZE = 14;

export const navIcons = {
  // Game
  kb: <BookOpen size={ICON_SIZE} strokeWidth={1.75} />,
  agent: <Bot size={ICON_SIZE} strokeWidth={1.75} />,
  chat: <MessageCircle size={ICON_SIZE} strokeWidth={1.75} />,
  session: <Dices size={ICON_SIZE} strokeWidth={1.75} />,
  // Player
  trophy: <Trophy size={ICON_SIZE} strokeWidth={1.75} />,
  partite: <Dices size={ICON_SIZE} strokeWidth={1.75} />,
  favorites: <Star size={ICON_SIZE} strokeWidth={1.75} />,
  achievement: <Award size={ICON_SIZE} strokeWidth={1.75} />,
  // Session
  players: <Users size={ICON_SIZE} strokeWidth={1.75} />,
  notes: <FileText size={ICON_SIZE} strokeWidth={1.75} />,
  tools: <Wrench size={ICON_SIZE} strokeWidth={1.75} />,
  photos: <Camera size={ICON_SIZE} strokeWidth={1.75} />,
  // Agent
  memory: <Brain size={ICON_SIZE} strokeWidth={1.75} />,
  config: <Settings size={ICON_SIZE} strokeWidth={1.75} />,
  // KB
  chunks: <Layers size={ICON_SIZE} strokeWidth={1.75} />,
  reindex: <RefreshCw size={ICON_SIZE} strokeWidth={1.75} />,
  preview: <Eye size={ICON_SIZE} strokeWidth={1.75} />,
  download: <Download size={ICON_SIZE} strokeWidth={1.75} />,
  // Chat
  messages: <MessageSquare size={ICON_SIZE} strokeWidth={1.75} />,
  archive: <Archive size={ICON_SIZE} strokeWidth={1.75} />,
  // Event
  location: <MapPin size={ICON_SIZE} strokeWidth={1.75} />,
  games: <Dices size={ICON_SIZE} strokeWidth={1.75} />,
  date: <Calendar size={ICON_SIZE} strokeWidth={1.75} />,
  // Toolkit/Tool
  decks: <Layout size={ICON_SIZE} strokeWidth={1.75} />,
  phases: <Clock size={ICON_SIZE} strokeWidth={1.75} />,
  history: <History size={ICON_SIZE} strokeWidth={1.75} />,
  use: <Play size={ICON_SIZE} strokeWidth={1.75} />,
  edit: <Edit size={ICON_SIZE} strokeWidth={1.75} />,
  copy: <Copy size={ICON_SIZE} strokeWidth={1.75} />,
} as const satisfies Record<string, ReactElement>;

export type NavIconKey = keyof typeof navIcons;
```

- [ ] **Step 2: Write index.ts barrel**

```typescript
// apps/web/src/components/ui/data-display/meeple-card/nav-items/index.ts
export { navIcons, type NavIconKey } from './icons';
export { buildGameNavItems, type GameNavCounts } from './buildGameNavItems';
export { buildPlayerNavItems, type PlayerNavCounts } from './buildPlayerNavItems';
export { buildSessionNavItems, type SessionNavCounts } from './buildSessionNavItems';
export { buildAgentNavItems, type AgentNavCounts } from './buildAgentNavItems';
export { buildKbNavItems, type KbNavActions } from './buildKbNavItems';
export { buildChatNavItems, type ChatNavCounts } from './buildChatNavItems';
export { buildEventNavItems, type EventNavCounts } from './buildEventNavItems';
export { buildToolkitNavItems, type ToolkitNavCounts } from './buildToolkitNavItems';
export { buildToolNavItems, type ToolNavActions } from './buildToolNavItems';
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/nav-items/
git commit -m "feat(meeple-card): add nav-items directory with Lucide icon registry"
```

---

### Task 3: Implement buildGameNavItems

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/nav-items/buildGameNavItems.ts`
- Create: `apps/web/src/components/ui/data-display/meeple-card/nav-items/__tests__/buildGameNavItems.test.ts`

- [ ] **Step 1: Write the failing test**

```typescript
// apps/web/src/components/ui/data-display/meeple-card/nav-items/__tests__/buildGameNavItems.test.ts
import { describe, expect, it, vi } from 'vitest';
import { buildGameNavItems } from '../buildGameNavItems';

describe('buildGameNavItems', () => {
  const baseHandlers = {
    onKbClick: vi.fn(),
    onAgentClick: vi.fn(),
    onChatClick: vi.fn(),
    onSessionClick: vi.fn(),
    onKbPlus: vi.fn(),
    onAgentPlus: vi.fn(),
    onChatPlus: vi.fn(),
    onSessionPlus: vi.fn(),
  };

  it('returns 4 nav items in fixed order', () => {
    const items = buildGameNavItems(
      { kbCount: 3, agentCount: 1, chatCount: 5, sessionCount: 2 },
      baseHandlers
    );
    expect(items).toHaveLength(4);
    expect(items.map(i => i.label)).toEqual(['KB', 'Agent', 'Chat', 'Sessioni']);
  });

  it('shows count when > 0', () => {
    const items = buildGameNavItems(
      { kbCount: 3, agentCount: 1, chatCount: 0, sessionCount: 0 },
      baseHandlers
    );
    expect(items[0].count).toBe(3);
    expect(items[1].count).toBe(1);
    expect(items[2].count).toBeUndefined();
    expect(items[3].count).toBeUndefined();
  });

  it('shows plus indicator when count is 0', () => {
    const items = buildGameNavItems(
      { kbCount: 0, agentCount: 0, chatCount: 0, sessionCount: 0 },
      baseHandlers
    );
    expect(items.every(i => i.showPlus)).toBe(true);
  });

  it('routes onClick to provided handlers', () => {
    const items = buildGameNavItems(
      { kbCount: 1, agentCount: 1, chatCount: 1, sessionCount: 1 },
      baseHandlers
    );
    items[0].onClick?.();
    expect(baseHandlers.onKbClick).toHaveBeenCalledOnce();
    items[1].onClick?.();
    expect(baseHandlers.onAgentClick).toHaveBeenCalledOnce();
    items[2].onClick?.();
    expect(baseHandlers.onChatClick).toHaveBeenCalledOnce();
    items[3].onClick?.();
    expect(baseHandlers.onSessionClick).toHaveBeenCalledOnce();
  });

  it('marks items disabled when handler is missing', () => {
    const items = buildGameNavItems(
      { kbCount: 1, agentCount: 0, chatCount: 0, sessionCount: 0 },
      { onKbClick: vi.fn() } // only kb handler provided
    );
    expect(items[0].disabled).toBeFalsy();
    expect(items[1].disabled).toBe(true);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test buildGameNavItems
```

Expected: FAIL with `Cannot find module '../buildGameNavItems'`

- [ ] **Step 3: Write minimal implementation**

```typescript
// apps/web/src/components/ui/data-display/meeple-card/nav-items/buildGameNavItems.ts
import type { NavFooterItem } from '../types';
import { navIcons } from './icons';

export interface GameNavCounts {
  kbCount: number;
  agentCount: number;
  chatCount: number;
  sessionCount: number;
}

export interface GameNavHandlers {
  onKbClick?: () => void;
  onAgentClick?: () => void;
  onChatClick?: () => void;
  onSessionClick?: () => void;
  onKbPlus?: () => void;
  onAgentPlus?: () => void;
  onChatPlus?: () => void;
  onSessionPlus?: () => void;
}

export function buildGameNavItems(
  counts: GameNavCounts,
  handlers: GameNavHandlers
): NavFooterItem[] {
  return [
    {
      icon: navIcons.kb,
      label: 'KB',
      entity: 'kb',
      count: counts.kbCount > 0 ? counts.kbCount : undefined,
      showPlus: counts.kbCount === 0,
      disabled: !handlers.onKbClick,
      onClick: handlers.onKbClick,
      onPlusClick: handlers.onKbPlus,
    },
    {
      icon: navIcons.agent,
      label: 'Agent',
      entity: 'agent',
      count: counts.agentCount > 0 ? counts.agentCount : undefined,
      showPlus: counts.agentCount === 0,
      disabled: !handlers.onAgentClick,
      onClick: handlers.onAgentClick,
      onPlusClick: handlers.onAgentPlus,
    },
    {
      icon: navIcons.chat,
      label: 'Chat',
      entity: 'chat',
      count: counts.chatCount > 0 ? counts.chatCount : undefined,
      showPlus: counts.chatCount === 0,
      disabled: !handlers.onChatClick,
      onClick: handlers.onChatClick,
      onPlusClick: handlers.onChatPlus,
    },
    {
      icon: navIcons.session,
      label: 'Sessioni',
      entity: 'session',
      count: counts.sessionCount > 0 ? counts.sessionCount : undefined,
      showPlus: counts.sessionCount === 0,
      disabled: !handlers.onSessionClick,
      onClick: handlers.onSessionClick,
      onPlusClick: handlers.onSessionPlus,
    },
  ];
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/web && pnpm test buildGameNavItems
```

Expected: PASS (5 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/nav-items/buildGameNavItems.ts apps/web/src/components/ui/data-display/meeple-card/nav-items/__tests__/buildGameNavItems.test.ts
git commit -m "feat(meeple-card): add buildGameNavItems with TDD coverage"
```

---

### Task 4: Implement remaining 8 nav-item builders

Each builder follows the same pattern as `buildGameNavItems`. To keep this plan compact, the structure for each is shown without TDD repetition. Tests for each builder follow the same shape: 4-5 tests covering count display, plus indicator, click routing, disabled state.

**Files:**
- Create: `apps/web/src/components/ui/data-display/meeple-card/nav-items/buildPlayerNavItems.ts`
- Create: `apps/web/src/components/ui/data-display/meeple-card/nav-items/buildSessionNavItems.ts`
- Create: `apps/web/src/components/ui/data-display/meeple-card/nav-items/buildAgentNavItems.ts`
- Create: `apps/web/src/components/ui/data-display/meeple-card/nav-items/buildKbNavItems.ts`
- Create: `apps/web/src/components/ui/data-display/meeple-card/nav-items/buildChatNavItems.ts`
- Create: `apps/web/src/components/ui/data-display/meeple-card/nav-items/buildEventNavItems.ts`
- Create: `apps/web/src/components/ui/data-display/meeple-card/nav-items/buildToolkitNavItems.ts`
- Create: `apps/web/src/components/ui/data-display/meeple-card/nav-items/buildToolNavItems.ts`
- Create: corresponding `__tests__/*.test.ts` for each

For brevity, here is `buildPlayerNavItems.ts` as a reference template; the other 7 follow the same pattern using their respective contracts from "NavItems Contracts per Entity Type" above.

```typescript
// apps/web/src/components/ui/data-display/meeple-card/nav-items/buildPlayerNavItems.ts
import type { NavFooterItem } from '../types';
import { navIcons } from './icons';

export interface PlayerNavCounts {
  totalWins: number;
  totalSessions: number;
  favoriteCount: number;
  achievementCount?: number; // optional, often unimplemented
}

export interface PlayerNavHandlers {
  onWinsClick?: () => void;
  onSessionsClick?: () => void;
  onFavoritesClick?: () => void;
  onAchievementsClick?: () => void;
}

export function buildPlayerNavItems(
  counts: PlayerNavCounts,
  handlers: PlayerNavHandlers
): NavFooterItem[] {
  return [
    {
      icon: navIcons.trophy,
      label: 'Vittorie',
      entity: 'player',
      count: counts.totalWins > 0 ? counts.totalWins : undefined,
      disabled: !handlers.onWinsClick,
      onClick: handlers.onWinsClick,
    },
    {
      icon: navIcons.partite,
      label: 'Partite',
      entity: 'session',
      count: counts.totalSessions > 0 ? counts.totalSessions : undefined,
      disabled: !handlers.onSessionsClick,
      onClick: handlers.onSessionsClick,
    },
    {
      icon: navIcons.favorites,
      label: 'Preferiti',
      entity: 'game',
      count: counts.favoriteCount > 0 ? counts.favoriteCount : undefined,
      disabled: !handlers.onFavoritesClick,
      onClick: handlers.onFavoritesClick,
    },
    {
      icon: navIcons.achievement,
      label: 'Trofei',
      entity: 'player',
      count: counts.achievementCount,
      disabled: counts.achievementCount === undefined || !handlers.onAchievementsClick,
      onClick: handlers.onAchievementsClick,
    },
  ];
}
```

- [ ] **Step 1: Write all 8 builder files** (one commit per builder, TDD)

For each builder:
1. Write failing test (5 cases each)
2. Run test, verify failure
3. Implement builder using the contract from the table above
4. Run test, verify pass
5. Commit individually: `feat(meeple-card): add build{Entity}NavItems`

**Reference contracts in "NavItems Contracts per Entity Type" section above for each entity's exact icons/labels/counts.**

- [ ] **Step 2: Final verification**

```bash
cd apps/web && pnpm test nav-items
```

Expected: PASS (~45 tests across 9 builders)

---

### Task 5: Add backend BatchGameNavCountsQuery + endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/BatchGameNavCountsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Handlers/BatchGameNavCountsHandler.cs`
- Create: `apps/api/src/Api/Routing/Games/GamesNavCountsEndpoint.cs`
- Modify: `apps/api/src/Api/Program.cs` (or wherever endpoints are mapped)
- Create: `tests/Api.Tests/BoundedContexts/GameManagement/BatchGameNavCountsHandlerTests.cs`

- [ ] **Step 1: Write the failing handler test**

```csharp
// tests/Api.Tests/BoundedContexts/GameManagement/BatchGameNavCountsHandlerTests.cs
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Handlers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
public class BatchGameNavCountsHandlerTests : IClassFixture<MeepleDbContextFixture>
{
    private readonly MeepleDbContextFixture _fixture;

    public BatchGameNavCountsHandlerTests(MeepleDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_ReturnsCountsForAllRequestedGames()
    {
        // Arrange
        await using var ctx = _fixture.CreateContext();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        // Seed: 2 PDFs, 1 agent, 3 chat sessions, 5 game sessions for gameId
        // ... seed entities ...

        var query = new BatchGameNavCountsQuery(userId, new[] { gameId });
        var handler = new BatchGameNavCountsHandler(ctx);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Counts.Should().ContainKey(gameId);
        result.Counts[gameId].KbCount.Should().Be(2);
        result.Counts[gameId].AgentCount.Should().Be(1);
        result.Counts[gameId].ChatCount.Should().Be(3);
        result.Counts[gameId].SessionCount.Should().Be(5);
    }

    [Fact]
    public async Task Handle_ReturnsZerosForUnknownGames()
    {
        await using var ctx = _fixture.CreateContext();
        var unknownGameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = new BatchGameNavCountsQuery(userId, new[] { unknownGameId });
        var handler = new BatchGameNavCountsHandler(ctx);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Counts[unknownGameId].KbCount.Should().Be(0);
        result.Counts[unknownGameId].AgentCount.Should().Be(0);
        result.Counts[unknownGameId].ChatCount.Should().Be(0);
        result.Counts[unknownGameId].SessionCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_RejectsBatchOver100Games()
    {
        await using var ctx = _fixture.CreateContext();
        var userId = Guid.NewGuid();
        var gameIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray();
        var query = new BatchGameNavCountsQuery(userId, gameIds);
        var handler = new BatchGameNavCountsHandler(ctx);

        var act = async () => await handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*max 100*");
    }
}
```

- [ ] **Step 2: Run test (verify failure)**

```bash
cd apps/api/src/Api && dotnet test --filter BatchGameNavCountsHandlerTests
```

Expected: FAIL — types don't exist

- [ ] **Step 3: Create the query DTO**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/BatchGameNavCountsQuery.cs
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

public sealed record GameNavCountsDto(int KbCount, int AgentCount, int ChatCount, int SessionCount);

public sealed record BatchGameNavCountsResult(IReadOnlyDictionary<Guid, GameNavCountsDto> Counts);

public sealed record BatchGameNavCountsQuery(Guid UserId, IReadOnlyCollection<Guid> GameIds)
    : IRequest<BatchGameNavCountsResult>;
```

- [ ] **Step 4: Implement the handler**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/Handlers/BatchGameNavCountsHandler.cs
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

public sealed class BatchGameNavCountsHandler : IRequestHandler<BatchGameNavCountsQuery, BatchGameNavCountsResult>
{
    private readonly MeepleDbContext _ctx;

    public BatchGameNavCountsHandler(MeepleDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<BatchGameNavCountsResult> Handle(BatchGameNavCountsQuery req, CancellationToken ct)
    {
        if (req.GameIds.Count > 100)
        {
            throw new ArgumentException("Batch size exceeds max 100 games", nameof(req));
        }

        var ids = req.GameIds.ToHashSet();

        // Single round-trip with grouped subqueries
        var counts = await _ctx.SharedGames
            .Where(g => ids.Contains(g.Id))
            .Select(g => new
            {
                GameId = g.Id,
                KbCount = _ctx.PdfDocuments.Count(p => p.GameId == g.Id && !p.IsDeleted),
                AgentCount = _ctx.Agents.Count(a => a.GameId == g.Id && a.OwnerUserId == req.UserId),
                ChatCount = _ctx.ChatSessions.Count(c => c.GameId == g.Id && c.UserId == req.UserId),
                SessionCount = _ctx.GameSessions.Count(s => s.GameId == g.Id && s.UserId == req.UserId),
            })
            .ToListAsync(ct);

        var dict = ids.ToDictionary(
            id => id,
            id =>
            {
                var c = counts.FirstOrDefault(x => x.GameId == id);
                return c == null
                    ? new GameNavCountsDto(0, 0, 0, 0)
                    : new GameNavCountsDto(c.KbCount, c.AgentCount, c.ChatCount, c.SessionCount);
            });

        return new BatchGameNavCountsResult(dict);
    }
}
```

- [ ] **Step 5: Create the endpoint**

```csharp
// apps/api/src/Api/Routing/Games/GamesNavCountsEndpoint.cs
using Api.BoundedContexts.GameManagement.Application.Queries;
using MediatR;

namespace Api.Routing.Games;

public static class GamesNavCountsEndpoint
{
    public static void MapBatchNavCounts(this RouteGroupBuilder group)
    {
        group.MapPost("/games/batch-nav-counts", async (
            BatchNavCountsRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!TryGetUserId(context, session, out var userId)) return Results.Unauthorized();

            var query = new BatchGameNavCountsQuery(userId, request.GameIds);
            var result = await mediator.Send(query, ct);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<BatchGameNavCountsResult>(200)
        .Produces(401)
        .WithTags("Games")
        .WithSummary("Batch fetch nav counts (KB/Agent/Chat/Session) for multiple games");
    }

    public sealed record BatchNavCountsRequest(IReadOnlyCollection<Guid> GameIds);
}
```

- [ ] **Step 6: Wire the endpoint in Program.cs**

Find the Games route group registration and add:

```csharp
gamesGroup.MapBatchNavCounts();
```

- [ ] **Step 7: Run all tests**

```bash
cd apps/api/src/Api && dotnet test --filter BatchGameNavCountsHandlerTests
```

Expected: PASS (3 tests)

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/BatchGameNavCountsQuery.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Application/Handlers/BatchGameNavCountsHandler.cs \
        apps/api/src/Api/Routing/Games/GamesNavCountsEndpoint.cs \
        apps/api/src/Api/Program.cs \
        tests/Api.Tests/BoundedContexts/GameManagement/BatchGameNavCountsHandlerTests.cs
git commit -m "feat(api): add batch nav counts endpoint for game grids"
```

---

### Task 6: Add frontend hook useBatchGameNavCounts

**Files:**
- Modify: `apps/web/src/lib/api/schemas/games.schemas.ts` (add response schema)
- Modify: `apps/web/src/lib/api/clients/games-client.ts` (add `getBatchNavCounts`)
- Create: `apps/web/src/hooks/queries/useBatchGameNavCounts.ts`
- Create: `apps/web/src/hooks/queries/useGameNavCounts.ts` (single-game wrapper)
- Create: `apps/web/src/hooks/queries/__tests__/useBatchGameNavCounts.test.tsx`

- [ ] **Step 1: Add Zod schema**

In `games.schemas.ts`:

```typescript
export const GameNavCountsSchema = z.object({
  kbCount: z.number().int().nonnegative(),
  agentCount: z.number().int().nonnegative(),
  chatCount: z.number().int().nonnegative(),
  sessionCount: z.number().int().nonnegative(),
});

export type GameNavCounts = z.infer<typeof GameNavCountsSchema>;

export const BatchGameNavCountsResponseSchema = z.object({
  counts: z.record(z.string().uuid(), GameNavCountsSchema),
});

export type BatchGameNavCountsResponse = z.infer<typeof BatchGameNavCountsResponseSchema>;
```

- [ ] **Step 2: Add API client method**

In `games-client.ts`:

```typescript
async getBatchNavCounts(gameIds: string[]): Promise<BatchGameNavCountsResponse> {
  const response = await this.http.post('/api/v1/games/batch-nav-counts', { gameIds });
  return BatchGameNavCountsResponseSchema.parse(response.data);
}
```

- [ ] **Step 3: Write the failing hook test**

```typescript
// apps/web/src/hooks/queries/__tests__/useBatchGameNavCounts.test.tsx
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { useBatchGameNavCounts } from '../useBatchGameNavCounts';

vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getBatchNavCounts: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';

describe('useBatchGameNavCounts', () => {
  let qc: QueryClient;
  beforeEach(() => {
    qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  });

  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );

  it('fetches counts for given game ids', async () => {
    const id1 = '11111111-1111-1111-1111-111111111111';
    const id2 = '22222222-2222-2222-2222-222222222222';
    (api.games.getBatchNavCounts as any).mockResolvedValue({
      counts: {
        [id1]: { kbCount: 3, agentCount: 1, chatCount: 0, sessionCount: 5 },
        [id2]: { kbCount: 0, agentCount: 0, chatCount: 0, sessionCount: 0 },
      },
    });

    const { result } = renderHook(() => useBatchGameNavCounts([id1, id2]), { wrapper });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.counts[id1].kbCount).toBe(3);
  });

  it('does not call API when gameIds is empty', async () => {
    renderHook(() => useBatchGameNavCounts([]), { wrapper });
    expect(api.games.getBatchNavCounts).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 4: Run test (verify failure)**

```bash
cd apps/web && pnpm test useBatchGameNavCounts
```

Expected: FAIL — module not found

- [ ] **Step 5: Implement hooks**

```typescript
// apps/web/src/hooks/queries/useBatchGameNavCounts.ts
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export function useBatchGameNavCounts(gameIds: string[], enabled = true) {
  return useQuery({
    queryKey: ['batch-game-nav-counts', ...gameIds.sort()],
    queryFn: () => api.games.getBatchNavCounts(gameIds),
    enabled: enabled && gameIds.length > 0,
    staleTime: 30 * 1000,
    gcTime: 5 * 60 * 1000,
  });
}
```

```typescript
// apps/web/src/hooks/queries/useGameNavCounts.ts
import { useBatchGameNavCounts } from './useBatchGameNavCounts';

export function useGameNavCounts(gameId: string | undefined, enabled = true) {
  const ids = gameId ? [gameId] : [];
  const query = useBatchGameNavCounts(ids, enabled);

  return {
    ...query,
    counts: gameId && query.data?.counts[gameId]
      ? query.data.counts[gameId]
      : { kbCount: 0, agentCount: 0, chatCount: 0, sessionCount: 0 },
  };
}
```

- [ ] **Step 6: Verify test passes**

```bash
cd apps/web && pnpm test useBatchGameNavCounts
```

Expected: PASS (2 tests)

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/lib/api/schemas/games.schemas.ts \
        apps/web/src/lib/api/clients/games-client.ts \
        apps/web/src/hooks/queries/useBatchGameNavCounts.ts \
        apps/web/src/hooks/queries/useGameNavCounts.ts \
        apps/web/src/hooks/queries/__tests__/useBatchGameNavCounts.test.tsx
git commit -m "feat(web): add useBatchGameNavCounts + useGameNavCounts hooks"
```

---

### Task 7: Add per-entity nav-counts hooks for non-game entities

**Files:**
- Create: `apps/web/src/hooks/queries/usePlayerNavCounts.ts`
- Create: `apps/web/src/hooks/queries/useSessionNavCounts.ts`
- Create: `apps/web/src/hooks/queries/useAgentNavCounts.ts`
- Create: `apps/web/src/hooks/queries/useKbNavCounts.ts` (mostly action-based, no query)
- Create: `apps/web/src/hooks/queries/useChatNavCounts.ts`
- Create: `apps/web/src/hooks/queries/useEventNavCounts.ts`

**Implementation notes:**
- `useAgentNavCounts(agentId)`: composes existing `api.agents.getStatus(id)` (for KB count) and `api.chatSessions.getByUserAndGame()` filtered to the agent. Returns `{ chatCount, kbCount }`.
- `useSessionNavCounts(sessionId)`: composes `useLiveSession(sessionId)` (for playerCount, notes presence) and `api.sessionTracking.getSnapshots(sessionId)` (for photoCount sum). Returns `{ playerCount, hasNotes, toolCount, photoCount }`.
- `useChatNavCounts(chatId, fallbackMessageCount?)`: returns `{ messageCount }` from `ChatSessionSummaryDto.messageCount`. If fallback is provided (already known from list view), uses it without an extra query.
- `useEventNavCounts(eventId, initial?)`: takes initial counts (already in GameNight DTO) to avoid extra queries. Returns `{ participantCount, gameCount }`.
- `useKbNavCounts(documentId)`: returns `{ chunkCount }` from `AdminKbDocument.chunkCount` if admin endpoint accessible, else `undefined` (slot disabled).
- `usePlayerNavCounts(playerId)`: wraps `api.playRecords.getPlayerStatistics(playerId)`. Returns `{ totalWins, totalSessions, favoriteCount: 0 }` (favorites disabled in v1).

For brevity, here is `usePlayerNavCounts.ts`. The other hooks follow the same pattern using existing endpoints identified in the API exploration.

```typescript
// apps/web/src/hooks/queries/usePlayerNavCounts.ts
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { PlayerNavCounts } from '@/components/ui/data-display/meeple-card/nav-items';

export function usePlayerNavCounts(playerId: string | undefined): {
  counts: PlayerNavCounts;
  isLoading: boolean;
} {
  const { data, isLoading } = useQuery({
    queryKey: ['player-stats', playerId],
    queryFn: () => api.playRecords.getPlayerStatistics(playerId!),
    enabled: !!playerId,
    staleTime: 60 * 1000,
  });

  return {
    counts: {
      totalWins: data?.totalWins ?? 0,
      totalSessions: data?.totalSessions ?? 0,
      favoriteCount: 0, // TODO: separate query when endpoint exists
    },
    isLoading,
  };
}
```

- [ ] **Step 1: Implement all 6 nav-counts hooks** (one commit per hook)

For each hook:
1. Use existing api method or new one
2. Return shape `{ counts: <EntityNavCounts>, isLoading }`
3. No tests required for this task — they will be exercised by adapter integration tests in Phase 2

- [ ] **Step 2: Commit each hook with**

```bash
git commit -m "feat(web): add use{Entity}NavCounts hook"
```

---

### Task 8: Update dev showcase page with navItems sections

**Files:**
- Modify: `apps/web/src/app/(public)/dev/meeple-card/page.tsx`

- [ ] **Step 1: Read current dev page**

```bash
cat apps/web/src/app/\(public\)/dev/meeple-card/page.tsx
```

- [ ] **Step 2: Add new section "NavFooter Showcase" demonstrating each entity's navItems**

After the existing "Variants" section in `MeepleCardDevPage`, add:

```tsx
import {
  buildGameNavItems,
  buildPlayerNavItems,
  buildSessionNavItems,
  buildAgentNavItems,
  buildKbNavItems,
  buildChatNavItems,
  buildEventNavItems,
  buildToolkitNavItems,
  buildToolNavItems,
} from '@/components/ui/data-display/meeple-card/nav-items';

// ... inside MeepleCardDevPage component, after Variants section ...

<Section title="NavFooter — Tutti gli stati" description="Esempi di navItems per ogni entity con count, plus, disabled.">
  <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
    {/* Game with all counts */}
    <div>
      <Label>game · counts pieni</Label>
      <MeepleCard
        entity="game"
        variant="grid"
        title="Catan"
        subtitle="Klaus Teuber · 1995"
        imageUrl={GAME_IMAGE}
        rating={4.2}
        ratingMax={5}
        metadata={[{ label: '3-4' }, { label: '60min' }]}
        navItems={buildGameNavItems(
          { kbCount: 3, agentCount: 1, chatCount: 5, sessionCount: 12 },
          {
            onKbClick: () => alert('KB!'),
            onAgentClick: () => alert('Agent!'),
            onChatClick: () => alert('Chat!'),
            onSessionClick: () => alert('Sessions!'),
          }
        )}
      />
    </div>

    {/* Game empty (plus indicators) */}
    <div>
      <Label>game · vuoto (plus)</Label>
      <MeepleCard
        entity="game"
        variant="grid"
        title="Azul"
        subtitle="2017"
        imageUrl="https://picsum.photos/seed/azul/400/300"
        rating={4.0}
        ratingMax={5}
        navItems={buildGameNavItems(
          { kbCount: 0, agentCount: 0, chatCount: 0, sessionCount: 0 },
          {
            onKbPlus: () => alert('Add KB!'),
            onAgentPlus: () => alert('Create agent!'),
            onChatPlus: () => alert('Start chat!'),
            onSessionPlus: () => alert('New session!'),
          }
        )}
      />
    </div>

    {/* Player */}
    <div>
      <Label>player</Label>
      <MeepleCard
        entity="player"
        variant="grid"
        title="Alice Rossi"
        subtitle="Giocatrice attiva"
        navItems={buildPlayerNavItems(
          { totalWins: 18, totalSessions: 42, favoriteCount: 7 },
          {
            onWinsClick: () => {},
            onSessionsClick: () => {},
            onFavoritesClick: () => {},
          }
        )}
      />
    </div>

    {/* Session */}
    <div>
      <Label>session</Label>
      <MeepleCard
        entity="session"
        variant="grid"
        title="Game Night #42"
        subtitle="Catan · 2h 15min"
        navItems={buildSessionNavItems(
          { playerCount: 4, hasNotes: true, toolCount: 6, photoCount: 12 },
          {
            onPlayersClick: () => {},
            onNotesClick: () => {},
            onToolsClick: () => {},
            onPhotosClick: () => {},
          }
        )}
      />
    </div>

    {/* Agent */}
    <div>
      <Label>agent</Label>
      <MeepleCard
        entity="agent"
        variant="grid"
        title="RulesBot Pro"
        subtitle="RAG · 12 fonti"
        navItems={buildAgentNavItems(
          { chatCount: 3, kbCount: 12 },
          {
            onChatClick: () => {},
            onKbClick: () => {},
            onConfigClick: () => {},
          }
        )}
      />
    </div>

    {/* KB doc */}
    <div>
      <Label>kb</Label>
      <MeepleCard
        entity="kb"
        variant="grid"
        title="Manuale Catan"
        subtitle="48 pagine"
        navItems={buildKbNavItems(
          { chunkCount: 124 },
          {
            onChunksClick: () => {},
            onReindexClick: () => {},
            onPreviewClick: () => {},
            onDownloadClick: () => {},
          }
        )}
      />
    </div>

    {/* Chat */}
    <div>
      <Label>chat</Label>
      <MeepleCard
        entity="chat"
        variant="grid"
        title="Chat Catan"
        subtitle="ieri"
        navItems={buildChatNavItems(
          { messageCount: 18 },
          {
            onMessagesClick: () => {},
            onAgentLinkClick: () => {},
            onArchiveClick: () => {},
          }
        )}
      />
    </div>

    {/* Event */}
    <div>
      <Label>event</Label>
      <MeepleCard
        entity="event"
        variant="grid"
        title="Serata Lupus"
        subtitle="Sabato 12 Apr"
        navItems={buildEventNavItems(
          { participantCount: 8, gameCount: 3 },
          {
            onParticipantsClick: () => {},
            onLocationClick: () => {},
            onGamesClick: () => {},
            onDateClick: () => {},
          }
        )}
      />
    </div>

    {/* Toolkit */}
    <div>
      <Label>toolkit</Label>
      <MeepleCard
        entity="toolkit"
        variant="grid"
        title="Catan Tools"
        subtitle="6 strumenti"
        navItems={buildToolkitNavItems(
          { toolCount: 6, deckCount: 2, phaseCount: 4, useCount: 18 },
          {
            onToolsClick: () => {},
            onDecksClick: () => {},
            onPhasesClick: () => {},
            onHistoryClick: () => {},
          }
        )}
      />
    </div>
  </div>
</Section>
```

- [ ] **Step 3: Run dev server and visually verify**

```bash
cd apps/web && pnpm dev
# Open http://localhost:3000/dev/meeple-card
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/\(public\)/dev/meeple-card/page.tsx
git commit -m "feat(dev): add navItems showcase to /dev/meeple-card page"
```

---

## Phase 2: Game Adapter Updates (10 adapters)

### Task 9: Update MeepleGameCatalogCard with navItems

**Files:**
- Modify: `apps/web/src/components/catalog/MeepleGameCatalogCard.tsx`
- Modify: `apps/web/src/__tests__/components/MeepleGameCatalogCard.test.tsx` (if exists)

- [ ] **Step 1: Add nav counts hook and drawer state**

In `MeepleGameCatalogCard.tsx`, after existing imports:

```tsx
import { buildGameNavItems } from '@/components/ui/data-display/meeple-card/nav-items';
import { useGameNavCounts } from '@/hooks/queries/useGameNavCounts';
```

Inside the component, after `inLibrary` derivation:

```tsx
// Fetch nav counts only when game is in user's library
const { counts: navCounts } = useGameNavCounts(game.id, inLibrary);

const navItems = useMemo(() => {
  if (!inLibrary) return undefined;
  return buildGameNavItems(navCounts, {
    onKbClick: () => setKbDrawerOpen(true),
    onAgentClick: () => setAgentDrawerOpen(true),
    onChatClick: () => setChatDrawerOpen(true),
    onSessionClick: () => setSessionDrawerOpen(true),
    onKbPlus: () => setKbDrawerOpen(true), // open in upload mode
    onAgentPlus: () => setAgentDrawerOpen(true),
    onChatPlus: () => setChatDrawerOpen(true),
    onSessionPlus: () => setSessionDrawerOpen(true),
  });
}, [inLibrary, navCounts]);
```

- [ ] **Step 2: Pass navItems to MeepleCard**

```tsx
<MeepleCard
  entity="game"
  variant={variant}
  title={game.title}
  subtitle={subtitle}
  imageUrl={game.imageUrl || undefined}
  rating={game.averageRating ?? 0}
  ratingMax={10}
  metadata={metadata}
  badge={badge}
  status={inLibrary ? 'owned' : undefined}  // NEW
  actions={actions}
  navItems={navItems}                        // NEW
  onClick={onClick ? () => onClick(game.id) : undefined}
  className={className}
  data-testid={`catalog-game-card-${game.id}`}
/>
```

- [ ] **Step 3: Run typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: PASS

- [ ] **Step 4: Run existing tests**

```bash
cd apps/web && pnpm test MeepleGameCatalogCard
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/catalog/MeepleGameCatalogCard.tsx
git commit -m "feat(catalog): wire navItems and drawers in MeepleGameCatalogCard"
```

---

### Task 10–17: Update remaining 9 game adapters (parallel-friendly)

Each task follows the same pattern as Task 9: add `useGameNavCounts`, derive `navItems` via `buildGameNavItems`, wire drawer state, pass to MeepleCard.

The 9 remaining game adapters:

| # | File | Drawer wiring needed? |
|---|------|----------------------|
| 10 | `apps/web/src/components/library/MeepleUserLibraryCard.tsx` | YES — drawers already declared but unused |
| 11 | `apps/web/src/components/library/MeepleLibraryGameCard.tsx` | YES |
| 12 | `apps/web/src/components/games/MeepleGameCard.tsx` | NO drawers — use router navigation |
| 13 | `apps/web/src/components/wishlist/MeepleWishlistCard.tsx` | NO — disable navItems (no library context) |
| 14 | `apps/web/src/components/dashboard/v2/RecentGames.tsx` | YES — use drawers |
| 15 | `apps/web/src/components/dashboard/RecentGamesRow.tsx` | YES |
| 16 | `apps/web/src/components/library/RecentLibraryCard.tsx` | YES |
| 17 | `apps/web/src/components/bgg/BggGameCard.tsx` | NO — disabled (not in library) |

For BGG components and Wishlist (when not in library), pass `navItems={undefined}` rather than building empty items — this hides the footer.

For non-library context (BGG search, catalog before adding), `inLibrary` is false → skip nav counts query → pass `undefined`.

- [ ] **Steps for each adapter (repeat for tasks 10–17):**

1. Add imports: `buildGameNavItems`, `useGameNavCounts`, drawer sheets if missing
2. Add drawer state (4 states)
3. Add `useGameNavCounts(gameId, /*enabled*/ inLibrary)`
4. Build `navItems` via `useMemo`
5. Pass `navItems` and `status` to MeepleCard
6. Render drawer sheets at component bottom (if not already present)
7. Run typecheck + existing tests
8. Commit

Each adapter commit message:
```
feat({domain}): wire navItems and drawers in {AdapterName}
```

---

## Phase 3: Player/Session/Agent/KB/Chat/Event Adapters (11 adapters)

### Task 18: Update MeeplePlayerCard with navItems

**Files:**
- Modify: `apps/web/src/components/players/MeeplePlayerCard.tsx`

- [ ] **Step 1: Add hooks and build navItems**

```tsx
import { buildPlayerNavItems } from '@/components/ui/data-display/meeple-card/nav-items';
import { usePlayerNavCounts } from '@/hooks/queries/usePlayerNavCounts';
import { useRouter } from 'next/navigation';
```

```tsx
const router = useRouter();
const { counts: playerCounts } = usePlayerNavCounts(player.id);

const navItems = useMemo(() =>
  buildPlayerNavItems(playerCounts, {
    onWinsClick: () => router.push(`/players/${player.id}/wins`),
    onSessionsClick: () => router.push(`/players/${player.id}/history`),
    onFavoritesClick: () => router.push(`/players/${player.id}/favorites`),
  }),
  [playerCounts, player.id, router]
);
```

- [ ] **Step 2: Pass to MeepleCard**

```tsx
<MeepleCard
  entity="player"
  variant={variant}
  title={player.displayName}
  subtitle={player.role}
  imageUrl={player.avatarUrl || undefined}
  navItems={navItems}
  onClick={onClick ? () => onClick(player.id) : undefined}
/>
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/players/MeeplePlayerCard.tsx
git commit -m "feat(players): wire navItems in MeeplePlayerCard"
```

---

### Task 19: Create MeepleAgentCard adapter (NEW)

**Files:**
- Create: `apps/web/src/components/agents/MeepleAgentCard.tsx`
- Create: `apps/web/src/components/agents/__tests__/MeepleAgentCard.test.tsx`

- [ ] **Step 1: Write the failing component test**

```tsx
// apps/web/src/components/agents/__tests__/MeepleAgentCard.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MeepleAgentCard } from '../MeepleAgentCard';

vi.mock('@/hooks/queries/useAgentNavCounts', () => ({
  useAgentNavCounts: () => ({
    counts: { chatCount: 5, kbCount: 12 },
    isLoading: false,
  }),
}));

describe('MeepleAgentCard', () => {
  const agent = {
    id: 'agent-1',
    name: 'RulesBot',
    description: 'Catan rules expert',
    iconUrl: null,
    gameId: 'game-1',
  };

  const wrapper = ({ children }: { children: React.ReactNode }) => {
    const qc = new QueryClient();
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  };

  it('renders agent name and description', () => {
    render(<MeepleAgentCard agent={agent} />, { wrapper });
    expect(screen.getByText('RulesBot')).toBeInTheDocument();
    expect(screen.getByText('Catan rules expert')).toBeInTheDocument();
  });

  it('renders nav footer with chat and KB counts', () => {
    render(<MeepleAgentCard agent={agent} />, { wrapper });
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test (verify failure)**

```bash
cd apps/web && pnpm test MeepleAgentCard
```

Expected: FAIL — module not found

- [ ] **Step 3: Implement the adapter**

```tsx
// apps/web/src/components/agents/MeepleAgentCard.tsx
'use client';

import { useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import { buildAgentNavItems } from '@/components/ui/data-display/meeple-card/nav-items';
import { useAgentNavCounts } from '@/hooks/queries/useAgentNavCounts';

export interface AgentDto {
  id: string;
  name: string;
  description: string | null;
  iconUrl: string | null;
  gameId: string;
}

export interface MeepleAgentCardProps {
  agent: AgentDto;
  variant?: MeepleCardVariant;
  onClick?: (agentId: string) => void;
  className?: string;
}

export function MeepleAgentCard({
  agent,
  variant = 'grid',
  onClick,
  className,
}: MeepleAgentCardProps) {
  const router = useRouter();
  const { counts } = useAgentNavCounts(agent.id);

  const navItems = useMemo(
    () =>
      buildAgentNavItems(counts, {
        onChatClick: () => router.push(`/chat?agentId=${agent.id}`),
        onKbClick: () => router.push(`/agents/${agent.id}/sources`),
        onConfigClick: () => router.push(`/agents/${agent.id}/settings`),
      }),
    [counts, agent.id, router]
  );

  return (
    <MeepleCard
      entity="agent"
      variant={variant}
      title={agent.name}
      subtitle={agent.description ?? undefined}
      imageUrl={agent.iconUrl ?? undefined}
      navItems={navItems}
      onClick={onClick ? () => onClick(agent.id) : undefined}
      className={className}
      data-testid={`agent-card-${agent.id}`}
    />
  );
}

export default MeepleAgentCard;
```

- [ ] **Step 4: Verify tests pass**

```bash
cd apps/web && pnpm test MeepleAgentCard
```

Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/agents/MeepleAgentCard.tsx \
        apps/web/src/components/agents/__tests__/MeepleAgentCard.test.tsx
git commit -m "feat(agents): create MeepleAgentCard adapter"
```

---

### Task 20: Create MeepleChatCard adapter (NEW)

**Files:**
- Create: `apps/web/src/components/chat-unified/MeepleChatCard.tsx`
- Create: `apps/web/src/components/chat-unified/__tests__/MeepleChatCard.test.tsx`

**Prerequisite:** Verify these helpers exist (search before implementing):
- `useArchiveChat` — likely in `apps/web/src/hooks/queries/useChatSessions.ts`. If absent, create it as a `useMutation` wrapping `api.chatSessions.archive(id)`. If the API archive endpoint doesn't exist either, **omit the archive button** (set `onArchiveClick: undefined` to disable that nav slot).
- `formatRelativeTime` — search `apps/web/src/lib/utils/`. If absent, use `format(new Date(date), 'PPp', { locale: it })` from `date-fns` directly.

Pattern identical to Task 19, but for chat sessions. Uses `buildChatNavItems` and `useChatNavCounts`.

```tsx
'use client';

import { useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { format, formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import { buildChatNavItems } from '@/components/ui/data-display/meeple-card/nav-items';
import { useChatNavCounts } from '@/hooks/queries/useChatNavCounts';

export interface ChatSummary {
  id: string;
  title: string;
  lastMessageAt: string;
  messageCount: number;
  agentId?: string;
}

export interface MeepleChatCardProps {
  chat: ChatSummary;
  variant?: MeepleCardVariant;
  onClick?: (chatId: string) => void;
  className?: string;
}

export function MeepleChatCard({ chat, variant = 'list', onClick, className }: MeepleChatCardProps) {
  const router = useRouter();
  const { counts } = useChatNavCounts(chat.id, chat.messageCount);

  const subtitle = formatDistanceToNow(new Date(chat.lastMessageAt), {
    addSuffix: true,
    locale: it,
  });

  const navItems = useMemo(
    () =>
      buildChatNavItems(counts, {
        onMessagesClick: () => router.push(`/chat/${chat.id}`),
        onAgentLinkClick: chat.agentId
          ? () => router.push(`/agents/${chat.agentId}`)
          : undefined,
        // Archive: omitted in v1 — wire when api.chatSessions.archive exists
        onArchiveClick: undefined,
      }),
    [counts, chat.id, chat.agentId, router]
  );

  return (
    <MeepleCard
      entity="chat"
      variant={variant}
      title={chat.title}
      subtitle={subtitle}
      navItems={navItems}
      onClick={onClick ? () => onClick(chat.id) : undefined}
      className={className}
      data-testid={`chat-card-${chat.id}`}
    />
  );
}

export default MeepleChatCard;
```

Test pattern identical to Task 19. Commit:
```bash
git commit -m "feat(chat): create MeepleChatCard adapter"
```

---

### Task 21: Create MeepleEventCard adapter (NEW)

**Files:**
- Create: `apps/web/src/components/game-night/MeepleEventCard.tsx`
- Create: `apps/web/src/components/game-night/__tests__/MeepleEventCard.test.tsx`

Pattern identical. Uses `buildEventNavItems` and `useEventNavCounts`. The event entity wraps GameNight.

```tsx
'use client';

import { useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import { buildEventNavItems } from '@/components/ui/data-display/meeple-card/nav-items';
import { useEventNavCounts } from '@/hooks/queries/useEventNavCounts';
import { format } from 'date-fns';
import { it } from 'date-fns/locale';

export interface GameNightSummary {
  id: string;
  title: string;
  scheduledAt: string;
  location: string | null;
  participantCount: number;
  gameCount: number;
}

export interface MeepleEventCardProps {
  event: GameNightSummary;
  variant?: MeepleCardVariant;
  onClick?: (eventId: string) => void;
  className?: string;
}

export function MeepleEventCard({ event, variant = 'grid', onClick, className }: MeepleEventCardProps) {
  const router = useRouter();
  const { counts } = useEventNavCounts(event.id, {
    participantCount: event.participantCount,
    gameCount: event.gameCount,
  });

  const navItems = useMemo(
    () =>
      buildEventNavItems(counts, {
        onParticipantsClick: () => router.push(`/game-nights/${event.id}/participants`),
        onLocationClick: event.location
          ? () => window.open(`https://maps.google.com/?q=${encodeURIComponent(event.location!)}`)
          : undefined,
        onGamesClick: () => router.push(`/game-nights/${event.id}/games`),
        onDateClick: () => router.push(`/calendar?date=${event.scheduledAt}`),
      }),
    [counts, event, router]
  );

  return (
    <MeepleCard
      entity="event"
      variant={variant}
      title={event.title}
      subtitle={format(new Date(event.scheduledAt), 'EEEE d MMM', { locale: it })}
      navItems={navItems}
      onClick={onClick ? () => onClick(event.id) : undefined}
      className={className}
      data-testid={`event-card-${event.id}`}
    />
  );
}

export default MeepleEventCard;
```

Commit:
```bash
git commit -m "feat(game-night): create MeepleEventCard adapter"
```

---

### Tasks 22–28: Update remaining 7 entity adapters

Same pattern applied to:

| # | File | Builder | Hook |
|---|------|---------|------|
| 22 | `apps/web/src/components/session/MeepleSessionCard.tsx` | `buildSessionNavItems` | `useSessionNavCounts` |
| 23 | `apps/web/src/components/session/MeepleResumeSessionCard.tsx` | `buildSessionNavItems` | `useSessionNavCounts` |
| 24 | `apps/web/src/components/session/MeepleParticipantCard.tsx` | `buildPlayerNavItems` | `usePlayerNavCounts` |
| 25 | `apps/web/src/components/library/private-game-detail/MeeplePausedSessionCard.tsx` | `buildSessionNavItems` | `useSessionNavCounts` |
| 26 | `apps/web/src/components/documents/MeepleKbCard.tsx` | `buildKbNavItems` | reindex action mutation |
| 27 | `apps/web/src/components/game-detail/MeeplePdfReferenceCard.tsx` | `buildKbNavItems` | reindex action mutation |
| 28 | `apps/web/src/components/admin/knowledge-base/vector-game-card.tsx` | `buildKbNavItems` (admin) | reindex action mutation |

For each adapter, follow Task 9's 5-step pattern. Commit message:
```
feat({domain}): wire navItems in {AdapterName}
```

---

### Task 29: Update remaining shared/contributor/playlist/wishlist adapters

| # | File | Notes |
|---|------|-------|
| 29a | `apps/web/src/components/shared-games/MeepleContributorCard.tsx` | Uses player navItems |
| 29b | `apps/web/src/components/playlists/MeeplePlaylistCard.tsx` | Uses game navItems for first game |
| 29c | `apps/web/src/components/toolbox/ToolboxKitCard.tsx` | Uses toolkit navItems |
| 29d | `apps/web/src/components/contributions/ShareRequestCard.tsx` | Uses game navItems |
| 29e | `apps/web/src/components/game-night/planning/MeepleAISuggestionCard.tsx` | Uses agent navItems |
| 29f | `apps/web/src/components/game-night/planning/MeepleDealtGameCard.tsx` | Uses game navItems (limited) |
| 29g | `apps/web/src/components/game-night/planning/MeepleGameNightCard.tsx` | Uses event navItems |
| 29h | `apps/web/src/components/bgg/BggPreviewCard.tsx` | NO navItems (BGG context) |

- [ ] **Steps for each:** Follow Task 9 pattern. Commit individually.

---

## Phase 4: Direct Consumer Refactor (24 files)

Each direct consumer must be replaced with the appropriate adapter.

### Task 30: Refactor HomeFeed

**Files:**
- Modify: `apps/web/src/components/features/home/HomeFeed.tsx`

- [ ] **Step 1: Replace MeepleCard sessions with MeepleSessionCard**

In `HomeFeed.tsx`, replace:

```tsx
{sessions.map(session => (
  <MeepleCard key={session.id} ... />
))}
```

with:

```tsx
{sessions.map(session => (
  <MeepleSessionCard
    key={session.id}
    session={session}
    variant="list"
    onClick={(id) => openDetail(id, 'session')}
  />
))}
```

- [ ] **Step 2: Replace recent games with MeepleGameCard adapter**

```tsx
{games.map(game => (
  <MeepleLibraryGameCard
    key={game.id}
    game={game}
    variant="grid"
    onClick={(id) => router.push(`/library/games/${id}`)}
  />
))}
```

- [ ] **Step 3: Replace game nights with MeepleEventCard**

```tsx
{nights.map(night => (
  <MeepleEventCard
    key={night.id}
    event={night}
    variant="list"
    onClick={(id) => router.push(`/game-nights/${id}`)}
  />
))}
```

- [ ] **Step 4: Replace chats with MeepleChatCard**

```tsx
{chats.map(chat => (
  <MeepleChatCard
    key={chat.id}
    chat={chat}
    variant="list"
    onClick={(id) => router.push(`/chat/${id}`)}
  />
))}
```

- [ ] **Step 5: Remove direct `MeepleCard` import**

Replace:
```tsx
import { MeepleCard, entityHsl } from '@/components/ui/data-display/meeple-card';
```

with:
```tsx
import { entityHsl } from '@/components/ui/data-display/meeple-card';
import { MeepleSessionCard } from '@/components/session/MeepleSessionCard';
import { MeepleLibraryGameCard } from '@/components/library/MeepleLibraryGameCard';
import { MeepleEventCard } from '@/components/game-night/MeepleEventCard';
import { MeepleChatCard } from '@/components/chat-unified/MeepleChatCard';
```

- [ ] **Step 6: Run typecheck and visual test**

```bash
cd apps/web && pnpm typecheck && pnpm dev
# Visit /home and verify cards render with nav-footer
```

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/features/home/HomeFeed.tsx
git commit -m "refactor(home): use entity-specific adapters in HomeFeed"
```

---

### Tasks 31–53: Refactor remaining 23 direct consumers

Each task follows Task 30's pattern: replace `<MeepleCard>` with the appropriate `<Meeple{Entity}Card>` adapter, remove direct MeepleCard import, run typecheck, commit.

| # | File | Replaces with |
|---|------|---------------|
| 31 | `apps/web/src/components/features/library/LibraryPanel.tsx` | MeepleGameCatalogCard / MeepleUserLibraryCard |
| 32 | `apps/web/src/components/features/play/PlayPanel.tsx` | MeepleSessionCard / MeepleResumeSessionCard |
| 33 | `apps/web/src/components/features/chat/ChatPanel.tsx` | MeepleChatCard |
| 34 | `apps/web/src/components/chat-unified/NewChatView.tsx` | MeepleGameCard / MeepleAgentCard |
| 35 | `apps/web/src/components/play-records/PlayHistory.tsx` | MeepleSessionCard |
| 36 | `apps/web/src/components/library/PublicLibraryPage.tsx` | MeepleGameCatalogCard |
| 37 | `apps/web/src/components/library/PersonalLibraryPage.tsx` | MeepleUserLibraryCard |
| 38 | `apps/web/src/components/library/LibraryGameGrid.tsx` | MeepleLibraryGameCard |
| 39 | `apps/web/src/components/library/RecentLibraryCard.tsx` | MeepleLibraryGameCard (already adapter? — verify) |
| 40 | `apps/web/src/components/library/KbStatusBadge.tsx` | Internal — verify usage |
| 41 | `apps/web/src/components/library/DowngradeTierModal.tsx` | MeepleLibraryGameCard |
| 42 | `apps/web/src/components/games/BggSearchPanel.tsx` | BggGameCard (adapter) |
| 43 | `apps/web/src/components/dashboard/v2/YourAgents.tsx` | MeepleAgentCard |
| 44 | `apps/web/src/components/dashboard/RecentGamesRow.tsx` | MeepleLibraryGameCard |
| 45 | `apps/web/src/components/dashboard/v2/RecentGames.tsx` | MeepleLibraryGameCard |
| 46 | `apps/web/src/components/collection/CollectionGameGrid.tsx` | MeepleLibraryGameCard |
| 47 | `apps/web/src/components/admin/shared-games/game-catalog-grid.tsx` | MeepleGameCatalogCard (admin variant) |
| 48 | `apps/web/src/app/(public)/shared-games/[id]/page.tsx` | MeepleLibraryGameCard (hero variant) |
| 49 | `apps/web/src/app/(public)/library/shared/[token]/page.tsx` | MeepleLibraryGameCard |
| 50 | `apps/web/src/app/(authenticated)/sessions/_content.tsx` | MeepleSessionCard / MeepleResumeSessionCard |
| 51 | `apps/web/src/app/(chat)/chat/page.tsx` | MeepleChatCard |
| 52 | `apps/web/src/app/(authenticated)/library/private/PrivateGamesClient.tsx` | MeepleLibraryGameCard (private variant) |
| 53 | `apps/web/src/app/(authenticated)/players/[id]/page.tsx` | MeeplePlayerCard (hero) |
| 54 | `apps/web/src/app/(authenticated)/knowledge-base/[id]/page.tsx` | MeepleKbCard (hero) |
| 55 | `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx` | MeepleLibraryGameCard (hero) |
| 56 | `apps/web/src/app/(authenticated)/agents/page.tsx` | MeepleAgentCard |
| 57 | `apps/web/src/app/(authenticated)/library/library-mobile.tsx` | MeepleUserLibraryCard |
| 58 | `apps/web/src/app/admin/(dashboard)/shared-games/import/steps/Step2MetadataReview.tsx` | MeepleGameCatalogCard |
| 59 | `apps/web/src/app/admin/(dashboard)/shared-games/import/steps/Step3PreviewConfirm.tsx` | MeepleGameCatalogCard |
| 59a | `apps/web/src/components/library/DowngradeTierModal.tsx` | MeepleLibraryGameCard (preview list) |
| 59b | `apps/web/src/components/games/BggSearchPanel.tsx` | BggGameCard adapter |
| 59c | `apps/web/src/components/chat-unified/AgentCreationWizard.tsx` | MeepleAgentCard preview |
| 59d | `apps/web/src/components/ui/data-display/entity-list-view/entity-list-view.tsx` | Use entity-specific adapters via type discriminator |
| 59e | `apps/web/src/hooks/useMeepleCardActions.ts` | KEEP — pure helper hook, no JSX. JSDoc example only. |

- [ ] **For each:** Apply Task 30 pattern. One commit per file.

**Excluded from refactor (false positives in inventory):**
- `apps/web/src/components/library/KbStatusBadge.tsx` — only mentions MeepleCard in JSDoc comment, no actual usage
- `apps/web/src/components/library/RecentLibraryCard.tsx` — already an adapter (verify and skip if so)
- Test files (`__tests__/*`) — handled in Phase 5
- Storybook/dev pages — handled in Phase 5
- README markdown files — handled in Phase 6

Final count: **24 direct consumers + 5 additional consumers identified during self-review = 29 files refactored in Phase 4** (Tasks 30–59e).

---

## Phase 5: Visualization & Test Updates

### Task 60: Update meeple-card.story.tsx (Storybook)

**Files:**
- Modify: `apps/web/src/components/showcase/stories/meeple-card.story.tsx`

- [ ] **Step 1: Add navItems showcase scenes**

Mirror the structure from Task 8 (dev page) but in storybook story format. Add a story for each entity showing nav-footer in:
- Empty state (all plus indicators)
- Partial state (some counts)
- Full state (all counts)
- Disabled state (some handlers missing)

- [ ] **Step 2: Commit**

```bash
git commit -m "feat(stories): showcase navItems in meeple-card story"
```

---

### Task 61: Update EntityCardsScene admin UI library

**Files:**
- Modify: `apps/web/src/components/admin/ui-library/scenes/EntityCardsScene.tsx`
- Modify: `apps/web/src/components/admin/ui-library/component-map.ts`

- [ ] **Step 1: Register new adapters in component-map**

```typescript
// component-map.ts — add entries for new adapters
{
  id: 'meeple-agent-card',
  name: 'MeepleAgentCard',
  category: 'cards',
  description: 'Agent entity adapter with navItems',
  component: MeepleAgentCard,
},
{
  id: 'meeple-chat-card',
  name: 'MeepleChatCard',
  category: 'cards',
  description: 'Chat entity adapter with navItems',
  component: MeepleChatCard,
},
{
  id: 'meeple-event-card',
  name: 'MeepleEventCard',
  category: 'cards',
  description: 'Event/GameNight entity adapter with navItems',
  component: MeepleEventCard,
},
```

- [ ] **Step 2: Update EntityCardsScene to demonstrate navItems**

In the scene rendering, ensure each card example passes `navItems` via the appropriate builder. This is the same pattern as Task 8.

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(admin-ui-library): register new adapters and showcase navItems"
```

---

### Task 62: Update MeepleCard.test.tsx

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCard.test.tsx`

- [ ] **Step 1: Add navItems rendering test**

```tsx
import { buildGameNavItems } from '../nav-items';

describe('MeepleCard navItems', () => {
  it('renders nav-footer when navItems prop is provided', () => {
    const items = buildGameNavItems(
      { kbCount: 3, agentCount: 0, chatCount: 5, sessionCount: 0 },
      {
        onKbClick: vi.fn(),
        onAgentClick: vi.fn(),
        onChatClick: vi.fn(),
        onSessionClick: vi.fn(),
      }
    );
    render(<MeepleCard entity="game" title="Test" navItems={items} />);
    expect(screen.getByText('KB')).toBeInTheDocument();
    expect(screen.getByText('Agent')).toBeInTheDocument();
    expect(screen.getByText('Chat')).toBeInTheDocument();
    expect(screen.getByText('Sessioni')).toBeInTheDocument();
  });

  it('shows count badges only for items with count > 0', () => {
    const items = buildGameNavItems(
      { kbCount: 3, agentCount: 0, chatCount: 5, sessionCount: 0 },
      { onKbClick: vi.fn(), onChatClick: vi.fn() }
    );
    render(<MeepleCard entity="game" title="Test" navItems={items} />);
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('shows plus indicator for items with count 0', () => {
    const items = buildGameNavItems(
      { kbCount: 0, agentCount: 0, chatCount: 0, sessionCount: 0 },
      { onKbPlus: vi.fn() }
    );
    render(<MeepleCard entity="game" title="Test" navItems={items} />);
    expect(screen.getAllByText('+').length).toBeGreaterThanOrEqual(1);
  });
});
```

- [ ] **Step 2: Run tests**

```bash
cd apps/web && pnpm test MeepleCard
```

Expected: PASS

- [ ] **Step 3: Commit**

```bash
git commit -m "test(meeple-card): add navItems rendering tests"
```

---

### Task 63: Update accessibility test (epic-4068)

**Files:**
- Modify: `apps/web/src/__tests__/a11y/epic-4068-accessibility.test.tsx`

- [ ] **Step 1: Add nav-footer button accessibility tests**

```tsx
it('NavFooter buttons have accessible labels', async () => {
  const items = buildGameNavItems(
    { kbCount: 3, agentCount: 0, chatCount: 5, sessionCount: 0 },
    { onKbClick: vi.fn(), onChatClick: vi.fn() }
  );
  const { container } = render(<MeepleCard entity="game" title="Catan" navItems={items} />);
  // axe scan
  const results = await axe(container);
  expect(results).toHaveNoViolations();
  // Verify button labels
  expect(screen.getByTitle('KB')).toHaveAttribute('aria-label', expect.any(String));
});
```

- [ ] **Step 2: Commit**

```bash
git commit -m "test(a11y): add navItems accessibility coverage"
```

---

### Task 64: Update CollectionDashboard test

**Files:**
- Modify: `apps/web/src/__tests__/components/CollectionDashboard.test.tsx`

- [ ] **Step 1: Update mocks to expect adapter usage**

Replace any `<MeepleCard>` mock expectations with `<MeepleLibraryGameCard>`.

- [ ] **Step 2: Commit**

```bash
git commit -m "test(collection): update mocks to use MeepleLibraryGameCard"
```

---

### Task 65: Update TagStrip integration test

**Files:**
- Modify: `apps/web/src/components/ui/tags/__tests__/TagStrip.integration.test.tsx`

- [ ] **Step 1: Verify the test still uses MeepleCard correctly**

If it tests TagStrip-on-MeepleCard interaction, ensure it still works with the new types. No changes likely needed unless icon types broke.

- [ ] **Step 2: Run test**

```bash
cd apps/web && pnpm test TagStrip.integration
```

Fix any breakage. Commit if changes needed.

---

## Phase 6: Legacy Cleanup

### Task 66: Delete legacy documentation files

**Files:**
- Delete: `docs/frontend/components/meeple-card.md`
- Delete: `docs/frontend/components/meeple-card-migration.md`
- Delete: `docs/superpowers/plans/archived/2026-04-03-meeplecard-adapter-migration.md`
- Delete: `docs/superpowers/specs/2026-04-03-meeplecard-adapter-migration-design.md`
- Delete: `apps/web/MIGRATION_GAMECARD_TO_MEEPLECARD.md` (if exists)
- Delete: `apps/web/src/components/ui/data-display/MEEPLE_CARD_USAGE.md`

- [ ] **Step 1: Verify each file exists, then delete**

```bash
cd D:/Repositories/meepleai-monorepo-backend
for f in \
  docs/frontend/components/meeple-card.md \
  docs/frontend/components/meeple-card-migration.md \
  docs/superpowers/plans/archived/2026-04-03-meeplecard-adapter-migration.md \
  docs/superpowers/specs/2026-04-03-meeplecard-adapter-migration-design.md \
  apps/web/MIGRATION_GAMECARD_TO_MEEPLECARD.md \
  apps/web/src/components/ui/data-display/MEEPLE_CARD_USAGE.md; do
  if [ -f "$f" ]; then
    git rm "$f"
    echo "Deleted: $f"
  else
    echo "Not present: $f"
  fi
done
```

- [ ] **Step 2: Update README.md to remove links to deleted files**

In `apps/web/src/components/ui/data-display/README.md`, search for and remove references to `MEEPLE_CARD_USAGE.md`, `meeple-card-migration.md`, etc.

- [ ] **Step 3: Search for orphaned references**

```bash
grep -rn "MEEPLE_CARD_USAGE\|meeple-card-migration\|MIGRATION_GAMECARD" \
  D:/Repositories/meepleai-monorepo-backend/apps \
  D:/Repositories/meepleai-monorepo-backend/docs
```

Expected: no results. Fix any remaining references.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: delete legacy MeepleCard documentation"
```

---

### Task 67: Verify no direct MeepleCard usage remains in consumer files

**Files:** None — verification only

- [ ] **Step 1: Grep for direct usage in non-adapter files**

```bash
grep -rn "import.*MeepleCard.*from.*meeple-card" \
  D:/Repositories/meepleai-monorepo-backend/apps/web/src/app \
  D:/Repositories/meepleai-monorepo-backend/apps/web/src/components/features
```

Expected: minimal results — only feature panels that legitimately use it via adapters.

```bash
grep -rln "<MeepleCard\b" \
  D:/Repositories/meepleai-monorepo-backend/apps/web/src/app \
  D:/Repositories/meepleai-monorepo-backend/apps/web/src/components/features \
  D:/Repositories/meepleai-monorepo-backend/apps/web/src/components/dashboard \
  D:/Repositories/meepleai-monorepo-backend/apps/web/src/components/library \
  D:/Repositories/meepleai-monorepo-backend/apps/web/src/components/play-records \
  D:/Repositories/meepleai-monorepo-backend/apps/web/src/components/collection
```

Expected: NO results — all consumers use adapters.

The only legitimate direct `<MeepleCard>` usage is in:
- `apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx` itself
- All `Meeple*Card.tsx` adapters
- `apps/web/src/app/(public)/dev/meeple-card/page.tsx` (showcase)
- `apps/web/src/components/showcase/stories/meeple-card.story.tsx` (storybook)
- `apps/web/src/components/admin/ui-library/scenes/EntityCardsScene.tsx` (admin showcase)

- [ ] **Step 2: If any unexpected results, refactor them and commit**

---

## Phase 7: Final Verification & Build

### Task 68: Run full test suite

**Files:** None

- [ ] **Step 1: Run all frontend tests**

```bash
cd apps/web && pnpm test
```

Expected: ALL PASS

- [ ] **Step 2: Run typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: NO ERRORS

- [ ] **Step 3: Run lint**

```bash
cd apps/web && pnpm lint
```

Expected: NO ERRORS

- [ ] **Step 4: Run backend tests**

```bash
cd apps/api/src/Api && dotnet test --filter "Category=Unit&BoundedContext=GameManagement"
```

Expected: ALL PASS

---

### Task 69: Build production bundle and Docker image

**Files:** None

- [ ] **Step 1: Build Next.js production bundle**

```bash
cd apps/web && pnpm build
```

Expected: build success, no errors

- [ ] **Step 2: Build Docker image with --no-cache**

```bash
cd D:/Repositories/meepleai-monorepo-backend
docker build --no-cache -t meepleai-web-meeplecard-complete -f apps/web/Dockerfile apps/web/
```

Expected: build success

- [ ] **Step 3: Run integration mode locally**

```bash
cd D:/Repositories/meepleai-monorepo-backend/infra
make tunnel
make integration
```

- [ ] **Step 4: Visual verification on http://localhost:3000**

Open the app and verify:
- /home shows cards with nav-footer (KB/Agent/Chat/Sessions)
- /dev/meeple-card shows full showcase with all entity types and navItems
- /library/personal-library shows game cards with counts and drawer triggers
- /agents shows agent cards with nav-footer
- /chat shows chat cards with nav-footer
- /sessions shows session cards with nav-footer
- Mockup pixel-match: open `admin-mockups/meeple-card-real-app-render.html` side-by-side and verify visual parity

---

### Task 70: Create PR

**Files:** None

- [ ] **Step 1: Push branch**

```bash
git push -u origin feature/meeplecard-consumers-completion
```

- [ ] **Step 2: Create PR targeting main-dev**

```bash
gh pr create --base main-dev --title "feat(meeple-card): complete consumer migration with navItems" --body "$(cat <<'EOF'
## Summary
Complete the MeepleCard rewrite by wiring `navItems`, `actions`, and `status` props through all 66 consumers. Adds 3 new entity adapters (MeepleAgentCard, MeepleChatCard, MeepleEventCard), introduces 9 nav-item builder helpers + per-entity hooks, and adds a backend batch endpoint to avoid N+1 on game grids.

## Changes
- **NEW:** `nav-items/` directory with 9 builder helpers and Lucide icon registry
- **NEW:** `useBatchGameNavCounts` + `useGameNavCounts` + 6 per-entity hooks
- **NEW:** `POST /api/v1/games/batch-nav-counts` endpoint
- **NEW:** 3 entity adapters (MeepleAgentCard, MeepleChatCard, MeepleEventCard)
- **MODIFIED:** All 21 existing adapters to wire navItems and drawers
- **MODIFIED:** All 24 direct consumers refactored to use adapters
- **MODIFIED:** Dev showcase, storybook, admin UI library to demonstrate navItems
- **DELETED:** 6 legacy documentation files

## Test plan
- [x] All frontend tests pass (`pnpm test`)
- [x] Backend tests pass (`dotnet test`)
- [x] Typecheck (`pnpm typecheck`) clean
- [x] Lint (`pnpm lint`) clean
- [x] Production build succeeds
- [x] Visual verification on /home, /library, /agents, /chat, /sessions, /dev/meeple-card
- [x] Mockup pixel-parity verified vs admin-mockups/*.html

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Self-Review Checklist (Run Before Marking Plan Complete)

### Spec Coverage

- [x] Section 6.7 NavFooter — covered by Task 1, 2, 3, 4 (nav-items + builders)
- [x] Section 11 Public API — covered by Task 1 (NavFooterItem update)
- [x] Mockup `meeple-card-real-app-render.html` — verified by Task 8 (dev showcase)
- [x] Mockup `meeple-card-nav-buttons-mockup.html` — verified by Task 8 + 60 (storybook)
- [x] All 9 entity types — covered by Task 4 (8 builders) + Task 3 (game)
- [x] All 5 variants — variants don't change, navItems is universal
- [x] No backwards-compat — covered by Task 66 (legacy cleanup) + Task 67 (verification grep)

### Type Consistency

- `NavFooterItem.icon: ReactNode` — Task 1 defines, all builders in Tasks 3-4 use, NavFooter renders
- `GameNavCounts { kbCount, agentCount, chatCount, sessionCount }` — Task 3 defines, Task 5 backend matches, Task 6 frontend hook returns
- `useGameNavCounts(gameId, enabled).counts` shape consistent across all consumers

### Placeholder Scan

- No "TBD", "TODO" in implementation steps (only in NOT IMPLEMENTED slots which are explicitly disabled with `disabled: true`)
- All code blocks contain real implementation
- All file paths are absolute and verified

### Scope Check

This plan covers ONE coherent feature: completing the MeepleCard navItems integration. It does NOT bundle:
- Backend RAG changes
- Authentication updates
- New entity types beyond the 9 already defined
- Mobile layout changes (already complete)

### Ambiguity Check

- "Disabled state" defined: `disabled: true` + no `onClick` handler → button non-interactive with opacity 0.45
- "Plus indicator" defined: shown when `count === 0`, click triggers `onPlusClick`
- "Drawer wiring" defined: existing drawers (already declared in adapters) get hooked to navItem onClick handlers
- Action vs nav distinction: actions are top-right hover-reveal (Cover overlay), navItems are bottom of card (NavFooter)

---

## Execution Notes

This plan has **75 tasks** across 7 phases. Phases 2 (game adapters) and 4 (consumer refactor) are highly parallelizable — multiple subagents can work on independent files concurrently.

Task counts per phase:
- Phase 0: 1 task (branch setup)
- Phase 1: 8 tasks (foundation: types, builders, hooks, backend endpoint, dev showcase)
- Phase 2: 9 tasks (Tasks 9–17 — game adapters)
- Phase 3: 12 tasks (Tasks 18–29 — entity adapters + 8 in 29a-h)
- Phase 4: 30 tasks (Tasks 30–59e — direct consumer refactor)
- Phase 5: 6 tasks (Tasks 60–65 — visualization & test updates)
- Phase 6: 2 tasks (Tasks 66–67 — legacy cleanup + verification)
- Phase 7: 3 tasks (Tasks 68–70 — verification + build + PR)

Recommended execution: **subagent-driven-development** with:
- **Phase 0+1 (Foundation):** Sequential, single agent — establishes the contract types and shared infrastructure all later phases depend on.
- **Phase 2 (Game adapters):** Parallel — 1 agent per adapter, 9 agents in parallel batch.
- **Phase 3 (Other entities):** Mixed — Tasks 19, 20, 21 (NEW adapters) sequential because they introduce new files used by Phase 4. Tasks 22–29 parallel.
- **Phase 4 (Consumer refactor):** Parallel batches of 5 — each agent gets 5 files. ~6 batches.
- **Phase 5 (Visualization):** Sequential, depends on Phase 4 completion.
- **Phase 6 (Cleanup):** Sequential.
- **Phase 7 (Verification + build + PR):** Sequential, single agent.

**Critical dependency:** Phase 1 Task 1 (NavFooterItem.icon: ReactNode change) MUST complete before Tasks 3–4 (builders use ReactNode icons). All other Phase 1 tasks can proceed in parallel after Task 1.

**Risk:** The git branch will go through several hundred commits. To keep the PR review-able, consider squash-merging by phase boundary when opening the final PR.
