/**
 * Tests for useFriendsActivity hook.
 *
 * Asse C (#1898) WP5 T5.
 */

import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

vi.mock('@/lib/api/core/httpClient', () => ({
  getApiBase: () => 'http://test',
}));

import { useFriendsActivity, type FriendActivity } from '../use-friends-activity';

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

const mockActivities: FriendActivity[] = [
  {
    friendUserId: 'friend-1',
    avatar: 'https://example.com/a1.jpg',
    name: 'Marco',
    verb: 'completed',
    gameOrEventId: 'game-1',
    gameOrEventType: 'game',
    gameOrEventName: 'Catan',
    timestamp: '2026-06-05T10:00:00Z',
  },
];

beforeEach(() => {
  vi.clearAllMocks();
});

describe('useFriendsActivity', () => {
  it('fetches activities successfully and returns array', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => mockActivities,
    }) as typeof fetch;

    const { result } = renderHook(() => useFriendsActivity(), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data).toEqual(mockActivities);
    expect(globalThis.fetch).toHaveBeenCalledWith(
      'http://test/api/v1/dashboard/friends-activity?limit=10',
      expect.objectContaining({ credentials: 'include' })
    );
  });

  it('returns empty array on 401 (silent fallback for unauthenticated)', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      json: async () => ({ error: 'unauthorized' }),
    }) as typeof fetch;

    const { result } = renderHook(() => useFriendsActivity(), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data).toEqual([]);
  });

  it('throws error on 500 server error', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
      json: async () => ({ error: 'server error' }),
    }) as typeof fetch;

    const { result } = renderHook(() => useFriendsActivity(), { wrapper });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    expect((result.current.error as Error).message).toContain('500');
  });

  it('respects enabled=false (no fetch)', () => {
    globalThis.fetch = vi.fn() as typeof fetch;

    renderHook(() => useFriendsActivity({ enabled: false }), { wrapper });

    expect(globalThis.fetch).not.toHaveBeenCalled();
  });

  it('respects limit query param', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => mockActivities,
    }) as typeof fetch;

    const { result } = renderHook(() => useFriendsActivity({ limit: 5 }), { wrapper });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(globalThis.fetch).toHaveBeenCalledWith(
      'http://test/api/v1/dashboard/friends-activity?limit=5',
      expect.any(Object)
    );
  });
});
