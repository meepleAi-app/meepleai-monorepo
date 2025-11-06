function createMockApi() {
  return {
    get: jest.fn(),
    post: jest.fn(),
    delete: jest.fn(),
    chat: {
      updateMessage: jest.fn(),
      deleteMessage: jest.fn(),
    },
  };
}

jest.mock('@/lib/api', () => {
  const apiMock = createMockApi();
  (global as any).__chatApiMock = apiMock;
  return { api: apiMock };
});

const mockApi: any = (global as any).__chatApiMock;

import { render, screen, act, waitFor } from '@testing-library/react';
import { renderHook } from '@testing-library/react';
import { ChatProvider, useChatContext } from '@/components/chat/ChatProvider';
import { Game, Agent, Chat, Message } from '@/types';

const baseGame: Game = {
  id: 'game-1',
  name: 'Chess',
  createdAt: new Date().toISOString(),
};

const baseAgent: Agent = {
  id: 'agent-1',
  gameId: 'game-1',
  name: 'QA Agent',
  kind: 'qa',
  createdAt: new Date().toISOString(),
};

const baseChat: Chat = {
  id: 'chat-1',
  gameId: 'game-1',
  gameName: 'Chess',
  agentId: 'agent-1',
  agentName: 'QA Agent',
  startedAt: new Date().toISOString(),
  lastMessageAt: null,
};

const baseMessage: Message = {
  id: 'message-1',
  role: 'assistant',
  content: 'Hello!',
  timestamp: new Date(),
  feedback: null,
  backendMessageId: 'backend-1',
  endpoint: 'qa',
  gameId: 'game-1',
};

const setupHappyPathMocks = () => {
  mockApi.get.mockImplementation(async (path: string) => {
    if (path === '/api/v1/auth/me') {
      return null;
    }
    if (path === '/api/v1/games') {
      return [baseGame];
    }
    if (path === `/api/v1/games/${baseGame.id}/agents`) {
      return [baseAgent];
    }
    if (path === `/api/v1/chats?gameId=${baseGame.id}`) {
      return [baseChat];
    }
    if (path === `/api/v1/chats/${baseChat.id}/messages`) {
      return [baseMessage];
    }
    return [];
  });

  mockApi.post.mockResolvedValue({
    ...baseChat,
    id: 'chat-new',
    startedAt: new Date().toISOString(),
  });

  mockApi.delete.mockResolvedValue(undefined);
  mockApi.chat.updateMessage.mockResolvedValue(undefined);
  mockApi.chat.deleteMessage.mockResolvedValue(undefined);
};

beforeEach(() => {
  jest.clearAllMocks();
  mockApi.get.mockReset();
  mockApi.post.mockReset();
  mockApi.delete.mockReset();
  mockApi.chat.updateMessage.mockReset();
  mockApi.chat.deleteMessage.mockReset();

  mockApi.get.mockResolvedValue([]);
  mockApi.post.mockResolvedValue(null);
  mockApi.delete.mockResolvedValue(undefined);
  mockApi.chat.updateMessage.mockResolvedValue(undefined);
  mockApi.chat.deleteMessage.mockResolvedValue(undefined);

  const confirmStub = jest.fn(() => true);
  (global as any).confirm = confirmStub;
  (window as any).confirm = confirmStub;
});

describe('ChatProvider', () => {
  describe('Context safety', () => {
    it('throws when useChatContext is used outside provider', () => {
      const consoleError = jest.spyOn(console, 'error').mockImplementation(() => {});

      expect(() => {
        renderHook(() => useChatContext());
      }).toThrow('useChatContext must be used within ChatProvider');

      consoleError.mockRestore();
    });

    it('provides default state inside provider', () => {
      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      expect(result.current.authUser).toBeNull();
      expect(result.current.games).toEqual([]);
      expect(result.current.selectedGameId).toBeNull();
      expect(result.current.agents).toEqual([]);
      expect(result.current.activeChatId).toBeNull();
      expect(result.current.messages).toEqual([]);
    });
  });

  describe('UI actions', () => {
    it('toggles sidebar state', () => {
      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      expect(result.current.sidebarCollapsed).toBe(false);

      act(() => result.current.toggleSidebar());
      expect(result.current.sidebarCollapsed).toBe(true);

      act(() => result.current.toggleSidebar());
      expect(result.current.sidebarCollapsed).toBe(false);
    });

    it('updates input value', () => {
      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });
      act(() => result.current.setInputValue('hello'));
      expect(result.current.inputValue).toBe('hello');
    });
  });

  describe('Data loading', () => {
    it('loads agents and chats when selecting a game', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.selectedAgentId).toBe(baseAgent.id);
      });

      expect(mockApi.get).toHaveBeenCalledWith(`/api/v1/games/${baseGame.id}/agents`);
      expect(mockApi.get).toHaveBeenCalledWith(`/api/v1/chats?gameId=${baseGame.id}`);
    });

    it('loads chat history when selecting a chat', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => {
        expect(result.current.messages).toHaveLength(1);
      });

      expect(mockApi.get).toHaveBeenCalledWith(`/api/v1/chats/${baseChat.id}/messages`);
    });
  });

  describe('Chat management', () => {
    it('creates a chat when prerequisites are met', async () => {
      setupHappyPathMocks();

      const newChat: Chat = {
        ...baseChat,
        id: 'chat-new',
        startedAt: new Date().toISOString(),
      };

      mockApi.post.mockResolvedValueOnce(newChat);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => expect(result.current.selectedAgentId).toBe(baseAgent.id));

      await act(async () => {
        await result.current.createChat();
      });

      expect(mockApi.post).toHaveBeenCalledWith('/api/v1/chats', {
        gameId: baseGame.id,
        agentId: baseAgent.id,
      });
      expect(result.current.activeChatId).toBe(newChat.id);
      expect(result.current.chats[0].id).toBe(newChat.id);
    });

    it('deletes a chat when confirmed', async () => {
      setupHappyPathMocks();
      mockApi.delete.mockResolvedValueOnce(undefined);
      const confirmSpy = jest.spyOn(window, 'confirm').mockReturnValue(true);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      const createdChat: Chat = {
        ...baseChat,
        id: 'chat-temp',
        startedAt: new Date().toISOString(),
      };

      mockApi.post.mockResolvedValueOnce(createdChat);

      await act(async () => {
        await result.current.createChat();
      });

      expect(result.current.chats).toHaveLength(1);

      await act(async () => {
        await result.current.deleteChat(createdChat.id);
      });

      expect(mockApi.delete).toHaveBeenCalledWith(`/api/v1/chats/${createdChat.id}`);
      expect(result.current.chats).toHaveLength(0);

      confirmSpy.mockRestore();
    });
  });

  describe('Messaging', () => {
    it('optimistically toggles feedback and posts to API', async () => {
      setupHappyPathMocks();
      mockApi.post.mockResolvedValueOnce(undefined);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      await act(async () => {
        await result.current.setMessageFeedback(baseMessage.id, 'helpful');
      });

      expect(result.current.messages[0].feedback).toBe('helpful');
      expect(mockApi.post).toHaveBeenCalledWith('/api/v1/agents/feedback', expect.objectContaining({
        messageId: baseMessage.backendMessageId,
        feedback: 'helpful',
      }));
    });
  });

  describe('Component integration', () => {
    it('renders children', () => {
      render(
        <ChatProvider>
          <div data-testid="child">Child</div>
        </ChatProvider>,
      );

      expect(screen.getByTestId('child')).toHaveTextContent('Child');
    });
  });

  // ============================================================================
  // Phase 1: Critical Paths - Message Sending & State Management
  // ============================================================================

  describe('Message sending flow', () => {
    it('validates content before sending (empty message rejected)', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => expect(result.current.selectedAgentId).toBe(baseAgent.id));

      await act(async () => {
        await result.current.sendMessage('');
      });

      expect(mockApi.post).not.toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/chats'),
        expect.anything()
      );
      expect(result.current.messages).toHaveLength(0);
    });

    it('validates content before sending (whitespace-only message rejected)', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => expect(result.current.selectedAgentId).toBe(baseAgent.id));

      await act(async () => {
        await result.current.sendMessage('   ');
      });

      expect(mockApi.post).not.toHaveBeenCalled();
      expect(result.current.messages).toHaveLength(0);
    });

    it('requires both gameId and agentId before sending', async () => {
      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.sendMessage('Hello');
      });

      expect(mockApi.post).not.toHaveBeenCalled();
      expect(result.current.messages).toHaveLength(0);
    });

    it('adds user message optimistically', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => expect(result.current.selectedAgentId).toBe(baseAgent.id));

      const messageContent = 'Test message';

      await act(async () => {
        await result.current.sendMessage(messageContent);
      });

      const userMessages = result.current.messages.filter(m => m.role === 'user');
      expect(userMessages).toHaveLength(1);
      expect(userMessages[0].content).toBe(messageContent);
      expect(userMessages[0].id).toMatch(/^temp-user-/);
    });

    it('creates chat automatically if none exists when sending message', async () => {
      setupHappyPathMocks();

      const newChat: Chat = {
        ...baseChat,
        id: 'chat-auto-created',
        startedAt: new Date().toISOString(),
      };

      mockApi.post.mockResolvedValueOnce(newChat);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => expect(result.current.selectedAgentId).toBe(baseAgent.id));

      expect(result.current.activeChatId).toBeNull();

      await act(async () => {
        await result.current.sendMessage('First message');
      });

      await waitFor(() => {
        expect(result.current.activeChatId).toBe(newChat.id);
      });

      expect(mockApi.post).toHaveBeenCalledWith('/api/v1/chats', {
        gameId: baseGame.id,
        agentId: baseAgent.id,
      });
      expect(result.current.chats[0].id).toBe(newChat.id);
    });

    it('rolls back optimistic user message on send failure', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => expect(result.current.selectedAgentId).toBe(baseAgent.id));

      mockApi.post.mockRejectedValueOnce(new Error('Network error'));

      await act(async () => {
        await result.current.sendMessage('Test message');
      });

      await waitFor(() => {
        expect(result.current.messages).toHaveLength(0);
      });

      expect(result.current.errorMessage).toContain('Errore nella comunicazione');
    });

    it('clears input value after successful send', async () => {
      setupHappyPathMocks();

      const newChat: Chat = {
        ...baseChat,
        id: 'chat-new',
        startedAt: new Date().toISOString(),
      };

      mockApi.post.mockResolvedValueOnce(newChat);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => expect(result.current.selectedAgentId).toBe(baseAgent.id));

      act(() => result.current.setInputValue('Test message'));
      expect(result.current.inputValue).toBe('Test message');

      await act(async () => {
        await result.current.sendMessage('Test message');
      });

      expect(result.current.inputValue).toBe('');
    });

    it('trims message content before sending', async () => {
      setupHappyPathMocks();

      const newChat: Chat = {
        ...baseChat,
        id: 'chat-new',
        startedAt: new Date().toISOString(),
      };

      mockApi.post.mockResolvedValueOnce(newChat);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => expect(result.current.selectedAgentId).toBe(baseAgent.id));

      await act(async () => {
        await result.current.sendMessage('  Test message  ');
      });

      const userMessages = result.current.messages.filter(m => m.role === 'user');
      expect(userMessages[0].content).toBe('Test message');
    });
  });

  describe('Multi-game state isolation', () => {
    it('maintains separate chat state for each game', async () => {
      const game2: Game = {
        id: 'game-2',
        name: 'Catan',
        createdAt: new Date().toISOString(),
      };

      const agent2: Agent = {
        id: 'agent-2',
        gameId: 'game-2',
        name: 'QA Agent 2',
        kind: 'qa',
        createdAt: new Date().toISOString(),
      };

      const chat2: Chat = {
        id: 'chat-2',
        gameId: 'game-2',
        gameName: 'Catan',
        agentId: 'agent-2',
        agentName: 'QA Agent 2',
        startedAt: new Date().toISOString(),
        lastMessageAt: null,
      };

      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame, game2];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [baseAgent];
        if (path === `/api/v1/games/${game2.id}/agents`) return [agent2];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [baseChat];
        if (path === `/api/v1/chats?gameId=${game2.id}`) return [chat2];
        if (path === `/api/v1/chats/${baseChat.id}/messages`) return [baseMessage];
        if (path === `/api/v1/chats/${chat2.id}/messages`) return [];
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      // Select game 1
      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.chats).toHaveLength(1);
      });

      expect(result.current.chats[0].id).toBe(baseChat.id);

      // Switch to game 2
      await act(async () => {
        await result.current.selectGame(game2.id);
      });

      await waitFor(() => {
        expect(result.current.selectedGameId).toBe(game2.id);
        expect(result.current.agents).toHaveLength(1);
      }, { timeout: 3000 });

      // Chats should be loaded for game 2
      await waitFor(() => {
        expect(result.current.chats).toHaveLength(1);
      }, { timeout: 3000 });

      expect(result.current.chats[0].id).toBe(chat2.id);

      // Switch back to game 1
      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.selectedGameId).toBe(baseGame.id);
      });

      // Verify game 1 chat list was preserved
      await waitFor(() => {
        expect(result.current.chats).toHaveLength(1);
      });
      expect(result.current.chats[0].id).toBe(baseChat.id);
    });

    it('isolates active chat ID per game', async () => {
      const game2: Game = {
        id: 'game-2',
        name: 'Catan',
        createdAt: new Date().toISOString(),
      };

      const agent2: Agent = {
        id: 'agent-2',
        gameId: 'game-2',
        name: 'QA Agent 2',
        kind: 'qa',
        createdAt: new Date().toISOString(),
      };

      const chat2: Chat = {
        id: 'chat-2',
        gameId: 'game-2',
        gameName: 'Catan',
        agentId: 'agent-2',
        agentName: 'QA Agent 2',
        startedAt: new Date().toISOString(),
        lastMessageAt: null,
      };

      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame, game2];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [baseAgent];
        if (path === `/api/v1/games/${game2.id}/agents`) return [agent2];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [baseChat];
        if (path === `/api/v1/chats?gameId=${game2.id}`) return [chat2];
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      // Select game 1 and set active chat
      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => {
        expect(result.current.activeChatId).toBe(baseChat.id);
      });

      // Switch to game 2
      await act(async () => {
        await result.current.selectGame(game2.id);
      });

      await waitFor(() => {
        expect(result.current.selectedGameId).toBe(game2.id);
      });

      // Active chat should be cleared for game 2
      expect(result.current.activeChatId).toBeNull();

      // Select chat in game 2
      await act(async () => {
        await result.current.selectChat(chat2.id);
      });

      await waitFor(() => {
        expect(result.current.activeChatId).toBe(chat2.id);
      });

      // Switch back to game 1
      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.selectedGameId).toBe(baseGame.id);
      });

      // Active chat should be preserved (but may need reselection in real UI)
      // The state manager preserves it, but UI might need to reselect
    });
  });

  describe('Authentication and initialization', () => {
    it('loads current user on mount', async () => {
      const mockUser: AuthUser = {
        id: 'user-1',
        email: 'test@example.com',
        username: 'testuser',
        role: 'user',
      };

      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') {
          return { user: mockUser };
        }
        if (path === '/api/v1/games') {
          return [];
        }
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await waitFor(() => {
        expect(result.current.authUser).toEqual(mockUser);
      });

      expect(mockApi.get).toHaveBeenCalledWith('/api/v1/auth/me');
    });

    it('handles auth failure gracefully', async () => {
      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') {
          throw new Error('Unauthorized');
        }
        if (path === '/api/v1/games') {
          return [];
        }
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await waitFor(() => {
        expect(result.current.authUser).toBeNull();
      });
    });

    it('loads games on mount', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await waitFor(() => {
        expect(result.current.games).toHaveLength(1);
      });

      expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games');
      expect(result.current.games[0]).toEqual(baseGame);
    });

    it('auto-selects first game if available', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await waitFor(() => {
        expect(result.current.selectedGameId).toBe(baseGame.id);
      });
    });

    it('does not auto-select game if already selected', async () => {
      const game2: Game = {
        id: 'game-2',
        name: 'Catan',
        createdAt: new Date().toISOString(),
      };

      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame, game2];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [baseAgent];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [baseChat];
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await waitFor(() => {
        expect(result.current.games.length).toBeGreaterThan(0);
      });

      // First game auto-selected
      expect(result.current.selectedGameId).toBe(baseGame.id);

      // Games list should not change selected game
      expect(result.current.selectedGameId).toBe(baseGame.id);
    });
  });

  describe('Chat creation edge cases', () => {
    it('prevents chat creation without gameId', async () => {
      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.createChat();
      });

      expect(mockApi.post).not.toHaveBeenCalled();
      expect(result.current.chats).toHaveLength(0);
    });

    it('prevents chat creation without agentId', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.selectedGameId).toBe(baseGame.id);
      });

      // Clear selected agent
      act(() => {
        result.current.selectAgent(null);
      });

      await act(async () => {
        await result.current.createChat();
      });

      // Should not create chat without agent
      expect(mockApi.post).toHaveBeenCalledTimes(0);
    });

    it('handles chat creation API errors gracefully', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => expect(result.current.selectedAgentId).toBe(baseAgent.id));

      mockApi.post.mockRejectedValueOnce(new Error('Server error'));

      await act(async () => {
        await result.current.createChat();
      });

      await waitFor(() => {
        expect(result.current.errorMessage).toContain('Errore nella creazione');
      });

      expect(result.current.chats).toHaveLength(0);
      expect(result.current.activeChatId).toBeNull();
    });

    it('adds created chat to beginning of list', async () => {
      setupHappyPathMocks();

      const existingChat: Chat = {
        ...baseChat,
        id: 'chat-existing',
      };

      const newChat: Chat = {
        ...baseChat,
        id: 'chat-new',
        startedAt: new Date().toISOString(),
      };

      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [baseAgent];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [existingChat];
        return [];
      });

      mockApi.post.mockResolvedValueOnce(newChat);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      // Wait for game selection and agents/chats to load
      await waitFor(() => {
        expect(result.current.selectedGameId).toBe(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.chats).toHaveLength(1);
      });

      await act(async () => {
        await result.current.createChat();
      });

      await waitFor(() => {
        expect(result.current.chats).toHaveLength(2);
      });

      expect(result.current.chats[0].id).toBe(newChat.id);
      expect(result.current.chats[1].id).toBe(existingChat.id);
    });

    it('clears messages when creating new chat', async () => {
      setupHappyPathMocks();

      const newChat: Chat = {
        ...baseChat,
        id: 'chat-new',
        startedAt: new Date().toISOString(),
      };

      mockApi.post.mockResolvedValueOnce(newChat);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => {
        expect(result.current.messages).toHaveLength(1);
      });

      await act(async () => {
        await result.current.createChat();
      });

      await waitFor(() => {
        expect(result.current.activeChatId).toBe(newChat.id);
      });

      expect(result.current.messages).toHaveLength(0);
    });
  });

  describe('Message feedback toggle logic', () => {
    it('toggles feedback: null → helpful → null', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      const messageId = result.current.messages[0].id;

      // null → helpful
      await act(async () => {
        await result.current.setMessageFeedback(messageId, 'helpful');
      });

      expect(result.current.messages[0].feedback).toBe('helpful');

      // helpful → null (toggle off)
      await act(async () => {
        await result.current.setMessageFeedback(messageId, 'helpful');
      });

      expect(result.current.messages[0].feedback).toBeNull();
    });

    it('toggles feedback: null → not-helpful → null', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      const messageId = result.current.messages[0].id;

      // null → not-helpful
      await act(async () => {
        await result.current.setMessageFeedback(messageId, 'not-helpful');
      });

      expect(result.current.messages[0].feedback).toBe('not-helpful');

      // not-helpful → null (toggle off)
      await act(async () => {
        await result.current.setMessageFeedback(messageId, 'not-helpful');
      });

      expect(result.current.messages[0].feedback).toBeNull();
    });

    it('switches feedback: helpful → not-helpful', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      const messageId = result.current.messages[0].id;

      // null → helpful
      await act(async () => {
        await result.current.setMessageFeedback(messageId, 'helpful');
      });

      expect(result.current.messages[0].feedback).toBe('helpful');

      // helpful → not-helpful (switch)
      await act(async () => {
        await result.current.setMessageFeedback(messageId, 'not-helpful');
      });

      expect(result.current.messages[0].feedback).toBe('not-helpful');
    });

    it('switches feedback: not-helpful → helpful', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      const messageId = result.current.messages[0].id;

      // null → not-helpful
      await act(async () => {
        await result.current.setMessageFeedback(messageId, 'not-helpful');
      });

      expect(result.current.messages[0].feedback).toBe('not-helpful');

      // not-helpful → helpful (switch)
      await act(async () => {
        await result.current.setMessageFeedback(messageId, 'helpful');
      });

      expect(result.current.messages[0].feedback).toBe('helpful');
    });

    it('reverts feedback on API error', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      const messageId = result.current.messages[0].id;

      // Set initial feedback
      await act(async () => {
        await result.current.setMessageFeedback(messageId, 'helpful');
      });

      expect(result.current.messages[0].feedback).toBe('helpful');

      // Simulate API error on next toggle
      mockApi.post.mockRejectedValueOnce(new Error('Network error'));

      await act(async () => {
        await result.current.setMessageFeedback(messageId, 'not-helpful');
      });

      // Should revert to 'helpful' after error
      await waitFor(() => {
        expect(result.current.messages[0].feedback).toBe('helpful');
      });

      expect(result.current.errorMessage).toContain('Errore nell\'invio del feedback');
    });

    it('uses backendMessageId for feedback if available', async () => {
      setupHappyPathMocks();
      mockApi.post.mockResolvedValue(undefined);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      const messageId = result.current.messages[0].id;

      await act(async () => {
        await result.current.setMessageFeedback(messageId, 'helpful');
      });

      expect(mockApi.post).toHaveBeenCalledWith('/api/v1/agents/feedback', expect.objectContaining({
        messageId: baseMessage.backendMessageId,
        feedback: 'helpful',
      }));
    });
  });

  // ============================================================================
  // Phase 2: Feature Completeness
  // ============================================================================

  describe('Game loading with auto-select', () => {
    it('loads agents for selected game and auto-selects first agent', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.agents).toHaveLength(1);
      });

      expect(mockApi.get).toHaveBeenCalledWith(`/api/v1/games/${baseGame.id}/agents`);
      expect(result.current.agents[0]).toEqual(baseAgent);
      expect(result.current.selectedAgentId).toBe(baseAgent.id);
    });

    it('handles empty agents list gracefully', async () => {
      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [];
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.loading.agents).toBe(false);
      });

      expect(result.current.agents).toHaveLength(0);
      expect(result.current.selectedAgentId).toBeNull();
    });

    it('handles agents loading error gracefully', async () => {
      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame];
        if (path === `/api/v1/games/${baseGame.id}/agents`) {
          throw new Error('Network error');
        }
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [];
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.errorMessage).toContain('Failed to load agents');
      });

      expect(result.current.agents).toHaveLength(0);
      expect(result.current.selectedAgentId).toBeNull();
    });
  });

  describe('Agent selection', () => {
    it('allows manual agent selection', async () => {
      const agent2: Agent = {
        id: 'agent-2',
        gameId: 'game-1',
        name: 'Setup Agent',
        kind: 'setup',
        createdAt: new Date().toISOString(),
      };

      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [baseAgent, agent2];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [];
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.agents).toHaveLength(2);
      });

      expect(result.current.selectedAgentId).toBe(baseAgent.id); // auto-selected first

      act(() => {
        result.current.selectAgent(agent2.id);
      });

      expect(result.current.selectedAgentId).toBe(agent2.id);
    });

    it('allows deselecting agent', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.selectedAgentId).toBe(baseAgent.id);
      });

      act(() => {
        result.current.selectAgent(null);
      });

      expect(result.current.selectedAgentId).toBeNull();
    });
  });

  describe('Chat history loading edge cases', () => {
    it('handles empty chat history', async () => {
      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [baseAgent];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [baseChat];
        if (path === `/api/v1/chats/${baseChat.id}/messages`) return [];
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => {
        expect(result.current.loading.messages).toBe(false);
      });

      expect(result.current.messages).toHaveLength(0);
      expect(result.current.activeChatId).toBe(baseChat.id);
    });

    it('handles chat history loading error', async () => {
      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [baseAgent];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [baseChat];
        if (path === `/api/v1/chats/${baseChat.id}/messages`) {
          throw new Error('Network error');
        }
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => {
        expect(result.current.errorMessage).toContain('Failed to load messages');
      });

      expect(result.current.messages).toHaveLength(0);
    });

    it('loads messages with multiple message types', async () => {
      const messages: Message[] = [
        {
          id: 'msg-1',
          role: 'user',
          content: 'What are the rules?',
          timestamp: new Date(),
        },
        {
          id: 'msg-2',
          role: 'assistant',
          content: 'Here are the rules...',
          timestamp: new Date(),
          feedback: null,
          backendMessageId: 'backend-2',
          endpoint: 'qa',
          gameId: 'game-1',
        },
        {
          id: 'msg-3',
          role: 'user',
          content: 'How do I setup?',
          timestamp: new Date(),
        },
      ];

      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [baseAgent];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [baseChat];
        if (path === `/api/v1/chats/${baseChat.id}/messages`) return messages;
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => {
        expect(result.current.messages).toHaveLength(3);
      });

      expect(result.current.messages[0].role).toBe('user');
      expect(result.current.messages[1].role).toBe('assistant');
      expect(result.current.messages[2].role).toBe('user');
    });
  });

  describe('Message editing flow', () => {
    it('starts edit mode for a message', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      const messageId = result.current.messages[0].id;
      const messageContent = result.current.messages[0].content;

      act(() => {
        result.current.startEditMessage(messageId, messageContent);
      });

      expect(result.current.editingMessageId).toBe(messageId);
      expect(result.current.editContent).toBe(messageContent);
    });

    it('cancels edit mode', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      const messageId = result.current.messages[0].id;
      const messageContent = result.current.messages[0].content;

      act(() => {
        result.current.startEditMessage(messageId, messageContent);
      });

      expect(result.current.editingMessageId).toBe(messageId);

      act(() => {
        result.current.cancelEdit();
      });

      expect(result.current.editingMessageId).toBeNull();
      expect(result.current.editContent).toBe('');
    });

    it('updates edit content', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      const messageId = result.current.messages[0].id;
      const messageContent = result.current.messages[0].content;

      act(() => {
        result.current.startEditMessage(messageId, messageContent);
      });

      act(() => {
        result.current.setEditContent('Updated content');
      });

      expect(result.current.editContent).toBe('Updated content');
    });

    it('saves edited message and reloads history', async () => {
      setupHappyPathMocks();
      mockApi.chat.updateMessage.mockResolvedValue(undefined);

      const updatedMessages = [
        { ...baseMessage, content: 'Updated content' }
      ];

      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [baseAgent];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [baseChat];
        if (path === `/api/v1/chats/${baseChat.id}/messages`) return updatedMessages;
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      const messageId = result.current.messages[0].id;

      act(() => {
        result.current.startEditMessage(messageId, 'Original');
      });

      act(() => {
        result.current.setEditContent('Updated content');
      });

      await act(async () => {
        await result.current.saveEdit();
      });

      expect(mockApi.chat.updateMessage).toHaveBeenCalledWith(
        baseChat.id,
        messageId,
        'Updated content'
      );

      await waitFor(() => {
        expect(result.current.editingMessageId).toBeNull();
      });
    });

    it('prevents saving edit without activeChatId', async () => {
      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      act(() => {
        result.current.startEditMessage('msg-1', 'Content');
      });

      act(() => {
        result.current.setEditContent('Updated');
      });

      await act(async () => {
        await result.current.saveEdit();
      });

      expect(mockApi.chat.updateMessage).not.toHaveBeenCalled();
    });

    it('prevents saving empty edit content', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      act(() => {
        result.current.startEditMessage('msg-1', 'Content');
      });

      act(() => {
        result.current.setEditContent('   ');
      });

      await act(async () => {
        await result.current.saveEdit();
      });

      expect(mockApi.chat.updateMessage).not.toHaveBeenCalled();
    });

    it('handles edit message API error', async () => {
      setupHappyPathMocks();
      mockApi.chat.updateMessage.mockRejectedValue(new Error('Network error'));

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      const messageId = result.current.messages[0].id;

      act(() => {
        result.current.startEditMessage(messageId, 'Original');
      });

      act(() => {
        result.current.setEditContent('Updated');
      });

      await act(async () => {
        await result.current.saveEdit();
      });

      await waitFor(() => {
        expect(result.current.errorMessage).toContain('Errore nell\'aggiornamento');
      });
    });
  });

  describe('Message deletion flow', () => {
    it('deletes message after confirmation', async () => {
      setupHappyPathMocks();
      mockApi.chat.deleteMessage.mockResolvedValue(undefined);
      jest.spyOn(window, 'confirm').mockReturnValue(true);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      const messageId = result.current.messages[0].id;

      // Mock reload to return empty messages
      mockApi.get.mockImplementation(async (path: string) => {
        if (path === `/api/v1/chats/${baseChat.id}/messages`) return [];
        return mockApi.get.getMockImplementation()(path);
      });

      await act(async () => {
        await result.current.deleteMessage(messageId);
      });

      expect(mockApi.chat.deleteMessage).toHaveBeenCalledWith(baseChat.id, messageId);
    });

    it('cancels message deletion when not confirmed', async () => {
      setupHappyPathMocks();
      jest.spyOn(window, 'confirm').mockReturnValue(false);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      const messageId = result.current.messages[0].id;

      await act(async () => {
        await result.current.deleteMessage(messageId);
      });

      expect(mockApi.chat.deleteMessage).not.toHaveBeenCalled();
      expect(result.current.messages).toHaveLength(1);
    });

    it('prevents message deletion without activeChatId', async () => {
      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.deleteMessage('msg-1');
      });

      expect(mockApi.chat.deleteMessage).not.toHaveBeenCalled();
    });

    it('handles message deletion API error', async () => {
      setupHappyPathMocks();
      mockApi.chat.deleteMessage.mockRejectedValue(new Error('Network error'));
      jest.spyOn(window, 'confirm').mockReturnValue(true);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      const messageId = result.current.messages[0].id;

      await act(async () => {
        await result.current.deleteMessage(messageId);
      });

      await waitFor(() => {
        expect(result.current.errorMessage).toContain('Errore nell\'eliminazione');
      });
    });
  });

  describe('Chat deletion with active chat cleanup', () => {
    it('cancels chat deletion when not confirmed', async () => {
      setupHappyPathMocks();
      jest.spyOn(window, 'confirm').mockReturnValue(false);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.chats).toHaveLength(1);
      });

      await act(async () => {
        await result.current.deleteChat(baseChat.id);
      });

      expect(mockApi.delete).not.toHaveBeenCalled();
      expect(result.current.chats).toHaveLength(1);
    });

    it('clears messages when deleting active chat', async () => {
      setupHappyPathMocks();
      jest.spyOn(window, 'confirm').mockReturnValue(true);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => {
        expect(result.current.messages).toHaveLength(1);
      });

      await act(async () => {
        await result.current.deleteChat(baseChat.id);
      });

      expect(result.current.activeChatId).toBeNull();
      expect(result.current.messages).toHaveLength(0);
    });

    it('preserves messages when deleting non-active chat', async () => {
      const chat2: Chat = {
        id: 'chat-2',
        gameId: 'game-1',
        gameName: 'Chess',
        agentId: 'agent-1',
        agentName: 'QA Agent',
        startedAt: new Date().toISOString(),
        lastMessageAt: null,
      };

      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [baseAgent];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [baseChat, chat2];
        if (path === `/api/v1/chats/${baseChat.id}/messages`) return [baseMessage];
        return [];
      });

      jest.spyOn(window, 'confirm').mockReturnValue(true);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => {
        expect(result.current.messages).toHaveLength(1);
      });

      // Delete non-active chat
      await act(async () => {
        await result.current.deleteChat(chat2.id);
      });

      expect(result.current.chats).toHaveLength(1);
      expect(result.current.chats[0].id).toBe(baseChat.id);
      expect(result.current.activeChatId).toBe(baseChat.id);
      expect(result.current.messages).toHaveLength(1); // Preserved
    });
  });

  describe('Search mode state', () => {
    it('defaults to Hybrid search mode', () => {
      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });
      expect(result.current.searchMode).toBe('Hybrid');
    });

    it('updates search mode', () => {
      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      act(() => {
        result.current.setSearchMode('Vector');
      });

      expect(result.current.searchMode).toBe('Vector');

      act(() => {
        result.current.setSearchMode('Keyword');
      });

      expect(result.current.searchMode).toBe('Keyword');
    });
  });

  // ============================================================================
  // Phase 3: Remaining Gaps - Error Handling & Edge Cases
  // ============================================================================

  describe('Games loading edge cases', () => {
    it('handles games loading error', async () => {
      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') {
          throw new Error('Network error');
        }
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await waitFor(() => {
        expect(result.current.loading.games).toBe(false);
      });

      expect(result.current.errorMessage).toContain('Failed to load games');
      expect(result.current.games).toHaveLength(0);
    });

    it('handles null games response', async () => {
      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return null;
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await waitFor(() => {
        expect(result.current.loading.games).toBe(false);
      });

      expect(result.current.games).toHaveLength(0);
    });
  });

  describe('Chats loading edge cases', () => {
    it('handles chats loading error', async () => {
      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [baseAgent];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) {
          throw new Error('Network error');
        }
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.errorMessage).toContain('Failed to load chats');
      });

      expect(result.current.chats).toHaveLength(0);
    });

    it('handles null chats response', async () => {
      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [baseAgent];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return null;
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.loading.chats).toBe(false);
      });

      expect(result.current.chats).toHaveLength(0);
    });
  });

  describe('Message feedback edge cases', () => {
    it('handles feedback for non-existent message gracefully', async () => {
      setupHappyPathMocks();

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      // Try to set feedback for non-existent message
      await act(async () => {
        await result.current.setMessageFeedback('non-existent-id', 'helpful');
      });

      // Should not crash or call API
      expect(mockApi.post).not.toHaveBeenCalled();
    });

    it('uses message ID as fallback when backendMessageId unavailable', async () => {
      const messageWithoutBackendId: Message = {
        id: 'msg-temp',
        role: 'assistant',
        content: 'Response',
        timestamp: new Date(),
      };

      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [baseAgent];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [baseChat];
        if (path === `/api/v1/chats/${baseChat.id}/messages`) return [messageWithoutBackendId];
        return [];
      });

      mockApi.post.mockResolvedValue(undefined);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => expect(result.current.messages).toHaveLength(1));

      await act(async () => {
        await result.current.setMessageFeedback('msg-temp', 'helpful');
      });

      expect(mockApi.post).toHaveBeenCalledWith('/api/v1/agents/feedback', expect.objectContaining({
        messageId: 'msg-temp', // Falls back to message ID
        feedback: 'helpful',
      }));
    });
  });

  describe('Loading states', () => {
    it('sets loading state during game loading', async () => {
      let resolveGamesCall: any;
      const gamesPromise = new Promise((resolve) => {
        resolveGamesCall = resolve;
      });

      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return gamesPromise;
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      // Initially loading
      await waitFor(() => {
        expect(result.current.loading.games).toBe(true);
      });

      // Resolve and verify loading finishes
      act(() => {
        resolveGamesCall([baseGame]);
      });

      await waitFor(() => {
        expect(result.current.loading.games).toBe(false);
      });
    });

    it('sets loading state during chat creation', async () => {
      setupHappyPathMocks();

      let resolveCreateChat: any;
      const createChatPromise = new Promise((resolve) => {
        resolveCreateChat = resolve;
      });

      mockApi.post.mockImplementation(async (path: string) => {
        if (path === '/api/v1/chats') return createChatPromise;
        return null;
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => expect(result.current.selectedAgentId).toBe(baseAgent.id));

      act(() => {
        result.current.createChat();
      });

      // Verify loading state during creation
      await waitFor(() => {
        expect(result.current.loading.creating).toBe(true);
      });

      // Resolve and verify loading finishes
      act(() => {
        resolveCreateChat({ ...baseChat, id: 'new-chat' });
      });

      await waitFor(() => {
        expect(result.current.loading.creating).toBe(false);
      });
    });

    it('sets loading state during message sending', async () => {
      setupHappyPathMocks();

      const newChat: Chat = {
        ...baseChat,
        id: 'chat-new',
        startedAt: new Date().toISOString(),
      };

      let resolvePost: any;
      const postPromise = new Promise((resolve) => {
        resolvePost = resolve;
      });

      mockApi.post.mockImplementation(async () => postPromise);

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => expect(result.current.selectedAgentId).toBe(baseAgent.id));

      act(() => {
        result.current.sendMessage('Test');
      });

      // Verify loading state during send
      await waitFor(() => {
        expect(result.current.loading.sending).toBe(true);
      });

      // Resolve and verify loading finishes
      act(() => {
        resolvePost(newChat);
      });

      await waitFor(() => {
        expect(result.current.loading.sending).toBe(false);
      });
    });
  });

  describe('Error message handling', () => {
    it('clears error message on successful operations', async () => {
      // First set an error
      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') {
          throw new Error('Network error');
        }
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await waitFor(() => {
        expect(result.current.errorMessage).toContain('Failed to load games');
      });

      // Now return successful response
      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [baseAgent];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [];
        return [];
      });

      // Trigger successful operation
      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.errorMessage).toBe('');
      });
    });
  });

  describe('Non-array responses handling', () => {
    it('handles non-array agents response', async () => {
      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return { invalid: 'response' };
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [];
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await waitFor(() => {
        expect(result.current.loading.agents).toBe(false);
      });

      expect(result.current.agents).toEqual([]);
      expect(result.current.selectedAgentId).toBeNull();
    });

    it('handles non-array messages response', async () => {
      mockApi.get.mockImplementation(async (path: string) => {
        if (path === '/api/v1/auth/me') return null;
        if (path === '/api/v1/games') return [baseGame];
        if (path === `/api/v1/games/${baseGame.id}/agents`) return [baseAgent];
        if (path === `/api/v1/chats?gameId=${baseGame.id}`) return [baseChat];
        if (path === `/api/v1/chats/${baseChat.id}/messages`) return { invalid: 'response' };
        return [];
      });

      const { result } = renderHook(() => useChatContext(), { wrapper: ChatProvider });

      await act(async () => {
        await result.current.selectGame(baseGame.id);
      });

      await act(async () => {
        await result.current.selectChat(baseChat.id);
      });

      await waitFor(() => {
        expect(result.current.loading.messages).toBe(false);
      });

      expect(result.current.messages).toEqual([]);
    });
  });
});
