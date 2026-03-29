'use client';

import React from 'react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { useRecentlyAddedGames } from '@/hooks/queries/useLibrary';

export function RecentGamesRow() {
  const { data, isLoading } = useRecentlyAddedGames(8);

  if (isLoading) {
    return (
      <div className="flex gap-3 overflow-x-auto px-4 py-2 scrollbar-none">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="h-32 w-24 shrink-0 animate-pulse rounded-lg bg-white/5" />
        ))}
      </div>
    );
  }

  const games = data?.items ?? [];
  if (games.length === 0) return null;

  return (
    <section>
      <h2 className="mb-2 px-4 text-sm font-medium text-[var(--gaming-text-secondary)]">
        Giochi recenti
      </h2>
      <div className="flex gap-3 overflow-x-auto px-4 pb-2 scrollbar-none">
        {games.map(entry => (
          <div key={entry.id} className="w-28 shrink-0">
            <MeepleCard
              entity="game"
              variant="compact"
              title={entry.gameTitle}
              imageUrl={entry.gameImageUrl ?? entry.gameIconUrl ?? undefined}
              detailHref={`/library/games/${entry.gameId}`}
            />
          </div>
        ))}
      </div>
    </section>
  );
}
