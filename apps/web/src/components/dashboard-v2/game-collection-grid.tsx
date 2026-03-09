/**
 * GameCollectionGrid Component
 * Issue #4909 - Uniform MeepleCard UI across dashboard, /games and admin
 *
 * Updated to use MeepleUserLibraryCard for visual and functional consistency
 * with /games catalog (flip, quick actions, navigateTo footer, full imageUrl).
 */

import {
  MeepleUserLibraryCard,
  MeepleUserLibraryCardSkeleton,
} from '@/components/library/MeepleUserLibraryCard';
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
          <MeepleUserLibraryCardSkeleton key={i} variant="grid" />
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
        <MeepleUserLibraryCard
          key={game.id}
          game={game}
          variant="grid"
          onClick={onGameClick}
        />
      ))}
    </div>
  );
}
