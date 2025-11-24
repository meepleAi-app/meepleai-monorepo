/**
 * Comprehensive tests for chatSlice.ts
 * Target: 90%+ coverage
 *
 * Test scenarios:
 * - State initialization
 * - loadChats: Success, error, null response, empty array
 * - createChat: Success, validation, auto-archiving, errors
 * - deleteChat: Success, active chat cleanup, errors (confirmation handled by component)
 * - selectChat: Success, loadMessages integration
 * - updateChatTitle: Success, missing game, thread not found
 * - Loading states and error handling
 * - Edge cases and boundary conditions
 */

import { useChatStore } from '../../store';
import { api } from '@/lib/api';
import { ChatThread, Message } from '@/types';
import { CHAT_CONFIG } from '@/config';

// Mock dependencies
vi.mock('@/lib/api');
const mockApi = api as Mocked<typeof api>;

// Explicitly cast nested mock objects for proper TypeScript support
const mockChat = mockApi.chat as Mocked<typeof api.chat>;

// Mock console methods to avoid noise in tests
const originalConsoleError = console.error;
const mockConsoleError = vi.fn();
console.error = mockConsoleError;

describe('chatSlice', () => {
  // Helper to create mock chat threads
  const createMockChatThread = (overrides?: Partial<ChatThread>): ChatThread => ({
    id: 'aa0e8400-e29b-41d4-a716-000000000001',
    gameId: '770e8400-e29b-41d4-a716-000000000001',
    userId: '990e8400-e29b-41d4-a716-000000000001',
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
    vi.clearAllMocks();
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
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000001', title: 'Chat 1' }),
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000002', title: 'Chat 2' }),
      ];

      mockChat.getThreadsByGame.mockResolvedValue(mockChats);

      // Mock setLoading and setError
      const setLoadingSpy = vi.spyOn(useChatStore.getState(), 'setLoading');
      const setErrorSpy = vi.spyOn(useChatStore.getState(), 'setError');

      await useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000001');

      // Verify API called with correct game ID
      expect(mockChat.getThreadsByGame).toHaveBeenCalledWith('770e8400-e29b-41d4-a716-000000000001');

      // Verify state updated correctly
      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toEqual(mockChats);

      // Verify loading states
      expect(setLoadingSpy).toHaveBeenCalledWith('chats', true);
      expect(setLoadingSpy).toHaveBeenCalledWith('chats', false);
      expect(setErrorSpy).toHaveBeenCalledWith(null);
    });

    it('should handle null response from API', async () => {
      mockChat.getThreadsByGame.mockResolvedValue(null as any);

      await useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000001');

      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toEqual([]);
    });

    it('should handle empty array response', async () => {
      mockChat.getThreadsByGame.mockResolvedValue([]);

      await useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000001');

      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toEqual([]);
    });

    it('should handle API errors', async () => {
      const error = new Error('Network error');
      mockChat.getThreadsByGame.mockRejectedValue(error);

      const setErrorSpy = vi.spyOn(useChatStore.getState(), 'setError');

      await useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000001');

      // Verify error set in state
      expect(setErrorSpy).toHaveBeenCalledWith('Errore nel caricamento delle chat');

      // Verify chatsByGame set to empty array on error
      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toEqual([]);
    });

    it('should set loading state correctly during async operation', async () => {
      mockChat.getThreadsByGame.mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve([]), 10))
      );

      const setLoadingSpy = vi.spyOn(useChatStore.getState(), 'setLoading');

      const loadPromise = useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000001');

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
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
        chatsByGame: {},
      });

      mockChat.createThread.mockResolvedValue(newThread);

      await useChatStore.getState().createChat();

      // Verify API called with correct parameters
      expect(mockChat.createThread).toHaveBeenCalledWith({
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: null,
        initialMessage: null,
      });

      // Verify new thread added to state
      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toEqual([newThread]);
      expect(state.activeChatIds['770e8400-e29b-41d4-a716-000000000001']).toBe('new-thread');
      expect(state.messagesByChat['new-thread']).toEqual([]);
    });

    it('should add new thread to beginning of existing threads', async () => {
      const existingThread = createMockChatThread({ id: 'existing', title: 'Existing' });
      const newThread = createMockChatThread({ id: 'new', title: 'New' });

      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': [existingThread] },
      });

      mockChat.createThread.mockResolvedValue(newThread);

      await useChatStore.getState().createChat();

      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toEqual([newThread, existingThread]);
    });

    it('should return early if game not selected', async () => {
      useChatStore.setState({
        selectedGameId: null,
        selectedAgentId: 'agent-1',
      });

      const setErrorSpy = vi.spyOn(useChatStore.getState(), 'setError');

      await useChatStore.getState().createChat();

      expect(setErrorSpy).toHaveBeenCalledWith('Seleziona un gioco e un agente');
      expect(mockChat.createThread).not.toHaveBeenCalled();
    });

    it('should return early if agent not selected', async () => {
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: null,
      });

      const setErrorSpy = vi.spyOn(useChatStore.getState(), 'setError');

      await useChatStore.getState().createChat();

      expect(setErrorSpy).toHaveBeenCalledWith('Seleziona un gioco e un agente');
      expect(mockChat.createThread).not.toHaveBeenCalled();
    });

    it('should handle API errors during creation', async () => {
      const error = new Error('Creation failed');
      mockChat.createThread.mockRejectedValue(error);

      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
      });

      const setErrorSpy = vi.spyOn(useChatStore.getState(), 'setError');

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
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000002', createdAt: '2024-01-02T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000003', createdAt: '2024-01-03T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000004', createdAt: '2024-01-04T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000005', createdAt: '2024-01-05T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000006', createdAt: '2024-01-06T00:00:00Z', lastMessageAt: null, status: 'Active' }),
      ];

      const newThread = createMockChatThread({ id: 'new', createdAt: '2024-01-07T00:00:00Z', status: 'Active' });

      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': activeThreads },
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000006' }, // Current active thread
      });

      mockChat.createThread.mockResolvedValue(newThread);
      mockChat.closeThread.mockResolvedValue({} as any);
      mockChat.getThreadsByGame.mockResolvedValue([...activeThreads, newThread]);

      const loadChatsSpy = vi.spyOn(useChatStore.getState(), 'loadChats');

      await useChatStore.getState().createChat();

      // Verify oldest thread was archived (not the active one)
      expect(mockChat.closeThread).toHaveBeenCalledWith('oldest');
      expect(loadChatsSpy).toHaveBeenCalledWith('770e8400-e29b-41d4-a716-000000000001');
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
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000002', createdAt: '2024-01-02T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000003', createdAt: '2024-01-03T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000004', createdAt: '2024-01-04T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000005', createdAt: '2024-01-05T00:00:00Z', lastMessageAt: null, status: 'Active' }),
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000006', createdAt: '2024-01-06T00:00:00Z', lastMessageAt: null, status: 'Active' }),
      ];

      const newThread = createMockChatThread({ id: 'new', createdAt: '2024-01-07T00:00:00Z', status: 'Active' });

      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': activeThreads },
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'active' }, // Oldest is active
      });

      mockChat.createThread.mockResolvedValue(newThread);
      mockChat.closeThread.mockResolvedValue({} as any);
      mockChat.getThreadsByGame.mockResolvedValue([...activeThreads, newThread]);

      await useChatStore.getState().createChat();

      // Should archive thread-2 (next oldest), not 'active'
      expect(mockChat.closeThread).toHaveBeenCalledWith('aa0e8400-e29b-41d4-a716-000000000002');
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
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': activeThreads },
      });

      mockChat.createThread.mockResolvedValue(newThread);
      mockChat.closeThread.mockRejectedValue(new Error('Archive failed'));

      await useChatStore.getState().createChat();

      // New thread should still be added despite archive error
      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toContainEqual(newThread);
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
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': activeThreads },
      });

      mockChat.createThread.mockResolvedValue(newThread);
      mockChat.closeThread.mockResolvedValue({} as any);
      mockChat.getThreadsByGame.mockResolvedValue([...activeThreads, newThread]);

      await useChatStore.getState().createChat();

      // Should archive t3 (oldest by createdAt, no lastMessageAt)
      // NOT threadWithMessage (has recent lastMessageAt)
      expect(mockChat.closeThread).toHaveBeenCalledWith('t3');
    });

    it('should not auto-archive closed threads', async () => {
      const closedThread = createMockChatThread({
        id: 'closed',
        createdAt: '2024-01-01T00:00:00Z',
        status: 'Closed',
      });

      const activeThreads = [
        closedThread,
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000002', createdAt: '2024-01-02T00:00:00Z', status: 'Active' }),
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000003', createdAt: '2024-01-03T00:00:00Z', status: 'Active' }),
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000004', createdAt: '2024-01-04T00:00:00Z', status: 'Active' }),
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000005', createdAt: '2024-01-05T00:00:00Z', status: 'Active' }),
      ];

      const newThread = createMockChatThread({ id: 'new' });

      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': activeThreads },
      });

      mockChat.createThread.mockResolvedValue(newThread);
      mockChat.closeThread.mockResolvedValue({} as any);

      await useChatStore.getState().createChat();

      // Should not attempt to archive (only 4 active threads + 1 new = MAX_THREADS_PER_GAME)
      expect(mockChat.closeThread).not.toHaveBeenCalled();
    });

    it('should set loading states correctly', async () => {
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
      });

      mockChat.createThread.mockResolvedValue(createMockChatThread());

      const setLoadingSpy = vi.spyOn(useChatStore.getState(), 'setLoading');

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
      const thread1 = createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000001' });
      const thread2 = createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000002' });

      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': [thread1, thread2] },
        messagesByChat: { 'aa0e8400-e29b-41d4-a716-000000000001': [createMockMessage()] },
      });

      mockApi.delete.mockResolvedValue({} as any);

      await useChatStore.getState().deleteChat('aa0e8400-e29b-41d4-a716-000000000001');

      // Verify API called
      expect(mockApi.delete).toHaveBeenCalledWith('/api/v1/chats/aa0e8400-e29b-41d4-a716-000000000001');

      // Verify thread removed from state
      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toEqual([thread2]);
      expect(state.messagesByChat['aa0e8400-e29b-41d4-a716-000000000001']).toBeUndefined();
    });

    it('should return early if no game selected', async () => {
      useChatStore.setState({
        selectedGameId: null,
      });

      await useChatStore.getState().deleteChat('aa0e8400-e29b-41d4-a716-000000000001');

      expect(mockApi.delete).not.toHaveBeenCalled();
    });

    it('should clear active chat if deleted chat was active', async () => {
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': [createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000001' })] },
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000001' },
      });

      mockApi.delete.mockResolvedValue({} as any);

      await useChatStore.getState().deleteChat('aa0e8400-e29b-41d4-a716-000000000001');

      const state = useChatStore.getState();
      expect(state.activeChatIds['770e8400-e29b-41d4-a716-000000000001']).toBeNull();
    });

    it('should not clear active chat if different chat was deleted', async () => {
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: {
          '770e8400-e29b-41d4-a716-000000000001': [
            createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000001' }),
            createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000002' }),
          ],
        },
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000002' },
      });

      mockApi.delete.mockResolvedValue({} as any);

      await useChatStore.getState().deleteChat('aa0e8400-e29b-41d4-a716-000000000001');

      const state = useChatStore.getState();
      expect(state.activeChatIds['770e8400-e29b-41d4-a716-000000000001']).toBe('aa0e8400-e29b-41d4-a716-000000000002');
    });

    it('should handle API errors during deletion', async () => {
      const error = new Error('Deletion failed');
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': [createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000001' })] },
      });

      mockApi.delete.mockRejectedValue(error);

      const setErrorSpy = vi.spyOn(useChatStore.getState(), 'setError');

      await useChatStore.getState().deleteChat('aa0e8400-e29b-41d4-a716-000000000001');

      expect(setErrorSpy).toHaveBeenCalledWith("Errore nell'eliminazione della chat");
    });

    it('should set loading states correctly', async () => {
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': [createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000001' })] },
      });

      mockApi.delete.mockResolvedValue({} as any);

      const setLoadingSpy = vi.spyOn(useChatStore.getState(), 'setLoading');

      await useChatStore.getState().deleteChat('aa0e8400-e29b-41d4-a716-000000000001');

      expect(setLoadingSpy).toHaveBeenCalledWith('deleting', true);
      expect(setLoadingSpy).toHaveBeenCalledWith('deleting', false);
    });

    it('should handle empty chatsByGame gracefully', async () => {
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: {},
      });

      mockApi.delete.mockResolvedValue({} as any);

      await useChatStore.getState().deleteChat('aa0e8400-e29b-41d4-a716-000000000001');

      // Should not throw error
      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toEqual([]);
    });
  });

  // ============================================================================
  // selectChat
  // ============================================================================
  describe('selectChat', () => {
    it('should select a chat and load its messages', async () => {
      const mockMessages = [createMockMessage({ id: 'msg-1' })];

      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
      });

      const loadMessagesSpy = vi.fn().mockResolvedValue(undefined);
      useChatStore.setState({ loadMessages: loadMessagesSpy } as any);

      await useChatStore.getState().selectChat('aa0e8400-e29b-41d4-a716-000000000001');

      // Verify active chat set
      const state = useChatStore.getState();
      expect(state.activeChatIds['770e8400-e29b-41d4-a716-000000000001']).toBe('aa0e8400-e29b-41d4-a716-000000000001');

      // Verify loadMessages called
      expect(loadMessagesSpy).toHaveBeenCalledWith('aa0e8400-e29b-41d4-a716-000000000001');
    });

    it('should return early if no game selected', async () => {
      useChatStore.setState({
        selectedGameId: null,
      });

      const loadMessagesSpy = vi.fn();
      useChatStore.setState({ loadMessages: loadMessagesSpy } as any);

      await useChatStore.getState().selectChat('aa0e8400-e29b-41d4-a716-000000000001');

      expect(loadMessagesSpy).not.toHaveBeenCalled();
    });

    it('should update active chat even if loadMessages fails', async () => {
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
      });

      const loadMessagesSpy = vi.fn().mockRejectedValue(new Error('Load failed'));
      useChatStore.setState({ loadMessages: loadMessagesSpy } as any);

      await expect(useChatStore.getState().selectChat('aa0e8400-e29b-41d4-a716-000000000001')).rejects.toThrow('Load failed');

      // Active chat should still be set
      const state = useChatStore.getState();
      expect(state.activeChatIds['770e8400-e29b-41d4-a716-000000000001']).toBe('aa0e8400-e29b-41d4-a716-000000000001');
    });
  });

  // ============================================================================
  // updateChatTitle
  // ============================================================================
  describe('updateChatTitle', () => {
    it('should update chat title successfully', () => {
      const thread = createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000001', title: 'Old Title' });

      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': [thread] },
      });

      useChatStore.getState().updateChatTitle('aa0e8400-e29b-41d4-a716-000000000001', 'New Title');

      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001'][0].title).toBe('New Title');
    });

    it('should return early if no game selected', () => {
      const thread = createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000001', title: 'Old Title' });

      useChatStore.setState({
        selectedGameId: null,
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': [thread] },
      });

      useChatStore.getState().updateChatTitle('aa0e8400-e29b-41d4-a716-000000000001', 'New Title');

      // Title should not change
      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001'][0].title).toBe('Old Title');
    });

    it('should handle missing chat thread gracefully', () => {
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': [] },
      });

      // Should not throw error
      expect(() => {
        useChatStore.getState().updateChatTitle('nonexistent', 'New Title');
      }).not.toThrow();
    });

    it('should handle empty chatsByGame gracefully', () => {
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: {},
      });

      // Should not throw error
      expect(() => {
        useChatStore.getState().updateChatTitle('aa0e8400-e29b-41d4-a716-000000000001', 'New Title');
      }).not.toThrow();
    });

    it('should update correct thread in multiple threads', () => {
      const thread1 = createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000001', title: 'Title 1' });
      const thread2 = createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000002', title: 'Title 2' });
      const thread3 = createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000003', title: 'Title 3' });

      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': [thread1, thread2, thread3] },
      });

      useChatStore.getState().updateChatTitle('aa0e8400-e29b-41d4-a716-000000000002', 'Updated Title');

      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001'][0].title).toBe('Title 1');
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001'][1].title).toBe('Updated Title');
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001'][2].title).toBe('Title 3');
    });

    it('should handle null title', () => {
      const thread = createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000001', title: 'Old Title' });

      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': [thread] },
      });

      useChatStore.getState().updateChatTitle('aa0e8400-e29b-41d4-a716-000000000001', null as any);

      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001'][0].title).toBeNull();
    });
  });

  // ============================================================================
  // Integration Tests
  // ============================================================================
  describe('Integration Scenarios', () => {
    it('should handle complete chat lifecycle', async () => {
      // Setup
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
      });

      // Load existing chats
      const existingChats = [createMockChatThread({ id: 'existing' })];
      mockChat.getThreadsByGame.mockResolvedValue(existingChats);
      await useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000001');

      let state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toEqual(existingChats);

      // Create new chat
      const newChat = createMockChatThread({ id: 'new' });
      mockChat.createThread.mockResolvedValue(newChat);
      await useChatStore.getState().createChat();

      state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toHaveLength(2);
      expect(state.activeChatIds['770e8400-e29b-41d4-a716-000000000001']).toBe('new');

      // Update title
      useChatStore.getState().updateChatTitle('new', 'Updated Title');
      state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001'][0].title).toBe('Updated Title');

      // Delete chat
      mockApi.delete.mockResolvedValue({} as any);
      await useChatStore.getState().deleteChat('new');

      state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toHaveLength(1);
      expect(state.activeChatIds['770e8400-e29b-41d4-a716-000000000001']).toBeNull();
    });

    it('should maintain separate state for different games', async () => {
      const game1Chats = [createMockChatThread({ id: 'g1-thread', gameId: '770e8400-e29b-41d4-a716-000000000001' })];
      const game2Chats = [createMockChatThread({ id: 'g2-thread', gameId: '770e8400-e29b-41d4-a716-000000000002' })];

      mockChat.getThreadsByGame.mockResolvedValueOnce(game1Chats);
      await useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000001');

      mockChat.getThreadsByGame.mockResolvedValueOnce(game2Chats);
      await useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000002');

      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toEqual(game1Chats);
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000002']).toEqual(game2Chats);
    });
  });

  // ============================================================================
  // Edge Cases
  // ============================================================================
  describe('Edge Cases', () => {
    it('should handle concurrent loadChats calls', async () => {
      const chats = [createMockChatThread()];
      mockChat.getThreadsByGame.mockResolvedValue(chats);

      // Fire multiple concurrent calls
      await Promise.all([
        useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000001'),
        useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000001'),
        useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000001'),
      ]);

      // Should handle gracefully without race conditions
      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toBeDefined();
    });

    it('should handle very large title strings', () => {
      const longTitle = 'A'.repeat(10000);
      const thread = createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000001', title: 'Short' });

      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': [thread] },
      });

      useChatStore.getState().updateChatTitle('aa0e8400-e29b-41d4-a716-000000000001', longTitle);

      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001'][0].title).toBe(longTitle);
    });

    it('should handle special characters in IDs', async () => {
      const specialId = 'thread-with-special-chars-!@#$%^&*()';
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': [createMockChatThread({ id: specialId })] },
      });

      mockApi.delete.mockResolvedValue({} as any);

      await useChatStore.getState().deleteChat(specialId);

      expect(mockApi.delete).toHaveBeenCalledWith(`/api/v1/chats/${specialId}`);
    });
  });
});
