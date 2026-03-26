/**
 * PublicLayout Component - Issue #2230
 * Simplified: removed old Sidebar/ActionBar refs (replaced by UserShell)
 *
 * Layout wrapper for public pages. Authenticated users see UserShell
 * via the (authenticated) route group; this layout is for unauthenticated
 * pages only (home, about, pricing, etc.).
 */

'use client';

import { type ReactNode } from 'react';

import { UnifiedHeader } from '@/components/layout/UnifiedHeader';
import { cn } from '@/lib/utils';

import { PublicFooter } from './PublicFooter';

export interface PublicLayoutProps {
  children: ReactNode;
  showNewsletter?: boolean;
  className?: string;
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
  const containerClass = CONTAINER_WIDTHS[containerWidth] || CONTAINER_WIDTHS.full;

  return (
    <div className="min-h-screen flex flex-col bg-background">
      <UnifiedHeader />

      <main id="main-content" className={cn('flex-1 w-full', className)}>
        <div className={cn('mx-auto px-4 sm:px-6 lg:px-8 py-8', containerClass)}>{children}</div>
      </main>

      <PublicFooter showNewsletter={showNewsletter} />
    </div>
  );
}
