/**
 * Collection schemas and types for generic collection operations.
 * Issue #4263: Phase 2 - Generic UserCollection System
 *
 * @module lib/api/schemas/collections.schemas
 */

// ============================================================================
// Entity Types
// ============================================================================

/**
 * Supported entity types for collections.
 * Maps to backend EntityType enum.
 */
export type EntityType =
  | 'game'
  | 'player'
  | 'event'
  | 'session'
  | 'agent'
  | 'document'
  | 'chatSession';

// ============================================================================
// DTOs
// ============================================================================

/**
 * Associated data that will be lost if entity is removed from collection.
 * Maps to backend AssociatedDataDto.
 */
export interface AssociatedDataDto {
  /** Whether the library entry has a custom AI agent configuration */
  hasCustomAgent: boolean;
  /** Whether the library entry has a private PDF uploaded */
  hasPrivatePdf: boolean;
  /** Number of chat sessions associated with this entity */
  chatSessionsCount: number;
  /** Number of recorded game sessions (play history) */
  gameSessionsCount: number;
  /** Number of setup checklist items */
  checklistItemsCount: number;
  /** Number of custom labels assigned to this entity */
  labelsCount: number;
}

/**
 * Collection status for any entity type.
 * Maps to backend CollectionStatusDto.
 */
export interface CollectionStatusDto {
  /** Whether the entity is in the user's collection */
  inCollection: boolean;
  /** Whether the entity is marked as favorite */
  isFavorite: boolean;
  /** Associated data (null if not in collection) */
  associatedData: AssociatedDataDto | null;
}

/**
 * Request body for adding entity to collection.
 */
export interface AddToCollectionRequest {
  /** Whether to mark entity as favorite */
  isFavorite?: boolean;
}
