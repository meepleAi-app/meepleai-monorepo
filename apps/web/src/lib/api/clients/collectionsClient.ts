/**
 * API client for generic collection operations.
 * Issue #4263: Phase 2 - Generic UserCollection System
 *
 * @module lib/api/clients/collectionsClient
 */

import type {
  AddToCollectionRequest,
  CollectionStatusDto,
  EntityType,
} from '../schemas/collections.schemas';

// ============================================================================
// API Client
// ============================================================================

/**
 * Get collection status for an entity.
 *
 * @param entityType - Type of entity (game, player, event, etc.)
 * @param entityId - ID of the entity
 * @returns Promise with collection status
 */
export async function getCollectionStatus(
  entityType: EntityType,
  entityId: string
): Promise<CollectionStatusDto> {
  const response = await fetch(
    `/api/v1/library/collections/${entityType}/${entityId}/status`,
    {
      credentials: 'include',
    }
  );

  if (!response.ok) {
    throw new Error('Failed to fetch collection status');
  }

  return response.json();
}

/**
 * Add entity to collection.
 *
 * @param entityType - Type of entity (game, player, event, etc.)
 * @param entityId - ID of the entity
 * @param options - Optional request body (e.g., isFavorite flag)
 * @returns Promise that resolves when entity is added
 */
export async function addToCollection(
  entityType: EntityType,
  entityId: string,
  options?: AddToCollectionRequest
): Promise<void> {
  const response = await fetch(
    `/api/v1/library/collections/${entityType}/${entityId}`,
    {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(options || {}),
    }
  );

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || 'Failed to add to collection');
  }
}

/**
 * Remove entity from collection.
 *
 * @param entityType - Type of entity (game, player, event, etc.)
 * @param entityId - ID of the entity
 * @returns Promise that resolves when entity is removed
 */
export async function removeFromCollection(
  entityType: EntityType,
  entityId: string
): Promise<void> {
  const response = await fetch(
    `/api/v1/library/collections/${entityType}/${entityId}`,
    {
      method: 'DELETE',
      credentials: 'include',
    }
  );

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.message || 'Failed to remove from collection');
  }
}
