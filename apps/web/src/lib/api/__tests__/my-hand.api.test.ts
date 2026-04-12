import { describe, it, expect, vi, beforeEach } from 'vitest';
import { getMyHand, updateHandSlot, clearHandSlot } from '../my-hand';

vi.mock('../client', () => ({
  apiClient: {
    get: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

import { apiClient } from '../client';

describe('my-hand API client', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('getMyHand calls GET /users/me/hand', async () => {
    (apiClient.get as ReturnType<typeof vi.fn>).mockResolvedValue([
      {
        slotType: 'toolkit',
        entityId: null,
        entityType: null,
        entityLabel: null,
        entityImageUrl: null,
        pinnedAt: null,
      },
    ]);

    const result = await getMyHand();
    expect(apiClient.get).toHaveBeenCalledWith('/users/me/hand');
    expect(result).toHaveLength(1);
  });

  it('updateHandSlot calls PUT /users/me/hand/{slotType}', async () => {
    const dto = {
      slotType: 'game',
      entityId: 'g-1',
      entityType: 'game',
      entityLabel: 'Catan',
      entityImageUrl: null,
      pinnedAt: '2026-04-09T00:00:00Z',
    };
    (apiClient.put as ReturnType<typeof vi.fn>).mockResolvedValue(dto);

    const result = await updateHandSlot('game', {
      entityId: 'g-1',
      entityType: 'game',
      entityLabel: 'Catan',
      entityImageUrl: null,
    });
    expect(apiClient.put).toHaveBeenCalledWith('/users/me/hand/game', {
      entityId: 'g-1',
      entityType: 'game',
      entityLabel: 'Catan',
      entityImageUrl: null,
    });
    expect(result.entityId).toBe('g-1');
  });

  it('clearHandSlot calls DELETE /users/me/hand/{slotType}', async () => {
    (apiClient.delete as ReturnType<typeof vi.fn>).mockResolvedValue(undefined);

    await clearHandSlot('toolkit');
    expect(apiClient.delete).toHaveBeenCalledWith('/users/me/hand/toolkit');
  });

  it('getMyHand returns empty array on null response', async () => {
    (apiClient.get as ReturnType<typeof vi.fn>).mockResolvedValue(null);
    const result = await getMyHand();
    expect(result).toEqual([]);
  });
});
