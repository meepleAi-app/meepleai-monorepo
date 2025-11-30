/**
 * messagesSlice Tests - Optimistic Updates: Helper Actions
 *
 * Tests for helper actions:
 * - addOptimisticMessage: Add optimistic messages to threads
 * - removeOptimisticMessage: Remove optimistic messages after success/error
 *
 * Target: 90%+ coverage
 */

import { Message } from '@/types';
import {
  createTestStore,
  setupTestEnvironment,
} from './messagesSlice.test-helpers';

describe('messagesSlice - Optimistic Updates: Helper Actions', () => {
  let store: ReturnType<typeof createTestStore>;

  beforeEach(() => {
    vi.clearAllMocks();
    const env = setupTestEnvironment();
    store = env.store;

    // Setup initial state
    store.setState({
      selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
      selectedAgentId: 'agent-1',
      activeChatIds: {},
      chatsByGame: {},
      messagesByChat: {},
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // ============================================================================
  // addOptimisticMessage
  // ============================================================================

  describe('addOptimisticMessage', () => {
    it('should add optimistic message to thread', () => {
      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [],
        },
      });

      const newMessage: Message = {
        id: 'temp-1',
        role: 'user',
        content: 'Optimistic message',
        timestamp: new Date(),
      };

      store.getState().addOptimisticMessage(newMessage, 'aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toHaveLength(1);
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0]).toMatchObject({
        ...newMessage,
        isOptimistic: true,
      });
    });

    it('should append to existing messages', () => {
      const existingMessage: Message = {
        id: 'msg-1',
        role: 'user',
        content: 'Existing message',
        timestamp: new Date(),
      };

      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [existingMessage],
        },
      });

      const newMessage: Message = {
        id: 'temp-1',
        role: 'assistant',
        content: 'Optimistic message',
        timestamp: new Date(),
      };

      store.getState().addOptimisticMessage(newMessage, 'aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toHaveLength(2);
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][1].isOptimistic).toBe(true);
    });

    it('should create thread entry if not exists', () => {
      const newMessage: Message = {
        id: 'temp-1',
        role: 'user',
        content: 'Optimistic message',
        timestamp: new Date(),
      };

      store.getState().addOptimisticMessage(newMessage, 'thread-new');

      const state = store.getState();
      expect(state.messagesByChat['thread-new']).toBeDefined();
      expect(state.messagesByChat['thread-new']).toHaveLength(1);
    });

    it('should handle when thread messages is undefined', () => {
      const newMessage: Message = {
        id: 'temp-1',
        role: 'user',
        content: 'New message',
        timestamp: new Date(),
      };

      // Don't initialize the thread
      store.getState().addOptimisticMessage(newMessage, 'new-thread');

      const state = store.getState();
      expect(state.messagesByChat['new-thread']).toBeDefined();
      expect(state.messagesByChat['new-thread'][0].isOptimistic).toBe(true);
    });

    it('should maintain message order', () => {
      const existingMessages: Message[] = [
        {
          id: 'msg-1',
          role: 'user',
          content: 'First',
          timestamp: new Date('2025-01-01T10:00:00Z'),
        },
        {
          id: 'msg-2',
          role: 'assistant',
          content: 'Second',
          timestamp: new Date('2025-01-01T10:01:00Z'),
        },
      ];

      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': existingMessages,
        },
      });

      const newMessage: Message = {
        id: 'temp-1',
        role: 'user',
        content: 'Third',
        timestamp: new Date('2025-01-01T10:02:00Z'),
      };

      store.getState().addOptimisticMessage(newMessage, 'aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      const messages = state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'];
      expect(messages).toHaveLength(3);
      expect(messages[2].id).toBe('temp-1');
      expect(messages[2].content).toBe('Third');
    });
  });

  // ============================================================================
  // removeOptimisticMessage
  // ============================================================================

  describe('removeOptimisticMessage', () => {
    it('should remove optimistic message from thread', () => {
      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'user',
              content: 'Message 1',
              timestamp: new Date(),
            },
            {
              id: 'temp-1',
              role: 'user',
              content: 'Optimistic',
              timestamp: new Date(),
              isOptimistic: true,
            },
            {
              id: 'msg-2',
              role: 'user',
              content: 'Message 2',
              timestamp: new Date(),
            },
          ],
        },
      });

      store.getState().removeOptimisticMessage('temp-1', 'aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toHaveLength(2);
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'].find(m => m.id === 'temp-1')).toBeUndefined();
    });

    it('should handle non-existent message gracefully', () => {
      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'user',
              content: 'Message 1',
              timestamp: new Date(),
            },
          ],
        },
      });

      store.getState().removeOptimisticMessage('non-existent', 'aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toHaveLength(1);
    });

    it('should handle non-existent thread gracefully', () => {
      store.getState().removeOptimisticMessage('msg-1', 'non-existent-thread');

      const state = store.getState();
      expect(state.messagesByChat['non-existent-thread']).toEqual([]);
    });

    it('should handle when thread has no messages', () => {
      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [],
        },
      });

      store.getState().removeOptimisticMessage('non-existent', 'aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toEqual([]);
    });

    it('should handle message in middle of array', () => {
      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'user',
              content: 'First',
              timestamp: new Date(),
            },
            {
              id: 'temp-1',
              role: 'user',
              content: 'Optimistic',
              timestamp: new Date(),
              isOptimistic: true,
            },
            {
              id: 'msg-2',
              role: 'user',
              content: 'Last',
              timestamp: new Date(),
            },
          ],
        },
      });

      store.getState().removeOptimisticMessage('temp-1', 'aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toHaveLength(2);
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].id).toBe('msg-1');
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][1].id).toBe('msg-2');
    });

    it('should handle removing first message', () => {
      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'temp-1',
              role: 'user',
              content: 'Optimistic',
              timestamp: new Date(),
              isOptimistic: true,
            },
            {
              id: 'msg-1',
              role: 'assistant',
              content: 'Response',
              timestamp: new Date(),
            },
          ],
        },
      });

      store.getState().removeOptimisticMessage('temp-1', 'aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toHaveLength(1);
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].id).toBe('msg-1');
    });

    it('should handle removing last message', () => {
      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'user',
              content: 'Question',
              timestamp: new Date(),
            },
            {
              id: 'temp-1',
              role: 'user',
              content: 'Optimistic',
              timestamp: new Date(),
              isOptimistic: true,
            },
          ],
        },
      });

      store.getState().removeOptimisticMessage('temp-1', 'aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toHaveLength(1);
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].id).toBe('msg-1');
    });
  });
});
