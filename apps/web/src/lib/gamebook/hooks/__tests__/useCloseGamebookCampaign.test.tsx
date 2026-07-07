import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import * as clientMod from '@/lib/api/gamebook-campaigns';
import { GamebookCampaignOutcome } from '@/lib/api/gamebook-campaigns';

import { useCloseGamebookCampaign } from '../useCloseGamebookCampaign';
import { gamebookCampaignKeys } from '../useGamebookCampaign';

vi.mock('@/lib/api/gamebook-campaigns', async importOriginal => {
  const actual = await importOriginal<typeof import('@/lib/api/gamebook-campaigns')>();
  return { ...actual, closeCampaign: vi.fn() };
});

const CAMPAIGN_ID = 'abc';
const closed = { id: CAMPAIGN_ID, title: 'C1', outcome: 1 } as never;

describe('useCloseGamebookCampaign', () => {
  const wrapper =
    (qc: QueryClient) =>
    ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={qc}>{children}</QueryClientProvider>
    );

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls closeCampaign with the outcome and primes the detail cache', async () => {
    vi.mocked(clientMod.closeCampaign).mockResolvedValueOnce(closed);
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });

    const { result } = renderHook(() => useCloseGamebookCampaign(CAMPAIGN_ID), {
      wrapper: wrapper(qc),
    });

    result.current.mutate(GamebookCampaignOutcome.Completed);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(clientMod.closeCampaign).toHaveBeenCalledWith(
      CAMPAIGN_ID,
      GamebookCampaignOutcome.Completed
    );
    expect(qc.getQueryData(gamebookCampaignKeys.detail(CAMPAIGN_ID))).toEqual(closed);
  });

  it('surfaces the error on a failed close', async () => {
    vi.mocked(clientMod.closeCampaign).mockRejectedValueOnce(new Error('409'));
    const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });

    const { result } = renderHook(() => useCloseGamebookCampaign(CAMPAIGN_ID), {
      wrapper: wrapper(qc),
    });

    result.current.mutate(GamebookCampaignOutcome.Abandoned);

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
