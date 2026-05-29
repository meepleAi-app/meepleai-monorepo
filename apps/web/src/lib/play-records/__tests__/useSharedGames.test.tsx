/**
 * @vitest-environment jsdom
 *
 * useSharedGames — Task 0.3 (Issue #1488).
 *
 * TanStack Query batch hook fetching multiple SharedGame details concurrently.
 *
 * Contract:
 *   1. dedupes input gameIds (null/duplicates stripped before fetch)
 *   2. returns Map<gameId, SharedGameDetail> keyed by id (consumers do O(1) lookup)
 *   3. fans out `Promise.all(getSharedGameDetail)` — no batch endpoint exists in BE,
 *      tracked as a follow-up; see JSDoc on the hook for the workaround rationale
 *   4. disabled when no valid ids (filter [], or all-null array)
 *   5. queryKey stable across input permutations (sorted)
 *   6. tolerates individual fetch failures (Promise.allSettled-like behaviour) —
 *      not implemented in MVP; first rejected promise rejects the entire query.
 *      MVP relies on TanStack Query `error` channel; per-id resilience is a
 *      follow-up if N+1 latency proves painful.
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { SharedGameDetail } from '@/lib/api/shared-games';

vi.mock('@/lib/api/shared-games', async orig => {
  const actual = await orig<typeof import('@/lib/api/shared-games')>();
  return {
    ...actual,
    getSharedGameDetail: vi.fn(),
  };
});

import { getSharedGameDetail } from '@/lib/api/shared-games';

import { useSharedGames } from '../useSharedGames';

const mockGet = getSharedGameDetail as ReturnType<typeof vi.fn>;

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

const ID_A = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const ID_B = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

function makeDetail(id: string, title: string): SharedGameDetail {
  return {
    id,
    bggId: null,
    title,
    yearPublished: 2024,
    description: '',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 60,
    minAge: 10,
    complexityRating: null,
    averageRating: null,
    imageUrl: '',
    thumbnailUrl: '',
    status: 'Published',
    isRagPublic: false,
    hasKnowledgeBase: false,
    createdAt: '2026-01-01T00:00:00Z',
    modifiedAt: null,
    toolkits: [],
    agents: [],
    kbs: [],
    toolkitsCount: 0,
    agentsCount: 0,
    kbsCount: 0,
    contributorsCount: 0,
    isTopRated: false,
    isNew: false,
  };
}

describe('useSharedGames', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('fans out Promise.all and returns a Map keyed by id', async () => {
    mockGet.mockImplementation((id: string) =>
      Promise.resolve(makeDetail(id, id === ID_A ? 'Catan' : 'Carcassonne'))
    );

    const { result } = renderHook(() => useSharedGames([ID_A, ID_B]), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(mockGet).toHaveBeenCalledTimes(2);
    expect(mockGet).toHaveBeenCalledWith(ID_A);
    expect(mockGet).toHaveBeenCalledWith(ID_B);
    const map = result.current.data as Map<string, SharedGameDetail>;
    expect(map.get(ID_A)?.title).toBe('Catan');
    expect(map.get(ID_B)?.title).toBe('Carcassonne');
  });

  it('deduplicates input ids before fetching', async () => {
    mockGet.mockResolvedValue(makeDetail(ID_A, 'Catan'));

    const { result } = renderHook(() => useSharedGames([ID_A, ID_A, ID_A]), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(mockGet).toHaveBeenCalledTimes(1);
    expect(mockGet).toHaveBeenCalledWith(ID_A);
  });

  it('filters null entries before fetching', async () => {
    mockGet.mockResolvedValue(makeDetail(ID_A, 'Catan'));

    const { result } = renderHook(() => useSharedGames([ID_A, null, null]), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(mockGet).toHaveBeenCalledTimes(1);
  });

  it('is disabled when no valid ids (all null)', async () => {
    const { result } = renderHook(() => useSharedGames([null, null]), {
      wrapper: createWrapper(),
    });

    // disabled queries surface as undefined data + fetchStatus 'idle'
    expect(result.current.fetchStatus).toBe('idle');
    expect(result.current.data).toBeUndefined();
    expect(mockGet).not.toHaveBeenCalled();
  });

  it('is disabled when input is empty', () => {
    const { result } = renderHook(() => useSharedGames([]), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
    expect(mockGet).not.toHaveBeenCalled();
  });

  it('queryKey is stable across input permutations (cache hit)', async () => {
    mockGet.mockImplementation((id: string) =>
      Promise.resolve(makeDetail(id, id === ID_A ? 'Catan' : 'Carcassonne'))
    );

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false, gcTime: 60 * 1000 } },
    });
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );

    const { result: r1 } = renderHook(() => useSharedGames([ID_A, ID_B]), { wrapper });
    await waitFor(() => expect(r1.current.data).toBeDefined());
    expect(mockGet).toHaveBeenCalledTimes(2);

    // Second render with permuted order → cache hit, NO new fetch
    const { result: r2 } = renderHook(() => useSharedGames([ID_B, ID_A]), { wrapper });
    await waitFor(() => expect(r2.current.data).toBeDefined());
    expect(mockGet).toHaveBeenCalledTimes(2);
  });
});
