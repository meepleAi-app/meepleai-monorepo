/**
 * useRecordRoundScore — #4156 fetta 2: a new cell is a POST, a correction a PUT, both on
 * `/api/v1/live-sessions/{id}/scores`; afterwards the live session is refetched.
 */

import { QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactNode } from 'react';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';

import { liveSessionKeys } from '../useLiveSession';
import { useRecordRoundScore } from '../useRecordRoundScore';

const recordScore = vi.hoisted(() => vi.fn());
const editScore = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({ api: { liveSessions: { recordScore, editScore } } }));

const REQUEST = { playerId: 'giulia', round: 2, dimension: 'punti', value: 30 };

function setup() {
  const queryClient = createTestQueryClient();
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries');
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  const { result } = renderHook(() => useRecordRoundScore('live-1'), { wrapper });
  return { result, invalidate };
}

describe('useRecordRoundScore', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    recordScore.mockResolvedValue(undefined);
    editScore.mockResolvedValue(undefined);
  });

  it('records a new cell with POST and refetches the live session', async () => {
    const { result, invalidate } = setup();
    await result.current.mutateAsync({ request: REQUEST, correction: false });
    expect(recordScore).toHaveBeenCalledWith('live-1', REQUEST);
    expect(editScore).not.toHaveBeenCalled();
    await waitFor(() =>
      expect(invalidate).toHaveBeenCalledWith({ queryKey: liveSessionKeys.detail('live-1') })
    );
  });

  it('corrects a cell with PUT', async () => {
    const { result } = setup();
    await result.current.mutateAsync({ request: REQUEST, correction: true });
    expect(editScore).toHaveBeenCalledWith('live-1', REQUEST);
    expect(recordScore).not.toHaveBeenCalled();
  });
});
