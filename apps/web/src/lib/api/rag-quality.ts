/**
 * RAG Quality Report API Client
 *
 * Client for `/api/v1/admin/rag-quality/report` endpoint.
 * Fetches observability data for RAG pipeline health monitoring.
 */

// ============================================================================
// Types
// ============================================================================

export interface RagQualityGameStats {
  gameId: string;
  gameTitle: string;
  chunkCount: number;
  raptorNodeCount: number;
  entityRelationCount: number;
}

export interface RagQualityEnhancementStatus {
  name: string;
  featureFlagKey: string;
  freeEnabled: boolean;
  normalEnabled: boolean;
  premiumEnabled: boolean;
}

export interface RagQualityReport {
  totalIndexedDocuments: number;
  totalRaptorSummaries: number;
  totalEntityRelations: number;
  totalEmbeddedChunks: number;
  topGamesByChunkCount: RagQualityGameStats[];
  enhancementStatuses: RagQualityEnhancementStatus[];
}

// ============================================================================
// API Functions
// ============================================================================

/**
 * Fetch RAG quality report from admin endpoint
 *
 * @returns Promise<RagQualityReport>
 * @throws Error if API request fails
 */
export async function fetchRagQualityReport(): Promise<RagQualityReport> {
  const response = await fetch('/api/v1/admin/rag-quality/report', {
    credentials: 'include',
  });

  if (!response.ok) {
    throw new Error(`RAG Quality API error: ${response.status} ${response.statusText}`);
  }

  return response.json();
}
