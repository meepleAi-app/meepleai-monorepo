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
import { ChatProvider, useChatContext } from '../ChatProvider';
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
});
