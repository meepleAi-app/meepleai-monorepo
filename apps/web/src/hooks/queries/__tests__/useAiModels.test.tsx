/**
 * Tests for useAiModels hooks
 *
 * Issue #2761: Sprint 1 - Query Hooks Coverage
 *
 * Tests TanStack Query hooks for AI models management (admin).
 */

import type { Mock } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import {
  useAiModels,
  useAiModel,
  useCostTracking,
  useUpdateModelConfig,
  useSetPrimaryModel,
  useTestModel,
  aiModelsKeys,
} from '../useAiModels';
import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { api } from '@/lib/api';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';
import type {
  AiModelDto,
  PagedAiModels,
  CostTrackingDto,
  TestModelResponse,
} from '@/lib/api';

// Mock API module
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAiModels: vi.fn(),
      getAiModelById: vi.fn(),
      getCostTracking: vi.fn(),
      updateModelConfig: vi.fn(),
      setPrimaryModel: vi.fn(),
      testModel: vi.fn(),
    },
  },
}));

describe('useAiModels hooks', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  // ==================== Query Keys ====================

  describe('aiModelsKeys', () => {
    it('generates correct base query keys', () => {
      expect(aiModelsKeys.all).toEqual(['aiModels']);
      expect(aiModelsKeys.lists()).toEqual(['aiModels', 'list']);
      expect(aiModelsKeys.costTracking()).toEqual(['aiModels', 'costTracking']);
    });

    it('generates correct list query keys with params', () => {
      expect(aiModelsKeys.list()).toEqual(['aiModels', 'list', { params: undefined }]);
      expect(aiModelsKeys.list({ status: 'active' })).toEqual([
        'aiModels',
        'list',
        { params: { status: 'active' } },
      ]);
      expect(aiModelsKeys.list({ page: 2, pageSize: 10 })).toEqual([
        'aiModels',
        'list',
        { params: { page: 2, pageSize: 10 } },
      ]);
    });

    it('generates correct detail query keys', () => {
      const modelId = 'model-123';
      expect(aiModelsKeys.detail(modelId)).toEqual(['aiModels', 'detail', modelId]);
    });

    it('generates unique keys for different params', () => {
      const key1 = aiModelsKeys.list({ status: 'active' });
      const key2 = aiModelsKeys.list({ status: 'inactive' });
      const key3 = aiModelsKeys.list({ page: 1 });

      expect(key1).not.toEqual(key2);
      expect(key1).not.toEqual(key3);
    });
  });

  // ==================== useAiModels ====================

  describe('useAiModels', () => {
    const mockModel: AiModelDto = {
      id: 'model-1',
      name: 'GPT-4',
      provider: 'openai',
      modelId: 'gpt-4-turbo',
      isActive: true,
      isPrimary: true,
      inputCostPer1k: 0.01,
      outputCostPer1k: 0.03,
      maxTokens: 128000,
      usageCount: 1500,
      totalCost: 45.50,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-15T00:00:00Z',
    };

    const mockModelsResponse: PagedAiModels = {
      items: [
        mockModel,
        {
          ...mockModel,
          id: 'model-2',
          name: 'Claude 3',
          provider: 'anthropic',
          modelId: 'claude-3-opus',
          isPrimary: false,
          usageCount: 800,
          totalCost: 24.00,
        },
      ],
      totalCount: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    };

    it('fetches AI models successfully', async () => {
      (api.admin.getAiModels as Mock).mockResolvedValue(mockModelsResponse);

      const { result } = renderHook(() => useAiModels(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.admin.getAiModels).toHaveBeenCalledWith(undefined);
      expect(result.current.data).toEqual(mockModelsResponse);
      expect(result.current.data?.items).toHaveLength(2);
    });

    it('fetches models with status filter', async () => {
      (api.admin.getAiModels as Mock).mockResolvedValue(mockModelsResponse);

      const params = { status: 'active' as const };
      const { result } = renderHook(() => useAiModels(params), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.admin.getAiModels).toHaveBeenCalledWith(params);
    });

    it('fetches models with pagination', async () => {
      (api.admin.getAiModels as Mock).mockResolvedValue(mockModelsResponse);

      const params = { page: 2, pageSize: 10 };
      const { result } = renderHook(() => useAiModels(params), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.admin.getAiModels).toHaveBeenCalledWith(params);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useAiModels(undefined, false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.admin.getAiModels).not.toHaveBeenCalled();
    });

    it('handles fetch errors', async () => {
      // Use auth error to skip retries (hook has custom retry: 3 for non-auth errors)
      const error = new Error('Failed to fetch models - 401 Unauthorized');
      (api.admin.getAiModels as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useAiModels(), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });

    it('does not retry on 401 auth error', async () => {
      const authError = new Error('401 Unauthorized');
      (api.admin.getAiModels as Mock).mockRejectedValue(authError);

      const { result } = renderHook(() => useAiModels(), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      // Should only have been called once (no retry)
      expect(api.admin.getAiModels).toHaveBeenCalledTimes(1);
    });

    it('does not retry on 403 forbidden error', async () => {
      const forbiddenError = new Error('403 Forbidden');
      (api.admin.getAiModels as Mock).mockRejectedValue(forbiddenError);

      const { result } = renderHook(() => useAiModels(), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(api.admin.getAiModels).toHaveBeenCalledTimes(1);
    });

    it('returns empty list when no models', async () => {
      const emptyResponse: PagedAiModels = {
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 20,
        totalPages: 0,
      };
      (api.admin.getAiModels as Mock).mockResolvedValue(emptyResponse);

      const { result } = renderHook(() => useAiModels(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data?.items).toHaveLength(0);
    });
  });

  // ==================== useAiModel ====================

  describe('useAiModel', () => {
    const modelId = 'model-123';
    const mockModel: AiModelDto = {
      id: modelId,
      name: 'GPT-4',
      provider: 'openai',
      modelId: 'gpt-4-turbo',
      isActive: true,
      isPrimary: true,
      inputCostPer1k: 0.01,
      outputCostPer1k: 0.03,
      maxTokens: 128000,
      usageCount: 1500,
      totalCost: 45.50,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-15T00:00:00Z',
    };

    it('fetches single model successfully', async () => {
      (api.admin.getAiModelById as Mock).mockResolvedValue(mockModel);

      const { result } = renderHook(() => useAiModel(modelId), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.admin.getAiModelById).toHaveBeenCalledWith(modelId);
      expect(result.current.data).toEqual(mockModel);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useAiModel(modelId, false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.admin.getAiModelById).not.toHaveBeenCalled();
    });

    it('does not fetch when modelId is empty', () => {
      const { result } = renderHook(() => useAiModel(''), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.admin.getAiModelById).not.toHaveBeenCalled();
    });

    it('handles fetch errors', async () => {
      const error = new Error('Model not found');
      (api.admin.getAiModelById as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useAiModel(modelId), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });
  });

  // ==================== useCostTracking ====================

  describe('useCostTracking', () => {
    const mockCostTracking: CostTrackingDto = {
      currentMonthCost: 150.50,
      previousMonthCost: 125.00,
      dailyAverage: 5.02,
      monthlyBudget: 500.00,
      budgetPercentUsed: 30.1,
      budgetRemaining: 349.50,
      costByModel: [
        { modelId: 'gpt-4', modelName: 'GPT-4', cost: 100.00, usageCount: 1000 },
        { modelId: 'claude-3', modelName: 'Claude 3', cost: 50.50, usageCount: 500 },
      ],
      lastUpdated: '2024-01-15T12:00:00Z',
    };

    it('fetches cost tracking successfully', async () => {
      (api.admin.getCostTracking as Mock).mockResolvedValue(mockCostTracking);

      const { result } = renderHook(() => useCostTracking(), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.admin.getCostTracking).toHaveBeenCalled();
      expect(result.current.data).toEqual(mockCostTracking);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useCostTracking(false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.admin.getCostTracking).not.toHaveBeenCalled();
    });

    it('handles fetch errors', async () => {
      const error = new Error('Failed to fetch cost tracking');
      (api.admin.getCostTracking as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useCostTracking(), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });
  });

  // ==================== useUpdateModelConfig ====================

  describe('useUpdateModelConfig', () => {
    const modelId = 'model-123';
    const existingModel: AiModelDto = {
      id: modelId,
      name: 'GPT-4',
      provider: 'openai',
      modelId: 'gpt-4-turbo',
      isActive: true,
      isPrimary: false,
      inputCostPer1k: 0.01,
      outputCostPer1k: 0.03,
      maxTokens: 128000,
      usageCount: 1500,
      totalCost: 45.50,
      createdAt: '2024-01-01T00:00:00Z',
      updatedAt: '2024-01-15T00:00:00Z',
    };

    const updatedModel: AiModelDto = {
      ...existingModel,
      isActive: false,
      maxTokens: 64000,
      updatedAt: new Date().toISOString(),
    };

    it('updates model config successfully', async () => {
      (api.admin.updateModelConfig as Mock).mockResolvedValue(updatedModel);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useUpdateModelConfig(), { wrapper });

      const request = { isActive: false, maxTokens: 64000 };
      await act(async () => {
        await result.current.mutateAsync({ modelId, request });
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(api.admin.updateModelConfig).toHaveBeenCalledWith(modelId, request);
      expect(result.current.data).toEqual(updatedModel);

      // Verify cache invalidation
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: aiModelsKeys.detail(modelId) });
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: aiModelsKeys.lists() });
    });

    it('performs optimistic update', async () => {
      queryClient.setQueryData(aiModelsKeys.detail(modelId), existingModel);

      (api.admin.updateModelConfig as Mock).mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve(updatedModel), 100))
      );

      const { result } = renderHook(() => useUpdateModelConfig(), { wrapper });

      act(() => {
        result.current.mutate({ modelId, request: { isActive: false } });
      });

      // Check optimistic update
      await waitFor(() => {
        const cachedData = queryClient.getQueryData<AiModelDto>(aiModelsKeys.detail(modelId));
        expect(cachedData?.isActive).toBe(false);
      });
    });

    it('rolls back on error', async () => {
      queryClient.setQueryData(aiModelsKeys.detail(modelId), existingModel);

      const error = new Error('Update failed');
      (api.admin.updateModelConfig as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useUpdateModelConfig(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync({ modelId, request: { isActive: false } });
        } catch {
          // Expected error
        }
      });

      // Verify rollback
      const cachedData = queryClient.getQueryData<AiModelDto>(aiModelsKeys.detail(modelId));
      expect(cachedData?.isActive).toBe(true); // Original value
    });

    it('handles update errors', async () => {
      const error = new Error('Permission denied');
      (api.admin.updateModelConfig as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useUpdateModelConfig(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync({ modelId, request: { isActive: false } });
        } catch (e) {
          expect(e).toEqual(error);
        }
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error).toEqual(error);
    });
  });

  // ==================== useSetPrimaryModel ====================

  describe('useSetPrimaryModel', () => {
    const mockModels: PagedAiModels = {
      items: [
        {
          id: 'model-1',
          name: 'GPT-4',
          provider: 'openai',
          modelId: 'gpt-4',
          isActive: true,
          isPrimary: true,
          inputCostPer1k: 0.01,
          outputCostPer1k: 0.03,
          maxTokens: 128000,
          usageCount: 1000,
          totalCost: 30.00,
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-15T00:00:00Z',
        },
        {
          id: 'model-2',
          name: 'Claude 3',
          provider: 'anthropic',
          modelId: 'claude-3',
          isActive: true,
          isPrimary: false,
          inputCostPer1k: 0.015,
          outputCostPer1k: 0.075,
          maxTokens: 200000,
          usageCount: 500,
          totalCost: 20.00,
          createdAt: '2024-01-02T00:00:00Z',
          updatedAt: '2024-01-15T00:00:00Z',
        },
      ],
      totalCount: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    };

    const updatedModel: AiModelDto = {
      ...mockModels.items[1],
      isPrimary: true,
    };

    it('sets primary model successfully', async () => {
      queryClient.setQueryData(aiModelsKeys.list(), mockModels);

      (api.admin.setPrimaryModel as Mock).mockResolvedValue(updatedModel);
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useSetPrimaryModel(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync({ modelId: 'model-2' });
      });

      expect(api.admin.setPrimaryModel).toHaveBeenCalledWith({ modelId: 'model-2' });

      // Verify cache invalidation
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: aiModelsKeys.lists() });
    });

    it('performs optimistic update on all list queries', async () => {
      queryClient.setQueryData(aiModelsKeys.list(), mockModels);

      (api.admin.setPrimaryModel as Mock).mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve(updatedModel), 100))
      );

      const { result } = renderHook(() => useSetPrimaryModel(), { wrapper });

      act(() => {
        result.current.mutate({ modelId: 'model-2' });
      });

      // Check optimistic update
      await waitFor(() => {
        const cachedData = queryClient.getQueryData<PagedAiModels>(aiModelsKeys.list());
        const model1 = cachedData?.items.find((m) => m.id === 'model-1');
        const model2 = cachedData?.items.find((m) => m.id === 'model-2');
        expect(model1?.isPrimary).toBe(false);
        expect(model2?.isPrimary).toBe(true);
      });
    });

    it('rolls back all queries on error', async () => {
      queryClient.setQueryData(aiModelsKeys.list(), mockModels);

      const error = new Error('Cannot set primary');
      (api.admin.setPrimaryModel as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useSetPrimaryModel(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync({ modelId: 'model-2' });
        } catch {
          // Expected error
        }
      });

      // Verify rollback
      const cachedData = queryClient.getQueryData<PagedAiModels>(aiModelsKeys.list());
      const model1 = cachedData?.items.find((m) => m.id === 'model-1');
      const model2 = cachedData?.items.find((m) => m.id === 'model-2');
      expect(model1?.isPrimary).toBe(true); // Original primary
      expect(model2?.isPrimary).toBe(false); // Original non-primary
    });

    it('handles set primary errors', async () => {
      const error = new Error('Model not found');
      (api.admin.setPrimaryModel as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useSetPrimaryModel(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync({ modelId: 'nonexistent' });
        } catch (e) {
          expect(e).toEqual(error);
        }
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error).toEqual(error);
    });
  });

  // ==================== useTestModel ====================

  describe('useTestModel', () => {
    const modelId = 'model-123';
    const mockTestResponse: TestModelResponse = {
      success: true,
      modelId,
      response: 'This is a test response from the AI model.',
      latencyMs: 1250,
      tokensUsed: 150,
      cost: 0.0045,
    };

    it('tests model successfully', async () => {
      (api.admin.testModel as Mock).mockResolvedValue(mockTestResponse);

      const { result } = renderHook(() => useTestModel(), { wrapper });

      const request = { prompt: 'Hello, how are you?', maxTokens: 100 };
      await act(async () => {
        await result.current.mutateAsync({ modelId, request });
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(api.admin.testModel).toHaveBeenCalledWith(modelId, request);
      expect(result.current.data).toEqual(mockTestResponse);
    });

    it('handles test errors', async () => {
      const error = new Error('Model test failed');
      (api.admin.testModel as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useTestModel(), { wrapper });

      await act(async () => {
        try {
          await result.current.mutateAsync({ modelId, request: { prompt: 'test' } });
        } catch (e) {
          expect(e).toEqual(error);
        }
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error).toEqual(error);
    });

    it('returns latency and cost metrics', async () => {
      (api.admin.testModel as Mock).mockResolvedValue(mockTestResponse);

      const { result } = renderHook(() => useTestModel(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync({ modelId, request: { prompt: 'test' } });
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(result.current.data?.latencyMs).toBe(1250);
      expect(result.current.data?.tokensUsed).toBe(150);
      expect(result.current.data?.cost).toBe(0.0045);
    });

    it('handles test failure response', async () => {
      const failedResponse: TestModelResponse = {
        success: false,
        modelId,
        response: '',
        latencyMs: 0,
        tokensUsed: 0,
        cost: 0,
        errorMessage: 'Model timeout',
      };
      (api.admin.testModel as Mock).mockResolvedValue(failedResponse);

      const { result } = renderHook(() => useTestModel(), { wrapper });

      await act(async () => {
        await result.current.mutateAsync({ modelId, request: { prompt: 'test' } });
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(result.current.data?.success).toBe(false);
      expect(result.current.data?.errorMessage).toBe('Model timeout');
    });
  });
});
