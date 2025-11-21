/**
 * messagesSlice Tests
 *
 * Comprehensive test coverage for messages slice:
 * - State management
 * - Message loading and caching
 * - Optimistic updates (send/edit/delete)
 * - Feedback management
 * - Error handling
 * - Loading states
 *
 * Target: 90%+ coverage
 */

import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';
import { subscribeWithSelector } from 'zustand/middleware';
import { createMessagesSlice } from '../messagesSlice';
import { ChatStore } from '../../types';
import { Message, ChatThread } from '@/types';
import { api } from '@/lib/api';

// Mock API
jest.mock('@/lib/api');
const mockApi = api as jest.Mocked<typeof api>;

// Explicitly cast nested mock objects for TypeScript
const mockChat = mockApi.chat as jest.Mocked<typeof api.chat>;

// Create test store with minimal slices
const createTestStore = () => {
  return create<ChatStore>()(
    subscribeWithSelector(
      immer((...a) => ({
        // Messages slice (what we're testing)
        ...createMessagesSlice(...a),

        // Minimal session slice
        selectedGameId: null,
        selectedAgentId: null,
        sidebarCollapsed: false,
        selectGame: (gameId: string | null) => {},
        selectAgent: (agentId: string | null) => {},
        toggleSidebar: () => {},
        setSidebarCollapsed: (collapsed: boolean) => {},

        // Minimal game slice
        games: [],
        agents: [],
        setGames: (games) => {},
        setAgents: (agents) => {},
        loadGames: async () => {},
        loadAgents: async () => {}, // Issue #868: Agents are global, no gameId parameter

        // Minimal chat slice
        chatsByGame: {},
        activeChatIds: {},
        loadChats: async (gameId: string) => {},
        createChat: async () => {},
        deleteChat: async (chatId: string) => {},
        selectChat: async (chatId: string) => {},
        updateChatTitle: (chatId: string, title: string) => {},

        // Minimal UI slice
        loading: {
          chats: false,
          messages: false,
          sending: false,
          creating: false,
          updating: false,
          deleting: false,
          games: false,
          agents: false,
        },
        error: null,
        inputValue: '',
        editingMessageId: null,
        editContent: '',
        searchMode: 'semantic',
        setLoading: (key, value) => {},
        setError: (error) => {},
        clearError: () => {},
        setInputValue: (value) => {},
        startEdit: (messageId, content) => {},
        cancelEdit: () => {},
        saveEdit: async (editMessageFn) => {},
        setEditContent: (content) => {},
        setSearchMode: (mode) => {},
      }))
    )
  );
};

describe('messagesSlice', () => {
  let store: ReturnType<typeof createTestStore>;
  let setState: jest.SpyInstance;
  let setLoading: jest.Mock;
  let setError: jest.Mock;
  let updateChatTitle: jest.Mock;

  // Helper to create mock ChatThread
  const createMockThread = (overrides?: Partial<ChatThread>): ChatThread => ({
    id: 'aa0e8400-e29b-41d4-a716-000000000001',
    gameId: '770e8400-e29b-41d4-a716-000000000001',
    title: 'Test Thread',
    createdAt: '2025-01-01T00:00:00Z',
    lastMessageAt: null,
    messageCount: 0,
    messages: [],
    ...overrides,
  });

  // Helper to create mock ChatMessageResponse
  const createMockMessageResponse = (overrides?: Partial<any>): any => ({
    id: 'msg-1',
    chatId: 'aa0e8400-e29b-41d4-a716-000000000001',
    userId: '990e8400-e29b-41d4-a716-000000000001',
    level: 'info',
    content: 'Test message',
    sequenceNumber: 1,
    createdAt: '2025-01-01T00:00:00Z',
    updatedAt: null,
    isDeleted: false,
    deletedAt: null,
    ...overrides,
  });

  beforeEach(() => {
    jest.clearAllMocks();
    store = createTestStore();

    // Spy on setState
    setState = jest.spyOn(store, 'setState');

    // Create mocks for UI methods
    setLoading = jest.fn();
    setError = jest.fn();
    updateChatTitle = jest.fn();

    // Override store methods with mocks
    store.setState({
      setLoading,
      setError,
      updateChatTitle,
    });
  });

  afterEach(() => {
    jest.restoreAllMocks();
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
  });

  // ============================================================================
  // sendMessage
  // ============================================================================

  describe('sendMessage', () => {
    beforeEach(() => {
      store.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
        activeChatIds: {},
        chatsByGame: {},
        messagesByChat: {},
      });
    });

    it('should create new thread and send message', async () => {
      const mockNewThread: ChatThread = {
        id: 'thread-new',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: 'What are the rules for setup?',
        createdAt: '2025-01-01T00:00:00Z',
        lastMessageAt: null,
        messageCount: 0,
        messages: [],
      };

      mockChat.createThread.mockResolvedValue(mockNewThread);
      mockChat.addMessage.mockResolvedValue(createMockThread());

      await store.getState().sendMessage('What are the rules for setup?');

      expect(mockChat.createThread).toHaveBeenCalledWith({
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: 'What are the rules for setup?',
        initialMessage: null,
      });

      expect(mockChat.addMessage).toHaveBeenCalledWith(
        'thread-new',
        {
          content: 'What are the rules for setup?',
          role: 'user',
        }
      );

      const state = store.getState();
      expect(state.activeChatIds['770e8400-e29b-41d4-a716-000000000001']).toBe('thread-new');
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toContainEqual(mockNewThread);
    });

    it('should truncate long titles to 50 chars', async () => {
      const longMessage = 'This is a very long message that exceeds fifty characters and should be truncated';
      const mockNewThread: ChatThread = {
        id: 'thread-new',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: longMessage.substring(0, 50) + '...',
        createdAt: '2025-01-01T00:00:00Z',
        lastMessageAt: null,
        messageCount: 0,
        messages: [],
      };

      mockChat.createThread.mockResolvedValue(mockNewThread);
      mockChat.addMessage.mockResolvedValue(createMockThread());

      await store.getState().sendMessage(longMessage);

      expect(mockChat.createThread).toHaveBeenCalledWith({
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: longMessage.substring(0, 50) + '...',
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

    it('should add optimistic message before API call', async () => {
      mockChat.createThread.mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve({
          id: 'thread-new',
          gameId: '770e8400-e29b-41d4-a716-000000000001',
          title: 'Test',
          createdAt: '2025-01-01T00:00:00Z',
          lastMessageAt: null,
          messageCount: 0,
          messages: [],
        }), 100))
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
      const mockLoadMessages = jest.fn();
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
      const mockLoadMessages = jest.fn();
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
      const mockLoadMessages = jest.fn();
      store.setState({ loadMessages: mockLoadMessages });

      mockChat.updateMessage.mockRejectedValue(new Error('API Error'));

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
      const mockLoadMessages = jest.fn();
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
      const mockLoadMessages = jest.fn();
      store.setState({ loadMessages: mockLoadMessages });

      mockChat.deleteMessage.mockRejectedValue(new Error('API Error'));

      await store.getState().deleteMessage('msg-1');

      expect(setError).toHaveBeenCalledWith("Errore nell'eliminazione del messaggio");
      expect(setLoading).toHaveBeenCalledWith('deleting', false);
    });
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
        feedback: 'helpful',
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
        feedback: 'not-helpful',
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
        feedback: 'helpful',
      });
    });

    it('should revert on API error', async () => {
      mockChat.submitAgentFeedback.mockRejectedValue(new Error('API Error'));

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
  });

  // ============================================================================
  // Additional Branch Coverage Tests
  // ============================================================================

  describe('Branch Coverage Edge Cases', () => {
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

    it('should handle title generation for exactly 50 character messages', async () => {
      const exactly50Chars = '12345678901234567890123456789012345678901234567890'; // exactly 50
      const mockNewThread: ChatThread = {
        id: 'thread-new',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: exactly50Chars,
        createdAt: '2025-01-01T00:00:00Z',
        lastMessageAt: null,
        messageCount: 0,
        messages: [],
      };

      store.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
      });

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

    it('should handle setMessageFeedback with null previous feedback', async () => {
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

    it('should handle setMessageFeedback with undefined previous feedback', async () => {
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

    it('should handle removeOptimisticMessage when thread has no messages', () => {
      store.setState({
        messagesByChat: {
          'aa0e8400-e29b-41d4-a716-000000000001': [],
        },
      });

      store.getState().removeOptimisticMessage('non-existent', 'aa0e8400-e29b-41d4-a716-000000000001');

      const state = store.getState();
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toEqual([]);
    });

    it('should handle updateMessageInThread when messages array is undefined', () => {
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

    it('should handle addOptimisticMessage when thread messages is undefined', () => {
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

    it('should handle sendMessage when message length is less than 50 chars', async () => {
      const shortMessage = 'Short message';
      const mockNewThread: ChatThread = {
        id: 'thread-new',
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: shortMessage,
        createdAt: '2025-01-01T00:00:00Z',
        lastMessageAt: null,
        messageCount: 0,
        messages: [],
      };

      store.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
      });

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

    it('should handle removeOptimisticMessage when message exists in middle of array', () => {
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

    it('should handle loadMessages when thread has empty messages array', async () => {
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

    it('should handle setMessageFeedback when message has no backendMessageId', async () => {
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
        feedback: 'helpful',
      });
    });

    it('should handle setMessageFeedback when message has no endpoint', async () => {
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
        feedback: 'helpful',
      });
    });

    it('should handle setMessageFeedback when message has no gameId', async () => {
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
        feedback: 'helpful',
      });
    });

    it('should handle setMessageFeedback when selectedGameId is null', async () => {
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

    it('should handle updateMessageInThread with multiple updates', () => {
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

      mockChat.addMessage.mockResolvedValue(createMockThread());

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
