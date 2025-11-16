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
          setGames: jest.fn(),
          setAgents: jest.fn(),
          loadGames: jest.fn(),
          loadAgents: jest.fn(),
          chatsByGame: {},
          activeChatIds: {},
          loadChats: jest.fn(),
          createChat: jest.fn(),
          deleteChat: jest.fn(),
          selectChat: jest.fn(),
          updateChatTitle: jest.fn(),
          messagesByChat: {},
          loadMessages: jest.fn(),
          sendMessage: jest.fn(),
          editMessage: jest.fn(),
          deleteMessage: jest.fn(),
          setMessageFeedback: jest.fn(),
          addOptimisticMessage: jest.fn(),
          removeOptimisticMessage: jest.fn(),
          updateMessageInThread: jest.fn(),
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
          setLoading: jest.fn(),
          setError: jest.fn(),
          clearError: jest.fn(),
          setInputValue: jest.fn(),
          startEdit: jest.fn(),
          cancelEdit: jest.fn(),
          saveEdit: jest.fn(),
          setEditContent: jest.fn(),
          setSearchMode: jest.fn(),
        }))
      )
    );
  };

  beforeEach(() => {
    store = createTestStore();
    jest.clearAllMocks();
  });

  afterEach(() => {
    jest.restoreAllMocks();
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
      const gameId = 'game-123';

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
      store.getState().selectGame('game-123');
      expect(store.getState().selectedGameId).toBe('game-123');

      // Then set to null
      store.getState().selectGame(null);

      expect(store.getState().selectedGameId).toBeNull();
    });

    it('should reset selectedAgentId when game changes', () => {
      const gameId1 = 'game-123';
      const gameId2 = 'game-456';
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
      const gameId = 'game-123';
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
      const gameId = 'game-123';
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
      const gameId = 'game-123';
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
      const gameId1 = 'game-1';
      const gameId2 = 'game-2';
      const gameId3 = 'game-3';
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
      const gameId = 'game-123';
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
      expect(store.getState().selectedAgentId).toBe(agentId);
    });
  });

  // ============================================================================
  // toggleSidebar Action Tests
  // ============================================================================

  describe('toggleSidebar', () => {
    it('should toggle sidebarCollapsed from false to true', () => {
      expect(store.getState().sidebarCollapsed).toBe(false);

      store.getState().toggleSidebar();

      expect(store.getState().sidebarCollapsed).toBe(true);
    });

    it('should toggle sidebarCollapsed from true to false', () => {
      // First set to true
      store.getState().setSidebarCollapsed(true);
      expect(store.getState().sidebarCollapsed).toBe(true);

      // Then toggle
      store.getState().toggleSidebar();

      expect(store.getState().sidebarCollapsed).toBe(false);
    });

    it('should toggle multiple times correctly', () => {
      expect(store.getState().sidebarCollapsed).toBe(false);

      store.getState().toggleSidebar();
      expect(store.getState().sidebarCollapsed).toBe(true);

      store.getState().toggleSidebar();
      expect(store.getState().sidebarCollapsed).toBe(false);

      store.getState().toggleSidebar();
      expect(store.getState().sidebarCollapsed).toBe(true);

      store.getState().toggleSidebar();
      expect(store.getState().sidebarCollapsed).toBe(false);
    });

    it('should not affect other session state', () => {
      const gameId = 'game-123';
      const agentId = 'agent-456';

      store.getState().selectGame(gameId);
      store.getState().selectAgent(agentId);

      expect(store.getState().selectedGameId).toBe(gameId);
      expect(store.getState().selectedAgentId).toBe(agentId);

      store.getState().toggleSidebar();

      expect(store.getState().selectedGameId).toBe(gameId);
      expect(store.getState().selectedAgentId).toBe(agentId);
      expect(store.getState().sidebarCollapsed).toBe(true);
    });
  });

  // ============================================================================
  // setSidebarCollapsed Action Tests
  // ============================================================================

  describe('setSidebarCollapsed', () => {
    it('should set sidebarCollapsed to true', () => {
      expect(store.getState().sidebarCollapsed).toBe(false);

      store.getState().setSidebarCollapsed(true);

      expect(store.getState().sidebarCollapsed).toBe(true);
    });

    it('should set sidebarCollapsed to false', () => {
      // First set to true
      store.getState().setSidebarCollapsed(true);
      expect(store.getState().sidebarCollapsed).toBe(true);

      // Then set to false
      store.getState().setSidebarCollapsed(false);

      expect(store.getState().sidebarCollapsed).toBe(false);
    });

    it('should allow setting the same value multiple times', () => {
      store.getState().setSidebarCollapsed(true);
      expect(store.getState().sidebarCollapsed).toBe(true);

      store.getState().setSidebarCollapsed(true);
      expect(store.getState().sidebarCollapsed).toBe(true);

      store.getState().setSidebarCollapsed(false);
      expect(store.getState().sidebarCollapsed).toBe(false);

      store.getState().setSidebarCollapsed(false);
      expect(store.getState().sidebarCollapsed).toBe(false);
    });

    it('should override toggle state', () => {
      expect(store.getState().sidebarCollapsed).toBe(false);

      store.getState().toggleSidebar();
      expect(store.getState().sidebarCollapsed).toBe(true);

      store.getState().setSidebarCollapsed(false);
      expect(store.getState().sidebarCollapsed).toBe(false);

      store.getState().toggleSidebar();
      expect(store.getState().sidebarCollapsed).toBe(true);

      store.getState().setSidebarCollapsed(true);
      expect(store.getState().sidebarCollapsed).toBe(true);
    });

    it('should not affect other session state', () => {
      const gameId = 'game-123';
      const agentId = 'agent-456';

      store.getState().selectGame(gameId);
      store.getState().selectAgent(agentId);

      expect(store.getState().selectedGameId).toBe(gameId);
      expect(store.getState().selectedAgentId).toBe(agentId);

      store.getState().setSidebarCollapsed(true);

      expect(store.getState().selectedGameId).toBe(gameId);
      expect(store.getState().selectedAgentId).toBe(agentId);
      expect(store.getState().sidebarCollapsed).toBe(true);
    });
  });

  // ============================================================================
  // Integration and Edge Case Tests
  // ============================================================================

  describe('Integration and Edge Cases', () => {
    it('should handle complete session workflow', () => {
      // Initial state
      expect(store.getState().selectedGameId).toBeNull();
      expect(store.getState().selectedAgentId).toBeNull();
      expect(store.getState().sidebarCollapsed).toBe(false);

      // Select game
      store.getState().selectGame('game-1');
      expect(store.getState().selectedGameId).toBe('game-1');
      expect(store.getState().selectedAgentId).toBeNull();

      // Select agent
      store.getState().selectAgent('agent-1');
      expect(store.getState().selectedGameId).toBe('game-1');
      expect(store.getState().selectedAgentId).toBe('agent-1');

      // Collapse sidebar
      store.getState().toggleSidebar();
      expect(store.getState().sidebarCollapsed).toBe(true);

      // Change game (should reset agent)
      store.getState().selectGame('game-2');
      expect(store.getState().selectedGameId).toBe('game-2');
      expect(store.getState().selectedAgentId).toBeNull();
      expect(store.getState().sidebarCollapsed).toBe(true);

      // Select new agent
      store.getState().selectAgent('agent-2');
      expect(store.getState().selectedGameId).toBe('game-2');
      expect(store.getState().selectedAgentId).toBe('agent-2');

      // Expand sidebar
      store.getState().setSidebarCollapsed(false);
      expect(store.getState().sidebarCollapsed).toBe(false);

      // Clear selection
      store.getState().selectGame(null);
      expect(store.getState().selectedGameId).toBeNull();
      expect(store.getState().selectedAgentId).toBeNull();
      expect(store.getState().sidebarCollapsed).toBe(false);
    });

    it('should maintain state independence between actions', () => {
      const gameId = 'game-123';
      const agentId = 'agent-456';

      // Set all state
      store.getState().selectGame(gameId);
      store.getState().selectAgent(agentId);
      store.getState().setSidebarCollapsed(true);

      // Verify all state is set
      expect(store.getState().selectedGameId).toBe(gameId);
      expect(store.getState().selectedAgentId).toBe(agentId);
      expect(store.getState().sidebarCollapsed).toBe(true);

      // Modify only sidebar
      store.getState().toggleSidebar();
      expect(store.getState().selectedGameId).toBe(gameId);
      expect(store.getState().selectedAgentId).toBe(agentId);
      expect(store.getState().sidebarCollapsed).toBe(false);

      // Modify only agent
      store.getState().selectAgent('agent-789');
      expect(store.getState().selectedGameId).toBe(gameId);
      expect(store.getState().selectedAgentId).toBe('agent-789');
      expect(store.getState().sidebarCollapsed).toBe(false);
    });

    it('should handle concurrent state updates', () => {
      const gameId = 'game-123';
      const agentId = 'agent-456';

      // Multiple rapid updates
      store.getState().selectGame(gameId);
      store.getState().selectAgent(agentId);
      store.getState().toggleSidebar();
      store.getState().setSidebarCollapsed(false);
      store.getState().selectGame('game-789');

      // Final state should reflect all updates in order
      expect(store.getState().selectedGameId).toBe('game-789');
      expect(store.getState().selectedAgentId).toBeNull(); // Reset by game change
      expect(store.getState().sidebarCollapsed).toBe(false);
    });

    it('should handle null-to-null transitions gracefully', () => {
      expect(store.getState().selectedGameId).toBeNull();
      expect(store.getState().selectedAgentId).toBeNull();

      store.getState().selectGame(null);
      store.getState().selectAgent(null);

      expect(store.getState().selectedGameId).toBeNull();
      expect(store.getState().selectedAgentId).toBeNull();
    });

    it('should preserve state through multiple game-agent cycles', () => {
      // First cycle
      store.getState().selectGame('game-1');
      store.getState().selectAgent('agent-1');
      expect(store.getState().selectedGameId).toBe('game-1');
      expect(store.getState().selectedAgentId).toBe('agent-1');

      // Second cycle
      store.getState().selectGame('game-2');
      expect(store.getState().selectedAgentId).toBeNull();
      store.getState().selectAgent('agent-2');
      expect(store.getState().selectedGameId).toBe('game-2');
      expect(store.getState().selectedAgentId).toBe('agent-2');

      // Third cycle
      store.getState().selectGame('game-3');
      expect(store.getState().selectedAgentId).toBeNull();
      store.getState().selectAgent('agent-3');
      expect(store.getState().selectedGameId).toBe('game-3');
      expect(store.getState().selectedAgentId).toBe('agent-3');
    });
  });

  // ============================================================================
  // State Consistency Tests
  // ============================================================================

  describe('State Consistency', () => {
    it('should maintain referential integrity when no changes occur', () => {
      const initialState = store.getState();
      const gameId = 'game-123';

      // Select same game twice
      store.getState().selectGame(gameId);
      const firstState = store.getState();

      store.getState().selectGame(gameId);
      const secondState = store.getState();

      expect(firstState.selectedGameId).toBe(secondState.selectedGameId);
      expect(firstState.selectedAgentId).toBe(secondState.selectedAgentId);
    });

    it('should handle sidebar state independently of selection state', () => {
      const gameId = 'game-123';
      const agentId = 'agent-456';

      // Set selections
      store.getState().selectGame(gameId);
      store.getState().selectAgent(agentId);

      // Toggle sidebar multiple times
      for (let i = 0; i < 5; i++) {
        store.getState().toggleSidebar();
      }

      // Selections should be unchanged
      expect(store.getState().selectedGameId).toBe(gameId);
      expect(store.getState().selectedAgentId).toBe(agentId);
      expect(store.getState().sidebarCollapsed).toBe(true); // Odd number of toggles
    });
  });
});
