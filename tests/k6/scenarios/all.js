/**
 * All Performance Tests
 *
 * Runs all performance test scenarios in sequence.
 * Use TEST_TYPE environment variable to control load level:
 * - smoke: Quick validation (1 minute)
 * - load: Normal traffic (10 minutes) - default
 * - stress: Beyond capacity (15 minutes)
 * - spike: Sudden surge (10 minutes)
 *
 * Usage:
 *   k6 run scenarios/all.js                    # Load test
 *   k6 run --env TEST_TYPE=smoke scenarios/all.js  # Smoke test
 *   k6 run --env TEST_TYPE=stress scenarios/all.js # Stress test
 *   k6 run --env TEST_TYPE=spike scenarios/all.js  # Spike test
 *
 * Issue #873
 */

import { group } from 'k6';
import { loadConfig, getTestType } from '../utils/common.js';
import { setupTestUser, teardownTestUser } from '../utils/auth.js';
import getThresholds from '../config/thresholds.js';

// Import test scenarios
import { default as ragSearchTest } from './rag-search.js';
import { default as chatTest } from './chat.js';
import { default as gamesTest } from './games.js';
import { default as sessionsTest } from './sessions.js';

const config = loadConfig();
const testType = getTestType();
const thresholds = getThresholds(testType); // Dynamic thresholds based on test type

export const options = {
  scenarios: {
    rag_search: {
      executor: 'ramping-vus',
      startVUs: 10,
      stages: testType === 'smoke' ? [
        { duration: '30s', target: 10 },
      ] : testType === 'stress' ? [
        { duration: '2m', target: 200 },
        { duration: '5m', target: 200 },
        { duration: '2m', target: 0 },
      ] : [ // load (default)
        { duration: '2m', target: 100 },
        { duration: '5m', target: 100 },
        { duration: '2m', target: 0 },
      ],
      exec: 'ragSearch',
      startTime: '0s',
    },

    chat: {
      executor: 'ramping-vus',
      startVUs: 5,
      stages: testType === 'smoke' ? [
        { duration: '30s', target: 5 },
      ] : testType === 'stress' ? [
        { duration: '2m', target: 100 },
        { duration: '5m', target: 100 },
        { duration: '2m', target: 0 },
      ] : [ // load (default)
        { duration: '2m', target: 50 },
        { duration: '5m', target: 50 },
        { duration: '2m', target: 0 },
      ],
      exec: 'chat',
      startTime: '0s',
    },

    games: {
      executor: 'ramping-vus',
      startVUs: 20,
      stages: testType === 'smoke' ? [
        { duration: '30s', target: 20 },
      ] : testType === 'stress' ? [
        { duration: '2m', target: 400 },
        { duration: '5m', target: 400 },
        { duration: '2m', target: 0 },
      ] : [ // load (default)
        { duration: '2m', target: 200 },
        { duration: '5m', target: 200 },
        { duration: '2m', target: 0 },
      ],
      exec: 'games',
      startTime: '0s',
    },

    sessions: {
      executor: 'ramping-vus',
      startVUs: 10,
      stages: testType === 'smoke' ? [
        { duration: '30s', target: 10 },
      ] : testType === 'stress' ? [
        { duration: '2m', target: 200 },
        { duration: '5m', target: 200 },
        { duration: '2m', target: 0 },
      ] : [ // load (default)
        { duration: '2m', target: 100 },
        { duration: '5m', target: 100 },
        { duration: '2m', target: 0 },
      ],
      exec: 'sessions',
      startTime: '0s',
    },
  },

  thresholds: thresholds,

  tags: {
    test_name: 'all',
    test_type: testType,
  },
};

export function setup() {
  console.log(`\n🚀 Starting MeepleAI Performance Test Suite`);
  console.log(`   Test Type: ${testType.toUpperCase()}`);
  console.log(`   API: ${config.apiBaseUrl}`);
  console.log(`   Game ID: ${config.testGameId}\n`);

  return setupTestUser(config.apiBaseUrl, config.testUser.email, config.testUser.password);
}

export function ragSearch(data) {
  group('RAG Search', () => {
    ragSearchTest(data);
  });
}

export function chat(data) {
  group('Chat Messaging', () => {
    chatTest(data);
  });
}

export function games(data) {
  group('Game Search', () => {
    gamesTest(data);
  });
}

export function sessions(data) {
  group('Session Management', () => {
    sessionsTest(data);
  });
}

export function teardown(data) {
  console.log('\n✅ Performance Test Suite Complete\n');
  teardownTestUser(config.apiBaseUrl, data.sessionToken);
}

export function handleSummary(data) {
  const timestamp = new Date().toISOString().replace(/:/g, '-').split('.')[0];

  return {
    [`reports/all-${testType}-${timestamp}.json`]: JSON.stringify(data, null, 2),
    'reports/summary.json': JSON.stringify(data, null, 2),
    stdout: textSummary(data, { indent: '  ', enableColors: true }),
  };
}

function textSummary(data, options) {
  const indent = options?.indent || '';

  let summary = `\n${indent}${'='.repeat(60)}\n`;
  summary += `${indent}  MeepleAI Performance Test Suite - ${testType.toUpperCase()}\n`;
  summary += `${indent}${'='.repeat(60)}\n\n`;

  const metrics = data.metrics;

  // Overall Stats
  summary += `${indent}📊 Overall Statistics\n`;
  summary += `${indent}${'-'.repeat(60)}\n`;
  summary += `${indent}  Total Requests: ${metrics.http_reqs?.values.count || 0}\n`;
  summary += `${indent}  Failed Requests: ${metrics.http_req_failed?.values.passes || 0} (${((metrics.http_req_failed?.values.rate || 0) * 100).toFixed(2)}%)\n`;
  summary += `${indent}  Requests/sec: ${(metrics.http_reqs?.values.rate || 0).toFixed(2)}\n`;
  summary += `${indent}  Data Received: ${formatBytes(metrics.data_received?.values.count || 0)}\n`;
  summary += `${indent}  Data Sent: ${formatBytes(metrics.data_sent?.values.count || 0)}\n\n`;

  // Response Times
  summary += `${indent}⏱️  Response Times\n`;
  summary += `${indent}${'-'.repeat(60)}\n`;
  summary += `${indent}  Average: ${(metrics.http_req_duration?.values.avg || 0).toFixed(2)}ms\n`;
  summary += `${indent}  P50: ${(metrics.http_req_duration?.values['p(50)'] || 0).toFixed(2)}ms\n`;
  summary += `${indent}  P95: ${(metrics.http_req_duration?.values['p(95)'] || 0).toFixed(2)}ms\n`;
  summary += `${indent}  P99: ${(metrics.http_req_duration?.values['p(99)'] || 0).toFixed(2)}ms\n`;
  summary += `${indent}  Max: ${(metrics.http_req_duration?.values.max || 0).toFixed(2)}ms\n\n`;

  // Endpoint-Specific Stats
  summary += `${indent}🎯 Endpoint Performance\n`;
  summary += `${indent}${'-'.repeat(60)}\n`;

  const endpoints = [
    { name: 'RAG Search', tag: 'rag-search', threshold: 2000 },
    { name: 'Chat', tag: 'chat', threshold: 1000 },
    { name: 'Games', tag: 'games', threshold: 500 },
    { name: 'Sessions', tag: 'sessions', threshold: 100 },
  ];

  for (const endpoint of endpoints) {
    const metricKey = `http_req_duration{endpoint:${endpoint.tag}}`;
    const endpointMetric = metrics[metricKey];

    if (endpointMetric) {
      const p95 = endpointMetric.values['p(95)'] || 0;
      const status = p95 < endpoint.threshold ? '✓' : '✗';
      summary += `${indent}  ${status} ${endpoint.name.padEnd(20)} P95: ${p95.toFixed(2)}ms (threshold: ${endpoint.threshold}ms)\n`;
    }
  }

  summary += `\n${indent}${'='.repeat(60)}\n\n`;

  return summary;
}

function formatBytes(bytes) {
  if (bytes < 1024) return bytes + ' B';
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB';
  if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
  return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
}
