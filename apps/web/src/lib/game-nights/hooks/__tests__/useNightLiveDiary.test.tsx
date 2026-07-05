/**
 * @vitest-environment jsdom
 *
 * useNightLiveDiary unit tests — #2633 Slice C2.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useNightLiveDiary } from '../useNightLiveDiary';

const getDiaryMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api/clients/gameNightSessionClient', () => ({
  gameNightSessionClient: { getDiary: getDiaryMock },
}));

const GN = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const entry = (over: Record<string, unknown> = {}) => ({
  id: '11111111-1111-4111-8111-111111111111',
  sessionId: '22222222-2222-4222-8222-222222222222',
  eventType: 'score_updated',
  description: '📊',
  payload: null,
  actorId: null,
  timestamp: '2026-07-04T20:00:00',
  ...over,
});

function wrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  };
}

describe('useNightLiveDiary', () => {
  beforeEach(() => vi.clearAllMocks());

  it('loads + resiliently parses the diary (drops a malformed row, keeps the rest)', async () => {
    getDiaryMock.mockResolvedValue({
      gameNightId: GN,
      entries: [
        entry(),
        { id: 'not-a-uuid' },
        entry({ id: '33333333-3333-4333-8333-333333333333' }),
      ],
    });

    const { result } = renderHook(() => useNightLiveDiary(GN), { wrapper: wrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(getDiaryMock).toHaveBeenCalledWith(GN);
    expect(result.current.data?.entries).toHaveLength(2);
  });

  it('is disabled for an empty id', () => {
    const { result } = renderHook(() => useNightLiveDiary(''), { wrapper: wrapper() });
    expect(result.current.fetchStatus).toBe('idle');
    expect(getDiaryMock).not.toHaveBeenCalled();
  });
});
