'use client';

/**
 * useNightLiveDiary — #2633 Slice C2.
 *
 * React Query loader over the canonical, participant-guarded diary read
 * (`GET /game-nights/{id}/diary` via `gameNightSessionClient.getDiary` — the single
 * client method for that endpoint, panel must-fix #2). `select` runs the per-row
 * resilient parse so one malformed entry cannot blow up the whole timeline (D8).
 *
 * Deliberately NOT named `useGameNightDiary`: that name is the pre-existing Zustand+SSE
 * hook (game-night session flow) with its own consumers — this is the RQ loader for the
 * night-live hub, composed with `useGameNightLive` in `NightLiveClientView`.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { gameNightSessionClient } from '@/lib/api/clients/gameNightSessionClient';
import {
  parseGameNightDiaryResilient,
  type GameNightDiaryDto,
} from '@/lib/api/schemas/game-nights.schemas';

/** Query key factory — distinct from the live key so each read caches/invalidates on its own. */
export const nightLiveDiaryKeys = {
  all: ['game-nights', 'diary'] as const,
  detail: (id: string) => [...nightLiveDiaryKeys.all, id] as const,
};

export function useNightLiveDiary(
  id: string,
  enabled: boolean = true
): UseQueryResult<GameNightDiaryDto, Error> {
  return useQuery({
    queryKey: nightLiveDiaryKeys.detail(id),
    queryFn: () => gameNightSessionClient.getDiary(id),
    select: parseGameNightDiaryResilient,
    enabled: enabled && !!id,
    staleTime: 30_000,
    retry: false,
  });
}
