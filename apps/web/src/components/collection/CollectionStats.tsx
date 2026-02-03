/**
 * CollectionStats Component - Issue #3476
 *
 * Hero stats section displaying collection metrics
 *
 * Features:
 * - Total Games count
 * - Private PDFs count
 * - Active Chats count
 * - Total Reading Time
 * - Animated stat cards
 * - Loading skeleton state
 *
 * @example
 * ```tsx
 * <CollectionStats stats={statsData} />
 * ```
 */

'use client';

import { motion } from 'framer-motion';
import {
  Library,
  FileText,
  MessageSquare,
  Clock,
} from 'lucide-react';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { cn } from '@/lib/utils';
import type { CollectionStatsProps, StatCard as StatCardType } from '@/types/collection';

import type { LucideIcon } from 'lucide-react';

// ============================================================================
// Helper Functions
// ============================================================================

function formatReadingTime(minutes: number): string {
  if (minutes < 60) return `${minutes}min`;
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  return mins > 0 ? `${hours}h ${mins}min` : `${hours}h`;
}

// ============================================================================
// StatCard Sub-Component
// ============================================================================

interface StatCardProps {
  icon: LucideIcon;
  label: string;
  value: number | string;
  iconColor: string;
  bgColor: string;
  index: number;
}

function StatCard({
  icon: Icon,
  label,
  value,
  iconColor,
  bgColor,
  index,
}: StatCardProps) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: index * 0.05, duration: 0.3 }}
      className="rounded-xl border border-border/60 bg-card/80 backdrop-blur-xl p-4"
      data-testid={`stat-card-${label.toLowerCase().replace(/\s+/g, '-')}`}
    >
      <div className="flex items-center gap-3">
        {/* Icon */}
        <div
          className={cn(
            'h-12 w-12 rounded-lg flex items-center justify-center shrink-0',
            bgColor
          )}
        >
          <Icon className={cn('h-6 w-6', iconColor)} />
        </div>

        {/* Content */}
        <div className="flex-1 min-w-0">
          <p className="text-xs text-muted-foreground mb-0.5">{label}</p>
          <p className="text-2xl font-bold tracking-tight" data-testid={`stat-value-${label.toLowerCase().replace(/\s+/g, '-')}`}>
            {value}
          </p>
        </div>
      </div>
    </motion.div>
  );
}

// ============================================================================
// Skeleton Component
// ============================================================================

function CollectionStatsSkeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn('grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4', className)}
      data-testid="collection-stats-skeleton"
    >
      {Array.from({ length: 4 }).map((_, i) => (
        <div
          key={i}
          className="rounded-xl border border-border/60 bg-card/80 backdrop-blur-xl p-4"
        >
          <div className="flex items-center gap-3">
            <Skeleton className="h-12 w-12 rounded-lg" />
            <div className="flex-1 space-y-2">
              <Skeleton className="h-3 w-20" />
              <Skeleton className="h-7 w-12" />
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

// ============================================================================
// CollectionStats Component
// ============================================================================

export function CollectionStats({
  stats,
  isLoading = false,
  className,
}: CollectionStatsProps) {
  if (isLoading || !stats) {
    return <CollectionStatsSkeleton className={className} />;
  }

  const statCards: StatCardType[] = [
    {
      label: 'Giochi Totali',
      value: stats.totalGames,
      icon: 'library',
      color: 'text-amber-600 dark:text-amber-400',
      bgColor: 'bg-amber-500/20',
    },
    {
      label: 'PDF Privati',
      value: stats.privatePdfsCount,
      icon: 'file-text',
      color: 'text-blue-600 dark:text-blue-400',
      bgColor: 'bg-blue-500/20',
    },
    {
      label: 'Chat Attive',
      value: stats.activeChats,
      icon: 'message-square',
      color: 'text-emerald-600 dark:text-emerald-400',
      bgColor: 'bg-emerald-500/20',
    },
    {
      label: 'Tempo Lettura',
      value: formatReadingTime(stats.totalReadingMinutes),
      icon: 'clock',
      color: 'text-purple-600 dark:text-purple-400',
      bgColor: 'bg-purple-500/20',
    },
  ];

  const iconMap: Record<string, LucideIcon> = {
    library: Library,
    'file-text': FileText,
    'message-square': MessageSquare,
    clock: Clock,
  };

  return (
    <section
      className={cn('grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4', className)}
      data-testid="collection-stats-section"
    >
      {statCards.map((stat, index) => (
        <StatCard
          key={stat.label}
          icon={iconMap[stat.icon]}
          label={stat.label}
          value={stat.value}
          iconColor={stat.color}
          bgColor={stat.bgColor}
          index={index}
        />
      ))}
    </section>
  );
}

export default CollectionStats;
