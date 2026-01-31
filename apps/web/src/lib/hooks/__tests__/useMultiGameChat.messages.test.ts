import { renderHook, act, waitFor } from '@testing-library/react';
import { useMultiGameChat, ChatMessage } from '../useMultiGameChat';
import * as api from '../../api';
import {
  mockChatWithHistory,
  chatGame1,
  chatGame2,
  chatWithBadJson,
} from './useMultiGameChat.test-helpers';

// Mock the API module with chat client structure
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

const mockApi = api.api.chat as any;

/**
 * Tests for useMultiGameChat message handling functionality
 * Covers: message loading, history management, context switching, and metadata parsing
 */
describe('useMultiGameChat - Messages', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Message Handling', () => {
    it('should load chat history and convert messages', async () => {
      mockApi.getThreadsByGame.mockResolvedValueOnce([]);
      mockApi.getThreadById.mockResolvedValueOnce(mockChatWithHistory);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // Wait for initial load
      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      await act(async () => {
        await result.current.loadChatHistory('chat-1');
      });

      await waitFor(
        () => {
          expect(result.current.messages).toHaveLength(2);
        },
        { timeout: 1000 }
      );

      const messages = result.current.messages;

      // Check first message (user)
      expect(messages[0].role).toBe('user');
      expect(messages[0].content).toBe('How do I play?');
      expect(messages[0].gameId).toBe('game-1');
      expect(messages[0].snippets).toBeUndefined();
      expect(messages[0].backendMessageId).toBe('msg-1');

      // Check second message (assistant with snippets)
      expect(messages[1].role).toBe('assistant');
      expect(messages[1].content).toBe('Here are the rules...');
      expect(messages[1].snippets).toEqual([
        { text: 'Rule 1', source: 'rules.pdf', page: 1, line: null },
      ]);
      expect(messages[1].backendMessageId).toBe('msg-2');
    });

    it('should set activeChatId when loading chat history', async () => {
      mockApi.getThreadsByGame.mockResolvedValueOnce([]);
      mockApi.getThreadById.mockResolvedValueOnce(mockChatWithHistory);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // Wait for initial load
      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      await act(async () => {
        await result.current.loadChatHistory('chat-1');
      });

      await waitFor(
        () => {
          expect(result.current.activeChatId).toBe('chat-1');
        },
        { timeout: 1000 }
      );
    });

    it('should set loading state while fetching messages', async () => {
      mockApi.getThreadsByGame.mockResolvedValueOnce([]); // Initial load

      let resolveChat: (value: typeof mockChatWithHistory) => void;
      const chatPromise = new Promise<typeof mockChatWithHistory>(resolve => {
        resolveChat = resolve;
      });

      mockApi.getThreadById.mockReturnValueOnce(chatPromise);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // Wait for initial load
      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      act(() => {
        result.current.loadChatHistory('chat-1');
      });

      await waitFor(
        () => {
          expect(result.current.isLoadingMessages).toBe(true);
        },
        { timeout: 1000 }
      );

      await act(async () => {
        resolveChat!(mockChatWithHistory);
        await chatPromise;
      });

      await waitFor(
        () => {
          expect(result.current.isLoadingMessages).toBe(false);
        },
        { timeout: 1000 }
      );
    });

    it('should handle API errors when loading chat history', async () => {
      const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      // TEST-685: Mock initial auto-load, then the failing loadChatHistory call
      mockApi.getThreadsByGame.mockResolvedValueOnce([]); // Initial auto-load from useEffect
      mockApi.getThreadById.mockRejectedValueOnce(new Error('Chat not found')); // loadChatHistory call

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // Wait for initial load to complete
      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      await act(async () => {
        try {
          await result.current.loadChatHistory('chat-1');
        } catch (e) {
          // Swallow error - we're testing error handling
        }
      });

      await waitFor(
        () => {
          expect(result.current.isLoadingMessages).toBe(false);
        },
        { timeout: 1000 }
      );

      expect(consoleErrorSpy).toHaveBeenCalledWith(
        'Error loading chat history:',
        expect.any(Error)
      );

      consoleErrorSpy.mockRestore();
    });

    it('should not load chat history if no active game', async () => {
      const { result } = renderHook(() => useMultiGameChat(null));

      await act(async () => {
        await result.current.loadChatHistory('chat-1');
      });

      expect(mockApi.getThreadsByGame).not.toHaveBeenCalled();
    });

    it('should handle messages with malformed metadata JSON', async () => {
      mockApi.getThreadsByGame.mockResolvedValueOnce([]);
      mockApi.getThreadById.mockResolvedValueOnce(chatWithBadJson);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // Wait for initial load
      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      await act(async () => {
        await result.current.loadChatHistory('chat-1');
      });

      await waitFor(
        () => {
          expect(result.current.messages).toHaveLength(1);
        },
        { timeout: 1000 }
      );

      // Should not have snippets due to JSON parse error
      expect(result.current.messages[0].snippets).toBeUndefined();
    });
  });

  describe('Context Switching', () => {
    it('should preserve messages per game when switching', async () => {
      mockApi.getThreadsByGame
        .mockResolvedValueOnce([{ ...chatGame1 }]) // chats for game-1
        .mockResolvedValueOnce([{ ...chatGame2 }]); // chats for game-2

      mockApi.getThreadById
        .mockResolvedValueOnce(chatGame1) // chat history for chat-1
        .mockResolvedValueOnce(chatGame2); // chat history for chat-2

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // Load game-1 chat history
      await act(async () => {
        await result.current.switchGame('game-1');
      });

      await act(async () => {
        await result.current.loadChatHistory('chat-1');
      });

      await waitFor(() => {
        expect(result.current.messages[0].content).toBe('Game 1 message');
      });

      // Switch to game-2 and load its chat
      await act(async () => {
        await result.current.switchGame('game-2');
      });

      await act(async () => {
        await result.current.loadChatHistory('chat-2');
      });

      await waitFor(() => {
        expect(result.current.messages[0].content).toBe('Game 2 message');
      });
    });

    it('should preserve activeChatId per game', async () => {
      const { result, rerender } = renderHook(({ gameId }) => useMultiGameChat(gameId), {
        initialProps: { gameId: 'game-1' },
      });

      // Set activeChatId for game-1
      act(() => {
        result.current.setActiveChatId('chat-1');
      });

      expect(result.current.activeChatId).toBe('chat-1');

      // Switch to game-2
      rerender({ gameId: 'game-2' });

      // activeChatId should be null for game-2
      await waitFor(() => {
        expect(result.current.activeChatId).toBeNull();
      });

      // Set activeChatId for game-2
      act(() => {
        result.current.setActiveChatId('chat-3');
      });

      expect(result.current.activeChatId).toBe('chat-3');

      // Switch back to game-1
      rerender({ gameId: 'game-1' });

      // Should still have chat-1
      await waitFor(() => {
        expect(result.current.activeChatId).toBe('chat-1');
      });
    });
  });
});
