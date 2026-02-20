/**
 * GameCollectionGrid Component - Issue #4581
 * Grid of MeepleCards for user's game collection
 */

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { UserGameDto } from '@/lib/api/dashboard-client';

interface GameCollectionGridProps {
  games: UserGameDto[];
  isLoading?: boolean;
  onGameClick?: (gameId: string) => void;
}

export function GameCollectionGrid({
  games,
  isLoading,
  onGameClick,
}: GameCollectionGridProps) {
  if (isLoading) {
    return (
      <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-4 gap-6">
        {[...Array(8)].map((_, i) => (
          <div
            key={i}
            className="h-80 animate-pulse rounded-lg bg-muted"
            aria-label="Loading games"
          />
        ))}
      </div>
    );
  }

  if (games.length === 0) {
    return (
      <div className="text-center py-12 text-muted-foreground">
        <p className="text-lg mb-2">Nessun gioco trovato</p>
        <p className="text-sm">Aggiungi giochi alla tua collezione per iniziare!</p>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      {games.map((game) => (
        <MeepleCard
          key={game.id}
          entity="game"
          variant="grid"
          title={game.title}
          subtitle={game.publisher ?? undefined}
          imageUrl={game.thumbnailUrl ?? game.imageUrl ?? undefined}
          rating={game.averageRating ?? undefined}
          ratingMax={10}
          metadata={[
            {
              label:
                game.minPlayers && game.maxPlayers
                  ? `👥 ${game.minPlayers}-${game.maxPlayers}`
                  : '',
            },
            {
              label: game.playingTimeMinutes ? `⏱️ ${game.playingTimeMinutes}m` : '',
            },
            {
              label: game.playCount > 0 ? `🎲 ${game.playCount}x giocato` : '',
            },
          ].filter((m) => m.label)}
          badge={game.isOwned ? 'Owned' : 'Wishlist'}
          onClick={onGameClick ? () => onGameClick(game.id) : undefined}
        />
      ))}
    </div>
  );
}
