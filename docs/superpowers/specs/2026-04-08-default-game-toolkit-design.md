# Default Game Toolkit — Design Specification

**Date**: 2026-04-08
**Status**: Approved
**Scope**: Frontend feature + minimal backend additions
**Bounded Contexts**: GameToolkit (config), SessionTracking (data)

---

## 1. Overview

Every MeepleCard with `entity="game"` displays a 🧰 Toolkit ManaPip in the NavFooter. Tapping it opens a **ToolkitDrawer** — a slide-down panel following the ExtraMeepleCardDrawer pattern — containing 4 tabbed tools for real-time game support.

The same ManaPip can appear on `session`, `player`, and other MeepleCard entity types, always linking to the toolkit of the associated game.

### Goals

- Provide a universal game companion (dice, notes, diary, scores) accessible from any game card
- Reuse existing backend infrastructure (GameToolkit config + SessionTracking data)
- Support both casual use (no session, local state) and full session tracking
- Enable AI-generated presets from game rulebooks via existing KB pipeline

### Non-Goals

- Real-time multiplayer sync (WebSocket/SSE between players) — future scope
- Custom tool plugins — only the 4 built-in tabs
- Mobile-native features (haptics, gyroscope dice) — web-only

---

## 2. Architecture

```
┌─────────────────────────────────────────────────┐
│                   FRONTEND                       │
│                                                  │
│  MeepleCard (game/session/...)                   │
│    └─ NavFooter ManaPip 🧰                       │
│         └─ onClick → open ToolkitDrawer          │
│                                                  │
│  ToolkitDrawer                                   │
│    ├─ Tab: 🎲 DiceRoller                         │
│    ├─ Tab: 📝 Notes                              │
│    ├─ Tab: 📖 EventDiary                         │
│    └─ Tab: 🏆 Scoreboard                         │
│                                                  │
│  PlayerContext (hook)                             │
│    ├─ source: "session" → SessionTracking API    │
│    └─ source: "local" → Zustand store            │
│         └─ promoteToSession() → creates Session  │
├─────────────────────────────────────────────────┤
│                   BACKEND                        │
│                                                  │
│  GameToolkit BC (CONFIG — read-only from FE)     │
│    ├─ Toolkit aggregate (widget config)          │
│    ├─ AI preset generation (KB-based)            │
│    ├─ Default auto-creation on game add          │
│    └─ NEW: UserDicePreset child entity           │
│                                                  │
│  SessionTracking BC (DATA — read/write from FE)  │
│    ├─ DiceRoll (formula, results, history)       │
│    ├─ ScoreEntry (multi-dimension, categories)   │
│    ├─ PlayerNote (private/shared)                │
│    ├─ Participant (name, color, role, turn)      │
│    └─ NEW: SessionEvent entity (diary log)       │
└─────────────────────────────────────────────────┘
```

### Data Flow

| Data | Source BC | Persistence |
|------|----------|-------------|
| Tool config, AI dice presets | GameToolkit | Server (per game) |
| User custom dice presets | GameToolkit | Server (per user per game) |
| Score entries | SessionTracking | Server (if session) or localStorage |
| Player notes | SessionTracking | Server (if session) or localStorage |
| Diary events | SessionTracking | Server (if session) or localStorage |
| Dice roll results | SessionTracking | Volatile — logged in diary only |
| Player list, turn, round | SessionTracking | Server (if session) or localStorage |

---

## 3. ToolkitDrawer Component

### Activation

The drawer is opened via a `NavFooterItem` with `entity: "toolkit"` on any MeepleCard. The consumer page (not MeepleCard itself) manages the drawer state:

```typescript
interface ToolkitDrawerProps {
  gameId: string;
  sessionId?: string;        // If present, sync from SessionTracking
  onClose: () => void;
  defaultTab?: 'dice' | 'notes' | 'diary' | 'scores';
}
```

### Visual Structure

- **Overlay**: semi-transparent backdrop (`rgba(0,0,0,0.3)`)
- **Panel**: slides down from below navbar, glassmorphism (`backdrop-filter: blur(16px) saturate(180%)`)
- **Drag handle**: 36px bar at top for gesture close
- **Tabs**: `🎲 Dadi | 📝 Note | 📖 Diario | 🏆 Punti` — Quicksand 11px bold, entity color underline (toolkit green h:142)
- **Content area**: scrollable, `padding: 12px 16px`
- **PlayerBar**: fixed at bottom, always visible across all tabs

### Animation

- Open: `transform: translateY(-105%) → translateY(0)`, 400ms `cubic-bezier(0.4, 0, 0.2, 1)`
- Close: reverse, or swipe-up gesture on drag handle

---

## 4. Tab: 🎲 Dice Roller

### Layout

```
PRESET RAPIDI (horizontal scroll)
[1d6] [2d6] [1d20] [3d6] [1d100]

★ Da Regolamento (collapsible — AI presets)
  ⚔️ Combattimento  2d6+1d8
  🎯 Skill check     1d20

⭐ I Miei Preset (collapsible — user custom)
  🏠 Casa rule       3d6
  [+ Salva combo corrente]

POOL BUILDER
  d4[0] d6[2] d8[1] d10[0] d12[0] d20[0]  +[3]
  [ 🎲 LANCIA ]

RISULTATO
  ⚄ ⚂ + ⬡3 = 12
  2d6: [5,3]  1d8: [3]  +3
```

### Preset Sources

| Source | Origin | API |
|--------|--------|-----|
| Universal | Hardcoded frontend | None — `[1d6, 2d6, 1d20, 3d6, 1d100]` |
| AI (from rulebook) | GameToolkit BC | `GET /game-toolkits/by-game/{gameId}` → `diceTools[]` |
| User custom | GameToolkit BC | `GET /game-toolkits/{id}/user-dice-presets` |

### Roll Logic

- **With session**: `POST /sessions/{id}/dice-rolls` — server-side cryptographic RNG, persisted
- **Without session**: client-side `crypto.getRandomValues()`, result volatile

### Result Display

- Shake animation: 300ms on dice icons
- Reveal: individual die results with type-specific icons + total
- Auto-log: result pushed to EventDiary as `dice_roll` event

### Backend: UserDicePreset (NEW)

New child entity on `GameToolkit` aggregate:

```csharp
public class UserDicePreset : Entity
{
    public Guid GameToolkitId { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; }        // max 50
    public string Formula { get; private set; }     // max 100, e.g. "2d6+1d8+3"
    public DateTime CreatedAt { get; private set; }
}
```

Endpoints:

```
POST   /api/v1/game-toolkits/{id}/user-dice-presets   → UserDicePresetDto
GET    /api/v1/game-toolkits/{id}/user-dice-presets    → List<UserDicePresetDto>
DELETE /api/v1/game-toolkits/{id}/user-dice-presets/{presetId} → 204
```

---

## 5. Tab: 📝 Notes

### Layout

```
CONDIVISE
  📌 Regole casa
     "Niente alleanze prima del round 3"
     Marco · 2min fa

  📝 Setup reminder
     "Mescolare carte evento dopo ogni era"
     Luca · 5min fa

  [ + Aggiungi nota condivisa ]

────────────────────

🔒 LE MIE NOTE (private)
  🤫 Obiettivo segreto:
     "Conquistare 3 città costiere"
     ora · ✏️ 🗑️

  [ + Aggiungi nota privata ]
```

### Data Mapping

Maps directly to `PlayerNote` in SessionTracking:

| UI Element | Backend Field |
|------------|---------------|
| Shared notes | `NoteType = Shared` |
| Private notes | `NoteType = Private` |
| Pin status | `TemplateKey = "pinned"` |
| Author | `CreatedBy` → `Participant.DisplayName` |

### Interactions

- Tap note → expand to edit mode (autosize textarea)
- Swipe left → delete with confirmation
- Long press → pin/unpin (moves to top)
- Light markdown: bold, italic, lists

### Without Session

Notes stored in Zustand local store. No owner distinction (no auth). On promotion to session → bulk `POST /sessions/{id}/notes`.

### With Session

- Create: `POST /sessions/{id}/notes` with `{ content, noteType, participantId }`
- Read: `GET /sessions/{id}/notes` with visibility filter for current user
- Real-time: `NoteAddedEvent` SSE (already exists)

### Backend Changes

None. `PlayerNote` entity covers all cases.

---

## 6. Tab: 📖 Event Diary

### Layout

```
FILTRI
[Tutti] [🎲 Dadi] [🏆 Punti] [📝 Note] [🔄 Turni] [✍️ Manual]

── Oggi ────────────────────
14:32  🎲  Marco lancia 2d6+1d8 → [5,3]+[7] = 15
14:31  🏆  Luca +3 Punti Vittoria (totale: 12)
14:28  🔄  Turno → Marco (R.3)
14:25  ✍️  Anna: "Luca sospetto alleanza con Marco"
14:20  📝  Marco aggiunge nota condivisa

── Round 2 ────────────────
14:15  🎲  Anna lancia 1d20 → [17] critico!
...

┌──────────────────────────┐
│ ✍️ Aggiungi nota al diario │
└──────────────────────────┘
```

### Event Types

| Type | Icon | Source | Auto-trigger |
|------|------|--------|-------------|
| `dice_roll` | 🎲 | DiceRoller tab | After every roll |
| `score_change` | 🏆 | Scoreboard tab | After score edit |
| `turn_change` | 🔄 | PlayerBar | On turn change tap |
| `note_added` | 📝 | Notes tab | On note create/pin |
| `manual_entry` | ✍️ | Diary input | User writes |
| `player_joined` | 👤 | PlayerBar | New player added |
| `round_advance` | 🔔 | Scoreboard/PlayerBar | On round change |

### Event Schema

```typescript
interface DiaryEvent {
  id: string;
  type: DiaryEventType;
  timestamp: Date;
  playerId?: string;
  playerName?: string;
  round?: number;
  payload: Record<string, unknown>;
}
```

Payload examples:
- `dice_roll`: `{ formula: "2d6+1d8", results: [5,3,7], total: 15 }`
- `score_change`: `{ category: "Punti Vittoria", delta: +3, newTotal: 12 }`
- `manual_entry`: `{ text: "Luca sospetto alleanza con Marco" }`

### Auto-Event Generation

The `ToolkitDrawerProvider` exposes a `logEvent(event)` function. Each tab calls it after actions:
1. Adds to local diary array (immediate UI update)
2. If session exists: `POST /sessions/{id}/events` in background (fire-and-forget)

### Grouping

Events grouped by `Round N` separators when round tracking is active, otherwise by date (Today/Yesterday). Infinite scroll downward (older events).

### Filters

Pill toggles — multi-select. Active filters are OR-combined. "Tutti" resets all filters.

### Backend: SessionEvent (NEW)

New entity in SessionTracking BC:

```csharp
public class SessionEvent : Entity
{
    public Guid SessionId { get; private set; }
    public Guid? ParticipantId { get; private set; }
    public string EventType { get; private set; }     // max 50
    public int? RoundNumber { get; private set; }
    public string PayloadJson { get; private set; }    // JSONB
    public DateTime Timestamp { get; private set; }

    public static SessionEvent Create(
        Guid sessionId, string eventType,
        string payloadJson, Guid? participantId = null,
        int? roundNumber = null) { ... }
}
```

Table: `session_tracking.session_events`

Indexes:
- `(SessionId, Timestamp DESC)` — primary query pattern
- `(SessionId, EventType)` — filter by type
- `(SessionId, RoundNumber)` — filter by round

Endpoints:

```
POST /api/v1/sessions/{id}/events
     Body: { eventType, payloadJson, participantId?, roundNumber? }
     Returns: SessionEventDto

GET  /api/v1/sessions/{id}/events?type=&round=&limit=50&cursor=
     Returns: PagedResult<SessionEventDto>
```

---

## 7. Tab: 🏆 Scoreboard

### Layout

```
CATEGORIE: [+ Aggiungi]
┌─────┬──────┬──────┬──────┬────────┐
│     │  PV  │ Oro  │ Terr.│ TOTALE │
├─────┼──────┼──────┼──────┼────────┤
│⠿ ●  │      │      │      │        │
│Marco│ [12] │  [8] │  [3] │   23   │
│ −[+]│ −[+] │ −[+] │ −[+] │        │
├─────┼──────┼──────┼──────┼────────┤
│⠿   │      │      │      │        │
│Luca │  [9] │ [11] │  [5] │   25   │
│ −[+]│ −[+] │ −[+] │ −[+] │        │
├─────┼──────┼──────┼──────┼────────┤
│⠿   │      │      │      │        │
│Anna │  [7] │  [6] │  [9] │   22   │
│ −[+]│ −[+] │ −[+] │ −[+] │        │
└─────┴──────┴──────┴──────┴────────┘

🥇 Luca 25 · 🥈 Marco 23 · 🥉 Anna 22

[ 🔄 Reset ] [ 📊 Dettaglio round ]
```

### Interactions

| Action | Gesture | Result |
|--------|---------|--------|
| Edit score | Tap cell → numeric input | Overwrites value, recalculates total |
| Quick increment | Tap +/− below cell | +1/-1 (long press: accelerating repeat) |
| Mark turn | Tap player avatar/name | Moves ● indicator, logs `turn_change` |
| Reorder rows | Drag handle ⠿ | Drag row up/down, persistent order |
| Add category | Tap [+ Aggiungi] in header | Name input, new column added |
| Remove category | Long press column header → confirm | Removes column (scores kept in diary) |
| Reset scores | Tap 🔄 → confirm | Zeros all, logs `score_reset` event |
| Round breakdown | Tap 📊 | Expand per-round breakdown below table |

### Round Breakdown (expandable)

```
📊 Breakdown per Round
  R1:  Marco +5  Luca +3  Anna +4
  R2:  Marco +4  Luca +8  Anna +2
  R3:  Marco +3  Luca +2  Anna +5  ← current

  [ Nuovo Round → R4 ]
```

Advancing round increments `currentRound` in PlayerContext and logs `round_advance` event.

### Data Mapping

```
Column "PV"  → ScoreEntry { Category = "PV" }
Each cell    → ScoreEntry { ParticipantId, Category, ScoreValue, RoundNumber? }
Total        → Calculated frontend: sum(entries per participant)
Ranking      → Calculated frontend: sort by total desc
```

### Predefined Categories from AI

If GameToolkit has a `ScoringTemplate` generated from KB (e.g., rulebook says "Victory Points, Resources, Territories"), categories are pre-populated. User can always add/remove.

### API (existing)

| Operation | With session | Without session |
|-----------|-------------|----------------|
| Read scores | `GET /sessions/{id}/scoreboard` | Zustand store |
| Update score | `PUT /sessions/{id}/scores` | Zustand store |
| Ranking | `GetScoreboardQuery` (exists) | Local calculation |

### Backend Changes

None. `ScoreEntry` with `Category` and `RoundNumber` already covers all cases. `GetScoreboardQuery` already returns breakdown by round and category.

---

## 8. PlayerContext & Session Promotion

### Unified Hook

```typescript
interface PlayerContextValue {
  // State
  mode: 'local' | 'session';
  players: Player[];
  currentTurnIndex: number;
  currentRound: number;

  // Player actions
  addPlayer: (name: string, color: string) => void;
  removePlayer: (id: string) => void;
  reorderPlayers: (orderedIds: string[]) => void;
  setTurn: (playerId: string) => void;
  advanceTurn: () => void;
  advanceRound: () => void;

  // Data actions (delegates to correct backend)
  rollDice: (formula: string) => DiceResult;
  addScore: (playerId: string, category: string, value: number) => void;
  addNote: (content: string, type: 'shared' | 'private') => void;
  logEvent: (event: DiaryEvent) => void;

  // Promotion
  canPromote: boolean;
  promote: () => Promise<void>;
  sessionId?: string;
}
```

### Mode Resolution

- `sessionId` prop provided → **session mode** (all data via SessionTracking API)
- No `sessionId` → **local mode** (Zustand store persisted to `localStorage` keyed by `toolkit:${gameId}`)

### Promotion Flow (local → session)

```
1. User taps "Salva come sessione" in PlayerBar
2. POST /sessions { gameId, participants: players[] }
3. Response: { sessionId, sessionCode, participants[] }
4. Migrate data (parallel):
   ├─ POST /sessions/{id}/scores      ← bulk score entries
   ├─ POST /sessions/{id}/notes       ← bulk player notes
   └─ POST /sessions/{id}/events      ← bulk diary events
5. Switch mode: 'local' → 'session'
   - Zustand store cleared for this gameId
   - All calls now go to SessionTracking APIs
   - SessionCode displayed for inviting other players
```

### PlayerBar (always visible)

```
🟠Marco  🟣Luca  🟢Anna  [+]    [⬆ Sessione]
 ●turno
```

Tap player → context menu: Mark turn / Edit / Change color / Remove

### Player Colors

8-color high-contrast palette, auto-assigned in order:

```typescript
const PLAYER_COLORS = [
  '#E67E22', '#9B59B6', '#2ECC71', '#3498DB',
  '#E74C3C', '#F1C40F', '#1ABC9C', '#E84393',
];
```

### Local Store

```typescript
interface ToolkitLocalStore {
  players: LocalPlayer[];
  currentTurnIndex: number;
  currentRound: number;
  scores: Record<string, Record<string, number>>;  // playerId → category → value
  notes: LocalNote[];
  diary: DiaryEvent[];
  customDicePresets: DicePreset[];
  scoreCategories: string[];
}
// Persisted in localStorage keyed by `toolkit:${gameId}`
```

### Persistence Rules

| Data | Persists across drawer close/reopen | Persists across page reload |
|------|-------------------------------------|-----------------------------|
| Players, turn, round | Yes | Yes (localStorage or session) |
| Scores | Yes | Yes |
| Notes | Yes | Yes |
| Diary events | Yes | Yes |
| Dice roll results | No (volatile) | No — logged in diary only |
| Custom dice presets | Yes | Yes (server-side via GameToolkit) |

---

## 9. Backend Changes Summary

### New: SessionEvent entity (SessionTracking BC)

```
SessionTracking/
├── Domain/Entities/SessionEvent.cs              ← NEW
├── Application/
│   ├── Commands/AddSessionEventCommand.cs       ← NEW
│   ├── Queries/GetSessionEventsQuery.cs         ← NEW
│   └── DTOs/SessionEventDtos.cs                 ← NEW
├── Infrastructure/Persistence/
│   └── Configurations/SessionEventEntityConfiguration.cs ← NEW
└── Domain/Repositories/ (extend or new ISessionEventRepository)
```

Endpoints: 2 new (POST + GET with filters)

### New: UserDicePreset entity (GameToolkit BC)

```
GameToolkit/
├── Domain/Entities/GameToolkit.cs               ← MODIFY (add _userDicePresets)
│   └── UserDicePreset.cs                        ← NEW child entity
├── Application/
│   ├── Commands/AddUserDicePresetCommand.cs      ← NEW
│   ├── Commands/RemoveUserDicePresetCommand.cs   ← NEW
│   └── Queries/GetUserDicePresetsQuery.cs        ← NEW
└── Infrastructure/Persistence/
    └── Configurations/UserDicePresetEntityConfiguration.cs ← NEW
```

Endpoints: 3 new (POST + GET + DELETE)

### Migrations

- 1 migration: `session_tracking.session_events` table
- 1 migration: `game_toolkit.user_dice_presets` table

### Impact Summary

| Area | New files | Modified files | Breaking changes |
|------|-----------|---------------|-----------------|
| SessionTracking BC | ~6 | ~1 | 0 |
| GameToolkit BC | ~5 | ~2 | 0 |
| Migrations | 2 | 0 | 0 |
| Routing | 0 | 2 | 0 |
| **Backend total** | **~13** | **~5** | **0** |

### Unchanged (already supports all requirements)

- `DiceRoll` — formulas, cryptographic RNG, d4-d100
- `ScoreEntry` — multi-category, round-based
- `PlayerNote` — Private/Shared/Template visibility
- `Session` + `Participant` — roles, turns, ready state
- `Toolkit` widget config — RandomGenerator, ScoreTracker, NoteManager
- `ScoringTemplate` — AI-generated categories from KB
- `DiceToolConfig` — AI-generated dice presets
- `GetScoreboardQuery` — ranking, round/category breakdown

---

## 10. Frontend File Structure

```
apps/web/src/
├── components/
│   └── toolkit-drawer/
│       ├── ToolkitDrawer.tsx
│       ├── ToolkitDrawerProvider.tsx
│       ├── index.ts
│       │
│       ├── tabs/
│       │   ├── DiceRollerTab.tsx
│       │   ├── DicePresetRow.tsx
│       │   ├── DicePoolBuilder.tsx
│       │   ├── DiceResultDisplay.tsx
│       │   ├── NotesTab.tsx
│       │   ├── NoteCard.tsx
│       │   ├── EventDiaryTab.tsx
│       │   ├── DiaryEventRow.tsx
│       │   ├── DiaryFilters.tsx
│       │   ├── ScoreboardTab.tsx
│       │   ├── ScoreCell.tsx
│       │   ├── ScoreCategoryHeader.tsx
│       │   ├── RankingBar.tsx
│       │   └── RoundBreakdown.tsx
│       │
│       ├── shared/
│       │   ├── PlayerBar.tsx
│       │   ├── PlayerAvatar.tsx
│       │   ├── PlayerSetupModal.tsx
│       │   └── PromoteSessionModal.tsx
│       │
│       └── hooks/
│           ├── useToolkitConfig.ts
│           ├── usePlayerContext.ts
│           ├── useDiceRoller.ts
│           ├── useScoreboard.ts
│           ├── useNotes.ts
│           ├── useDiary.ts
│           └── useToolkitSession.ts
│
├── stores/
│   └── toolkit-local-store.ts
│
└── lib/api/
    └── toolkit-api.ts
```

### Integration Point

MeepleCard itself is NOT modified. Consumer pages add the navItem and render the drawer:

```typescript
<MeepleCard
  entity="game"
  title={game.title}
  navItems={[
    {
      icon: '🧰', label: 'Toolkit', entity: 'toolkit',
      onClick: () => openToolkitDrawer(game.id),
    },
  ]}
/>

{drawerOpen && (
  <ToolkitDrawer
    gameId={drawerGameId}
    sessionId={activeSession?.id}
    onClose={closeDrawer}
  />
)}
```

### File Count

| Area | New | Modified |
|------|-----|----------|
| `toolkit-drawer/` components | 22 | 0 |
| `stores/` | 1 | 0 |
| `lib/api/` | 1 | 0 |
| Consumer pages | 0 | 2-3 |
| MeepleCard | 0 | 0 |
| **Frontend total** | **24** | **2-3** |

---

## 11. Grand Total

| Layer | New files | Modified | Breaking changes |
|-------|-----------|----------|-----------------|
| Backend | ~13 | ~5 | 0 |
| Frontend | ~24 | ~3 | 0 |
| Migrations | 2 | 0 | 0 |
| **Total** | **~39** | **~8** | **0** |
