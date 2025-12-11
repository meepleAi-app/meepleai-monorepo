/**
 * Infrastructure Monitoring Performance Test - Issue #902
 *
 * Load tests for admin infrastructure monitoring endpoints with:
 * - GET /api/v1/admin/infrastructure/details (main data)
 * - Prometheus metrics aggregation
 * - Service health checks
 * - Real-time polling simulation
 *
 * Test Scenarios:
 * - smoke: 5 VUs, 1 minute (quick validation)
 * - load: 100 VUs, 5 minutes (Issue #902 requirement)
 * - stress: 200 VUs, 10 minutes (capacity limits)
 * - spike: 10 → 500 VUs instant surge
 *
 * Performance Targets:
 * - P95 latency < 1000ms (infrastructure details)
 * - P99 latency < 2000ms
 * - Throughput > 100 req/s
 * - Error rate < 2%
 *
 * Usage:
 *   k6 run --env TEST_TYPE=load scenarios/infrastructure.js
 *   k6 run --env TEST_TYPE=smoke scenarios/infrastructure.js
 *   k6 run --env TEST_TYPE=stress scenarios/infrastructure.js
 */

import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import { loadConfig, getTestType } from '../utils/common.js';
import { setupTestUser, teardownTestUser, getAuthHeaders } from '../utils/auth.js';

const config = loadConfig();
const testType = getTestType();

// Custom metrics
const infrastructureLatency = new Trend('infrastructure_latency', true);
const serviceHealthChecks = new Counter('service_health_checks');
const prometheusQueries = new Counter('prometheus_queries');
const pollingRate = new Rate('polling_success_rate');

// Test configuration based on type
const testConfigs = {
  smoke: {
    vus: 5,
    duration: '1m',
    thresholds: {
      'http_req_duration{endpoint:infrastructure}': ['p(95)<2000', 'p(99)<3000'],
      'http_req_failed{endpoint:infrastructure}': ['rate<0.05'], // 5% for smoke
      'infrastructure_latency': ['avg<1500'],
      'polling_success_rate': ['rate>0.90'],
    },
  },
  load: {
    vus: 100,
    duration: '5m',
    thresholds: {
      'http_req_duration{endpoint:infrastructure}': ['p(95)<1000', 'p(99)<2000'],
      'http_req_failed{endpoint:infrastructure}': ['rate<0.02'], // 2% for load
      'infrastructure_latency': ['avg<800'],
      'polling_success_rate': ['rate>0.95'],
    },
  },
  stress: {
    vus: 200,
    duration: '10m',
    thresholds: {
      'http_req_duration{endpoint:infrastructure}': ['p(95)<1500', 'p(99)<3000'],
      'http_req_failed{endpoint:infrastructure}': ['rate<0.05'], // 5% acceptable under stress
      'infrastructure_latency': ['avg<1200'],
      'polling_success_rate': ['rate>0.85'],
    },
  },
  spike: {
    stages: [
      { duration: '30s', target: 10 },
      { duration: '10s', target: 500 }, // Instant spike
      { duration: '3m', target: 500 }, // Sustain spike
      { duration: '1m', target: 10 }, // Recovery
    ],
    thresholds: {
      'http_req_duration{endpoint:infrastructure}': ['p(95)<2000', 'p(99)<4000'],
      'http_req_failed{endpoint:infrastructure}': ['rate<0.10'], // 10% during spike acceptable
      'infrastructure_latency': ['avg<1500'],
      'polling_success_rate': ['rate>0.80'],
    },
  },
};

const currentConfig = testConfigs[testType] || testConfigs.load;

export const options = {
  vus: currentConfig.vus,
  duration: currentConfig.duration,
  stages: currentConfig.stages,
  thresholds: currentConfig.thresholds,
  tags: {
    test_name: 'infrastructure',
    test_type: testType,
  },
};

export function setup() {
  console.log(`\n🔍 Infrastructure Monitoring Load Test`);
  console.log(`   Test Type: ${testType.toUpperCase()}`);
  console.log(`   API: ${config.apiBaseUrl}`);
  console.log(`   VUs: ${currentConfig.vus || 'staged'}`);
  console.log(`   Duration: ${currentConfig.duration || 'staged'}\n`);

  return setupTestUser(config.apiBaseUrl, config.testUser.email, config.testUser.password);
}

export default function (data) {
  const headers = getAuthHeaders(data.sessionToken);

  group('Infrastructure Details Endpoint', () => {
    const startTime = Date.now();

    const response = http.get(`${config.apiBaseUrl}/api/v1/admin/infrastructure/details`, {
      headers,
      tags: { endpoint: 'infrastructure' },
    });

    const duration = Date.now() - startTime;
    infrastructureLatency.add(duration);

    // Validation checks
    const checkResult = check(response, {
      'status is 200': r => r.status === 200,
      'response has overall status': r => {
        const body = JSON.parse(r.body);
        return body.overall && body.overall.state !== undefined;
      },
      'response has services array': r => {
        const body = JSON.parse(r.body);
        return Array.isArray(body.services) && body.services.length > 0;
      },
      'response has prometheus metrics': r => {
        const body = JSON.parse(r.body);
        return body.prometheusMetrics && body.prometheusMetrics.apiRequestsLast24h !== undefined;
      },
      'overall health state is valid': r => {
        const body = JSON.parse(r.body);
        return ['Healthy', 'Degraded', 'Unhealthy'].includes(body.overall.state);
      },
      'services have required fields': r => {
        const body = JSON.parse(r.body);
        const firstService = body.services[0];
        return (
          firstService.serviceName &&
          firstService.state &&
          firstService.responseTime &&
          firstService.checkedAt
        );
      },
      'prometheus metrics complete': r => {
        const body = JSON.parse(r.body);
        const metrics = body.prometheusMetrics;
        return (
          metrics.apiRequestsLast24h >= 0 &&
          metrics.avgLatencyMs >= 0 &&
          metrics.errorRate >= 0 &&
          metrics.errorRate <= 1 &&
          metrics.llmCostLast24h >= 0
        );
      },
      'response time < 1s': () => duration < 1000,
      'response time < 2s': () => duration < 2000,
    });

    // Track polling success
    pollingRate.add(checkResult);

    // Count service health checks
    if (response.status === 200) {
      try {
        const body = JSON.parse(response.body);
        serviceHealthChecks.add(body.services.length);
      } catch (e) {
        console.error('Failed to parse response body');
      }
    }
  });

  // Simulate real-world polling behavior (30s intervals)
  if (testType === 'load' || testType === 'smoke') {
    sleep(30); // Wait 30s between polls (realistic user behavior)
  } else if (testType === 'stress') {
    sleep(10); // More aggressive polling under stress
  } else if (testType === 'spike') {
    sleep(5); // Rapid polling during spike
  }
}

export function teardown(data) {
  console.log('\n✅ Infrastructure Load Test Complete\n');
  console.log(`   Total Health Checks: ${serviceHealthChecks.value}`);
  console.log(`   Average Latency: ${infrastructureLatency.avg.toFixed(2)}ms`);
  console.log(`   P95 Latency: ${infrastructureLatency.p95.toFixed(2)}ms\n`);

  teardownTestUser(config.apiBaseUrl, data.sessionToken);
}

export function handleSummary(data) {
  const timestamp = new Date().toISOString().replace(/:/g, '-').split('.')[0];

  return {
    [`reports/infrastructure-${testType}-${timestamp}.json`]: JSON.stringify(data, null, 2),
    stdout: textSummary(data, { indent: '  ', enableColors: true }),
  };
}

function textSummary(data, options) {
  const indent = options?.indent || '';

  let summary = `\n${indent}${'='.repeat(60)}\n`;
  summary += `${indent}  Infrastructure Monitoring Load Test - ${testType.toUpperCase()}\n`;
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

  // Infrastructure-Specific Metrics
  summary += `${indent}🏗️  Infrastructure Metrics\n`;
  summary += `${indent}${'-'.repeat(60)}\n`;
  summary += `${indent}  Avg Infrastructure Latency: ${(metrics.infrastructure_latency?.values.avg || 0).toFixed(2)}ms\n`;
  summary += `${indent}  P95 Infrastructure Latency: ${(metrics.infrastructure_latency?.values['p(95)'] || 0).toFixed(2)}ms\n`;
  summary += `${indent}  Total Service Health Checks: ${metrics.service_health_checks?.values.count || 0}\n`;
  summary += `${indent}  Polling Success Rate: ${((metrics.polling_success_rate?.values.rate || 0) * 100).toFixed(2)}%\n\n`;

  // Performance Targets
  const targets = currentConfig.thresholds['http_req_duration{endpoint:infrastructure}'];
  const p95Target = parseInt(targets[0].match(/\d+/)?.[0] || '1000');
  const p99Target = parseInt(targets[1].match(/\d+/)?.[0] || '2000');
  const actualP95 = metrics.http_req_duration?.values['p(95)'] || 0;
  const actualP99 = metrics.http_req_duration?.values['p(99)'] || 0;

  summary += `${indent}🎯 Performance Targets\n`;
  summary += `${indent}${'-'.repeat(60)}\n`;
  summary += `${indent}  P95 Target: ${p95Target}ms | Actual: ${actualP95.toFixed(2)}ms ${actualP95 < p95Target ? '✓' : '✗'}\n`;
  summary += `${indent}  P99 Target: ${p99Target}ms | Actual: ${actualP99.toFixed(2)}ms ${actualP99 < p99Target ? '✓' : '✗'}\n`;
  summary += `${indent}  Error Rate: ${((metrics.http_req_failed?.values.rate || 0) * 100).toFixed(2)}% ${metrics.http_req_failed?.values.rate < 0.02 ? '✓' : '✗'}\n\n`;

  summary += `${indent}${'='.repeat(60)}\n\n`;

  return summary;
}

function formatBytes(bytes) {
  if (bytes < 1024) return bytes + ' B';
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB';
  if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
  return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
}
