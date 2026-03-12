/**
 * React Query hook for cross-BC RAG readiness status.
 *
 * Auto-refreshes every 5s while documents are processing,
 * stops polling when all documents reach a terminal state.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { GameRagReadiness } from '@/lib/api/schemas/rag-setup.schemas';

export const ragReadinessKeys = {
  all: ['ragReadiness'] as const,
  byGame: (gameId: string) => [...ragReadinessKeys.all, gameId] as const,
};

export function useGameRagReadiness(gameId: string, enabled = true) {
  return useQuery<GameRagReadiness | null, Error>({
    queryKey: ragReadinessKeys.byGame(gameId),
    queryFn: () => api.sharedGames.getGameRagReadiness(gameId),
    enabled: enabled && !!gameId,
    staleTime: 10_000, // 10s
    refetchInterval: (query) => {
      const data = query.state.data;
      // Auto-refresh while documents are processing
      if (data?.processingDocuments && data.processingDocuments > 0) {
        return 5_000; // 5s while processing
      }
      return false; // Stop polling when stable
    },
  });
}
