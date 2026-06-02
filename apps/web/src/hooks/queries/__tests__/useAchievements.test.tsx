/**
 * useAchievements hook unit tests (#1542).
 *
 * Mocks apiClient.get to verify:
 *  - Hook calls the documented endpoint
 *  - Returns parsed data on success
 *  - Returns [] when the API returns null/undefined (defensive default)
 *  - Surfaces errors via React Query's `error` state
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { apiClient } from '@/lib/api/client';

import { useAchievements, type AchievementDto } from '../useAchievements';

vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: vi.fn(),
  },
}));

function createWrapper() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

const sampleAchievement: AchievementDto = {
  id: '00000000-0000-0000-0000-000000000001',
  code: 'first_win',
  name: 'First Win',
  description: 'Won your first game',
  iconUrl: '🏆',
  points: 10,
  rarity: 'Common',
  category: 'Gameplay',
  threshold: 1,
  progress: 100,
  isUnlocked: true,
  unlockedAt: '2026-06-01T12:00:00Z',
};

describe('useAchievements', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls GET /api/v1/achievements and returns the parsed list', async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce([sampleAchievement]);

    const { result } = renderHook(() => useAchievements(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(apiClient.get).toHaveBeenCalledWith('/api/v1/achievements');
    expect(result.current.data).toEqual([sampleAchievement]);
  });

  it('returns [] when the API returns null (defensive fallback)', async () => {
    vi.mocked(apiClient.get).mockResolvedValueOnce(null as unknown as AchievementDto[]);

    const { result } = renderHook(() => useAchievements(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(result.current.data).toEqual([]);
  });

  it('surfaces the error when the API request fails', async () => {
    vi.mocked(apiClient.get).mockRejectedValueOnce(new Error('network'));

    const { result } = renderHook(() => useAchievements(), { wrapper: createWrapper() });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(result.current.error).toBeInstanceOf(Error);
    expect((result.current.error as Error).message).toBe('network');
  });
});
