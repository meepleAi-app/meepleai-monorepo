'use client';

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { UserGameKbStatus } from '@/lib/api/clients/knowledgeBaseClient';

type CoverageLevel = UserGameKbStatus['coverageLevel'];

interface UseGameKbStatusResult {
  isIndexed: boolean;
  documentCount: number;
  coverageLevel: CoverageLevel;
  isLoading: boolean;
  error: Error | null;
}

/**
 * Hook to check whether a game has an indexed knowledge base (PDF + RAG).
 *
 * Returns safe defaults (`isIndexed=false`, `documentCount=0`, `coverageLevel='None'`)
 * when `gameId` is null, when the backend returns null, or while fetching.
 *
 * Used by the Game Night wizard to surface a soft "KB available" hint
 * (MVP hardening F1 — PDF-aware filter).
 */
export function useGameKbStatus(gameId: string | null): UseGameKbStatusResult {
  const query = useQuery({
    // 'normalized' suffix avoids cache-key collision with the legacy
    // hook at @/hooks/use-game-kb-status which uses ['game-kb-status', gameId]
    // and a different staleTime (5 minutes vs our 60s).
    queryKey: ['game-kb-status', 'normalized', gameId],
    queryFn: () => api.knowledgeBase.getUserGameKbStatus(gameId as string),
    enabled: gameId !== null,
    staleTime: 60_000,
  });

  return {
    isIndexed: query.data?.isIndexed ?? false,
    documentCount: query.data?.documentCount ?? 0,
    coverageLevel: query.data?.coverageLevel ?? 'None',
    isLoading: query.isLoading,
    error: query.error as Error | null,
  };
}
