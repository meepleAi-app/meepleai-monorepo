/**
 * React Query hooks for chat sessions.
 * @see Issue #4695
 */

import { useQuery } from '@tanstack/react-query';

import { useAuth } from '@/components/auth/AuthProvider';
import { api } from '@/lib/api';

export const chatSessionKeys = {
  all: ['chat-sessions'] as const,
  recent: (userId: string, limit?: number) =>
    [...chatSessionKeys.all, 'recent', userId, { limit }] as const,
  byGame: (userId: string, gameId: string) =>
    [...chatSessionKeys.all, 'byGame', userId, gameId] as const,
  detail: (sessionId: string) =>
    [...chatSessionKeys.all, 'detail', sessionId] as const,
  limit: (userId: string) =>
    [...chatSessionKeys.all, 'limit', userId] as const,
};

/**
 * Fetch user's recent chat sessions across all games.
 */
export function useRecentChatSessions(limit = 50) {
  const { user } = useAuth();
  const userId = user?.id;

  return useQuery({
    queryKey: chatSessionKeys.recent(userId ?? '', limit),
    queryFn: () => api.chatSessions.getRecent(userId!, { limit }),
    enabled: !!userId,
    staleTime: 30_000,
  });
}

/**
 * Fetch the user's chat session tier limit and current usage.
 * Issue #4913: Used to show the usage banner.
 */
export function useChatSessionLimit() {
  const { user } = useAuth();
  const userId = user?.id;

  return useQuery({
    queryKey: chatSessionKeys.limit(userId ?? ''),
    queryFn: () => api.chatSessions.getLimit(userId!),
    enabled: !!userId,
    staleTime: 60_000,
  });
}

/**
 * Fetch a single chat session by ID.
 */
export function useChatSession(sessionId: string | undefined) {
  return useQuery({
    queryKey: chatSessionKeys.detail(sessionId ?? ''),
    queryFn: () => api.chatSessions.getById(sessionId!),
    enabled: !!sessionId,
    staleTime: 10_000,
  });
}
