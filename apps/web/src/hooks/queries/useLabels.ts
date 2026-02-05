/**
 * useLabels - TanStack Query hooks for game labels (Issue #3514)
 *
 * Provides automatic caching and mutations for game labels:
 * - Fetch all available labels (predefined + custom)
 * - Fetch labels for a specific game
 * - Add/remove labels from games
 * - Create/delete custom labels
 * - Optimistic updates for instant UI feedback
 */

import {
  useQuery,
  useMutation,
  useQueryClient,
  UseQueryResult,
  UseMutationResult,
} from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { LabelDto, CreateCustomLabelRequest } from '@/lib/api/schemas/library.schemas';

import { libraryKeys } from './useLibrary';

/**
 * Query key factory for label queries
 */
export const labelKeys = {
  all: ['labels'] as const,
  list: () => [...labelKeys.all, 'list'] as const,
  gameLabels: (gameId: string) => [...labelKeys.all, 'game', gameId] as const,
};

/**
 * Hook to fetch all available labels (predefined + user custom)
 *
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with array of labels
 */
export function useLabels(enabled: boolean = true): UseQueryResult<LabelDto[], Error> {
  return useQuery({
    queryKey: labelKeys.list(),
    queryFn: async (): Promise<LabelDto[]> => {
      return api.library.getLabels();
    },
    enabled,
    staleTime: 5 * 60 * 1000, // Labels don't change often (5min)
  });
}

/**
 * Hook to fetch labels assigned to a specific game
 *
 * @param gameId - Game UUID in user's library
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with array of labels
 */
export function useGameLabels(
  gameId: string,
  enabled: boolean = true
): UseQueryResult<LabelDto[], Error> {
  return useQuery({
    queryKey: labelKeys.gameLabels(gameId),
    queryFn: async (): Promise<LabelDto[]> => {
      return api.library.getGameLabels(gameId);
    },
    enabled: enabled && !!gameId,
    staleTime: 2 * 60 * 1000, // Game labels can change more frequently (2min)
  });
}

/**
 * Hook to add a label to a game with optimistic update
 *
 * Issue #3514: Optimistic updates for instant feedback
 * - Immediately adds label to game labels cache
 * - Rolls back on error
 * - Refetches on settlement for consistency
 *
 * @returns UseMutationResult for adding label
 */
export function useAddLabelToGame(): UseMutationResult<
  LabelDto,
  Error,
  { gameId: string; label: LabelDto }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ gameId, label }: { gameId: string; label: LabelDto }) => {
      return api.library.addLabelToGame(gameId, label.id);
    },
    onMutate: async ({ gameId, label }) => {
      // Cancel any outgoing refetches
      await queryClient.cancelQueries({ queryKey: labelKeys.gameLabels(gameId) });

      // Snapshot previous value
      const previousLabels = queryClient.getQueryData<LabelDto[]>(labelKeys.gameLabels(gameId));

      // Optimistically add the label
      queryClient.setQueryData<LabelDto[]>(labelKeys.gameLabels(gameId), (old) => {
        if (!old) return [label];
        // Avoid duplicates
        if (old.some((l) => l.id === label.id)) return old;
        return [...old, label];
      });

      return { previousLabels };
    },
    onError: (_err, { gameId }, context) => {
      // Rollback on error
      if (context?.previousLabels) {
        queryClient.setQueryData(labelKeys.gameLabels(gameId), context.previousLabels);
      }
    },
    onSettled: (_data, _err, { gameId }) => {
      // Refetch for consistency
      queryClient.invalidateQueries({ queryKey: labelKeys.gameLabels(gameId) });
      // Also invalidate game detail to refresh labels there
      queryClient.invalidateQueries({ queryKey: libraryKeys.gameDetail(gameId) });
    },
  });
}

/**
 * Hook to remove a label from a game with optimistic update
 *
 * Issue #3514: Optimistic updates for instant feedback
 * - Immediately removes label from game labels cache
 * - Rolls back on error
 * - Refetches on settlement for consistency
 *
 * @returns UseMutationResult for removing label
 */
export function useRemoveLabelFromGame(): UseMutationResult<
  void,
  Error,
  { gameId: string; labelId: string }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ gameId, labelId }: { gameId: string; labelId: string }) => {
      return api.library.removeLabelFromGame(gameId, labelId);
    },
    onMutate: async ({ gameId, labelId }) => {
      // Cancel any outgoing refetches
      await queryClient.cancelQueries({ queryKey: labelKeys.gameLabels(gameId) });

      // Snapshot previous value
      const previousLabels = queryClient.getQueryData<LabelDto[]>(labelKeys.gameLabels(gameId));

      // Optimistically remove the label
      queryClient.setQueryData<LabelDto[]>(labelKeys.gameLabels(gameId), (old) => {
        if (!old) return [];
        return old.filter((l) => l.id !== labelId);
      });

      return { previousLabels };
    },
    onError: (_err, { gameId }, context) => {
      // Rollback on error
      if (context?.previousLabels) {
        queryClient.setQueryData(labelKeys.gameLabels(gameId), context.previousLabels);
      }
    },
    onSettled: (_data, _err, { gameId }) => {
      // Refetch for consistency
      queryClient.invalidateQueries({ queryKey: labelKeys.gameLabels(gameId) });
      queryClient.invalidateQueries({ queryKey: libraryKeys.gameDetail(gameId) });
    },
  });
}

/**
 * Hook to create a custom label
 *
 * @returns UseMutationResult for creating label
 */
export function useCreateCustomLabel(): UseMutationResult<
  LabelDto,
  Error,
  CreateCustomLabelRequest
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: CreateCustomLabelRequest) => {
      return api.library.createCustomLabel(request);
    },
    onSuccess: () => {
      // Invalidate labels list to include new label
      queryClient.invalidateQueries({ queryKey: labelKeys.list() });
    },
  });
}

/**
 * Hook to delete a custom label
 *
 * @returns UseMutationResult for deleting label
 */
export function useDeleteCustomLabel(): UseMutationResult<void, Error, string> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (labelId: string) => {
      return api.library.deleteCustomLabel(labelId);
    },
    onSuccess: () => {
      // Invalidate labels list to remove deleted label
      queryClient.invalidateQueries({ queryKey: labelKeys.list() });
      // Also invalidate all game labels since they might have used this label
      queryClient.invalidateQueries({ queryKey: labelKeys.all });
    },
  });
}
