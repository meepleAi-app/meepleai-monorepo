/**
 * useGameAgentDocuments - User-scoped Agent Document Selection Hooks
 *
 * React Query hooks for fetching and updating per-game agent document
 * selections from the user library endpoints.
 * Used by the DocumentSelectionPanel in the game agent configuration flow.
 */

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { AvailableDocuments } from '@/lib/api/schemas/agent-documents.schemas';

// ========== Query Keys ==========

export const gameAgentDocumentKeys = {
  all: ['gameAgentDocuments'] as const,
  forGame: (gameId: string) => [...gameAgentDocumentKeys.all, gameId] as const,
};

// ========== Query Hook ==========

/**
 * Fetch available documents for a library game's agent
 *
 * Returns base documents (from shared catalog) and additional documents
 * (user's private PDFs), each with an isSelected flag.
 *
 * @param gameId - The library game ID
 * @param enabled - Whether the query should execute (default: true)
 */
export function useGameAgentDocuments(gameId: string, enabled: boolean = true) {
  return useQuery({
    queryKey: gameAgentDocumentKeys.forGame(gameId),
    queryFn: () => api.agentDocuments.getAvailableDocuments(gameId),
    enabled,
    staleTime: 30_000,
  });
}

// ========== Mutation Hook ==========

/**
 * Update selected documents for a library game's agent
 *
 * Performs optimistic updates on the local cache, rolling back
 * on error and invalidating on settle.
 *
 * @param gameId - The library game ID
 */
export function useUpdateGameAgentDocuments(gameId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (selectedDocumentIds: string[]) =>
      api.agentDocuments.updateSelectedDocuments(gameId, selectedDocumentIds),
    onMutate: async newSelection => {
      await queryClient.cancelQueries({
        queryKey: gameAgentDocumentKeys.forGame(gameId),
      });
      const previous = queryClient.getQueryData<AvailableDocuments>(
        gameAgentDocumentKeys.forGame(gameId)
      );
      if (previous) {
        const selectionSet = new Set(newSelection);
        queryClient.setQueryData<AvailableDocuments>(gameAgentDocumentKeys.forGame(gameId), {
          ...previous,
          baseDocuments: previous.baseDocuments.map(d => ({
            ...d,
            isSelected: selectionSet.has(d.documentId),
          })),
          additionalDocuments: previous.additionalDocuments.map(d => ({
            ...d,
            isSelected: selectionSet.has(d.documentId),
          })),
        });
      }
      return { previous };
    },
    onError: (_err, _vars, context) => {
      if (context?.previous) {
        queryClient.setQueryData(gameAgentDocumentKeys.forGame(gameId), context.previous);
      }
    },
    onSettled: () => {
      void queryClient.invalidateQueries({
        queryKey: gameAgentDocumentKeys.forGame(gameId),
      });
    },
  });
}
