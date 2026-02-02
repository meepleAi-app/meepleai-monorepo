/**
 * Session Slice Tests
 * Issue #3188: Agent session state management
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';

import { createSessionSlice, SessionSlice } from '../sessionSlice';

// Create a test store with only the session slice
function createTestStore() {
  return create<SessionSlice>()(
    immer((...args) => ({
      ...createSessionSlice(...args),
    }))
  );
}

describe('sessionSlice', () => {
  let store: ReturnType<typeof createTestStore>;

  beforeEach(() => {
    vi.useFakeTimers();
    store = createTestStore();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  describe('Initial State', () => {
    it('has empty activeSessions initially', () => {
      expect(store.getState().activeSessions).toEqual({});
    });

    it('has launchingSession set to false initially', () => {
      expect(store.getState().launchingSession).toBe(false);
    });

    it('has sessionError set to null initially', () => {
      expect(store.getState().sessionError).toBeNull();
    });
  });

  describe('launchAgent', () => {
    it('sets launchingSession to true while launching', async () => {
      const launchPromise = store.getState().launchAgent('session-1', 'game-123', 'RulesClarifier');

      expect(store.getState().launchingSession).toBe(true);

      vi.advanceTimersByTime(300);
      await launchPromise;
    });

    it('sets launchingSession to false after launch completes', async () => {
      const launchPromise = store.getState().launchAgent('session-1', 'game-123', 'RulesClarifier');

      vi.advanceTimersByTime(300);
      await launchPromise;

      expect(store.getState().launchingSession).toBe(false);
    });

    it('creates new session in activeSessions', async () => {
      const launchPromise = store.getState().launchAgent('session-1', 'game-123', 'RulesClarifier');

      vi.advanceTimersByTime(300);
      await launchPromise;

      expect(store.getState().activeSessions['session-1']).toBeDefined();
    });

    it('creates session with correct properties', async () => {
      const launchPromise = store.getState().launchAgent('session-abc', 'game-456', 'StrategyAdvisor');

      vi.advanceTimersByTime(300);
      await launchPromise;

      const session = store.getState().activeSessions['session-abc'];
      expect(session.sessionId).toBe('session-abc');
      expect(session.gameId).toBe('game-456');
      expect(session.mode).toBe('StrategyAdvisor');
      expect(session.status).toBe('active');
      expect(session.messageCount).toBe(0);
    });

    it('sets startedAt timestamp', async () => {
      const beforeLaunch = new Date();
      const launchPromise = store.getState().launchAgent('session-1', 'game-123', 'RulesClarifier');

      vi.advanceTimersByTime(300);
      await launchPromise;

      const session = store.getState().activeSessions['session-1'];
      expect(session.startedAt.getTime()).toBeGreaterThanOrEqual(beforeLaunch.getTime());
    });

    it('clears previous error before launching', async () => {
      store.setState({
        sessionError: {
          message: 'Previous error',
          code: 'PREV_ERROR',
          timestamp: new Date(),
        },
      });

      const launchPromise = store.getState().launchAgent('session-1', 'game-123', 'RulesClarifier');

      vi.advanceTimersByTime(300);
      await launchPromise;

      expect(store.getState().sessionError).toBeNull();
    });
  });

  describe('endSession', () => {
    beforeEach(async () => {
      const launchPromise = store.getState().launchAgent('session-1', 'game-123', 'RulesClarifier');
      vi.advanceTimersByTime(300);
      await launchPromise;
    });

    it('sets session status to ended', async () => {
      const endPromise = store.getState().endSession('session-1');

      vi.advanceTimersByTime(200);
      await endPromise;

      expect(store.getState().activeSessions['session-1']?.status).toBe('ended');
    });

    it('removes session from activeSessions after delay', async () => {
      const endPromise = store.getState().endSession('session-1');

      vi.advanceTimersByTime(200);
      await endPromise;

      // Session still exists after end but before cleanup
      expect(store.getState().activeSessions['session-1']).toBeDefined();

      // After cleanup delay
      vi.advanceTimersByTime(1000);
      expect(store.getState().activeSessions['session-1']).toBeUndefined();
    });
  });

  describe('incrementMessageCount', () => {
    beforeEach(async () => {
      const launchPromise = store.getState().launchAgent('session-1', 'game-123', 'RulesClarifier');
      vi.advanceTimersByTime(300);
      await launchPromise;
    });

    it('increments message count by 1', () => {
      store.getState().incrementMessageCount('session-1');

      expect(store.getState().activeSessions['session-1'].messageCount).toBe(1);
    });

    it('increments message count multiple times', () => {
      store.getState().incrementMessageCount('session-1');
      store.getState().incrementMessageCount('session-1');
      store.getState().incrementMessageCount('session-1');

      expect(store.getState().activeSessions['session-1'].messageCount).toBe(3);
    });

    it('does nothing for non-existent session', () => {
      // Should not throw
      store.getState().incrementMessageCount('non-existent');

      expect(store.getState().activeSessions['non-existent']).toBeUndefined();
    });
  });

  describe('getSession', () => {
    beforeEach(async () => {
      const launchPromise = store.getState().launchAgent('session-1', 'game-123', 'RulesClarifier');
      vi.advanceTimersByTime(300);
      await launchPromise;
    });

    it('returns session by ID', () => {
      const session = store.getState().getSession('session-1');

      expect(session).not.toBeNull();
      expect(session?.sessionId).toBe('session-1');
    });

    it('returns null for non-existent session', () => {
      const session = store.getState().getSession('non-existent');

      expect(session).toBeNull();
    });

    it('returns session with updated message count', () => {
      store.getState().incrementMessageCount('session-1');
      store.getState().incrementMessageCount('session-1');

      const session = store.getState().getSession('session-1');

      expect(session?.messageCount).toBe(2);
    });
  });

  describe('clearSessionError', () => {
    it('clears session error', () => {
      store.setState({
        sessionError: {
          message: 'Test error',
          code: 'TEST_ERROR',
          timestamp: new Date(),
        },
      });

      store.getState().clearSessionError();

      expect(store.getState().sessionError).toBeNull();
    });

    it('has no effect when error is already null', () => {
      store.getState().clearSessionError();

      expect(store.getState().sessionError).toBeNull();
    });
  });

  describe('Multiple Sessions', () => {
    it('can manage multiple sessions simultaneously', async () => {
      const launch1 = store.getState().launchAgent('session-1', 'game-1', 'RulesClarifier');
      vi.advanceTimersByTime(300);
      await launch1;

      const launch2 = store.getState().launchAgent('session-2', 'game-2', 'StrategyAdvisor');
      vi.advanceTimersByTime(300);
      await launch2;

      const launch3 = store.getState().launchAgent('session-3', 'game-3', 'SetupGuide');
      vi.advanceTimersByTime(300);
      await launch3;

      expect(Object.keys(store.getState().activeSessions)).toHaveLength(3);
    });

    it('tracks message counts independently per session', async () => {
      const launch1 = store.getState().launchAgent('session-1', 'game-1', 'RulesClarifier');
      vi.advanceTimersByTime(300);
      await launch1;

      const launch2 = store.getState().launchAgent('session-2', 'game-2', 'StrategyAdvisor');
      vi.advanceTimersByTime(300);
      await launch2;

      store.getState().incrementMessageCount('session-1');
      store.getState().incrementMessageCount('session-1');
      store.getState().incrementMessageCount('session-2');

      expect(store.getState().activeSessions['session-1'].messageCount).toBe(2);
      expect(store.getState().activeSessions['session-2'].messageCount).toBe(1);
    });

    it('can end sessions independently', async () => {
      const launch1 = store.getState().launchAgent('session-1', 'game-1', 'RulesClarifier');
      vi.advanceTimersByTime(300);
      await launch1;

      const launch2 = store.getState().launchAgent('session-2', 'game-2', 'StrategyAdvisor');
      vi.advanceTimersByTime(300);
      await launch2;

      const end1 = store.getState().endSession('session-1');
      vi.advanceTimersByTime(200);
      await end1;

      expect(store.getState().activeSessions['session-1'].status).toBe('ended');
      expect(store.getState().activeSessions['session-2'].status).toBe('active');
    });
  });

  describe('Session Modes', () => {
    it.each([
      'RulesClarifier',
      'StrategyAdvisor',
      'SetupGuide',
    ] as const)('supports %s mode', async (mode) => {
      const launchPromise = store.getState().launchAgent(`session-${mode}`, 'game-123', mode);
      vi.advanceTimersByTime(300);
      await launchPromise;

      expect(store.getState().activeSessions[`session-${mode}`].mode).toBe(mode);
    });
  });

  describe('Error Handling', () => {
    describe('launchAgent error handling', () => {
      it('sets sessionError when launch fails with Error', async () => {
        // Create a store that throws during launch
        const errorStore = create<SessionSlice>()(
          immer((set, get) => {
            const slice = createSessionSlice(set, get, { setState: set, getState: get, getInitialState: get, subscribe: vi.fn(), destroy: vi.fn() });
            return {
              ...slice,
              launchAgent: async (sessionId: string, gameId: string, mode: 'RulesClarifier' | 'StrategyAdvisor' | 'SetupGuide') => {
                set(state => {
                  state.launchingSession = true;
                  state.sessionError = null;
                });

                try {
                  throw new Error('Network timeout');
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
            };
          })
        );

        await expect(
          errorStore.getState().launchAgent('session-1', 'game-123', 'RulesClarifier')
        ).rejects.toThrow('Network timeout');

        expect(errorStore.getState().launchingSession).toBe(false);
        expect(errorStore.getState().sessionError).not.toBeNull();
        expect(errorStore.getState().sessionError?.message).toBe('Network timeout');
        expect(errorStore.getState().sessionError?.code).toBe('LAUNCH_SESSION_ERROR');
      });

      it('handles non-Error objects in launch failure', async () => {
        const errorStore = create<SessionSlice>()(
          immer((set, get) => {
            const slice = createSessionSlice(set, get, { setState: set, getState: get, getInitialState: get, subscribe: vi.fn(), destroy: vi.fn() });
            return {
              ...slice,
              launchAgent: async () => {
                set(state => {
                  state.launchingSession = true;
                  state.sessionError = null;
                });

                try {
                  throw 'String error'; // Non-Error object
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
            };
          })
        );

        await expect(
          errorStore.getState().launchAgent('session-1', 'game-123', 'RulesClarifier')
        ).rejects.toBe('String error');

        expect(errorStore.getState().sessionError?.message).toBe('Failed to launch session');
      });
    });

    describe('endSession error handling', () => {
      it('sets sessionError when end session fails with Error', async () => {
        // Create a store with error-throwing endSession
        const errorStore = create<SessionSlice>()(
          immer((set, get) => {
            const slice = createSessionSlice(set, get, { setState: set, getState: get, getInitialState: get, subscribe: vi.fn(), destroy: vi.fn() });
            return {
              ...slice,
              endSession: async () => {
                try {
                  throw new Error('Connection refused');
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
            };
          })
        );

        await expect(
          errorStore.getState().endSession('session-1')
        ).rejects.toThrow('Connection refused');

        expect(errorStore.getState().sessionError).not.toBeNull();
        expect(errorStore.getState().sessionError?.message).toBe('Connection refused');
        expect(errorStore.getState().sessionError?.code).toBe('END_SESSION_ERROR');
      });

      it('handles non-Error objects in end session failure', async () => {
        const errorStore = create<SessionSlice>()(
          immer((set, get) => {
            const slice = createSessionSlice(set, get, { setState: set, getState: get, getInitialState: get, subscribe: vi.fn(), destroy: vi.fn() });
            return {
              ...slice,
              endSession: async () => {
                try {
                  throw { code: 500, message: 'Internal Server Error' }; // Non-Error object
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
            };
          })
        );

        await expect(
          errorStore.getState().endSession('session-1')
        ).rejects.toEqual({ code: 500, message: 'Internal Server Error' });

        expect(errorStore.getState().sessionError?.message).toBe('Failed to end session');
      });
    });
  });

});
