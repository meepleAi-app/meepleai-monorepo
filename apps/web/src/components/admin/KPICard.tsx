/**
 * KPICard Component - Issue #2785
 *
 * Individual KPI card for admin dashboard metrics display.
 * Features:
 * - Icon with colored background
 * - Large value display (font-quicksand)
 * - Optional trend indicator with percentage
 * - Optional badge (warning/success/error variants)
 * - Optional subtitle
 * - Hover effects with corner decoration
 * - Dark mode compatible
 *
 * Part of Epic #2783 - Admin Dashboard Redesign
 */

'use client';

import type { ReactNode } from 'react';

import { TrendingUp, TrendingDown } from 'lucide-react';

import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type BadgeVariant = 'warning' | 'success' | 'error';

export interface KPICardData {
  /** Card title (e.g., "Utenti Totali") */
  title: string;
  /** Main value to display */
  value: string | number;
  /** Trend percentage (positive = up, negative = down) */
  trend?: number;
  /** Label for trend (e.g., "vs mese scorso") */
  trendLabel?: string;
  /** Icon to display in colored box */
  icon: ReactNode;
  /** Optional badge text (e.g., "12 in attesa") */
  badge?: string;
  /** Badge color variant */
  badgeVariant?: BadgeVariant;
  /** Optional subtitle below value */
  subtitle?: string;
}

export interface KPICardProps extends KPICardData {
  /** Additional CSS classes */
  className?: string;
  /** Test ID for testing */
  'data-testid'?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Get badge CSS classes based on variant
 */
function getBadgeClasses(variant?: BadgeVariant): string {
  switch (variant) {
    case 'warning':
      return 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400';
    case 'error':
      return 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400';
    case 'success':
    default:
      return 'bg-green-100 text-green-700 dark:bg-green-500/20 dark:text-green-400';
  }
}

// ============================================================================
// Component
// ============================================================================

export function KPICard({
  title,
  value,
  trend,
  trendLabel,
  icon,
  badge,
  badgeVariant,
  subtitle,
  className,
  'data-testid': testId,
}: KPICardProps) {
  const hasTrend = trend !== undefined && trend !== null;
  const isPositiveTrend = hasTrend && trend > 0;
  const isNegativeTrend = hasTrend && trend < 0;

  return (
    <div
      className={cn(
        'group relative overflow-hidden rounded-xl border border-meeple-border bg-white p-5',
        'hover-card hover-shadow-meeple',
        'dark:border-stone-800 dark:bg-stone-900',
        className
      )}
      data-testid={testId}
      style={{
        boxShadow: '0 1px 3px rgba(139, 90, 60, 0.05)',
      }}
    >
      {/* Decorative corner - scales on hover */}
      <div
        className="absolute -right-6 -top-6 h-16 w-16 rounded-full bg-gradient-to-br from-orange-500/10 to-amber-500/10 transition-transform duration-300 group-hover:scale-150"
        aria-hidden="true"
      />

      <div className="relative">
        {/* Header: Icon + Badge */}
        <div className="mb-3 flex items-center justify-between">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-orange-100 text-orange-600 dark:bg-orange-500/20 dark:text-orange-400">
            {icon}
          </div>
          {badge && (
            <span
              className={cn(
                'rounded-full px-2 py-1 text-xs font-medium',
                getBadgeClasses(badgeVariant)
              )}
              data-testid={testId ? `${testId}-badge` : undefined}
            >
              {badge}
            </span>
          )}
        </div>

        {/* Title */}
        <p className="text-sm font-medium text-stone-500 dark:text-stone-400">
          {title}
        </p>

        {/* Value */}
        <p
          className="mt-1 font-quicksand text-3xl font-bold text-stone-900 dark:text-white"
          data-testid={testId ? `${testId}-value` : undefined}
        >
          {value}
        </p>

        {/* Trend Indicator */}
        {hasTrend && (
          <div className="mt-2 flex items-center gap-1 text-sm">
            {isPositiveTrend ? (
              <TrendingUp className="h-4 w-4 text-green-500" aria-hidden="true" />
            ) : isNegativeTrend ? (
              <TrendingDown className="h-4 w-4 text-red-500" aria-hidden="true" />
            ) : null}
            <span
              className={cn(
                isPositiveTrend && 'text-green-600 dark:text-green-400',
                isNegativeTrend && 'text-red-600 dark:text-red-400',
                !isPositiveTrend && !isNegativeTrend && 'text-stone-500'
              )}
              data-testid={testId ? `${testId}-trend` : undefined}
            >
              {isPositiveTrend ? '+' : ''}
              {trend}%
            </span>
            {trendLabel && (
              <span className="text-stone-400 dark:text-stone-500">
                {trendLabel}
              </span>
            )}
          </div>
        )}

        {/* Subtitle */}
        {subtitle && (
          <p className="mt-2 text-sm text-stone-400 dark:text-stone-500">
            {subtitle}
          </p>
        )}
      </div>
    </div>
  );
}

export default KPICard;
