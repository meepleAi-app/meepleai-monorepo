/**
 * useSearch Hook Tests
 * Unit tests for search functionality (Issue #1101)
 */

import { renderHook, act } from '@testing-library/react';
import { useSearch } from '../useSearch';
import type { Game, Agent, Message, ChatThread } from '@/types';
import { createMockAgent } from '@/__tests__/fixtures/common-fixtures';

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: (key: string) => store[key] || null,
    setItem: (key: string, value: string) => {
      store[key] = value;
    },
    removeItem: (key: string) => {
      delete store[key];
    },
    clear: () => {
      store = {};
    },
  };
})();

Object.defineProperty(window, 'localStorage', {
  value: localStorageMock,
});

// Test data
const mockGames: Game[] = [
  { id: '770e8400-e29b-41d4-a716-000000000001', name: 'Chess', createdAt: '2024-01-01T00:00:00Z' },
  { id: '770e8400-e29b-41d4-a716-000000000002', name: 'Go', createdAt: '2024-01-02T00:00:00Z' },
];

const mockAgents: Agent[] = [
  createMockAgent({ id: 'agent-1', name: 'Chess Master', type: 'Expert', createdAt: '2024-01-01T00:00:00Z' }),
  createMockAgent({ id: 'agent-2', name: 'Go Sensei', type: 'Expert', createdAt: '2024-01-02T00:00:00Z' }),
];

const mockMessages: Message[] = [
  {
    id: 'msg-1',
    role: 'user',
    content: 'How do I castle in chess?',
    timestamp: new Date('2024-01-01T10:00:00Z'),
    gameId: '770e8400-e29b-41d4-a716-000000000001',
  },
  {
    id: 'msg-2',
    role: 'assistant',
    content: 'Castling is a special move involving the king and rook.',
    timestamp: new Date('2024-01-01T10:01:00Z'),
    gameId: '770e8400-e29b-41d4-a716-000000000001',
  },
];

const mockChats: ChatThread[] = [
  {
    id: 'chat-1',
    gameId: '770e8400-e29b-41d4-a716-000000000001',
    title: 'Learning Chess Basics',
    createdAt: '2024-01-01T09:00:00Z',
    lastMessageAt: '2024-01-01T10:01:00Z',
    messageCount: 2,
    messages: [],
  },
];

describe('useSearch', () => {
  beforeEach(() => {
    localStorageMock.clear();
  });

  it('should initialize with empty state', () => {
    const { result } = renderHook(() =>
      useSearch({
        messages: mockMessages,
        chats: mockChats,
        games: mockGames,
        agents: mockAgents,
      })
    );

    expect(result.current.query).toBe('');
    expect(result.current.filters).toEqual({});
    expect(result.current.recentSearches).toEqual([]);
  });

  it('should search messages with fuzzy matching', () => {
    const { result } = renderHook(() =>
      useSearch({
        messages: mockMessages,
        chats: mockChats,
        games: mockGames,
        agents: mockAgents,
      })
    );

    let searchResults: any;
    act(() => {
      searchResults = result.current.search({ query: 'castle', limit: 10 });
    });

    expect(searchResults).toBeDefined();
    expect(searchResults.length).toBeGreaterThan(0);
    expect(searchResults[0].type).toBe('message');
  });

  it('should filter results by game', () => {
    const { result } = renderHook(() =>
      useSearch({
        messages: mockMessages,
        chats: mockChats,
        games: mockGames,
        agents: mockAgents,
      })
    );

    let searchResults: any;
    act(() => {
      searchResults = result.current.search({
        query: '',
        filters: { gameId: '770e8400-e29b-41d4-a716-000000000001' },
        limit: 100,
      });
    });

    expect(searchResults).toBeDefined();
    searchResults.forEach((result: any) => {
      if (result.type === 'message' || result.type === 'chat') {
        expect(result.gameId).toBe('770e8400-e29b-41d4-a716-000000000001');
      }
      // Agents are global and should be included regardless of game filter
      if (result.type === 'agent') {
        expect(result.gameId).toBeUndefined();
      }
    });
  });

  it('should filter results by date range', () => {
    const { result } = renderHook(() =>
      useSearch({
        messages: mockMessages,
        chats: mockChats,
        games: mockGames,
        agents: mockAgents,
      })
    );

    const dateFrom = new Date('2024-01-01T00:00:00Z');
    const dateTo = new Date('2024-01-01T23:59:59Z');

    let searchResults: any;
    act(() => {
      searchResults = result.current.search({
        query: '',
        filters: { dateFrom, dateTo },
        limit: 100,
      });
    });

    expect(searchResults).toBeDefined();
    searchResults.forEach((result: any) => {
      if (result.timestamp) {
        expect(result.timestamp >= dateFrom).toBe(true);
        expect(result.timestamp <= dateTo).toBe(true);
      }
    });
  });

  it('should filter results by type', () => {
    const { result } = renderHook(() =>
      useSearch({
        messages: mockMessages,
        chats: mockChats,
        games: mockGames,
        agents: mockAgents,
      })
    );

    let searchResults: any;
    act(() => {
      searchResults = result.current.search({
        query: '',
        filters: { types: ['game'] },
        limit: 100,
      });
    });

    expect(searchResults).toBeDefined();
    searchResults.forEach((result: any) => {
      expect(result.type).toBe('game');
    });
  });

  it('should add search to recent history', () => {
    const { result } = renderHook(() =>
      useSearch({
        messages: mockMessages,
        chats: mockChats,
        games: mockGames,
        agents: mockAgents,
      })
    );

    act(() => {
      result.current.addToRecentSearches('chess', {}, 5);
    });

    expect(result.current.recentSearches).toHaveLength(1);
    expect(result.current.recentSearches[0].query).toBe('chess');
    expect(result.current.recentSearches[0].resultCount).toBe(5);
  });

  it('should persist recent searches to localStorage', () => {
    const { result } = renderHook(() =>
      useSearch({
        messages: mockMessages,
        chats: mockChats,
        games: mockGames,
        agents: mockAgents,
      })
    );

    act(() => {
      result.current.addToRecentSearches('chess', {}, 5);
    });

    const stored = localStorage.getItem('meepleai_recent_searches');
    expect(stored).toBeTruthy();

    const parsed = JSON.parse(stored!);
    expect(parsed).toHaveLength(1);
    expect(parsed[0].query).toBe('chess');
  });

  it('should load recent searches from localStorage on mount', () => {
    // Pre-populate localStorage
    const recentSearches = [
      {
        id: '1',
        query: 'test',
        filters: {},
        timestamp: new Date().toISOString(),
        resultCount: 3,
      },
    ];
    localStorage.setItem('meepleai_recent_searches', JSON.stringify(recentSearches));

    const { result } = renderHook(() =>
      useSearch({
        messages: mockMessages,
        chats: mockChats,
        games: mockGames,
        agents: mockAgents,
      })
    );

    expect(result.current.recentSearches).toHaveLength(1);
    expect(result.current.recentSearches[0].query).toBe('test');
  });

  it('should clear recent searches', () => {
    const { result } = renderHook(() =>
      useSearch({
        messages: mockMessages,
        chats: mockChats,
        games: mockGames,
        agents: mockAgents,
      })
    );

    act(() => {
      result.current.addToRecentSearches('chess', {}, 5);
    });

    expect(result.current.recentSearches).toHaveLength(1);

    act(() => {
      result.current.clearRecentSearches();
    });

    expect(result.current.recentSearches).toHaveLength(0);
    expect(localStorage.getItem('meepleai_recent_searches')).toBeNull();
  });

  it('should remove specific recent search', () => {
    const { result } = renderHook(() =>
      useSearch({
        messages: mockMessages,
        chats: mockChats,
        games: mockGames,
        agents: mockAgents,
      })
    );

    act(() => {
      result.current.addToRecentSearches('chess', {}, 5);
      result.current.addToRecentSearches('go', {}, 3);
    });

    expect(result.current.recentSearches).toHaveLength(2);

    const idToRemove = result.current.recentSearches[0].id;

    act(() => {
      result.current.removeRecentSearch(idToRemove);
    });

    expect(result.current.recentSearches).toHaveLength(1);
    expect(result.current.recentSearches[0].query).toBe('chess');
  });

  it('should limit recent searches to maximum', () => {
    const { result } = renderHook(() =>
      useSearch({
        messages: mockMessages,
        chats: mockChats,
        games: mockGames,
        agents: mockAgents,
      })
    );

    // Add 25 searches (max is 20)
    act(() => {
      for (let i = 0; i < 25; i++) {
        result.current.addToRecentSearches(`query-${i}`, {}, i);
      }
    });

    expect(result.current.recentSearches).toHaveLength(20);
    // Most recent should be first
    expect(result.current.recentSearches[0].query).toBe('query-24');
  });

  it('should not add empty queries to recent searches', () => {
    const { result } = renderHook(() =>
      useSearch({
        messages: mockMessages,
        chats: mockChats,
        games: mockGames,
        agents: mockAgents,
      })
    );

    act(() => {
      result.current.addToRecentSearches('', {}, 0);
      result.current.addToRecentSearches('   ', {}, 0);
    });

    expect(result.current.recentSearches).toHaveLength(0);
  });

  it('should return results sorted by timestamp when no query', () => {
    const { result } = renderHook(() =>
      useSearch({
        messages: mockMessages,
        chats: mockChats,
        games: mockGames,
        agents: mockAgents,
      })
    );

    let searchResults: any;
    act(() => {
      searchResults = result.current.search({ query: '', limit: 100 });
    });

    expect(searchResults).toBeDefined();

    // Check that results with timestamps are sorted (newest first)
    const resultsWithTimestamps = searchResults.filter((r: any) => r.timestamp);
    for (let i = 0; i < resultsWithTimestamps.length - 1; i++) {
      expect(resultsWithTimestamps[i].timestamp.getTime()).toBeGreaterThanOrEqual(
        resultsWithTimestamps[i + 1].timestamp.getTime()
      );
    }
  });

  it('should respect custom threshold for fuzzy matching', () => {
    const { result } = renderHook(() =>
      useSearch({
        messages: mockMessages,
        chats: mockChats,
        games: mockGames,
        agents: mockAgents,
      })
    );

    let strictResults: any;
    let looseResults: any;

    act(() => {
      // Strict matching (lower threshold = more strict)
      strictResults = result.current.search({
        query: 'cstle', // Typo for "castle"
        threshold: 0.2,
        limit: 10,
      });

      // Loose matching (higher threshold = more lenient)
      looseResults = result.current.search({
        query: 'cstle',
        threshold: 0.5,
        limit: 10,
      });
    });

    // Loose matching should return more results
    expect(looseResults.length).toBeGreaterThanOrEqual(strictResults.length);
  });
});
