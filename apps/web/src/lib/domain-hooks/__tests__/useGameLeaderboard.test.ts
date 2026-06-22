import { describe, it, expect, beforeEach, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { createElement, type ReactNode } from 'react';

import { useGameLeaderboard, GAME_LEADERBOARD_QUERY_KEY } from '../useGameLeaderboard';
import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';

vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getLeaderboard: vi.fn(),
    },
  },
}));

const mockResponse = {
  gameId: '11111111-1111-1111-1111-111111111111',
  entries: [
    {
      playerId: '22222222-2222-2222-2222-222222222222',
      displayName: 'Marco Rossi',
      initials: 'MR',
      wins: 8,
      plays: 14,
      avgScore: 87.5,
      lastPlayedAt: '2026-05-20T12:00:00.000Z',
    },
  ],
  returnedCount: 1,
  since: null,
};

describe('useGameLeaderboard (#1467)', () => {
  let queryClient: QueryClient;

  const createWrapper = () => {
    return ({ children }: { children: ReactNode }) =>
      createElement(QueryClientProvider, { client: queryClient }, children);
  };

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  it('generates a stable query key with since/limit defaults', () => {
    expect(GAME_LEADERBOARD_QUERY_KEY('game-1')).toEqual(['game-leaderboard', 'game-1', null, 10]);
    expect(GAME_LEADERBOARD_QUERY_KEY('game-1', '2026-01-01T00:00:00.000Z', 5)).toEqual([
      'game-leaderboard',
      'game-1',
      '2026-01-01T00:00:00.000Z',
      5,
    ]);
  });

  it('fetches the leaderboard via the games client', async () => {
    const { api } = await import('@/lib/api');
    vi.mocked(api.games.getLeaderboard).mockResolvedValueOnce(mockResponse);

    const { result } = renderHook(() => useGameLeaderboard('game-1'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual(mockResponse);
    // After the #1466 `enabled` option destructure, no-options calls forward `{}`
    // (semantically equivalent to undefined — no since/limit params).
    expect(api.games.getLeaderboard).toHaveBeenCalledWith('game-1', {});
  });

  it('forwards since/limit options to the client', async () => {
    const { api } = await import('@/lib/api');
    vi.mocked(api.games.getLeaderboard).mockResolvedValueOnce(mockResponse);
    const options = { since: '2026-01-01T00:00:00.000Z', limit: 5 };

    const { result } = renderHook(() => useGameLeaderboard('game-1', options), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(api.games.getLeaderboard).toHaveBeenCalledWith('game-1', options);
  });

  it('is disabled when gameId is empty', () => {
    const { result } = renderHook(() => useGameLeaderboard(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.fetchStatus).toBe('idle');
  });
});
