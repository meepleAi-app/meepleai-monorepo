/**
 * MSW handlers for admin endpoints (Issue #2914)
 *
 * Covers: /api/v1/admin/* dashboard routes
 * - Dashboard stats and analytics
 * - Recent activity feed
 * - Infrastructure monitoring
 */

import { http, HttpResponse } from 'msw';

import {
  createMockDashboardStats,
  createMockRecentActivity,
  createMockInfrastructureDetails,
  createMockAdminStats,
  createMockErrorResponse,
  createNetworkDelay,
} from '../../fixtures/mockAdminData';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Stateful in-memory store for admin data
let dashboardStats = createMockDashboardStats();
let recentActivity = createMockRecentActivity();
let infrastructureDetails = createMockInfrastructureDetails();
let adminStats = createMockAdminStats();

// Simulate network failures for testing
let shouldFailStats = false;
let shouldFailAnalytics = false;
let shouldFailActivity = false;
let shouldFailInfrastructure = false;
let networkLatency = 0;

export const adminHandlers = [
  // GET /api/v1/admin/stats - Legacy stats endpoint
  http.get(`${API_BASE}/api/v1/admin/stats`, async () => {
    if (networkLatency > 0) {
      await createNetworkDelay(networkLatency);
    }

    if (shouldFailStats) {
      return HttpResponse.json(createMockErrorResponse(500, 'Failed to fetch admin stats'), {
        status: 500,
      });
    }

    return HttpResponse.json(adminStats, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // GET /api/v1/admin/analytics - Dashboard stats with trends
  http.get(`${API_BASE}/api/v1/admin/analytics`, async () => {
    if (networkLatency > 0) {
      await createNetworkDelay(networkLatency);
    }

    if (shouldFailAnalytics) {
      return HttpResponse.json(createMockErrorResponse(500, 'Analytics service unavailable'), {
        status: 500,
      });
    }

    return HttpResponse.json(dashboardStats, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // GET /api/v1/admin/activity - Recent activity feed
  http.get(`${API_BASE}/api/v1/admin/activity`, async ({ request }) => {
    if (networkLatency > 0) {
      await createNetworkDelay(networkLatency);
    }

    if (shouldFailActivity) {
      return HttpResponse.json(
        createMockErrorResponse(503, 'Activity service temporarily unavailable'),
        {
          status: 503,
        }
      );
    }

    // Parse query params for filtering
    const url = new URL(request.url);
    const limit = url.searchParams.get('limit');

    // Apply limit if provided
    const filteredActivity = limit
      ? {
          ...recentActivity,
          events: recentActivity.events.slice(0, parseInt(limit, 10)),
        }
      : recentActivity;

    return HttpResponse.json(filteredActivity, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // GET /api/v1/admin/infrastructure/details - Infrastructure monitoring
  http.get(`${API_BASE}/api/v1/admin/infrastructure/details`, async () => {
    if (networkLatency > 0) {
      await createNetworkDelay(networkLatency);
    }

    if (shouldFailInfrastructure) {
      return HttpResponse.json(
        createMockErrorResponse(500, 'Infrastructure monitoring unavailable'),
        {
          status: 500,
        }
      );
    }

    return HttpResponse.json(infrastructureDetails, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),
];

/**
 * Helper to simulate API failures for testing error states
 */
export const setAdminApiFailures = (failures: {
  stats?: boolean;
  analytics?: boolean;
  activity?: boolean;
  infrastructure?: boolean;
}) => {
  shouldFailStats = failures.stats ?? false;
  shouldFailAnalytics = failures.analytics ?? false;
  shouldFailActivity = failures.activity ?? false;
  shouldFailInfrastructure = failures.infrastructure ?? false;
};

/**
 * Helper to simulate network latency for testing loading states
 */
export const setAdminNetworkLatency = (ms: number) => {
  networkLatency = ms;
};

/**
 * Helper to update dashboard stats for testing dynamic data
 */
export const updateDashboardStats = (updates: Parameters<typeof createMockDashboardStats>[0]) => {
  dashboardStats = createMockDashboardStats(updates);
};

/**
 * Helper to update recent activity for testing dynamic feeds
 */
export const updateRecentActivity = (
  updates: Parameters<typeof createMockRecentActivity>[0]
) => {
  recentActivity = createMockRecentActivity(updates);
};

/**
 * Helper to update infrastructure details for testing health states
 */
export const updateInfrastructureDetails = (
  updates: Parameters<typeof createMockInfrastructureDetails>[0]
) => {
  infrastructureDetails = createMockInfrastructureDetails(updates);
};

/**
 * Helper to reset all admin state between tests
 */
export const resetAdminState = () => {
  dashboardStats = createMockDashboardStats();
  recentActivity = createMockRecentActivity();
  infrastructureDetails = createMockInfrastructureDetails();
  adminStats = createMockAdminStats();
  shouldFailStats = false;
  shouldFailAnalytics = false;
  shouldFailActivity = false;
  shouldFailInfrastructure = false;
  networkLatency = 0;
};
