/**
 * GameProvider - Game and agent selection state management
 *
 * Manages:
 * - Games catalog and selection
 * - Agents per game
 * - Agent selection
 *
 * Nested under AuthProvider in provider hierarchy
 */

import React, { createContext, useContext, useState, useCallback, useMemo, useEffect, PropsWithChildren } from 'react';
import { Game, Agent } from '@/types';
import { api } from '@/lib/api';

// ============================================================================
// Types
// ============================================================================

interface LoadingState {
  games: boolean;
  agents: boolean;
}

export interface GameContextValue {
  // Games
  games: Game[];
  selectedGame: Game | null;
  selectedGameId: string | null;
  selectGame: (gameId: string) => Promise<void>;
  createGame: (name: string) => Promise<Game>;
  refreshGames: () => Promise<void>;

  // Agents
  agents: Agent[];
  selectedAgent: Agent | null;
  selectedAgentId: string | null;
  selectAgent: (agentId: string | null) => void;

  // State
  loading: LoadingState;
  error: string | null;
}

// ============================================================================
// Context
// ============================================================================

const GameContext = createContext<GameContextValue | null>(null);

// ============================================================================
// Provider Component
// ============================================================================

export function GameProvider({ children }: PropsWithChildren) {
  const [games, setGames] = useState<Game[]>([]);
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);
  const [agents, setAgents] = useState<Agent[]>([]);
  const [selectedAgentId, setSelectedAgentId] = useState<string | null>(null);
  const [loading, setLoading] = useState<LoadingState>({
    games: false,
    agents: false,
  });
  const [error, setError] = useState<string | null>(null);

  // Derived values
  const selectedGame = useMemo(
    () => (Array.isArray(games) ? games.find((g) => g.id === selectedGameId) : null) ?? null,
    [games, selectedGameId]
  );

  const selectedAgent = useMemo(
    () => (Array.isArray(agents) ? agents.find((a) => a.id === selectedAgentId) : null) ?? null,
    [agents, selectedAgentId]
  );

  // Load games on mount
  useEffect(() => {
    void loadGames();
  }, []);

  // Load agents when game changes
  useEffect(() => {
    if (selectedGameId) {
      void loadAgents(selectedGameId);
    } else {
      setAgents([]);
      setSelectedAgentId(null);
    }
  }, [selectedGameId]);

  const loadGames = useCallback(async () => {
    setLoading((prev) => ({ ...prev, games: true }));
    setError(null);
    try {
      const gamesList = await api.get<Game[]>('/api/v1/games');
      setGames(gamesList ?? []);

      // Auto-select first game if available and no game selected
      if (gamesList && gamesList.length > 0 && !selectedGameId) {
        setSelectedGameId(gamesList[0].id);
      }
    } catch (err) {
      console.error('Failed to load games:', err);
      setError('Failed to load games');
      setGames([]);
    } finally {
      setLoading((prev) => ({ ...prev, games: false }));
    }
  }, [selectedGameId]);

  const loadAgents = useCallback(async (gameId: string) => {
    setLoading((prev) => ({ ...prev, agents: true }));
    setError(null);
    try {
      const agentsList = await api.get<Agent[]>(`/api/v1/games/${gameId}/agents`);
      setAgents(agentsList ?? []);

      // Auto-select first agent if available
      if (agentsList && agentsList.length > 0) {
        setSelectedAgentId(agentsList[0].id);
      } else {
        setSelectedAgentId(null);
      }
    } catch (err) {
      console.error('Failed to load agents:', err);
      setError('Failed to load agents');
      setAgents([]);
      setSelectedAgentId(null);
    } finally {
      setLoading((prev) => ({ ...prev, agents: false }));
    }
  }, []);

  const selectGame = useCallback(async (gameId: string) => {
    setSelectedGameId(gameId);
    // Agents will load via useEffect
  }, []);

  const selectAgent = useCallback((agentId: string | null) => {
    setSelectedAgentId(agentId);
  }, []);

  const createGame = useCallback(async (name: string): Promise<Game> => {
    setError(null);
    try {
      const newGame = await api.post<Game>('/api/v1/games', { name });
      setGames((prev) => [...prev, newGame]);
      setSelectedGameId(newGame.id);
      return newGame;
    } catch (err) {
      console.error('Failed to create game:', err);
      setError('Failed to create game');
      throw err;
    }
  }, []);

  const refreshGames = useCallback(async () => {
    await loadGames();
  }, [loadGames]);

  const value = useMemo<GameContextValue>(
    () => ({
      games,
      selectedGame,
      selectedGameId,
      selectGame,
      createGame,
      refreshGames,
      agents,
      selectedAgent,
      selectedAgentId,
      selectAgent,
      loading,
      error,
    }),
    [
      games,
      selectedGame,
      selectedGameId,
      selectGame,
      createGame,
      refreshGames,
      agents,
      selectedAgent,
      selectedAgentId,
      selectAgent,
      loading,
      error,
    ]
  );

  return <GameContext.Provider value={value}>{children}</GameContext.Provider>;
}

// ============================================================================
// Hook
// ============================================================================

/**
 * Hook to access game context
 * Throws error if used outside GameProvider
 */
export function useGame(): GameContextValue {
  const context = useContext(GameContext);
  if (!context) {
    throw new Error('useGame must be used within GameProvider');
  }
  return context;
}
