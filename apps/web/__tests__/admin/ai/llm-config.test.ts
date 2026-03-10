/**
 * Admin Client - LLM System Config Methods Tests (Issue #5495)
 *
 * Tests for getLlmSystemConfig and updateLlmSystemConfig methods.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createAdminClient } from '@/lib/api/clients/adminClient';
import type { HttpClient } from '@/lib/api/core/httpClient';
import type { LlmSystemConfigDto } from '@/lib/api/schemas';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
  baseUrl: 'http://localhost:8080',
} as any;

const mockConfig: LlmSystemConfigDto = {
  circuitBreakerFailureThreshold: 5,
  circuitBreakerOpenDurationSeconds: 30,
  circuitBreakerSuccessThreshold: 3,
  dailyBudgetUsd: 10.0,
  monthlyBudgetUsd: 100.0,
  fallbackChainJson: '[]',
  source: 'database',
  lastUpdatedAt: '2026-03-09T12:00:00Z',
  lastUpdatedByUserId: '00000000-0000-0000-0000-000000000001',
};

describe('AdminClient - LLM System Config (Issue #5495)', () => {
  let client: ReturnType<typeof createAdminClient>;

  beforeEach(() => {
    vi.clearAllMocks();
    client = createAdminClient({ httpClient: mockHttpClient });
  });

  describe('getLlmSystemConfig', () => {
    it('should GET from /api/v1/admin/llm/config', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(mockConfig);

      const result = await client.getLlmSystemConfig();

      expect(result).toEqual(mockConfig);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/llm/config',
        expect.any(Object)
      );
    });

    it('should throw when response is null', async () => {
      vi.mocked(mockHttpClient.get).mockResolvedValue(null);

      await expect(client.getLlmSystemConfig()).rejects.toThrow(
        'Failed to fetch LLM system config'
      );
    });
  });

  describe('updateLlmSystemConfig', () => {
    it('should PUT to /api/v1/admin/llm/config with request body', async () => {
      const updatedConfig = { ...mockConfig, circuitBreakerFailureThreshold: 10 };
      vi.mocked(mockHttpClient.put).mockResolvedValue(updatedConfig);

      const request = {
        circuitBreakerFailureThreshold: 10,
        circuitBreakerOpenDurationSeconds: 30,
        circuitBreakerSuccessThreshold: 3,
        dailyBudgetUsd: 10.0,
        monthlyBudgetUsd: 100.0,
        fallbackChainJson: '[]',
      };

      const result = await client.updateLlmSystemConfig(request);

      expect(result).toEqual(updatedConfig);
      expect(mockHttpClient.put).toHaveBeenCalledWith(
        '/api/v1/admin/llm/config',
        request,
        expect.any(Object)
      );
    });

    it('should throw when response is null', async () => {
      vi.mocked(mockHttpClient.put).mockResolvedValue(null);

      await expect(
        client.updateLlmSystemConfig({
          circuitBreakerFailureThreshold: 5,
          circuitBreakerOpenDurationSeconds: 30,
          circuitBreakerSuccessThreshold: 3,
          dailyBudgetUsd: 10.0,
          monthlyBudgetUsd: 100.0,
          fallbackChainJson: '[]',
        })
      ).rejects.toThrow('Failed to update LLM system config');
    });
  });
});
