/**
 * Knowledge Base Client Tests - Issue #5311
 *
 * Coverage: RAG config API client methods
 * - getRagConfig: Fetch user's RAG configuration
 * - saveRagConfig: Persist RAG configuration
 * - resetRagConfig: Reset to defaults
 * - getEmbeddingStatus: Fetch embedding status (existing)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createKnowledgeBaseClient } from '../knowledgeBaseClient';
import type { HttpClient } from '../../core/httpClient';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
} as HttpClient;

const MOCK_RAG_CONFIG = {
  generation: { temperature: 0.7, topK: 40, topP: 0.9, maxTokens: 1000 },
  retrieval: { chunkSize: 500, chunkOverlap: 10, topResults: 5, similarityThreshold: 0.7 },
  reranker: { enabled: true, model: 'cross-encoder/ms-marco-MiniLM-L-12-v2', topN: 10 },
  models: { primaryModel: 'gpt-4o-mini', fallbackModel: null, evaluationModel: null },
  strategySpecific: { hybridAlpha: 0.5, contextWindow: 5, maxHops: 3 },
  activeStrategy: 'Hybrid',
};

describe('KnowledgeBaseClient - Issue #5311', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('getEmbeddingStatus', () => {
    it('should fetch embedding status for a game', async () => {
      const mockStatus = {
        status: 'Completed',
        progress: 100,
        totalChunks: 42,
        processedChunks: 42,
        errorMessage: null,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockStatus);

      const client = createKnowledgeBaseClient({ httpClient: mockHttpClient });
      const result = await client.getEmbeddingStatus('game-123');

      expect(result).toEqual(mockStatus);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/knowledge-base/game-123/status',
        expect.any(Object)
      );
    });
  });

  describe('getRagConfig', () => {
    it('should fetch RAG config without strategy filter', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(MOCK_RAG_CONFIG);

      const client = createKnowledgeBaseClient({ httpClient: mockHttpClient });
      const result = await client.getRagConfig();

      expect(result).toEqual(MOCK_RAG_CONFIG);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/rag-dashboard/config',
        expect.any(Object)
      );
    });

    it('should fetch RAG config with strategy filter', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(MOCK_RAG_CONFIG);

      const client = createKnowledgeBaseClient({ httpClient: mockHttpClient });
      await client.getRagConfig('Semantic');

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/rag-dashboard/config?strategy=Semantic',
        expect.any(Object)
      );
    });

    it('should return null when not authenticated', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      const client = createKnowledgeBaseClient({ httpClient: mockHttpClient });
      const result = await client.getRagConfig();

      expect(result).toBeNull();
    });

    it('should handle API error', async () => {
      vi.mocked(mockHttpClient.get).mockRejectedValue(new Error('Network error'));

      const client = createKnowledgeBaseClient({ httpClient: mockHttpClient });
      await expect(client.getRagConfig()).rejects.toThrow('Network error');
    });
  });

  describe('saveRagConfig', () => {
    it('should save RAG config via PUT', async () => {
      vi.mocked(mockHttpClient.put).mockResolvedValue(MOCK_RAG_CONFIG);

      const client = createKnowledgeBaseClient({ httpClient: mockHttpClient });
      const result = await client.saveRagConfig(MOCK_RAG_CONFIG);

      expect(result).toEqual(MOCK_RAG_CONFIG);
      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/rag-dashboard/config',
        MOCK_RAG_CONFIG,
        expect.any(Object)
      );
    });

    it('should handle validation error from server', async () => {
      vi.mocked(mockHttpClient.put).mockRejectedValue(new Error('Validation failed'));

      const client = createKnowledgeBaseClient({ httpClient: mockHttpClient });
      await expect(client.saveRagConfig(MOCK_RAG_CONFIG)).rejects.toThrow('Validation failed');
    });
  });

  describe('resetRagConfig', () => {
    it('should reset config without strategy', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(MOCK_RAG_CONFIG);

      const client = createKnowledgeBaseClient({ httpClient: mockHttpClient });
      const result = await client.resetRagConfig();

      expect(result).toEqual(MOCK_RAG_CONFIG);
      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/rag-dashboard/config/reset',
        {},
        expect.any(Object)
      );
    });

    it('should reset config for specific strategy', async () => {
      vi.mocked(mockHttpClient.post).mockResolvedValue(MOCK_RAG_CONFIG);

      const client = createKnowledgeBaseClient({ httpClient: mockHttpClient });
      await client.resetRagConfig('Semantic');

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        '/api/v1/rag-dashboard/config/reset',
        { strategy: 'Semantic' },
        expect.any(Object)
      );
    });

    it('should handle API error on reset', async () => {
      vi.mocked(mockHttpClient.post).mockRejectedValue(new Error('Server error'));

      const client = createKnowledgeBaseClient({ httpClient: mockHttpClient });
      await expect(client.resetRagConfig()).rejects.toThrow('Server error');
    });
  });
});
