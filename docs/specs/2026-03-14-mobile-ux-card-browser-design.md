# Mobile UX: Card Browser & Navigation Redesign

**Date:** 2026-03-14
**Status:** Draft
**Scope:** Mobile-first UX overhaul — card focus experience, cross-domain navigation, scroll reduction

---

## Problem Statement

The current mobile experience has three pain points:

1. **Cards are too small on mobile** — grid cards at 280px in single-column don't use the screen effectively
2. **Too much scrolling** — both in lists (many items stacked vertically) and inside card content
3. **Slow cross-domain navigation** — switching between games, agents, collections, and chat requires multiple taps through the menu

## Design Goals

- Card in focus occupies nearly the entire smartphone screen
- Carousel swipe between cards without closing/reopening
- Direct card-to-card navigation via entity links
- Deck stack (history drawer) for previously visited cards
- Hybrid bottom tab bar (5 quick slots) + full domain hub
- Medium-size cards in grid/list views with less scroll
- All content reachable within 2-3 taps from any point

---

## Architecture Overview

### New Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `MeepleCardBrowser` | `ui/data-display/meeple-card-browser/` | Full-screen overlay with carousel engine |
| `MeepleCardExpanded` | `ui/data-display/meeple-card/variants/` | New card variant for focus view |
| `DeckStackDrawer` | `ui/data-display/meeple-card-browser/` | Right drawer with card visit history |
| `CardLinkBar` | `ui/data-display/meeple-card-browser/` | Tabbed related-card links below expanded card |
| `MobileBottomBar` | `layout/MobileBottomBar/` | Fixed bottom tab bar (5 domain slots) |
| `DomainHub` | `layout/DomainHub/` | Full domain grid page |

### State Management

A `CardBrowserContext` (React Context) manages:

```typescript
interface CardBrowserState {
  isOpen: boolean;
  cards: CardRef[];          // current list context
  currentIndex: number;      // active position in carousel
  history: CardRef[];        // deck stack (max 50, FIFO)
}

interface CardRef {
  id: string;
  entity: MeepleEntityType;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  color: string;             // HSL entity color
}
```

- Persistence: `sessionStorage` (resets on browser refresh)
- No duplicates: consecutive visits to the same card don't push to history
- **Carousel context reset:** When the user navigates to a different domain list and taps a card, `cards[]` resets to the new list's items. The `history[]` (deck stack) is NOT reset — it accumulates across domains, giving the user a cross-domain breadcrumb trail.
- **Carousel lazy loading:** The carousel loads all `CardRef` metadata from the current list (lightweight: id, title, image URL). The full card content (metadata, tags, links) is fetched on-demand as the user swipes, with preloading for adjacent cards (current ± 1).

### Interaction with Existing Components

- `FloatingActionBar`: hidden on mobile (`md:hidden` on FAB, `md:block` stays for desktop)
- `MobileBottomBar`: replaces FAB on mobile, hides when overlay is open
- `TopNavbar`: unchanged, overlay opens below it
- Existing pages: only add `MeepleCardBrowser` wrapper around their grids

---

## Component Specifications

### 1. MeepleCardBrowser — Overlay & Carousel

**Trigger:** Tap on any `MeepleCard` in a grid/list view.

**Layout (mobile viewport):**

```
┌──────────────────────────┐
│ TopNavbar                │  56px
├──────────────────────────┤
│                          │
│   MeepleCardExpanded     │  ~65% viewport
│                          │
│   CardLinkBar            │  ~15% viewport
│   (tabbed related cards) │
│                          │
│  [← prev]  2/12  [→]    │  carousel indicator
├──────────────────────────┤
│ BottomBar (semi-transp)  │  56px
└──────────────────────────┘
```

**Carousel behavior:**
- Horizontal swipe between cards from the same list/grid context
- `scroll-snap-type: x mandatory` for crisp snapping
- Preloads previous and next cards for fluid transitions
- Discrete `n/total` indicator at the bottom

**Open animation:**
- Origin-aware: expands from the tap point
- `scale(0.8) → scale(1)` + `opacity(0→1)`, 300ms ease-out
- Uses `framer-motion` `AnimatePresence`

**Close gestures:**
- Swipe down → `translateY(0→100vh)`, 250ms ease-in
- Tap backdrop
- Browser back button / native back gesture
- ESC key (desktop)

**Browser history integration:**
- Opening the overlay pushes a history entry via `history.pushState({ cardBrowser: true }, '')`
- Closing the overlay (any method) calls `history.back()` if the current state has `cardBrowser: true`
- The `popstate` event listener on `window` closes the overlay when the user hits back
- This ensures the back button/gesture closes the overlay instead of navigating away from the page
- Next.js App Router is not affected: no route change occurs, only `pushState` on the raw history API

**Carousel transitions:**
- `translateX` with spring physics, 200ms

### 2. MeepleCardExpanded — Full-Screen Card Variant

New `expanded` variant for MeepleCard, designed to fill the viewport without vertical scroll.

**Internal layout:**

```
┌──────────────────────────┐
│  Cover Image             │  40-45% height
│  (object-fit: cover)     │
│                          │
│  ┌─ Status badges ─────┐│  overlaid on image
│  │ owned ★ 8.2         ││
│  └─────────────────────┘│
├──────────────────────────┤
│  Title                   │  text-lg, line-clamp-2
│  Subtitle                │  text-sm, line-clamp-1
├──────────────────────────┤
│  Metadata chips          │  flex-wrap, max 4 visible
│  [2-4 players] [60min]   │
│  [Strategy] [2024]       │
├──────────────────────────┤
│  Tag strip               │  scroll-x, single row
│  [RPG] [Fantasy] [Co-op] │
├──────────────────────────┤
│  Action bar              │
│  [♡ Wishlist] [▶ Play]   │
│  [📖 See all →]          │
└──────────────────────────┘
```

**Anti-scroll principles:**
- Cover image: `max(40vh, 200px)` adaptive height
- Title: `line-clamp-2`, subtitle: `line-clamp-1`
- Metadata: max 4 chips, rest accessible in detail page
- Tags: horizontal scroll, never wraps to multiple lines
- Everything must fit in viewport without vertical scroll

**Entity type adaptation:**
- Same structure for all 9 `MeepleEntityType` values
- Entity color tints: top cover border, status badges, metadata chips
- Metadata fields change per type:
  - `game`: players, duration, complexity, year
  - `player`: games played, win rate, last active
  - `session`: players, game, date, score
  - `agent`: model, strategy, invocation count
  - `kb`: document count, last indexed, status
  - `chatSession`: agent, game, message count
  - `event`: date, location, participant count
  - `toolkit`: tool count, category
  - `custom`: user-defined metadata (passthrough)

### 3. DeckStackDrawer — Card History

**Activation:** Swipe from right screen edge, or tap stack icon in overlay.

**Layout:**

```
┌──────────────────────┐
│  History (12)      ✕  │  header
├──────────────────────┤
│  [compact card]      │  current (highlighted)
│  [compact card]      │  previous
│  [compact card]      │  ...
│  ...                 │
├──────────────────────┤
│  [Clear history]     │  footer
└──────────────────────┘
```

**Specifications:**
- Width: `80vw` max `320px` (matches `--size-mobile-sheet-width`)
- Uses existing `Sheet` component (`@/components/ui/navigation/sheet`, side: `right`)
- Each entry: `MeepleCard` compact variant (40x40 avatar + title + 1 line info)
- Current card: entity-colored border + tinted background
- Vertical scroll inside drawer if >6 entries
- Tap entry → closes drawer, browser navigates to that card
- Dark overlay on rest of screen

**History management (in `CardBrowserContext`):**
- Auto-push on every card visit (grid tap, link tap, deck tap)
- No consecutive duplicates
- Max 50 entries (FIFO)
- "Clear history" empties the array
- Persistence: `sessionStorage`

### 4. CardLinkBar — Related Card Links

**Position:** Below `MeepleCardExpanded` inside the overlay (~15% viewport).

**Structure:**

```
┌──────────────────────────────────┐
│  [Related]  [Similar]  [Coll.]   │  tab header, scroll-x
├──────────────────────────────────┤
│  [compact] [compact] [compact] → │  horizontal scroll
└──────────────────────────────────┘
```

**Tabs per entity type (all 9 `MeepleEntityType` values):**

| Entity | Tab 1 | Tab 2 | Tab 3 |
|--------|-------|-------|-------|
| `game` | Expansions/Related | Similar (by mechanic) | In your collections |
| `player` | Recent sessions | Games played | — |
| `session` | Players | Game | Related chat |
| `agent` | Recent chats | Associated games | — |
| `kb` | Related documents | Source game | — |
| `chatSession` | Agent | Discussed game | — |
| `event` | Participants | Games played | — |
| `toolkit` | Related agents | — | — |
| `custom` | Entity links only | — | — |

**Behavior:**
- Cards inside tabs: `compact` variant (40x40 + title)
- Horizontal scroll, max 10 cards per tab
- Tap a compact card → updates browser focus, pushes to deck stack history
- Tabs only shown if they have content
- If no tabs have content, CardLinkBar doesn't render; expanded card takes more space

**Data sources:**
- Related: existing `EntityLink` system (Issue #5183)
- Similar: backend vector similarity search via Qdrant (existing `IQdrantVectorStoreAdapter` + `PgVectorStoreAdapter` in KnowledgeBase). The similarity endpoint already exists for RAG retrieval; the CardLinkBar reuses the same vector search to find games with similar embeddings. If a dedicated "similar games" endpoint is needed (beyond raw vector search), it would be a thin wrapper — not a new system.
- Collections: UserLibrary queries

### 5. MobileBottomBar — Domain Tab Bar

**Visibility:** Mobile only (`md:hidden`).

```
┌──────────────────────────────────┐
│  🏠    🎲    💬    📚    👤     │  56px fixed bottom
│ Home  Games   AI  Library Prof  │
└──────────────────────────────────┘
```

**Specifications:**
- Height: `56px`
- Background: `bg-card/90 backdrop-blur-md` (glassmorphism)
- Safe area: `pb-[env(safe-area-inset-bottom)]` for notched iPhones
- Active tab: entity-colored icon + bold label
- Inactive tabs: `text-muted-foreground`
- Touch target: min `44px` (WCAG AA)
- Hides when `MeepleCardBrowser` overlay is open (`translateY` animated, 200ms)

**Slots:**
1. **Home** → DomainHub page
2. **Games** → GameManagement list
3. **AI** → KnowledgeBase (agents + chat)
4. **Library** → UserLibrary
5. **Profile** → Administration / settings

**Coexistence with FloatingActionBar:**
- Mobile (< 768px): `MobileBottomBar` visible, `FloatingActionBar` hidden
- Desktop (>= 768px): `FloatingActionBar` visible, `MobileBottomBar` hidden
- No modifications to existing `FloatingActionBar`

### 6. DomainHub — Full Domain Grid

**Reachable from:** "Home" tab in bottom bar.

**Layout:**

```
┌──────────────────────────┐
│  Welcome back, User! 👋  │  greeting
├──────────────────────────┤
│  ┌──────┐  ┌──────┐     │
│  │ 🎲   │  │ 🤖   │     │
│  │Games │  │Agents│     │  2-column grid
│  └──────┘  └──────┘     │
│  ┌──────┐  ┌──────┐     │
│  │ 💬   │  │ 📚   │     │
│  │ Chat │  │Libr. │     │
│  └──────┘  └──────┘     │
│  ...                     │
└──────────────────────────┘
```

**Specifications:**
- Grid: `grid grid-cols-2 gap-4 p-6`
- Each tile: `aspect-square`, `rounded-2xl`, background tinted with domain color (`bg-entity/10`)
- Tap → navigates to domain list page
- Large centered icon + label below
- No scroll needed for 8-10 domains (all fit in viewport)
- Future: drag & drop reorder (not in scope now)

**Domains mapped from bounded contexts:**
1. Games (GameManagement)
2. Agents (KnowledgeBase)
3. Chat (KnowledgeBase)
4. Library (UserLibrary)
5. Leaderboards (Gamification)
6. Sessions (SessionTracking)
7. Notifications (UserNotifications)
8. Settings (Administration + SystemConfiguration)

Note: Bottom bar "Profile" slot maps to the same Administration/Settings destination as the hub "Settings" tile — consistent naming as "Settings" in both places.

---

## Anti-Scroll Optimizations

### List Views (Grid/List pages)

**1. Mobile grid: 2 columns instead of 1**
- `grid-cols-2` with compact cards (~160px width)
- Reduced cover aspect ratio
- Gap: `gap-3` instead of `gap-6`
- Result: ~4-6 cards visible per screen vs 1-2 current

**2. Compact list view**
- Uses existing `list` variant with tighter spacing
- Padding: `p-2` instead of `p-3`, thumbnail: `48x48` instead of `64x64`
- ~6-8 items visible per screen

**3. View toggle in page header**
```
┌──────────────────────────┐
│  Games (42)     [▦] [≡]  │  grid/list toggle
└──────────────────────────┘
```
- Two icons: grid (default) / list
- Preference saved in `localStorage` per domain

**4. Infinite scroll**
- Initial load: 20 items
- `IntersectionObserver` for automatic loading
- Skeleton loader for next 4-6 items
- No "load more" button — fluid and automatic

### Sticky Quick Filters

```
┌──────────────────────────┐
│  Games (42)     [▦] [≡]  │
├──────────────────────────┤
│  [All] [Owned] [Wishlist]│  horizontal chip filters
│  [Played] [For Trade]    │  sticky below header
└──────────────────────────┘
```

- Horizontal scrollable chips
- Sticky below page header
- Reduce visible set without page change
- Domain-specific:
  - Games: owned/wishlist/played/for-trade
  - Agents: active/configured/all
  - Collections: mine/shared/public
  - Sessions: recent/ongoing/completed

---

## MeepleCard Refactor — File Decomposition

The current `meeple-card.tsx` is 1,322 lines. This work adds a new variant and touches the component significantly — natural time to decompose.

### New Structure

```
apps/web/src/components/ui/data-display/
├── meeple-card/
│   ├── index.ts                    → public re-export (API unchanged)
│   ├── MeepleCard.tsx              → main component (~200 lines)
│   │                                 props, variant routing, React.memo
│   ├── variants/
│   │   ├── MeepleCardGrid.tsx      → grid variant (~150 lines)
│   │   ├── MeepleCardList.tsx      → list variant (~100 lines)
│   │   ├── MeepleCardCompact.tsx   → compact variant (~80 lines)
│   │   ├── MeepleCardFeatured.tsx  → featured variant (~120 lines)
│   │   ├── MeepleCardHero.tsx      → hero variant (~100 lines)
│   │   └── MeepleCardExpanded.tsx  → NEW expanded variant (~150 lines)
│   ├── parts/
│   │   ├── CardCover.tsx           → cover image + shimmer (~80 lines)
│   │   ├── CardBadges.tsx          → status, rating, entity links (~60 lines)
│   │   ├── CardMetadata.tsx        → metadata chips (~50 lines)
│   │   ├── CardActions.tsx         → quick actions, wishlist (~80 lines)
│   │   ├── CardFlip.tsx            → existing 3D flip logic extraction (~70 lines)
│   │   └── CardTagStrip.tsx        → tag strip vertical/horizontal (~50 lines)
│   ├── hooks/
│   │   ├── useMobileInteraction.ts → tap detection, bottom sheet (~60 lines)
│   │   └── useCardAnimation.ts     → hover transforms, shimmer (~40 lines)
│   ├── types.ts                    → all interfaces/types (~80 lines)
│   └── constants.ts                → entity colors, sizing, animations (~40 lines)
├── meeple-card-mobile-tags.tsx     → stays where it is (already separate)
```

### Public API — No Breaking Changes

```tsx
// Before and after: identical import
import { MeepleCard } from '@/components/ui/data-display/meeple-card';

// Identical usage
<MeepleCard entity="game" variant="grid" title="Catan" ... />

// New variant available
<MeepleCard entity="game" variant="expanded" title="Catan" ... />
```

The `index.ts` re-export ensures all existing imports work without modification. Pages using `MeepleCard` are untouched except for adding the `MeepleCardBrowser` wrapper around their grids.

---

## Navigation Flow Summary

```
DomainHub ──tap──→ Domain List Page (grid/list)
                        │
                     tap card
                        │
                        ▼
              MeepleCardBrowser (overlay)
              ┌─────────────────────┐
              │ MeepleCardExpanded  │◄──── swipe L/R (carousel)
              │                     │
              │ CardLinkBar         │──tap──→ new card (push to history)
              │  [Related][Similar] │
              └─────────────────────┘
                   │           │
              swipe right   tap "See all"
                   │           │
                   ▼           ▼
            DeckStackDrawer   Detail Page (full route)
            [history list]
                   │
              tap entry
                   │
                   ▼
            Browser navigates to that card
```

**Tap count from any point:**
- Switch domain: 1 tap (bottom bar) or 2 taps (Home → Hub → domain)
- Focus a card: 1 tap from list
- Navigate to related card: 1 tap from CardLinkBar
- Go back in history: swipe right + 1 tap
- Full detail page: 1 tap "See all" from focused card

---

## Technical Dependencies

- `framer-motion`: already in project (used by FloatingActionBar)
- `Sheet` component: already in project (`@/components/ui/navigation/sheet`)
- `IntersectionObserver`: native browser API
- `scroll-snap-type`: native CSS
- `sessionStorage`: native browser API
- No new npm dependencies required

## Testing Strategy

- **Unit tests**: each new component (variants, hooks, context)
- **Integration tests**: CardBrowser ↔ DeckStack ↔ CardLinkBar interaction
- **E2E (Playwright)**: full flow — grid → tap → carousel → link → deck stack → close
- **Visual regression**: MeepleCardExpanded at different viewport sizes
- **Accessibility**: touch targets ≥44px, focus management in overlay, screen reader announcements

## Out of Scope

- Desktop-specific changes (this spec is mobile-first; desktop behavior stays as-is)
- DomainHub drag & drop reorder
- Search within the card browser overlay
- Offline/PWA support for card history
- Backend API changes (uses existing endpoints and EntityLinks)
