/**
 * LibrarySnapshot - Dashboard Widget for Library Overview
 * Issue #3310 - Implement LibrarySnapshot component
 * Issue #3912 - Library Snapshot Component (enhancements)
 *
 * Features:
 * - Quota progress bar with dynamic colors (< 50% green, 50-80% amber, > 80% red)
 * - Top 3 games by play count
 * - Cover thumbnails with lazy loading
 * - Rating display with 5-star system (0-5 scale for better UX)
 * - Empty state with "Aggiungi Primo Gioco" CTA
 * - Loading skeleton state
 * - Hover lift effects with shadow
 * - Warm Tabletop aesthetic (amber/emerald/red palette)
 *
 * Note: Uses 5-star rating (0-5) instead of 10-point scale per Issue #3912.
 * Rationale: Industry standard, better UX, compact display.
 * Conversion from 10-point: rating_5 = Math.round(rating_10 / 2)
 *
 * @example
 * ```tsx
 * <LibrarySnapshot quota={{used: 127, total: 200}} topGames={topGames} />
 * ```
 */

'use client';

import React, { useMemo } from 'react';

import { motion } from 'framer-motion';
import {
  Library,
  Star,
  ChevronRight,
  Plus,
  Gamepad2,
} from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';

import { Progress } from '@/components/ui/feedback/progress';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface TopGame {
  id: string;
  title: string;
  coverUrl?: string;
  rating: number; // 0-5
  playCount: number;
}

export interface LibraryQuota {
  used: number;
  total: number;
}

export interface LibrarySnapshotProps {
  /** Library quota data */
  quota?: LibraryQuota;
  /** Top games by play count */
  topGames?: TopGame[];
  /** Loading state */
  isLoading?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Mock Data (for development)
// ============================================================================

const MOCK_QUOTA: LibraryQuota = {
  used: 127,
  total: 200,
};

const MOCK_TOP_GAMES: TopGame[] = [
  {
    id: 'game-1',
    title: 'Catan',
    coverUrl: '/images/games/catan.jpg',
    rating: 5,
    playCount: 45,
  },
  {
    id: 'game-2',
    title: 'Ticket to Ride',
    coverUrl: '/images/games/ticket.jpg',
    rating: 4,
    playCount: 32,
  },
  {
    id: 'game-3',
    title: 'Azul',
    coverUrl: '/images/games/azul.jpg',
    rating: 4,
    playCount: 28,
  },
];

// ============================================================================
// Helper Functions
// ============================================================================

function getQuotaColor(percentage: number): {
  bar: string;
  text: string;
  bg: string;
} {
  if (percentage < 50) {
    return {
      bar: 'bg-emerald-500',
      text: 'text-emerald-600 dark:text-emerald-400',
      bg: 'bg-emerald-500/10',
    };
  }
  if (percentage < 80) {
    return {
      bar: 'bg-amber-500',
      text: 'text-amber-600 dark:text-amber-400',
      bg: 'bg-amber-500/10',
    };
  }
  return {
    bar: 'bg-red-500',
    text: 'text-red-600 dark:text-red-400',
    bg: 'bg-red-500/10',
  };
}

function renderStars(rating: number): React.ReactNode[] {
  return Array.from({ length: 5 }, (_, i) => (
    <Star
      key={i}
      className={cn(
        'h-3 w-3',
        i < rating
          ? 'fill-amber-400 text-amber-400'
          : 'fill-muted text-muted'
      )}
    />
  ));
}

// ============================================================================
// Skeleton Component
// ============================================================================

function LibrarySnapshotSkeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="library-snapshot-skeleton"
    >
      {/* Header Skeleton */}
      <div className="flex items-center gap-2 mb-4">
        <Skeleton className="h-8 w-8 rounded-lg" />
        <Skeleton className="h-5 w-40" />
      </div>

      {/* Progress Skeleton */}
      <div className="mb-4 space-y-2">
        <Skeleton className="h-4 w-32" />
        <Skeleton className="h-2 w-full rounded-full" />
      </div>

      {/* Games Skeleton */}
      <div className="space-y-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="flex items-center gap-3">
            <Skeleton className="h-12 w-12 rounded-lg" />
            <div className="flex-1 space-y-2">
              <Skeleton className="h-4 w-24" />
              <Skeleton className="h-3 w-32" />
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Empty State Component
// ============================================================================

function EmptyState() {
  return (
    <div
      className="flex flex-col items-center justify-center py-6 text-center"
      data-testid="library-snapshot-empty"
    >
      <div className="h-12 w-12 rounded-full bg-muted/50 flex items-center justify-center mb-3">
        <Gamepad2 className="h-6 w-6 text-muted-foreground/50" />
      </div>
      <p className="text-sm text-muted-foreground mb-3">
        Nessun gioco nella collezione
      </p>
      <Link href="/library/add">
        <Button
          size="sm"
          variant="outline"
          className="rounded-full"
          data-testid="add-first-game-cta"
        >
          <Plus className="h-4 w-4 mr-1" />
          Aggiungi Primo Gioco
        </Button>
      </Link>
    </div>
  );
}

// ============================================================================
// Game Card Component
// ============================================================================

function GameCard({ game, index }: { game: TopGame; index: number }) {
  return (
    <motion.div
      initial={{ opacity: 0, x: -10 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ delay: index * 0.1 }}
      className="flex items-center gap-3 p-2 rounded-xl hover:bg-muted/30 hover:-translate-y-0.5 hover:shadow-lg transition-all duration-200"
      data-testid={`top-game-${game.id}`}
    >
      {/* Cover Image */}
      <div className="relative h-12 w-12 shrink-0 rounded-lg overflow-hidden bg-muted">
        {game.coverUrl ? (
          <Image
            src={game.coverUrl}
            alt={game.title}
            fill
            sizes="48px"
            className="object-cover"
            loading="lazy"
          />
        ) : (
          <div className="h-full w-full flex items-center justify-center">
            <Gamepad2 className="h-6 w-6 text-muted-foreground/50" />
          </div>
        )}
      </div>

      {/* Game Info */}
      <div className="flex-1 min-w-0">
        <p
          className="font-medium text-sm truncate"
          data-testid={`game-title-${game.id}`}
        >
          {game.title}
        </p>
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <span className="flex items-center gap-0.5" data-testid={`game-rating-${game.id}`}>
            {renderStars(game.rating)}
          </span>
          <span className="text-muted-foreground/50">•</span>
          <span data-testid={`game-plays-${game.id}`}>{game.playCount} partite</span>
        </div>
      </div>
    </motion.div>
  );
}

// ============================================================================
// LibrarySnapshot Component
// ============================================================================

export function LibrarySnapshot({
  quota = { used: 0, total: 0 },
  topGames = [],
  isLoading = false,
  className,
}: LibrarySnapshotProps) {
  const percentage = useMemo(
    () => (quota.total > 0 ? Math.round((quota.used / quota.total) * 100) : 0),
    [quota]
  );
  const quotaColors = useMemo(() => getQuotaColor(percentage), [percentage]);
  const displayGames = useMemo(() => topGames.slice(0, 3), [topGames]);

  if (isLoading) {
    return <LibrarySnapshotSkeleton className={className} />;
  }

  const isEmpty = topGames.length === 0;

  return (
    <section
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="library-snapshot-widget"
    >
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <div className="h-8 w-8 rounded-lg bg-amber-500/20 flex items-center justify-center">
            <Library className="h-4 w-4 text-amber-600 dark:text-amber-400" />
          </div>
          <h3 className="font-semibold text-sm" data-testid="library-snapshot-title">
            La Mia Collezione
          </h3>
        </div>

        <Link
          href="/library"
          className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1 transition-colors"
          data-testid="view-library-link"
        >
          Vedi Tutto
          <ChevronRight className="h-3 w-3" />
        </Link>
      </div>

      {/* Quota Progress */}
      <div className="mb-4" data-testid="quota-section">
        <div className="flex items-center justify-between mb-2">
          <span className="text-xs text-muted-foreground">
            Quota Libreria
          </span>
          <span
            className={cn('text-xs font-medium', quotaColors.text)}
            data-testid="quota-text"
          >
            {quota.used}/{quota.total} ({percentage}%)
          </span>
        </div>
        <Progress
          value={percentage}
          className={cn('h-2', `[&>div]:${quotaColors.bar}`)}
          aria-label="Quota libreria"
          data-testid="quota-progress"
        />
      </div>

      {/* Content */}
      {isEmpty ? (
        <EmptyState />
      ) : (
        <>
          {/* Top Games Label */}
          <p className="text-xs text-muted-foreground mb-2">
            Top 3 Giochi per Partite
          </p>

          {/* Games List */}
          <div className="space-y-1" data-testid="top-games-list">
            {displayGames.map((game, index) => (
              <GameCard key={game.id} game={game} index={index} />
            ))}
          </div>
        </>
      )}
    </section>
  );
}

export default LibrarySnapshot;
