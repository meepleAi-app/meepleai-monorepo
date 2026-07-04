'use client';

/**
 * useGameNightLive — #2633 Slice B (LD-1, LD-12).
 *
 * Single-endpoint React Query loader over `GET /game-nights/{id}/live`
 * (`api.gameNights.getLive`). It wires the PURE `mapNightLiveToViewModel` through
 * React Query `select`, returning `UseQueryResult<NightLiveViewModel, Error>` — the
 * raw-UseQueryResult convention of `useGameNightConflictCheck`, so the view keeps
 * its full isLoading/isError/error branching (the LD-10 error taxonomy).
 *
 * The clock is injected from `useNow(60_000)` so elapsed/actual stay live while the
 * mapper remains deterministic. `retry: false` mirrors the sibling game-night hook.
 */

import { useCallback } from 'react';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useNow } from '@/hooks/useNow';
import { api } from '@/lib/api';
import type { GameNightLiveDto } from '@/lib/api/schemas/game-nights.schemas';
import { mapNightLiveToViewModel, type NightLiveViewModel } from '@/lib/game-nights/mapNightLive';

/** Query key factory for the night-live read (distinct from the lean game-night keys). */
export const gameNightLiveKeys = {
  all: ['game-nights', 'live'] as const,
  detail: (id: string) => [...gameNightLiveKeys.all, id] as const,
};

export function useGameNightLive(
  id: string,
  enabled: boolean = true
): UseQueryResult<NightLiveViewModel, Error> {
  const now = useNow(60_000);

  // Stable per `now` tick: `select` re-runs only when the data or the clock
  // changes, not on every render.
  const select = useCallback(
    (dto: GameNightLiveDto): NightLiveViewModel => mapNightLiveToViewModel(dto, now),
    [now]
  );

  return useQuery({
    queryKey: gameNightLiveKeys.detail(id),
    queryFn: () => api.gameNights.getLive(id),
    select,
    enabled: enabled && !!id,
    staleTime: 30_000,
    retry: false,
  });
}
