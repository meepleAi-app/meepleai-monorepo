/**
 * NavItems - Navigation links with active state
 * Issue #3288 - Phase 2: Navbar Components
 *
 * Features:
 * - Navigation items with icons and labels
 * - Active state detection via pathname
 * - Keyboard navigation support
 * - WCAG 2.1 AA accessible
 */

'use client';

import { type LucideIcon } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';

/**
 * Navigation item definition
 */
export interface NavItem {
  /** URL path */
  href: string;
  /** Display label */
  label: string;
  /** Lucide icon component */
  icon: LucideIcon;
  /** Accessible label */
  ariaLabel?: string;
  /** Whether item is admin-only */
  adminOnly?: boolean;
  /** Test ID for testing */
  testId?: string;
}

export interface NavItemsProps {
  /** Navigation items to display */
  items: NavItem[];
  /** Layout direction */
  direction?: 'horizontal' | 'vertical';
  /** Whether to show icons */
  showIcons?: boolean;
  /** Whether to show labels */
  showLabels?: boolean;
  /** Additional className */
  className?: string;
  /** Item className override */
  itemClassName?: string;
  /** Callback when item is clicked */
  onItemClick?: (item: NavItem) => void;
}

/**
 * Check if a route is active
 */
function isRouteActive(href: string, pathname: string | null): boolean {
  if (!pathname) return false;

  if (href === '/') {
    return pathname === '/';
  }
  if (href === '/dashboard') {
    return pathname === '/dashboard';
  }
  if (href === '/profile') {
    return pathname === '/profile' || pathname.startsWith('/profile/');
  }
  return pathname.startsWith(href);
}

/**
 * NavItems Component
 *
 * Renders a list of navigation links with active state highlighting.
 * Supports both horizontal (desktop navbar) and vertical (mobile menu) layouts.
 */
export function NavItems({
  items,
  direction = 'horizontal',
  showIcons = true,
  showLabels = true,
  className,
  itemClassName,
  onItemClick,
}: NavItemsProps) {
  const pathname = usePathname();

  return (
    <nav
      className={cn(
        'flex',
        direction === 'horizontal' ? 'flex-row items-center gap-1' : 'flex-col gap-1',
        className
      )}
      aria-label="Main navigation"
    >
      {items.map((item) => {
        const { href, label, icon: Icon, ariaLabel, testId } = item;
        const isActive = isRouteActive(href, pathname);

        return (
          <Link
            key={href}
            href={href}
            aria-label={ariaLabel || label}
            aria-current={isActive ? 'page' : undefined}
            data-testid={testId}
            onClick={() => onItemClick?.(item)}
            className={cn(
              // Base styles
              'flex items-center gap-2',
              direction === 'horizontal' ? 'px-3 py-2' : 'px-4 py-3',
              'rounded-lg text-sm font-medium',
              // Transitions
              'transition-colors duration-200',
              // Focus state
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2',
              // Active vs inactive states
              isActive
                ? [
                    'bg-[hsl(262_83%_62%/0.1)] dark:bg-[hsl(262_83%_62%/0.2)]',
                    'text-[hsl(262_83%_62%)] font-semibold',
                  ]
                : [
                    'text-muted-foreground',
                    'hover:text-primary hover:bg-muted dark:hover:bg-muted/70',
                  ],
              itemClassName
            )}
          >
            {showIcons && (
              <Icon
                className={cn(
                  'h-5 w-5 flex-shrink-0',
                  direction === 'vertical' && 'h-5 w-5'
                )}
                aria-hidden="true"
              />
            )}
            {showLabels && <span>{label}</span>}
          </Link>
        );
      })}
    </nav>
  );
}

/**
 * Single navigation item (for use outside NavItems list)
 */
export interface NavItemButtonProps {
  item: NavItem;
  showIcon?: boolean;
  showLabel?: boolean;
  className?: string;
  onClick?: () => void;
}

export function NavItemButton({
  item,
  showIcon = true,
  showLabel = true,
  className,
  onClick,
}: NavItemButtonProps) {
  const pathname = usePathname();
  const { href, label, icon: Icon, ariaLabel, testId } = item;
  const isActive = isRouteActive(href, pathname);

  return (
    <Link
      href={href}
      aria-label={ariaLabel || label}
      aria-current={isActive ? 'page' : undefined}
      data-testid={testId}
      onClick={onClick}
      className={cn(
        'flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium',
        'transition-colors duration-200',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2',
        isActive
          ? 'bg-[hsl(262_83%_62%/0.1)] text-[hsl(262_83%_62%)] font-semibold'
          : 'text-muted-foreground hover:text-primary hover:bg-muted',
        className
      )}
    >
      {showIcon && <Icon className="h-5 w-5" aria-hidden="true" />}
      {showLabel && <span>{label}</span>}
    </Link>
  );
}
