# MeepleAI Layout Redesign — Gaming Immersive

**Date**: 2026-03-24
**Status**: Approved
**Scope**: All user-facing pages (Home, Libreria, Chat, Sessioni, Scopri)

## Summary

Replace the current flat, generic layout with a Gaming Immersive layout that uses:
- Hybrid collapsible sidebar (desktop)
- MeepleCard list variant as default on mobile
- Hero banners for contextual content
- Quick actions row for fast navigation
- All changes are **in-place updates** to existing components — no v2 duplicates.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Layout style | Gaming Immersive (A) | Strong identity, visual impact, board game personality |
| Mobile content | MeepleCard list + filters (V3) | Highest info-density, 7-8 games visible without scrolling |
| Desktop sidebar | Hybrid collapsible (C) | 52px icons → 220px nav on hover. Best of both worlds. |
| Desktop content grid | 4-col MeepleCard grid | Maximizes card count with sidebar collapsed |
| Page scope | All 5 user pages | Consistent experience across the app |
| Component strategy | Update existing, no v2 | No parallel components. Replace/modify in place. |

## Design Tokens

No changes to the existing token system. The redesign uses:
- Warm palette: `--background: hsl(30, 25%, 97%)`, `--primary: hsl(25, 95%, 38%)`
- Fonts: Quicksand (headings), Inter (body), Nunito (metadata)
- Warm shadows: `--shadow-warm-sm` through `--shadow-warm-xl`
- Parchment texture: `--texture-parchment`
- Entity colors: game (orange), session (indigo), chat (blue), agent (amber), etc.

## Layout Shell

### Components Modified

| Component | File | Change |
|-----------|------|--------|
| `UserShellClient` | `src/components/layout/UserShell/UserShellClient.tsx` | Add hybrid sidebar, restructure layout grid |
| `UserTopNav` | `src/components/layout/UserShell/UserTopNav.tsx` | Add ⌘K search trigger, adjust breadcrumb |
| `UserTabBar` | `src/components/layout/UserShell/UserTabBar.tsx` | No structural change (already correct) |
| `UserDesktopSidebar` | `src/components/layout/UserShell/UserDesktopSidebar.tsx` | **Replace** with hybrid collapsible sidebar |
| `Layout` | `src/components/layout/Layout.tsx` | Adjust padding calculations for new sidebar |

### Desktop (lg+)

```
┌──────────────────────────────────────────────────┐
│ UserTopNav (48px sticky)                          │
│ Logo | Breadcrumb | ⌘K Search | Bell | Avatar    │
├──────┬───────────────────────────────────────────┤
│ Side │ Content Area (flex-1, p-20px)             │
│ bar  │                                           │
│ 52px │ Hero Banner                                │
│ ───→ │ Quick Actions Row                          │
│ 220px│ Section Title + MeepleCard Grid 4col       │
│ on   │ ...                                        │
│hover │                                            │
└──────┴───────────────────────────────────────────┘
```

**Hybrid Sidebar behavior:**
- Default: 52px width, icon-only, vertical flex column
- Icons: 34x34px, border-radius 10px, tooltip on hover
- Active icon: `bg-primary`, white icon, warm shadow glow
- Badge support: notification count (absolute top-right on icon)
- On hover: expands to 220px with 300ms cubic-bezier(0.4,0,0.2,1)
- Expanded: shows labels, counters, section dividers (Navigazione, AI Assistant, Collezioni)
- Content area does NOT reflow — sidebar overlays content when expanded
- `z-index: 30` when expanded, backdrop optional

**CSS layout strategy:**
- `UserShellClient` uses `flex` with sidebar as first child
- Collapsed sidebar: `width: 52px`, `flex-shrink: 0`, `position: relative`
- On hover: sidebar uses `position: fixed`, `left: 0`, `top: 48px` (below TopNav), `height: calc(100vh - 48px)`, `width: 220px`, `z-index: 30`
- Content area has permanent `ml-[52px]` on desktop (not flex-grow next to sidebar)
- Optional: semi-transparent backdrop `bg-black/10` behind expanded sidebar, dismisses on click

**Accessibility:**
- Sidebar: `role="navigation"`, `aria-label="Navigazione principale"`
- Collapsed icons: `aria-label` on each icon button (e.g., "Libreria, 24 giochi")
- `aria-expanded` on sidebar container tracks hover/focus state
- Keyboard: `Tab` navigates between icons; `Enter`/`Space` activates link; sidebar expands on focus-within (not just hover)
- Screen readers: sr-only labels on all icons (accessible even when collapsed)
- `prefers-reduced-motion`: sidebar transition set to `0ms`, no animation

**Sidebar sections (expanded):**
```
NAVIGAZIONE
  🏠 Dashboard
  📚 I Miei Giochi [24]
  ❤️ Wishlist [8]
  🎲 Sessioni [3]

AI ASSISTANT
  💬 Chat RAG
  📄 Documenti [12]
  🤖 Agenti [5]

COLLEZIONI
  ⭐ Preferiti
  👥 Con amici
  🎯 Strategici
```

### Mobile (<lg)

```
┌──────────────────────────┐
│ UserTopNav (48px)        │
│ Logo | Bell | Avatar     │
├──────────────────────────┤
│ Content (p-12px pb-80px) │
│                          │
│ Hero (compact)           │
│ Quick Actions (scroll)   │
│ Filters (chips)          │
│ MeepleCard list variant  │
│ ...                      │
├──────────────────────────┤
│ UserTabBar (64px fixed)  │
│ Home | Libreria | Gioca  │
│         | Chat           │
└──────────────────────────┘
```

- No sidebar on mobile — TabBar handles all navigation
- Breadcrumb hidden on mobile — page title rendered as `<h1>` in each page's content area (PageHeader component)
- Safe area bottom: `env(safe-area-inset-bottom)`

### Preserved Shell Components

The following existing components in `UserShellClient` are **preserved unchanged**:
- `ContextBar` — rendered between TopNav and main content
- `BackToSessionFAB` — floating button below TabBar
- `DashboardEngineProvider` — wraps all children for stateful dashboard features

## Page Designs

### 1. Home / Dashboard

**File**: `src/app/(authenticated)/dashboard/page.tsx` + `DashboardClient.tsx`

**Desktop:**
- Hero banner: gradient `hsla(25,80%,45%,0.15) → hsla(271,70%,55%,0.08) → hsla(220,70%,55%,0.08)`, border-radius 14px, shows next session/event, badge + CTA
- Quick actions row: 5 icon buttons (Libreria, Nuova partita, Chat AI, Regole, Scopri), 44x44px, horizontal flex, `bg-card border-border shadow-warm-sm`
- "Giocati di recente": horizontal scroll, MeepleCard grid variant 160px width, scroll-snap
- "La Tua Libreria": MeepleCard grid 4col, cover 7:10, full features (overlay, action strip, rating, footer, mana pips)
- "Popolari questa settimana": horizontal scroll, MeepleCard grid with ranking badge (#1, #2, #3) in cover-label slot

**Mobile:**
- Hero compact: reduced padding, smaller font, CTA touch-friendly
- Quick actions: horizontal scroll, 44x44 touch targets
- "Giocati di recente": carousel MeepleCard 120px, aspect 1:1
- "La Tua Libreria": MeepleCard **list variant** + filter chips (Tutti, Recenti, Rating ↓, players, duration)
- "Popolari": carousel horizontal

**Loading/empty/error states:**
- Hero: skeleton with gradient pulse while loading session data
- Card grids/lists: `MeepleCardSkeleton` (already exists) × expected count
- Empty library: EmptyState component with CTA "Aggiungi il tuo primo gioco"
- Error: toast notification + retry button, content area shows last cached data via React Query

### 2. Libreria

**File**: `src/app/(authenticated)/library/page.tsx`

**Desktop:**
- PageHeader: title + count + "Aggiungi gioco" button
- Filter chips row: Tutti, Recenti, Più giocati, Rating ↓, player count, duration, complexity
- View toggle: grid/list icons top-right
- Default: MeepleCard grid 4col with cover 7:10, full overlay, action strip, rating, footer, mana pips
- List mode: MeepleCard list variant

**Mobile:**
- Default: MeepleCard **list variant** (info-dense)
- Filter chips scrollable at top
- Toggle to grid 2col available
- Pull-to-refresh and infinite scroll are **deferred** — not in this redesign scope. Standard pagination or "load more" button for now.

### 3. Chat AI

**Files**: `src/app/(chat)/chat/layout.tsx` (2-panel layout), `src/app/(chat)/chat/page.tsx` (conversation list), `src/app/(chat)/chat/[threadId]/page.tsx` (active chat), `src/app/(chat)/chat/new/page.tsx`

**Desktop:**
- 2-panel layout: conversation list left (MeepleCard compact/list for chatSession, entity color blue) + active chat right
- Chat area: message bubbles, markdown, code blocks
- Collapsible context sidebar (right): KB docs and linked games as MeepleCard compact

**Mobile:**
- Conversation list full-screen (MeepleCard list variant, chat blue entity color)
- Tap → full-screen chat with back button in TopNav
- Context: accessible via bottom sheet or swipe gesture

### 4. Sessioni (Gioca)

**File**: `src/app/(authenticated)/sessions/page.tsx`

**Desktop:**
- Active sessions: MeepleCard featured variant (16:9 cover) at top
- Past sessions: MeepleCard grid 3-4col with status badge, score table, turn sequence
- CTA "Nuova sessione" prominent

**Mobile:**
- Active session: MeepleCard hero variant full-width
- Past sessions: MeepleCard list variant with status + compact score
- FAB "Nuova sessione" bottom-right

### 5. Scopri / Catalogo

**Files**: `src/app/(public)/games/page.tsx` (main catalog), `src/app/(public)/games/catalog/page.tsx` (shared catalog), `src/app/(public)/games/components/` (catalog components)

**Desktop:**
- Prominent search bar at top
- Category tabs/chips: Strategici, Party, Cooperativi, Solo, Nuove uscite
- MeepleCard grid 4col from shared catalog, cover overlay with category label
- BGG import as secondary action

**Mobile:**
- Sticky search below TopNav
- Category carousel horizontal
- MeepleCard list variant default, toggle to grid 2col
- BGG import in action menu

## MeepleCard Usage Matrix

| Page | Desktop Variant | Mobile Variant | Entity |
|------|----------------|----------------|--------|
| Home — Recenti | grid (scroll row) | grid (carousel) | game |
| Home — Libreria | grid 4col | **list** | game |
| Home — Trending | grid (scroll row) | grid (carousel) | game |
| Libreria | grid 4col (toggle list) | **list** (toggle grid 2col) | game |
| Chat — list | compact/list | **list** | chatSession |
| Sessioni — active | featured | hero | session |
| Sessioni — past | grid 3-4col | **list** | session |
| Scopri | grid 4col | **list** (toggle grid 2col) | game |

## New Components

| Component | Target Path | Purpose |
|-----------|-------------|---------|
| `HybridSidebar` | `src/components/layout/UserShell/HybridSidebar.tsx` | Replaces `UserDesktopSidebar`. Collapsible 52px→220px. |
| `QuickActionsRow` | `src/components/dashboard/QuickActionsRow.tsx` | Horizontal icon buttons for mobile/desktop home |
| `HeroBanner` | `src/components/dashboard/HeroBanner.tsx` | Replaces existing `HeroZone` (`dashboard/v2/HeroZone.tsx`, `dashboard/zones/HeroZone.tsx`, `dashboard/hero-zone.tsx`). Contextual hero with gradient, badge, CTA. |

`UserDesktopSidebar` is removed and replaced by `HybridSidebar`.

**Existing components reused (NOT new):**
- `ViewToggle` — already exists at `src/app/(public)/games/components/ViewToggle.tsx`. Move to `src/components/ui/ViewToggle.tsx` for shared use across Libreria/Scopri.
- `CommandPalette` — already exists at `src/components/layout/CommandPalette.tsx`. The ⌘K trigger in TopNav opens this existing component.

**Dashboard v2 cleanup:** `HeroBanner` replaces the existing `HeroZone` and `QuickStats` components under `dashboard/v2/`, `dashboard/zones/`, and `dashboard/`. These are removed after `HeroBanner` is implemented. `QuickActionsRow` replaces `QuickStats`.

### Prop Interfaces

```typescript
interface HybridSidebarProps {
  className?: string;
}
// Internal state: isExpanded (hover-driven), activeItem (from pathname)
// Sidebar items and counters fetched via existing React Query hooks
// Counter data: useUserLibrary().count, useWishlist().count, useSessions().count,
//               useDocuments().count, useAgents().count

interface HeroBannerProps {
  title: string;
  subtitle: string;
  badge?: { text: string; variant: 'live' | 'featured' | 'new' };
  cta?: { label: string; href: string; icon?: React.ReactNode };
  gradient?: 'primary' | 'session' | 'chat'; // defaults to 'primary'
  className?: string;
}

interface QuickActionsRowProps {
  actions: Array<{
    icon: React.ReactNode;
    label: string;
    href: string;
    entityColor?: string; // HSL border accent
  }>;
  className?: string;
}
```

## Components NOT Changed

- `MeepleCard` and all its variants — used as-is (grid, list, compact, featured, hero)
- `MeepleCard` styles, entity colors, animation system — unchanged
- `AdminShell` and admin pages — out of scope
- `UserTabBar` — structurally unchanged (already has glassmorphism, entity colors, 4 tabs)
- Design tokens — unchanged

## Responsive Breakpoints

| Breakpoint | Behavior |
|------------|----------|
| < 640px (sm, mobile) | TabBar, no sidebar, list variant default, 2col grid optional |
| 640-767px (sm-md, large phone) | TabBar, no sidebar, 2col grid, list variant available |
| 768-1023px (md-lg, tablet) | TabBar, no sidebar, 3col grid, list variant available |
| ≥ 1024px (lg+, desktop) | Hybrid sidebar, no TabBar, 4col grid (min card width ~220px, fallback to 3col at 1024-1100px if content area < 900px), hero banner full |

## Implementation Order

Suggested build sequence (each is independently shippable):

0. **Shell restructure** — Refactor `UserShellClient` layout from flex-child sidebar to fixed-offset model (`ml-[52px]` on desktop). Update `Layout.tsx` padding calculations. This is the prerequisite for step 1 and affects all pages. Ship as standalone PR for safe rollback.
1. **HybridSidebar** — Replace `UserDesktopSidebar` with `HybridSidebar.tsx`. Wire into `UserShellClient`. Add ⌘K trigger to `UserTopNav` (opens existing `CommandPalette`).
2. **Home/Dashboard** — Replace `HeroZone`/`QuickStats` with `HeroBanner`/`QuickActionsRow`. Section layout with scroll rows and grid. Clean up old dashboard v2 components.
3. **Libreria** — filter chips, move `ViewToggle` to shared location, mobile list default
4. **Chat** — 2-panel desktop layout in `(chat)/chat/layout.tsx`, full-screen mobile
5. **Sessioni** — featured/hero active session, list past sessions
6. **Scopri** — search, categories, catalog grid/list

## Mockups Reference

Visual mockups created during brainstorming are in:
`.superpowers/brainstorm/2264-1774350005/`

| File | Content |
|------|---------|
| `layout-with-meeplecard.html` | Desktop comparison A/B/C with real MeepleCard |
| `gaming-mobile.html` | Mobile mockup with notes |
| `gaming-mobile-variations.html` | V1/V2/V3 mobile variations |
| `sidebar-comparison.html` | Sidebar A/B/C with interactive hybrid |
