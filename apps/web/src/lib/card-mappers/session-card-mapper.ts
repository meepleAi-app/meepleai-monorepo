import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

import { sessionStatusToLabel } from './shared-utils';

/**
 * Mappa un GameSessionDto alle props di MeepleCard.
 */
export function buildSessionCardProps(session: GameSessionDto): Partial<MeepleCardProps> {
  const stateInfo = sessionStatusToLabel(session.status);

  return {
    badge: stateInfo.text,
  };
}
