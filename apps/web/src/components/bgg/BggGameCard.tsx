/**
 * BggGameCard - Issue #4141
 * Issue #4859: Migrated to MeepleCard design system
 *
 * Compact selectable card for BGG search results.
 * Uses MeepleCard with entity="game" orange styling.
 */

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { BggSearchResult } from '@/types/bgg';

interface BggGameCardProps {
  game: BggSearchResult;
  selected?: boolean;
  onSelect: (gameId: number) => void;
}

export function BggGameCard({ game, selected = false, onSelect }: BggGameCardProps) {
  return (
    <MeepleCard
      entity="game"
      variant="compact"
      title={game.name}
      subtitle={`${game.yearPublished}`}
      imageUrl={game.thumbnail || undefined}
      badge={selected ? 'Selezionato' : undefined}
      onClick={() => onSelect(game.id)}
      data-testid={`bgg-game-card-${game.id}`}
    />
  );
}
