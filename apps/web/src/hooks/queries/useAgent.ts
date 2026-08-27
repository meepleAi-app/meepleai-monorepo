/**
 * useAgent — parent hook for `/agents/[id]` orchestrator (Wave C.2, Issue #581).
 *
 * Wraps `api.agents.getById` in TanStack Query. Accepts `string | null` so the
 * orchestrator can pass the normalized agentId directly without a null-guard:
 *   - agentId === null  → query is disabled (Cell 1 contract)
 *   - agentId === ''    → query is disabled (defensive — should not occur post-norm)
 *   - agentId valid str → query enabled, fetches AgentDto | null from backend
 *
 * Phase 0.5 contract sez. 2.2 — parent hook gating.
 * Mirror of Wave C.1 useLibraryGameDetail (disabled via enabled flag, not throw).
 *
 * Refs #581.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import { ApiError } from '@/lib/api/core/errors';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

// ─── Query key factory ──────────────────────────────────────────────────────

export const agentKeys = {
  all: ['agents'] as const,
  detail: (id: string) => ['agents', 'detail', id] as const,
};

// ─── Hook ──────────────────────────────────────────────────────────────────

/**
 * Fetches a single agent by ID. Returns `null` when the agent is not found
 * (backend returns 404 → client returns null, NOT throws).
 *
 * @param agentId - Normalized agent UUID or null (null → query disabled)
 * @returns TanStack Query result with AgentDto | null
 */
export function useAgent(agentId: string | null): UseQueryResult<AgentDto | null> {
  return useQuery({
    queryKey: agentKeys.detail(agentId ?? ''),
    queryFn: async (): Promise<AgentDto | null> => {
      // Safety net: if somehow enabled fires with null, throw a clear error
      if (!agentId) throw new Error('agentId is required');

      try {
        return await api.agents.getById(agentId);
      } catch (err) {
        // #3852 — un agente inesistente non e' un guasto. Lasciando propagare il 404,
        // `isError` diventava true e la macchina a stati mostrava «qualcosa e' andato storto»
        // invece di «questo agente non esiste»: due situazioni diverse per chi legge, con
        // reazioni diverse — tornare indietro, oppure credere che il sistema sia rotto.
        //
        // `null` mappa su `hasData: false`, che la FSM traduce gia' in 'not-found'.
        // Stesso schema di useLiveSession e useLiveSessionPhases.
        if (err instanceof ApiError && err.statusCode === 404) return null;
        throw err;
      }
    },
    enabled: !!agentId,
    retry: false,
    staleTime: 60_000,
  });
}
