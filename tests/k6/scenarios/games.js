/**
 * Game Search Performance Test
 *
 * Target: < 500ms p95 latency, 2000 req/s throughput
 * Endpoint: GET /api/v1/games
 *
 * Issue #873
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { loadConfig, getHeaders, validateResponse, getLoadProfile, getTestType } from '../utils/common.js';
import { setupTestUser, teardownTestUser } from '../utils/auth.js';
import getThresholds from '../config/thresholds.js';

const config = loadConfig();
const testType = getTestType();
const thresholds = getThresholds(testType); // Dynamic thresholds based on test type

export const options = {
  ...getLoadProfile(testType, 200), // Base 200 VUs for high throughput

  thresholds: {
    'http_req_duration{endpoint:games}': thresholds['http_req_duration{endpoint:games}'],
    'http_req_failed': thresholds['http_req_failed'],
  },

  tags: {
    test_name: 'games',
  },
};

export function setup() {
  return setupTestUser(config.apiBaseUrl, config.testUser.email, config.testUser.password);
}

export default function (data) {
  const headers = getHeaders(data.sessionToken);

  const response = http.get(
    `${config.apiBaseUrl}/api/v1/games`,
    {
      headers: headers,
      tags: { endpoint: 'games' },
    }
  );

  // Validate response
  const success = validateResponse(response, 200);

  // Additional checks - parse once and reuse
  if (success) {
    try {
      const body = JSON.parse(response.body);
      check(body, {
        'has games array': (b) => Array.isArray(b),
        'has at least one game': (b) => b.length > 0,
        'games have required fields': (b) => {
          if (b.length === 0) return true;
          const game = b[0];
          return game.id && game.title && game.publisher;
        },
      });
    } catch (e) {
      console.error('Failed to parse games response:', e);
    }
  }

  sleep(0.5); // 500ms think time (fast browsing)
}

export function teardown(data) {
  teardownTestUser(config.apiBaseUrl, data.sessionToken);
}

export function handleSummary(data) {
  return {
    'reports/games-summary.json': JSON.stringify(data, null, 2),
    stdout: textSummary(data, { indent: '  ', enableColors: true }),
  };
}

function textSummary(data, options) {
  const indent = options?.indent || '';

  let summary = `\n${indent}Game Search Performance Test Summary\n`;
  summary += `${indent}====================================\n\n`;

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
  const p95Threshold = 500;
  const p95Value = metrics.http_req_duration?.values['p(95)'] || 0;
  const p95Status = p95Value < p95Threshold ? '✓ PASS' : '✗ FAIL';
  summary += `${indent}  P95 < 500ms: ${p95Value.toFixed(2)}ms ${p95Status}\n`;

  const errorThreshold = 0.005;
  const errorRate = metrics.http_req_failed?.values.rate || 0;
  const errorStatus = errorRate < errorThreshold ? '✓ PASS' : '✗ FAIL';
  summary += `${indent}  Error Rate < 0.5%: ${(errorRate * 100).toFixed(2)}% ${errorStatus}\n`;

  return summary;
}
