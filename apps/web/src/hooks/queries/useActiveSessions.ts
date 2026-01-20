/**
 * useActiveSessions - TanStack Query hooks for active game sessions
 *
 * Provides automatic caching for active session data on dashboard.
 * Issue #2617: Session Tracking Integration
 */

import { useQuery, useMutation, useQueryClient, UseQueryResult, UseMutationResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { PaginatedSessionsResponse, GameSessionDto } from '@/lib/api/schemas';

/**
 * Query key factory for sessions queries
 */
export const sessionsKeys = {
  all: ['sessions'] as const,
  active: () => [...sessionsKeys.all, 'active'] as const,
  activeList: (limit?: number) => [...sessionsKeys.active(), { limit }] as const,
  detail: (id: string) => [...sessionsKeys.all, 'detail', id] as const,
  history: (filters?: Record<string, unknown>) => [...sessionsKeys.all, 'history', { filters }] as const,
};

/**
 * Hook to fetch active sessions for dashboard widget
 *
 * Returns up to `limit` active sessions (InProgress, Paused, Setup).
 * Uses short stale time since sessions change frequently.
 *
 * @param limit - Maximum number of sessions to return (default: 5)
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with active sessions data
 */
export function useActiveSessions(
  limit: number = 5,
  enabled: boolean = true
): UseQueryResult<PaginatedSessionsResponse, Error> {
  return useQuery({
    queryKey: sessionsKeys.activeList(limit),
    queryFn: async (): Promise<PaginatedSessionsResponse> => {
      return api.sessions.getActive(limit, 0);
    },
    enabled,
    staleTime: 30 * 1000, // Sessions change frequently (30s)
    refetchOnWindowFocus: true,
  });
}

/**
 * Hook to fetch a single session by ID
 *
 * @param sessionId - Session ID
 * @param enabled - Whether to run the query (default: true)
 * @returns UseQueryResult with session data
 */
export function useSession(
  sessionId: string,
  enabled: boolean = true
): UseQueryResult<GameSessionDto | null, Error> {
  return useQuery({
    queryKey: sessionsKeys.detail(sessionId),
    queryFn: async (): Promise<GameSessionDto | null> => {
      return api.sessions.getById(sessionId);
    },
    enabled: enabled && !!sessionId,
    staleTime: 30 * 1000,
  });
}

/**
 * Hook to pause an active session
 *
 * @returns UseMutationResult for pausing a session
 */
export function usePauseSession(): UseMutationResult<GameSessionDto, Error, string> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (sessionId: string): Promise<GameSessionDto> => {
      return api.sessions.pause(sessionId);
    },
    onSuccess: () => {
      // Invalidate active sessions to refresh the list
      queryClient.invalidateQueries({ queryKey: sessionsKeys.active() });
    },
  });
}

/**
 * Hook to resume a paused session
 *
 * @returns UseMutationResult for resuming a session
 */
export function useResumeSession(): UseMutationResult<GameSessionDto, Error, string> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (sessionId: string): Promise<GameSessionDto> => {
      return api.sessions.resume(sessionId);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: sessionsKeys.active() });
    },
  });
}

/**
 * Hook to end a session
 *
 * @returns UseMutationResult for ending a session
 */
export function useEndSession(): UseMutationResult<
  GameSessionDto,
  Error,
  { sessionId: string; winnerName?: string }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      sessionId,
      winnerName,
    }: {
      sessionId: string;
      winnerName?: string;
    }): Promise<GameSessionDto> => {
      return api.sessions.end(sessionId, winnerName);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: sessionsKeys.active() });
      queryClient.invalidateQueries({ queryKey: sessionsKeys.history() });
    },
  });
}
