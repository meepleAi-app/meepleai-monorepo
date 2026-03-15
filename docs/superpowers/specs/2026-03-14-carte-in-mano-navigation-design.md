# Carte in Mano — Navigation & Layout Redesign

**Date**: 2026-03-14
**Status**: Draft
**Scope**: Unified shell for User + Admin, card-based navigation, removal of old layout

## Problem Statement

The current app has two separate layout systems (AppShell for users, AdminShell for admins) with significant code duplication. The navigation relies on a traditional sidebar + top nav pattern that doesn't leverage the card-based entity model central to MeepleAI.

Existing card navigation components are production-ready and integrated:
- `CardStackPanel` — integrated in AppShellClient and LayoutShell, positioned on the **right** side as a FAB-triggered panel
- `useNavigationTrail` — active hook using sessionStorage + useSyncExternalStore with custom events
- `useHandContext` — working Zustand store with swipe/focus support, max 7 cards
- `CardFocusLayout` + `HandStack` — built and tested but not yet wired into layouts

This redesign **repositions** the card stack from a secondary right-side panel to the **primary left-side navigation**, replaces the traditional sidebar, and unifies both shells.

## Design Overview

Replace both AppShell and AdminShell with a single `UnifiedShell` that uses a **"Carte in Mano" (Cards in Hand)** metaphor: navigating to a page is like drawing a card from a deck. Cards stack on the left side of the main area, growing bottom-to-top. The bottom nav acts as contextual actions and card-drawing triggers.

### Core Concepts

- **Drawing a card**: Visiting a page adds it as a card to your hand
- **Card stack**: Left side of main area (moved from current right-side panel), cards grow bottom→up
- **Pinned cards**: Main sections (Library, Discover, Chat, Sessions) are always present, protected from auto-removal
- **Hand limit**: 10 cards max — 4 pinned + 6 dynamic slots. Oldest non-pinned card removed (FIFO) when full
- **Admin mode**: Toggle in top nav switches card stack → tabbed sidebar for operational tools
- **Contextual bottom nav**: Shows section actions when a card is in focus, main sections when no focus

### Design Decisions

| Decision | Rationale |
|----------|-----------|
| Card stack on LEFT (not right) | Left = primary navigation position; right was secondary/overlay. Deliberate repositioning. |
| Max 10 cards (4 pinned + 6 dynamic) | 4 default pinned sections leave 6 usable slots — enough to explore without clutter |
| sessionStorage for cards, localStorage for pins | New tab = fresh hand (intentional), but pin preferences persist across sessions |
| Multi-card deferred to Phase 2 | Next.js App Router cannot render two independent pages simultaneously — needs parallel routes PoC first |
| Breadcrumbs removed | Card title in top nav + card stack provides sufficient context; deeply nested admin pages use sidebar tabs |

## Layout Structure

### Top Nav (L1)

Fixed top bar, minimal:

```
┌──────────────────────────────────────────────────────┐
│  🎲 Logo  │  🃏 Card Title + Entity Icon  │ [User/Admin] 🔍 🔔 👤 │
└──────────────────────────────────────────────────────┘
```

- **Left**: Logo/brand
- **Center**: Title of focused card with entity-colored icon (e.g. 🟠 "Catan"). When no card focused, shows app name
- **Right**: Admin toggle (hidden for non-admin users), search, notifications, avatar menu
- Height: `h-14` (56px), sticky `top-0 z-40`
- **Admin toggle visibility**: Only rendered if user has admin/editor role (checked via session claims). Non-admin users see no toggle.

### Card Stack (Left — User Context)

Vertical stack of mini-cards on the left side of the main area, growing bottom→up:

```
┌────────────────────────────────────────────┐
│ TopNav                                     │
├──────┬─────────────────────────────────────┤
│      │                                     │
│      │         Main Area                   │
│      │      (Focused Card = page)          │
│      │                                     │
│ [C6] │                                     │
│ [C5] │                                     │
│ [C4] │                                     │
│ [C3] │  ← Dynamic cards grow upward       │
│ [C2] │                                     │
│ [C1] │                                     │
│ ──── │  ← Visual separator                │
│ [📚] │  ← Pinned cards at bottom          │
│ [🎮] │                                     │
│ [💬] │                                     │
│ [📋] │                                     │
├──────┴─────────────────────────────────────┤
│ Bottom Nav (contextual actions)            │
└────────────────────────────────────────────┘
```

#### Three Card Detail Levels

| Level | Name | Width | Content | When |
|-------|------|-------|---------|------|
| Mini | Stack collapsed | ~56px | Entity icon + step number | Default on mobile, collapsed desktop |
| Card | Stack expanded | ~180px | Icon + title + subtitle + entity badge | Expanded desktop, mobile swipe-expand |
| Full | Main area | Remaining | Complete page content | Card in focus |

#### Card Stack Behavior

- **Max hand size**: 10 cards total (pinned + dynamic)
- **Default pinned cards**: Library, Discover, Chat, Sessions (4 cards) — leaves 6 dynamic slots
- **Pinned card rules**:
  - Pinned cards are always visible at bottom of stack, separated visually from dynamic cards
  - Count toward the 10-card limit
  - Protected from FIFO eviction
  - User can unpin manually → card becomes dynamic (subject to FIFO). The section remains accessible via bottom nav "draw card" action
  - User can pin any dynamic card → if hand is full, oldest non-pinned card is evicted to make room
- **FIFO eviction**: When hand is full and a new card is drawn, oldest non-pinned card removed
- **Manual close**: X button or swipe to remove a card (both pinned and dynamic — unpin+remove in one gesture for pinned)
- **Click to focus**: Clicking a card in the stack makes it the focused card, main area navigates to its URL
- **Duplicate prevention**: Drawing a card already in hand focuses it instead of adding a duplicate
- **DOM ordering**: Cards rendered in reverse order (newest at top of DOM, flex-end alignment) to achieve visual bottom→up growth

### Tabbed Sidebar (Left — Admin Context)

When admin toggle is active, the left panel switches from card stack to a traditional tabbed sidebar:

```
┌────────────────────────────────────────────┐
│ TopNav                         [Admin] 🔍 👤│
├──────┬─────────────────────────────────────┤
│ Over │                                     │
│ view │     Admin Page Content              │
│──────│                                     │
│ Cont │                                     │
│ ent  │                                     │
│──────│                                     │
│ AI   │                                     │
│──────│                                     │
│Users │                                     │
│──────│                                     │
│Syste │                                     │
│ m    │                                     │
│──────│                                     │
│Analy │                                     │
│tics │                                     │
├──────┴─────────────────────────────────────┤
│ Bottom Nav (admin section actions)         │
└────────────────────────────────────────────┘
```

- Same 6 sections as current admin: Overview, Content, AI, Users, System, Analytics
- Tabs are vertical, each tab shows its sub-items when active
- Admin entities are not cards (services, logs, config) — sidebar tabs are the right pattern
- Configuration reuses `admin-dashboard-navigation.ts` structure
- **Context switch behavior**: Toggling to admin mode navigates to `/admin/overview` (or last visited admin page, stored in localStorage). Toggling back to user mode navigates to the last focused card's URL (or `/library` as fallback)

### Bottom Nav (L3)

Contextual action bar at the bottom, content changes based on state:

**No card in focus** (or freshly logged in):
```
┌──────────────────────────────────────────────┐
│  📚 Library  │  🎮 Discover  │  💬 Chat  │  📋 Sessions  │
└──────────────────────────────────────────────┘
```
Tapping a section = "draw card" → navigates to that section's search/browse page and adds it to hand.

**Card in focus** (e.g. a game):
```
┌──────────────────────────────────────────────┐
│  🎲 New Session  │  ⭐ Wishlist  │  🤖 Chat AI  │  📄 PDF  │
└──────────────────────────────────────────────┘
```
Actions are specific to the focused card's entity type.

**Admin mode**:
```
┌──────────────────────────────────────────────┐
│  Actions specific to active admin section    │
└──────────────────────────────────────────────┘
```
Admin bottom nav actions are derived from existing `NavConfig` definitions for each admin section.

### Multi-Card View (Large Screens) — DEFERRED TO PHASE 2

> **Architectural constraint**: Next.js App Router renders one `page.tsx` per route segment. Rendering two independent pages (e.g. `/library/game-123` and `/sessions/456`) simultaneously requires one of:
> - **Parallel routes** (`@slot` convention) — needs refactoring of route structure
> - **Content caching** — render one page, cache its DOM, show cached snapshot beside live page
> - **Portals/iframes** — heavyweight, fragile
>
> This feature is deferred to a separate Phase 2 after the core shell lands. A proof-of-concept will evaluate the parallel routes approach first.

For Phase 1, all screens use **single focus** mode regardless of width. The card stack on wide screens (≥ 1280px) simply has more vertical space.

### Mobile Layout (< 768px)

Same paradigm, compact:

```
┌──────────────────────────┐
│ TopNav (Logo + Title + 👤)│
├────┬─────────────────────┤
│    │                     │
│ 📚 │   Main Area         │
│ 🎮 │   (Focused Card)    │
│ 💬 │                     │
│ 📋 │                     │
│ ── │                     │
│ C1 │                     │
│ C2 │                     │
│    │                     │
├────┴─────────────────────┤
│ Bottom Nav (contextual)  │
└──────────────────────────┘
```

- **Mini-stack**: 56px wide, entity icons only (Mini level)
- **Expandable**: Tap expand button or swipe right to expand to Card level (~180px overlay), showing titles and subtitles
- **Bottom nav**: Same contextual behavior as desktop
- **Admin mode**: Mini-stack replaced by compact tab icons for admin sections

## Component Architecture

### New Components

| Component | Purpose |
|-----------|---------|
| `UnifiedShell` | RSC wrapper — reads sidebar cookie, session role, renders UnifiedShellClient |
| `UnifiedShellClient` | Client component with NavigationProvider, CardHand, ErrorBoundary wrappers |
| `CardStack` | Left panel, renders mini/card level cards bottom→up with flex-end alignment |
| `CardStackItem` | Single card in the stack, supports Mini and Card detail levels |
| `ContextualBottomNav` | Bottom nav with contextual actions derived from focused card entity |
| `UnifiedTopNav` | Minimal top nav with card title + admin toggle + utilities |
| `AdminTabSidebar` | Tabbed sidebar for admin context, reuses admin-dashboard-navigation.ts |
| `AdminToggle` | User ↔ Admin context switch button (only rendered for admin role users) |

### Reused Components (with modifications)

| Component | Changes |
|-----------|---------|
| `MeepleCard` | No changes — used for Full level rendering in main area |
| `admin-dashboard-navigation.ts` | No changes — reused for AdminTabSidebar section/item config |
| `CommandPalette` | No changes — still triggered from UnifiedTopNav search button |

### Components to Remove

| Component | File(s) | Replacement |
|-----------|---------|-------------|
| `AppShell` + `AppShellClient` | `layout/AppShell/` | `UnifiedShell` + `UnifiedShellClient` |
| `Sidebar` + `SidebarContextNav` + `SidebarToggle` + `SidebarNav` + `SidebarUser` | `layout/Sidebar/` | `CardStack` (user) / `AdminTabSidebar` (admin) |
| `AdminShell` | `admin/layout/AdminShell.tsx` | `UnifiedShell` |
| `AdminTopNav` | `admin/layout/AdminTopNav.tsx` | `UnifiedTopNav` |
| `AdminContextualSidebar` | `admin/layout/AdminContextualSidebar.tsx` | `AdminTabSidebar` |
| `AdminMobileNav` | `admin/layout/AdminMobileNav.tsx` | Mobile card stack / admin tab icons |
| `AdminMobileTabBar` | `admin/layout/AdminMobileTabBar.tsx` | `ContextualBottomNav` |
| `MobileNavDrawer` | `layout/MobileNavDrawer.tsx` | Card stack replaces drawer |
| `FloatingActionBar` | `layout/FloatingActionBar/` | `ContextualBottomNav` |
| `AdaptiveBottomBar` | `layout/AdaptiveBottomBar/` | `ContextualBottomNav` |
| `NavActionBar` + `ActionBarButton` + `ActionBarOverflow` | `layout/ActionBar/` | `ContextualBottomNav` |
| `DesktopBreadcrumb` + `MobileBreadcrumb` | `layout/Breadcrumb/` | Card title in UnifiedTopNav |
| `CardStackPanel` | `ui/navigation/card-stack-panel.tsx` | `CardStack` (repositioned left, integrated) |

### Hooks to Remove/Replace

| Hook | Replacement |
|------|-------------|
| `useSetNavConfig` (47 files) | `useBottomNavActions` — see migration example below |
| `useHandContext` | `useCardHand` (superset) |
| `useNavigationTrail` | `useCardHand` (merged — sessionStorage persistence kept, useSyncExternalStore pattern preserved for cross-tab reactivity) |
| `useSidebarState` | Removed — card stack collapse/expand uses `useCardHand.expandedStack` |

### Cross-Cutting Features Preserved

| Feature | Current Location | New Location |
|---------|-----------------|-------------|
| `ImpersonationBanner` | AppShellClient | UnifiedShellClient (above UnifiedTopNav) |
| `OnboardingReminderBanner` | AppShellClient | UnifiedShellClient (below UnifiedTopNav) |
| `ErrorBoundary` wrappers | AppShellClient (per section) | UnifiedShellClient (per section: TopNav, CardStack, MainArea, BottomNav) |
| Cookie-based initial state | AppShell RSC reads sidebar cookie | UnifiedShell RSC reads card stack collapsed cookie |
| Safe area insets | AdaptiveBottomBar | ContextualBottomNav (`pb-[env(safe-area-inset-bottom)]`) |
| Skip-to-content link | AppShellClient | UnifiedShellClient |

## State Management

### `useCardHand` (Zustand store)

Merges `useHandContext` + `useNavigationTrail` into a single store:

```typescript
interface HandCard {
  id: string;                    // Entity UUID (primary key for dedup)
  entity: MeepleEntityType;
  title: string;
  href: string;                  // Navigation URL
  subtitle?: string;
  imageUrl?: string;
}

interface CardHandState {
  // State
  cards: HandCard[];
  focusedIdx: number;
  pinnedIds: Set<string>;
  maxHandSize: number;           // Default: 10
  context: 'user' | 'admin';
  expandedStack: boolean;
  highlightEntity: MeepleEntityType | null;  // From NavigationTrail

  // Card actions
  drawCard: (card: HandCard) => void;        // Add to hand (FIFO if full)
  discardCard: (id: string) => void;         // Remove manually
  focusCard: (index: number) => void;        // Set focused card
  focusByHref: (href: string) => void;       // Focus card by URL match

  // Pin actions
  pinCard: (id: string) => void;
  unpinCard: (id: string) => void;

  // Navigation
  swipeNext: () => void;                     // Focus next card (preserved from useHandContext)
  swipePrev: () => void;                     // Focus previous card

  // Context
  toggleContext: () => void;                 // Switch user ↔ admin
  toggleExpandStack: () => void;

  // Highlight (preserved from NavigationTrail)
  setHighlightEntity: (entity: MeepleEntityType | null) => void;

  // Cleanup
  clear: () => void;                         // Clear non-pinned cards
}
```

**Card identity**: `id` (entity UUID) is the primary key for deduplication. `href` is used for URL-based lookup (`focusByHref`) when the router detects a navigation.

**Persistence**:
- `cards` + `focusedIdx` → `sessionStorage` (new tab = fresh hand)
- `pinnedIds` + `expandedStack` → `localStorage` (persists across sessions)
- Cross-tab reactivity: `window.dispatchEvent(new Event('card-hand-change'))` + `useSyncExternalStore` for the sessionStorage portion

### `useBottomNavActions` (derived hook)

Returns contextual actions based on focused card's entity type:

```typescript
interface BottomNavAction {
  id: string;
  label: string;
  icon: LucideIcon;
  onClick: () => void;
  variant?: 'primary' | 'secondary' | 'ghost' | 'destructive';
  badge?: number | string;
  hidden?: boolean;
  disabled?: boolean;
}

function useBottomNavActions(): BottomNavAction[];
```

### NavConfig Migration Pattern

**Before** (current pattern — 47 files):
```typescript
// library/NavConfig.tsx
export function LibraryNavConfig() {
  const setNavConfig = useSetNavConfig();
  useEffect(() => {
    setNavConfig({
      miniNav: [
        { id: 'collection', label: 'Collection', href: '/library', icon: BookOpen },
        { id: 'wishlist', label: 'Wishlist', href: '/library?tab=wishlist', icon: Star },
      ],
      actionBar: [
        { id: 'add-game', label: 'Aggiungi gioco', icon: Plus, variant: 'primary', onClick: () => ... },
      ],
    });
  }, [setNavConfig]);
  return null;
}
```

**After** (new pattern):
```typescript
// config/bottom-nav-actions.ts — centralized entity action registry
export const ENTITY_ACTIONS: Record<MeepleEntityType, BottomNavActionDef[]> = {
  game: [
    { id: 'new-session', label: 'Nuova Sessione', icon: Gamepad2, variant: 'primary' },
    { id: 'wishlist', label: 'Wishlist', icon: Star },
    { id: 'chat-ai', label: 'Chat AI', icon: Bot },
    { id: 'upload-pdf', label: 'Carica PDF', icon: FileText },
  ],
  // ... other entity types
};

export const DEFAULT_ACTIONS: BottomNavActionDef[] = [
  { id: 'library', label: 'Library', icon: BookOpen, drawCard: { entity: 'game', href: '/library' } },
  { id: 'discover', label: 'Discover', icon: Compass, drawCard: { entity: 'game', href: '/discover' } },
  { id: 'chat', label: 'Chat', icon: MessageSquare, drawCard: { entity: 'chatSession', href: '/chat' } },
  { id: 'sessions', label: 'Sessions', icon: ClipboardList, drawCard: { entity: 'session', href: '/sessions' } },
];
```

Per-page NavConfig components are **deleted**. The `useBottomNavActions` hook reads from the centralized registry + the focused card's entity type. No per-page configuration needed.

**Admin NavConfig components** (5 hub pages) are also deleted — their action definitions migrate to the admin section config in `admin-dashboard-navigation.ts` with an `actions` field per section.

### Entity Action Mappings

| Entity | Actions |
|--------|---------|
| `game` | New Session, Add to Wishlist, Chat AI, Upload PDF |
| `session` | Add Notes, Score, End Session |
| `agent` | Chat, Edit, View KB Cards |
| `kb` | View Documents, Reindex, Edit |
| `player` | View Sessions, Invite, Stats |
| `chatSession` | Continue Chat, Export, Share |
| `toolkit` | View Tools, Link Games |
| `event` | View Details, RSVP, Share |
| `custom` | View Details |

**No card focused** (default): Library, Discover, Chat, Sessions — each as "draw card" action leading to search/browse.

## Route Integration

### User Routes — Card Drawing

Pages register themselves as cards when mounted:

```typescript
// In each page component (e.g. game detail)
const { drawCard } = useCardHand();

useEffect(() => {
  drawCard({
    id: game.id,
    entity: 'game',
    title: game.title,
    href: `/library/${game.id}`,
    subtitle: game.publisher,
    imageUrl: game.imageUrl,
  });
}, [game.id]);
```

Section index pages (e.g. `/library`) draw a section card:
```typescript
useEffect(() => {
  drawCard({
    id: 'section-library',
    entity: 'game',
    title: 'Library',
    href: '/library',
  });
}, []);
```

### Admin Routes

Admin routes don't draw cards. The admin sidebar tabs handle navigation directly. The `UnifiedShell` detects `/admin/*` routes via `usePathname()` and shows the tabbed sidebar instead of the card stack.

### Chat SSE Consideration

Chat sessions maintain SSE connections. When a chat card loses focus (user clicks another card), the chat component unmounts (standard Next.js behavior). The SSE connection closes, but chat history is persisted server-side. When the user refocuses the chat card, the component remounts and reconnects. This is the same behavior as navigating away from chat today — no regression.

### Layout File Changes

```
app/
├── layout.tsx (root — no changes)
├── (auth)/layout.tsx (no changes — minimal auth layout)
├── (authenticated)/layout.tsx → uses UnifiedShell
├── (chat)/layout.tsx → uses UnifiedShell (same shell, chat cards drawn)
├── (public)/layout.tsx (no changes — public layout)
├── (dev)/layout.tsx (no changes)
├── admin/(dashboard)/layout.tsx → uses UnifiedShell with admin context
```

## Responsive Breakpoints

| Breakpoint | Card Stack | Main Area | Bottom Nav |
|------------|-----------|-----------|------------|
| < 768px (mobile) | Mini (56px), expandable to 180px overlay | Single focus | Contextual, single row |
| 768px–1279px (tablet/desktop) | Card level (180px), collapsible to Mini | Single focus | Contextual, single row |
| ≥ 1280px (wide) | Card level (180px) | Single focus (multi-card deferred) | Contextual, single row |

## Accessibility

### Keyboard Navigation
- `Alt+1..9`: Focus card by position in stack
- `Alt+↑/↓`: Navigate between cards in stack
- `Escape`: Collapse expanded stack (mobile)
- `Tab`: Standard focus traversal through stack items

### ARIA
- Card stack: `role="navigation"` + `aria-label="Card hand"`
- Each card: `role="link"` + `aria-current="page"` when focused
- Bottom nav: `role="toolbar"` + `aria-label="Page actions"`
- Admin toggle: `role="switch"` + `aria-checked`

### Screen Reader Announcements
- Card drawn: `aria-live="polite"` region announces "Card added: {title}"
- Card discarded: announces "Card removed: {title}"
- Focus change: announces "Now viewing: {title}"
- FIFO eviction: announces "Card removed to make room: {title}"

### Focus Management
- Drawing a card: focus moves to main area (new content)
- Discarding focused card: focus moves to next card in stack (or previous if last)
- Admin toggle: focus moves to first item in new left panel

## Migration Strategy

**Approach**: Big Bang build, phased PRs for integration and cleanup.

### PR 1: Foundation (new components + store)
- Create `useCardHand` store
- Create `useBottomNavActions` hook
- Create centralized entity action registry (`config/bottom-nav-actions.ts`)
- Build `UnifiedTopNav`, `CardStack`, `CardStackItem`, `ContextualBottomNav`, `AdminTabSidebar`, `AdminToggle`
- Build `UnifiedShell` + `UnifiedShellClient`
- Unit tests for all new components and hooks
- **No old code deleted yet** — both systems coexist temporarily

### PR 2: User Layout Swap
- Replace `(authenticated)/layout.tsx` to use UnifiedShell
- Replace `(chat)/layout.tsx` to use UnifiedShell
- Add `drawCard` calls to all user page components
- Delete per-page NavConfig components for user sections
- E2E verification of all user routes

### PR 3: Admin Layout Swap
- Replace `admin/(dashboard)/layout.tsx` to use UnifiedShell with admin context
- Migrate admin NavConfig actions to `admin-dashboard-navigation.ts`
- Delete per-page NavConfig components for admin sections
- E2E verification of all admin routes

### PR 4: Cleanup
- Delete old components: AppShell, AppShellClient, Sidebar/*, AdminShell, AdminTopNav, AdminContextualSidebar, AdminMobileNav, AdminMobileTabBar, MobileNavDrawer, FloatingActionBar, AdaptiveBottomBar, ActionBar/*, Breadcrumb/*, CardStackPanel (old right-side)
- Delete old hooks: useSetNavConfig, useHandContext, useNavigationTrail, useSidebarState
- Delete old tests for all removed components
- Clean up unused imports, types, and context files
- Verify no dead imports remain

### PR 5: Test Suite
- Component tests for UnifiedShell in both user and admin contexts
- E2E tests: card draw/focus/discard/pin, admin toggle, mobile expand/collapse
- Responsive tests at mobile/tablet/desktop breakpoints
- Accessibility audit (keyboard nav, screen reader)
- Performance: shell hydration < 100ms (Time to Interactive), card transitions < 200ms (animation complete)

### Phase 2 (Future — separate epic)
- Multi-card side-by-side view on xl+ screens
- Requires parallel routes PoC or content caching approach
- Not blocked by Phase 1

## Design Tokens

Consistent with existing glassmorphic design system:

- Card stack background: `bg-background/50 backdrop-blur-sm`
- Card item (Mini): entity-colored left border via CSS custom property `--card-hsl`, `bg-card/60`
- Card item (focused): `bg-[hsl(var(--card-hsl)/0.1)]`, `border-[hsl(var(--card-hsl)/0.25)]`, `shadow-sm`
- Card item (pinned indicator): small pin icon, `text-muted-foreground/50`
- Visual separator (pinned vs dynamic): `border-t border-border/20` with 8px margin
- Bottom nav: `bg-background/95 backdrop-blur-xl`, `border-t border-border/40`
- Admin toggle inactive: `text-muted-foreground`
- Admin toggle active: `bg-destructive/10 text-destructive border-destructive/30` (red tint for admin awareness)
- Fonts: `font-quicksand` (headings/card titles), `font-nunito` (body/metadata)

## Success Criteria

- Single UnifiedShell component serves both user and admin contexts
- Card-based navigation works on all breakpoints (mobile, tablet, desktop)
- Card drawing, focusing, pinning, FIFO eviction, manual discard all work correctly
- Admin toggle switches left panel seamlessly (card stack ↔ tabbed sidebar)
- Bottom nav shows correct contextual actions per entity type
- All old layout components, hooks, and tests removed (zero dead code)
- No visual regression on existing pages
- Accessibility: keyboard navigation, ARIA roles, screen reader announcements
- Performance: shell hydration < 100ms TTI, card transitions < 200ms
- ImpersonationBanner, OnboardingBanner, ErrorBoundary, safe-area-insets preserved
