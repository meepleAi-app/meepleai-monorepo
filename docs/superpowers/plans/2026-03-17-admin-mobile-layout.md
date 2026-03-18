# Admin Mobile Layout Redesign — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the admin layout usable on smartphones by hiding the sidebar on mobile and adding a hamburger drawer + breadcrumb bar.

**Architecture:** CSS-only responsive gating at `md` (768px). Below `md`, sidebar hides (`hidden md:flex`), hamburger + breadcrumb appear (`md:hidden`). Components always mount in admin context — visibility controlled by CSS, not JS `isDesktop`.

**Tech Stack:** Next.js 16, React 19, Tailwind 4, shadcn/ui Sheet, Lucide icons

**Spec:** `docs/superpowers/specs/2026-03-17-admin-mobile-layout-design.md`

---

## File Map

| Action | File | Responsibility |
|--------|------|---------------|
| Modify | `apps/web/src/components/layout/UnifiedShell/AdminTabSidebar.tsx` | Add `hidden md:flex` to root |
| Modify | `apps/web/src/components/layout/UnifiedShell/UnifiedTopNav.tsx` | Add hamburger button (admin + mobile) |
| Modify | `apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx` | Wire drawer state, mount new components |
| Create | `apps/web/src/components/layout/UnifiedShell/AdminMobileDrawer.tsx` | Sheet wrapping AdminTabSidebar |
| Create | `apps/web/src/components/layout/UnifiedShell/AdminBreadcrumb.tsx` | Breadcrumb bar with dropdown |
| Create | `apps/web/src/components/layout/UnifiedShell/__tests__/AdminMobileDrawer.test.tsx` | Unit tests for drawer |
| Create | `apps/web/src/components/layout/UnifiedShell/__tests__/AdminBreadcrumb.test.tsx` | Unit tests for breadcrumb |
| Modify | `apps/web/src/components/layout/UnifiedShell/__tests__/AdminTabSidebar.test.tsx` | Test responsive hiding |
| Modify | `apps/web/src/components/layout/UnifiedShell/__tests__/UnifiedTopNav.test.tsx` | Test hamburger button |
| Create | `apps/web/e2e/admin/admin-mobile-nav.spec.ts` | E2E mobile navigation tests |

---

## Chunk 1: AdminTabSidebar — Hide on Mobile

### Task 1: Hide AdminTabSidebar below md breakpoint

**Files:**
- Modify: `apps/web/src/components/layout/UnifiedShell/AdminTabSidebar.tsx:19-21`
- Modify: `apps/web/src/components/layout/UnifiedShell/__tests__/AdminTabSidebar.test.tsx`

- [ ] **Step 1: Add test for responsive hiding**

Add to `apps/web/src/components/layout/UnifiedShell/__tests__/AdminTabSidebar.test.tsx`:

```tsx
it('has responsive classes to hide on mobile', () => {
  render(<AdminTabSidebar />);
  const sidebar = screen.getByTestId('admin-tab-sidebar');
  expect(sidebar.className).toMatch(/hidden/);
  expect(sidebar.className).toMatch(/md:flex/);
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/AdminTabSidebar.test.tsx`

Expected: FAIL — className does not contain `hidden` or `md:flex`.

- [ ] **Step 3: Add responsive classes to AdminTabSidebar**

In `apps/web/src/components/layout/UnifiedShell/AdminTabSidebar.tsx`, change line 19-21:

```tsx
// BEFORE
className={cn(
  'flex flex-col h-full w-[200px] border-r border-border/30',
  'bg-background/50 backdrop-blur-sm'
)}

// AFTER
className={cn(
  'hidden md:flex flex-col h-full w-[200px] border-r border-border/30',
  'bg-background/50 backdrop-blur-sm'
)}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/AdminTabSidebar.test.tsx`

Expected: All 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/AdminTabSidebar.tsx apps/web/src/components/layout/UnifiedShell/__tests__/AdminTabSidebar.test.tsx
git commit -m "feat(admin): hide sidebar on mobile with hidden md:flex"
```

---

## Chunk 2: AdminMobileDrawer — Sheet Wrapping Sidebar

### Task 2: Create AdminMobileDrawer component

**Files:**
- Create: `apps/web/src/components/layout/UnifiedShell/AdminMobileDrawer.tsx`
- Create: `apps/web/src/components/layout/UnifiedShell/__tests__/AdminMobileDrawer.test.tsx`

- [ ] **Step 1: Write failing tests for AdminMobileDrawer**

Create `apps/web/src/components/layout/UnifiedShell/__tests__/AdminMobileDrawer.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AdminMobileDrawer } from '../AdminMobileDrawer';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(() => '/admin/overview'),
}));

describe('AdminMobileDrawer', () => {
  it('renders AdminTabSidebar inside Sheet when open', () => {
    render(<AdminMobileDrawer open={true} onOpenChange={vi.fn()} />);
    expect(screen.getByTestId('admin-tab-sidebar')).toBeInTheDocument();
  });

  it('does not render when open is false', () => {
    const { container } = render(<AdminMobileDrawer open={false} onOpenChange={vi.fn()} />);
    // Component returns null when closed
    expect(container.firstElementChild).toBeNull();
  });

  it('calls onOpenChange when close is triggered', async () => {
    const onOpenChange = vi.fn();
    render(<AdminMobileDrawer open={true} onOpenChange={onOpenChange} />);
    // shadcn Sheet has a close button with sr-only text "Close"
    const closeBtn = screen.getByRole('button', { name: /close/i });
    await userEvent.click(closeBtn);
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/AdminMobileDrawer.test.tsx`

Expected: FAIL — module not found.

- [ ] **Step 3: Implement AdminMobileDrawer**

Create `apps/web/src/components/layout/UnifiedShell/AdminMobileDrawer.tsx`:

```tsx
'use client';

import { useEffect } from 'react';

import { usePathname } from 'next/navigation';

import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/navigation/sheet';

import { AdminTabSidebar } from './AdminTabSidebar';

interface AdminMobileDrawerProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function AdminMobileDrawer({ open, onOpenChange }: AdminMobileDrawerProps) {
  const pathname = usePathname();

  // Close drawer on navigation
  useEffect(() => {
    if (open) {
      onOpenChange(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pathname]);

  // Don't render Sheet at all when closed — avoids portal/overlay leaking to desktop
  if (!open) return null;

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent
        side="left"
        className="w-[260px] p-0"
        id="admin-mobile-drawer"
      >
        <SheetHeader className="sr-only">
          <SheetTitle>Admin Navigation</SheetTitle>
        </SheetHeader>
        <AdminTabSidebar forceVisible />
      </SheetContent>
    </Sheet>
  );
}
```

**Key decisions:**
- Returns `null` when closed — no Sheet portal leaking to desktop, no CSS hiding needed
- Uses `forceVisible` prop on AdminTabSidebar to override `hidden md:flex` (avoids Tailwind class specificity issues)

- [ ] **Step 3b: Update AdminTabSidebar to accept forceVisible prop**

In `apps/web/src/components/layout/UnifiedShell/AdminTabSidebar.tsx`, add a `forceVisible` prop:

```tsx
// Change function signature:
interface AdminTabSidebarProps {
  /** Force visibility when rendered inside Sheet drawer (overrides hidden md:flex) */
  forceVisible?: boolean;
}

export function AdminTabSidebar({ forceVisible }: AdminTabSidebarProps) {
  const pathname = usePathname();
  const activeSection = getActiveSection(pathname);

  return (
    <div
      className={cn(
        forceVisible
          ? 'flex flex-col h-full w-full'
          : 'hidden md:flex flex-col h-full w-[200px] border-r border-border/30',
        'bg-background/50 backdrop-blur-sm'
      )}
      data-testid="admin-tab-sidebar"
    >
```

When `forceVisible` is true, sidebar uses `flex flex-col` (always visible, full width, no border — Sheet controls width). When false (default), uses the responsive `hidden md:flex` with fixed 200px width.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/AdminMobileDrawer.test.tsx`

Expected: All 4 tests PASS.

- [ ] **Step 5: Run existing AdminTabSidebar tests to verify no regression**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/AdminTabSidebar.test.tsx`

Expected: All 4 tests PASS (the new `className` prop is optional, existing usage unchanged).

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/AdminMobileDrawer.tsx apps/web/src/components/layout/UnifiedShell/AdminTabSidebar.tsx apps/web/src/components/layout/UnifiedShell/__tests__/AdminMobileDrawer.test.tsx
git commit -m "feat(admin): add AdminMobileDrawer with Sheet wrapping sidebar"
```

---

## Chunk 3: Hamburger Button in UnifiedTopNav

### Task 3: Add hamburger button to UnifiedTopNav

**Files:**
- Modify: `apps/web/src/components/layout/UnifiedShell/UnifiedTopNav.tsx`
- Modify: `apps/web/src/components/layout/UnifiedShell/__tests__/UnifiedTopNav.test.tsx`

- [ ] **Step 1: Write failing tests for hamburger button**

Add to `apps/web/src/components/layout/UnifiedShell/__tests__/UnifiedTopNav.test.tsx`:

```tsx
import userEvent from '@testing-library/user-event';

// Add these tests inside the existing describe block (they share the beforeEach cleanup):

it('shows hamburger button for admin users', () => {
  const onMenuToggle = vi.fn();
  render(<UnifiedTopNav isAdmin={true} onMenuToggle={onMenuToggle} />);
  const hamburger = screen.getByRole('button', { name: /open admin menu/i });
  expect(hamburger).toBeInTheDocument();
});

it('calls onMenuToggle when hamburger is clicked', async () => {
  const onMenuToggle = vi.fn();
  render(<UnifiedTopNav isAdmin={true} onMenuToggle={onMenuToggle} />);
  const hamburger = screen.getByRole('button', { name: /open admin menu/i });
  await userEvent.click(hamburger);
  expect(onMenuToggle).toHaveBeenCalledOnce();
});

it('does not show hamburger when onMenuToggle is not provided', () => {
  render(<UnifiedTopNav isAdmin={true} />);
  expect(screen.queryByRole('button', { name: /open admin menu/i })).not.toBeInTheDocument();
});
```

Note: These go inside the existing `describe('UnifiedTopNav', ...)` block which already has the `beforeEach` that clears `useCardHand` state.

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/UnifiedTopNav.test.tsx`

Expected: FAIL — no button with "open admin menu" label.

- [ ] **Step 3: Add hamburger button to UnifiedTopNav**

In `apps/web/src/components/layout/UnifiedShell/UnifiedTopNav.tsx`:

1. Add `Menu` to lucide imports:
```tsx
import { Search, Bell, Menu } from 'lucide-react';
```

2. Add prop to interface:
```tsx
interface UnifiedTopNavProps {
  isAdmin: boolean;
  userMenu?: React.ReactNode;
  notificationBell?: React.ReactNode;
  searchTrigger?: React.ReactNode;
  miniCards?: React.ReactNode;
  /** Callback to open admin mobile drawer */
  onMenuToggle?: () => void;
  /** Whether the admin drawer is open (for aria-expanded) */
  isMenuOpen?: boolean;
}
```

3. Add to destructuring:
```tsx
export function UnifiedTopNav({
  isAdmin,
  userMenu,
  notificationBell,
  searchTrigger,
  miniCards,
  onMenuToggle,
  isMenuOpen,
}: UnifiedTopNavProps) {
```

4. Add hamburger button right after `<header>` opening, before the Logo link:
```tsx
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
    <Menu className="w-5 h-5" />
  </button>
)}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/UnifiedTopNav.test.tsx`

Expected: All 8 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/UnifiedTopNav.tsx apps/web/src/components/layout/UnifiedShell/__tests__/UnifiedTopNav.test.tsx
git commit -m "feat(admin): add hamburger button to UnifiedTopNav for mobile"
```

---

## Chunk 4: AdminBreadcrumb Component

### Task 4: Create AdminBreadcrumb component

**Files:**
- Create: `apps/web/src/components/layout/UnifiedShell/AdminBreadcrumb.tsx`
- Create: `apps/web/src/components/layout/UnifiedShell/__tests__/AdminBreadcrumb.test.tsx`

- [ ] **Step 1: Write failing tests for AdminBreadcrumb**

Create `apps/web/src/components/layout/UnifiedShell/__tests__/AdminBreadcrumb.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AdminBreadcrumb } from '../AdminBreadcrumb';

const { mockPush, mockUsePathname } = vi.hoisted(() => ({
  mockPush: vi.fn(),
  mockUsePathname: vi.fn(() => '/admin/agents/pipeline'),
}));

vi.mock('next/navigation', () => ({
  usePathname: mockUsePathname,
  useRouter: () => ({ push: mockPush }),
}));

describe('AdminBreadcrumb', () => {
  beforeEach(() => {
    mockPush.mockClear();
    mockUsePathname.mockReturnValue('/admin/agents/pipeline');
  });

  it('renders section and active sub-item from pathname', () => {
    render(<AdminBreadcrumb />);
    expect(screen.getByText('AI')).toBeInTheDocument();
    expect(screen.getByText('Pipeline Explorer')).toBeInTheDocument();
  });

  it('shows chevron when section has multiple sub-items', () => {
    render(<AdminBreadcrumb />);
    expect(screen.getByRole('button', { name: /show sub-sections/i })).toBeInTheDocument();
  });

  it('opens dropdown on chevron click showing all sub-items', async () => {
    render(<AdminBreadcrumb />);
    await userEvent.click(screen.getByRole('button', { name: /show sub-sections/i }));
    // AI section has 13 items — check a few
    expect(screen.getByText('All Agents')).toBeInTheDocument();
    expect(screen.getByText('Debug Console')).toBeInTheDocument();
    expect(screen.getByText('Usage & Costs')).toBeInTheDocument();
  });

  it('navigates when dropdown item is clicked', async () => {
    render(<AdminBreadcrumb />);
    await userEvent.click(screen.getByRole('button', { name: /show sub-sections/i }));
    await userEvent.click(screen.getByText('Debug Console'));
    expect(mockPush).toHaveBeenCalledWith('/admin/agents/debug');
  });

  it('closes dropdown on outside click', async () => {
    render(<AdminBreadcrumb />);
    await userEvent.click(screen.getByRole('button', { name: /show sub-sections/i }));
    expect(screen.getByText('All Agents')).toBeInTheDocument();
    // Click outside
    await userEvent.click(document.body);
    expect(screen.queryByText('All Agents')).not.toBeInTheDocument();
  });

  it('shows fallback for unmapped paths', () => {
    mockUsePathname.mockReturnValue('/admin/unknown/deep');
    render(<AdminBreadcrumb />);
    // Should capitalize URL segment as fallback
    expect(screen.getByText('Unknown')).toBeInTheDocument();
  });

  it('shows only section name for root admin path', () => {
    mockUsePathname.mockReturnValue('/admin/overview');
    render(<AdminBreadcrumb />);
    expect(screen.getByText('Overview')).toBeInTheDocument();
  });

  it('navigates to section hub when section name is clicked', async () => {
    render(<AdminBreadcrumb />);
    const sectionLink = screen.getByText('AI').closest('a');
    expect(sectionLink).toHaveAttribute('href', '/admin/agents');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/AdminBreadcrumb.test.tsx`

Expected: FAIL — module not found.

- [ ] **Step 3: Implement AdminBreadcrumb**

Create `apps/web/src/components/layout/UnifiedShell/AdminBreadcrumb.tsx`:

```tsx
'use client';

import { useEffect, useRef, useState } from 'react';

import { ChevronDown } from 'lucide-react';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';

import {
  DASHBOARD_SECTIONS,
  getActiveSection,
  isSidebarItemActive,
} from '@/config/admin-dashboard-navigation';
import { cn } from '@/lib/utils';

export function AdminBreadcrumb() {
  const pathname = usePathname();
  const router = useRouter();
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const activeSection = getActiveSection(pathname);

  // Find active sub-item
  const activeItem = activeSection?.sidebarItems.find(item =>
    isSidebarItemActive(item, pathname)
  );

  // Fallback: capitalize last pathname segment
  const fallbackLabel = (() => {
    if (activeSection) return null;
    const segments = pathname.replace(/^\/admin\/?/, '').split('/').filter(Boolean);
    if (segments.length === 0) return 'Overview';
    return segments[0].charAt(0).toUpperCase() + segments[0].slice(1);
  })();

  const hasSubItems = (activeSection?.sidebarItems.length ?? 0) > 1;

  // Close dropdown on outside click
  useEffect(() => {
    if (!dropdownOpen) return;
    function handleClick(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setDropdownOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [dropdownOpen]);

  // Close dropdown on navigation
  useEffect(() => {
    setDropdownOpen(false);
  }, [pathname]);

  return (
    <div className="md:hidden" ref={dropdownRef}>
      <div
        className={cn(
          'h-9 bg-background/80 backdrop-blur-sm',
          'border-b',
          dropdownOpen ? 'border-primary' : 'border-border/40',
          'flex items-center px-4 gap-1.5'
        )}
        data-testid="admin-breadcrumb"
      >
        {activeSection ? (
          <>
            <Link
              href={activeSection.baseRoute}
              className="text-xs font-semibold text-primary font-quicksand hover:underline"
            >
              {activeSection.label}
            </Link>
            {activeItem && (
              <>
                <span className="text-xs text-muted-foreground">›</span>
                <span className="text-xs font-medium text-foreground font-nunito truncate">
                  {activeItem.label}
                </span>
              </>
            )}
          </>
        ) : (
          <span className="text-xs font-semibold text-primary font-quicksand">
            {fallbackLabel}
          </span>
        )}

        {hasSubItems && (
          <button
            type="button"
            className="ml-auto flex items-center justify-center w-6 h-6 rounded text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors"
            onClick={() => setDropdownOpen(prev => !prev)}
            aria-label="Show sub-sections"
            aria-expanded={dropdownOpen}
          >
            <ChevronDown
              className={cn('w-3.5 h-3.5 transition-transform', dropdownOpen && 'rotate-180')}
            />
          </button>
        )}
      </div>

      {/* Dropdown */}
      {dropdownOpen && activeSection && (
        <div
          className="absolute left-0 right-0 z-50 bg-popover border-b-2 border-primary shadow-lg"
          data-testid="admin-breadcrumb-dropdown"
        >
          <div className="py-1">
            {activeSection.sidebarItems.map(item => {
              const isActive = isSidebarItemActive(item, pathname);
              const ItemIcon = item.icon;

              return (
                <button
                  key={item.href}
                  type="button"
                  className={cn(
                    'flex items-center gap-2.5 w-full px-4 py-2.5 text-left',
                    'transition-colors border-l-[3px]',
                    isActive
                      ? 'bg-primary/5 border-primary text-foreground font-medium'
                      : 'border-transparent text-muted-foreground hover:bg-muted/50 hover:text-foreground'
                  )}
                  onClick={() => {
                    router.push(item.href);
                    setDropdownOpen(false);
                  }}
                >
                  <ItemIcon className="w-4 h-4 shrink-0" />
                  <div className="min-w-0">
                    <div className="text-sm font-nunito">{item.label}</div>
                    {isActive && (
                      <div className="text-[10px] text-primary">current page</div>
                    )}
                  </div>
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/AdminBreadcrumb.test.tsx`

Expected: All 7 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/AdminBreadcrumb.tsx apps/web/src/components/layout/UnifiedShell/__tests__/AdminBreadcrumb.test.tsx
git commit -m "feat(admin): add AdminBreadcrumb with section dropdown navigation"
```

---

## Chunk 5: Wire Everything in UnifiedShellClient

### Task 5: Integrate drawer and breadcrumb into UnifiedShellClient

**Files:**
- Modify: `apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx`

- [ ] **Step 1: Add imports for new components**

At the top of `UnifiedShellClient.tsx`, add imports after line 25:

```tsx
import { AdminBreadcrumb } from './AdminBreadcrumb';
import { AdminMobileDrawer } from './AdminMobileDrawer';
```

- [ ] **Step 2: Add drawerOpen state**

Inside `UnifiedShellClient` function, after line 64 (`const { handleCardClick, activeSheet, closeSheet } = usePlaceholderActions();`), add:

```tsx
const [adminDrawerOpen, setAdminDrawerOpen] = useState(false);
```

- [ ] **Step 3: Pass onMenuToggle to UnifiedTopNav**

Update the `<UnifiedTopNav>` JSX (around line 113) to add the new props:

```tsx
<UnifiedTopNav
  isAdmin={isAdmin}
  onMenuToggle={() => setAdminDrawerOpen(true)}
  isMenuOpen={adminDrawerOpen}
  userMenu={userMenu}
  notificationBell={notificationBell}
  searchTrigger={searchTrigger}
  miniCards={
    !isDesktop && isHandCollapsed && cards.length > 0 ? (
      <NavbarMiniCards
        cards={cards}
        onExpand={id => {
          expandHand();
          const idx = cards.findIndex(c => c.id === id);
          if (idx >= 0) focusCard(idx);
        }}
      />
    ) : undefined
  }
/>
```

- [ ] **Step 4: Add AdminBreadcrumb and AdminMobileDrawer**

After the `</ErrorBoundary>` for UnifiedTopNav (line 131), and before `{/* Hand Drawer */}` (line 133), add:

```tsx
{/* Admin mobile: breadcrumb + drawer */}
{isAdminContext && (
  <>
    <AdminBreadcrumb />
    <AdminMobileDrawer
      open={adminDrawerOpen}
      onOpenChange={setAdminDrawerOpen}
    />
  </>
)}
```

- [ ] **Step 5: Run all UnifiedShell tests to verify no regression**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/`

Expected: All existing tests PASS.

- [ ] **Step 6: Run typecheck**

Run: `cd apps/web && pnpm tsc --noEmit --pretty 2>&1 | head -30`

Expected: No new errors.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/layout/UnifiedShell/UnifiedShellClient.tsx
git commit -m "feat(admin): wire hamburger drawer and breadcrumb into UnifiedShellClient"
```

---

## Chunk 6: E2E Tests

### Task 6: Add Playwright E2E tests for mobile admin navigation

**Files:**
- Create: `apps/web/e2e/admin/admin-mobile-nav.spec.ts`

- [ ] **Step 1: Create E2E test file**

Create `apps/web/e2e/admin/admin-mobile-nav.spec.ts`:

```ts
/**
 * Admin Mobile Navigation — E2E Tests
 * Validates hamburger drawer + breadcrumb on mobile viewports
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function mockAdminAuth(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/auth/me`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { id: 'admin-1', email: 'admin@meepleai.dev', displayName: 'Admin', role: 'Admin' },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    })
  );
}

async function mockAdminApi(page: Page) {
  // Mock common admin API endpoints to prevent 404s
  await page.context().route(`${API_BASE}/api/v1/**`, (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: '{}' })
  );
}

test.describe('Admin Mobile Navigation', () => {
  test.describe('Mobile viewport (375x812)', () => {
    test.use({ viewport: { width: 375, height: 812 } });

    test.beforeEach(async ({ page }) => {
      await mockAdminAuth(page);
      await mockAdminApi(page);
    });

    test('sidebar is hidden on mobile', async ({ page }) => {
      await page.goto('/admin/overview');
      await expect(page.getByTestId('admin-tab-sidebar').first()).toBeHidden();
    });

    test('hamburger button is visible and opens drawer', async ({ page }) => {
      await page.goto('/admin/overview');
      const hamburger = page.getByRole('button', { name: /open admin menu/i }).first();
      await expect(hamburger).toBeVisible();
      await hamburger.click();

      // Drawer should show all 6 sections
      await expect(page.getByTestId('admin-tab-overview').first()).toBeVisible();
      await expect(page.getByTestId('admin-tab-content').first()).toBeVisible();
      await expect(page.getByTestId('admin-tab-ai').first()).toBeVisible();
      await expect(page.getByTestId('admin-tab-users').first()).toBeVisible();
      await expect(page.getByTestId('admin-tab-system').first()).toBeVisible();
      await expect(page.getByTestId('admin-tab-analytics').first()).toBeVisible();
    });

    test('navigation from drawer closes it', async ({ page }) => {
      await page.goto('/admin/overview');
      const hamburger = page.getByRole('button', { name: /open admin menu/i }).first();
      await hamburger.click();

      // Click on AI section
      await page.getByTestId('admin-tab-ai').first().click();
      await page.waitForURL('**/admin/agents');

      // Drawer should close after navigation
      // Verify we navigated correctly
      await expect(page).toHaveURL(/\/admin\/agents/);
    });

    test('breadcrumb shows current section', async ({ page }) => {
      await page.goto('/admin/agents/pipeline');
      const breadcrumb = page.getByTestId('admin-breadcrumb').first();
      await expect(breadcrumb).toBeVisible();
      await expect(breadcrumb.getByText('AI').first()).toBeVisible();
    });

    test('breadcrumb dropdown shows sub-sections', async ({ page }) => {
      await page.goto('/admin/agents/pipeline');
      const chevron = page.getByRole('button', { name: /show sub-sections/i }).first();
      await chevron.click();

      const dropdown = page.getByTestId('admin-breadcrumb-dropdown').first();
      await expect(dropdown).toBeVisible();
      await expect(dropdown.getByText('All Agents').first()).toBeVisible();
      await expect(dropdown.getByText('Debug Console').first()).toBeVisible();
    });

    test('navigate from breadcrumb dropdown', async ({ page }) => {
      await page.goto('/admin/agents/pipeline');
      const chevron = page.getByRole('button', { name: /show sub-sections/i }).first();
      await chevron.click();

      await page.getByTestId('admin-breadcrumb-dropdown').first().getByText('Debug Console').first().click();
      await page.waitForURL('**/admin/agents/debug');
      await expect(page).toHaveURL(/\/admin\/agents\/debug/);
    });
  });

  test.describe('Desktop viewport (1280x800)', () => {
    test.use({ viewport: { width: 1280, height: 800 } });

    test.beforeEach(async ({ page }) => {
      await mockAdminAuth(page);
      await mockAdminApi(page);
    });

    test('sidebar is visible and hamburger is hidden on desktop', async ({ page }) => {
      await page.goto('/admin/overview');
      await expect(page.getByTestId('admin-tab-sidebar').first()).toBeVisible();
      await expect(
        page.getByRole('button', { name: /open admin menu/i })
      ).toHaveCount(0);
    });
  });

  test.describe('Viewport resize transition', () => {
    test.beforeEach(async ({ page }) => {
      await mockAdminAuth(page);
      await mockAdminApi(page);
    });

    test('resize from desktop to mobile hides sidebar and shows hamburger', async ({ page }) => {
      // Start at desktop
      await page.setViewportSize({ width: 1280, height: 800 });
      await page.goto('/admin/overview');
      await expect(page.getByTestId('admin-tab-sidebar').first()).toBeVisible();

      // Resize to mobile
      await page.setViewportSize({ width: 375, height: 812 });
      await expect(page.getByTestId('admin-tab-sidebar').first()).toBeHidden();
      await expect(page.getByRole('button', { name: /open admin menu/i }).first()).toBeVisible();
    });
  });
});
```

- [ ] **Step 2: Commit E2E tests**

```bash
git add apps/web/e2e/admin/admin-mobile-nav.spec.ts
git commit -m "test(admin): add E2E tests for mobile admin navigation"
```

- [ ] **Step 3: Run E2E tests (optional, requires dev server)**

Run: `cd apps/web && PLAYWRIGHT_AUTH_BYPASS=true pnpm playwright test e2e/admin/admin-mobile-nav.spec.ts --project=chromium`

Expected: All 7 tests PASS. If dev server is not running, skip this step.

---

## Chunk 7: Final Verification

### Task 7: Full test suite + typecheck

- [ ] **Step 1: Run all UnifiedShell unit tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/UnifiedShell/__tests__/`

Expected: All tests PASS (existing + new).

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && pnpm tsc --noEmit --pretty 2>&1 | head -50`

Expected: No new type errors.

- [ ] **Step 3: Run lint**

Run: `cd apps/web && pnpm lint 2>&1 | tail -20`

Expected: No new lint errors.

- [ ] **Step 4: Final commit with all remaining changes (if any)**

```bash
git status
# If any unstaged changes remain:
git add -A apps/web/src/components/layout/UnifiedShell/
git commit -m "chore(admin): cleanup and final adjustments for mobile layout"
```

- [ ] **Step 5: Create PR to frontend-dev**

The feature branch should target `frontend-dev` (parent branch per git workflow rules).

```bash
git push -u origin $(git branch --show-current)
gh pr create --base frontend-dev --title "feat(admin): mobile-responsive admin layout with hamburger + breadcrumb" --body "$(cat <<'EOF'
## Summary
- Hide AdminTabSidebar on mobile (`hidden md:flex`)
- Add hamburger button in UnifiedTopNav to open sidebar in a Sheet drawer
- Add AdminBreadcrumb bar showing current section with dropdown navigation
- Content area goes from ~175px to full width on mobile

## Test plan
- [ ] Unit tests: AdminBreadcrumb (7), AdminMobileDrawer (4), UnifiedTopNav (3 new)
- [ ] E2E tests: admin-mobile-nav.spec.ts (7 cases, mobile + desktop viewports)
- [ ] Manual: Open /admin on mobile (375px), verify sidebar hidden, hamburger works, breadcrumb shows
- [ ] Manual: Open /admin on desktop (1280px), verify sidebar visible, no hamburger

Closes #TBD

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
