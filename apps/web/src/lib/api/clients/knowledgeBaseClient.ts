/**
 * Knowledge Base Client (Issue #4065)
 *
 * Client for Knowledge Base bounded context status endpoint.
 * Used by useEmbeddingStatus hook to poll RAG readiness.
 */

import {
  KnowledgeBaseStatusSchema,
  RagConfigSchema,
  type KnowledgeBaseStatus,
  type RagConfigResponse,
} from '../schemas/knowledge-base.schemas';

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
        KnowledgeBaseStatusSchema
      );
    },

    /**
     * Get user's RAG configuration (Issue #5311)
     * @param strategy Optional strategy filter
     * @returns Current RAG config or defaults
     */
    async getRagConfig(strategy?: string): Promise<RagConfigResponse | null> {
      const query = strategy ? `?strategy=${encodeURIComponent(strategy)}` : '';
      return httpClient.get(`/api/v1/rag-dashboard/config${query}`, RagConfigSchema);
    },

    /**
     * Save user's RAG configuration (Issue #5311)
     * @param config Full RAG config to persist
     * @returns Saved config
     */
    async saveRagConfig(config: RagConfigResponse): Promise<RagConfigResponse> {
      return httpClient.put(`/api/v1/rag-dashboard/config`, config, RagConfigSchema);
    },

    /**
     * Reset user's RAG configuration to defaults (Issue #5311)
     * @param strategy Optional strategy to reset
     * @returns Default config
     */
    async resetRagConfig(strategy?: string): Promise<RagConfigResponse> {
      const body = strategy ? { strategy } : {};
      return httpClient.post(`/api/v1/rag-dashboard/config/reset`, body, RagConfigSchema);
    },
  };
}
