/* eslint-disable security/detect-object-injection -- Safe Zustand store key access with typed session IDs */
/**
 * Session Slice (Issue #3188)
 *
 * Manages active agent sessions (ephemeral, not persisted):
 * - Session lifecycle (launch, end)
 * - Active session tracking
 * - Message count tracking
 *
 * Dependencies: none (standalone slice)
 */

import { StateCreator } from 'zustand';

import { AgentSession, AgentStoreError } from '../types';
import { AgentMode } from '@/components/agent';
import type { AgentStore } from '../types/store.types';

export interface SessionSlice {
  // ============================================================================
  // State
  // ============================================================================

  /** Active sessions by session ID */
  activeSessions: Record<string, AgentSession>;

  /** Loading state for session operations */
  launchingSession: boolean;

  /** Last error for session operations */
  sessionError: AgentStoreError | null;

  // ============================================================================
  // Actions
  // ============================================================================

  /** Launch new agent session */
  launchAgent: (sessionId: string, gameId: string, mode: AgentMode) => Promise<void>;

  /** End active session */
  endSession: (sessionId: string) => Promise<void>;

  /** Increment message count for session */
  incrementMessageCount: (sessionId: string) => void;

  /** Get active session by ID */
  getSession: (sessionId: string) => AgentSession | null;

  /** Clear session error */
  clearSessionError: () => void;
}

export const createSessionSlice: StateCreator<
  AgentStore,
  [['zustand/immer', never]],
  [],
  SessionSlice
> = (set, get) => ({
  // ============================================================================
  // Initial State
  // ============================================================================
  activeSessions: {},
  launchingSession: false,
  sessionError: null,

  // ============================================================================
  // Actions
  // ============================================================================

  /**
   * Launch new agent session
   */
  launchAgent: async (sessionId, gameId, mode) => {
    set(state => {
      state.launchingSession = true;
      state.sessionError = null;
    });

    try {
      // TODO: Replace with actual backend API call
      // await fetch('/api/v1/agent/sessions', {
      //   method: 'POST',
      //   headers: { 'Content-Type': 'application/json' },
      //   body: JSON.stringify({ sessionId, gameId, mode }),
      // });

      // Mock delay
      await new Promise(resolve => setTimeout(resolve, 300));

      const newSession: AgentSession = {
        sessionId,
        gameId,
        mode,
        startedAt: new Date(),
        status: 'active',
        messageCount: 0,
      };

      set(state => {
        state.activeSessions[sessionId] = newSession;
        state.launchingSession = false;
      });
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to launch session';

      set(state => {
        state.launchingSession = false;
        state.sessionError = {
          message: errorMessage,
          code: 'LAUNCH_SESSION_ERROR',
          timestamp: new Date(),
        };
      });

      throw error;
    }
  },

  /**
   * End active session
   */
  endSession: async sessionId => {
    try {
      // TODO: Replace with actual backend API call
      // await fetch(`/api/v1/agent/sessions/${sessionId}`, {
      //   method: 'DELETE',
      // });

      // Mock delay
      await new Promise(resolve => setTimeout(resolve, 200));

      set(state => {
        const session = state.activeSessions[sessionId];
        if (session) {
          session.status = 'ended';
        }
      });

      // Remove from active sessions after a delay (for UI transition)
      setTimeout(() => {
        set(state => {
          delete state.activeSessions[sessionId];
        });
      }, 1000);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to end session';

      set(state => {
        state.sessionError = {
          message: errorMessage,
          code: 'END_SESSION_ERROR',
          timestamp: new Date(),
        };
      });

      throw error;
    }
  },

  /**
   * Increment message count for session
   */
  incrementMessageCount: sessionId =>
    set(state => {
      const session = state.activeSessions[sessionId];
      if (session) {
        session.messageCount++;
      }
    }),

  /**
   * Get session by ID
   */
  getSession: sessionId => {
    const state = get();
    return state.activeSessions[sessionId] || null;
  },

  /**
   * Clear session error
   */
  clearSessionError: () =>
    set(state => {
      state.sessionError = null;
    }),
});
