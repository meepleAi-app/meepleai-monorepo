/**
 * useLiveSessionDiary — TanStack Query loader for the session diary timeline.
 *
 * Issue #2575: hydrates historical diary entries on load. Previously the diary was SSE-passive
 * only, so reopening a session showed no prior entries until new SSE events arrived. The diary
 * key nests under liveSessionKeys.detail(id), so the useAddDiaryEntry mutation's
 * invalidateQueries({ queryKey: liveSessionKeys.detail(id) }) refreshes this query too.
 */
import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { liveSessionKeys } from '@/hooks/queries/useLiveSession';
import { api } from '@/lib/api';
import type { LiveSessionDiaryEntryDto } from '@/lib/api/schemas/live-sessions.schemas';

export const liveSessionDiaryKeys = {
  detail: (id: string) => [...liveSessionKeys.detail(id), 'diary'] as const,
};

export function useLiveSessionDiary(
  sessionId: string,
  enabled: boolean = true
): UseQueryResult<LiveSessionDiaryEntryDto[], Error> {
  return useQuery({
    queryKey: liveSessionDiaryKeys.detail(sessionId),
    queryFn: () => api.liveSessions.getDiary(sessionId),
    enabled: enabled && !!sessionId,
    staleTime: 30 * 1000,
  });
}
