import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { MockedApiClient } from '@/test-utils/api-client-mock';

import { getMyHand, updateHandSlot, clearHandSlot } from '../my-hand';

const mockApi = vi.hoisted<MockedApiClient>(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
  head: vi.fn(),
  options: vi.fn(),
}));
vi.mock('../client', () => ({ apiClient: mockApi }));

describe('my-hand API client', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('getMyHand calls GET /api/v1/users/me/hand', async () => {
    mockApi.get.mockResolvedValue([
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
    expect(mockApi.get).toHaveBeenCalledWith('/api/v1/users/me/hand');
    expect(result).toHaveLength(1);
  });

  it('updateHandSlot calls PUT /api/v1/users/me/hand/{slotType}', async () => {
    const dto = {
      slotType: 'game',
      entityId: 'g-1',
      entityType: 'game',
      entityLabel: 'Catan',
      entityImageUrl: null,
      pinnedAt: '2026-04-09T00:00:00Z',
    };
    mockApi.put.mockResolvedValue(dto);

    const result = await updateHandSlot('game', {
      entityId: 'g-1',
      entityType: 'game',
      entityLabel: 'Catan',
      entityImageUrl: null,
    });
    expect(mockApi.put).toHaveBeenCalledWith('/api/v1/users/me/hand/game', {
      entityId: 'g-1',
      entityType: 'game',
      entityLabel: 'Catan',
      entityImageUrl: null,
    });
    expect(result.entityId).toBe('g-1');
  });

  it('clearHandSlot calls DELETE /api/v1/users/me/hand/{slotType}', async () => {
    mockApi.delete.mockResolvedValue(undefined);

    await clearHandSlot('toolkit');
    expect(mockApi.delete).toHaveBeenCalledWith('/api/v1/users/me/hand/toolkit');
  });

  it('getMyHand returns empty array on null response', async () => {
    mockApi.get.mockResolvedValue(null);
    const result = await getMyHand();
    expect(result).toEqual([]);
  });
});
