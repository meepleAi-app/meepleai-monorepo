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
    queryFn: () => {
      // Safety net: if somehow enabled fires with null, throw a clear error
      if (!agentId) throw new Error('agentId is required');
      return api.agents.getById(agentId);
    },
    enabled: !!agentId,
    retry: false,
    staleTime: 60_000,
  });
}
