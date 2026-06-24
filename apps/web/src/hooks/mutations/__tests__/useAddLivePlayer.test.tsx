/**
 * Tests for useAddLivePlayer (Issue #2505).
 *
 * Covers:
 *   - mutate calls api.liveSessions.addPlayer with correct sessionId + request
 *   - success → invalidates liveSessionKeys.detail(sessionId)
 *   - error → does NOT invalidate queries
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import type { JSX, ReactNode } from 'react';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { useAddLivePlayer } from '../useAddLivePlayer';
import { liveSessionKeys } from '@/hooks/queries/useLiveSession';

// ---------------------------------------------------------------------------
// Mock @/lib/api
// ---------------------------------------------------------------------------

const addPlayerMock = vi.fn<[string, unknown], Promise<string>>();

vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      addPlayer: (sessionId: string, req: unknown) => addPlayerMock(sessionId, req),
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

const SESSION_ID = '00000000-0000-4000-8000-000000002505';
const PLAYER_ID = '00000000-0000-4000-8000-000000000001';

const BASE_REQUEST = {
  displayName: 'Marco',
  color: 'Red' as const,
};

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('useAddLivePlayer (#2505)', () => {
  beforeEach(() => {
    addPlayerMock.mockReset();
  });

  it('calls api.liveSessions.addPlayer with correct sessionId and request', async () => {
    addPlayerMock.mockResolvedValueOnce(PLAYER_ID);

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useAddLivePlayer(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      await result.current.mutateAsync(BASE_REQUEST);
    });

    expect(addPlayerMock).toHaveBeenCalledOnce();
    expect(addPlayerMock).toHaveBeenCalledWith(SESSION_ID, BASE_REQUEST);
  });

  it('returns the playerId string on success', async () => {
    addPlayerMock.mockResolvedValueOnce(PLAYER_ID);

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useAddLivePlayer(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    let data: string | undefined;
    await act(async () => {
      data = await result.current.mutateAsync(BASE_REQUEST);
    });

    expect(data).toBe(PLAYER_ID);
  });

  it('invalidates liveSessionKeys.detail on success', async () => {
    addPlayerMock.mockResolvedValueOnce(PLAYER_ID);

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');

    const { result } = renderHook(() => useAddLivePlayer(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      await result.current.mutateAsync(BASE_REQUEST);
    });

    expect(invalidateSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        queryKey: liveSessionKeys.detail(SESSION_ID),
      })
    );
  });

  it('does NOT invalidate queries on error', async () => {
    addPlayerMock.mockRejectedValueOnce(new Error('Network error'));

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries');

    const { result } = renderHook(() => useAddLivePlayer(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    await act(async () => {
      try {
        await result.current.mutateAsync(BASE_REQUEST);
      } catch {
        // expected
      }
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(invalidateSpy).not.toHaveBeenCalled();
  });

  it('passes userId when provided (registered user)', async () => {
    addPlayerMock.mockResolvedValueOnce(PLAYER_ID);

    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
    const { result } = renderHook(() => useAddLivePlayer(SESSION_ID), {
      wrapper: makeWrapper(qc),
    });

    const requestWithUser = {
      displayName: 'Giulia',
      color: 'Blue' as const,
      userId: '99999999-9999-9999-9999-999999999999',
    };

    await act(async () => {
      await result.current.mutateAsync(requestWithUser);
    });

    expect(addPlayerMock).toHaveBeenCalledWith(SESSION_ID, requestWithUser);
  });
});
