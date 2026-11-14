/**
 * ISSUE-2918: Admin Dashboard Load Testing - 100 Concurrent Admins with 30s Polling
 *
 * Scenario: Realistic admin dashboard usage with periodic polling
 * - 100 concurrent admin users (VUs)
 * - Each user polls metrics every 30 seconds
 * - Simulates real-time dashboard monitoring
 *
 * Performance Targets:
 * - Response time p95 < 500ms
 * - Response time p99 < 1s
 * - Error rate < 1%
 * - CPU usage < 70%
 * - Memory usage < 80%
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import {
  config,
  authenticateAdmin,
  authHeaders,
  queryString,
  recordLatency,
  scenariosFor,
  thresholdsFor,
} from '../utils/shared-config.js';

// Environment variables
const API_BASE_URL = config.apiBaseUrl;
const TEST_TYPE = __ENV.TEST_TYPE || 'smoke';

// Custom metrics
const cacheHitRate = new Rate('cache_hit_rate');
const pollingLatency = new Trend('polling_latency');
const endpointErrors = new Counter('endpoint_errors');

// Test configuration
export const options = {
  scenarios: scenariosFor(TEST_TYPE, {
    // Smoke test: Quick validation (5 VUs, 1 minute)
    smoke: {
      executor: 'constant-vus',
      vus: 5,
      duration: '1m',
      tags: { test_type: 'smoke' },
      exec: 'adminPollingScenario',
    },

    // Load test: Realistic admin polling (100 VUs, 5 minutes)
    load: {
      executor: 'constant-vus',
      vus: 100,
      duration: '5m',
      tags: { test_type: 'load' },
      exec: 'adminPollingScenario',
    },

    // Stress test: Peak load (200 VUs, 10 minutes)
    stress: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '2m', target: 100 },  // Ramp up to baseline
        { duration: '5m', target: 200 },  // Ramp to peak
        { duration: '2m', target: 200 },  // Hold peak
        { duration: '1m', target: 0 },    // Ramp down
      ],
      tags: { test_type: 'stress' },
      exec: 'adminPollingScenario',
    },
  }),

  // Performance thresholds (ISSUE-2918 DoD)
  thresholds: thresholdsFor(TEST_TYPE, {
    // Response times
    'http_req_duration{test_type:load}': [
      'p(95)<500',    // p95 < 500ms
      'p(99)<1000',   // p99 < 1s
    ],
    'polling_latency': [
      'p(95)<500',
      'p(99)<1000',
    ],

    // Error rates
    'http_req_failed': ['rate<0.01'],  // < 1% error rate
    'auth_errors': ['count<5'],         // < 5 auth failures total
    'endpoint_errors': ['count<10'],    // < 10 endpoint failures total

    // Cache effectiveness
    'cache_hit_rate': ['rate>0.7'],     // > 70% cache hit rate

    // Response-time budget per request (recordLatency)
    'latency_within_budget': ['rate>0.95'],

    // Request success
    'checks': ['rate>0.95'],            // > 95% check success rate
  }),

  // HTTP configuration
  httpDebug: __ENV.HTTP_DEBUG === 'true' ? 'full' : '',
  insecureSkipTLSVerify: true,
  noConnectionReuse: false,

  // Output
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

/**
 * Poll admin metrics endpoint
 * @param {string} token - Session cookie value (shared-config authenticate)
 * @param {string} endpoint - Endpoint path
 * @param {object} queryParams - Query parameters
 * @returns {object} Response object
 */
function pollMetrics(token, endpoint, queryParams = {}) {
  const query = queryString(queryParams);
  const url = `${API_BASE_URL}${endpoint}${query ? '?' + query : ''}`;

  const params = {
    headers: authHeaders(token),
    tags: { endpoint: endpoint.split('/').pop() },
  };

  const startTime = Date.now();
  const response = http.get(url, params);
  const latency = Date.now() - startTime;

  pollingLatency.add(latency);

  // Check for cache headers
  const cacheStatus = response.headers['X-Cache-Status'];
  if (cacheStatus) {
    cacheHitRate.add(cacheStatus === 'HIT' ? 1 : 0);
  }

  const success = check(response, {
    'metrics: status 200': (r) => r.status === 200,
    'metrics: valid JSON': (r) => {
      try {
        r.json();
        return true;
      } catch {
        return false;
      }
    },
  });
  recordLatency(response, 1000);

  if (!success) {
    endpointErrors.add(1);
  }

  return response;
}

/**
 * Simulate realistic admin polling behavior
 */
export function adminPollingScenario() {
  // Authenticate once per VU
  const token = authenticateAdmin();
  if (!token) {
    console.error('Failed to authenticate, skipping polling');
    return;
  }

  // Define admin endpoints to poll (weighted distribution)
  const adminEndpoints = [
    // LLM Analytics (40% of requests - most frequent)
    { path: '/api/v1/admin/llm/efficiency-report', weight: 0.25, params: {} },
    { path: '/api/v1/admin/llm/monthly-report', weight: 0.10, params: {} },
    { path: '/api/v1/admin/llm/model-recommendations', weight: 0.05, params: { useCase: 'qa' } },

    // Reports (30% of requests). #4978: /admin/reports, /system-health and /usage-stats do not exist (404); these
    // are the two GET routes of ReportingEndpoints, and `limit` is a required parameter of /executions.
    { path: '/api/v1/admin/reports/scheduled', weight: 0.15, params: {} },
    { path: '/api/v1/admin/reports/executions', weight: 0.15, params: { limit: 20 } },

    // AI Models & Tier Routing (20% of requests)
    { path: '/api/v1/admin/ai-models', weight: 0.10, params: {} },
    { path: '/api/v1/admin/tier-routing', weight: 0.10, params: {} },

    // Alert Configuration (10% of requests - less frequent)
    { path: '/api/v1/admin/alert-configuration', weight: 0.05, params: {} },
    { path: '/api/v1/admin/alert-rules', weight: 0.05, params: {} },
  ];

  // Polling loop (30 second intervals)
  const pollingInterval = 30; // seconds
  const iterations = 10;       // Total polling iterations

  for (let i = 0; i < iterations; i++) {
    // Randomly select endpoint based on weight
    const random = Math.random();
    let cumulativeWeight = 0;
    let selectedEndpoint = adminEndpoints[0];

    for (const endpoint of adminEndpoints) {
      cumulativeWeight += endpoint.weight;
      if (random <= cumulativeWeight) {
        selectedEndpoint = endpoint;
        break;
      }
    }

    // Poll selected endpoint
    pollMetrics(token, selectedEndpoint.path, selectedEndpoint.params);

    // Simulate realistic dashboard behavior: multiple quick polls
    // (users typically check 2-3 different sections in quick succession)
    const quickPollCount = Math.floor(Math.random() * 3) + 1;
    const quickPollStartTime = Date.now();
    for (let j = 0; j < quickPollCount; j++) {
      const quickEndpoint = adminEndpoints[Math.floor(Math.random() * adminEndpoints.length)];
      pollMetrics(token, quickEndpoint.path, quickEndpoint.params);
      sleep(1); // Brief pause between quick polls
    }
    const quickPollElapsedTime = (Date.now() - quickPollStartTime) / 1000; // Convert to seconds

    // Wait for next polling interval
    if (i < iterations - 1) {
      const remainingSleep = Math.max(0, pollingInterval - quickPollElapsedTime);
      sleep(remainingSleep); // Adjust for actual elapsed time
    }
  }
}

/**
 * Setup function (runs once per VU)
 */
export function setup() {
  console.log('='.repeat(60));
  console.log('K6 Admin Polling Load Test - ISSUE-2918');
  console.log('='.repeat(60));
  console.log(`API Base URL: ${API_BASE_URL}`);
  console.log(`Test User: ${config.testAdminEmail}`);
  console.log(`Scenarios: ${Object.keys(options.scenarios).join(', ')}`);
  console.log('='.repeat(60));

  // Verify API health
  const healthCheck = http.get(`${API_BASE_URL}/health/live`);
  if (healthCheck.status !== 200) {
    throw new Error(`API health check failed: ${healthCheck.status}`);
  }
  console.log('✅ API health check passed');

  return {};
}

/**
 * Teardown function (runs once at end)
 */
export function teardown(data) {
  console.log('='.repeat(60));
  console.log('Test completed');
  console.log('='.repeat(60));
}

/**
 * Handle summary (custom summary output)
 */
export function handleSummary(data) {
  const summary = {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    // `-summary.json`, like the other scenarios: utils/baseline-compare.sh reads only that pattern (#4978).
    'reports/admin-polling-summary.json': JSON.stringify(data, null, 2),
  };

  return summary;
}

// Import textSummary from k6 (if available)
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';
