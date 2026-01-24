/* eslint-disable security/detect-object-injection -- Safe variant styles Record access */
/**
 * StatCard Component - Issue #874, #882, #2245, #2850
 *
 * Reusable metric display card for admin dashboard.
 * Shows icon, value, label, trend indicator with loading state.
 *
 * MeepleAI Design System (Issue #2850):
 * - Hover: translateY(-3px), shadow 0 8px 20px rgba(139, 90, 60, 0.12)
 * - Border hover: #d2691e
 * - Icon background: #fef3e2
 * - Value font: 'Quicksand' bold
 * - Trend colors: positive #16a34a, negative #dc2626, neutral #999
 *
 * Performance: React.memo optimized (Issue #2245)
 * - Used 6+ times in admin dashboard
 * - Pure presentational component
 * - Props rarely change after initial render
 */

import React from 'react';

import { ArrowUpIcon, ArrowDownIcon, MinusIcon } from 'lucide-react';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

export interface StatCardProps {
  label: string;
  value: string | number;
  icon?: LucideIcon;
  trend?: 'up' | 'down' | 'neutral';
  trendValue?: string;
  variant?: 'default' | 'success' | 'warning' | 'danger';
  loading?: boolean;
  className?: string;
}

// Issue #2850: MeepleAI Design System variant styles
const variantStyles = {
  default: 'border-border/50 dark:border-border/30 bg-card/90 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none',
  success: 'border-green-200 bg-green-50/50 dark:bg-green-500/10',
  warning: 'border-yellow-200 bg-yellow-50/50 dark:bg-yellow-500/10',
  danger: 'border-red-200 bg-red-50/50 dark:bg-red-500/10',
};

// Issue #2850: MeepleAI trend colors
const trendStyles = {
  up: 'text-[#16a34a]',    // positive
  down: 'text-[#dc2626]',  // negative
  neutral: 'text-[#999]',  // neutral
};

// Issue #2850: MeepleAI icon background #fef3e2
const iconVariantStyles = {
  default: 'text-[#d2691e] bg-[#fef3e2]',
  success: 'text-green-600 bg-green-100',
  warning: 'text-yellow-600 bg-yellow-100',
  danger: 'text-red-600 bg-red-100',
};

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
      <Card className={cn(variantStyles[variant], className)} data-testid="statcard-loading">
        {/* Issue #2850: Padding 2rem (p-8) */}
        <CardContent className="p-8">
          <div className="flex items-start gap-4">
            {/* Issue #2850: Icon 48x48px, rounded-xl */}
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
        // Issue #2850: MeepleAI hover effects
        // translateY(-3px), shadow 0 8px 20px rgba(139, 90, 60, 0.12), border #d2691e
        // Note: No cursor-pointer as card is not interactive
        'transition-all duration-300 rounded-2xl',
        'shadow-[0_1px_3px_rgba(139,90,60,0.05)]',
        'hover:-translate-y-[3px] hover:shadow-[0_8px_20px_rgba(139,90,60,0.12)] hover:border-[#d2691e]',
        className
      )}
      data-testid="stat-card"
    >
      {/* Issue #2850: Padding 2rem (p-8) */}
      <CardContent className="p-8">
        <div className="flex items-start gap-4">
          {Icon && (
            <div
              // Issue #2850: Icon 48x48px, rounded-xl, bg #fef3e2
              className={cn('p-3 rounded-xl shrink-0', iconVariantStyles[variant])}
              data-testid="stat-card-icon"
            >
              <Icon className="h-6 w-6" aria-hidden="true" />
            </div>
          )}
          <div className="flex-1 min-w-0">
            <div
              // Issue #2850: Label styling
              className="text-[0.9375rem] font-semibold text-[#666] mb-4"
              data-testid="stat-card-label"
            >
              {label}
            </div>
            {/* Issue #2850: Value font Quicksand bold, 3rem, #2d2d2d */}
            <div
              className="font-['Quicksand',sans-serif] text-5xl font-bold text-[#2d2d2d] leading-none mb-3"
              data-testid="stat-card-value"
            >
              {value}
            </div>
            {trend && trendValue && (
              <div
                // Issue #2850: Trend font-weight 700
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