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

// ============================================================================
// Bulk Operations (Issue #4268)
// ============================================================================

/**
 * Aggregated associated data for bulk operations.
 * Used to show total data loss across multiple entities.
 */
export interface BulkAssociatedDataDto {
  /** Total custom AI agents that will be deleted */
  totalCustomAgents: number;
  /** Total private PDFs that will be deleted */
  totalPrivatePdfs: number;
  /** Total chat sessions that will be deleted */
  totalChatSessions: number;
  /** Total game sessions that will be deleted */
  totalGameSessions: number;
  /** Total checklist items that will be deleted */
  totalChecklistItems: number;
  /** Total custom labels that will be unassigned */
  totalLabels: number;
}

/**
 * Result of a bulk operation with partial success details.
 */
export interface BulkOperationResult {
  /** Total number of entities requested */
  totalRequested: number;
  /** Number of successfully processed entities */
  successCount: number;
  /** Number of failed entities */
  failedCount: number;
  /** Error details for failed entities */
  errors: Array<{
    entityId: string;
    error: string;
  }>;
}

/**
 * Request body for bulk adding entities to collection.
 */
export interface BulkAddToCollectionRequest {
  /** List of entity IDs to add */
  entityIds: readonly string[];
  /** Whether to mark all as favorite */
  isFavorite?: boolean;
  /** Optional notes to apply to all */
  notes?: string;
}

/**
 * Request body for bulk removing entities from collection.
 */
export interface BulkRemoveFromCollectionRequest {
  /** List of entity IDs to remove */
  entityIds: readonly string[];
}

/**
 * Request body for getting bulk associated data.
 */
export interface BulkGetAssociatedDataRequest {
  /** List of entity IDs to check */
  entityIds: readonly string[];
}
