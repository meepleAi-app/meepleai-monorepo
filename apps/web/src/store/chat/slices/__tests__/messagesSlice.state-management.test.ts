/**
 * messagesSlice Tests - State Management
 *
 * Tests for:
 * - Initial state
 * - loadMessages
 *
 * Target: 90%+ coverage
 */

import { ChatThread } from '@/types';
import {
  createTestStore,
  createMockThread,
  mockChat,
  setupTestEnvironment,
} from './messagesSlice.test-helpers';

describe('messagesSlice - State Management', () => {
  let store: ReturnType<typeof createTestStore>;
  let setLoading: Mock;
  let setError: Mock;

  beforeEach(() => {
    vi.clearAllMocks();
    const env = setupTestEnvironment();
    store = env.store;
    setLoading = env.setLoading;
    setError = env.setError;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // ============================================================================
  // Initial State
  // ============================================================================

  describe('Initial State', () => {
    it('should have empty messagesByChat initially', () => {
      const state = store.getState();
      expect(state.messagesByChat).toEqual({});
    });
  });

  // ============================================================================
  // loadMessages
  // ============================================================================

  describe('loadMessages', () => {
    const mockThread: ChatThread = {
      id: 'aa0e8400-e29b-41d4-a716-000000000001',
      gameId: '770e8400-e29b-41d4-a716-000000000001',
      title: 'Test Thread',
      createdAt: '2025-01-01T00:00:00Z',
      lastMessageAt: '2025-01-01T01:00:00Z',
      messageCount: 2,
      messages: [
        {
          content: 'User message',
          role: 'user',
          timestamp: '2025-01-01T00:00:00Z',
          backendMessageId: 'msg-1',
          endpoint: 'qa',
          gameId: '770e8400-e29b-41d4-a716-000000000001',
          feedback: null,
        },
        {
          content: 'Assistant message',
          role: 'assistant',
          timestamp: '2025-01-01T00:30:00Z',
          backendMessageId: 'msg-2',
          endpoint: 'qa',
          gameId: '770e8400-e29b-41d4-a716-000000000001',
          feedback: 'helpful',
        },
      ],
    };

    it('should load messages successfully', async () => {
      mockChat.getThreadById.mockResolvedValue(mockThread);

      await store.getState().loadMessages('aa0e8400-e29b-41d4-a716-000000000001');

      expect(setLoading).toHaveBeenCalledWith('messages', true);
      expect(mockChat.getThreadById).toHaveBeenCalledWith('aa0e8400-e29b-41d4-a716-000000000001');
      expect(setLoading).toHaveBeenCalledWith('messages', false);

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toHaveLength(2);
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0]).toMatchObject({
        id: 'aa0e8400-e29b-41d4-a716-000000000001-0',
        role: 'user',
        content: 'User message',
        backendMessageId: 'msg-1',
        endpoint: 'qa',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        feedback: null,
      });
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][1]).toMatchObject({
        id: 'aa0e8400-e29b-41d4-a716-000000000001-1',
        role: 'assistant',
        content: 'Assistant message',
        backendMessageId: 'msg-2',
        feedback: 'helpful',
      });
    });

    it('should handle null thread (not found)', async () => {
      mockChat.getThreadById.mockResolvedValue(null as any);

      await store.getState().loadMessages('aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toEqual([]);
      expect(setLoading).toHaveBeenCalledWith('messages', true);
      expect(setLoading).toHaveBeenCalledWith('messages', false);
    });

    it('should handle API errors', async () => {
      const error = new Error('API Error');
      mockChat.getThreadById.mockRejectedValue(error);

      await store.getState().loadMessages('aa0e8400-e29b-41d4-a716-000000000001');

      expect(setError).toHaveBeenCalledWith('Errore nel caricamento dei messaggi');
      expect(setLoading).toHaveBeenCalledWith('messages', false);

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toEqual([]);
    });

    it('should handle non-Error rejections', async () => {
      mockChat.getThreadById.mockRejectedValue('String error');

      await store.getState().loadMessages('aa0e8400-e29b-41d4-a716-000000000001');

      expect(setError).toHaveBeenCalledWith('Errore nel caricamento dei messaggi');
      expect(setLoading).toHaveBeenCalledWith('messages', false);

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toEqual([]);
    });

    it('should convert timestamps to Date objects', async () => {
      mockChat.getThreadById.mockResolvedValue(mockThread);

      await store.getState().loadMessages('aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      const messages = state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'];

      expect(messages[0].timestamp).toBeInstanceOf(Date);
      expect(messages[1].timestamp).toBeInstanceOf(Date);
    });

    it('should handle messages without optional fields', async () => {
      const minimalThread: ChatThread = {
        id: 'aa0e8400-e29b-41d4-a716-000000000002',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: 'Minimal Thread',
        createdAt: '2025-01-01T00:00:00Z',
        lastMessageAt: null,
        messageCount: 1,
        messages: [
          {
            content: 'Simple message',
            role: 'user',
            timestamp: '2025-01-01T00:00:00Z',
          },
        ],
      };

      mockChat.getThreadById.mockResolvedValue(minimalThread);

      await store.getState().loadMessages('aa0e8400-e29b-41d4-a716-000000000002');

      const state = store.getState();
      const messages = state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000002'];

      expect(messages[0]).toMatchObject({
        id: 'aa0e8400-e29b-41d4-a716-000000000002-0',
        role: 'user',
        content: 'Simple message',
        feedback: null,
      });
      expect(messages[0].backendMessageId).toBeUndefined();
      expect(messages[0].endpoint).toBeUndefined();
      expect(messages[0].gameId).toBeUndefined();
    });

    it('should handle messages with null feedback field', async () => {
      const threadWithNullFeedback: ChatThread = {
        id: 'aa0e8400-e29b-41d4-a716-000000000001',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: 'Test Thread',
        createdAt: '2025-01-01T00:00:00Z',
        lastMessageAt: null,
        messageCount: 1,
        messages: [
          {
            content: 'Message with null feedback',
            role: 'assistant',
            timestamp: '2025-01-01T00:00:00Z',
            feedback: null,
          },
        ],
      };

      mockChat.getThreadById.mockResolvedValue(threadWithNullFeedback);

      await store.getState().loadMessages('aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].feedback).toBeNull();
    });

    it('should handle messages with undefined feedback field', async () => {
      const threadWithUndefinedFeedback: ChatThread = {
        id: 'aa0e8400-e29b-41d4-a716-000000000001',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: 'Test Thread',
        createdAt: '2025-01-01T00:00:00Z',
        lastMessageAt: null,
        messageCount: 1,
        messages: [
          {
            content: 'Message without feedback',
            role: 'assistant',
            timestamp: '2025-01-01T00:00:00Z',
          },
        ],
      };

      mockChat.getThreadById.mockResolvedValue(threadWithUndefinedFeedback);

      await store.getState().loadMessages('aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].feedback).toBeNull();
    });

    it('should handle thread with empty messages array', async () => {
      const emptyThread: ChatThread = {
        id: 'aa0e8400-e29b-41d4-a716-000000000001',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: 'Empty Thread',
        createdAt: '2025-01-01T00:00:00Z',
        lastMessageAt: null,
        messageCount: 0,
        messages: [],
      };

      mockChat.getThreadById.mockResolvedValue(emptyThread);

      await store.getState().loadMessages('aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toEqual([]);
    });
  });
});
