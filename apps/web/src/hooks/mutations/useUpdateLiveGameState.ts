/**
 * useUpdateLiveGameState — #3025 L1 host/participant mutation to replace the opaque
 * live game-state (`PUT /live-sessions/{id}/game-state`). The state is game-agnostic
 * JSON at L1 (per-game typing = L2). On success, invalidates the live-session query so
 * the DTO re-hydrates; live consumers also get the `session:game-state` SSE event.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';

import { liveSessionKeys } from '@/hooks/queries/useLiveSession';
import { api } from '@/lib/api';

export function useUpdateLiveGameState(sessionId: string): UseMutationResult<void, Error, unknown> {
  const queryClient = useQueryClient();
  return useMutation<void, Error, unknown>({
    mutationFn: (state: unknown) => api.liveSessions.updateGameState(sessionId, state),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: liveSessionKeys.detail(sessionId) });
    },
  });
}
