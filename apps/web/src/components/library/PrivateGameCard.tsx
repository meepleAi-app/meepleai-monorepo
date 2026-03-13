/**
 * Private Game Card Component
 * Issue #3669: Phase 8 - Frontend Integration (Task 8.3)
 * Issue #4857: Migrated to MeepleCard design system
 *
 * Displays private games using the MeepleCard glassmorphic design system
 * with entity="game" styling (orange accents).
 */

'use client';

import { Calendar, Clock, Edit2, Share2, Trash2, Users } from 'lucide-react';

import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import type { PrivateGameDto } from '@/lib/api/schemas/private-games.schemas';

export interface PrivateGameCardProps {
  game: PrivateGameDto;
  onEdit?: (game: PrivateGameDto) => void;
  onDelete?: (gameId: string) => void;
  onPropose?: (game: PrivateGameDto) => void;
  onClick?: () => void;
}

export function PrivateGameCard({
  game,
  onEdit,
  onDelete,
  onPropose,
  onClick,
}: PrivateGameCardProps) {
  const subtitle = [
    `${game.minPlayers}-${game.maxPlayers} players`,
    game.yearPublished ? `(${game.yearPublished})` : null,
    game.playingTimeMinutes ? `${game.playingTimeMinutes} min` : null,
  ]
    .filter(Boolean)
    .join(' \u00b7 ');

  const metadata: MeepleCardMetadata[] = [
    { icon: Users, label: `${game.minPlayers}-${game.maxPlayers}` },
    ...(game.playingTimeMinutes ? [{ icon: Clock, label: `${game.playingTimeMinutes} min` }] : []),
    ...(game.yearPublished ? [{ icon: Calendar, label: `${game.yearPublished}` }] : []),
  ];

  const quickActions = [
    ...(onEdit ? [{ icon: Edit2, label: 'Edit', onClick: () => onEdit(game) }] : []),
    ...(onPropose ? [{ icon: Share2, label: 'Propose', onClick: () => onPropose(game) }] : []),
    ...(onDelete
      ? [{ icon: Trash2, label: 'Delete', onClick: () => onDelete(game.id), destructive: true }]
      : []),
  ];

  return (
    <MeepleCard
      entity="game"
      variant="grid"
      title={game.title}
      subtitle={subtitle}
      imageUrl={game.imageUrl || undefined}
      badge="Private"
      metadata={metadata}
      onClick={onClick}
      quickActions={quickActions.length > 0 ? quickActions : undefined}
      data-testid={`game-card-${game.id}`}
    />
  );
}
