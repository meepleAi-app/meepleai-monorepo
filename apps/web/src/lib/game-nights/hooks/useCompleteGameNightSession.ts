'use client';

/**
 * useCompleteGameNightSession — #2634 C4.
 *
 * Mutation that completes the current live game (POST /game-nights/{id}/sessions/complete via the
 * single client method `gameNightSessionClient.completeSession`). On success it invalidates the
 * live AND diary reads (the game flips to completed with its winner, and a completion diary event
 * appears). `retry: false` so a 409/403 surfaces once; the view discriminates ForbiddenError (403,
 * non-organizer) vs ConflictError (409, no live session / winner not a participant) for distinct
 * feedback and keeps the action pending-locked while in flight.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';

import { gameNightSessionClient } from '@/lib/api/clients/gameNightSessionClient';

import { gameNightLiveKeys } from './useGameNightLive';
import { nightLiveDiaryKeys } from './useNightLiveDiary';

export interface CompleteGameNightSessionVars {
  /** A tracking-Session Participant.Id, or undefined to complete with no winner. */
  readonly winnerId?: string;
}

export function useCompleteGameNightSession(
  gameNightId: string
): UseMutationResult<void, Error, CompleteGameNightSessionVars> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ winnerId }: CompleteGameNightSessionVars) =>
      gameNightSessionClient.completeSession(gameNightId, winnerId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: gameNightLiveKeys.detail(gameNightId) });
      void queryClient.invalidateQueries({ queryKey: nightLiveDiaryKeys.detail(gameNightId) });
    },
    retry: false,
  });
}
