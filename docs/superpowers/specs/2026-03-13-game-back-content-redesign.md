# GameBackContent Redesign — Issue #336

> **Date**: 2026-03-13 | **Status**: Approved | **Scope**: `GameBackContent.tsx` + `MeepleLibraryGameCard.tsx` + tests

## Goal

Redesign the back face of the Game MeepleCard with an enriched header, horizontal stats row, condensed KB summary, tag pills, navigation links with counters, and a compact footer. All new fields are optional — sections auto-hide when data is absent.

## Design Decisions

| # | Question | Choice | Rationale |
|---|----------|--------|-----------|
| 1 | Data availability strategy | **A) Full interface, optional fields** | Sections gracefully hidden via `{field && <Section/>}`. Zero mock data. |
| 2 | Action layout | **C) Nav links with counters + footer toggles** | Separates navigation (sessions, notes, links) from toggle actions (favorite). Reduces clutter. |
| 3 | Header author field | **B) Reuse subtitle (publisher)** | Publisher already available in data flow. No new `author` prop needed. |
| 4 | Stats layout | **B) Horizontal row with separators** | Compact, preserves vertical space for new sections. Proven pattern. |

## Interfaces

### GameBackData (updated)

```typescript
export interface GameBackData {
  // --- Existing (preserve exact types from current code) ---
  complexityRating?: number | null;
  playingTimeMinutes?: number | null;
  minPlayers?: number | null;          // flat fields, NOT nested object
  maxPlayers?: number | null;
  averageRating?: number | null;
  kbDocuments?: Array<{ id: string; fileName: string; status: string }>;  // fileName, NOT title
  hasKb?: boolean;
  kbCardCount?: number;

  // --- New (all optional, hidden when absent) ---
  timesPlayed?: number | null;           // stat: games played
  winRate?: number | null;               // stat: win % (0-100)
  totalPlayTimeMinutes?: number | null;  // stat: total play time
  lastPlayedLabel?: string;              // shown in stats row, e.g. "2 giorni fa"
  categories?: string[];                 // tag pills
  mechanics?: string[];                  // tag pills
  bggWeight?: number | null;             // meta: BGG weight (1-5)
  bestPlayerCount?: number | null;       // meta: best player count
  entityLinkCount?: number;              // nav link count
  noteCount?: number;                    // nav link count
  sessionCount?: number;                 // nav link count
}
```

**Note**: `subtitle` (publisher) is passed via `GameBackContentProps`, not `GameBackData`, matching the existing `title` pattern. `nextGameNight` removed (orphaned after action migration).

### GameBackContentProps (updated)

```typescript
export interface GameBackContentProps {
  title: string;
  subtitle?: string;     // NEW: publisher, shown in enriched header
  entityColor: string;
  data: GameBackData;
  actions: GameBackActions;
  detailHref?: string;   // EXISTING: used for "Dettaglio →" footer link
}
```

### GameBackActions (updated)

```typescript
export interface GameBackActions {
  onChatAgent?: () => void;
  onToggleFavorite?: () => void;
  isFavorite?: boolean;
  // --- Navigation (counters in GameBackData) ---
  onViewSessions?: () => void;
  onViewNotes?: () => void;
  onViewLinks?: () => void;
  // --- Removed: onNewSession, onAddToGameNight ---
}
```

**Migration note for removed actions**: `onNewSession` and `onAddToGameNight` currently in `MeepleLibraryGameCard.tsx` will be removed from the actions object. The session creation flow moves to the detail page (`/library/games/{id}/sessions/new`), accessible via the "Dettaglio →" footer link. Game night functionality moves to the detail page as well.

## Constants

```typescript
const MAX_VISIBLE_TAGS = 6;  // Tag pills overflow threshold
```

## Layout (top → bottom, 6 sections)

```
┌─────────────────────────────┐
│ ① ENRICHED HEADER           │  Title + subtitle on entity gradient
│    "Agricola"                │  White text, subtitle muted
│    "di Lookout Games"        │
├─────────────────────────────┤
│ ② STATS ROW                 │  12 Partite | 67% Vittorie | Ultimo: 2gg fa
│    bold value + muted label  │  Horizontal, "|" separators
│    (hidden if no stats)      │  lastPlayedLabel shown here
├─────────────────────────────┤
│ ③ KB SUMMARY                │  "3 documenti KB" + aggregate status
│    compact, 1-2 lines        │  Click → drawer KB tab
│    (hidden if !hasKb)        │
├─────────────────────────────┤
│ ④ TAGS                      │  Scrollable horizontal pills
│    [Strategia] [Worker P.]   │  Categories + Mechanics mixed
│    (hidden if no tags)       │  Max 6 visible + "+N" overflow
├─────────────────────────────┤
│ ⑤ NAVIGATION LINKS          │  Sessioni (3)  →
│    list with counters        │  Note (2)      →
│    + chevron right           │  Collegamenti (5) →
│    (hidden if no links)      │
├─────────────────────────────┤
│ ⑥ COMPACT FOOTER            │  ♥ (toggle) · BGG 3.2 · Best 4p · Dettaglio →
│    icons + inline meta       │  Always visible (at least "Dettaglio →")
└─────────────────────────────┘
```

### Visibility Rules

| Section | Guard condition |
|---------|----------------|
| ① Header | Always visible (title always exists) |
| ② Stats | `timesPlayed != null \|\| winRate != null \|\| totalPlayTimeMinutes != null \|\| lastPlayedLabel` |
| ③ KB Summary | `hasKb && kbDocuments?.length > 0` |
| ④ Tags | `categories?.length > 0 \|\| mechanics?.length > 0` |
| ⑤ Nav Links | At least one `onView*` handler provided AND its corresponding count > 0 |
| ⑥ Footer | Always visible |

**Note**: Stats row uses `!= null` checks (not truthiness) so `timesPlayed: 0` correctly renders the section. Nav Links section is hidden until counter APIs are available — this is intentional (no empty nav links).

## Component Structure

All sub-components are **private to `GameBackContent.tsx`** (not exported). No new files created.

| Sub-component | Responsibility |
|---------------|----------------|
| `StatsRow` | Renders stat items with `\|` separators |
| `KbSummary` | Condensed KB doc count + aggregate status badge |
| `TagPills` | Horizontal pill list with overflow `+N` (MAX_VISIBLE_TAGS = 6) |
| `NavLinks` | Vertical list: icon + label + count + ChevronRight |
| `CompactFooter` | Heart toggle + meta pills + "Dettaglio →" via `detailHref` |

### Styling

| Element | Classes |
|---------|---------|
| Header gradient | `style={{ background: \`linear-gradient(...hsl(${entityColor})...)\` }}` (dynamic, not hardcoded hue) |
| Header title | `font-quicksand font-bold text-white` |
| Header subtitle | `text-white/70 text-sm` |
| Stats values | `font-bold text-sm` |
| Stats labels | `text-muted-foreground text-xs` |
| Stats separator | `text-muted-foreground/50` |
| Tag pills | `bg-muted text-muted-foreground text-[10px] px-2 py-0.5 rounded-full` |
| Nav link row | `hover:bg-muted/50 rounded-lg px-2 py-1.5 transition-colors` |
| Footer heart | Filled (favorite) / outline (not), entity-colored |
| Footer meta | `text-muted-foreground text-xs` |
| Footer detail link | `<Link href={detailHref}>` `text-sm font-medium` entity-colored |

**Note**: Header gradient uses dynamic `entityColor` prop (not hardcoded `hsl(25,95%,45%)`), so the component remains reusable if extended to other entity types.

## Consumer Impact: MeepleLibraryGameCard.tsx

### Fields passed immediately

- `subtitle` → from existing `publisher` prop (now via `GameBackContentProps`)
- All existing fields unchanged (same flat field names, same `| null` types)

### Fields not yet available (interface ready, not passed)

| Field | Requires | Effect when absent |
|-------|----------|--------------------|
| `timesPlayed`, `winRate`, `totalPlayTimeMinutes` | Session aggregate API | Stats row hidden |
| `lastPlayedLabel` | Session query | Stats row hidden (unless other stats present) |
| `categories`, `mechanics` | Game model extension | Tags section hidden |
| `bggWeight`, `bestPlayerCount` | BGG import | Footer meta pills hidden |
| `sessionCount`, `noteCount`, `entityLinkCount` | Counter APIs | Nav Links section hidden |

### Action changes

| Action | Change | Migration |
|--------|--------|-----------|
| `onNewSession` | **Removed** | Accessible via detail page (`/library/games/{id}/sessions/new`) |
| `onAddToGameNight` | **Removed** | Accessible via detail page |
| `onViewSessions` | **Added** | No-op until counter APIs available (nav link hidden anyway) |
| `onViewNotes` | **Added** | No-op until counter APIs available |
| `onViewLinks` | **Added** | No-op until counter APIs available |
| `onChatAgent` | Unchanged | — |
| `onToggleFavorite` | Unchanged | — |

## Testing Strategy

### Updated existing tests (~45)

- Header tests → verify subtitle rendering (present/absent)
- Action tests → remove `onNewSession`/`onAddToGameNight` tests, add nav link tests
- Footer tests → verify heart toggle, meta pills, "Dettaglio →" link
- Null handling → verify `null` values don't break rendering

### New tests (~20)

| Test | Assertion |
|------|-----------|
| **Stats row** | |
| Stats row renders with 1+ stat | At least one stat value visible |
| Stats row hidden when all null/undefined | Section not in DOM |
| Stats row with `timesPlayed: 0` | Section visible, shows "0 Partite" |
| Stats formatting | "67%" not "0.67", time formatted as hours |
| `lastPlayedLabel` in stats row | Shown as last item when present |
| **KB summary** | |
| KB summary condensed | Shows "3 documenti" text |
| KB summary aggregate status | Green if all indexed, amber otherwise |
| **Tags** | |
| Tags with ≤ 6 items | All pills visible, no overflow |
| Tags with > 6 items | First 6 + "+N" overflow pill |
| Tags hidden when empty arrays | Section not in DOM |
| Tags categories only | Renders without mechanics |
| **Navigation links** | |
| Nav links render with handler + count | Row visible with correct count |
| Nav links click calls handler | `onViewSessions` called on click |
| Nav links hidden when no data | Section not in DOM |
| **Footer** | |
| Footer heart toggle state | Filled vs outline icon, aria-pressed |
| Footer meta pills optional | BGG/best player only when present |
| Footer "Dettaglio →" always present | Always in DOM as `<Link>` |
| **Accessibility** | |
| Heart toggle keyboard accessible | Responds to Enter/Space |
| Nav links keyboard navigable | Focusable, activatable |
| Tag overflow announces count | aria-label on "+N" pill |
| **Integration** | |
| Graceful degradation | Only header + footer when no optional data |
| Consumer passes subtitle | Publisher flows from MeepleLibraryGameCard |

### Labels standardization

All user-facing labels in Italian:

| English concept | Italian label |
|-----------------|---------------|
| Games played | "Partite" |
| Win rate | "Vittorie" |
| Total time | "Tempo" |
| Detail | "Dettaglio" |
| Sessions | "Sessioni" |
| Notes | "Note" |
| Links | "Collegamenti" |
| Documents | "documenti KB" |

## Files Modified

| File | Change |
|------|--------|
| `meeple-card-features/GameBackContent.tsx` | Full redesign: interfaces + 6-section layout |
| `MeepleLibraryGameCard.tsx` | Update props passed (add subtitle), remove old actions, add new nav handlers |
| `__tests__/GameBackContent.test.tsx` | Update ~45 tests + add ~20 new |
