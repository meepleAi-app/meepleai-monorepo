/**
 * useAdminAiModels - TanStack Query hooks for the admin AI Models surface.
 *
 * Issue #1442 Phase 1a — wire `models-table.tsx` to the live API.
 * Mirrors the pattern in `useAdminCategories.ts` (#1440):
 *   - query key: `['admin-ai-models']`
 *   - mutation: `onSuccess` invalidates the list (no optimistic update —
 *     `usage` / `costPer1k` are server-derived and must round-trip).
 */

import {
  useMutation,
  useQuery,
  type UseMutationResult,
  type UseQueryResult,
} from '@tanstack/react-query';

import { listAdminAiModels, toggleAdminAiModel, type AiModelDto } from '@/lib/api/admin-ai-models';
import { getQueryClient } from '@/lib/queryClient';

export const adminAiModelsKeys = {
  all: ['admin-ai-models'] as const,
};

export function useAdminAiModels(): UseQueryResult<AiModelDto[], Error> {
  return useQuery({
    queryKey: adminAiModelsKeys.all,
    queryFn: listAdminAiModels,
    staleTime: 30_000,
  });
}

export function useToggleAdminAiModel(): UseMutationResult<AiModelDto, Error, string> {
  const queryClient = getQueryClient();
  return useMutation({
    mutationFn: toggleAdminAiModel,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: adminAiModelsKeys.all });
    },
  });
}
