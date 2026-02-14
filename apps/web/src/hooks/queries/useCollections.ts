/**
 * React Query hooks for generic collection operations.
 * Issue #4263: Phase 2 - Generic UserCollection System
 *
 * Routing Strategy:
 * - entityType='game' → routes to Phase 1 hooks (useCollectionActions)
 * - Other types → uses generic collection API
 *
 * @module hooks/queries/useCollections
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';

import {
  addToCollection,
  getCollectionStatus,
  removeFromCollection,
} from '@/lib/api/clients/collectionsClient';
import type {
  AddToCollectionRequest,
  AssociatedDataDto,
  CollectionStatusDto,
  EntityType,
} from '@/lib/api/schemas/collections.schemas';

// ============================================================================
// Query Hooks
// ============================================================================

/**
 * Hook to fetch collection status for an entity.
 *
 * @param entityType - Type of entity (game, player, event, etc.)
 * @param entityId - ID of the entity
 * @param options - Query options (e.g., enabled flag)
 * @returns Query result with collection status
 */
export function useCollectionStatus(
  entityType: EntityType,
  entityId: string,
  options?: { enabled?: boolean }
) {
  return useQuery<CollectionStatusDto>({
    queryKey: ['collection-status', entityType, entityId],
    queryFn: () => getCollectionStatus(entityType, entityId),
    enabled: !!entityId && entityId.length > 0 && (options?.enabled ?? true), // Issue #4263: Prevent invalid API calls
    staleTime: 30000, // 30 seconds
  });
}

// ============================================================================
// Mutation Hooks
// ============================================================================

/**
 * Hook to add entity to collection.
 *
 * @param entityType - Type of entity (game, player, event, etc.)
 * @param entityId - ID of the entity
 * @returns Mutation object with optimistic updates
 */
export function useAddToCollection(entityType: EntityType, entityId: string) {
  const queryClient = useQueryClient();
  const queryKey = ['collection-status', entityType, entityId];

  return useMutation({
    mutationFn: (options?: AddToCollectionRequest) =>
      addToCollection(entityType, entityId, options),
    onMutate: async () => {
      // Cancel outgoing refetches
      await queryClient.cancelQueries({ queryKey });

      // Snapshot previous value
      const previous = queryClient.getQueryData<CollectionStatusDto>(queryKey);

      // Optimistic update
      queryClient.setQueryData<CollectionStatusDto>(queryKey, (old) => ({
        inCollection: true,
        isFavorite: old?.isFavorite ?? false,
        associatedData: null,
      }));

      return { previous };
    },
    onError: (_err, _variables, context) => {
      // Rollback on error
      if (context?.previous) {
        queryClient.setQueryData(queryKey, context.previous);
      }
      toast.error('Impossibile aggiungere alla collezione');
    },
    onSuccess: () => {
      toast.success('Aggiunto alla collezione!');
      // Invalidate related queries
      queryClient.invalidateQueries({ queryKey: ['user-library'] });
      queryClient.invalidateQueries({
        queryKey: ['collection-status', entityType],
      });
    },
  });
}

/**
 * Hook to remove entity from collection.
 *
 * @param entityType - Type of entity (game, player, event, etc.)
 * @param entityId - ID of the entity
 * @param onRemovalWarning - Optional callback to show warning modal with associated data
 * @returns Mutation object with optimistic updates
 */
export function useRemoveFromCollection(
  entityType: EntityType,
  entityId: string,
  onRemovalWarning?: (
    data: AssociatedDataDto,
    onConfirm: () => void
  ) => void
) {
  const queryClient = useQueryClient();
  const queryKey = ['collection-status', entityType, entityId];

  const mutation = useMutation({
    mutationFn: () => removeFromCollection(entityType, entityId),
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData<CollectionStatusDto>(queryKey);

      // Optimistic update
      queryClient.setQueryData<CollectionStatusDto>(queryKey, () => ({
        inCollection: false,
        isFavorite: false,
        associatedData: null,
      }));

      return { previous };
    },
    onError: (_err, _variables, context) => {
      if (context?.previous) {
        queryClient.setQueryData(queryKey, context.previous);
      }
      toast.error('Impossibile rimuovere dalla collezione');
    },
    onSuccess: () => {
      toast.success('Rimosso dalla collezione');
      queryClient.invalidateQueries({ queryKey: ['user-library'] });
      queryClient.invalidateQueries({
        queryKey: ['collection-status', entityType],
      });
    },
  });

  // Wrapper that checks for associated data and shows warning
  const handleRemove = (onConfirm?: () => void) => {
    const currentStatus =
      queryClient.getQueryData<CollectionStatusDto>(queryKey);
    const associatedData = currentStatus?.associatedData;

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
        mutation.mutate();
      });
    } else {
      // No associated data, remove directly
      onConfirm?.();
      mutation.mutate();
    }
  };

  return {
    ...mutation,
    remove: handleRemove,
  };
}
