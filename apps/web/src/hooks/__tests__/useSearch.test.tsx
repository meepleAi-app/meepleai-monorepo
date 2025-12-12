/**
 * Comprehensive Tests for useSearch Hook (Issue #1661 - Fase 1.3)
 * Coverage target: 95%+
 */

import { renderHook, act } from '@testing-library/react';
import { useSearch } from '../useSearch';
import type { Game, Message, ChatThread, Agent } from '@/types';

describe('useSearch', () => {
  const mockGames: Game[] = [
    {
      id: '1',
      title: 'Catan',
      publisher: 'KOSMOS',
      yearPublished: 1995,
      minPlayers: 3,
      maxPlayers: 4,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-01T00:00:00Z',
    } as Game,
  ];

  const mockMessages: Message[] = [
    {
      id: 'msg-1',
      content: 'How do I play Catan?',
      role: 'user',
      gameId: '1',
      timestamp: new Date('2025-01-15T10:00:00Z'),
    } as Message,
  ];

  beforeEach(() => {
    localStorage.clear();
  });

  it('should initialize with empty query and filters', () => {
    const { result } = renderHook(() => useSearch({ games: mockGames }));

    expect(result.current.query).toBe('');
    expect(result.current.filters).toEqual({});
  });

  it('should perform fuzzy search', () => {
    const { result } = renderHook(() => useSearch({ games: mockGames }));

    const results = result.current.search({ query: 'catan' });

    expect(results.length).toBeGreaterThan(0);
    expect(results[0].title).toContain('Catan');
  });

  it('should filter by type', () => {
    const { result } = renderHook(() => useSearch({ games: mockGames, messages: mockMessages }));

    const results = result.current.search({
      query: 'catan',
      filters: { types: ['game'] },
    });

    expect(results.every(r => r.type === 'game')).toBe(true);
  });

  it('should limit results', () => {
    const { result } = renderHook(() => useSearch({ games: mockGames }));

    const results = result.current.search({ query: '', limit: 1 });

    expect(results.length).toBeLessThanOrEqual(1);
  });

  it('should add to recent searches', () => {
    const { result } = renderHook(() => useSearch({ games: mockGames }));

    act(() => {
      result.current.addToRecentSearches('catan', {}, 5);
    });

    expect(result.current.recentSearches.length).toBe(1);
    expect(result.current.recentSearches[0].query).toBe('catan');
  });

  it('should clear recent searches', () => {
    const { result } = renderHook(() => useSearch({ games: mockGames }));

    act(() => {
      result.current.addToRecentSearches('catan', {}, 5);
      result.current.clearRecentSearches();
    });

    expect(result.current.recentSearches).toEqual([]);
  });

  it('should remove specific recent search', () => {
    const { result } = renderHook(() => useSearch({ games: mockGames }));

    act(() => {
      result.current.addToRecentSearches('catan', {}, 5);
    });

    const searchId = result.current.recentSearches[0].id;

    act(() => {
      result.current.removeRecentSearch(searchId);
    });

    expect(result.current.recentSearches).toEqual([]);
  });

  // Issue #2030: Chat context filtering tests
  describe('chatId filtering', () => {
    const mockChats: ChatThread[] = [
      {
        id: 'chat-1',
        gameId: '1',
        title: 'First Chat',
        messageCount: 2,
        createdAt: '2025-01-10T00:00:00Z',
        lastMessageAt: '2025-01-15T00:00:00Z',
        messages: [],
      } as ChatThread,
      {
        id: 'chat-2',
        gameId: '1',
        title: 'Second Chat',
        messageCount: 1,
        createdAt: '2025-01-12T00:00:00Z',
        lastMessageAt: '2025-01-16T00:00:00Z',
        messages: [],
      } as ChatThread,
    ];

    const mockMessagesWithChat: Message[] = [
      {
        id: 'msg-1',
        content: 'Message in chat-1',
        role: 'user',
        gameId: '1',
        timestamp: new Date('2025-01-15T10:00:00Z'),
      } as Message,
      {
        id: 'msg-2',
        content: 'Another message in chat-1',
        role: 'assistant',
        gameId: '1',
        timestamp: new Date('2025-01-15T10:05:00Z'),
      } as Message,
      {
        id: 'msg-3',
        content: 'Message in chat-2',
        role: 'user',
        gameId: '1',
        timestamp: new Date('2025-01-16T10:00:00Z'),
      } as Message,
    ];

    const mockMessagesByChat = {
      'chat-1': [mockMessagesWithChat[0], mockMessagesWithChat[1]],
      'chat-2': [mockMessagesWithChat[2]],
    };

    it('should assign chatId to messages from messagesByChat', () => {
      const { result } = renderHook(() =>
        useSearch({
          messages: mockMessagesWithChat,
          messagesByChat: mockMessagesByChat,
          chats: mockChats,
        })
      );

      const results = result.current.search({ query: '' });
      const messageResults = results.filter(r => r.type === 'message');

      expect(messageResults.length).toBe(3);
      expect(messageResults.find(r => r.id === 'msg-1')?.chatId).toBe('chat-1');
      expect(messageResults.find(r => r.id === 'msg-2')?.chatId).toBe('chat-1');
      expect(messageResults.find(r => r.id === 'msg-3')?.chatId).toBe('chat-2');
    });

    it('should filter messages by chatId', () => {
      const { result } = renderHook(() =>
        useSearch({
          messages: mockMessagesWithChat,
          messagesByChat: mockMessagesByChat,
          chats: mockChats,
        })
      );

      const results = result.current.search({
        query: '',
        filters: { chatId: 'chat-1' },
      });

      const messageResults = results.filter(r => r.type === 'message');
      expect(messageResults.length).toBe(2);
      expect(messageResults.every(r => r.chatId === 'chat-1')).toBe(true);
    });

    it('should filter chats by chatId', () => {
      const { result } = renderHook(() =>
        useSearch({
          chats: mockChats,
        })
      );

      const results = result.current.search({
        query: '',
        filters: { chatId: 'chat-1' },
      });

      expect(results.length).toBe(1);
      expect(results[0].id).toBe('chat-1');
    });

    it('should exclude non-chat types when chatId filter is active', () => {
      const { result } = renderHook(() =>
        useSearch({
          games: mockGames,
          chats: mockChats,
        })
      );

      const results = result.current.search({
        query: '',
        filters: { chatId: 'chat-1' },
      });

      expect(results.every(r => r.type === 'chat' || r.type === 'message')).toBe(true);
      expect(results.some(r => r.type === 'game')).toBe(false);
    });
  });
});
