/**
 * useChatSessions - TanStack Query hooks for chat session persistence
 *
 * Issue #3484: Frontend Chat History Integration
 * Backend: Issue #3483 - Chat Session Persistence Service
 *
 * Provides automatic caching for chat sessions with user/game associations.
 */

import {
  useQuery,
  useMutation,
  useQueryClient,
  UseQueryResult,
  UseMutationResult,
  useInfiniteQuery,
  UseInfiniteQueryResult,
} from '@tanstack/react-query';

import {
  api,
  type ChatSessionDto,
  type ChatSessionSummaryDto,
  type CreateChatSessionRequest,
} from '@/lib/api';

/**
 * Query key factory for chat session queries
 */
export const chatSessionKeys = {
  all: ['chatSessions'] as const,
  recent: (userId: string) => [...chatSessionKeys.all, 'recent', userId] as const,
  byUserGame: (userId: string, gameId: string) =>
    [...chatSessionKeys.all, 'userGame', userId, gameId] as const,
  detail: (sessionId: string) => [...chatSessionKeys.all, 'detail', sessionId] as const,
};

/**
 * Hook to fetch user's recent chat sessions across all games
 *
 * Features:
 * - Automatic caching per user
 * - Refetch on window focus (detect new sessions from other tabs)
 *
 * @param userId User ID
 * @param options Query options
 * @returns UseQueryResult with recent chat sessions
 */
export function useRecentChatSessions(
  userId: string | undefined,
  options?: { limit?: number; enabled?: boolean }
): UseQueryResult<{ sessions: ChatSessionSummaryDto[]; totalCount: number }, Error> {
  return useQuery({
    queryKey: chatSessionKeys.recent(userId ?? ''),
    queryFn: async () => {
      if (!userId) return { sessions: [], totalCount: 0 };
      return api.chatSessions.getRecent(userId, { limit: options?.limit ?? 10 });
    },
    enabled: !!userId && (options?.enabled ?? true),
    staleTime: 2 * 60 * 1000, // 2 minutes - sessions change frequently
    refetchOnWindowFocus: true,
  });
}

/**
 * Hook to fetch user's chat sessions for a specific game
 *
 * @param userId User ID
 * @param gameId Game ID
 * @param options Query options
 * @returns UseQueryResult with chat sessions for the game
 */
export function useGameChatSessions(
  userId: string | undefined,
  gameId: string | undefined,
  options?: { page?: number; pageSize?: number; enabled?: boolean }
): UseQueryResult<{ sessions: ChatSessionSummaryDto[]; totalCount: number }, Error> {
  return useQuery({
    queryKey: chatSessionKeys.byUserGame(userId ?? '', gameId ?? ''),
    queryFn: async () => {
      if (!userId || !gameId) return { sessions: [], totalCount: 0 };
      return api.chatSessions.getByUserAndGame(userId, gameId, {
        page: options?.page,
        pageSize: options?.pageSize,
      });
    },
    enabled: !!userId && !!gameId && (options?.enabled ?? true),
    staleTime: 2 * 60 * 1000,
    refetchOnWindowFocus: true,
  });
}

/**
 * Hook to fetch user's chat sessions for a game with infinite scroll
 *
 * @param userId User ID
 * @param gameId Game ID
 * @param pageSize Number of items per page
 * @returns UseInfiniteQueryResult with paginated chat sessions
 */
export function useInfiniteGameChatSessions(
  userId: string | undefined,
  gameId: string | undefined,
  pageSize: number = 10
): UseInfiniteQueryResult<{ sessions: ChatSessionSummaryDto[]; totalCount: number }, Error> {
  return useInfiniteQuery({
    queryKey: [...chatSessionKeys.byUserGame(userId ?? '', gameId ?? ''), 'infinite'],
    queryFn: async ({ pageParam = 1 }) => {
      if (!userId || !gameId) return { sessions: [], totalCount: 0 };
      return api.chatSessions.getByUserAndGame(userId, gameId, {
        page: pageParam,
        pageSize,
      });
    },
    initialPageParam: 1,
    getNextPageParam: (lastPage, allPages) => {
      const loadedCount = allPages.reduce((acc, page) => acc + page.sessions.length, 0);
      return loadedCount < lastPage.totalCount ? allPages.length + 1 : undefined;
    },
    enabled: !!userId && !!gameId,
    staleTime: 2 * 60 * 1000,
  });
}

/**
 * Hook to fetch a single chat session by ID
 *
 * @param sessionId Session ID
 * @param enabled Whether to run the query (default: true)
 * @returns UseQueryResult with chat session details
 */
export function useChatSession(
  sessionId: string | undefined,
  enabled: boolean = true
): UseQueryResult<ChatSessionDto | null, Error> {
  return useQuery({
    queryKey: chatSessionKeys.detail(sessionId ?? ''),
    queryFn: async () => {
      if (!sessionId) return null;
      return api.chatSessions.getById(sessionId);
    },
    enabled: !!sessionId && enabled,
    staleTime: 1 * 60 * 1000, // 1 minute for active sessions
  });
}

/**
 * Mutation hook to create a new chat session
 *
 * Features:
 * - Automatically invalidates recent sessions on success
 * - Automatically invalidates game-specific sessions on success
 *
 * @returns UseMutationResult for creating chat session
 */
export function useCreateChatSession(): UseMutationResult<
  ChatSessionDto,
  Error,
  CreateChatSessionRequest
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: CreateChatSessionRequest) => {
      return api.chatSessions.create(request);
    },
    onSuccess: (newSession) => {
      // Invalidate user's recent sessions
      queryClient.invalidateQueries({
        queryKey: chatSessionKeys.recent(newSession.userId),
      });
      // Invalidate user's sessions for this game
      queryClient.invalidateQueries({
        queryKey: chatSessionKeys.byUserGame(newSession.userId, newSession.gameId),
      });
    },
  });
}

/**
 * Mutation hook to add a message to a chat session
 *
 * Features:
 * - Automatically invalidates the session on success
 * - Automatically invalidates recent sessions list
 *
 * @returns UseMutationResult for adding message
 */
export function useAddChatSessionMessage(): UseMutationResult<
  ChatSessionDto,
  Error,
  {
    sessionId: string;
    role: 'user' | 'assistant' | 'system';
    content: string;
    metadata?: Record<string, unknown> | null;
  }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ sessionId, ...request }) => {
      return api.chatSessions.addMessage(sessionId, request);
    },
    onSuccess: (updatedSession, variables) => {
      // Update the cached session
      queryClient.setQueryData(
        chatSessionKeys.detail(variables.sessionId),
        updatedSession
      );
      // Invalidate recent sessions (timestamp changed)
      queryClient.invalidateQueries({
        queryKey: chatSessionKeys.recent(updatedSession.userId),
      });
      // Invalidate game sessions list
      queryClient.invalidateQueries({
        queryKey: chatSessionKeys.byUserGame(updatedSession.userId, updatedSession.gameId),
      });
    },
  });
}

/**
 * Mutation hook to delete a chat session
 *
 * Features:
 * - Automatically invalidates all related caches on success
 *
 * @returns UseMutationResult for deleting chat session
 */
export function useDeleteChatSession(): UseMutationResult<
  void,
  Error,
  { sessionId: string; userId: string; gameId: string }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ sessionId }) => {
      return api.chatSessions.delete(sessionId);
    },
    onSuccess: (_, variables) => {
      // Remove from cache
      queryClient.removeQueries({
        queryKey: chatSessionKeys.detail(variables.sessionId),
      });
      // Invalidate user's recent sessions
      queryClient.invalidateQueries({
        queryKey: chatSessionKeys.recent(variables.userId),
      });
      // Invalidate user's game sessions
      queryClient.invalidateQueries({
        queryKey: chatSessionKeys.byUserGame(variables.userId, variables.gameId),
      });
    },
  });
}
