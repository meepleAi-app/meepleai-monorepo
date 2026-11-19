/**
 * #5074: the private games page proposes through this hook, which calls the private game's proposal route.
 * Before, the page called CreateShareRequest, which knows only shared games and answered 404.
 */

import { QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';

const proposePrivateGame = vi.hoisted(() => vi.fn());
const createShareRequest = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({
  api: { shareRequests: { proposePrivateGame, createShareRequest } },
}));

import { useProposePrivateGame } from '../useShareRequests';

function wrapper({ children }: { children: ReactNode }) {
  return <QueryClientProvider client={createTestQueryClient()}>{children}</QueryClientProvider>;
}

describe('useProposePrivateGame (#5074)', () => {
  it('calls the private game proposal route, not CreateShareRequest', async () => {
    const reply = {
      shareRequestId: '4f3b2a10-6c1d-4e8f-9a2b-3c4d5e6f7a8b',
      status: 'Pending',
      contributionType: 'NewGameProposal',
      createdAt: '2026-11-19T08:00:00Z',
    };
    proposePrivateGame.mockResolvedValue(reply);
    const { result } = renderHook(() => useProposePrivateGame(), { wrapper });

    let answer: unknown;
    await act(async () => {
      answer = await result.current.mutateAsync({
        privateGameId: 'game-1',
        notes: 'Please add it',
      });
    });

    expect(proposePrivateGame).toHaveBeenCalledWith('game-1', 'Please add it');
    expect(createShareRequest).not.toHaveBeenCalled();
    expect(answer).toEqual(reply);
  });
});
