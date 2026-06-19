'use client';

import { useQuery } from '@tanstack/react-query';

import type { PlayerEntry } from '@/components/play-records/SessionCreateForm';
import { api } from '@/lib/api';
import type { SessionCreateForm as SessionCreateFormData } from '@/lib/api/schemas/play-records.schemas';

export interface GameNightPrefill {
  initialValues: Partial<SessionCreateFormData>;
  initialPlayers: PlayerEntry[];
}

/**
 * #2348: Builds create-form initial values from a completed GameNight
 * (deep-link `/play-records/new?gameNightId=...`). Maps date/location/game +
 * the Accepted RSVP roster. RSVP/game failures degrade gracefully —
 * only the gamenight fetch gates `isError`.
 */
export function useGameNightPrefill(gameNightId: string | null) {
  const enabled = !!gameNightId;

  const nightQ = useQuery({
    queryKey: ['game-nights', 'detail', gameNightId],
    queryFn: () => api.gameNights.getById(gameNightId!),
    enabled,
    retry: false,
  });

  const rsvpsQ = useQuery({
    queryKey: ['game-nights', 'rsvps', gameNightId],
    queryFn: () => api.gameNights.getRsvps(gameNightId!),
    enabled,
    retry: false,
  });

  const firstGameId = nightQ.data?.gameIds?.[0];
  const gameQ = useQuery({
    queryKey: ['games', 'detail', firstGameId],
    queryFn: () => api.games.getById(firstGameId!),
    enabled: !!firstGameId,
    retry: false,
  });

  const isLoading =
    enabled && (nightQ.isLoading || rsvpsQ.isLoading || (!!firstGameId && gameQ.isLoading));
  const isError = enabled && nightQ.isError;

  // Only treat the game as successfully loaded when the data is actually
  // present. If the fetch errored or was never triggered (no firstGameId),
  // we fall back to freeform so the user can enter a name manually rather
  // than surfacing a broken catalog state with an empty game name.
  const hasGame = !!gameQ.data;

  const prefill: GameNightPrefill | null = nightQ.data
    ? {
        initialValues: {
          gameType: hasGame ? 'catalog' : 'freeform',
          gameId: hasGame ? firstGameId : undefined,
          // api.games.getById returns Game which has a `title` field
          gameName: gameQ.data?.title ?? '',
          sessionDate: new Date(nightQ.data.scheduledAt),
          location: nightQ.data.location ?? '',
        },
        initialPlayers: (rsvpsQ.data ?? [])
          .filter(r => r.status === 'Accepted')
          .map(r => ({ id: r.userId, name: r.userName, score: '' })),
      }
    : null;

  return { prefill, isLoading, isError, enabled };
}
