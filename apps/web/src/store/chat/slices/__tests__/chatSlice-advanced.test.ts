/**
 * Tests for chatSlice Advanced Features
 *
 * Coverage: Auto-archiving, selectChat, integration scenarios, edge cases
 * Target: 90%+ coverage
 */

import { useChatStore } from '../../store';
import { api } from '@/lib/api';
import { CHAT_CONFIG } from '@/config';
import {
  createMockChatThread,
  createMockChatThreads,
  setupConsoleMocks,
  resetChatStore,
} from './chatSlice.test-helpers';

// Mock dependencies
vi.mock('@/lib/api');
const mockApi = api as Mocked<typeof api>;
const mockChat = mockApi.chat as Mocked<typeof api.chat>;

describe('chatSlice - Advanced Features', () => {
  const consoleMocks = setupConsoleMocks();

  beforeEach(() => {
    resetChatStore(useChatStore);
    vi.clearAllMocks();
    consoleMocks.mockConsoleError.mockClear();
  });

  afterAll(() => {
    consoleMocks.restore();
  });

  describe('createChat - Auto-archiving', () => {
    it('should auto-archive oldest thread when limit exceeded', async () => {
      const oldestThread = createMockChatThread({
        id: 'oldest',
        createdAt: '2024-01-01T00:00:00Z',
        lastMessageAt: null,
        status: 'Active',
      });

      const activeThreads = [
        oldestThread,
        ...createMockChatThreads(5, { status: 'Active' }).map((t, i) => ({
          ...t,
          id: `aa0e8400-e29b-41d4-a716-00000000000${i + 2}`,
          createdAt: `2024-01-0${i + 2}T00:00:00Z`,
        })),
      ];

      const newThread = createMockChatThread({ id: 'new', createdAt: '2024-01-07T00:00:00Z', status: 'Active' });

      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': activeThreads },
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'aa0e8400-e29b-41d4-a716-000000000006' },
      });

      mockChat.createThread.mockResolvedValue(newThread);
      mockChat.closeThread.mockResolvedValue({} as any);
      mockChat.getThreadsByGame.mockResolvedValue([...activeThreads, newThread]);

      const loadChatsSpy = vi.spyOn(useChatStore.getState(), 'loadChats');

      await useChatStore.getState().createChat();

      expect(mockChat.closeThread).toHaveBeenCalledWith('oldest');
      expect(loadChatsSpy).toHaveBeenCalledWith('770e8400-e29b-41d4-a716-000000000001');
    });

    it('should not auto-archive the currently active thread', async () => {
      const activeThread = createMockChatThread({
        id: 'active',
        createdAt: '2024-01-01T00:00:00Z',
        lastMessageAt: null,
        status: 'Active',
      });

      const activeThreads = [
        activeThread,
        ...createMockChatThreads(5, { status: 'Active' }).map((t, i) => ({
          ...t,
          id: `aa0e8400-e29b-41d4-a716-00000000000${i + 2}`,
          createdAt: `2024-01-0${i + 2}T00:00:00Z`,
        })),
      ];

      const newThread = createMockChatThread({ id: 'new', createdAt: '2024-01-07T00:00:00Z', status: 'Active' });

      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': activeThreads },
        activeChatIds: { '770e8400-e29b-41d4-a716-000000000001': 'active' },
      });

      mockChat.createThread.mockResolvedValue(newThread);
      mockChat.closeThread.mockResolvedValue({} as any);
      mockChat.getThreadsByGame.mockResolvedValue([...activeThreads, newThread]);

      await useChatStore.getState().createChat();

      expect(mockChat.closeThread).toHaveBeenCalledWith('aa0e8400-e29b-41d4-a716-000000000002');
    });

    it('should handle auto-archive errors gracefully', async () => {
      const activeThreads = createMockChatThreads(6, { status: 'Active' });
      const newThread = createMockChatThread({ id: 'new', status: 'Active' });

      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
        selectedAgentId: 'agent-1',
        chatsByGame: { '770e8400-e29b-41d4-a716-000000000001': activeThreads },
      });

      mockChat.createThread.mockResolvedValue(newThread);
      mockChat.closeThread.mockRejectedValue(new Error('Archive failed'));

      await useChatStore.getState().createChat();

      const state = useChatStore.getState();
      expect(state.chatsByGame['770e8400-e29b-41d4-a716-000000000001']).toContainEqual(newThread);
    });

    it('should use lastMessageAt for sorting when available', async () => {
      const threadWithMessage = createMockChatThread({
        id: 'with-msg',
        createdAt: '2024-01-01T00:00:00Z',
        lastMessageAt: '2024-01-10T00:00:00Z',
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
        ...createMockChatThreads(4, { status: 'Active' }).map((t, i) => ({
          ...t,
          id: `t${i + 3}`,
          createdAt: `2024-01-0${i + 3}T00:00:00Z`,
        })),
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
        ...createMockChatThreads(4, { status: 'Active' }).map((t, i) => ({
          ...t,
          id: `aa0e8400-e29b-41d4-a716-00000000000${i + 2}`,
          createdAt: `2024-01-0${i + 2}T00:00:00Z`,
        })),
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

      expect(mockChat.closeThread).not.toHaveBeenCalled();
    });
  });

  describe('selectChat', () => {
    it('should select a chat and load its messages', async () => {
      useChatStore.setState({
        selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
      });

      const loadMessagesSpy = vi.fn().mockResolvedValue(undefined);
      useChatStore.setState({ loadMessages: loadMessagesSpy } as any);

      await useChatStore.getState().selectChat('aa0e8400-e29b-41d4-a716-000000000001');

      const state = useChatStore.getState();
      expect(state.activeChatIds['770e8400-e29b-41d4-a716-000000000001']).toBe('aa0e8400-e29b-41d4-a716-000000000001');

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

      const state = useChatStore.getState();
      expect(state.activeChatIds['770e8400-e29b-41d4-a716-000000000001']).toBe('aa0e8400-e29b-41d4-a716-000000000001');
    });
  });

  describe('Integration Scenarios', () => {
    it('should handle complete chat lifecycle', async () => {
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

  describe('Edge Cases', () => {
    it('should handle concurrent loadChats calls', async () => {
      const chats = [createMockChatThread()];
      mockChat.getThreadsByGame.mockResolvedValue(chats);

      await Promise.all([
        useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000001'),
        useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000001'),
        useChatStore.getState().loadChats('770e8400-e29b-41d4-a716-000000000001'),
      ]);

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
