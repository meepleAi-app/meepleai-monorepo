/**
 * useAddLivePlayer — mutation hook for POST /api/v1/live-sessions/{id}/players
 *
 * Issue #2505: adds a player (guest or registered user) to a live session.
 * Returns the new playerId on success and invalidates the session detail query.
 */
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { liveSessionKeys } from '@/hooks/queries/useLiveSession';
import { api } from '@/lib/api';
import { ApiError } from '@/lib/api/core/errors';
import type { AddPlayerRequest } from '@/lib/api/schemas/live-sessions.schemas';

/**
 * Mutation hook to add a player to a live session.
 *
 * @param sessionId - LiveGameSession ID
 */
export function useAddLivePlayer(sessionId: string) {
  const queryClient = useQueryClient();

  return useMutation<string, ApiError | Error, AddPlayerRequest>({
    mutationFn: (req: AddPlayerRequest) => api.liveSessions.addPlayer(sessionId, req),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: liveSessionKeys.detail(sessionId) });
    },
  });
}
