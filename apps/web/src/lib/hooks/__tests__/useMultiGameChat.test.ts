import { renderHook, act, waitFor } from '@testing-library/react';
import { useMultiGameChat, Chat, ChatMessage, ChatWithHistory } from '../useMultiGameChat';
import * as api from '../../api';

// Mock the API module
jest.mock('../../api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
    delete: jest.fn(),
  },
}));

const mockApi = api.api as jest.Mocked<typeof api.api>;

describe('useMultiGameChat', () => {
  beforeEach(() => {
    jest.clearAllMocks();
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

  describe('Multi-Game Management', () => {
    const mockChatsGame1: Chat[] = [
      {
        id: 'chat-1',
        gameId: 'game-1',
        gameName: 'Catan',
        agentId: 'agent-1',
        agentName: 'Rules Agent',
        startedAt: '2025-01-01T00:00:00Z',
        lastMessageAt: '2025-01-01T01:00:00Z',
      },
      {
        id: 'chat-2',
        gameId: 'game-1',
        gameName: 'Catan',
        agentId: 'agent-1',
        agentName: 'Rules Agent',
        startedAt: '2025-01-01T02:00:00Z',
        lastMessageAt: null,
      },
    ];

    const mockChatsGame2: Chat[] = [
      {
        id: 'chat-3',
        gameId: 'game-2',
        gameName: 'Pandemic',
        agentId: 'agent-1',
        agentName: 'Rules Agent',
        startedAt: '2025-01-01T03:00:00Z',
        lastMessageAt: '2025-01-01T04:00:00Z',
      },
    ];

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
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
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

  describe('Message Handling', () => {
    const mockChatWithHistory: ChatWithHistory = {
      id: 'chat-1',
      gameId: 'game-1',
      gameName: 'Catan',
      agentId: 'agent-1',
      agentName: 'Rules Agent',
      startedAt: '2025-01-01T00:00:00Z',
      lastMessageAt: '2025-01-01T01:00:00Z',
      messages: [
        {
          id: 'msg-1',
          level: 'user',
          message: 'How do I play?',
          metadataJson: null,
          createdAt: '2025-01-01T00:00:00Z',
        },
        {
          id: 'msg-2',
          level: 'assistant',
          message: 'Here are the rules...',
          metadataJson: JSON.stringify({
            snippets: [
              { text: 'Rule 1', source: 'rules.pdf', page: 1, line: null },
            ],
          }),
          createdAt: '2025-01-01T00:01:00Z',
        },
      ],
    };

    it('should load chat history and convert messages', async () => {
      mockApi.get.mockResolvedValueOnce([]).mockResolvedValueOnce(mockChatWithHistory);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // Wait for initial load
      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      await act(async () => {
        await result.current.loadChatHistory('chat-1');
      });

      await waitFor(() => {
        expect(result.current.messages).toHaveLength(2);
      }, { timeout: 1000 });

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
      mockApi.get.mockResolvedValueOnce([]).mockResolvedValueOnce(mockChatWithHistory);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // Wait for initial load
      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      await act(async () => {
        await result.current.loadChatHistory('chat-1');
      });

      await waitFor(() => {
        expect(result.current.activeChatId).toBe('chat-1');
      }, { timeout: 1000 });
    });

    it('should set loading state while fetching messages', async () => {
      mockApi.get.mockResolvedValueOnce([]); // Initial load

      let resolveChat: (value: ChatWithHistory) => void;
      const chatPromise = new Promise<ChatWithHistory>((resolve) => {
        resolveChat = resolve;
      });

      mockApi.get.mockReturnValueOnce(chatPromise);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // Wait for initial load
      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      act(() => {
        result.current.loadChatHistory('chat-1');
      });

      await waitFor(() => {
        expect(result.current.isLoadingMessages).toBe(true);
      }, { timeout: 1000 });

      await act(async () => {
        resolveChat!(mockChatWithHistory);
        await chatPromise;
      });

      await waitFor(() => {
        expect(result.current.isLoadingMessages).toBe(false);
      }, { timeout: 1000 });
    });

    it('should handle API errors when loading chat history', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      // TEST-685: Mock initial auto-load, then the failing loadChatHistory call
      mockApi.get
        .mockResolvedValueOnce([]) // Initial auto-load from useEffect
        .mockRejectedValueOnce(new Error('Chat not found')); // loadChatHistory call

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

      await waitFor(() => {
        expect(result.current.isLoadingMessages).toBe(false);
      }, { timeout: 1000 });

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

      expect(mockApi.get).not.toHaveBeenCalled();
    });

    it('should handle messages with malformed metadata JSON', async () => {
      const chatWithBadJson: ChatWithHistory = {
        ...mockChatWithHistory,
        messages: [
          {
            id: 'msg-1',
            level: 'assistant',
            message: 'Test message',
            metadataJson: '{invalid json}',
            createdAt: '2025-01-01T00:00:00Z',
          },
        ],
      };

      mockApi.get.mockResolvedValueOnce([]).mockResolvedValueOnce(chatWithBadJson);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      // Wait for initial load
      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      await act(async () => {
        await result.current.loadChatHistory('chat-1');
      });

      await waitFor(() => {
        expect(result.current.messages).toHaveLength(1);
      }, { timeout: 1000 });

      // Should not have snippets due to JSON parse error
      expect(result.current.messages[0].snippets).toBeUndefined();
    });
  });

  describe('Context Switching', () => {
    it('should preserve messages per game when switching', async () => {
      const chatGame1: ChatWithHistory = {
        id: 'chat-1',
        gameId: 'game-1',
        gameName: 'Catan',
        agentId: 'agent-1',
        agentName: 'Rules Agent',
        startedAt: '2025-01-01T00:00:00Z',
        lastMessageAt: '2025-01-01T01:00:00Z',
        messages: [
          {
            id: 'msg-1',
            level: 'user',
            message: 'Game 1 message',
            metadataJson: null,
            createdAt: '2025-01-01T00:00:00Z',
          },
        ],
      };

      const chatGame2: ChatWithHistory = {
        id: 'chat-2',
        gameId: 'game-2',
        gameName: 'Pandemic',
        agentId: 'agent-1',
        agentName: 'Rules Agent',
        startedAt: '2025-01-01T02:00:00Z',
        lastMessageAt: '2025-01-01T03:00:00Z',
        messages: [
          {
            id: 'msg-2',
            level: 'user',
            message: 'Game 2 message',
            metadataJson: null,
            createdAt: '2025-01-01T02:00:00Z',
          },
        ],
      };

      mockApi.get
        .mockResolvedValueOnce([{ ...chatGame1 }]) // chats for game-1
        .mockResolvedValueOnce(chatGame1) // chat history for chat-1
        .mockResolvedValueOnce([{ ...chatGame2 }]) // chats for game-2
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
      const { result, rerender } = renderHook(
        ({ gameId }) => useMultiGameChat(gameId),
        { initialProps: { gameId: 'game-1' } }
      );

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

  describe('Create New Chat', () => {
    const mockNewChat: Chat = {
      id: 'chat-new',
      gameId: 'game-1',
      gameName: 'Catan',
      agentId: 'agent-1',
      agentName: 'Rules Agent',
      startedAt: '2025-01-01T05:00:00Z',
      lastMessageAt: null,
    };

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
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
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
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
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

  describe('State Setters', () => {
    it('should set messages using direct value', () => {
      const { result } = renderHook(() => useMultiGameChat('game-1'));

      const testMessages = [
        {
          id: 'msg-1',
          role: 'user' as const,
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

      const message1 = {
        id: 'msg-1',
        role: 'user' as const,
        content: 'Hello',
        timestamp: new Date(),
      };

      const message2 = {
        id: 'msg-2',
        role: 'assistant' as const,
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

      const testMessages = [
        {
          id: 'msg-1',
          role: 'user' as const,
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
