/**
 * useAdminDashboard Hook Tests - Issue #3026
 * Target: >90% coverage
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { createElement, type ReactNode } from 'react';

import { useAdminDashboard, ADMIN_DASHBOARD_QUERY_KEY } from '../useAdminDashboard';
import { api } from '@/lib/api';
import type { DashboardStats } from '@/lib/api';
import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAnalytics: vi.fn(),
    },
  },
}));

const mockStats: DashboardStats = {
  totalUsers: 150,
  activeUsers: 87,
  totalGames: 245,
  totalSessions: 1234,
  totalDocuments: 456,
  averageSessionDuration: 45.5,
  mostPlayedGames: [
    { gameId: 'game-1', title: 'Catan', playCount: 89 },
  ],
  recentActivity: [
    {
      id: 'activity-1',
      userId: 'user-123',
      userName: 'Alice',
      activityType: 'GameAdded',
      timestamp: '2024-01-20T10:30:00Z',
      details: 'Added Catan',
    },
  ],
  userGrowth: {
    daily: [{ date: '2024-01-15', count: 5 }],
    weekly: [{ week: '2024-W03', count: 45 }],
    monthly: [{ month: '2024-01', count: 120 }],
  },
  sessionStatistics: {
    totalSessions: 1234,
    averageDuration: 45.5,
    byGameType: [{ gameType: 'Strategy', sessionCount: 567 }],
  },
};

describe('useAdminDashboard', () => {
  let queryClient: QueryClient;

  const createWrapper = () => {
    return ({ children }: { children: ReactNode }) =>
      createElement(QueryClientProvider, { client: queryClient }, children);
  };

  beforeEach(() => {
    queryClient = createTestQueryClient();
    vi.clearAllMocks();
  });

  describe('Query Key', () => {
    it('generates correct query key without params', () => {
      expect(ADMIN_DASHBOARD_QUERY_KEY).toEqual(['admin', 'analytics']);
    });
  });

  describe('Data Fetching', () => {
    it('fetches data successfully', async () => {
      vi.mocked(api.admin.getAnalytics).mockResolvedValueOnce(mockStats);

      const { result } = renderHook(() => useAdminDashboard(), {
        wrapper: createWrapper(),
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(result.current.data).toEqual(mockStats);
    });

    it('passes params to API', async () => {
      vi.mocked(api.admin.getAnalytics).mockResolvedValueOnce(mockStats);

      const params = { roleFilter: 'admin' };

      const { result } = renderHook(() => useAdminDashboard({ params }), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(api.admin.getAnalytics).toHaveBeenCalledWith(params);
    });

    it('handles null response', async () => {
      vi.mocked(api.admin.getAnalytics).mockResolvedValueOnce(null);

      const { result } = renderHook(() => useAdminDashboard(), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isSuccess).toBe(true));

      expect(result.current.data).toBeNull();
    });
  });

  describe('Error Handling', () => {
    it('handles API error', async () => {
      vi.mocked(api.admin.getAnalytics).mockRejectedValueOnce(new Error('API error'));

      const { result } = renderHook(() => useAdminDashboard(), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error).toEqual(new Error('API error'));
    });

    it('handles 403 Forbidden', async () => {
      vi.mocked(api.admin.getAnalytics).mockRejectedValueOnce(new Error('Forbidden'));

      const { result } = renderHook(() => useAdminDashboard(), {
        wrapper: createWrapper(),
      });

      await waitFor(() => expect(result.current.isError).toBe(true));

      expect(result.current.error?.message).toBe('Forbidden');
    });
  });

  describe('Options', () => {
    it('respects enabled option', () => {
      const { result } = renderHook(() => useAdminDashboard({ enabled: false }), {
        wrapper: createWrapper(),
      });

      expect(result.current.isLoading).toBe(false);
      expect(api.admin.getAnalytics).not.toHaveBeenCalled();
    });
  });
});
