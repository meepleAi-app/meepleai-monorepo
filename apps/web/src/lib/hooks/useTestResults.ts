/**
 * Test Results Hooks (Issue #3379)
 *
 * React Query hooks for test results history & persistence.
 */

import {
  useQuery,
  useMutation,
  useQueryClient,
  type UseQueryOptions,
  type UseMutationOptions,
} from '@tanstack/react-query';

import { api } from '@/lib/api';
import type {
  TestResult,
  TestResultList,
  SaveTestResultRequest,
  TestResultsQuery,
} from '@/lib/api/schemas/test-results.schemas';

// Query keys
export const TEST_RESULTS_QUERY_KEYS = {
  all: ['testResults'] as const,
  list: (query?: TestResultsQuery) => [...TEST_RESULTS_QUERY_KEYS.all, 'list', query] as const,
  detail: (id: string) => [...TEST_RESULTS_QUERY_KEYS.all, 'detail', id] as const,
};

/**
 * Hook for fetching paginated test results with filters
 */
export function useTestResults(
  query?: TestResultsQuery,
  options?: Omit<UseQueryOptions<TestResultList, Error>, 'queryKey' | 'queryFn'>
) {
  return useQuery<TestResultList, Error>({
    queryKey: TEST_RESULTS_QUERY_KEYS.list(query),
    queryFn: () => api.testResults.getAll(query),
    ...options,
  });
}

/**
 * Hook for fetching a single test result by ID
 */
export function useTestResult(
  id: string,
  options?: Omit<UseQueryOptions<TestResult | null, Error>, 'queryKey' | 'queryFn'>
) {
  return useQuery<TestResult | null, Error>({
    queryKey: TEST_RESULTS_QUERY_KEYS.detail(id),
    queryFn: () => api.testResults.getById(id),
    enabled: !!id,
    ...options,
  });
}

/**
 * Hook for saving a test result
 */
export function useSaveTestResult(
  options?: Omit<UseMutationOptions<{ id: string }, Error, SaveTestResultRequest>, 'mutationFn'>
) {
  const queryClient = useQueryClient();

  return useMutation<{ id: string }, Error, SaveTestResultRequest>({
    mutationFn: (request) => api.testResults.save(request),
    onSuccess: () => {
      // Invalidate all test results queries to refresh the list
      queryClient.invalidateQueries({ queryKey: TEST_RESULTS_QUERY_KEYS.all });
    },
    ...options,
  });
}

/**
 * Hook for deleting a test result
 */
export function useDeleteTestResult(
  options?: Omit<UseMutationOptions<void, Error, string>, 'mutationFn'>
) {
  const queryClient = useQueryClient();

  return useMutation<void, Error, string>({
    mutationFn: (id) => api.testResults.delete(id),
    onSuccess: () => {
      // Invalidate all test results queries to refresh the list
      queryClient.invalidateQueries({ queryKey: TEST_RESULTS_QUERY_KEYS.all });
    },
    ...options,
  });
}
