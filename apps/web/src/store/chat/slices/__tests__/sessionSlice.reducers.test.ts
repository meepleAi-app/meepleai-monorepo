/**
 * sessionSlice.reducers.test.ts
 * Tests for Session Slice reducers: toggleSidebar, setSidebarCollapsed
 * (Split from sessionSlice.test.ts - Issue #1504)
 */

import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import { subscribeWithSelector } from 'zustand/middleware';
import { createSessionSlice } from '../sessionSlice';
import { ChatStore } from '../../types';

describe('sessionSlice reducers', () => {
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
      const gameId = '770e8400-e29b-41d4-a716-000000000123';
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
      const gameId = '770e8400-e29b-41d4-a716-000000000123';
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
      store.getState().selectGame('770e8400-e29b-41d4-a716-000000000001');
      expect(store.getState().selectedGameId).toBe('770e8400-e29b-41d4-a716-000000000001');
      expect(store.getState().selectedAgentId).toBeNull();

      // Select agent
      store.getState().selectAgent('agent-1');
      expect(store.getState().selectedGameId).toBe('770e8400-e29b-41d4-a716-000000000001');
      expect(store.getState().selectedAgentId).toBe('agent-1');

      // Collapse sidebar
      store.getState().toggleSidebar();
      expect(store.getState().sidebarCollapsed).toBe(true);

      // Change game (should reset agent)
      store.getState().selectGame('770e8400-e29b-41d4-a716-000000000002');
      expect(store.getState().selectedGameId).toBe('770e8400-e29b-41d4-a716-000000000002');
      expect(store.getState().selectedAgentId).toBeNull();
      expect(store.getState().sidebarCollapsed).toBe(true);

      // Select new agent
      store.getState().selectAgent('agent-2');
      expect(store.getState().selectedGameId).toBe('770e8400-e29b-41d4-a716-000000000002');
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
      const gameId = '770e8400-e29b-41d4-a716-000000000123';
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
      const gameId = '770e8400-e29b-41d4-a716-000000000123';
      const agentId = 'agent-456';

      // Multiple rapid updates
      store.getState().selectGame(gameId);
      store.getState().selectAgent(agentId);
      store.getState().toggleSidebar();
      store.getState().setSidebarCollapsed(false);
      store.getState().selectGame('770e8400-e29b-41d4-a716-000000000789');

      // Final state should reflect all updates in order
      expect(store.getState().selectedGameId).toBe('770e8400-e29b-41d4-a716-000000000789');
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
      store.getState().selectGame('770e8400-e29b-41d4-a716-000000000001');
      store.getState().selectAgent('agent-1');
      expect(store.getState().selectedGameId).toBe('770e8400-e29b-41d4-a716-000000000001');
      expect(store.getState().selectedAgentId).toBe('agent-1');

      // Second cycle
      store.getState().selectGame('770e8400-e29b-41d4-a716-000000000002');
      expect(store.getState().selectedAgentId).toBeNull();
      store.getState().selectAgent('agent-2');
      expect(store.getState().selectedGameId).toBe('770e8400-e29b-41d4-a716-000000000002');
      expect(store.getState().selectedAgentId).toBe('agent-2');

      // Third cycle
      store.getState().selectGame('770e8400-e29b-41d4-a716-000000000003');
      expect(store.getState().selectedAgentId).toBeNull();
      store.getState().selectAgent('agent-3');
      expect(store.getState().selectedGameId).toBe('770e8400-e29b-41d4-a716-000000000003');
      expect(store.getState().selectedAgentId).toBe('agent-3');
    });
  });

  // ============================================================================
  // setSelectedDocuments Action Tests (Issue #2051)
  // ============================================================================

  describe('setSelectedDocuments', () => {
    it('should set selected document IDs', () => {
      expect(store.getState().selectedDocumentIds).toBeNull();

      store.getState().setSelectedDocuments(['doc-1', 'doc-2', 'doc-3']);

      expect(store.getState().selectedDocumentIds).toEqual(['doc-1', 'doc-2', 'doc-3']);
    });

    it('should set to null for all documents', () => {
      // First set specific documents
      store.getState().setSelectedDocuments(['doc-1', 'doc-2']);
      expect(store.getState().selectedDocumentIds).toEqual(['doc-1', 'doc-2']);

      // Then set to null (all documents)
      store.getState().setSelectedDocuments(null);

      expect(store.getState().selectedDocumentIds).toBeNull();
    });

    it('should replace existing selection', () => {
      store.getState().setSelectedDocuments(['doc-1', 'doc-2']);
      expect(store.getState().selectedDocumentIds).toEqual(['doc-1', 'doc-2']);

      store.getState().setSelectedDocuments(['doc-3']);

      expect(store.getState().selectedDocumentIds).toEqual(['doc-3']);
    });

    it('should handle empty array', () => {
      store.getState().setSelectedDocuments([]);

      expect(store.getState().selectedDocumentIds).toEqual([]);
    });

    it('should reset when game changes', () => {
      store.getState().setSelectedDocuments(['doc-1', 'doc-2']);
      expect(store.getState().selectedDocumentIds).toEqual(['doc-1', 'doc-2']);

      store.getState().selectGame('770e8400-e29b-41d4-a716-000000000001');

      // selectedDocumentIds should be reset to null on game change
      expect(store.getState().selectedDocumentIds).toBeNull();
    });

    it('should not affect other session state', () => {
      const gameId = '770e8400-e29b-41d4-a716-000000000123';
      const agentId = 'agent-456';

      store.getState().selectGame(gameId);
      store.getState().selectAgent(agentId);
      store.getState().setSidebarCollapsed(true);

      store.getState().setSelectedDocuments(['doc-1', 'doc-2']);

      expect(store.getState().selectedGameId).toBe(gameId);
      expect(store.getState().selectedAgentId).toBe(agentId);
      expect(store.getState().sidebarCollapsed).toBe(true);
      expect(store.getState().selectedDocumentIds).toEqual(['doc-1', 'doc-2']);
    });

    it('should preserve document selection when agent changes', () => {
      store.getState().selectGame('770e8400-e29b-41d4-a716-000000000001');
      store.getState().setSelectedDocuments(['doc-1', 'doc-2']);

      // Agent change should NOT reset documents (same game)
      store.getState().selectAgent('agent-1');
      expect(store.getState().selectedDocumentIds).toEqual(['doc-1', 'doc-2']);

      store.getState().selectAgent('agent-2');
      expect(store.getState().selectedDocumentIds).toEqual(['doc-1', 'doc-2']);
    });
  });

  // ============================================================================
  // State Consistency Tests
  // ============================================================================

  describe('State Consistency', () => {
    it('should maintain referential integrity when no changes occur', () => {
      const initialState = store.getState();
      const gameId = '770e8400-e29b-41d4-a716-000000000123';

      // Select same game twice
      store.getState().selectGame(gameId);
      const firstState = store.getState();

      store.getState().selectGame(gameId);
      const secondState = store.getState();

      expect(firstState.selectedGameId).toBe(secondState.selectedGameId);
      expect(firstState.selectedAgentId).toBe(secondState.selectedAgentId);
    });

    it('should handle sidebar state independently of selection state', () => {
      const gameId = '770e8400-e29b-41d4-a716-000000000123';
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
