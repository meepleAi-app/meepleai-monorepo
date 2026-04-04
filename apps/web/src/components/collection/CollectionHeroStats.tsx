'use client';

import { motion } from 'framer-motion';
import { TrendingUp, type LucideIcon } from 'lucide-react';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { COLLECTION_TEST_IDS } from '@/lib/test-ids';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface HeroStat {
  id: string;
  icon: LucideIcon;
  value: number;
  label: string;
  trend?: number;
  color: 'amber' | 'emerald' | 'teal' | 'purple';
}

// ============================================================================
// Constants
// ============================================================================

const COLOR_MAP = {
  amber: {
    bg: 'bg-amber-500/15 dark:bg-amber-500/20',
    icon: 'text-amber-600 dark:text-amber-400',
    border: 'border-amber-500/30',
    glow: 'shadow-amber-500/20',
  },
  emerald: {
    bg: 'bg-emerald-500/15 dark:bg-emerald-500/20',
    icon: 'text-emerald-600 dark:text-emerald-400',
    border: 'border-emerald-500/30',
    glow: 'shadow-emerald-500/20',
  },
  teal: {
    bg: 'bg-teal-500/15 dark:bg-teal-500/20',
    icon: 'text-teal-600 dark:text-teal-400',
    border: 'border-teal-500/30',
    glow: 'shadow-teal-500/20',
  },
  purple: {
    bg: 'bg-purple-500/15 dark:bg-purple-500/20',
    icon: 'text-purple-600 dark:text-purple-400',
    border: 'border-purple-500/30',
    glow: 'shadow-purple-500/20',
  },
};

// ============================================================================
// Sub-Components
// ============================================================================

function HeroStatCard({ stat, index }: { stat: HeroStat; index: number }) {
  const colors = COLOR_MAP[stat.color];
  const Icon = stat.icon;

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: index * 0.05 }}
      className={cn(
        'relative flex flex-col gap-1 p-4 rounded-2xl',
        'border backdrop-blur-sm',
        'transition-all duration-300',
        'hover:scale-[1.02] hover:shadow-lg',
        colors.bg,
        colors.border
      )}
      data-testid={COLLECTION_TEST_IDS.heroStat(stat.id)}
    >
      <div className="flex items-center justify-between">
        <Icon className={cn('h-5 w-5', colors.icon)} />
        {stat.trend !== undefined && stat.trend > 0 && (
          <span className="flex items-center gap-0.5 text-xs text-emerald-600 dark:text-emerald-400">
            <TrendingUp className="h-3 w-3" />+{stat.trend}
          </span>
        )}
      </div>
      <div className="mt-1">
        <span className="font-heading text-2xl font-bold tracking-tight">{stat.value}</span>
      </div>
      <span className="text-xs text-muted-foreground">{stat.label}</span>
    </motion.div>
  );
}

function HeroStatSkeleton() {
  return (
    <div className="flex flex-col gap-2 p-4 rounded-2xl border bg-muted/20">
      <Skeleton className="h-5 w-5 rounded" />
      <Skeleton className="h-8 w-16 mt-1" />
      <Skeleton className="h-3 w-12" />
    </div>
  );
}

// ============================================================================
// Exported Component
// ============================================================================

interface CollectionHeroStatsProps {
  heroStats: HeroStat[];
  isLoading: boolean;
}

export function CollectionHeroStats({ heroStats, isLoading }: CollectionHeroStatsProps) {
  return (
    <section aria-label="Collection statistics">
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        {isLoading
          ? Array.from({ length: 4 }).map((_, i) => <HeroStatSkeleton key={i} />)
          : heroStats.map((stat, index) => (
              <HeroStatCard key={stat.id} stat={stat} index={index} />
            ))}
      </div>
    </section>
  );
}
