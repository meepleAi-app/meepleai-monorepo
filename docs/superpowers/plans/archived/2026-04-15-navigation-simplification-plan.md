# Navigation Simplification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Simplify frontend navigation from 6 overlapping layers to 2 (TopBar + in-page content) plus ManaPips for relational navigation.

**Architecture:** Replace MiniNavSlot, HandRail, ActionPill, FloatingActionPill, ActionBar with a minimal TopBar (hamburger + logo + search + avatar), a side drawer for full navigation, in-page tabs and PageHeader for contextual nav, and a mobile-only CTA pill. Admin area follows same pattern — AdminTabSidebar replaced by AdminSideDrawer.

**Tech Stack:** Next.js 16 (App Router), React 19, Tailwind 4, shadcn/ui (Radix), Zustand, Vitest

**Spec:** `docs/superpowers/specs/2026-04-15-navigation-simplification-design.md`

---

## File Structure

### New Files to Create

| File | Responsibility |
|------|---------------|
| `src/components/layout/PageHeader.tsx` | Reusable "← Parent" back link + title + CTA + optional tabs |
| `src/components/layout/SideDrawer/SideDrawer.tsx` | User hamburger menu with nav items |
| `src/components/layout/SideDrawer/SideDrawerItems.tsx` | Nav item rendering with "Altro" collapsible |
| `src/components/layout/AdminSideDrawer/AdminSideDrawer.tsx` | Admin hamburger menu with grouped sections |
| `src/components/layout/MobileCTAPill.tsx` | Fixed bottom CTA pill, mobile only, scroll-aware |
| `src/components/layout/SearchOverlay.tsx` | Expandable search from 🔍 icon |
| `src/components/layout/UserShell/TopBarV2.tsx` | Simplified TopBar: hamburger + logo + 🔍 + avatar |
| `src/__tests__/components/layout/PageHeader.test.tsx` | Tests for PageHeader |
| `src/__tests__/components/layout/SideDrawer.test.tsx` | Tests for SideDrawer |
| `src/__tests__/components/layout/MobileCTAPill.test.tsx` | Tests for MobileCTAPill |
| `src/__tests__/components/layout/SearchOverlay.test.tsx` | Tests for SearchOverlay |
| `src/__tests__/components/layout/TopBarV2.test.tsx` | Tests for TopBarV2 |
| `src/__tests__/components/layout/AdminSideDrawer.test.tsx` | Tests for AdminSideDrawer |

### Files to Modify

| File | Change |
|------|--------|
| `src/components/layout/UserShell/DesktopShell.tsx` | Remove HandRail, MiniNavSlot, ActionPill, ActionBar, HandDrawer; add MobileCTAPill; swap TopBar → TopBarV2; add SideDrawer |
| `src/components/layout/AdminShell/AdminShell.tsx` | Remove AdminTabSidebar, AdminBreadcrumb; add AdminSideDrawer; swap header to TopBarV2 pattern |
| `src/components/layout/UserMenuDropdown.tsx` | Add Notifications menu item with badge |
| `src/app/(authenticated)/library/LibraryHub.tsx` | Remove useMiniNavConfig; add PageHeader + in-page tabs |
| `src/app/(authenticated)/library/[gameId]/layout.tsx` | Remove useMiniNavConfig; add PageHeader + in-page tabs |
| `src/app/(authenticated)/sessions/layout.tsx` | Remove useMiniNavConfig; add PageHeader + in-page tabs |
| `src/app/(authenticated)/sessions/[id]/layout.tsx` | Remove useMiniNavConfig; add PageHeader + in-page tabs |
| `src/app/(authenticated)/sessions/[id]/notes/page.tsx` | Remove explicit back button |
| `src/app/(authenticated)/play-records/layout.tsx` | Remove useMiniNavConfig; add PageHeader + in-page tabs |
| `src/app/(authenticated)/games/page.tsx` | Remove useMiniNavConfig; add PageHeader + in-page tabs |
| `src/app/(authenticated)/games/[id]/faqs/page.tsx` | Replace back button with PageHeader |
| `src/app/(authenticated)/games/[id]/reviews/page.tsx` | Replace back button with PageHeader |
| `src/app/(authenticated)/dashboard/DashboardClient.tsx` | Remove useMiniNavConfig |
| `src/app/(authenticated)/discover/page.tsx` | Remove useMiniNavConfig |
| `src/app/(authenticated)/toolkit/page.tsx` | Remove useMiniNavConfig |
| `src/app/(authenticated)/sessions/page.tsx` | Remove useMiniNavConfig (if separate from layout) |
| `src/components/session/live/SessionNavConfig.tsx` | Remove useMiniNavConfig; add in-page tabs |
| `src/config/navigation.ts` | Add drawer grouping config; add "Altro" group |

### Files to Delete (Phase 4)

| File | Reason |
|------|--------|
| `src/components/layout/UserShell/MiniNavSlot.tsx` | Replaced by in-page tabs |
| `src/components/layout/UserShell/TopBarNavLinks.tsx` | Replaced by SideDrawer |
| `src/components/layout/UserShell/TopBarChatButton.tsx` | Replaced by SideDrawer "Chat AI" |
| `src/components/layout/UserShell/RecentsBar.tsx` | Removed entirely |
| `src/components/layout/ActionPill/` | Replaced by in-page CTA + MobileCTAPill |
| `src/components/layout/FloatingActionPill.tsx` | Replaced by in-page CTA + MobileCTAPill |
| `src/components/layout/HandRail/` | Removed entirely |
| `src/components/layout/mobile/ActionBar.tsx` | Replaced by MobileCTAPill |
| `src/components/layout/mobile/HandDrawer.tsx` | Replaced by SideDrawer |
| `src/hooks/useMiniNavConfig.ts` | No longer needed |
| `src/lib/stores/mini-nav-config-store.ts` | No longer needed |
| `src/lib/hooks/useNavBreadcrumb.ts` | No longer needed |
| `src/hooks/useDrawCard.ts` | No longer needed (hand system removed) |
| `src/lib/stores/card-hand-store.ts` | No longer needed |
| `src/components/admin/AdminBreadcrumbs.tsx` | Dead code |
| `src/components/admin/BackToHub.tsx` | Dead code |
| `src/components/layout/AdminShell/AdminTabSidebar.tsx` | Replaced by AdminSideDrawer |
| `src/components/layout/AdminShell/AdminBreadcrumb.tsx` | Replaced by PageHeader |
| `src/components/layout/AdminShell/AdminMobileDrawer.tsx` | Replaced by AdminSideDrawer |
| `src/components/layout/UserShell/TopBar.tsx` | Replaced by TopBarV2 |
| `src/components/layout/UserShell/TopBarSearchPill.tsx` | Replaced by SearchOverlay |

---

## Phase 1: New Components (no breaking changes)

### Task 1: PageHeader Component

**Files:**
- Create: `apps/web/src/components/layout/PageHeader.tsx`
- Create: `apps/web/src/__tests__/components/layout/PageHeader.test.tsx`

- [ ] **Step 1: Write the test file**

```tsx
// apps/web/src/__tests__/components/layout/PageHeader.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { PageHeader } from '@/components/layout/PageHeader';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => <a href={href} {...props}>{children}</a>,
}));

describe('PageHeader', () => {
  it('renders title', () => {
    render(<PageHeader title="I Miei Giochi" />);
    expect(screen.getByRole('heading', { name: 'I Miei Giochi' })).toBeInTheDocument();
  });

  it('renders back link when parentHref and parentLabel provided', () => {
    render(<PageHeader title="Catan" parentHref="/library" parentLabel="Libreria" />);
    const backLink = screen.getByRole('link', { name: /libreria/i });
    expect(backLink).toHaveAttribute('href', '/library');
    expect(backLink).toHaveTextContent('← Libreria');
  });

  it('does not render back link when no parent props', () => {
    render(<PageHeader title="Dashboard" />);
    expect(screen.queryByText(/←/)).not.toBeInTheDocument();
  });

  it('renders primary action button', () => {
    const onClick = vi.fn();
    render(
      <PageHeader
        title="I Miei Giochi"
        primaryAction={{ label: '+ Aggiungi gioco', onClick }}
      />
    );
    expect(screen.getByRole('button', { name: '+ Aggiungi gioco' })).toBeInTheDocument();
  });

  it('renders subtitle when provided', () => {
    render(<PageHeader title="I Miei Giochi" subtitle="42 giochi" />);
    expect(screen.getByText('42 giochi')).toBeInTheDocument();
  });

  it('renders tabs when provided', () => {
    render(
      <PageHeader
        title="Libreria"
        tabs={[
          { id: 'collection', label: 'Collezione', href: '/library' },
          { id: 'wishlist', label: 'Wishlist', href: '/library/wishlist' },
        ]}
        activeTabId="collection"
      />
    );
    expect(screen.getByText('Collezione')).toBeInTheDocument();
    expect(screen.getByText('Wishlist')).toBeInTheDocument();
  });

  it('highlights active tab', () => {
    render(
      <PageHeader
        title="Libreria"
        tabs={[
          { id: 'collection', label: 'Collezione', href: '/library' },
          { id: 'wishlist', label: 'Wishlist', href: '/library/wishlist' },
        ]}
        activeTabId="collection"
      />
    );
    const activeTab = screen.getByText('Collezione').closest('a');
    expect(activeTab).toHaveAttribute('aria-current', 'page');
  });

  it('renders tab count badge when provided', () => {
    render(
      <PageHeader
        title="Libreria"
        tabs={[
          { id: 'wishlist', label: 'Wishlist', href: '/library/wishlist', count: 5 },
        ]}
        activeTabId="wishlist"
      />
    );
    expect(screen.getByText('5')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/layout/PageHeader.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement PageHeader**

```tsx
// apps/web/src/components/layout/PageHeader.tsx
'use client';

import Link from 'next/link';
import { cn } from '@/lib/utils';

export interface PageHeaderTab {
  id: string;
  label: string;
  href: string;
  count?: number;
}

export interface PageHeaderAction {
  label: string;
  onClick: () => void;
  icon?: React.ReactNode;
}

export interface PageHeaderProps {
  title: string;
  subtitle?: string;
  parentHref?: string;
  parentLabel?: string;
  primaryAction?: PageHeaderAction;
  tabs?: PageHeaderTab[];
  activeTabId?: string;
  className?: string;
}

export function PageHeader({
  title,
  subtitle,
  parentHref,
  parentLabel,
  primaryAction,
  tabs,
  activeTabId,
  className,
}: PageHeaderProps) {
  return (
    <div className={cn('px-6 pt-4 pb-0', className)}>
      {/* Back link */}
      {parentHref && parentLabel && (
        <Link
          href={parentHref}
          className="inline-flex items-center gap-1 text-sm text-[hsl(174,60%,41%)] hover:text-[hsl(174,60%,51%)] transition-colors mb-1"
        >
          ← {parentLabel}
        </Link>
      )}

      {/* Title row */}
      <div className="flex items-center justify-between gap-4 mb-1">
        <div>
          <h1 className="text-xl font-bold font-[family-name:var(--font-quicksand)] text-foreground">
            {title}
          </h1>
          {subtitle && (
            <p className="text-sm text-muted-foreground">{subtitle}</p>
          )}
        </div>
        {primaryAction && (
          <button
            onClick={primaryAction.onClick}
            className="shrink-0 inline-flex items-center gap-1.5 px-4 py-2 rounded-lg bg-[hsl(25,95%,45%)] text-white text-sm font-medium hover:bg-[hsl(25,95%,38%)] transition-colors"
          >
            {primaryAction.icon}
            {primaryAction.label}
          </button>
        )}
      </div>

      {/* Tabs */}
      {tabs && tabs.length > 0 && (
        <nav className="flex gap-0 border-b border-border mt-2 -mx-6 px-6 overflow-x-auto">
          {tabs.map((tab) => (
            <Link
              key={tab.id}
              href={tab.href}
              aria-current={tab.id === activeTabId ? 'page' : undefined}
              className={cn(
                'px-4 py-2.5 text-sm font-medium whitespace-nowrap transition-colors border-b-2 -mb-px',
                tab.id === activeTabId
                  ? 'text-[hsl(25,95%,45%)] border-[hsl(25,95%,45%)]'
                  : 'text-muted-foreground border-transparent hover:text-foreground hover:border-border'
              )}
            >
              {tab.label}
              {tab.count != null && (
                <span className="ml-1.5 text-xs bg-muted px-1.5 py-0.5 rounded-full">
                  {tab.count}
                </span>
              )}
            </Link>
          ))}
        </nav>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/layout/PageHeader.test.tsx`
Expected: PASS (7 tests)

- [ ] **Step 5: Commit**

```bash
cd apps/web && git add src/components/layout/PageHeader.tsx src/__tests__/components/layout/PageHeader.test.tsx
git commit -m "feat(frontend): add PageHeader component for in-page navigation"
```

---

### Task 2: SideDrawer Component

**Files:**
- Create: `apps/web/src/components/layout/SideDrawer/SideDrawer.tsx`
- Create: `apps/web/src/components/layout/SideDrawer/SideDrawerItems.tsx`
- Create: `apps/web/src/__tests__/components/layout/SideDrawer.test.tsx`

- [ ] **Step 1: Write tests**

```tsx
// apps/web/src/__tests__/components/layout/SideDrawer.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { SideDrawer } from '@/components/layout/SideDrawer/SideDrawer';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => <a href={href} {...props}>{children}</a>,
}));
vi.mock('next/navigation', () => ({
  usePathname: () => '/library',
}));
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({
    data: { displayName: 'Mario', email: 'mario@test.com', role: 'admin' },
  }),
}));
vi.mock('@/actions/auth', () => ({
  logoutAction: vi.fn(),
}));

describe('SideDrawer', () => {
  it('renders primary nav items when open', () => {
    render(<SideDrawer open={true} onClose={vi.fn()} />);
    expect(screen.getByText('Dashboard')).toBeInTheDocument();
    expect(screen.getByText('Libreria')).toBeInTheDocument();
    expect(screen.getByText('Sessioni')).toBeInTheDocument();
    expect(screen.getByText('Chat AI')).toBeInTheDocument();
  });

  it('does not render when closed', () => {
    render(<SideDrawer open={false} onClose={vi.fn()} />);
    expect(screen.queryByText('Dashboard')).not.toBeInTheDocument();
  });

  it('highlights active route', () => {
    render(<SideDrawer open={true} onClose={vi.fn()} />);
    const libraryItem = screen.getByText('Libreria').closest('a');
    expect(libraryItem).toHaveAttribute('aria-current', 'page');
  });

  it('toggles Altro sub-menu on click', () => {
    render(<SideDrawer open={true} onClose={vi.fn()} />);
    const altroButton = screen.getByText('Altro');
    expect(screen.queryByText('Giocatori')).not.toBeInTheDocument();
    fireEvent.click(altroButton);
    expect(screen.getByText('Giocatori')).toBeInTheDocument();
  });

  it('shows Admin Hub for admin users', () => {
    render(<SideDrawer open={true} onClose={vi.fn()} />);
    expect(screen.getByText('Admin Hub')).toBeInTheDocument();
  });

  it('calls onClose when overlay clicked', () => {
    const onClose = vi.fn();
    render(<SideDrawer open={true} onClose={onClose} />);
    const overlay = screen.getByTestId('drawer-overlay');
    fireEvent.click(overlay);
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('shows user info', () => {
    render(<SideDrawer open={true} onClose={vi.fn()} />);
    expect(screen.getByText('Mario')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/layout/SideDrawer.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement SideDrawerItems**

```tsx
// apps/web/src/components/layout/SideDrawer/SideDrawerItems.tsx
'use client';

import { useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
  LayoutDashboard, BookOpen, Target, Dice5, MessageCircle,
  Users2, Moon, Wrench, PenTool, Bell, User, Settings, Shield, LogOut, ChevronRight,
} from 'lucide-react';
import { cn } from '@/lib/utils';

interface NavItem {
  label: string;
  href: string;
  icon: React.ComponentType<{ className?: string }>;
}

const PRIMARY_ITEMS: NavItem[] = [
  { label: 'Dashboard', href: '/dashboard', icon: LayoutDashboard },
  { label: 'Libreria', href: '/library', icon: BookOpen },
  { label: 'Sessioni', href: '/sessions', icon: Target },
  { label: 'Partite & Statistiche', href: '/play-records', icon: Dice5 },
  { label: 'Chat AI', href: '/chat', icon: MessageCircle },
];

const SECONDARY_ITEMS: NavItem[] = [
  { label: 'Giocatori', href: '/players', icon: Users2 },
  { label: 'Serate', href: '/game-nights', icon: Moon },
  { label: 'Toolkit', href: '/toolkit', icon: Wrench },
  { label: 'Editor Agenti', href: '/editor', icon: PenTool },
];

interface SideDrawerItemsProps {
  userRole?: string;
  onNavigate: () => void;
}

export function SideDrawerItems({ userRole, onNavigate }: SideDrawerItemsProps) {
  const pathname = usePathname();
  const [altroOpen, setAltroOpen] = useState(() =>
    SECONDARY_ITEMS.some((item) => pathname?.startsWith(item.href))
  );
  const isAdmin = userRole === 'admin' || userRole === 'superadmin';

  const isActive = (href: string) =>
    pathname === href || (href !== '/dashboard' && pathname?.startsWith(href));

  return (
    <nav className="flex flex-col gap-0.5 py-2">
      {/* Primary */}
      {PRIMARY_ITEMS.map((item) => (
        <Link
          key={item.href}
          href={item.href}
          onClick={onNavigate}
          aria-current={isActive(item.href) ? 'page' : undefined}
          className={cn(
            'flex items-center gap-3 px-4 py-2.5 text-sm font-medium rounded-lg mx-2 transition-colors',
            isActive(item.href)
              ? 'bg-[hsla(25,95%,45%,0.12)] text-[hsl(25,95%,45%)]'
              : 'text-muted-foreground hover:bg-muted hover:text-foreground'
          )}
        >
          <item.icon className="size-5 shrink-0" />
          {item.label}
        </Link>
      ))}

      <div className="mx-4 my-2 border-t border-border" />

      {/* Altro (collapsible) */}
      <button
        onClick={() => setAltroOpen((v) => !v)}
        className="flex items-center gap-3 px-4 py-2.5 text-sm font-medium rounded-lg mx-2 text-muted-foreground hover:bg-muted hover:text-foreground transition-colors"
      >
        <ChevronRight className={cn('size-5 shrink-0 transition-transform', altroOpen && 'rotate-90')} />
        Altro
      </button>
      {altroOpen && (
        <div className="flex flex-col gap-0.5 ml-4">
          {SECONDARY_ITEMS.map((item) => (
            <Link
              key={item.href}
              href={item.href}
              onClick={onNavigate}
              aria-current={isActive(item.href) ? 'page' : undefined}
              className={cn(
                'flex items-center gap-3 px-4 py-2 text-sm rounded-lg mx-2 transition-colors',
                isActive(item.href)
                  ? 'bg-[hsla(25,95%,45%,0.12)] text-[hsl(25,95%,45%)] font-medium'
                  : 'text-muted-foreground hover:bg-muted hover:text-foreground'
              )}
            >
              <item.icon className="size-4 shrink-0" />
              {item.label}
            </Link>
          ))}
        </div>
      )}

      <div className="mx-4 my-2 border-t border-border" />

      {/* Account */}
      <Link href="/notifications" onClick={onNavigate}
        className="flex items-center gap-3 px-4 py-2.5 text-sm rounded-lg mx-2 text-muted-foreground hover:bg-muted hover:text-foreground">
        <Bell className="size-5 shrink-0" />
        Notifiche
      </Link>
      <Link href="/profile" onClick={onNavigate}
        className="flex items-center gap-3 px-4 py-2.5 text-sm rounded-lg mx-2 text-muted-foreground hover:bg-muted hover:text-foreground">
        <User className="size-5 shrink-0" />
        Profilo
      </Link>
      <Link href="/profile?tab=settings" onClick={onNavigate}
        className="flex items-center gap-3 px-4 py-2.5 text-sm rounded-lg mx-2 text-muted-foreground hover:bg-muted hover:text-foreground">
        <Settings className="size-5 shrink-0" />
        Impostazioni
      </Link>
      {isAdmin && (
        <Link href="/admin/overview" onClick={onNavigate}
          className="flex items-center gap-3 px-4 py-2.5 text-sm rounded-lg mx-2 text-muted-foreground hover:bg-muted hover:text-foreground">
          <Shield className="size-5 shrink-0" />
          Admin Hub
        </Link>
      )}

      <div className="mx-4 my-2 border-t border-border" />

      <button
        onClick={onNavigate}
        data-action="logout"
        className="flex items-center gap-3 px-4 py-2.5 text-sm rounded-lg mx-2 text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition-colors"
      >
        <LogOut className="size-5 shrink-0" />
        Logout
      </button>
    </nav>
  );
}
```

- [ ] **Step 4: Implement SideDrawer**

```tsx
// apps/web/src/components/layout/SideDrawer/SideDrawer.tsx
'use client';

import { useEffect } from 'react';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { logoutAction } from '@/actions/auth';
import { SideDrawerItems } from './SideDrawerItems';
import { cn } from '@/lib/utils';

interface SideDrawerProps {
  open: boolean;
  onClose: () => void;
}

export function SideDrawer({ open, onClose }: SideDrawerProps) {
  const { data: user } = useCurrentUser();

  // Close on Escape
  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open, onClose]);

  // Prevent body scroll when open
  useEffect(() => {
    if (open) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }
    return () => { document.body.style.overflow = ''; };
  }, [open]);

  if (!open) return null;

  const handleNavigate = () => onClose();

  const handleLogout = async () => {
    onClose();
    await logoutAction();
  };

  const initials = user?.displayName?.charAt(0) ?? user?.email?.charAt(0) ?? '?';

  return (
    <>
      {/* Overlay */}
      <div
        data-testid="drawer-overlay"
        className="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm"
        onClick={onClose}
      />

      {/* Drawer panel */}
      <aside
        className={cn(
          'fixed left-0 top-0 z-50 h-dvh w-[280px] bg-[var(--bg-base)]',
          'border-r border-border shadow-xl',
          'flex flex-col',
          'animate-in slide-in-from-left duration-200',
        )}
      >
        {/* User header */}
        <div className="flex items-center gap-3 px-4 py-4 border-b border-border">
          <div className="size-10 rounded-full bg-[hsl(25,95%,45%)] flex items-center justify-center text-white font-bold text-sm">
            {initials.toUpperCase()}
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-semibold truncate">{user?.displayName ?? 'Utente'}</p>
            <p className="text-xs text-muted-foreground truncate">{user?.email}</p>
          </div>
        </div>

        {/* Nav items */}
        <div className="flex-1 overflow-y-auto">
          <SideDrawerItems
            userRole={user?.role}
            onNavigate={handleNavigate}
          />
        </div>
      </aside>
    </>
  );
}
```

- [ ] **Step 5: Run tests**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/layout/SideDrawer.test.tsx`
Expected: PASS (7 tests). Adjust mocks if `useAuth` import path differs — check existing usage with `grep -r "useAuth" apps/web/src/components/layout/UserMenuDropdown.tsx` for correct import path.

- [ ] **Step 6: Commit**

```bash
cd apps/web && git add src/components/layout/SideDrawer/ src/__tests__/components/layout/SideDrawer.test.tsx
git commit -m "feat(frontend): add SideDrawer hamburger navigation component"
```

---

### Task 3: MobileCTAPill Component

**Files:**
- Create: `apps/web/src/components/layout/MobileCTAPill.tsx`
- Create: `apps/web/src/__tests__/components/layout/MobileCTAPill.test.tsx`

- [ ] **Step 1: Write tests**

```tsx
// apps/web/src/__tests__/components/layout/MobileCTAPill.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { MobileCTAPill } from '@/components/layout/MobileCTAPill';

vi.mock('next/navigation', () => ({
  usePathname: () => '/library',
}));

describe('MobileCTAPill', () => {
  it('renders CTA for library page', () => {
    render(<MobileCTAPill />);
    expect(screen.getByRole('button', { name: /aggiungi gioco/i })).toBeInTheDocument();
  });

  it('has md:hidden class for desktop hiding', () => {
    const { container } = render(<MobileCTAPill />);
    const pill = container.firstElementChild;
    expect(pill?.className).toContain('md:hidden');
  });
});
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/layout/MobileCTAPill.test.tsx`

- [ ] **Step 3: Implement MobileCTAPill**

```tsx
// apps/web/src/components/layout/MobileCTAPill.tsx
'use client';

import { useState, useEffect } from 'react';
import { usePathname, useRouter } from 'next/navigation';
import { cn } from '@/lib/utils';

const CTA_MAP: Record<string, { label: string; href: string }> = {
  '/library': { label: '+ Aggiungi gioco', href: '/library?action=add' },
  '/sessions': { label: '+ Nuova sessione', href: '/sessions/new' },
  '/play-records': { label: '+ Nuova partita', href: '/play-records/new' },
  '/chat': { label: '+ Nuova chat', href: '/chat/new' },
};

export function MobileCTAPill() {
  const pathname = usePathname();
  const router = useRouter();
  const [visible, setVisible] = useState(true);

  // Hide on scroll down, show on scroll up
  useEffect(() => {
    let lastY = window.scrollY;
    const onScroll = () => {
      const y = window.scrollY;
      setVisible(y < lastY || y < 50);
      lastY = y;
    };
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  const match = Object.entries(CTA_MAP).find(([prefix]) => pathname?.startsWith(prefix));
  if (!match) return null;

  const [, cta] = match;

  return (
    <div
      className={cn(
        'fixed bottom-4 left-1/2 -translate-x-1/2 z-40 md:hidden transition-all duration-200',
        visible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-4 pointer-events-none'
      )}
    >
      <button
        onClick={() => router.push(cta.href)}
        className="px-6 py-2.5 rounded-full bg-[hsl(25,95%,45%)] text-white text-sm font-semibold shadow-lg hover:bg-[hsl(25,95%,38%)] active:scale-95 transition-all"
      >
        {cta.label}
      </button>
    </div>
  );
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/layout/MobileCTAPill.test.tsx`

- [ ] **Step 5: Commit**

```bash
cd apps/web && git add src/components/layout/MobileCTAPill.tsx src/__tests__/components/layout/MobileCTAPill.test.tsx
git commit -m "feat(frontend): add MobileCTAPill for mobile-only CTA"
```

---

### Task 4: SearchOverlay Component

**Files:**
- Create: `apps/web/src/components/layout/SearchOverlay.tsx`
- Create: `apps/web/src/__tests__/components/layout/SearchOverlay.test.tsx`

- [ ] **Step 1: Write tests**

```tsx
// apps/web/src/__tests__/components/layout/SearchOverlay.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { SearchOverlay } from '@/components/layout/SearchOverlay';

describe('SearchOverlay', () => {
  it('renders search input when open', () => {
    render(<SearchOverlay open={true} onClose={vi.fn()} />);
    expect(screen.getByRole('searchbox')).toBeInTheDocument();
  });

  it('does not render when closed', () => {
    render(<SearchOverlay open={false} onClose={vi.fn()} />);
    expect(screen.queryByRole('searchbox')).not.toBeInTheDocument();
  });

  it('focuses input on open', () => {
    render(<SearchOverlay open={true} onClose={vi.fn()} />);
    expect(screen.getByRole('searchbox')).toHaveFocus();
  });

  it('calls onClose on Escape', () => {
    const onClose = vi.fn();
    render(<SearchOverlay open={true} onClose={onClose} />);
    fireEvent.keyDown(screen.getByRole('searchbox'), { key: 'Escape' });
    expect(onClose).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 2: Run tests — expect FAIL**

- [ ] **Step 3: Implement SearchOverlay**

```tsx
// apps/web/src/components/layout/SearchOverlay.tsx
'use client';

import { useEffect, useRef } from 'react';
import { Search, X } from 'lucide-react';

interface SearchOverlayProps {
  open: boolean;
  onClose: () => void;
}

export function SearchOverlay({ open, onClose }: SearchOverlayProps) {
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (open) {
      // Small delay to ensure DOM is ready
      requestAnimationFrame(() => inputRef.current?.focus());
    }
  }, [open]);

  if (!open) return null;

  return (
    <>
      <div className="fixed inset-0 z-50 bg-black/30" onClick={onClose} />
      <div className="fixed top-0 left-0 right-0 z-50 bg-[var(--bg-base)] border-b border-border shadow-lg p-4">
        <div className="max-w-xl mx-auto flex items-center gap-3">
          <Search className="size-5 text-muted-foreground shrink-0" />
          <input
            ref={inputRef}
            type="search"
            role="searchbox"
            placeholder="Cerca giochi, sessioni, giocatori..."
            className="flex-1 bg-transparent text-foreground text-sm outline-none placeholder:text-muted-foreground"
            onKeyDown={(e) => {
              if (e.key === 'Escape') onClose();
            }}
          />
          <button onClick={onClose} className="text-muted-foreground hover:text-foreground">
            <X className="size-5" />
          </button>
        </div>
      </div>
    </>
  );
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/layout/SearchOverlay.test.tsx`

- [ ] **Step 5: Commit**

```bash
cd apps/web && git add src/components/layout/SearchOverlay.tsx src/__tests__/components/layout/SearchOverlay.test.tsx
git commit -m "feat(frontend): add SearchOverlay expandable search component"
```

---

### Task 5: TopBarV2 Component

**Files:**
- Create: `apps/web/src/components/layout/UserShell/TopBarV2.tsx`
- Create: `apps/web/src/__tests__/components/layout/TopBarV2.test.tsx`
- Read: `apps/web/src/components/layout/UserShell/TopBar.tsx` (for styling reference)
- Read: `apps/web/src/components/layout/UserMenuDropdown.tsx` (for import path)

- [ ] **Step 1: Write tests**

```tsx
// apps/web/src/__tests__/components/layout/TopBarV2.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { TopBarV2 } from '@/components/layout/UserShell/TopBarV2';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => <a href={href} {...props}>{children}</a>,
}));
vi.mock('@/components/layout/UserMenuDropdown', () => ({
  UserMenuDropdown: () => <div data-testid="user-menu">Avatar</div>,
}));

describe('TopBarV2', () => {
  it('renders hamburger button', () => {
    render(<TopBarV2 onHamburgerClick={vi.fn()} />);
    expect(screen.getByLabelText(/menu/i)).toBeInTheDocument();
  });

  it('renders logo linking to home', () => {
    render(<TopBarV2 onHamburgerClick={vi.fn()} />);
    const logo = screen.getByRole('link', { name: /meepleai/i });
    expect(logo).toHaveAttribute('href', '/dashboard');
  });

  it('renders search button', () => {
    render(<TopBarV2 onHamburgerClick={vi.fn()} />);
    expect(screen.getByLabelText(/cerca/i)).toBeInTheDocument();
  });

  it('renders user menu', () => {
    render(<TopBarV2 onHamburgerClick={vi.fn()} />);
    expect(screen.getByTestId('user-menu')).toBeInTheDocument();
  });

  it('calls onHamburgerClick when hamburger pressed', () => {
    const onClick = vi.fn();
    render(<TopBarV2 onHamburgerClick={onClick} />);
    fireEvent.click(screen.getByLabelText(/menu/i));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('calls onSearchClick when search pressed', () => {
    const onClick = vi.fn();
    render(<TopBarV2 onHamburgerClick={vi.fn()} onSearchClick={onClick} />);
    fireEvent.click(screen.getByLabelText(/cerca/i));
    expect(onClick).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 2: Run tests — expect FAIL**

- [ ] **Step 3: Implement TopBarV2**

```tsx
// apps/web/src/components/layout/UserShell/TopBarV2.tsx
'use client';

import Link from 'next/link';
import { Menu, Search } from 'lucide-react';
import { UserMenuDropdown } from '@/components/layout/UserMenuDropdown';

interface TopBarV2Props {
  onHamburgerClick: () => void;
  onSearchClick?: () => void;
  /** Optional: show admin badge next to logo */
  adminMode?: boolean;
}

export function TopBarV2({ onHamburgerClick, onSearchClick, adminMode }: TopBarV2Props) {
  return (
    <header
      className="sticky top-0 z-40 flex h-12 items-center justify-between gap-4 border-b px-4 backdrop-blur-[16px]"
      style={{
        background: 'color-mix(in srgb, var(--bg-base) 80%, transparent)',
        borderColor: 'color-mix(in srgb, var(--border) 50%, transparent)',
      }}
    >
      {/* Left: hamburger + logo */}
      <div className="flex items-center gap-3">
        <button
          onClick={onHamburgerClick}
          aria-label="Menu navigazione"
          className="flex items-center justify-center size-9 rounded-lg text-muted-foreground hover:bg-muted hover:text-foreground transition-colors"
        >
          <Menu className="size-5" />
        </button>
        <Link
          href="/dashboard"
          aria-label="MeepleAI"
          className="flex items-center gap-2 text-foreground font-bold font-[family-name:var(--font-quicksand)]"
        >
          <span className="text-lg">🎲</span>
          <span className="text-sm">
            MeepleAI
            {adminMode && (
              <span className="ml-1.5 text-xs bg-destructive/10 text-destructive px-1.5 py-0.5 rounded">
                Admin
              </span>
            )}
          </span>
        </Link>
      </div>

      {/* Right: search + avatar */}
      <div className="flex items-center gap-2">
        <button
          onClick={onSearchClick}
          aria-label="Cerca"
          className="flex items-center justify-center size-9 rounded-lg text-muted-foreground hover:bg-muted hover:text-foreground transition-colors"
        >
          <Search className="size-5" />
        </button>
        <UserMenuDropdown />
      </div>
    </header>
  );
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/layout/TopBarV2.test.tsx`

- [ ] **Step 5: Commit**

```bash
cd apps/web && git add src/components/layout/UserShell/TopBarV2.tsx src/__tests__/components/layout/TopBarV2.test.tsx
git commit -m "feat(frontend): add TopBarV2 minimal topbar with hamburger"
```

---

### Task 6: AdminSideDrawer Component

**Files:**
- Create: `apps/web/src/components/layout/AdminSideDrawer/AdminSideDrawer.tsx`
- Create: `apps/web/src/__tests__/components/layout/AdminSideDrawer.test.tsx`
- Read: `apps/web/src/config/admin-dashboard-navigation.ts` (for DASHBOARD_SECTIONS structure)

- [ ] **Step 1: Write tests**

```tsx
// apps/web/src/__tests__/components/layout/AdminSideDrawer.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { AdminSideDrawer } from '@/components/layout/AdminSideDrawer/AdminSideDrawer';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => <a href={href} {...props}>{children}</a>,
}));
vi.mock('next/navigation', () => ({
  usePathname: () => '/admin/overview',
}));

describe('AdminSideDrawer', () => {
  it('renders "Torna all\'app" link when open', () => {
    render(<AdminSideDrawer open={true} onClose={vi.fn()} />);
    expect(screen.getByText(/torna all/i)).toBeInTheDocument();
  });

  it('renders admin sections', () => {
    render(<AdminSideDrawer open={true} onClose={vi.fn()} />);
    expect(screen.getByText('Overview')).toBeInTheDocument();
    expect(screen.getByText('Content')).toBeInTheDocument();
    expect(screen.getByText('AI')).toBeInTheDocument();
    expect(screen.getByText('Users')).toBeInTheDocument();
  });

  it('does not render when closed', () => {
    render(<AdminSideDrawer open={false} onClose={vi.fn()} />);
    expect(screen.queryByText('Overview')).not.toBeInTheDocument();
  });

  it('calls onClose on overlay click', () => {
    const onClose = vi.fn();
    render(<AdminSideDrawer open={true} onClose={onClose} />);
    fireEvent.click(screen.getByTestId('drawer-overlay'));
    expect(onClose).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 2: Run tests — expect FAIL**

- [ ] **Step 3: Implement AdminSideDrawer**

Build this component following the same pattern as SideDrawer but with admin-specific sections from `DASHBOARD_SECTIONS` config. Group items with collapsible "Altro" sub-menus for sections with >3 items. Include "← Torna all'app" link at the top linking to `/dashboard`.

Read `apps/web/src/config/admin-dashboard-navigation.ts` for the exact section structure, then map each section to a group with its sidebar items. Use the same overlay + slide-in animation pattern as SideDrawer.

- [ ] **Step 4: Run tests — expect PASS**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/layout/AdminSideDrawer.test.tsx`

- [ ] **Step 5: Commit**

```bash
cd apps/web && git add src/components/layout/AdminSideDrawer/ src/__tests__/components/layout/AdminSideDrawer.test.tsx
git commit -m "feat(frontend): add AdminSideDrawer hamburger menu for admin area"
```

---

## Phase 2: Page Migration

Each task removes `useMiniNavConfig` from a page/layout and replaces it with `PageHeader` + in-page tabs. The old MiniNavSlot continues rendering (still in DesktopShell) — it just receives no config, so it renders nothing.

### Task 7: Migrate Library Hub

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/LibraryHub.tsx`

- [ ] **Step 1: Read the current file**

Run: `cat apps/web/src/app/\(authenticated\)/library/LibraryHub.tsx | head -50` to understand the current useMiniNavConfig call and imports.

- [ ] **Step 2: Remove useMiniNavConfig, add PageHeader**

Replace the `useMiniNavConfig(miniNavConfig)` call and `miniNavConfig` useMemo with a `<PageHeader>` in the JSX return:

```tsx
import { PageHeader } from '@/components/layout/PageHeader';

// Remove: import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
// Remove: const miniNavConfig = useMemo(...);
// Remove: useMiniNavConfig(miniNavConfig);

// Add to top of JSX return, before existing content:
<PageHeader
  title="I Miei Giochi"
  subtitle={`${games.length} giochi`}
  tabs={[
    { id: 'hub', label: 'Hub', href: '/library' },
    { id: 'wishlist', label: 'Wishlist', href: '/library/wishlist', count: headerStats.wishlist },
  ]}
  activeTabId="hub"
  primaryAction={{
    label: '+ Aggiungi gioco',
    onClick: () => router.push('/library?action=add'),
  }}
/>
```

- [ ] **Step 3: Verify page renders correctly**

Run: `cd apps/web && pnpm build` (check for TypeScript errors)

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/library/LibraryHub.tsx
git commit -m "refactor(frontend): migrate Library Hub from MiniNav to PageHeader"
```

---

### Task 8: Migrate Library Game Detail

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/[gameId]/layout.tsx`

- [ ] **Step 1: Read the current layout file**

- [ ] **Step 2: Replace useMiniNavConfig with PageHeader**

```tsx
import { PageHeader } from '@/components/layout/PageHeader';

// Remove useMiniNavConfig call. Add PageHeader to JSX:
<PageHeader
  title={game?.title ?? 'Gioco'}
  parentHref="/library"
  parentLabel="Libreria"
  tabs={[
    { id: 'details', label: 'Dettagli', href: `/library/${gameId}` },
    { id: 'agent', label: 'Agente', href: `/library/${gameId}?tab=agent` },
    { id: 'toolkit', label: 'Toolkit', href: `/library/${gameId}?tab=toolkit` },
    { id: 'faq', label: 'FAQ', href: `/library/${gameId}?tab=faq` },
  ]}
  activeTabId={activeTabId}
  primaryAction={{
    label: '💬 Chat con Agente',
    onClick: () => router.push(`/chat/new?gameId=${gameId}`),
  }}
/>
```

- [ ] **Step 3: Build check**

Run: `cd apps/web && pnpm build`

- [ ] **Step 4: Commit**

```bash
git commit -m "refactor(frontend): migrate Library Game Detail from MiniNav to PageHeader"
```

---

### Task 9: Migrate Sessions List

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/layout.tsx`
- Modify: `apps/web/src/app/(authenticated)/sessions/page.tsx` (if it also calls useMiniNavConfig separately)

- [ ] **Step 1: Read both files**

- [ ] **Step 2: Replace useMiniNavConfig in layout with PageHeader**

```tsx
<PageHeader
  title="Sessioni"
  tabs={[
    { id: 'active', label: 'Attive', href: '/sessions' },
    { id: 'history', label: 'Storico', href: '/sessions?tab=history' },
  ]}
  activeTabId={pathname?.includes('tab=history') ? 'history' : 'active'}
  primaryAction={{
    label: '▶ Nuova Sessione',
    onClick: () => router.push('/sessions/new'),
  }}
/>
```

Remove useMiniNavConfig from page.tsx if present (it also calls it — remove the duplicate).

- [ ] **Step 3: Build check**

- [ ] **Step 4: Commit**

```bash
git commit -m "refactor(frontend): migrate Sessions list from MiniNav to PageHeader"
```

---

### Task 10: Migrate Session Detail

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/layout.tsx`
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/notes/page.tsx` (remove back button)

- [ ] **Step 1: Read both files**

- [ ] **Step 2: Replace useMiniNavConfig in session layout**

```tsx
<PageHeader
  title={session?.title ?? 'Sessione'}
  parentHref="/sessions"
  parentLabel="Sessioni"
  tabs={[
    { id: 'scores', label: 'Punteggi', href: `/sessions/${id}` },
    { id: 'tools', label: 'Strumenti', href: `/sessions/${id}/tools` },
    { id: 'chat', label: 'Chat', href: `/sessions/${id}/chat` },
    { id: 'notes', label: 'Note', href: `/sessions/${id}/notes` },
  ]}
  activeTabId={activeTabId}
  primaryAction={{
    label: '＋ Aggiungi Punteggio',
    onClick: () => setScoreSheetOpen(true),
  }}
/>
```

- [ ] **Step 3: Remove back button from notes page**

In `sessions/[id]/notes/page.tsx`, remove the `<Button>← Back to Session</Button>` (around lines 73-89). The PageHeader in the layout already provides "← Sessioni" back link.

- [ ] **Step 4: Build check + commit**

```bash
git commit -m "refactor(frontend): migrate Session Detail from MiniNav to PageHeader, remove duplicate back button"
```

---

### Task 11: Migrate Play Records

**Files:**
- Modify: `apps/web/src/app/(authenticated)/play-records/layout.tsx`

- [ ] **Step 1: Read file, replace useMiniNavConfig**

```tsx
<PageHeader
  title="Partite & Statistiche"
  tabs={[
    { id: 'records', label: 'Partite', href: '/play-records' },
    { id: 'stats', label: 'Statistiche', href: '/play-records?tab=stats' },
  ]}
  activeTabId={pathname?.includes('tab=stats') ? 'stats' : 'records'}
  primaryAction={{
    label: '＋ Nuova Partita',
    onClick: () => router.push('/play-records/new'),
  }}
/>
```

- [ ] **Step 2: Build check + commit**

```bash
git commit -m "refactor(frontend): migrate Play Records from MiniNav to PageHeader"
```

---

### Task 12: Migrate Games Page

**Files:**
- Modify: `apps/web/src/app/(authenticated)/games/page.tsx`

- [ ] **Step 1: Replace useMiniNavConfig**

```tsx
<PageHeader
  title="Giochi"
  tabs={[
    { id: 'library', label: 'Libreria', href: '/games?tab=library' },
    { id: 'catalog', label: 'Catalogo', href: '/games?tab=catalog' },
    { id: 'kb', label: 'Knowledge Base', href: '/games?tab=kb' },
  ]}
  activeTabId={activeTab}
/>
```

- [ ] **Step 2: Build check + commit**

```bash
git commit -m "refactor(frontend): migrate Games page from MiniNav to PageHeader"
```

---

### Task 13: Migrate Simple Pages (Dashboard, Discover, Toolkit)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx`
- Modify: `apps/web/src/app/(authenticated)/discover/page.tsx`
- Modify: `apps/web/src/app/(authenticated)/toolkit/page.tsx`

- [ ] **Step 1: Remove useMiniNavConfig from all three**

These pages used MiniNav with only 1 tab (effectively no tabs). Simply remove the `useMiniNavConfig` call and its import. No PageHeader needed — they are root pages (Pattern C from spec).

- [ ] **Step 2: Build check + commit**

```bash
git commit -m "refactor(frontend): remove useMiniNavConfig from Dashboard, Discover, Toolkit"
```

---

### Task 14: Migrate Live Session

**Files:**
- Modify: `apps/web/src/components/session/live/SessionNavConfig.tsx`

- [ ] **Step 1: Read file**

This is a component that only calls `useMiniNavConfig`. It needs to be converted to render in-page tabs instead. Replace the entire component: instead of calling `useMiniNavConfig`, it should render a `<PageHeader>` or return the tabs config for the parent to render.

- [ ] **Step 2: Convert to PageHeader render**

Replace the `useMiniNavConfig` call. The parent component that renders `<SessionNavConfig>` should now render `<PageHeader>` instead, with:

```tsx
<PageHeader
  title="Partita"
  parentHref="/sessions"
  parentLabel="Sessioni"
  tabs={[
    { id: 'partita', label: 'Partita', href: `/sessions/live/${sessionId}` },
    { id: 'agent', label: 'Chat AI', href: `/sessions/live/${sessionId}/agent` },
    { id: 'scores', label: 'Punteggi', href: `/sessions/live/${sessionId}/scores` },
    { id: 'photos', label: 'Foto', href: `/sessions/live/${sessionId}/photos` },
    { id: 'players', label: 'Giocatori', href: `/sessions/live/${sessionId}/players` },
  ]}
  activeTabId={activeTabId}
/>
```

If `SessionNavConfig` is only used for its side effect (calling useMiniNavConfig), it can be deleted and the PageHeader placed directly in the parent layout.

- [ ] **Step 3: Build check + commit**

```bash
git commit -m "refactor(frontend): migrate Live Session from MiniNav to PageHeader"
```

---

### Task 15: Replace Back Buttons in Game Sub-Pages

**Files:**
- Modify: `apps/web/src/app/(authenticated)/games/[id]/faqs/page.tsx` (~line 87-91)
- Modify: `apps/web/src/app/(authenticated)/games/[id]/reviews/page.tsx` (~line 220-224)
- Modify: `apps/web/src/app/(authenticated)/games/[id]/rules/page.tsx`

- [ ] **Step 1: Read both files**

- [ ] **Step 2: Replace explicit back buttons with PageHeader**

In each file (faqs, reviews, rules), replace the `<Button><ArrowLeft/> Back to Game</Button>` with:

```tsx
<PageHeader
  title="FAQ"  // or "Recensioni" or "Regolamento"
  parentHref={`/library/${gameId}`}
  parentLabel="Gioco"
/>
```

Remove the ArrowLeft import if no longer used.

- [ ] **Step 3: Build check + commit**

```bash
git commit -m "refactor(frontend): replace back buttons in game FAQs/Reviews with PageHeader"
```

---

### Task 16: Replace Back Buttons in Editor Pages

**Files:**
- Modify: `apps/web/src/app/(authenticated)/editor/agent-proposals/create/page.tsx`
- Modify: `apps/web/src/app/(authenticated)/editor/agent-proposals/[id]/edit/page.tsx`
- Modify: `apps/web/src/app/(authenticated)/editor/agent-proposals/[id]/test/page.tsx`

- [ ] **Step 1: Read files, identify back button code**

- [ ] **Step 2: Replace with PageHeader**

```tsx
<PageHeader
  title="Crea Proposta"  // or "Modifica Proposta" / "Test Proposta"
  parentHref="/editor/agent-proposals"
  parentLabel="Proposte"
/>
```

- [ ] **Step 3: Build check + commit**

```bash
git commit -m "refactor(frontend): replace back buttons in Editor pages with PageHeader"
```

---

## Phase 3: Shell Swap

### Task 17: Swap DesktopShell to New Architecture

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/DesktopShell.tsx`

This is the key task — it replaces the old shell structure with the new one.

- [ ] **Step 1: Read the current DesktopShell.tsx (54 lines)**

- [ ] **Step 2: Rewrite DesktopShell**

Replace the entire file. New structure:

```tsx
// apps/web/src/components/layout/UserShell/DesktopShell.tsx
'use client';

import { useState, type ReactNode } from 'react';
import { ChatSlideOverPanel } from '@/components/chat/panel/ChatSlideOverPanel';
import { MobileCTAPill } from '@/components/layout/MobileCTAPill';
import { SearchOverlay } from '@/components/layout/SearchOverlay';
import { SideDrawer } from '@/components/layout/SideDrawer/SideDrawer';
import { SessionBanner } from './SessionBanner';
import { TopBarV2 } from './TopBarV2';

interface DesktopShellProps {
  children: ReactNode;
}

export function DesktopShell({ children }: DesktopShellProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);

  return (
    <div className="min-h-dvh flex flex-col bg-[var(--bg-base)]">
      <TopBarV2
        onHamburgerClick={() => setDrawerOpen(true)}
        onSearchClick={() => setSearchOpen(true)}
      />

      <SessionBanner />

      <main className="flex-1 overflow-y-auto">
        {children}
      </main>

      <MobileCTAPill />
      <ChatSlideOverPanel />
      <SideDrawer open={drawerOpen} onClose={() => setDrawerOpen(false)} />
      <SearchOverlay open={searchOpen} onClose={() => setSearchOpen(false)} />
    </div>
  );
}
```

This removes: HandRail, MiniNavSlot, ActionPill, ActionBar, HandDrawer.

- [ ] **Step 3: Build check**

Run: `cd apps/web && pnpm build`
Fix any import errors if removed components are re-exported from index files.

- [ ] **Step 4: Run existing tests**

Run: `cd apps/web && pnpm vitest run` to check for regressions. Tests that reference MiniNavSlot, HandRail, ActionPill, ActionBar in DesktopShell will fail — these are expected and should be updated or removed in Phase 4.

- [ ] **Step 5: Commit**

```bash
git commit -m "refactor(frontend): swap DesktopShell to TopBarV2 + SideDrawer + MobileCTAPill architecture"
```

---

## Phase 4: Cleanup Dead Code

### Task 18: Delete Removed Components and Stores

**Files to delete** (run each `git rm` command):

- [ ] **Step 1: Delete navigation components**

```bash
cd apps/web
git rm src/components/layout/UserShell/MiniNavSlot.tsx
git rm src/components/layout/UserShell/TopBarNavLinks.tsx
git rm src/components/layout/UserShell/TopBarChatButton.tsx
git rm src/components/layout/UserShell/RecentsBar.tsx
git rm src/components/layout/UserShell/TopBar.tsx
git rm src/components/layout/UserShell/TopBarSearchPill.tsx
git rm -r src/components/layout/ActionPill/
git rm src/components/layout/FloatingActionPill.tsx
git rm -r src/components/layout/HandRail/
git rm src/components/layout/mobile/ActionBar.tsx
git rm src/components/layout/mobile/HandDrawer.tsx
```

- [ ] **Step 2: Delete stores and hooks**

```bash
git rm src/hooks/useMiniNavConfig.ts
git rm src/lib/stores/mini-nav-config-store.ts
git rm src/lib/hooks/useNavBreadcrumb.ts
git rm src/hooks/useDrawCard.ts
git rm src/lib/stores/card-hand-store.ts
```

- [ ] **Step 3: Delete dead admin components**

```bash
git rm src/components/admin/AdminBreadcrumbs.tsx
git rm src/components/admin/BackToHub.tsx
```

- [ ] **Step 4: Delete obsolete tests**

```bash
# Delete tests for removed components (check which exist first)
git rm -f src/__tests__/components/layout/FloatingActionPill.test.tsx 2>/dev/null
git rm -f src/__tests__/components/layout/ActionPill.test.tsx 2>/dev/null
git rm -f src/__tests__/components/layout/HandRail.test.tsx 2>/dev/null
git rm -f src/__tests__/components/layout/mobile/ActionBar.test.tsx 2>/dev/null
git rm -f src/__tests__/stores/card-hand-store.test.ts 2>/dev/null
git rm -f src/__tests__/hooks/useDrawCard.test.ts 2>/dev/null
git rm -f src/__tests__/hooks/useNavBreadcrumb.test.ts 2>/dev/null
git rm -f src/components/layout/UserShell/__tests__/RecentsBar.test.tsx 2>/dev/null
```

- [ ] **Step 5: Fix any broken imports**

Run: `cd apps/web && pnpm build`

Fix any files that still import deleted modules. Common ones:
- `src/config/navigation.ts` — may reference deleted types
- `src/components/layout/UserShell/index.ts` — may re-export deleted components
- Any barrel exports (`index.ts` files in deleted directories)

- [ ] **Step 6: Run full test suite**

Run: `cd apps/web && pnpm vitest run`

Remove or fix any remaining test failures caused by deleted dependencies.

- [ ] **Step 7: Commit**

```bash
git commit -m "chore(frontend): delete 20+ obsolete navigation components, stores, and tests"
```

---

## Phase 5: Admin Migration

### Task 19: Swap AdminShell to New Architecture

**Files:**
- Modify: `apps/web/src/components/layout/AdminShell/AdminShell.tsx`

- [ ] **Step 1: Read current AdminShell.tsx**

- [ ] **Step 2: Rewrite AdminShell**

```tsx
// apps/web/src/components/layout/AdminShell/AdminShell.tsx
'use client';

import { useState, type ReactNode } from 'react';
import { DashboardEngineProvider } from '@/components/dashboard';
import { AdminSideDrawer } from '@/components/layout/AdminSideDrawer/AdminSideDrawer';
import { SearchOverlay } from '@/components/layout/SearchOverlay';
import { TopBarV2 } from '@/components/layout/UserShell/TopBarV2';

interface AdminShellProps {
  children: ReactNode;
}

export function AdminShell({ children }: AdminShellProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);

  return (
    <div className="flex min-h-dvh flex-col bg-[var(--bg-base)]">
      <TopBarV2
        onHamburgerClick={() => setDrawerOpen(true)}
        onSearchClick={() => setSearchOpen(true)}
        adminMode
      />

      <main id="main-content" className="flex-1 overflow-y-auto">
        <DashboardEngineProvider>
          {children}
        </DashboardEngineProvider>
      </main>

      <AdminSideDrawer open={drawerOpen} onClose={() => setDrawerOpen(false)} />
      <SearchOverlay open={searchOpen} onClose={() => setSearchOpen(false)} />
    </div>
  );
}
```

Removes: AdminTabSidebar, AdminBreadcrumb, AdminMobileDrawer, ViewModeToggle from header, NotificationBell from header.

- [ ] **Step 3: Build check**

Run: `cd apps/web && pnpm build`

- [ ] **Step 4: Delete old admin shell components**

```bash
git rm apps/web/src/components/layout/AdminShell/AdminTabSidebar.tsx
git rm apps/web/src/components/layout/AdminShell/AdminBreadcrumb.tsx
git rm apps/web/src/components/layout/AdminShell/AdminMobileDrawer.tsx
```

- [ ] **Step 5: Fix broken imports + test**

Run: `cd apps/web && pnpm build && pnpm vitest run`

- [ ] **Step 6: Commit**

```bash
git commit -m "refactor(frontend): swap AdminShell to TopBarV2 + AdminSideDrawer architecture"
```

---

### Task 20: Add PageHeader to Key Admin Pages

**Files:**
- Modify: Key admin pages that need "← Parent" back links

- [ ] **Step 1: Identify admin pages needing PageHeader**

The admin pages that previously relied on AdminBreadcrumb for location context now need explicit PageHeader with back links. Key pages:

- `/admin/shared-games/[id]` — needs `← Content` back link
- `/admin/knowledge-base/documents` — needs PageHeader with KB tabs
- `/admin/agents/definitions/[id]/edit` — needs `← Agent Definitions` back link
- `/admin/users/[id]` — needs `← Users` back link

- [ ] **Step 2: Add PageHeader to admin sub-pages**

For each admin sub-page, add:

```tsx
import { PageHeader } from '@/components/layout/PageHeader';

// At top of page content:
<PageHeader
  title="Knowledge Base"
  parentHref="/admin/shared-games"
  parentLabel="Content"
  tabs={[
    { id: 'documents', label: 'Documents', href: '/admin/knowledge-base/documents' },
    { id: 'queue', label: 'Queue', href: '/admin/knowledge-base/queue' },
    { id: 'vectors', label: 'Vectors', href: '/admin/knowledge-base/vectors' },
  ]}
  activeTabId={activeTab}
/>
```

Focus on the most-used admin pages first. Others can be migrated incrementally.

- [ ] **Step 3: Build check + test + commit**

```bash
git commit -m "refactor(frontend): add PageHeader to key admin sub-pages"
```

---

### Task 21: Add Notifications to UserMenuDropdown

**Files:**
- Modify: `apps/web/src/components/layout/UserMenuDropdown.tsx` (~line 85, before Profile)

- [ ] **Step 1: Read UserMenuDropdown.tsx**

- [ ] **Step 2: Add Notifications menu item**

Add a `DropdownMenuItem` for Notifications with badge, right after the user info label and before Profile:

```tsx
import { Bell } from 'lucide-react';
import { useNotificationStore, selectUnreadCount } from '@/stores/notification/store';

// Inside the DropdownMenuContent, before Profile item:
const unreadCount = useNotificationStore(selectUnreadCount);

<DropdownMenuItem asChild>
  <Link href="/notifications" className="flex items-center justify-between">
    <span className="flex items-center gap-2">
      <Bell className="size-4" />
      Notifiche
    </span>
    {unreadCount > 0 && (
      <span className="text-xs bg-destructive text-destructive-foreground px-1.5 py-0.5 rounded-full">
        {unreadCount > 9 ? '9+' : unreadCount}
      </span>
    )}
  </Link>
</DropdownMenuItem>
```

- [ ] **Step 3: Build check + commit**

```bash
git commit -m "feat(frontend): add Notifications entry with badge to UserMenuDropdown"
```

---

### Task 22: ManaPips Tooltip Enhancement

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPips.tsx`

- [ ] **Step 1: Read the current ManaPips component**

- [ ] **Step 2: Add tooltip on hover**

Wrap each pip in a `title` attribute or a Radix `<Tooltip>` showing the entity count and type:

```tsx
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';

// For each pip in the map:
<Tooltip>
  <TooltipTrigger asChild>
    <span className={/* existing pip classes */} style={/* existing pip styles */} />
  </TooltipTrigger>
  <TooltipContent side="top" className="text-xs">
    {pip.count ?? 0} {ENTITY_LABELS[pip.entityType]}
  </TooltipContent>
</Tooltip>
```

Where `ENTITY_LABELS` maps entity types to Italian labels: `{ session: 'sessioni', kb: 'documenti', agent: 'agenti', player: 'giocatori', game: 'giochi', chat: 'chat', event: 'eventi' }`.

- [ ] **Step 3: Build check + commit**

```bash
git commit -m "feat(frontend): add tooltip on hover to ManaPips showing entity count"
```

---

### Task 23: Final Verification

- [ ] **Step 1: Full build**

Run: `cd apps/web && pnpm build`
Expected: 0 errors

- [ ] **Step 2: Full test suite**

Run: `cd apps/web && pnpm vitest run`
Expected: All tests pass (some deleted test files will reduce count, but no failures)

- [ ] **Step 3: Type check**

Run: `cd apps/web && pnpm typecheck`
Expected: 0 errors

- [ ] **Step 4: Lint**

Run: `cd apps/web && pnpm lint`
Expected: 0 errors (or only pre-existing ones)

- [ ] **Step 5: Manual smoke test**

Start dev: `cd infra && make dev-core`
Open http://localhost:3000 and verify:
- [ ] TopBar shows: hamburger + logo + 🔍 + avatar
- [ ] Hamburger opens side drawer with all nav items
- [ ] "Altro" sub-menu expands/collapses
- [ ] Active state highlights current page in drawer
- [ ] Library page shows PageHeader with tabs (Collezione/Wishlist)
- [ ] Game detail shows "← Libreria" back link
- [ ] Sessions shows PageHeader with tabs (Attive/Storico)
- [ ] Mobile: CTA pill appears at bottom
- [ ] Search icon opens overlay
- [ ] ManaPips still work (click pip → DeckStack → Drawer)
- [ ] Admin: hamburger opens admin drawer with sections
- [ ] Admin: "← Torna all'app" works

- [ ] **Step 6: Final commit (if any fixes needed)**

```bash
git commit -m "fix(frontend): address navigation redesign smoke test issues"
```
