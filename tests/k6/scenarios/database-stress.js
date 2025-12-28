/**
 * Database Stress Testing
 *
 * Tests concurrent query handling, complex joins, aggregations
 *
 * Issue #873
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { loadConfig, getHeaders, validateResponse, getTestType } from '../utils/common.js';
import { setupTestUser, teardownTestUser } from '../utils/auth.js';
import { recordDbMetrics } from '../utils/metrics.js';
import getThresholds from '../config/thresholds.js';

const config = loadConfig();
const testType = getTestType();
const thresholds = getThresholds(testType); // Dynamic thresholds based on test type

export const options = {
  scenarios: {
    // Complex query scenario - tests joins and aggregations
    complex_queries: {
      executor: 'ramping-vus',
      startVUs: 10,
      stages: [
        { duration: '1m', target: 50 },
        { duration: '3m', target: 50 },
        { duration: '1m', target: 0 },
      ],
      gracefulRampDown: '30s',
      exec: 'complexQueries',
    },

    // Concurrent write scenario - tests write contention
    concurrent_writes: {
      executor: 'constant-arrival-rate',
      rate: 100, // 100 writes per second
      timeUnit: '1s',
      duration: '2m',
      preAllocatedVUs: 20,
      maxVUs: 100,
      exec: 'concurrentWrites',
      startTime: '5m', // Start after complex queries
    },

    // Read-heavy scenario - tests read scalability
    read_heavy: {
      executor: 'ramping-arrival-rate',
      startRate: 100,
      timeUnit: '1s',
      stages: [
        { duration: '30s', target: 500 },
        { duration: '1m', target: 1000 },
        { duration: '30s', target: 100 },
      ],
      preAllocatedVUs: 50,
      maxVUs: 200,
      exec: 'readHeavy',
      startTime: '7m', // Start after concurrent writes
    },
  },

  thresholds: {
    'http_req_duration{endpoint:database}': thresholds['http_req_duration{endpoint:database}'],
    'http_req_failed': thresholds['http_req_failed'],
    'db_query_time': ['p(95)<200'],
  },

  tags: {
    test_name: 'database-stress',
  },
};

export function setup() {
  return setupTestUser(config.apiBaseUrl, config.testUser.email, config.testUser.password);
}

// Complex queries scenario
export function complexQueries(data) {
  const headers = getHeaders(data.sessionToken);
  const startTime = Date.now();

  // Get game details with extended metadata (complex query with joins)
  const response = http.get(
    `${config.apiBaseUrl}/api/v1/games/${config.testGameId}/details`,
    {
      headers: headers,
      tags: { endpoint: 'database', operation: 'complex-query' },
    }
  );

  const queryTime = Date.now() - startTime;
  recordDbMetrics(queryTime, response.status !== 200);

  validateResponse(response, 200);

  check(response, {
    'has game details': (r) => {
      if (r.status !== 200) return false;
      const body = JSON.parse(r.body);
      return body.id && body.title && body.statistics;
    },
  });

  sleep(1);
}

// Concurrent writes scenario
export function concurrentWrites(data) {
  const headers = getHeaders(data.sessionToken);
  const startTime = Date.now();

  // Create a chat interaction (write operation)
  const payload = JSON.stringify({
    gameId: config.testGameId,
    query: `Test query ${Date.now()}`,
    searchMode: 'hybrid',
  });

  const response = http.post(
    `${config.apiBaseUrl}/api/v1/agents/qa`,
    payload,
    {
      headers: headers,
      tags: { endpoint: 'database', operation: 'write' },
    }
  );

  const queryTime = Date.now() - startTime;
  recordDbMetrics(queryTime, response.status !== 200);

  validateResponse(response, 200);

  sleep(0.5);
}

// Read-heavy scenario
export function readHeavy(data) {
  const headers = getHeaders(data.sessionToken);
  const startTime = Date.now();

  // Mix of different read operations
  const operations = [
    () => http.get(`${config.apiBaseUrl}/api/v1/games`, { headers, tags: { endpoint: 'database', operation: 'read-all' } }),
    () => http.get(`${config.apiBaseUrl}/api/v1/games/${config.testGameId}`, { headers, tags: { endpoint: 'database', operation: 'read-one' } }),
    () => http.get(`${config.apiBaseUrl}/api/v1/users/me/sessions`, { headers, tags: { endpoint: 'database', operation: 'read-sessions' } }),
  ];

  const operation = operations[Math.floor(Math.random() * operations.length)];
  const response = operation();

  const queryTime = Date.now() - startTime;
  recordDbMetrics(queryTime, response.status !== 200);

  validateResponse(response, 200);

  // No sleep for read-heavy scenario (simulates high load)
}

export function teardown(data) {
  teardownTestUser(config.apiBaseUrl, data.sessionToken);
}

export function handleSummary(data) {
  return {
    'reports/database-stress-summary.json': JSON.stringify(data, null, 2),
    stdout: textSummary(data, { indent: '  ' }),
  };
}

function textSummary(data, options) {
  const indent = options?.indent || '';

  let summary = `\n${indent}Database Stress Test Summary\n`;
  summary += `${indent}============================\n\n`;

  const metrics = data.metrics;

  summary += `${indent}Total Requests: ${metrics.http_reqs?.values.count || 0}\n`;
  summary += `${indent}Failed Requests: ${metrics.http_req_failed?.values.passes || 0}\n\n`;

  summary += `${indent}Query Times:\n`;
  summary += `${indent}  Avg: ${(metrics.db_query_time?.values.avg || 0).toFixed(2)}ms\n`;
  summary += `${indent}  P95: ${(metrics.db_query_time?.values['p(95)'] || 0).toFixed(2)}ms\n`;
  summary += `${indent}  P99: ${(metrics.db_query_time?.values['p(99)'] || 0).toFixed(2)}ms\n\n`;

  summary += `${indent}Connection Errors: ${metrics.db_connection_errors?.values.count || 0}\n`;

  return summary;
}
