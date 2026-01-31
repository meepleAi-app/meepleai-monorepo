/**
 * sessionSlice Actions Tests
 */
import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import { subscribeWithSelector } from 'zustand/middleware';
import { createSessionSlice } from '../sessionSlice';
import { ChatStore } from '../../types';

describe('sessionSlice actions', () => {
  const createTestStore = () => {
    return create<ChatStore>()(
      subscribeWithSelector(
        immer((...args) => ({
          ...createSessionSlice(...args),
          // Mock other slices
          games: [],
          agents: [],
          setGames: vi.fn(),
          setAgents: vi.fn(),
          loadGames: vi.fn(),
          loadAgents: vi.fn(),
          chatsByGame: {},
          activeChatIds: {},
          loadChats: vi.fn(),
          createChat: vi.fn(),
          deleteChat: vi.fn(),
          selectChat: vi.fn(),
          updateChatTitle: vi.fn(),
          messagesByChat: {},
          loadMessages: vi.fn(),
          sendMessage: vi.fn(),
          editMessage: vi.fn(),
          deleteMessage: vi.fn(),
          setMessageFeedback: vi.fn(),
          addOptimisticMessage: vi.fn(),
          removeOptimisticMessage: vi.fn(),
          updateMessageInThread: vi.fn(),
          loading: {
            chats: false,
            messages: false,
            sending: false,
            creating: false,
            updating: false,
            deleting: false,
            games: false,
            agents: false,
          },
          error: null,
          inputValue: '',
          editingMessageId: null,
          editContent: '',
          searchMode: 'hybrid',
          setLoading: vi.fn(),
          setError: vi.fn(),
          clearError: vi.fn(),
          setInputValue: vi.fn(),
          startEdit: vi.fn(),
          cancelEdit: vi.fn(),
          saveEdit: vi.fn(),
          setEditContent: vi.fn(),
          setSearchMode: vi.fn(),
        }))
      )
    );
  };

  let store: ReturnType<typeof createTestStore>;

  beforeEach(() => {
    store = createTestStore();
  });

  describe('selectGame', () => {
    it('should select a game', () => {
      store.getState().selectGame('game-1');
      expect(store.getState().selectedGameId).toBe('game-1');
    });

    it('should reset agent when game changes', () => {
      store.getState().selectGame('game-1');
      store.getState().selectAgent('agent-1');

      expect(store.getState().selectedAgentId).toBe('agent-1');

      store.getState().selectGame('game-2');

      expect(store.getState().selectedGameId).toBe('game-2');
      expect(store.getState().selectedAgentId).toBeNull();
    });

    it('should not reset agent when same game is selected', () => {
      store.getState().selectGame('game-1');
      store.getState().selectAgent('agent-1');
      store.getState().selectGame('game-1');

      expect(store.getState().selectedAgentId).toBe('agent-1');
    });

    it('should allow selecting null game', () => {
      store.getState().selectGame('game-1');
      store.getState().selectGame(null);

      expect(store.getState().selectedGameId).toBeNull();
    });
  });

  describe('selectAgent', () => {
    it('should select an agent', () => {
      store.getState().selectAgent('agent-1');
      expect(store.getState().selectedAgentId).toBe('agent-1');
    });

    it('should allow selecting null agent', () => {
      store.getState().selectAgent('agent-1');
      store.getState().selectAgent(null);

      expect(store.getState().selectedAgentId).toBeNull();
    });

    it('should allow changing agents', () => {
      store.getState().selectAgent('agent-1');
      store.getState().selectAgent('agent-2');

      expect(store.getState().selectedAgentId).toBe('agent-2');
    });
  });

  describe('toggleSidebar', () => {
    it('should toggle sidebar state', () => {
      expect(store.getState().sidebarCollapsed).toBe(false);

      store.getState().toggleSidebar();
      expect(store.getState().sidebarCollapsed).toBe(true);

      store.getState().toggleSidebar();
      expect(store.getState().sidebarCollapsed).toBe(false);
    });
  });

  describe('setSidebarCollapsed', () => {
    it('should set sidebar collapsed state', () => {
      store.getState().setSidebarCollapsed(true);
      expect(store.getState().sidebarCollapsed).toBe(true);

      store.getState().setSidebarCollapsed(false);
      expect(store.getState().sidebarCollapsed).toBe(false);
    });
  });
});
