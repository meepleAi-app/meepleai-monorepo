'use client';

/**
 * useGameNightSessions — #4752 (the night → live game bridge).
 *
 * The sessions of a night, for the night DETAIL page: status, order, and the live game of each sitting
 * (`liveSessionId`). It reads `GET /game-nights/{id}/live` through the SAME query key as
 * `useGameNightLive`, so the two share one cached response and the one invalidation the start/complete
 * mutations already do — the page does not fetch twice and cannot disagree with the night-live page.
 *
 * The read admits only the organizer and the invited players (403 for anyone else): for them the list is
 * simply empty, and `retry: false` keeps the refusal from being repeated.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import { gameNightLiveKeys } from '@/lib/game-nights/hooks/useGameNightLive';
import { mapNightSessions } from '@/lib/game-nights/mapNightSessions';
import type { GameNightActiveSession } from '@/stores/game-night/types';

export function useGameNightSessions(
  id: string,
  enabled: boolean = true
): UseQueryResult<GameNightActiveSession[], Error> {
  return useQuery({
    queryKey: gameNightLiveKeys.detail(id),
    queryFn: () => api.gameNights.getLive(id),
    select: mapNightSessions,
    enabled: enabled && !!id,
    staleTime: 30_000,
    retry: false,
  });
}
