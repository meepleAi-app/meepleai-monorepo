/**
 * Session Management Performance Test
 *
 * Target: < 100ms p95 latency, 1000 req/s throughput
 * Endpoint: GET /api/v1/auth/sessions
 *
 * Issue #873
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { loadConfig, getHeaders, validateResponse, getLoadProfile, getTestType, retryWithBackoff } from '../utils/common.js';
import { setupTestUser, teardownTestUser } from '../utils/auth.js';
import getThresholds from '../config/thresholds.js';

const config = loadConfig();
const testType = getTestType();
const thresholds = getThresholds(testType); // Dynamic thresholds based on test type

export const options = {
  ...getLoadProfile(testType, 100), // Base 100 VUs for high throughput

  thresholds: {
    'http_req_duration{endpoint:sessions}': thresholds['http_req_duration{endpoint:sessions}'],
    'http_req_failed': thresholds['http_req_failed'],
  },

  tags: {
    test_name: 'sessions',
  },
};

export function setup() {
  return setupTestUser(config.apiBaseUrl, config.testUser.email, config.testUser.password);
}

export default function (data) {
  const headers = getHeaders(data.sessionToken);

  // Issue #1663: Use retry with exponential backoff for rate limit resilience
  const response = retryWithBackoff(() =>
    http.get(
      `${config.apiBaseUrl}/api/v1/users/me/sessions`,
      {
        headers: headers,
        tags: { endpoint: 'sessions' },
      }
    ),
    { maxRetries: 3, initialDelayMs: 1000 }
  );

  // Validate response
  const success = validateResponse(response, 200);

  // Additional checks - parse once and reuse
  if (success) {
    try {
      const body = JSON.parse(response.body);
      check(body, {
        'has sessions array': (b) => Array.isArray(b),
        // Issue #2286: Smoke tests don't require data (infrastructure validation only)
        'has at least one session': (b) => testType === 'smoke' || b.length > 0,
        'sessions have required fields': (b) => {
          if (!Array.isArray(b) || b.length === 0) return true;
          const session = b[0];
          return session && session.id && session.userId && session.createdAt;
        },
      });
    } catch (e) {
      console.error('Failed to parse sessions response:', e);
    }
  }

  sleep(0.2); // 200ms think time (fast polling)
}

export function teardown(data) {
  teardownTestUser(config.apiBaseUrl, data.sessionToken);
}

export function handleSummary(data) {
  return {
    'reports/sessions-summary.json': JSON.stringify(data, null, 2),
    stdout: textSummary(data, { indent: '  ', enableColors: true }),
  };
}

function textSummary(data, options) {
  const indent = options?.indent || '';

  let summary = `\n${indent}Session Management Performance Test Summary\n`;
  summary += `${indent}===========================================\n\n`;

  const metrics = data.metrics;

  summary += `${indent}Requests:\n`;
  summary += `${indent}  Total: ${metrics.http_reqs?.values.count || 0}\n`;
  summary += `${indent}  Failed: ${metrics.http_req_failed?.values.passes || 0} (${((metrics.http_req_failed?.values.rate || 0) * 100).toFixed(2)}%)\n\n`;

  summary += `${indent}Response Times:\n`;
  summary += `${indent}  Avg: ${(metrics.http_req_duration?.values.avg || 0).toFixed(2)}ms\n`;
  summary += `${indent}  P50: ${(metrics.http_req_duration?.values['p(50)'] || 0).toFixed(2)}ms\n`;
  summary += `${indent}  P95: ${(metrics.http_req_duration?.values['p(95)'] || 0).toFixed(2)}ms\n`;
  summary += `${indent}  P99: ${(metrics.http_req_duration?.values['p(99)'] || 0).toFixed(2)}ms\n\n`;

  summary += `${indent}Throughput:\n`;
  summary += `${indent}  Requests/sec: ${(metrics.http_reqs?.values.rate || 0).toFixed(2)}\n\n`;

  summary += `${indent}Thresholds:\n`;
  const p95Threshold = 100;
  const p95Value = metrics.http_req_duration?.values['p(95)'] || 0;
  const p95Status = p95Value < p95Threshold ? '✓ PASS' : '✗ FAIL';
  summary += `${indent}  P95 < 100ms: ${p95Value.toFixed(2)}ms ${p95Status}\n`;

  const errorThreshold = 0.001;
  const errorRate = metrics.http_req_failed?.values.rate || 0;
  const errorStatus = errorRate < errorThreshold ? '✓ PASS' : '✗ FAIL';
  summary += `${indent}  Error Rate < 0.1%: ${(errorRate * 100).toFixed(2)}% ${errorStatus}\n`;

  return summary;
}
