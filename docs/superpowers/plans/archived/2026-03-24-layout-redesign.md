# Layout Redesign (Gaming Immersive) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current flat layout with a Gaming Immersive layout across all user-facing pages: hybrid sidebar on desktop, MeepleCard list variant on mobile, hero banners, quick actions.

**Architecture:** In-place updates to existing shell components (`UserShellClient`, `UserTopNav`, `UserDesktopSidebar`, `Layout`). New components (`HybridSidebar`, `HeroBanner`, `QuickActionsRow`) replace old ones — no v2 duplicates. Each task is an independent PR to `frontend-dev`.

**Tech Stack:** Next.js 16, React 19, Tailwind 4, shadcn/ui, Zustand, React Query, Lucide icons, cmdk (CommandPalette)

**Spec:** `docs/superpowers/specs/2026-03-24-layout-redesign-design.md`

---

## File Map

### Shell (Tasks 0-1)
| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `apps/web/src/components/layout/UserShell/UserShellClient.tsx` | Restructure from flex-child to fixed-offset sidebar model |
| Modify | `apps/web/src/components/layout/Layout.tsx` | Adjust padding for 52px sidebar offset on desktop |
| Delete | `apps/web/src/components/layout/UserShell/UserDesktopSidebar.tsx` | Replaced by HybridSidebar |
| Create | `apps/web/src/components/layout/UserShell/HybridSidebar.tsx` | Collapsible 52px→220px sidebar |
| Create | `apps/web/src/components/layout/UserShell/__tests__/HybridSidebar.test.tsx` | Tests for sidebar |
| Modify | `apps/web/src/components/layout/UserShell/UserTopNav.tsx` | Add ⌘K search trigger button |

### Dashboard (Task 2)
| Action | File | Responsibility |
|--------|------|----------------|
| Create | `apps/web/src/components/dashboard/HeroBanner.tsx` | Contextual hero with gradient, badge, CTA |
| Create | `apps/web/src/components/dashboard/QuickActionsRow.tsx` | Quick action icon buttons |
| Create | `apps/web/src/components/dashboard/__tests__/HeroBanner.test.tsx` | Tests |
| Create | `apps/web/src/components/dashboard/__tests__/QuickActionsRow.test.tsx` | Tests |
| Create | `apps/web/src/components/dashboard/DashboardScrollRow.tsx` | Horizontal scroll container with scroll-snap |
| Modify | `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx` | Replace HeroZone/QuickStats with new components, add sections |
| Delete (after) | `apps/web/src/components/dashboard/v2/HeroZone.tsx` | Replaced by HeroBanner |
| Delete (after) | `apps/web/src/components/dashboard/v2/QuickStats.tsx` | Replaced by QuickActionsRow |
| Delete (after) | `apps/web/src/components/dashboard/zones/HeroZone.tsx` | Duplicate of v2 |
| Delete (after) | `apps/web/src/components/dashboard/zones/StatsZone.tsx` | Replaced |
| Delete (after) | `apps/web/src/components/dashboard/hero-zone.tsx` | Legacy duplicate |
| Delete (after) | `apps/web/src/components/dashboard/quick-stats.tsx` | Legacy duplicate |
| Delete (after) | `apps/web/src/components/dashboard/v2/ActiveSessions.tsx` | Replaced by dashboard sections |
| Delete (after) | `apps/web/src/components/dashboard/v2/RecentGames.tsx` | Replaced by MeepleCard grid/scroll |
| Delete (after) | `apps/web/src/components/dashboard/v2/YourAgents.tsx` | Moved to sidebar navigation |
| Delete (after) | `apps/web/src/components/dashboard/v2/RecentActivitySidebar.tsx` | Removed (sidebar handles nav) |
| Delete (after) | `apps/web/src/components/dashboard/v2/RecentAgentsSidebar.tsx` | Removed |
| Delete (after) | `apps/web/src/components/dashboard/v2/RecentChatsSidebar.tsx` | Removed |
| Delete (after) | `apps/web/src/components/dashboard/zones/__tests__/StatsZone.test.tsx` | Orphan test |
| Delete (after) | `apps/web/src/components/dashboard/exploration/` | Entire dir — replaced by new dashboard sections |

### Libreria (Task 3)
| Action | File | Responsibility |
|--------|------|----------------|
| Move | `apps/web/src/app/(public)/games/components/ViewToggle.tsx` → `apps/web/src/components/ui/ViewToggle.tsx` | Shared grid/list toggle |
| Modify | `apps/web/src/app/(authenticated)/library/_content.tsx` | Add filter chips, view toggle, mobile list default |
| Create | `apps/web/src/components/ui/FilterChipsRow.tsx` | Reusable scrollable filter chips |
| Create | `apps/web/src/components/ui/__tests__/FilterChipsRow.test.tsx` | Tests |

### Chat (Task 4)
| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `apps/web/src/app/(chat)/chat/layout.tsx` | 2-panel layout on desktop |
| Modify | `apps/web/src/app/(chat)/chat/page.tsx` | Conversation list with MeepleCard list variant |

### Sessioni (Task 5)
| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `apps/web/src/app/(authenticated)/sessions/_content.tsx` | Featured active session, list variant for past |
| Modify | `apps/web/src/app/(authenticated)/sessions/page.tsx` | Add FAB for new session on mobile |

### Scopri (Task 6)
| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `apps/web/src/app/(public)/games/page.tsx` | Search bar, category chips, grid 4col |
| Modify | `apps/web/src/app/(public)/games/catalog/page.tsx` | Same pattern for shared catalog |
| Update | `apps/web/src/app/(public)/games/components/ViewToggle.tsx` | Update imports after move |

---

## Task 0: Shell Restructure

**Branch:** `feature/layout-shell-restructure`
**PR target:** `frontend-dev`
**Files:**
- Modify: `apps/web/src/components/layout/UserShell/UserShellClient.tsx`
- Modify: `apps/web/src/components/layout/Layout.tsx`

This task changes the layout model from flex-child sidebar to fixed-offset, so the new HybridSidebar (Task 1) can overlay without content reflow.

- [ ] **Step 1: Write test for desktop layout offset**

```typescript
// apps/web/src/components/layout/UserShell/__tests__/UserShellClient.test.tsx
import { render, screen } from '@testing-library/react';
import { UserShellClient } from '../UserShellClient';

// Mock all child components
vi.mock('../UserDesktopSidebar', () => ({
  UserDesktopSidebar: () => <aside data-testid="sidebar" />,
}));
vi.mock('../UserTopNav', () => ({
  UserTopNav: () => <header data-testid="topnav" />,
}));
vi.mock('../UserTabBar', () => ({
  UserTabBar: () => <nav data-testid="tabbar" />,
}));
vi.mock('../../ContextBar', () => ({
  ContextBar: () => <div data-testid="context-bar" />,
}));
vi.mock('@/components/dashboard', () => ({
  DashboardEngineProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));
vi.mock('@/components/session/BackToSessionFAB', () => ({
  BackToSessionFAB: () => null,
}));

describe('UserShellClient', () => {
  it('renders main content area with sidebar offset on desktop', () => {
    render(<UserShellClient>content</UserShellClient>);
    const main = screen.getByRole('main');
    expect(main).toBeInTheDocument();
  });

  it('preserves ContextBar, DashboardEngineProvider, BackToSessionFAB', () => {
    render(<UserShellClient>content</UserShellClient>);
    expect(screen.getByTestId('context-bar')).toBeInTheDocument();
    expect(screen.getByTestId('topnav')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it passes with current code**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/__tests__/UserShellClient.test.tsx`

- [ ] **Step 3: Modify UserShellClient — fixed-offset model**

Replace the current flex layout in `apps/web/src/components/layout/UserShell/UserShellClient.tsx`:

```typescript
'use client';

import { Suspense, type ReactNode } from 'react';

import { DashboardEngineProvider } from '@/components/dashboard';
import { BackToSessionFAB } from '@/components/session/BackToSessionFAB';

import { ContextBar } from '../ContextBar';
import { UserDesktopSidebar } from './UserDesktopSidebar';
import { UserTabBar } from './UserTabBar';
import { UserTopNav } from './UserTopNav';

interface UserShellClientProps {
  children: ReactNode;
}

export function UserShellClient({ children }: UserShellClientProps) {
  return (
    <div className="h-dvh bg-background">
      <UserTopNav />
      <ContextBar />
      <Suspense>
        <UserDesktopSidebar />
      </Suspense>
      <main className="lg:ml-[52px] flex-1 overflow-y-auto pb-16 lg:pb-0 h-[calc(100dvh-48px)]">
        <DashboardEngineProvider>{children}</DashboardEngineProvider>
      </main>
      <Suspense>
        <UserTabBar />
      </Suspense>
      <BackToSessionFAB />
    </div>
  );
}
```

Key changes:
- Root is no longer `flex h-dvh` — it's just `h-dvh bg-background`
- TopNav and ContextBar are outside the flex row
- Sidebar is positioned independently (still renders as flex, but offset via `lg:ml-[52px]` on main)
- `main` uses `lg:ml-[52px]` for permanent 52px offset on desktop
- Main height: `h-[calc(100dvh-48px)]` accounts for TopNav

- [ ] **Step 4: Update Layout.tsx padding — remove pt-16 (header is outside)**

In `apps/web/src/components/layout/Layout.tsx`, the `pt-16` class was for a taller header. Since UserTopNav is `h-12` (48px) and now it's outside the scrollable area, we remove `pt-16`:

```typescript
// In the <main> className, remove 'pt-16' since the header is now outside the scrollable area
// The content starts directly after the header in the new shell model
```

Find `'pt-16', // Header height` and remove it. The Layout component is now nested inside UserShellClient's `<main>` which already has the correct offset.

- [ ] **Step 5: Run existing tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/`
Expected: All pass. If any Layout tests reference `pt-16`, update them.

- [ ] **Step 6: Visual smoke test**

Run: `cd apps/web && pnpm dev`
Open http://localhost:3000/dashboard — verify:
- TopNav at top
- Sidebar still visible on desktop (left side)
- Content offset by 52px on desktop
- Mobile: no sidebar, TabBar at bottom
- ContextBar visible below TopNav
- BackToSessionFAB visible

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/layout/UserShell/UserShellClient.tsx apps/web/src/components/layout/Layout.tsx
git commit -m "refactor(layout): restructure shell to fixed-offset sidebar model

Prepares for hybrid collapsible sidebar. Content area uses ml-[52px]
on desktop instead of flex-child model. No visual changes yet."
```

---

## Task 1: HybridSidebar + TopNav ⌘K Trigger

**Branch:** `feature/layout-hybrid-sidebar`
**PR target:** `frontend-dev`
**Files:**
- Create: `apps/web/src/components/layout/UserShell/HybridSidebar.tsx`
- Create: `apps/web/src/components/layout/UserShell/__tests__/HybridSidebar.test.tsx`
- Delete: `apps/web/src/components/layout/UserShell/UserDesktopSidebar.tsx`
- Modify: `apps/web/src/components/layout/UserShell/UserShellClient.tsx`
- Modify: `apps/web/src/components/layout/UserShell/UserTopNav.tsx`

- [ ] **Step 1: Write HybridSidebar tests**

```typescript
// apps/web/src/components/layout/UserShell/__tests__/HybridSidebar.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
  useSearchParams: () => new URLSearchParams(),
}));

import { HybridSidebar } from '../HybridSidebar';

describe('HybridSidebar', () => {
  it('renders as navigation landmark', () => {
    render(<HybridSidebar />);
    expect(screen.getByRole('navigation', { name: /navigazione principale/i })).toBeInTheDocument();
  });

  it('renders icon buttons for all nav items', () => {
    render(<HybridSidebar />);
    expect(screen.getByLabelText(/dashboard/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/libreria/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/sessioni/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/chat/i)).toBeInTheDocument();
  });

  it('marks active item based on pathname', () => {
    render(<HybridSidebar />);
    const dashboardLink = screen.getByLabelText(/dashboard/i);
    expect(dashboardLink).toHaveAttribute('aria-current', 'page');
  });

  it('is hidden on mobile (lg:flex)', () => {
    render(<HybridSidebar />);
    const nav = screen.getByRole('navigation', { name: /navigazione principale/i });
    expect(nav.className).toContain('hidden');
    expect(nav.className).toContain('lg:flex');
  });

  it('shows expanded labels on focus-within', async () => {
    render(<HybridSidebar />);
    const nav = screen.getByRole('navigation', { name: /navigazione principale/i });
    // The sidebar uses group/focus-within for expansion
    expect(nav.className).toContain('group');
  });
});
```

- [ ] **Step 2: Run tests — should fail (HybridSidebar doesn't exist)**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/__tests__/HybridSidebar.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement HybridSidebar**

```typescript
// apps/web/src/components/layout/UserShell/HybridSidebar.tsx
'use client';

import {
  BookOpen,
  Bot,
  Dice5,
  FileText,
  Heart,
  House,
  MessageCircle,
  Settings,
  Star,
  Users,
  Target,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname, useSearchParams } from 'next/navigation';

import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

// ─── Configuration ───

interface SidebarItem {
  id: string;
  label: string;
  href: string;
  icon: LucideIcon;
  colorVar: string;
  isActive: (pathname: string, searchParams: URLSearchParams) => boolean;
  /** Live counter displayed as badge (collapsed) and inline number (expanded) */
  count?: number;
}

interface SidebarSection {
  label: string;
  items: SidebarItem[];
}

const SECTIONS: SidebarSection[] = [
  {
    label: 'Navigazione',
    items: [
      {
        id: 'dashboard',
        label: 'Dashboard',
        href: '/dashboard',
        icon: House,
        colorVar: 'hsl(var(--primary))',
        isActive: p => p === '/dashboard',
      },
      {
        id: 'library',
        label: 'Libreria',
        href: '/library?tab=collection',
        icon: BookOpen,
        colorVar: 'hsl(var(--color-entity-game, 25 95% 45%))',
        isActive: (p, sp) => p === '/library' || (p === '/library' && sp.has('tab')),
      },
      {
        id: 'wishlist',
        label: 'Wishlist',
        href: '/library?tab=wishlist',
        icon: Heart,
        colorVar: 'hsl(var(--color-entity-event, 350 89% 60%))',
        isActive: (p, sp) => p === '/library' && sp.get('tab') === 'wishlist',
      },
      {
        id: 'sessions',
        label: 'Sessioni',
        href: '/sessions',
        icon: Dice5,
        colorVar: 'hsl(var(--color-entity-session, 240 60% 55%))',
        isActive: p => p.startsWith('/sessions'),
      },
    ],
  },
  {
    label: 'AI Assistant',
    items: [
      {
        id: 'chat',
        label: 'Chat RAG',
        href: '/chat',
        icon: MessageCircle,
        colorVar: 'hsl(var(--color-entity-chat, 220 80% 55%))',
        isActive: p => p.startsWith('/chat'),
      },
      {
        id: 'documents',
        label: 'Documenti',
        href: '/library?tab=private',
        icon: FileText,
        colorVar: 'hsl(174, 60%, 40%)',
        isActive: (p, sp) => p === '/library' && sp.get('tab') === 'private',
      },
      {
        id: 'agents',
        label: 'Agenti',
        href: '/agents',
        icon: Bot,
        colorVar: 'hsl(38, 92%, 50%)',
        isActive: p => p.startsWith('/agents'),
      },
    ],
  },
  {
    label: 'Collezioni',
    items: [
      {
        id: 'favorites',
        label: 'Preferiti',
        href: '/library?tab=collection&filter=favorites',
        icon: Star,
        colorVar: 'hsl(45, 90%, 48%)',
        isActive: () => false, // TODO: wire when filter exists
      },
      {
        id: 'with-friends',
        label: 'Con amici',
        href: '/library?tab=collection&filter=multiplayer',
        icon: Users,
        colorVar: 'hsl(262, 83%, 58%)',
        isActive: () => false,
      },
      {
        id: 'strategic',
        label: 'Strategici',
        href: '/library?tab=collection&filter=strategic',
        icon: Target,
        colorVar: 'hsl(220, 80%, 55%)',
        isActive: () => false,
      },
    ],
  },
];

// Flatten for icon-only view
const ALL_NAV_ITEMS = SECTIONS.flatMap(s => s.items);
const MAIN_NAV_ITEMS = SECTIONS[0].items; // Only main nav in collapsed mode
const BOTTOM_ITEMS: SidebarItem[] = [
  {
    id: 'settings',
    label: 'Impostazioni',
    href: '/settings',
    icon: Settings,
    colorVar: 'hsl(var(--muted-foreground))',
    isActive: p => p.startsWith('/settings'),
  },
];

// ─── Component ───

interface HybridSidebarProps {
  className?: string;
}

export function HybridSidebar({ className }: HybridSidebarProps) {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  // TODO: Wire real counts from React Query hooks when implementing
  // e.g. const { data: libCount } = useLibraryCount();

  return (
    <nav
      role="navigation"
      aria-label="Navigazione principale"
      aria-expanded={false} // Managed by CSS :hover/:focus-within — JS state not needed
      className={cn(
        // Base: hidden on mobile, flex column on desktop
        'hidden lg:flex flex-col',
        // Positioning: fixed to left, below TopNav
        'fixed left-0 top-12 z-30',
        'h-[calc(100dvh-48px)]',
        // Sizing: collapsed 52px, expands on hover/focus
        'w-[52px] hover:w-[220px] focus-within:w-[220px]',
        'transition-[width] duration-300 ease-[cubic-bezier(0.4,0,0.2,1)]',
        'motion-reduce:transition-none',
        // Styling
        'bg-background border-r border-border/40',
        'overflow-hidden overflow-y-auto',
        'group',
        className
      )}
      data-testid="hybrid-sidebar"
    >
      {/* Icon-only view (collapsed) + labels (expanded via group-hover) */}
      <div className="flex-1 flex flex-col py-3 gap-0.5">
        {SECTIONS.map((section, sIdx) => (
          <div key={section.label}>
            {/* Section label — only visible when expanded */}
            {sIdx > 0 && <div className="h-px bg-border/40 mx-3 my-2" />}
            <span className="px-4 text-[9px] font-bold uppercase tracking-wider text-muted-foreground opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity duration-200 whitespace-nowrap">
              {section.label}
            </span>

            {section.items.map(item => {
              const isActive = item.isActive(pathname, searchParams);
              const Icon = item.icon;

              return (
                <Link
                  key={item.id}
                  href={item.href}
                  className={cn(
                    'relative flex items-center gap-3',
                    'mx-2 rounded-[10px]',
                    'transition-colors duration-200',
                    'hover:bg-muted/50',
                    isActive ? 'bg-primary text-white shadow-[0_2px_8px_rgba(180,80,0,0.3)]' : 'text-muted-foreground hover:text-foreground'
                  )}
                  aria-label={item.label}
                  aria-current={isActive ? 'page' : undefined}
                >
                  {/* Icon container: 34x34px per spec */}
                  <div className="w-[34px] h-[34px] flex items-center justify-center shrink-0 relative">
                    <Icon className="w-5 h-5" aria-hidden="true" />
                    {/* Badge counter (collapsed mode) */}
                    {item.count !== undefined && item.count > 0 && (
                      <span className="absolute -top-1 -right-1 w-4 h-4 rounded-full bg-[hsl(var(--color-entity-event,350_89%_60%))] text-white text-[8px] font-bold flex items-center justify-center group-hover:hidden group-focus-within:hidden">
                        {item.count > 9 ? '9+' : item.count}
                      </span>
                    )}
                  </div>
                  <span className="text-xs font-medium whitespace-nowrap opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity duration-200 flex items-center gap-2">
                    {item.label}
                    {/* Counter in expanded mode */}
                    {item.count !== undefined && item.count > 0 && (
                      <span className="text-[10px] bg-muted px-1.5 py-0.5 rounded-md text-muted-foreground">
                        {item.count}
                      </span>
                    )}
                  </span>
                  <span className="sr-only">{item.label}{item.count ? `, ${item.count} elementi` : ''}</span>
                </Link>
              );
            })}
          </div>
        ))}
      </div>

      {/* Bottom: settings */}
      <div className="flex flex-col gap-0.5 pb-3">
        {BOTTOM_ITEMS.map(item => {
          const isActive = item.isActive(pathname, searchParams);
          const Icon = item.icon;
          return (
            <Link
              key={item.id}
              href={item.href}
              className={cn(
                'flex items-center gap-3',
                'mx-2 px-2 py-2 rounded-[10px]',
                'transition-colors duration-200',
                'hover:bg-muted/50',
                isActive ? 'text-foreground' : 'text-muted-foreground hover:text-foreground'
              )}
              aria-label={item.label}
            >
              <Icon className="w-[18px] h-[18px] shrink-0" aria-hidden="true" />
              <span className="text-xs font-medium whitespace-nowrap opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity duration-200">
                {item.label}
              </span>
            </Link>
          );
        })}
      </div>
    </nav>
  );
}
```

- [ ] **Step 4: Run HybridSidebar tests — should pass**

Run: `cd apps/web && pnpm vitest run src/components/layout/UserShell/__tests__/HybridSidebar.test.tsx`
Expected: PASS

- [ ] **Step 5: Wire HybridSidebar into UserShellClient**

In `apps/web/src/components/layout/UserShell/UserShellClient.tsx`, replace the import:

```typescript
// OLD:
import { UserDesktopSidebar } from './UserDesktopSidebar';
// NEW:
import { HybridSidebar } from './HybridSidebar';
```

And in the JSX, replace:
```typescript
// OLD:
<Suspense>
  <UserDesktopSidebar />
</Suspense>
// NEW:
<Suspense>
  <HybridSidebar />
</Suspense>
```

- [ ] **Step 6: Delete UserDesktopSidebar**

```bash
rm apps/web/src/components/layout/UserShell/UserDesktopSidebar.tsx
```

Remove any tests that reference `UserDesktopSidebar` directly.

- [ ] **Step 7: Add ⌘K trigger to UserTopNav**

In `apps/web/src/components/layout/UserShell/UserTopNav.tsx`, add a search trigger button in the center area (where breadcrumb/sectionTitle is). Add it next to the right-side controls:

After the `<NotificationBell />` and before `<UserMenuDropdown />`, add:

```tsx
<button
  type="button"
  className="hidden lg:flex items-center gap-2 px-3 py-1.5 rounded-lg bg-muted border border-border text-muted-foreground text-xs hover:bg-muted/80 transition-colors"
  onClick={() => {
    // Dispatch Cmd+K event to open CommandPalette
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', metaKey: true }));
  }}
  aria-label="Cerca (⌘K)"
>
  <Search className="w-3.5 h-3.5" />
  <span>Cerca...</span>
  <kbd className="text-[10px] bg-background border border-border px-1.5 py-0.5 rounded ml-2">⌘K</kbd>
</button>
```

Import `Search` from `lucide-react`.

- [ ] **Step 8: Run all shell tests**

Run: `cd apps/web && pnpm vitest run src/components/layout/`
Expected: All pass

- [ ] **Step 9: Visual smoke test**

Run: `cd apps/web && pnpm dev`
Verify:
- Desktop: 52px icon sidebar on left, expands on hover to 220px with labels and sections
- Active page highlighted with primary color
- ⌘K trigger button visible in TopNav on desktop
- Mobile: no sidebar, TabBar at bottom (unchanged)
- ContextBar, BackToSessionFAB still work

- [ ] **Step 10: Commit**

```bash
git add apps/web/src/components/layout/UserShell/HybridSidebar.tsx apps/web/src/components/layout/UserShell/__tests__/HybridSidebar.test.tsx apps/web/src/components/layout/UserShell/UserShellClient.tsx apps/web/src/components/layout/UserShell/UserTopNav.tsx
git rm apps/web/src/components/layout/UserShell/UserDesktopSidebar.tsx
git commit -m "feat(layout): add HybridSidebar replacing UserDesktopSidebar

Collapsible 52px→220px sidebar with icon-only collapsed state and
full nav on hover/focus. Sections: Navigazione, AI Assistant, Collezioni.
Adds ⌘K search trigger to UserTopNav. Deletes old UserDesktopSidebar."
```

---

## Task 2: Home/Dashboard Redesign

**Branch:** `feature/layout-dashboard`
**PR target:** `frontend-dev`
**Files:**
- Create: `apps/web/src/components/dashboard/HeroBanner.tsx`
- Create: `apps/web/src/components/dashboard/__tests__/HeroBanner.test.tsx`
- Create: `apps/web/src/components/dashboard/QuickActionsRow.tsx`
- Create: `apps/web/src/components/dashboard/__tests__/QuickActionsRow.test.tsx`
- Create: `apps/web/src/components/dashboard/DashboardScrollRow.tsx`
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`
- Delete: old HeroZone/QuickStats duplicates

- [ ] **Step 1: Write HeroBanner tests**

```typescript
// apps/web/src/components/dashboard/__tests__/HeroBanner.test.tsx
import { render, screen } from '@testing-library/react';
import { HeroBanner } from '../HeroBanner';

describe('HeroBanner', () => {
  it('renders title and subtitle', () => {
    render(<HeroBanner title="Stasera Catan!" subtitle="4 giocatori" />);
    expect(screen.getByText('Stasera Catan!')).toBeInTheDocument();
    expect(screen.getByText('4 giocatori')).toBeInTheDocument();
  });

  it('renders badge when provided', () => {
    render(<HeroBanner title="Test" subtitle="Sub" badge={{ text: 'LIVE', variant: 'live' }} />);
    expect(screen.getByText('LIVE')).toBeInTheDocument();
  });

  it('renders CTA link when provided', () => {
    render(<HeroBanner title="Test" subtitle="Sub" cta={{ label: 'Entra', href: '/session/1' }} />);
    expect(screen.getByRole('link', { name: /entra/i })).toHaveAttribute('href', '/session/1');
  });

  it('renders without CTA or badge (minimal)', () => {
    render(<HeroBanner title="Benvenuto" subtitle="Esplora la tua libreria" />);
    expect(screen.getByText('Benvenuto')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — FAIL**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/HeroBanner.test.tsx`

- [ ] **Step 3: Implement HeroBanner**

```typescript
// apps/web/src/components/dashboard/HeroBanner.tsx
'use client';

import Link from 'next/link';

import { cn } from '@/lib/utils';

const GRADIENTS = {
  primary: 'from-primary/15 via-accent/8 to-[hsl(220,70%,55%)]/8',
  session: 'from-[hsl(240,60%,55%)]/15 via-primary/8 to-accent/8',
  chat: 'from-[hsl(220,80%,55%)]/15 via-accent/8 to-primary/8',
} as const;

const BADGE_STYLES = {
  live: 'bg-primary text-white',
  featured: 'bg-primary text-white',
  new: 'bg-secondary text-white',
} as const;

interface HeroBannerProps {
  title: string;
  subtitle: string;
  badge?: { text: string; variant: 'live' | 'featured' | 'new' };
  cta?: { label: string; href: string; icon?: React.ReactNode };
  gradient?: 'primary' | 'session' | 'chat';
  className?: string;
}

export function HeroBanner({
  title,
  subtitle,
  badge,
  cta,
  gradient = 'primary',
  className,
}: HeroBannerProps) {
  return (
    <section
      className={cn(
        'relative rounded-[14px] overflow-hidden',
        'p-4 sm:p-5',
        'bg-gradient-to-r',
        GRADIENTS[gradient],
        'border border-primary/12',
        className
      )}
    >
      {badge && (
        <span
          className={cn(
            'absolute top-3 right-3',
            'px-2.5 py-0.5 rounded-md',
            'text-[10px] font-bold tracking-wide',
            BADGE_STYLES[badge.variant]
          )}
        >
          {badge.text}
        </span>
      )}

      <h2 className="font-quicksand font-bold text-base sm:text-lg text-foreground pr-20">
        {title}
      </h2>
      <p className="text-xs sm:text-sm text-muted-foreground mt-1">{subtitle}</p>

      {cta && (
        <Link
          href={cta.href}
          className="inline-flex items-center gap-1.5 mt-3 px-3 py-1.5 rounded-lg bg-primary text-white text-xs font-bold font-quicksand hover:bg-primary/90 transition-colors"
        >
          {cta.icon}
          {cta.label}
        </Link>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Run HeroBanner tests — PASS**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/HeroBanner.test.tsx`

- [ ] **Step 5: Write QuickActionsRow tests**

```typescript
// apps/web/src/components/dashboard/__tests__/QuickActionsRow.test.tsx
import { render, screen } from '@testing-library/react';
import { BookOpen } from 'lucide-react';
import { QuickActionsRow } from '../QuickActionsRow';

describe('QuickActionsRow', () => {
  const actions = [
    { icon: <BookOpen data-testid="icon" />, label: 'Libreria', href: '/library' },
    { icon: <BookOpen />, label: 'Chat AI', href: '/chat' },
  ];

  it('renders all action links', () => {
    render(<QuickActionsRow actions={actions} />);
    expect(screen.getByRole('link', { name: /libreria/i })).toHaveAttribute('href', '/library');
    expect(screen.getByRole('link', { name: /chat ai/i })).toHaveAttribute('href', '/chat');
  });

  it('renders icon and label for each action', () => {
    render(<QuickActionsRow actions={actions} />);
    expect(screen.getByText('Libreria')).toBeInTheDocument();
    expect(screen.getByText('Chat AI')).toBeInTheDocument();
  });
});
```

- [ ] **Step 6: Implement QuickActionsRow**

```typescript
// apps/web/src/components/dashboard/QuickActionsRow.tsx
'use client';

import Link from 'next/link';

import { cn } from '@/lib/utils';

interface QuickAction {
  icon: React.ReactNode;
  label: string;
  href: string;
  entityColor?: string;
}

interface QuickActionsRowProps {
  actions: QuickAction[];
  className?: string;
}

export function QuickActionsRow({ actions, className }: QuickActionsRowProps) {
  return (
    <div
      className={cn(
        'flex gap-2 overflow-x-auto scrollbar-none pb-1',
        '-mx-1 px-1', // Allow shadow to show
        className
      )}
    >
      {actions.map(action => (
        <Link
          key={action.href}
          href={action.href}
          className="flex flex-col items-center gap-1 min-w-[56px] shrink-0"
          aria-label={action.label}
        >
          <div
            className="w-11 h-11 rounded-xl flex items-center justify-center bg-card border border-border [box-shadow:var(--shadow-warm-sm)] text-muted-foreground hover:text-foreground transition-colors"
            style={action.entityColor ? { borderColor: `hsl(${action.entityColor} / 0.3)` } : undefined}
          >
            {action.icon}
          </div>
          <span className="text-[9px] font-semibold text-muted-foreground text-center leading-tight">
            {action.label}
          </span>
        </Link>
      ))}
    </div>
  );
}
```

- [ ] **Step 7: Run QuickActionsRow tests — PASS**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/QuickActionsRow.test.tsx`

- [ ] **Step 8a: Write DashboardScrollRow test**

```typescript
// apps/web/src/components/dashboard/__tests__/DashboardScrollRow.test.tsx
import { render, screen } from '@testing-library/react';
import { DashboardScrollRow } from '../DashboardScrollRow';

describe('DashboardScrollRow', () => {
  it('renders children in a scrollable container', () => {
    render(
      <DashboardScrollRow>
        <div data-testid="child-1">A</div>
        <div data-testid="child-2">B</div>
      </DashboardScrollRow>
    );
    expect(screen.getByTestId('child-1')).toBeInTheDocument();
    expect(screen.getByTestId('child-2')).toBeInTheDocument();
  });

  it('applies scroll-snap classes', () => {
    const { container } = render(<DashboardScrollRow>content</DashboardScrollRow>);
    const row = container.firstChild as HTMLElement;
    expect(row.className).toContain('snap-x');
    expect(row.className).toContain('overflow-x-auto');
  });
});
```

- [ ] **Step 8b: Implement DashboardScrollRow**

```typescript
// apps/web/src/components/dashboard/DashboardScrollRow.tsx
'use client';

import { cn } from '@/lib/utils';

interface DashboardScrollRowProps {
  children: React.ReactNode;
  className?: string;
}

export function DashboardScrollRow({ children, className }: DashboardScrollRowProps) {
  return (
    <div
      className={cn(
        'flex gap-3 overflow-x-auto scrollbar-none',
        'snap-x snap-mandatory',
        '-mx-1 px-1 pb-1',
        className
      )}
    >
      {children}
    </div>
  );
}
```

- [ ] **Step 8c: Run DashboardScrollRow test — PASS**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/__tests__/DashboardScrollRow.test.tsx`

- [ ] **Step 9: Rewrite dashboard-client.tsx**

Replace the entire content of `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`:

```typescript
'use client';

import { useEffect, useMemo } from 'react';

import { BookOpen, Dice5, FileText, MessageCircle, Search } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { HeroBanner } from '@/components/dashboard/HeroBanner';
import { QuickActionsRow } from '@/components/dashboard/QuickActionsRow';
import { DashboardScrollRow } from '@/components/dashboard/DashboardScrollRow';
import { MeepleCard } from '@/components/ui/data-display/meeple-card/MeepleCard';
import { MeepleCardSkeleton } from '@/components/ui/data-display/meeple-card-parts';
import { EmptyState } from '@/components/layout/Layout';
import { useRecentChatSessions } from '@/hooks/queries/useChatSessions';
import { useRecentAgents } from '@/hooks/queries/useRecentAgents';
import type { UserGameDto } from '@/lib/api/dashboard-client';
import { useDashboardStore } from '@/lib/stores/dashboard-store';
import { useLayout } from '@/components/layout/LayoutProvider';

export function DashboardClient() {
  const router = useRouter();
  const { user } = useAuth();
  const { responsive } = useLayout();
  const isMobile = responsive.isMobile;

  const {
    stats, isLoadingStats, fetchStats,
    recentSessions, isLoadingSessions, fetchRecentSessions,
    updateFilters, games, isLoadingGames,
  } = useDashboardStore();

  const { data: agentsRaw } = useRecentAgents(5);
  const { data: chatSessionsRaw } = useRecentChatSessions(4);

  useEffect(() => {
    fetchStats();
    fetchRecentSessions(5);
    updateFilters({ sort: 'playCount', pageSize: 8, page: 1 });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const userName = user?.displayName ?? user?.email?.split('@')[0] ?? 'Giocatore';
  const nextSession = recentSessions[0]; // Most recent active session for hero

  const quickActions = [
    { icon: <BookOpen className="w-5 h-5" />, label: 'Libreria', href: '/library', entityColor: '25 95% 45%' },
    { icon: <Dice5 className="w-5 h-5" />, label: 'Nuova partita', href: '/sessions/new', entityColor: '240 60% 55%' },
    { icon: <MessageCircle className="w-5 h-5" />, label: 'Chat AI', href: '/chat', entityColor: '220 80% 55%' },
    { icon: <FileText className="w-5 h-5" />, label: 'Regole', href: '/library?tab=private', entityColor: '174 60% 40%' },
    { icon: <Search className="w-5 h-5" />, label: 'Scopri', href: '/games' },
  ];

  return (
    <div className="flex flex-col gap-5 w-full max-w-7xl mx-auto px-3 sm:px-4 py-4 sm:py-5">
      {/* Hero Banner */}
      <HeroBanner
        title={nextSession ? `Prossima: ${nextSession.gameName}` : `Ciao ${userName}!`}
        subtitle={nextSession ? `${nextSession.playerCount} giocatori` : 'Esplora la tua libreria di giochi'}
        badge={nextSession ? { text: '🔥 LIVE', variant: 'live' } : undefined}
        cta={nextSession
          ? { label: '🎲 Entra nella sessione', href: `/sessions/${nextSession.id}` }
          : { label: 'Aggiungi un gioco', href: '/library' }
        }
      />

      {/* Quick Actions */}
      <QuickActionsRow actions={quickActions} />

      {/* Recent Games — horizontal scroll */}
      <section>
        <div className="flex items-center justify-between mb-2">
          <h2 className="font-quicksand font-bold text-sm text-foreground flex items-center gap-1.5">
            <span className="text-primary">🕐</span> Giocati di recente
          </h2>
          <Link href="/library" className="text-xs text-primary font-semibold">Tutti →</Link>
        </div>
        {isLoadingGames ? (
          <DashboardScrollRow>
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="w-[140px] sm:w-[160px] shrink-0 snap-start">
                <MeepleCardSkeleton variant="grid" />
              </div>
            ))}
          </DashboardScrollRow>
        ) : games.length === 0 ? (
          <EmptyState title="Nessun gioco ancora" description="Aggiungi il tuo primo gioco alla libreria" action={<Link href="/library" className="text-sm text-primary font-semibold">Aggiungi gioco →</Link>} />
        ) : (
          <DashboardScrollRow>
            {games.slice(0, 8).map((game: UserGameDto) => (
              <div key={game.id} className="w-[140px] sm:w-[160px] shrink-0 snap-start">
                <MeepleCard
                  entity="game"
                  variant="grid"
                  title={game.title}
                  subtitle={game.publisher}
                  imageUrl={game.imageUrl ?? game.thumbnailUrl}
                  rating={game.averageRating}
                  ratingMax={10}
                  metadata={[
                    ...(game.minPlayers && game.maxPlayers ? [{ label: `👥 ${game.minPlayers}-${game.maxPlayers}` }] : []),
                    ...(game.playingTimeMinutes ? [{ label: `⏱ ${game.playingTimeMinutes}'` }] : []),
                  ]}
                  onClick={() => router.push(`/library/${game.id}`)}
                />
              </div>
            ))}
          </DashboardScrollRow>
        )}
      </section>

      {/* Library section — grid desktop, list mobile */}
      <section>
        <div className="flex items-center justify-between mb-2">
          <h2 className="font-quicksand font-bold text-sm text-foreground flex items-center gap-1.5">
            <span className="text-primary">📚</span> La Tua Libreria
          </h2>
          <Link href="/library" className="text-xs text-primary font-semibold">{games.length} giochi →</Link>
        </div>
        {isLoadingGames ? (
          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
            {Array.from({ length: 4 }).map((_, i) => (
              <MeepleCardSkeleton key={i} variant={isMobile ? 'list' : 'grid'} />
            ))}
          </div>
        ) : (
          <div className={isMobile ? 'flex flex-col gap-2' : 'grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3'}>
            {games.slice(0, isMobile ? 6 : 8).map((game: UserGameDto) => (
              <MeepleCard
                key={game.id}
                entity="game"
                variant={isMobile ? 'list' : 'grid'}
                title={game.title}
                subtitle={game.publisher}
                imageUrl={game.imageUrl ?? game.thumbnailUrl}
                rating={game.averageRating}
                ratingMax={10}
                metadata={[
                  ...(game.minPlayers && game.maxPlayers ? [{ label: `👥 ${game.minPlayers}-${game.maxPlayers}` }] : []),
                  ...(game.playingTimeMinutes ? [{ label: `⏱ ${game.playingTimeMinutes}'` }] : []),
                ]}
                onClick={() => router.push(`/library/${game.id}`)}
              />
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
```

This replaces all 8 v2 imports with the new components. The sidebar panels (RecentAgents, RecentChats, RecentActivity) are removed — navigation is handled by HybridSidebar now. Loading states use `MeepleCardSkeleton`, empty state uses the existing `EmptyState` component.

- [ ] **Step 10: Delete all replaced dashboard components**

```bash
# v2/ — all files replaced by new dashboard layout
rm -rf apps/web/src/components/dashboard/v2/

# zones/ — all replaced
rm -rf apps/web/src/components/dashboard/zones/

# exploration/ — replaced by new sections
rm -rf apps/web/src/components/dashboard/exploration/

# Root-level legacy duplicates
rm apps/web/src/components/dashboard/hero-zone.tsx
rm apps/web/src/components/dashboard/quick-stats.tsx
rm apps/web/src/components/dashboard/__tests__/hero-zone.test.tsx
rm apps/web/src/components/dashboard/__tests__/ExplorationView.test.tsx
rm apps/web/src/components/dashboard/__tests__/DashboardRenderer.test.tsx
```

Verify no remaining imports of deleted files:
```bash
grep -r "from.*dashboard/v2\|from.*dashboard/zones\|from.*dashboard/exploration\|from.*hero-zone\|from.*quick-stats" apps/web/src/ --include="*.tsx" --include="*.ts" -l
```
Fix any remaining imports found.

- [ ] **Step 11: Run all dashboard tests**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/ src/app/(authenticated)/dashboard/`
Expected: All pass (old tests deleted, new tests pass)

- [ ] **Step 12: Visual smoke test**

Run: `cd apps/web && pnpm dev`
Verify at http://localhost:3000/dashboard:
- HeroBanner at top with gradient and next session info
- QuickActionsRow below hero (5 icon buttons)
- Scroll row for "Giocati di recente" with MeepleCard cards
- Grid section for library
- No more sidebar with RecentAgents/RecentChats (moved to HybridSidebar)

- [ ] **Step 13: Commit**

```bash
git add -u && git add apps/web/src/components/dashboard/ apps/web/src/app/
git commit -m "feat(dashboard): gaming immersive home with HeroBanner and QuickActionsRow

Replaces HeroZone, QuickStats, and sidebar layout with new gaming-style
dashboard. HeroBanner shows contextual session info. QuickActionsRow
provides fast navigation. MeepleCard scroll rows for recent/trending.
Removes 8 obsolete dashboard v2/zones files."
```

---

## Task 3: Libreria Redesign

**Branch:** `feature/layout-libreria`
**PR target:** `frontend-dev`
**Files:**
- Move: `apps/web/src/app/(public)/games/components/ViewToggle.tsx` → `apps/web/src/components/ui/ViewToggle.tsx`
- Create: `apps/web/src/components/ui/FilterChipsRow.tsx`
- Create: `apps/web/src/components/ui/__tests__/FilterChipsRow.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/_content.tsx`
- Modify: `apps/web/src/app/(public)/games/components/` (update imports)

- [ ] **Step 1: Write FilterChipsRow tests**

```typescript
// apps/web/src/components/ui/__tests__/FilterChipsRow.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FilterChipsRow } from '../FilterChipsRow';

describe('FilterChipsRow', () => {
  const chips = [
    { id: 'all', label: 'Tutti' },
    { id: 'recent', label: 'Recenti' },
    { id: 'rating', label: 'Rating ↓' },
  ];

  it('renders all chips', () => {
    render(<FilterChipsRow chips={chips} activeId="all" onSelect={() => {}} />);
    expect(screen.getByText('Tutti')).toBeInTheDocument();
    expect(screen.getByText('Recenti')).toBeInTheDocument();
    expect(screen.getByText('Rating ↓')).toBeInTheDocument();
  });

  it('highlights active chip', () => {
    render(<FilterChipsRow chips={chips} activeId="all" onSelect={() => {}} />);
    const allChip = screen.getByText('Tutti');
    expect(allChip.closest('button')).toHaveClass('bg-primary');
  });

  it('calls onSelect when chip clicked', async () => {
    const onSelect = vi.fn();
    render(<FilterChipsRow chips={chips} activeId="all" onSelect={onSelect} />);
    await userEvent.click(screen.getByText('Recenti'));
    expect(onSelect).toHaveBeenCalledWith('recent');
  });
});
```

- [ ] **Step 2: Run test — FAIL**

- [ ] **Step 3: Implement FilterChipsRow**

```typescript
// apps/web/src/components/ui/FilterChipsRow.tsx
'use client';

import { cn } from '@/lib/utils';

interface FilterChip {
  id: string;
  label: string;
}

interface FilterChipsRowProps {
  chips: FilterChip[];
  activeId: string;
  onSelect: (id: string) => void;
  className?: string;
}

export function FilterChipsRow({ chips, activeId, onSelect, className }: FilterChipsRowProps) {
  return (
    <div
      className={cn(
        'flex gap-1.5 overflow-x-auto scrollbar-none pb-1',
        className
      )}
      role="tablist"
    >
      {chips.map(chip => (
        <button
          key={chip.id}
          type="button"
          role="tab"
          aria-selected={chip.id === activeId}
          className={cn(
            'shrink-0 px-3 py-1 rounded-[var(--radius)]',
            'text-xs font-semibold border transition-colors',
            chip.id === activeId
              ? 'bg-primary text-white border-primary'
              : 'bg-card text-muted-foreground border-border hover:bg-muted/50'
          )}
          onClick={() => onSelect(chip.id)}
        >
          {chip.label}
        </button>
      ))}
    </div>
  );
}
```

- [ ] **Step 4: Run FilterChipsRow tests — PASS**

- [ ] **Step 5: Move ViewToggle to shared location**

```bash
mv apps/web/src/app/\(public\)/games/components/ViewToggle.tsx apps/web/src/components/ui/ViewToggle.tsx
```

Update imports in `apps/web/src/app/(public)/games/components/` and any files that import `ViewToggle` from the old path. Search with:
```bash
grep -r "ViewToggle" apps/web/src/ --include="*.tsx" --include="*.ts" -l
```

- [ ] **Step 6: Modify library _content.tsx**

Add FilterChipsRow, ViewToggle, and responsive MeepleCard variant switching to `apps/web/src/app/(authenticated)/library/_content.tsx`.

Key changes:
- Add state: `viewMode: 'grid' | 'list'` (default: responsive — list on mobile, grid on desktop)
- Add FilterChipsRow above the game list
- Use ViewToggle component for manual switching
- Render MeepleCard with `variant={isMobile ? 'list' : viewMode}` (or user-overridden)
- Grid: `grid grid-cols-2 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3`

- [ ] **Step 7: Run library tests**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/library/`

- [ ] **Step 8: Visual smoke test**

Verify at http://localhost:3000/library:
- Filter chips row scrollable
- View toggle grid/list
- Mobile: list variant default
- Desktop: 4col grid default

- [ ] **Step 9: Commit**

```bash
git add -u && git add apps/web/src/components/dashboard/ apps/web/src/app/
git commit -m "feat(library): filter chips, view toggle, mobile list default

Adds FilterChipsRow for library filtering. Moves ViewToggle to shared
ui/ location. Library page defaults to MeepleCard list on mobile,
grid 4col on desktop, with manual toggle."
```

---

## Task 4: Chat Redesign

**Branch:** `feature/layout-chat`
**PR target:** `frontend-dev`
**Files:**
- Modify: `apps/web/src/app/(chat)/chat/layout.tsx`
- Modify: `apps/web/src/app/(chat)/chat/page.tsx`

- [ ] **Step 1: Read current chat layout and page**

Read `apps/web/src/app/(chat)/chat/layout.tsx` and `page.tsx` to understand current structure before modifying.

- [ ] **Step 2: Modify chat layout for 2-panel desktop**

Update `apps/web/src/app/(chat)/chat/layout.tsx`:
- Desktop: flex row with conversation list (w-80 border-r) + children (flex-1)
- Mobile: children only (conversation list is the page.tsx content)

```tsx
// Key layout pattern:
<div className="flex h-full">
  {/* Conversation list panel — desktop only */}
  <aside className="hidden lg:flex w-80 border-r border-border/40 flex-col overflow-y-auto">
    <ChatConversationList />
  </aside>
  {/* Main content — chat thread or conversation list on mobile */}
  <div className="flex-1 min-w-0">
    {children}
  </div>
</div>
```

- [ ] **Step 3: Update chat page.tsx for MeepleCard list variant**

Use MeepleCard list variant with entity `chatSession` for conversation list items. Each conversation shows: title, last message preview, message count, agent name, timestamp.

- [ ] **Step 4: Run chat tests**

Run: `cd apps/web && pnpm vitest run src/app/(chat)/`

- [ ] **Step 5: Visual smoke test**

Verify at http://localhost:3000/chat:
- Desktop: 2-panel (list left, chat right)
- Mobile: full-screen conversation list, tap → chat
- MeepleCard list variant with chat blue entity color

- [ ] **Step 6: Commit**

```bash
git add -u && git add apps/web/src/components/dashboard/ apps/web/src/app/
git commit -m "feat(chat): 2-panel layout desktop, MeepleCard list conversations

Desktop shows conversation list panel (w-80) + active chat side by side.
Mobile keeps full-screen flow. Conversations use MeepleCard list variant
with chatSession entity color."
```

---

## Task 5: Sessioni Redesign

**Branch:** `feature/layout-sessioni`
**PR target:** `frontend-dev`
**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/_content.tsx`
- Modify: `apps/web/src/app/(authenticated)/sessions/page.tsx`

- [ ] **Step 1: Read current sessions content**

Read `_content.tsx` to understand current structure.

- [ ] **Step 2: Update sessions layout**

Key changes to `_content.tsx`:
- Active sessions: render with MeepleCard `featured` variant (desktop) / `hero` variant (mobile)
- Past sessions: MeepleCard `grid` 3-4col (desktop) / `list` (mobile)
- Add session status badge, score table for each session card

- [ ] **Step 3: Add FAB for new session on mobile**

In `page.tsx`, add a floating action button (mobile only):
```tsx
<Link
  href="/sessions/new"
  className="fixed bottom-20 right-4 lg:hidden z-30 w-14 h-14 rounded-full bg-primary text-white shadow-lg flex items-center justify-center"
  aria-label="Nuova sessione"
>
  <Plus className="w-6 h-6" />
</Link>
```

- [ ] **Step 4: Run sessions tests**

Run: `cd apps/web && pnpm vitest run src/app/(authenticated)/sessions/`

- [ ] **Step 5: Visual smoke test**

Verify at http://localhost:3000/sessions:
- Active sessions: large featured cards (desktop), hero (mobile)
- Past sessions: grid (desktop), list (mobile)
- FAB visible on mobile

- [ ] **Step 6: Commit**

```bash
git add -u && git add apps/web/src/components/dashboard/ apps/web/src/app/
git commit -m "feat(sessions): featured/hero active sessions, list past sessions

Active sessions use MeepleCard featured (desktop) and hero (mobile).
Past sessions use grid 3-4col desktop, list mobile. Adds mobile FAB
for new session creation."
```

---

## Task 6: Scopri / Catalogo Redesign

**Branch:** `feature/layout-scopri`
**PR target:** `frontend-dev`
**Depends on:** Task 3 (ViewToggle move). If Task 3 is not yet merged, import ViewToggle from original path `@/app/(public)/games/components/ViewToggle` instead.
**Files:**
- Modify: `apps/web/src/app/(public)/games/page.tsx`
- Modify: `apps/web/src/app/(public)/games/catalog/page.tsx`
- Modify: imports for `ViewToggle` in catalog components

- [ ] **Step 1: Read current catalog pages**

Read `page.tsx` and `catalog/page.tsx` to understand current structure.

- [ ] **Step 2: Update games page — search + categories + grid**

Key changes to `apps/web/src/app/(public)/games/page.tsx`:
- Prominent search input at top (sticky on mobile)
- Category FilterChipsRow: Tutti, Strategici, Party, Cooperativi, Solo, Nuove uscite
- MeepleCard grid 4col (desktop) / list (mobile) with ViewToggle
- Update ViewToggle import to `@/components/ui/ViewToggle`

- [ ] **Step 3: Update catalog page similarly**

Apply same pattern to `catalog/page.tsx` for the shared catalog view.

- [ ] **Step 4: Run catalog tests**

Run: `cd apps/web && pnpm vitest run src/app/(public)/games/`

- [ ] **Step 5: Visual smoke test**

Verify at http://localhost:3000/games:
- Search bar prominent
- Category chips scrollable
- Grid 4col desktop, list mobile
- ViewToggle works

- [ ] **Step 6: Commit**

```bash
git add -u && git add apps/web/src/components/dashboard/ apps/web/src/app/
git commit -m "feat(scopri): search bar, category chips, grid/list responsive catalog

Adds prominent search bar, category FilterChipsRow, and responsive
grid (4col desktop) / list (mobile) layout for game catalog pages.
ViewToggle shared from ui/ components."
```

---

## Summary

| Task | Branch | Scope | Key Files |
|------|--------|-------|-----------|
| 0 | `feature/layout-shell-restructure` | Shell restructure | UserShellClient, Layout |
| 1 | `feature/layout-hybrid-sidebar` | HybridSidebar + ⌘K | HybridSidebar (new), UserTopNav |
| 2 | `feature/layout-dashboard` | Home redesign | HeroBanner, QuickActionsRow, dashboard-client |
| 3 | `feature/layout-libreria` | Library redesign | FilterChipsRow, ViewToggle, _content |
| 4 | `feature/layout-chat` | Chat 2-panel | chat/layout, chat/page |
| 5 | `feature/layout-sessioni` | Sessions redesign | sessions/_content, sessions/page |
| 6 | `feature/layout-scopri` | Catalog redesign | games/page, games/catalog/page |

All PRs target `frontend-dev`. Each is independently shippable.
