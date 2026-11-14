/**
 * K6 Shared Configuration Module - Issue #2928
 *
 * Common utilities, thresholds, and authentication helpers for all K6 load test scenarios.
 * Provides consistent configuration across:
 * - User Dashboard Polling
 * - Library Browsing
 * - Catalog Search
 * - Admin Concurrent Actions
 */

import http from 'k6/http';
import { check } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// ============================================
// ENVIRONMENT CONFIGURATION
// ============================================

export const config = {
  apiBaseUrl: __ENV.API_BASE_URL || 'http://localhost:8080',
  testUserEmail: __ENV.TEST_USER_EMAIL || 'user@example.com',
  testUserPassword: __ENV.TEST_USER_PASSWORD || 'UserPassword123!',
  testAdminEmail: __ENV.TEST_ADMIN_EMAIL || 'admin@example.com',
  testAdminPassword: __ENV.TEST_ADMIN_PASSWORD || 'AdminPassword123!',
};

// ============================================
// SHARED METRICS
// ============================================

// Common metrics used across all scenarios
export const sharedMetrics = {
  // Error tracking
  authErrors: new Counter('auth_errors'),
  endpointErrors: new Counter('endpoint_errors'),
  validationErrors: new Counter('validation_errors'),

  // Performance tracking
  requestLatency: new Trend('request_latency'),
  cacheHitRate: new Rate('cache_hit_rate'),
  // #4978: the response-time budget of a request. It used to be a check, which made `checks` a latency
  // threshold as well; see recordLatency.
  latencyWithinBudget: new Rate('latency_within_budget'),

  // Scenario-specific (created dynamically)
  createScenarioMetrics: (scenarioName) => ({
    latency: new Trend(`${scenarioName}_latency`),
    errors: new Counter(`${scenarioName}_errors`),
    requests: new Counter(`${scenarioName}_requests`),
  }),
};

// ============================================
// COMMON THRESHOLDS
// ============================================

/**
 * Standard performance thresholds based on project requirements.
 * p95 < 500ms, p99 < 1s, error rate < 1%
 */
export const standardThresholds = {
  // Response times
  'http_req_duration': ['p(95)<500', 'p(99)<1000'],
  'request_latency': ['p(95)<500', 'p(99)<1000'],

  // Error rates
  'http_req_failed': ['rate<0.01'],
  'auth_errors': ['count<5'],
  'endpoint_errors': ['count<50'],

  // Cache effectiveness
  'cache_hit_rate': ['rate>0.5'],

  // Response-time budget per request (recordLatency)
  'latency_within_budget': ['rate>0.95'],

  // Request success
  'checks': ['rate>0.95'],
};

/**
 * Relaxed thresholds for stress testing scenarios.
 */
export const stressThresholds = {
  'http_req_duration': ['p(95)<1000', 'p(99)<2000'],
  'http_req_failed': ['rate<0.05'],
  'auth_errors': ['count<10'],
  'endpoint_errors': ['count<100'],
  'checks': ['rate>0.90'],
};

// ============================================
// TEST SCENARIO CONFIGURATIONS
// ============================================

/**
 * Predefined scenario configurations.
 * Use these to create consistent test patterns across scenarios.
 */
export const scenarioConfigs = {
  // Smoke test: Quick validation (5 VUs, 1 minute)
  smoke: {
    executor: 'constant-vus',
    vus: 5,
    duration: '1m',
  },

  // Load test: Realistic usage (50-100 VUs, 5 minutes)
  load: {
    executor: 'constant-vus',
    vus: 50,
    duration: '5m',
  },

  // Stress test: Peak load (ramp to 200 VUs, 10 minutes)
  stress: {
    executor: 'ramping-vus',
    startVUs: 0,
    stages: [
      { duration: '2m', target: 50 },
      { duration: '3m', target: 100 },
      { duration: '3m', target: 200 },
      { duration: '2m', target: 0 },
    ],
  },

  // Soak test: Extended duration (30 VUs, 30 minutes)
  soak: {
    executor: 'constant-vus',
    vus: 30,
    duration: '30m',
  },
};

/**
 * Keep only the scenario of the requested test type (#4978). Declaring smoke, load and stress together runs
 * them all at once, whatever TEST_TYPE says: a "smoke" run of a file used to take ten minutes.
 * @param {string} testType - TEST_TYPE: smoke, load, stress, or 'all' for every scenario
 * @param {object} scenarios - Scenario configurations keyed by test type
 * @returns {object} The scenarios to put in options.scenarios
 */
export function scenariosFor(testType, scenarios) {
  if (testType === 'all') {
    return scenarios;
  }
  return { [testType]: scenarios[testType] || scenarios.smoke };
}

// A smoke run is a correctness check (#4978): it fails on authentication errors and on failed checks.
const SMOKE_GATING_METRICS = ['auth_errors', 'checks'];

/**
 * Thresholds for the requested test type. In smoke only the correctness thresholds stay: on a runner that also
 * hosts the API, latency thresholds would turn a slow runner into a red run. Their metrics still appear in the
 * end-of-test summary.
 * @param {string} testType - TEST_TYPE
 * @param {object} thresholds - Every threshold of the scenario
 * @returns {object} The thresholds to put in options.thresholds
 */
export function thresholdsFor(testType, thresholds) {
  if (testType !== 'smoke') {
    return thresholds;
  }
  return Object.fromEntries(
    Object.entries(thresholds).filter(([metric]) => SMOKE_GATING_METRICS.includes(metric))
  );
}

// ============================================
// AUTHENTICATION HELPERS
// ============================================

// The API authenticates with the session cookie alone (#4978): /auth/login answers { user, expiresAt } and sets
// this cookie, and it ignores an `Authorization: Bearer` header. The scenarios used to wait for a `token` field
// that never comes, so no request of theirs was ever authenticated. The "token" these helpers pass around is the
// cookie's value.
export const SESSION_COOKIE = 'meepleai_session';

function sessionCookieOf(response) {
  const cookies = response.cookies[SESSION_COOKIE];
  return cookies && cookies.length > 0 && cookies[0].value ? cookies[0].value : null;
}

/**
 * Authenticate a user and return the session cookie value.
 * @param {string} email - User email
 * @param {string} password - User password
 * @param {string} role - 'user' or 'admin' for logging
 * @returns {string|null} Session cookie value or null on failure
 */
export function authenticate(email, password, role = 'user') {
  const loginPayload = JSON.stringify({
    email: email,
    password: password,
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
    },
    tags: { endpoint: 'auth_login', role: role },
  };

  const response = http.post(
    `${config.apiBaseUrl}/api/v1/auth/login`,
    loginPayload,
    params
  );

  const loginSuccess = check(response, {
    [`${role}: login successful`]: (r) => r.status === 200,
    [`${role}: session cookie received`]: (r) => sessionCookieOf(r) !== null,
  });

  if (!loginSuccess) {
    sharedMetrics.authErrors.add(1);
    console.error(`[${role}] Authentication failed: ${response.status} - ${response.body}`);
    return null;
  }

  return sessionCookieOf(response);
}

/**
 * Authenticate as regular user using default credentials.
 * @returns {string|null} Session cookie value or null on failure
 */
export function authenticateUser() {
  return authenticate(config.testUserEmail, config.testUserPassword, 'user');
}

/**
 * Authenticate as admin user using default credentials.
 * @returns {string|null} Session cookie value or null on failure
 */
export function authenticateAdmin() {
  return authenticate(config.testAdminEmail, config.testAdminPassword, 'admin');
}

// ============================================
// HTTP REQUEST HELPERS
// ============================================

/**
 * Create the headers of an authenticated request. The session goes in an explicit Cookie header, so the
 * request does not depend on the cookie jar keeping a `Secure` cookie over plain http.
 * @param {string} token - Session cookie value
 * @returns {object} Headers object
 */
export function authHeaders(token) {
  return {
    'Cookie': `${SESSION_COOKIE}=${token}`,
    'Content-Type': 'application/json',
  };
}

/**
 * Record whether a response came within its time budget (#4978). Not a check: `checks` gates correctness,
 * and in smoke a slow runner must not turn it red. The rate shows in the summary, and gates load and stress
 * runs through the `latency_within_budget` threshold.
 * @param {object} response - HTTP response
 * @param {number} budgetMs - Time budget in milliseconds
 */
export function recordLatency(response, budgetMs) {
  sharedMetrics.latencyWithinBudget.add(response.timings.duration < budgetMs);
}

/**
 * Encode query parameters (#4978). The k6 runtime has no URLSearchParams: every authenticated GET used to throw a
 * ReferenceError there, and k6 ends an iteration that throws without failing the run, so the scenarios exited 0
 * without calling a single endpoint. utils/run-scenarios.sh now fails a scenario that throws.
 * @param {object} params - Query parameters; undefined and null values are left out
 * @returns {string} The query string, without the leading '?'
 */
export function queryString(params = {}) {
  return Object.entries(params)
    .filter(([, value]) => value !== undefined && value !== null)
    .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(String(value))}`)
    .join('&');
}

/**
 * Make authenticated GET request with metrics tracking.
 * @param {string} token - JWT token
 * @param {string} endpoint - API endpoint path (without base URL)
 * @param {object} queryParams - Query parameters
 * @param {object} tags - Request tags for metrics
 * @returns {object} HTTP response
 */
export function authGet(token, endpoint, queryParams = {}, tags = {}) {
  const query = queryString(queryParams);
  const url = `${config.apiBaseUrl}${endpoint}${query ? '?' + query : ''}`;

  const params = {
    headers: authHeaders(token),
    tags: { endpoint: endpoint.split('/').pop(), ...tags },
  };

  const startTime = Date.now();
  const response = http.get(url, params);
  const latency = Date.now() - startTime;

  sharedMetrics.requestLatency.add(latency);

  // Track cache hits
  const cacheStatus = response.headers['X-Cache-Status'];
  if (cacheStatus) {
    sharedMetrics.cacheHitRate.add(cacheStatus === 'HIT' ? 1 : 0);
  }

  return response;
}

/**
 * Make authenticated POST request with metrics tracking.
 * @param {string} token - JWT token
 * @param {string} endpoint - API endpoint path
 * @param {object} body - Request body
 * @param {object} tags - Request tags for metrics
 * @returns {object} HTTP response
 */
export function authPost(token, endpoint, body = {}, tags = {}) {
  const url = `${config.apiBaseUrl}${endpoint}`;

  const params = {
    headers: authHeaders(token),
    tags: { endpoint: endpoint.split('/').pop(), ...tags },
  };

  const startTime = Date.now();
  const response = http.post(url, JSON.stringify(body), params);
  const latency = Date.now() - startTime;

  sharedMetrics.requestLatency.add(latency);

  return response;
}

/**
 * Make authenticated PUT request with metrics tracking.
 * @param {string} token - JWT token
 * @param {string} endpoint - API endpoint path
 * @param {object} body - Request body
 * @param {object} tags - Request tags for metrics
 * @returns {object} HTTP response
 */
export function authPut(token, endpoint, body = {}, tags = {}) {
  const url = `${config.apiBaseUrl}${endpoint}`;

  const params = {
    headers: authHeaders(token),
    tags: { endpoint: endpoint.split('/').pop(), ...tags },
  };

  const startTime = Date.now();
  const response = http.put(url, JSON.stringify(body), params);
  const latency = Date.now() - startTime;

  sharedMetrics.requestLatency.add(latency);

  return response;
}

// ============================================
// VALIDATION HELPERS
// ============================================

/**
 * Standard response validation checks.
 * @param {object} response - HTTP response
 * @param {string} description - Check description prefix
 * @returns {boolean} True if all checks pass
 */
export function validateResponse(response, description = 'request') {
  const success = check(response, {
    [`${description}: status 200`]: (r) => r.status === 200,
    [`${description}: valid JSON`]: (r) => {
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
    sharedMetrics.endpointErrors.add(1);
  }

  return success;
}

/**
 * Validate paginated response structure.
 * @param {object} response - HTTP response
 * @param {string} description - Check description prefix
 * @returns {boolean} True if all checks pass
 */
export function validatePaginatedResponse(response, description = 'paginated') {
  const success = check(response, {
    [`${description}: status 200`]: (r) => r.status === 200,
    [`${description}: has items`]: (r) => {
      try {
        const data = r.json();
        return Array.isArray(data.items) || Array.isArray(data.data) || Array.isArray(data);
      } catch {
        return false;
      }
    },
  });
  recordLatency(response, 1000);

  if (!success) {
    sharedMetrics.endpointErrors.add(1);
  }

  return success;
}

// ============================================
// UTILITY FUNCTIONS
// ============================================

/**
 * Generate random page number for pagination testing.
 * @param {number} maxPages - Maximum page number
 * @returns {number} Random page between 1 and maxPages
 */
export function randomPage(maxPages = 10) {
  return Math.floor(Math.random() * maxPages) + 1;
}

/**
 * Generate random page size for pagination testing.
 * @returns {number} Random page size (10, 20, 50)
 */
export function randomPageSize() {
  const sizes = [10, 20, 50];
  return sizes[Math.floor(Math.random() * sizes.length)];
}

/**
 * Select random item from array based on weights.
 * @param {Array} items - Array of items with 'weight' property
 * @returns {object} Selected item
 */
export function weightedRandom(items) {
  const random = Math.random();
  let cumulativeWeight = 0;

  for (const item of items) {
    cumulativeWeight += item.weight;
    if (random <= cumulativeWeight) {
      return item;
    }
  }

  return items[items.length - 1];
}

/**
 * API health check validation.
 * @returns {boolean} True if API is healthy
 */
export function checkApiHealth() {
  const response = http.get(`${config.apiBaseUrl}/health/live`);
  return response.status === 200;
}

// ============================================
// SETUP/TEARDOWN HELPERS
// ============================================

/**
 * Standard setup function for scenarios.
 * @param {string} scenarioName - Name of the scenario
 * @param {object} options - K6 options object
 */
export function standardSetup(scenarioName, options) {
  console.log('='.repeat(60));
  console.log(`K6 Load Test - ${scenarioName} - Issue #2928`);
  console.log('='.repeat(60));
  console.log(`API Base URL: ${config.apiBaseUrl}`);
  console.log(`Scenarios: ${Object.keys(options.scenarios || {}).join(', ')}`);
  console.log('='.repeat(60));

  // Verify API health
  if (!checkApiHealth()) {
    throw new Error('API health check failed');
  }
  console.log('API health check passed');

  return {};
}

/**
 * Standard teardown function for scenarios.
 */
export function standardTeardown() {
  console.log('='.repeat(60));
  console.log('Test completed');
  console.log('='.repeat(60));
}
