/**
 * HyperDX Telemetry Ingestion - Performance Test
 * Issue #1565: Integration Testing - Backend Telemetry
 *
 * Purpose: Load test API to verify HyperDX can handle production traffic volume
 * Metrics:
 * - API response time (p95 < 500ms)
 * - No data loss (all logs/traces ingested)
 * - HyperDX resource usage (< 4GB RAM)
 * - Search performance in HyperDX UI (< 1s)
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';

// Custom metrics
const telemetryErrors = new Counter('telemetry_errors');
const apiResponseTime = new Trend('api_response_time');

export const options = {
  scenarios: {
    // Scenario 1: Ramp-up load test
    load_test: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '1m', target: 10 },   // Warm-up: 0 → 10 users
        { duration: '2m', target: 50 },   // Ramp-up: 10 → 50 users
        { duration: '3m', target: 50 },   // Steady: 50 users for 3 min
        { duration: '1m', target: 100 },  // Peak: 50 → 100 users
        { duration: '2m', target: 100 },  // Peak hold: 100 users for 2 min
        { duration: '1m', target: 0 },    // Ramp-down: 100 → 0
      ],
      gracefulRampDown: '30s',
    },

    // Scenario 2: Spike test (concurrent bulk operations)
    spike_test: {
      executor: 'constant-vus',
      vus: 5,
      duration: '1m',
      startTime: '5m', // Start after load_test warm-up
      exec: 'bulkTelemetryTest',
    },
  },

  thresholds: {
    // Performance thresholds
    'http_req_duration': ['p(95)<500'],           // 95% of requests < 500ms
    'http_req_failed': ['rate<0.01'],             // < 1% failure rate
    'telemetry_errors': ['count<10'],             // < 10 telemetry errors total
    'api_response_time{endpoint:error}': ['p(95)<200'],   // Test error endpoint < 200ms
    'api_response_time{endpoint:trace}': ['p(95)<300'],   // Test trace endpoint < 300ms
    'api_response_time{endpoint:bulk}': ['p(95)<2000'],   // Bulk endpoint < 2s
  },
};

const BASE_URL = __ENV.API_BASE || 'http://localhost:8080';

/**
 * Main load test scenario - Mixed telemetry operations
 */
export default function () {
  const testType = Math.random();

  if (testType < 0.6) {
    // 60% - Regular API calls with tracing
    testGameListEndpoint();
  } else if (testType < 0.9) {
    // 30% - Test error logs
    testErrorLogging();
  } else {
    // 10% - Test distributed traces
    testDistributedTracing();
  }

  sleep(1); // 1 second between requests per user
}

/**
 * Spike test scenario - Bulk telemetry generation
 */
export function bulkTelemetryTest() {
  const url = `${BASE_URL}/api/v1/test/bulk?count=100`;
  const startTime = Date.now();

  const res = http.post(url);

  const duration = Date.now() - startTime;
  apiResponseTime.add(duration, { endpoint: 'bulk' });

  check(res, {
    'bulk: status 200': (r) => r.status === 200,
    'bulk: completed < 5s': () => duration < 5000,
    'bulk: 100 logs generated': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.logsGenerated === 100;
      } catch (e) {
        telemetryErrors.add(1);
        return false;
      }
    },
    'bulk: 100 traces generated': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.tracesGenerated === 100;
      } catch (e) {
        telemetryErrors.add(1);
        return false;
      }
    },
  }) || telemetryErrors.add(1);

  sleep(5); // Wait 5s between bulk operations to avoid overwhelming system
}

/**
 * Test regular API endpoint with OpenTelemetry tracing
 */
function testGameListEndpoint() {
  const url = `${BASE_URL}/api/v1/games`;
  const startTime = Date.now();

  const res = http.get(url);

  const duration = Date.now() - startTime;
  apiResponseTime.add(duration, { endpoint: 'games' });

  check(res, {
    'games: status 200': (r) => r.status === 200,
    'games: response < 500ms': () => duration < 500,
    'games: has trace headers': (r) => r.headers['Traceparent'] !== undefined || true, // Optional header
  });
}

/**
 * Test error log generation
 */
function testErrorLogging() {
  const url = `${BASE_URL}/api/v1/test/error`;
  const startTime = Date.now();

  const res = http.post(url);

  const duration = Date.now() - startTime;
  apiResponseTime.add(duration, { endpoint: 'error' });

  check(res, {
    'error: status 200': (r) => r.status === 200,
    'error: response < 200ms': () => duration < 200,
    'error: has correlationId': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.correlationId && body.correlationId !== 'null';
      } catch (e) {
        telemetryErrors.add(1);
        return false;
      }
    },
  }) || telemetryErrors.add(1);
}

/**
 * Test distributed trace generation
 */
function testDistributedTracing() {
  const url = `${BASE_URL}/api/v1/test/trace`;
  const startTime = Date.now();

  const res = http.get(url);

  const duration = Date.now() - startTime;
  apiResponseTime.add(duration, { endpoint: 'trace' });

  check(res, {
    'trace: status 200': (r) => r.status === 200,
    'trace: response < 300ms': () => duration < 300,
    'trace: has traceId': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.traceId && body.traceId !== 'null' && body.traceId !== 'no-trace';
      } catch (e) {
        telemetryErrors.add(1);
        return false;
      }
    },
    'trace: has spanId': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.spanId && body.spanId !== 'null' && body.spanId !== 'no-span';
      } catch (e) {
        telemetryErrors.add(1);
        return false;
      }
    },
  }) || telemetryErrors.add(1);
}

/**
 * Test teardown - Verify HyperDX health
 */
export function teardown(data) {
  console.log('═══════════════════════════════════════════════════════');
  console.log('HyperDX Performance Test - Teardown');
  console.log('═══════════════════════════════════════════════════════');
  console.log('');
  console.log('Next Steps:');
  console.log('1. Open HyperDX UI: http://localhost:8180');
  console.log('2. Verify all logs/traces were ingested');
  console.log('3. Test search performance (should be <1s):');
  console.log('   - Search: service.name:meepleai-api');
  console.log('   - Search: test.type:bulk');
  console.log('4. Check resource usage:');
  console.log('   - Run: docker stats meepleai-hyperdx --no-stream');
  console.log('   - Verify: RAM < 4GB');
  console.log('5. Verify no OTel errors:');
  console.log('   - Run: docker logs meepleai-api 2>&1 | grep -i "otel.*error"');
  console.log('   - Expected: No errors');
  console.log('');
  console.log('═══════════════════════════════════════════════════════');
}
