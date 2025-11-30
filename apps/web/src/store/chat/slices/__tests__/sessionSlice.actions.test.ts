/**
 * sessionSlice.test.ts
 * Comprehensive tests for Session Slice (Issue #1083)
 *
 * Coverage targets:
 * - State initialization
 * - selectGame action and agent reset behavior
 * - selectAgent action
 * - toggleSidebar action
 * - setSidebarCollapsed action
 * - State transitions and edge cases
 */

import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import { subscribeWithSelector } from 'zustand/middleware';
import { createSessionSlice } from '../sessionSlice';
import { ChatStore } from '../../types';

describe('sessionSlice', () => {
  let store: ReturnType<typeof createTestStore>;

  // Helper to create a test store with session slice
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

  beforeEach(() => {
    store = createTestStore();
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // ============================================================================
  // State Initialization Tests
  // ============================================================================

  describe('State Initialization', () => {
    it('should initialize with null selectedGameId', () => {
      expect(store.getState().selectedGameId).toBeNull();
    });

    it('should initialize with null selectedAgentId', () => {
      expect(store.getState().selectedAgentId).toBeNull();
    });

    it('should initialize with sidebarCollapsed as false', () => {
      expect(store.getState().sidebarCollapsed).toBe(false);
    });
  });

  // ============================================================================
  // selectGame Action Tests
  // ============================================================================

  describe('selectGame', () => {
    it('should set selectedGameId to the provided value', () => {
      const gameId = '770e8400-e29b-41d4-a716-000000000123';

      store.getState().selectGame(gameId);

      expect(store.getState().selectedGameId).toBe(gameId);
    });

    it('should handle string gameId', () => {
      const gameId = 'game-abc-xyz';

      store.getState().selectGame(gameId);

      expect(store.getState().selectedGameId).toBe(gameId);
    });

    it('should handle null gameId', () => {
      // First set a game
      store.getState().selectGame('770e8400-e29b-41d4-a716-000000000123');
      expect(store.getState().selectedGameId).toBe('770e8400-e29b-41d4-a716-000000000123');

      // Then set to null
      store.getState().selectGame(null);

      expect(store.getState().selectedGameId).toBeNull();
    });

    it('should reset selectedAgentId when game changes', () => {
      const gameId1 = '770e8400-e29b-41d4-a716-000000000123';
      const gameId2 = '770e8400-e29b-41d4-a716-000000000456';
      const agentId = 'agent-789';

      // Select game and agent
      store.getState().selectGame(gameId1);
      store.getState().selectAgent(agentId);

      expect(store.getState().selectedGameId).toBe(gameId1);
      expect(store.getState().selectedAgentId).toBe(agentId);

      // Change game
      store.getState().selectGame(gameId2);

      expect(store.getState().selectedGameId).toBe(gameId2);
      expect(store.getState().selectedAgentId).toBeNull();
    });

    it('should NOT reset selectedAgentId when selecting the same game', () => {
      const gameId = '770e8400-e29b-41d4-a716-000000000123';
      const agentId = 'agent-789';

      // Select game and agent
      store.getState().selectGame(gameId);
      store.getState().selectAgent(agentId);

      expect(store.getState().selectedGameId).toBe(gameId);
      expect(store.getState().selectedAgentId).toBe(agentId);

      // Select same game again
      store.getState().selectGame(gameId);

      expect(store.getState().selectedGameId).toBe(gameId);
      expect(store.getState().selectedAgentId).toBe(agentId);
    });

    it('should reset selectedAgentId when changing from null to a game', () => {
      const gameId = '770e8400-e29b-41d4-a716-000000000123';
      const agentId = 'agent-789';

      // Start with null game
      expect(store.getState().selectedGameId).toBeNull();

      // Set an agent (edge case - shouldn't normally happen but should be handled)
      store.getState().selectAgent(agentId);
      expect(store.getState().selectedAgentId).toBe(agentId);

      // Select a game
      store.getState().selectGame(gameId);

      expect(store.getState().selectedGameId).toBe(gameId);
      expect(store.getState().selectedAgentId).toBeNull();
    });

    it('should reset selectedAgentId when changing from a game to null', () => {
      const gameId = '770e8400-e29b-41d4-a716-000000000123';
      const agentId = 'agent-789';

      // Select game and agent
      store.getState().selectGame(gameId);
      store.getState().selectAgent(agentId);

      expect(store.getState().selectedGameId).toBe(gameId);
      expect(store.getState().selectedAgentId).toBe(agentId);

      // Deselect game
      store.getState().selectGame(null);

      expect(store.getState().selectedGameId).toBeNull();
      expect(store.getState().selectedAgentId).toBeNull();
    });

    it('should handle rapid game changes correctly', () => {
      const gameId1 = '770e8400-e29b-41d4-a716-000000000001';
      const gameId2 = '770e8400-e29b-41d4-a716-000000000002';
      const gameId3 = '770e8400-e29b-41d4-a716-000000000003';
      const agentId = 'agent-123';

      store.getState().selectGame(gameId1);
      store.getState().selectAgent(agentId);

      store.getState().selectGame(gameId2);
      expect(store.getState().selectedAgentId).toBeNull();

      store.getState().selectAgent(agentId);
      expect(store.getState().selectedAgentId).toBe(agentId);

      store.getState().selectGame(gameId3);
      expect(store.getState().selectedAgentId).toBeNull();
    });
  });

  // ============================================================================
  // selectAgent Action Tests
  // ============================================================================

  describe('selectAgent', () => {
    it('should set selectedAgentId to the provided value', () => {
      const agentId = 'agent-123';

      store.getState().selectAgent(agentId);

      expect(store.getState().selectedAgentId).toBe(agentId);
    });

    it('should handle string agentId', () => {
      const agentId = 'agent-abc-xyz';

      store.getState().selectAgent(agentId);

      expect(store.getState().selectedAgentId).toBe(agentId);
    });

    it('should handle null agentId', () => {
      // First set an agent
      store.getState().selectAgent('agent-123');
      expect(store.getState().selectedAgentId).toBe('agent-123');

      // Then set to null
      store.getState().selectAgent(null);

      expect(store.getState().selectedAgentId).toBeNull();
    });

    it('should allow changing from one agent to another', () => {
      const agentId1 = 'agent-123';
      const agentId2 = 'agent-456';

      store.getState().selectAgent(agentId1);
      expect(store.getState().selectedAgentId).toBe(agentId1);

      store.getState().selectAgent(agentId2);
      expect(store.getState().selectedAgentId).toBe(agentId2);
    });

    it('should not affect selectedGameId', () => {
      const gameId = '770e8400-e29b-41d4-a716-000000000123';
      const agentId1 = 'agent-123';
      const agentId2 = 'agent-456';

      store.getState().selectGame(gameId);
      store.getState().selectAgent(agentId1);

      expect(store.getState().selectedGameId).toBe(gameId);
      expect(store.getState().selectedAgentId).toBe(agentId1);

      store.getState().selectAgent(agentId2);

      expect(store.getState().selectedGameId).toBe(gameId);
      expect(store.getState().selectedAgentId).toBe(agentId2);
    });

    it('should allow selecting the same agent multiple times', () => {
      const agentId = 'agent-123';

      store.getState().selectAgent(agentId);
      expect(store.getState().selectedAgentId).toBe(agentId);

      store.getState().selectAgent(agentId);
