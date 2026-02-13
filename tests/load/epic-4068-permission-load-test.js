/**
 * Epic #4068: Load Test for Permission System
 *
 * Tests permission API endpoints under load
 * Run with: k6 run epic-4068-permission-load-test.js
 *
 * Requirements:
 * - k6 installed: brew install k6 (macOS) or choco install k6 (Windows)
 * - API running on http://localhost:8080
 * - Test users seeded in database
 */

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// Custom metrics
const permissionCheckDuration = new Trend('permission_check_duration', true);
const permissionDeniedRate = new Rate('permission_denied_rate');
const cacheHitRate = new Rate('cache_hit_rate');
const permissionChecks = new Counter('permission_checks_total');

// Test configuration
export const options = {
  stages: [
    // Ramp-up: 0 → 50 users over 1 minute
    { duration: '1m', target: 50 },

    // Steady state: 50 users for 3 minutes
    { duration: '3m', target: 50 },

    // Ramp-up: 50 → 100 users over 1 minute
    { duration: '1m', target: 100 },

    // Peak load: 100 users for 5 minutes
    { duration: '5m', target: 100 },

    // Spike test: 100 → 200 users over 30 seconds
    { duration: '30s', target: 200 },

    // Spike duration: 200 users for 1 minute
    { duration: '1m', target: 200 },

    // Ramp-down: 200 → 0 over 1 minute
    { duration: '1m', target: 0 },
  ],

  thresholds: {
    // Permission API targets (Epic #4068 requirements)
    'http_req_duration{endpoint:permissions_me}': ['p(95)<100'], // 95% < 100ms
    'http_req_duration{endpoint:permissions_check}': ['p(95)<50'], // 95% < 50ms

    // Error rate
    'http_req_failed': ['rate<0.01'], // < 1% errors

    // Custom metrics
    'permission_check_duration': ['p(95)<100'],
    'permission_denied_rate': ['rate<0.1'], // < 10% denied (most users should have access)
    'cache_hit_rate': ['rate>0.8'], // > 80% cache hits (React Query frontend cache)
  },
};

// Test data: Different user tiers
const TEST_USERS = [
  { email: 'free@example.com', tier: 'free', token: null },
  { email: 'normal@example.com', tier: 'normal', token: null },
  { email: 'pro@example.com', tier: 'pro', token: null },
  { email: 'enterprise@example.com', tier: 'enterprise', token: null },
  { email: 'editor@example.com', tier: 'free', role: 'editor', token: null },
  { email: 'admin@example.com', tier: 'free', role: 'admin', token: null },
];

const BASE_URL = __ENV.API_URL || 'http://localhost:8080';
const API_BASE = `${BASE_URL}/api/v1`;

// Setup: Login all test users and cache tokens
export function setup() {
  const tokens = {};

  TEST_USERS.forEach(user => {
    const loginRes = http.post(`${API_BASE}/auth/login`, JSON.stringify({
      email: user.email,
      password: 'password' // Test password
    }), {
      headers: { 'Content-Type': 'application/json' }
    });

    if (loginRes.status === 200) {
      const body = JSON.parse(loginRes.body);
      tokens[user.email] = body.accessToken;
    } else {
      console.error(`Failed to login ${user.email}: ${loginRes.status}`);
    }
  });

  return { tokens };
}

// Main test function
export default function (data) {
  // Select random test user
  const user = TEST_USERS[Math.floor(Math.random() * TEST_USERS.length)];
  const token = data.tokens[user.email];

  if (!token) {
    console.error(`No token for ${user.email}`);
    return;
  }

  const headers = {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  };

  // Test 1: Get User Permissions (Most common operation)
  group('GET /permissions/me', () => {
    const startTime = Date.now();

    const res = http.get(`${API_BASE}/permissions/me`, { headers, tags: { endpoint: 'permissions_me' } });

    const duration = Date.now() - startTime;
    permissionCheckDuration.add(duration);
    permissionChecks.add(1);

    check(res, {
      'status is 200': (r) => r.status === 200,
      'has tier': (r) => {
        const body = JSON.parse(r.body);
        return body.tier !== undefined;
      },
      'has accessible features': (r) => {
        const body = JSON.parse(r.body);
        return Array.isArray(body.accessibleFeatures);
      },
      'response time < 100ms': (r) => r.timings.duration < 100,
    });

    if (res.status === 200) {
      const body = JSON.parse(res.body);

      // Verify tier matches expected
      if (user.tier && body.tier !== user.tier) {
        console.error(`Tier mismatch for ${user.email}: expected ${user.tier}, got ${body.tier}`);
      }

      // Track cache hits (if response header indicates cache)
      const isCached = res.headers['X-Cache-Hit'] === 'true';
      cacheHitRate.add(isCached ? 1 : 0);
    }
  });

  sleep(0.5); // Brief pause between requests

  // Test 2: Check Feature Permission (Specific feature check)
  group('GET /permissions/check', () => {
    const features = ['wishlist', 'bulk-select', 'drag-drop', 'agent.create', 'analytics.view'];
    const feature = features[Math.floor(Math.random() * features.length)];

    const res = http.get(`${API_BASE}/permissions/check?feature=${feature}`, {
      headers,
      tags: { endpoint: 'permissions_check', feature }
    });

    check(res, {
      'status is 200': (r) => r.status === 200,
      'has hasAccess field': (r) => {
        const body = JSON.parse(r.body);
        return typeof body.hasAccess === 'boolean';
      },
      'has reason field': (r) => {
        const body = JSON.parse(r.body);
        return typeof body.reason === 'string';
      },
      'response time < 50ms': (r) => r.timings.duration < 50,
    });

    if (res.status === 200) {
      const body = JSON.parse(r.body);

      // Track denied permissions
      if (!body.hasAccess) {
        permissionDeniedRate.add(1);
      } else {
        permissionDeniedRate.add(0);
      }

      // Expected denials for Free tier
      if (user.tier === 'free' && ['bulk-select', 'agent.create', 'analytics.view'].includes(feature)) {
        if (body.hasAccess) {
          console.error(`Free user ${user.email} has access to ${feature} (unexpected)`);
        }
      }
    }
  });

  sleep(0.5);

  // Test 3: Check State-Based Permission (Advanced)
  group('GET /permissions/check?state=X', () => {
    const res = http.get(`${API_BASE}/permissions/check?feature=view-game&state=published`, {
      headers,
      tags: { endpoint: 'permissions_check_state' }
    });

    check(res, {
      'status is 200': (r) => r.status === 200,
      'state validation works': (r) => {
        const body = JSON.parse(r.body);
        return body.hasAccess !== undefined;
      }
    });
  });

  sleep(1); // Pause between full test iterations
}

// Teardown: Optional cleanup
export function teardown(data) {
  console.log('Load test complete');
  console.log(`Total permission checks: ${permissionChecks.value}`);
}

// Custom summary
export function handleSummary(data) {
  return {
    'epic-4068-load-test-results.json': JSON.stringify(data, null, 2),
    stdout: `
╔═══════════════════════════════════════════════════════════════╗
║            Epic #4068: Permission Load Test Results          ║
╚═══════════════════════════════════════════════════════════════╝

📊 Requests:
  • Total:              ${data.metrics.http_reqs.values.count}
  • Rate:               ${data.metrics.http_reqs.values.rate.toFixed(2)}/s
  • Failed:             ${data.metrics.http_req_failed.values.rate.toFixed(4)} (${(data.metrics.http_req_failed.values.rate * 100).toFixed(2)}%)

⏱️  Latency (GET /permissions/me):
  • Min:                ${data.metrics['http_req_duration{endpoint:permissions_me}']?.values.min.toFixed(2)}ms
  • Avg:                ${data.metrics['http_req_duration{endpoint:permissions_me}']?.values.avg.toFixed(2)}ms
  • p95:                ${data.metrics['http_req_duration{endpoint:permissions_me}']?.values['p(95)'].toFixed(2)}ms
  • Max:                ${data.metrics['http_req_duration{endpoint:permissions_me}']?.values.max.toFixed(2)}ms

⏱️  Latency (GET /permissions/check):
  • Min:                ${data.metrics['http_req_duration{endpoint:permissions_check}']?.values.min.toFixed(2)}ms
  • Avg:                ${data.metrics['http_req_duration{endpoint:permissions_check}']?.values.avg.toFixed(2)}ms
  • p95:                ${data.metrics['http_req_duration{endpoint:permissions_check}']?.values['p(95)'].toFixed(2)}ms
  • Max:                ${data.metrics['http_req_duration{endpoint:permissions_check}']?.values.max.toFixed(2)}ms

📈 Custom Metrics:
  • Permission Denied Rate:  ${(data.metrics.permission_denied_rate?.values.rate * 100).toFixed(2)}%
  • Cache Hit Rate:          ${(data.metrics.cache_hit_rate?.values.rate * 100).toFixed(2)}%
  • Total Permission Checks: ${data.metrics.permission_checks_total?.values.count}

✅ Thresholds:
  • /permissions/me p95 < 100ms:     ${data.metrics['http_req_duration{endpoint:permissions_me}']?.values['p(95)'] < 100 ? 'PASS' : 'FAIL'}
  • /permissions/check p95 < 50ms:   ${data.metrics['http_req_duration{endpoint:permissions_check}']?.values['p(95)'] < 50 ? 'PASS' : 'FAIL'}
  • Error rate < 1%:                  ${data.metrics.http_req_failed.values.rate < 0.01 ? 'PASS' : 'FAIL'}
  • Permission denied < 10%:          ${data.metrics.permission_denied_rate?.values.rate < 0.1 ? 'PASS' : 'FAIL'}

📁 Detailed results: epic-4068-load-test-results.json
    `
  };
}
