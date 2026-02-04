/**
 * KpiCard - Glassmorphic KPI Display Card
 * Issue #3308 - Implement HeroStats component with 4 KPI cards
 *
 * Features:
 * - Glassmorphic styling with backdrop blur
 * - Icon, value, label, and trend indicator
 * - Click-through navigation
 * - Skeleton loading state
 * - Responsive sizing
 *
 * @example
 * ```tsx
 * <KpiCard
 *   icon={Library}
 *   iconColor="amber"
 *   value={127}
 *   label="Collezione"
 *   trend={{ value: 3, period: "mese" }}
 *   href="/library"
 * />
 * ```
 */

'use client';

import { motion } from 'framer-motion';
import { TrendingUp, TrendingDown, Flame, type LucideIcon } from 'lucide-react';
import Link from 'next/link';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type KpiColorVariant = 'amber' | 'emerald' | 'blue' | 'purple';

export interface KpiTrend {
  /** Trend value (positive = up, negative = down) */
  value: number;
  /** Time period for the trend (e.g., "mese", "7gg") */
  period: string;
}

export interface KpiStreak {
  /** Current streak count */
  count: number;
  /** Whether streak is currently active */
  isActive: boolean;
}

export interface KpiCardProps {
  /** Lucide icon component */
  icon: LucideIcon;
  /** Color variant for icon background */
  iconColor: KpiColorVariant;
  /** Main numeric value to display */
  value: number;
  /** Label text below the value */
  label: string;
  /** Optional trend indicator */
  trend?: KpiTrend;
  /** Optional streak indicator (for played games) */
  streak?: KpiStreak;
  /** Navigation href when card is clicked */
  href: string;
  /** Loading state */
  isLoading?: boolean;
  /** Additional CSS classes */
  className?: string;
  /** Test ID for testing */
  testId?: string;
}

// ============================================================================
// Color Mappings
// ============================================================================

const colorVariants: Record<KpiColorVariant, {
  iconBg: string;
  iconText: string;
  gradient: string;
  hoverBorder: string;
}> = {
  amber: {
    iconBg: 'bg-amber-500/20',
    iconText: 'text-amber-600 dark:text-amber-400',
    gradient: 'from-amber-500/10 to-amber-600/5',
    hoverBorder: 'hover:border-amber-500/30',
  },
  emerald: {
    iconBg: 'bg-emerald-500/20',
    iconText: 'text-emerald-600 dark:text-emerald-400',
    gradient: 'from-emerald-500/10 to-emerald-600/5',
    hoverBorder: 'hover:border-emerald-500/30',
  },
  blue: {
    iconBg: 'bg-blue-500/20',
    iconText: 'text-blue-600 dark:text-blue-400',
    gradient: 'from-blue-500/10 to-blue-600/5',
    hoverBorder: 'hover:border-blue-500/30',
  },
  purple: {
    iconBg: 'bg-purple-500/20',
    iconText: 'text-purple-600 dark:text-purple-400',
    gradient: 'from-purple-500/10 to-purple-600/5',
    hoverBorder: 'hover:border-purple-500/30',
  },
};

// ============================================================================
// Skeleton Component
// ============================================================================

export function KpiCardSkeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 p-4 backdrop-blur-xl',
        className
      )}
      data-testid="kpi-card-skeleton"
    >
      <div className="flex items-start gap-3">
        <Skeleton className="h-10 w-10 rounded-xl" />
        <div className="flex-1 space-y-2">
          <Skeleton className="h-8 w-16" />
          <Skeleton className="h-4 w-20" />
        </div>
      </div>
      <div className="mt-3">
        <Skeleton className="h-4 w-24" />
      </div>
    </div>
  );
}

// ============================================================================
// KpiCard Component
// ============================================================================

export function KpiCard({
  icon: Icon,
  iconColor,
  value,
  label,
  trend,
  streak,
  href,
  isLoading = false,
  className,
  testId,
}: KpiCardProps) {
   
  const colors = colorVariants[iconColor];

  if (isLoading) {
    return <KpiCardSkeleton className={className} />;
  }

  const formattedValue = new Intl.NumberFormat('it-IT').format(value);

  return (
    <Link href={href} className="block">
      <motion.div
        whileHover={{ y: -4, scale: 1.02 }}
        whileTap={{ scale: 0.98 }}
        transition={{ type: 'spring', stiffness: 400, damping: 25 }}
        className={cn(
          // Base glassmorphic card
          'relative overflow-hidden rounded-2xl border border-border/60',
          'bg-gradient-to-br backdrop-blur-xl',
          colors.gradient,
          'bg-card/80 p-4',
          // Hover effects
          'cursor-pointer transition-colors duration-200',
          colors.hoverBorder,
          'hover:shadow-lg hover:shadow-primary/5',
          className
        )}
        data-testid={testId ?? 'kpi-card'}
      >
        {/* Content */}
        <div className="flex items-start gap-3">
          {/* Icon */}
          <div
            className={cn(
              'flex h-10 w-10 shrink-0 items-center justify-center rounded-xl',
              colors.iconBg
            )}
          >
            <Icon className={cn('h-5 w-5', colors.iconText)} />
          </div>

          {/* Value and Label */}
          <div className="min-w-0 flex-1">
            <p
              className="font-playfair text-2xl font-bold tracking-tight"
              data-testid="kpi-value"
            >
              {formattedValue}
            </p>
            <p className="text-sm text-muted-foreground" data-testid="kpi-label">
              {label}
            </p>
          </div>
        </div>

        {/* Trend / Streak Indicator */}
        <div className="mt-3 flex items-center gap-2">
          {trend && <TrendIndicator trend={trend} />}
          {streak && streak.isActive && <StreakIndicator count={streak.count} />}
        </div>
      </motion.div>
    </Link>
  );
}

// ============================================================================
// Sub-Components
// ============================================================================

function TrendIndicator({ trend }: { trend: KpiTrend }) {
  const isPositive = trend.value >= 0;
  const TrendIcon = isPositive ? TrendingUp : TrendingDown;

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 text-xs font-medium',
        isPositive ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400'
      )}
      data-testid="kpi-trend"
    >
      <TrendIcon className="h-3 w-3" />
      {isPositive && '+'}
      {trend.value} {trend.period}
    </span>
  );
}

function StreakIndicator({ count }: { count: number }) {
  return (
    <span
      className="inline-flex items-center gap-1 text-xs font-medium text-orange-600 dark:text-orange-400"
      data-testid="kpi-streak"
    >
      <Flame className="h-3 w-3" />
      {count}d
    </span>
  );
}

export default KpiCard;
