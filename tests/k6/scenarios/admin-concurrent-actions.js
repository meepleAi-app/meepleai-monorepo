/**
 * ISSUE-2928: Concurrent Admin Actions Load Testing
 *
 * Scenario 4: Multiple admins performing concurrent operations.
 * Extends admin-polling.js with write operations and concurrent action testing.
 *
 * Focus areas:
 * - Concurrent read/write operations
 * - Batch admin actions
 * - User management operations
 * - Configuration changes
 * - Audit log stress testing
 *
 * Performance Targets:
 * - Response time p95 < 500ms for reads
 * - Response time p99 < 1s for writes
 * - Error rate < 1%
 * - No data corruption under concurrent load
 */

import http from 'k6/http';
import { sleep, check, group } from 'k6';
import { Rate, Trend, Counter, Gauge } from 'k6/metrics';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';
import {
  config,
  authenticateAdmin,
  authGet,
  authPost,
  authPut,
  queryString,
  validateResponse,
  validatePaginatedResponse,
  standardThresholds,
  scenarioConfigs,
  scenariosFor,
  thresholdsFor,
  recordLatency,
  standardSetup,
  standardTeardown,
  weightedRandom,
} from '../utils/shared-config.js';

// ============================================
// CUSTOM METRICS
// ============================================

// Read operation metrics
const adminReadLatency = new Trend('admin_read_latency');
const adminWriteLatency = new Trend('admin_write_latency');
const batchOperationLatency = new Trend('batch_operation_latency');

// Error tracking
const readErrors = new Counter('admin_read_errors');
const writeErrors = new Counter('admin_write_errors');
const concurrencyErrors = new Counter('concurrency_errors');

// Concurrency tracking
const activeOperations = new Gauge('active_operations');
const concurrentWrites = new Counter('concurrent_writes');

// ============================================
// TEST CONFIGURATION
// ============================================

const TEST_TYPE = __ENV.TEST_TYPE || 'smoke';

const testTypeScenarios = {
  // Smoke test: Quick validation
  smoke: {
    executor: 'constant-vus',
    vus: 3,
    duration: '1m',
    tags: { test_type: 'smoke', scenario: 'admin-concurrent' },
    exec: 'adminConcurrentScenario',
  },

  // Load test: Multiple concurrent admins (20 VUs)
  load: {
    executor: 'constant-vus',
    vus: 20,
    duration: '5m',
    tags: { test_type: 'load', scenario: 'admin-concurrent' },
    exec: 'adminConcurrentScenario',
  },

  // Stress test: High concurrency admin operations
  stress: {
    executor: 'ramping-vus',
    startVUs: 0,
    stages: [
      { duration: '1m', target: 10 },
      { duration: '2m', target: 30 },
      { duration: '3m', target: 50 },
      { duration: '2m', target: 50 },
      { duration: '2m', target: 0 },
    ],
    tags: { test_type: 'stress', scenario: 'admin-concurrent' },
    exec: 'adminConcurrentScenario',
  },
};

// Concurrent writes: Focus on write operations
const concurrentWritesScenario = {
  executor: 'per-vu-iterations',
  vus: 10,
  iterations: 20,
  maxDuration: '10m',
  tags: { test_type: 'concurrent-writes', scenario: 'admin-concurrent' },
  exec: 'concurrentWriteScenario',
};

export const options = {
  // concurrent_writes runs with every test type but smoke, as it did when all the scenarios were declared
  // together (#4978).
  scenarios: TEST_TYPE === 'smoke'
    ? scenariosFor(TEST_TYPE, testTypeScenarios)
    : { ...scenariosFor(TEST_TYPE, testTypeScenarios), concurrent_writes: concurrentWritesScenario },

  // Performance thresholds
  thresholds: thresholdsFor(TEST_TYPE, {
    ...standardThresholds,
    'admin_read_latency': ['p(95)<500', 'p(99)<1000'],
    'admin_write_latency': ['p(95)<750', 'p(99)<1500'],
    'batch_operation_latency': ['p(95)<1000', 'p(99)<2000'],
    'admin_read_errors': ['count<10'],
    'admin_write_errors': ['count<5'],
    'concurrency_errors': ['count<3'],
  }),

  // HTTP configuration
  httpDebug: __ENV.HTTP_DEBUG === 'true' ? 'full' : '',
  insecureSkipTLSVerify: true,
  noConnectionReuse: false,

  // Output
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

// ============================================
// ADMIN ENDPOINTS
// ============================================

// Read endpoints (from admin-polling.js + additional). #4978: /admin/reports and /admin/reports/system-health
// do not exist (404); the two GET routes of ReportingEndpoints take their place.
const readEndpoints = [
  { path: '/api/v1/admin/llm/efficiency-report', weight: 0.15 },
  { path: '/api/v1/admin/llm/monthly-report', weight: 0.10 },
  { path: '/api/v1/admin/reports/scheduled', weight: 0.15 },
  { path: '/api/v1/admin/reports/executions', weight: 0.10, params: { limit: 20 } },
  { path: '/api/v1/admin/ai-models', weight: 0.10 },
  { path: '/api/v1/admin/tier-routing', weight: 0.10 },
  { path: '/api/v1/admin/alert-configuration', weight: 0.10 },
  { path: '/api/v1/admin/users', weight: 0.10, params: { page: 1, pageSize: 20 } },
  { path: '/api/v1/admin/audit-log', weight: 0.10, params: { limit: 50 } },
];

// The feature flag the writes toggle. setup() creates it when it is missing.
const LOAD_TEST_FLAG = 'k6.load-test-flag';

// Write endpoints (configuration updates). #4978: the old list targeted routes that do not exist
// (PUT /admin/feature-flags, POST /admin/cache/invalidate, PUT /admin/rate-limits/adjust, PUT /admin/system-config:
// 404 or 405) or sent a body the API rejects (PUT /admin/alert-configuration: 422), and the check accepted 400 and
// 404, so nobody noticed. These are real routes, each with the read that shows the change (`verify`). There is no
// admin route that adjusts the rate limits of a tier or a system config, so those two writes are gone.
const writeEndpoints = [
  {
    path: '/api/v1/admin/alert-configuration',
    method: 'PUT',
    weight: 0.40,
    body: () => ({
      configKey: 'k6.load-test.enabled',
      configValue: Math.random() > 0.5 ? 'true' : 'false',
      category: 'Global',
      description: 'Written by the k6 admin-concurrent scenario (#4978)',
    }),
    verify: '/api/v1/admin/alert-configuration',
  },
  {
    path: `/api/v1/admin/feature-flags/${LOAD_TEST_FLAG}/toggle`,
    method: 'POST',
    weight: 0.35,
    query: () => ({ enabled: Math.random() > 0.5 }),
    verify: `/api/v1/admin/feature-flags/${LOAD_TEST_FLAG}`,
  },
  {
    path: '/api/v1/admin/configurations/cache/invalidate',
    method: 'POST',
    weight: 0.25,
    verify: '/api/v1/admin/configurations',
  },
];

// ============================================
// ADMIN OPERATIONS
// ============================================

/**
 * Perform admin read operation with metrics tracking.
 */
function adminRead(token, endpoint) {
  const startTime = Date.now();
  activeOperations.add(1);

  const response = authGet(token, endpoint.path, endpoint.params || {});
  const latency = Date.now() - startTime;

  adminReadLatency.add(latency);
  activeOperations.add(-1);

  // #4978: 200 only. Accepting 404 made a route that no longer exists look like a working one.
  const success = check(response, {
    'read: status OK': (r) => r.status === 200,
  });
  recordLatency(response, 1000);

  if (!success) {
    readErrors.add(1);
  }

  return response;
}

/**
 * Perform admin write operation with metrics tracking.
 */
function adminWrite(token, endpoint) {
  const startTime = Date.now();
  activeOperations.add(1);
  concurrentWrites.add(1);

  const body = typeof endpoint.body === 'function' ? endpoint.body() : endpoint.body;
  const query = typeof endpoint.query === 'function' ? endpoint.query() : endpoint.query;
  const path = query ? `${endpoint.path}?${queryString(query)}` : endpoint.path;
  let response;

  if (endpoint.method === 'POST') {
    response = authPost(token, path, body);
  } else {
    response = authPut(token, path, body);
  }

  const latency = Date.now() - startTime;
  adminWriteLatency.add(latency);
  activeOperations.add(-1);

  const success = check(response, {
    // #4978: 2xx only. Accepting 400 and 404 hid that most of these writes went to routes that do not exist.
    'write: status OK': (r) => [200, 201, 204].includes(r.status),
    'write: no conflict': (r) => r.status !== 409,
  });

  if (!success) {
    writeErrors.add(1);
    if (response.status === 409) {
      concurrencyErrors.add(1);
    }
  }

  return response;
}

/**
 * Perform batch read operation (multiple endpoints in sequence).
 */
function batchRead(token, endpoints) {
  const startTime = Date.now();

  const results = endpoints.map((endpoint) => adminRead(token, endpoint));

  batchOperationLatency.add(Date.now() - startTime);

  return results;
}

// ============================================
// ADMIN ACTION PATTERNS
// ============================================

/**
 * Pattern 1: Dashboard refresh - multiple concurrent reads
 * Admin refreshes dashboard, triggering multiple API calls.
 */
function dashboardRefreshPattern(token) {
  group('dashboard-refresh', function () {
    // Simulate dashboard loading multiple widgets concurrently
    const dashboardEndpoints = readEndpoints.slice(0, 5);
    batchRead(token, dashboardEndpoints);
  });
}

/**
 * Pattern 2: User management - read/write cycle
 * Admin views user list, then performs action.
 */
function userManagementPattern(token) {
  group('user-management', function () {
    // View user list
    adminRead(token, { path: '/api/v1/admin/users', params: { page: 1, pageSize: 20 } });

    sleep(1); // Admin reviews list

    // View specific user (simulated)
    adminRead(token, { path: '/api/v1/admin/audit-log', params: { limit: 20 } });

    sleep(0.5);

    // Potential write operation (30% chance)
    if (Math.random() < 0.3) {
      // Simulated user action - use cache invalidation as proxy
      adminWrite(token, writeEndpoints.find((e) => e.path.includes('cache')));
    }
  });
}

/**
 * Pattern 3: Configuration update - read-modify-write cycle
 * Admin reads config, modifies, and saves.
 */
function configurationUpdatePattern(token) {
  group('config-update', function () {
    // Read current configuration
    adminRead(token, { path: '/api/v1/admin/alert-configuration' });

    sleep(2); // Admin reviews and modifies

    // Write updated configuration
    const writeEndpoint = weightedRandom(writeEndpoints);
    adminWrite(token, writeEndpoint);

    sleep(0.5);

    // Verify change
    adminRead(token, { path: writeEndpoint.verify });
  });
}

/**
 * Pattern 4: Monitoring review - heavy read pattern
 * Admin reviews system health and reports.
 */
function monitoringReviewPattern(token) {
  group('monitoring-review', function () {
    const monitoringEndpoints = [
      { path: '/api/v1/admin/reports/executions', params: { limit: 20 } },
      { path: '/api/v1/admin/llm/efficiency-report' },
      { path: '/api/v1/admin/llm/monthly-report' },
      { path: '/api/v1/admin/tier-routing' },
    ];

    for (const endpoint of monitoringEndpoints) {
      adminRead(token, endpoint);
      sleep(0.5); // Reading time
    }
  });
}

/**
 * Pattern 5: Rapid config changes - stress test writes
 * Multiple rapid configuration updates.
 */
function rapidConfigChangesPattern(token) {
  group('rapid-config-changes', function () {
    // Multiple write operations in quick succession
    for (let i = 0; i < 3; i++) {
      const endpoint = weightedRandom(writeEndpoints);
      adminWrite(token, endpoint);
      sleep(0.2);
    }

    // Verify final state
    adminRead(token, { path: '/api/v1/admin/alert-configuration' });
  });
}

// ============================================
// MAIN SCENARIOS
// ============================================

/**
 * Main concurrent admin scenario.
 * Simulates realistic admin behavior with mixed operations.
 */
export function adminConcurrentScenario() {
  const token = authenticateAdmin();
  if (!token) {
    console.error('Failed to authenticate admin, skipping scenario');
    return;
  }

  // Action patterns with weights
  const patterns = [
    { fn: dashboardRefreshPattern, weight: 0.30 },
    { fn: userManagementPattern, weight: 0.25 },
    { fn: configurationUpdatePattern, weight: 0.20 },
    { fn: monitoringReviewPattern, weight: 0.20 },
    { fn: rapidConfigChangesPattern, weight: 0.05 },
  ];

  // Execute multiple admin sessions
  const sessionCount = 5;
  for (let i = 0; i < sessionCount; i++) {
    // Select pattern based on weight
    const pattern = weightedRandom(patterns);
    pattern.fn(token);

    // Wait between sessions
    if (i < sessionCount - 1) {
      sleep(Math.random() * 5 + 3); // 3-8 seconds
    }
  }
}

/**
 * Concurrent write stress test scenario.
 * Focus on testing concurrent write operations for data integrity.
 */
export function concurrentWriteScenario() {
  const token = authenticateAdmin();
  if (!token) {
    console.error('Failed to authenticate admin, skipping scenario');
    return;
  }

  // Rapid fire write operations
  const writeCount = 5;
  for (let i = 0; i < writeCount; i++) {
    const endpoint = weightedRandom(writeEndpoints);
    adminWrite(token, endpoint);

    // Brief pause to allow interleaving with other VUs
    sleep(Math.random() * 0.5 + 0.1);
  }

  // Final verification read
  adminRead(token, { path: '/api/v1/admin/alert-configuration' });
}

// ============================================
// LIFECYCLE HOOKS
// ============================================

export function setup() {
  const data = standardSetup('Admin Concurrent Actions', options);

  // The flag the writes toggle (#4978). Created only when missing: POST /admin/feature-flags with a key that
  // already exists answers 500, not 409 (#5016).
  const token = authenticateAdmin();
  if (!token) {
    throw new Error('setup: admin login failed');
  }
  const existing = authGet(token, `/api/v1/admin/feature-flags/${LOAD_TEST_FLAG}`);
  if (existing.status === 404) {
    const created = authPost(token, '/api/v1/admin/feature-flags', {
      key: LOAD_TEST_FLAG,
      enabled: false,
      description: 'Toggled by the k6 admin-concurrent scenario (#4978)',
    });
    if (created.status !== 201 && created.status !== 200) {
      throw new Error(`setup: creating feature flag ${LOAD_TEST_FLAG} answered ${created.status}`);
    }
  } else if (existing.status !== 200) {
    throw new Error(`setup: reading feature flag ${LOAD_TEST_FLAG} answered ${existing.status}`);
  }

  return data;
}

export function teardown(data) {
  standardTeardown();
}

export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    'reports/admin-concurrent-summary.json': JSON.stringify(data, null, 2),
  };
}
