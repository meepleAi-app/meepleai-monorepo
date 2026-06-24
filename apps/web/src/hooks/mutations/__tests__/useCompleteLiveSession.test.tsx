/**
 * Tests for useCompleteLiveSession (Issue #2503).
 *
 * Covers:
 *   - mutate calls api.liveSessions.completeSession with correct sessionId
 *   - success → invalidates liveSessionKeys.detail(sessionId)
 *   - error → does NOT invalidate queries
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import type { JSX, ReactNode } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { useCompleteLiveSession } from '../useCompleteLiveSession';
import { liveSessionKeys } from '@/hooks/queries/useLiveSession';

// ---------------------------------------------------------------------------
// Mock @/lib/api
// ---------------------------------------------------------------------------

const completeSessionMock = vi.fn<[string], Promise<void>>();

vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      completeSession: (sessionId: string) => completeSessionMock(sessionId),
    },
  },
}));

// ---------------------------------------------------------------------------
// Wrapper factory
// ---------------------------------------------------------------------------

function makeWrapper(qc: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }): JSX.Element {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  };
}

const SESSION_ID = '00000000-0000-4000-8000-000000002503';

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('useCompleteLiveSession (#2503)', () => {
  beforeEach(() => {
    completeSessionMock.mockReset();
  });

  it('calls api.liveSessions.completeSession with the correct sessionId', async () => {
    completeSessionMock.mockResolvedValueOnce(undefined);

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useCompleteLiveSession(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      await result.current.mutateAsync();
    });

    expect(completeSessionMock).toHaveBeenCalledOnce();
    expect(completeSessionMock).toHaveBeenCalledWith(SESSION_ID);
  });

  it('invalidates liveSessionKeys.detail on success', async () => {
    completeSessionMock.mockResolvedValueOnce(undefined);

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');

    const { result } = renderHook(() => useCompleteLiveSession(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      await result.current.mutateAsync();
    });

    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        queryKey: liveSessionKeys.detail(SESSION_ID),
      })
    );
  });

  it('does NOT invalidate queries on error', async () => {
    completeSessionMock.mockRejectedValueOnce(new Error('Network error'));

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');

    const { result } = renderHook(() => useCompleteLiveSession(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      try {
        await result.current.mutateAsync();
      } catch {
        // expected
      }
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(invalidateSpy).not.toHaveBeenCalled();
  });

  it('isPending is true while mutation is in-flight', async () => {
    let resolveComplete!: () => void;
    completeSessionMock.mockReturnValueOnce(
      new Promise<void>(res => {
        resolveComplete = res;
      })
    );

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useCompleteLiveSession(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    act(() => {
      result.current.mutate();
    });

    await waitFor(() => expect(result.current.isPending).toBe(true));

    await act(async () => {
      resolveComplete();
    });

    await waitFor(() => expect(result.current.isPending).toBe(false));
  });
});
