/**
 * mapNightSessions — #4752 (the night → live game bridge).
 *
 * Projects the sessions of the night-live read (`GET /game-nights/{id}/live`) onto the shape the night
 * DETAIL page lists. That page used to read them from the planning store's `activeSessions`, which no
 * production code ever filled: the «Partite» list was always empty, `hasActiveSession` always false and
 * «Concludi serata» always off. The night-live read is the one source that knows each sitting's status
 * and its live game.
 *
 * PURE (no clock, no I/O) so the status/liveness rules are testable without a query client.
 */

import type { GameNightLiveDto, GameNightSessionDto } from '@/lib/api/schemas/game-nights.schemas';
import type { GameNightActiveSession } from '@/stores/game-night/types';

/**
 * The list badge. Canonical liveness (#3188 D4: `isLive`, sourced from the tracking Session) wins over
 * the raw link status, as in `mapNightLive`; otherwise the link status, with the terminal states
 * collapsed (Skipped and Corrupted are both "not played to the end": `skipped`).
 */
function toListStatus(session: GameNightSessionDto): GameNightActiveSession['status'] {
  if (session.isLive === true) return 'in_progress';

  switch (session.status) {
    case 'Pending':
      return 'pending';
    case 'InProgress':
      return 'in_progress';
    case 'Completed':
      return 'completed';
    case 'Skipped':
      return 'skipped';
    case 'Corrupted':
      return 'skipped';
    default: {
      const exhaustive: never = session.status;
      return exhaustive;
    }
  }
}

export function mapNightSessions(dto: GameNightLiveDto): GameNightActiveSession[] {
  return [...dto.sessions]
    .sort((a, b) => a.playOrder - b.playOrder)
    .map(session => ({
      id: session.sessionId,
      // The read has no id for the link row: the tracking session id is the stable identity of the sitting.
      gameNightSessionId: session.sessionId,
      gameId: session.gameId,
      gameTitle: session.gameTitle,
      playOrder: session.playOrder,
      status: toListStatus(session),
      winnerId: session.winnerId ?? undefined,
      startedAt: session.startedAt ?? undefined,
      completedAt: session.completedAt ?? undefined,
      liveSessionId: session.liveSessionId ?? undefined,
    }));
}
