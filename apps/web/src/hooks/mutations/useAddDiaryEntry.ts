/**
 * useAddDiaryEntry — mutation hook for POST /api/v1/live-sessions/{id}/diary
 * Issue #2575: FE diary write-path (the SP3 #2570 BE endpoint is text-only).
 *
 * No optimistic actionLog append: the new entry arrives over the existing session:diary SSE
 * stream and is folded into actionLog by compose-session-live-state.applyDiary, so an optimistic
 * append would double-render.
 */
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { liveSessionKeys } from '@/hooks/queries/useLiveSession';
import { api } from '@/lib/api';

export function useAddDiaryEntry(sessionId: string) {
  const queryClient = useQueryClient();

  return useMutation<string, Error, { text: string }>({
    mutationFn: ({ text }) => api.liveSessions.addDiary(sessionId, { text }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: liveSessionKeys.detail(sessionId) });
    },
  });
}
