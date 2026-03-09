/**
 * Layout - Main Layout Wrapper Component
 * Issue #3287 - Phase 1: Core Layout Structure
 *
 * Wraps all pages with the layout structure including:
 * - Navbar (top navigation)
 * - Main content area with responsive padding
 * - ActionBar (bottom bar with context actions)
 * - FAB (floating action button, mobile only)
 * - Safe area handling for mobile devices
 *
 * @example
 * ```tsx
 * <Layout>
 *   <YourPageContent />
 * </Layout>
 * ```
 */

'use client';

import { type ReactNode } from 'react';

import { cn } from '@/lib/utils';

import { useLayout } from './LayoutProvider';

/**
 * Layout component props
 */
export interface LayoutProps {
  /** Page content */
  children: ReactNode;
  /** Additional className for the main content area */
  className?: string;
  /** Whether to show the action bar (default: true) */
  showActionBar?: boolean;
  /** Whether to show the FAB (default: true on mobile) */
  showFAB?: boolean;
  /** Whether to apply padding to main content (default: true) */
  withPadding?: boolean;
  /** Whether this is a full-bleed layout (no max-width constraint) */
  fullBleed?: boolean;
}

/**
 * Layout Component
 *
 * Provides the main application layout structure with responsive
 * behavior and safe area handling for mobile devices.
 */
export function Layout({
  children,
  className,
  showActionBar = true,
  showFAB: _showFAB = true,
  withPadding = true,
  fullBleed = false,
}: LayoutProps) {
  const { responsive, isMenuOpen, multiSelect } = useLayout();
  const { isMobile, isDesktop: _isDesktop } = responsive;

  // Calculate bottom padding based on visible elements
  // ActionBar: 56px + safe-area, bottom nav: 72px
  const bottomPadding = isMobile
    ? showActionBar
      ? 'pb-[calc(72px+56px+env(safe-area-inset-bottom))]'
      : 'pb-[calc(72px+env(safe-area-inset-bottom))]'
    : showActionBar
      ? 'pb-[72px]'
      : 'pb-0';

  return (
    <div
      className={cn(
        'flex min-h-screen flex-col',
        // Prevent body scroll when menu is open
        isMenuOpen && 'overflow-hidden'
      )}
    >
      {/* Main content area */}
      <main
        className={cn(
          'flex-1',
          // Header spacing
          'pt-16', // Header height
          // Bottom spacing for navigation elements
          bottomPadding,
          // Content constraints
          !fullBleed && 'mx-auto w-full',
          !fullBleed && !isMobile && 'max-w-7xl',
          // Padding
          withPadding && 'px-4 sm:px-6 lg:px-8',
          withPadding && 'py-4 sm:py-6',
          // Multi-select mode adjustments
          multiSelect.isActive && 'pb-[calc(56px+72px+env(safe-area-inset-bottom))]',
          className
        )}
      >
        {children}
      </main>
    </div>
  );
}

/**
 * PageHeader - Consistent page header styling
 */
export interface PageHeaderProps {
  /** Page title */
  title: string;
  /** Optional description */
  description?: string;
  /** Optional actions to display on the right */
  actions?: ReactNode;
  /** Additional className */
  className?: string;
}

export function PageHeader({ title, description, actions, className }: PageHeaderProps) {
  return (
    <div
      className={cn(
        'flex flex-col gap-1 sm:flex-row sm:items-center sm:justify-between',
        'mb-6',
        className
      )}
    >
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight sm:text-3xl">{title}</h1>
        {description && (
          <p className="mt-1 text-sm text-muted-foreground sm:text-base">{description}</p>
        )}
      </div>
      {actions && <div className="mt-4 flex items-center gap-2 sm:mt-0">{actions}</div>}
    </div>
  );
}

/**
 * PageContent - Main content area with consistent styling
 */
export interface PageContentProps {
  /** Content */
  children: ReactNode;
  /** Additional className */
  className?: string;
  /** Maximum width constraint */
  maxWidth?: 'sm' | 'md' | 'lg' | 'xl' | '2xl' | 'full';
}

const maxWidthClasses = {
  sm: 'max-w-screen-sm',
  md: 'max-w-screen-md',
  lg: 'max-w-screen-lg',
  xl: 'max-w-screen-xl',
  '2xl': 'max-w-screen-2xl',
  full: 'max-w-full',
};

export function PageContent({ children, className, maxWidth = 'full' }: PageContentProps) {
  return (
    <div
      className={cn(
        'w-full',
        // eslint-disable-next-line security/detect-object-injection -- maxWidth is from typed PageMaxWidth union
        maxWidthClasses[maxWidth],
        maxWidth !== 'full' && 'mx-auto',
        className
      )}
    >
      {children}
    </div>
  );
}

/**
 * EmptyState - Consistent empty state component
 */
export interface EmptyStateProps {
  /** Icon to display */
  icon?: ReactNode;
  /** Title text */
  title: string;
  /** Description text */
  description?: string;
  /** Action button or content */
  action?: ReactNode;
  /** Additional className */
  className?: string;
}

export function EmptyState({ icon, title, description, action, className }: EmptyStateProps) {
  return (
    <div className={cn('flex flex-col items-center justify-center py-12 text-center', className)}>
      {icon && <div className="mb-4 text-muted-foreground">{icon}</div>}
      <h3 className="font-quicksand text-lg font-semibold">{title}</h3>
      {description && <p className="mt-1 max-w-sm text-sm text-muted-foreground">{description}</p>}
      {action && <div className="mt-4">{action}</div>}
    </div>
  );
}

/**
 * LoadingState - Consistent loading indicator
 */
export interface LoadingStateProps {
  /** Loading message */
  message?: string;
  /** Additional className */
  className?: string;
}

export function LoadingState({ message = 'Caricamento...', className }: LoadingStateProps) {
  return (
    <div
      className={cn('flex flex-col items-center justify-center py-12', className)}
      role="status"
      aria-busy="true"
      aria-label={message}
    >
      <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
      <p className="mt-4 text-sm text-muted-foreground">{message}</p>
    </div>
  );
}
