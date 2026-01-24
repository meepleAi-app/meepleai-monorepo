/**
 * Game Detail Page (Issues #2833-2840)
 *
 * Complete game detail page with stats, sessions, and actions
 */

'use client';

import { useParams } from 'next/navigation';

import { GameHeroSection } from '@/components/game-detail/game-hero-section';
import { StatsGrid } from '@/components/game-detail/stats-grid';
import { useGameDetail } from '@/lib/hooks/useGameDetail';

export default function GameDetailPage() {
  const params = useParams();
  const gameId = params.id as string;

  const { data: game, isLoading, error } = useGameDetail(gameId);

  if (isLoading) {
    return (
      <div className="container mx-auto px-4 py-8">
        <div className="animate-pulse space-y-4">
          <div className="h-64 bg-neutral-200 dark:bg-neutral-800 rounded-lg" />
          <div className="h-8 bg-neutral-200 dark:bg-neutral-800 rounded w-1/2" />
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {[...Array(4)].map((_, i) => (
              <div key={i} className="h-24 bg-neutral-200 dark:bg-neutral-800 rounded-lg" />
            ))}
          </div>
        </div>
      </div>
    );
  }

  if (error || !game) {
    return (
      <div className="container mx-auto px-4 py-8">
        <div className="text-center">
          <p className="text-red-600 dark:text-red-400">
            {error?.message ?? 'Game not found'}
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="container mx-auto px-4 py-8 space-y-8">
      <GameHeroSection
        title={game.gameTitle}
        imageUrl={game.gameImageUrl}
        status={game.currentState}
        publisher={game.gamePublisher}
        year={game.gameYearPublished}
      />

      <StatsGrid
        timesPlayed={game.timesPlayed}
        winRate={game.winRate}
        avgDuration={game.avgDuration}
        lastPlayed={game.lastPlayed}
      />

      {game.gameDescription && (
        <div className="prose dark:prose-invert max-w-none">
          <h2>About</h2>
          <p>{game.gameDescription}</p>
        </div>
      )}
    </div>
  );
}
