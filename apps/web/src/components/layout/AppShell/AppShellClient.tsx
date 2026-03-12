/**
 * AppShellClient — Auth-aware layout shell (client component)
 *
 * Unified shell that handles both authenticated and public users:
 * - Authenticated: shows Sidebar + full navigation system
 * - Public: shows TopNavbar only, no sidebar margin offset
 *
 * Uses AdaptiveBottomBar (mobile) instead of separate
 * MobileTabBar + FloatingActionBar + SmartFAB components.
 */

'use client';

import { type ReactNode, Suspense } from 'react';

import { ErrorBoundary } from '@/components/errors/ErrorBoundary';
import { ImpersonationBanner } from '@/components/ui/feedback/impersonation-banner';
import { CardStackPanel } from '@/components/ui/navigation/card-stack-panel';
import { NavigationProvider } from '@/context/NavigationContext';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useBottomPadding } from '@/hooks/useBottomPadding';
import { useSidebarState } from '@/hooks/useSidebarState';
import { cn } from '@/lib/utils';
import { useImpersonationStore } from '@/store/impersonation';

import { AdaptiveBottomBar } from '../AdaptiveBottomBar';
import { FloatingActionBar } from '../FloatingActionBar';
import { MobileBreadcrumb } from '../MobileBreadcrumb';
import { Sidebar } from '../Sidebar/Sidebar';
import { TopNavbar } from '../TopNavbar';

// ─── Props ──────────────────────────────────────────────────────────────────────

export interface AppShellClientProps {
  children: ReactNode;
  /** Initial sidebar collapsed state from cookie (avoids flash) */
  initialSidebarCollapsed?: boolean;
  /** Remove horizontal padding from the content area */
  fullWidth?: boolean;
  /** Additional className for the main element */
  className?: string;
}

// ─── Component ──────────────────────────────────────────────────────────────────

export function AppShellClient({
  children,
  initialSidebarCollapsed = false,
  fullWidth = false,
  className,
}: AppShellClientProps) {
  const { data: user } = useCurrentUser();
  const isAuthenticated = !!user;
  const { isImpersonating, impersonatedUser, isLoading, endImpersonation } =
    useImpersonationStore();
  const { isCollapsed, toggle } = useSidebarState(initialSidebarCollapsed);
  const bottomPadding = useBottomPadding();

  return (
    <NavigationProvider>
      <div className="min-h-screen flex flex-col bg-background" data-testid="app-shell">
        {/* Impersonation Banner (fixed at top when active) */}
        <ImpersonationBanner
          isImpersonating={isImpersonating}
          impersonatedUser={impersonatedUser}
          onEndImpersonation={endImpersonation}
          isLoading={isLoading}
        />

        {/* L1: TopNavbar (always visible) */}
        <TopNavbar />

        {/* Desktop Sidebar (authenticated only) */}
        {isAuthenticated && <Sidebar isCollapsed={isCollapsed} onToggle={toggle} />}

        {/* Content area (offset by sidebar width when authenticated) */}
        <div
          className={cn(
            'flex flex-col flex-1',
            'transition-[margin] duration-200 ease-in-out',
            isAuthenticated &&
              (isCollapsed
                ? 'md:ml-[var(--sidebar-width-collapsed)]'
                : 'md:ml-[var(--sidebar-width-expanded)]')
          )}
          data-testid="app-shell-content"
        >
          {/* Mobile Breadcrumb (mobile only) */}
          <MobileBreadcrumb />

          {/* Main content */}
          <main
            id="main-content"
            tabIndex={-1}
            style={{ viewTransitionName: 'page-content' }}
            className={cn(
              'flex-1',
              !fullWidth && 'px-4 sm:px-6 lg:px-8',
              bottomPadding,
              'pt-4',
              className
            )}
          >
            {children}
          </main>

          {/* L3: Desktop FloatingActionBar (hidden on mobile) */}
          <ErrorBoundary fallback={null} componentName="FloatingActionBar">
            <Suspense>
              <div className="hidden md:block">
                <FloatingActionBar />
              </div>
            </Suspense>
          </ErrorBoundary>
        </div>

        {/* Mobile: AdaptiveBottomBar (replaces MobileTabBar + SmartFAB) */}
        <ErrorBoundary fallback={null} componentName="AdaptiveBottomBar">
          <AdaptiveBottomBar />
        </ErrorBoundary>

        {/* Card Stack Panel */}
        <ErrorBoundary fallback={null} componentName="CardStackPanel">
          <CardStackPanel />
        </ErrorBoundary>
      </div>
    </NavigationProvider>
  );
}
