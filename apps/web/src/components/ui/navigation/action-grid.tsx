/* eslint-disable security/detect-object-injection -- Safe action config Record access */
/**
 * ActionGrid Component - Generic Quick Actions Grid
 *
 * Extracted from Admin QuickActions for reuse across the application.
 * Displays a responsive grid of action buttons/links.
 *
 * @module components/ui/navigation/action-grid
 * @see Issue #2925 - Component Library extraction
 *
 * Features:
 * - Responsive grid (3 cols desktop, 2 tablet, 1 mobile)
 * - Icon + label + optional description
 * - Variant-based or gradient-based styling
 * - Hover effects with icon scaling
 * - Loading skeleton state
 * - Optional badge for notifications
 *
 * @example
 * ```tsx
 * // Basic usage
 * <ActionGrid actions={[
 *   { id: 'add', label: 'Add Item', href: '/add', icon: PlusIcon },
 *   { id: 'settings', label: 'Settings', href: '/settings', icon: SettingsIcon },
 * ]} />
 *
 * // With gradients
 * <ActionGrid actions={[
 *   { id: 'add', label: 'Add', href: '/add', icon: PlusIcon, gradient: 'green-emerald' },
 * ]} />
 * ```
 */

import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

/**
 * Available gradient presets
 */
export type GradientPreset =
  | 'green-emerald'
  | 'blue-indigo'
  | 'amber-orange'
  | 'red-rose'
  | 'purple-violet'
  | 'stone-stone';

/**
 * Action item configuration
 */
export interface ActionItem {
  /** Unique identifier */
  id: string;
  /** Display label */
  label: string;
  /** Optional description text */
  description?: string;
  /** Navigation URL */
  href: string;
  /** Lucide icon component */
  icon: LucideIcon;
  /** Optional notification badge */
  badge?: number | string;
  /** Color variant (used when no gradient) */
  variant?: 'default' | 'primary' | 'warning' | 'danger';
  /** Gradient preset (overrides variant) */
  gradient?: GradientPreset;
}

/**
 * Props for the ActionGrid component
 */
export interface ActionGridProps {
  /** Array of action items to display */
  actions: ActionItem[];
  /** Show loading skeleton */
  loading?: boolean;
  /** Additional CSS classes */
  className?: string;
  /** Card title (optional, no card wrapper if not provided) */
  title?: string;
  /** Icon for the title */
  titleIcon?: LucideIcon;
  /** Dynamic badge counts keyed by action id */
  badges?: Record<string, number>;
  /** Number of columns on large screens (default: 3) */
  columns?: 2 | 3 | 4;
  /** Show as card with header (default: true if title provided) */
  showCard?: boolean;
}

/**
 * Variant styling for action items
 */
const variantStyles: Record<string, string> = {
  default: 'hover:bg-muted/50 hover:border-border',
  primary: 'hover:bg-blue-50 hover:border-blue-300 dark:hover:bg-blue-500/10',
  warning: 'hover:bg-yellow-50 hover:border-yellow-300 dark:hover:bg-yellow-500/10',
  danger: 'hover:bg-red-50 hover:border-red-300 dark:hover:bg-red-500/10',
};

/**
 * Icon container styling per variant
 */
const iconVariantStyles: Record<string, string> = {
  default: 'text-muted-foreground bg-muted',
  primary: 'text-blue-600 bg-blue-100 dark:text-blue-400 dark:bg-blue-500/20',
  warning: 'text-yellow-600 bg-yellow-100 dark:text-yellow-400 dark:bg-yellow-500/20',
  danger: 'text-red-600 bg-red-100 dark:text-red-400 dark:bg-red-500/20',
};

/**
 * Badge styling per variant
 */
const badgeVariantStyles: Record<string, string> = {
  default: 'bg-muted-foreground',
  primary: 'bg-blue-500',
  warning: 'bg-yellow-500',
  danger: 'bg-red-500',
};

/**
 * Gradient styling presets
 */
const gradientStyles: Record<GradientPreset, string> = {
  'green-emerald': 'bg-gradient-to-br from-green-500 to-emerald-600 text-white',
  'blue-indigo': 'bg-gradient-to-br from-blue-500 to-indigo-600 text-white',
  'amber-orange': 'bg-gradient-to-br from-amber-500 to-orange-600 text-white',
  'red-rose': 'bg-gradient-to-br from-red-500 to-rose-600 text-white',
  'purple-violet': 'bg-gradient-to-br from-purple-500 to-violet-600 text-white',
  'stone-stone': 'bg-gradient-to-br from-stone-500 to-stone-600 text-white',
};

/**
 * Column configuration classes
 */
const columnClasses: Record<number, string> = {
  2: 'grid-cols-1 sm:grid-cols-2',
  3: 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-3',
  4: 'grid-cols-1 sm:grid-cols-2 lg:grid-cols-4',
};

/**
 * Loading skeleton for the grid
 */
function ActionGridSkeleton({ columns = 3, className }: { columns?: number; className?: string }) {
  return (
    <div
      className={cn('grid gap-3', columnClasses[columns], className)}
      data-testid="action-grid-skeleton"
    >
      {Array.from({ length: 6 }).map((_, index) => (
        <div
          key={`skeleton-${index}`}
          className="flex items-center gap-3 p-3 border rounded-lg"
        >
          <Skeleton className="h-10 w-10 rounded-lg shrink-0" />
          <div className="flex-1 space-y-1">
            <Skeleton className="h-4 w-24" />
            <Skeleton className="h-3 w-32" />
          </div>
        </div>
      ))}
    </div>
  );
}

/**
 * Action grid content (without card wrapper)
 */
function ActionGridContent({
  actions,
  badges,
  columns = 3,
  className,
}: Pick<ActionGridProps, 'actions' | 'badges' | 'columns' | 'className'>) {
  return (
    <div
      className={cn('grid gap-3', columnClasses[columns], className)}
      data-testid="action-grid"
    >
      {actions.map(action => {
        const Icon = action.icon;
        const variant = action.variant || 'default';
        const badgeCount = badges?.[action.id] ?? action.badge;

        // Use gradient styling if specified
        const gradient = action.gradient;
        const linkHoverClass = gradient
          ? 'hover:border-orange-500 hover:shadow-md'
          : variantStyles[variant];
        const iconClass = gradient
          ? gradientStyles[gradient]
          : iconVariantStyles[variant];

        return (
          <Link
            key={action.id}
            href={action.href}
            className={cn(
              'group flex items-center gap-3 p-3 border rounded-lg transition-all duration-200',
              linkHoverClass
            )}
            data-testid={`action-${action.id}`}
          >
            <div
              className={cn(
                'p-2 rounded-lg shrink-0 transition-transform duration-200',
                gradient && 'group-hover:scale-110',
                iconClass
              )}
              aria-hidden="true"
            >
              <Icon className="h-5 w-5" />
            </div>
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2">
                <span className="text-sm font-medium text-foreground">{action.label}</span>
                {badgeCount !== undefined && (
                  <Badge
                    className={cn(
                      'text-white text-xs px-1.5 py-0.5',
                      gradient ? 'bg-orange-500' : badgeVariantStyles[variant]
                    )}
                  >
                    {typeof badgeCount === 'number' && badgeCount > 99 ? '99+' : badgeCount}
                  </Badge>
                )}
              </div>
              {action.description && (
                <p className="text-xs text-muted-foreground truncate">{action.description}</p>
              )}
            </div>
          </Link>
        );
      })}
    </div>
  );
}

/**
 * ActionGrid - A reusable quick actions grid component
 *
 * Displays a responsive grid of action links with icons, optional badges,
 * and gradient or variant-based styling.
 */
export function ActionGrid({
  actions,
  loading = false,
  className,
  title,
  titleIcon: TitleIcon,
  badges,
  columns = 3,
  showCard,
}: ActionGridProps) {
  // Show card wrapper if title is provided or showCard is explicitly true
  const shouldShowCard = showCard ?? !!title;

  if (loading) {
    if (shouldShowCard) {
      return (
        <Card className={className}>
          <CardHeader className="pb-2">
            <Skeleton className="h-6 w-32" />
          </CardHeader>
          <CardContent>
            <ActionGridSkeleton columns={columns} />
          </CardContent>
        </Card>
      );
    }
    return <ActionGridSkeleton columns={columns} className={className} />;
  }

  if (shouldShowCard) {
    return (
      <Card className={className}>
        {title && (
          <CardHeader className="pb-2">
            <CardTitle className="text-lg font-semibold flex items-center gap-2">
              {TitleIcon && <TitleIcon className="h-5 w-5 text-yellow-500" aria-hidden="true" />}
              <span data-testid="action-grid-title">{title}</span>
            </CardTitle>
          </CardHeader>
        )}
        <CardContent>
          <ActionGridContent actions={actions} badges={badges} columns={columns} />
        </CardContent>
      </Card>
    );
  }

  return <ActionGridContent actions={actions} badges={badges} columns={columns} className={className} />;
}
