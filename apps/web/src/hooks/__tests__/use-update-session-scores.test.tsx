/**
 * Tests for useUpdateSessionScores mutation hook.
 *
 * Asse D follow-up P1 (#1899) T5.
 */

import type { ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('@/lib/api/core/httpClient', () => ({
  getApiBase: () => 'http://test',
}));

import { UpdateSessionScoresError, useUpdateSessionScores } from '../use-update-session-scores';

function buildWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');
  function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }
  return { queryClient, invalidateSpy, Wrapper };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('useUpdateSessionScores', () => {
  it('serializes scoreData as JSON string and PUTs to the polymorphic endpoint on success', async () => {
    const fetchSpy = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({
        sessionId: 'session-1',
        scoringType: 'Points',
        computedWinnerId: 'p1',
      }),
    });
    globalThis.fetch = fetchSpy as typeof fetch;

    const { Wrapper } = buildWrapper();
    const { result } = renderHook(() => useUpdateSessionScores(), { wrapper: Wrapper });

    await act(async () => {
      await result.current.mutateAsync({
        sessionId: 'session-1',
        scoringType: 'Points',
        scoreData: { scores: [{ playerId: 'p1', points: 50 }] },
      });
    });

    expect(fetchSpy).toHaveBeenCalledWith(
      'http://test/api/v1/game-sessions/session-1/scores-polymorphic',
      expect.objectContaining({
        method: 'PUT',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
      })
    );

    const body = JSON.parse((fetchSpy.mock.calls[0][1] as RequestInit).body as string);
    expect(body.scoringType).toBe('Points');
    expect(typeof body.scoreData).toBe('string');
    expect(JSON.parse(body.scoreData)).toEqual({
      scores: [{ playerId: 'p1', points: 50 }],
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.computedWinnerId).toBe('p1');
  });

  it('throws UpdateSessionScoresError(kind="forbidden") on 403', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 403,
      json: async () => ({ error: 'forbidden' }),
    }) as typeof fetch;

    const { Wrapper } = buildWrapper();
    const { result } = renderHook(() => useUpdateSessionScores(), { wrapper: Wrapper });

    await act(async () => {
      await expect(
        result.current.mutateAsync({
          sessionId: 'session-1',
          scoringType: 'Points',
          scoreData: { scores: [] },
        })
      ).rejects.toBeInstanceOf(UpdateSessionScoresError);
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.kind).toBe('forbidden');
    expect(result.current.error?.status).toBe(403);
  });

  it('throws UpdateSessionScoresError(kind="validation") on 400 and exposes details', async () => {
    const details = { errors: [{ field: 'scores', message: 'empty' }] };
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 400,
      json: async () => details,
    }) as typeof fetch;

    const { Wrapper } = buildWrapper();
    const { result } = renderHook(() => useUpdateSessionScores(), { wrapper: Wrapper });

    await act(async () => {
      await expect(
        result.current.mutateAsync({
          sessionId: 'session-1',
          scoringType: 'Points',
          scoreData: { scores: [] },
        })
      ).rejects.toMatchObject({ kind: 'validation', status: 400 });
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.details).toEqual(details);
  });

  it('throws UpdateSessionScoresError(kind="server") on 500', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
      json: async () => ({ error: 'boom' }),
    }) as typeof fetch;

    const { Wrapper } = buildWrapper();
    const { result } = renderHook(() => useUpdateSessionScores(), { wrapper: Wrapper });

    await act(async () => {
      await expect(
        result.current.mutateAsync({
          sessionId: 'session-1',
          scoringType: 'Points',
          scoreData: { scores: [] },
        })
      ).rejects.toMatchObject({ kind: 'server', status: 500 });
    });
  });

  it('invalidates session and live-session caches on success', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({
        sessionId: 'session-1',
        scoringType: 'Points',
        computedWinnerId: null,
      }),
    }) as typeof fetch;

    const { invalidateSpy, Wrapper } = buildWrapper();
    const { result } = renderHook(() => useUpdateSessionScores(), { wrapper: Wrapper });

    await act(async () => {
      await result.current.mutateAsync({
        sessionId: 'session-1',
        scoringType: 'Points',
        scoreData: { scores: [] },
      });
    });

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['session', 'session-1'] });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['live-session', 'session-1'] });
  });
});
