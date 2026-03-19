# Layout Unification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace 3 inconsistent layout systems with 2 (UserShell + AdminShell) and a shared UserMenuDropdown.

**Architecture:** Alpha layout becomes the new UserShell (tab panels + swipeable + overlay). Admin components from UnifiedShell move into AdminShell. A shared UserMenuDropdown extracted from UnifiedHeader is used by all layouts. The old card-based UnifiedShell UI is removed; `useCardHand` store is kept (12+ consumers).

**Tech Stack:** Next.js 16 App Router, React 19, TypeScript, Zustand, Tailwind 4, shadcn/ui, framer-motion

**Spec:** `docs/superpowers/specs/2026-03-19-layout-unification-design.md`

---

## File Map

### Create
| File | Responsibility |
|------|---------------|
| `components/layout/UserMenuDropdown.tsx` | Shared user avatar dropdown (profile, settings, admin links, theme, logout) |
| `components/layout/UserShell/UserShell.tsx` | Server wrapper (from AlphaShell) |
| `components/layout/UserShell/UserShellClient.tsx` | Client layout with DashboardEngineProvider (from AlphaShellClient) |
| `components/layout/UserShell/UserTopNav.tsx` | Top nav with UserMenuDropdown (from AlphaTopNav) |
| `components/layout/UserShell/UserDesktopSidebar.tsx` | Desktop sidebar (from AlphaDesktopSidebar) |
| `components/layout/UserShell/UserTabBar.tsx` | Mobile bottom tabs (from AlphaTabBar) |
| `components/layout/UserShell/SwipeableContainer.tsx` | Tab panel container (from alpha/) |
| `components/layout/UserShell/index.ts` | Barrel exports |
| `components/layout/AdminShell/AdminShell.tsx` | Admin layout assembling admin components |
| `components/layout/AdminShell/index.ts` | Barrel exports |
| `hooks/useNavigation.ts` | Renamed from useAlphaNav |
| `lib/stores/navStore.ts` | Renamed from alphaNavStore |

### Modify
| File | Change |
|------|--------|
| `components/layout/UnifiedHeader.tsx` | Replace inline UserMenu with UserMenuDropdown |
| `app/(authenticated)/layout.tsx` | Use UserShell, remove LayoutSwitch |
| `app/(chat)/layout.tsx` | Use UserShell, update JSDoc |
| `app/admin/(dashboard)/layout.tsx` | Use AdminShell |
| `components/features/home/HomeFeed.tsx:26` | Update useAlphaNav → useNavigation import |
| `components/features/library/LibraryPanel.tsx:24` | Update useAlphaNav → useNavigation import |
| `components/features/play/PlayPanel.tsx:23` | Update useAlphaNav → useNavigation import |
| `components/features/common/CardDetailModal.tsx:19` | Update useAlphaNav → useNavigation import |

### Move (from UnifiedShell/ to AdminShell/)
| File | Notes |
|------|-------|
| `AdminTabSidebar.tsx` | Update import path in AdminMobileDrawer |
| `AdminBreadcrumb.tsx` | No internal changes |
| `AdminMobileDrawer.tsx` | Update AdminTabSidebar import to relative |

### Delete
See spec "Deletions" section for full list. Handled in Task 7.

---

## Task 1: Create `UserMenuDropdown`

**Files:**
- Create: `apps/web/src/components/layout/UserMenuDropdown.tsx`
- Modify: (none yet — consumers wired in later tasks)

- [ ] **Step 1: Create UserMenuDropdown component**

Extract from `UnifiedHeader.tsx` lines 79-185. Standalone client component with all user menu logic:

```tsx
'use client';

import { useTransition } from 'react';

import { Settings, Shield, LogOut, UserIcon, User, FileEdit } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { logoutAction } from '@/actions/auth';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { ThemeToggle } from '@/components/ui/navigation/ThemeToggle';
import { Button } from '@/components/ui/primitives/button';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useNavigationItems } from '@/hooks/useNavigationItems';

export function UserMenuDropdown() {
  const router = useRouter();
  const { isAuthLoading, isAuthenticated } = useNavigationItems();
  const { data: user } = useCurrentUser();
  const [isLoggingOut, startTransition] = useTransition();

  const isAdmin =
    user?.role?.toLowerCase() === 'admin' || user?.role?.toLowerCase() === 'superadmin';
  const isEditor = user?.role?.toLowerCase() === 'editor' || isAdmin;

  const handleLogout = () => {
    startTransition(async () => {
      const result = await logoutAction();
      if (result.success) {
        router.push('/login');
      }
    });
  };

  const userInitial =
    user?.displayName?.charAt(0).toUpperCase() || user?.email?.charAt(0).toUpperCase() || 'U';

  if (isAuthLoading) {
    return (
      <div className="flex items-center" data-testid="user-menu-loading">
        <div className="w-8 h-8 rounded-full bg-muted animate-pulse" />
      </div>
    );
  }

  if (!isAuthenticated || !user) {
    return (
      <Link href="/login">
        <Button variant="default" size="sm">
          Accedi
        </Button>
      </Link>
    );
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          aria-label="User menu"
          data-testid="user-menu-trigger"
        >
          {user.displayName ? (
            <div className="w-8 h-8 rounded-full bg-primary/20 flex items-center justify-center">
              <span className="text-sm font-semibold text-primary">{userInitial}</span>
            </div>
          ) : (
            <UserIcon className="h-5 w-5" />
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel>
          <div className="flex flex-col space-y-1">
            <p className="text-sm font-medium leading-none">{user.displayName || 'Utente'}</p>
            <p className="text-xs leading-none text-muted-foreground">{user.email}</p>
          </div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />

        <DropdownMenuItem asChild data-testid="profile-menu-item">
          <Link href="/profile" className="flex items-center gap-2 cursor-pointer">
            <User className="h-4 w-4" />
            <span>Profilo</span>
          </Link>
        </DropdownMenuItem>

        <DropdownMenuSeparator />

        {isEditor && (
          <>
            <DropdownMenuItem asChild data-testid="editor-panel-menu-item">
              <Link href="/editor" className="flex items-center gap-2 cursor-pointer">
                <FileEdit className="h-4 w-4" />
                <span>Editor Panel</span>
              </Link>
            </DropdownMenuItem>
            <DropdownMenuSeparator />
          </>
        )}

        {isAdmin && (
          <>
            <DropdownMenuItem asChild data-testid="admin-panel-menu-item">
              <Link href="/admin/overview" className="flex items-center gap-2 cursor-pointer">
                <Shield className="h-4 w-4" />
                <span>Admin Panel</span>
              </Link>
            </DropdownMenuItem>
            <DropdownMenuSeparator />
          </>
        )}

        <DropdownMenuItem asChild data-testid="settings-menu-item">
          <Link href="/profile?tab=settings" className="flex items-center gap-2 cursor-pointer">
            <Settings className="h-4 w-4" />
            <span>Impostazioni</span>
          </Link>
        </DropdownMenuItem>

        <DropdownMenuSeparator />

        <div className="px-2 py-1.5">
          <ThemeToggle showLabel size="sm" className="w-full justify-start" />
        </div>

        <DropdownMenuSeparator />

        <DropdownMenuItem
          onClick={handleLogout}
          disabled={isLoggingOut}
          className="flex items-center gap-2 cursor-pointer text-destructive focus:text-destructive"
          data-testid="logout-menu-item"
        >
          <LogOut className="h-4 w-4" />
          <span>{isLoggingOut ? 'Disconnessione...' : 'Esci'}</span>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
```

- [ ] **Step 2: Verify TypeScript compiles**

Run: `cd apps/web && npx tsc --noEmit 2>&1 | head -20`
Expected: No errors related to UserMenuDropdown

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/layout/UserMenuDropdown.tsx
git commit -m "feat(layout): extract shared UserMenuDropdown component"
```

---

## Task 2: Rename navigation store and hook

**Files:**
- Create: `apps/web/src/lib/stores/navStore.ts` (renamed from `alphaNavStore.ts`)
- Create: `apps/web/src/hooks/useNavigation.ts` (renamed from `useAlphaNav.ts`)
- Modify: 8 consumer files (import path updates)

- [ ] **Step 1: Create `navStore.ts`**

Copy `apps/web/src/lib/stores/alphaNavStore.ts` → `apps/web/src/lib/stores/navStore.ts`.

Rename types and exports:
- `AlphaTab` → `NavTab`
- `AlphaNavState` → `NavState`
- `useAlphaNavStore` → `useNavStore`
- Update JSDoc: "Alpha Navigation Store" → "Navigation Store"

```ts
/**
 * Navigation Store
 *
 * Lightweight Zustand store for layout tab navigation,
 * detail modal state, and section title management.
 * Ephemeral state — no persist middleware needed.
 */

import { create } from 'zustand';

export type NavTab = 'home' | 'library' | 'play' | 'chat';

interface NavState {
  activeTab: NavTab;
  setActiveTab: (tab: NavTab) => void;

  detailEntityId: string | null;
  detailEntityType: string | null;
  openDetail: (entityId: string, entityType: string) => void;
  closeDetail: () => void;

  sectionTitle: string;
  setSectionTitle: (title: string) => void;
}

export const useNavStore = create<NavState>(set => ({
  activeTab: 'home',
  setActiveTab: tab => set({ activeTab: tab }),

  detailEntityId: null,
  detailEntityType: null,
  openDetail: (entityId, entityType) =>
    set({ detailEntityId: entityId, detailEntityType: entityType }),
  closeDetail: () => set({ detailEntityId: null, detailEntityType: null }),

  sectionTitle: 'Home',
  setSectionTitle: title => set({ sectionTitle: title }),
}));
```

- [ ] **Step 2: Create `useNavigation.ts`**

Create `apps/web/src/hooks/useNavigation.ts`:

```ts
/**
 * useNavigation Hook
 *
 * Re-export of the navigation store for convenient hook-style usage.
 */

export { useNavStore as useNavigation } from '@/lib/stores/navStore';
export type { NavTab } from '@/lib/stores/navStore';
```

- [ ] **Step 3: Update feature panel imports (4 files)**

In each file, replace:
```ts
import { useAlphaNav } from '@/hooks/useAlphaNav';
```
With:
```ts
import { useNavigation } from '@/hooks/useNavigation';
```
And replace all `useAlphaNav()` calls with `useNavigation()`.

Files:
- `apps/web/src/components/features/home/HomeFeed.tsx:26`
- `apps/web/src/components/features/library/LibraryPanel.tsx:24`
- `apps/web/src/components/features/play/PlayPanel.tsx:23`
- `apps/web/src/components/features/common/CardDetailModal.tsx:19`

For files importing `type AlphaTab`, replace with `type NavTab`.

- [ ] **Step 4: Verify TypeScript compiles**

Run: `cd apps/web && npx tsc --noEmit 2>&1 | head -20`
Expected: No errors. Old files still exist (deleted later), new files compile.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/stores/navStore.ts apps/web/src/hooks/useNavigation.ts \
  apps/web/src/components/features/home/HomeFeed.tsx \
  apps/web/src/components/features/library/LibraryPanel.tsx \
  apps/web/src/components/features/play/PlayPanel.tsx \
  apps/web/src/components/features/common/CardDetailModal.tsx
git commit -m "refactor(nav): rename alphaNavStore → navStore, useAlphaNav → useNavigation"
```

---

## Task 3: Create `UserShell/` directory

**Files:**
- Create: `apps/web/src/components/layout/UserShell/UserShell.tsx`
- Create: `apps/web/src/components/layout/UserShell/UserShellClient.tsx`
- Create: `apps/web/src/components/layout/UserShell/UserTopNav.tsx`
- Create: `apps/web/src/components/layout/UserShell/UserDesktopSidebar.tsx`
- Create: `apps/web/src/components/layout/UserShell/UserTabBar.tsx`
- Create: `apps/web/src/components/layout/UserShell/SwipeableContainer.tsx`
- Create: `apps/web/src/components/layout/UserShell/index.ts`

- [ ] **Step 1: Create `UserShell.tsx` (server wrapper)**

Based on `alpha/AlphaShell.tsx`. Remove feature flag references.

```tsx
import { type ReactNode } from 'react';

import { UserShellClient } from './UserShellClient';

interface UserShellProps {
  children: ReactNode;
}

export async function UserShell({ children }: UserShellProps) {
  return <UserShellClient>{children}</UserShellClient>;
}
```

- [ ] **Step 2: Create `UserShellClient.tsx`**

Based on `alpha/AlphaShellClient.tsx`. Changes:
- Remove `isAdmin` prop (UserShell is always user context)
- Add `DashboardEngineProvider` wrapper
- Use renamed components (UserDesktopSidebar, UserTopNav, UserTabBar)
- Remove `/alpha` from TAB_PANEL_ROUTES

```tsx
'use client';

import { type ReactNode } from 'react';

import { usePathname } from 'next/navigation';

import { ChatPanel } from '@/components/features/chat/ChatPanel';
import { HomeFeed } from '@/components/features/home/HomeFeed';
import { LibraryPanel } from '@/components/features/library/LibraryPanel';
import { PlayPanel } from '@/components/features/play/PlayPanel';
import { DashboardEngineProvider } from '@/components/dashboard-v2';

import { UserDesktopSidebar } from './UserDesktopSidebar';
import { UserTabBar } from './UserTabBar';
import { UserTopNav } from './UserTopNav';
import { SwipeableContainer } from './SwipeableContainer';

const TAB_PANEL_ROUTES = ['/', '/home', '/dashboard'];

interface UserShellClientProps {
  children: ReactNode;
}

export function UserShellClient({ children }: UserShellClientProps) {
  const pathname = usePathname();
  const isTabPanelRoute = TAB_PANEL_ROUTES.includes(pathname);

  return (
    <div className="flex h-dvh bg-background">
      <UserDesktopSidebar />

      <div className="flex flex-col flex-1 min-w-0">
        <UserTopNav />

        <DashboardEngineProvider>
          {/* Tab panels always mounted (hidden via CSS) to preserve scroll/state */}
          <div className={isTabPanelRoute ? 'flex-1 min-h-0' : 'hidden'}>
            <SwipeableContainer>
              <HomeFeed />
              <LibraryPanel />
              <PlayPanel />
              <ChatPanel />
            </SwipeableContainer>
          </div>

          {/* Route content rendered as overlay when not on a tab panel route */}
          {!isTabPanelRoute && (
            <main className="flex-1 overflow-y-auto pb-16 lg:pb-0">{children}</main>
          )}
        </DashboardEngineProvider>

        <UserTabBar />
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Create `UserTopNav.tsx`**

Based on `alpha/AlphaTopNav.tsx`. Changes:
- Replace inline dropdown (lines 80-129) with `<UserMenuDropdown />`
- Replace `useAlphaNav` with `useNavigation`
- Remove `isAdmin` prop (handled by UserMenuDropdown internally)

```tsx
'use client';

import Link from 'next/link';

import { NotificationBell } from '@/components/notifications';
import { useNavigation } from '@/hooks/useNavigation';
import { cn } from '@/lib/utils';

import { UserMenuDropdown } from '../UserMenuDropdown';

interface UserTopNavProps {
  /** Whether the admin hamburger and toggle should be shown */
  isAdmin?: boolean;
  /** Callback to open admin mobile drawer */
  onMenuToggle?: () => void;
  /** Whether the admin drawer is open */
  isMenuOpen?: boolean;
}

export function UserTopNav({ isAdmin, onMenuToggle, isMenuOpen }: UserTopNavProps) {
  const { sectionTitle } = useNavigation();

  return (
    <header
      className={cn(
        'sticky top-0 z-40 h-14',
        'flex items-center justify-between px-4',
        'bg-background/90 backdrop-blur-xl',
        'border-b border-border/40'
      )}
      data-testid="user-top-nav"
    >
      {/* Hamburger (mobile admin only) */}
      {isAdmin && onMenuToggle && (
        <button
          type="button"
          className="md:hidden flex items-center justify-center w-9 h-9 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors"
          onClick={onMenuToggle}
          aria-label="Open admin menu"
          aria-expanded={isMenuOpen}
          aria-controls="admin-mobile-drawer"
        >
          <svg xmlns="http://www.w3.org/2000/svg" className="w-5 h-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="4" x2="20" y1="12" y2="12"/><line x1="4" x2="20" y1="6" y2="6"/><line x1="4" x2="20" y1="18" y2="18"/></svg>
        </button>
      )}

      {/* Left: Logo */}
      <Link href="/dashboard" className="flex items-center gap-2 shrink-0">
        <span className="text-lg font-bold font-quicksand">MeepleAI</span>
      </Link>

      {/* Center: Section title */}
      <div className="flex-1 flex items-center justify-center min-w-0 mx-4">
        <span className="text-sm font-medium text-muted-foreground font-quicksand truncate">
          {sectionTitle}
        </span>
      </div>

      {/* Right: Notification bell + User menu */}
      <div className="flex items-center gap-2 shrink-0">
        <NotificationBell />
        <UserMenuDropdown />
      </div>
    </header>
  );
}
```

- [ ] **Step 4: Create `UserDesktopSidebar.tsx`**

Copy `alpha/AlphaDesktopSidebar.tsx` → `UserShell/UserDesktopSidebar.tsx`.

Changes:
- Replace `import { useAlphaNav, type AlphaTab } from '@/hooks/useAlphaNav'` with `import { useNavigation, type NavTab } from '@/hooks/useNavigation'`
- Replace all `AlphaTab` type references with `NavTab`
- Replace `useAlphaNav()` call with `useNavigation()`
- **Remove `isAdmin` prop entirely** from `interface` AND `function signature` (it's non-optional `isAdmin: boolean` — leaving it causes TS error since `UserShellClient` passes no props)
- **Delete the entire admin link section** at the bottom of the component (the `{isAdmin && (...)}` block with the Shield icon, ~lines 135-159)
- Remove `className` prop from interface and function signature (not needed)
- Remove unused imports: `Link` from next/link, `Shield` from lucide-react
- Update data-testid: `alpha-desktop-sidebar` → `user-desktop-sidebar`

- [ ] **Step 5: Create `UserTabBar.tsx`**

Copy `alpha/AlphaTabBar.tsx` → `UserShell/UserTabBar.tsx`.

Changes:
- Replace `import { useAlphaNav, type AlphaTab } from '@/hooks/useAlphaNav'` with `import { useNavigation, type NavTab } from '@/hooks/useNavigation'`
- Replace all `AlphaTab` type references with `NavTab`
- Replace `useAlphaNav()` call with `useNavigation()`
- Update data-testid: `alpha-tab-bar` → `user-tab-bar`

- [ ] **Step 6: Create `SwipeableContainer.tsx`**

Copy `alpha/SwipeableContainer.tsx` → `UserShell/SwipeableContainer.tsx`.

Changes:
- Replace `import { useAlphaNav, type AlphaTab } from '@/hooks/useAlphaNav'` with `import { useNavigation, type NavTab } from '@/hooks/useNavigation'`
- Replace `AlphaTab` in `TAB_ORDER` type with `NavTab`
- Replace `useAlphaNav()` call with `useNavigation()`

- [ ] **Step 7: Create `index.ts`**

```ts
export { UserShell } from './UserShell';
export { UserShellClient } from './UserShellClient';
export { UserTopNav } from './UserTopNav';
```

- [ ] **Step 8: Verify TypeScript compiles**

Run: `cd apps/web && npx tsc --noEmit 2>&1 | head -20`
Expected: No errors

- [ ] **Step 9: Commit**

```bash
git add apps/web/src/components/layout/UserShell/
git commit -m "feat(layout): create UserShell directory from alpha components"
```

---

## Task 4: Create `AdminShell/` directory

**Files:**
- Create: `apps/web/src/components/layout/AdminShell/AdminShell.tsx`
- Create: `apps/web/src/components/layout/AdminShell/index.ts`
- Move: `AdminTabSidebar.tsx`, `AdminBreadcrumb.tsx`, `AdminMobileDrawer.tsx` from `UnifiedShell/`

- [ ] **Step 1: Copy admin components from UnifiedShell/**

Copy these 3 files from `apps/web/src/components/layout/UnifiedShell/` to `apps/web/src/components/layout/AdminShell/`:
- `AdminTabSidebar.tsx` — no changes needed
- `AdminBreadcrumb.tsx` — no changes needed
- `AdminMobileDrawer.tsx` — update import from `'./AdminTabSidebar'` (already relative, stays the same)

- [ ] **Step 2: Create `AdminShell.tsx`**

```tsx
'use client';

import { type ReactNode, useState } from 'react';

import { DashboardEngineProvider } from '@/components/dashboard-v2';

import { UserTopNav } from '../UserShell/UserTopNav';

import { AdminBreadcrumb } from './AdminBreadcrumb';
import { AdminMobileDrawer } from './AdminMobileDrawer';
import { AdminTabSidebar } from './AdminTabSidebar';

interface AdminShellProps {
  children: ReactNode;
}

export function AdminShell({ children }: AdminShellProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);

  return (
    <div className="flex h-dvh bg-background">
      <AdminTabSidebar />

      <div className="flex flex-col flex-1 min-w-0">
        <UserTopNav
          isAdmin
          onMenuToggle={() => setDrawerOpen(true)}
          isMenuOpen={drawerOpen}
        />
        <AdminBreadcrumb />
        <AdminMobileDrawer open={drawerOpen} onOpenChange={setDrawerOpen} />

        <DashboardEngineProvider>
          <main className="flex-1 overflow-y-auto">{children}</main>
        </DashboardEngineProvider>
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Create `index.ts`**

```ts
export { AdminShell } from './AdminShell';
```

- [ ] **Step 4: Verify TypeScript compiles**

Run: `cd apps/web && npx tsc --noEmit 2>&1 | head -20`
Expected: No errors

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/AdminShell/
git commit -m "feat(layout): create AdminShell with admin sidebar and shared top nav"
```

---

## Task 5: Wire new shells into route layouts

**Files:**
- Modify: `apps/web/src/app/(authenticated)/layout.tsx`
- Modify: `apps/web/src/app/(chat)/layout.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/layout.tsx`
- Modify: `apps/web/src/components/layout/UnifiedHeader.tsx`

- [ ] **Step 1: Update `(authenticated)/layout.tsx`**

Replace entire file content:

```tsx
/**
 * Authenticated Route Group Layout
 *
 * Uses UserShell for all authenticated user pages.
 * Admin pages use a separate AdminShell via admin/(dashboard)/layout.tsx.
 */

import { type ReactNode } from 'react';

import { UserShell } from '@/components/layout/UserShell';

export default async function AuthenticatedRouteLayout({ children }: { children: ReactNode }) {
  return <UserShell>{children}</UserShell>;
}
```

- [ ] **Step 2: Update `(chat)/layout.tsx`**

Replace entire file content:

```tsx
/**
 * Chat Route Group Layout
 *
 * Uses UserShell for the chat experience.
 * (chat) is a sibling of (authenticated), so it needs its own shell wrapper.
 */

import { type ReactNode } from 'react';

import { UserShell } from '@/components/layout/UserShell';

export default function ChatLayout({ children }: { children: ReactNode }) {
  return <UserShell>{children}</UserShell>;
}
```

- [ ] **Step 3: Update `admin/(dashboard)/layout.tsx`**

Replace `UnifiedShell` with `AdminShell`:

```tsx
import { type ReactNode } from 'react';

import { type Metadata } from 'next';

import { PdfProcessingNotifier } from '@/components/admin/layout/PdfProcessingNotifier';
import { RequireRole } from '@/components/auth/RequireRole';
import { AdminShell } from '@/components/layout/AdminShell';

export const metadata: Metadata = {
  title: {
    template: '%s | MeepleAI Admin',
    default: 'Admin Dashboard | MeepleAI',
  },
  description: 'MeepleAI Administration Dashboard',
};

export default function DashboardLayout({ children }: { children: ReactNode }) {
  return (
    <RequireRole allowedRoles={['Admin']}>
      <PdfProcessingNotifier />
      <AdminShell>{children}</AdminShell>
    </RequireRole>
  );
}
```

- [ ] **Step 4: Update `UnifiedHeader.tsx`**

Replace the inline `UserMenu` function component (lines 79-185) and its supporting logic with `<UserMenuDropdown />`.

Remove these imports (now in UserMenuDropdown):
- `useState, useEffect, useTransition` (keep `useState, useEffect` for scroll)
- `Settings, Shield, LogOut, UserIcon, User, FileEdit` from lucide-react
- `logoutAction`
- `DropdownMenu*` components
- `ThemeToggle`
- `Button`
- `useCurrentUser`
- `useNavigationItems`

Add import:
```tsx
import { UserMenuDropdown } from './UserMenuDropdown';
```

Replace `<UserMenu />` (line 212) with `<UserMenuDropdown />`.

Delete the entire `const UserMenu = () => { ... }` function (lines 79-185) and the supporting variables `isAdmin`, `isEditor`, `handleLogout`, `userInitial`, `isLoggingOut`.

Keep: `isScrolled` state + scroll effect, `MeepleLogo`, `NotificationBell` (needs `isAuthenticated` check — keep `useNavigationItems` for that).

- [ ] **Step 5: Verify TypeScript compiles**

Run: `cd apps/web && npx tsc --noEmit 2>&1 | head -20`
Expected: No errors

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/(authenticated)/layout.tsx \
  apps/web/src/app/(chat)/layout.tsx \
  apps/web/src/app/admin/(dashboard)/layout.tsx \
  apps/web/src/components/layout/UnifiedHeader.tsx
git commit -m "refactor(layout): wire UserShell and AdminShell into route layouts"
```

---

## Task 6: Verify application builds and runs

- [ ] **Step 1: Full TypeScript check**

Run: `cd apps/web && npx tsc --noEmit`
Expected: Clean output, no errors

- [ ] **Step 2: Lint check**

Run: `cd apps/web && pnpm lint 2>&1 | tail -20`
Expected: No new errors (warnings OK)

- [ ] **Step 3: Build check**

Run: `cd apps/web && pnpm build 2>&1 | tail -30`
Expected: Build succeeds. Some pages may have warnings about unused imports in old files — that's OK, they'll be deleted in Task 7.

- [ ] **Step 4: Fix any issues found**

If TypeScript or build errors occur, fix them before proceeding.

- [ ] **Step 5: Commit any fixes**

```bash
git add -A && git commit -m "fix(layout): resolve build issues from layout unification"
```

---

## Task 7: Delete old layout system

**IMPORTANT:** Only proceed after Task 6 confirms the build works.

- [ ] **Step 1: Delete `alpha/` directory**

```bash
rm -rf apps/web/src/components/layout/alpha/
```

- [ ] **Step 2: Delete old hook and store files**

```bash
rm apps/web/src/hooks/useAlphaNav.ts
rm apps/web/src/lib/stores/alphaNavStore.ts
```

- [ ] **Step 3: Verify build still works after alpha deletion**

Run: `cd apps/web && npx tsc --noEmit 2>&1 | head -20`
Expected: No errors (all consumers now use new paths)

- [ ] **Step 4: Commit alpha deletion**

```bash
git add -A && git commit -m "refactor(layout): delete alpha/ directory and old nav store"
```

- [ ] **Step 5: Delete `UnifiedShell/` directory**

```bash
rm -rf apps/web/src/components/layout/UnifiedShell/
```

- [ ] **Step 6: Delete `MobileTabBar/` and `ContextualBottomSheet/` directories**

```bash
rm -rf apps/web/src/components/layout/MobileTabBar/
rm -rf apps/web/src/components/layout/ContextualBottomSheet/
```

- [ ] **Step 7: Delete sheet components**

```bash
rm apps/web/src/components/sheets/SearchAgentSheet.tsx
rm apps/web/src/components/sheets/SearchGameSheet.tsx
rm apps/web/src/components/sheets/SessionSheet.tsx
rm apps/web/src/components/sheets/ToolkitSheet.tsx
```

- [ ] **Step 8: Delete hooks**

```bash
rm apps/web/src/hooks/useBottomNavActions.ts
rm apps/web/src/hooks/usePlaceholderActions.ts
rm apps/web/src/hooks/useContextualSheetActions.ts
rm apps/web/src/hooks/useScrollHideNav.ts
rm apps/web/src/hooks/useBottomPadding.ts
rm apps/web/src/hooks/useContextualEntity.ts
```

- [ ] **Step 9: Delete remaining test files**

Note: `MobileTabBar/__tests__/` and `ContextualBottomSheet/__tests__/` were already removed by `rm -rf` in Steps 5-6. These are the remaining standalone test files:

```bash
rm -f apps/web/src/__tests__/components/sheets/SessionSheet.test.tsx
rm -f apps/web/src/__tests__/components/sheets/SearchGameSheet.test.tsx
rm -f apps/web/src/__tests__/components/toolkit/toolkit.test.tsx
rm -f apps/web/src/__tests__/config/placeholder-action-cards.test.ts
rm -f apps/web/src/hooks/__tests__/useContextualEntity.test.ts
```

- [ ] **Step 10: Fix broken imports**

Run: `cd apps/web && npx tsc --noEmit 2>&1`

Grep for any remaining imports of deleted modules:
```bash
cd apps/web/src && grep -r "UnifiedShell\|from.*alpha\|useAlphaNav\|alphaNavStore\|MobileTabBar\|ContextualBottomSheet\|SearchAgentSheet\|SearchGameSheet\|SessionSheet\|ToolkitSheet\|useBottomNavActions\|usePlaceholderActions\|useContextualSheetActions\|useScrollHideNav\|useBottomPadding\|useContextualEntity" --include="*.ts" --include="*.tsx" -l
```

Fix any remaining broken imports found. Common patterns:
- Files importing from `@/components/layout/UnifiedShell` → remove or redirect
- Files importing deleted hooks → remove the import and any usage

- [ ] **Step 11: Verify clean build**

Run: `cd apps/web && npx tsc --noEmit && pnpm build 2>&1 | tail -10`
Expected: Clean build, no errors

- [ ] **Step 12: Commit all deletions**

```bash
git add -A && git commit -m "refactor(layout): delete UnifiedShell, MobileTabBar, ContextualBottomSheet, old sheets and hooks"
```

---

## Task 8: Final verification and cleanup

- [ ] **Step 1: Run full test suite**

Run: `cd apps/web && pnpm test 2>&1 | tail -30`

Expect some test failures from tests that import deleted components. Fix by removing dead test files or updating imports.

- [ ] **Step 2: Run lint**

Run: `cd apps/web && pnpm lint 2>&1 | tail -20`

Fix any new lint errors (unused imports in modified files).

- [ ] **Step 3: Verify file structure matches spec**

```bash
ls -la apps/web/src/components/layout/UserShell/
ls -la apps/web/src/components/layout/AdminShell/
ls apps/web/src/components/layout/UserMenuDropdown.tsx
ls apps/web/src/components/layout/UnifiedHeader.tsx
```

Expected: All files exist as specified in the spec's "File Structure (After)" section.

Verify deleted directories are gone:
```bash
ls apps/web/src/components/layout/UnifiedShell/ 2>&1  # Should: No such file
ls apps/web/src/components/layout/alpha/ 2>&1          # Should: No such file
ls apps/web/src/components/layout/MobileTabBar/ 2>&1   # Should: No such file
```

- [ ] **Step 4: Commit any final fixes**

```bash
git add -A && git commit -m "chore(layout): final cleanup after layout unification"
```
