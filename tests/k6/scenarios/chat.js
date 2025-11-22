/**
 * Chat Messaging Performance Test
 *
 * Target: < 1s p95 latency, 500 req/s throughput
 * Endpoint: POST /api/v1/agents/qa
 *
 * Issue #873
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { loadConfig, getHeaders, getRandomQuery, validateResponse, getLoadProfile, getTestType } from '../utils/common.js';
import { setupTestUser, teardownTestUser } from '../utils/auth.js';
import { recordRagMetrics } from '../utils/metrics.js';
import { thresholds } from '../config/thresholds.js';

const config = loadConfig();
const testType = getTestType();

export const options = {
  ...getLoadProfile(testType, 50), // Base 50 VUs for moderate throughput

  thresholds: {
    'http_req_duration{endpoint:chat}': thresholds['http_req_duration{endpoint:chat}'],
    'http_req_failed': thresholds['http_req_failed'],
  },

  tags: {
    test_name: 'chat',
  },
};

export function setup() {
  return setupTestUser(config.apiBaseUrl, config.testUser.email, config.testUser.password);
}

export default function (data) {
  const headers = getHeaders(data.sessionToken);
  const query = getRandomQuery();

  const payload = JSON.stringify({
    gameId: config.testGameId,
    query: query,
    searchMode: 'hybrid',
  });

  const response = http.post(
    `${config.apiBaseUrl}/api/v1/agents/qa`,
    payload,
    {
      headers: headers,
      tags: { endpoint: 'chat' },
    }
  );

  // Validate response
  const success = validateResponse(response, 200);

  // Record custom metrics
  recordRagMetrics(response);

  // Additional checks - parse once and reuse
  if (success) {
    try {
      const body = JSON.parse(response.body);
      check(body, {
        'has answer': (b) => b.answer && b.answer.length > 0,
        'has snippets': (b) => b.snippets && b.snippets.length > 0,
        'has confidence': (b) => b.confidence !== undefined && b.confidence > 0,
        'has follow-up questions': (b) => b.followUpQuestions && Array.isArray(b.followUpQuestions),
      });
    } catch (e) {
      console.error('Failed to parse chat response:', e);
    }
  }

  sleep(2); // 2s think time (simulates user reading response)
}

export function teardown(data) {
  teardownTestUser(config.apiBaseUrl, data.sessionToken);
}

export function handleSummary(data) {
  return {
    'reports/chat-summary.json': JSON.stringify(data, null, 2),
    stdout: textSummary(data, { indent: '  ', enableColors: true }),
  };
}

function textSummary(data, options) {
  const indent = options?.indent || '';

  let summary = `\n${indent}Chat Messaging Performance Test Summary\n`;
  summary += `${indent}========================================\n\n`;

  const metrics = data.metrics;

  summary += `${indent}Requests:\n`;
  summary += `${indent}  Total: ${metrics.http_reqs?.values.count || 0}\n`;
  summary += `${indent}  Failed: ${metrics.http_req_failed?.values.passes || 0} (${((metrics.http_req_failed?.values.rate || 0) * 100).toFixed(2)}%)\n\n`;

  summary += `${indent}Response Times:\n`;
  summary += `${indent}  Avg: ${(metrics.http_req_duration?.values.avg || 0).toFixed(2)}ms\n`;
  summary += `${indent}  P50: ${(metrics.http_req_duration?.values['p(50)'] || 0).toFixed(2)}ms\n`;
  summary += `${indent}  P95: ${(metrics.http_req_duration?.values['p(95)'] || 0).toFixed(2)}ms\n`;
  summary += `${indent}  P99: ${(metrics.http_req_duration?.values['p(99)'] || 0).toFixed(2)}ms\n\n`;

  summary += `${indent}Thresholds:\n`;
  const p95Threshold = 1000;
  const p95Value = metrics.http_req_duration?.values['p(95)'] || 0;
  const p95Status = p95Value < p95Threshold ? '✓ PASS' : '✗ FAIL';
  summary += `${indent}  P95 < 1000ms: ${p95Value.toFixed(2)}ms ${p95Status}\n`;

  const errorThreshold = 0.01;
  const errorRate = metrics.http_req_failed?.values.rate || 0;
  const errorStatus = errorRate < errorThreshold ? '✓ PASS' : '✗ FAIL';
  summary += `${indent}  Error Rate < 1%: ${(errorRate * 100).toFixed(2)}% ${errorStatus}\n`;

  return summary;
}
