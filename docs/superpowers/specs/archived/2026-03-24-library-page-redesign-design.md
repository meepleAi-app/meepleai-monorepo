# Library Page Redesign — Gaming Immersive

**Date**: 2026-03-24
**Status**: Approved
**Parent Spec**: `docs/superpowers/specs/2026-03-24-layout-redesign-design.md`
**Scope**: `/library` page only — desktop, mobile, empty state

## Summary

Redesign the Library page to match the Gaming Immersive layout spec, adding:
- PageHeader with title, count, and primary CTA
- Hero Banner (contextual — next session or onboarding)
- Expanded filter chips (beyond state-only to player count, duration, mechanics)
- MeepleCard grid 4col (desktop) / list variant default (mobile)
- Gaming immersive empty state with animated illustration and quick-start actions
- Compact quota widget repositioned as sidebar (desktop) or collapsible (mobile)

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| PageHeader | Title + count badge + "Aggiungi Gioco" CTA | Missing entirely — users lack page context and primary action |
| Hero Banner | Contextual (next session or onboarding) | Adds personality and guides user to next action |
| Filter chips | Expanded set (7 chips) | Current 4 state-only chips miss key discovery filters |
| Empty state | Animated meeples + 3 quick-start cards + trending | Current is generic — no gaming personality or guidance |
| Quota widget | Compact sticky sidebar (desktop) | Current version dominates 30% viewport, distracting |
| Mobile default | List variant | Spec requirement: 7-8 games visible without scroll |
| Search | Remove filter-row search (keep TopNav ⌘K only) | Eliminates duplicated search input |
| Breadcrumb | Fix to show "Libreria › I Miei Giochi" | Currently shows "Home" incorrectly |

## Components Modified

| Component | File | Change |
|-----------|------|--------|
| `PersonalLibraryPage` | `src/components/library/PersonalLibraryPage.tsx` | **Primary restructuring target**: add PageHeader, Hero, sidebar quota layout, new filter wiring |
| `LibraryFilters` | `src/components/library/LibraryFilters.tsx` | Expand filter chips, remove search input, remove sort dropdown (sorting now via chips) |
| `LibraryEmptyState` | `src/components/library/LibraryEmptyState.tsx` | Replace with gaming immersive version (in-place update) |
| `LibraryQuickStats` | `src/components/library/LibraryQuickStats.tsx` | Remove (replaced by PageHeader count badge) |
| `UsageWidget` | `src/components/library/UsageWidget.tsx` | Compact version (top 3 quotas: Giochi privati, PDF mese, Query oggi), sticky sidebar positioning, keep "Passa a Premium" CTA |
| `ViewModeToggle` | `src/components/library/ViewModeToggle.tsx` | Reuse existing component (consolidate with `ViewToggle` from games as per parent spec later) |
| `_content.tsx` | `src/app/(authenticated)/library/_content.tsx` | Minor: adjust UsageWidget positioning (moved into PersonalLibraryPage flex container) |
| `layout.tsx` | `src/app/(authenticated)/library/layout.tsx` | No structural change needed |

## New Components

| Component | File | Purpose |
|-----------|------|---------|
| `LibraryPageHeader` | `src/components/library/LibraryPageHeader.tsx` | Title + count + CTA (reuses existing patterns) |
| `LibraryHeroBanner` | `src/components/library/LibraryHeroBanner.tsx` | Contextual hero: next session or welcome |
| `LibraryEmptyStateImmersive` | `src/components/library/LibraryEmptyStateImmersive.tsx` | Animated empty state with quick-start cards |
| `TrendingGamesRow` | `src/components/library/TrendingGamesRow.tsx` | Horizontal trending cards (empty state only) |

Note: `LibraryEmptyStateImmersive` replaces the existing `LibraryEmptyState` component (in-place update, not v2).

### Prop Interfaces

```typescript
interface LibraryPageHeaderProps {
  gameCount: number;
  onAddGame: () => void;
  className?: string;
}

interface LibraryHeroBannerProps {
  // Create new hook: src/hooks/queries/useNextSession.ts
  // Uses GET /api/v1/sessions?status=upcoming&limit=1 (existing endpoint)
  // Falls back to discovery variant if no upcoming sessions
  className?: string;
}

interface LibraryEmptyStateProps {
  onExploreCatalog: () => void;
  onImportBgg: () => void;
  onCreateCustom: () => void;
}

interface TrendingGamesRowProps {
  className?: string;
  // Fetches top 5 from: GET /api/v1/shared-catalog?sortBy=popularity&limit=5
  // Uses GetSharedGamesQuery from SharedGameCatalog bounded context
}
```

## Desktop Layout (lg+)

```
┌──────────────────────────────────────────────────────────┐
│ TopNav: MeepleAI | Libreria › I Miei Giochi | ⌘K | 🔔 S │
├──────┬─────────────────────────────────────┬─────────────┤
│ Side │ PageHeader                          │ Quota       │
│ bar  │ [I Miei Giochi] [24] [+ Aggiungi]  │ Widget      │
│ 52px │                                     │ (compact)   │
│      │ Hero Banner                         │ sticky      │
│      │ [Prossima sessione — Ven 28]  [→]   │ top: 68px   │
│      │                                     │ w: 200px    │
│      │ Filters                             │             │
│      │ [Tutti][Recenti][Rating][2-4p][60m] │             │
│      │                              [▦][☰] │             │
│      │                                     │             │
│      │ MeepleCard Grid 4col               │             │
│      │ ┌────┐ ┌────┐ ┌────┐ ┌────┐        │             │
│      │ │7:10│ │7:10│ │7:10│ │7:10│        │             │
│      │ │    │ │    │ │    │ │    │        │             │
│      │ └────┘ └────┘ └────┘ └────┘        │             │
│      │ ┌────┐ ┌────┐ ┌────┐ ┌────┐        │             │
│      │ │    │ │    │ │    │ │    │        │             │
│      │ └────┘ └────┘ └────┘ └────┘        │             │
└──────┴─────────────────────────────────────┴─────────────┘
```

**Layout CSS strategy:**
- Main content: `flex: 1`, `max-width: 1000px`
- Quota widget: `w-[200px]`, `sticky top-[68px]`, `flex-shrink-0`, `ml-5`
- Content area uses existing flex layout from `_content.tsx`

## Mobile Layout (<lg)

```
┌──────────────────────────────┐
│ TopNav: MeepleAI    🔔   S  │
├──────────────────────────────┤
│ I Miei Giochi    [24]   [+] │
│                              │
│ ┌──────────────────────────┐ │
│ │ Hero compact (inline)    │ │
│ │ Ven 28 · 3 giocatori [→]│ │
│ └──────────────────────────┘ │
│                              │
│ [Tutti][Recenti][Rating]→→→  │
│ (scroll)                     │
│                              │
│ 24 risultati        [▦][☰]  │
│                              │
│ ┌──────────────────────────┐ │
│ │🪐 Terraforming Mars  4.8│ │
│ │   FryxGames · 1-5 · 120'│ │
│ ├──────────────────────────┤ │
│ │⚔️ Scythe             4.5│ │
│ │   Stonemaier · 1-5 · 115'│ │
│ ├──────────────────────────┤ │
│ │🐦 Wingspan            4.3│ │
│ │   Stonemaier · 1-5 · 70' │ │
│ ├──────────────────────────┤ │
│ │🌳 Everdell            4.2│ │
│ │   Starling · 1-4 · 80'  │ │
│ ├──────────────────────────┤ │
│ │🔷 Azul               4.6│ │
│ │   Plan B · 2-4 · 45'    │ │
│ ├──────────────────────────┤ │
│ │🌋 Spirit Island       4.9│ │
│ │   GTG · 1-4 · 120'      │ │
│ ├──────────────────────────┤ │
│ │🏝️ Catan              4.0│ │
│ │   KOSMOS · 3-4 · 75'    │ │
│ └──────────────────────────┘ │
├──────────────────────────────┤
│ 🏠 Home | 📚 Lib | 🎲 | 💬  │
└──────────────────────────────┘
```

**Mobile specifics:**
- No sidebar (TabBar handles navigation)
- Page title as `<h1>` (no breadcrumb on mobile)
- FAB circular `+` button instead of full CTA
- Hero compact: single line with arrow CTA
- Filter chips: horizontal scroll, `overflow-x: auto`, `-webkit-overflow-scrolling: touch`
- Default: **list variant** (toggle to grid 2col available)
- List card: 52px thumb, title/subtitle/meta-chips left, rating/fav right
- KB status: green dot on thumbnail (bottom-right)
- `pb-80px` for TabBar clearance

## Empty State — Gaming Immersive

```
┌────────────────────────────────────────┐
│ I Miei Giochi              [+ Aggiungi]│
│                                        │
│        🎲    ♟️                         │
│     🧩   ┌─────────┐    🃏            │
│          │  📚     │                   │
│    🎯    │ (glow)  │    🏰            │
│          └─────────┘                   │
│                                        │
│    La tua collezione ti aspetta        │
│    Aggiungi i tuoi giochi preferiti,   │
│    carica i manuali PDF e lascia che   │
│    l'AI ti aiuti con le regole.        │
│                                        │
│ ┌──────────┐ ┌──────────┐ ┌──────────┐│
│ │ 🔍       │ │ 📤       │ │ ✏️       ││
│ │ Esplora  │ │ Importa  │ │ Crea     ││
│ │ Catalogo │ │ da BGG   │ │ Custom   ││
│ └──────────┘ └──────────┘ └──────────┘│
│                                        │
│ 🔥 Popolari questa settimana  Vedi →  │
│ ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐       │
│ │#1 │ │#2 │ │#3 │ │#4 │ │#5 │       │
│ └───┘ └───┘ └───┘ └───┘ └───┘       │
└────────────────────────────────────────┘
```

**Empty state details:**
- Floating meeple icons: `animation: float 4s ease-in-out infinite` with staggered delays
- Central glow ring: `radial-gradient` + pulsing `box-shadow` on `📚` icon
- Title: `font-quicksand 800`, gradient text `fg → primary`
- 3 Quick-start cards with entity-color `border-top: 3px` (orange, blue, green)
- Trending row: horizontal scroll, cards with ranking badge (#1-#5)
- Mobile: quick-start cards stack to single column, trending stays horizontal

**Animations respect `prefers-reduced-motion`:**
```css
@media (prefers-reduced-motion: reduce) {
  .meeple-float { animation: none; }
  .empty-glow { animation: none; box-shadow: 0 0 20px hsla(25,95%,60%,0.15); }
}
```

## Filter Chips — Expanded Set

| Chip | Filter Logic | Backend Support |
|------|-------------|-----------------|
| **Tutti** | No filter (default active) | — |
| **Recenti** | `sortBy: addedAt DESC`, limit 20 | `GetUserGamesQuery` sort param (existing) |
| **Più giocati** | `sortBy: playCount DESC` | `GetUserGamesQuery` already supports `Sort = "playCount"` |
| **Rating ↓** | `sortBy: rating DESC` | `GetUserGamesQuery` sort param (existing) |
| **2-4 giocatori** | `minPlayers <= 4 AND maxPlayers >= 2` | DTO already has `MinPlayers`/`MaxPlayers` — add query filter params |
| **<60 min** | `maxPlayTime <= 60` | DTO already has `PlayingTimeMinutes` — add query filter param |
| **Strategici** | `category = 'strategy'` | `GetUserGamesQuery` already supports `Category` filter |

**Backend query**: Use `GetUserGamesQuery` (at `BoundedContexts/UserLibrary/Application/Queries/Games/GetUserGamesQuery.cs`), NOT `GetUserLibraryQuery`. The games query already has `PlayCount` sorting, `Category` filtering, and the DTO includes `MinPlayers`/`MaxPlayers`/`PlayingTimeMinutes`.

**Backend changes required:**
- Add `MinPlayers`, `MaxPlayers`, `MaxPlayTime` filter params to `GetUserGamesQuery`
- Frontend: switch library page from `GetUserLibraryQuery` to `GetUserGamesQuery` for the main grid (or wire both depending on existing usage)

## Hero Banner — Contextual Logic

```typescript
// Pseudocode for hero variant selection
if (hasUpcomingSession) {
  // Show next session variant
  variant = 'session';
  title = session.name;
  subtitle = `${session.playerCount} giocatori · ${session.games.join(', ')}`;
  cta = { label: 'Vedi Dettagli', href: `/sessions/${session.id}` };
} else if (isNewUser && libraryCount === 0) {
  // No hero on empty state (empty state component handles onboarding)
  variant = 'hidden';
} else {
  // Show discovery variant
  variant = 'discovery';
  title = 'Scopri nuovi giochi';
  subtitle = 'Esplora il catalogo e arricchisci la tua collezione';
  cta = { label: 'Esplora Catalogo', href: '/games' };
}
```

## Quota Widget — Compact Redesign

**Current**: Full-width panel taking ~30% viewport, always visible, 7 quota items displayed.

**New (desktop)**: Compact sticky sidebar, 200px wide, showing top 3 quotas only.

**New (mobile)**: Hidden by default. Accessible via user menu or settings.

```
┌─────────────────┐
│ Il tuo piano Free│
│                  │
│ Giochi    24/50  │
│ ████████░░░░ 48% │
│ PDF mese   2/3   │
│ ██████████░░ 67% │
│ Query oggi 8/20  │
│ ██████░░░░░░ 40% │
└─────────────────┘
```

## Loading & Error States

- **PageHeader**: skeleton (text placeholder 200px + button placeholder)
- **Hero**: skeleton with gradient pulse, matches hero dimensions
- **Card grid/list**: `MeepleCardSkeleton` × 8 (grid) or × 6 (list)
- **Filters**: skeleton chips × 5
- **Error**: toast notification + retry button, content shows last cached data (React Query)
- **Empty state**: no loading state needed (instant render)

## Accessibility

| Element | Requirement |
|---------|-------------|
| PageHeader CTA | `aria-label="Aggiungi un gioco alla libreria"` |
| Filter chips | `role="toolbar"`, `aria-label="Filtra giochi"`, each chip: `role="button"` + `aria-pressed="true/false"` (multi-select supported) |
| View toggle | `role="toolbar"`, `aria-label="Modalità visualizzazione"`, each button: `aria-pressed` |
| Hero banner | `role="banner"`, `aria-label="Prossima sessione"` |
| Empty state illustration | `aria-hidden="true"` (decorative) |
| Quick-start cards | `role="link"`, descriptive `aria-label` |
| List card KB dot | `aria-label="Knowledge base pronta"` on status dot |
| Trending row | `role="list"`, cards as `role="listitem"` |

## Testing Strategy

**Unit tests:**
- `LibraryPageHeader`: renders title, count, CTA click handler
- `LibraryHeroBanner`: session variant vs discovery variant vs hidden
- `LibraryEmptyStateImmersive`: 3 quick-start cards render, click handlers
- `LibraryFilters` (expanded): all 7 chips render, active state toggle, filter callback

**Integration tests:**
- Page loads with PageHeader + Hero + Filters + Grid
- Empty library shows immersive empty state (not grid)
- Mobile viewport shows list variant by default
- Filter chip click updates query params and re-fetches

**E2E (Playwright):**
- Full library page load with authenticated user
- Empty state → click "Esplora Catalogo" → navigates to catalog
- Filter interaction → content updates
- View toggle grid ↔ list

## Mockups

Visual mockups created during brainstorming at:
`.superpowers/brainstorm/2781-1774377785/library-all-mockups.html`

Open locally: `http://localhost:63019` (3 tabs: Desktop, Empty State, Mobile)
