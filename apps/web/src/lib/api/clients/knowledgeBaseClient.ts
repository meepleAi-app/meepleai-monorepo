/**
 * Knowledge Base Client (Issue #4065)
 *
 * Client for Knowledge Base bounded context status endpoint.
 * Used by useEmbeddingStatus hook to poll RAG readiness.
 */

import { KnowledgeBaseStatusSchema, type KnowledgeBaseStatus } from '../schemas/knowledge-base.schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateKnowledgeBaseClientParams {
  httpClient: HttpClient;
}

export type KnowledgeBaseClient = ReturnType<typeof createKnowledgeBaseClient>;

/**
 * Knowledge Base API client with Zod validation
 */
export function createKnowledgeBaseClient({ httpClient }: CreateKnowledgeBaseClientParams) {
  return {
    /**
     * Get embedding status for a game's knowledge base
     * @param gameId Game ID (GUID format)
     * @returns Current embedding status with chunk progress
     */
    async getEmbeddingStatus(gameId: string): Promise<KnowledgeBaseStatus | null> {
      return httpClient.get(
        `/api/v1/knowledge-base/${encodeURIComponent(gameId)}/status`,
        KnowledgeBaseStatusSchema,
      );
    },
  };
}
