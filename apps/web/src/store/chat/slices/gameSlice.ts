/**
 * Game Slice (Issue #1083)
 *
 * Manages game catalog state:
 * - Games list
 * - Agents per game
 * - Loading from API
 *
 * Dependencies: UI slice (for loading states)
 */

import { StateCreator } from 'zustand';
import { ChatStore, GameSlice } from '../types';
import { api } from '@/lib/api';

export const createGameSlice: StateCreator<
  ChatStore,
  [['zustand/immer', never], ['zustand/subscribeWithSelector', never]],
  [],
  GameSlice
> = (set, get) => ({
  // ============================================================================
  // State
  // ============================================================================
  games: [],
  agents: [],

  // ============================================================================
  // Actions
  // ============================================================================
  setGames: (games) =>
    set((state) => {
      state.games = games;
    }),

  setAgents: (agents) =>
    set((state) => {
      state.agents = agents;
    }),

  loadGames: async () => {
    const { setLoading, setError } = get();
    setLoading('games', true);
    setError(null);

    try {
      const response = await api.get<Array<{ id: string; name: string; createdAt?: string }>>('/api/v1/games');
      set((state) => {
        state.games = response ?? [];
      });
    } catch (err) {
      console.error('Failed to load games:', err);
      setError('Errore nel caricamento dei giochi');
      set((state) => {
        state.games = [];
      });
    } finally {
      setLoading('games', false);
    }
  },

  loadAgents: async () => {
    const { setLoading, setError } = get();
    setLoading('agents', true);
    setError(null);

    try {
      // Issue #868: Load available (active) agents using agentsClient
      // Agents are global and not tied to specific games
      const response = await api.agents.getAvailable();
      set((state) => {
        state.agents = response ?? [];
      });
    } catch (err) {
      console.error('Failed to load agents:', err);
      setError('Errore nel caricamento degli agenti');
      set((state) => {
        state.agents = [];
      });
    } finally {
      setLoading('agents', false);
    }
  },
});
