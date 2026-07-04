/**
 * @vitest-environment jsdom
 *
 * useStartNextGame unit tests — #2633 WS1 DEC-10.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ConflictError, NotFoundError } from '@/lib/api/core/errors';

import {
  isMaxLiveBlockedError,
  MAX_LIVE_SESSIONS_EXCEEDED,
  useStartNextGame,
} from '../useStartNextGame';

const startNextGameMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({
  api: { gameNights: { startNextGame: startNextGameMock } },
}));

const GN_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

describe('isMaxLiveBlockedError', () => {
  it('is true only for a ConflictError carrying the MAX_LIVE code', () => {
    expect(
      isMaxLiveBlockedError(new ConflictError({ message: 'x', code: MAX_LIVE_SESSIONS_EXCEEDED }))
    ).toBe(true);
    expect(isMaxLiveBlockedError(new ConflictError({ message: 'x', code: 'other' }))).toBe(false);
    expect(isMaxLiveBlockedError(new ConflictError({ message: 'x' }))).toBe(false);
    expect(isMaxLiveBlockedError(new NotFoundError({ message: 'x' }))).toBe(false);
    expect(isMaxLiveBlockedError(new Error('x'))).toBe(false);
  });
});

describe('useStartNextGame', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('delegates to api.gameNights.startNextGame with the game id + title', async () => {
    startNextGameMock.mockResolvedValue({
      sessionId: 's1',
      gameNightSessionId: 'gns1',
      sessionCode: 'ABC',
      playOrder: 2,
    });

    const { result } = renderHook(() => useStartNextGame(GN_ID), { wrapper: createWrapper() });
    result.current.mutate({ gameId: 'g1', gameTitle: 'Catan' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(startNextGameMock).toHaveBeenCalledWith(GN_ID, 'g1', 'Catan');
  });

  it('surfaces the ConflictError so the view can discriminate the max-live 409', async () => {
    startNextGameMock.mockRejectedValue(
      new ConflictError({ message: 'blocked', code: MAX_LIVE_SESSIONS_EXCEEDED })
    );

    const { result } = renderHook(() => useStartNextGame(GN_ID), { wrapper: createWrapper() });
    result.current.mutate({ gameId: 'g1', gameTitle: 'Catan' });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(isMaxLiveBlockedError(result.current.error)).toBe(true);
  });
});
