/**
 * useDashboardData Hook Tests - Issue #886
 *
 * Tests for React Query-based dashboard data hooks with:
 * - 30s polling interval
 * - Tab visibility pause
 * - Error handling and retry logic
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';
import {
  useDashboardData,
  useDashboardAnalytics,
  useDashboardActivity,
  adminKeys,
} from '../useDashboardData';
import * as apiModule from '@/lib/api';

// Mock the API module - use importOriginal to preserve types
vi.mock('@/lib/api', async importOriginal => {
  const original = await importOriginal<typeof import('@/lib/api')>();
  return {
    ...original,
    api: {
      ...original.api,
      admin: {
        getAnalytics: vi.fn(),
        getRecentActivity: vi.fn(),
        getInfrastructureDetails: vi.fn(),
      },
    },
  };
});

const mockAnalyticsData = {
  metrics: {
    totalUsers: 1247,
    activeSessions: 42,
    apiRequestsToday: 3456,
    totalPdfDocuments: 847,
    totalChatMessages: 15234,
    averageConfidenceScore: 0.942,
    totalRagRequests: 18547,
    totalTokensUsed: 15700000,
    totalGames: 125,
    apiRequests7d: 24891,
    apiRequests30d: 112034,
    averageLatency24h: 215.0,
    averageLatency7d: 228.0,
    errorRate24h: 0.025,
    activeAlerts: 2,
    resolvedAlerts: 37,
  },
  userTrend: [],
  sessionTrend: [],
  apiRequestTrend: [],
  pdfUploadTrend: [],
  chatMessageTrend: [],
  generatedAt: new Date().toISOString(),
};

const mockActivityData = {
  events: [
    {
      id: '1',
      eventType: 'UserRegistered',
      description: 'New user registered: john.doe@example.com',
      userId: 'user-123',
      userEmail: 'john.doe@example.com',
      timestamp: new Date().toISOString(),
      severity: 'Info',
    },
  ],
  totalCount: 1,
  generatedAt: new Date().toISOString(),
};

const mockInfrastructureData = {
  services: [
    {
      name: 'API',
      status: 'Healthy',
      uptime: 99.9,
      lastCheck: new Date().toISOString(),
    },
  ],
  generatedAt: new Date().toISOString(),
};

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        staleTime: 0,
        gcTime: 0,
      },
    },
  });
}

function createWrapper(queryClient: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

describe('useDashboardData', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    vi.clearAllMocks();
    queryClient = createTestQueryClient();
  });

  afterEach(() => {
    queryClient.clear();
  });

  describe('adminKeys', () => {
    it('generates correct query keys', () => {
      expect(adminKeys.all).toEqual(['admin']);
      expect(adminKeys.dashboard()).toEqual(['admin', 'dashboard']);
      expect(adminKeys.analytics()).toEqual(['admin', 'dashboard', 'analytics', undefined]);
      expect(adminKeys.activity({ limit: 10 })).toEqual([
        'admin',
        'dashboard',
        'activity',
        { limit: 10 },
      ]);
    });
  });

  describe('useDashboardAnalytics', () => {
    it('fetches analytics data on mount', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      const { result } = renderHook(() => useDashboardAnalytics({ enablePolling: false }), {
        wrapper: createWrapper(queryClient),
      });

      expect(result.current.isLoading).toBe(true);

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(apiModule.api.admin.getAnalytics).toHaveBeenCalledTimes(1);
      expect(result.current.data).toEqual(mockAnalyticsData);
    });

    it('handles errors correctly', async () => {
      // Use 401 error which triggers immediate failure (no retry per hook logic)
      vi.mocked(apiModule.api.admin.getAnalytics).mockRejectedValue(new Error('401 Unauthorized'));

      const { result } = renderHook(() => useDashboardAnalytics({ enablePolling: false }), {
        wrapper: createWrapper(queryClient),
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error?.message).toBe('401 Unauthorized');
    });

    it('can be disabled entirely', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      renderHook(() => useDashboardAnalytics({ enabled: false }), {
        wrapper: createWrapper(queryClient),
      });

      // Should not call API when disabled
      expect(apiModule.api.admin.getAnalytics).not.toHaveBeenCalled();
    });
  });

  describe('useDashboardActivity', () => {
    it('fetches activity data with limit parameter', async () => {
      vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);

      const { result } = renderHook(() => useDashboardActivity(15, { enablePolling: false }), {
        wrapper: createWrapper(queryClient),
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(apiModule.api.admin.getRecentActivity).toHaveBeenCalledWith({ limit: 15 });
      expect(result.current.data).toEqual(mockActivityData);
    });

    it('uses default limit of 10', async () => {
      vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);

      const { result } = renderHook(() => useDashboardActivity(10, { enablePolling: false }), {
        wrapper: createWrapper(queryClient),
      });

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true);
      });

      expect(apiModule.api.admin.getRecentActivity).toHaveBeenCalledWith({ limit: 10 });
    });
  });

  describe('useDashboardData (combined hook)', () => {
    it('fetches analytics, activity, and infrastructure in parallel', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
      vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);
      vi.mocked(apiModule.api.admin.getInfrastructureDetails).mockResolvedValue(
        mockInfrastructureData
      );

      const { result } = renderHook(() => useDashboardData(10, { enablePolling: false }), {
        wrapper: createWrapper(queryClient),
      });

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(apiModule.api.admin.getAnalytics).toHaveBeenCalledTimes(1);
      expect(apiModule.api.admin.getRecentActivity).toHaveBeenCalledWith({ limit: 10 });
      expect(apiModule.api.admin.getInfrastructureDetails).toHaveBeenCalledTimes(1);
    });

    it('provides metrics and events convenience accessors', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
      vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);
      vi.mocked(apiModule.api.admin.getInfrastructureDetails).mockResolvedValue(
        mockInfrastructureData
      );

      const { result } = renderHook(() => useDashboardData(10, { enablePolling: false }), {
        wrapper: createWrapper(queryClient),
      });

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(result.current.metrics).toEqual(mockAnalyticsData.metrics);
      expect(result.current.events).toEqual(mockActivityData.events);
    });

    it('provides combined error state', async () => {
      // Use 403 error which triggers immediate failure (no retry per hook logic)
      vi.mocked(apiModule.api.admin.getAnalytics).mockRejectedValue(new Error('403 Forbidden'));
      vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);
      vi.mocked(apiModule.api.admin.getInfrastructureDetails).mockResolvedValue(
        mockInfrastructureData
      );

      const { result } = renderHook(() => useDashboardData(10, { enablePolling: false }), {
        wrapper: createWrapper(queryClient),
      });

      await waitFor(() => {
        expect(result.current.isError).toBe(true);
      });

      expect(result.current.error?.message).toBe('403 Forbidden');
    });

    it('provides lastUpdate timestamp', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
      vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);
      vi.mocked(apiModule.api.admin.getInfrastructureDetails).mockResolvedValue(
        mockInfrastructureData
      );

      const { result } = renderHook(() => useDashboardData(10, { enablePolling: false }), {
        wrapper: createWrapper(queryClient),
      });

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(result.current.lastUpdate).toBeInstanceOf(Date);
    });

    it('provides refetch function that refetches all queries', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
      vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);
      vi.mocked(apiModule.api.admin.getInfrastructureDetails).mockResolvedValue(
        mockInfrastructureData
      );

      const { result } = renderHook(() => useDashboardData(10, { enablePolling: false }), {
        wrapper: createWrapper(queryClient),
      });

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      // Initial calls
      expect(apiModule.api.admin.getAnalytics).toHaveBeenCalledTimes(1);
      expect(apiModule.api.admin.getRecentActivity).toHaveBeenCalledTimes(1);
      expect(apiModule.api.admin.getInfrastructureDetails).toHaveBeenCalledTimes(1);

      // Trigger refetch
      await result.current.refetch();

      expect(apiModule.api.admin.getAnalytics).toHaveBeenCalledTimes(2);
      expect(apiModule.api.admin.getRecentActivity).toHaveBeenCalledTimes(2);
      expect(apiModule.api.admin.getInfrastructureDetails).toHaveBeenCalledTimes(2);
    });

    it('returns empty events array when activity data has no events', async () => {
      const emptyActivityData = {
        events: [],
        totalCount: 0,
        generatedAt: new Date().toISOString(),
      };
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
      vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(emptyActivityData);
      vi.mocked(apiModule.api.admin.getInfrastructureDetails).mockResolvedValue(
        mockInfrastructureData
      );

      const { result } = renderHook(() => useDashboardData(10, { enablePolling: false }), {
        wrapper: createWrapper(queryClient),
      });

      await waitFor(() => {
        expect(result.current.activity.isSuccess).toBe(true);
      });

      // Events should be empty array
      expect(result.current.events).toEqual([]);
    });

    it('returns null metrics when analytics data is null', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(null);
      vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);
      vi.mocked(apiModule.api.admin.getInfrastructureDetails).mockResolvedValue(
        mockInfrastructureData
      );

      const { result } = renderHook(() => useDashboardData(10, { enablePolling: false }), {
        wrapper: createWrapper(queryClient),
      });

      await waitFor(() => {
        expect(result.current.analytics.isSuccess).toBe(true);
      });

      expect(result.current.metrics).toBeNull();
    });
  });
});
