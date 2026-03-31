import { renderHook, waitFor } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: {
    playRecords: { getHistory: vi.fn() },
    badges: { getMyBadges: vi.fn() },
  },
}));

import { api } from '@/lib/api';
import { useActivityFeed } from '../useActivityFeed';

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(api.playRecords.getHistory).mockResolvedValue({
    records: [],
    totalCount: 0,
    page: 1,
    pageSize: 10,
    totalPages: 0,
  });
  vi.mocked(api.badges.getMyBadges).mockResolvedValue([]);
});

describe('useActivityFeed', () => {
  it('starts with isLoading true', () => {
    const { result } = renderHook(() => useActivityFeed());
    expect(result.current.isLoading).toBe(true);
  });

  it('returns items sorted by timestamp desc after fetch', async () => {
    vi.mocked(api.playRecords.getHistory).mockResolvedValue({
      records: [
        {
          id: 's1',
          gameName: 'Wingspan',
          sessionDate: '2026-03-30T20:00:00Z',
          duration: null,
          status: 'Completed',
          playerCount: 3,
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 10,
      totalPages: 1,
    });
    vi.mocked(api.badges.getMyBadges).mockResolvedValue([
      {
        id: 'a1',
        name: 'Prime Ali',
        description: 'First badge',
        tier: 'Bronze',
        iconUrl: '/badges/prime-ali.png',
        earnedAt: '2026-03-29T15:00:00Z',
        isDisplayed: true,
      },
    ]);
    const { result } = renderHook(() => useActivityFeed());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.items.length).toBe(2);
    // most recent first
    expect(new Date(result.current.items[0].timestamp).getTime()).toBeGreaterThanOrEqual(
      new Date(result.current.items[1].timestamp).getTime()
    );
  });

  it('assigns correct type to items', async () => {
    vi.mocked(api.playRecords.getHistory).mockResolvedValue({
      records: [
        {
          id: 's1',
          gameName: 'Wingspan',
          sessionDate: '2026-03-30T20:00:00Z',
          duration: null,
          status: 'Completed',
          playerCount: 3,
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 10,
      totalPages: 1,
    });
    vi.mocked(api.badges.getMyBadges).mockResolvedValue([
      {
        id: 'a1',
        name: 'Prime Ali',
        description: 'First badge',
        tier: 'Bronze',
        iconUrl: '/badges/prime-ali.png',
        earnedAt: '2026-03-29T15:00:00Z',
        isDisplayed: true,
      },
    ]);
    const { result } = renderHook(() => useActivityFeed());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    const types = result.current.items.map(i => i.type);
    expect(types).toContain('session');
    expect(types).toContain('achievement');
  });

  it('exposes error if both fetches fail', async () => {
    vi.mocked(api.playRecords.getHistory).mockRejectedValue(new Error('fail'));
    vi.mocked(api.badges.getMyBadges).mockRejectedValue(new Error('fail'));
    const { result } = renderHook(() => useActivityFeed());
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.error).toBeTruthy();
  });
});
