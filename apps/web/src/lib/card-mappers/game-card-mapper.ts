import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card';
import type { Game } from '@/lib/api';

import { formatPlayTime } from './shared-utils';

/**
 * Mappa un Game DTO alle props di base di MeepleCard.
 * Non include props di callback (actions, clicks) — quelle restano nell'adapter del componente.
 *
 * Nota: Il tipo `Game` non include `mechanics`/`categories` (quelli sono su `BggGameDetails`
 * e `SharedGameDto`). Identity chips richiedono un DTO più ricco, da passare direttamente
 * nell'adapter quando disponibile.
 */
export function buildGameCardProps(game: Game): Partial<MeepleCardProps> {
  // playerCountDisplay
  const hasPlayers = game.minPlayers !== null || game.maxPlayers !== null;
  const playerCountDisplay = hasPlayers
    ? `${game.minPlayers ?? '?'}-${game.maxPlayers ?? '?'}p`
    : undefined;

  // playTimeDisplay — average of min/max
  let playTimeDisplay: string | undefined;
  const min = game.minPlayTimeMinutes ?? null;
  const max = game.maxPlayTimeMinutes ?? null;
  if (min !== null || max !== null) {
    const avg = Math.round(((min ?? max ?? 0) + (max ?? min ?? 0)) / 2);
    playTimeDisplay = formatPlayTime(avg);
  }

  // subtitle
  const subtitleParts: string[] = [];
  if (game.publisher) subtitleParts.push(game.publisher);
  if (game.yearPublished) subtitleParts.push(String(game.yearPublished));
  const subtitle = subtitleParts.length > 0 ? subtitleParts.join(' \u00B7 ') : undefined;

  return {
    title: game.title,
    subtitle,
    imageUrl: game.imageUrl ?? undefined,
    rating: game.averageRating ?? undefined,
    ratingMax: 10,
    playerCountDisplay,
    playTimeDisplay,
  };
}
