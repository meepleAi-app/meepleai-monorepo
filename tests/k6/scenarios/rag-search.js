/**
 * RAG Search Performance Test
 *
 * Target: < 2s p95 latency, 1000 req/s throughput
 * Endpoint: POST /api/v1/knowledge-base/search
 *
 * Issue #873
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { loadConfig, getHeaders, getRandomQuery, validateResponse, getLoadProfile, getTestType } from '../utils/common.js';
import { setupTestUser, teardownTestUser } from '../utils/auth.js';
import { recordRagMetrics } from '../utils/metrics.js';
import getThresholds from '../config/thresholds.js';

const config = loadConfig();
const testType = getTestType();
const thresholds = getThresholds(testType); // Dynamic thresholds based on test type

export const options = {
  ...getLoadProfile(testType, 100), // Base 100 VUs for high throughput

  thresholds: {
    'http_req_duration{endpoint:rag-search}': thresholds['http_req_duration{endpoint:rag-search}'],
    'http_req_failed': thresholds['http_req_failed'],
    ...(thresholds['rag_confidence'] && { 'rag_confidence': thresholds['rag_confidence'] }), // Conditional (smoke tests skip)
  },

  tags: {
    test_name: 'rag-search',
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
    topK: 5,
    minScore: 0.7,
    searchMode: 'hybrid',
    language: 'en',
  });

  const response = http.post(
    `${config.apiBaseUrl}/api/v1/knowledge-base/search`,
    payload,
    {
      headers: headers,
      tags: { endpoint: 'rag-search' },
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
        'has results': (b) => b.results && b.results.length > 0,
        'search mode is hybrid': (b) => b.searchMode === 'hybrid',
      });
    } catch (e) {
      console.error('Failed to parse RAG search response:', e);
    }
  }

  sleep(1); // 1s think time
}

export function teardown(data) {
  teardownTestUser(config.apiBaseUrl, data.sessionToken);
}

export function handleSummary(data) {
  return {
    'reports/rag-search-summary.json': JSON.stringify(data, null, 2),
    stdout: textSummary(data, { indent: '  ', enableColors: true }),
  };
}

function textSummary(data, options) {
  const indent = options?.indent || '';
  const colors = options?.enableColors || false;

  let summary = `\n${indent}RAG Search Performance Test Summary\n`;
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

  summary += `${indent}RAG Metrics:\n`;
  summary += `${indent}  Avg Confidence: ${(metrics.rag_confidence?.values.avg || 0).toFixed(3)}\n`;
  summary += `${indent}  Avg Snippets: ${(metrics.rag_snippet_count?.values.avg || 0).toFixed(1)}\n`;
  summary += `${indent}  Avg Tokens: ${(metrics.rag_tokens?.values.avg || 0).toFixed(0)}\n\n`;

  summary += `${indent}Thresholds:\n`;
  const p95Threshold = 2000;
  const p95Value = metrics.http_req_duration?.values['p(95)'] || 0;
  const p95Status = p95Value < p95Threshold ? '✓ PASS' : '✗ FAIL';
  summary += `${indent}  P95 < 2000ms: ${p95Value.toFixed(2)}ms ${p95Status}\n`;

  const errorThreshold = 0.01;
  const errorRate = metrics.http_req_failed?.values.rate || 0;
  const errorStatus = errorRate < errorThreshold ? '✓ PASS' : '✗ FAIL';
  summary += `${indent}  Error Rate < 1%: ${(errorRate * 100).toFixed(2)}% ${errorStatus}\n`;

  return summary;
}
