/**
 * Session Store (Issue #3163)
 *
 * Zustand store for Generic Toolkit session lifecycle and real-time state.
 * Manages session creation, joining, score updates, and finalization.
 *
 * Features:
 * - Session lifecycle: create, join, pause, resume, finalize
 * - Real-time score updates via SSE
 * - Optimistic UI for score submissions
 * - Error handling and loading states
 */

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

import type { Session, Participant, ScoreEntry, ScoreboardData } from '@/components/session/types';

/**
 * Create session request
 */
export interface CreateSessionRequest {
  participants: Array<{
    displayName: string;
  }>;
  sessionDate?: Date;
  location?: string;
}

/**
 * Update score request
 */
export interface UpdateScoreRequest {
  participantId: string;
  roundNumber?: number | null;
  category?: string | null;
  scoreValue: number;
}

/**
 * Finalize session request
 */
export interface FinalizeSessionRequest {
  ranks: Record<string, number>; // participantId -> rank
}

/**
 * Session Store State
 */
export interface SessionStore {
  // State
  activeSession: Session | null;
  scoreboard: ScoreboardData | null;
  participants: Participant[];
  isLoading: boolean;
  error: string | null;

  // Actions
  createSession: (request: CreateSessionRequest) => Promise<void>;
  joinSession: (code: string) => Promise<void>;
  loadSession: (sessionId: string) => Promise<void>;
  updateScore: (request: UpdateScoreRequest) => Promise<void>;
  pauseSession: () => Promise<void>;
  resumeSession: () => Promise<void>;
  finalizeSession: (request: FinalizeSessionRequest) => Promise<void>;

  // SSE Integration
  addScoreFromSSE: (scoreEntry: ScoreEntry) => void;

  // Reset
  reset: () => void;
}

/**
 * Initial state
 */
const initialState = {
  activeSession: null,
  scoreboard: null,
  participants: [],
  isLoading: false,
  error: null,
};

/**
 * Session Store with devtools
 */
export const useSessionStore = create<SessionStore>()(
  devtools(
    (set, get) => ({
      ...initialState,

      // ========== Create Session ==========
      createSession: async (request: CreateSessionRequest) => {
        set({ isLoading: true, error: null }, false, 'createSession/start');

        try {
          const baseUrl = process.env.NEXT_PUBLIC_API_BASE || '';
          const response = await fetch(`${baseUrl}/api/v1/sessions`, {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
            },
            credentials: 'include',
            body: JSON.stringify(request),
          });

          if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
          }

          const session: Session = await response.json();

          set(
            {
              activeSession: session,
              isLoading: false,
            },
            false,
            'createSession/success'
          );
        } catch (err) {
          const errorMessage = err instanceof Error ? err.message : 'Failed to create session';
          set({ error: errorMessage, isLoading: false }, false, 'createSession/error');
          throw err;
        }
      },

      // ========== Join Session ==========
      joinSession: async (code: string) => {
        set({ isLoading: true, error: null }, false, 'joinSession/start');

        try {
          const baseUrl = process.env.NEXT_PUBLIC_API_BASE || '';
          const response = await fetch(`${baseUrl}/api/v1/sessions/join/${code}`, {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
            },
            credentials: 'include',
          });

          if (!response.ok) {
            if (response.status === 404) {
              throw new Error('Session not found. Please check the code.');
            }
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
          }

          const session: Session = await response.json();

          set(
            {
              activeSession: session,
              isLoading: false,
            },
            false,
            'joinSession/success'
          );
        } catch (err) {
          const errorMessage = err instanceof Error ? err.message : 'Failed to join session';
          set({ error: errorMessage, isLoading: false }, false, 'joinSession/error');
          throw err;
        }
      },

      // ========== Load Session Details ==========
      loadSession: async (sessionId: string) => {
        set({ isLoading: true, error: null }, false, 'loadSession/start');

        try {
          const baseUrl = process.env.NEXT_PUBLIC_API_BASE || '';
          const response = await fetch(`${baseUrl}/api/v1/sessions/${sessionId}/details`, {
            method: 'GET',
            headers: {
              'Content-Type': 'application/json',
            },
            credentials: 'include',
          });

          if (!response.ok) {
            if (response.status === 404) {
              throw new Error('Session not found');
            }
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
          }

          const data: {
            session: Session;
            participants: Participant[];
            scoreboard: ScoreboardData;
          } = await response.json();

          set(
            {
              activeSession: data.session,
              participants: data.participants,
              scoreboard: data.scoreboard,
              isLoading: false,
            },
            false,
            'loadSession/success'
          );
        } catch (err) {
          const errorMessage = err instanceof Error ? err.message : 'Failed to load session';
          set({ error: errorMessage, isLoading: false }, false, 'loadSession/error');
          throw err;
        }
      },

      // ========== Update Score (Optimistic UI) ==========
      updateScore: async (request: UpdateScoreRequest) => {
        const { activeSession, scoreboard } = get();
        if (!activeSession) {
          throw new Error('No active session');
        }

        // Optimistic update: Create temporary score entry
        const optimisticScore: ScoreEntry = {
          id: `temp-${Date.now()}`,
          participantId: request.participantId,
          roundNumber: request.roundNumber ?? null,
          category: request.category ?? null,
          scoreValue: request.scoreValue,
          timestamp: new Date(),
          createdBy: 'current-user', // Will be replaced by backend
        };

        // Add optimistic score
        set(
          state => ({
            scoreboard: state.scoreboard
              ? {
                  ...state.scoreboard,
                  scores: [...state.scoreboard.scores, optimisticScore],
                }
              : null,
          }),
          false,
          'updateScore/optimistic'
        );

        try {
          const baseUrl = process.env.NEXT_PUBLIC_API_BASE || '';
          const response = await fetch(`${baseUrl}/api/v1/sessions/${activeSession.id}/scores`, {
            method: 'PUT',
            headers: {
              'Content-Type': 'application/json',
            },
            credentials: 'include',
            body: JSON.stringify(request),
          });

          if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
          }

          const actualScore: ScoreEntry = await response.json();

          // Replace optimistic score with actual score
          set(
            state => ({
              scoreboard: state.scoreboard
                ? {
                    ...state.scoreboard,
                    scores: state.scoreboard.scores.map(s =>
                      s.id === optimisticScore.id
                        ? { ...actualScore, timestamp: new Date(actualScore.timestamp) }
                        : s
                    ),
                  }
                : null,
            }),
            false,
            'updateScore/success'
          );
        } catch (err) {
          // Revert optimistic update on error
          set(
            state => ({
              scoreboard: state.scoreboard
                ? {
                    ...state.scoreboard,
                    scores: state.scoreboard.scores.filter(s => s.id !== optimisticScore.id),
                  }
                : null,
              error: err instanceof Error ? err.message : 'Failed to update score',
            }),
            false,
            'updateScore/error'
          );
          throw err;
        }
      },

      // ========== Pause Session ==========
      pauseSession: async () => {
        const { activeSession } = get();
        if (!activeSession) {
          throw new Error('No active session');
        }

        set({ isLoading: true, error: null }, false, 'pauseSession/start');

        try {
          const baseUrl = process.env.NEXT_PUBLIC_API_BASE || '';
          const response = await fetch(`${baseUrl}/api/v1/sessions/${activeSession.id}/pause`, {
            method: 'PUT',
            credentials: 'include',
          });

          if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
          }

          const updatedSession: Session = await response.json();

          set(
            {
              activeSession: updatedSession,
              isLoading: false,
            },
            false,
            'pauseSession/success'
          );
        } catch (err) {
          const errorMessage = err instanceof Error ? err.message : 'Failed to pause session';
          set({ error: errorMessage, isLoading: false }, false, 'pauseSession/error');
          throw err;
        }
      },

      // ========== Resume Session ==========
      resumeSession: async () => {
        const { activeSession } = get();
        if (!activeSession) {
          throw new Error('No active session');
        }

        set({ isLoading: true, error: null }, false, 'resumeSession/start');

        try {
          const baseUrl = process.env.NEXT_PUBLIC_API_BASE || '';
          const response = await fetch(`${baseUrl}/api/v1/sessions/${activeSession.id}/resume`, {
            method: 'PUT',
            credentials: 'include',
          });

          if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
          }

          const updatedSession: Session = await response.json();

          set(
            {
              activeSession: updatedSession,
              isLoading: false,
            },
            false,
            'resumeSession/success'
          );
        } catch (err) {
          const errorMessage = err instanceof Error ? err.message : 'Failed to resume session';
          set({ error: errorMessage, isLoading: false }, false, 'resumeSession/error');
          throw err;
        }
      },

      // ========== Finalize Session ==========
      finalizeSession: async (request: FinalizeSessionRequest) => {
        const { activeSession } = get();
        if (!activeSession) {
          throw new Error('No active session');
        }

        set({ isLoading: true, error: null }, false, 'finalizeSession/start');

        try {
          const baseUrl = process.env.NEXT_PUBLIC_API_BASE || '';
          const response = await fetch(
            `${baseUrl}/api/v1/sessions/${activeSession.id}/finalize`,
            {
              method: 'PUT',
              headers: {
                'Content-Type': 'application/json',
              },
              credentials: 'include',
              body: JSON.stringify(request),
            }
          );

          if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
          }

          const updatedSession: Session = await response.json();

          set(
            {
              activeSession: updatedSession,
              isLoading: false,
            },
            false,
            'finalizeSession/success'
          );
        } catch (err) {
          const errorMessage = err instanceof Error ? err.message : 'Failed to finalize session';
          set({ error: errorMessage, isLoading: false }, false, 'finalizeSession/error');
          throw err;
        }
      },

      // ========== SSE Integration ==========
      addScoreFromSSE: (scoreEntry: ScoreEntry) => {
        set(
          state => ({
            scoreboard: state.scoreboard
              ? {
                  ...state.scoreboard,
                  scores: [...state.scoreboard.scores, scoreEntry],
                }
              : null,
          }),
          false,
          'addScoreFromSSE'
        );
      },

      // ========== Reset ==========
      reset: () => {
        set(initialState, false, 'reset');
      },
    }),
    {
      name: 'SessionStore',
    }
  )
);
