/* eslint-disable security/detect-object-injection -- Safe variant styles Record access */
/**
 * StatCard Component - Issue #874, #882, #2245
 *
 * Reusable metric display card for admin dashboard.
 * Shows icon, value, label, trend indicator with loading state.
 *
 * Performance: React.memo optimized (Issue #2245)
 * - Used 6+ times in admin dashboard
 * - Pure presentational component
 * - Props rarely change after initial render
 */

import React from 'react';

import { ArrowUpIcon, ArrowDownIcon, MinusIcon } from 'lucide-react';

import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
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

const variantStyles = {
  default: 'border-gray-200',
  success: 'border-green-200 bg-green-50/50',
  warning: 'border-yellow-200 bg-yellow-50/50',
  danger: 'border-red-200 bg-red-50/50',
};

const trendStyles = {
  up: 'text-green-600',
  down: 'text-red-600',
  neutral: 'text-gray-500',
};

const iconVariantStyles = {
  default: 'text-gray-600 bg-gray-100',
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
        <CardContent className="p-6">
          <div className="flex items-start gap-4">
            {Icon && <Skeleton className="h-10 w-10 rounded-lg shrink-0" />}
            <div className="flex-1 space-y-2">
              <Skeleton className="h-3 w-20" />
              <Skeleton className="h-8 w-24" />
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
        'transition-all duration-200 hover:shadow-md hover:border-gray-300',
        className
      )}
      data-testid="stat-card"
    >
      <CardContent className="p-6">
        <div className="flex items-start gap-4">
          {Icon && (
            <div
              className={cn('p-2 rounded-lg shrink-0', iconVariantStyles[variant])}
              data-testid="stat-card-icon"
            >
              <Icon className="h-6 w-6" aria-hidden="true" />
            </div>
          )}
          <div className="flex-1 min-w-0">
            <div
              className="text-xs font-medium text-gray-500 uppercase tracking-wider mb-2"
              data-testid="stat-card-label"
            >
              {label}
            </div>
            <div className="text-3xl font-bold text-gray-900" data-testid="stat-card-value">
              {value}
            </div>
            {trend && trendValue && (
              <div
                className={cn(
                  'flex items-center gap-1 mt-2 text-sm font-medium',
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