import { create } from 'zustand';
import { devtools, persist } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

import type {
  SessionStatus,
  ActivityEvent,
  PlayerScore,
  SessionParticipant,
  StartSessionPayload,
} from './types';

interface SessionState {
  status: SessionStatus;
  sessionId: string | null;
  gameId: string | null;
  gameTitle: string | null;
  participants: SessionParticipant[];
  isPaused: boolean;
  currentTurn: number;
  events: ActivityEvent[];
  scores: PlayerScore[];
  timerStartedAt: string | null;

  startSession: (payload: StartSessionPayload) => void;
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
  gameTitle: null as string | null,
  participants: [] as SessionParticipant[],
  isPaused: false,
  currentTurn: 1,
  events: [] as ActivityEvent[],
  scores: [] as PlayerScore[],
  timerStartedAt: null as string | null,
};

export const useSessionStore = create<SessionState>()(
  devtools(
    persist(
      immer(set => ({
        ...initialState,

        startSession: payload =>
          set(s => {
            s.status = 'live';
            s.sessionId = payload.sessionId;
            s.gameId = payload.gameId;
            s.gameTitle = payload.gameTitle;
            s.participants = payload.participants;
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
      {
        name: 'meepleai-session',
        partialize: s => ({
          sessionId: s.sessionId,
          gameId: s.gameId,
          gameTitle: s.gameTitle,
          participants: s.participants,
          status: s.status,
          currentTurn: s.currentTurn,
          scores: s.scores,
        }),
      }
    ),
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
export const selectParticipants = (s: SessionState) => s.participants;
export const selectGameTitle = (s: SessionState) => s.gameTitle;
