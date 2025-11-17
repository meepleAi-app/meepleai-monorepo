/**
 * Tests for useChats hooks
 *
 * Issue #1255: FE-QUALITY — Restore 90% test coverage
 *
 * Tests TanStack Query hooks for chat thread management
 */

import { renderHook, waitFor } from '@testing-library/react';
import {
  useChats,
  useChatThread,
  useMessages,
  useCreateChat,
  useAddMessage,
  useEditMessage,
  useDeleteMessage,
  useCloseChat,
  useReopenChat,
  chatKeys,
} from '../useChats';
import { createTestQueryClient, renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { api } from '@/lib/api';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';

// Mock API module
jest.mock('@/lib/api', () => ({
  api: {
    chat: {
      getThreadsByGame: jest.fn(),
      getThreadById: jest.fn(),
      createThread: jest.fn(),
      addMessage: jest.fn(),
      updateMessage: jest.fn(),
      deleteMessage: jest.fn(),
      closeThread: jest.fn(),
      reopenThread: jest.fn(),
    },
  },
}));

describe('useChats hooks', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createTestQueryClient();
    jest.clearAllMocks();
  });

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  describe('chatKeys', () => {
    it('generates correct query keys', () => {
      expect(chatKeys.all).toEqual(['chats']);
      expect(chatKeys.byGame('game-1')).toEqual(['chats', 'game', 'game-1']);
      expect(chatKeys.detail('thread-1')).toEqual(['chats', 'detail', 'thread-1']);
      expect(chatKeys.messages('thread-1')).toEqual(['chats', 'messages', 'thread-1']);
    });
  });

  describe('useChats', () => {
    it('fetches chats for a game', async () => {
      const mockChats = [
        { id: 'chat-1', gameId: 'game-1', title: 'Chat 1', messages: [] },
        { id: 'chat-2', gameId: 'game-1', title: 'Chat 2', messages: [] },
      ];
      (api.chat.getThreadsByGame as jest.Mock).mockResolvedValue(mockChats);

      const { result } = renderHook(() => useChats('game-1'), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.getThreadsByGame).toHaveBeenCalledWith('game-1');
      expect(result.current.data).toEqual(mockChats);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useChats('game-1', false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.chat.getThreadsByGame).not.toHaveBeenCalled();
    });

    it('handles fetch errors', async () => {
      const error = new Error('Failed to fetch chats');
      (api.chat.getThreadsByGame as jest.Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useChats('game-1'), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });
  });

  describe('useChatThread', () => {
    it('fetches a single chat thread', async () => {
      const mockThread = { id: 'thread-1', gameId: 'game-1', title: 'Thread', messages: [] };
      (api.chat.getThreadById as jest.Mock).mockResolvedValue(mockThread);

      const { result } = renderHook(() => useChatThread('thread-1'), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.getThreadById).toHaveBeenCalledWith('thread-1');
      expect(result.current.data).toEqual(mockThread);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useChatThread('thread-1', false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.chat.getThreadById).not.toHaveBeenCalled();
    });
  });

  describe('useMessages', () => {
    it('fetches messages for a chat (alias for useChatThread)', async () => {
      const mockThread = { id: 'thread-1', gameId: 'game-1', title: 'Thread', messages: [] };
      (api.chat.getThreadById as jest.Mock).mockResolvedValue(mockThread);

      const { result } = renderHook(() => useMessages('thread-1'), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.getThreadById).toHaveBeenCalledWith('thread-1');
      expect(result.current.data).toEqual(mockThread);
    });
  });

  describe('useCreateChat', () => {
    it('creates a new chat thread', async () => {
      const request = { gameId: 'game-1', title: 'New Chat' };
      const mockNewChat = { id: 'new-chat', ...request, messages: [] };
      (api.chat.createThread as jest.Mock).mockResolvedValue(mockNewChat);

      const { result } = renderHook(() => useCreateChat(), { wrapper });

      result.current.mutate(request);

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.createThread).toHaveBeenCalledWith(request);
      expect(result.current.data).toEqual(mockNewChat);
    });

    it('invalidates game chats query on success', async () => {
      const request = { gameId: 'game-1', title: 'New Chat' };
      const mockNewChat = { id: 'new-chat', ...request, messages: [] };
      (api.chat.createThread as jest.Mock).mockResolvedValue(mockNewChat);

      const invalidateSpy = jest.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useCreateChat(), { wrapper });

      result.current.mutate(request);

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.byGame('game-1'),
      });
    });
  });

  describe('useAddMessage', () => {
    it('adds a message to a thread', async () => {
      const request = { content: 'Hello', role: 'user' as const };
      const mockUpdatedThread = {
        id: 'thread-1',
        gameId: 'game-1',
        messages: [{ id: 'msg-1', ...request }],
      };
      (api.chat.addMessage as jest.Mock).mockResolvedValue(mockUpdatedThread);

      const { result } = renderHook(() => useAddMessage(), { wrapper });

      result.current.mutate({ threadId: 'thread-1', request });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.addMessage).toHaveBeenCalledWith('thread-1', request);
      expect(result.current.data).toEqual(mockUpdatedThread);
    });

    it('invalidates thread and game queries on success', async () => {
      const request = { content: 'Hello', role: 'user' as const };
      const mockUpdatedThread = {
        id: 'thread-1',
        gameId: 'game-1',
        messages: [{ id: 'msg-1', ...request }],
      };
      (api.chat.addMessage as jest.Mock).mockResolvedValue(mockUpdatedThread);

      const invalidateSpy = jest.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useAddMessage(), { wrapper });

      result.current.mutate({ threadId: 'thread-1', request });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.detail('thread-1'),
      });
      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.byGame('game-1'),
      });
    });
  });

  describe('useEditMessage', () => {
    it('edits a message', async () => {
      const variables = { chatId: 'chat-1', messageId: 'msg-1', content: 'Updated' };
      (api.chat.updateMessage as jest.Mock).mockResolvedValue({ success: true });

      const { result } = renderHook(() => useEditMessage(), { wrapper });

      result.current.mutate(variables);

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.updateMessage).toHaveBeenCalledWith('chat-1', 'msg-1', 'Updated');
    });

    it('invalidates chat thread on success', async () => {
      const variables = { chatId: 'chat-1', messageId: 'msg-1', content: 'Updated' };
      (api.chat.updateMessage as jest.Mock).mockResolvedValue({ success: true });

      const invalidateSpy = jest.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useEditMessage(), { wrapper });

      result.current.mutate(variables);

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.detail('chat-1'),
      });
    });
  });

  describe('useDeleteMessage', () => {
    it('deletes a message', async () => {
      const variables = { chatId: 'chat-1', messageId: 'msg-1' };
      (api.chat.deleteMessage as jest.Mock).mockResolvedValue(undefined);

      const { result } = renderHook(() => useDeleteMessage(), { wrapper });

      result.current.mutate(variables);

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.deleteMessage).toHaveBeenCalledWith('chat-1', 'msg-1');
    });

    it('invalidates chat thread on success', async () => {
      const variables = { chatId: 'chat-1', messageId: 'msg-1' };
      (api.chat.deleteMessage as jest.Mock).mockResolvedValue(undefined);

      const invalidateSpy = jest.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useDeleteMessage(), { wrapper });

      result.current.mutate(variables);

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.detail('chat-1'),
      });
    });
  });

  describe('useCloseChat', () => {
    it('closes a chat thread', async () => {
      const mockClosedThread = {
        id: 'thread-1',
        gameId: 'game-1',
        closed: true,
        messages: [],
      };
      (api.chat.closeThread as jest.Mock).mockResolvedValue(mockClosedThread);

      const { result } = renderHook(() => useCloseChat(), { wrapper });

      result.current.mutate('thread-1');

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.closeThread).toHaveBeenCalledWith('thread-1');
      expect(result.current.data).toEqual(mockClosedThread);
    });

    it('invalidates thread and game queries on success', async () => {
      const mockClosedThread = {
        id: 'thread-1',
        gameId: 'game-1',
        closed: true,
        messages: [],
      };
      (api.chat.closeThread as jest.Mock).mockResolvedValue(mockClosedThread);

      const invalidateSpy = jest.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useCloseChat(), { wrapper });

      result.current.mutate('thread-1');

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.detail('thread-1'),
      });
      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.byGame('game-1'),
      });
    });
  });

  describe('useReopenChat', () => {
    it('reopens a closed chat thread', async () => {
      const mockReopenedThread = {
        id: 'thread-1',
        gameId: 'game-1',
        closed: false,
        messages: [],
      };
      (api.chat.reopenThread as jest.Mock).mockResolvedValue(mockReopenedThread);

      const { result } = renderHook(() => useReopenChat(), { wrapper });

      result.current.mutate('thread-1');

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.reopenThread).toHaveBeenCalledWith('thread-1');
      expect(result.current.data).toEqual(mockReopenedThread);
    });

    it('invalidates thread and game queries on success', async () => {
      const mockReopenedThread = {
        id: 'thread-1',
        gameId: 'game-1',
        closed: false,
        messages: [],
      };
      (api.chat.reopenThread as jest.Mock).mockResolvedValue(mockReopenedThread);

      const invalidateSpy = jest.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useReopenChat(), { wrapper });

      result.current.mutate('thread-1');

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.detail('thread-1'),
      });
      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.byGame('game-1'),
      });
    });
  });
});
