/**
 * CollectionStatsBar - Hero Stats Component
 * Issue #3649 - User Collection Dashboard Enhancement
 *
 * Compact hero stats bar for the collection section displaying:
 * - Total Games count
 * - Private PDFs count
 * - Total Sessions count
 *
 * @example
 * ```tsx
 * <CollectionStatsBar stats={heroStats} isLoading={false} />
 * ```
 */

'use client';

import { motion } from 'framer-motion';
import { Gamepad2, Lock, History } from 'lucide-react';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { cn } from '@/lib/utils';
import type { CollectionHeroStats } from '@/types/collection';

import type { LucideIcon } from 'lucide-react';

// ============================================================================
// Types
// ============================================================================

export interface CollectionStatsBarProps {
  stats?: CollectionHeroStats | null;
  isLoading?: boolean;
  className?: string;
}

interface StatItemProps {
  icon: LucideIcon;
  label: string;
  value: number;
  iconColor: string;
  bgColor: string;
  index: number;
}

// ============================================================================
// StatItem Sub-Component
// ============================================================================

function StatItem({
  icon: Icon,
  label,
  value,
  iconColor,
  bgColor,
  index,
}: StatItemProps) {
  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ delay: index * 0.05, duration: 0.25 }}
      className="flex items-center gap-2 px-3 py-2 rounded-lg bg-muted/50 hover:bg-muted/80 transition-colors"
      data-testid={`stat-item-${label.toLowerCase().replace(/\s+/g, '-')}`}
    >
      <div className={cn('h-8 w-8 rounded-md flex items-center justify-center', bgColor)}>
        <Icon className={cn('h-4 w-4', iconColor)} />
      </div>
      <div className="min-w-0">
        <p className="text-[10px] uppercase tracking-wide text-muted-foreground font-medium">
          {label}
        </p>
        <p
          className="text-lg font-bold tracking-tight leading-none"
          data-testid={`stat-value-${label.toLowerCase().replace(/\s+/g, '-')}`}
        >
          {value}
        </p>
      </div>
    </motion.div>
  );
}

// ============================================================================
// Skeleton Component
// ============================================================================

function CollectionStatsBarSkeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn('flex flex-wrap gap-3', className)}
      data-testid="collection-stats-bar-skeleton"
    >
      {Array.from({ length: 3 }).map((_, i) => (
        <div
          key={i}
          className="flex items-center gap-2 px-3 py-2 rounded-lg bg-muted/50"
        >
          <Skeleton className="h-8 w-8 rounded-md" />
          <div className="space-y-1">
            <Skeleton className="h-2 w-14" />
            <Skeleton className="h-5 w-8" />
          </div>
        </div>
      ))}
    </div>
  );
}

// ============================================================================
// CollectionStatsBar Component
// ============================================================================

export function CollectionStatsBar({
  stats,
  isLoading = false,
  className,
}: CollectionStatsBarProps) {
  if (isLoading || !stats) {
    return <CollectionStatsBarSkeleton className={className} />;
  }

  const statItems = [
    {
      icon: Gamepad2,
      label: 'Giochi',
      value: stats.totalGames,
      iconColor: 'text-amber-600 dark:text-amber-400',
      bgColor: 'bg-amber-500/20',
    },
    {
      icon: Lock,
      label: 'PDF Privati',
      value: stats.privatePdfsCount,
      iconColor: 'text-purple-600 dark:text-purple-400',
      bgColor: 'bg-purple-500/20',
    },
    {
      icon: History,
      label: 'Sessioni',
      value: stats.totalSessions,
      iconColor: 'text-emerald-600 dark:text-emerald-400',
      bgColor: 'bg-emerald-500/20',
    },
  ];

  return (
    <section
      className={cn('flex flex-wrap gap-3', className)}
      data-testid="collection-stats-bar"
      aria-label="Statistiche collezione"
    >
      {statItems.map((item, index) => (
        <StatItem
          key={item.label}
          icon={item.icon}
          label={item.label}
          value={item.value}
          iconColor={item.iconColor}
          bgColor={item.bgColor}
          index={index}
        />
      ))}
    </section>
  );
}

export default CollectionStatsBar;
