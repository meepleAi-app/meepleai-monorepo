/**
 * Dashboard Store Tests - Issue #4586
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useDashboardStore } from '../dashboard-store';
import * as dashboardClient from '@/lib/api/dashboard-client';

vi.mock('@/lib/api/dashboard-client');

describe('useDashboardStore', () => {
  beforeEach(() => {
    const { result } = renderHook(() => useDashboardStore());
    act(() => {
      result.current.reset();
    });
    vi.clearAllMocks();
  });

  it('initializes with empty state', () => {
    const { result } = renderHook(() => useDashboardStore());

    expect(result.current.stats).toBeNull();
    expect(result.current.recentSessions).toEqual([]);
    expect(result.current.games).toEqual([]);
    expect(result.current.totalGamesCount).toBe(0);
  });

  it('fetches stats successfully', async () => {
    const mockStats = {
      totalGames: 47,
      monthlyPlays: 12,
      monthlyPlaysChange: 15,
      weeklyPlayTime: '08:30:00',
      monthlyFavorites: 3,
    };

    vi.spyOn(dashboardClient.dashboardClient, 'getUserStats').mockResolvedValue(
      mockStats
    );

    const { result } = renderHook(() => useDashboardStore());

    await act(async () => {
      await result.current.fetchStats();
    });

    expect(result.current.stats).toEqual(mockStats);
    expect(result.current.isLoadingStats).toBe(false);
  });

  it('handles fetch errors', async () => {
    vi.spyOn(dashboardClient.dashboardClient, 'getUserStats').mockRejectedValue(
      new Error('Network error')
    );

    const { result } = renderHook(() => useDashboardStore());

    await act(async () => {
      await result.current.fetchStats();
    });

    expect(result.current.statsError).toBe('Network error');
    expect(result.current.isLoadingStats).toBe(false);
  });

  it('updates filters and refetches games', async () => {
    const mockGames = {
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
      totalPages: 0,
    };

    vi.spyOn(dashboardClient.dashboardClient, 'getUserGames').mockResolvedValue(
      mockGames
    );

    const { result } = renderHook(() => useDashboardStore());

    act(() => {
      result.current.updateFilters({ category: 'strategy' });
    });

    expect(result.current.filters.category).toBe('strategy');
    expect(result.current.filters.page).toBe(1); // Reset to page 1
  });
});
