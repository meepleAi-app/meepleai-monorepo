/**
 * AuthenticatedLayout Component
 * Issue #3479 - Layout System v2: Unified Layout for Authenticated Pages
 *
 * Navigation structure:
 * - Desktop: Sidebar (collapsible, fixed left) — no top header
 * - Mobile: Compact UnifiedHeader (48px) + UnifiedActionBar (56px bottom)
 *
 * This layout replaces PublicLayout for authenticated routes.
 */

'use client';

import { type ReactNode } from 'react';

import { UnifiedActionBar, UnifiedActionBarSpacer } from '@/components/layout/ActionBar';
import { Breadcrumb } from '@/components/layout/Breadcrumb';
import { Sidebar } from '@/components/layout/Sidebar';
import { UnifiedHeader } from '@/components/layout/UnifiedHeader';
import { ImpersonationBanner } from '@/components/ui/feedback/impersonation-banner';
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
 * - Desktop: Collapsible sidebar with nav + user section
 * - Mobile: Compact header (48px) + bottom ActionBar (56px) with FAB
 */
export function AuthenticatedLayout({
  children,
  showBreadcrumb = true,
  showActionBar = true,
  className,
  fullWidth = false,
}: AuthenticatedLayoutProps) {
  // Impersonation state (Issue #3349)
  const { isImpersonating, impersonatedUser, isLoading, endImpersonation } = useImpersonationStore();
  const { isCollapsed, toggle } = useSidebarState();

  return (
    <div className="min-h-screen flex flex-col bg-background">
      {/* Impersonation Banner - Shows when admin is impersonating a user */}
      <ImpersonationBanner
        isImpersonating={isImpersonating}
        impersonatedUser={impersonatedUser}
        onEndImpersonation={endImpersonation}
        isLoading={isLoading}
      />

      {/* Desktop Sidebar (fixed, md+ only) */}
      <Sidebar isCollapsed={isCollapsed} onToggle={toggle} />

      {/* Mobile Header (md:hidden) */}
      <div className="md:hidden">
        <UnifiedHeader />
      </div>

      {/* Main Content Area */}
      <main
        className={cn(
          'flex-1',
          // Mobile: offset for compact header (48px)
          'pt-12 md:pt-0',
          // Desktop: offset for sidebar width
          isCollapsed ? 'md:ml-[60px]' : 'md:ml-[220px]',
          'transition-[margin-left] duration-200 ease-in-out motion-reduce:transition-none',
          isImpersonating && 'pt-20 md:pt-8',
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

      {/* Unified ActionBar - Mobile only: bottom nav + integrated FAB */}
      {showActionBar && (
        <>
          <UnifiedActionBar />
          <UnifiedActionBarSpacer />
        </>
      )}
    </div>
  );
}

export default AuthenticatedLayout;
