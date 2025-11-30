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
describe('useMultiGameChat - State Management', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Initial State', () => {
    it('should have correct initial state with null activeGameId', () => {
      const { result } = renderHook(() => useMultiGameChat(null));

      expect(result.current.activeChatId).toBeNull();
      expect(result.current.chats).toEqual([]);
      expect(result.current.messages).toEqual([]);
      expect(result.current.isLoadingChats).toBe(false);
      expect(result.current.isLoadingMessages).toBe(false);
    });

    it('should have correct initial state with activeGameId', async () => {
      mockApi.get.mockResolvedValueOnce([]);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // Wait for initial load to complete
      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      expect(result.current.activeChatId).toBeNull();
      expect(result.current.chats).toEqual([]);
      expect(result.current.messages).toEqual([]);
      expect(result.current.isLoadingMessages).toBe(false);
    });

    it('should provide all control functions', () => {
      const { result } = renderHook(() => useMultiGameChat('game-1'));

      expect(result.current.switchGame).toBeDefined();
      expect(result.current.loadChatHistory).toBeDefined();
      expect(result.current.createNewChat).toBeDefined();
      expect(result.current.deleteChat).toBeDefined();
      expect(result.current.setMessages).toBeDefined();
      expect(result.current.setChats).toBeDefined();
      expect(result.current.setActiveChatId).toBeDefined();
      expect(result.current.hasGameState).toBeDefined();
      expect(result.current.getGameChatCount).toBeDefined();

      expect(typeof result.current.switchGame).toBe('function');
      expect(typeof result.current.loadChatHistory).toBe('function');
      expect(typeof result.current.createNewChat).toBe('function');
      expect(typeof result.current.deleteChat).toBe('function');
      expect(typeof result.current.setMessages).toBe('function');
      expect(typeof result.current.setChats).toBe('function');
      expect(typeof result.current.setActiveChatId).toBe('function');
      expect(typeof result.current.hasGameState).toBe('function');
      expect(typeof result.current.getGameChatCount).toBe('function');
    });
  });

  describe('Multi-Game State Isolation', () => {
    it('should load chats when switching to a new game', async () => {
      mockApi.get.mockResolvedValueOnce(mockChatsGame1);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await act(async () => {
        await result.current.switchGame('game-1');
      });

      await waitFor(() => {
        expect(result.current.chats).toEqual(mockChatsGame1);
      });

      expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1');
      expect(result.current.isLoadingChats).toBe(false);
    });

    it('should maintain separate chat states for different games', async () => {
      mockApi.get
        .mockResolvedValueOnce([]) // Initial load for game-1
        .mockResolvedValueOnce(mockChatsGame1)
        .mockResolvedValueOnce(mockChatsGame2);

      const { result, rerender } = renderHook(
        ({ gameId }) => useMultiGameChat(gameId),
        { initialProps: { gameId: 'game-1' } }
      );

      // Wait for initial load
      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      // Load game-1 chats
      await act(async () => {
        await result.current.switchGame('game-1');
      });

      await waitFor(() => {
        expect(result.current.chats).toEqual(mockChatsGame1);
      });

      // Switch to game-2
      rerender({ gameId: 'game-2' });

      await waitFor(() => {
        expect(result.current.chats).toEqual(mockChatsGame2);
      });

      // Verify game-1 and game-2 have separate states
      expect(result.current.hasGameState('game-1')).toBe(true);
      expect(result.current.hasGameState('game-2')).toBe(true);
      expect(result.current.getGameChatCount('game-1')).toBe(2);
      expect(result.current.getGameChatCount('game-2')).toBe(1);
    });

    it('should not reload chats if already loaded for a game', async () => {
      mockApi.get.mockResolvedValueOnce(mockChatsGame1);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // First switch - should load
      await act(async () => {
        await result.current.switchGame('game-1');
      });

      await waitFor(() => {
        expect(result.current.chats).toEqual(mockChatsGame1);
      });

      expect(mockApi.get).toHaveBeenCalledTimes(1);

      // Second switch to same game - should not reload
      await act(async () => {
        await result.current.switchGame('game-1');
      });

      await waitFor(() => {
        expect(result.current.chats).toEqual(mockChatsGame1);
      });

      // Still only called once
      expect(mockApi.get).toHaveBeenCalledTimes(1);
    });

    it('should set loading state while fetching chats', async () => {
      let resolveChats: (value: Chat[]) => void;
      const chatsPromise = new Promise<Chat[]>((resolve) => {
        resolveChats = resolve;
      });

      mockApi.get.mockReturnValueOnce(chatsPromise);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      act(() => {
        result.current.switchGame('game-1');
      });

      // Should be loading
      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(true);
      });

      // Resolve the promise
      await act(async () => {
        resolveChats!(mockChatsGame1);
        await chatsPromise;
      });

      // Should no longer be loading
      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
        expect(result.current.chats).toEqual(mockChatsGame1);
      });
    });

    it('should handle API errors when loading chats', async () => {
      const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      mockApi.get.mockRejectedValueOnce(new Error('Network error'));

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await act(async () => {
        try {
          await result.current.switchGame('game-1');
        } catch (e) {
          // Swallow error - we're testing error handling
        }
      });

      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      }, { timeout: 1000 });

      expect(result.current.chats).toEqual([]);
      expect(consoleErrorSpy).toHaveBeenCalledWith(
        'Error loading chats for game game-1:',
        expect.any(Error)
      );

      consoleErrorSpy.mockRestore();
    });
  });

  describe('State Setters', () => {
    it('should set messages using direct value', () => {
      const { result } = renderHook(() => useMultiGameChat('game-1'));

      const testMessages: ChatMessage[] = [
        {
          id: 'msg-1',
          role: 'user',
          content: 'Hello',
          timestamp: new Date(),
        },
      ];

      act(() => {
        result.current.setMessages(testMessages);
      });

      expect(result.current.messages).toEqual(testMessages);
    });

    it('should set messages using updater function', () => {
      const { result } = renderHook(() => useMultiGameChat('game-1'));

      const message1: ChatMessage = {
        id: 'msg-1',
        role: 'user',
        content: 'Hello',
        timestamp: new Date(),
      };

      const message2: ChatMessage = {
        id: 'msg-2',
        role: 'assistant',
