/**
 * useCollectionActions - Hook for managing collection add/remove operations
 *
 * Provides optimistic UI updates, error handling with rollback, and associated
 * data retrieval for removal warnings.
 *
 * Cross-cache sync: Both ['library-status', gameId] (this hook) and
 * ['library', 'status', gameId] (useGameInLibraryStatus) are kept in sync
 * via optimistic updates and invalidation.
 *
 * @module hooks/use-collection-actions
 * @see Issue #4259 - Collection Quick Actions for MeepleCard
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';

import { libraryKeys } from '@/hooks/queries/useLibrary';

// ============================================================================
// Types
// ============================================================================

export interface AssociatedData {
  hasCustomAgent: boolean;
  hasPrivatePdf: boolean;
  chatSessionsCount: number;
  gameSessionsCount: number;
  checklistItemsCount: number;
  labelsCount: number;
}

interface LibraryStatusResponse {
  inLibrary: boolean;
  isFavorite: boolean;
  associatedData: AssociatedData | null;
}

export interface CollectionActions {
  /** Whether the entity is currently in the user's collection */
  isInCollection: boolean;
  /** Whether the entity is marked as favorite */
  isFavorite: boolean;
  /** Associated data that will be lost on removal */
  associatedData: AssociatedData | null;
  /** Loading state */
  isLoading: boolean;
  /** Add entity to collection */
  add: () => void;
  /** Remove entity from collection (triggers warning modal if has associated data) */
  remove: (onConfirm?: () => void) => void;
  /** Check if entity has associated data */
  hasAssociatedData: boolean;
}

// ============================================================================
// Hook
// ============================================================================

/**
 * Hook for managing collection operations for a game entity.
 *
 * @param gameId - The ID of the game
 * @param onRemovalWarning - Callback to show warning modal with associated data
 * @returns Collection actions and state
 */
export function useCollectionActions(
  gameId: string,
  onRemovalWarning?: (data: AssociatedData, onConfirm: () => void) => void
): CollectionActions {
  const queryClient = useQueryClient();
  const queryKey = ['library-status', gameId];
  // Cross-cache key used by useGameInLibraryStatus
  const libraryStatusKey = libraryKeys.gameStatus(gameId);

  // Fetch library status
  const { data, isLoading } = useQuery<LibraryStatusResponse>({
    queryKey,
    queryFn: async () => {
      const response = await fetch(`/api/v1/library/games/${gameId}/status`, {
        credentials: 'include',
      });

      if (!response.ok) {
        throw new Error('Failed to fetch library status');
      }

      return response.json();
    },
    enabled: gameId.length > 0, // Issue #4259: Only fetch when gameId is valid
    staleTime: 30000, // 30 seconds
  });

  // Add to collection mutation
  const addMutation = useMutation({
    mutationFn: async () => {
      const response = await fetch(`/api/v1/library/games/${gameId}`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({}),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.message || 'Failed to add to collection');
      }

      return response.json();
    },
    onMutate: async () => {
      // Cancel outgoing refetches
      await queryClient.cancelQueries({ queryKey });
      await queryClient.cancelQueries({ queryKey: libraryStatusKey });

      // Snapshot previous values
      const previous = queryClient.getQueryData<LibraryStatusResponse>(queryKey);
      const previousLibraryStatus = queryClient.getQueryData(libraryStatusKey);

      // Optimistic update: this hook's cache
      queryClient.setQueryData<LibraryStatusResponse>(queryKey, (old) => ({
        inLibrary: true,
        isFavorite: old?.isFavorite ?? false,
        associatedData: null,
      }));

      // Optimistic update: useGameInLibraryStatus cache (cross-cache sync)
      queryClient.setQueryData(libraryStatusKey, (old: Record<string, unknown> | undefined) => (
        old ? { ...old, inLibrary: true } : { inLibrary: true, isFavorite: false }
      ));

      return { previous, previousLibraryStatus };
    },
    onError: (_err, _variables, context) => {
      // Rollback on error
      if (context?.previous) {
        queryClient.setQueryData(queryKey, context.previous);
      }
      if (context?.previousLibraryStatus) {
        queryClient.setQueryData(libraryStatusKey, context.previousLibraryStatus);
      }
      toast.error('Impossibile aggiungere il gioco alla collezione');
    },
    onSuccess: () => {
      toast.success('Gioco aggiunto alla collezione!');
    },
    onSettled: () => {
      // Invalidate all related caches for consistency
      queryClient.invalidateQueries({ queryKey: ['user-library'] });
      queryClient.invalidateQueries({ queryKey: libraryStatusKey });
      queryClient.invalidateQueries({ queryKey: libraryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.stats() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.quota() });
    },
  });

  // Remove from collection mutation
  const removeMutation = useMutation({
    mutationFn: async () => {
      const response = await fetch(`/api/v1/library/games/${gameId}`, {
        method: 'DELETE',
        credentials: 'include',
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.message || 'Failed to remove from collection');
      }
    },
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey });
      await queryClient.cancelQueries({ queryKey: libraryStatusKey });

      const previous = queryClient.getQueryData<LibraryStatusResponse>(queryKey);
      const previousLibraryStatus = queryClient.getQueryData(libraryStatusKey);

      // Optimistic update: this hook's cache
      queryClient.setQueryData<LibraryStatusResponse>(queryKey, () => ({
        inLibrary: false,
        isFavorite: false,
        associatedData: null,
      }));

      // Optimistic update: useGameInLibraryStatus cache (cross-cache sync)
      queryClient.setQueryData(libraryStatusKey, (old: Record<string, unknown> | undefined) => (
        old ? { ...old, inLibrary: false, isFavorite: false } : { inLibrary: false, isFavorite: false }
      ));

      return { previous, previousLibraryStatus };
    },
    onError: (_err, _variables, context) => {
      if (context?.previous) {
        queryClient.setQueryData(queryKey, context.previous);
      }
      if (context?.previousLibraryStatus) {
        queryClient.setQueryData(libraryStatusKey, context.previousLibraryStatus);
      }
      toast.error('Impossibile rimuovere il gioco dalla collezione');
    },
    onSuccess: () => {
      toast.success('Gioco rimosso dalla collezione');
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['user-library'] });
      queryClient.invalidateQueries({ queryKey: libraryStatusKey });
      queryClient.invalidateQueries({ queryKey: libraryKeys.lists() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.stats() });
      queryClient.invalidateQueries({ queryKey: libraryKeys.quota() });
    },
  });

  // Removal logic with warning
  const handleRemove = (onConfirm?: () => void) => {
    const associatedData = data?.associatedData;

    // Check if has associated data
    const hasData =
      associatedData &&
      (associatedData.hasCustomAgent ||
        associatedData.hasPrivatePdf ||
        associatedData.chatSessionsCount > 0 ||
        associatedData.gameSessionsCount > 0 ||
        associatedData.checklistItemsCount > 0 ||
        associatedData.labelsCount > 0);

    if (hasData && onRemovalWarning) {
      // Show warning modal
      onRemovalWarning(associatedData, () => {
        onConfirm?.();
        removeMutation.mutate();
      });
    } else {
      // No associated data, remove directly
      onConfirm?.();
      removeMutation.mutate();
    }
  };

  return {
    isInCollection: data?.inLibrary ?? false,
    isFavorite: data?.isFavorite ?? false,
    associatedData: data?.associatedData ?? null,
    isLoading,
    add: () => addMutation.mutate(),
    remove: handleRemove,
    hasAssociatedData:
      data?.associatedData !== null &&
      (data?.associatedData?.hasCustomAgent ||
        data?.associatedData?.hasPrivatePdf ||
        (data?.associatedData?.chatSessionsCount ?? 0) > 0 ||
        (data?.associatedData?.gameSessionsCount ?? 0) > 0 ||
        (data?.associatedData?.checklistItemsCount ?? 0) > 0 ||
        (data?.associatedData?.labelsCount ?? 0) > 0),
  };
}
