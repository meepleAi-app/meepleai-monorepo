/**
 * AuthenticatedLayout Component
 * Issue #3479 - Layout System v2: Unified Layout for Authenticated Pages
 *
 * Combines:
 * - UnifiedHeader (desktop nav + mobile top bar)
 * - SmartFAB (context-aware floating action button)
 * - UnifiedActionBar (bottom nav + context actions)
 * - Breadcrumb (navigation context)
 *
 * This layout replaces PublicLayout for authenticated routes.
 */

'use client';

import { type ReactNode } from 'react';

import { UnifiedActionBar, UnifiedActionBarSpacer } from '@/components/layout/ActionBar';
import { Breadcrumb } from '@/components/layout/Breadcrumb';
import { SmartFAB } from '@/components/layout/SmartFAB';
import { UnifiedHeader } from '@/components/layout/UnifiedHeader';
import { cn } from '@/lib/utils';

export interface AuthenticatedLayoutProps {
  /** Page content */
  children: ReactNode;
  /** Whether to show the breadcrumb navigation */
  showBreadcrumb?: boolean;
  /** Whether to show the SmartFAB */
  showFAB?: boolean;
  /** Whether to show the UnifiedActionBar (bottom nav) */
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
 * - Desktop: Header with full nav + breadcrumbs
 * - Mobile: Compact header + bottom nav bar + FAB
 */
export function AuthenticatedLayout({
  children,
  showBreadcrumb = true,
  showFAB = true,
  showActionBar = true,
  className,
  fullWidth = false,
}: AuthenticatedLayoutProps) {
  return (
    <div className="min-h-screen flex flex-col bg-background">
      {/* Top Header - UnifiedHeader */}
      <UnifiedHeader />

      {/* Main Content Area */}
      <main
        className={cn(
          'flex-1',
          'pt-16', // Header height offset
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

      {/* Smart FAB - Mobile only, context-aware */}
      {showFAB && <SmartFAB />}

      {/* Unified ActionBar - Bottom navigation + context actions */}
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
