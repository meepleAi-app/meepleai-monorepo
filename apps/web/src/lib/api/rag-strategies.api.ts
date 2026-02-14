import { HttpClient } from './core/httpClient';

/**
 * RAG Strategy API Client
 * Issue #3: Agent Config Selectors
 *
 * Fetches available RAG strategies from backend for user/editor selection.
 */

const api = new HttpClient();

/**
 * RAG Strategy DTO (matches backend RagStrategyDto)
 */
export interface RagStrategyDto {
  name: string;
  displayName: string;
  description: string;
  complexity: number;
  estimatedTokens: number;
  requiresAdmin: boolean;
  useCase: string;
}

/**
 * Response from GET /api/v1/rag/strategies
 */
export interface GetRagStrategiesResponse {
  strategies: RagStrategyDto[];
}

export const ragStrategiesApi = {
  /**
   * Get all available RAG strategies
   * Endpoint: GET /api/v1/rag/strategies (Issue #8)
   */
  getAll: async (): Promise<RagStrategyDto[]> => {
    const result = await api.get<GetRagStrategiesResponse>('/api/v1/rag/strategies');
    return result?.strategies || [];
  },
};
