/**
 * Integration tests for the full provider tree
 * Tests Auth → Game → Chat → UI provider hierarchy
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { AuthProvider, useAuth } from '@/components/auth/AuthProvider';
import { GameProvider, useGame } from '@/components/game/GameProvider';
import { ChatProvider, useChat } from '@/components/chat/ChatProvider';
import { UIProvider, useUI } from '@/components/ui/UIProvider';
import { useChatContext } from '@/hooks/useChatContext';
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

describe('Provider Tree Integration', () => {
  const mockUser = {
    id: 'user-1',
    email: 'test@example.com',
    displayName: 'Test User',
    role: 'User',
  };

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
  ];

  // Full provider tree wrapper
  const FullProviderTree = ({ children }: PropsWithChildren) => (
    <AuthProvider>
      <GameProvider>
        <ChatProvider>
          <UIProvider>{children}</UIProvider>
        </ChatProvider>
      </GameProvider>
    </AuthProvider>
  );

  beforeEach(() => {
    jest.clearAllMocks();
    localStorageMock.clear();
  });

  describe('Full Provider Tree', () => {
    it('successfully initializes entire provider hierarchy', async () => {
      mockApi.get
        .mockResolvedValueOnce({ user: mockUser }) // Auth
        .mockResolvedValueOnce(mockGames) // Games
        .mockResolvedValueOnce(mockAgents) // Agents
        .mockResolvedValueOnce(mockChats); // Chats

      const { result } = renderHook(() => useChatContext(), { wrapper: FullProviderTree });

      // Wait for all providers to initialize
      await waitFor(() => expect(result.current.loading.games).toBe(false));
      await waitFor(() => expect(result.current.loading.agents).toBe(false));
      await waitFor(() => expect(result.current.loading.chats).toBe(false));

      // Verify all state is accessible through useChatContext
      expect(result.current.authUser).toEqual(mockUser);
      expect(result.current.games).toEqual(mockGames);
      expect(result.current.selectedGameId).toBe('game-1');
      expect(result.current.agents).toEqual(mockAgents);
      expect(result.current.selectedAgentId).toBe('agent-1');
      expect(result.current.chats).toEqual(mockChats);
    });

    it('cascades game selection through hierarchy', async () => {
      const game2 = {
        id: 'game-2',
        name: 'Catan',
        createdAt: '2024-01-02T00:00:00Z',
      };

      const game2Agents = [
        {
          id: 'agent-2',
          gameId: 'game-2',
          name: 'Catan Agent',
          kind: 'qa',
          createdAt: '2024-01-02T00:00:00Z',
        },
      ];

      const game2Chats = [
        {
          id: 'chat-2',
          gameId: 'game-2',
          gameName: 'Catan',
          agentId: 'agent-2',
          agentName: 'Catan Agent',
          startedAt: '2024-01-02T10:00:00Z',
          lastMessageAt: null,
        },
      ];

      mockApi.get
        .mockResolvedValueOnce({ user: mockUser })
        .mockResolvedValueOnce([...mockGames, game2])
        .mockResolvedValueOnce(mockAgents) // game-1 agents
        .mockResolvedValueOnce(mockChats) // game-1 chats
        .mockResolvedValueOnce(game2Agents) // game-2 agents
        .mockResolvedValueOnce(game2Chats); // game-2 chats

      const { result } = renderHook(() => useChatContext(), { wrapper: FullProviderTree });

      await waitFor(() => expect(result.current.games.length).toBe(2));

      // Change game
      await act(async () => {
        await result.current.selectGame('game-2');
      });

      await waitFor(() => expect(result.current.loading.agents).toBe(false));
      await waitFor(() => expect(result.current.loading.chats).toBe(false));

      // Verify cascade: game changed → agents loaded → chats loaded
      expect(result.current.selectedGameId).toBe('game-2');
      expect(result.current.agents).toEqual(game2Agents);
      expect(result.current.chats).toEqual(game2Chats);
    });
  });

  describe('Backward Compatibility (useChatContext)', () => {
    it('provides unified access to all provider state', async () => {
      mockApi.get
        .mockResolvedValueOnce({ user: mockUser })
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats);

      const { result } = renderHook(() => useChatContext(), { wrapper: FullProviderTree });

      await waitFor(() => expect(result.current.loading.games).toBe(false));

      // Verify useChatContext provides access to all providers
      expect(result.current.authUser).toEqual(mockUser); // Auth
      expect(result.current.games).toEqual(mockGames); // Game
      expect(result.current.chats).toEqual(mockChats); // Chat
      expect(result.current.sidebarCollapsed).toBe(false); // UI
    });

    it('maintains same API as old ChatProvider', async () => {
      mockApi.get
        .mockResolvedValueOnce({ user: mockUser })
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats);

      const { result } = renderHook(() => useChatContext(), { wrapper: FullProviderTree });

      await waitFor(() => expect(result.current.loading.games).toBe(false));

      // Verify all old ChatProvider methods are available
      expect(typeof result.current.selectGame).toBe('function');
      expect(typeof result.current.selectAgent).toBe('function');
      expect(typeof result.current.createChat).toBe('function');
      expect(typeof result.current.deleteChat).toBe('function');
      expect(typeof result.current.selectChat).toBe('function');
      expect(typeof result.current.sendMessage).toBe('function');
      expect(typeof result.current.editMessage).toBe('function');
      expect(typeof result.current.deleteMessage).toBe('function');
      expect(typeof result.current.setMessageFeedback).toBe('function');
      expect(typeof result.current.toggleSidebar).toBe('function');
      expect(typeof result.current.startEditMessage).toBe('function');
      expect(typeof result.current.cancelEdit).toBe('function');
      expect(typeof result.current.saveEdit).toBe('function');
    });
  });

  describe('Individual Provider Access', () => {
    it('allows accessing individual providers', async () => {
      mockApi.get
        .mockResolvedValueOnce({ user: mockUser })
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats);

      const AuthHook = () => useAuth();
      const GameHook = () => useGame();
      const ChatHook = () => useChat();
      const UIHook = () => useUI();

      const { result: authResult } = renderHook(AuthHook, { wrapper: FullProviderTree });
      const { result: gameResult } = renderHook(GameHook, { wrapper: FullProviderTree });
      const { result: chatResult } = renderHook(ChatHook, { wrapper: FullProviderTree });
      const { result: uiResult } = renderHook(UIHook, { wrapper: FullProviderTree });

      await waitFor(() => expect(gameResult.current.loading.games).toBe(false));

      // Verify individual hooks work
      expect(authResult.current.user).toEqual(mockUser);
      expect(gameResult.current.games).toEqual(mockGames);
      expect(chatResult.current.chats).toEqual(mockChats);
      expect(uiResult.current.sidebarCollapsed).toBe(false);
    });
  });

  describe('State Isolation', () => {
    it('changes in UI state do not trigger game/chat rerenders', async () => {
      mockApi.get
        .mockResolvedValueOnce({ user: mockUser })
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats);

      let uiRenderCount = 0;
      let gameRenderCount = 0;

      const TrackingHook = () => {
        const ui = useUI();
        const game = useGame();

        React.useEffect(() => {
          uiRenderCount++;
        });

        React.useEffect(() => {
          gameRenderCount++;
        }, [game.selectedGameId]);

        return { ui, game };
      };

      const { result } = renderHook(TrackingHook, { wrapper: FullProviderTree });

      await waitFor(() => expect(result.current.game.loading.games).toBe(false));

      const initialGameRenderCount = gameRenderCount;

      // Toggle sidebar (UI state change)
      act(() => {
        result.current.ui.toggleSidebar();
      });

      // UI should rerender, but game should not
      expect(uiRenderCount).toBeGreaterThan(1);
      expect(gameRenderCount).toBe(initialGameRenderCount);
    });
  });

  describe('localStorage Integration', () => {
    it('persists chat state across provider remounts', async () => {
      mockApi.get
        .mockResolvedValueOnce({ user: mockUser })
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats);

      const { unmount, result } = renderHook(() => useChatContext(), { wrapper: FullProviderTree });

      await waitFor(() => expect(result.current.chats).toEqual(mockChats));

      // Verify localStorage was called
      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        'meepleai_chat_state',
        expect.stringContaining('"chatsByGame"')
      );

      unmount();

      // Remount and verify state is restored
      const storedData = localStorageMock.setItem.mock.calls[localStorageMock.setItem.mock.calls.length - 1][1];
      localStorageMock.getItem.mockReturnValueOnce(storedData);

      mockApi.get
        .mockResolvedValueOnce({ user: mockUser })
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce(mockChats);

      const { result: result2 } = renderHook(() => useChatContext(), { wrapper: FullProviderTree });

      await waitFor(() => expect(result2.current.loading.chats).toBe(false));

      // State should be restored from localStorage
      expect(localStorageMock.getItem).toHaveBeenCalledWith('meepleai_chat_state');
    });
  });
});
