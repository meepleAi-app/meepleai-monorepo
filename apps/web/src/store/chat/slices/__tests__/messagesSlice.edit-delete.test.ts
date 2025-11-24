/**
 * messagesSlice Tests - Edit and Delete Operations
 *
 * Tests for:
 * - editMessage
 * - deleteMessage
 * - updateMessageInThread
 *
 * Target: 90%+ coverage
 */

import {
  createTestStore,
  createMockMessageResponse,
  mockChat,
  setupTestEnvironment,
} from './messagesSlice.test-helpers';

describe('messagesSlice - Edit and Delete Operations', () => {
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
  // editMessage
  // ============================================================================

  describe('editMessage', () => {
    beforeEach(() => {
      store.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'user',
              content: 'Original message',
              timestamp: new Date(),
            },
          ],
        },
      });
    });

    it('should edit message successfully', async () => {
      const mockLoadMessages = vi.fn();
      store.setState({ loadMessages: mockLoadMessages });

      mockChat.updateMessage.mockResolvedValue(createMockMessageResponse());

      await store.getState().editMessage('msg-1', 'Updated message');

      expect(mockChat.updateMessage).toHaveBeenCalledWith(
        'aa0e8400-e29b-41d4-a716-000000000001',
        'msg-1',
        'Updated message'
      );
      expect(mockLoadMessages).toHaveBeenCalledWith('aa0e8400-e29b-41d4-a716-000000000001');
      expect(setLoading).toHaveBeenCalledWith('updating', true);
      expect(setLoading).toHaveBeenCalledWith('updating', false);
    });

    it('should trim message content', async () => {
      const mockLoadMessages = vi.fn();
      store.setState({ loadMessages: mockLoadMessages });

      mockChat.updateMessage.mockResolvedValue(createMockMessageResponse());

      await store.getState().editMessage('msg-1', '  Updated message  ');

      expect(mockChat.updateMessage).toHaveBeenCalledWith(
        'aa0e8400-e29b-41d4-a716-000000000001',
        'msg-1',
        'Updated message'
      );
    });

    it('should not edit if no game selected', async () => {
      store.setState({ selectedGameId: null });

      await store.getState().editMessage('msg-1', 'Updated message');

      expect(mockChat.updateMessage).not.toHaveBeenCalled();
    });

    it('should not edit with empty content', async () => {
      await store.getState().editMessage('msg-1', '   ');

      expect(mockChat.updateMessage).not.toHaveBeenCalled();
    });

    it('should not edit if no active thread', async () => {
      store.setState({ activeChatIds: {} });

      await store.getState().editMessage('msg-1', 'Updated message');

      expect(mockChat.updateMessage).not.toHaveBeenCalled();
    });

    it('should handle API errors', async () => {
      const mockLoadMessages = vi.fn();
      store.setState({ loadMessages: mockLoadMessages });

      mockChat.updateMessage.mockRejectedValue(new Error('API Error'));

      await store.getState().editMessage('msg-1', 'Updated message');

      expect(setError).toHaveBeenCalledWith("Errore nell'aggiornamento del messaggio");
      expect(setLoading).toHaveBeenCalledWith('updating', false);
    });

    it('should handle non-Error rejections on edit', async () => {
      const mockLoadMessages = vi.fn();
      store.setState({ loadMessages: mockLoadMessages });

      mockChat.updateMessage.mockRejectedValue('String error');

      await store.getState().editMessage('msg-1', 'Updated message');

      expect(setError).toHaveBeenCalledWith("Errore nell'aggiornamento del messaggio");
      expect(setLoading).toHaveBeenCalledWith('updating', false);
    });
  });

  // ============================================================================
  // deleteMessage
  // ============================================================================

  describe('deleteMessage', () => {
    beforeEach(() => {
      store.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'user',
              content: 'Message to delete',
              timestamp: new Date(),
            },
          ],
        },
      });
    });

    it('should delete message successfully', async () => {
      const mockLoadMessages = vi.fn();
      store.setState({ loadMessages: mockLoadMessages });

      mockChat.deleteMessage.mockResolvedValue(createMockMessageResponse());

      await store.getState().deleteMessage('msg-1');

      expect(mockChat.deleteMessage).toHaveBeenCalledWith('aa0e8400-e29b-41d4-a716-000000000001', 'msg-1');
      expect(mockLoadMessages).toHaveBeenCalledWith('aa0e8400-e29b-41d4-a716-000000000001');
      expect(setLoading).toHaveBeenCalledWith('deleting', true);
      expect(setLoading).toHaveBeenCalledWith('deleting', false);
    });

    it('should not delete if no game selected', async () => {
      store.setState({ selectedGameId: null });

      await store.getState().deleteMessage('msg-1');

      expect(mockChat.deleteMessage).not.toHaveBeenCalled();
    });

    it('should not delete if no active thread', async () => {
      store.setState({ activeChatIds: {} });

      await store.getState().deleteMessage('msg-1');

      expect(mockChat.deleteMessage).not.toHaveBeenCalled();
    });

    it('should handle API errors', async () => {
      const mockLoadMessages = vi.fn();
      store.setState({ loadMessages: mockLoadMessages });

      mockChat.deleteMessage.mockRejectedValue(new Error('API Error'));

      await store.getState().deleteMessage('msg-1');

      expect(setError).toHaveBeenCalledWith("Errore nell'eliminazione del messaggio");
      expect(setLoading).toHaveBeenCalledWith('deleting', false);
    });

    it('should handle non-Error rejections on delete', async () => {
      const mockLoadMessages = vi.fn();
      store.setState({ loadMessages: mockLoadMessages });

      mockChat.deleteMessage.mockRejectedValue('String error');

      await store.getState().deleteMessage('msg-1');

      expect(setError).toHaveBeenCalledWith("Errore nell'eliminazione del messaggio");
      expect(setLoading).toHaveBeenCalledWith('deleting', false);
    });
  });

  // ============================================================================
  // updateMessageInThread
  // ============================================================================

  describe('updateMessageInThread', () => {
    beforeEach(() => {
      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'user',
              content: 'Original content',
              timestamp: new Date('2025-01-01T00:00:00Z'),
              feedback: null,
            },
            {
              id: 'msg-2',
              role: 'assistant',
              content: 'Response',
              timestamp: new Date('2025-01-01T00:01:00Z'),
              feedback: 'helpful',
            },
          ],
        },
      });
    });

    it('should update message fields', () => {
      store.getState().updateMessageInThread('aa0e8400-e29b-41d4-a716-000000000001', 'msg-1', {
        content: 'Updated content',
        feedback: 'not-helpful',
      });

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0]).toMatchObject({
        id: 'msg-1',
        content: 'Updated content',
        feedback: 'not-helpful',
        role: 'user', // preserved
      });
    });

    it('should update single field', () => {
      store.getState().updateMessageInThread('aa0e8400-e29b-41d4-a716-000000000001', 'msg-1', {
        feedback: 'helpful',
      });

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0]).toMatchObject({
        id: 'msg-1',
        content: 'Original content', // preserved
        feedback: 'helpful',
      });
    });

    it('should handle non-existent message gracefully', () => {
      store.getState().updateMessageInThread('aa0e8400-e29b-41d4-a716-000000000001', 'non-existent', {
        content: 'New content',
      });

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toHaveLength(2);
      // Original messages unchanged
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].content).toBe('Original content');
    });

    it('should handle non-existent thread gracefully', () => {
      store.getState().updateMessageInThread('non-existent-thread', 'msg-1', {
        content: 'New content',
      });

      const state = store.getState();
      // Non-existent thread should remain undefined (not created by update)
      expect(state.messagesByChat['non-existent-thread']).toBeUndefined();
    });

    it('should preserve other message properties', () => {
      const originalTimestamp = store.getState().messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].timestamp;

      store.getState().updateMessageInThread('aa0e8400-e29b-41d4-a716-000000000001', 'msg-1', {
        content: 'Updated content',
      });

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].timestamp).toEqual(originalTimestamp);
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].role).toBe('user');
    });

    it('should update isOptimistic flag', () => {
      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'temp-1',
              role: 'user',
              content: 'Optimistic message',
              timestamp: new Date(),
              isOptimistic: true,
            },
          ],
        },
      });

      store.getState().updateMessageInThread('aa0e8400-e29b-41d4-a716-000000000001', 'temp-1', {
        isOptimistic: false,
      });

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0].isOptimistic).toBe(false);
    });

    it('should handle when messages array is undefined', () => {
      // Ensure thread doesn't exist
      const state = store.getState();
      expect(state.messagesByChat['new-thread']).toBeUndefined();

      store.getState().updateMessageInThread('new-thread', 'msg-1', {
        content: 'New content',
      });

      const newState = store.getState();
      // updateMessageInThread uses ?? [] so it should handle undefined gracefully
      expect(newState.messagesByChat['new-thread']).toBeUndefined();
    });

    it('should handle multiple updates', () => {
      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [
            {
              id: 'msg-1',
              role: 'user',
              content: 'Original',
              timestamp: new Date(),
              feedback: null,
              isOptimistic: false,
            },
          ],
        },
      });

      store.getState().updateMessageInThread('aa0e8400-e29b-41d4-a716-000000000001', 'msg-1', {
        content: 'Updated',
        feedback: 'helpful',
        isOptimistic: true,
      });

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'][0]).toMatchObject({
        id: 'msg-1',
        content: 'Updated',
        feedback: 'helpful',
        isOptimistic: true,
      });
    });
  });
});
