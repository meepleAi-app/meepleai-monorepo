# Layout Cleanup & Redesign — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Simplify the 3-tier navigation, remove deprecated components, integrate breadcrumb + search into TopNavbar, and connect backend/frontend gaps.

**Architecture:** Eliminate MiniNav (L2) and migrate its tabs into SidebarContextNav. Add DesktopBreadcrumb and SearchTrigger (via useCommandPalette) into TopNavbar. Remove 12+ deprecated Navbar files. Consolidate search by using the existing CommandPalette/useCommandPalette as the canonical ⌘K handler and removing GlobalSearch.

**Tech Stack:** Next.js 16, React 19, Tailwind 4, shadcn/ui, Framer Motion, cmdk, Zustand

**Spec:** `docs/superpowers/specs/2026-03-12-layout-cleanup-redesign.md`

---

## Chunk 1: Cleanup — Remove Deprecated Components

### Task 1: Relocate Logo.tsx from Navbar/ to TopNavbar/

**Files:**
- Move: `apps/web/src/components/layout/Navbar/Logo.tsx` → `apps/web/src/components/layout/TopNavbar/Logo.tsx`
- Modify: `apps/web/src/components/layout/TopNavbar/TopNavbar.tsx` (update import)
- Modify: `apps/web/src/components/layout/TopBar/TopBar.tsx` (update import)
- Modify: `apps/web/src/components/layout/index.ts` (update barrel export)

- [ ] **Step 1: Move Logo.tsx to TopNavbar/**

```bash
mv apps/web/src/components/layout/Navbar/Logo.tsx apps/web/src/components/layout/TopNavbar/Logo.tsx
```

- [ ] **Step 2: Update import in TopNavbar.tsx**

In `apps/web/src/components/layout/TopNavbar/TopNavbar.tsx`, change:
```typescript
// FROM
import { Logo } from '../Navbar/Logo';
// TO
import { Logo } from './Logo';
```

- [ ] **Step 3: Update import in TopBar.tsx**

In `apps/web/src/components/layout/TopBar/TopBar.tsx`, change:
```typescript
// FROM
import { Logo } from '@/components/layout/Navbar/Logo';
// TO
import { Logo } from '@/components/layout/TopNavbar/Logo';
```

- [ ] **Step 4: Update barrel export in layout/index.ts**

In `apps/web/src/components/layout/index.ts`, change Logo export source from `Navbar` to `TopNavbar`.

- [ ] **Step 5: Search for any other Logo imports from Navbar/**

```bash
cd apps/web && grep -r "from.*Navbar/Logo\|from.*Navbar.*Logo" src/ --include="*.tsx" --include="*.ts"
```

Fix any remaining imports found.

- [ ] **Step 6: Verify build passes**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/layout/TopNavbar/Logo.tsx apps/web/src/components/layout/TopNavbar/TopNavbar.tsx apps/web/src/components/layout/TopBar/TopBar.tsx apps/web/src/components/layout/index.ts
git commit -m "refactor: relocate Logo.tsx from Navbar/ to TopNavbar/"
```

---

### Task 1b: Relocate NotificationCenter.tsx from Navbar/ to notifications/

**Files:**
- Move: `apps/web/src/components/layout/Navbar/NotificationCenter.tsx` → `apps/web/src/components/notifications/NotificationCenter.tsx`
- Modify: `apps/web/src/components/notifications/NotificationBell.tsx` (update import)
- Modify: `apps/web/src/components/notifications/index.ts` (update re-export)
- Modify: `apps/web/src/components/notifications/__tests__/NotificationBell.test.tsx` (update mock path)

- [ ] **Step 1: Move NotificationCenter.tsx to notifications/**

```bash
mv apps/web/src/components/layout/Navbar/NotificationCenter.tsx apps/web/src/components/notifications/NotificationCenter.tsx
```

- [ ] **Step 2: Update import in NotificationBell.tsx**

In `apps/web/src/components/notifications/NotificationBell.tsx`, change:
```typescript
// FROM
import { NotificationCenter } from '@/components/layout/Navbar/NotificationCenter';
// TO
import { NotificationCenter } from './NotificationCenter';
```

- [ ] **Step 3: Update re-export in notifications/index.ts**

In `apps/web/src/components/notifications/index.ts`, change the export source:
```typescript
// FROM
export { NotificationCenter } from '@/components/layout/Navbar/NotificationCenter';
// TO
export { NotificationCenter } from './NotificationCenter';
```

- [ ] **Step 4: Update test mock path**

In `apps/web/src/components/notifications/__tests__/NotificationBell.test.tsx`, update mock path from `@/components/layout/Navbar/NotificationCenter` to `../NotificationCenter` or `@/components/notifications/NotificationCenter`.

- [ ] **Step 5: Search for other NotificationCenter imports from Navbar/**

```bash
cd apps/web && grep -r "from.*Navbar/NotificationCenter\|from.*Navbar.*NotificationCenter" src/ --include="*.tsx" --include="*.ts"
```

Fix any remaining imports.

- [ ] **Step 6: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/notifications/NotificationCenter.tsx apps/web/src/components/notifications/NotificationBell.tsx apps/web/src/components/notifications/index.ts apps/web/src/components/notifications/__tests__/
git commit -m "refactor: relocate NotificationCenter.tsx from Navbar/ to notifications/"
```

---

### Task 2: Delete deprecated Navbar components

**Files:**
- Delete: `apps/web/src/components/layout/Navbar/Navbar.tsx`
- Delete: `apps/web/src/components/layout/Navbar/UniversalNavbar.tsx`
- Delete: `apps/web/src/components/layout/Navbar/NavbarMobileDrawer.tsx`
- Delete: `apps/web/src/components/layout/Navbar/NavbarDropdown.tsx`
- Delete: `apps/web/src/components/layout/Navbar/NavbarUserMenu.tsx`
- Delete: `apps/web/src/components/layout/Navbar/HamburgerButton.tsx`
- Delete: `apps/web/src/components/layout/Navbar/HamburgerMenu.tsx`
- Delete: `apps/web/src/components/layout/Navbar/NavItems.tsx`
- Delete: `apps/web/src/components/layout/Navbar/ProfileBar.tsx`
- Delete: `apps/web/src/components/layout/Navbar/index.ts`
- Delete: `apps/web/src/components/layout/Navbar/__tests__/` (all test files)

Note: Logo.tsx and NotificationCenter.tsx were relocated in Tasks 1 and 1b — they are no longer in this directory.

- [ ] **Step 1: Verify no active imports remain**

```bash
cd apps/web && grep -r "from.*layout/Navbar" src/ --include="*.tsx" --include="*.ts" | grep -v "Logo"
```

Expected: Zero hits (Logo was relocated in Task 1). If hits remain, update imports first.

- [ ] **Step 2: Remove barrel exports for Navbar in layout/index.ts**

In `apps/web/src/components/layout/index.ts`, remove all re-exports from `Navbar/` (HamburgerButton, HamburgerMenu, NavItems, ProfileBar, NotificationCenter, etc.). Keep only Logo (now from TopNavbar/).

- [ ] **Step 3: Delete the Navbar/ directory**

```bash
rm -rf apps/web/src/components/layout/Navbar/
```

- [ ] **Step 4: Verify build passes**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 5: Run tests**

```bash
cd apps/web && pnpm test --run
```

- [ ] **Step 6: Commit**

```bash
git add -A apps/web/src/components/layout/Navbar/ apps/web/src/components/layout/index.ts
git commit -m "refactor: remove deprecated Navbar components (5 components + helpers + tests)"
```

---

### Task 3: Remove notification bell from SidebarUser expanded state

**Files:**
- Modify: `apps/web/src/components/layout/Sidebar/SidebarUser.tsx`

- [ ] **Step 1: Read SidebarUser.tsx current state**

Check lines 77-91 for the notification bell rendering in both collapsed and expanded states.

- [ ] **Step 2: Keep bell in collapsed, remove from expanded**

In `apps/web/src/components/layout/Sidebar/SidebarUser.tsx`:

The expanded state (around lines 86-91) shows NotificationBell + ThemeToggle side-by-side. Remove NotificationBell from the expanded state only. The collapsed state (lines 77-84) keeps NotificationBell in the Tooltip wrapper.

```tsx
// COLLAPSED STATE — KEEP AS-IS (lines ~77-84)
{isCollapsed && (
  <Tooltip>
    <TooltipTrigger asChild>
      <NotificationBell />
    </TooltipTrigger>
    <TooltipContent>Notifications</TooltipContent>
  </Tooltip>
)}

// EXPANDED STATE — REMOVE NotificationBell, keep ThemeToggle only
{!isCollapsed && (
  <div className="flex items-center gap-2">
    <ThemeToggle />
  </div>
)}
```

- [ ] **Step 3: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/layout/Sidebar/SidebarUser.tsx
git commit -m "refactor: remove notification bell from expanded sidebar (keep in collapsed)"
```

---

## Chunk 2: Eliminate MiniNav + Migrate Tabs to SidebarContextNav

### Task 4: Add miniNavTabs rendering to SidebarContextNav

**Files:**
- Modify: `apps/web/src/components/layout/Sidebar/SidebarContextNav.tsx`

- [ ] **Step 1: Read SidebarContextNav.tsx to understand panel rendering**

Read `apps/web/src/components/layout/Sidebar/SidebarContextNav.tsx` — understand how contextual panels are rendered (around lines 129-205) and the animation pattern.

- [ ] **Step 2: Read NavigationContext to understand miniNavTabs type**

Read `apps/web/src/context/NavigationContext.tsx` and `apps/web/src/types/navigation.ts` to understand the `NavTab` interface and `miniNavTabs` array structure.

- [ ] **Step 3: Import useNavigation and add tabs section**

In `SidebarContextNav.tsx`, add a new section that reads `miniNavTabs` from `useNavigation()` and renders them below the contextual panels:

```tsx
import { useNavigation } from '@/context/NavigationContext';

// Inside the component, after the contextual panels:
const { miniNavTabs } = useNavigation();

// Derive a readable section label from the existing contextKey (already computed in this component):
// contextKey is "dashboard" | "library" | "games" | "chat" | "sessions" | etc.
// Capitalize it for display. Do NOT use activeZone from NavigationContext (it's a raw zone key, not human-readable).
const sectionLabel = contextKey ? contextKey.charAt(0).toUpperCase() + contextKey.slice(1).replace(/-/g, ' ') : '';

// Render section (with animation, matching existing panel pattern):
{miniNavTabs.length > 0 && (
  <motion.div
    key="context-tabs"
    variants={PANEL_VARIANTS}
    initial="hidden"
    animate="visible"
    exit="hidden"
    className="mt-2 space-y-0.5"
  >
    {sectionLabel && (
      <div className={cn(
        "px-3 py-1 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground",
        isCollapsed && "hidden"
      )}>
        {sectionLabel}
      </div>
    )}
    {miniNavTabs.map((tab) => (
      <Link
        key={tab.href}
        href={tab.href}
        className={cn(
          "flex items-center gap-2 rounded-md px-3 py-1.5 text-sm transition-colors",
          isCollapsed && "justify-center px-2",
          tab.isActive
            ? "bg-primary/10 text-primary font-medium"
            : "text-muted-foreground hover:text-foreground hover:bg-accent"
        )}
      >
        {tab.icon && <tab.icon className="h-4 w-4 shrink-0" />}
        {!isCollapsed && <span>{tab.label}</span>}
      </Link>
    ))}
  </motion.div>
)}
```

Note: Tabs without an `icon` field are hidden when `isCollapsed` is true (they render nothing visible since the label is also hidden).

- [ ] **Step 4: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 5: Test manually — navigate to Dashboard, Library, etc. and verify tabs appear in sidebar**

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/layout/Sidebar/SidebarContextNav.tsx
git commit -m "feat: render miniNavTabs in SidebarContextNav"
```

---

### Task 5: Remove MiniNav from AppShellClient and delete MiniNav component

**Files:**
- Modify: `apps/web/src/components/layout/AppShell/AppShellClient.tsx`
- Delete: `apps/web/src/components/layout/MiniNav/` (entire directory)
- Modify: `apps/web/src/components/layout/index.ts` (remove MiniNav export if present)

- [ ] **Step 1: Remove MiniNav rendering from AppShellClient.tsx**

In `apps/web/src/components/layout/AppShell/AppShellClient.tsx` (around line 92):

Remove the `<MiniNav />` component and its import. Also remove any `top` offset CSS that accounts for MiniNav height (48px). The content area below TopNavbar should now start at `top-14` (56px for TopNavbar only).

- [ ] **Step 2: Search for ALL MiniNav consumers and remove/replace**

```bash
cd apps/web && grep -r "from.*MiniNav\|import.*MiniNav" src/ --include="*.tsx" --include="*.ts"
```

Known consumers beyond AppShellClient:
- `apps/web/src/app/(chat)/layout.tsx` — imports and renders `<MiniNav />` in a sticky div. This layout has its own `NavigationProvider`. **Remove MiniNav** from this layout. Chat tabs will be handled by the chat route's own tab system or SidebarContextNav.
- `apps/web/src/components/layout/LayoutShell/LayoutShell.tsx` — imports and renders `<MiniNav />` for the Game Table UX shell. **Remove MiniNav** from LayoutShell. Game Table context tabs will be handled by SidebarContextNav (same pattern as all other pages).
- `apps/web/src/components/layout/__tests__/ReducedMotion.test.tsx` and `accessibility.test.tsx` — import MiniNav. **Delete or update** these test references.

For each consumer: remove the `<MiniNav />` rendering and its import. Ensure the page's NavConfig tabs are still declared via `useSetNavConfig` so they appear in SidebarContextNav.

- [ ] **Step 3: Remove MiniNav export from layout/index.ts**

If `apps/web/src/components/layout/index.ts` exports MiniNav, remove it.

- [ ] **Step 4: Delete MiniNav directory**

```bash
rm -rf apps/web/src/components/layout/MiniNav/
```

- [ ] **Step 5: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 6: Run tests**

```bash
cd apps/web && pnpm test --run
```

Fix any test failures related to MiniNav removal (tests that reference MiniNav should be deleted or updated).

- [ ] **Step 7: Commit**

```bash
git add -A apps/web/src/components/layout/MiniNav/ apps/web/src/components/layout/AppShell/AppShellClient.tsx apps/web/src/components/layout/index.ts
git commit -m "refactor: remove MiniNav component, tabs now rendered in SidebarContextNav"
```

---

## Chunk 3: TopNavbar Enhancements — Breadcrumb + Search

### Task 6: Integrate DesktopBreadcrumb into TopNavbar

**Files:**
- Modify: `apps/web/src/components/layout/TopNavbar/TopNavbar.tsx`

- [ ] **Step 1: Read current TopNavbar layout**

Read `apps/web/src/components/layout/TopNavbar/TopNavbar.tsx` (166 lines). Current layout: Left = MobileNavDrawer + Logo, Right = NotificationBell + UserMenu.

- [ ] **Step 2: Add DesktopBreadcrumb after Logo**

```tsx
import { DesktopBreadcrumb } from '../Breadcrumb/DesktopBreadcrumb';

// In the left section, after Logo:
<Logo variant="auto" asLink />
<DesktopBreadcrumb className="hidden md:flex items-center text-sm text-muted-foreground ml-2" />
```

- [ ] **Step 3: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/layout/TopNavbar/TopNavbar.tsx
git commit -m "feat: integrate DesktopBreadcrumb into TopNavbar"
```

---

### Task 7: Add SearchTrigger + CommandPalette to TopNavbar

**Files:**
- Modify: `apps/web/src/components/layout/TopNavbar/TopNavbar.tsx`

The existing `TopBar/TopBar.tsx` already demonstrates the canonical pattern: a `SearchTrigger` button that calls `useCommandPalette().toggle()` to open the `CommandPalette` modal. We follow the same pattern.

- [ ] **Step 1: Read TopBar.tsx to understand the search trigger pattern**

Read `apps/web/src/components/layout/TopBar/TopBar.tsx` — it uses `SearchTrigger` + `useCommandPalette`. This is the pattern to replicate in TopNavbar.

- [ ] **Step 2: Add SearchTrigger and CommandPalette to TopNavbar**

In `apps/web/src/components/layout/TopNavbar/TopNavbar.tsx`:

```tsx
import { useCommandPalette } from '@/hooks/useCommandPalette';
import { CommandPalette } from '../CommandPalette';

// Inside the component:
const commandPalette = useCommandPalette();

// In the right section, before NotificationBell — add a search trigger button:
<button
  onClick={commandPalette.toggle}
  className="hidden md:flex items-center gap-2 rounded-md border border-border/40 bg-muted/50 px-3 py-1.5 text-sm text-muted-foreground hover:bg-accent transition-colors"
>
  <Search className="h-4 w-4" />
  <span>Search...</span>
  <kbd className="ml-4 text-xs border border-border rounded px-1.5 py-0.5">⌘K</kbd>
</button>

// At the end of the component JSX, render the CommandPalette modal:
<CommandPalette
  isOpen={commandPalette.isOpen}
  onClose={commandPalette.close}
/>
```

- [ ] **Step 3: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/layout/TopNavbar/TopNavbar.tsx
git commit -m "feat: add search trigger + CommandPalette to TopNavbar"
```

---

### Task 8: Remove GlobalSearch and consolidate to CommandPalette

**Files:**
- Delete: `apps/web/src/components/layout/GlobalSearch/` (entire directory)
- Modify: `apps/web/src/components/admin/layout/AdminTopNav.tsx`
- Modify: `apps/web/src/components/layout/index.ts`
- Delete: `apps/web/src/hooks/useGlobalSearch.ts` (if it exists and is only used by GlobalSearch)

- [ ] **Step 1: Replace GlobalSearch in AdminTopNav with CommandPalette trigger**

In `apps/web/src/components/admin/layout/AdminTopNav.tsx` (line 10 imports GlobalSearch, line 122 renders it):

Replace with the same SearchTrigger + CommandPalette pattern from Task 7:

```tsx
// Remove:
import { GlobalSearch } from '@/components/layout/GlobalSearch/GlobalSearch';

// Add:
import { useCommandPalette } from '@/hooks/useCommandPalette';
import { CommandPalette } from '@/components/layout/CommandPalette';

// Replace <GlobalSearch /> with the search trigger button + CommandPalette modal
```

- [ ] **Step 2: Remove GlobalSearch exports from layout/index.ts**

Remove all GlobalSearch-related exports from `apps/web/src/components/layout/index.ts`.

- [ ] **Step 3: Search for any remaining GlobalSearch imports**

```bash
cd apps/web && grep -r "from.*GlobalSearch\|import.*GlobalSearch" src/ --include="*.tsx" --include="*.ts"
```

Remove or redirect any remaining imports.

- [ ] **Step 4: Delete GlobalSearch directory**

```bash
rm -rf apps/web/src/components/layout/GlobalSearch/
```

- [ ] **Step 5: Verify no double ⌘K triggers**

```bash
cd apps/web && grep -r "metaKey\|ctrlKey.*[kK]" src/ --include="*.tsx" --include="*.ts" -l
```

Only `useCommandPalette.ts` should register ⌘K.

- [ ] **Step 6: Verify build and tests**

```bash
cd apps/web && pnpm build && pnpm test --run
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: remove GlobalSearch, consolidate search to CommandPalette (⌘K)"
```

---

## Chunk 4: Backend/Frontend Gap Fixes

### Task 9: Audit Entity Links UI — already exists

**Files:**
- Check: `apps/web/src/components/ui/data-display/entity-link/`

- [ ] **Step 1: Verify entity-link components exist and are functional**

The exploration found entity-link components already exist at `components/ui/data-display/entity-link/` with: `entity-link-badge`, `entity-link-card`, `entity-link-chip`, `entity-link-preview-row`, `entity-relationship-graph`, `related-entities-section`, `add-entity-link-modal`, `use-entity-links` hook.

Read the directory to confirm.

- [ ] **Step 2: Check if entity links are integrated in game detail page**

```bash
cd apps/web && grep -r "entity-link\|EntityLink\|RelatedEntities\|useEntityLinks" src/app/ --include="*.tsx" -l
```

- [ ] **Step 3: If not integrated, add RelatedEntitiesSection to game detail**

In the game detail page (`apps/web/src/app/(authenticated)/library/games/[gameId]/` or similar), add:

```tsx
import { RelatedEntitiesSection } from '@/components/ui/data-display/entity-link/related-entities-section';

// In the game detail layout, after main content:
<RelatedEntitiesSection
  sourceEntityId={gameId}
  sourceEntityType="game"
/>
```

- [ ] **Step 4: If not integrated, add to session detail**

Similarly for `apps/web/src/app/(authenticated)/sessions/[id]/`:

```tsx
<RelatedEntitiesSection
  sourceEntityId={sessionId}
  sourceEntityType="session"
/>
```

- [ ] **Step 5: Commit (if changes made)**

```bash
git add -A
git commit -m "feat: integrate entity links panel in game and session detail pages"
```

---

### Task 10: Audit and wire RAG Pipeline Builder

**Files:**
- Check: `apps/web/src/app/(authenticated)/pipeline-builder/page.tsx`
- Check: `apps/web/src/lib/api/clients/` for RAG-related clients

- [ ] **Step 1: Read pipeline-builder page**

Read `apps/web/src/app/(authenticated)/pipeline-builder/page.tsx` to understand current state.

- [ ] **Step 2: Check which API clients it uses**

```bash
cd apps/web && grep -r "ragExecution\|ragPipeline\|RagStrategy" src/app/.*pipeline-builder --include="*.tsx" --include="*.ts"
```

- [ ] **Step 3: If API clients are not wired, connect them**

Wire `ragExecution` client calls for pipeline list, creation, and test execution. Follow existing patterns from other pages that use API clients.

- [ ] **Step 4: Add navigation link in admin sidebar**

If pipeline-builder is not reachable from admin navigation, add a link in the admin sidebar under AI/Knowledge Base section.

- [ ] **Step 5: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 6: Commit (if changes made)**

```bash
git add -A
git commit -m "feat: wire RAG pipeline builder to backend API clients"
```

---

### Task 11: Audit and consolidate Game Toolkit

**Files:**
- Check: `apps/web/src/app/(authenticated)/toolkit/page.tsx`
- Check: `apps/web/src/app/(authenticated)/toolkit/demo/page.tsx`
- Check: `apps/web/src/app/(authenticated)/toolkit/new-demo/page.tsx`

- [ ] **Step 1: Read toolkit pages**

Read all toolkit-related pages to understand their purpose and which API clients they use.

- [ ] **Step 2: Check API client usage**

```bash
cd apps/web && grep -r "gameToolkit\|GameToolkit" src/app/.*toolkit --include="*.tsx" --include="*.ts"
```

- [ ] **Step 3: If demo pages duplicate main toolkit, remove them**

If `demo/page.tsx` or `new-demo/page.tsx` are redundant with the main `toolkit/page.tsx`, delete them.

- [ ] **Step 4: Verify toolkit is reachable from game detail**

Check if game detail pages have a link/action to access the toolkit. If not, add an action in the FloatingActionBar config for game detail pages:

```tsx
// In the game detail NavConfig:
actionBar: [
  // existing actions...
  { label: 'AI Toolkit', icon: Wand2Icon, href: `/toolkit?gameId=${gameId}` },
]
```

- [ ] **Step 5: Verify build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 6: Commit (if changes made)**

```bash
git add -A
git commit -m "feat: consolidate toolkit pages and add game detail action"
```

---

## Chunk 5: Final Verification and Cleanup

### Task 12: Full verification pass

- [ ] **Step 1: Verify no deprecated imports remain**

```bash
cd apps/web && grep -r "from.*layout/Navbar[^U]" src/ --include="*.tsx" --include="*.ts"
cd apps/web && grep -r "from.*MiniNav" src/ --include="*.tsx" --include="*.ts"
```

Both should return zero hits.

- [ ] **Step 2: Full build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 3: Full test suite**

```bash
cd apps/web && pnpm test --run
```

- [ ] **Step 4: Lint and typecheck**

```bash
cd apps/web && pnpm lint && pnpm typecheck
```

- [ ] **Step 5: Fix any remaining test failures**

Tests referencing MiniNav, deprecated Navbar components, or GlobalSearch-specific tests may need updating or removal.

- [ ] **Step 6: Final commit**

```bash
git add -A
git commit -m "chore: cleanup remaining references and fix tests after layout redesign"
```

---

## Summary

| Task | Description | Estimated Complexity |
|------|-------------|---------------------|
| 1 | Relocate Logo.tsx to TopNavbar/ | Low |
| 1b | Relocate NotificationCenter.tsx to notifications/ | Low |
| 2 | Delete deprecated Navbar/ directory | Low |
| 3 | Remove notification bell from expanded sidebar | Low |
| 4 | Add miniNavTabs to SidebarContextNav | Medium |
| 5 | Remove MiniNav from all consumers + delete (AppShellClient, chat layout, LayoutShell, tests) | Medium-High |
| 6 | Integrate DesktopBreadcrumb into TopNavbar | Low |
| 7 | Add SearchTrigger + CommandPalette to TopNavbar | Low |
| 8 | Remove GlobalSearch, consolidate to CommandPalette | Medium |
| 9 | Audit Entity Links integration (already exists) | Low |
| 10 | Wire RAG Pipeline Builder | Medium |
| 11 | Consolidate Game Toolkit | Medium |
| 12 | Final verification | Low |
