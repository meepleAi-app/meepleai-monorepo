# Game Toolbox — Design Specification

> **Status**: Design approved
> **Date**: 2026-03-14
> **Scope**: New bounded context `GameToolbox` with Card Deck tool, MeepleCard integration, template system

---

## 1. Overview

The Toolbox is a first-class, per-game configurable container that holds a modular set of tools. It replaces the flat widget dashboard with a structured system where each game gets exactly the tools it needs, with optional phase-based workflow.

### Key Decisions

| Decision | Choice |
|----------|--------|
| **Nature** | First-class entity with its own UX + modular per-game composition |
| **Configuration** | Predefined templates + full host customization |
| **Tool interaction** | Shared context (freeform) + optional phased workflow |
| **Card Deck scope** | Decks with random draw — no per-player hands/visibility in v1 |
| **Usage context** | Live sessions + offline companion |
| **v1 scope** | Toolbox framework + Card Deck as new tool; 6 existing widgets migrate via adapter |
| **Architecture** | New bounded context `GameToolbox` with bridge to existing `GameToolkit` |

---

## 2. Domain Model

### 2.1 Entities

**Toolbox** (aggregate root)
- Belongs to a `Game` (or standalone for offline use)
- `Mode`: `Freeform` | `Phased`
- Contains an ordered collection of `ToolboxTool`
- Has a `SharedContext` (player list, current turn, current round)
- Can be instantiated from a `ToolboxTemplate`

**ToolboxTemplate**
- Reusable blueprint: "Toolbox for Catan", "Toolbox for Poker"
- Defines: which tools to include, configuration for each, phases (if phased)
- Source: `Manual` | `Community` | `AI` (generated from Knowledge Base)
- Can be cloned and customized by the host

**ToolboxTool** (abstract base)
- Common interface: `Id`, `Type`, `Config` (JSON), `State` (JSON), `IsEnabled`, `Order`
- Two categories:
  - **AdaptedTool**: wrapper for the 6 existing widgets (DiceRoller, TurnManager, ScoreTracker, ResourceManager, Notes, Whiteboard)
  - **NativeTool**: new tools born in the Toolbox context (Timer, Counter in future — not in v1)

> **Note**: CardDeck is an AdaptedTool (facade over `SessionTracking.SessionDeck`), not a NativeTool. See Section 3.0.

**Phase** (only in Phased mode)
- `Name`, `Order`, `ActiveToolIds[]` (which tools are enabled in this phase)
- `AutoAdvance`: optional future feature for automatic phase progression

### 2.2 Value Objects

**SharedContext**
- `Players`: ordered list with name, color, avatar
- `CurrentPlayerIndex`: whose turn it is
- `CurrentRound`: round number
- `CustomProperties`: key-value dictionary for game-specific data

**DeckType**: enum `Standard52` | `Standard52WithJokers` | `Custom`

**ToolboxMode**: enum `Freeform` | `Phased`

### 2.3 Relationships

```
ToolboxTemplate --creates--> Toolbox
Toolbox --contains--> ToolboxTool[]
Toolbox --has--> SharedContext
Toolbox --has--> Phase[] (if Phased)
Phase --activates--> ToolboxTool[]
Toolbox --adapts--> GameToolkit widgets (via adapter)
Game --auto-creates--> Toolbox (BR-01 from EntityLink spec)
```

---

## 3. Card Deck Tool

A deck/draw system for random card extraction. Not a card game simulator — cards drawn are visible to all players.

### 3.0 Architectural Decision: CardDeck vs SessionDeck

The existing `SessionTracking` bounded context contains a `SessionDeck` entity with full card game support (hands, discard piles, per-player visibility). The Toolbox `CardDeck` is an **AdaptedTool facade over `SessionDeck`**, following the same adapter pattern used for the 6 existing widgets.

- `CardDeck` in `GameToolbox` delegates to `SessionDeck` in `SessionTracking`
- Only the simplified API surface (Shuffle, Draw, Reset) is exposed through the Toolbox
- No new database tables for cards — reuses `SessionDeck` persistence
- The `SessionDeck` features beyond v1 scope (hands, discard, visibility) remain available for future Toolbox versions without migration

This means `CardDeck` is NOT a NativeTool — it is an **AdaptedTool** like the other 6 widgets, wrapping `SessionDeck` with a simplified interface.

### 3.1 Facade Surface

**CardDeck** (AdaptedTool wrapping SessionDeck)
- Contains a collection of `Card` (delegated to SessionDeck)
- `DeckType`: Standard52, Standard52WithJokers, Custom
- Multiple decks can exist within the same Toolbox

**Card** (mapped from SessionDeck's card model)
- `Id`, `Name`, `Value` (optional string/number), `Suit` (optional)
- `CustomProperties`: dictionary for arbitrary properties (e.g., "effect", "cost")
- `IsDrawn`: whether the card has been extracted from the deck

### 3.2 Operations

| Operation | Delegates to | Description |
|-----------|-------------|-------------|
| `Shuffle(deckId)` | `SessionDeck.Shuffle()` | Randomize card order within the deck |
| `Draw(deckId, count)` | `SessionDeck.DrawCards()` | Extract N random cards, mark as drawn |
| `Reset(deckId)` | `SessionDeck.Reset()` | Return all cards to the deck |

### 3.3 Examples

**Poker deck**:
```
Deck: 52 standard cards
→ Shuffle → Draw(5) → A♥ K♠ Q♦ J♣ 10♥
→ Draw(3) → 3♠ 7♦ 9♣
→ Reset → all 52 cards back in deck
```

**Monopoly "Chance" deck**:
```
Deck: 16 custom cards
  Card: { name: "Go to Jail", effect: "move_to_jail" }
  Card: { name: "Collect $200", effect: "collect_200" }
  ...
→ Shuffle → Draw(1) → "Go to Jail!"
→ When empty → Reset + Shuffle
```

### 3.4 Out of Scope (v1)

- Zones (Hand, Table, Discard)
- Per-player visibility
- Move/Flip/RevealTo operations
- Differentiated front/back per card

---

## 4. Frontend Architecture

### 4.1 MeepleCard Integration

The Toolbox integrates with the MeepleCard system at two levels:

| Level | Card Type | Entity | Color |
|-------|-----------|--------|-------|
| **Container** | Toolbox Kit Card | `toolkit` (existing, green `142 70% 45%`) | Green |
| **Content** | Tool Card | `tool` (new entity type) | Cyan/Teal `172 66% 50%` |

**New design token**:
```css
--color-entity-tool: 195 80% 50%;  /* Sky Blue — distinct from toolkit green (142) and kb_card teal (174) */
```

**Implementation required in**:
- `apps/web/src/styles/design-tokens.css`: add `--color-entity-tool`
- `apps/web/src/components/ui/data-display/meeple-card-styles.ts`: add `'tool'` to `MeepleEntityType` union, add entry in `entityColors` record
- `CardNavigationFooter`, `entity-table-view.tsx`, and any component switching on `MeepleEntityType`: add `tool` case

Each Tool Card has a visual sub-type icon:
- 🎲 DiceRoller, 📊 ScoreTracker, 🔄 TurnManager, 📦 ResourceManager, 📝 Notes, 🎨 Whiteboard, 🃏 CardDeck

### 4.2 Toolbox Kit Card

**Front**:
```
┌─────────────────────────────────────────┐
│ [🔧 TOOLKIT]                            │
│ ┌───────────────────────────────────┐   │
│ │        [game cover image]         │   │
│ └───────────────────────────────────┘   │
│ Catan Toolkit                           │
│ Freeform · 5 tools · 3 players         │
├─────────────────────────────────────────┤
│  🎲 Dice  📦 Resources  🃏 Cards  +2   │  ← Tool preview chips
├─────────────────────────────────────────┤
│  — Navigate to —                        │
│  [🎲 Game] [🎮 Session]                │
└─────────────────────────────────────────┘
```

**Back** (flip):
```
┌─────────────────────────────────────────┐
│ [🔧 TOOLKIT]              [FREEFORM]   │
│                                         │
│ Tools:                                  │
│  🎲 Dice (2d6)                         │
│  📦 Resources (Wood, Clay, Grain...)   │
│  🃏 Development Cards (25 custom)      │
│  📊 Score (per round)                  │
│  🔄 Turns (3-4 players)               │
│                                         │
│ Phases: —                               │
│ Template: "Catan Standard"              │
└─────────────────────────────────────────┘
```

**Drawer** — tabs: Overview | Tools | Phases | Links
- "Tools" tab shows a list of Tool Cards (clickable to expand inline or open full view)

### 4.3 Tool Card

**Front** (compact variant within Toolbox):
```
┌──────────────────────┐
│ [🎲 TOOL]            │
│ Dice                 │
│ 2d6 · History: 5    │
│ [▶ Use]             │
└──────────────────────┘

┌──────────────────────┐
│ [🃏 TOOL]            │
│ Development Cards    │
│ 25 cards · 18 in    │
│ deck · 7 drawn      │
│ [Draw] [Shuffle]    │
└──────────────────────┘
```

### 4.4 Freeform Mode

Tool Cards displayed as collapsible panels. User expands/collapses what they need. SharedContext always visible at top.

```
┌─────────────────────────────┐
│ 🎲 Toolbox: Catan  FREEFORM│
├─────────────────────────────┤
│ 👥 Players (3/4)           │  ← SharedContext sticky
│  Marco · Lucia · Leo       │
├─────────────────────────────┤
│ 🎲 Dice               [▲] │  ← Tool Card expanded
│  ⚃ + ⚄ = 8                │
├─────────────────────────────┤
│ 📦 Resources           [▼] │  ← Tool Card collapsed
│ 🃏 Cards               [▼] │
│ 📊 Score               [▼] │
│ 🔄 Turns               [▼] │
└─────────────────────────────┘
```

### 4.5 Phased Mode

Adds a phase timeline bar at the top. Tool Cards activate/lock based on current phase. Locked Tool Cards appear greyed out and non-interactive.

```
┌─────────────────────────────────────┐
│ ⚙️ Toolbox: Agricola        PHASED │
├─────────────────────────────────────┤
│ [●Sowing]─[○Harvest]─[○Feed]─[○End]│  ← Phase timeline
├─────────────────────────────────────┤
│ 👥 Marco (turn) · Lucia · Leo  R3  │
├─────────────────────────────────────┤
│ 🌾 Worker Placement    ✅ active   │  ← Active in current phase
├─────────────────────────────────────┤
│ 📊 Score               🔒 locked   │  ← Not active in Sowing
│ 🍽️ Feeding             🔒 locked   │
├─────────────────────────────────────┤
│ [▶ Next Phase]                      │
└─────────────────────────────────────┘
```

### 4.6 Offline Companion

Same UX but without real-time sync. A banner indicates "Offline mode — solo per te". Useful for testing configurations or learning game phases.

### 4.7 Responsive / Mobile

- Tool Cards become full-width
- SharedContext compresses to a sticky top bar
- Phase timeline scrolls horizontally
- Card Deck uses vertical scroll for drawn cards

### 4.8 Navigation Flow

```
Game Card → click 🔧 → Toolbox Kit Card → click tool → Tool Card (expanded/interactive)
```

In live session, the Toolbox Kit Card opens directly in active mode with all Tool Cards interactive.

---

## 5. Backend Architecture

### 5.1 Bounded Context Structure

```
BoundedContexts/GameToolbox/
├── Domain/
│   ├── Entities/
│   │   ├── Toolbox.cs              — Aggregate root
│   │   ├── ToolboxTool.cs          — Base entity
│   │   ├── Phase.cs                — Phase entity
│   │   └── ToolboxTemplate.cs      — Template blueprint
│   └── ValueObjects/
│       ├── SharedContext.cs
│       └── ToolboxMode.cs
├── Application/
│   ├── Commands/
│   │   ├── CreateToolbox
│   │   ├── UpdateToolboxMode
│   │   ├── AddToolToToolbox
│   │   ├── RemoveToolFromToolbox
│   │   ├── ReorderTools
│   │   ├── UpdateSharedContext
│   │   ├── CreateCardDeck
│   │   ├── ShuffleCardDeck
│   │   ├── DrawCards
│   │   ├── ResetCardDeck
│   │   ├── AddPhase / RemovePhase / ReorderPhases
│   │   ├── AdvancePhase
│   │   ├── CreateToolboxTemplate
│   │   └── ApplyToolboxTemplate
│   └── Queries/
│       ├── GetToolbox
│       ├── GetToolboxByGame
│       ├── GetToolboxTemplates
│       └── GetAvailableTools
├── Infrastructure/
│   └── Persistence, Repositories, EF Config
└── Adapters/
    ├── ToolkitWidgetAdapter.cs     — Bridge to 6 existing widgets
    └── CardDeckAdapter.cs          — Facade over SessionTracking.SessionDeck
```

### 5.2 API Routes

```
# Toolbox CRUD
GET    /api/v1/toolboxes/{id}                          → GetToolbox query
GET    /api/v1/toolboxes/by-game/{gameId}              → GetToolboxByGame query
POST   /api/v1/toolboxes                               → CreateToolbox command
PUT    /api/v1/toolboxes/{id}/mode                     → UpdateToolboxMode command

# Tool management
GET    /api/v1/toolboxes/{id}/available-tools           → GetAvailableTools query
POST   /api/v1/toolboxes/{id}/tools                    → AddToolToToolbox command
DELETE /api/v1/toolboxes/{id}/tools/{toolId}            → RemoveToolFromToolbox command
PUT    /api/v1/toolboxes/{id}/tools/reorder            → ReorderTools command

# Shared context
PUT    /api/v1/toolboxes/{id}/shared-context           → UpdateSharedContext command

# Card Deck operations
POST   /api/v1/toolboxes/{id}/card-decks               → CreateCardDeck command
POST   /api/v1/toolboxes/{id}/card-decks/{deckId}/shuffle → ShuffleCardDeck command
POST   /api/v1/toolboxes/{id}/card-decks/{deckId}/draw?count=1 → DrawCards command
POST   /api/v1/toolboxes/{id}/card-decks/{deckId}/reset → ResetCardDeck command

# Phases
POST   /api/v1/toolboxes/{id}/phases                   → AddPhase command
DELETE /api/v1/toolboxes/{id}/phases/{phaseId}          → RemovePhase command
PUT    /api/v1/toolboxes/{id}/phases/reorder           → ReorderPhases command
POST   /api/v1/toolboxes/{id}/phases/advance           → AdvancePhase command

# Templates
GET    /api/v1/toolbox-templates                       → GetToolboxTemplates query
GET    /api/v1/toolbox-templates/by-game/{gameId}      → GetToolboxTemplates query (filtered)
POST   /api/v1/toolbox-templates                       → CreateToolboxTemplate command
POST   /api/v1/toolbox-templates/{id}/apply            → ApplyToolboxTemplate command
```

> **Note**: `ApplyToolboxTemplate` auto-creates all tools defined in the template, including CardDecks (delegates to `CreateCardDeck` internally). No separate API calls needed for template application.

### 5.3 Real-time Sync

Reuses the existing SSE infrastructure. The Toolbox emits domain events:

| Event | Trigger | Broadcast to |
|-------|---------|-------------|
| `CardDrawn` | Draw operation | All session participants |
| `DeckShuffled` | Shuffle operation | All session participants |
| `DeckReset` | Reset operation | All session participants |
| `PhaseAdvanced` | Phase change | All session participants |
| `SharedContextUpdated` | Player/turn/round change | All session participants |
| `ToolAdded` / `ToolRemoved` | Config change | All session participants |

### 5.4 Adapter Layer

The `ToolkitWidgetAdapter` maps existing GameToolkit widgets to `ToolboxTool` interface:

| Existing Entity | Mapped As |
|----------------|-----------|
| RandomGeneratorWidget | ToolboxTool(Type: "DiceRoller") |
| TurnManagerWidget | ToolboxTool(Type: "TurnManager") |
| ScoreTrackerWidget | ToolboxTool(Type: "ScoreTracker") |
| ResourceManagerWidget | ToolboxTool(Type: "ResourceManager") |
| NoteManagerWidget | ToolboxTool(Type: "Notes") |
| WhiteboardWidget | ToolboxTool(Type: "Whiteboard") |
| **SessionDeck** | **ToolboxTool(Type: "CardDeck")** |

The adapter delegates all operations to existing implementations. No rewrite needed. CardDeck delegates to `SessionTracking.SessionDeck` with a simplified API surface (Shuffle/Draw/Reset only).

---

## 6. State Management & Data Flow

### 6.1 Zustand Store

```typescript
interface ToolboxStore {
  toolbox: Toolbox | null;
  activeTools: Record<string, ToolState>;
  sharedContext: SharedContext;
  currentPhase: Phase | null;
  isOffline: boolean;

  // Actions
  loadToolbox(id: string): Promise<void>;
  updateSharedContext(ctx: Partial<SharedContext>): void;
  advancePhase(): void;
  toggleTool(toolId: string): void;
  toolAction(toolId: string, action: string, params?: unknown): void;

  // Card Deck slice
  cardDecks: Record<string, DeckState>;
  shuffle(deckId: string): void;
  draw(deckId: string, count: number): void;
  reset(deckId: string): void;
}
```

### 6.2 Sync Strategy

| Scenario | Mechanism |
|----------|-----------|
| Live session | SSE (existing infra) — Toolbox events broadcast to participants |
| Offline companion | Local state only, no sync |
| Optimistic UI | Existing pattern: immediate local update, rollback on server rejection |

### 6.3 Data Flow Example — Draw Card

```
User clicks [Draw]
  → toolboxStore.draw(deckId, 1)
  → Optimistic: card shown as drawn immediately
  → POST /api/v1/toolboxes/{id}/card-decks/{deckId}/draw?count=1
  → Server validates, persists, emits CardDrawn event
  → SSE broadcast: { type: "CardDrawn", card: {...}, deckId }
  → Other players receive event → update their view
  → On error: rollback optimistic update
```

### 6.4 Persistence

| Data | Storage | When |
|------|---------|------|
| Toolbox config (tools, phases) | PostgreSQL via EF Core | On save/template creation |
| Tool state runtime (scores, drawn cards) | JSON in existing `ToolkitSessionState` (SessionTracking BC) | Every action |
| SharedContext | PostgreSQL | Every turn/round change |

---

## 7. Template System

### 7.1 Template Structure

```json
{
  "name": "Catan Standard",
  "gameId": "uuid-or-null",
  "mode": "Freeform",
  "source": "AI",
  "tools": [
    { "type": "DiceRoller", "config": { "formula": "2d6" } },
    { "type": "ResourceManager", "config": { "resources": ["Wood","Clay","Grain","Ore","Wool"] } },
    { "type": "CardDeck", "config": { "deckType": "Custom", "cards": ["...25 development cards"] } },
    { "type": "ScoreTracker", "config": { "categories": ["Roads","Settlements","Cities","Bonus"] } },
    { "type": "TurnManager", "config": { "maxPlayers": 4 } }
  ],
  "phases": [],
  "sharedContextDefaults": { "customProperties": { "victoryTarget": 10 } }
}
```

### 7.2 Template Sources

| Source | How it works |
|--------|-------------|
| **Manual** | Host saves their configuration as a personal template |
| **Community** | Shared template visible to all users (moderated) |
| **AI** | Generated from game's Knowledge Base via a **new** `GenerateToolboxTemplateFromKb` command in `GameToolbox` BC. The existing `GameToolkit.generate-from-kb` endpoint produces `AiToolkitSuggestionDto` which is incompatible with `ToolboxTemplate` (no phases, no SharedContext defaults, different tool array format). The new command reuses the same KB retrieval/AI pipeline but maps output to `ToolboxTemplate` structure. |

### 7.3 User Flow

```
1. User opens a game in library
2. BR-01: Toolkit Card already exists (auto-created)
3. Click Toolkit Card → "Configure Toolbox"
4. If templates exist:
   → Show template list (AI-suggested on top)
   → User picks → Toolbox created from template
   → User can customize: add/remove tools, edit config
   → [Save as my template] optional
5. If no templates:
   → "Create from scratch" → tool catalog → compose
   → [Save as template] optional
```

---

## 8. Migration Path

| Phase | What happens | User impact |
|-------|-------------|-------------|
| **Phase 1** | New `GameToolbox` bounded context + CardDeck tool + Adapter layer | Existing 6 widgets continue to work via adapter |
| **Phase 2** | New Toolbox UX with Tool Cards, existing widgets appear as Tool Cards | User sees new layout, same tools underneath |
| **Phase 3** | Template system + Phased mode | Full functionality |
| **Deprecation** | Old ToolkitDashboard remains accessible with "Try the new Toolbox" banner | No breakage, gradual migration |

Existing data (widget state in sessions) remains valid — the Adapter maps it to the new model without data loss.

---

## 9. Design Token Additions

```css
/* New entity type for Tool Cards */
--color-entity-tool: 195 80% 50%;  /* Sky Blue — distinct from toolkit (142), kb_card (174) */
```

Tool sub-type icons:
| Type | Icon | Usage |
|------|------|-------|
| DiceRoller | 🎲 | Dice tool card |
| ScoreTracker | 📊 | Score tool card |
| TurnManager | 🔄 | Turn tool card |
| ResourceManager | 📦 | Resource tool card |
| Notes | 📝 | Notes tool card |
| Whiteboard | 🎨 | Whiteboard tool card |
| CardDeck | 🃏 | Card deck tool card |

---

## 10. Out of Scope (Future)

- Card zones (Hand, Table, Discard) with per-player visibility
- Card Move/Flip/RevealTo operations
- Timer tool
- Specialized counter tool
- Auto-advance phase conditions
- Sandbox/preview mode for template creators
- Drag & drop tool reordering in UI
