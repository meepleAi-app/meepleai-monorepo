/**
 * messagesSlice Tests - Feedback and Edge Cases
 *
 * Tests for:
 * - setMessageFeedback
 * - Branch coverage edge cases
 * - Integration edge cases
 *
 * Target: 90%+ coverage
 */

import { Message } from '@/types';
import {
  createTestStore,
  mockChat,
  setupTestEnvironment,
} from './messagesSlice.test-helpers';

describe('messagesSlice - Feedback and Edge Cases', () => {
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
  // setMessageFeedback
  // ============================================================================

  describe('setMessageFeedback', () => {
    beforeEach(() => {
      store.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'assistant',
              content: 'Assistant message',
              timestamp: new Date(),
              backendMessageId: 'backend-msg-1',
              endpoint: 'qa',
              gameId: '770e8400-e29b-41d4-a716-000000000001',
              feedback: null,
            },
          ],
        },
      });
    });

    it('should set feedback successfully', async () => {
      mockChat.submitAgentFeedback.mockResolvedValue(undefined);

      await store.getState().setMessageFeedback('msg-1', 'helpful');

      expect(mockChat.submitAgentFeedback).toHaveBeenCalledWith({
        messageId: 'backend-msg-1',
        endpoint: 'qa',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        outcome: 'helpful',
      });

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].feedback).toBe('helpful');
    });

    it('should toggle feedback to null if same value', async () => {
      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'assistant',
              content: 'Assistant message',
              timestamp: new Date(),
              backendMessageId: 'backend-msg-1',
              endpoint: 'qa',
              gameId: '770e8400-e29b-41d4-a716-000000000001',
              feedback: 'helpful',
            },
          ],
        },
      });

      await store.getState().setMessageFeedback('msg-1', 'helpful');

      // API should NOT be called when toggling to null (FE-IMP-005: API doesn't accept null)
      expect(mockChat.submitAgentFeedback).not.toHaveBeenCalled();

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].feedback).toBeNull();
    });

    it('should change feedback from one value to another', async () => {
      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'assistant',
              content: 'Assistant message',
              timestamp: new Date(),
              backendMessageId: 'backend-msg-1',
              endpoint: 'qa',
              gameId: '770e8400-e29b-41d4-a716-000000000001',
              feedback: 'helpful',
            },
          ],
        },
      });

      mockChat.submitAgentFeedback.mockResolvedValue(undefined);

      await store.getState().setMessageFeedback('msg-1', 'not-helpful');

      expect(mockChat.submitAgentFeedback).toHaveBeenCalledWith({
        messageId: 'backend-msg-1',
        endpoint: 'qa',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        outcome: 'not-helpful',
      });

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].feedback).toBe('not-helpful');
    });

    it('should use defaults for missing optional fields', async () => {
      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'assistant',
              content: 'Assistant message',
              timestamp: new Date(),
              feedback: null,
            },
          ],
        },
      });

      mockChat.submitAgentFeedback.mockResolvedValue(undefined);

      await store.getState().setMessageFeedback('msg-1', 'helpful');

      expect(mockChat.submitAgentFeedback).toHaveBeenCalledWith({
        messageId: 'msg-1',
        endpoint: 'qa',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        outcome: 'helpful',
      });
    });

    it('should revert on API error', async () => {
      mockChat.submitAgentFeedback.mockRejectedValue(new Error('API Error'));

      await store.getState().setMessageFeedback('msg-1', 'helpful');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].feedback).toBeNull();
    });

    it('should revert on non-Error rejections', async () => {
      mockChat.submitAgentFeedback.mockRejectedValue('String error');

      await store.getState().setMessageFeedback('msg-1', 'helpful');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].feedback).toBeNull();
    });

    it('should not set feedback if no game selected', async () => {
      store.setState({ selectedGameId: null });

      await store.getState().setMessageFeedback('msg-1', 'helpful');

      expect(mockChat.submitAgentFeedback).not.toHaveBeenCalled();
    });

    it('should not set feedback if no active thread', async () => {
      store.setState({ activeChatIds: {} });

      await store.getState().setMessageFeedback('msg-1', 'helpful');

      expect(mockChat.submitAgentFeedback).not.toHaveBeenCalled();
    });

    it('should not set feedback if message not found', async () => {
      await store.getState().setMessageFeedback('non-existent', 'helpful');

      expect(mockChat.submitAgentFeedback).not.toHaveBeenCalled();
    });

    it('should handle null previous feedback', async () => {
      store.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'assistant',
              content: 'Message',
              timestamp: new Date(),
              backendMessageId: 'backend-1',
              endpoint: 'qa',
              gameId: '770e8400-e29b-41d4-a716-000000000001',
              feedback: null,
            },
          ],
        },
      });

      mockChat.submitAgentFeedback.mockResolvedValue(undefined);

      await store.getState().setMessageFeedback('msg-1', 'helpful');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].feedback).toBe('helpful');
    });

    it('should handle undefined previous feedback', async () => {
      store.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'assistant',
              content: 'Message',
              timestamp: new Date(),
              backendMessageId: 'backend-1',
              endpoint: 'qa',
              gameId: '770e8400-e29b-41d4-a716-000000000001',
            },
          ],
        },
      });

      mockChat.submitAgentFeedback.mockResolvedValue(undefined);

      await store.getState().setMessageFeedback('msg-1', 'not-helpful');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].feedback).toBe('not-helpful');
    });

    it('should handle message with no backendMessageId', async () => {
      store.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'assistant',
              content: 'Message',
              timestamp: new Date(),
              endpoint: 'qa',
              gameId: '770e8400-e29b-41d4-a716-000000000001',
              feedback: null,
            },
          ],
        },
      });

      mockChat.submitAgentFeedback.mockResolvedValue(undefined);

      await store.getState().setMessageFeedback('msg-1', 'helpful');

      // Should use msg-1 as fallback when backendMessageId is undefined
      expect(mockChat.submitAgentFeedback).toHaveBeenCalledWith({
        messageId: 'msg-1',
        endpoint: 'qa',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        outcome: 'helpful',
      });
    });

    it('should handle message with no endpoint', async () => {
      store.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'assistant',
              content: 'Message',
              timestamp: new Date(),
              backendMessageId: 'backend-1',
              gameId: '770e8400-e29b-41d4-a716-000000000001',
              feedback: null,
            },
          ],
        },
      });

      mockChat.submitAgentFeedback.mockResolvedValue(undefined);

      await store.getState().setMessageFeedback('msg-1', 'helpful');

      // Should use 'qa' as default endpoint
      expect(mockChat.submitAgentFeedback).toHaveBeenCalledWith({
        messageId: 'backend-1',
        endpoint: 'qa',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        outcome: 'helpful',
      });
    });

    it('should handle message with no gameId', async () => {
      store.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'assistant',
              content: 'Message',
              timestamp: new Date(),
              backendMessageId: 'backend-1',
              endpoint: 'qa',
              feedback: null,
            },
          ],
        },
      });

      mockChat.submitAgentFeedback.mockResolvedValue(undefined);

      await store.getState().setMessageFeedback('msg-1', 'helpful');

      // Should use selectedGameId as fallback
      expect(mockChat.submitAgentFeedback).toHaveBeenCalledWith({
        messageId: 'backend-1',
        endpoint: 'qa',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        outcome: 'helpful',
      });
    });

    it('should handle when selectedGameId is null', async () => {
      store.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'assistant',
              content: 'Message',
              timestamp: new Date(),
              backendMessageId: 'backend-1',
              endpoint: 'qa',
              feedback: null,
            },
          ],
        },
      });

      // Set selectedGameId to null after setting up messages
      store.setState({ selectedGameId: null });

      await store.getState().setMessageFeedback('msg-1', 'helpful');

      // Should not call API when no selected game
      expect(mockChat.submitAgentFeedback).not.toHaveBeenCalled();
    });
  });

  // ============================================================================
  // Edge Cases and Integration
  // ============================================================================

  describe('Edge Cases', () => {
    it('should handle concurrent message operations', async () => {
      store.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: { 'aa0e8400-e29b-41d4-a716-000000000001': [] },
      });

      mockChat.addMessage.mockResolvedValue({
        id: 'aa0e8400-e29b-41d4-a716-000000000001',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: 'Test Thread',
        createdAt: '2025-01-01T00:00:00Z',
        lastMessageAt: null,
        messageCount: 0,
        messages: [],
      });

      // Send multiple messages concurrently
      await Promise.all([
        store.getState().sendMessage('Message 1'),
        store.getState().sendMessage('Message 2'),
        store.getState().sendMessage('Message 3'),
      ]);

      expect(mockChat.addMessage).toHaveBeenCalledTimes(3);
    });

    it('should handle rapid feedback changes', async () => {
      store.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'assistant',
              content: 'Message',
              timestamp: new Date(),
              backendMessageId: 'backend-1',
              endpoint: 'qa',
              gameId: '770e8400-e29b-41d4-a716-000000000001',
              feedback: null,
            },
          ],
        },
      });

      mockChat.submitAgentFeedback.mockResolvedValue(undefined);

      // Rapid feedback changes
      await Promise.all([
        store.getState().setMessageFeedback('msg-1', 'helpful'),
        store.getState().setMessageFeedback('msg-1', 'not-helpful'),
      ]);

      expect(mockChat.submitAgentFeedback).toHaveBeenCalledTimes(2);
    });

    it('should preserve message order during operations', async () => {
      const messages: Message[] = [
        {
          id: 'msg-1',
          role: 'user',
          content: 'First',
          timestamp: new Date('2025-01-01T00:00:00Z'),
        },
        {
          id: 'msg-2',
          role: 'assistant',
          content: 'Second',
          timestamp: new Date('2025-01-01T00:01:00Z'),
        },
        {
          id: 'msg-3',
          role: 'user',
          content: 'Third',
          timestamp: new Date('2025-01-01T00:02:00Z'),
        },
      ];

      store.setState({
        messagesByChat: { 'aa0e8400-e29b-41d4-a716-000000000001': messages },
      });

      // Add optimistic message
      const newMessage: Message = {
        id: 'temp-1',
        role: 'user',
        content: 'Fourth',
        timestamp: new Date('2025-01-01T00:03:00Z'),
      };

      store.getState().addOptimisticMessage(newMessage, 'aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toHaveLength(4);
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][3].content).toBe('Fourth');
    });
  });
});
