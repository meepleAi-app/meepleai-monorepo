'use client';

/**
 * useGameKbStatus — KB-04
 *
 * Fetches user-facing knowledge base status for a game, including
 * coverage level and suggested questions.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';

export function useGameKbStatus(gameId: string | undefined) {
  return useQuery({
    queryKey: ['game-kb-status', gameId],
    queryFn: () => api.knowledgeBase.getUserGameKbStatus(gameId!),
    enabled: !!gameId,
    staleTime: 5 * 60 * 1000, // 5 min
  });
}
