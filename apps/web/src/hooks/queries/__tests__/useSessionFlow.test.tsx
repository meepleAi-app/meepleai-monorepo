/**
 * @vitest-environment jsdom
 *
 * useCompleteGameNight cache-invalidation tests — SI-3 #2634.
 *
 * On a successful `/complete`, the hook must invalidate NOT ONLY the existing
 * diary/detail/all keys, but ALSO (SI-3) the night-live read + the whole gamebook
 * campaign family — so the cross-surface "Serata" spine strip on the gamebook play
 * page reflects the in-progress→completed transition (the strip is keyed by
 * campaignId, unknown at finalize time → the broad campaign-family invalidation).
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { gameNightLiveKeys } from '@/lib/game-nights/hooks/useGameNightLive';
import { gamebookCampaignKeys } from '@/lib/gamebook/hooks/useGamebookCampaign';

import { gameNightKeys } from '../useGameNights';
import { sessionFlowKeys, useCompleteGameNight } from '../useSessionFlow';

const completeGameNightMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({
  api: { sessionFlow: { completeGameNight: completeGameNightMock } },
}));

const GN = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

function setup() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const invalidateSpy = vi.spyOn(qc, 'invalidateQueries').mockResolvedValue(undefined);
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
  return { invalidateSpy, wrapper };
}

describe('useCompleteGameNight — cache invalidation (SI-3)', () => {
  beforeEach(() => vi.clearAllMocks());

  it('calls the API with the game night id', async () => {
    completeGameNightMock.mockResolvedValue(undefined);
    const { wrapper } = setup();
    const { result } = renderHook(() => useCompleteGameNight(), { wrapper });

    result.current.mutate(GN);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(completeGameNightMock).toHaveBeenCalledWith(GN);
  });

  it('invalidates the diary/detail/all keys on success (existing contract preserved)', async () => {
    completeGameNightMock.mockResolvedValue(undefined);
    const { invalidateSpy, wrapper } = setup();
    const { result } = renderHook(() => useCompleteGameNight(), { wrapper });

    result.current.mutate(GN);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: sessionFlowKeys.gameNightDiary(GN) });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: gameNightKeys.detail(GN) });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: gameNightKeys.all });
  });

  it('ALSO invalidates the night-live read + the gamebook campaign family (SI-3 cross-surface)', async () => {
    completeGameNightMock.mockResolvedValue(undefined);
    const { invalidateSpy, wrapper } = setup();
    const { result } = renderHook(() => useCompleteGameNight(), { wrapper });

    result.current.mutate(GN);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: gameNightLiveKeys.detail(GN) });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: gamebookCampaignKeys.all });
  });
});
