import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import * as client from '@/lib/api/gamebook-campaigns';

import { useGamebookCampaign } from '../useGamebookCampaign';

vi.mock('@/lib/api/gamebook-campaigns');

// The hook returns whatever getCampaign resolves — no schema parsing in the hook layer.
// Using arbitrary strings is fine here since getCampaign is fully mocked.
const fakeCampaign = {
  id: 'abc',
  gameId: 'g',
  ownerUserId: 'o',
  title: 'C1',
  currentParagraph: 47,
  history: [47],
  lastReadAt: '2026-05-07T12:00:00Z',
  createdAt: '2026-05-07T12:00:00Z',
  updatedAt: '2026-05-07T12:00:00Z',
};

describe('useGamebookCampaign', () => {
  const wrapper =
    (qc: QueryClient) =>
    ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={qc}>{children}</QueryClientProvider>
    );

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('fetches campaign by id when enabled', async () => {
    vi.mocked(client.getCampaign).mockResolvedValueOnce(fakeCampaign as never);
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    const { result } = renderHook(() => useGamebookCampaign('abc'), { wrapper: wrapper(qc) });

    await waitFor(() => expect(result.current.data?.title).toBe('C1'));
    expect(client.getCampaign).toHaveBeenCalledWith('abc');
  });

  it('does not fetch when id is undefined', () => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useGamebookCampaign(undefined), { wrapper: wrapper(qc) });
    expect(client.getCampaign).not.toHaveBeenCalled();
  });
});
