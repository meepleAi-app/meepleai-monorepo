/**
 * @vitest-environment jsdom
 *
 * useGamebooks / useQuotaInfo unit tests — SP6 Phase B Task 3 (Issue #788),
 * updated for backend wiring (Issue #869).
 *
 * Coverage:
 *   - Stable queryKey contract (gamebookKeys)
 *   - useGamebooks fetches via `fetchUserGamebooks` and surfaces the response
 *   - useGamebooks propagates fetch errors to the query state
 *   - useQuotaInfo still returns canonical fixture data (Gate B carryover —
 *     backend GET /api/v1/users/me/quota deferred to follow-up)
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { gamebookIndexFixtures, type GamebookCardData } from '@/lib/gamebook-index';

const fetchUserGamebooksMock =
  vi.fn<(signal?: AbortSignal) => Promise<readonly GamebookCardData[]>>();

vi.mock('@/lib/api/gamebooks-list', () => ({
  fetchUserGamebooks: (signal?: AbortSignal) => fetchUserGamebooksMock(signal),
}));

import { gamebookKeys, useGamebooks, useQuotaInfo } from '../useGamebooks';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

beforeEach(() => {
  fetchUserGamebooksMock.mockReset();
});

describe('gamebookKeys', () => {
  it('returns stable key for myGamebooks', () => {
    expect(gamebookKeys.myGamebooks()).toEqual(['gamebooks', 'me']);
  });

  it('returns stable key for quota', () => {
    expect(gamebookKeys.quota()).toEqual(['gamebooks', 'quota']);
  });

  it('exposes "all" base key', () => {
    expect(gamebookKeys.all).toEqual(['gamebooks']);
  });
});

describe('useGamebooks', () => {
  it('returns the gamebooks resolved by fetchUserGamebooks', async () => {
    fetchUserGamebooksMock.mockResolvedValue(gamebookIndexFixtures.default.gamebooks);

    const { result } = renderHook(() => useGamebooks(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(fetchUserGamebooksMock).toHaveBeenCalledTimes(1);
    expect(result.current.data).toEqual(gamebookIndexFixtures.default.gamebooks);
  });

  it('returns an empty list without throwing when the user has no gamebooks', async () => {
    fetchUserGamebooksMock.mockResolvedValue([]);

    const { result } = renderHook(() => useGamebooks(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual([]);
  });

  it('exposes the fetch error to the query state', async () => {
    const failure = new Error('Gamebooks list API error 500: oops');
    fetchUserGamebooksMock.mockRejectedValue(failure);

    const { result } = renderHook(() => useGamebooks(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBe(failure);
  });
});

describe('useQuotaInfo', () => {
  it('returns canonical fixture quota on success', async () => {
    const { result } = renderHook(() => useQuotaInfo(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(gamebookIndexFixtures.default.quota);
  });

  it('exposes default quota 12/50 for free tier', async () => {
    const { result } = renderHook(() => useQuotaInfo(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.used).toBe(12);
    expect(result.current.data?.total).toBe(50);
    expect(result.current.data?.tier).toBe('free');
  });
});
