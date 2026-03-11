import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

import type { SessionStatus, ActivityEvent, PlayerScore } from './types';

interface SessionState {
  status: SessionStatus;
  sessionId: string | null;
  gameId: string | null;
  isPaused: boolean;
  currentTurn: number;
  events: ActivityEvent[];
  scores: PlayerScore[];
  timerStartedAt: string | null;

  startSession: (sessionId: string, gameId: string) => void;
  endSession: () => void;
  togglePause: () => void;
  addEvent: (event: ActivityEvent) => void;
  updateScore: (playerId: string, score: number) => void;
  nextTurn: () => void;
  reset: () => void;
}

const initialState = {
  status: 'idle' as SessionStatus,
  sessionId: null as string | null,
  gameId: null as string | null,
  isPaused: false,
  currentTurn: 1,
  events: [] as ActivityEvent[],
  scores: [] as PlayerScore[],
  timerStartedAt: null as string | null,
};

export const useSessionStore = create<SessionState>()(
  devtools(
    immer(set => ({
      ...initialState,

      startSession: (sessionId, gameId) =>
        set(s => {
          s.status = 'live';
          s.sessionId = sessionId;
          s.gameId = gameId;
          s.timerStartedAt = new Date().toISOString();
        }),

      endSession: () =>
        set(s => {
          s.status = 'ended';
        }),

      togglePause: () =>
        set(s => {
          s.isPaused = !s.isPaused;
          s.status = s.isPaused ? 'paused' : 'live';
        }),

      addEvent: event =>
        set(s => {
          s.events.push(event);
        }),

      updateScore: (playerId, score) =>
        set(s => {
          const existing = s.scores.find(sc => sc.playerId === playerId);
          if (existing) {
            existing.score = score;
          } else {
            s.scores.push({ playerId, score });
          }
        }),

      nextTurn: () =>
        set(s => {
          s.currentTurn += 1;
        }),

      reset: () => set(() => ({ ...initialState })),
    })),
    { name: 'session-store' }
  )
);

// Selectors
export const selectStatus = (s: SessionState) => s.status;
export const selectEvents = (s: SessionState) => s.events;
export const selectScores = (s: SessionState) => s.scores;
export const selectCurrentTurn = (s: SessionState) => s.currentTurn;
export const selectIsPaused = (s: SessionState) => s.isPaused;
export const selectIsLive = (s: SessionState) => s.status === 'live' || s.status === 'paused';
