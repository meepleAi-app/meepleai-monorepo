/**
 * Admin Client - LLM System Config Methods Tests (Issue #5495, #4169)
 *
 * Tests for getLlmSystemConfig, updateLlmSystemConfig and getLlmBudgetUsage.
 * #4169: the request carries what the LLM gateway applies (caps, degrade threshold, quotas) and no
 * longer the circuit breaker thresholds nor the fallback chain, which the runtime does not read.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createAdminClient } from '@/lib/api/clients/adminClient';
import type { HttpClient } from '@/lib/api/core/httpClient';
import type {
  LlmBudgetUsage,
  LlmSystemConfigDto,
  UpdateLlmSystemConfigRequest,
} from '@/lib/api/schemas';

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
  dailyBudgetUsd: 1.0,
  monthlyBudgetUsd: 20.0,
  degradeThresholdPercent: 80,
  backgroundQuotaPercent: 30,
  evaluationQuotaPercent: 20,
  source: 'database',
  lastUpdatedAt: '2026-03-09T12:00:00Z',
  lastUpdatedByUserId: '00000000-0000-0000-0000-000000000001',
};

const request: UpdateLlmSystemConfigRequest = {
  dailyBudgetUsd: 2.0,
  monthlyBudgetUsd: 20.0,
  degradeThresholdPercent: 70,
  backgroundQuotaPercent: 30,
  evaluationQuotaPercent: 20,
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
      const updatedConfig = { ...mockConfig, ...request };
      vi.mocked(mockHttpClient.put).mockResolvedValue(updatedConfig);

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

      await expect(client.updateLlmSystemConfig(request)).rejects.toThrow(
        'Failed to update LLM system config'
      );
    });
  });

  describe('getLlmBudgetUsage (#4169)', () => {
    it('should GET from /api/v1/admin/llm/budget/usage', async () => {
      const usage: LlmBudgetUsage = {
        available: true,
        day: '2026-10-11',
        today: { totalUsd: 0.4, answersUsd: 0.3, backgroundUsd: 0.1, evaluationsUsd: 0 },
        month: { totalUsd: 4, answersUsd: 3, backgroundUsd: 0.5, evaluationsUsd: 0.5 },
        isDegraded: false,
      };
      vi.mocked(mockHttpClient.get).mockResolvedValue(usage);

      const result = await client.getLlmBudgetUsage();

      expect(result).toEqual(usage);
      expect(mockHttpClient.get).toHaveBeenCalledWith(
        '/api/v1/admin/llm/budget/usage',
        expect.any(Object)
      );
    });
  });
});
