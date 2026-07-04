'use client';

/**
 * useStartNextGame — #2633 WS1 DEC-10.
 *
 * Mutation that starts the next planned game (POST /game-nights/{id}/sessions). On success it
 * invalidates the night-live query so the badge/current-game re-derive from the read model
 * (single source of truth — no optimistic flip). A 409 with code MAX_LIVE_SESSIONS_EXCEEDED is
 * the server-authoritative signal that a session is already live; the view discriminates via
 * `isMaxLiveBlockedError` to mount the blocked modal.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import { ConflictError } from '@/lib/api/core/errors';
import type { StartGameNightSessionResult } from '@/lib/api/schemas/game-nights.schemas';

import { gameNightLiveKeys } from './useGameNightLive';

export const MAX_LIVE_SESSIONS_EXCEEDED = 'MAX_LIVE_SESSIONS_EXCEEDED';

/** DEC-10: true iff the error is the max-1-live 409 (→ blocked modal, not a generic toast). */
export function isMaxLiveBlockedError(error: unknown): boolean {
  return error instanceof ConflictError && error.code === MAX_LIVE_SESSIONS_EXCEEDED;
}

export interface StartNextGameVars {
  readonly gameId: string;
  readonly gameTitle: string;
}

export function useStartNextGame(
  gameNightId: string
): UseMutationResult<StartGameNightSessionResult, Error, StartNextGameVars> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ gameId, gameTitle }: StartNextGameVars) =>
      api.gameNights.startNextGame(gameNightId, gameId, gameTitle),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: gameNightLiveKeys.detail(gameNightId) });
    },
    retry: false,
  });
}
