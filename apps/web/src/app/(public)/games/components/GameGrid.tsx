/**
 * GameGrid Component (Issue #1838: PAGE-003)
 *
 * Client component for rendering games in grid or list layout.
 * Uses existing GameCard component (UI-003) with responsive grid.
 *
 * Features:
 * - Responsive grid: 2 cols mobile → 3 cols tablet → 4 cols desktop
 * - List layout: Full-width cards
 * - Empty state with friendly message
 * - Loading skeleton (20 cards)
 * - Click navigation to game detail page
 *
 * Note (Issue #3894): Not migrated to EntityListView because:
 * 1. Uses MeepleGameCatalogCard (specialized adapter with library hooks),
 *    not MeepleCard which EntityListView renders via renderItem.
 * 2. View mode is URL-driven (server component parent), not localStorage.
 * 3. Data is fetched server-side with pagination - incompatible with
 *    EntityListView's client-side search/sort/filter pipeline.
 * Migration requires adding a `renderCard` prop to EntityListView.
 */

'use client';

import React, { useMemo } from 'react';

import { useRouter } from 'next/navigation';

import { MeepleGameCatalogCard } from '@/components/catalog/MeepleGameCatalogCard';
import {
  type CarouselGame,
  GameCarousel,
  GameCarouselSkeleton,
} from '@/components/ui/data-display/game-carousel';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useBatchGameStatus } from '@/hooks/queries/useBatchGameStatus';
import { SharedGame } from '@/lib/api';

export interface GameGridProps {
  /** Games to display */
  games: SharedGame[];
  /** View mode */
  variant: 'grid' | 'list' | 'carousel';
  /** Loading state */
  loading?: boolean;
}

function mapToCarouselGame(game: SharedGame): CarouselGame {
  const subtitle = [
    game.yearPublished ? `${game.yearPublished}` : null,
    game.bggId ? `BGG #${game.bggId}` : null,
  ]
    .filter(Boolean)
    .join(' · ');

  return {
    id: game.id,
    title: game.title,
    subtitle: subtitle || undefined,
    imageUrl: game.imageUrl || undefined,
    rating: game.averageRating ?? undefined,
    ratingMax: 10,
    description: game.description || undefined,
    metadata: [
      ...(game.minPlayers && game.maxPlayers
        ? [{ label: 'Players', value: `${game.minPlayers}-${game.maxPlayers}` }]
        : []),
      ...(game.playingTimeMinutes
        ? [{ label: 'Time', value: `${game.playingTimeMinutes} min` }]
        : []),
    ],
  };
}

export function GameGrid({ games, variant, loading = false }: GameGridProps) {
  const router = useRouter();

  // Batch API: Fetch library status for all games in a single call (Issue #4581)
  const gameIds = useMemo(() => games.map(g => g.id), [games]);
  const { data: batchStatus } = useBatchGameStatus(gameIds, !loading && gameIds.length > 0);

  const handleGameClick = (gameId: string) => {
    // Navigate to public game detail page (Issue #3522)
    router.push(`/games/${gameId}`);
  };

  // Loading skeleton
  if (loading) {
    if (variant === 'carousel') {
      return <GameCarouselSkeleton />;
    }
    return (
      <div
        className={
          variant === 'grid'
            ? 'grid grid-cols-1 md:grid-cols-3 lg:grid-cols-4 gap-6'
            : 'flex flex-col gap-4'
        }
      >
        {Array.from({ length: 20 }).map((_, i) => (
          <Skeleton
            key={`skeleton-${i}`}
            className={variant === 'grid' ? 'h-[300px]' : 'h-[120px]'}
          />
        ))}
      </div>
    );
  }

  // Empty state
  if (games.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <div className="text-6xl mb-4">🎲</div>
        <h3 className="text-lg font-semibold mb-2">Nessun gioco trovato</h3>
        <p className="text-muted-foreground">Prova a modificare i filtri o la ricerca</p>
      </div>
    );
  }

  // Carousel view
  if (variant === 'carousel') {
    return (
      <GameCarousel
        games={games.map(mapToCarouselGame)}
        onGameSelect={game => handleGameClick(game.id)}
        showDots
        autoPlay={false}
        flippable
      />
    );
  }

  // Grid view
  if (variant === 'grid') {
    return (
      <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-4 gap-6">
        {games.map(game => (
          <MeepleGameCatalogCard
            key={game.id}
            game={game}
            variant="grid"
            flippable
            onClick={() => handleGameClick(game.id)}
            libraryStatus={batchStatus?.results[game.id]}
            className="h-full"
          />
        ))}
      </div>
    );
  }

  // List view
  return (
    <div className="flex flex-col gap-4">
      {games.map(game => (
        <MeepleGameCatalogCard
          key={game.id}
          game={game}
          variant="list"
          onClick={() => handleGameClick(game.id)}
          libraryStatus={batchStatus?.results[game.id]}
        />
      ))}
    </div>
  );
}
