# Layout Cleanup & Redesign

**Date:** 2026-03-12
**Branch:** from `frontend-dev`, PR to `frontend-dev`
**Scope:** Single PR — cleanup + bar improvements + backend/frontend gap fixes

## Goal

Simplify the 3-tier navigation system, remove deprecated components, and connect unused backend endpoints to the frontend.

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Notifications | TopNavbar only | Remove duplicate from Sidebar bottom |
| Contextual navigation | SidebarContextNav only | Eliminate MiniNav; reclaim 48px vertical space |
| TopNavbar content | Breadcrumb + Search (⌘K) | Provide orientation and global search |
| FloatingActionBar | Keep unchanged | Solid pattern; actions stay separate from navigation |
| Mobile | Keep unchanged | AdaptiveBottomBar + MobileNavDrawer remain as-is |
| Backend/frontend gaps | Implement all three | Entity Links, RAG Pipeline, Game Toolkit |

## Part 1: Cleanup — Remove Deprecated Components

### Relocate before deleting

**`Navbar/Logo.tsx`** is still imported by `TopNavbar/TopNavbar.tsx` and `TopBar/TopBar.tsx`. Move it to `components/layout/TopNavbar/Logo.tsx` and update all imports before deleting the Navbar directory.

**Other non-deprecated files** in `Navbar/` (e.g., `NotificationCenter.tsx`, `HamburgerButton.tsx`, `HamburgerMenu.tsx`, `NavItems.tsx`, `ProfileBar.tsx`): audit each for active imports. Relocate any that are still referenced; delete the rest.

### Delete from `Navbar/` directory

Delete only confirmed-deprecated files:

| File | Reason |
|------|--------|
| `Navbar/Navbar.tsx` | Replaced by TopNavbar in Epic #5033 |
| `Navbar/UniversalNavbar.tsx` | Old unified navbar from #4936 |
| `Navbar/NavbarMobileDrawer.tsx` | Part of deprecated Navbar (distinct from `MobileNavDrawer.tsx`) |
| `Navbar/NavbarDropdown.tsx` | Part of deprecated Navbar |
| `Navbar/NavbarUserMenu.tsx` | Part of deprecated Navbar |
| `Navbar/__tests__/*` | Tests for deprecated components (incl. NavbarV2.test.tsx) |
| `Navbar/index.ts` | Barrel file — remove after relocating Logo and any other live exports |

After relocating live files and deleting deprecated ones, remove the empty `Navbar/` directory.

### Modify

- **`Sidebar/SidebarUser.tsx`** — Remove the notification bell from the **expanded** state only. In **collapsed** state (`isCollapsed=true`), the bell is the sole non-avatar interactive element; keep it there so collapsed-sidebar users retain notification access. TopNavbar bell becomes the primary location; collapsed-sidebar bell becomes a secondary convenience.
- **`components/layout/index.ts`** — Update barrel exports: remove `Navbar` re-exports, add `Logo` from its new `TopNavbar/` location.
- **All imports referencing `Navbar/`** — Search and redirect to new locations (Logo → TopNavbar/).

### Verification

- `grep -r "from.*layout/Navbar" apps/web/src/` must return zero hits (scoped to import paths, avoiding false positives from `TopNavbar/` directory name)
- `pnpm build` succeeds with zero errors
- All existing tests pass after deletion

## Part 2: Bar Improvements

### 2a. Eliminate MiniNav

**Delete:**
- `components/layout/MiniNav/MiniNav.tsx` and related test files
- Remove `<MiniNav />` rendering from `AppShellClient.tsx`

**Migrate tabs to SidebarContextNav:**

The `NavigationContext` interface stays unchanged. Pages still declare tabs via `useSetNavConfig({ miniNav: [...] })`. The change is in the consumer:

- `SidebarContextNav.tsx` reads `miniNavTabs` from `useNavigation()` and renders them as indented sub-items beneath the active section
- Format: uppercase section label → indented list of contextual tabs with active state (orange highlight)
- Animated appearance using existing Framer Motion patterns in SidebarContextNav
- **Collapsed sidebar behavior:** Tabs that have an `icon` field render as icon-only items (consistent with existing SidebarContextNav collapsed pattern). Tabs without an icon are hidden in collapsed mode — this is acceptable since users can expand the sidebar to see all tabs. Add a required `icon` field to the `NavTab` type for future tabs, but do not enforce it retroactively on existing NavConfig files.

**Files that declare miniNav tabs (~15 NavConfig files) require no changes.** They declare data; only the renderer changes. Tabs missing icons will simply not appear in collapsed-sidebar mode.

**Files modified:**
1. `AppShellClient.tsx` — remove `<MiniNav />`
2. `SidebarContextNav.tsx` — add miniNavTabs rendering
3. `MiniNav/` directory — delete

### 2b. TopNavbar Enhancements

**Modify `TopNavbar/TopNavbar.tsx` layout:**

```
[Hamburger (mobile)] [Logo] [Breadcrumb] --- [Search ⌘K] [🔔 Badge] [Avatar]
```

**Integrate existing `DesktopBreadcrumb.tsx`** (from `components/layout/Breadcrumb/DesktopBreadcrumb.tsx`):
- This component already reads `usePathname()` and calls `buildBreadcrumbs(pathname)` from `@/lib/breadcrumb-utils`
- Import `DesktopBreadcrumb` into `TopNavbar.tsx` and place it after the Logo
- No new breadcrumb component or hook needed — reuse existing implementation
- Mobile: hidden (MobileBreadcrumb already handles mobile)
- If `DesktopBreadcrumb` needs styling adjustments for TopNavbar context (inline vs block), modify its props or add a `variant` prop

**Integrate existing `CommandPalette.tsx`** (from `components/layout/CommandPalette.tsx`):
- This component already uses `cmdk` library with full keyboard navigation, filters, recent searches, and multi-type results (games, agents, PDFs, chats)
- The existing `hooks/useCommandPalette.ts` already registers ⌘K/Ctrl+K globally and manages open/close state
- Add the search trigger button (compact input with "Search..." placeholder + ⌘K badge) to `TopNavbar.tsx`
- Wire the trigger to call `useCommandPalette().open()`
- Remove any other `CommandPalette` rendering location (e.g., `GlobalSearch/GlobalSearch.tsx`) to avoid duplicate ⌘K listeners — consolidate into one canonical location in `TopNavbar`
- **`AdminTopNav.tsx`** also imports and renders `<GlobalSearch />` directly — replace with the same CommandPalette trigger pattern used in TopNavbar
- No new component or hook needed

## Part 3: Backend/Frontend Gap Fixes

### 3a. Entity Links UI

**Context:** Backend endpoints `EntityLinkUserEndpoints.cs` and `EntityLinkAdminEndpoints.cs` provide bidirectional relationships (game ↔ PDF, KB card ↔ session). Frontend client `entityLinks` exists but no UI consumes it.

**New components:**

`components/features/entity-links/EntityLinksPanel.tsx`
- Reusable panel showing linked entities for a given source entity
- Displays entity type icon, name, and link to detail page
- Supports: game, pdf, kbCard, session entity types
- Collapsible section with count badge

`components/features/entity-links/EntityLinkItem.tsx`
- Single entity link row: icon + name + type badge + navigate action

**New hook: `hooks/useEntityLinks.ts`**
- Calls `entityLinks` client methods
- Fetches links for a given entity ID and type
- Caches results via React Query

**Integration points:**
- `library/[gameId]/` — show linked PDFs and KB cards
- `sessions/[id]/` — show linked games and agents
- Admin entity link management page (if admin endpoints warrant it)

### 3b. RAG Pipeline Builder

**Context:** Page `pipeline-builder/` exists. Backend has `RagPipelineEndpoints.cs` and `RagStrategyEndpoints.cs`. Frontend client `ragExecution` exists.

**Approach:** Audit existing page code → connect to correct API clients → verify data flows.

**Expected work:**
- Wire `ragExecution` client calls into the pipeline builder page
- Ensure pipeline list, creation, and test execution work end-to-end
- Add navigation link in admin Sidebar under AI/Knowledge Base section

### 3c. Game Toolkit

**Context:** Pages `toolkit/` and `toolkit-demo/` exist. Backend has `GameToolkitRoutes.cs`. Frontend client `gameToolkit` exists.

**Approach:** Audit → connect → consolidate.

**Expected work:**
- Verify `toolkit/` page uses `gameToolkit` client correctly
- If `toolkit-demo/` duplicates `toolkit/`, remove the demo page
- Add "AI Toolkit" action in FloatingActionBar when viewing a game detail page
- Ensure toolkit is reachable from game detail via SidebarContextNav or action bar

## Component Architecture

```
AppShellClient
├── TopNavbar (modified)
│   ├── Hamburger (mobile, unchanged)
│   ├── Logo (relocated from Navbar/ to TopNavbar/)
│   ├── DesktopBreadcrumb (EXISTING, integrated into TopNavbar as-is)
│   ├── CommandPalette trigger (EXISTING, integrated into TopNavbar)
│   ├── NotificationBell (unchanged, primary location)
│   └── UserMenu (unchanged)
├── Sidebar (unchanged structure)
│   ├── SidebarContextNav (modified — now renders miniNav tabs)
│   └── SidebarUser (modified — bell removed in expanded, kept in collapsed)
├── [MiniNav — DELETED]
├── MobileBreadcrumb (unchanged)
├── Main Content
│   └── EntityLinksPanel (NEW, inline in detail pages)
├── FloatingActionBar (unchanged, desktop)
└── AdaptiveBottomBar (unchanged, mobile)
```

## Files Summary

### Relocate
- `Navbar/Logo.tsx` → `TopNavbar/Logo.tsx` (update all imports)
- Other live files in `Navbar/` — audit and relocate as needed

### Delete
- `components/layout/Navbar/` — deprecated files only (Navbar.tsx, UniversalNavbar.tsx, NavbarMobileDrawer.tsx, NavbarDropdown.tsx, NavbarUserMenu.tsx, tests, barrel file). Remove empty directory after.
- `components/layout/MiniNav/` — entire directory
- `components/layout/GlobalSearch/` — duplicates CommandPalette's ⌘K listener; remove entirely (including tests: `GlobalSearch.test.tsx`, `GlobalSearch.a11y.test.tsx`)
- `toolkit-demo/` page — if it duplicates `toolkit/`

### New
- `components/features/entity-links/EntityLinksPanel.tsx`
- `components/features/entity-links/EntityLinkItem.tsx`
- `hooks/useEntityLinks.ts`

### Modify
- `components/layout/TopNavbar/TopNavbar.tsx` — import DesktopBreadcrumb + CommandPalette trigger + search button
- `components/admin/layout/AdminTopNav.tsx` — replace `<GlobalSearch />` import with CommandPalette trigger
- `components/layout/AppShell/AppShellClient.tsx` — remove `<MiniNav />`
- `components/layout/Sidebar/SidebarContextNav.tsx` — render miniNav tabs from context
- `components/layout/Sidebar/SidebarUser.tsx` — remove notification bell from expanded state only; keep in collapsed state
- `components/layout/index.ts` — update barrel exports (remove Navbar, add Logo from TopNavbar)
- `pipeline-builder/` page — wire API clients
- `toolkit/` page — wire API client
- Game detail pages — add EntityLinksPanel
- Session detail page — add EntityLinksPanel

### Unchanged
- `FloatingActionBar/` — no changes
- `AdaptiveBottomBar/` — no changes
- `MobileNavDrawer.tsx` — no changes
- `NavigationContext.tsx` — interface unchanged (consumers change)
- `CommandPalette.tsx` — existing component, integrated into TopNavbar (not rewritten)
- `hooks/useCommandPalette.ts` — existing hook, no changes needed
- `components/layout/Breadcrumb/DesktopBreadcrumb.tsx` — integrated into TopNavbar as-is; add variant prop in follow-up if styling adjustments needed
- All `NavConfig.tsx` files — data declarations unchanged

## Testing Strategy

| Area | Test Type | Coverage |
|------|-----------|----------|
| Breadcrumb | Unit test: URL → breadcrumb segments | All route patterns |
| CommandPalette | Unit test: open/close, keyboard shortcut, search | Core interactions |
| SidebarContextNav tabs | Unit test: renders miniNav tabs from context | Tab declaration + active state |
| EntityLinksPanel | Unit test: renders links, handles empty state | CRUD + edge cases |
| Navbar deletion | Verify zero imports remain | Grep check |
| MiniNav deletion | Verify zero imports remain | Grep check |
| Integration | E2E: navigate between sections, verify sidebar tabs update | Critical user flows |
| Accessibility | Breadcrumb ARIA landmarks, CommandPalette focus trap | WCAG 2.1 AA |

## Risks

| Risk | Mitigation |
|------|------------|
| Large PR (many file changes) | Organize commits logically: cleanup → bars → gaps |
| MiniNav removal breaks mobile | Mobile uses AdaptiveBottomBar, not MiniNav — verify in tests |
| Import breakage from Navbar deletion | Relocate Logo.tsx first; run `grep -r "from.*layout/Navbar" apps/web/src/` + `pnpm build` before committing |
| Duplicate ⌘K listeners | Consolidate: remove GlobalSearch if it registers a competing listener; use existing CommandPalette as single source |
| Logo.tsx relocation breaks imports | Update all import paths; verify with build |
| Collapsed sidebar loses notifications | Keep bell in collapsed state; remove only from expanded state |
| Tabs without icons in collapsed sidebar | Hide icon-less tabs in collapsed mode; acceptable since user can expand |
| CommandPalette scope creep | Reuse existing cmdk-based CommandPalette as-is; no new features in this PR |
| Entity Links data not populated | Handle empty state gracefully in EntityLinksPanel |
