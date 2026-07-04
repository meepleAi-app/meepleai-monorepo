/**
 * @vitest-environment jsdom
 *
 * useGameNightLive unit tests — #2633 Slice B (LD-1, LD-12).
 *
 * The hook is a single-endpoint React Query loader over GET /game-nights/{id}/live
 * that wires the PURE mapper through `select`, returning a NightLiveViewModel. The
 * clock is injected from `useNow` (mocked here to a fixed instant for determinism).
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { GameNightLiveDto } from '@/lib/api/schemas/game-nights.schemas';

import { gameNightLiveKeys, useGameNightLive } from '../useGameNightLive';

const getLiveMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({
  api: { gameNights: { getLive: getLiveMock } },
}));
vi.mock('@/hooks/useNow', () => ({
  useNow: () => new Date('2026-07-04T22:35:00Z'),
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

const GN_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

const LIVE_DTO: GameNightLiveDto = {
  id: GN_ID,
  title: 'Serata Eldoria',
  status: 'Published',
  sessions: [
    {
      sessionId: '11111111-1111-4111-8111-111111111111',
      gameId: '22222222-2222-4222-8222-222222222222',
      gameTitle: 'Brass',
      playOrder: 1,
      status: 'Completed',
      winnerId: null,
      startedAt: '2026-07-04T20:00:00Z',
      completedAt: '2026-07-04T21:53:00Z',
    },
    {
      sessionId: '33333333-3333-4333-8333-333333333333',
      gameId: '44444444-4444-4444-8444-444444444444',
      gameTitle: 'Spirit Island',
      playOrder: 2,
      status: 'InProgress',
      winnerId: null,
      startedAt: '2026-07-04T22:00:00Z',
      completedAt: null,
    },
  ],
  isViewerOrganizer: true,
  plannedLineup: [],
};

describe('useGameNightLive — query key factory', () => {
  it('namespaces the detail key under game-nights/live', () => {
    expect(gameNightLiveKeys.detail(GN_ID)).toEqual(['game-nights', 'live', GN_ID]);
  });
});

describe('useGameNightLive — mapping', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('delegates to api.gameNights.getLive and exposes the mapped viewmodel', async () => {
    getLiveMock.mockResolvedValue(LIVE_DTO);

    const { result } = renderHook(() => useGameNightLive(GN_ID), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(getLiveMock).toHaveBeenCalledWith(GN_ID);
    const vm = result.current.data!;
    expect(vm.night.title).toBe('Serata Eldoria');
    expect(vm.total).toBe(2);
    expect(vm.current).toBe(2);
    expect(vm.status).toBe('live');
    expect(vm.plannedGames.map(g => g.id)).toEqual([
      '11111111-1111-4111-8111-111111111111',
      '33333333-3333-4333-8333-333333333333',
    ]);
    // time derived against the mocked fixed `now` (22:35Z)
    expect(vm.elapsed).toBe('2h 35m');
    expect(vm.plannedGames[0].actual).toBe('113m');
  });

  it('does not fire when id is empty', () => {
    renderHook(() => useGameNightLive(''), { wrapper: createWrapper() });
    expect(getLiveMock).not.toHaveBeenCalled();
  });

  it('surfaces the rejection on error', async () => {
    getLiveMock.mockRejectedValue(new Error('boom'));

    const { result } = renderHook(() => useGameNightLive(GN_ID), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error?.message).toBe('boom');
  });
});
