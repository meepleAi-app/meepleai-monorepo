/**
 * usePrivateGameKbStatus - TanStack Query hook for private game KB/RAG status
 *
 * Issue #3664: Private game PDF support — KB readiness polling.
 *
 * Polls /api/v1/private-games/{id}/kb-status every 3 seconds and stops
 * automatically when the status reaches a terminal state (Completed | Failed).
 */

import { useRef } from 'react';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { KnowledgeBaseStatus } from '@/lib/api/schemas/knowledge-base.schemas';

export const privateGameKbStatusKeys = {
  all: ['private-game-kb-status'] as const,
  byGame: (gameId: string) => [...privateGameKbStatusKeys.all, gameId] as const,
};

const TERMINAL_STATUSES = new Set<KnowledgeBaseStatus['status']>(['Completed', 'Failed']);

/** Returns polling interval based on unchanged cycle count (adaptive back-off) */
export function getAdaptiveInterval(cycleCount: number): number {
  if (cycleCount >= 20) return 10_000;
  if (cycleCount >= 10) return 5_000;
  return 3_000;
}

/**
 * Hook to poll the KB status for a private game.
 *
 * @param privateGameId - UUID of the private game (leave empty/undefined to disable)
 * @returns UseQueryResult with KnowledgeBaseStatus data
 */
export function usePrivateGameKbStatus(
  privateGameId: string | null | undefined
): UseQueryResult<KnowledgeBaseStatus, Error> {
  const prevStatusRef = useRef<string | null>(null);
  const cycleCountRef = useRef(0);

  return useQuery({
    queryKey: privateGameKbStatusKeys.byGame(privateGameId ?? ''),
    queryFn: () =>
      api.knowledgeBase.getPrivateGameKbStatus(privateGameId!).then(data => {
        if (!data) throw new Error('No KB status data returned');
        return data;
      }),
    enabled: !!privateGameId,
    refetchInterval: query => {
      const status = query.state.data?.status;
      if (status && TERMINAL_STATUSES.has(status)) return false;

      // Adaptive interval: slow down when status is not changing
      if (status && status === prevStatusRef.current) {
        cycleCountRef.current += 1;
      } else {
        cycleCountRef.current = 0;
        prevStatusRef.current = status ?? null;
      }

      return getAdaptiveInterval(cycleCountRef.current);
    },
    refetchOnWindowFocus: false,
    staleTime: 0,
    retry: false,
  });
}
