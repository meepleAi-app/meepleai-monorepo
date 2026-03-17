# Mobile UX Overhaul — Design Spec

**Date**: 2026-03-17
**Status**: Approved
**Branch**: `frontend-dev`

## Problem Statement

The current mobile layout on smartphone screens is cluttered with 6 simultaneous navigation layers consuming ~288px (43%) of a 667px iPhone SE screen, leaving only 57% for actual content.

**Current navigation stack (top → bottom):**
1. UnifiedTopNav (56px) — logo, focused card title, search/bell/avatar
2. MobileBreadcrumb (36px) — back arrow + breadcrumb trail
3. HandDrawer (80px) — horizontal card carousel
4. *Content area* (~379px)
5. ContextualBottomNav (44px) — page-specific actions
6. MobileTabBar (72px) — 5 navigation tabs
7. SmartFAB (56px overlay) — floating action button

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Approach | A (Unified ActionBar) + B (Collapsing Header) | Max impact, min risk |
| Contextual actions | Tab centrale morphing + swipe-up bottom sheet | 1-tap primary + full actions via swipe |
| Header scroll behavior | Hide on scroll-down, reveal on scroll-up | Chrome mobile pattern, gains 56px |
| SmartFAB | Remove entirely | Redundant with morphed center tab |
| HandDrawer | Remove as fixed bar, cards move to bottom sheet | Saves 80px, cards still accessible |
| ContextualBottomNav | Remove, merge into bottom sheet | Eliminates duplicate bottom bar |
| MobileBreadcrumb | Remove as bar, inline in content flow | Saves 36px, still navigable |

## New Layout Architecture

### Screen Budget (iPhone SE, 667px)

| Layer | Before | After (idle) | After (scrolled) |
|-------|--------|-------------|-------------------|
| TopNav | 56px | 56px | 0px (hidden) |
| Breadcrumb | 36px | 0px (inline) | 0px (inline) |
| HandDrawer | 80px | 0px (in sheet) | 0px (in sheet) |
| Content | ~379px (57%) | ~539px (81%) | ~603px (90%) |
| ContextualBottomNav | 44px | 0px (in sheet) | 0px (in sheet) |
| MobileTabBar | 72px | 64px | 64px |
| SmartFAB | 56px overlay | 0px (removed) | 0px (removed) |

### Component Changes

#### 1. UnifiedTopNav — Hide/Reveal on Scroll

**File**: `src/components/layout/UnifiedShell/UnifiedTopNav.tsx`

**Behavior**:
- Default: visible at 56px (sticky top)
- Scroll-down: slides up and hides completely with `transform: translateY(-100%)` transition (200ms ease-out)
- Scroll-up: slides back into view with `transform: translateY(0)` transition (200ms ease-out)
- Threshold: 10px scroll delta to trigger (prevents jitter)

**Changes**:
- Keep `sticky top-0` positioning (NOT `fixed`) — this preserves the existing `flex flex-col h-screen` shell architecture in `UnifiedShellClient`. The TopNav participates in the flex column naturally, and hiding is achieved via `transform: translateY(-100%)` + negative margin to collapse its space.
- Add `transition-[transform,margin] duration-200 ease-out`
- When hidden: `transform: translateY(-100%)` + `mt-[-56px]` to collapse flex space, so `<main>` naturally fills the freed height without requiring `pt-14` offset.
- When visible: `transform: translateY(0)` + `mt-0`
- Remove focused card title center section on mobile (redundant with morphed tab)
- Keep: logo (left), search + bell + avatar (right)
- **Banner handling**: Impersonation banner and onboarding banner render *above* the TopNav in `UnifiedShellClient`. They remain sticky and unaffected by the hide/reveal — only the TopNav itself hides. Banners stay visible during scroll.

**Scroll container note**: The scrollable element is `<main id="main-content" className="flex-1 overflow-y-auto">`, NOT `window`. The `useScrollHideNav` hook MUST attach its scroll listener to the `<main>` element via ref, not to `window.addEventListener('scroll')`.

**New hook**: `useScrollHideNav` — wraps existing `useScrollDirection` but accepts a `scrollContainerRef` parameter. See hook specification below.

#### 2. MobileTabBar — Morphing Center Tab

**File**: `src/components/layout/MobileTabBar/MobileTabBar.tsx`

**Behavior**:
- 5 tabs remain: Dashboard, Library, **Center (contextual)**, Chat, Profile
- Center tab morphs based on current route context:
  - `/library/games/[id]` → shows game entity icon + game name
  - `/sessions/[id]` → shows session icon + session name
  - `/chat/[id]` → shows agent icon + agent name
  - Default (no context) → shows Discover (Gamepad2 icon)
- Tap on morphed center tab: opens the ContextualBottomSheet
- Tap on Discover (default state): navigates to `/games`
- Visual: morphed state uses entity color border + slightly larger icon container (44x44 rounded-xl vs standard icon)

**Swipe-up indicator**:
- Small horizontal line (36x4px, `bg-white/15`, `rounded-full`) positioned 8px above TabBar
- Only visible when there are contextual actions available (bottom sheet has content)
- Subtle pulse animation on first visit (onboarding hint)

**Changes**:
- Add `contextEntity` prop from route context (via `useContextualEntity` hook)
- Center tab renders conditionally based on `contextEntity`
- Add swipe-up gesture detection on TabBar area
- Height stays 64px (reduced from 72px — safe area handled separately)
- Safe area: `pb-[env(safe-area-inset-bottom)]` moves to wrapper, not TabBar itself

**Guest (unauthenticated) behavior**:
- Guest sees only 2 tabs: Dashboard + Discover (auth-gated tabs hidden)
- With 2 tabs, center tab morphing is DISABLED — Discover stays as a normal tab
- Swipe-up indicator is hidden for guests (no contextual actions available)
- Bottom sheet is disabled for guests
- When user logs in, TabBar transitions to 5-tab layout with morphing enabled

#### 3. ContextualBottomSheet — New Component

**File**: `src/components/layout/ContextualBottomSheet/ContextualBottomSheet.tsx`

**Trigger**:
- Swipe-up from TabBar swipe indicator
- Tap on morphed center tab

**Content sections**:

```
┌─────────────────────────────┐
│         ── handle ──        │  ← drag handle (40x4px)
│                             │
│  🎲 Catan                   │  ← entity header (icon + name + subtitle)
│  Azioni rapide              │
│─────────────────────────────│
│                             │
│  📚 Carte Aperte (3)        │  ← open cards section (from old HandDrawer)
│  ┌──────┐ ┌──────┐ ┌─────┐ │
│  │Catan │ │ Risk │ │Azul │ │  ← horizontal scroll, tap to focus
│  └──────┘ └──────┘ └─────┘ │
│─────────────────────────────│
│                             │
│  ┌──────────┐ ┌──────────┐ │  ← contextual actions grid (2 columns)
│  │  📋 FAQ  │ │ 📖 Rules │ │
│  └──────────┘ └──────────┘ │
│  ┌──────────┐ ┌──────────┐ │
│  │💬 Reviews│ │🎯Strategy│ │
│  └──────────┘ └──────────┘ │
│  ┌─────────────────────────┐│
│  │   🎮 Nuova Sessione     ││  ← primary CTA (full width, accent color)
│  └─────────────────────────┘│
│                             │
└─────────────────────────────┘
```

**Behavior**:
- Opens as bottom sheet overlay with backdrop (50% black)
- Default height: ~50% viewport, draggable to ~80%
- Dismiss: swipe-down, tap backdrop, or tap close
- Sections are dynamic — "Carte Aperte" only shows if `cards.length > 0` (from `useCardHand`), actions vary by context
- **Loading/empty state**: On first visit (before card hand seed effect runs), the "Carte Aperte" section is hidden. The swipe indicator only becomes visible when the bottom sheet has at least one section with content (either cards OR contextual actions). This prevents an empty sheet from opening.
- Uses `framer-motion` or CSS transitions for smooth open/close
- Glassmorphism: `bg-card/95 backdrop-blur-xl rounded-t-2xl`

**Context-specific actions** (NEW hook `useContextualSheetActions` — do NOT reuse `useBottomNavActions` which only handles session-focused quick actions):

The new `useContextualSheetActions` hook consumes `useRouteContext` (existing at `src/hooks/useRouteContext.ts`) to provide route-based actions:

| Route Pattern | Actions |
|--------------|---------|
| `/library/games/[id]` | FAQ, Rules, Reviews, Strategy, New Session, Ask AI |
| `/sessions/[id]` | Add Note, Scoreboard, End Session, Share |
| `/chat/[id]` | New Topic, History, Settings |
| `/library` | Add Game, Import, Filter |
| `/dashboard` | Quick Start, Recent Games |
| Default | None (sheet disabled, center tab = Discover) |

**Note**: The existing `useBottomNavActions` hook is retained as-is for `ContextualBottomNav` on desktop. It must NOT be modified to avoid regression.

#### 4. Inline Breadcrumb

**Replaces**: `src/components/layout/MobileBreadcrumb/MobileBreadcrumb.tsx` (on mobile)

**Implementation**: Not a separate component — breadcrumb becomes part of the page content flow on mobile.

```tsx
// In page layouts, add at top of content:
<div className="flex items-center gap-1.5 text-xs text-muted-foreground md:hidden mb-3">
  <Link href={parentPath} className="text-primary">
    <ArrowLeft className="w-3.5 h-3.5" />
  </Link>
  <span>{parentLabel}</span>
  <ChevronRight className="w-3 h-3" />
  <span className="text-foreground font-medium truncate">{currentLabel}</span>
</div>
```

- Only visible on mobile (`md:hidden`)
- Scrolls with content (not fixed)
- Simpler than current MobileBreadcrumb — just parent + current

#### 5. Components to Remove (Mobile Only)

| Component | Action | Desktop Impact |
|-----------|--------|----------------|
| `SmartFAB` | Delete entirely | Was already `md:hidden` |
| `HandDrawer` | Hide on mobile, keep on desktop as-is | Cards move to bottom sheet on mobile |
| `ContextualBottomNav` | Hide on mobile, keep on desktop | Actions move to bottom sheet |
| `MobileBreadcrumb` | Remove component | Replaced by inline breadcrumb in content |

### New Hooks

#### `useScrollHideNav`

```typescript
interface UseScrollHideNavOptions {
  scrollContainerRef: RefObject<HTMLElement>;  // REQUIRED — the <main> element ref
  threshold?: number;      // px of scroll delta to trigger (default: 10)
  disabled?: boolean;      // disable on certain pages
}

interface UseScrollHideNavReturn {
  isNavVisible: boolean;
  scrollDirection: 'up' | 'down' | null;
}

function useScrollHideNav(options: UseScrollHideNavOptions): UseScrollHideNavReturn;
```

**Implementation note**: This hook wraps the existing `useScrollDirection` hook but overrides its scroll listener target. The existing `useScrollDirection` listens to `window` scroll events, but the app's scrollable container is `<main id="main-content" className="flex-1 overflow-y-auto">`, NOT the window. This hook MUST attach to the `scrollContainerRef` element.

During Phase 2→3 transition (before SmartFAB removal), both `useScrollDirection` (used by SmartFAB with 50px threshold on window) and `useScrollHideNav` (10px threshold on `<main>`) will coexist. This is acceptable because they listen to different targets — no dual listener conflict.

#### `useContextualEntity`

```typescript
interface ContextualEntity {
  type: 'game' | 'session' | 'agent' | 'player' | null;
  id: string | null;
  title: string;
  icon: React.ComponentType;
  color: string; // HSL value for entity theming
}

function useContextualEntity(): ContextualEntity | null;
```

Derives the current contextual entity from the route pathname and card hand state.

#### `useBottomSheet`

```typescript
interface UseBottomSheetReturn {
  isOpen: boolean;
  open: () => void;
  close: () => void;
  toggle: () => void;
}

function useBottomSheet(): UseBottomSheetReturn;
```

Simple state management for the bottom sheet. Could be a Zustand slice in the existing layout store.

## Interaction Flows

### Flow 1: User opens game detail page

1. User taps a game card → navigates to `/library/games/catan-123`
2. TopNav shows normally (logo + search/bell/avatar)
3. Content shows inline breadcrumb → `← Library / Games` then game details
4. MobileTabBar center tab morphs from "Discover" to "🎲 Catan" with orange border
5. Swipe indicator line appears above TabBar

### Flow 2: User scrolls game content

1. User scrolls down → TopNav hides (slides up, 200ms)
2. Content area expands to ~90% of screen
3. User scrolls up → TopNav reappears (slides down, 200ms)
4. MobileTabBar stays fixed at bottom always

### Flow 3: User accesses contextual actions

1. User taps morphed center tab "🎲 Catan" OR swipes up from TabBar
2. ContextualBottomSheet opens with backdrop
3. Shows: open cards carousel + action grid (FAQ, Rules, Reviews, Strategy, New Session)
4. User taps "Rules" → sheet closes, navigates to rules section
5. OR user swipes down → sheet closes

### Flow 4: User on Dashboard (no context)

1. Center tab shows default "🎮 Discover"
2. No swipe indicator visible (no contextual actions)
3. Tap on Discover → navigates to `/games` catalog
4. TopNav hide/reveal still works on scroll

## Migration Strategy

### Phase 1: New Components (non-breaking)
1. Create `useScrollHideNav` hook (with `scrollContainerRef` parameter — listens to `<main>` not `window`)
2. Create `useContextualEntity` hook
3. Create `useContextualSheetActions` hook (new — consumes `useRouteContext`)
4. Create `ContextualBottomSheet` component (with loading/empty state for cards section)
5. Create `useBottomSheet` store/hook

### Phase 2: Modify Existing (gradual)
6. Update `UnifiedShellClient` — pass `mainRef` to `useScrollHideNav`, pass `isNavVisible` to `UnifiedTopNav`
7. Update `UnifiedTopNav` — add hide/reveal behavior via `transform + negative margin` (mobile only, sticky preserved)
8. Update `MobileTabBar` — add morphing center tab + swipe indicator + guest-state handling
9. Add inline breadcrumb to page layouts

### Phase 3: Remove Old (cleanup)
10. Audit all `MobileBreadcrumb` import sites (`grep -r "MobileBreadcrumb" src/`) before deletion
11. Remove `SmartFAB` component, directory, and all references
12. Hide `HandDrawer` on mobile (keep desktop)
13. Hide `ContextualBottomNav` on mobile (keep desktop)
14. Remove `MobileBreadcrumb` component, directory, AND test file (`__tests__/MobileBreadcrumb.test.tsx`)
15. Remove `useBottomPadding` hook adjustments for removed components
16. Update E2E tests (`bottom-nav.spec.ts`, `mobile-card-browser.spec.ts`)

## Accessibility

- Touch targets remain 44x44px minimum (WCAG 2.1 AA)
- Bottom sheet has proper `role="dialog"` + `aria-modal="true"`
- Focus trap inside bottom sheet when open
- Escape key closes bottom sheet
- Swipe-up gesture has tap alternative (center tab tap)
- Screen reader announces tab morphing: "Catan actions, button" vs "Discover, link"
- Reduced motion: disable slide animations, use opacity transitions instead

## Testing Plan

| Test | Type | Description |
|------|------|-------------|
| TopNav hide/reveal | E2E | Scroll down → hidden, scroll up → visible |
| Center tab morphing | E2E | Navigate to game → center shows game name |
| Bottom sheet open/close | E2E | Swipe up + tap center → opens, swipe down → closes |
| Bottom sheet actions | E2E | Tap FAQ → navigates to FAQ section |
| Open cards in sheet | E2E | Cards from hand appear in bottom sheet |
| Default Discover tab | E2E | On dashboard, center = Discover, no swipe indicator |
| Inline breadcrumb | E2E | Game detail shows inline breadcrumb, tappable back |
| No SmartFAB | E2E | FAB no longer renders on any mobile page |
| Desktop unchanged | E2E | All desktop components render as before |
| Safe area handling | Manual | Test on iPhone with notch |
| Accessibility | E2E | Focus trap, ARIA roles, keyboard navigation |

## Files Affected

### New Files
- `src/components/layout/ContextualBottomSheet/ContextualBottomSheet.tsx`
- `src/components/layout/ContextualBottomSheet/index.ts`
- `src/hooks/useScrollHideNav.ts`
- `src/hooks/useContextualEntity.ts`
- `src/hooks/useContextualSheetActions.ts`

### Modified Files
- `src/components/layout/UnifiedShell/UnifiedTopNav.tsx` — hide/reveal via transform + negative margin
- `src/components/layout/UnifiedShell/UnifiedShellClient.tsx` — pass mainRef, integrate useScrollHideNav
- `src/components/layout/UnifiedShell/UnifiedShell.tsx` — remove mobile HandDrawer/ContextualBottomNav
- `src/components/layout/MobileTabBar/MobileTabBar.tsx` — morphing center + swipe + guest handling
- Page layouts — add inline breadcrumb

### Removed Files
- `src/components/layout/SmartFAB/SmartFAB.tsx` (and entire directory)
- `src/components/layout/MobileBreadcrumb/MobileBreadcrumb.tsx` (and entire directory including `__tests__/`)

### Modified to Hide on Mobile
- `src/components/layout/UnifiedShell/HandDrawer.tsx` — add `md:flex hidden`
- `src/components/layout/UnifiedShell/ContextualBottomNav.tsx` — add `hidden md:flex`
