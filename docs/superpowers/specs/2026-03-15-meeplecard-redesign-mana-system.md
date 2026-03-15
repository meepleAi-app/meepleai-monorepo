# MeepleCard Redesign — Mana System

**Date**: 2026-03-15
**Status**: Draft
**Scope**: Frontend card system redesign with TCG-inspired visual language

## Overview

Redesign the MeepleCard component system to use a unified "Mana" visual language inspired by trading card games (Magic: The Gathering). Each entity type gets a distinctive mana symbol used consistently across the entire UI: card badges, link footers, deck stacks, drawer tabs, and navigation.

The visual style is **hybrid app/TCG**: the UI stays modern and app-like, but the mana symbol system pervades all entity interactions as the unifying design language.

## Mana System — 16 Entity Types

### Tier 1: Core Entities

| Entity | Symbol | Color (HSL) | Meaning |
|--------|--------|-------------|---------|
| Game | 🎲 → SVG | `25 95% 45%` Orange | Strategy and chance |
| Session | ⏳ → SVG | `240 60% 55%` Indigo | Time and play |
| Player | ♟ → SVG | `262 83% 58%` Purple | The player piece |
| Event | ✦ → SVG | `350 89% 60%` Rose | Special moments |

### Tier 2: Social & Organization

| Entity | Symbol | Color (HSL) | Meaning |
|--------|--------|-------------|---------|
| Collection | 📦 → SVG | `20 70% 42%` Copper | Your deck of games |
| Group | 👥 → SVG | `280 50% 48%` Warm Violet | Your fixed party |
| Location | 📍 → SVG | `200 55% 45%` Slate Cyan | Where you play |
| Expansion | 🃏 → SVG | `290 65% 50%` Magenta | Game expansions |

### Tier 3: AI & Knowledge

| Entity | Symbol | Color (HSL) | Meaning |
|--------|--------|-------------|---------|
| Agent | ⚡ → SVG | `38 92% 50%` Amber | Active AI intelligence |
| Knowledge (`kb`) | 📜 → SVG | `174 60% 40%` Teal | Rules and lore |
| Chat (`chatSession`) | 💬 → SVG | `220 80% 55%` Blue | AI conversation |
| Note | 📝 → SVG | `40 30% 42%` Warm Gray | Personal annotations |

### Tier 4: Tools & Meta

| Entity | Symbol | Color (HSL) | Meaning |
|--------|--------|-------------|---------|
| Toolkit | ⚙ → SVG | `142 70% 45%` Green | Tool set |
| Tool | 🔧 → SVG | `195 80% 50%` Sky | Single tool |
| Achievement | 🏆 → SVG | `45 90% 48%` Gold | Earned reward |
| Custom | ✧ → SVG | `220 15% 45%` Silver | Wildcard type (supports `customColor` prop override) |

### Mana Symbol Visual Style

Each mana symbol is rendered as an SVG with:

- **Circular shape** with radial gradient (lighter at 35% 35%, darker at edges)
- **Outer ring**: 2px border at entity color, 35% opacity
- **Inner highlight**: 1px border at `rgba(255,255,255,0.08)` for embossed feel
- **Drop shadow**: `0 4px 16px` at entity color, 35% opacity
- **Icon**: centered SVG glyph (not emoji) in white with subtle drop shadow

Sizes:
- **Full**: 64px (mana grid, drawer header)
- **Medium**: 28px (link footer, entity badge)
- **Mini**: 18-20px (deck stack, inline references, tab indicators)

## Card Anatomy — Front

The front of every MeepleCard has these zones, top to bottom:

```
┌──────────────────────────────┐
│ [mana badge]     [status chip]│  ← Entity badge (top-left), Status (top-right)
│                  [act1][act2] │  ← 1-2 primary actions (top-right, subtle)
│▌                              │  ← Entity accent bar (left edge, 4px)
│▌    ┌──────────────────┐      │
│▌    │                  │      │  ← Cover image (55% height)
│▌    │   COVER IMAGE    │      │
│▌    │                  │      │
│▌    └──────────────────┘      │
│▌ ● Tag strip (left edge)      │  ← Colored dots for categories
│▌                              │
│  Title                        │  ← Quicksand 700
│  Subtitle                     │  ← Nunito 400, muted
│  ★★★★☆ 8.4                   │  ← Rating (if applicable)
│  [tag] [tag] [tag]            │  ← Label tags
│───────────────────────────────│
│  (⏳)(📜)(⚡)          +2     │  ← Mana pip link footer
└──────────────────────────────┘
```

### Zone Rules

1. **Entity Mana Badge** (top-left): Always visible. Shows mana symbol + entity name in uppercase. Background at entity color, 85% opacity with backdrop-blur.

2. **Status Chip** (top-right): Visible for important states only. Colored chip with text (Posseduto, In Corso, Active, Error). Reinforced by border glow effect.

3. **Primary Actions** (top-right, below status): 1-2 actions max on front. Context-dependent per entity type (e.g., Game: "Play" + "Ask AI"; Session: "Pause" + "Score"). Appear on hover as translucent rounded buttons.

4. **Entity Accent Bar** (left edge): 4px vertical bar in entity color. Always visible.

5. **Cover Image**: 55% of card height. Gradient overlay fading to card background at bottom. Shimmer on hover.

6. **Tag Strip** (left edge): Vertical colored dots for categories/genres. Max 3 visible.

7. **Title + Subtitle**: Quicksand 700 for title, Nunito for subtitle. 2-line clamp on title.

8. **Rating**: Star display + numeric value. Only for entities that have ratings (Game, Agent).

9. **Tag Labels**: Small rounded chips for metadata (genre, player count, duration).

10. **Mana Link Footer**: Row of mana pip icons linking to related entities. Each pip is clickable → opens deck stack. Shows "+N" count for additional links.

### Status Glow System

Status is communicated through two channels:

- **Chip** (text): For important/active states
- **Border glow** (visual reinforcement):

| State | Glow Effect |
|-------|-------------|
| Active / In Corso | Pulsing glow animation at entity color |
| Completa / Success | Solid subtle glow |
| Pausa / Idle | No glow, slightly dimmed card |
| Error / Failed | Red pulsing glow |
| New / Unread | Bright glow ring |

## Card Anatomy — Back (Modular Blocks)

The back is composed from reusable blocks. Each entity type declares which blocks to show and in what order.

### Available Blocks

| Block | Purpose | Used by |
|-------|---------|---------|
| `StatsBlock` | Key-value statistics | Game, Session, Player, Agent |
| `ActionsBlock` | List of action buttons | All entities |
| `TimelineBlock` | Chronological event list | Session, Event |
| `RankingBlock` | Ordered player ranking | Session |
| `KBPreviewBlock` | Knowledge base summary | Game, Agent |
| `MembersBlock` | List of players/members | Group, Event, Session |
| `ContentsBlock` | Items contained within | Collection, Toolkit |
| `HistoryBlock` | Recent activity/queries | Agent, Chat |
| `ProgressBlock` | Progress bar/milestone | Achievement, Session |
| `NotesBlock` | Freeform text notes | Note, Session |
| `DetailLinkBlock` | "Go to full detail" CTA | All entities |

### Entity → Block Mapping

```yaml
game:
  - StatsBlock       # plays, win rate, avg time
  - ActionsBlock     # new session, add to collection, ask AI
  - KBPreviewBlock   # docs count, last query

session:
  - RankingBlock     # player scores
  - TimelineBlock    # session events
  - ActionsBlock     # add score, snapshot, notes

player:
  - StatsBlock       # games owned, sessions, win rate
  - ActionsBlock     # invite, compare stats
  - ProgressBlock    # level/XP progress

event:
  - MembersBlock     # confirmed attendees
  - TimelineBlock    # event schedule
  - ActionsBlock     # RSVP, add game, share

agent:
  - StatsBlock       # queries, avg time, accuracy
  - HistoryBlock     # recent queries
  - ActionsBlock     # chat, configure, retrain

kb:
  - StatsBlock       # docs, chunks, index status
  - ContentsBlock    # document list
  - ActionsBlock     # upload, reindex

chatSession:
  - HistoryBlock     # recent messages preview
  - ActionsBlock     # continue, export, delete

collection:
  - ContentsBlock    # games in collection
  - StatsBlock       # count, total value, genres
  - ActionsBlock     # add game, share, export

group:
  - MembersBlock     # group members
  - StatsBlock       # sessions together, favorite games
  - ActionsBlock     # plan event, invite

location:
  - StatsBlock       # events hosted, last visit
  - ActionsBlock     # plan event here, directions

expansion:
  - StatsBlock       # parent game, compatibility
  - ActionsBlock     # add to collection, mark owned

toolkit:
  - ContentsBlock    # tools in kit
  - ActionsBlock     # open whiteboard, configure

tool:
  - StatsBlock       # usage count
  - ActionsBlock     # open, configure

achievement:
  - ProgressBlock    # completion percentage
  - StatsBlock       # date earned, rarity

note:
  - NotesBlock       # full note content
  - ActionsBlock     # edit, share, link to entity

custom:
  - StatsBlock       # custom metadata
  - ActionsBlock     # configured actions
```

### Block Interface

Every block implements:

```typescript
type BlockType =
  | 'stats' | 'actions' | 'timeline' | 'ranking' | 'kbPreview'
  | 'members' | 'contents' | 'history' | 'progress' | 'notes' | 'detailLink';

interface BlockAction {
  label: string;
  icon?: string;           // mana symbol or action icon
  onClick: () => void;
  variant?: 'primary' | 'secondary' | 'danger';
}

// Discriminated union — one shape per block type
type BlockData =
  | { type: 'stats'; entries: Array<{ label: string; value: string | number; icon?: string }> }
  | { type: 'actions'; actions: BlockAction[] }
  | { type: 'timeline'; events: Array<{ time: string; label: string; icon?: string }> }
  | { type: 'ranking'; players: Array<{ name: string; score: number; position: number; isLeader?: boolean }> }
  | { type: 'kbPreview'; docsCount: number; chunksCount: number; lastQuery?: string; indexStatus: string }
  | { type: 'members'; members: Array<{ name: string; role?: string; avatarUrl?: string }> }
  | { type: 'contents'; items: Array<{ title: string; entityType: MeepleEntityType; id: string; status?: string }> }
  | { type: 'history'; entries: Array<{ timestamp: string; message: string; sender?: string }> }
  | { type: 'progress'; current: number; target: number; label: string; milestones?: Array<{ at: number; label: string }> }
  | { type: 'notes'; content: string; updatedAt: string }
  | { type: 'detailLink'; href: string; label: string };

interface CardBackBlock {
  type: BlockType;
  title: string;           // section header
  entityColor: HSLColor;   // for accent styling
  data: BlockData;         // type-specific payload
  actions?: BlockAction[]; // optional inline actions
}
```

## Deck Stack

When a user clicks a mana pip in the link footer, a deck stack opens showing related entities of that type.

### Behavior

1. **Trigger**: Click on a mana pip in the link footer
2. **Animation**: Cards fan out from the pip position, overlaying below the parent card
3. **Layout**: Horizontal fan with slight rotation (-8° to +8°), compact variant cards
4. **Content**: Each card in the stack shows: mana symbol, title, status chip (if active)
5. **Interaction**:
   - Click a card in the stack → navigates to that entity (or opens its own deck stack)
   - Click outside or press Escape → collapses the stack
   - Hover a card → lifts it from the stack (translateY + scale)
6. **Count**: If >5 related entities, show first 5 + "View all N" link
7. **Empty state**: If no related entities of that type, mana pip is not shown

### Compact Card in Deck Stack

```
┌─────────────────────┐
│ (🎲) Terraforming   │  ← mana + title (truncated)
│      Mars     [Owned]│  ← status chip (if applicable)
└─────────────────────┘
```

Size: ~120px × 48px. Entity accent border on left. Background matches entity color at 5% opacity.

## Extra Drawer — Polymorphic Container

The right-side drawer serves as a multi-purpose container. Its content depends on the entity type and what the user needs.

### Drawer Modes

| Mode | Trigger | Content |
|------|---------|---------|
| **Info** | Click info button | Extended entity details, full stats |
| **Functional** | Entity-specific action | Whiteboard (Toolkit), Score Calculator, Chat interface |
| **Navigation** | Click entity links | Full list of related entities, filterable by mana type |

### Structure

```
┌──────────────────────────────┐
│ (🎲) Entity Name          ✕ │  ← Entity-colored header with mana
│──────────────────────────────│
│ [Tab1] [Tab2] [Tab3] [Tab4]  │  ← Dynamic tabs based on linked mana
│──────────────────────────────│
│                              │
│  Scrollable content area     │  ← Widgets/info/tools
│                              │
│  Adapts to drawer mode       │
│                              │
└──────────────────────────────┘
```

### Tab Generation

Tabs are generated dynamically from the entity's linked mana types:

- **Overview tab**: Always present as first tab (entity-colored mana as icon)
- **Linked entity tabs**: One tab per linked mana type (e.g., a Game with Sessions, KB, and Agent gets ⏳ 📜 ⚡ tabs)
- **Functional tabs**: Entity-specific tools (e.g., Toolkit adds a "Whiteboard" tab with ⚙ icon)

### Width

- Mobile: Full-width sheet
- Desktop: 600px fixed width
- Animation: Slide from right, 300ms ease-out

## Entity Relationship Map

Defines which entity types can link to which others:

```yaml
game:       [session, kb, agent, expansion, collection, note]
session:    [game, player, event, location, note, group]
player:     [session, group, achievement, collection, note]
event:      [session, game, player, location, group]
collection: [game, expansion, player]
group:      [player, event, session, location]
location:   [event, session, group]
expansion:  [game, collection]
agent:      [game, kb, chatSession, tool]
kb:         [game, agent, note]
chatSession:[agent, game, player]
toolkit:    [tool, game]
tool:       [toolkit, game]
achievement:[player, game, session]
note:       [game, session, player, kb, event]
custom:     []  # No default links. Use `linkedEntityTypes` prop to configure at runtime.
```

## Primary Actions per Entity

Each entity type declares its 1-2 front-face primary actions:

| Entity | Action 1 | Action 2 |
|--------|----------|----------|
| Game | ▶ Play (new session) | 💬 Ask AI |
| Session | ⏸ Pause/▶ Resume | 📊 Score |
| Player | ✉ Invite | 📊 Stats |
| Event | ✓ RSVP | 📤 Share |
| Collection | + Add game | 📤 Share |
| Group | 📅 Plan event | ✉ Invite |
| Location | 📅 Plan here | 📍 Map |
| Expansion | + Add to collection | — |
| Agent | 💬 Chat | ⚙ Config |
| KB | 📄 Upload | 🔄 Reindex |
| Chat | ▶ Continue | — |
| Toolkit | ▶ Open | — |
| Tool | ▶ Use | — |
| Achievement | — (display only) | — |
| Note | ✏ Edit | — |
| Custom | Configurable | — |

## Visual Design Tokens

### Typography

- **Card title**: Quicksand 700, 0.95rem
- **Card subtitle**: Nunito 400, 0.78rem
- **Entity badge**: Quicksand 700, 10px, uppercase, `tracking-wider` (0.05em, matches existing tokens)
- **Tag labels**: Nunito 600, 9px, uppercase
- **Status chip**: Nunito 600, 9px, uppercase

### Shadows (Warm-Toned)

Reuses existing token scale from `meeple-card-v2-design-tokens.md`:

```css
/* Existing tokens — no new shadow tokens needed */
--shadow-warm-md   /* card default */
--shadow-warm-lg   /* card hover */

/* New token — mana-specific only */
--shadow-mana: 0 4px 16px var(--entity-color-alpha-35);
```

### Animations

| Effect | Duration | Easing |
|--------|----------|--------|
| Card hover lift | 250ms | cubic-bezier(0.4, 0, 0.2, 1) |
| Flip | 700ms | cubic-bezier(0.4, 0, 0.2, 1) |
| Deck stack fan | 300ms | cubic-bezier(0.34, 1.56, 0.64, 1) |
| Deck stack collapse | 200ms | ease-in |
| Status glow pulse | 2000ms | ease-in-out, infinite |
| Drawer slide | 300ms | ease-out |
| Mana pip hover | 150ms | ease-out |

### Card Dimensions

| Variant | Aspect Ratio | Min Width |
|---------|-------------|-----------|
| Grid | 7:10 | 200px |
| List | auto | 280px (height: 64px) |
| Compact | auto | 120px (height: 48px) |
| Featured | 16:9 | 300px |
| Hero | auto | full-width (min-h: 320px) |
| Expanded | auto | 320px (min-h: 400px) |

## Naming & Backward Compatibility

### Entity Type Keys

The `MeepleEntityType` union keeps existing keys unchanged. Display names differ from keys:

| Key (code) | Display Name | Notes |
|------------|-------------|-------|
| `kb` | Knowledge | Existing key preserved |
| `chatSession` | Chat | Existing key preserved |
| `custom` | Custom | `customColor` prop continues to override mana symbol color |
| `collection` | Collection | NEW — add to union |
| `group` | Group | NEW — add to union |
| `location` | Location | NEW — add to union |
| `expansion` | Expansion | NEW — add to union |
| `achievement` | Achievement | NEW — add to union |
| `note` | Note | NEW — add to union |

### Deprecated Props Migration

The following existing props are replaced by the mana pip system:

| Old Prop | Replacement | Migration |
|----------|-------------|-----------|
| `navigateTo` | Mana pip link footer | Phase 2: adapter converts `ResolvedNavigationLink[]` to mana pips. Prop kept as deprecated alias during transition. |
| `linkCount` | Auto-computed from entity links | Phase 2: ignored if mana pips present, fallback if not. |
| `firstLinkPreview` | Deck stack on pip click | Phase 4: removed after deck stack ships. |
| `onLinksClick` | Deck stack interaction | Phase 4: removed after deck stack ships. |
| `infoHref` | `entityId` + drawer | Phase 5: kept as fallback for entities without drawer support. |

### Stub Strategy for New Entity Types (Phases 1-5)

During frontend development before Phase 6 backend work:

- New entity types use **empty data stubs** — blocks render with "No data yet" placeholder
- `useEntityDetail(type, id)` generic hook returns `{ loading: false, data: null }` for unimplemented types
- Deck stack shows "Coming soon" for pips linking to unimplemented entity types
- All switch/case branches include the 6 new types with graceful empty states

## Deck Stack — Variant-Specific Behavior

| Parent Variant | Deck Stack Behavior |
|----------------|-------------------|
| Grid | Fan below card, overlaying grid items (uses portal) |
| Featured | Fan below card, wider compact cards |
| Hero | Fan below hero section |
| List | Popover below the row (uses portal, no content push) |
| Compact | Popover below (uses portal, minimal fan, dropdown-like) |
| Expanded | Fan below card |

All deck stacks render via React Portal to avoid overflow clipping.

## Drawer Data Fetching

### Generic Entity Detail Hook

Replace entity-specific hooks with a generic dispatcher:

```typescript
function useEntityDetail(type: MeepleEntityType, id: string) {
  // Routes to entity-specific fetcher internally
  // Returns standardized { data, loading, error } shape
  // For unimplemented types: returns { data: null, loading: false, error: null }
}
```

Existing hooks (`useGameDetail`, `useAgentDetail`, etc.) become internal implementations. `DrawerEntityType` extends to match all 16 `MeepleEntityType` values.

## Migration Strategy

### Phase 1: Mana Symbol System
- Create SVG icon set for all 16 entity types
- Build `ManaSymbol` component (sizes: full/medium/mini)
- Add new entity types to `MeepleEntityType` union
- Update `entityColors` map with all 16 colors

### Phase 2: Card Front Redesign
- Refactor entity badge to use `ManaSymbol`
- Implement status glow system alongside existing chip
- Refactor link footer to use mana pips
- Add primary action slots (1-2 per entity)

### Phase 3: Modular Back Blocks
- Extract existing `GameBackContent`/`SessionBackContent` into blocks
- Build block component library (11 block types)
- Create entity→block configuration registry
- Wire up FlipCard to use block composition

### Phase 4: Deck Stack
- Build `DeckStack` component with fan animation
- Create compact card variant for stack items
- Wire mana pip clicks to deck stack
- Handle empty states and overflow (>5 items)

### Phase 5: Drawer Evolution
- Refactor `ExtraMeepleCardDrawer` for dynamic tab generation
- Add functional drawer modes (Whiteboard, Calculator, Chat)
- Wire drawer tabs to mana system
- Support all 16 entity types

### Phase 6: New Entity Types
- Add backend support for Collection, Group, Location, Expansion, Achievement, Note
- Create entity-specific back block configurations
- Build entity-specific drawer content
- Wire entity relationship map

## Out of Scope

- Custom SVG icon design (use emoji placeholders, commission SVGs later)
- Backend bounded context changes for new entities (separate spec)
- Mobile-specific gestures (swipe to flip, etc.)
- Offline support
- Performance optimization for large deck stacks (>100 items)

## Success Criteria

1. All 16 entity types render with correct mana symbol and color
2. Any entity card can flip to show its configured back blocks
3. Mana pip clicks open deck stack with compact related cards
4. Drawer opens with dynamic tabs matching linked entity types
5. Status is visible through both chip and border glow
6. No regression in existing Game/Session card functionality
7. Visual coherence: mana symbols are the single consistent visual thread across all card interactions
