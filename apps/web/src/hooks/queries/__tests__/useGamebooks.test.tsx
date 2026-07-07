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
 *   - useQuotaInfo fetches via `fetchUserQuota` (#2750 C14 — GET /api/v1/users/me/quota)
 *     and propagates errors to the query state
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { gamebookIndexFixtures, type GamebookCardData, type QuotaInfo } from '@/lib/gamebook-index';

const fetchUserGamebooksMock =
  vi.fn<(signal?: AbortSignal) => Promise<readonly GamebookCardData[]>>();

vi.mock('@/lib/api/gamebooks-list', () => ({
  fetchUserGamebooks: (signal?: AbortSignal) => fetchUserGamebooksMock(signal),
}));

const fetchUserQuotaMock = vi.fn<(signal?: AbortSignal) => Promise<QuotaInfo>>();

vi.mock('@/lib/api/gamebook-quota', () => ({
  fetchUserQuota: (signal?: AbortSignal) => fetchUserQuotaMock(signal),
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
  fetchUserQuotaMock.mockReset();
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
  it('returns the quota resolved by fetchUserQuota', async () => {
    fetchUserQuotaMock.mockResolvedValue(gamebookIndexFixtures.default.quota);

    const { result } = renderHook(() => useQuotaInfo(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(fetchUserQuotaMock).toHaveBeenCalledTimes(1);
    expect(result.current.data).toEqual(gamebookIndexFixtures.default.quota);
  });

  it('surfaces the real per-user quota from the endpoint', async () => {
    const real: QuotaInfo = {
      used: 3,
      total: 50,
      resetDate: '2026-08-01T00:00:00.000Z',
      tier: 'free',
    };
    fetchUserQuotaMock.mockResolvedValue(real);

    const { result } = renderHook(() => useQuotaInfo(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data?.used).toBe(3);
    expect(result.current.data?.total).toBe(50);
    expect(result.current.data?.tier).toBe('free');
  });

  it('exposes the fetch error to the query state', async () => {
    const failure = new Error('Quota API error 401: unauthorized');
    fetchUserQuotaMock.mockRejectedValue(failure);

    const { result } = renderHook(() => useQuotaInfo(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBe(failure);
  });
});
