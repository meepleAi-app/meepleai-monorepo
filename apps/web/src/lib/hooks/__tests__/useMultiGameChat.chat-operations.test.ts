import { renderHook, act, waitFor } from '@testing-library/react';
import { useMultiGameChat, Chat } from '../useMultiGameChat';
import * as api from '../../api';
import { mockNewChat } from './useMultiGameChat.test-helpers';

// Mock the API module
vi.mock('../../api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
}));

const mockApi = api.api as Mocked<typeof api.api>;

/**
 * Tests for useMultiGameChat chat operations functionality
 * Covers: chat creation, deletion, and chat lifecycle management
 */
describe('useMultiGameChat - Chat Operations', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Create New Chat', () => {
    it('should create new chat and add to chats list', async () => {
      mockApi.post.mockResolvedValueOnce(mockNewChat);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      let createdChat: Chat | null = null;

      await act(async () => {
        createdChat = await result.current.createNewChat('game-1', 'agent-1');
      });

      expect(createdChat).toEqual(mockNewChat);
      expect(mockApi.post).toHaveBeenCalledWith('/api/v1/chats', {
        gameId: 'game-1',
        agentId: 'agent-1',
      });

      await waitFor(() => {
        expect(result.current.chats).toContainEqual(mockNewChat);
      });
    });

    it('should set new chat as active and clear messages', async () => {
      mockApi.post.mockResolvedValueOnce(mockNewChat);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await act(async () => {
        await result.current.createNewChat('game-1', 'agent-1');
      });

      await waitFor(() => {
        expect(result.current.activeChatId).toBe('chat-new');
        expect(result.current.messages).toEqual([]);
      });
    });

    it('should handle API errors when creating chat', async () => {
      const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      mockApi.post.mockRejectedValueOnce(new Error('Failed to create chat'));

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      let createdChat: Chat | null = 'unset' as any;

      await act(async () => {
        try {
          createdChat = await result.current.createNewChat('game-1', 'agent-1');
        } catch (e) {
          // Swallow error - we're testing error handling
          createdChat = null;
        }
      });

      await waitFor(() => {
        expect(createdChat).toBeNull();
      }, { timeout: 1000 });

      expect(consoleErrorSpy).toHaveBeenCalledWith(
        'Error creating chat:',
        expect.any(Error)
      );

      consoleErrorSpy.mockRestore();
    });

    it('should return null if API returns no chat', async () => {
      mockApi.post.mockResolvedValueOnce(null);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      let createdChat: Chat | null = null;

      await act(async () => {
        createdChat = await result.current.createNewChat('game-1', 'agent-1');
      });

      expect(createdChat).toBeNull();
    });
  });

  describe('Delete Chat', () => {
    const mockChats: Chat[] = [
      {
        id: 'chat-1',
        gameId: 'game-1',
        gameName: 'Catan',
        agentId: 'agent-1',
        agentName: 'Rules Agent',
        startedAt: '2025-01-01T00:00:00Z',
        lastMessageAt: null,
      },
      {
        id: 'chat-2',
        gameId: 'game-1',
        gameName: 'Catan',
        agentId: 'agent-1',
        agentName: 'Rules Agent',
        startedAt: '2025-01-01T01:00:00Z',
        lastMessageAt: null,
      },
    ];

    it('should delete chat and remove from chats list', async () => {
      mockApi.get.mockResolvedValueOnce(mockChats);
      mockApi.delete.mockResolvedValueOnce(undefined);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // Load chats first
      await act(async () => {
        await result.current.switchGame('game-1');
      });

      await waitFor(() => {
        expect(result.current.chats).toHaveLength(2);
      });

      // Delete chat-1
      await act(async () => {
        await result.current.deleteChat('chat-1');
      });

      await waitFor(() => {
        expect(result.current.chats).toHaveLength(1);
        expect(result.current.chats[0].id).toBe('chat-2');
      });

      expect(mockApi.delete).toHaveBeenCalledWith('/api/v1/chats/chat-1');
    });

    it('should clear activeChatId and messages if deleting active chat', async () => {
      mockApi.get.mockResolvedValueOnce(mockChats);
      mockApi.delete.mockResolvedValueOnce(undefined);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // Load chats and set active
      await act(async () => {
        await result.current.switchGame('game-1');
      });

      await waitFor(() => {
        expect(result.current.chats).toHaveLength(2);
      });

      act(() => {
        result.current.setActiveChatId('chat-1');
        result.current.setMessages([
          {
            id: 'msg-1',
            role: 'user',
            content: 'Test',
            timestamp: new Date(),
          },
        ]);
      });

      // Delete active chat
      await act(async () => {
        await result.current.deleteChat('chat-1');
      });

      await waitFor(() => {
        expect(result.current.activeChatId).toBeNull();
        expect(result.current.messages).toEqual([]);
      });
    });

    it('should not clear messages if deleting non-active chat', async () => {
      mockApi.get.mockResolvedValueOnce(mockChats);
      mockApi.delete.mockResolvedValueOnce(undefined);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await act(async () => {
        await result.current.switchGame('game-1');
      });

      await waitFor(() => {
        expect(result.current.chats).toHaveLength(2);
      });

      const testMessages = [
        {
          id: 'msg-1',
          role: 'user' as const,
          content: 'Test',
          timestamp: new Date(),
        },
      ];

      act(() => {
        result.current.setActiveChatId('chat-2');
        result.current.setMessages(testMessages);
      });

      // Delete chat-1 (not active)
      await act(async () => {
        await result.current.deleteChat('chat-1');
      });

      await waitFor(() => {
        expect(result.current.chats).toHaveLength(1);
      });

      expect(result.current.activeChatId).toBe('chat-2');
      expect(result.current.messages).toEqual(testMessages);
    });

    it('should handle API errors when deleting chat', async () => {
      const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      mockApi.delete.mockRejectedValueOnce(new Error('Delete failed'));

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await act(async () => {
        try {
          await result.current.deleteChat('chat-1');
        } catch (e) {
          // Swallow error - we're testing error handling
        }
      });

      await waitFor(() => {
        expect(consoleErrorSpy).toHaveBeenCalled();
      }, { timeout: 1000 });

      expect(consoleErrorSpy).toHaveBeenCalledWith(
        'Error deleting chat:',
        expect.any(Error)
      );

      consoleErrorSpy.mockRestore();
    });

    it('should not delete if no active game', async () => {
      const { result } = renderHook(() => useMultiGameChat(null));

      await act(async () => {
        await result.current.deleteChat('chat-1');
      });

      expect(mockApi.delete).not.toHaveBeenCalled();
    });
  });
});
