/**
 * Session Store — Rewritten for Sessions Redesign (Issue #5041)
 *
 * Zustand store for LiveGameSession lifecycle and real-time state.
 * Uses liveSessionsClient + sessionTrackingClient instead of raw fetch.
 *
 * Features:
 * - Session lifecycle: create, join, pause, resume, complete
 * - Real-time score updates via SSE
 * - Optimistic UI for score submissions
 * - Player management
 * - Active tool tracking
 */

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

import type { Participant, ScoreEntry, ScoreboardData } from '@/components/session/types';
import { api } from '@/lib/api';
import type {
  LiveSessionDto,
  LiveSessionSummaryDto,
  LiveSessionRoundScoreDto,
  LiveSessionStatus,
  CreateLiveSessionRequest,
  AddPlayerRequest,
  RecordScoreRequest,
} from '@/lib/api/schemas/live-sessions.schemas';

/**
 * Active tool identifier. Base tools have fixed IDs; custom toolkit tools use their name.
 * Issue #4973: Session Tool Rail.
 */
export type ToolId = 'scoreboard' | 'turn-order' | 'dice' | 'whiteboard' | string;

/**
 * Session Store State
 */
export interface SessionStore {
  // State
  activeSession: LiveSessionDto | null;
  activeSessions: LiveSessionSummaryDto[];
  scores: LiveSessionRoundScoreDto[];
  isLoading: boolean;
  error: string | null;

  /** Currently active tool in the session Tool Rail (Issue #4974) */
  activeTool: ToolId;

  // Legacy compat (used by toolkit pages)
  /** @deprecated Use activeSession.players instead */
  scoreboard: ScoreboardData | null;
  /** @deprecated Use activeSession.players instead */
  participants: Participant[];
  /** @deprecated Use completeSession instead */
  finalizeSession: (request: { ranks: Record<string, number> }) => Promise<void>;
  /** @deprecated Use handleScoreUpdate instead */
  addScoreFromSSE: (scoreEntry: ScoreEntry) => void;
  /** @deprecated Use recordScore instead */
  updateScore: (request: {
    participantId: string;
    roundNumber?: number | null;
    category?: string | null;
    scoreValue: number;
  }) => Promise<void>;

  // Actions — Session Lifecycle
  createSession: (request: CreateLiveSessionRequest) => Promise<string>;
  joinSession: (code: string) => Promise<LiveSessionDto>;
  loadSession: (sessionId: string) => Promise<void>;
  loadActiveSessions: () => Promise<void>;
  startSession: () => Promise<void>;
  pauseSession: () => Promise<void>;
  resumeSession: () => Promise<void>;
  completeSession: () => Promise<void>;

  // Actions — Players
  addPlayer: (request: AddPlayerRequest) => Promise<string>;
  removePlayer: (playerId: string) => Promise<void>;

  // Actions — Scoring
  recordScore: (request: RecordScoreRequest) => Promise<void>;
  loadScores: () => Promise<void>;

  /** Set the active tool in the Tool Rail */
  setActiveTool: (tool: ToolId) => void;

  // SSE Integration
  handleScoreUpdate: (score: LiveSessionRoundScoreDto) => void;
  handleSessionUpdate: (session: LiveSessionDto) => void;

  // Reset
  reset: () => void;
}

const initialState = {
  activeSession: null as LiveSessionDto | null,
  activeSessions: [] as LiveSessionSummaryDto[],
  scores: [] as LiveSessionRoundScoreDto[],
  isLoading: false,
  error: null as string | null,
  activeTool: 'scoreboard' as ToolId,
  // Legacy compat
  scoreboard: null as ScoreboardData | null,
  participants: [] as Participant[],
};

export const useSessionStore = create<SessionStore>()(
  devtools(
    (set, get) => ({
      ...initialState,

      // ========== Create Session ==========
      createSession: async (request: CreateLiveSessionRequest) => {
        set({ isLoading: true, error: null }, false, 'createSession/start');
        try {
          const sessionId = await api.liveSessions.createSession(request);
          // Load the full session after creation
          const session = await api.liveSessions.getSession(sessionId);
          set({ activeSession: session, isLoading: false }, false, 'createSession/success');
          return sessionId;
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to create session';
          set({ error: msg, isLoading: false }, false, 'createSession/error');
          throw err;
        }
      },

      // ========== Join Session ==========
      joinSession: async (code: string) => {
        set({ isLoading: true, error: null }, false, 'joinSession/start');
        try {
          const session = await api.liveSessions.getByCode(code);
          set({ activeSession: session, isLoading: false }, false, 'joinSession/success');
          return session;
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to join session';
          set({ error: msg, isLoading: false }, false, 'joinSession/error');
          throw err;
        }
      },

      // ========== Load Session ==========
      loadSession: async (sessionId: string) => {
        set({ isLoading: true, error: null }, false, 'loadSession/start');
        try {
          const session = await api.liveSessions.getSession(sessionId);
          set(
            {
              activeSession: session,
              scores: session.roundScores,
              isLoading: false,
            },
            false,
            'loadSession/success'
          );
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to load session';
          set({ error: msg, isLoading: false }, false, 'loadSession/error');
          throw err;
        }
      },

      // ========== Load Active Sessions ==========
      loadActiveSessions: async () => {
        set({ isLoading: true, error: null }, false, 'loadActiveSessions/start');
        try {
          const sessions = await api.liveSessions.getActive();
          set({ activeSessions: sessions, isLoading: false }, false, 'loadActiveSessions/success');
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to load sessions';
          set({ error: msg, isLoading: false }, false, 'loadActiveSessions/error');
          throw err;
        }
      },

      // ========== Start Session ==========
      startSession: async () => {
        const { activeSession } = get();
        if (!activeSession) throw new Error('No active session');

        set({ isLoading: true, error: null }, false, 'startSession/start');
        try {
          await api.liveSessions.startSession(activeSession.id);
          const updated = await api.liveSessions.getSession(activeSession.id);
          set({ activeSession: updated, isLoading: false }, false, 'startSession/success');
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to start session';
          set({ error: msg, isLoading: false }, false, 'startSession/error');
          throw err;
        }
      },

      // ========== Pause Session ==========
      pauseSession: async () => {
        const { activeSession } = get();
        if (!activeSession) throw new Error('No active session');

        set({ isLoading: true, error: null }, false, 'pauseSession/start');
        try {
          await api.liveSessions.pauseSession(activeSession.id);
          set(
            state => ({
              activeSession: state.activeSession
                ? { ...state.activeSession, status: 'Paused' as LiveSessionStatus }
                : null,
              isLoading: false,
            }),
            false,
            'pauseSession/success'
          );
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to pause session';
          set({ error: msg, isLoading: false }, false, 'pauseSession/error');
          throw err;
        }
      },

      // ========== Resume Session ==========
      resumeSession: async () => {
        const { activeSession } = get();
        if (!activeSession) throw new Error('No active session');

        set({ isLoading: true, error: null }, false, 'resumeSession/start');
        try {
          await api.liveSessions.resumeSession(activeSession.id);
          set(
            state => ({
              activeSession: state.activeSession
                ? { ...state.activeSession, status: 'InProgress' as LiveSessionStatus }
                : null,
              isLoading: false,
            }),
            false,
            'resumeSession/success'
          );
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to resume session';
          set({ error: msg, isLoading: false }, false, 'resumeSession/error');
          throw err;
        }
      },

      // ========== Complete Session ==========
      completeSession: async () => {
        const { activeSession } = get();
        if (!activeSession) throw new Error('No active session');

        set({ isLoading: true, error: null }, false, 'completeSession/start');
        try {
          await api.liveSessions.completeSession(activeSession.id);
          set(
            state => ({
              activeSession: state.activeSession
                ? { ...state.activeSession, status: 'Completed' as LiveSessionStatus }
                : null,
              isLoading: false,
            }),
            false,
            'completeSession/success'
          );
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to complete session';
          set({ error: msg, isLoading: false }, false, 'completeSession/error');
          throw err;
        }
      },

      // ========== Add Player ==========
      addPlayer: async (request: AddPlayerRequest) => {
        const { activeSession } = get();
        if (!activeSession) throw new Error('No active session');

        try {
          const playerId = await api.liveSessions.addPlayer(activeSession.id, request);
          // Reload session to get updated player list
          const updated = await api.liveSessions.getSession(activeSession.id);
          set({ activeSession: updated }, false, 'addPlayer/success');
          return playerId;
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to add player';
          set({ error: msg }, false, 'addPlayer/error');
          throw err;
        }
      },

      // ========== Remove Player ==========
      removePlayer: async (playerId: string) => {
        const { activeSession } = get();
        if (!activeSession) throw new Error('No active session');

        try {
          await api.liveSessions.removePlayer(activeSession.id, playerId);
          set(
            state => ({
              activeSession: state.activeSession
                ? {
                    ...state.activeSession,
                    players: state.activeSession.players.filter(p => p.id !== playerId),
                  }
                : null,
            }),
            false,
            'removePlayer/success'
          );
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to remove player';
          set({ error: msg }, false, 'removePlayer/error');
          throw err;
        }
      },

      // ========== Record Score (Optimistic UI) ==========
      recordScore: async (request: RecordScoreRequest) => {
        const { activeSession } = get();
        if (!activeSession) throw new Error('No active session');

        // Optimistic update
        const optimisticScore: LiveSessionRoundScoreDto = {
          playerId: request.playerId,
          round: request.round,
          dimension: request.dimension,
          value: request.value,
          unit: request.unit ?? null,
          recordedAt: new Date().toISOString(),
        };

        set(
          state => ({
            scores: [...state.scores, optimisticScore],
          }),
          false,
          'recordScore/optimistic'
        );

        try {
          await api.liveSessions.recordScore(activeSession.id, request);
        } catch (err) {
          // Revert optimistic update
          set(
            state => ({
              scores: state.scores.filter(
                s =>
                  !(
                    s.playerId === request.playerId &&
                    s.round === request.round &&
                    s.dimension === request.dimension &&
                    s.recordedAt === optimisticScore.recordedAt
                  )
              ),
              error: err instanceof Error ? err.message : 'Failed to record score',
            }),
            false,
            'recordScore/error'
          );
          throw err;
        }
      },

      // ========== Load Scores ==========
      loadScores: async () => {
        const { activeSession } = get();
        if (!activeSession) return;

        try {
          const scores = await api.liveSessions.getScores(activeSession.id);
          set({ scores }, false, 'loadScores/success');
        } catch (err) {
          const msg = err instanceof Error ? err.message : 'Failed to load scores';
          set({ error: msg }, false, 'loadScores/error');
        }
      },

      // ========== Active Tool ==========
      setActiveTool: (tool: ToolId) => {
        set({ activeTool: tool }, false, 'setActiveTool');
      },

      // ========== SSE Integration ==========
      handleScoreUpdate: (score: LiveSessionRoundScoreDto) => {
        set(
          state => ({
            scores: [
              ...state.scores.filter(
                s =>
                  !(
                    s.playerId === score.playerId &&
                    s.round === score.round &&
                    s.dimension === score.dimension
                  )
              ),
              score,
            ],
          }),
          false,
          'handleScoreUpdate'
        );
      },

      handleSessionUpdate: (session: LiveSessionDto) => {
        set({ activeSession: session }, false, 'handleSessionUpdate');
      },

      // ========== Legacy Compat ==========
      finalizeSession: async () => {
        const { activeSession } = get();
        if (!activeSession) throw new Error('No active session');
        await api.liveSessions.completeSession(activeSession.id);
        set(
          state => ({
            activeSession: state.activeSession
              ? { ...state.activeSession, status: 'Completed' as LiveSessionStatus }
              : null,
          }),
          false,
          'finalizeSession/compat'
        );
      },

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
          'addScoreFromSSE/compat'
        );
      },

      // Legacy updateScore compat
      updateScore: async (request: {
        participantId: string;
        roundNumber?: number | null;
        category?: string | null;
        scoreValue: number;
      }) => {
        const { activeSession } = get();
        if (!activeSession) throw new Error('No active session');
        await api.sessionTracking.updateScore(activeSession.id, {
          participantId: request.participantId,
          roundNumber: request.roundNumber ?? null,
          category: request.category ?? null,
          scoreValue: request.scoreValue,
        });
      },

      // ========== Reset ==========
      reset: () => {
        set(initialState, false, 'reset');
      },
    }),
    { name: 'SessionStore' }
  )
);
