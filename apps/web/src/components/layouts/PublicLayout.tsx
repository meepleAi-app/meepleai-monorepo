/**
 * PublicLayout Component - Issue #2230
 * Updated: Issue #3104 - Use UnifiedHeader
 *
 * Layout wrapper per pagine pubbliche dell'applicazione.
 * - Authenticated users: Sidebar (desktop) + compact header (mobile) + ActionBar
 * - Unauthenticated users: UnifiedHeader (all sizes)
 *
 * Features:
 * - Container responsive
 * - Min-height per footer sticky
 * - Dark mode support
 * - Consistent spacing
 * - Unified navigation across all pages
 */

'use client';

import { ReactNode } from 'react';

import { UnifiedActionBar, UnifiedActionBarSpacer } from '@/components/layout/ActionBar';
import { MobileTabBar } from '@/components/layout/MobileTabBar';
import { Sidebar } from '@/components/layout/Sidebar';
import { UnifiedHeader } from '@/components/layout/UnifiedHeader';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useSidebarState } from '@/hooks/useSidebarState';
import { cn } from '@/lib/utils';

import { PublicFooter } from './PublicFooter';

export interface PublicLayoutProps {
  /** Page content */
  children: ReactNode;
  /** Show newsletter in footer */
  showNewsletter?: boolean;
  /** Additional className for main content */
  className?: string;
  /** Container max width (default: full) */
  containerWidth?: 'sm' | 'md' | 'lg' | 'xl' | 'full';
}

const CONTAINER_WIDTHS: Record<string, string> = {
  sm: 'max-w-3xl',
  md: 'max-w-5xl',
  lg: 'max-w-7xl',
  xl: 'max-w-screen-2xl',
  full: 'max-w-full',
};

export function PublicLayout({
  children,
  showNewsletter = false,
  className,
  containerWidth = 'full',
}: PublicLayoutProps) {
  // eslint-disable-next-line security/detect-object-injection
  const containerClass = CONTAINER_WIDTHS[containerWidth] || CONTAINER_WIDTHS.full;
  const { data: user } = useCurrentUser();
  const isAuthenticated = !!user;
  const { isCollapsed, toggle } = useSidebarState();

  return (
    <div className="min-h-screen flex flex-col bg-background">
      {/* Sidebar for authenticated users on desktop */}
      {isAuthenticated && <Sidebar isCollapsed={isCollapsed} onToggle={toggle} />}

      {/* Header: mobile-only for authenticated (sidebar handles desktop), all sizes for guests */}
      <div className={isAuthenticated ? 'md:hidden' : undefined}>
        <UnifiedHeader />
      </div>

      {/* Main Content */}
      <main
        id="main-content"
        className={cn(
          'flex-1 w-full',
          // Bottom padding for mobile MobileTabBar (72px + gap)
          'pb-24 md:pb-0',
          // Desktop sidebar offset for authenticated users
          isAuthenticated && (isCollapsed ? 'md:ml-[60px]' : 'md:ml-[220px]'),
          isAuthenticated &&
            'transition-[margin-left] duration-200 ease-in-out motion-reduce:transition-none',
          className
        )}
      >
        <div className={cn('mx-auto px-4 sm:px-6 lg:px-8 py-8', containerClass)}>{children}</div>
      </main>

      {/* Footer - hidden on mobile to make room for bottom nav */}
      <div
        className={cn(
          'hidden md:block',
          isAuthenticated && (isCollapsed ? 'md:ml-[60px]' : 'md:ml-[220px]'),
          isAuthenticated &&
            'transition-[margin-left] duration-200 ease-in-out motion-reduce:transition-none'
        )}
      >
        <PublicFooter showNewsletter={showNewsletter} />
      </div>

      {/* Action Bar (authenticated only) */}
      {isAuthenticated && (
        <>
          <UnifiedActionBar />
          <UnifiedActionBarSpacer />
        </>
      )}

      {/* MobileTabBar (persistent mobile navigation, auth-gated internally) */}
      <MobileTabBar />
    </div>
  );
}
