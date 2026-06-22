import { describe, expect, it, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';

import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

import { useHybridHubItems } from '../useHybridHubItems';

const mockUseLibrary = vi.fn();
const mockUseActiveSessions = vi.fn();
const mockUseRecentChatSessions = vi.fn();
const mockUseAgents = vi.fn();
const mockUseUserKbDocs = vi.fn();

vi.mock('../useLibrary', () => ({ useLibrary: (...args: unknown[]) => mockUseLibrary(...args) }));
vi.mock('../useActiveSessions', () => ({
  useActiveSessions: (...args: unknown[]) => mockUseActiveSessions(...args),
}));
vi.mock('../useChatSessions', () => ({
  useRecentChatSessions: (...args: unknown[]) => mockUseRecentChatSessions(...args),
}));
vi.mock('../useAgents', () => ({ useAgents: (...args: unknown[]) => mockUseAgents(...args) }));
vi.mock('../useUserKbDocs', () => ({
  useUserKbDocs: (...args: unknown[]) => mockUseUserKbDocs(...args),
}));

function ok<T>(data: T) {
  return { data, isLoading: false, isError: false, error: null };
}
function loading() {
  return { data: undefined, isLoading: true, isError: false, error: null };
}
function failed(err: Error) {
  return { data: undefined, isLoading: false, isError: true, error: err };
}

const gameEntry = {
  id: 'g1',
  userId: 'u',
  gameId: 'game-1',
  gameTitle: 'Catan',
  gamePublisher: 'Kosmos',
  gameYearPublished: 1995,
  gameIconUrl: null,
  gameImageUrl: null,
  addedAt: '2026-01-01T00:00:00Z',
  notes: null,
  isFavorite: false,
  currentState: 'Owned' as const,
  stateChangedAt: null,
  stateNotes: null,
  hasKb: false,
  kbCardCount: 0,
  kbIndexedCount: 0,
  kbProcessingCount: 0,
  ownershipDeclaredAt: null,
  hasRagAccess: false,
  agentIsOwned: true,
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 60,
  complexityRating: 2,
  averageRating: 7,
  privateGameId: null,
  isPrivateGame: false,
  canProposeToCatalog: false,
};
const sessionDto = {
  id: 's1',
  gameId: 'game-1',
  status: 'Completed',
  startedAt: '2026-02-01T00:00:00Z',
  completedAt: '2026-02-01T01:00:00Z',
  playerCount: 4,
  players: [],
  winnerName: 'Alice',
  notes: null,
  durationMinutes: 60,
};
const chatDto = {
  id: 'c1',
  userId: 'u',
  gameId: 'game-1',
  gameTitle: 'Catan',
  agentId: null,
  agentType: null,
  agentName: null,
  title: 'How to play?',
  messageCount: 3,
  lastMessagePreview: null,
  createdAt: '2026-03-01T00:00:00Z',
  lastMessageAt: '2026-03-01T00:10:00Z',
  isArchived: false,
};

beforeEach(() => {
  mockUseLibrary.mockReturnValue(
    ok({
      items: [gameEntry],
      page: 1,
      pageSize: 50,
      totalCount: 1,
      totalPages: 1,
      hasNextPage: false,
      hasPreviousPage: false,
    })
  );
  mockUseActiveSessions.mockReturnValue(
    ok({ sessions: [sessionDto], page: 1, pageSize: 20, totalCount: 1 })
  );
  mockUseRecentChatSessions.mockReturnValue(ok({ sessions: [chatDto], totalCount: 1 }));
  mockUseAgents.mockReturnValue(ok([]));
  mockUseUserKbDocs.mockReturnValue(ok({ items: [], total: 0, page: 1, pageSize: 20 }));
});

describe('useHybridHubItems', () => {
  it('maps the 3 ready sources into HybridHubSources (game/session/chat), agents/kb empty', () => {
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.sources.games.map(i => i.entity)).toEqual(['game']);
    expect(result.current.sources.sessions.map(i => i.entity)).toEqual(['session']);
    expect(result.current.sources.chat.map(i => i.entity)).toEqual(['chat']);
    expect(result.current.sources.agents).toEqual([]);
    expect(result.current.sources.kb).toEqual([]);
  });

  it('unwraps .items / .sessions from paginated responses; chat passes through', () => {
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.sources.games[0]?.id).toBe('g1');
    expect(result.current.sources.sessions[0]?.id).toBe('s1');
    expect(result.current.sources.chat[0]?.id).toBe('c1');
  });

  it('caps each source to 20 items', () => {
    const many = Array.from({ length: 30 }, (_, i) => ({
      ...gameEntry,
      id: `g${i}`,
      gameId: `game-${i}`,
    }));
    mockUseLibrary.mockReturnValue(
      ok({
        items: many,
        page: 1,
        pageSize: 50,
        totalCount: 30,
        totalPages: 1,
        hasNextPage: false,
        hasPreviousPage: false,
      })
    );
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.sources.games).toHaveLength(20);
  });

  it('reports totalCounts per source (pre-cap)', () => {
    const many = Array.from({ length: 30 }, (_, i) => ({
      ...gameEntry,
      id: `g${i}`,
      gameId: `game-${i}`,
    }));
    mockUseLibrary.mockReturnValue(
      ok({
        items: many,
        page: 1,
        pageSize: 50,
        totalCount: 30,
        totalPages: 1,
        hasNextPage: false,
        hasPreviousPage: false,
      })
    );
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.totalCounts.games).toBe(30);
    expect(result.current.totalCounts.sessions).toBe(1);
    expect(result.current.totalCounts.chat).toBe(1);
  });

  it('isLoading=true while any ready source is loading', () => {
    mockUseActiveSessions.mockReturnValue(loading());
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.isLoading).toBe(true);
  });

  it('partialErrors records the failing source; others still map', () => {
    mockUseActiveSessions.mockReturnValue(failed(new Error('boom')));
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.partialErrors.sessions).toBeInstanceOf(Error);
    expect(result.current.partialErrors.games).toBeNull();
    expect(result.current.sources.games).toHaveLength(1);
    expect(result.current.sources.sessions).toEqual([]);
  });

  it('allFailed=true only when every source errors (including agents+kb)', () => {
    mockUseLibrary.mockReturnValue(failed(new Error('a')));
    mockUseActiveSessions.mockReturnValue(failed(new Error('b')));
    mockUseRecentChatSessions.mockReturnValue(failed(new Error('c')));
    mockUseAgents.mockReturnValue(failed(new Error('d')));
    mockUseUserKbDocs.mockReturnValue(failed(new Error('e')));
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.allFailed).toBe(true);
  });

  it('allFailed=false when at least one ready source succeeds', () => {
    mockUseLibrary.mockReturnValue(failed(new Error('a')));
    mockUseActiveSessions.mockReturnValue(
      ok({ sessions: [sessionDto], page: 1, pageSize: 20, totalCount: 1 })
    );
    mockUseRecentChatSessions.mockReturnValue(failed(new Error('c')));
    const { result } = renderHook(() => useHybridHubItems());
    expect(result.current.allFailed).toBe(false);
  });

  it('AC2.b.5: kb endpoint fails, agents OK — graceful degradation', () => {
    const agentDto: AgentDto = {
      id: '11111111-1111-1111-1111-111111111111',
      name: 'Agent Test',
      type: 'Tutor',
      strategyName: 'HybridSearch',
      strategyParameters: {},
      isActive: true,
      createdAt: '2026-05-28T10:00:00+00:00',
      lastInvokedAt: null,
      invocationCount: 0,
      isRecentlyUsed: false,
      isIdle: true,
    };

    mockUseUserKbDocs.mockReturnValue(failed(new Error('500 server error')));
    mockUseAgents.mockReturnValue(ok([agentDto]));

    const { result } = renderHook(() => useHybridHubItems());

    expect(result.current.sources.agents).toHaveLength(1);
    expect(result.current.sources.kb).toEqual([]);
    expect(result.current.partialErrors.kb).toBeInstanceOf(Error);
    expect(result.current.partialErrors.agents).toBeNull();
    expect(result.current.allFailed).toBe(false);
  });
});
