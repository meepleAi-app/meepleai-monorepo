/**
 * useRecentSessions Hook Unit Tests
 *
 * Coverage areas:
 * - Loading state on mount
 * - Returns sessions after successful fetch
 * - Exposes error when fetch fails
 */

import { renderHook, waitFor } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: {
    playRecords: {
      getHistory: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';
import { useRecentSessions } from '../useRecentSessions';

const mockRecords = [
  {
    id: 's1',
    gameName: 'Wingspan',
    sessionDate: '2026-03-30T20:00:00Z',
    duration: null,
    status: 'Completed',
    playerCount: 3,
  },
  {
    id: 's2',
    gameName: 'Catan',
    sessionDate: '2026-03-28T19:00:00Z',
    duration: null,
    status: 'Completed',
    playerCount: 4,
  },
];

beforeEach(() => vi.clearAllMocks());

describe('useRecentSessions', () => {
  it('starts with isLoading true', () => {
    vi.mocked(api.playRecords.getHistory).mockResolvedValue({
      records: [],
      totalCount: 0,
      page: 1,
      pageSize: 3,
      totalPages: 0,
    });
    const { result } = renderHook(() => useRecentSessions(3));
    expect(result.current.isLoading).toBe(true);
  });

  it('returns sessions after fetch', async () => {
    vi.mocked(api.playRecords.getHistory).mockResolvedValue({
      records: mockRecords,
      totalCount: 2,
      page: 1,
      pageSize: 3,
      totalPages: 1,
    });
    const { result } = renderHook(() => useRecentSessions(3));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.sessions).toHaveLength(2);
    expect(result.current.sessions[0].gameName).toBe('Wingspan');
  });

  it('exposes error if fetch fails', async () => {
    vi.mocked(api.playRecords.getHistory).mockRejectedValue(new Error('Network error'));
    const { result } = renderHook(() => useRecentSessions(3));
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.error).toBeTruthy();
    expect(result.current.error).toBe('Network error');
  });
});
