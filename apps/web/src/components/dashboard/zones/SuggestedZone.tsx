'use client';

import { MeepleCard, MeepleCardSkeleton } from '@/components/ui/data-display/meeple-card';
import { useRecentlyAddedGames } from '@/hooks/queries/useLibrary';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

export function SuggestedZone() {
  const { data, isLoading } = useRecentlyAddedGames(8);
  const games: UserLibraryEntry[] = data?.items ?? [];

  if (isLoading) {
    return (
      <section data-testid="suggested-zone-skeleton" className="flex-1">
        <h3 className="text-sm font-medium text-foreground/80 mb-2">
          Potrebbe piacerti
          <span className="ml-1.5 text-xs text-primary/70 font-normal">AI</span>
        </h3>
        <div className="overflow-x-auto flex gap-4 snap-x snap-mandatory pb-2 scrollbar-hide">
          {Array.from({ length: 4 }, (_, i) => (
            <div key={i} className="min-w-[200px] max-w-[220px] snap-start shrink-0">
              <MeepleCardSkeleton variant="grid" />
            </div>
          ))}
        </div>
      </section>
    );
  }

  // Only show if user has >= 3 games
  if (games.length < 3) return null;

  return (
    <section data-testid="suggested-zone" className="flex-1">
      <h3 className="text-sm font-medium text-foreground/80 mb-2">
        Potrebbe piacerti
        <span className="ml-1.5 text-xs text-primary/70 font-normal">AI</span>
      </h3>
      <div
        className="overflow-x-auto flex gap-4 snap-x snap-mandatory pb-2 scrollbar-hide"
        style={{ WebkitOverflowScrolling: 'touch' }}
      >
        {games.map(game => (
          <div key={game.id} className="min-w-[200px] max-w-[220px] snap-start shrink-0">
            <MeepleCard
              id={game.gameId}
              entity="game"
              variant="grid"
              title={game.gameTitle}
              subtitle={game.gamePublisher ?? undefined}
              imageUrl={game.gameImageUrl ?? undefined}
              data-testid={`suggested-${game.gameId}`}
            />
          </div>
        ))}
      </div>
    </section>
  );
}
