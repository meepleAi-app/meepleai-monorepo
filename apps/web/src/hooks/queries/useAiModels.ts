/**
 * useAiModels - TanStack Query hooks for AI Models Management
 *
 * Issue #2521: Admin AI Models Management Dashboard
 *
 * Provides caching, polling, and mutations for AI model configuration and cost tracking.
 */

import {
  useQuery,
  useMutation,
  useQueryClient,
  UseQueryResult,
  UseMutationResult,
} from '@tanstack/react-query';

import {
  api,
  type AiModelDto,
  type PagedAiModels,
  type ConfigureModelRequest,
  type SetPrimaryModelRequest,
  type CostTrackingDto,
  type TestModelRequest,
  type TestModelResponse,
} from '@/lib/api';

/**
 * Query key factory for AI models queries
 */
export const aiModelsKeys = {
  all: ['aiModels'] as const,
  lists: () => [...aiModelsKeys.all, 'list'] as const,
  list: (params?: { status?: string; page?: number; pageSize?: number }) =>
    [...aiModelsKeys.lists(), { params }] as const,
  detail: (modelId: string) => [...aiModelsKeys.all, 'detail', modelId] as const,
  costTracking: () => [...aiModelsKeys.all, 'costTracking'] as const,
};

/**
 * Hook to fetch all AI models with usage statistics
 *
 * Features:
 * - Automatic caching per filter/page combination
 * - Auto-refresh every 30 seconds (cost data changes frequently)
 * - Pagination support
 *
 * @param params - Filter and pagination parameters
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with paginated AI models
 */
export function useAiModels(
  params?: { status?: 'active' | 'inactive' | 'all'; page?: number; pageSize?: number },
  enabled: boolean = true
): UseQueryResult<PagedAiModels, Error> {
  return useQuery({
    queryKey: aiModelsKeys.list(params),
    queryFn: async (): Promise<PagedAiModels> => {
      return api.admin.getAiModels(params);
    },
    enabled,
    // Refresh every 30 seconds for real-time usage stats
    refetchInterval: 30_000,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: true,
    staleTime: 30_000,
    retry: (failureCount, error) => {
      // Don't retry on auth errors
      if (error instanceof Error && (error.message.includes('401') || error.message.includes('403'))) {
        return false;
      }
      return failureCount < 3;
    },
  });
}

/**
 * Hook to fetch single AI model by ID
 *
 * @param modelId - Model UUID
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with model details
 */
export function useAiModel(
  modelId: string,
  enabled: boolean = true
): UseQueryResult<AiModelDto, Error> {
  return useQuery({
    queryKey: aiModelsKeys.detail(modelId),
    queryFn: async () => {
      return api.admin.getAiModelById(modelId);
    },
    enabled: enabled && !!modelId,
    // Model config changes less frequently (2 minutes)
    staleTime: 2 * 60 * 1000,
  });
}

/**
 * Hook to fetch cost tracking data
 *
 * Features:
 * - Auto-refresh every 30 seconds for real-time monitoring
 * - Budget alerts based on percentage thresholds
 *
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with cost tracking data
 */
export function useCostTracking(
  enabled: boolean = true
): UseQueryResult<CostTrackingDto, Error> {
  return useQuery({
    queryKey: aiModelsKeys.costTracking(),
    queryFn: async () => {
      return api.admin.getCostTracking();
    },
    enabled,
    // Refresh every 30 seconds for real-time cost monitoring
    refetchInterval: 30_000,
    refetchIntervalInBackground: false,
    refetchOnWindowFocus: true,
    staleTime: 30_000,
  });
}

/**
 * Hook to update AI model configuration
 *
 * Features:
 * - Optimistic updates for immediate UI feedback
 * - Automatic cache invalidation
 * - Rollback on error
 *
 * @returns UseMutationResult for updating model configuration
 */
export function useUpdateModelConfig(): UseMutationResult<
  AiModelDto,
  Error,
  { modelId: string; request: ConfigureModelRequest },
  { previousModel: AiModelDto | undefined }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ modelId, request }) => {
      return api.admin.updateModelConfig(modelId, request);
    },

    // Optimistic update
    onMutate: async ({ modelId, request }) => {
      await queryClient.cancelQueries({ queryKey: aiModelsKeys.detail(modelId) });

      const previousModel = queryClient.getQueryData<AiModelDto>(aiModelsKeys.detail(modelId));

      queryClient.setQueryData<AiModelDto>(aiModelsKeys.detail(modelId), (old) => {
        if (!old) return old;
        return {
          ...old,
          ...request,
          updatedAt: new Date().toISOString(),
        };
      });

      return { previousModel };
    },

    onError: (_error, { modelId }, context) => {
      if (context?.previousModel) {
        queryClient.setQueryData(aiModelsKeys.detail(modelId), context.previousModel);
      }
    },

    onSettled: (_data, _error, { modelId }) => {
      queryClient.invalidateQueries({ queryKey: aiModelsKeys.detail(modelId) });
      queryClient.invalidateQueries({ queryKey: aiModelsKeys.lists() });
    },
  });
}

/**
 * Hook to set primary AI model
 *
 * Features:
 * - Optimistic updates (unset previous primary, set new primary)
 * - Automatic cache invalidation
 * - Rollback on error
 *
 * @returns UseMutationResult for setting primary model
 */
export function useSetPrimaryModel(): UseMutationResult<
  AiModelDto,
  Error,
  SetPrimaryModelRequest,
  { previousModels: PagedAiModels | undefined }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request) => {
      return api.admin.setPrimaryModel(request);
    },

    // Optimistic update: unset previous primary, set new primary
    onMutate: async (request) => {
      await queryClient.cancelQueries({ queryKey: aiModelsKeys.lists() });

      const previousModels = queryClient.getQueryData<PagedAiModels>(aiModelsKeys.lists()[0]);

      queryClient.setQueryData<PagedAiModels>(aiModelsKeys.lists()[0], (old) => {
        if (!old) return old;
        return {
          ...old,
          items: old.items.map((model) => ({
            ...model,
            isPrimary: model.id === request.modelId,
          })),
        };
      });

      return { previousModels };
    },

    onError: (_error, _request, context) => {
      if (context?.previousModels) {
        queryClient.setQueryData(aiModelsKeys.lists()[0], context.previousModels);
      }
    },

    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: aiModelsKeys.lists() });
    },
  });
}

/**
 * Hook to test AI model with sample prompt
 *
 * @returns UseMutationResult for testing model
 */
export function useTestModel(): UseMutationResult<
  TestModelResponse,
  Error,
  { modelId: string; request: TestModelRequest }
> {
  return useMutation({
    mutationFn: async ({ modelId, request }) => {
      return api.admin.testModel(modelId, request);
    },
  });
}
