/**
 * useMultiGameChat State Tests
 *
 * Tests for state management: initial state, getGameState, hasGameState, getGameChatCount
 */
import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useMultiGameChat } from '../useMultiGameChat';
import { api } from '../../api';

// Mock the api module with chat client structure
vi.mock('../../api', () => ({
  api: {
    chat: {
      getThreadsByGame: vi.fn(),
      getThreadById: vi.fn(),
      createThread: vi.fn(),
      addMessage: vi.fn(),
      deleteThread: vi.fn(),
    },
  },
}));

describe('useMultiGameChat state', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('initial state', () => {
    it('should have empty state when no game is selected', () => {
      const { result } = renderHook(() => useMultiGameChat(null));

      expect(result.current.activeChatId).toBeNull();
      expect(result.current.chats).toEqual([]);
      expect(result.current.messages).toEqual([]);
      expect(result.current.isLoadingChats).toBe(false);
      expect(result.current.isLoadingMessages).toBe(false);
    });

    it('should initialize state when game is provided', async () => {
      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([] as any);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      expect(result.current.activeChatId).toBeNull();
      expect(result.current.chats).toEqual([]);
    });
  });

  describe('hasGameState', () => {
    it('should return false for uninitialized game', () => {
      const { result } = renderHook(() => useMultiGameChat(null));

      expect(result.current.hasGameState('unknown-game')).toBe(false);
    });

    it('should return true for initialized game', async () => {
      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([] as any);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.hasGameState('game-1')).toBe(true);
      });
    });
  });

  describe('getGameChatCount', () => {
    it('should return 0 for game with no chats', async () => {
      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([] as any);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      expect(result.current.getGameChatCount('game-1')).toBe(0);
    });

    it('should return correct count for game with chats', async () => {
      const mockChats = [
        {
          id: 'chat-1',
          gameId: 'game-1',
          gameName: 'Game',
          agentId: 'agent-1',
          agentName: 'Agent',
          startedAt: '2024-01-01',
          lastMessageAt: null,
        },
        {
          id: 'chat-2',
          gameId: 'game-1',
          gameName: 'Game',
          agentId: 'agent-1',
          agentName: 'Agent',
          startedAt: '2024-01-02',
          lastMessageAt: null,
        },
      ];

      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce(mockChats as any);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.chats).toHaveLength(2);
      });

      expect(result.current.getGameChatCount('game-1')).toBe(2);
    });

    it('should return 0 for uninitialized game', () => {
      const { result } = renderHook(() => useMultiGameChat(null));

      expect(result.current.getGameChatCount('unknown')).toBe(0);
    });
  });

  describe('setMessages', () => {
    it('should update messages for current game', async () => {
      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([] as any);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      const newMessage = {
        id: 'msg-1',
        role: 'user' as const,
        content: 'Hello',
        timestamp: new Date(),
      };

      act(() => {
        result.current.setMessages([newMessage]);
      });

      expect(result.current.messages).toHaveLength(1);
      expect(result.current.messages[0].content).toBe('Hello');
    });

    it('should not update messages when no game is selected', async () => {
      const { result } = renderHook(() => useMultiGameChat(null));

      act(() => {
        result.current.setMessages([
          {
            id: 'msg-1',
            role: 'user',
            content: 'Test',
            timestamp: new Date(),
          },
        ]);
      });

      expect(result.current.messages).toEqual([]);
    });
  });

  describe('setChats', () => {
    it('should update chats for current game', async () => {
      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([] as any);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      const newChat = {
        id: 'chat-1',
        gameId: 'game-1',
        gameName: 'Game',
        agentId: 'agent-1',
        agentName: 'Agent',
        startedAt: '2024-01-01',
        lastMessageAt: null,
      };

      act(() => {
        result.current.setChats([newChat]);
      });

      expect(result.current.chats).toHaveLength(1);
    });
  });

  describe('setActiveChatId', () => {
    it('should update active chat ID', async () => {
      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([] as any);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      act(() => {
        result.current.setActiveChatId('chat-123');
      });

      expect(result.current.activeChatId).toBe('chat-123');
    });

    it('should allow setting to null', async () => {
      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([] as any);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      act(() => {
        result.current.setActiveChatId('chat-123');
      });

      act(() => {
        result.current.setActiveChatId(null);
      });

      expect(result.current.activeChatId).toBeNull();
    });
  });
});
