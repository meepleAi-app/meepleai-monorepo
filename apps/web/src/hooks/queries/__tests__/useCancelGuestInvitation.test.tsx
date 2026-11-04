/**
 * #4721: the organizer withdraws an invitation sent by email. The list on screen is stale after any outcome
 * — also a refused withdrawal (the guest answered in the meantime) — so the night's queries are refreshed on
 * settle, not only on success.
 */

import type { ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { gameNightKeys, useCancelGuestInvitation } from '../useGameNights';

const cancelGuestInvitation = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api', () => ({
  api: { gameNights: { cancelGuestInvitation } },
}));

const NIGHT_ID = '11111111-1111-4111-8111-111111111111';
const INVITATION_ID = '55555555-5555-4555-8555-555555555555';

function setup() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const invalidate = vi.spyOn(client, 'invalidateQueries');
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  return { invalidate, ...renderHook(() => useCancelGuestInvitation(NIGHT_ID), { wrapper }) };
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('useCancelGuestInvitation (#4721)', () => {
  it('withdraws that invitation of that night, and refreshes the night and its lists', async () => {
    cancelGuestInvitation.mockResolvedValue(undefined);
    const { result, invalidate } = setup();

    await act(async () => {
      await result.current.mutateAsync(INVITATION_ID);
    });

    expect(cancelGuestInvitation).toHaveBeenCalledWith(NIGHT_ID, INVITATION_ID);
    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: gameNightKeys.all }));
  });

  it('refreshes also when the withdrawal is refused, because what is on screen is stale', async () => {
    cancelGuestInvitation.mockRejectedValue(new Error('409'));
    const { result, invalidate } = setup();

    await act(async () => {
      await expect(result.current.mutateAsync(INVITATION_ID)).rejects.toThrow('409');
    });

    await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: gameNightKeys.all }));
  });
});
