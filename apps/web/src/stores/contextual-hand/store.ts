/**
 * Contextual Hand Store (Session Flow v2.1)
 *
 * Single source of truth for the "Contextual Hand" UI — a persistent
 * floating panel that tracks the user's current game session.
 *
 * Responsibilities:
 * - Load / recover the current Active or Paused session on mount
 * - Persist `lastActiveSessionId` in localStorage for page-reload recovery
 * - Expose actions for the full session lifecycle (create, pause, resume,
 *   turn order, advance turn, dice rolls, scores, diary)
 * - Track loading/error state for UI feedback
 *
 * Middleware Stack:
 * - devtools: Browser DevTools integration
 * - persist: localStorage for session recovery across page reloads
 * - immer: Mutable state updates
 */

import { create } from 'zustand';
import { devtools, persist } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

import { api } from '@/lib/api';

import type { ContextualHandStore, HandContext } from './types';

// ─── Constants ─────────────────────────────────────────────────────────────

const STORE_NAME = 'meepleai-contextual-hand';

// ─── Initial State ─────────────────────────────────────────────────────────

const initialState = {
  context: 'idle' as HandContext,
  currentSession: null as ContextualHandStore['currentSession'],
  createResult: null as ContextualHandStore['createResult'],
  isLoading: false,
  error: null as string | null,
  diaryEntries: [] as ContextualHandStore['diaryEntries'],
  isDiaryLoading: false,
  kbReadiness: null as ContextualHandStore['kbReadiness'],
};

// ─── Helpers ───────────────────────────────────────────────────────────────

/** Map backend session status string to HandContext. */
function statusToContext(status: string): HandContext {
  switch (status) {
    case 'Active':
      return 'active';
    case 'Paused':
      return 'paused';
    default:
      return 'idle';
  }
}

// ─── Store ─────────────────────────────────────────────────────────────────

export const useContextualHandStore = create<ContextualHandStore>()(
  devtools(
    persist(
      immer((set, get) => ({
        ...initialState,

        // ── Lifecycle ────────────────────────────────────────────────

        initialize: async () => {
          set(s => {
            s.isLoading = true;
            s.error = null;
          });
          try {
            const session = await api.sessionFlow.getCurrentSession();
            if (session) {
              set(s => {
                s.currentSession = session;
                s.context = statusToContext(session.status);
                s.isLoading = false;
              });
            } else {
              set(s => {
                s.context = 'idle';
                s.currentSession = null;
                s.isLoading = false;
              });
            }
          } catch (error) {
            set(s => {
              s.error = (error as Error).message;
              s.isLoading = false;
            });
          }
        },

        startSession: async (gameId, guestNames, gameNightEventId) => {
          set(s => {
            s.isLoading = true;
            s.error = null;
          });
          try {
            const result = await api.sessionFlow.createSession(gameId, {
              sessionType: 'GameSpecific',
              participants: [],
              guestNames,
              gameNightEventId,
            });

            set(s => {
              s.createResult = result;
              s.currentSession = {
                sessionId: result.sessionId,
                gameId,
                status: 'Active',
                sessionCode: result.sessionCode,
                sessionDate: new Date().toISOString(),
                updatedAt: null,
                gameNightEventId: result.gameNightEventId,
              };
              s.context = 'active';
              s.isLoading = false;
            });
          } catch (error) {
            set(s => {
              s.error = (error as Error).message;
              s.isLoading = false;
            });
          }
        },

        pauseSession: async () => {
          const { currentSession } = get();
          if (!currentSession) return;

          set(s => {
            s.isLoading = true;
            s.error = null;
          });
          try {
            await api.sessionFlow.pauseSession(currentSession.sessionId);
            set(s => {
              if (s.currentSession) {
                s.currentSession.status = 'Paused';
              }
              s.context = 'paused';
              s.isLoading = false;
            });
          } catch (error) {
            set(s => {
              s.error = (error as Error).message;
              s.isLoading = false;
            });
          }
        },

        resumeSession: async () => {
          const { currentSession } = get();
          if (!currentSession) return;

          set(s => {
            s.isLoading = true;
            s.error = null;
          });
          try {
            await api.sessionFlow.resumeSession(currentSession.sessionId);
            set(s => {
              if (s.currentSession) {
                s.currentSession.status = 'Active';
              }
              s.context = 'active';
              s.isLoading = false;
            });
          } catch (error) {
            set(s => {
              s.error = (error as Error).message;
              s.isLoading = false;
            });
          }
        },

        // ── Gameplay ─────────────────────────────────────────────────

        setTurnOrder: async (method, order) => {
          const { currentSession } = get();
          if (!currentSession) return null;

          try {
            const result = await api.sessionFlow.setTurnOrder(currentSession.sessionId, {
              method,
              order,
            });
            return result;
          } catch (error) {
            set(s => {
              s.error = (error as Error).message;
            });
            return null;
          }
        },

        advanceTurn: async () => {
          const { currentSession } = get();
          if (!currentSession) return null;

          try {
            return await api.sessionFlow.advanceTurn(currentSession.sessionId);
          } catch (error) {
            set(s => {
              s.error = (error as Error).message;
            });
            return null;
          }
        },

        rollDice: async (participantId, formula, label) => {
          const { currentSession } = get();
          if (!currentSession) return null;

          try {
            return await api.sessionFlow.rollSessionDice(currentSession.sessionId, {
              participantId,
              formula,
              label,
            });
          } catch (error) {
            set(s => {
              s.error = (error as Error).message;
            });
            return null;
          }
        },

        upsertScore: async (participantId, newValue, roundNumber, category, reason) => {
          const { currentSession } = get();
          if (!currentSession) return null;

          try {
            return await api.sessionFlow.upsertScore(currentSession.sessionId, {
              participantId,
              newValue,
              roundNumber,
              category,
              reason,
            });
          } catch (error) {
            set(s => {
              s.error = (error as Error).message;
            });
            return null;
          }
        },

        // ── Diary ────────────────────────────────────────────────────

        loadDiary: async eventTypes => {
          const { currentSession } = get();
          if (!currentSession) return;

          set(s => {
            s.isDiaryLoading = true;
          });
          try {
            const entries = await api.sessionFlow.getSessionDiary(currentSession.sessionId, {
              eventTypes,
            });
            set(s => {
              s.diaryEntries = entries;
              s.isDiaryLoading = false;
            });
          } catch (error) {
            set(s => {
              s.error = (error as Error).message;
              s.isDiaryLoading = false;
            });
          }
        },

        // ── KB Readiness ─────────────────────────────────────────────

        checkKbReadiness: async gameId => {
          try {
            const result = await api.sessionFlow.getKbReadiness(gameId);
            set(s => {
              s.kbReadiness = result;
            });
          } catch (error) {
            set(s => {
              s.error = (error as Error).message;
            });
          }
        },

        // ── Reset ────────────────────────────────────────────────────

        reset: () =>
          set(s => {
            Object.assign(s, initialState);
          }),
      })),
      {
        name: STORE_NAME,
        partialize: s => ({
          currentSession: s.currentSession,
          context: s.context,
        }),
      }
    ),
    { name: 'contextual-hand-store' }
  )
);

// ─── Selectors ─────────────────────────────────────────────────────────────

export const selectContext = (s: ContextualHandStore) => s.context;
export const selectCurrentSession = (s: ContextualHandStore) => s.currentSession;
export const selectIsLoading = (s: ContextualHandStore) => s.isLoading;
export const selectError = (s: ContextualHandStore) => s.error;
export const selectDiaryEntries = (s: ContextualHandStore) => s.diaryEntries;
export const selectIsDiaryLoading = (s: ContextualHandStore) => s.isDiaryLoading;
export const selectKbReadiness = (s: ContextualHandStore) => s.kbReadiness;
export const selectCreateResult = (s: ContextualHandStore) => s.createResult;
export const selectHasActiveSession = (s: ContextualHandStore) =>
  s.context === 'active' || s.context === 'paused';
export const selectSessionId = (s: ContextualHandStore) => s.currentSession?.sessionId ?? null;
