import {
  useQuery,
  useMutation,
  useQueryClient,
  type UseQueryResult,
  type UseMutationResult,
} from '@tanstack/react-query';
import { api } from '@/lib/api';
import type {
  CreateShareRequestCommand,
  CreateShareRequestResponse,
  PaginatedShareRequestsResponse,
  RateLimitStatusDto,
  UserShareRequestDto,
} from '@/lib/api';
import type { GetUserShareRequestsParams } from '@/lib/api/clients/shareRequestsClient';

/**
 * React Query hooks for Share Requests
 * Issue #2743: Frontend - UI Condivisione da Libreria
 */

/**
 * Query key factory for share requests
 */
export const shareRequestsKeys = {
  all: ['shareRequests'] as const,
  lists: () => [...shareRequestsKeys.all, 'list'] as const,
  list: (params?: GetUserShareRequestsParams) =>
    [...shareRequestsKeys.lists(), { params }] as const,
  detail: (id: string) => [...shareRequestsKeys.all, 'detail', id] as const,
  rateLimit: () => [...shareRequestsKeys.all, 'rateLimit'] as const,
};

/**
 * Get paginated list of user's share requests
 */
export function useShareRequests(
  params?: GetUserShareRequestsParams,
  enabled: boolean = true
): UseQueryResult<PaginatedShareRequestsResponse, Error> {
  return useQuery({
    queryKey: shareRequestsKeys.list(params),
    queryFn: async (): Promise<PaginatedShareRequestsResponse> => {
      return api.shareRequests.getUserShareRequests(params);
    },
    enabled,
    staleTime: 30 * 1000, // 30 seconds
  });
}

/**
 * Get a specific share request by ID
 */
export function useShareRequest(
  id: string,
  enabled: boolean = true
): UseQueryResult<UserShareRequestDto, Error> {
  return useQuery({
    queryKey: shareRequestsKeys.detail(id),
    queryFn: async (): Promise<UserShareRequestDto> => {
      return api.shareRequests.getShareRequestById(id);
    },
    enabled: enabled && !!id,
    staleTime: 30 * 1000, // 30 seconds
  });
}

/**
 * Get current rate limit status for the user
 */
export function useRateLimitStatus(
  enabled: boolean = true
): UseQueryResult<RateLimitStatusDto, Error> {
  return useQuery({
    queryKey: shareRequestsKeys.rateLimit(),
    queryFn: async (): Promise<RateLimitStatusDto> => {
      return api.shareRequests.getRateLimitStatus();
    },
    enabled,
    staleTime: 10 * 1000, // 10 seconds (fresher for rate limit checks)
    refetchInterval: 30 * 1000, // Auto-refetch every 30 seconds
  });
}

/**
 * Create a new share request
 */
export function useCreateShareRequest(): UseMutationResult<
  CreateShareRequestResponse,
  Error,
  CreateShareRequestCommand
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (command: CreateShareRequestCommand) => {
      return api.shareRequests.createShareRequest(command);
    },
    onSuccess: () => {
      // Invalidate lists to show the new request
      queryClient.invalidateQueries({ queryKey: shareRequestsKeys.lists() });
      // Invalidate rate limit to update counts
      queryClient.invalidateQueries({ queryKey: shareRequestsKeys.rateLimit() });
    },
  });
}
