'use client';

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { UserGameKbStatus } from '@/lib/api/clients/knowledgeBaseClient';

type CoverageLevel = UserGameKbStatus['coverageLevel'];

interface UseGameKbStatusResult {
  isIndexed: boolean;
  documentCount: number;
  coverageLevel: CoverageLevel;
  suggestedQuestions: readonly string[];
  isLoading: boolean;
  error: Error | null;
}

const EMPTY_SUGGESTED_QUESTIONS: readonly string[] = Object.freeze([]);

/**
 * Hook to check whether a game has an indexed knowledge base (PDF + RAG).
 *
 * Returns safe defaults (`isIndexed=false`, `documentCount=0`, `coverageLevel='None'`,
 * empty `suggestedQuestions`) when `gameId` is null, when the backend returns null,
 * or while fetching.
 *
 * Used by:
 *  - Game Night wizard soft "KB available" hint (F1)
 *  - Library game detail page & game-table knowledge zone
 */
export function useGameKbStatus(gameId: string | null | undefined): UseGameKbStatusResult {
  const query = useQuery({
    queryKey: ['game-kb-status', gameId],
    queryFn: () => api.knowledgeBase.getUserGameKbStatus(gameId as string),
    enabled: gameId != null,
    staleTime: 60_000,
  });

  return {
    isIndexed: query.data?.isIndexed ?? false,
    documentCount: query.data?.documentCount ?? 0,
    coverageLevel: query.data?.coverageLevel ?? 'None',
    suggestedQuestions: query.data?.suggestedQuestions ?? EMPTY_SUGGESTED_QUESTIONS,
    isLoading: query.isLoading,
    error: query.error as Error | null,
  };
}
