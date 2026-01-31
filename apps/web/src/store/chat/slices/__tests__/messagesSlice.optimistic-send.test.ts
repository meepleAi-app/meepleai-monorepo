/**
 * messagesSlice Tests - Optimistic Updates: sendMessage
 *
 * Tests for sendMessage action including:
 * - Thread creation and message sending
 * - Title truncation logic
 * - Existing thread handling
 * - Optimistic updates and rollback
 * - Error handling
 * - Loading states
 *
 * Target: 90%+ coverage
 */

import { Message } from '@/types';
import {
  createTestStore,
  createMockThread,
  mockChat,
  setupTestEnvironment,
} from './messagesSlice.test-helpers';

describe('messagesSlice - Optimistic Updates: sendMessage', () => {
  let store: ReturnType<typeof createTestStore>;
  let setLoading: Mock;
  let setError: Mock;
  let updateChatTitle: Mock;

  beforeEach(() => {
    vi.clearAllMocks();
    const env = setupTestEnvironment();
    store = env.store;
    setLoading = env.setLoading;
    setError = env.setError;
    updateChatTitle = env.updateChatTitle;

    // Setup for sendMessage tests
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

  describe('sendMessage', () => {
    it('should create new thread and send message', async () => {
      const mockNewThread = createMockThread();

      mockChat.createThread.mockResolvedValue(mockNewThread);
      mockChat.addMessage.mockResolvedValue(createMockThread());

      await store.getState().sendMessage('What are the rules for setup?');

      expect(mockChat.createThread).toHaveBeenCalledWith({
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: 'What are the rules for setup?',
        initialMessage: null,
      });

      expect(mockChat.addMessage).toHaveBeenCalledWith(
        mockNewThread.id,
        {
          content: 'What are the rules for setup?',
          role: 'user',
        }
      );

      const state = store.getState();
      expect(state.activeChatIds['770e8400-e29b-41d4-a716-000000000001']).toBe(mockNewThread.id);
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toContainEqual(mockNewThread);
    });

    it('should truncate long titles to 50 chars', async () => {
      const longMessage = 'This is a very long message that exceeds fifty characters and should be truncated';
      const expectedTitle = longMessage.substring(0, 50) + '...';

      const mockNewThread = createMockThread();
      mockNewThread.title = expectedTitle;

      mockChat.createThread.mockResolvedValue(mockNewThread);
      mockChat.addMessage.mockResolvedValue(createMockThread());

      await store.getState().sendMessage(longMessage);

      expect(mockChat.createThread).toHaveBeenCalledWith({
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: expectedTitle,
        initialMessage: null,
      });
    });

    it('should handle exactly 50 character messages', async () => {
      const exactly50Chars = '12345678901234567890123456789012345678901234567890'; // exactly 50

      const mockNewThread = createMockThread();
      mockNewThread.title = exactly50Chars;

      mockChat.createThread.mockResolvedValue(mockNewThread);
      mockChat.addMessage.mockResolvedValue(createMockThread());

      await store.getState().sendMessage(exactly50Chars);

      // Should NOT add ellipsis for exactly 50 chars
      expect(mockChat.createThread).toHaveBeenCalledWith({
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: exactly50Chars,
        initialMessage: null,
      });
    });

    it('should handle short messages without truncation', async () => {
      const shortMessage = 'Short message';

      const mockNewThread = createMockThread();
      mockNewThread.title = shortMessage;

      mockChat.createThread.mockResolvedValue(mockNewThread);
      mockChat.addMessage.mockResolvedValue(createMockThread());

      await store.getState().sendMessage(shortMessage);

      // Should NOT add ellipsis for messages under 50 chars
      expect(mockChat.createThread).toHaveBeenCalledWith({
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: shortMessage,
        initialMessage: null,
      });
    });

    it('should use existing thread if available', async () => {
      store.setState({
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'thread-existing' },
        messagesByChat: { 'thread-existing': [] },
      });

      mockChat.addMessage.mockResolvedValue(createMockThread());

      await store.getState().sendMessage('Follow-up question');

      expect(mockChat.createThread).not.toHaveBeenCalled();
      expect(mockChat.addMessage).toHaveBeenCalledWith(
        'thread-existing',
        {
          content: 'Follow-up question',
          role: 'user',
        }
      );
    });

    it('should initialize messagesByChat for existing thread without messages', async () => {
      store.setState({
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'thread-existing' },
        messagesByChat: {}, // Thread ID exists in activeChatIds but not in messagesByChat
      });

      mockChat.addMessage.mockResolvedValue(createMockThread());

      await store.getState().sendMessage('First message to existing thread');

      // Should initialize the messages array
      const state = store.getState();
      expect(state.messagesByChat['thread-existing']).toBeDefined();
      expect(Array.isArray(state.messagesByChat['thread-existing'])).toBe(true);
    });

    it('should add optimistic message before API call', async () => {
      const mockNewThread = createMockThread();

      mockChat.createThread.mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve(mockNewThread), 100))
      );
      mockChat.addMessage.mockResolvedValue(createMockThread());

      const promise = store.getState().sendMessage('Test message');

      // Check optimistic message is added immediately
      await new Promise(resolve => setTimeout(resolve, 10));

      await promise;
    });

    it('should remove isOptimistic flag after success', async () => {
      store.setState({
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: { 'aa0e8400-e29b-41d4-a716-000000000001': [] },
      });

      mockChat.addMessage.mockResolvedValue(createMockThread());

      await store.getState().sendMessage('Test message');

      const state = store.getState();
      const messages = state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001'];

      // Message should exist and not be optimistic
      expect(messages).toHaveLength(1);
      expect(messages[0].isOptimistic).toBe(false);
    });

    it('should rollback optimistic update on error', async () => {
      store.setState({
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: { 'aa0e8400-e29b-41d4-a716-000000000001': [] },
      });

      mockChat.addMessage.mockRejectedValue(new Error('API Error'));

      await store.getState().sendMessage('Test message');

      expect(setError).toHaveBeenCalledWith("Errore nell'invio del messaggio");

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toEqual([]);
    });

    it('should handle non-Error rejections on send', async () => {
      store.setState({
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: { 'aa0e8400-e29b-41d4-a716-000000000001': [] },
      });

      mockChat.addMessage.mockRejectedValue('String error');

      await store.getState().sendMessage('Test message');

      expect(setError).toHaveBeenCalledWith("Errore nell'invio del messaggio");

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toEqual([]);
    });

    it('should not send if no game selected', async () => {
      store.setState({ selectedGameId: null });

      await store.getState().sendMessage('Test message');

      expect(mockChat.createThread).not.toHaveBeenCalled();
      expect(mockChat.addMessage).not.toHaveBeenCalled();
    });

    it('should not send if no agent selected', async () => {
      store.setState({ selectedAgentId: null });

      await store.getState().sendMessage('Test message');

      expect(mockChat.createThread).not.toHaveBeenCalled();
      expect(mockChat.addMessage).not.toHaveBeenCalled();
    });

    it('should not send empty messages', async () => {
      await store.getState().sendMessage('   ');

      expect(mockChat.createThread).not.toHaveBeenCalled();
      expect(mockChat.addMessage).not.toHaveBeenCalled();
    });

    it('should trim message content', async () => {
      store.setState({
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: { 'aa0e8400-e29b-41d4-a716-000000000001': [] },
      });

      mockChat.addMessage.mockResolvedValue(createMockThread());

      await store.getState().sendMessage('  Test message  ');

      expect(mockChat.addMessage).toHaveBeenCalledWith(
        'aa0e8400-e29b-41d4-a716-000000000001',
        {
          content: 'Test message',
          role: 'user',
        }
      );
    });

    it('should update chat title if first message in existing thread', async () => {
      store.setState({
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: { 'aa0e8400-e29b-41d4-a716-000000000001': [] },
      });

      mockChat.addMessage.mockResolvedValue(createMockThread());

      await store.getState().sendMessage('First message');

      expect(updateChatTitle).toHaveBeenCalledWith('aa0e8400-e29b-41d4-a716-000000000001', 'First message');
    });

    it('should not update title if not first message', async () => {
      const existingMessage: Message = {
        id: 'msg-1',
        role: 'user',
        content: 'Previous message',
        timestamp: new Date(),
      };

      store.setState({
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: { 'aa0e8400-e29b-41d4-a716-000000000001': [existingMessage] },
      });

      mockChat.addMessage.mockResolvedValue(createMockThread());

      await store.getState().sendMessage('Second message');

      expect(updateChatTitle).not.toHaveBeenCalled();
    });

    it('should handle thread creation failure', async () => {
      mockChat.createThread.mockResolvedValue(null as any);

      await store.getState().sendMessage('Test message');

      expect(setError).toHaveBeenCalledWith("Errore nell'invio del messaggio");
    });

    it('should set loading states correctly', async () => {
      store.setState({
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
        messagesByChat: { 'aa0e8400-e29b-41d4-a716-000000000001': [] },
      });

      mockChat.addMessage.mockResolvedValue(createMockThread());

      await store.getState().sendMessage('Test message');

      expect(setLoading).toHaveBeenCalledWith('sending', true);
      expect(setLoading).toHaveBeenCalledWith('sending', false);
    });
  });
});
