import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { fetchKbDocConsumingAgents } from '@/lib/api/admin-kb-used-by';
import type { KbDocConsumingAgent } from '@/lib/api/schemas/kb-consuming-agents.schemas';

export const kbDocConsumingAgentsKeys = {
  all: ['kb', 'doc', 'consuming-agents'] as const,
  byId: (docId: string) => [...kbDocConsumingAgentsKeys.all, docId] as const,
};

export interface UseKbDocConsumingAgentsOptions {
  readonly docId: string | null | undefined;
  readonly enabled?: boolean;
}

/**
 * TanStack Query hook listing agents that consume a given document.
 * No polling — the association is static (changes only on agent edit).
 * Backend contract: GET /api/v1/admin/kb/docs/{docId}/agents.
 * Issue #1651.
 */
export function useKbDocConsumingAgents(
  options: UseKbDocConsumingAgentsOptions
): UseQueryResult<KbDocConsumingAgent[], Error> {
  const { docId, enabled = true } = options;
  const isValid = typeof docId === 'string' && docId.length > 0;

  return useQuery<KbDocConsumingAgent[], Error>({
    queryKey: isValid ? kbDocConsumingAgentsKeys.byId(docId) : kbDocConsumingAgentsKeys.all,
    queryFn: async () => (isValid ? fetchKbDocConsumingAgents(docId) : []),
    enabled: enabled && isValid,
    staleTime: 30_000,
  });
}
