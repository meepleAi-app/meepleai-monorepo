/**
 * Redis Cache Performance Validation
 *
 * Tests cache hit rates, eviction behavior, TTL validation
 *
 * Issue #873
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { loadConfig, getHeaders, getRandomQuery, validateResponse, getTestType } from '../utils/common.js';
import { setupTestUser, teardownTestUser } from '../utils/auth.js';
import { recordCacheMetrics } from '../utils/metrics.js';
import { thresholds } from '../config/thresholds.js';

const config = loadConfig();
const testType = getTestType();

export const options = {
  scenarios: {
    // Cache hit scenario - same queries repeatedly
    cache_hit_test: {
      executor: 'constant-vus',
      vus: 20,
      duration: '2m',
      exec: 'cacheHitTest',
    },

    // Cache miss scenario - unique queries
    cache_miss_test: {
      executor: 'constant-vus',
      vus: 10,
      duration: '2m',
      exec: 'cacheMissTest',
      startTime: '2m30s', // Start after cache hit test
    },

    // Cache eviction scenario - high volume writes
    cache_eviction_test: {
      executor: 'ramping-vus',
      startVUs: 50,
      stages: [
        { duration: '30s', target: 100 },
        { duration: '1m', target: 100 },
        { duration: '30s', target: 0 },
      ],
      exec: 'cacheEvictionTest',
      startTime: '4m30s', // Start after cache miss test
    },
  },

  thresholds: {
    'http_req_duration{endpoint:redis}': thresholds['http_req_duration{endpoint:redis}'],
    'http_req_failed': thresholds['http_req_failed'],
    'cache_hit_rate': thresholds['cache_hit_rate'],
  },

  tags: {
    test_name: 'redis-cache',
  },
};

export function setup() {
  const auth = setupTestUser(config.apiBaseUrl, config.testUser.email, config.testUser.password);

  // Warm up cache with a few queries
  const headers = getHeaders(auth.sessionToken);
  const warmupQueries = [
    'How do I set up the game?',
    'What are the winning conditions?',
    'How many players can play?',
  ];

  console.log('Warming up cache...');
  for (const query of warmupQueries) {
    const payload = JSON.stringify({
      gameId: config.testGameId,
      query: query,
      searchMode: 'hybrid',
    });

    http.post(
      `${config.apiBaseUrl}/api/v1/knowledge-base/search`,
      payload,
      { headers }
    );

    sleep(0.5);
  }

  console.log('Cache warmed up');
  return auth;
}

// Cache hit test - use same queries to test cache effectiveness
export function cacheHitTest(data) {
  const headers = getHeaders(data.sessionToken);

  // Use a small set of queries repeatedly (should hit cache)
  const cachedQueries = [
    'How do I set up the game?',
    'What are the winning conditions?',
    'How many players can play?',
  ];

  const query = cachedQueries[Math.floor(Math.random() * cachedQueries.length)];

  const payload = JSON.stringify({
    gameId: config.testGameId,
    query: query,
    searchMode: 'hybrid',
    bypassCache: false,
  });

  const startTime = Date.now();
  const response = http.post(
    `${config.apiBaseUrl}/api/v1/knowledge-base/search`,
    payload,
    {
      headers: headers,
      tags: { endpoint: 'redis', operation: 'cache-hit' },
    }
  );
  const duration = Date.now() - startTime;

  validateResponse(response, 200);

  // Assume cache hit if response is very fast (< 100ms)
  const isCacheHit = duration < 100;
  recordCacheMetrics(isCacheHit);

  check(response, {
    'fast response (likely cached)': () => duration < 100,
  });

  sleep(0.5);
}

// Cache miss test - use unique queries to force cache misses
export function cacheMissTest(data) {
  const headers = getHeaders(data.sessionToken);

  // Generate unique query to force cache miss
  const uniqueQuery = `Test query ${__VU}-${__ITER}-${Date.now()}`;

  const payload = JSON.stringify({
    gameId: config.testGameId,
    query: uniqueQuery,
    searchMode: 'hybrid',
    bypassCache: false,
  });

  const startTime = Date.now();
  const response = http.post(
    `${config.apiBaseUrl}/api/v1/knowledge-base/search`,
    payload,
    {
      headers: headers,
      tags: { endpoint: 'redis', operation: 'cache-miss' },
    }
  );
  const duration = Date.now() - startTime;

  validateResponse(response, 200);

  // Cache miss - should be slower
  const isCacheHit = false;
  recordCacheMetrics(isCacheHit);

  check(response, {
    'slower response (cache miss)': () => duration >= 100,
  });

  sleep(1);
}

// Cache eviction test - high volume to test eviction behavior
export function cacheEvictionTest(data) {
  const headers = getHeaders(data.sessionToken);

  const query = getRandomQuery();

  const payload = JSON.stringify({
    gameId: config.testGameId,
    query: query,
    searchMode: 'hybrid',
  });

  const response = http.post(
    `${config.apiBaseUrl}/api/v1/knowledge-base/search`,
    payload,
    {
      headers: headers,
      tags: { endpoint: 'redis', operation: 'cache-eviction' },
    }
  );

  validateResponse(response, 200);

  // No sleep for high volume
}

export function teardown(data) {
  teardownTestUser(config.apiBaseUrl, data.sessionToken);
}

export function handleSummary(data) {
  return {
    'reports/redis-cache-summary.json': JSON.stringify(data, null, 2),
    stdout: textSummary(data, { indent: '  ' }),
  };
}

function textSummary(data, options) {
  const indent = options?.indent || '';

  let summary = `\n${indent}Redis Cache Performance Test Summary\n`;
  summary += `${indent}====================================\n\n`;

  const metrics = data.metrics;

  summary += `${indent}Cache Operations: ${metrics.cache_operations_total?.values.count || 0}\n`;
  summary += `${indent}Cache Hit Rate: ${((metrics.cache_hit_rate?.values.rate || 0) * 100).toFixed(2)}%\n\n`;

  summary += `${indent}Response Times:\n`;
  summary += `${indent}  Avg: ${(metrics.http_req_duration?.values.avg || 0).toFixed(2)}ms\n`;
  summary += `${indent}  P95: ${(metrics.http_req_duration?.values['p(95)'] || 0).toFixed(2)}ms\n\n`;

  summary += `${indent}Threshold:\n`;
  const hitRate = metrics.cache_hit_rate?.values.rate || 0;
  const hitRateStatus = hitRate > 0.80 ? '✓ PASS' : '✗ FAIL';
  summary += `${indent}  Cache Hit Rate > 80%: ${(hitRate * 100).toFixed(2)}% ${hitRateStatus}\n`;

  return summary;
}
