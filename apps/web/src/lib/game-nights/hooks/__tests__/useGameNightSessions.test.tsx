/**
 * @vitest-environment jsdom
 *
 * useGameNightSessions unit tests — #4752 (the night → live game bridge).
 *
 * The night DETAIL page lists the games of the night from the night-live read. The hook must share the
 * query (key and response) with `useGameNightLive` — one fetch, one invalidation — and project the response
 * through the pure mapper, including the live game of each sitting.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { GameNightLiveDto } from '@/lib/api/schemas/game-nights.schemas';

import { gameNightLiveKeys } from '../useGameNightLive';
import { useGameNightSessions } from '../useGameNightSessions';

const getLiveMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({
  api: { gameNights: { getLive: getLiveMock } },
}));

const GN_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const LIVE_ID = 'bbbb1111-1111-4111-8111-111111111111';

const LIVE_DTO: GameNightLiveDto = {
  id: GN_ID,
  title: 'Serata',
  status: 'InProgress',
  sessions: [
    {
      sessionId: '11111111-1111-4111-8111-111111111111',
      gameId: '22222222-2222-4222-8222-222222222222',
      gameTitle: 'Catan',
      playOrder: 1,
      status: 'InProgress',
      isLive: true,
      winnerId: null,
      winnerName: null,
      startedAt: '2026-07-04T20:00:00Z',
      completedAt: null,
      liveSessionId: LIVE_ID,
    },
  ],
  isViewerOrganizer: true,
  plannedLineup: [],
  currentSessionRoster: [],
};

function createClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false } } });
}

function wrapperFor(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

beforeEach(() => {
  getLiveMock.mockReset();
});

describe('useGameNightSessions', () => {
  it('lists the sittings of the night-live read, with the live game of each', async () => {
    getLiveMock.mockResolvedValue(LIVE_DTO);

    const { result } = renderHook(() => useGameNightSessions(GN_ID), {
      wrapper: wrapperFor(createClient()),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(getLiveMock).toHaveBeenCalledWith(GN_ID);
    expect(result.current.data).toEqual([
      expect.objectContaining({
        gameTitle: 'Catan',
        status: 'in_progress',
        liveSessionId: LIVE_ID,
      }),
    ]);
  });

  it('shares the query of useGameNightLive: the key is the same, so one response serves both', async () => {
    getLiveMock.mockResolvedValue(LIVE_DTO);
    const client = createClient();

    const { result } = renderHook(() => useGameNightSessions(GN_ID), {
      wrapper: wrapperFor(client),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    // The cache holds the RAW response under the night-live key — what the start/complete mutations
    // invalidate — and the sessions hook only projects it.
    expect(client.getQueryData(gameNightLiveKeys.detail(GN_ID))).toEqual(LIVE_DTO);
  });

  it('does not fetch when disabled (a Draft or Cancelled night has no games)', () => {
    renderHook(() => useGameNightSessions(GN_ID, false), { wrapper: wrapperFor(createClient()) });

    expect(getLiveMock).not.toHaveBeenCalled();
  });

  it('does not fetch without an id', () => {
    renderHook(() => useGameNightSessions(''), { wrapper: wrapperFor(createClient()) });

    expect(getLiveMock).not.toHaveBeenCalled();
  });

  it('does not repeat a refusal: a viewer the read refuses (403) gets no data and one request', async () => {
    getLiveMock.mockRejectedValue(new Error('403'));
    // The client's DEFAULT is to retry (three times, with no delay): the single request below is the hook's
    // own `retry: false`, not a client that happens to have retries off. The other tests use a client with
    // retries off and would pass either way.
    const retrying = new QueryClient({ defaultOptions: { queries: { retry: 3, retryDelay: 0 } } });

    const { result } = renderHook(() => useGameNightSessions(GN_ID), {
      wrapper: wrapperFor(retrying),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.data).toBeUndefined();
    expect(getLiveMock).toHaveBeenCalledTimes(1);
  });
});
