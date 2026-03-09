/**
 * LayoutShell — Left Sidebar + Slim TopNavbar Layout
 * Issue #5035 — LayoutShell Component
 *
 * Full-page shell with left sidebar navigation:
 *
 *   ┌────────────────────────────────────────────┐
 *   │ TopNavbar (sticky h-14, slim: logo+avatar) │
 *   ├──────┬─────────────────────────────────────┤
 *   │      │ MiniNav (h-10, context-aware)       │ ← auto-hides when no tabs
 *   │ Side ├─────────────────────────────────────┤
 *   │ bar  │                                     │
 *   │      │   Page Content (scrollable)         │
 *   │      │                                     │
 *   │      │   ┌───────────────────┐             │
 *   │      │   │ FloatingActionBar │             │
 *   │      │   └───────────────────┘             │
 *   └──────┴─────────────────────────────────────┘
 *
 * Responsibilities:
 * - Provides NavigationProvider so pages can call useSetNavConfig()
 * - Renders Sidebar (desktop-only, collapsible) + slim TopNavbar
 * - Renders MiniNav + FloatingActionBar offset by sidebar width
 * - Preserves ImpersonationBanner and CardStackPanel
 * - Mobile: no sidebar, MobileNavDrawer via hamburger in TopNavbar
 *
 * Usage:
 * ```tsx
 * import { LayoutShell } from '@/components/layout/LayoutShell';
 *
 * export default function AuthenticatedRouteLayout({ children }) {
 *   return <LayoutShell>{children}</LayoutShell>;
 * }
 * ```
 */

'use client';

import { type ReactNode, Suspense } from 'react';

import { ImpersonationBanner } from '@/components/ui/feedback/impersonation-banner';
import { CardStackPanel } from '@/components/ui/navigation/card-stack-panel';
import { NavigationProvider } from '@/context/NavigationContext';
import { useBottomPadding } from '@/hooks/useBottomPadding';
import { useSidebarState } from '@/hooks/useSidebarState';
import { cn } from '@/lib/utils';
import { useImpersonationStore } from '@/store/impersonation';

import { FloatingActionBar } from '../FloatingActionBar';
import { MiniNav } from '../MiniNav';
import { MobileBreadcrumb } from '../MobileBreadcrumb';
import { MobileTabBar } from '../MobileTabBar';
import { Sidebar } from '../Sidebar/Sidebar';
import { SmartFAB } from '../SmartFAB';
import { TopNavbar } from '../TopNavbar';

// ─── LayoutShell ──────────────────────────────────────────────────────────────

export interface LayoutShellProps {
  children: ReactNode;
  /** Remove horizontal padding from the content area (for full-bleed pages) */
  fullWidth?: boolean;
  /** Additional className for the main element */
  className?: string;
}

/**
 * LayoutShell (Concept 4 — Floating)
 *
 * Composes the 3-tier navigation system into a single shell.
 * NavigationProvider is internal so pages can register their nav config
 * via useSetNavConfig() anywhere in the tree.
 */
export function LayoutShell({ children, fullWidth = false, className }: LayoutShellProps) {
  return (
    <NavigationProvider>
      <LayoutShellInner fullWidth={fullWidth} className={className}>
        {children}
      </LayoutShellInner>
    </NavigationProvider>
  );
}

// ─── Inner shell (needs NavigationProvider in scope) ─────────────────────────

function LayoutShellInner({ children, fullWidth, className }: LayoutShellProps) {
  const { isImpersonating, impersonatedUser, isLoading, endImpersonation } =
    useImpersonationStore();
  const { isCollapsed, toggle } = useSidebarState();
  const bottomPadding = useBottomPadding();

  return (
    <div className="min-h-screen flex flex-col bg-background" data-testid="layout-shell">
      {/* Impersonation Banner (fixed at top when active) */}
      <ImpersonationBanner
        isImpersonating={isImpersonating}
        impersonatedUser={impersonatedUser}
        onEndImpersonation={endImpersonation}
        isLoading={isLoading}
      />

      {/* ── Level 1: TopNavbar (slim: logo + notifications + user) ────────── */}
      <TopNavbar />

      {/* ── Desktop Sidebar (hidden on mobile) ────────────────────────────── */}
      <Sidebar isCollapsed={isCollapsed} onToggle={toggle} />

      {/* ── Content area (offset by sidebar width on desktop) ─────────────── */}
      <div
        className={cn(
          'flex flex-col flex-1',
          'transition-[margin] duration-200 ease-in-out',
          isCollapsed ? 'md:ml-[60px]' : 'md:ml-[220px]'
        )}
        data-testid="layout-content-area"
      >
        {/* ── Level 2: MiniNav (auto-hides when no tabs) ─────────────────── */}
        <Suspense>
          <MiniNav />
        </Suspense>

        {/* ── Mobile Breadcrumb (below MiniNav, mobile only) ───────────── */}
        <MobileBreadcrumb />

        {/* ── Main content ─────────────────────────────────────────────────── */}
        <main
          id="main-content"
          tabIndex={-1}
          className={cn(
            'flex-1',
            // Horizontal padding unless fullWidth
            !fullWidth && 'px-4 sm:px-6 lg:px-8',
            // Dynamic bottom padding based on visible bottom bars
            bottomPadding,
            // Top spacing
            'pt-4',
            className
          )}
        >
          {children}
        </main>

        {/* ── Level 3: FloatingActionBar (auto-hides when no actions) ───── */}
        <FloatingActionBar />

        {/* ── SmartFAB: context-aware primary action (mobile only) ────────── */}
        <SmartFAB />
      </div>

      {/* ── Level 0: MobileTabBar (persistent mobile navigation) ───────── */}
      <MobileTabBar />

      {/* Card Stack Panel — "Carte in Mano" */}
      <CardStackPanel />
    </div>
  );
}
