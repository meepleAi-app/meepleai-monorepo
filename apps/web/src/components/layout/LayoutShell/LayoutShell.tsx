/**
 * LayoutShell — CardRack + TopBar Layout ("Game Table" UX)
 * Issue #5035 — LayoutShell Component (updated for Game Table redesign)
 *
 * Full-page shell with Card Rack sidebar navigation:
 *
 *   ┌──────────────────────────────────────────────────────────┐
 *   │ TopBar (sticky h-12, breadcrumb+⌘K+avatar)              │
 *   ├──────┬──────────────────────────────────┬────────────────┤
 *   │ Card │ MiniNav (h-12, context-aware)    │ QuickView      │
 *   │ Rack ├──────────────────────────────────┤ 300px, xl+     │
 *   │ 64px │                                  │ rules/FAQ/AI   │
 *   │      │   Page Content (scrollable)      │                │
 *   │      │                                  │                │
 *   │      │   ┌───────────────────┐          │                │
 *   │      │   │ FloatingActionBar │          │                │
 *   │      │   └───────────────────┘          │                │
 *   └──────┴──────────────────────────────────┴────────────────┘
 *
 * Responsibilities:
 * - Provides NavigationProvider so pages can call useSetNavConfig()
 * - Renders CardRack (desktop-only, hover-expand 64→240px) + TopBar
 * - Renders MiniNav + FloatingActionBar offset by CardRack width
 * - Preserves ImpersonationBanner and CardStackPanel
 * - Mobile: no sidebar, MobileNavDrawer via hamburger in TopBar
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

import { ErrorBoundary } from '@/components/errors/ErrorBoundary';
import { ImpersonationBanner } from '@/components/ui/feedback/impersonation-banner';
import { CardStackPanel } from '@/components/ui/navigation/card-stack-panel';
import { NavigationProvider } from '@/context/NavigationContext';
import { useBottomPadding } from '@/hooks/useBottomPadding';
import { cn } from '@/lib/utils';
import { useImpersonationStore } from '@/store/impersonation';

import { CardRack } from '../CardRack';
import { FloatingActionBar } from '../FloatingActionBar';
import { MiniNav } from '../MiniNav';
import { MobileBreadcrumb } from '../MobileBreadcrumb';
import { MobileTabBar } from '../MobileTabBar';
import { QuickView } from '../QuickView';
import { SmartFAB } from '../SmartFAB';
import { TopBar } from '../TopBar';

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

      {/* ── Level 1: TopBar (breadcrumb + ⌘K search + notifications + user) ── */}
      <TopBar />

      {/* ── Desktop CardRack (hidden on mobile, hover-expand 64→240px) ──── */}
      <CardRack />

      {/* ── Content area (offset by CardRack width on desktop) ────────────── */}
      <div
        className={cn(
          'flex flex-1',
          'transition-[margin] duration-200 ease-in-out',
          'md:ml-[var(--card-rack-width,64px)]'
        )}
      >
        <div className="flex flex-col flex-1" data-testid="layout-content-area">
          {/* ── Level 2: MiniNav (auto-hides when no tabs) ─────────────────── */}
          <ErrorBoundary fallback={null} componentName="MiniNav">
            <Suspense>
              <MiniNav />
            </Suspense>
          </ErrorBoundary>

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
          <ErrorBoundary fallback={null} componentName="FloatingActionBar">
            <Suspense>
              <FloatingActionBar />
            </Suspense>
          </ErrorBoundary>

          {/* ── SmartFAB: context-aware primary action (mobile only) ────────── */}
          <ErrorBoundary fallback={null} componentName="SmartFAB">
            <Suspense>
              <SmartFAB />
            </Suspense>
          </ErrorBoundary>
        </div>

        {/* ── QuickView panel (rules / FAQ / AI, xl+ only) ─────────────────── */}
        <QuickView />
      </div>

      {/* ── Level 0: MobileTabBar (persistent mobile navigation) ───────── */}
      <ErrorBoundary fallback={null} componentName="MobileTabBar">
        <MobileTabBar />
      </ErrorBoundary>

      {/* Card Stack Panel — "Carte in Mano" */}
      <ErrorBoundary fallback={null} componentName="CardStackPanel">
        <CardStackPanel />
      </ErrorBoundary>
    </div>
  );
}
