/**
 * Tests for useChats hooks
 *
 * Issue #1255: FE-QUALITY — Restore 90% test coverage
 *
 * Tests TanStack Query hooks for chat thread management
 */

import type { Mock } from 'vitest';
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
vi.mock('@/lib/api', () => ({
  api: {
    chat: {
      getThreadsByGame: vi.fn(),
      getThreadById: vi.fn(),
      createThread: vi.fn(),
      addMessage: vi.fn(),
      updateMessage: vi.fn(),
      deleteMessage: vi.fn(),
      closeThread: vi.fn(),
      reopenThread: vi.fn(),
    },
  },
}));

describe('useChats hooks', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );

  describe('chatKeys', () => {
    it('generates correct query keys', () => {
      expect(chatKeys.all).toEqual(['chats']);
      expect(chatKeys.byGame('770e8400-e29b-41d4-a716-000000000001')).toEqual(['chats', 'game', '770e8400-e29b-41d4-a716-000000000001']);
      expect(chatKeys.detail('aa0e8400-e29b-41d4-a716-000000000001')).toEqual(['chats', 'detail', 'aa0e8400-e29b-41d4-a716-000000000001']);
      expect(chatKeys.messages('aa0e8400-e29b-41d4-a716-000000000001')).toEqual(['chats', 'messages', 'aa0e8400-e29b-41d4-a716-000000000001']);
    });
  });

  describe('useChats', () => {
    it('fetches chats for a game', async () => {
      const mockChats = [
        { id: 'chat-1', gameId: '770e8400-e29b-41d4-a716-000000000001', title: 'Chat 1', messages: [] },
        { id: 'chat-2', gameId: '770e8400-e29b-41d4-a716-000000000001', title: 'Chat 2', messages: [] },
      ];
      (api.chat.getThreadsByGame as Mock).mockResolvedValue(mockChats);

      const { result } = renderHook(() => useChats('770e8400-e29b-41d4-a716-000000000001'), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.getThreadsByGame).toHaveBeenCalledWith('770e8400-e29b-41d4-a716-000000000001');
      expect(result.current.data).toEqual(mockChats);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useChats('770e8400-e29b-41d4-a716-000000000001', false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.chat.getThreadsByGame).not.toHaveBeenCalled();
    });

    it('handles fetch errors', async () => {
      const error = new Error('Failed to fetch chats');
      (api.chat.getThreadsByGame as Mock).mockRejectedValue(error);

      const { result } = renderHook(() => useChats('770e8400-e29b-41d4-a716-000000000001'), { wrapper });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(error);
    });
  });

  describe('useChatThread', () => {
    it('fetches a single chat thread', async () => {
      const mockThread = { id: 'aa0e8400-e29b-41d4-a716-000000000001', gameId: '770e8400-e29b-41d4-a716-000000000001', title: 'Thread', messages: [] };
      (api.chat.getThreadById as Mock).mockResolvedValue(mockThread);

      const { result } = renderHook(() => useChatThread('aa0e8400-e29b-41d4-a716-000000000001'), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.getThreadById).toHaveBeenCalledWith('aa0e8400-e29b-41d4-a716-000000000001');
      expect(result.current.data).toEqual(mockThread);
    });

    it('does not fetch when disabled', () => {
      const { result } = renderHook(() => useChatThread('aa0e8400-e29b-41d4-a716-000000000001', false), { wrapper });

      expect(result.current.isFetching).toBe(false);
      expect(api.chat.getThreadById).not.toHaveBeenCalled();
    });
  });

  describe('useMessages', () => {
    it('fetches messages for a chat (alias for useChatThread)', async () => {
      const mockThread = { id: 'aa0e8400-e29b-41d4-a716-000000000001', gameId: '770e8400-e29b-41d4-a716-000000000001', title: 'Thread', messages: [] };
      (api.chat.getThreadById as Mock).mockResolvedValue(mockThread);

      const { result } = renderHook(() => useMessages('aa0e8400-e29b-41d4-a716-000000000001'), { wrapper });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.getThreadById).toHaveBeenCalledWith('aa0e8400-e29b-41d4-a716-000000000001');
      expect(result.current.data).toEqual(mockThread);
    });
  });

  describe('useCreateChat', () => {
    it('creates a new chat thread', async () => {
      const request = { gameId: '770e8400-e29b-41d4-a716-000000000001', title: 'New Chat' };
      const mockNewChat = { id: 'new-chat', ...request, messages: [] };
      (api.chat.createThread as Mock).mockResolvedValue(mockNewChat);

      const { result } = renderHook(() => useCreateChat(), { wrapper });

      result.current.mutate(request);

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.createThread).toHaveBeenCalledWith(request);
      expect(result.current.data).toEqual(mockNewChat);
    });

    it('invalidates game chats query on success', async () => {
      const request = { gameId: '770e8400-e29b-41d4-a716-000000000001', title: 'New Chat' };
      const mockNewChat = { id: 'new-chat', ...request, messages: [] };
      (api.chat.createThread as Mock).mockResolvedValue(mockNewChat);

      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useCreateChat(), { wrapper });

      result.current.mutate(request);

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.byGame('770e8400-e29b-41d4-a716-000000000001'),
      });
    });
  });

  describe('useAddMessage', () => {
    it('adds a message to a thread', async () => {
      const request = { content: 'Hello', role: 'user' as const };
      const mockUpdatedThread = {
        id: 'aa0e8400-e29b-41d4-a716-000000000001',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        messages: [{ id: 'msg-1', ...request }],
      };
      (api.chat.addMessage as Mock).mockResolvedValue(mockUpdatedThread);

      const { result } = renderHook(() => useAddMessage(), { wrapper });

      result.current.mutate({ threadId: 'aa0e8400-e29b-41d4-a716-000000000001', request });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.addMessage).toHaveBeenCalledWith('aa0e8400-e29b-41d4-a716-000000000001', request);
      expect(result.current.data).toEqual(mockUpdatedThread);
    });

    it('invalidates thread and game queries on success', async () => {
      const request = { content: 'Hello', role: 'user' as const };
      const mockUpdatedThread = {
        id: 'aa0e8400-e29b-41d4-a716-000000000001',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        messages: [{ id: 'msg-1', ...request }],
      };
      (api.chat.addMessage as Mock).mockResolvedValue(mockUpdatedThread);

      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useAddMessage(), { wrapper });

      result.current.mutate({ threadId: 'aa0e8400-e29b-41d4-a716-000000000001', request });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.detail('aa0e8400-e29b-41d4-a716-000000000001'),
      });
      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.byGame('770e8400-e29b-41d4-a716-000000000001'),
      });
    });
  });

  describe('useEditMessage', () => {
    it('edits a message', async () => {
      const variables = { chatId: 'chat-1', messageId: 'msg-1', content: 'Updated' };
      (api.chat.updateMessage as Mock).mockResolvedValue({ success: true });

      const { result } = renderHook(() => useEditMessage(), { wrapper });

      result.current.mutate(variables);

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.updateMessage).toHaveBeenCalledWith('chat-1', 'msg-1', 'Updated');
    });

    it('invalidates chat thread on success', async () => {
      const variables = { chatId: 'chat-1', messageId: 'msg-1', content: 'Updated' };
      (api.chat.updateMessage as Mock).mockResolvedValue({ success: true });

      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

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
      (api.chat.deleteMessage as Mock).mockResolvedValue(undefined);

      const { result } = renderHook(() => useDeleteMessage(), { wrapper });

      result.current.mutate(variables);

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.deleteMessage).toHaveBeenCalledWith('chat-1', 'msg-1');
    });

    it('invalidates chat thread on success', async () => {
      const variables = { chatId: 'chat-1', messageId: 'msg-1' };
      (api.chat.deleteMessage as Mock).mockResolvedValue(undefined);

      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

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
        id: 'aa0e8400-e29b-41d4-a716-000000000001',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        closed: true,
        messages: [],
      };
      (api.chat.closeThread as Mock).mockResolvedValue(mockClosedThread);

      const { result } = renderHook(() => useCloseChat(), { wrapper });

      result.current.mutate('aa0e8400-e29b-41d4-a716-000000000001');

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.closeThread).toHaveBeenCalledWith('aa0e8400-e29b-41d4-a716-000000000001');
      expect(result.current.data).toEqual(mockClosedThread);
    });

    it('invalidates thread and game queries on success', async () => {
      const mockClosedThread = {
        id: 'aa0e8400-e29b-41d4-a716-000000000001',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        closed: true,
        messages: [],
      };
      (api.chat.closeThread as Mock).mockResolvedValue(mockClosedThread);

      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useCloseChat(), { wrapper });

      result.current.mutate('aa0e8400-e29b-41d4-a716-000000000001');

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.detail('aa0e8400-e29b-41d4-a716-000000000001'),
      });
      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.byGame('770e8400-e29b-41d4-a716-000000000001'),
      });
    });
  });

  describe('useReopenChat', () => {
    it('reopens a closed chat thread', async () => {
      const mockReopenedThread = {
        id: 'aa0e8400-e29b-41d4-a716-000000000001',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        closed: false,
        messages: [],
      };
      (api.chat.reopenThread as Mock).mockResolvedValue(mockReopenedThread);

      const { result } = renderHook(() => useReopenChat(), { wrapper });

      result.current.mutate('aa0e8400-e29b-41d4-a716-000000000001');

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.chat.reopenThread).toHaveBeenCalledWith('aa0e8400-e29b-41d4-a716-000000000001');
      expect(result.current.data).toEqual(mockReopenedThread);
    });

    it('invalidates thread and game queries on success', async () => {
      const mockReopenedThread = {
        id: 'aa0e8400-e29b-41d4-a716-000000000001',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        closed: false,
        messages: [],
      };
      (api.chat.reopenThread as Mock).mockResolvedValue(mockReopenedThread);

      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

      const { result } = renderHook(() => useReopenChat(), { wrapper });

      result.current.mutate('aa0e8400-e29b-41d4-a716-000000000001');

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.detail('aa0e8400-e29b-41d4-a716-000000000001'),
      });
      expect(invalidateSpy).toHaveBeenCalledWith({
        queryKey: chatKeys.byGame('770e8400-e29b-41d4-a716-000000000001'),
      });
    });
  });
});
