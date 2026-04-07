/**
 * BggPreviewCard - Issue #4141
 * Issue #4859: Migrated to MeepleCard design system
 *
 * Featured card showing detailed BGG game info before import.
 * Uses MeepleCard with entity="game" variant="featured".
 */

import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import type { BggGameDetailsDto } from '@/types/bgg';

interface BggPreviewCardProps {
  game: BggGameDetailsDto;
}

export function BggPreviewCard({ game }: BggPreviewCardProps) {
  const playerLabel =
    game.minPlayers === game.maxPlayers
      ? `${game.minPlayers} players`
      : `${game.minPlayers}-${game.maxPlayers} players`;

  const metadata: MeepleCardMetadata[] = [
    { label: playerLabel },
    { label: `${game.playingTime} min` },
    { label: `${game.rating.toFixed(1)} / 10` },
  ];

  return (
    <MeepleCard
      entity="game"
      variant="featured"
      title={game.name}
      subtitle={`${game.yearPublished} \u00b7 Age ${game.minAge}+`}
      imageUrl={game.thumbnail || undefined}
      rating={game.rating}
      ratingMax={10}
      metadata={metadata}
      badge={`ID #${game.id}`}
      data-testid={`bgg-preview-card-${game.id}`}
    />
  );
}
