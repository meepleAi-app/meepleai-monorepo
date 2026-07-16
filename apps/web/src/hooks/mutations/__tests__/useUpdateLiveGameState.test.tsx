/**
 * Tests for useUpdateLiveGameState (#3025 L1).
 *
 * Covers:
 *   - mutate calls api.liveSessions.updateGameState with sessionId + state
 *   - success → invalidates liveSessionKeys.detail(sessionId)
 *   - error → does NOT invalidate queries
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import type { JSX, ReactNode } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { useUpdateLiveGameState } from '../useUpdateLiveGameState';
import { liveSessionKeys } from '@/hooks/queries/useLiveSession';

// ---------------------------------------------------------------------------
// Mock @/lib/api
// ---------------------------------------------------------------------------

const updateGameStateMock = vi.fn<[string, unknown], Promise<void>>();

vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      updateGameState: (sessionId: string, state: unknown) => updateGameStateMock(sessionId, state),
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

const SESSION_ID = '00000000-0000-4000-8000-000000003025';
const STATE = { round: 3, activePlayer: 'p1' };

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('useUpdateLiveGameState (#3025 L1)', () => {
  beforeEach(() => {
    updateGameStateMock.mockReset();
  });

  it('calls api.liveSessions.updateGameState with sessionId and state', async () => {
    updateGameStateMock.mockResolvedValueOnce(undefined);

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useUpdateLiveGameState(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      await result.current.mutateAsync(STATE);
    });

    expect(updateGameStateMock).toHaveBeenCalledOnce();
    expect(updateGameStateMock).toHaveBeenCalledWith(SESSION_ID, STATE);
  });

  it('invalidates liveSessionKeys.detail on success', async () => {
    updateGameStateMock.mockResolvedValueOnce(undefined);

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');

    const { result } = renderHook(() => useUpdateLiveGameState(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      await result.current.mutateAsync(STATE);
    });

    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        queryKey: liveSessionKeys.detail(SESSION_ID),
      })
    );
  });

  it('does NOT invalidate queries on error', async () => {
    updateGameStateMock.mockRejectedValueOnce(new Error('Network error'));

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');

    const { result } = renderHook(() => useUpdateLiveGameState(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      try {
        await result.current.mutateAsync(STATE);
      } catch {
        // expected
      }
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(invalidateSpy).not.toHaveBeenCalled();
  });

  it('forwards a null state (clearing the game-state)', async () => {
    updateGameStateMock.mockResolvedValueOnce(undefined);

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useUpdateLiveGameState(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      await result.current.mutateAsync(null);
    });

    expect(updateGameStateMock).toHaveBeenCalledWith(SESSION_ID, null);
  });
});
