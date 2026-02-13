/**
 * useBulkCollectionActions - Hook for managing bulk collection operations
 *
 * Provides optimistic UI updates, partial success handling, and aggregated
 * data retrieval for bulk removal warnings.
 *
 * @module hooks/use-bulk-collection-actions
 * @see Issue #4268 - Phase 3: Bulk Collection Actions
 */

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';

import {
  bulkAddToCollection,
  bulkRemoveFromCollection,
  getBulkAssociatedData,
} from '@/lib/api/clients/collectionsClient';
import type {
  BulkAddToCollectionRequest,
  BulkAssociatedDataDto,
  BulkOperationResult,
  BulkRemoveFromCollectionRequest,
  EntityType,
} from '@/lib/api/schemas/collections.schemas';

// ============================================================================
// Types
// ============================================================================

export interface BulkCollectionActions {
  /** Add multiple entities to collection */
  bulkAdd: (request: BulkAddToCollectionRequest) => void;
  /** Remove multiple entities from collection (triggers warning modal) */
  bulkRemove: (request: BulkRemoveFromCollectionRequest) => void;
  /** Fetch aggregated data for removal warning */
  fetchAggregatedData: (
    entityIds: readonly string[]
  ) => Promise<BulkAssociatedDataDto>;
  /** Whether add operation is loading */
  isAdding: boolean;
  /** Whether remove operation is loading */
  isRemoving: boolean;
  /** Whether fetching aggregated data */
  isFetchingData: boolean;
}

// ============================================================================
// Hook
// ============================================================================

/**
 * Hook for managing bulk collection operations.
 *
 * Features:
 * - Optimistic UI updates
 * - Partial success handling (18 succeed, 2 fail → user gets 18 + error details)
 * - Toast notifications with success/failure counts
 * - Automatic query invalidation
 *
 * @param entityType - Type of entities being operated on
 * @returns Bulk collection actions and loading states
 */
export function useBulkCollectionActions(
  entityType: EntityType
): BulkCollectionActions {
  const queryClient = useQueryClient();

  // Bulk add mutation
  const addMutation = useMutation({
    mutationFn: (request: BulkAddToCollectionRequest) =>
      bulkAddToCollection(entityType, request),
    onMutate: async () => {
      // Show loading toast
      toast.loading('Aggiungendo alla collezione...');
    },
    onSuccess: (result: BulkOperationResult) => {
      // Dismiss loading toast
      toast.dismiss();

      // Invalidate queries
      queryClient.invalidateQueries({ queryKey: ['user-library'] });
      queryClient.invalidateQueries({ queryKey: ['library-status'] });

      // Show success/warning toast based on result
      if (result.failedCount === 0) {
        toast.success(
          `${result.successCount} ${getPluralLabel(entityType, result.successCount)} aggiunti!`
        );
      } else if (result.successCount > 0) {
        // Partial success
        toast.warning(
          `${result.successCount}/${result.totalRequested} ${getPluralLabel(entityType, result.successCount)} aggiunti. ${result.failedCount} falliti.`,
          {
            description: result.errors[0]?.error || 'Alcuni elementi non sono stati aggiunti',
            duration: 5000,
          }
        );
      } else {
        // Total failure
        toast.error(
          `Impossibile aggiungere ${result.totalRequested} ${getPluralLabel(entityType, result.totalRequested)}`,
          {
            description: result.errors[0]?.error || 'Operazione fallita',
          }
        );
      }
    },
    onError: (error: Error) => {
      toast.dismiss();
      toast.error('Errore durante l\'aggiunta alla collezione', {
        description: error.message,
      });
    },
  });

  // Bulk remove mutation
  const removeMutation = useMutation({
    mutationFn: (request: BulkRemoveFromCollectionRequest) =>
      bulkRemoveFromCollection(entityType, request),
    onMutate: async () => {
      toast.loading('Rimozione dalla collezione...');
    },
    onSuccess: (result: BulkOperationResult) => {
      toast.dismiss();

      // Invalidate queries
      queryClient.invalidateQueries({ queryKey: ['user-library'] });
      queryClient.invalidateQueries({ queryKey: ['library-status'] });

      // Show success/warning toast
      if (result.failedCount === 0) {
        toast.success(
          `${result.successCount} ${getPluralLabel(entityType, result.successCount)} rimossi!`
        );
      } else if (result.successCount > 0) {
        toast.warning(
          `${result.successCount}/${result.totalRequested} ${getPluralLabel(entityType, result.successCount)} rimossi. ${result.failedCount} falliti.`,
          {
            description: result.errors[0]?.error || 'Alcuni elementi non sono stati rimossi',
            duration: 5000,
          }
        );
      } else {
        toast.error(
          `Impossibile rimuovere ${result.totalRequested} ${getPluralLabel(entityType, result.totalRequested)}`,
          {
            description: result.errors[0]?.error || 'Operazione fallita',
          }
        );
      }
    },
    onError: (error: Error) => {
      toast.dismiss();
      toast.error('Errore durante la rimozione dalla collezione', {
        description: error.message,
      });
    },
  });

  // Fetch aggregated data for warning (non-mutation query)
  const fetchAggregatedData = async (
    entityIds: readonly string[]
  ): Promise<BulkAssociatedDataDto> => {
    return getBulkAssociatedData(entityType, { entityIds });
  };

  return {
    bulkAdd: (request) => addMutation.mutate(request),
    bulkRemove: (request) => removeMutation.mutate(request),
    fetchAggregatedData,
    isAdding: addMutation.isPending,
    isRemoving: removeMutation.isPending,
    isFetchingData: false, // No loading state for fetch (it's called manually)
  };
}

// ============================================================================
// Utils
// ============================================================================

/**
 * Get plural label for entity type in Italian
 */
function getPluralLabel(entityType: EntityType, count: number): string {
  const labels: Record<EntityType, { singular: string; plural: string }> = {
    game: { singular: 'gioco', plural: 'giochi' },
    player: { singular: 'giocatore', plural: 'giocatori' },
    event: { singular: 'evento', plural: 'eventi' },
    session: { singular: 'sessione', plural: 'sessioni' },
    agent: { singular: 'agente', plural: 'agenti' },
    document: { singular: 'documento', plural: 'documenti' },
    chatSession: { singular: 'chat', plural: 'chat' },
  };

  const label = labels[entityType];
  return count === 1 ? label.singular : label.plural;
}
