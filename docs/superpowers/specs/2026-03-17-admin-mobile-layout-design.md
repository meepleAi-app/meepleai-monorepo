# Admin Mobile Layout Redesign

**Date**: 2026-03-17
**Status**: Approved
**Approach**: C — Hamburger Drawer + Breadcrumb Bar

## Problem

The admin layout is unusable on smartphones. `AdminTabSidebar` renders at a fixed 200px on all viewports, leaving only 175px for content on a 375px phone — 47% of the screen. No responsive classes hide it on mobile. No hamburger menu, drawer, or collapse mechanism exists. The user-facing layout handles this with `HandDrawer`, but admin is explicitly excluded (`!isAdminContext`).

## Context

- Admin mobile usage is infrequent but must work when needed
- Primary mobile operations: reading data, light actions (approve, toggle, dismiss)
- All 6 admin sections must remain accessible (Overview, Content, AI, Users, System, Analytics)
- Existing navigation config lives in `admin-dashboard-navigation.ts` (6 sections, dynamic sub-items)

## Design

### Layout Change

Hide `AdminTabSidebar` on viewports below `md` (768px). Replace it with two mobile-only elements:

1. **Hamburger button** in `UnifiedTopNav` — opens the existing sidebar inside a Sheet
2. **Breadcrumb bar** below the TopNav — shows current location, offers dropdown for in-section navigation

Content area goes from ~175px to ~375px (approximate, before padding).

### Component Architecture

#### Modified Files (3)

**AdminTabSidebar.tsx** (~1 line)
Add `hidden md:flex` to the root container. Current classes `flex flex-col h-full w-[200px] ...` become `hidden md:flex flex-col h-full w-[200px] ...`. At `<md`, `hidden` applies (display:none). At `≥md`, `md:flex` overrides with display:flex and `flex-col` sets column direction. The sidebar disappears below 768px.

**UnifiedTopNav.tsx** (~10 lines)
Add a hamburger button (`md:hidden`) that appears only in admin context. New prop: `onMenuToggle?: () => void`. The button requires `aria-label="Open admin menu"`, `aria-expanded={drawerOpen}`, and `aria-controls="admin-mobile-drawer"`.

**UnifiedShellClient.tsx** (~15 lines)
Add `AdminMobileDrawer` and `AdminBreadcrumb` to the render tree when `isAdminContext`. Do NOT gate on `isDesktop` (which triggers at `lg`/1024px, mismatching the `md`/768px CSS breakpoint). Instead, always mount these components in admin context and rely on CSS classes (`md:hidden`) for visibility. This avoids a 768-1023px gap where JS and CSS would disagree. Manage `drawerOpen` state via `useState`, pass setter to TopNav and Sheet.

#### New Files (2)

**AdminMobileDrawer.tsx** (~40 lines)
Location: `components/layout/UnifiedShell/`

A shadcn Sheet (side="left", `id="admin-mobile-drawer"`) that wraps `AdminTabSidebar`. Props: `open`, `onOpenChange`. Closes automatically on navigation (listens to pathname change via `usePathname()`). Uses Sheet's default slide-in animation. The Sheet's root element has `md:hidden` to prevent rendering on desktop viewports.

**AdminBreadcrumb.tsx** (~80 lines)
Location: `components/layout/UnifiedShell/`

A `h-9` (36px) bar below the TopNav, visible only on mobile (`md:hidden`). Reads `usePathname()` and maps the current path to sections defined in `admin-dashboard-navigation.ts`.

Renders: `SectionName › SubsectionName` with a chevron (▾) that opens a dropdown listing all sub-items in the current section.

### Breadcrumb Behavior

| Action | Result |
|--------|--------|
| Tap section name (e.g. "AI") | Navigate to section hub page |
| Tap chevron ▾ | Open dropdown with all sub-items in current section |
| Tap item in dropdown | Navigate to that page, close dropdown |
| Tap outside dropdown | Close dropdown without navigating |
| Section has 1 page only | No chevron, breadcrumb is plain text |
| Root `/admin` path | Show "Overview" only, no separator |
| Unmapped path segment | Fallback: capitalize URL segment |

### Data Flow

```
UnifiedShellClient (isAdminContext = true)
├── useState(drawerOpen)
├── UnifiedTopNav → hamburger (md:hidden) → onMenuToggle → setDrawerOpen(true)
├── AdminBreadcrumb → md:hidden → reads usePathname() + navigation config
├── AdminMobileDrawer → md:hidden → open={drawerOpen} → onOpenChange={setDrawerOpen}
│   └── AdminTabSidebar (reused inside Sheet)
└── AdminTabSidebar → hidden md:flex (desktop only, standalone)
```

The `drawerOpen` state lives in `UnifiedShellClient`, which already manages `isAdminContext`. No new context providers needed. All mobile-only components are always mounted in admin context but hidden via CSS `md:hidden` — this avoids a breakpoint mismatch between `useResponsive()` (1024px) and `md` (768px).

### Responsive Breakpoint

- **< 768px (below md)**: Sidebar hidden, hamburger visible, breadcrumb visible
- **≥ 768px (md and above)**: Sidebar visible, hamburger hidden, breadcrumb hidden

Note: `useResponsive().isDesktop` triggers at `lg` (1024px), not `md` (768px). This design intentionally uses CSS-only responsive gating at `md` to avoid breakpoint mismatches. Tablet in landscape (≥768px) shows the sidebar; tablet in portrait (<768px) uses hamburger + breadcrumb.

### Edge Cases

**Deep paths** (`/admin/agents/builder/new`): Breadcrumb shows up to the deepest matched segment, ignores extra depth.

**Browser back/forward**: Breadcrumb updates reactively via `usePathname()`.

**Deep link on mobile**: Breadcrumb renders correctly from pathname. Drawer starts closed.

**Device rotation**: Breakpoint `md` handles the transition. Landscape tablet shows sidebar; portrait hides it.

**Resize from mobile to desktop while drawer is open**: CSS `md:hidden` hides drawer and breadcrumb; `hidden md:flex` shows sidebar. The Sheet overlay becomes invisible but `drawerOpen` state may remain `true` — harmless since the Sheet is CSS-hidden. On resize back to mobile, the Sheet reappears in its previous state; the user can close it normally.

**Safe area (notch, home indicator)**: Already handled by `ContextualBottomNav` with `pb-[env(safe-area-inset-bottom)]`. No changes needed.

## Testing

### Unit Tests (~14 cases)

**AdminBreadcrumb.test.tsx** (~7 tests):
- Renders section + sub-section from pathname
- Shows chevron only when section has >1 sub-item
- Tap chevron opens dropdown with correct items
- Tap dropdown item calls `router.push`
- Tap outside closes dropdown
- Fallback for unmapped paths
- Does not render on viewport ≥768px

**AdminMobileDrawer.test.tsx** (~4 tests):
- Sheet opens/closes with `open` prop
- Renders `AdminTabSidebar` inside
- Navigation closes the drawer
- Accessibility: focus trap, aria-label, Escape closes

**UnifiedTopNav.test.tsx** (~3 tests added):
- Hamburger visible only in admin context on mobile
- Click hamburger calls `onMenuToggle`
- Hamburger hidden on desktop

### E2E Tests (~8 cases)

**admin-mobile-nav.spec.ts**:
- Mobile viewport (375×812): sidebar hidden
- Hamburger opens drawer with all 6 sections
- Navigate from drawer → correct page → drawer closes
- Breadcrumb shows current section
- Breadcrumb dropdown shows sub-sections
- Navigate from dropdown → correct page
- Desktop viewport (1280×800): sidebar visible, no hamburger
- Resize desktop → mobile: correct transition

### E2E Patterns

Follow established conventions:
- `page.context().route()` for API mocking (not `page.route()`)
- `.first()` on all locators (strict mode)
- `PLAYWRIGHT_AUTH_BYPASS=true` for auth bypass
- Mobile viewport: `375 × 812` (iPhone 13)

## Scope

### In Scope
- Hide sidebar on mobile with responsive class
- Hamburger button in TopNav (admin context only)
- Sheet drawer wrapping existing sidebar
- Breadcrumb bar with section dropdown
- Unit tests for new components
- E2E tests for mobile navigation flow

### Out of Scope
- Redesigning admin page content for mobile (cards, tables, charts)
- Touch gestures (swipe to open drawer)
- Offline support
- Admin mobile-specific features or shortcuts

## Impact

| Metric | Before | After |
|--------|--------|-------|
| Content width (375px phone) | 175px | 375px |
| Files touched | — | 5 (3 modified, 2 new) |
| Lines added (net) | — | ~150 |
| New unit tests | — | ~14 |
| New E2E tests | — | ~8 |
| Breaking changes | — | None |
