/**
 * AuthenticatedLayout Component
 * Issue #3479 - Layout System v2: Unified Layout for Authenticated Pages
 *
 * Combines:
 * - UnifiedHeader (desktop nav + settings + notifications + user menu)
 * - UnifiedActionBar (mobile-only: bottom nav + integrated FAB + context actions)
 * - Breadcrumb (navigation context)
 *
 * Navigation structure:
 * - Desktop: UnifiedHeader handles all navigation
 * - Mobile: UnifiedHeader (top) + UnifiedActionBar (bottom with integrated FAB)
 *
 * This layout replaces PublicLayout for authenticated routes.
 */

'use client';

import { type ReactNode } from 'react';

import { UnifiedActionBar, UnifiedActionBarSpacer } from '@/components/layout/ActionBar';
import { Breadcrumb } from '@/components/layout/Breadcrumb';
import { UnifiedHeader } from '@/components/layout/UnifiedHeader';
import { ImpersonationBanner } from '@/components/ui/feedback/impersonation-banner';
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
 * - Desktop: Header with full nav + settings + breadcrumbs
 * - Mobile: Compact header + bottom nav bar with integrated FAB
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

  return (
    <div className="min-h-screen flex flex-col bg-background">
      {/* Impersonation Banner - Shows when admin is impersonating a user */}
      <ImpersonationBanner
        isImpersonating={isImpersonating}
        impersonatedUser={impersonatedUser}
        onEndImpersonation={endImpersonation}
        isLoading={isLoading}
      />

      {/* Top Header - UnifiedHeader (Desktop nav, Mobile top bar) */}
      <UnifiedHeader />

      {/* Main Content Area */}
      <main
        className={cn(
          'flex-1',
          'pt-16', // Header height offset
          isImpersonating && 'pt-24', // Extra padding for impersonation banner
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
