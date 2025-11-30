import { renderHook, act, waitFor } from '@testing-library/react';
import { useMultiGameChat, Chat, ChatMessage } from '../useMultiGameChat';
import * as api from '../../api';
import { mockChatsGame1, mockChatsGame2 } from './useMultiGameChat.test-helpers';

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
 * Tests for useMultiGameChat state management functionality
 * Covers: initial state, multi-game state isolation, state setters, and state inspection
 */
        content: 'Hi',
        timestamp: new Date(),
      };

      act(() => {
        result.current.setMessages([message1]);
      });

      act(() => {
        result.current.setMessages((prev) => [...prev, message2]);
      });

      expect(result.current.messages).toEqual([message1, message2]);
    });

    it('should set chats using direct value', () => {
      const { result } = renderHook(() => useMultiGameChat('game-1'));

      const testChats: Chat[] = [
        {
          id: 'chat-1',
          gameId: 'game-1',
          gameName: 'Catan',
          agentId: 'agent-1',
          agentName: 'Rules Agent',
          startedAt: '2025-01-01T00:00:00Z',
          lastMessageAt: null,
        },
      ];

      act(() => {
        result.current.setChats(testChats);
      });

      expect(result.current.chats).toEqual(testChats);
    });

    it('should set chats using updater function', () => {
      const { result } = renderHook(() => useMultiGameChat('game-1'));

      const chat1: Chat = {
        id: 'chat-1',
        gameId: 'game-1',
        gameName: 'Catan',
        agentId: 'agent-1',
        agentName: 'Rules Agent',
        startedAt: '2025-01-01T00:00:00Z',
        lastMessageAt: null,
      };

      const chat2: Chat = {
        id: 'chat-2',
        gameId: 'game-1',
        gameName: 'Catan',
        agentId: 'agent-1',
        agentName: 'Rules Agent',
        startedAt: '2025-01-01T01:00:00Z',
        lastMessageAt: null,
      };

      act(() => {
        result.current.setChats([chat1]);
      });

      act(() => {
        result.current.setChats((prev) => [...prev, chat2]);
      });

      expect(result.current.chats).toEqual([chat1, chat2]);
    });

    it('should not update if no active game', () => {
      const { result } = renderHook(() => useMultiGameChat(null));

      const testMessages: ChatMessage[] = [
        {
          id: 'msg-1',
          role: 'user',
          content: 'Test',
          timestamp: new Date(),
        },
      ];

      act(() => {
        result.current.setMessages(testMessages);
      });

      expect(result.current.messages).toEqual([]);
    });
  });

  describe('State Inspection', () => {
    it('should check if game has state', async () => {
      mockApi.get
        .mockResolvedValueOnce([]) // Initial load
        .mockResolvedValueOnce([]); // switchGame call

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // Wait for initial load
      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      // After initial load, state should exist
      expect(result.current.hasGameState('game-1')).toBe(true);
      expect(result.current.hasGameState('game-2')).toBe(false);

      await act(async () => {
        await result.current.switchGame('game-1');
      });

      await waitFor(() => {
        expect(result.current.hasGameState('game-1')).toBe(true);
      });
    });

    it('should get correct chat count for game', async () => {
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

      mockApi.get.mockResolvedValueOnce(mockChats);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      expect(result.current.getGameChatCount('game-1')).toBe(0);

      await act(async () => {
        await result.current.switchGame('game-1');
      });

      await waitFor(() => {
        expect(result.current.getGameChatCount('game-1')).toBe(2);
      });
    });

    it('should return 0 for non-existent game', () => {
      const { result } = renderHook(() => useMultiGameChat('game-1'));

      expect(result.current.getGameChatCount('non-existent')).toBe(0);
    });
  });

  describe('ActiveGameId Changes', () => {
    it('should load chats when activeGameId changes', async () => {
      mockApi.get.mockResolvedValueOnce([]);

      const { rerender } = renderHook(
        ({ gameId }) => useMultiGameChat(gameId),
        { initialProps: { gameId: 'game-1' as string | null } }
      );

      await waitFor(() => {
        expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1');
      });

      mockApi.get.mockResolvedValueOnce([]);

      rerender({ gameId: 'game-2' });

      await waitFor(() => {
        expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-2');
      });
    });

    it('should not load chats when activeGameId is null', () => {
      const { rerender } = renderHook(
        ({ gameId }) => useMultiGameChat(gameId),
        { initialProps: { gameId: 'game-1' as string | null } }
      );

      mockApi.get.mockClear();

      rerender({ gameId: null });

      expect(mockApi.get).not.toHaveBeenCalled();
    });
  });

  describe('Cleanup Operations', () => {
    it('should maintain state across rerenders', async () => {
      mockApi.get.mockResolvedValueOnce([
        {
          id: 'chat-1',
          gameId: 'game-1',
          gameName: 'Catan',
          agentId: 'agent-1',
          agentName: 'Rules Agent',
          startedAt: '2025-01-01T00:00:00Z',
          lastMessageAt: null,
        },
      ]);

      const { result, rerender } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.chats).toHaveLength(1);
      });

      const originalChats = result.current.chats;

      // Rerender
      rerender();

      expect(result.current.chats).toEqual(originalChats);
    });

    it('should handle rapid game switches', async () => {
      mockApi.get
        .mockResolvedValueOnce([]) // Initial game-1
        .mockResolvedValueOnce([]) // game-1 switch
        .mockResolvedValueOnce([]) // game-2
        .mockResolvedValueOnce([]); // game-3

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // Wait for initial load
      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      }, { timeout: 1000 });

      // Rapidly switch games
      await act(async () => {
        await result.current.switchGame('game-1');
        await result.current.switchGame('game-2');
        await result.current.switchGame('game-3');
      });

      await waitFor(() => {
        expect(mockApi.get).toHaveBeenCalledTimes(4);
      }, { timeout: 1000 });

      expect(result.current.hasGameState('game-1')).toBe(true);
      expect(result.current.hasGameState('game-2')).toBe(true);
      expect(result.current.hasGameState('game-3')).toBe(true);
    });
  });
});
