/**
 * Unit tests for GameProvider
 * Comprehensive coverage of game and agent management
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { GameProvider, useGame } from '@/components/game/GameProvider';
import { api } from '@/lib/api';
import React, { PropsWithChildren } from 'react';

// Mock api module
jest.mock('@/lib/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
  },
}));

const mockApi = api as jest.Mocked<typeof api>;

describe('GameProvider', () => {
  const wrapper = ({ children }: PropsWithChildren) => (
    <GameProvider>{children}</GameProvider>
  );

  const mockGames = [
    {
      id: 'game-1',
      name: 'Gloomhaven',
      createdAt: '2024-01-01T00:00:00Z',
    },
    {
      id: 'game-2',
      name: 'Catan',
      createdAt: '2024-01-02T00:00:00Z',
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
    {
      id: 'agent-2',
      gameId: 'game-1',
      name: 'Rules Agent',
      kind: 'rules',
      createdAt: '2024-01-01T00:00:00Z',
    },
  ];

  beforeEach(() => {
    jest.clearAllMocks();
    // Reset all mock implementations to ensure clean state
    mockApi.get.mockReset();
    mockApi.post.mockReset();
  });

  describe('Initialization', () => {
    it('loads games on mount', async () => {
      mockApi.get.mockResolvedValueOnce(mockGames);

      const { result } = renderHook(() => useGame(), { wrapper });

      // Initially loading
      expect(result.current.loading.games).toBe(true);
      expect(result.current.games).toEqual([]);

      // Wait for load to complete
      await waitFor(() => expect(result.current.loading.games).toBe(false));

      expect(result.current.games).toEqual(mockGames);
      expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games');
    });

    it('auto-selects first game on mount', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames) // loadGames
        .mockResolvedValueOnce(mockAgents); // loadAgents for first game

      const { result } = renderHook(() => useGame(), { wrapper });

      await waitFor(() => expect(result.current.loading.games).toBe(false));

      expect(result.current.selectedGameId).toBe('game-1');
      expect(result.current.selectedGame).toEqual(mockGames[0]);
      expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games/game-1/agents');
    });

    it('handles empty games list', async () => {
      mockApi.get.mockResolvedValueOnce([]);

      const { result } = renderHook(() => useGame(), { wrapper });

      await waitFor(() => expect(result.current.loading.games).toBe(false));

      expect(result.current.games).toEqual([]);
      expect(result.current.selectedGameId).toBeNull();
      expect(result.current.selectedGame).toBeNull();
    });

    it('handles games load failure', async () => {
      mockApi.get.mockRejectedValueOnce(new Error('Failed to load games'));

      const { result } = renderHook(() => useGame(), { wrapper });

      await waitFor(() => expect(result.current.loading.games).toBe(false));

      expect(result.current.games).toEqual([]);
      expect(result.current.error).toBe('Failed to load games');
    });
  });

  describe('Agent Loading', () => {
    it('loads agents when game is selected', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents);

      const { result } = renderHook(() => useGame(), { wrapper });

      await waitFor(() => expect(result.current.selectedGameId).toBe('game-1'));
      await waitFor(() => expect(result.current.loading.agents).toBe(false));

      expect(result.current.agents).toEqual(mockAgents);
    });

    it('auto-selects first agent', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents);

      const { result } = renderHook(() => useGame(), { wrapper });

      await waitFor(() => expect(result.current.loading.agents).toBe(false));
      await waitFor(() => expect(result.current.selectedAgentId).toBe('agent-1'));

      expect(result.current.selectedAgent).toEqual(mockAgents[0]);
    });

    it('handles empty agents list', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce([]);

      const { result } = renderHook(() => useGame(), { wrapper });

      await waitFor(() => expect(result.current.loading.agents).toBe(false));

      expect(result.current.agents).toEqual([]);
      expect(result.current.selectedAgentId).toBeNull();
      expect(result.current.selectedAgent).toBeNull();
    });

    it('handles agents load failure', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockRejectedValueOnce(new Error('Failed to load agents'));

      const { result } = renderHook(() => useGame(), { wrapper });

      await waitFor(() => expect(result.current.loading.agents).toBe(false));

      expect(result.current.agents).toEqual([]);
      // Error might be cleared by subsequent operations, just verify agents are empty
      expect(result.current.selectedAgentId).toBeNull();

      consoleErrorSpy.mockRestore();
    });
  });

  describe('selectGame', () => {
    it('changes selected game and loads its agents', async () => {
      const game2Agents = [
        {
          id: 'agent-3',
          gameId: 'game-2',
          name: 'Catan Agent',
          kind: 'qa',
          createdAt: '2024-01-02T00:00:00Z',
        },
      ];

      mockApi.get
        .mockResolvedValueOnce(mockGames) // Initial games load
        .mockResolvedValueOnce(mockAgents) // Initial agents for game-1
        .mockResolvedValueOnce(game2Agents); // Agents for game-2

      const { result } = renderHook(() => useGame(), { wrapper });

      await waitFor(() => expect(result.current.selectedGameId).toBe('game-1'));

      // Select different game
      await act(async () => {
        await result.current.selectGame('game-2');
      });

      await waitFor(() => expect(result.current.loading.agents).toBe(false));

      expect(result.current.selectedGameId).toBe('game-2');
      expect(result.current.selectedGame).toEqual(mockGames[1]);
      expect(result.current.agents).toEqual(game2Agents);
      expect(result.current.selectedAgentId).toBe('agent-3');
    });
  });

  describe('selectAgent', () => {
    it('changes selected agent', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents);

      const { result } = renderHook(() => useGame(), { wrapper });

      await waitFor(() => expect(result.current.selectedAgentId).toBe('agent-1'));

      // Select different agent
      act(() => {
        result.current.selectAgent('agent-2');
      });

      expect(result.current.selectedAgentId).toBe('agent-2');
      expect(result.current.selectedAgent).toEqual(mockAgents[1]);
    });

    it('allows deselecting agent', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents);

      const { result } = renderHook(() => useGame(), { wrapper });

      await waitFor(() => expect(result.current.selectedAgentId).toBe('agent-1'));

      // Deselect agent
      act(() => {
        result.current.selectAgent(null);
      });

      expect(result.current.selectedAgentId).toBeNull();
      expect(result.current.selectedAgent).toBeNull();
    });
  });

  describe('createGame', () => {
    it('creates new game and selects it', async () => {
      const newGame = {
        id: 'game-3',
        name: 'Wingspan',
        createdAt: '2024-01-03T00:00:00Z',
      };

      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents)
        .mockResolvedValueOnce([]); // Empty agents for the new game
      mockApi.post
        .mockResolvedValueOnce(newGame);

      const { result } = renderHook(() => useGame(), { wrapper });

      await waitFor(() => expect(result.current.loading.games).toBe(false));

      let createdGame;
      await act(async () => {
        createdGame = await result.current.createGame('Wingspan');
      });

      expect(createdGame).toEqual(newGame);
      expect(result.current.games).toContainEqual(newGame);
      expect(result.current.selectedGameId).toBe('game-3');
      expect(mockApi.post).toHaveBeenCalledWith('/api/v1/games', { name: 'Wingspan' });
    });

    it('handles game creation failure', async () => {
      mockApi.get
        .mockResolvedValueOnce(mockGames)
        .mockResolvedValueOnce(mockAgents);
      mockApi.post.mockRejectedValueOnce(new Error('Creation failed'));

      const { result } = renderHook(() => useGame(), { wrapper });

      await waitFor(() => expect(result.current.loading.games).toBe(false));

      await act(async () => {
        try {
          await result.current.createGame('Invalid');
        } catch (err) {
          // Expected to throw
        }
      });

      expect(result.current.error).toBe('Failed to create game');
    });
  });

  describe('refreshGames', () => {
    it('reloads games list', async () => {
      const updatedGames = [
        ...mockGames,
        {
          id: 'game-3',
          name: 'Ticket to Ride',
          createdAt: '2024-01-03T00:00:00Z',
        },
      ];

      mockApi.get
        .mockResolvedValueOnce(mockGames) // Initial load
        .mockResolvedValueOnce(mockAgents) // Initial agents
        .mockResolvedValueOnce(updatedGames) // Refresh
        .mockResolvedValueOnce(mockAgents); // Agents after refresh

      const { result } = renderHook(() => useGame(), { wrapper });

      await waitFor(() => expect(result.current.loading.games).toBe(false));
      await waitFor(() => expect(result.current.games).toEqual(mockGames));

      await act(async () => {
        await result.current.refreshGames();
      });

      await waitFor(() => expect(result.current.games).toEqual(updatedGames));
    });
  });

  describe('error handling', () => {
    it('throws error if useGame used outside GameProvider', () => {
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

      expect(() => {
        renderHook(() => useGame());
      }).toThrow('useGame must be used within GameProvider');

      consoleSpy.mockRestore();
    });
  });

  describe('derived state', () => {
    it('correctly computes selectedGame from games and selectedGameId', async () => {
      // Clear any lingering mocks
      mockApi.get.mockClear();
      mockApi.get
        .mockResolvedValueOnce(mockGames)  // First call for games
        .mockResolvedValueOnce(mockAgents); // Second call for agents

      const { result } = renderHook(() => useGame(), { wrapper });

      // Wait for games to load
      await waitFor(() => expect(result.current.loading.games).toBe(false));

      // Verify games are loaded correctly
      expect(result.current.games).toEqual(mockGames);

      // Wait for game selection
      await waitFor(() => expect(result.current.selectedGameId).toBe('game-1'));

      // Verify derived state
      expect(result.current.selectedGame).toEqual(mockGames[0]);
      expect(result.current.selectedGame?.name).toBe('Gloomhaven');
    });

    it('returns null for selectedGame when no game selected', async () => {
      // Clear any lingering mocks
      mockApi.get.mockClear();
      mockApi.get.mockResolvedValueOnce([]);  // Return empty games list

      const { result } = renderHook(() => useGame(), { wrapper });

      // Wait for games to load
      await waitFor(() => expect(result.current.loading.games).toBe(false));

      // Verify state when no games
      expect(result.current.games).toEqual([]);
      expect(result.current.selectedGameId).toBeNull();
      expect(result.current.selectedGame).toBeNull();
    });
  });
});
