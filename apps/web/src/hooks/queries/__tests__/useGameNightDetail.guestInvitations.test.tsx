/**
 * #4706: the guests invited by email are not in the RSVP roster, and the endpoint that lists them
 * is organizer-only. The detail hook fetches them for the host and for nobody else.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { api } from '@/lib/api';

import { useGameNightDetail } from '../useGameNightDetail';

vi.mock('@/lib/api', () => ({
  api: {
    gameNights: {
      getById: vi.fn(),
      getRsvps: vi.fn(),
      getGuestInvitations: vi.fn(),
      rsvp: vi.fn(),
    },
  },
}));

const NIGHT_ID = '11111111-1111-4111-8111-111111111111';
const HOST_ID = '22222222-2222-4222-8222-222222222222';
const MEMBER_ID = '33333333-3333-4333-8333-333333333333';

const event = {
  id: NIGHT_ID,
  organizerId: HOST_ID,
  organizerName: 'Anna',
  title: 'Serata Catan',
  status: 'Published',
  acceptedCount: 0,
  pendingCount: 1,
  totalInvited: 1,
};

const memberRsvp = {
  id: '44444444-4444-4444-8444-444444444444',
  userId: MEMBER_ID,
  userName: 'Bruno',
  status: 'Pending',
  respondedAt: null,
  createdAt: '2026-10-29T10:00:00Z',
};

const carla = {
  id: '55555555-5555-4555-8555-555555555555',
  email: 'carla@example.com',
  status: 'Accepted',
  respondedAt: '2026-10-29T11:00:00Z',
  respondedByName: 'Carla',
  expiresAt: '2026-11-05T10:00:00Z',
  createdAt: '2026-10-29T10:00:00Z',
};

function wrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

const gameNights = api.gameNights as unknown as {
  getById: ReturnType<typeof vi.fn>;
  getRsvps: ReturnType<typeof vi.fn>;
  getGuestInvitations: ReturnType<typeof vi.fn>;
};

beforeEach(() => {
  vi.clearAllMocks();
  gameNights.getById.mockResolvedValue(event);
  gameNights.getRsvps.mockResolvedValue([memberRsvp]);
  gameNights.getGuestInvitations.mockResolvedValue([carla]);
});

describe('useGameNightDetail — guests invited by email', () => {
  it('gives the host the guests, alongside the member roster', async () => {
    const { result } = renderHook(() => useGameNightDetail(NIGHT_ID, HOST_ID), {
      wrapper: wrapper(),
    });

    await waitFor(() => expect(result.current.guestInvitations).toEqual([carla]));
    expect(gameNights.getGuestInvitations).toHaveBeenCalledWith(NIGHT_ID);
    expect(result.current.rsvps).toEqual([memberRsvp]);
  });

  it('never asks for them on behalf of an invited member', async () => {
    const { result } = renderHook(() => useGameNightDetail(NIGHT_ID, MEMBER_ID), {
      wrapper: wrapper(),
    });

    await waitFor(() => expect(result.current.actor?.actor).toBe('guest'));
    expect(gameNights.getGuestInvitations).not.toHaveBeenCalled();
    expect(result.current.guestInvitations).toBeUndefined();
  });

  it('does not fail the page when the guests cannot be read', async () => {
    gameNights.getGuestInvitations.mockRejectedValue(new Error('boom'));

    const { result } = renderHook(() => useGameNightDetail(NIGHT_ID, HOST_ID), {
      wrapper: wrapper(),
    });

    await waitFor(() => expect(gameNights.getGuestInvitations).toHaveBeenCalled());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isError).toBe(false);
    expect(result.current.guestInvitations).toBeUndefined();
  });
});
