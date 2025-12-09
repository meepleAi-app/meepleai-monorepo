/**
 * StatCard Component - Issue #874
 *
 * Reusable metric display card for admin dashboard.
 * Shows value, label, and optional trend indicator.
 */

import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { ArrowUpIcon, ArrowDownIcon, MinusIcon } from 'lucide-react';

export interface StatCardProps {
  label: string;
  value: string | number;
  trend?: 'up' | 'down' | 'neutral';
  trendValue?: string;
  variant?: 'default' | 'success' | 'warning' | 'danger';
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

export function StatCard({
  label,
  value,
  trend,
  trendValue,
  variant = 'default',
  className,
}: StatCardProps) {
  const TrendIcon = trend === 'up' ? ArrowUpIcon : trend === 'down' ? ArrowDownIcon : MinusIcon;

  return (
    <Card className={cn(variantStyles[variant], className)}>
      <CardContent className="p-6">
        <div className="text-xs font-medium text-gray-500 uppercase tracking-wider mb-2">
          {label}
        </div>
        <div className="text-3xl font-bold text-gray-900">{value}</div>
        {trend && trendValue && (
          <div
            className={cn('flex items-center gap-1 mt-2 text-sm font-medium', trendStyles[trend])}
          >
            <TrendIcon className="h-4 w-4" aria-hidden="true" />
            <span>{trendValue}</span>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
