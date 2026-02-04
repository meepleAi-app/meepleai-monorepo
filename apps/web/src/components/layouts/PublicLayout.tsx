/**
 * PublicLayout Component - Issue #2230
 * Updated: Issue #3104 - Use UnifiedHeader
 *
 * Layout wrapper per pagine pubbliche dell'applicazione.
 * Compone UnifiedHeader + content area + PublicFooter + BottomNav (mobile).
 *
 * @deprecated For authenticated pages - Issue #3479
 * For authenticated routes, use AuthenticatedLayout instead which provides:
 * - UnifiedHeader (desktop nav + settings + notifications)
 * - UnifiedActionBar (mobile bottom nav with integrated FAB)
 * - Better mobile-first experience
 *
 * This component should only be used for truly public pages (marketing, login, etc.)
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

import { BottomNav } from '@/components/layout/BottomNav';
import { UnifiedHeader } from '@/components/layout/UnifiedHeader';
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

  return (
    <div className="min-h-screen flex flex-col bg-background">
      {/* Unified Header (handles auth internally) */}
      <UnifiedHeader />

      {/* Main Content - add padding bottom for mobile bottom nav */}
      <main id="main-content" className={cn('flex-1 w-full pb-20 md:pb-0', className)}>
        <div className={cn('mx-auto px-4 sm:px-6 lg:px-8 py-8', containerClass)}>{children}</div>
      </main>

      {/* Footer - hidden on mobile to make room for BottomNav */}
      <div className="hidden md:block">
        <PublicFooter showNewsletter={showNewsletter} />
      </div>

      {/* Bottom Navigation (mobile only) */}
      <BottomNav />
    </div>
  );
}
