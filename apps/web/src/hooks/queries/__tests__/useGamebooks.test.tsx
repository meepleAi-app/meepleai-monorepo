/**
 * @vitest-environment jsdom
 *
 * useGamebooks / useQuotaInfo unit tests — SP6 Phase B Task 3 (Issue #788).
 *
 * Coverage:
 *   - Stable queryKey contract (gamebookKeys)
 *   - useGamebooks resolves to canonical fixture data
 *   - useQuotaInfo resolves to canonical fixture data
 *   - Hooks remain in sync with `gamebookIndexFixtures.default`
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { gamebookIndexFixtures } from '@/lib/gamebook-index';

import { gamebookKeys, useGamebooks, useQuotaInfo } from '../useGamebooks';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

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
  it('returns canonical fixture gamebooks on success', async () => {
    const { result } = renderHook(() => useGamebooks(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(gamebookIndexFixtures.default.gamebooks);
  });

  it('exposes 4 fixture gamebooks (1 ready Nanolith, 1 ready Brass, 1 indexing, 1 error)', async () => {
    const { result } = renderHook(() => useGamebooks(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toHaveLength(4);
    const statuses = (result.current.data ?? []).map(gb => gb.status);
    expect(statuses).toEqual(['ready', 'ready', 'indexing', 'error']);
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
