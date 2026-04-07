import type { MeepleCardMetadata, MeepleCardProps } from '@/components/ui/data-display/meeple-card';
import type { Game } from '@/lib/api';

import { formatPlayTime } from './shared-utils';

/**
 * Mappa un Game DTO alle props di base di MeepleCard.
 * Non include props di callback (actions, clicks) — quelle restano nell'adapter del componente.
 */
export function buildGameCardProps(game: Game): Partial<MeepleCardProps> {
  // playerCount metadata
  const hasPlayers = game.minPlayers !== null || game.maxPlayers !== null;
  const playerCountLabel = hasPlayers
    ? `${game.minPlayers ?? '?'}-${game.maxPlayers ?? '?'}p`
    : undefined;

  // playTimeDisplay — average of min/max
  let playTimeLabel: string | undefined;
  const min = game.minPlayTimeMinutes ?? null;
  const max = game.maxPlayTimeMinutes ?? null;
  if (min !== null || max !== null) {
    const avg = Math.round(((min ?? max ?? 0) + (max ?? min ?? 0)) / 2);
    playTimeLabel = formatPlayTime(avg);
  }

  // subtitle
  const subtitleParts: string[] = [];
  if (game.publisher) subtitleParts.push(game.publisher);
  if (game.yearPublished) subtitleParts.push(String(game.yearPublished));
  const subtitle = subtitleParts.length > 0 ? subtitleParts.join(' \u00B7 ') : undefined;

  const metadata: MeepleCardMetadata[] = [];
  if (playerCountLabel) metadata.push({ label: playerCountLabel });
  if (playTimeLabel) metadata.push({ label: playTimeLabel });

  return {
    title: game.title,
    subtitle,
    imageUrl: game.imageUrl ?? undefined,
    rating: game.averageRating ?? undefined,
    ratingMax: 10,
    ...(metadata.length > 0 ? { metadata } : {}),
  };
}
