# Epic: Mobile-First UX Redesign

> **Status**: PLANNED
> **Created**: 2026-03-06
> **Branch**: `feature/mobile-first-ux` (from `frontend-dev`)
> **Parent branch**: `frontend-dev`
> **Expert Panel**: Cockburn (UX), Fowler (Architecture), Wiegers (Requirements), Nygard (Production), Crispin (Quality)

---

## Problem Statement

The transition from BottomNav to FloatingActionBar caused a **critical mobile navigation regression**. Mobile users have no persistent global navigation — the only way to move between sections is through a hamburger menu (3 taps vs 1). Additionally, several spec features (SmartFAB, mobile breadcrumb, scroll behaviors) were never implemented, and public pages lack consistent responsive design.

## Evidence

- Screenshots: library.png, libraryMobile.png, createAgent.png
- Navigability analysis: docs/frontend/navigability-analysis.md
- Layout spec: docs/frontend/layout-spec.md (SmartFAB, ActionBar priority, breadcrumb)
- Deprecated BottomNav still in codebase (components/layout/BottomNav.tsx)
- FloatingActionBar: context actions only, NOT navigation — disappears when no actions configured

## Architecture

```
CURRENT (mobile):
  L1 [TopNavbar: logo + hamburger + bell + avatar]    <- always visible
  L2 [MiniNav: context tabs]                          <- often empty
     [Content]
  L3 [FloatingActionBar: page actions]                <- often empty

TARGET (mobile):
  L1 [TopNavbar: logo + bell + avatar]                <- always visible
  L2 [MiniNav: context tabs]                          <- section-aware
     [MobileBreadcrumb: back nav]                     <- new
     [Content]
  L3 [FloatingActionBar: page actions]                <- auto-hides on scroll
  -- [SmartFAB: primary action]                       <- new, context-aware
  L0 [MobileTabBar: global nav]                       <- new, always visible
```

---

## Sprint 1 — P0: Fix Broken Mobile Navigation

### Issue 1: MobileTabBar Component
**Priority**: CRITICAL
**Effort**: 2 days
**Dependencies**: None

**Description**: Create a new persistent bottom tab bar for mobile navigation, replacing the deprecated BottomNav with a design that matches the current glassmorphism design system.

**Acceptance Criteria**:
- [ ] New component: `src/components/layout/MobileTabBar/MobileTabBar.tsx`
- [ ] 5 tabs: Dashboard (`/dashboard`), Library (`/library`), Discover (`/games`), Chat (`/chat/new`), Profile (`/profile`)
- [ ] Visible only on mobile: `md:hidden`
- [ ] Fixed bottom, z-40
- [ ] Height: 72px + `env(safe-area-inset-bottom)`
- [ ] Glassmorphism: `bg-card/90 backdrop-blur-md border-t border-border/50`
- [ ] Active state: primary color + semibold + filled icon
- [ ] Inactive: muted-foreground + outline icon
- [ ] Auth-gated: Guest sees Dashboard + Discover only
- [ ] Touch targets: 44x44px minimum (WCAG 2.1 AA)
- [ ] `aria-label="Primary navigation"`, `aria-current="page"` on active
- [ ] Font: Nunito 10px labels
- [ ] Integrated into LayoutShell (below FloatingActionBar in DOM)
- [ ] Tests: component test + responsive behavior test

**Technical Notes**:
- Use `useAuthUser()` for auth gating (same pattern as old BottomNav)
- Use `usePathname()` for active state (prefix matching)
- Do NOT use deprecated BottomNav — new component from scratch
- Coexistence: FloatingActionBar moves to `bottom-[calc(72px+1.5rem)]` on mobile

**Files to create/modify**:
- CREATE: `src/components/layout/MobileTabBar/MobileTabBar.tsx`
- CREATE: `src/components/layout/MobileTabBar/index.ts`
- CREATE: `src/components/layout/MobileTabBar/__tests__/MobileTabBar.test.tsx`
- MODIFY: `src/components/layout/LayoutShell/LayoutShell.tsx` (add MobileTabBar)
- MODIFY: `src/components/layout/FloatingActionBar/FloatingActionBar.tsx` (adjust position)

---

### Issue 2: Dynamic Content Padding
**Priority**: CRITICAL
**Effort**: 0.5 days
**Dependencies**: Issue 1

**Description**: Replace the static `pb-24` in LayoutShell with dynamic padding that accounts for which bottom elements are visible.

**Acceptance Criteria**:
- [ ] Main content padding adapts to visible bottom bars
- [ ] MobileTabBar + FloatingActionBar visible: `pb-36` + safe area
- [ ] MobileTabBar only: `pb-20` + safe area
- [ ] FloatingActionBar only (desktop): `pb-24`
- [ ] Nothing visible: `pb-6`
- [ ] iOS safe area: `env(safe-area-inset-bottom)` on mobile
- [ ] No layout shift when FloatingActionBar appears/disappears
- [ ] Tests: padding calculation test

**Technical Notes**:
- Use `useResponsive()` hook (already exists) to detect mobile
- Track FloatingActionBar visibility via NavigationContext (actionBarActions.length > 0)
- Consider a `useBottomPadding()` hook for clean abstraction

**Files to modify**:
- MODIFY: `src/components/layout/LayoutShell/LayoutShell.tsx`
- CREATE: `src/hooks/useBottomPadding.ts` (optional, for clean abstraction)

---

### Issue 3: Public Pages — MobileTabBar + Responsive Landing
**Priority**: HIGH
**Effort**: 1.5 days
**Dependencies**: Issue 1

**Description**: Extend the MobileTabBar to public pages (with auth-gated tabs) and improve the landing/public page responsive design.

**Acceptance Criteria**:
- [ ] MobileTabBar visible on public pages (guest version: Home + Discover)
- [ ] Public layout (`(public)/layout.tsx`) integrates MobileTabBar
- [ ] Landing page: responsive hero section (text stacks on mobile)
- [ ] `/games` catalog: responsive card grid (1 col mobile, 2 sm, 3 lg)
- [ ] Login page: mobile-friendly form (full-width inputs, proper spacing)
- [ ] Footer: stacks on mobile, horizontal on desktop
- [ ] Tests: public page responsive tests

**Technical Notes**:
- PublicLayout uses UnifiedHeader — add MobileTabBar alongside
- Login page (screenshot): already looks decent, minor spacing improvements
- Game catalog: verify MeepleCard grid is truly mobile-first

**Files to modify**:
- MODIFY: `src/app/(public)/layout.tsx`
- MODIFY: Landing page components (hero, features, CTA sections)
- VERIFY: `src/app/(auth)/layout.tsx` (login/register responsive)

---

## Sprint 2 — P1: UX Enhancements

### Issue 4: FloatingActionBar — Auto-Hide on Scroll
**Priority**: HIGH
**Effort**: 0.5 days
**Dependencies**: Issue 1

**Description**: Hide the FloatingActionBar when the user scrolls down (reading mode), show it again on scroll up.

**Acceptance Criteria**:
- [ ] Scroll down > 50px: FloatingActionBar slides down + fades out (200ms)
- [ ] Scroll up: FloatingActionBar slides up + fades in (200ms)
- [ ] Tap on MobileTabBar: force-show FloatingActionBar
- [ ] Desktop: no auto-hide (always visible when actions present)
- [ ] `prefers-reduced-motion`: instant show/hide, no animation
- [ ] Tests: scroll behavior test

**Technical Notes**:
- Create `useScrollDirection()` hook or use existing scroll patterns
- Use `transform: translateY(calc(100% + 24px))` for GPU-accelerated hide
- Passive scroll listener for performance

**Files to create/modify**:
- CREATE: `src/hooks/useScrollDirection.ts`
- MODIFY: `src/components/layout/FloatingActionBar/FloatingActionBar.tsx`

---

### Issue 5: Mobile Breadcrumb / Back Navigation
**Priority**: HIGH
**Effort**: 1.5 days
**Dependencies**: None

**Description**: Add a lightweight breadcrumb bar on mobile (below MiniNav) that shows the navigation path and allows quick back navigation.

**Acceptance Criteria**:
- [ ] New component: `MobileBreadcrumb`
- [ ] Visible only on mobile: `md:hidden`
- [ ] Shows: `arrow-left icon + "Section > Subsection"`
- [ ] Tap arrow or parent label to navigate back
- [ ] Examples: "Library > Preferiti", "Catalogo > Carcassonne", "Admin > Agents"
- [ ] Collapses to "arrow-left Section" on viewport < 375px
- [ ] Height: 36px, bg-muted/50, text-sm, font-nunito
- [ ] Smooth transition on route change (fade 150ms)
- [ ] `role="navigation"` + `aria-label="Breadcrumb"`
- [ ] Tests: rendering + navigation tests
- [ ] Integrates in LayoutShell between MiniNav and main content

**Technical Notes**:
- Data source: `usePathname()` + route label mapping from config/navigation.ts
- Pattern: `config/breadcrumb-labels.ts` with route-to-label map
- Fallback: title-case segment name if no config match

**Files to create/modify**:
- CREATE: `src/components/layout/MobileBreadcrumb/MobileBreadcrumb.tsx`
- CREATE: `src/components/layout/MobileBreadcrumb/index.ts`
- CREATE: `src/config/breadcrumb-labels.ts`
- MODIFY: `src/components/layout/LayoutShell/LayoutShell.tsx`

---

### Issue 6: SmartFAB — Context-Aware Primary Action (Simplified)
**Priority**: HIGH
**Effort**: 1 day
**Dependencies**: Issue 1

**Description**: Implement a simplified SmartFAB (single floating button) that shows the primary action for the current context on mobile only.

**Acceptance Criteria**:
- [ ] New component: `SmartFAB`
- [ ] Visible only on mobile: `md:hidden`
- [ ] Position: right-4, above MobileTabBar (`bottom-[calc(72px+1rem)]`)
- [ ] Size: 56px diameter
- [ ] Icon changes based on route context:
  - `/library`: Plus (add game)
  - `/library/[id]`: Play (start session)
  - `/games`: Search (advanced search)
  - `/games/[id]`: Plus (add to library)
  - `/chat`: Plus (new chat)
  - `/sessions`: Plus (new session)
  - `/dashboard`: Sparkles (AI chat)
  - Default: MessageSquare (chat)
- [ ] Design: bg-primary, text-primary-foreground, shadow-lg, rounded-full
- [ ] Haptic-ready: `navigator.vibrate?.(10)` on tap
- [ ] Hides during fast scroll (reuses useScrollDirection)
- [ ] `aria-label` changes dynamically with context
- [ ] Tests: context mapping test + visibility test
- [ ] Long-press QuickMenu: **NOT in this issue** (deferred to P3)

**Technical Notes**:
- Context mapping: `config/smart-fab.ts` with route-to-action map
- Use `usePathname()` for context detection
- Coexists with FloatingActionBar (different z-index, different position)

**Files to create/modify**:
- CREATE: `src/components/layout/SmartFAB/SmartFAB.tsx`
- CREATE: `src/components/layout/SmartFAB/index.ts`
- CREATE: `src/config/smart-fab.ts`
- MODIFY: `src/components/layout/LayoutShell/LayoutShell.tsx`

---

## Sprint 3 — P2: Polish & Accessibility

### Issue 7: Touch-Friendly Tooltips
**Priority**: MEDIUM
**Effort**: 0.5 days
**Dependencies**: None

**Description**: Replace hover-only tooltips in FloatingActionBar with touch-friendly alternatives.

**Acceptance Criteria**:
- [ ] On mobile: show text labels below icons (like iOS tab bar) instead of tooltips
- [ ] On desktop: keep hover tooltips as-is
- [ ] Disabled actions: show disabledTooltip on long-press (mobile) or hover (desktop)
- [ ] Tests: tooltip accessibility test

**Files to modify**:
- MODIFY: `src/components/layout/FloatingActionBar/FloatingActionBar.tsx`

---

### Issue 8: prefers-reduced-motion Global Support
**Priority**: MEDIUM
**Effort**: 0.5 days
**Dependencies**: None

**Description**: Add global CSS rule to respect `prefers-reduced-motion`, and verify existing hook is used.

**Acceptance Criteria**:
- [ ] globals.css: `@media (prefers-reduced-motion: reduce)` rule kills all animations
- [ ] Verify `usePrefersReducedMotion()` hook is used in animated components
- [ ] FloatingActionBar: instant show/hide when reduced motion
- [ ] MiniNav: no smooth scrolling when reduced motion
- [ ] Page transitions: disabled when reduced motion
- [ ] Tests: reduced motion behavior test

**Files to modify**:
- MODIFY: `src/styles/globals.css`
- VERIFY: Components using animate-* classes

---

### Issue 9: FloatingActionBar Safe Area Fix
**Priority**: MEDIUM
**Effort**: 0.5 days
**Dependencies**: Issue 1

**Description**: Fix FloatingActionBar positioning on iOS devices with notch/home indicator.

**Acceptance Criteria**:
- [ ] FloatingActionBar respects `env(safe-area-inset-bottom)`
- [ ] On mobile with MobileTabBar: positioned above tab bar
- [ ] On desktop: positioned at bottom-6 (unchanged)
- [ ] No content clipping on iPhone 14/15 series
- [ ] Tests: visual regression test with safe area mocking

**Files to modify**:
- MODIFY: `src/components/layout/FloatingActionBar/FloatingActionBar.tsx`

---

### Issue 10: Scroll Listener Consolidation
**Priority**: MEDIUM
**Effort**: 1 day
**Dependencies**: Issue 4

**Description**: Consolidate duplicated scroll listeners into a shared hook.

**Acceptance Criteria**:
- [ ] New `useScrollState()` hook: direction, velocity, isScrolling, scrollY
- [ ] TopNavbar uses shared hook for shadow
- [ ] FloatingActionBar uses shared hook for auto-hide
- [ ] SmartFAB uses shared hook for hide on fast scroll
- [ ] Single passive scroll listener per page
- [ ] Performance: requestAnimationFrame throttling
- [ ] Tests: hook behavior test

**Files to create/modify**:
- CREATE: `src/hooks/useScrollState.ts`
- MODIFY: `src/components/layout/TopNavbar/TopNavbar.tsx`
- MODIFY: `src/components/layout/FloatingActionBar/FloatingActionBar.tsx`
- MODIFY: `src/components/layout/SmartFAB/SmartFAB.tsx`

---

## Sprint 4 — P3: Future Enhancements (Backlog)

| # | Feature | Effort | Description |
|---|---------|--------|-------------|
| 11 | SmartFAB QuickMenu (long-press) | 2 days | Long-press menu with 2-3 secondary actions per context |
| 12 | SmartFAB morph animation | 1 day | Icon rotation + scale transition on context change |
| 13 | Virtual keyboard detection | 0.5 days | Hide FAB/TabBar when keyboard open (visualViewport API) |
| 14 | Haptic feedback system | 0.5 days | navigator.vibrate on FAB tap, context switch, success/error |
| 15 | Sidebar strategy rework | 3 days | Split fixed nav zone + contextual zone (R6 from nav analysis) |
| 16 | Tablet split-view | 2 days | List + detail side-by-side on tablet landscape |
| 17 | Admin MobileTabBar | 1 day | Admin-specific bottom tabs (Overview, Users, Games, AI, KB) |
| 18 | Multi-select mode polish | 1 day | Batch action bar replaces FloatingActionBar during selection |
| 19 | Remove deprecated BottomNav | 0.5 days | Delete BottomNav.tsx + stories + tests after MobileTabBar stable |

---

## Implementation Order

```
Sprint 1 (P0):  Issue 1 → Issue 2 → Issue 3          (4 days)
Sprint 2 (P1):  Issue 4 + Issue 5 (parallel) → Issue 6  (3 days)
Sprint 3 (P2):  Issue 7 + Issue 8 (parallel) → Issue 9 → Issue 10  (2.5 days)
Sprint 4 (P3):  Backlog issues as capacity allows

Total estimated: ~9.5 days for Sprints 1-3
```

## Test Strategy

Each issue includes component tests. Additionally:
- **E2E tests** (Playwright): Mobile viewport (375x812) navigation flow
- **Visual regression**: Screenshot comparison of mobile layouts
- **Accessibility audit**: axe-core scan on mobile viewport
- **Performance**: Lighthouse mobile score before/after

## Definition of Done (per issue)

- [ ] Component implemented + tests passing
- [ ] Responsive behavior verified on 375px, 768px, 1280px viewports
- [ ] Dark mode verified
- [ ] Accessibility: aria-labels, focus management, touch targets
- [ ] PR created to `frontend-dev` (parent branch)
- [ ] Code review passed
- [ ] Merged + branch cleaned up

---

## Files Index

### New Files (to create)
```
src/components/layout/MobileTabBar/MobileTabBar.tsx
src/components/layout/MobileTabBar/index.ts
src/components/layout/MobileTabBar/__tests__/MobileTabBar.test.tsx
src/components/layout/MobileBreadcrumb/MobileBreadcrumb.tsx
src/components/layout/MobileBreadcrumb/index.ts
src/components/layout/SmartFAB/SmartFAB.tsx
src/components/layout/SmartFAB/index.ts
src/config/smart-fab.ts
src/config/breadcrumb-labels.ts
src/hooks/useScrollDirection.ts (or useScrollState.ts)
src/hooks/useBottomPadding.ts
```

### Modified Files
```
src/components/layout/LayoutShell/LayoutShell.tsx
src/components/layout/FloatingActionBar/FloatingActionBar.tsx
src/components/layout/TopNavbar/TopNavbar.tsx
src/app/(public)/layout.tsx
src/styles/globals.css
```

### Deprecated (to remove in P3)
```
src/components/layout/BottomNav.tsx (Issue 19)
src/components/layout/BottomNav.stories.tsx
src/components/layout/__tests__/BottomNav.test.tsx
```
