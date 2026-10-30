/**
 * useCompleteLiveSession — mutation hook for POST /api/v1/live-sessions/{id}/complete
 * Issue #2503: endgame trigger for host.
 */
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { liveSessionKeys } from '@/hooks/queries/useLiveSession';
import { api } from '@/lib/api';
import { gameNightLiveKeys } from '@/lib/game-nights/hooks/useGameNightLive';
import { nightLiveDiaryKeys } from '@/lib/game-nights/hooks/useNightLiveDiary';

export function useCompleteLiveSession(sessionId: string) {
  const queryClient = useQueryClient();

  return useMutation<void, Error, void>({
    mutationFn: () => api.liveSessions.completeSession(sessionId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: liveSessionKeys.detail(sessionId) });
    },
    // #4752 (the night → live game bridge): a game played from a night closes that night's sitting too, and
    // the night's reads (the detail page's games list, the night-live page, its diary) must follow — or
    // «Concludi serata» stays off until their 30 s of staleness run out. It runs whether the call succeeded
    // OR not: «Termina partita» repeated on a game that is already completed answers 409, but it is also
    // what repairs a night whose first follow-up failed, and the user must see the night as it now is. A
    // game that belongs to no night has no such queries cached: nothing refetches.
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: gameNightLiveKeys.all });
      void queryClient.invalidateQueries({ queryKey: nightLiveDiaryKeys.all });
    },
  });
}
