/**
 * CatalogTrending - Dashboard Widget for Trending Games
 * Issue #3318 - Implement CatalogTrending widget
 *
 * Features:
 * - Shows top 3-5 trending games in community
 * - Trend percentages with visual indicators
 * - Color coding (green positive, red negative)
 * - Click navigation to game detail
 * - CTA to view full catalog
 * - Loading skeleton state
 *
 * @example
 * ```tsx
 * <CatalogTrending />
 * ```
 */

'use client';

import { useMemo } from 'react';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { motion } from 'framer-motion';
import {
  Flame,
  TrendingUp,
  TrendingDown,
  Minus,
  ChevronRight,
  BarChart3,
} from 'lucide-react';
import Link from 'next/link';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface TrendingGame {
  id: string;
  name: string;
  trend: number; // percentage (+15, -5, etc)
  rank: number;
  previousRank: number;
  imageUrl?: string | null;
}

export interface CatalogTrendingProps {
  /** Trending games data */
  games?: TrendingGame[];
  /** Loading state */
  isLoading?: boolean;
  /** Last updated timestamp */
  lastUpdated?: string;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Constants
// ============================================================================

const MAX_GAMES = 5;

// ============================================================================
// Mock Data (for development)
// ============================================================================

const MOCK_GAMES: TrendingGame[] = [
  {
    id: 'game-1',
    name: 'Ark Nova',
    trend: 15,
    rank: 1,
    previousRank: 2,
  },
  {
    id: 'game-2',
    name: 'Wingspan',
    trend: 12,
    rank: 2,
    previousRank: 3,
  },
  {
    id: 'game-3',
    name: 'Dune: Imperium',
    trend: 10,
    rank: 3,
    previousRank: 1,
  },
  {
    id: 'game-4',
    name: 'Earth',
    trend: 8,
    rank: 4,
    previousRank: 6,
  },
  {
    id: 'game-5',
    name: 'Cascadia',
    trend: 5,
    rank: 5,
    previousRank: 4,
  },
];

// ============================================================================
// Skeleton Component
// ============================================================================

function CatalogTrendingSkeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="catalog-trending-skeleton"
    >
      {/* Header Skeleton */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <Skeleton className="h-8 w-8 rounded-lg" />
          <Skeleton className="h-5 w-32" />
        </div>
      </div>

      {/* Subtitle Skeleton */}
      <Skeleton className="h-4 w-40 mb-3" />

      {/* Items Skeleton */}
      <div className="space-y-2">
        {Array.from({ length: 5 }).map((_, i) => (
          <div
            key={i}
            className="flex items-center justify-between p-2 rounded-lg"
          >
            <div className="flex items-center gap-3">
              <Skeleton className="h-5 w-5 rounded" />
              <Skeleton className="h-4 w-28" />
            </div>
            <Skeleton className="h-5 w-14" />
          </div>
        ))}
      </div>

      {/* Footer Skeleton */}
      <div className="mt-4 space-y-2">
        <Skeleton className="h-3 w-32" />
        <Skeleton className="h-9 w-full rounded-full" />
      </div>
    </div>
  );
}

// ============================================================================
// Trend Icon Component
// ============================================================================

function TrendIcon({ trend }: { trend: number }) {
  if (trend >= 10) {
    return <Flame className="h-4 w-4 text-orange-500" data-testid="trend-icon-hot" />;
  }
  if (trend > 0) {
    return <TrendingUp className="h-4 w-4 text-emerald-500" data-testid="trend-icon-up" />;
  }
  if (trend < 0) {
    return <TrendingDown className="h-4 w-4 text-red-500" data-testid="trend-icon-down" />;
  }
  return <Minus className="h-4 w-4 text-muted-foreground" data-testid="trend-icon-stable" />;
}

// ============================================================================
// Trend Badge Component
// ============================================================================

function TrendBadge({ trend }: { trend: number }) {
  const isPositive = trend > 0;
  const isNegative = trend < 0;
  const isHot = trend >= 10;

  return (
    <div
      className={cn(
        'flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium',
        isHot && 'bg-orange-100 text-orange-700 dark:bg-orange-900/30 dark:text-orange-400',
        isPositive && !isHot && 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400',
        isNegative && 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400',
        !isPositive && !isNegative && 'bg-muted text-muted-foreground'
      )}
      data-testid="trend-badge"
    >
      <TrendIcon trend={trend} />
      <span>
        {isPositive && '+'}
        {trend}%
      </span>
    </div>
  );
}

// ============================================================================
// Trending Game Row Component
// ============================================================================

interface TrendingGameRowProps {
  game: TrendingGame;
  index: number;
}

function TrendingGameRow({ game, index }: TrendingGameRowProps) {
  return (
    <motion.div
      initial={{ opacity: 0, x: -10 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ delay: index * 0.05 }}
    >
      <Link
        href={`/games/${game.id}`}
        className="flex items-center justify-between p-2 rounded-lg hover:bg-muted/50 transition-colors group"
        data-testid={`trending-game-${game.id}`}
      >
        <div className="flex items-center gap-3 min-w-0">
          {/* Rank */}
          <span
            className={cn(
              'text-sm font-bold w-5 text-center tabular-nums',
              game.rank === 1 && 'text-amber-500',
              game.rank === 2 && 'text-slate-400',
              game.rank === 3 && 'text-amber-700',
              game.rank > 3 && 'text-muted-foreground'
            )}
            data-testid={`trending-rank-${game.id}`}
          >
            {game.rank}.
          </span>

          {/* Game Name */}
          <span
            className="text-sm font-medium truncate group-hover:text-primary transition-colors"
            data-testid={`trending-name-${game.id}`}
          >
            {game.name}
          </span>
        </div>

        {/* Trend Badge */}
        <div data-testid={`trending-trend-${game.id}`}>
          <TrendBadge trend={game.trend} />
        </div>
      </Link>
    </motion.div>
  );
}

// ============================================================================
// Empty State Component
// ============================================================================

function EmptyState() {
  return (
    <div
      className="flex flex-col items-center justify-center py-6 text-center"
      data-testid="catalog-trending-empty"
    >
      <div className="h-12 w-12 rounded-full bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center mb-3">
        <BarChart3 className="h-6 w-6 text-blue-500" />
      </div>
      <p className="text-sm text-muted-foreground mb-1">
        Nessun dato trending
      </p>
      <p className="text-xs text-muted-foreground mb-3">
        I dati saranno disponibili a breve
      </p>
      <Link href="/games/catalog">
        <Button
          size="sm"
          variant="outline"
          className="rounded-full"
          data-testid="explore-catalog-empty-cta"
        >
          <BarChart3 className="h-4 w-4 mr-1" />
          Esplora Catalogo
        </Button>
      </Link>
    </div>
  );
}

// ============================================================================
// CatalogTrending Component
// ============================================================================

export function CatalogTrending({
  games = MOCK_GAMES,
  isLoading = false,
  lastUpdated,
  className,
}: CatalogTrendingProps) {
  // Sort by rank and limit to MAX_GAMES
  const sortedGames = useMemo(() => {
    return [...games]
      .sort((a, b) => a.rank - b.rank)
      .slice(0, MAX_GAMES);
  }, [games]);

  if (isLoading) {
    return <CatalogTrendingSkeleton className={className} />;
  }

  return (
    <section
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="catalog-trending-widget"
    >
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <div className="h-8 w-8 rounded-lg bg-blue-500/20 flex items-center justify-center">
            <BarChart3 className="h-4 w-4 text-blue-600 dark:text-blue-400" />
          </div>
          <h3
            className="font-semibold text-sm"
            data-testid="catalog-trending-title"
          >
            Catalog Trending
          </h3>
        </div>
      </div>

      {/* Content */}
      {sortedGames.length === 0 ? (
        <EmptyState />
      ) : (
        <>
          {/* Subtitle */}
          <p className="text-xs text-muted-foreground mb-3 flex items-center gap-1">
            <Flame className="h-3 w-3 text-orange-500" />
            Trending questa settimana:
          </p>

          {/* Games List */}
          <div className="space-y-1" data-testid="trending-games-list">
            {sortedGames.map((game, index) => (
              <TrendingGameRow
                key={game.id}
                game={game}
                index={index}
              />
            ))}
          </div>

          {/* Footer */}
          <div className="mt-4">
            {/* Last Updated */}
            {lastUpdated && (
              <p
                className="text-xs text-muted-foreground mb-2"
                data-testid="last-updated"
              >
                Aggiornato {formatDistanceToNow(new Date(lastUpdated), { addSuffix: true, locale: it })}
              </p>
            )}

            {/* CTA */}
            <Link href="/games/catalog" className="block">
              <Button
                variant="outline"
                className="w-full rounded-full"
                data-testid="view-catalog-cta"
              >
                Vedi Catalogo Completo
                <ChevronRight className="h-4 w-4 ml-1" />
              </Button>
            </Link>
          </div>
        </>
      )}
    </section>
  );
}

export default CatalogTrending;
