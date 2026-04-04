import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

import { buildLinkedEntities, sessionStatusToLabel } from './shared-utils';

/**
 * Mappa un GameSessionDto alle props di MeepleCard.
 * Il gioco collegato appare come pip linkedEntity (non identityChip).
 */
export function buildSessionCardProps(session: GameSessionDto): Partial<MeepleCardProps> {
  const linkedEntities = buildLinkedEntities({
    gameCount: session.gameId ? 1 : 0,
  });

  return {
    linkedEntities: linkedEntities.length > 0 ? linkedEntities : undefined,
    stateLabel: sessionStatusToLabel(session.status),
  };
}
