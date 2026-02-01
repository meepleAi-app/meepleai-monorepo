/**
 * ISSUE-2928: User Dashboard Load Testing - 30s Polling Interval
 *
 * Scenario 1: Realistic user dashboard usage with periodic polling.
 * Simulates authenticated users viewing their dashboard with auto-refresh.
 *
 * Endpoints tested:
 * - GET /api/v1/users/profile
 * - GET /api/v1/users/me/activity
 * - GET /api/v1/notifications
 * - GET /api/v1/notifications/unread-count
 * - GET /api/v1/users/me/upload-quota
 * - GET /api/v1/users/me/ai-usage
 *
 * Performance Targets:
 * - Response time p95 < 500ms
 * - Response time p99 < 1s
 * - Error rate < 1%
 */

import { sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';
import {
  config,
  authenticateUser,
  authGet,
  validateResponse,
  standardThresholds,
  stressThresholds,
  scenarioConfigs,
  standardSetup,
  standardTeardown,
  weightedRandom,
} from '../utils/shared-config.js';

// ============================================
// CUSTOM METRICS
// ============================================

const dashboardLatency = new Trend('dashboard_polling_latency');
const profileErrors = new Counter('profile_errors');
const notificationErrors = new Counter('notification_errors');
const activityErrors = new Counter('activity_errors');

// ============================================
// TEST CONFIGURATION
// ============================================

// Determine test type from environment
const TEST_TYPE = __ENV.TEST_TYPE || 'smoke';

// Define all scenarios
const allScenarios = {
  // Smoke test: Quick validation (5 VUs, 1 minute)
  smoke: {
    ...scenarioConfigs.smoke,
    tags: { test_type: 'smoke', scenario: 'user-dashboard' },
    exec: 'userDashboardPolling',
  },

  // Load test: Realistic user dashboard polling (50 VUs, 5 minutes)
  load: {
    ...scenarioConfigs.load,
    tags: { test_type: 'load', scenario: 'user-dashboard' },
    exec: 'userDashboardPolling',
  },

  // Stress test: Peak load (200 VUs, 10 minutes)
  stress: {
    ...scenarioConfigs.stress,
    tags: { test_type: 'stress', scenario: 'user-dashboard' },
    exec: 'userDashboardPolling',
  },
};

// Select scenario(s) based on TEST_TYPE
const selectedScenarios =
  TEST_TYPE === 'all'
    ? allScenarios
    : { [TEST_TYPE]: allScenarios[TEST_TYPE] || allScenarios.smoke };

export const options = {
  scenarios: selectedScenarios,

  // Performance thresholds
  thresholds: {
    ...standardThresholds,
    'dashboard_polling_latency': ['p(95)<500', 'p(99)<1000'],
    'profile_errors': ['count<5'],
    'notification_errors': ['count<10'],
    'activity_errors': ['count<10'],
  },

  // HTTP configuration
  httpDebug: __ENV.HTTP_DEBUG === 'true' ? 'full' : '',
  insecureSkipTLSVerify: true,
  noConnectionReuse: false,

  // Output
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

// ============================================
// DASHBOARD ENDPOINTS
// ============================================

const dashboardEndpoints = [
  // Profile - Core user info (30% of requests)
  {
    path: '/api/v1/users/profile',
    weight: 0.30,
    errorMetric: profileErrors,
    description: 'user-profile',
  },

  // Notifications - High frequency polling (25% of requests)
  {
    path: '/api/v1/notifications/unread-count',
    weight: 0.15,
    errorMetric: notificationErrors,
    description: 'notification-count',
  },
  {
    path: '/api/v1/notifications',
    weight: 0.10,
    params: { limit: 10 },
    errorMetric: notificationErrors,
    description: 'notifications-list',
  },

  // Activity timeline (20% of requests)
  {
    path: '/api/v1/users/me/activity',
    weight: 0.20,
    params: { limit: 50 },
    errorMetric: activityErrors,
    description: 'activity-timeline',
  },

  // Quotas and usage (15% of requests)
  {
    path: '/api/v1/users/me/upload-quota',
    weight: 0.10,
    errorMetric: profileErrors,
    description: 'upload-quota',
  },

  // AI usage - less frequent (10% of requests)
  {
    path: '/api/v1/users/me/ai-usage',
    weight: 0.10,
    params: { days: 30 },
    errorMetric: profileErrors,
    description: 'ai-usage',
  },

  // Preferences - occasional check (5% of requests)
  {
    path: '/api/v1/users/preferences',
    weight: 0.05,
    errorMetric: profileErrors,
    description: 'user-preferences',
  },
];

// ============================================
// MAIN SCENARIO
// ============================================

/**
 * User dashboard polling scenario.
 * Simulates realistic user behavior with dashboard auto-refresh.
 */
export function userDashboardPolling() {
  // Authenticate once per VU
  const token = authenticateUser();
  if (!token) {
    console.error('Failed to authenticate user, skipping scenario');
    return;
  }

  // Polling configuration
  const pollingInterval = 30; // seconds
  const iterations = 10;       // Total polling iterations per VU

  for (let i = 0; i < iterations; i++) {
    // Initial dashboard load - fetch multiple endpoints
    const initialLoadStart = Date.now();
    initialDashboardLoad(token);
    const initialLoadTime = Date.now() - initialLoadStart;

    // Simulate user interaction - random endpoint polling
    const quickPollCount = Math.floor(Math.random() * 3) + 1;
    for (let j = 0; j < quickPollCount; j++) {
      const endpoint = weightedRandom(dashboardEndpoints);
      pollEndpoint(token, endpoint);
      sleep(1); // Brief pause between quick polls
    }

    // Calculate remaining sleep time for polling interval
    const elapsedTime = (Date.now() - initialLoadStart) / 1000;
    const remainingSleep = Math.max(0, pollingInterval - elapsedTime);

    // Wait for next polling interval (except last iteration)
    if (i < iterations - 1) {
      sleep(remainingSleep);
    }
  }
}

/**
 * Initial dashboard load - fetches core dashboard data.
 * @param {string} token - JWT token
 */
function initialDashboardLoad(token) {
  // Profile is always fetched first
  pollEndpoint(token, dashboardEndpoints[0]);

  // Then notification count (badge)
  pollEndpoint(token, dashboardEndpoints[1]);

  // Activity timeline
  pollEndpoint(token, dashboardEndpoints[3]);
}

/**
 * Poll a specific endpoint with validation and metrics.
 * @param {string} token - JWT token
 * @param {object} endpoint - Endpoint configuration
 */
function pollEndpoint(token, endpoint) {
  const startTime = Date.now();

  const response = authGet(
    token,
    endpoint.path,
    endpoint.params || {},
    { endpoint_type: endpoint.description }
  );

  const latency = Date.now() - startTime;
  dashboardLatency.add(latency);

  const success = validateResponse(response, endpoint.description);
  if (!success && endpoint.errorMetric) {
    endpoint.errorMetric.add(1);
  }
}

// ============================================
// LIFECYCLE HOOKS
// ============================================

/**
 * Setup function (runs once before test)
 */
export function setup() {
  return standardSetup('User Dashboard Polling', options);
}

/**
 * Teardown function (runs once after test)
 */
export function teardown(data) {
  standardTeardown();
}

/**
 * Handle summary output
 */
export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    'reports/user-dashboard-summary.json': JSON.stringify(data, null, 2),
  };
}
