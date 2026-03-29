import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

import type { GameNightSummary, GameNightPlayer, GameNightGame, TimelineSlot } from './types';

interface GameNightState {
  gameNights: GameNightSummary[];
  selectedId: string | null;
  isLoading: boolean;
  error: string | null;
  players: GameNightPlayer[];
  selectedGames: GameNightGame[];
  timeline: TimelineSlot[];

  setGameNights: (nights: GameNightSummary[]) => void;
  selectGameNight: (id: string | null) => void;
  setLoading: (loading: boolean) => void;
  setError: (error: string | null) => void;
  addPlayer: (player: GameNightPlayer) => void;
  removePlayer: (playerId: string) => void;
  addGame: (game: GameNightGame) => void;
  removeGame: (gameId: string) => void;
  setTimeline: (timeline: TimelineSlot[]) => void;
  reset: () => void;
}

const initialState = {
  gameNights: [] as GameNightSummary[],
  selectedId: null as string | null,
  isLoading: false,
  error: null as string | null,
  players: [] as GameNightPlayer[],
  selectedGames: [] as GameNightGame[],
  timeline: [] as TimelineSlot[],
};

export const useGameNightStore = create<GameNightState>()(
  devtools(
    immer(set => ({
      ...initialState,
      setGameNights: nights =>
        set(s => {
          s.gameNights = nights;
        }),
      selectGameNight: id =>
        set(s => {
          s.selectedId = id;
        }),
      setLoading: loading =>
        set(s => {
          s.isLoading = loading;
        }),
      setError: error =>
        set(s => {
          s.error = error;
        }),
      addPlayer: player =>
        set(s => {
          if (!s.players.find(p => p.id === player.id)) {
            s.players.push(player);
          }
        }),
      removePlayer: playerId =>
        set(s => {
          s.players = s.players.filter(p => p.id !== playerId);
        }),
      addGame: game =>
        set(s => {
          if (!s.selectedGames.find(g => g.id === game.id)) {
            s.selectedGames.push(game);
          }
        }),
      removeGame: gameId =>
        set(s => {
          s.selectedGames = s.selectedGames.filter(g => g.id !== gameId);
        }),
      setTimeline: timeline =>
        set(s => {
          s.timeline = timeline;
        }),
      reset: () => set(() => ({ ...initialState })),
    })),
    { name: 'game-night-store' }
  )
);

// Selectors
export const selectGameNights = (s: GameNightState) => s.gameNights;
export const selectSelectedId = (s: GameNightState) => s.selectedId;
export const selectPlayers = (s: GameNightState) => s.players;
export const selectSelectedGames = (s: GameNightState) => s.selectedGames;
export const selectTimeline = (s: GameNightState) => s.timeline;
export const selectIsLoading = (s: GameNightState) => s.isLoading;
export const selectPlayerCount = (s: GameNightState) => s.players.length;
