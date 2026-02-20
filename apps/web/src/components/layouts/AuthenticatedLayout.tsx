/**
 * AuthenticatedLayout Component - Issue #4936 (updated)
 * Issue #3479 - Layout System v2: Unified Layout for Authenticated Pages
 *
 * Navigation structure:
 * - ALL breakpoints: UniversalNavbar (56px, fixed top) — Logo + Search + Profile
 * - Desktop (md+): Sidebar (collapsible, fixed left, starts at top-14)
 * - Mobile (<md):  UnifiedActionBar (56px, fixed bottom) with FAB
 *
 * This layout replaces PublicLayout for authenticated routes.
 */

'use client';

import { type ReactNode } from 'react';

import { UnifiedActionBar, UnifiedActionBarSpacer } from '@/components/layout/ActionBar';
import { Breadcrumb } from '@/components/layout/Breadcrumb';
import { UniversalNavbar } from '@/components/layout/Navbar/UniversalNavbar';
import { Sidebar } from '@/components/layout/Sidebar';
import { ImpersonationBanner } from '@/components/ui/feedback/impersonation-banner';
import { CardStackPanel } from '@/components/ui/navigation/card-stack-panel';
import { useSidebarState } from '@/hooks/useSidebarState';
import { cn } from '@/lib/utils';
import { useImpersonationStore } from '@/store/impersonation';

export interface AuthenticatedLayoutProps {
  /** Page content */
  children: ReactNode;
  /** Whether to show the breadcrumb navigation */
  showBreadcrumb?: boolean;
  /** Whether to show the UnifiedActionBar (mobile bottom nav with FAB) */
  showActionBar?: boolean;
  /** Additional CSS classes for the main content area */
  className?: string;
  /** Full width layout (no max-width container) */
  fullWidth?: boolean;
}

/**
 * AuthenticatedLayout
 *
 * Main layout component for authenticated pages.
 * Provides unified navigation experience with:
 * - ALL: Universal top navbar (56px) with Logo + Search + ProfileBar
 * - Desktop: Collapsible context-sensitive sidebar (starts below navbar)
 * - Mobile: Bottom ActionBar (56px) with FAB
 */
export function AuthenticatedLayout({
  children,
  showBreadcrumb = true,
  showActionBar = true,
  className,
  fullWidth = false,
}: AuthenticatedLayoutProps) {
  const { isImpersonating, impersonatedUser, isLoading, endImpersonation } = useImpersonationStore();
  const { isCollapsed, toggle } = useSidebarState();

  return (
    <div className="min-h-screen flex flex-col bg-background">
      {/* ── Universal Navbar (ALL breakpoints, fixed top, h-14 = 56px) ── */}
      <UniversalNavbar />

      {/* ── Impersonation Banner (below navbar) ─────────────────────── */}
      <ImpersonationBanner
        isImpersonating={isImpersonating}
        impersonatedUser={impersonatedUser}
        onEndImpersonation={endImpersonation}
        isLoading={isLoading}
      />

      {/* ── Desktop Sidebar (fixed, md+, starts at top-14) ───────────── */}
      <Sidebar isCollapsed={isCollapsed} onToggle={toggle} />

      {/* ── Main Content Area ────────────────────────────────────────── */}
      <main
        className={cn(
          'flex-1',
          // ALL breakpoints: offset for Universal Navbar (56px = pt-14)
          'pt-14',
          // Desktop: offset for sidebar width (sidebar starts at top-14)
          isCollapsed ? 'md:ml-[60px]' : 'md:ml-[220px]',
          'transition-[margin-left] duration-200 ease-in-out motion-reduce:transition-none',
          // Extra top padding when impersonation banner is visible
          isImpersonating && 'pt-24 md:pt-24',
          !fullWidth && 'container mx-auto px-4 sm:px-6 lg:px-8',
          className
        )}
      >
        {/* Breadcrumb Navigation (hidden on mobile) */}
        {showBreadcrumb && (
          <div className="hidden sm:block py-3">
            <Breadcrumb />
          </div>
        )}

        {/* Page Content */}
        <div className="pb-4">
          {children}
        </div>
      </main>

      {/* ── Unified ActionBar (Mobile only: bottom nav + FAB) ─────────── */}
      {showActionBar && (
        <>
          <UnifiedActionBar />
          <UnifiedActionBarSpacer />
        </>
      )}

      {/* Card Stack Panel — "Carte in Mano" navigation */}
      <CardStackPanel />
    </div>
  );
}

export default AuthenticatedLayout;
