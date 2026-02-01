/* eslint-disable security/detect-object-injection -- Safe variant styles Record access */
/**
 * StatCard Component - Generic Metric Display Card
 *
 * Extracted from Admin Dashboard for reuse across the application.
 * Shows icon, value, label, trend indicator with loading state.
 *
 * @module components/ui/data-display/stat-card
 * @see Issue #2925 - Component Library extraction
 *
 * Design System:
 * - Hover: translateY(-3px), shadow 0 8px 20px rgba(139, 90, 60, 0.12)
 * - Border hover: #d2691e (MeepleAI orange)
 * - Icon background: #fef3e2
 * - Value font: 'Quicksand' bold
 * - Trend colors: positive #16a34a, negative #dc2626, neutral #999
 *
 * @example
 * ```tsx
 * // Basic usage
 * <StatCard label="Total Users" value="1,247" />
 *
 * // With icon and trend
 * <StatCard
 *   label="Active Sessions"
 *   value="42"
 *   icon={Users}
 *   trend="up"
 *   trendValue="+15% from yesterday"
 *   variant="success"
 * />
 *
 * // Loading state
 * <StatCard label="Loading..." value="" loading />
 * ```
 */

import React from 'react';

import { ArrowUpIcon, ArrowDownIcon, MinusIcon } from 'lucide-react';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

/**
 * Props for the StatCard component
 */
export interface StatCardProps {
  /** Display label for the metric */
  label: string;
  /** Metric value (string or number, will be displayed as-is) */
  value: string | number;
  /** Optional Lucide icon component */
  icon?: LucideIcon;
  /** Trend direction indicator */
  trend?: 'up' | 'down' | 'neutral';
  /** Trend description text (e.g., "+15% from yesterday") */
  trendValue?: string;
  /** Visual variant for semantic meaning */
  variant?: 'default' | 'success' | 'warning' | 'danger';
  /** Show loading skeleton */
  loading?: boolean;
  /** Additional CSS classes */
  className?: string;
}

/**
 * Card background and border styles per variant
 */
const variantStyles: Record<NonNullable<StatCardProps['variant']>, string> = {
  default: 'border-border/50 dark:border-border/30 bg-card/90 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none',
  success: 'border-green-200 bg-green-50/50 dark:bg-green-500/10',
  warning: 'border-yellow-200 bg-yellow-50/50 dark:bg-yellow-500/10',
  danger: 'border-red-200 bg-red-50/50 dark:bg-red-500/10',
};

/**
 * Trend arrow colors
 */
const trendStyles: Record<NonNullable<StatCardProps['trend']>, string> = {
  up: 'text-[#16a34a]',    // positive green
  down: 'text-[#dc2626]',  // negative red
  neutral: 'text-[#999]',  // neutral gray
};

/**
 * Icon container styles per variant
 */
const iconVariantStyles: Record<NonNullable<StatCardProps['variant']>, string> = {
  default: 'text-[#d2691e] bg-[#fef3e2]',
  success: 'text-green-600 bg-green-100',
  warning: 'text-yellow-600 bg-yellow-100',
  danger: 'text-red-600 bg-red-100',
};

/**
 * StatCard - A reusable metric display card component
 *
 * Displays a single metric with optional icon, trend indicator, and loading state.
 * Optimized with React.memo for performance in dashboard grids.
 */
export const StatCard = React.memo(function StatCard({
  label,
  value,
  icon: Icon,
  trend,
  trendValue,
  variant = 'default',
  loading = false,
  className,
}: StatCardProps) {
  const TrendIcon = trend === 'up' ? ArrowUpIcon : trend === 'down' ? ArrowDownIcon : MinusIcon;

  if (loading) {
    return (
      <Card className={cn(variantStyles[variant], className)} data-testid="stat-card-loading">
        <CardContent className="p-8">
          <div className="flex items-start gap-4">
            {Icon && <Skeleton className="h-12 w-12 rounded-xl shrink-0 bg-[#fef3e2]" />}
            <div className="flex-1 space-y-2">
              <Skeleton className="h-3 w-20" />
              <Skeleton className="h-12 w-24" />
              <Skeleton className="h-4 w-28" />
            </div>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card
      className={cn(
        variantStyles[variant],
        'transition-all duration-300 rounded-2xl',
        'shadow-[0_1px_3px_rgba(139,90,60,0.05)]',
        'hover:-translate-y-[3px] hover:shadow-[0_8px_20px_rgba(139,90,60,0.12)] hover:border-[#d2691e]',
        className
      )}
      data-testid="stat-card"
    >
      <CardContent className="p-8">
        <div className="flex items-start gap-4">
          {Icon && (
            <div
              className={cn('p-3 rounded-xl shrink-0', iconVariantStyles[variant])}
              data-testid="stat-card-icon"
            >
              <Icon className="h-6 w-6" aria-hidden="true" />
            </div>
          )}
          <div className="flex-1 min-w-0">
            <div
              className="text-[0.9375rem] font-semibold text-[#666] mb-4"
              data-testid="stat-card-label"
            >
              {label}
            </div>
            <div
              className="font-['Quicksand',sans-serif] text-5xl font-bold text-[#2d2d2d] leading-none mb-3"
              data-testid="stat-card-value"
            >
              {value}
            </div>
            {trend && trendValue && (
              <div
                className={cn(
                  'flex items-center gap-1 text-[0.9375rem] font-bold',
                  trendStyles[trend]
                )}
                data-testid="stat-card-trend"
              >
                <TrendIcon className="h-4 w-4" aria-hidden="true" />
                <span>{trendValue}</span>
              </div>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
});
