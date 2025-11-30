/**
 * Tests for chatSlice CRUD Operations
 *
 * Coverage: loadChats, createChat, deleteChat, updateChatTitle
 * Target: 90%+ coverage
 */

import { useChatStore } from '../../store';
import { api } from '@/lib/api';
import {
  createMockChatThread,
  createMockMessage,
  setupConsoleMocks,
  resetChatStore,
  expectLoadingTransitions,
} from './chatSlice.test-helpers';

// Mock dependencies
vi.mock('@/lib/api');
const mockApi = api as Mocked<typeof api>;
const mockChat = mockApi.chat as Mocked<typeof api.chat>;

describe('chatSlice - CRUD Operations', () => {
  const consoleMocks = setupConsoleMocks();

  beforeEach(() => {
    resetChatStore(useChatStore);
    vi.clearAllMocks();
    consoleMocks.mockConsoleError.mockClear();
  });

  afterAll(() => {
    consoleMocks.restore();
  });

  describe('Initial State', () => {
    it('should initialize with empty state', () => {
      const state = useChatStore.getState();
      expect(state.chatsByGame).toEqual({});
      expect(state.activeChatIds).toEqual({});
    });
  });

  describe('loadChats', () => {
    it('should load chats successfully for a game', async () => {
      const mockChats = [
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000001', title: 'Chat 1' }),
        createMockChatThread({ id: 'aa0e8400-e29b-41d4-a716-000000000002', title: 'Chat 2' }),
      ];

      mockChat.getThreadsByGame.mockResolvedValue(mockChats);

      const setLoadingSpy = vi.spyOn(useChatStore.getState(), 'setLoading');
      const setErrorSpy = vi.spyOn(useChatStore.getState(), 'setError');

      await useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000001');

      expect(mockChat.getThreadsByGame).toHaveBeenCalledWith('770e8400-e29b-41d4-a716-000000000001');

      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toEqual(mockChats);

      expectLoadingTransitions(setLoadingSpy, 'chats');
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

      expect(setErrorSpy).toHaveBeenCalledWith('Errore nel caricamento delle chat');

      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toEqual([]);
    });

    it('should set loading state correctly during async operation', async () => {
      mockChat.getThreadsByGame.mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve([]), 10))
      );

      const setLoadingSpy = vi.spyOn(useChatStore.getState(), 'setLoading');

      const loadPromise = useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000001');

      expect(setLoadingSpy).toHaveBeenCalledWith('chats', true);

      await loadPromise;

      expect(setLoadingSpy).toHaveBeenCalledWith('chats', false);
    });
  });

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

      expect(mockChat.createThread).toHaveBeenCalledWith({
        gameId: '770e8400-e29b-41d4-a716-000000000001',
        title: null,
        initialMessage: null,
      });

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

    it('should set loading states correctly', async () => {
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
      });

      mockChat.createThread.mockResolvedValue(createMockChatThread());

      const setLoadingSpy = vi.spyOn(useChatStore.getState(), 'setLoading');

      await useChatStore.getState().createChat();

      expectLoadingTransitions(setLoadingSpy, 'creating');
    });
  });

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

      expect(mockApi.delete).toHaveBeenCalledWith('/api/v1/chats/aa0e8400-e29b-41d4-a716-000000000001');

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

      expectLoadingTransitions(setLoadingSpy, 'deleting');
    });

    it('should handle empty chatsByGame gracefully', async () => {
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: {},
      });

      mockApi.delete.mockResolvedValue({} as any);

      await useChatStore.getState().deleteChat('aa0e8400-e29b-41d4-a716-000000000001');

      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toEqual([]);
    });
  });

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

      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001'][0].title).toBe('Old Title');
    });

    it('should handle missing chat thread gracefully', () => {
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': [] },
      });

      expect(() => {
        useChatStore.getState().updateChatTitle('nonexistent', 'New Title');
      }).not.toThrow();
    });

    it('should handle empty chatsByGame gracefully', () => {
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        chatsByGame: {},
      });

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
});
