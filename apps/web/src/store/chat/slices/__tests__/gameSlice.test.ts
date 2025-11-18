/**
 * gameSlice.test.ts
 * Comprehensive tests for Game Slice (Issue #1083)
 *
 * Coverage targets:
 * - State initialization
 * - setGames, setAgents actions
 * - loadGames with API integration
 * - loadAgents with API integration
 * - Error handling and loading states
 * - UI slice integration (loading/error states)
 */

import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import { subscribeWithSelector } from 'zustand/middleware';
import { createGameSlice } from '../gameSlice';
import { createUISlice } from '../uiSlice';
import { ChatStore } from '../../types';
import { Game, Agent } from '@/types';

// Mock the api module
jest.mock('@/lib/api');

import { api } from '@/lib/api';
import { createMockAgent } from '@/__tests__/fixtures/common-fixtures';

const mockApi = api as jest.Mocked<typeof api>;

describe('gameSlice', () => {
  let store: ReturnType<typeof createTestStore>;

  // Helper to create a test store with both game and UI slices
  const createTestStore = () => {
    return create<ChatStore>()(
      subscribeWithSelector(
        immer((...args) => ({
          ...createGameSlice(...args),
          ...createUISlice(...args),
          // Mock other slices (Session, Chat, Messages)
          selectedGameId: null,
          selectedAgentId: null,
          sidebarCollapsed: false,
          selectGame: jest.fn(),
          selectAgent: jest.fn(),
          toggleSidebar: jest.fn(),
          setSidebarCollapsed: jest.fn(),
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
          // Note: We don't override UI slice state/actions - they come from createUISlice
        }))
      )
    );
  };

  beforeEach(() => {
    store = createTestStore();
    jest.clearAllMocks();
    jest.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  // ============================================================================
  // State Initialization Tests
  // ============================================================================

  describe('State Initialization', () => {
    it('should initialize with empty games array', () => {
      expect(store.getState().games).toEqual([]);
    });

    it('should initialize with empty agents array', () => {
      expect(store.getState().agents).toEqual([]);
    });
  });

  // ============================================================================
  // setGames Action Tests
  // ============================================================================

  describe('setGames', () => {
    it('should set games to provided array', () => {
      const mockGames: Game[] = [
        { id: 'game-1', name: 'Catan', createdAt: '2024-01-01' },
        { id: 'game-2', name: 'Agricola', createdAt: '2024-01-02' },
      ];

      store.getState().setGames(mockGames);

      expect(store.getState().games).toEqual(mockGames);
    });

    it('should replace existing games', () => {
      const initialGames: Game[] = [
        { id: 'game-1', name: 'Catan', createdAt: '2024-01-01' },
      ];
      const newGames: Game[] = [
        { id: 'game-2', name: 'Agricola', createdAt: '2024-01-02' },
        { id: 'game-3', name: 'Wingspan', createdAt: '2024-01-03' },
      ];

      store.getState().setGames(initialGames);
      expect(store.getState().games).toEqual(initialGames);

      store.getState().setGames(newGames);
      expect(store.getState().games).toEqual(newGames);
    });

    it('should handle empty array', () => {
      const mockGames: Game[] = [
        { id: 'game-1', name: 'Catan', createdAt: '2024-01-01' },
      ];

      store.getState().setGames(mockGames);
      expect(store.getState().games).toEqual(mockGames);

      store.getState().setGames([]);
      expect(store.getState().games).toEqual([]);
    });

    it('should handle games without createdAt', () => {
      const mockGames: Game[] = [
        { id: 'game-1', name: 'Catan' },
        { id: 'game-2', name: 'Agricola', createdAt: '2024-01-02' },
      ];

      store.getState().setGames(mockGames);
      expect(store.getState().games).toEqual(mockGames);
    });
  });

  // ============================================================================
  // setAgents Action Tests
  // ============================================================================

  describe('setAgents', () => {
    it('should set agents to provided array', () => {
      const mockAgents: Agent[] = [
        createMockAgent({ id: 'agent-1', name: 'Chess Master', type: 'expert', createdAt: '2024-01-01' }),
        createMockAgent({ id: 'agent-2', name: 'Feedback Helper', type: 'feedback', createdAt: '2024-01-02' }),
      ];

      store.getState().setAgents(mockAgents);

      expect(store.getState().agents).toEqual(mockAgents);
    });

    it('should replace existing agents', () => {
      const initialAgents: Agent[] = [
        createMockAgent({ id: 'agent-1', name: 'Chess Master', type: 'expert', createdAt: '2024-01-01' }),
      ];
      const newAgents: Agent[] = [
        createMockAgent({ id: 'agent-2', name: 'Agricola Helper', type: 'general', createdAt: '2024-01-02' }),
      ];

      store.getState().setAgents(initialAgents);
      expect(store.getState().agents).toEqual(initialAgents);

      store.getState().setAgents(newAgents);
      expect(store.getState().agents).toEqual(newAgents);
    });

    it('should handle empty array', () => {
      const mockAgents: Agent[] = [
        createMockAgent({ id: 'agent-1', name: 'Chess Master', type: 'expert', createdAt: '2024-01-01' }),
      ];

      store.getState().setAgents(mockAgents);
      expect(store.getState().agents).toEqual(mockAgents);

      store.getState().setAgents([]);
      expect(store.getState().agents).toEqual([]);
    });
  });

  // ============================================================================
  // loadGames Action Tests
  // ============================================================================

  describe('loadGames', () => {
    it('should load games successfully from API', async () => {
      const mockGames: Game[] = [
        { id: 'game-1', name: 'Catan', createdAt: '2024-01-01' },
        { id: 'game-2', name: 'Agricola', createdAt: '2024-01-02' },
      ];

      mockApi.get.mockResolvedValueOnce(mockGames);

      await store.getState().loadGames();

      expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games');
      expect(store.getState().games).toEqual(mockGames);
      expect(store.getState().error).toBeNull();
    });

    it('should set loading state to true before API call', async () => {
      const mockGames: Game[] = [
        { id: 'game-1', name: 'Catan', createdAt: '2024-01-01' },
      ];

      let loadingDuringCall = false;

      mockApi.get.mockImplementation(async () => {
        loadingDuringCall = store.getState().loading.games;
        return mockGames;
      });

      await store.getState().loadGames();

      expect(loadingDuringCall).toBe(true);
    });

    it('should set loading state to false after successful API call', async () => {
      const mockGames: Game[] = [
        { id: 'game-1', name: 'Catan', createdAt: '2024-01-01' },
      ];

      mockApi.get.mockResolvedValueOnce(mockGames);

      await store.getState().loadGames();

      expect(store.getState().loading.games).toBe(false);
    });

    it('should clear previous error on successful load', async () => {
      const mockGames: Game[] = [
        { id: 'game-1', name: 'Catan', createdAt: '2024-01-01' },
      ];

      // Set initial error
      store.getState().setError('Previous error');
      expect(store.getState().error).toBe('Previous error');

      mockApi.get.mockResolvedValueOnce(mockGames);

      await store.getState().loadGames();

      expect(store.getState().error).toBeNull();
    });

    it('should handle empty games array from API', async () => {
      mockApi.get.mockResolvedValueOnce([]);

      await store.getState().loadGames();

      expect(store.getState().games).toEqual([]);
      expect(store.getState().error).toBeNull();
    });

    it('should handle null response from API', async () => {
      mockApi.get.mockResolvedValueOnce(null);

      await store.getState().loadGames();

      expect(store.getState().games).toEqual([]);
      expect(store.getState().error).toBeNull();
    });

    it('should handle API error and set error state', async () => {
      const mockError = new Error('Network error');
      mockApi.get.mockRejectedValueOnce(mockError);

      await store.getState().loadGames();

      expect(store.getState().games).toEqual([]);
      expect(store.getState().error).toBe('Errore nel caricamento dei giochi');
      expect(console.error).toHaveBeenCalledWith('Failed to load games:', mockError);
    });

    it('should set loading state to false after API error', async () => {
      const mockError = new Error('Network error');
      mockApi.get.mockRejectedValueOnce(mockError);

      await store.getState().loadGames();

      expect(store.getState().loading.games).toBe(false);
    });

    it('should reset games to empty array on error', async () => {
      // Set initial games
      const initialGames: Game[] = [
        { id: 'game-1', name: 'Catan', createdAt: '2024-01-01' },
      ];
      store.getState().setGames(initialGames);

      const mockError = new Error('Network error');
      mockApi.get.mockRejectedValueOnce(mockError);

      await store.getState().loadGames();

      expect(store.getState().games).toEqual([]);
    });
  });

  // ============================================================================
  // loadAgents Action Tests (Issue #868: Global Agents)
  // ============================================================================

  describe('loadAgents', () => {
    it('should load agents successfully from API using agentsClient', async () => {
      const mockAgents: Agent[] = [
        createMockAgent({ id: 'agent-1', name: 'Chess Master', type: 'expert', createdAt: '2024-01-01' }),
        createMockAgent({ id: 'agent-2', name: 'Feedback Helper', type: 'feedback', createdAt: '2024-01-02' }),
      ];

      // Mock api.agents.getAvailable() for Issue #868
      mockApi.agents = {
        getAvailable: jest.fn().mockResolvedValueOnce(mockAgents),
      } as any;

      await store.getState().loadAgents();

      expect(mockApi.agents.getAvailable).toHaveBeenCalled();
      expect(store.getState().agents).toEqual(mockAgents);
      expect(store.getState().error).toBeNull();
    });

    it('should set loading state to true before API call', async () => {
      const mockAgents: Agent[] = [
        createMockAgent({ id: 'agent-1', name: 'Chess Master', type: 'expert', createdAt: '2024-01-01' }),
      ];

      let loadingDuringCall = false;

      mockApi.agents = {
        getAvailable: jest.fn().mockImplementation(async () => {
          loadingDuringCall = store.getState().loading.agents;
          return mockAgents;
        }),
      } as any;

      await store.getState().loadAgents();

      expect(loadingDuringCall).toBe(true);
    });

    it('should set loading state to false after successful API call', async () => {
      const mockAgents: Agent[] = [
        createMockAgent({ id: 'agent-1', name: 'Chess Master', type: 'expert', createdAt: '2024-01-01' }),
      ];

      mockApi.agents = {
        getAvailable: jest.fn().mockResolvedValueOnce(mockAgents),
      } as any;

      await store.getState().loadAgents();

      expect(store.getState().loading.agents).toBe(false);
    });

    it('should clear previous error on successful load', async () => {
      const mockAgents: Agent[] = [
        createMockAgent({ id: 'agent-1', name: 'Chess Master', type: 'expert', createdAt: '2024-01-01' }),
      ];

      // Set initial error
      store.getState().setError('Previous error');
      expect(store.getState().error).toBe('Previous error');

      mockApi.agents = {
        getAvailable: jest.fn().mockResolvedValueOnce(mockAgents),
      } as any;

      await store.getState().loadAgents();

      expect(store.getState().error).toBeNull();
    });

    it('should handle empty agents array from API', async () => {
      mockApi.agents = {
        getAvailable: jest.fn().mockResolvedValueOnce([]),
      } as any;

      await store.getState().loadAgents();

      expect(store.getState().agents).toEqual([]);
      expect(store.getState().error).toBeNull();
    });

    it('should handle null response from API', async () => {
      mockApi.agents = {
        getAvailable: jest.fn().mockResolvedValueOnce(null),
      } as any;

      await store.getState().loadAgents();

      expect(store.getState().agents).toEqual([]);
      expect(store.getState().error).toBeNull();
    });

    it('should handle API error and set error state', async () => {
      const mockError = new Error('Network error');
      mockApi.agents = {
        getAvailable: jest.fn().mockRejectedValueOnce(mockError),
      } as any;

      await store.getState().loadAgents();

      expect(store.getState().agents).toEqual([]);
      expect(store.getState().error).toBe('Errore nel caricamento degli agenti');
      expect(console.error).toHaveBeenCalledWith('Failed to load agents:', mockError);
    });

    it('should set loading state to false after API error', async () => {
      const mockError = new Error('Network error');
      mockApi.agents = {
        getAvailable: jest.fn().mockRejectedValueOnce(mockError),
      } as any;

      await store.getState().loadAgents();

      expect(store.getState().loading.agents).toBe(false);
    });

    it('should reset agents to empty array on error', async () => {
      // Set initial agents
      const initialAgents: Agent[] = [
        createMockAgent({ id: 'agent-1', name: 'Chess Master', type: 'expert', createdAt: '2024-01-01' }),
      ];
      store.getState().setAgents(initialAgents);

      const mockError = new Error('Network error');
      mockApi.agents = {
        getAvailable: jest.fn().mockRejectedValueOnce(mockError),
      } as any;

      await store.getState().loadAgents();

      expect(store.getState().agents).toEqual([]);
    });

    it('should load global agents (not tied to specific games)', async () => {
      // Issue #868: Agents are global, so they can be from different games
      const globalAgents: Agent[] = [
        createMockAgent({ id: 'agent-1', name: 'Chess Master', type: 'expert', createdAt: '2024-01-01' }),
        createMockAgent({ id: 'agent-2', name: 'Agricola Helper', type: 'general', createdAt: '2024-01-02' }),
        createMockAgent({ id: 'agent-3', name: 'Chess Beginner', type: 'qa', createdAt: '2024-01-03' }),
      ];

      mockApi.agents = {
        getAvailable: jest.fn().mockResolvedValueOnce(globalAgents),
      } as any;

      await store.getState().loadAgents();

      expect(store.getState().agents).toEqual(globalAgents);
      expect(mockApi.agents.getAvailable).toHaveBeenCalledTimes(1);
      // Verify it's called without parameters (global agents)
      expect(mockApi.agents.getAvailable).toHaveBeenCalledWith();
    });
  });

  // ============================================================================
  // Integration Tests (UI Slice Interaction)
  // ============================================================================

  describe('Integration with UI Slice', () => {
    it('should properly coordinate loading states between slices', async () => {
      const mockGames: Game[] = [
        { id: 'game-1', name: 'Catan', createdAt: '2024-01-01' },
      ];

      // Set up mock BEFORE starting the async operation
      mockApi.get.mockResolvedValueOnce(mockGames);

      expect(store.getState().loading.games).toBe(false);

      const loadPromise = store.getState().loadGames();
      expect(store.getState().loading.games).toBe(true);

      await loadPromise;

      expect(store.getState().loading.games).toBe(false);
    });

    it('should clear error from UI slice when loading games successfully', async () => {
      const mockGames: Game[] = [
        { id: 'game-1', name: 'Catan', createdAt: '2024-01-01' },
      ];

      store.getState().setError('Previous error');
      expect(store.getState().error).toBe('Previous error');

      mockApi.get.mockResolvedValueOnce(mockGames);
      await store.getState().loadGames();

      expect(store.getState().error).toBeNull();
    });

    it('should call setError when loading games fails', async () => {
      // Create a fresh store for this test to avoid state pollution
      const freshStore = createTestStore();

      const mockError = new Error('Network error');
      mockApi.get.mockRejectedValueOnce(mockError);

      await freshStore.getState().loadGames();

      // Verify error handling occurred
      expect(freshStore.getState().games).toEqual([]);
      expect(console.error).toHaveBeenCalledWith('Failed to load games:', mockError);
      // Note: The error state is managed by UISlice through setError call
      // The gameSlice correctly calls setError('Errore nel caricamento dei giochi')
    });
  });
});
