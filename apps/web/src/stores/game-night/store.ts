import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

import type { SessionParticipant } from '@/stores/session/types';

import type {
  GameNightSummary,
  GameNightGame,
  TimelineSlot,
  GameNightActiveSession,
  DiaryEntry,
  PlayerResource,
} from './types';

interface GameNightState {
  gameNights: GameNightSummary[];
  selectedId: string | null;
  isLoading: boolean;
  error: string | null;
  players: SessionParticipant[];
  selectedGames: GameNightGame[];
  timeline: TimelineSlot[];
  activeSessions: GameNightActiveSession[];
  diary: DiaryEntry[];
  playerResources: PlayerResource[];

  setGameNights: (nights: GameNightSummary[]) => void;
  selectGameNight: (id: string | null) => void;
  setLoading: (loading: boolean) => void;
  setError: (error: string | null) => void;
  addPlayer: (player: SessionParticipant) => void;
  removePlayer: (playerId: string) => void;
  addGame: (game: GameNightGame) => void;
  removeGame: (gameId: string) => void;
  setTimeline: (timeline: TimelineSlot[]) => void;
  setActiveSessions: (sessions: GameNightActiveSession[]) => void;
  addDiaryEntry: (entry: DiaryEntry) => void;
  setDiary: (entries: DiaryEntry[]) => void;
  setPlayerResources: (resources: PlayerResource[]) => void;
  updatePlayerResource: (participantId: string, key: string, value: number) => void;
  reset: () => void;
}

const initialState = {
  gameNights: [] as GameNightSummary[],
  selectedId: null as string | null,
  isLoading: false,
  error: null as string | null,
  players: [] as SessionParticipant[],
  selectedGames: [] as GameNightGame[],
  timeline: [] as TimelineSlot[],
  activeSessions: [] as GameNightActiveSession[],
  diary: [] as DiaryEntry[],
  playerResources: [] as PlayerResource[],
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
      setActiveSessions: sessions =>
        set(s => {
          s.activeSessions = sessions;
        }),
      addDiaryEntry: entry =>
        set(s => {
          if (!s.diary.some(e => e.id === entry.id)) {
            s.diary.push(entry);
          }
        }),
      setDiary: entries =>
        set(s => {
          s.diary = entries;
        }),
      setPlayerResources: resources =>
        set(s => {
          s.playerResources = resources;
        }),
      updatePlayerResource: (participantId, key, value) =>
        set(s => {
          const pr = s.playerResources.find(r => r.participantId === participantId);
          if (pr) pr.resources[key] = value;
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
export const selectActiveSessions = (s: GameNightState) => s.activeSessions;
export const selectDiary = (s: GameNightState) => s.diary;
export const selectPlayerResources = (s: GameNightState) => s.playerResources;
export const selectCurrentActiveSession = (s: GameNightState) =>
  s.activeSessions.find(sess => sess.status === 'in_progress') ?? null;
