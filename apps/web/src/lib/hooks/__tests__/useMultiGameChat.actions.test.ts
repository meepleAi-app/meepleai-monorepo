/**
 * useMultiGameChat Actions Tests
 *
 * Tests for actions: switchGame, loadChatHistory, createNewChat, deleteChat
 */
import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useMultiGameChat } from '../useMultiGameChat';
import { api } from '../../api';

// Mock the api module with chat client structure
vi.mock('../../api', () => ({
  api: {
    chat: {
      getThreadsByGame: vi.fn(),
      getThreadById: vi.fn(),
      createThread: vi.fn(),
      addMessage: vi.fn(),
      deleteThread: vi.fn(),
    },
  },
}));

describe('useMultiGameChat actions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('switchGame', () => {
    it('should load chats when switching to new game', async () => {
      const mockChats = [
        {
          id: 'chat-1',
          gameId: 'game-1',
          gameName: 'Game 1',
          agentId: 'agent-1',
          agentName: 'Agent',
          startedAt: '2024-01-01',
          lastMessageAt: null,
        },
      ];

      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce(mockChats as any);

      const { result } = renderHook(() => useMultiGameChat(null));

      await act(async () => {
        await result.current.switchGame('game-1');
      });

      expect(api.chat.getThreadsByGame).toHaveBeenCalledWith('game-1');
    });

    it('should not reload chats if already loaded', async () => {
      const mockChats = [
        {
          id: 'chat-1',
          gameId: 'game-1',
          gameName: 'Game 1',
          agentId: 'agent-1',
          agentName: 'Agent',
          startedAt: '2024-01-01',
          lastMessageAt: null,
        },
      ];

      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce(mockChats as any);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.chats).toHaveLength(1);
      });

      // Clear mocks and try switching to same game
      vi.clearAllMocks();

      await act(async () => {
        await result.current.switchGame('game-1');
      });

      // Should not call API again since chats are already loaded
      expect(api.chat.getThreadsByGame).not.toHaveBeenCalled();
    });

    it('should handle API errors gracefully', async () => {
      vi.mocked(api.chat.getThreadsByGame).mockRejectedValueOnce(new Error('Network error'));

      const { result } = renderHook(() => useMultiGameChat(null));

      await act(async () => {
        await result.current.switchGame('game-1');
      });

      expect(result.current.chats).toEqual([]);
      expect(result.current.isLoadingChats).toBe(false);
    });
  });

  describe('loadChatHistory', () => {
    it('should load messages for a chat', async () => {
      const mockChats = [];
      const mockChatHistory = {
        id: 'chat-1',
        gameId: 'game-1',
        gameName: 'Game',
        agentId: 'agent-1',
        agentName: 'Agent',
        startedAt: '2024-01-01',
        lastMessageAt: null,
        messages: [
          {
            id: 'msg-1',
            level: 'user',
            message: 'Hello',
            metadataJson: null,
            createdAt: '2024-01-01T10:00:00Z',
          },
          {
            id: 'msg-2',
            level: 'assistant',
            message: 'Hi there!',
            metadataJson: null,
            createdAt: '2024-01-01T10:01:00Z',
          },
        ],
      };

      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce(mockChats as any);
      vi.mocked(api.chat.getThreadById).mockResolvedValueOnce(mockChatHistory as any);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      await act(async () => {
        await result.current.loadChatHistory('chat-1');
      });

      expect(api.chat.getThreadById).toHaveBeenCalledWith('chat-1');
      expect(result.current.messages).toHaveLength(2);
      expect(result.current.messages[0].role).toBe('user');
      expect(result.current.messages[1].role).toBe('assistant');
    });

    it('should parse message metadata with snippets', async () => {
      const mockChats = [];
      const mockChatHistory = {
        id: 'chat-1',
        gameId: 'game-1',
        gameName: 'Game',
        agentId: 'agent-1',
        agentName: 'Agent',
        startedAt: '2024-01-01',
        lastMessageAt: null,
        messages: [
          {
            id: 'msg-1',
            level: 'assistant',
            message: 'Answer with sources',
            metadataJson: JSON.stringify({
              snippets: [{ text: 'Source text', source: 'rulebook.pdf' }],
            }),
            createdAt: '2024-01-01T10:00:00Z',
          },
        ],
      };

      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce(mockChats as any);
      vi.mocked(api.chat.getThreadById).mockResolvedValueOnce(mockChatHistory as any);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      await act(async () => {
        await result.current.loadChatHistory('chat-1');
      });

      expect(result.current.messages[0].snippets).toHaveLength(1);
      expect(result.current.messages[0].snippets![0].source).toBe('rulebook.pdf');
    });

    it('should not load when no game is selected', async () => {
      const { result } = renderHook(() => useMultiGameChat(null));

      await act(async () => {
        await result.current.loadChatHistory('chat-1');
      });

      expect(api.chat.getThreadsByGame).not.toHaveBeenCalled();
    });
  });

  describe('createNewChat', () => {
    it('should create a new chat and add it to the list', async () => {
      const mockChats: unknown[] = [];
      const newChat = {
        id: 'new-chat',
        gameId: 'game-1',
        gameName: 'Game',
        agentId: 'agent-1',
        agentName: 'Agent',
        startedAt: '2024-01-01',
        lastMessageAt: null,
      };

      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce(mockChats as any);
      vi.mocked(api.chat.createThread).mockResolvedValueOnce(newChat as any);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      let createdChat;
      await act(async () => {
        createdChat = await result.current.createNewChat('game-1', 'agent-1');
      });

      expect(api.chat.createThread).toHaveBeenCalledWith({
        gameId: 'game-1',
      });
      expect(createdChat).toEqual(newChat);
      expect(result.current.chats).toContainEqual(newChat);
      expect(result.current.activeChatId).toBe('new-chat');
    });

    it('should return null on API error', async () => {
      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([] as any);
      vi.mocked(api.chat.createThread).mockRejectedValueOnce(new Error('API Error'));

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      let createdChat;
      await act(async () => {
        createdChat = await result.current.createNewChat('game-1', 'agent-1');
      });

      expect(createdChat).toBeNull();
    });

    it('should return null when API returns null', async () => {
      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce([] as any);
      vi.mocked(api.chat.createThread).mockResolvedValueOnce(null as any);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.isLoadingChats).toBe(false);
      });

      let createdChat;
      await act(async () => {
        createdChat = await result.current.createNewChat('game-1', 'agent-1');
      });

      expect(createdChat).toBeNull();
    });
  });

  describe('deleteChat', () => {
    it('should delete chat and update state', async () => {
      const mockChats = [
        {
          id: 'chat-1',
          gameId: 'game-1',
          gameName: 'Game',
          agentId: 'agent-1',
          agentName: 'Agent',
          startedAt: '2024-01-01',
          lastMessageAt: null,
        },
        {
          id: 'chat-2',
          gameId: 'game-1',
          gameName: 'Game',
          agentId: 'agent-1',
          agentName: 'Agent',
          startedAt: '2024-01-02',
          lastMessageAt: null,
        },
      ];

      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce(mockChats as any);
      vi.mocked(api.chat.deleteThread).mockResolvedValueOnce(undefined as any);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.chats).toHaveLength(2);
      });

      await act(async () => {
        await result.current.deleteChat('chat-1');
      });

      expect(api.chat.deleteThread).toHaveBeenCalledWith('chat-1');
      expect(result.current.chats).toHaveLength(1);
      expect(result.current.chats[0].id).toBe('chat-2');
    });

    it('should clear active chat and messages when deleting active chat', async () => {
      const mockChats = [
        {
          id: 'chat-1',
          gameId: 'game-1',
          gameName: 'Game',
          agentId: 'agent-1',
          agentName: 'Agent',
          startedAt: '2024-01-01',
          lastMessageAt: null,
        },
      ];

      vi.mocked(api.chat.getThreadsByGame).mockResolvedValueOnce(mockChats as any);
      vi.mocked(api.chat.deleteThread).mockResolvedValueOnce(undefined as any);

      const { result } = renderHook(() => useMultiGameChat('game-1'));

      await waitFor(() => {
        expect(result.current.chats).toHaveLength(1);
      });

      // Set active chat
      act(() => {
        result.current.setActiveChatId('chat-1');
        result.current.setMessages([
          { id: 'msg-1', role: 'user', content: 'Hi', timestamp: new Date() },
        ]);
      });

      expect(result.current.activeChatId).toBe('chat-1');

      await act(async () => {
        await result.current.deleteChat('chat-1');
      });

      expect(result.current.activeChatId).toBeNull();
      expect(result.current.messages).toEqual([]);
    });

    it('should not delete when no game is selected', async () => {
      const { result } = renderHook(() => useMultiGameChat(null));

      await act(async () => {
        await result.current.deleteChat('chat-1');
      });

      expect(api.chat.deleteThread).not.toHaveBeenCalled();
    });
  });
});
