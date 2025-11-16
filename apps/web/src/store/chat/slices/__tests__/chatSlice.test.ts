/**
 * Comprehensive tests for chatSlice.ts
 * Target: 90%+ coverage
 *
 * Test scenarios:
 * - State initialization
 * - loadChats: Success, error, null response, empty array
 * - createChat: Success, validation, auto-archiving, errors
 * - deleteChat: Success, user cancellation, active chat cleanup, errors
 * - selectChat: Success, loadMessages integration
 * - updateChatTitle: Success, missing game, thread not found
 * - Loading states and error handling
 * - Edge cases and boundary conditions
 */

import { useChatStore } from '../../store';
import { api } from '@/lib/api';
import { ChatThread, Message } from '@/types';

// Mock dependencies
jest.mock('@/lib/api');
const mockApi = api as jest.Mocked<typeof api>;

// Explicitly cast nested mock objects for proper TypeScript support
const mockChatThreads = mockApi.chatThreads as jest.Mocked<typeof api.chatThreads>;
const mockChat = mockApi.chat as jest.Mocked<typeof api.chat>;

// Mock window.confirm
const mockConfirm = jest.fn();
global.confirm = mockConfirm;

// Mock console methods to avoid noise in tests
const originalConsoleError = console.error;
const mockConsoleError = jest.fn();
console.error = mockConsoleError;

describe('chatSlice', () => {
  // Helper to create mock chat threads
  const createMockChatThread = (overrides?: Partial<ChatThread>): ChatThread => ({
    id: 'thread-1',
    gameId: 'game-1',
    userId: 'user-1',
    title: 'Test Thread',
    status: 'Active',
    createdAt: '2024-01-01T00:00:00Z',
    lastMessageAt: null,
    messageCount: 0,
    messages: [],
    ...overrides,
  });

  // Helper to create mock messages
  const createMockMessage = (overrides?: Partial<Message>): Message => ({
    id: 'msg-1',
    role: 'user' as const,
    content: 'Test message',
    timestamp: new Date('2024-01-01T00:00:00Z'),
    ...overrides,
  });

  beforeEach(() => {
    // Reset store to clean state
    useChatStore.setState({
      chatsByGame: {},
      activeChatIds: {},
      messagesByChat: {},
      selectedGameId: null,
      selectedAgentId: null,
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
    });

    // Clear all mocks
    jest.clearAllMocks();
    mockConsoleError.mockClear();
  });

  afterAll(() => {
    console.error = originalConsoleError;
  });

  // ============================================================================
  // State Initialization
  // ============================================================================
  describe('Initial State', () => {
    it('should initialize with empty state', () => {
      const state = useChatStore.getState();
      expect(state.chatsByGame).toEqual({});
      expect(state.activeChatIds).toEqual({});
    });
  });

  // ============================================================================
  // loadChats
  // ============================================================================
  describe('loadChats', () => {
    it('should load chats successfully for a game', async () => {
      const mockChats: ChatThread[] = [
        createMockChatThread({ id: 'thread-1', title: 'Chat 1' }),
        createMockChatThread({ id: 'thread-2', title: 'Chat 2' }),
      ];

      mockChatThreads.getByGame.mockResolvedValue(mockChats);

      // Mock setLoading and setError
      const setLoadingSpy = jest.spyOn(useChatStore.getState(), 'setLoading');
      const setErrorSpy = jest.spyOn(useChatStore.getState(), 'setError');

      await useChatStore.getState().loadChats('game-1');

      // Verify API called with correct game ID
      expect(mockChatThreads.getByGame).toHaveBeenCalledWith('game-1');

      // Verify state updated correctly
      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1']).toEqual(mockChats);

      // Verify loading states
      expect(setLoadingSpy).toHaveBeenCalledWith('chats', true);
      expect(setLoadingSpy).toHaveBeenCalledWith('chats', false);
      expect(setErrorSpy).toHaveBeenCalledWith(null);
    });

    it('should handle null response from API', async () => {
      mockChatThreads.getByGame.mockResolvedValue(null as any);

      await useChatStore.getState().loadChats('game-1');

      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1']).toEqual([]);
    });

    it('should handle empty array response', async () => {
      mockChatThreads.getByGame.mockResolvedValue([]);

      await useChatStore.getState().loadChats('game-1');

      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1']).toEqual([]);
    });

    it('should handle API errors', async () => {
      const error = new Error('Network error');
      mockChatThreads.getByGame.mockRejectedValue(error);

      const setErrorSpy = jest.spyOn(useChatStore.getState(), 'setError');

      await useChatStore.getState().loadChats('game-1');

      // Verify error set in state
      expect(setErrorSpy).toHaveBeenCalledWith('Errore nel caricamento delle chat');

      // Verify chatsByGame set to empty array on error
      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1']).toEqual([]);
    });

    it('should set loading state correctly during async operation', async () => {
      mockChatThreads.getByGame.mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve([]), 10))
      );

      const setLoadingSpy = jest.spyOn(useChatStore.getState(), 'setLoading');

      const loadPromise = useChatStore.getState().loadChats('game-1');

      // Verify loading set to true immediately
      expect(setLoadingSpy).toHaveBeenCalledWith('chats', true);

      await loadPromise;

      // Verify loading set to false after completion
      expect(setLoadingSpy).toHaveBeenCalledWith('chats', false);
    });
  });

  // ============================================================================
  // createChat
  // ============================================================================
  describe('createChat', () => {
    it('should create a new chat successfully', async () => {
      const newThread = createMockChatThread({ id: 'new-thread', title: 'New Chat' });

      useChatStore.setState({
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
        chatsByGame: {},
      });

      mockChatThreads.create.mockResolvedValue(newThread);

      await useChatStore.getState().createChat();

      // Verify API called with correct parameters
      expect(mockChatThreads.create).toHaveBeenCalledWith({
        gameId: 'game-1',
        title: null,
        initialMessage: null,
      });

      // Verify new thread added to state
      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1']).toEqual([newThread]);
      expect(state.activeChatIds['game-1']).toBe('new-thread');
      expect(state.messagesByChat['new-thread']).toEqual([]);
    });

    it('should add new thread to beginning of existing threads', async () => {
      const existingThread = createMockChatThread({ id: 'existing', title: 'Existing' });
      const newThread = createMockChatThread({ id: 'new', title: 'New' });

      useChatStore.setState({
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
        chatsByGame: { 'game-1': [existingThread] },
      });

      mockChatThreads.create.mockResolvedValue(newThread);

      await useChatStore.getState().createChat();

      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1']).toEqual([newThread, existingThread]);
    });

    it('should return early if game not selected', async () => {
      useChatStore.setState({
        selectedGameId: null,
        selectedAgentId: 'agent-1',
      });

      const setErrorSpy = jest.spyOn(useChatStore.getState(), 'setError');

      await useChatStore.getState().createChat();

      expect(setErrorSpy).toHaveBeenCalledWith('Seleziona un gioco e un agente');
      expect(mockChatThreads.create).not.toHaveBeenCalled();
    });

    it('should return early if agent not selected', async () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        selectedAgentId: null,
      });

      const setErrorSpy = jest.spyOn(useChatStore.getState(), 'setError');

      await useChatStore.getState().createChat();

      expect(setErrorSpy).toHaveBeenCalledWith('Seleziona un gioco e un agente');
      expect(mockChatThreads.create).not.toHaveBeenCalled();
    });

    it('should handle API errors during creation', async () => {
      const error = new Error('Creation failed');
      mockChatThreads.create.mockRejectedValue(error);

      useChatStore.setState({
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
      });

      const setErrorSpy = jest.spyOn(useChatStore.getState(), 'setError');

      await useChatStore.getState().createChat();

      expect(setErrorSpy).toHaveBeenCalledWith('Errore nella creazione della chat');
    });

    it('should auto-archive oldest thread when limit exceeded', async () => {
      // NOTE: Due to implementation detail, auto-archive checks OLD state (before adding new thread)
      // So we need 6+ threads ALREADY to trigger auto-archive
      const oldestThread = createMockChatThread({
        id: 'oldest',
        createdAt: '2024-01-01T00:00:00Z',
        lastMessageAt: null,
        status: 'Active',
      });

      const activeThreads = [
        oldestThread,
        createMockChatThread({ id: 'thread-2', createdAt: '2024-01-02T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'thread-3', createdAt: '2024-01-03T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'thread-4', createdAt: '2024-01-04T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'thread-5', createdAt: '2024-01-05T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'thread-6', createdAt: '2024-01-06T00:00:00Z', lastMessageAt: null, status: 'Active' }),
      ];

      const newThread = createMockChatThread({ id: 'new', createdAt: '2024-01-07T00:00:00Z', status: 'Active' });

      useChatStore.setState({
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
        chatsByGame: { 'game-1': activeThreads },
        activeChatIds: { 'game-1': 'thread-6' }, // Current active thread
      });

      mockChatThreads.create.mockResolvedValue(newThread);
      mockChatThreads.close.mockResolvedValue({} as any);
      mockChatThreads.getByGame.mockResolvedValue([...activeThreads, newThread]);

      const loadChatsSpy = jest.spyOn(useChatStore.getState(), 'loadChats');

      await useChatStore.getState().createChat();

      // Verify oldest thread was archived (not the active one)
      expect(mockChatThreads.close).toHaveBeenCalledWith('oldest');
      expect(loadChatsSpy).toHaveBeenCalledWith('game-1');
    });

    it('should not auto-archive the currently active thread', async () => {
      // NOTE: Need 6+ threads to trigger auto-archive
      const activeThread = createMockChatThread({
        id: 'active',
        createdAt: '2024-01-01T00:00:00Z', // Oldest
        lastMessageAt: null,
        status: 'Active',
      });

      const activeThreads = [
        activeThread,
        createMockChatThread({ id: 'thread-2', createdAt: '2024-01-02T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'thread-3', createdAt: '2024-01-03T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'thread-4', createdAt: '2024-01-04T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'thread-5', createdAt: '2024-01-05T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'thread-6', createdAt: '2024-01-06T00:00:00Z', lastMessageAt: null, status: 'Active' }),
      ];

      const newThread = createMockChatThread({ id: 'new', createdAt: '2024-01-07T00:00:00Z', status: 'Active' });

      useChatStore.setState({
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
        chatsByGame: { 'game-1': activeThreads },
        activeChatIds: { 'game-1': 'active' }, // Oldest is active
      });

      mockChatThreads.create.mockResolvedValue(newThread);
      mockChatThreads.close.mockResolvedValue({} as any);
      mockChatThreads.getByGame.mockResolvedValue([...activeThreads, newThread]);

      await useChatStore.getState().createChat();

      // Should archive thread-2 (next oldest), not 'active'
      expect(mockChatThreads.close).toHaveBeenCalledWith('thread-2');
    });

    it('should handle auto-archive errors gracefully', async () => {
      // NOTE: Need 6+ threads to trigger auto-archive
      const activeThreads = Array.from({ length: 6 }, (_, i) =>
        createMockChatThread({
          id: `thread-${i}`,
          createdAt: `2024-01-0${i + 1}T00:00:00Z`,
          lastMessageAt: null,
          status: 'Active',
        })
      );

      const newThread = createMockChatThread({ id: 'new', status: 'Active' });

      useChatStore.setState({
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
        chatsByGame: { 'game-1': activeThreads },
      });

      mockChatThreads.create.mockResolvedValue(newThread);
      mockChatThreads.close.mockRejectedValue(new Error('Archive failed'));

      await useChatStore.getState().createChat();

      // New thread should still be added despite archive error
      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1']).toContainEqual(newThread);
    });

    it('should use lastMessageAt for sorting when available', async () => {
      // NOTE: Need 6+ threads to trigger auto-archive
      const threadWithMessage = createMockChatThread({
        id: 'with-msg',
        createdAt: '2024-01-01T00:00:00Z',
        lastMessageAt: '2024-01-10T00:00:00Z', // Recent message
        status: 'Active',
      });

      const olderThread = createMockChatThread({
        id: 'older',
        createdAt: '2024-01-05T00:00:00Z',
        lastMessageAt: null,
        status: 'Active',
      });

      const activeThreads = [
        threadWithMessage,
        olderThread,
        createMockChatThread({ id: 't3', createdAt: '2024-01-03T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 't4', createdAt: '2024-01-04T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 't5', createdAt: '2024-01-06T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 't6', createdAt: '2024-01-07T00:00:00Z', lastMessageAt: null, status: 'Active' }),
      ];

      const newThread = createMockChatThread({ id: 'new', status: 'Active' });

      useChatStore.setState({
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
        chatsByGame: { 'game-1': activeThreads },
      });

      mockChatThreads.create.mockResolvedValue(newThread);
      mockChatThreads.close.mockResolvedValue({} as any);
      mockChatThreads.getByGame.mockResolvedValue([...activeThreads, newThread]);

      await useChatStore.getState().createChat();

      // Should archive t3 (oldest by createdAt, no lastMessageAt)
      // NOT threadWithMessage (has recent lastMessageAt)
      expect(mockChatThreads.close).toHaveBeenCalledWith('t3');
    });

    it('should not auto-archive closed threads', async () => {
      const closedThread = createMockChatThread({
        id: 'closed',
        createdAt: '2024-01-01T00:00:00Z',
        status: 'Closed',
      });

      const activeThreads = [
        closedThread,
        createMockChatThread({ id: 'thread-2', createdAt: '2024-01-02T00:00:00Z', status: 'Active' }),
        createMockChatThread({ id: 'thread-3', createdAt: '2024-01-03T00:00:00Z', status: 'Active' }),
        createMockChatThread({ id: 'thread-4', createdAt: '2024-01-04T00:00:00Z', status: 'Active' }),
        createMockChatThread({ id: 'thread-5', createdAt: '2024-01-05T00:00:00Z', status: 'Active' }),
      ];

      const newThread = createMockChatThread({ id: 'new' });

      useChatStore.setState({
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
        chatsByGame: { 'game-1': activeThreads },
      });

      mockChatThreads.create.mockResolvedValue(newThread);
      mockChatThreads.close.mockResolvedValue({} as any);

      await useChatStore.getState().createChat();

      // Should not attempt to archive (only 4 active threads + 1 new = 5)
      expect(mockChatThreads.close).not.toHaveBeenCalled();
    });

    it('should set loading states correctly', async () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
      });

      mockChatThreads.create.mockResolvedValue(createMockChatThread());

      const setLoadingSpy = jest.spyOn(useChatStore.getState(), 'setLoading');

      await useChatStore.getState().createChat();

      expect(setLoadingSpy).toHaveBeenCalledWith('creating', true);
      expect(setLoadingSpy).toHaveBeenCalledWith('creating', false);
    });
  });

  // ============================================================================
  // deleteChat
  // ============================================================================
  describe('deleteChat', () => {
    it('should delete a chat successfully', async () => {
      const thread1 = createMockChatThread({ id: 'thread-1' });
      const thread2 = createMockChatThread({ id: 'thread-2' });

      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: { 'game-1': [thread1, thread2] },
        messagesByChat: { 'thread-1': [createMockMessage()] },
      });

      mockConfirm.mockReturnValue(true);
      mockApi.delete.mockResolvedValue({} as any);

      await useChatStore.getState().deleteChat('thread-1');

      // Verify confirmation dialog shown
      expect(mockConfirm).toHaveBeenCalledWith('Sei sicuro di voler eliminare questa chat?');

      // Verify API called
      expect(mockApi.delete).toHaveBeenCalledWith('/api/v1/chats/thread-1');

      // Verify thread removed from state
      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1']).toEqual([thread2]);
      expect(state.messagesByChat['thread-1']).toBeUndefined();
    });

    it('should return early if user cancels confirmation', async () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: { 'game-1': [createMockChatThread({ id: 'thread-1' })] },
      });

      mockConfirm.mockReturnValue(false);

      await useChatStore.getState().deleteChat('thread-1');

      expect(mockApi.delete).not.toHaveBeenCalled();
    });

    it('should return early if no game selected', async () => {
      useChatStore.setState({
        selectedGameId: null,
      });

      await useChatStore.getState().deleteChat('thread-1');

      expect(mockConfirm).not.toHaveBeenCalled();
      expect(mockApi.delete).not.toHaveBeenCalled();
    });

    it('should clear active chat if deleted chat was active', async () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: { 'game-1': [createMockChatThread({ id: 'thread-1' })] },
        activeChatIds: { 'game-1': 'thread-1' },
      });

      mockConfirm.mockReturnValue(true);
      mockApi.delete.mockResolvedValue({} as any);

      await useChatStore.getState().deleteChat('thread-1');

      const state = useChatStore.getState();
      expect(state.activeChatIds['game-1']).toBeNull();
    });

    it('should not clear active chat if different chat was deleted', async () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: {
          'game-1': [
            createMockChatThread({ id: 'thread-1' }),
            createMockChatThread({ id: 'thread-2' }),
          ],
        },
        activeChatIds: { 'game-1': 'thread-2' },
      });

      mockConfirm.mockReturnValue(true);
      mockApi.delete.mockResolvedValue({} as any);

      await useChatStore.getState().deleteChat('thread-1');

      const state = useChatStore.getState();
      expect(state.activeChatIds['game-1']).toBe('thread-2');
    });

    it('should handle API errors during deletion', async () => {
      const error = new Error('Deletion failed');
      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: { 'game-1': [createMockChatThread({ id: 'thread-1' })] },
      });

      mockConfirm.mockReturnValue(true);
      mockApi.delete.mockRejectedValue(error);

      const setErrorSpy = jest.spyOn(useChatStore.getState(), 'setError');

      await useChatStore.getState().deleteChat('thread-1');

      expect(setErrorSpy).toHaveBeenCalledWith("Errore nell'eliminazione della chat");
    });

    it('should set loading states correctly', async () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: { 'game-1': [createMockChatThread({ id: 'thread-1' })] },
      });

      mockConfirm.mockReturnValue(true);
      mockApi.delete.mockResolvedValue({} as any);

      const setLoadingSpy = jest.spyOn(useChatStore.getState(), 'setLoading');

      await useChatStore.getState().deleteChat('thread-1');

      expect(setLoadingSpy).toHaveBeenCalledWith('deleting', true);
      expect(setLoadingSpy).toHaveBeenCalledWith('deleting', false);
    });

    it('should handle empty chatsByGame gracefully', async () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: {},
      });

      mockConfirm.mockReturnValue(true);
      mockApi.delete.mockResolvedValue({} as any);

      await useChatStore.getState().deleteChat('thread-1');

      // Should not throw error
      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1']).toEqual([]);
    });
  });

  // ============================================================================
  // selectChat
  // ============================================================================
  describe('selectChat', () => {
    it('should select a chat and load its messages', async () => {
      const mockMessages = [createMockMessage({ id: 'msg-1' })];

      useChatStore.setState({
        selectedGameId: 'game-1',
      });

      const loadMessagesSpy = jest.fn().mockResolvedValue(undefined);
      useChatStore.setState({ loadMessages: loadMessagesSpy } as any);

      await useChatStore.getState().selectChat('thread-1');

      // Verify active chat set
      const state = useChatStore.getState();
      expect(state.activeChatIds['game-1']).toBe('thread-1');

      // Verify loadMessages called
      expect(loadMessagesSpy).toHaveBeenCalledWith('thread-1');
    });

    it('should return early if no game selected', async () => {
      useChatStore.setState({
        selectedGameId: null,
      });

      const loadMessagesSpy = jest.fn();
      useChatStore.setState({ loadMessages: loadMessagesSpy } as any);

      await useChatStore.getState().selectChat('thread-1');

      expect(loadMessagesSpy).not.toHaveBeenCalled();
    });

    it('should update active chat even if loadMessages fails', async () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
      });

      const loadMessagesSpy = jest.fn().mockRejectedValue(new Error('Load failed'));
      useChatStore.setState({ loadMessages: loadMessagesSpy } as any);

      await expect(useChatStore.getState().selectChat('thread-1')).rejects.toThrow('Load failed');

      // Active chat should still be set
      const state = useChatStore.getState();
      expect(state.activeChatIds['game-1']).toBe('thread-1');
    });
  });

  // ============================================================================
  // updateChatTitle
  // ============================================================================
  describe('updateChatTitle', () => {
    it('should update chat title successfully', () => {
      const thread = createMockChatThread({ id: 'thread-1', title: 'Old Title' });

      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: { 'game-1': [thread] },
      });

      useChatStore.getState().updateChatTitle('thread-1', 'New Title');

      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1'][0].title).toBe('New Title');
    });

    it('should return early if no game selected', () => {
      const thread = createMockChatThread({ id: 'thread-1', title: 'Old Title' });

      useChatStore.setState({
        selectedGameId: null,
        chatsByGame: { 'game-1': [thread] },
      });

      useChatStore.getState().updateChatTitle('thread-1', 'New Title');

      // Title should not change
      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1'][0].title).toBe('Old Title');
    });

    it('should handle missing chat thread gracefully', () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: { 'game-1': [] },
      });

      // Should not throw error
      expect(() => {
        useChatStore.getState().updateChatTitle('nonexistent', 'New Title');
      }).not.toThrow();
    });

    it('should handle empty chatsByGame gracefully', () => {
      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: {},
      });

      // Should not throw error
      expect(() => {
        useChatStore.getState().updateChatTitle('thread-1', 'New Title');
      }).not.toThrow();
    });

    it('should update correct thread in multiple threads', () => {
      const thread1 = createMockChatThread({ id: 'thread-1', title: 'Title 1' });
      const thread2 = createMockChatThread({ id: 'thread-2', title: 'Title 2' });
      const thread3 = createMockChatThread({ id: 'thread-3', title: 'Title 3' });

      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: { 'game-1': [thread1, thread2, thread3] },
      });

      useChatStore.getState().updateChatTitle('thread-2', 'Updated Title');

      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1'][0].title).toBe('Title 1');
      expect(state.chatsByGame['game-1'][1].title).toBe('Updated Title');
      expect(state.chatsByGame['game-1'][2].title).toBe('Title 3');
    });

    it('should handle null title', () => {
      const thread = createMockChatThread({ id: 'thread-1', title: 'Old Title' });

      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: { 'game-1': [thread] },
      });

      useChatStore.getState().updateChatTitle('thread-1', null as any);

      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1'][0].title).toBeNull();
    });
  });

  // ============================================================================
  // Integration Tests
  // ============================================================================
  describe('Integration Scenarios', () => {
    it('should handle complete chat lifecycle', async () => {
      // Setup
      useChatStore.setState({
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
      });

      // Load existing chats
      const existingChats = [createMockChatThread({ id: 'existing' })];
      mockChatThreads.getByGame.mockResolvedValue(existingChats);
      await useChatStore.getState().loadChats('game-1');

      let state = useChatStore.getState();
      expect(state.chatsByGame['game-1']).toEqual(existingChats);

      // Create new chat
      const newChat = createMockChatThread({ id: 'new' });
      mockChatThreads.create.mockResolvedValue(newChat);
      await useChatStore.getState().createChat();

      state = useChatStore.getState();
      expect(state.chatsByGame['game-1']).toHaveLength(2);
      expect(state.activeChatIds['game-1']).toBe('new');

      // Update title
      useChatStore.getState().updateChatTitle('new', 'Updated Title');
      state = useChatStore.getState();
      expect(state.chatsByGame['game-1'][0].title).toBe('Updated Title');

      // Delete chat
      mockConfirm.mockReturnValue(true);
      mockApi.delete.mockResolvedValue({} as any);
      await useChatStore.getState().deleteChat('new');

      state = useChatStore.getState();
      expect(state.chatsByGame['game-1']).toHaveLength(1);
      expect(state.activeChatIds['game-1']).toBeNull();
    });

    it('should maintain separate state for different games', async () => {
      const game1Chats = [createMockChatThread({ id: 'g1-thread', gameId: 'game-1' })];
      const game2Chats = [createMockChatThread({ id: 'g2-thread', gameId: 'game-2' })];

      mockChatThreads.getByGame.mockResolvedValueOnce(game1Chats);
      await useChatStore.getState().loadChats('game-1');

      mockChatThreads.getByGame.mockResolvedValueOnce(game2Chats);
      await useChatStore.getState().loadChats('game-2');

      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1']).toEqual(game1Chats);
      expect(state.chatsByGame['game-2']).toEqual(game2Chats);
    });
  });

  // ============================================================================
  // Edge Cases
  // ============================================================================
  describe('Edge Cases', () => {
    it('should handle concurrent loadChats calls', async () => {
      const chats = [createMockChatThread()];
      mockChatThreads.getByGame.mockResolvedValue(chats);

      // Fire multiple concurrent calls
      await Promise.all([
        useChatStore.getState().loadChats('game-1'),
        useChatStore.getState().loadChats('game-1'),
        useChatStore.getState().loadChats('game-1'),
      ]);

      // Should handle gracefully without race conditions
      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1']).toBeDefined();
    });

    it('should handle very large title strings', () => {
      const longTitle = 'A'.repeat(10000);
      const thread = createMockChatThread({ id: 'thread-1', title: 'Short' });

      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: { 'game-1': [thread] },
      });

      useChatStore.getState().updateChatTitle('thread-1', longTitle);

      const state = useChatStore.getState();
      expect(state.chatsByGame['game-1'][0].title).toBe(longTitle);
    });

    it('should handle special characters in IDs', async () => {
      const specialId = 'thread-with-special-chars-!@#$%^&*()';
      useChatStore.setState({
        selectedGameId: 'game-1',
        chatsByGame: { 'game-1': [createMockChatThread({ id: specialId })] },
      });

      mockConfirm.mockReturnValue(true);
      mockApi.delete.mockResolvedValue({} as any);

      await useChatStore.getState().deleteChat(specialId);

      expect(mockApi.delete).toHaveBeenCalledWith(`/api/v1/chats/${specialId}`);
    });
  });
});
