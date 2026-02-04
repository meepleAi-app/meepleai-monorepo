/**
 * useTierStrategy - TanStack Query hooks for Tier-Strategy Configuration
 * Issue #3440: Admin UI for tier-strategy configuration
 */

import {
  useQuery,
  useMutation,
  useQueryClient,
  type UseQueryResult,
  type UseMutationResult,
} from '@tanstack/react-query';

import {
  api,
  type TierStrategyMatrixDto,
  type StrategyModelMappingDto,
  type TierStrategyAccessDto,
  type TierStrategyResetResultDto,
  type UpdateTierStrategyAccessRequest,
  type UpdateStrategyModelMappingRequest,
  type ResetTierStrategyConfigRequest,
} from '@/lib/api';

/**
 * Query key factory for tier-strategy queries
 */
export const tierStrategyKeys = {
  all: ['tierStrategy'] as const,
  matrix: () => [...tierStrategyKeys.all, 'matrix'] as const,
  modelMappings: () => [...tierStrategyKeys.all, 'modelMappings'] as const,
};

/**
 * Hook to fetch the tier-strategy access matrix
 */
export function useTierStrategyMatrix(
  enabled: boolean = true
): UseQueryResult<TierStrategyMatrixDto, Error> {
  return useQuery({
    queryKey: tierStrategyKeys.matrix(),
    queryFn: async (): Promise<TierStrategyMatrixDto> => {
      return api.tierStrategy.getMatrix();
    },
    enabled,
    staleTime: 2 * 60 * 1000, // 2 minutes
    refetchOnWindowFocus: true,
    retry: (failureCount, error) => {
      if (error instanceof Error && (error.message.includes('401') || error.message.includes('403'))) {
        return false;
      }
      return failureCount < 3;
    },
  });
}

/**
 * Hook to fetch strategy-model mappings
 */
export function useStrategyModelMappings(
  enabled: boolean = true
): UseQueryResult<StrategyModelMappingDto[], Error> {
  return useQuery({
    queryKey: tierStrategyKeys.modelMappings(),
    queryFn: async (): Promise<StrategyModelMappingDto[]> => {
      return api.tierStrategy.getModelMappings();
    },
    enabled,
    staleTime: 2 * 60 * 1000,
    refetchOnWindowFocus: true,
    retry: (failureCount, error) => {
      if (error instanceof Error && (error.message.includes('401') || error.message.includes('403'))) {
        return false;
      }
      return failureCount < 3;
    },
  });
}

/**
 * Hook to update tier-strategy access
 */
export function useUpdateTierStrategyAccess(): UseMutationResult<
  TierStrategyAccessDto,
  Error,
  UpdateTierStrategyAccessRequest,
  { previousMatrix: TierStrategyMatrixDto | undefined }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: UpdateTierStrategyAccessRequest) => {
      return api.tierStrategy.updateAccess(request);
    },

    onMutate: async request => {
      await queryClient.cancelQueries({ queryKey: tierStrategyKeys.matrix() });

      const previousMatrix = queryClient.getQueryData<TierStrategyMatrixDto>(
        tierStrategyKeys.matrix()
      );

      // Optimistic update
      if (previousMatrix) {
        queryClient.setQueryData<TierStrategyMatrixDto>(tierStrategyKeys.matrix(), old => {
          if (!old) return old;
          return {
            ...old,
            accessMatrix: old.accessMatrix.map(entry =>
              entry.tier === request.tier && entry.strategy === request.strategy
                ? { ...entry, isEnabled: request.isEnabled, isDefault: false }
                : entry
            ),
          };
        });
      }

      return { previousMatrix };
    },

    onError: (_error, _request, context) => {
      if (context?.previousMatrix) {
        queryClient.setQueryData(tierStrategyKeys.matrix(), context.previousMatrix);
      }
    },

    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: tierStrategyKeys.matrix() });
    },
  });
}

/**
 * Hook to update strategy-model mapping
 */
export function useUpdateStrategyModelMapping(): UseMutationResult<
  StrategyModelMappingDto,
  Error,
  UpdateStrategyModelMappingRequest,
  { previousMappings: StrategyModelMappingDto[] | undefined }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: UpdateStrategyModelMappingRequest) => {
      return api.tierStrategy.updateModelMapping(request);
    },

    onMutate: async request => {
      await queryClient.cancelQueries({ queryKey: tierStrategyKeys.modelMappings() });

      const previousMappings = queryClient.getQueryData<StrategyModelMappingDto[]>(
        tierStrategyKeys.modelMappings()
      );

      // Optimistic update
      if (previousMappings) {
        queryClient.setQueryData<StrategyModelMappingDto[]>(
          tierStrategyKeys.modelMappings(),
          old => {
            if (!old) return old;
            return old.map(mapping =>
              mapping.strategy === request.strategy
                ? {
                    ...mapping,
                    provider: request.provider,
                    primaryModel: request.primaryModel,
                    fallbackModels: request.fallbackModels ?? mapping.fallbackModels,
                    isDefault: false,
                  }
                : mapping
            );
          }
        );
      }

      return { previousMappings };
    },

    onError: (_error, _request, context) => {
      if (context?.previousMappings) {
        queryClient.setQueryData(tierStrategyKeys.modelMappings(), context.previousMappings);
      }
    },

    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: tierStrategyKeys.modelMappings() });
    },
  });
}

/**
 * Hook to reset tier-strategy configuration to defaults
 */
export function useResetTierStrategyConfig(): UseMutationResult<
  TierStrategyResetResultDto,
  Error,
  ResetTierStrategyConfigRequest | undefined
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request?: ResetTierStrategyConfigRequest) => {
      return api.tierStrategy.reset(request);
    },

    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: tierStrategyKeys.all });
    },
  });
}
