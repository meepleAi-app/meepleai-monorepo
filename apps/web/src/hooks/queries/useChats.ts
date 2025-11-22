/**
 * useChats - TanStack Query hooks for chat threads
 *
 * Issue #1079: FE-IMP-003 — TanStack Query Data Layer
 *
 * Provides automatic caching for chat threads and messages.
 */

import { useQuery, useMutation, useQueryClient, UseQueryResult, UseMutationResult } from '@tanstack/react-query';
import { api, ChatThreadDto, ChatMessageResponse, CreateChatThreadRequest, AddMessageRequest } from '@/lib/api';

/**
 * Query key factory for chat queries
 */
export const chatKeys = {
  all: ['chats'] as const,
  byGame: (gameId: string) => [...chatKeys.all, 'game', gameId] as const,
  detail: (threadId: string) => [...chatKeys.all, 'detail', threadId] as const,
  messages: (threadId: string) => [...chatKeys.all, 'messages', threadId] as const,
};

/**
 * Hook to fetch chat threads for a specific game
 *
 * Features:
 * - Automatic caching per game
 * - Refetch on window focus (detect new chats from other tabs)
 *
 * @param gameId Game ID
 * @param enabled Whether to run the query (default: true)
 * @returns UseQueryResult with chat threads
 */
export function useChats(gameId: string, enabled: boolean = true): UseQueryResult<ChatThreadDto[], Error> {
  return useQuery({
    queryKey: chatKeys.byGame(gameId),
    queryFn: async (): Promise<ChatThreadDto[]> => {
      return (api.chat as any).getThreadsByGame(gameId);
    },
    enabled,
    staleTime: 2 * 60 * 1000, // Chats change more frequently (2min)
    refetchOnWindowFocus: true,
  });
}

/**
 * Hook to fetch single chat thread by ID
 *
 * @param threadId Thread ID
 * @param enabled Whether to run the query (default: true)
 * @returns UseQueryResult with chat thread details
 */
export function useChatThread(threadId: string, enabled: boolean = true): UseQueryResult<ChatThreadDto | null, Error> {
  return useQuery({
    queryKey: chatKeys.detail(threadId),
    queryFn: async (): Promise<ChatThreadDto | null> => {
      return (api.chat as any).getThreadById(threadId);
    },
    enabled,
    staleTime: 1 * 60 * 1000, // Active chat thread (1min stale time)
  });
}

/**
 * Hook to fetch messages for a chat thread
 * Alias for useChatThread (messages are included in thread response)
 *
 * @param chatId Thread ID
 * @param enabled Whether to run the query (default: true)
 * @returns UseQueryResult with chat thread (includes messages)
 */
export function useMessages(chatId: string, enabled: boolean = true): UseQueryResult<ChatThreadDto | null, Error> {
  return useChatThread(chatId, enabled);
}

/**
 * Mutation hook to create a new chat thread
 *
 * Features:
 * - Automatically invalidates game's chat list on success
 * - Optimistic updates (optional)
 *
 * @returns UseMutationResult for creating chat
 */
export function useCreateChat(): UseMutationResult<ChatThreadDto, Error, CreateChatThreadRequest> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: CreateChatThreadRequest): Promise<ChatThreadDto> => {
      return (api.chat as any).createThread(request);
    },
    onSuccess: (newChat) => {
      // Invalidate the game's chat list to refetch
      if (newChat.gameId) {
        queryClient.invalidateQueries({ queryKey: chatKeys.byGame(newChat.gameId) });
      }
    },
  });
}

/**
 * Mutation hook to add a message to a chat thread
 *
 * Features:
 * - Automatically invalidates thread on success
 * - Optimistic updates support
 *
 * @returns UseMutationResult for adding message
 */
export function useAddMessage(): UseMutationResult<
  ChatThreadDto,
  Error,
  { threadId: string; request: AddMessageRequest }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ threadId, request }): Promise<ChatThreadDto> => {
      return (api.chat as any).addMessage(threadId, request);
    },
    onSuccess: (updatedThread, variables) => {
      // Invalidate the specific thread to refetch
      queryClient.invalidateQueries({ queryKey: chatKeys.detail(variables.threadId) });

      // Also invalidate the game's chat list (message count changed)
      if (updatedThread.gameId) {
        queryClient.invalidateQueries({ queryKey: chatKeys.byGame(updatedThread.gameId) });
      }
    },
  });
}

/**
 * Mutation hook to edit a message
 *
 * @returns UseMutationResult with the updated message (ChatMessageResponse)
 *          Note: Invalidates the chat thread query to trigger a full refetch
 */
export function useEditMessage(): UseMutationResult<
  ChatMessageResponse,
  Error,
  { chatId: string; messageId: string; content: string }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ chatId, messageId, content }): Promise<ChatMessageResponse> => {
      return (api.chat as any).updateMessage(chatId, messageId, content);
    },
    onSuccess: (_, variables) => {
      // Invalidate the chat thread to refetch
      queryClient.invalidateQueries({ queryKey: chatKeys.detail(variables.chatId) });
    },
  });
}

/**
 * Mutation hook to delete a message
 *
 * @returns UseMutationResult for deleting message
 */
export function useDeleteMessage(): UseMutationResult<
  void,
  Error,
  { chatId: string; messageId: string }
> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ chatId, messageId }) => {
      return (api.chat as any).deleteMessage(chatId, messageId);
    },
    onSuccess: (_, variables) => {
      // Invalidate the chat thread to refetch
      queryClient.invalidateQueries({ queryKey: chatKeys.detail(variables.chatId) });
    },
  });
}

/**
 * Mutation hook to close a chat thread
 *
 * @returns UseMutationResult for closing chat
 */
export function useCloseChat(): UseMutationResult<ChatThreadDto, Error, string> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (threadId: string): Promise<ChatThreadDto> => {
      return (api.chat as any).closeThread(threadId);
    },
    onSuccess: (closedThread, threadId) => {
      // Invalidate the specific thread
      queryClient.invalidateQueries({ queryKey: chatKeys.detail(threadId) });

      // Invalidate the game's chat list
      if (closedThread.gameId) {
        queryClient.invalidateQueries({ queryKey: chatKeys.byGame(closedThread.gameId) });
      }
    },
  });
}

/**
 * Mutation hook to reopen a closed chat thread
 *
 * @returns UseMutationResult for reopening chat
 */
export function useReopenChat(): UseMutationResult<ChatThreadDto, Error, string> {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (threadId: string): Promise<ChatThreadDto> => {
      return (api.chat as any).reopenThread(threadId);
    },
    onSuccess: (reopenedThread, threadId) => {
      // Invalidate the specific thread
      queryClient.invalidateQueries({ queryKey: chatKeys.detail(threadId) });

      // Invalidate the game's chat list
      if (reopenedThread.gameId) {
        queryClient.invalidateQueries({ queryKey: chatKeys.byGame(reopenedThread.gameId) });
      }
    },
  });
}
