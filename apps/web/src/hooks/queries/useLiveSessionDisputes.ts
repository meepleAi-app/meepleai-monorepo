/**
 * useLiveSessionDisputes — TanStack Query loader that hydrates the Arbitro tab's dispute history.
 *
 * Issue #3391 (finding C8): the dispute history was SignalR-only, so reopening/reloading a session
 * showed no prior verdicts until a new SignalR event arrived. This hook loads the persisted history
 * from REST on mount and syncs it into the live-session store (`setDisputes`), the same store slice
 * that `useSignalRSession` keeps live via `addDispute`. The disputes key nests under
 * liveSessionKeys.detail(id) so dispute mutations can invalidate it.
 */
import { useEffect } from 'react';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { liveSessionKeys } from '@/hooks/queries/useLiveSession';
import { api } from '@/lib/api';
import type { RuleDisputeDto } from '@/lib/api/schemas/improvvisata.schemas';
import { useLiveSessionStore, type RuleDispute } from '@/lib/stores/live-session-store';

export const liveSessionDisputeKeys = {
  detail: (id: string) => [...liveSessionKeys.detail(id), 'disputes'] as const,
};

/** Maps the REST DTO to the store's RuleDispute model. v2 badge fields arrive via SignalR only. */
function toRuleDispute(dto: RuleDisputeDto): RuleDispute {
  return {
    id: dto.id,
    description: dto.description,
    verdict: dto.verdict,
    ruleReferences: dto.ruleReferences,
    raisedByPlayerName: dto.raisedByPlayerName,
    timestamp: dto.timestamp,
  };
}

export function useLiveSessionDisputes(
  sessionId: string,
  enabled: boolean = true
): UseQueryResult<RuleDisputeDto[], Error> {
  // Zustand action selector — stable reference across renders, safe as an effect dependency.
  const setDisputes = useLiveSessionStore(s => s.setDisputes);

  const query = useQuery({
    queryKey: liveSessionDisputeKeys.detail(sessionId),
    queryFn: () => api.liveSessions.getDisputes(sessionId),
    enabled: enabled && !!sessionId,
    staleTime: 30 * 1000,
  });

  useEffect(() => {
    if (!query.data) return;
    // Merge the authoritative REST snapshot with any live (SignalR-appended) disputes not yet
    // reflected in it, dedup by id, so a stale-cache remount within staleTime never drops a
    // dispute newer than the last fetch (#3391 review). On id collision the store entry wins:
    // it carries the live v2 badge fields (confidence/outcome/votes) the REST summary omits.
    // `addDispute` remains the live append path; this only reconciles on (re)hydration.
    const byId = new Map<string, RuleDispute>();
    for (const d of query.data.map(toRuleDispute)) byId.set(d.id, d);
    for (const d of useLiveSessionStore.getState().disputes) byId.set(d.id, d);
    const merged = [...byId.values()].sort((a, b) => a.timestamp.localeCompare(b.timestamp));
    setDisputes(merged);
  }, [query.data, setDisputes]);

  return query;
}
