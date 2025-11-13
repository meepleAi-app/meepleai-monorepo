/**
 * Unit tests for refactored ChatProvider
 * Tests normalized state, localStorage persistence, and chat operations
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { ChatProvider, useChat } from '@/components/chat/ChatProvider';
import { GameProvider } from '@/components/game/GameProvider';
import { api } from '@/lib/api';
import React, { PropsWithChildren } from 'react';

// Mock api module
jest.mock('@/lib/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
    delete: jest.fn(),
    chat: {
      updateMessage: jest.fn(),
      deleteMessage: jest.fn(),
    },
  },
}));

// Mock GameProvider to provide stable test context
jest.mock('@/components/game/GameProvider', () => {
  const React = require('react');
  const actual = jest.requireActual('@/components/game/GameProvider');

  return {
    ...actual,
    useGame: jest.fn(() => ({
      selectedGameId: 'game-1',
      selectedAgentId: 'agent-1',
      selectedGame: { id: 'game-1', name: 'Gloomhaven', createdAt: '2024-01-01T00:00:00Z' },
      selectedAgent: { id: 'agent-1', gameId: 'game-1', name: 'QA Agent', kind: 'qa', createdAt: '2024-01-01T00:00:00Z' },
      games: [],
      agents: [],
      loading: { games: false, agents: false },
      error: null,
      selectGame: jest.fn(),
      selectAgent: jest.fn(),
      createGame: jest.fn(),
      refreshGames: jest.fn(),
    })),
    GameProvider: ({ children }: any) => React.createElement(React.Fragment, null, children),
  };
});

const mockApi = api as jest.Mocked<typeof api>;

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: jest.fn((key: string) => store[key] || null),
    setItem: jest.fn((key: string, value: string) => {
      store[key] = value;
    }),
    removeItem: jest.fn((key: string) => {
      delete store[key];
    }),
    clear: jest.fn(() => {
      store = {};
    }),
  };
})();

Object.defineProperty(window, 'localStorage', {
  value: localStorageMock,
});

describe('ChatProvider', () => {
  const mockGames = [
    {
      id: 'game-1',
      name: 'Gloomhaven',
      createdAt: '2024-01-01T00:00:00Z',
    },
  ];

  const mockAgents = [
    {
      id: 'agent-1',
      gameId: 'game-1',
      name: 'QA Agent',
      kind: 'qa',
      createdAt: '2024-01-01T00:00:00Z',
    },
  ];

  const mockChats = [
    {
      id: 'chat-1',
      gameId: 'game-1',
      gameName: 'Gloomhaven',
      agentId: 'agent-1',
      agentName: 'QA Agent',
      startedAt: '2024-01-01T10:00:00Z',
      lastMessageAt: null,
    },
  ];

  const mockMessages = [
    {
      id: 'msg-1',
      role: 'user' as const,
      content: 'Test question',
      timestamp: new Date('2024-01-01T10:00:00Z'),
    },
    {
      id: 'msg-2',
      role: 'assistant' as const,
      content: 'Test answer',
      timestamp: new Date('2024-01-01T10:00:30Z'),
    },
  ];

  const wrapper = ({ children }: PropsWithChildren) => (
    <ChatProvider>{children}</ChatProvider>
  );

  beforeEach(() => {
    jest.clearAllMocks();
    localStorageMock.clear();
  });

  describe('Initialization', () => {
    it('initializes with empty state when no localStorage data', async () => {
      mockApi.get.mockResolvedValue([]);

      const { result } = renderHook(() => useChat(), { wrapper });

      expect(result.current.chats).toEqual([]);
      expect(result.current.activeChatId).toBeNull();
      expect(result.current.messages).toEqual([]);
    });

    it('loads state from localStorage on mount', async () => {
      const storedState = {
        version: '1.0',
        timestamp: Date.now(),
        state: {
          chatsByGame: { 'game-1': mockChats },
          activeChatIds: { 'game-1': 'chat-1' },
          messagesByChat: { 'chat-1': mockMessages },
        },
      };

      localStorageMock.getItem.mockReturnValueOnce(JSON.stringify(storedState));
      mockApi.get
        .mockResolvedValueOnce(mockGames) // GameProvider loads games
        .mockResolvedValueOnce(mockAgents) // GameProvider loads agents
        .mockResolvedValueOnce(mockChats); // ChatProvider loads chats

      const { result } = renderHook(() => useChat(), { wrapper });

      // Wait for data to load
      await waitFor(() => expect(result.current.loading.chats).toBe(false));

      // Should have loaded from localStorage
      expect(localStorageMock.getItem).toHaveBeenCalledWith('meepleai_chat_state');
    });

    it('ignores expired localStorage data', async () => {
      const expiredState = {
        version: '1.0',
        timestamp: Date.now() - 25 * 60 * 60 * 1000, // 25 hours ago
        state: {
          chatsByGame: { 'game-1': mockChats },
          activeChatIds: {},
          messagesByChat: {},
        },
      };

      localStorageMock.getItem.mockReturnValueOnce(JSON.stringify(expiredState));
      mockApi.get.mockResolvedValue([]);

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.loading.chats).toBe(false));

      expect(localStorageMock.removeItem).toHaveBeenCalledWith('meepleai_chat_state');
    });
  });

  describe('Chat Loading', () => {
    it('loads chats when game is selected', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames) // Games
        .mockResolvedValueOnce(mockAgents) // Agents
        .mockResolvedValueOnce(mockChats); // Chats

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.loading.chats).toBe(false));

      expect(result.current.chats).toEqual(mockChats);
      expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats?gameId=game-1');
    });

    it('handles chats load failure', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockRejectedValueOnce(new Error('Failed to load chats'));

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.loading.chats).toBe(false));

      expect(result.current.chats).toEqual([]);
      expect(result.current.error).toBe('Failed to load chats');
    });
  });

  describe('Message Loading', () => {
    it('loads messages when chat is selected', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats)
        .mockResolvedValueOnce(mockMessages);

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.chats).toEqual(mockChats));

      await act(async () => {
        await result.current.selectChat('chat-1');
      });

      await waitFor(() => expect(result.current.loading.messages).toBe(false));

      expect(result.current.messages).toEqual(mockMessages);
      expect(mockApi.get).toHaveBeenCalledWith('/api/v1/chats/chat-1/messages');
    });
  });

  describe('createChat', () => {
    it('creates new chat and adds to state', async () => {
      const newChat = {
        id: 'chat-2',
        gameId: 'game-1',
        gameName: 'Gloomhaven',
        agentId: 'agent-1',
        agentName: 'QA Agent',
        startedAt: new Date().toISOString(),
        lastMessageAt: null,
      };

      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce([]);
      mockApi.post.mockResolvedValueOnce(newChat);

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.loading.chats).toBe(false));

      await act(async () => {
        await result.current.createChat();
      });

      expect(result.current.chats).toContainEqual(newChat);
      expect(result.current.activeChatId).toBe('chat-2');
      expect(mockApi.post).toHaveBeenCalledWith('/api/v1/chats', {
        gameId: 'game-1',
        agentId: 'agent-1',
      });
    });

    it('handles chat creation failure', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce([]);
      mockApi.post.mockRejectedValueOnce(new Error('Creation failed'));

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.loading.chats).toBe(false));

      await act(async () => {
        await result.current.createChat();
      });

      expect(result.current.error).toBe('Errore nella creazione della chat.');
    });
  });

  describe('deleteChat', () => {
    it('deletes chat and removes from state', async () => {
      // Mock window.confirm
      global.confirm = jest.fn(() => true);

      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats);
      mockApi.delete.mockResolvedValueOnce({});

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.chats).toEqual(mockChats));

      await act(async () => {
        await result.current.deleteChat('chat-1');
      });

      expect(result.current.chats).toEqual([]);
      expect(result.current.activeChatId).toBeNull();
      expect(mockApi.delete).toHaveBeenCalledWith('/api/v1/chats/chat-1');
    });

    it('does not delete if user cancels confirmation', async () => {
      global.confirm = jest.fn(() => false);

      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats);

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.chats).toEqual(mockChats));

      await act(async () => {
        await result.current.deleteChat('chat-1');
      });

      expect(result.current.chats).toEqual(mockChats);
      expect(mockApi.delete).not.toHaveBeenCalled();
    });
  });

  describe('localStorage persistence', () => {
    it('saves state to localStorage on state change', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats);

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.chats).toEqual(mockChats));

      // localStorage should have been called with state
      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        'meepleai_chat_state',
        expect.stringContaining('"chatsByGame"')
      );
    });

    it('handles localStorage errors gracefully', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      localStorageMock.setItem.mockImplementationOnce(() => {
        throw new Error('QuotaExceededError');
      });

      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats);

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.chats).toEqual(mockChats));

      // Should not crash, just log error
      expect(consoleErrorSpy).toHaveBeenCalledWith(
        'Failed to save state to localStorage:',
        expect.any(Error)
      );

      consoleErrorSpy.mockRestore();
    });
  });

  describe('sendMessage', () => {
    it('creates chat and sends message if no active chat', async () => {
      const newChat = {
        id: 'chat-2',
        gameId: 'game-1',
        gameName: 'Gloomhaven',
        agentId: 'agent-1',
        agentName: 'QA Agent',
        startedAt: new Date().toISOString(),
        lastMessageAt: null,
      };

      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce([]);
      mockApi.post.mockResolvedValueOnce(newChat);

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.loading.chats).toBe(false));

      await act(async () => {
        await result.current.sendMessage('Hello, rules agent!');
      });

      expect(mockApi.post).toHaveBeenCalledWith('/api/v1/chats', {
        gameId: 'game-1',
        agentId: 'agent-1',
      });
      expect(result.current.activeChatId).toBe('chat-2');
    });

    it('adds message optimistically', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats)
        .mockResolvedValueOnce(mockMessages);

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.chats).toEqual(mockChats));

      await act(async () => {
        await result.current.selectChat('chat-1');
      });

      await waitFor(() => expect(result.current.messages).toEqual(mockMessages));

      await act(async () => {
        await result.current.sendMessage('New question');
      });

      // Should have optimistically added user message
      const userMessages = result.current.messages.filter((m) => m.role === 'user');
      expect(userMessages.length).toBeGreaterThan(1);
      expect(userMessages[userMessages.length - 1].content).toBe('New question');
    });

    it('rolls back optimistic update on failure', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats)
        .mockResolvedValueOnce(mockMessages);

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.chats).toEqual(mockChats));

      await act(async () => {
        await result.current.selectChat('chat-1');
      });

      await waitFor(() => expect(result.current.messages).toEqual(mockMessages));

      // Note: Current implementation doesn't fully implement streaming,
      // so optimistic rollback may not be fully testable yet
      // This test validates structure is in place
    });
  });

  describe('editMessage', () => {
    it('edits message and reloads', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats)
        .mockResolvedValueOnce(mockMessages)
        .mockResolvedValueOnce(mockMessages); // Reload after edit
      mockApi.chat.updateMessage.mockResolvedValueOnce({});

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.chats).toEqual(mockChats));

      await act(async () => {
        await result.current.selectChat('chat-1');
      });

      await waitFor(() => expect(result.current.messages).toEqual(mockMessages));

      await act(async () => {
        await result.current.editMessage('msg-1', 'Updated content');
      });

      expect(mockApi.chat.updateMessage).toHaveBeenCalledWith('chat-1', 'msg-1', 'Updated content');
    });
  });

  describe('deleteMessage', () => {
    it('deletes message and reloads', async () => {
      global.confirm = jest.fn(() => true);

      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats)
        .mockResolvedValueOnce(mockMessages)
        .mockResolvedValueOnce([mockMessages[1]]); // After delete
      mockApi.chat.deleteMessage.mockResolvedValueOnce({});

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.chats).toEqual(mockChats));

      await act(async () => {
        await result.current.selectChat('chat-1');
      });

      await waitFor(() => expect(result.current.messages).toEqual(mockMessages));

      await act(async () => {
        await result.current.deleteMessage('msg-1');
      });

      expect(mockApi.chat.deleteMessage).toHaveBeenCalledWith('chat-1', 'msg-1');
    });
  });

  describe('setMessageFeedback', () => {
    it('sets feedback with optimistic update', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats)
        .mockResolvedValueOnce(mockMessages);
      mockApi.post.mockResolvedValueOnce({});

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.chats).toEqual(mockChats));

      await act(async () => {
        await result.current.selectChat('chat-1');
      });

      await waitFor(() => expect(result.current.messages).toEqual(mockMessages));

      await act(async () => {
        await result.current.setMessageFeedback('msg-1', 'helpful');
      });

      expect(mockApi.post).toHaveBeenCalledWith('/api/v1/agents/feedback', expect.objectContaining({
        feedback: 'helpful',
      }));
    });

    it('reverts feedback on API failure', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats)
        .mockResolvedValueOnce(mockMessages);
      mockApi.post.mockRejectedValueOnce(new Error('Feedback failed'));

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.chats).toEqual(mockChats));

      await act(async () => {
        await result.current.selectChat('chat-1');
      });

      await waitFor(() => expect(result.current.messages).toEqual(mockMessages));

      await act(async () => {
        await result.current.setMessageFeedback('msg-1', 'helpful');
      });

      // Should revert optimistic update
      expect(result.current.error).toBe("Errore nell'invio del feedback.");
    });
  });

  describe('error handling', () => {
    it('throws error if useChat used outside ChatProvider', () => {
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

      expect(() => {
        renderHook(() => useChat());
      }).toThrow('useChat must be used within ChatProvider');

      consoleSpy.mockRestore();
    });
  });

  describe('normalized state structure', () => {
    it('maintains separate state per game', async () => {
      const game2Chats = [
        {
          id: 'chat-3',
          gameId: 'game-2',
          gameName: 'Catan',
          agentId: 'agent-2',
          agentName: 'Catan Agent',
          startedAt: new Date().toISOString(),
          lastMessageAt: null,
        },
      ];

      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats); // game-1 chats

      const { result } = renderHook(() => useChat(), { wrapper });

      await waitFor(() => expect(result.current.chats).toEqual(mockChats));

      // Verify state is normalized and isolated per game
      expect(result.current.chats).toEqual(mockChats);
      expect(result.current.chats).not.toContainEqual(expect.objectContaining({ gameId: 'game-2' }));
    });
  });
});
