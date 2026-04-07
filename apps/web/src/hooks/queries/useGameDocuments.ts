'use client';

/**
 * useKbGameDocuments — fetch KB documents for a game (user-facing)
 *
 * Calls GET /api/v1/knowledge-base/{gameId}/documents.
 * Returns indexed documents with category and version metadata.
 *
 * NOTE: Distinct from useGameDocuments in useGames.ts which fetches
 * PDF processing documents via the Games client. This hook fetches
 * KB-indexed documents with category and version info.
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';

export const kbGameDocumentKeys = {
  all: ['kb-game-documents'] as const,
  byGame: (gameId: string) => ['kb-game-documents', gameId] as const,
} as const;

/**
 * Fetch KB documents for a game.
 *
 * @param gameId - Game UUID, or undefined to skip the query
 * @param enabled - Additional enable flag (default: true)
 * @returns React Query result with the list of KB game documents
 *
 * @example
 * ```tsx
 * const { data: documents = [], isLoading } = useKbGameDocuments(game.id);
 * ```
 */
export function useKbGameDocuments(gameId: string | undefined, enabled: boolean = true) {
  return useQuery<GameDocument[], Error>({
    queryKey: kbGameDocumentKeys.byGame(gameId ?? ''),
    queryFn: () => api.knowledgeBase.getGameDocuments(gameId!),
    enabled: enabled && !!gameId,
    staleTime: 5 * 60_000, // 5 minutes — documents don't change frequently
    gcTime: 10 * 60_000, // 10 minutes cache
  });
}
