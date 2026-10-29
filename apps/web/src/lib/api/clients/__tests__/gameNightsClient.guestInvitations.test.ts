/**
 * gameNightsClient — the guests invited by email (#4706) and their withdrawal (#4721).
 *
 * The reads feed the response through the schema; the withdrawal is one DELETE on the guest-invitations route
 * of that night, with nothing in the body.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createGameNightsClient } from '../gameNightsClient';

const mockHttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
};

const GN_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const INVITATION_ID = '55555555-5555-4555-8555-555555555555';

const GUEST = {
  id: INVITATION_ID,
  email: 'carla@example.com',
  status: 'Accepted',
  respondedAt: '2026-10-29T11:00:00Z',
  respondedByName: 'Carla',
  expiresAt: '2026-11-05T10:00:00Z',
  createdAt: '2026-10-29T10:00:00Z',
};

const client = createGameNightsClient({
  httpClient: mockHttpClient as unknown as Parameters<
    typeof createGameNightsClient
  >[0]['httpClient'],
});

beforeEach(() => {
  vi.clearAllMocks();
});

describe('gameNightsClient.getGuestInvitations (#4706)', () => {
  it('reads the guests of that night and parses them', async () => {
    mockHttpClient.get.mockResolvedValue([GUEST]);

    const guests = await client.getGuestInvitations(GN_ID);

    expect(mockHttpClient.get).toHaveBeenCalledWith(
      `/api/v1/game-nights/${GN_ID}/guest-invitations`
    );
    expect(guests).toEqual([GUEST]);
  });

  it('treats an empty answer as no guests', async () => {
    mockHttpClient.get.mockResolvedValue(null);

    await expect(client.getGuestInvitations(GN_ID)).resolves.toEqual([]);
  });
});

describe('gameNightsClient.cancelGuestInvitation (#4721)', () => {
  it('sends one DELETE to that invitation of that night', async () => {
    mockHttpClient.delete.mockResolvedValue(undefined);

    await client.cancelGuestInvitation(GN_ID, INVITATION_ID);

    expect(mockHttpClient.delete).toHaveBeenCalledTimes(1);
    expect(mockHttpClient.delete).toHaveBeenCalledWith(
      `/api/v1/game-nights/${GN_ID}/guest-invitations/${INVITATION_ID}`
    );
  });

  it('lets a refusal reach the caller', async () => {
    mockHttpClient.delete.mockRejectedValue(new Error('409'));

    await expect(client.cancelGuestInvitation(GN_ID, INVITATION_ID)).rejects.toThrow('409');
  });
});
