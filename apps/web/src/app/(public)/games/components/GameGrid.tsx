/**
 * GameGrid Component (Issue #1838: PAGE-003)
 *
 * Server component for rendering games in grid or list layout.
 * Uses existing GameCard component (UI-003) with responsive grid.
 *
 * Features:
 * - Responsive grid: 2 cols mobile → 3 cols tablet → 4 cols desktop
 * - List layout: Full-width cards
 * - Empty state with friendly message
 * - Loading skeleton (20 cards)
 */

import React from 'react';

import { GameCard } from '@/components/games/GameCard';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Game } from '@/lib/api';

export interface GameGridProps {
  /** Games to display */
  games: Game[];
  /** View mode */
  variant: 'grid' | 'list';
  /** Loading state */
  loading?: boolean;
}

export function GameGrid({ games, variant, loading = false }: GameGridProps) {
  // Loading skeleton
  if (loading) {
    return (
      <div
        className={
          variant === 'grid'
            ? 'grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6'
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

  // Grid view
  if (variant === 'grid') {
    return (
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-6">
        {games.map(game => (
          <GameCard key={game.id} game={game} variant="grid" />
        ))}
      </div>
    );
  }

  // List view
  return (
    <div className="flex flex-col gap-4">
      {games.map(game => (
        <GameCard key={game.id} game={game} variant="list" />
      ))}
    </div>
  );
}
