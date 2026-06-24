/**
 * useCompleteLiveSession — mutation hook for POST /api/v1/live-sessions/{id}/complete
 * Issue #2503: endgame trigger for host.
 */
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { liveSessionKeys } from '@/hooks/queries/useLiveSession';
import { api } from '@/lib/api';

export function useCompleteLiveSession(sessionId: string) {
  const queryClient = useQueryClient();

  return useMutation<void, Error, void>({
    mutationFn: () => api.liveSessions.completeSession(sessionId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: liveSessionKeys.detail(sessionId) });
    },
  });
}
