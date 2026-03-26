# Layout Unification Design Spec

**Date**: 2026-03-19
**Status**: Approved
**Goal**: Simplify from 3 layout systems to 2 (UserShell + AdminShell) with shared user menu, removing the old card-based UnifiedShell and the alpha feature flag.

## Problem

Three inconsistent layout systems exist:

| Layout | Context | User Menu | Notifications | Profile Link |
|--------|---------|-----------|---------------|-------------|
| UnifiedHeader | Public pages | Full dropdown | NotificationBell | /profile |
| AlphaShell | Alpha flag (authenticated) | Partial dropdown | NotificationBell | /profile |
| UnifiedShell | Authenticated + Admin | **Missing** | Placeholder (non-functional) | **Missing** |

The default authenticated layout (UnifiedShell) has no user menu, no profile link, and non-functional notification placeholders. AlphaShell has a working user menu but is gated behind a feature flag.

## Solution

### Target Architecture

```
RootLayout
├── (auth) → AuthLayout (login/register, minimal)
├── (public) → PublicLayout + UnifiedHeader (guest pages)
├── (authenticated) → UserShell (all authenticated user pages)
├── (chat) → UserShell
└── admin/(dashboard) → AdminShell (admin/editor pages)
```

Two shells + one lightweight public header. No feature flags.

### Component 1: `UserMenuDropdown`

**File**: `components/layout/UserMenuDropdown.tsx`

Shared dropdown component extracted from `UnifiedHeader` (lines 79-185).

**States**:
- Loading: skeleton avatar (8x8 rounded pulse)
- Not authenticated: "Accedi" button linking to /login
- Authenticated: avatar with initial + dropdown

**Dropdown items** (authenticated):
1. Header: displayName + email
2. Profilo → `/profile`
3. Impostazioni → `/profile?tab=settings`
4. Editor Panel → `/editor` (if editor or admin role)
5. Admin Panel → `/admin/overview` (if admin role)
6. Theme toggle
7. Esci (logout with loading state "Disconnessione...")

**Dependencies**:
- `useCurrentUser()` for user data
- `useNavigationItems()` for isAuthenticated/isAuthLoading
- `logoutAction` server action
- DropdownMenu primitives from `ui/navigation/dropdown-menu`
- Icons: User, Settings, Shield, FileEdit, LogOut from lucide-react
- ThemeToggle from `ui/navigation/ThemeToggle`

**Labels**: Italian ("Profilo", "Impostazioni", "Esci", "Accedi", "Disconnessione...")

### Component 2: `UserShell`

**Directory**: `components/layout/UserShell/`

Based on AlphaShell structure. Rename and enhance alpha components:

| Source (alpha/) | Target (UserShell/) | Changes |
|----------------|---------------------|---------|
| AlphaShell.tsx | UserShell.tsx | Server wrapper, remove feature flag |
| AlphaShellClient.tsx | UserShellClient.tsx | Add DashboardEngineProvider wrapper |
| AlphaTopNav.tsx | UserTopNav.tsx | Replace inline dropdown with `<UserMenuDropdown />` |
| AlphaDesktopSidebar.tsx | UserDesktopSidebar.tsx | Rename only |
| AlphaTabBar.tsx | UserTabBar.tsx | Rename only |
| SwipeableContainer.tsx | SwipeableContainer.tsx | Move, update `useAlphaNav` → `useNavigation` import |

**UserShellClient structure**:
```
<div className="flex h-dvh bg-background">
  <UserDesktopSidebar />
  <div className="flex flex-col flex-1 min-w-0">
    <UserTopNav isAdmin={false} />
    <DashboardEngineProvider>
      {isTabPanelRoute ? <SwipeableContainer /> : <main>{children}</main>}
    </DashboardEngineProvider>
    <UserTabBar />
  </div>
</div>
```

**Tab panels** (SwipeableContainer children — all verified to exist):
- `components/features/home/HomeFeed.tsx`
- `components/features/library/LibraryPanel.tsx`
- `components/features/play/PlayPanel.tsx`
- `components/features/chat/ChatPanel.tsx`

**Tab panel routes** (no overlay): `/`, `/home`, `/dashboard`
(Remove `/alpha` from list — no longer a valid route.)

**UserTopNav changes** (only modification from AlphaTopNav):
```tsx
// Before (AlphaTopNav lines 80-129): ~50 lines inline dropdown
<DropdownMenu>...</DropdownMenu>

// After: 1 component
<UserMenuDropdown />
```

AlphaTopNav already has `<NotificationBell />` — no change needed there.

### Component 3: `AdminShell`

**File**: `components/layout/AdminShell/AdminShell.tsx`

New lightweight shell assembling existing admin components with the shared top nav.

**Structure**:
```
<div className="flex h-dvh bg-background">
  <AdminTabSidebar />
  <div className="flex flex-col flex-1 min-w-0">
    <UserTopNav isAdmin={true} onMenuToggle={...} isMenuOpen={...} />
    <AdminBreadcrumb />
    <AdminMobileDrawer open={...} onOpenChange={...} />
    <DashboardEngineProvider>
      <main>{children}</main>
    </DashboardEngineProvider>
  </div>
</div>
```

**Reused components** (moved from `UnifiedShell/` to `AdminShell/`):
- `AdminTabSidebar.tsx` — desktop sidebar with section navigation (NOT `AdminBreadcrumbs.tsx` from `components/admin/` which is a separate reusable primitive)
- `AdminBreadcrumb.tsx` — shell-level mobile breadcrumb
- `AdminMobileDrawer.tsx` — mobile hamburger drawer

**UserTopNav in admin context**:
- Shows hamburger button (mobile) when `onMenuToggle` is provided
- Same user menu and notifications as user context

### Component 4: `UnifiedHeader` (modified)

**File**: `components/layout/UnifiedHeader.tsx`

Replace inline UserMenu function component (~100 lines) with `<UserMenuDropdown />`.

Keeps: MeepleLogo, scroll shadow, 48px height, glassmorphism.
Removes: All user menu logic (delegated to shared component).

### Route Layout Changes

**`(authenticated)/layout.tsx`**:
```tsx
// Before
import { AlphaShell, LayoutSwitch } from '@/components/layout/alpha';
import { UnifiedShell } from '@/components/layout/UnifiedShell';

return (
  <LayoutSwitch
    alphaSlot={<AlphaShell isAdmin={userIsAdmin}>{children}</AlphaShell>}
    defaultSlot={<UnifiedShell isAdmin={userIsAdmin}>{children}</UnifiedShell>}
  />
);

// After
import { UserShell } from '@/components/layout/UserShell';

return <UserShell>{children}</UserShell>;
```

Note: `isAdmin` prop removed from UserShell — UserShell is always user context. Admin status is handled by UserMenuDropdown internally (shows admin/editor links in dropdown).

**`(chat)/layout.tsx`**:
```tsx
// Before (update JSDoc comment too — currently says "card-hand navigation")
<UnifiedShell>{children}</UnifiedShell>

// After
<UserShell>{children}</UserShell>
```

**`admin/(dashboard)/layout.tsx`**:
```tsx
// Before
<UnifiedShell isAdmin>{children}</UnifiedShell>

// After
<AdminShell>{children}</AdminShell>
```

## Deletions

### Directories (entire)
- `components/layout/UnifiedShell/` — all files including `index.ts`
- `components/layout/alpha/` — all files (after migration to `UserShell/`)
- `components/layout/MobileTabBar/` — directory with `MobileTabBar.tsx`, `index.ts`, and `__tests__/`
- `components/layout/ContextualBottomSheet/` — directory with component, `index.ts`, and `__tests__/`

### Components (individual files)
- `components/sheets/SearchAgentSheet.tsx`
- `components/sheets/SearchGameSheet.tsx`
- `components/sheets/SessionSheet.tsx`
- `components/sheets/ToolkitSheet.tsx`

### Hooks to delete
- `hooks/useBottomNavActions.ts`
- `hooks/usePlaceholderActions.ts`
- `hooks/useContextualSheetActions.ts`
- `hooks/useScrollHideNav.ts`
- `hooks/useBottomPadding.ts`
- `hooks/useContextualEntity.ts` (depends on useCardHand — verify no other consumers first)

### Hooks/Stores to KEEP (used by 12+ page components)
- `stores/use-card-hand.ts` — **KEPT**. Used by 12+ page/feature components (sessions, library, gaming-hub, agents, game-nights, profile, discover, knowledge-base, chat, dashboard-v2). These call `drawCard`, `protectCard`, etc. for navigation state. Removing would break all of them. The store stays as-is; consumer migration to the new navigation system is out of scope for this PR.
- `config/entity-actions.ts` — **KEPT**. Used by `dashboard/session-quick-actions.tsx` which imports `SESSION_QUICK_ACTIONS`. Removing would break the dashboard.

### Renames
- `hooks/useAlphaNav.ts` → `hooks/useNavigation.ts`
- `lib/stores/alphaNavStore.ts` → `lib/stores/navStore.ts`
- Update ALL imports in (8 files): `UserTopNav`, `UserDesktopSidebar`, `UserTabBar`, `SwipeableContainer`, `HomeFeed.tsx`, `LibraryPanel.tsx`, `PlayPanel.tsx`, `CardDetailModal.tsx`

### Test files to delete
- `__tests__/components/sheets/SessionSheet.test.tsx`
- `__tests__/components/sheets/SearchGameSheet.test.tsx`
- `__tests__/components/toolkit/toolkit.test.tsx`
- `components/layout/MobileTabBar/__tests__/MobileTabBar.test.tsx`
- `components/layout/ContextualBottomSheet/__tests__/ContextualBottomSheet.test.tsx`
- `__tests__/config/placeholder-action-cards.test.ts`
- `hooks/__tests__/useContextualEntity.test.ts`
- Any test files inside `UnifiedShell/` or `alpha/` directories

### Kept (used elsewhere)
- `AgentCreationSheet` — used by MeepleLibraryGameCard, MeepleGameCard (standalone, does not import SearchAgentSheet)
- `DashboardEngineProvider` — moved into both UserShellClient and AdminShell
- `useCardHand` store — kept, used by 12+ page components
- `config/entity-actions.ts` — kept, used by dashboard-v2
- `components/admin/AdminBreadcrumbs.tsx` — separate component from `AdminBreadcrumb.tsx`, unrelated
- `components/layout/DeckTrackerSync.tsx` — used by `agents/[id]/page.tsx`, not safe to delete

## File Structure (After)

```
components/layout/
├── UserShell/
│   ├── UserShell.tsx            (server wrapper)
│   ├── UserShellClient.tsx      (client layout + DashboardEngineProvider)
│   ├── UserTopNav.tsx           (shared top nav, used by both shells)
│   ├── UserDesktopSidebar.tsx   (desktop sidebar)
│   ├── UserTabBar.tsx           (mobile bottom tabs)
│   ├── SwipeableContainer.tsx   (tab panel container)
│   └── index.ts
├── AdminShell/
│   ├── AdminShell.tsx           (admin layout + DashboardEngineProvider)
│   ├── AdminTabSidebar.tsx      (moved from UnifiedShell/)
│   ├── AdminBreadcrumb.tsx      (moved from UnifiedShell/)
│   ├── AdminMobileDrawer.tsx    (moved from UnifiedShell/)
│   └── index.ts
├── UserMenuDropdown.tsx         (shared user menu)
├── UnifiedHeader.tsx            (public pages, uses UserMenuDropdown)
└── PublicLayout.tsx             (public wrapper)
```

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| `useCardHand` consumers (12+ files) call `drawCard` etc. but CardStack UI is removed | Store is kept; calls become no-ops visually. Consumer migration is a follow-up task. No runtime errors. |
| DashboardEngineProvider removal breaks dashboard-v2 components | Explicitly wrap in both UserShellClient and AdminShell |
| SearchAgentSheet removal loses the "search agent → create agent" flow from card placeholders | Flow was only accessible via card placeholder system. AgentCreationSheet is still usable standalone from game cards. Re-adding the flow to tab panels is a follow-up. |
| `useAlphaNav` rename misses consumers | Explicit list of 8 files to update (4 UserShell components + 3 feature panels + CardDetailModal) |
| Test files reference deleted components | Explicit list of test files to delete |
| Stale JSDoc comments in layout files | Update comments in all modified layout.tsx files |

## Out of Scope

- Migrating `useCardHand` consumers to the new navigation pattern (follow-up)
- Redesigning the tab panel content (HomeFeed, LibraryPanel, PlayPanel, ChatPanel)
- Changing admin sidebar sections or navigation structure
- Adding new features to the user menu
- Mobile gesture behavior changes in SwipeableContainer
- Responsive breakpoint changes
- Cleaning up `config/entity-actions.ts` exports no longer needed
