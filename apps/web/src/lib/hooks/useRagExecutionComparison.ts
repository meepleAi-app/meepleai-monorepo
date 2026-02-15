/**
 * useRagExecutionComparison Hook (Issue #4459)
 *
 * React Query hook for comparing two RAG executions side-by-side.
 */

import {
  useMutation,
  type UseMutationOptions,
} from '@tanstack/react-query';

import { api } from '@/lib/api';
import type {
  CompareExecutionsRequest,
  ExecutionComparison,
} from '@/lib/api/schemas/rag-execution.schemas';

/**
 * Hook for comparing two RAG executions.
 * Uses mutation because comparison is triggered on-demand with execution IDs.
 */
export function useRagExecutionComparison(
  options?: Omit<
    UseMutationOptions<ExecutionComparison, Error, CompareExecutionsRequest>,
    'mutationFn'
  >
) {
  return useMutation<ExecutionComparison, Error, CompareExecutionsRequest>({
    mutationFn: (request) => api.ragExecution.compare(request),
    ...options,
  });
}
