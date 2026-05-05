/**
 * @vitest-environment jsdom
 *
 * Wave A.4 (Issue #603) — useSharedGameDetail TanStack Query hook tests.
 *
 * Verifies the contract from `useSharedGameDetail.ts`:
 *  - queryFn calls `getSharedGameDetail(id)`
 *  - `initialData` seeds the cache (no fetch on mount when present)
 *  - `enabled: id.length > 0` guards against empty-id router transitions
 *  - `refetch()` triggers a new fetch and resolves the wrapper Promise
 *  - Error state surfaced via `isError` + `error`
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { SharedGameDetailV2 } from '@/lib/api/shared-games';

vi.mock('@/lib/api/shared-games', async orig => {
  const actual = await orig<typeof import('@/lib/api/shared-games')>();
  return {
    ...actual,
    getSharedGameDetail: vi.fn(),
  };
});

import { getSharedGameDetail, SharedGamesApiError } from '@/lib/api/shared-games';
import { useSharedGameDetail } from './useSharedGameDetail';

const mockGet = getSharedGameDetail as ReturnType<typeof vi.fn>;

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

const SAMPLE_ID = '11111111-1111-1111-1111-111111111111';

const SAMPLE_DETAIL: SharedGameDetailV2 = {
  id: SAMPLE_ID,
  bggId: null,
  title: 'Catan',
  yearPublished: 1995,
  description: 'Settlers',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 75,
  minAge: 10,
  complexityRating: 2.3,
  averageRating: 7.4,
  imageUrl: '',
  thumbnailUrl: '',
  status: 'Published',
  createdAt: '2026-04-15T00:00:00Z',
  modifiedAt: null,
  toolkits: [],
  agents: [],
  kbs: [],
  toolkitsCount: 0,
  agentsCount: 0,
  kbsCount: 0,
  contributorsCount: 0,
  hasKnowledgeBase: false,
  isTopRated: false,
  isNew: false,
};

describe('useSharedGameDetail (Wave A.4)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls getSharedGameDetail with the given id', async () => {
    mockGet.mockResolvedValue(SAMPLE_DETAIL);
    const { result } = renderHook(() => useSharedGameDetail({ id: SAMPLE_ID }), {
      wrapper: createWrapper(),
    });
    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(mockGet).toHaveBeenCalledWith(SAMPLE_ID);
    expect(result.current.data).toEqual(SAMPLE_DETAIL);
  });

  it('seeds cache with initialData and skips initial fetch', async () => {
    mockGet.mockResolvedValue(SAMPLE_DETAIL);
    const { result } = renderHook(
      () => useSharedGameDetail({ id: SAMPLE_ID, initialData: SAMPLE_DETAIL }),
      { wrapper: createWrapper() }
    );
    // initialData should be available immediately
    expect(result.current.data).toEqual(SAMPLE_DETAIL);
    expect(result.current.isLoading).toBe(false);
    // staleTime is 60_000 — no immediate refetch on mount
    expect(mockGet).not.toHaveBeenCalled();
  });

  it('does not fetch when id is empty (enabled gate)', () => {
    const { result } = renderHook(() => useSharedGameDetail({ id: '' }), {
      wrapper: createWrapper(),
    });
    expect(mockGet).not.toHaveBeenCalled();
    expect(result.current.isLoading).toBe(false);
    expect(result.current.data).toBeUndefined();
  });

  it('exposes refetch that triggers a new fetch', async () => {
    mockGet.mockResolvedValue(SAMPLE_DETAIL);
    const { result } = renderHook(() => useSharedGameDetail({ id: SAMPLE_ID }), {
      wrapper: createWrapper(),
    });
    await waitFor(() => expect(result.current.data).toBeDefined());
    expect(mockGet).toHaveBeenCalledTimes(1);

    await result.current.refetch();
    expect(mockGet).toHaveBeenCalledTimes(2);
  });

  it('surfaces error state when the query fn rejects', async () => {
    const err = new Error('boom');
    mockGet.mockRejectedValue(err);
    const { result } = renderHook(() => useSharedGameDetail({ id: SAMPLE_ID }), {
      wrapper: createWrapper(),
    });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBe(err);
    expect(result.current.data).toBeUndefined();
  });

  // --- Issue #615 — FSM status derivation ---

  describe('FSM status (Issue #615)', () => {
    it('derives status="default" when data has nested entities', () => {
      const populated: SharedGameDetailV2 = {
        ...SAMPLE_DETAIL,
        toolkitsCount: 2,
        agentsCount: 0,
        kbsCount: 1,
      };
      const { result } = renderHook(
        () => useSharedGameDetail({ id: SAMPLE_ID, initialData: populated }),
        { wrapper: createWrapper() }
      );
      expect(result.current.status).toBe('default');
    });

    it('derives status="empty" when all nested counts are zero', () => {
      // SAMPLE_DETAIL has toolkitsCount=0, agentsCount=0, kbsCount=0
      const { result } = renderHook(
        () => useSharedGameDetail({ id: SAMPLE_ID, initialData: SAMPLE_DETAIL }),
        { wrapper: createWrapper() }
      );
      expect(result.current.status).toBe('empty');
    });

    it('derives status="not-found" on SharedGamesApiError with httpStatus=404', async () => {
      const err = new SharedGamesApiError('Not found', 'http', { httpStatus: 404 });
      mockGet.mockRejectedValue(err);
      const { result } = renderHook(() => useSharedGameDetail({ id: SAMPLE_ID }), {
        wrapper: createWrapper(),
      });
      await waitFor(() => expect(result.current.isError).toBe(true));
      expect(result.current.status).toBe('not-found');
    });

    it('derives status="error" on non-404 SharedGamesApiError (e.g. 5xx)', async () => {
      const err = new SharedGamesApiError('Server error', 'http', { httpStatus: 500 });
      mockGet.mockRejectedValue(err);
      const { result } = renderHook(() => useSharedGameDetail({ id: SAMPLE_ID }), {
        wrapper: createWrapper(),
      });
      await waitFor(() => expect(result.current.isError).toBe(true));
      expect(result.current.status).toBe('error');
    });

    it('derives status="error" on timeout / network failure', async () => {
      const err = new SharedGamesApiError('Timeout', 'timeout');
      mockGet.mockRejectedValue(err);
      const { result } = renderHook(() => useSharedGameDetail({ id: SAMPLE_ID }), {
        wrapper: createWrapper(),
      });
      await waitFor(() => expect(result.current.isError).toBe(true));
      expect(result.current.status).toBe('error');
    });

    it('derives status="error" on non-typed Error (defensive default)', async () => {
      mockGet.mockRejectedValue(new Error('boom'));
      const { result } = renderHook(() => useSharedGameDetail({ id: SAMPLE_ID }), {
        wrapper: createWrapper(),
      });
      await waitFor(() => expect(result.current.isError).toBe(true));
      expect(result.current.status).toBe('error');
    });

    it('derives status="loading" when no data + no error + first fetch in flight', async () => {
      // Slow promise so loading state is observable
      let resolveFn: (v: SharedGameDetailV2) => void = () => {};
      mockGet.mockReturnValue(
        new Promise<SharedGameDetailV2>(resolve => {
          resolveFn = resolve;
        })
      );
      const { result } = renderHook(() => useSharedGameDetail({ id: SAMPLE_ID }), {
        wrapper: createWrapper(),
      });
      expect(result.current.status).toBe('loading');
      // Cleanup so the promise doesn't leak
      resolveFn(SAMPLE_DETAIL);
      await waitFor(() => expect(result.current.data).toBeDefined());
    });

    it('keeps status="error" priority over stale data during refetch failure', async () => {
      // First call resolves; second call rejects with 5xx — the FSM contract says
      // an error during refetch over stale data must surface "error", not "default".
      mockGet
        .mockResolvedValueOnce(SAMPLE_DETAIL)
        .mockRejectedValueOnce(new SharedGamesApiError('Boom', 'http', { httpStatus: 500 }));
      const { result } = renderHook(() => useSharedGameDetail({ id: SAMPLE_ID }), {
        wrapper: createWrapper(),
      });
      await waitFor(() => expect(result.current.data).toBeDefined());
      expect(result.current.status).toBe('empty'); // SAMPLE_DETAIL has zero counts

      await result.current.refetch();
      await waitFor(() => expect(result.current.isError).toBe(true));
      expect(result.current.status).toBe('error');
    });
  });
});
