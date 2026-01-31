/**
 * Tests for gameSlice (Issue #2340)
 *
 * Coverage target: 90%+
 * Tests: Game catalog state management, API integration
 */

import { useChatStore } from '../../store';
import { api } from '@/lib/api';
import { resetChatStore, setupConsoleMocks } from './chatSlice.test-helpers';
import type { Game } from '@/types';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

// Mock dependencies
vi.mock('@/lib/api');
const mockApi = api as Mocked<typeof api>;

describe('gameSlice', () => {
  const consoleMocks = setupConsoleMocks();

  beforeEach(() => {
    resetChatStore(useChatStore);
    vi.clearAllMocks();
    consoleMocks.mockConsoleError.mockClear();
  });

  afterAll(() => {
    consoleMocks.restore();
  });

  describe('Initial State', () => {
    it('should initialize with empty games and agents', () => {
      const state = useChatStore.getState();
      expect(state.games).toEqual([]);
      expect(state.agents).toEqual([]);
    });
  });

  describe('setGames', () => {
    it('should update games state', () => {
      const mockGames: Game[] = [
        { id: 'game-1', name: 'Chess', bggId: null },
        { id: 'game-2', name: 'Monopoly', bggId: 12345 },
      ];

      useChatStore.getState().setGames(mockGames);

      const state = useChatStore.getState();
      expect(state.games).toEqual(mockGames);
      expect(state.games).toHaveLength(2);
    });

    it('should replace existing games', () => {
      const initial: Game[] = [{ id: 'old-1', name: 'Old Game', bggId: null }];
      useChatStore.getState().setGames(initial);

      const updated: Game[] = [{ id: 'new-1', name: 'New Game', bggId: null }];
      useChatStore.getState().setGames(updated);

      const state = useChatStore.getState();
      expect(state.games).toEqual(updated);
      expect(state.games).toHaveLength(1);
    });
  });

  describe('setAgents', () => {
    it('should update agents state', () => {
      const mockAgents: AgentDto[] = [
        {
          id: 'agent-1',
          name: 'Rules Expert',
          description: 'Expert in game rules',
          modelProvider: 'openai',
          modelName: 'gpt-4',
          temperature: 0.7,
          maxTokens: 1000,
          systemPrompt: 'You are a rules expert',
          isActive: true,
        },
      ];

      useChatStore.getState().setAgents(mockAgents);

      const state = useChatStore.getState();
      expect(state.agents).toEqual(mockAgents);
      expect(state.agents).toHaveLength(1);
    });
  });

  describe('loadGames', () => {
    it('should load games successfully', async () => {
      const mockGames: Game[] = [
        { id: 'game-1', name: 'Catan', bggId: 13 },
        { id: 'game-2', name: 'Ticket to Ride', bggId: 9209 },
      ];

      mockApi.games.getAll = vi.fn().mockResolvedValueOnce({
        games: mockGames,
        total: 2,
      });

      await useChatStore.getState().loadGames();

      const state = useChatStore.getState();
      expect(state.games).toEqual(mockGames);
      expect(state.loading.games).toBe(false);
      expect(state.error).toBeNull();
    });

    it('should handle empty response', async () => {
      mockApi.games.getAll = vi.fn().mockResolvedValueOnce({ games: null, total: 0 });

      await useChatStore.getState().loadGames();

      const state = useChatStore.getState();
      expect(state.games).toEqual([]);
      expect(state.loading.games).toBe(false);
    });

    it('should handle API errors', async () => {
      mockApi.games.getAll = vi.fn().mockRejectedValueOnce(new Error('Network error'));

      await useChatStore.getState().loadGames();

      const state = useChatStore.getState();
      expect(state.games).toEqual([]);
      expect(state.error).toBe('Errore nel caricamento dei giochi');
      expect(state.loading.games).toBe(false);
    });

    it('should set loading state during fetch', async () => {
      let resolvePromise: (value: any) => void;
      const promise = new Promise(resolve => {
        resolvePromise = resolve;
      });

      mockApi.games.getAll = vi.fn().mockReturnValueOnce(promise);

      const loadPromise = useChatStore.getState().loadGames();

      // Should be loading immediately
      expect(useChatStore.getState().loading.games).toBe(true);

      // Resolve and wait
      resolvePromise!({ games: [], total: 0 });
      await loadPromise;

      expect(useChatStore.getState().loading.games).toBe(false);
    });
  });

  describe('loadAgents', () => {
    it('should load agents successfully', async () => {
      const mockAgents: AgentDto[] = [
        {
          id: 'agent-1',
          name: 'Rules Agent',
          description: 'Helps with rules',
          modelProvider: 'openai',
          modelName: 'gpt-4',
          temperature: 0.7,
          maxTokens: 2000,
          systemPrompt: 'System prompt',
          isActive: true,
        },
        {
          id: 'agent-2',
          name: 'Strategy Agent',
          description: 'Helps with strategy',
          modelProvider: 'anthropic',
          modelName: 'claude-3',
          temperature: 0.5,
          maxTokens: 1500,
          systemPrompt: 'System prompt 2',
          isActive: true,
        },
      ];

      mockApi.agents.getAvailable = vi.fn().mockResolvedValueOnce(mockAgents);

      await useChatStore.getState().loadAgents();

      const state = useChatStore.getState();
      expect(state.agents).toEqual(mockAgents);
      expect(state.loading.agents).toBe(false);
      expect(state.error).toBeNull();
    });

    it('should handle null response', async () => {
      mockApi.agents.getAvailable = vi.fn().mockResolvedValueOnce(null);

      await useChatStore.getState().loadAgents();

      const state = useChatStore.getState();
      expect(state.agents).toEqual([]);
      expect(state.loading.agents).toBe(false);
    });

    it('should handle API errors', async () => {
      mockApi.agents.getAvailable = vi.fn().mockRejectedValueOnce(new Error('Server error'));

      await useChatStore.getState().loadAgents();

      const state = useChatStore.getState();
      expect(state.agents).toEqual([]);
      expect(state.error).toBe('Errore nel caricamento degli agenti');
      expect(state.loading.agents).toBe(false);
    });

    it('should set loading state during fetch', async () => {
      let resolvePromise: (value: any) => void;
      const promise = new Promise(resolve => {
        resolvePromise = resolve;
      });

      mockApi.agents.getAvailable = vi.fn().mockReturnValueOnce(promise);

      const loadPromise = useChatStore.getState().loadAgents();

      // Should be loading
      expect(useChatStore.getState().loading.agents).toBe(true);

      // Resolve
      resolvePromise!([]);
      await loadPromise;

      expect(useChatStore.getState().loading.agents).toBe(false);
    });
  });
});
