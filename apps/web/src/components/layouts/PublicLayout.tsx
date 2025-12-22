/**
 * PublicLayout Component - Issue #2230
 *
 * Layout wrapper per pagine pubbliche dell'applicazione.
 * Compone PublicHeader + content area + PublicFooter.
 *
 * Features:
 * - Container responsive
 * - Min-height per footer sticky
 * - Dark mode support
 * - Consistent spacing
 */

'use client';

import { ReactNode } from 'react';

import { cn } from '@/lib/utils';

import { PublicFooter } from './PublicFooter';
import { PublicHeader, type PublicUser } from './PublicHeader';

export interface PublicLayoutProps {
  /** Page content */
  children: ReactNode;
  /** Current user (undefined if not authenticated) */
  user?: PublicUser;
  /** Logout callback */
  onLogout?: () => void;
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
  user,
  onLogout,
  showNewsletter = false,
  className,
  containerWidth = 'full',
}: PublicLayoutProps) {
  // eslint-disable-next-line security/detect-object-injection
  const containerClass = CONTAINER_WIDTHS[containerWidth] || CONTAINER_WIDTHS.full;

  return (
    <div className="min-h-screen flex flex-col bg-background">
      {/* Header */}
      <PublicHeader user={user} onLogout={onLogout} />

      {/* Main Content */}
      <main id="main-content" className={cn('flex-1 w-full', className)}>
        <div className={cn('mx-auto px-4 sm:px-6 lg:px-8 py-8', containerClass)}>{children}</div>
      </main>

      {/* Footer */}
      <PublicFooter showNewsletter={showNewsletter} />
    </div>
  );
}
