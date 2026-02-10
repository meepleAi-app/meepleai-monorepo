/**
 * Dashboard API Integration Tests (Issue #2914)
 *
 * Integration tests for Admin Dashboard data fetching with MSW mocks.
 *
 * Test Coverage:
 * - Dashboard data fetching with TanStack Query
 * - Loading states
 * - Error states (API failure, network error)
 * - Real-time polling (30s interval simulation)
 * - Cache behavior (stale-while-revalidate)
 *
 * Part of Epic #2783 - Admin Dashboard Testing
 */

import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { QueryClient } from '@tanstack/react-query';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

import { createTestQueryClient, renderWithQuery } from '@/__tests__/utils/query-test-utils';
import {
  setAdminApiFailures,
  setAdminNetworkLatency,
  updateDashboardStats,
  resetAdminState,
} from '@/__tests__/mocks/handlers';
import { useAdminDashboard } from '@/lib/hooks/useAdminDashboard';
import { createMockDashboardMetrics } from '@/__tests__/fixtures/mockAdminData';
import { resetAllCircuits } from '@/lib/api/core/circuitBreaker';
import { globalRequestCache } from '@/lib/api/core/requestCache';

/**
 * Test component that uses admin dashboard hooks
 */
function DashboardTestComponent() {
  const { data: analytics, isLoading, isError, error, refetch } = useAdminDashboard();

  if (isLoading) {
    return <div data-testid="dashboard-loading">Loading dashboard data...</div>;
  }

  if (isError) {
    return (
      <div data-testid="dashboard-error">
        Error: {error instanceof Error ? error.message : 'Unknown error'}
        <button onClick={() => refetch()} data-testid="refetch-button">
          Retry
        </button>
      </div>
    );
  }

  if (!analytics) {
    return <div data-testid="dashboard-no-data">No data available</div>;
  }

  return (
    <div data-testid="dashboard-data">
      <div data-testid="total-users">{analytics.metrics.totalUsers}</div>
      <div data-testid="active-sessions">{analytics.metrics.activeSessions}</div>
      <div data-testid="api-requests-today">{analytics.metrics.apiRequestsToday}</div>
      <div data-testid="total-games">{analytics.metrics.totalGames}</div>
      <div data-testid="generated-at">{analytics.generatedAt}</div>
      <button onClick={() => refetch()} data-testid="refetch-button">
        Refresh
      </button>
    </div>
  );
}

// Issue #3981: Re-enabled with real timers for data fetching tests.
// Polling/timing tests use fake timers only where needed with runOnlyPendingTimersAsync.
describe('Dashboard API Integration Tests', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = createTestQueryClient();
    resetAdminState();
    resetAllCircuits();
    globalRequestCache.clear();
  });

  afterEach(() => {
    queryClient.clear();
    vi.useRealTimers();
  });

  describe('Data Fetching with TanStack Query', () => {
    it('should fetch and display dashboard data successfully', async () => {
      renderWithQuery(<DashboardTestComponent />, { queryClient });

      // Initially should show loading
      expect(screen.getByTestId('dashboard-loading')).toBeInTheDocument();

      // Wait for data to load
      await waitFor(() => {
        expect(screen.getByTestId('dashboard-data')).toBeInTheDocument();
      });

      // Verify data is displayed correctly
      expect(screen.getByTestId('total-users')).toHaveTextContent('2847');
      expect(screen.getByTestId('active-sessions')).toHaveTextContent('156');
      expect(screen.getByTestId('api-requests-today')).toHaveTextContent('8492');
      expect(screen.getByTestId('total-games')).toHaveTextContent('1234');
    });

    it('should refetch data when refetch button is clicked', async () => {
      renderWithQuery(<DashboardTestComponent />, { queryClient });

      // Wait for initial load
      await waitFor(() => {
        expect(screen.getByTestId('dashboard-data')).toBeInTheDocument();
      });

      // Update mock data and clear request dedup cache to ensure fresh request
      updateDashboardStats({
        metrics: createMockDashboardMetrics({ totalUsers: 3000 }),
      });
      globalRequestCache.clear();

      // Click refetch button
      fireEvent.click(screen.getByTestId('refetch-button'));

      // Wait for refetch to complete
      await waitFor(
        () => {
          expect(screen.getByTestId('total-users')).toHaveTextContent('3000');
        },
        { timeout: 3000 }
      );
    });
  });

  describe('Loading States', () => {
    it('should show loading state during initial fetch', async () => {
      setAdminNetworkLatency(500);

      renderWithQuery(<DashboardTestComponent />, { queryClient });

      // Should show loading immediately
      expect(screen.getByTestId('dashboard-loading')).toBeInTheDocument();
      expect(screen.queryByTestId('dashboard-data')).not.toBeInTheDocument();

      // Wait for data to load
      await waitFor(
        () => {
          expect(screen.getByTestId('dashboard-data')).toBeInTheDocument();
        },
        { timeout: 2000 }
      );
    });

    it('should keep showing data during background refetch', async () => {
      renderWithQuery(<DashboardTestComponent />, { queryClient });

      // Wait for initial load
      await waitFor(() => {
        expect(screen.getByTestId('dashboard-data')).toBeInTheDocument();
      });

      // Add latency for refetch, update mock data, clear dedup cache
      setAdminNetworkLatency(300);
      updateDashboardStats({
        metrics: createMockDashboardMetrics({ totalUsers: 3100 }),
      });
      globalRequestCache.clear();

      // Click refetch - TanStack Query v5 keeps showing stale data during refetch
      fireEvent.click(screen.getByTestId('refetch-button'));

      // Should still show existing data (stale-while-revalidate behavior)
      expect(screen.getByTestId('dashboard-data')).toBeInTheDocument();
      expect(screen.getByTestId('total-users')).toHaveTextContent('2847');

      // Wait for refetch to complete with new data
      await waitFor(
        () => {
          expect(screen.getByTestId('total-users')).toHaveTextContent('3100');
        },
        { timeout: 3000 }
      );
    });
  });

  describe('Error States', () => {
    it('should handle API failure (500 error)', async () => {
      setAdminApiFailures({ analytics: true });

      renderWithQuery(<DashboardTestComponent />, { queryClient });

      // Wait for error to be displayed (httpClient has its own retry logic ~3s)
      await waitFor(
        () => {
          expect(screen.getByTestId('dashboard-error')).toBeInTheDocument();
        },
        { timeout: 10000 }
      );

      // Verify error message
      expect(screen.getByTestId('dashboard-error')).toHaveTextContent('Error:');
    });

    it('should handle network error', async () => {
      // Use API failure to simulate network error (more reliable than timeout)
      setAdminApiFailures({ analytics: true });

      const errorQueryClient = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
            staleTime: 0,
            refetchOnMount: false,
            refetchOnWindowFocus: false,
            refetchOnReconnect: false,
            networkMode: 'online',
          },
        },
      });

      renderWithQuery(<DashboardTestComponent />, {
        queryClient: errorQueryClient,
      });

      // Should show loading initially
      expect(screen.getByTestId('dashboard-loading')).toBeInTheDocument();

      // Wait for error state (httpClient has its own retry logic ~7s)
      await waitFor(
        () => {
          expect(screen.getByTestId('dashboard-error')).toBeInTheDocument();
        },
        { timeout: 15000 }
      );

      errorQueryClient.clear();
    });

    it('should recover from error on retry', async () => {
      // Set to fail BEFORE rendering
      setAdminApiFailures({ analytics: true });

      renderWithQuery(<DashboardTestComponent />, { queryClient });

      // Wait for error (httpClient has its own retry logic ~7s)
      await waitFor(
        () => {
          expect(screen.getByTestId('dashboard-error')).toBeInTheDocument();
        },
        { timeout: 15000 }
      );

      // Fix the API and reset circuit breaker + dedup cache
      setAdminApiFailures({ analytics: false });
      resetAllCircuits();
      globalRequestCache.clear();

      // Retry by clicking refetch
      fireEvent.click(screen.getByTestId('refetch-button'));

      // Should now succeed (may take a moment due to httpClient retry)
      await waitFor(
        () => {
          expect(screen.getByTestId('dashboard-data')).toBeInTheDocument();
        },
        { timeout: 15000 }
      );
    });
  });

  describe('Real-time Polling (30s interval)', () => {
    it('should refetch data every 30 seconds with polling enabled', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });

      const queryClientWithPolling = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
            staleTime: 25000, // 25s (stale before 30s refetch)
            refetchInterval: 30000, // 30s polling
            refetchOnMount: false,
            refetchOnWindowFocus: false,
            refetchOnReconnect: false,
          },
        },
      });

      renderWithQuery(<DashboardTestComponent />, { queryClient: queryClientWithPolling });

      // Wait for initial load
      await vi.advanceTimersByTimeAsync(1000);
      await waitFor(() => {
        expect(screen.getByTestId('dashboard-data')).toBeInTheDocument();
      });

      // Initial data
      expect(screen.getByTestId('total-users')).toHaveTextContent('2847');

      // Update mock data for next poll and clear dedup cache
      updateDashboardStats({
        metrics: createMockDashboardMetrics({ totalUsers: 3000 }),
      });
      globalRequestCache.clear();

      // Advance time by 30 seconds (trigger poll)
      await vi.advanceTimersByTimeAsync(30000);

      // Wait for polling refetch
      await waitFor(
        () => {
          expect(screen.getByTestId('total-users')).toHaveTextContent('3000');
        },
        { timeout: 5000 }
      );

      queryClientWithPolling.clear();
    });

    it('should continue polling after multiple intervals', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });

      const queryClientWithPolling = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
            staleTime: 25000,
            refetchInterval: 30000,
            refetchOnMount: false,
            refetchOnWindowFocus: false,
            refetchOnReconnect: false,
          },
        },
      });

      renderWithQuery(<DashboardTestComponent />, { queryClient: queryClientWithPolling });

      await vi.advanceTimersByTimeAsync(1000);
      await waitFor(() => {
        expect(screen.getByTestId('dashboard-data')).toBeInTheDocument();
      });

      // First poll (30s)
      updateDashboardStats({
        metrics: createMockDashboardMetrics({ totalUsers: 3000 }),
      });
      globalRequestCache.clear();
      await vi.advanceTimersByTimeAsync(30000);

      await waitFor(() => {
        expect(screen.getByTestId('total-users')).toHaveTextContent('3000');
      });

      // Second poll (60s total)
      updateDashboardStats({
        metrics: createMockDashboardMetrics({ totalUsers: 3500 }),
      });
      globalRequestCache.clear();
      await vi.advanceTimersByTimeAsync(30000);

      await waitFor(() => {
        expect(screen.getByTestId('total-users')).toHaveTextContent('3500');
      });

      queryClientWithPolling.clear();
    });
  });

  describe('Cache Behavior (stale-while-revalidate)', () => {
    it('should serve stale data while revalidating', async () => {
      const queryClientWithStaleWhileRevalidate = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
            staleTime: 0, // Data becomes stale immediately
            gcTime: 60000, // Keep in cache for 60s
            refetchOnMount: 'always',
            refetchOnWindowFocus: false,
            refetchOnReconnect: false,
          },
        },
      });

      // First render - fetch initial data
      const { unmount } = renderWithQuery(<DashboardTestComponent />, {
        queryClient: queryClientWithStaleWhileRevalidate,
      });

      await waitFor(() => {
        expect(screen.getByTestId('dashboard-data')).toBeInTheDocument();
      });

      expect(screen.getByTestId('total-users')).toHaveTextContent('2847');

      // Unmount component
      unmount();

      // Update mock data and clear dedup cache
      updateDashboardStats({
        metrics: createMockDashboardMetrics({ totalUsers: 3200 }),
      });
      globalRequestCache.clear();

      // Re-render (stale data should be shown immediately, then revalidated)
      renderWithQuery(<DashboardTestComponent />, {
        queryClient: queryClientWithStaleWhileRevalidate,
      });

      // Should show stale data immediately (no loading state)
      expect(screen.getByTestId('dashboard-data')).toBeInTheDocument();
      expect(screen.getByTestId('total-users')).toHaveTextContent('2847'); // Stale data

      // Wait for revalidation to complete
      await waitFor(() => {
        expect(screen.getByTestId('total-users')).toHaveTextContent('3200'); // Fresh data
      });

      queryClientWithStaleWhileRevalidate.clear();
    });

    it('should refetch when data becomes stale', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });

      const queryClientWithStaleTime = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
            staleTime: 5000, // 5s before stale
            gcTime: 60000,
            refetchOnMount: 'always',
            refetchOnWindowFocus: false,
            refetchOnReconnect: false,
          },
        },
      });

      const { unmount } = renderWithQuery(<DashboardTestComponent />, {
        queryClient: queryClientWithStaleTime,
      });

      await vi.advanceTimersByTimeAsync(1000);
      await waitFor(() => {
        expect(screen.getByTestId('dashboard-data')).toBeInTheDocument();
      });

      // Data is fresh for 5 seconds
      expect(screen.getByTestId('total-users')).toHaveTextContent('2847');

      // Unmount to allow remounting
      unmount();

      // Advance time by 6 seconds (data becomes stale)
      await vi.advanceTimersByTimeAsync(6000);

      // Update mock data and clear dedup cache
      updateDashboardStats({
        metrics: createMockDashboardMetrics({ totalUsers: 3100 }),
      });
      globalRequestCache.clear();

      // Re-render should trigger refetch because data is stale and refetchOnMount is 'always'
      renderWithQuery(<DashboardTestComponent />, { queryClient: queryClientWithStaleTime });

      await vi.advanceTimersByTimeAsync(2000);
      // Should show fresh data after revalidation
      await waitFor(() => {
        expect(screen.getByTestId('total-users')).toHaveTextContent('3100');
      });

      queryClientWithStaleTime.clear();
    });
  });
});
