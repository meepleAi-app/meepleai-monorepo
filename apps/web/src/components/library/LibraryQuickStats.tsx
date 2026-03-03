'use client';

/**
 * LibraryQuickStats — Compact stats row for the Collection tab
 *
 * Shows key library statistics in a single glassmorphic row:
 * - Total games count
 * - Favorite games count
 * - Most recent addition date (hidden on mobile)
 */

import { Calendar, Heart, Trophy } from 'lucide-react';

import { useLibraryStats } from '@/hooks/queries/useLibrary';
import { cn } from '@/lib/utils';

export interface LibraryQuickStatsProps {
  className?: string;
}

export function LibraryQuickStats({ className }: LibraryQuickStatsProps) {
  const { data: stats, isLoading } = useLibraryStats();

  if (isLoading || !stats) return null;

  const formattedDate = stats.newestAddedAt
    ? new Date(stats.newestAddedAt).toLocaleDateString('it-IT', {
        day: 'numeric',
        month: 'short',
      })
    : null;

  return (
    <div
      className={cn(
        'flex items-center gap-4 rounded-xl px-4 py-2.5',
        'bg-white/50 dark:bg-card/50 backdrop-blur-sm',
        'border border-white/20 dark:border-border/30',
        'text-sm text-muted-foreground',
        className
      )}
    >
      <div className="flex items-center gap-1.5">
        <Trophy className="h-3.5 w-3.5 text-amber-500" aria-hidden="true" />
        <span className="tabular-nums font-medium">{stats.totalGames}</span>
        <span className="hidden sm:inline">giochi</span>
      </div>

      <div className="h-3.5 w-px bg-border/50" aria-hidden="true" />

      <div className="flex items-center gap-1.5">
        <Heart className="h-3.5 w-3.5 text-rose-500" aria-hidden="true" />
        <span className="tabular-nums font-medium">{stats.favoriteGames}</span>
        <span className="hidden sm:inline">preferiti</span>
      </div>

      {formattedDate && (
        <>
          <div className="hidden md:block h-3.5 w-px bg-border/50" aria-hidden="true" />
          <div className="hidden md:flex items-center gap-1.5">
            <Calendar className="h-3.5 w-3.5 text-blue-500" aria-hidden="true" />
            <span>ultimo: {formattedDate}</span>
          </div>
        </>
      )}
    </div>
  );
}
