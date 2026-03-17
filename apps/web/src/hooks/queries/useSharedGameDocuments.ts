'use client';

/**
 * useSharedGameDocuments — fetch active KB documents for a shared game (user-facing)
 *
 * Calls GET /api/v1/shared-games/{gameId}/documents.
 * Access is granted when the game is RAG-public or the user has it in their library.
 * Returns an empty array when access is denied (not a hard error).
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { SharedGameDocument } from '@/lib/api/schemas/shared-games.schemas';

export const sharedGameDocumentKeys = {
  all: ['shared-game-documents'] as const,
  byGame: (gameId: string) => ['shared-game-documents', gameId] as const,
} as const;

/**
 * Fetch active documents for a shared game accessible to the authenticated user.
 *
 * @param gameId - Game UUID, or null/undefined to skip the query
 * @returns React Query result with the list of active documents
 *
 * @example
 * ```tsx
 * const { data: documents = [], isLoading } = useSharedGameDocuments(game.id);
 * ```
 */
export function useSharedGameDocuments(gameId: string | null | undefined) {
  return useQuery<SharedGameDocument[], Error>({
    queryKey: sharedGameDocumentKeys.byGame(gameId ?? ''),
    queryFn: () => api.sharedGames.getDocumentsForUser(gameId!),
    enabled: !!gameId,
    staleTime: 60_000, // 1 minute — documents don't change frequently
    gcTime: 5 * 60_000, // 5 minutes cache
  });
}
