/**
 * Conversation Slice Tests
 * Issue #3188: Agent conversation state management
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';

import { createConversationSlice, ConversationSlice } from '../conversationSlice';

// Create a test store with only the conversation slice
function createTestStore() {
  return create<ConversationSlice>()(
    immer((...args) => ({
      ...createConversationSlice(...args),
    }))
  );
}

describe('conversationSlice', () => {
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
    it('has empty conversations initially', () => {
      expect(store.getState().conversations).toEqual({});
    });

    it('has empty conversationCache initially', () => {
      expect(store.getState().conversationCache).toEqual([]);
    });

    it('has sendingMessage set to false initially', () => {
      expect(store.getState().sendingMessage).toBe(false);
    });

    it('has loadingHistory set to false initially', () => {
      expect(store.getState().loadingHistory).toBe(false);
    });

    it('has conversationError set to null initially', () => {
      expect(store.getState().conversationError).toBeNull();
    });
  });

  describe('sendMessage', () => {
    it('sets sendingMessage to true while sending', async () => {
      const sendPromise = store.getState().sendMessage('session-1', 'Hello', 'game-123');

      expect(store.getState().sendingMessage).toBe(true);

      vi.advanceTimersByTime(1000);
      await sendPromise;
    });

    it('sets sendingMessage to false after sending completes', async () => {
      const sendPromise = store.getState().sendMessage('session-1', 'Hello', 'game-123');

      vi.advanceTimersByTime(1000);
      await sendPromise;

      expect(store.getState().sendingMessage).toBe(false);
    });

    it('adds user message optimistically before response', async () => {
      const sendPromise = store.getState().sendMessage('session-1', 'Test message', 'game-123');

      // User message should be added immediately
      expect(store.getState().conversations['session-1']).toHaveLength(1);
      expect(store.getState().conversations['session-1'][0].type).toBe('user');
      expect(store.getState().conversations['session-1'][0].content).toBe('Test message');

      vi.advanceTimersByTime(1000);
      await sendPromise;
    });

    it('adds agent response after delay', async () => {
      const sendPromise = store.getState().sendMessage('session-1', 'Hello', 'game-123');

      vi.advanceTimersByTime(1000);
      await sendPromise;

      const messages = store.getState().conversations['session-1'];
      expect(messages).toHaveLength(2);
      expect(messages[1].type).toBe('agent');
    });

    it('clears previous error before sending', async () => {
      store.setState({
        conversationError: {
          message: 'Previous error',
          code: 'PREV_ERROR',
          timestamp: new Date(),
        },
      });

      const sendPromise = store.getState().sendMessage('session-1', 'Hello', 'game-123');

      vi.advanceTimersByTime(1000);
      await sendPromise;

      expect(store.getState().conversationError).toBeNull();
    });

    it('updates cache after successful send', async () => {
      const sendPromise = store.getState().sendMessage('session-1', 'Hello', 'game-123');

      vi.advanceTimersByTime(1000);
      await sendPromise;

      expect(store.getState().conversationCache).toHaveLength(1);
      expect(store.getState().conversationCache[0].sessionId).toBe('session-1');
    });
  });

  describe('loadHistory', () => {
    it('sets loadingHistory to true while loading', async () => {
      const loadPromise = store.getState().loadHistory('session-1', 'game-123');

      expect(store.getState().loadingHistory).toBe(true);

      vi.advanceTimersByTime(500);
      await loadPromise;
    });

    it('sets loadingHistory to false after loading completes', async () => {
      const loadPromise = store.getState().loadHistory('session-1', 'game-123');

      vi.advanceTimersByTime(500);
      await loadPromise;

      expect(store.getState().loadingHistory).toBe(false);
    });

    it('stores loaded messages in conversations', async () => {
      const loadPromise = store.getState().loadHistory('session-1', 'game-123');

      vi.advanceTimersByTime(500);
      await loadPromise;

      expect(store.getState().conversations['session-1']).toBeDefined();
      expect(store.getState().conversations['session-1'].length).toBeGreaterThan(0);
    });

    it('clears previous error before loading', async () => {
      store.setState({
        conversationError: {
          message: 'Previous error',
          code: 'PREV_ERROR',
          timestamp: new Date(),
        },
      });

      const loadPromise = store.getState().loadHistory('session-1', 'game-123');

      vi.advanceTimersByTime(500);
      await loadPromise;

      expect(store.getState().conversationError).toBeNull();
    });

    it('updates cache after successful load', async () => {
      const loadPromise = store.getState().loadHistory('session-1', 'game-123');

      vi.advanceTimersByTime(500);
      await loadPromise;

      expect(store.getState().conversationCache.length).toBeGreaterThan(0);
    });
  });

  describe('addMessageLocal', () => {
    it('adds message to empty conversation', () => {
      store.getState().addMessageLocal('session-1', {
        type: 'user',
        content: 'Test message',
        timestamp: new Date(),
      });

      expect(store.getState().conversations['session-1']).toHaveLength(1);
    });

    it('adds message to existing conversation', () => {
      store.getState().addMessageLocal('session-1', {
        type: 'user',
        content: 'First message',
        timestamp: new Date(),
      });

      store.getState().addMessageLocal('session-1', {
        type: 'agent',
        content: 'Response',
        timestamp: new Date(),
      });

      expect(store.getState().conversations['session-1']).toHaveLength(2);
    });

    it('preserves message properties', () => {
      const timestamp = new Date();
      store.getState().addMessageLocal('session-1', {
        type: 'agent',
        content: 'Agent response',
        timestamp,
        citations: [{ page: 5, source: 'rulebook' }],
      });

      const message = store.getState().conversations['session-1'][0];
      expect(message.type).toBe('agent');
      expect(message.content).toBe('Agent response');
      expect(message.citations).toEqual([{ page: 5, source: 'rulebook' }]);
    });
  });

  describe('updateCache', () => {
    beforeEach(async () => {
      // Add a conversation first
      store.getState().addMessageLocal('session-1', {
        type: 'user',
        content: 'Hello',
        timestamp: new Date(),
      });
    });

    it('creates cache entry for conversation', () => {
      store.getState().updateCache('session-1', 'game-123');

      expect(store.getState().conversationCache).toHaveLength(1);
      expect(store.getState().conversationCache[0].sessionId).toBe('session-1');
      expect(store.getState().conversationCache[0].gameId).toBe('game-123');
    });

    it('does not create cache for empty conversation', () => {
      store.getState().updateCache('empty-session', 'game-123');

      expect(store.getState().conversationCache).toHaveLength(0);
    });

    it('updates existing cache entry', () => {
      store.getState().updateCache('session-1', 'game-123');

      store.getState().addMessageLocal('session-1', {
        type: 'agent',
        content: 'Response',
        timestamp: new Date(),
      });

      store.getState().updateCache('session-1', 'game-123');

      expect(store.getState().conversationCache).toHaveLength(1);
      expect(store.getState().conversationCache[0].messages).toHaveLength(2);
    });

    it('limits cache to 5 conversations', async () => {
      // Add 6 conversations
      for (let i = 1; i <= 6; i++) {
        store.getState().addMessageLocal(`session-${i}`, {
          type: 'user',
          content: `Message ${i}`,
          timestamp: new Date(),
        });
        store.getState().updateCache(`session-${i}`, `game-${i}`);
      }

      expect(store.getState().conversationCache).toHaveLength(5);
    });

    it('keeps newest conversations in cache', async () => {
      // Add 6 conversations
      for (let i = 1; i <= 6; i++) {
        store.getState().addMessageLocal(`session-${i}`, {
          type: 'user',
          content: `Message ${i}`,
          timestamp: new Date(),
        });
        store.getState().updateCache(`session-${i}`, `game-${i}`);
      }

      // session-1 should be evicted, session-6 should be first
      expect(store.getState().conversationCache[0].sessionId).toBe('session-6');
      expect(
        store.getState().conversationCache.find(c => c.sessionId === 'session-1')
      ).toBeUndefined();
    });
  });

  describe('clearConversationError', () => {
    it('clears conversation error', () => {
      store.setState({
        conversationError: {
          message: 'Test error',
          code: 'TEST_ERROR',
          timestamp: new Date(),
        },
      });

      store.getState().clearConversationError();

      expect(store.getState().conversationError).toBeNull();
    });

    it('has no effect when error is already null', () => {
      store.getState().clearConversationError();

      expect(store.getState().conversationError).toBeNull();
    });
  });

  describe('Multiple Sessions', () => {
    it('can manage multiple conversations independently', async () => {
      const send1 = store.getState().sendMessage('session-1', 'Hello 1', 'game-1');
      vi.advanceTimersByTime(1000);
      await send1;

      const send2 = store.getState().sendMessage('session-2', 'Hello 2', 'game-2');
      vi.advanceTimersByTime(1000);
      await send2;

      expect(store.getState().conversations['session-1']).toBeDefined();
      expect(store.getState().conversations['session-2']).toBeDefined();
      expect(store.getState().conversations['session-1'][0].content).toBe('Hello 1');
      expect(store.getState().conversations['session-2'][0].content).toBe('Hello 2');
    });
  });

  describe('Error Handling', () => {
    describe('sendMessage error handling', () => {
      it('sets conversationError when send fails with Error', async () => {
        const errorStore = create<ConversationSlice>()(
          immer((set, get) => {
            const slice = createConversationSlice(set, get, { setState: set, getState: get, getInitialState: get, subscribe: vi.fn(), destroy: vi.fn() });
            return {
              ...slice,
              sendMessage: async (sessionId: string, content: string, _gameId: string) => {
                set(state => {
                  state.sendingMessage = true;
                  state.conversationError = null;
                  // Optimistic update
                  if (!state.conversations[sessionId]) {
                    state.conversations[sessionId] = [];
                  }
                  state.conversations[sessionId].push({
                    type: 'user',
                    content,
                    timestamp: new Date(),
                  });
                });

                try {
                  throw new Error('Network unavailable');
                } catch (error) {
                  const errorMessage = error instanceof Error ? error.message : 'Failed to send message';

                  // Rollback optimistic update
                  set(state => {
                    const messages = state.conversations[sessionId];
                    if (messages && messages.length > 0) {
                      messages.pop();
                    }
                    state.sendingMessage = false;
                    state.conversationError = {
                      message: errorMessage,
                      code: 'SEND_MESSAGE_ERROR',
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
          errorStore.getState().sendMessage('session-1', 'Hello', 'game-123')
        ).rejects.toThrow('Network unavailable');

        expect(errorStore.getState().sendingMessage).toBe(false);
        expect(errorStore.getState().conversationError).not.toBeNull();
        expect(errorStore.getState().conversationError?.message).toBe('Network unavailable');
        expect(errorStore.getState().conversationError?.code).toBe('SEND_MESSAGE_ERROR');
      });

      it('rolls back optimistic update on send failure', async () => {
        const errorStore = create<ConversationSlice>()(
          immer((set, get) => {
            const slice = createConversationSlice(set, get, { setState: set, getState: get, getInitialState: get, subscribe: vi.fn(), destroy: vi.fn() });
            return {
              ...slice,
              sendMessage: async (sessionId: string, content: string, _gameId: string) => {
                set(state => {
                  state.sendingMessage = true;
                  if (!state.conversations[sessionId]) {
                    state.conversations[sessionId] = [];
                  }
                  state.conversations[sessionId].push({
                    type: 'user',
                    content,
                    timestamp: new Date(),
                  });
                });

                // Message added
                try {
                  throw new Error('Failed');
                } catch (error) {
                  // Rollback
                  set(state => {
                    const messages = state.conversations[sessionId];
                    if (messages && messages.length > 0) {
                      messages.pop();
                    }
                    state.sendingMessage = false;
                    state.conversationError = {
                      message: 'Failed',
                      code: 'SEND_MESSAGE_ERROR',
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
          errorStore.getState().sendMessage('session-1', 'Test', 'game-123')
        ).rejects.toThrow('Failed');

        // Message should be rolled back
        expect(errorStore.getState().conversations['session-1'] || []).toHaveLength(0);
      });

      it('handles non-Error objects in send failure', async () => {
        const errorStore = create<ConversationSlice>()(
          immer((set, get) => {
            const slice = createConversationSlice(set, get, { setState: set, getState: get, getInitialState: get, subscribe: vi.fn(), destroy: vi.fn() });
            return {
              ...slice,
              sendMessage: async (sessionId: string, content: string, _gameId: string) => {
                set(state => {
                  state.sendingMessage = true;
                  if (!state.conversations[sessionId]) {
                    state.conversations[sessionId] = [];
                  }
                  state.conversations[sessionId].push({
                    type: 'user',
                    content,
                    timestamp: new Date(),
                  });
                });

                try {
                  throw 'String error';
                } catch (error) {
                  const errorMessage = error instanceof Error ? error.message : 'Failed to send message';
                  set(state => {
                    const messages = state.conversations[sessionId];
                    if (messages && messages.length > 0) {
                      messages.pop();
                    }
                    state.sendingMessage = false;
                    state.conversationError = {
                      message: errorMessage,
                      code: 'SEND_MESSAGE_ERROR',
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
          errorStore.getState().sendMessage('session-1', 'Hello', 'game-123')
        ).rejects.toBe('String error');

        expect(errorStore.getState().conversationError?.message).toBe('Failed to send message');
      });
    });

    describe('loadHistory error handling', () => {
      it('sets conversationError when load fails with Error', async () => {
        const errorStore = create<ConversationSlice>()(
          immer((set, get) => {
            const slice = createConversationSlice(set, get, { setState: set, getState: get, getInitialState: get, subscribe: vi.fn(), destroy: vi.fn() });
            return {
              ...slice,
              loadHistory: async (_sessionId: string, _gameId: string) => {
                set(state => {
                  state.loadingHistory = true;
                  state.conversationError = null;
                });

                try {
                  throw new Error('Server timeout');
                } catch (error) {
                  const errorMessage = error instanceof Error ? error.message : 'Failed to load history';
                  set(state => {
                    state.loadingHistory = false;
                    state.conversationError = {
                      message: errorMessage,
                      code: 'LOAD_HISTORY_ERROR',
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
          errorStore.getState().loadHistory('session-1', 'game-123')
        ).rejects.toThrow('Server timeout');

        expect(errorStore.getState().loadingHistory).toBe(false);
        expect(errorStore.getState().conversationError).not.toBeNull();
        expect(errorStore.getState().conversationError?.message).toBe('Server timeout');
        expect(errorStore.getState().conversationError?.code).toBe('LOAD_HISTORY_ERROR');
      });

      it('handles non-Error objects in load failure', async () => {
        const errorStore = create<ConversationSlice>()(
          immer((set, get) => {
            const slice = createConversationSlice(set, get, { setState: set, getState: get, getInitialState: get, subscribe: vi.fn(), destroy: vi.fn() });
            return {
              ...slice,
              loadHistory: async () => {
                set(state => {
                  state.loadingHistory = true;
                  state.conversationError = null;
                });

                try {
                  throw { status: 404 };
                } catch (error) {
                  const errorMessage = error instanceof Error ? error.message : 'Failed to load history';
                  set(state => {
                    state.loadingHistory = false;
                    state.conversationError = {
                      message: errorMessage,
                      code: 'LOAD_HISTORY_ERROR',
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
          errorStore.getState().loadHistory('session-1', 'game-123')
        ).rejects.toEqual({ status: 404 });

        expect(errorStore.getState().conversationError?.message).toBe('Failed to load history');
      });
    });
  });
});
