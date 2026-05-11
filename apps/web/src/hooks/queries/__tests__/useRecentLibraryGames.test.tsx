/**
 * useRecentLibraryGames — unit tests
 * Spec: docs/superpowers/specs/2026-05-09-game-night-user-flow-design.md §G3
 */
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import type { ReactNode } from 'react';

import { useRecentsStore } from '@/stores/use-recents';

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: vi.fn(),
  useRecentlyAddedGames: vi.fn(),
}));

import { useLibrary, useRecentlyAddedGames } from '@/hooks/queries/useLibrary';
import { useRecentLibraryGames } from '../useRecentLibraryGames';

function wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

const entry = (id: string, addedAt = '2026-05-01T00:00:00Z') => ({
  id: `entry-${id}`,
  userId: 'u1',
  gameId: id,
  gameTitle: `Game ${id}`,
  isFavorite: false,
  currentState: 'Owned' as const,
  addedAt,
  hasKb: true,
  kbCardCount: 1,
  kbIndexedCount: 1,
  kbProcessingCount: 0,
  hasRagAccess: true,
  agentIsOwned: true,
  isPrivateGame: false,
  canProposeToCatalog: false,
});

const emptyPaginated = {
  items: [],
  page: 1,
  pageSize: 50,
  totalCount: 0,
  totalPages: 0,
  hasNextPage: false,
  hasPreviousPage: false,
};

describe('useRecentLibraryGames', () => {
  beforeEach(() => {
    useRecentsStore.setState({ items: [] });
    vi.mocked(useLibrary).mockReturnValue({
      data: emptyPaginated,
      isLoading: false,
      isError: false,
    } as any);
    vi.mocked(useRecentlyAddedGames).mockReturnValue({
      data: emptyPaginated,
      isLoading: false,
      isError: false,
    } as any);
  });

  it('reports loading when library resolved but recently-added still pending', async () => {
    vi.mocked(useLibrary).mockReturnValue({
      data: emptyPaginated,
      isLoading: false,
      isError: false,
    } as any);
    vi.mocked(useRecentlyAddedGames).mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
    } as any);

    const { result } = renderHook(() => useRecentLibraryGames(5), { wrapper });

    expect(result.current.isLoading).toBe(true);
    expect(result.current.entries).toEqual([]);
  });

  it('returns recents-store games in order, mapped to library entries', async () => {
    const e1 = entry('aaa');
    const e2 = entry('bbb');
    vi.mocked(useLibrary).mockReturnValue({
      data: { ...emptyPaginated, items: [e1, e2], totalCount: 2, totalPages: 1 },
      isLoading: false,
      isError: false,
    } as any);
    useRecentsStore.setState({
      items: [
        {
          id: 'bbb',
          entity: 'game',
          title: 'Game bbb',
          href: '/library/bbb',
          visitedAt: 1731147600000 /* 2026-05-09T10:00:00Z */,
        },
        {
          id: 'aaa',
          entity: 'game',
          title: 'Game aaa',
          href: '/library/aaa',
          visitedAt: 1731144000000 /* 2026-05-09T09:00:00Z */,
        },
      ],
    });

    const { result } = renderHook(() => useRecentLibraryGames(5), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.entries.map(e => e.gameId)).toEqual(['bbb', 'aaa']);
  });

  it('falls back to recently-added when recents store empty', async () => {
    const e1 = entry('xxx', '2026-05-08T00:00:00Z');
    vi.mocked(useRecentlyAddedGames).mockReturnValue({
      data: { ...emptyPaginated, items: [e1], totalCount: 1, totalPages: 1 },
      isLoading: false,
      isError: false,
    } as any);

    const { result } = renderHook(() => useRecentLibraryGames(5), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.entries.map(e => e.gameId)).toEqual(['xxx']);
  });

  it('truncates to limit (≤ MAX_RECENTS=4 in real store; setState bypasses cap)', async () => {
    const items = ['a', 'b', 'c', 'd'].map(id => entry(id));
    vi.mocked(useLibrary).mockReturnValue({
      data: { ...emptyPaginated, items, totalCount: 4, totalPages: 1 },
      isLoading: false,
      isError: false,
    } as any);
    useRecentsStore.setState({
      items: items.map(e => ({
        id: e.gameId,
        entity: 'game' as const,
        title: e.gameTitle,
        href: `/library/${e.gameId}`,
        visitedAt: 1731147600000 /* 2026-05-09T10:00:00Z */,
      })),
    });

    const { result } = renderHook(() => useRecentLibraryGames(2), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.entries).toHaveLength(2);
  });

  it('skips recents whose game is no longer in library', async () => {
    const e1 = entry('inlib');
    vi.mocked(useLibrary).mockReturnValue({
      data: { ...emptyPaginated, items: [e1], totalCount: 1, totalPages: 1 },
      isLoading: false,
      isError: false,
    } as any);
    useRecentsStore.setState({
      items: [
        {
          id: 'removed',
          entity: 'game',
          title: 'Removed',
          href: '/library/removed',
          visitedAt: 1731147600000 /* 2026-05-09T10:00:00Z */,
        },
        {
          id: 'inlib',
          entity: 'game',
          title: 'In Lib',
          href: '/library/inlib',
          visitedAt: 1731144000000 /* 2026-05-09T09:00:00Z */,
        },
      ],
    });

    const { result } = renderHook(() => useRecentLibraryGames(5), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.entries.map(e => e.gameId)).toEqual(['inlib']);
  });

  it('returns empty entries when disabled', async () => {
    vi.mocked(useLibrary).mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    } as any);
    vi.mocked(useRecentlyAddedGames).mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    } as any);

    const { result } = renderHook(() => useRecentLibraryGames(5, false), { wrapper });

    expect(result.current.entries).toEqual([]);
    expect(result.current.isLoading).toBe(false);
  });

  it('reports isError when library errors and recents store is empty', async () => {
    vi.mocked(useLibrary).mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    } as any);
    // recentlyAdded mock già emptyPaginated da beforeEach
    useRecentsStore.setState({ items: [] });

    const { result } = renderHook(() => useRecentLibraryGames(5), { wrapper });
    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.entries).toEqual([]);
  });

  it('does NOT report isError when library errors but recents store has items', async () => {
    vi.mocked(useLibrary).mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    } as any);
    useRecentsStore.setState({
      items: [
        {
          id: 'cached-game',
          entity: 'game',
          title: 'Cached',
          href: '/library/cached-game',
          visitedAt: 1731147600000,
        },
      ],
    });

    const { result } = renderHook(() => useRecentLibraryGames(5), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.isError).toBe(false);
  });
});
