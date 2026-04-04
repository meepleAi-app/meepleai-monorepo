/**
 * ISSUE-184: Performance Regression Baseline - Deploy Smoke Test
 *
 * Lightweight post-deploy validation against live URLs.
 * Designed for CI/CD pipelines to confirm core public endpoints
 * are healthy after a deployment completes.
 *
 * This script is self-contained and requires NO authentication.
 * Do NOT import from shared-config.js (requires auth setup).
 *
 * Endpoints tested (no auth required):
 * - GET {API_BASE_URL}/health
 * - GET {BASE_URL}/
 * - GET {API_BASE_URL}/api/v1/shared-game-catalog/games?limit=1
 *
 * Configuration via environment variables:
 *   BASE_URL     - Frontend base URL (default: https://meepleai.app)
 *   API_BASE_URL - API base URL (default: BASE_URL). Set explicitly
 *                  in production where API lives on a separate subdomain.
 *
 * Performance Targets (relaxed for external network):
 * - Response time p95 < 2000ms
 * - Response time p99 < 3000ms
 * - Error rate < 10%
 *
 * Usage:
 *   k6 run tests/k6/scenarios/deploy-smoke.js \
 *     --env BASE_URL=https://meepleai.app \
 *     --env API_BASE_URL=https://api.meepleai.app \
 *     --summary-export=reports/deploy-smoke-summary.json
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';

// ============================================
// CONFIGURATION
// ============================================

const BASE_URL = __ENV.BASE_URL || 'https://meepleai.app';
const API_BASE_URL = __ENV.API_BASE_URL || BASE_URL;

// ============================================
// CUSTOM METRICS
// ============================================

const healthLatency = new Trend('smoke_health_latency');
const frontendLatency = new Trend('smoke_frontend_latency');
const catalogLatency = new Trend('smoke_catalog_latency');
const smokeErrors = new Counter('smoke_errors');
const smokeErrorRate = new Rate('smoke_error_rate');

// ============================================
// TEST CONFIGURATION
// ============================================

export const options = {
  scenarios: {
    deploy_smoke: {
      executor: 'per-vu-iterations',
      vus: 3,
      iterations: 10,
      maxDuration: '60s',
      tags: { test_type: 'smoke', scenario: 'deploy-smoke' },
    },
  },

  // Relaxed thresholds for external network latency
  thresholds: {
    'http_req_failed': ['rate<0.10'],
    'http_req_duration': ['p(95)<2000', 'p(99)<3000'],
    'smoke_health_latency': ['p(95)<2000', 'p(99)<3000'],
    'smoke_frontend_latency': ['p(95)<2000', 'p(99)<3000'],
    'smoke_catalog_latency': ['p(95)<2000', 'p(99)<3000'],
    'smoke_error_rate': ['rate<0.10'],
  },

  // HTTP configuration
  httpDebug: __ENV.HTTP_DEBUG === 'true' ? 'full' : '',
  insecureSkipTLSVerify: false,
  noConnectionReuse: false,

  // Output
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

// ============================================
// SMOKE CHECKS
// ============================================

/**
 * Check 1: API health endpoint.
 * Verifies the backend is reachable and reports healthy status.
 */
function checkHealth() {
  const url = `${API_BASE_URL}/health`;

  const res = http.get(url, {
    tags: { endpoint: 'health' },
    timeout: '5s',
  });

  const passed = check(res, {
    'health: status 200': (r) => r.status === 200,
    'health: response time < 2s': (r) => r.timings.duration < 2000,
    'health: body not empty': (r) => r.body !== null && r.body.length > 0,
  });

  healthLatency.add(res.timings.duration);

  if (!passed) {
    smokeErrors.add(1);
    smokeErrorRate.add(1);
    console.error(`[health] FAILED - status=${res.status} duration=${res.timings.duration}ms url=${url}`);
  } else {
    smokeErrorRate.add(0);
  }
}

/**
 * Check 2: Frontend root page.
 * Verifies the web application is serving the root page.
 */
function checkFrontend() {
  const url = `${BASE_URL}/`;

  const res = http.get(url, {
    tags: { endpoint: 'frontend-root' },
    timeout: '10s',
  });

  const passed = check(res, {
    'frontend: status 200': (r) => r.status === 200,
    'frontend: response time < 2s': (r) => r.timings.duration < 2000,
    'frontend: body not empty': (r) => r.body !== null && r.body.length > 0,
  });

  frontendLatency.add(res.timings.duration);

  if (!passed) {
    smokeErrors.add(1);
    smokeErrorRate.add(1);
    console.error(`[frontend] FAILED - status=${res.status} duration=${res.timings.duration}ms url=${url}`);
  } else {
    smokeErrorRate.add(0);
  }
}

/**
 * Check 3: Public catalog API endpoint.
 * Verifies the shared game catalog is accessible and returns data.
 */
function checkCatalog() {
  const url = `${API_BASE_URL}/api/v1/shared-games?pageNumber=1&pageSize=1`;

  const res = http.get(url, {
    headers: { 'Accept': 'application/json' },
    tags: { endpoint: 'catalog-games' },
    timeout: '10s',
  });

  const passed = check(res, {
    'catalog: status OK': (r) => r.status === 200,
    'catalog: response time < 2s': (r) => r.timings.duration < 2000,
    'catalog: content-type json': (r) => {
      const ct = r.headers['Content-Type'] || '';
      return ct.includes('application/json');
    },
    'catalog: body not empty': (r) => r.body !== null && r.body.length > 0,
  });

  catalogLatency.add(res.timings.duration);

  if (!passed) {
    smokeErrors.add(1);
    smokeErrorRate.add(1);
    console.error(`[catalog] FAILED - status=${res.status} duration=${res.timings.duration}ms url=${url}`);
  } else {
    smokeErrorRate.add(0);
  }
}

// ============================================
// MAIN SCENARIO
// ============================================

/**
 * Deploy smoke scenario.
 * Runs all three public endpoint checks sequentially per iteration.
 * Short sleep between checks to avoid thundering-herd on the same VU.
 */
export default function deploySmoke() {
  checkHealth();
  sleep(0.5);

  checkFrontend();
  sleep(0.5);

  checkCatalog();
}

// ============================================
// LIFECYCLE HOOKS
// ============================================

export function setup() {
  console.log('=== Deploy Smoke Test ===');
  console.log(`BASE_URL:     ${BASE_URL}`);
  console.log(`API_BASE_URL: ${API_BASE_URL}`);
  console.log(`VUs: 3 | Iterations: 10 | Max duration: 60s`);
  console.log('========================');
}

export function handleSummary(data) {
  // K6 v0.54+ removed --summary-export; write JSON from handleSummary instead.
  // Path is passed via --env SUMMARY_EXPORT_PATH in the CI workflow.
  const summaryPath = __ENV.SUMMARY_EXPORT_PATH || 'reports/deploy/deploy-smoke-summary.json';
  return {
    [summaryPath]: JSON.stringify(data),
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
  };
}
