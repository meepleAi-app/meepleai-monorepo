/**
 * @vitest-environment jsdom
 *
 * useResumeGamebookSitting unit tests — SI-4 (#2635).
 *
 * Resuming a gamebook campaign's game-night for a 2nd sitting opens a NEW live Session via the
 * Attach path (POST /game-nights/{id}/gamebook-sessions). Mirrors useStartNextGame: a max-live
 * 409 must reach the view discriminably so the resume CTA can surface a specific message.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ConflictError, ForbiddenError } from '@/lib/api/core/errors';
import {
  isMaxLiveBlockedError,
  MAX_LIVE_SESSIONS_EXCEEDED,
} from '@/lib/game-nights/hooks/useStartNextGame';

import { useResumeGamebookSitting } from '../useResumeGamebookSitting';

const attachMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api/clients/gameNightSessionClient', () => ({
  gameNightSessionClient: { attachGamebookCampaign: attachMock },
}));

const GN_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const CAMPAIGN_ID = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

describe('useResumeGamebookSitting', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('delegates to gameNightSessionClient.attachGamebookCampaign with the night + campaign id', async () => {
    attachMock.mockResolvedValue({
      sessionId: 's1',
      gameNightSessionId: 'gns1',
      sessionCode: 'ABC',
      playOrder: 2,
    });

    const { result } = renderHook(() => useResumeGamebookSitting(GN_ID), {
      wrapper: createWrapper(),
    });
    result.current.mutate({ campaignId: CAMPAIGN_ID });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(attachMock).toHaveBeenCalledWith(GN_ID, CAMPAIGN_ID);
    expect(result.current.data?.sessionId).toBe('s1');
  });

  it('surfaces the max-live ConflictError so the view can discriminate the 409', async () => {
    attachMock.mockRejectedValue(
      new ConflictError({ message: 'blocked', code: MAX_LIVE_SESSIONS_EXCEEDED })
    );

    const { result } = renderHook(() => useResumeGamebookSitting(GN_ID), {
      wrapper: createWrapper(),
    });
    result.current.mutate({ campaignId: CAMPAIGN_ID });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(isMaxLiveBlockedError(result.current.error)).toBe(true);
  });

  it('surfaces a ForbiddenError (non-organizer) unchanged', async () => {
    attachMock.mockRejectedValue(new ForbiddenError({ message: 'nope' }));

    const { result } = renderHook(() => useResumeGamebookSitting(GN_ID), {
      wrapper: createWrapper(),
    });
    result.current.mutate({ campaignId: CAMPAIGN_ID });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeInstanceOf(ForbiddenError);
  });
});
