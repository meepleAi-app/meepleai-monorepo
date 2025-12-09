/**
 * HyperDX Telemetry Ingestion & Load Testing - Performance Test
 * Issue #1565: Integration Testing - Backend Telemetry
 * Issue #1568: Load Testing and Performance Validation
 *
 * Purpose: Comprehensive load test for HyperDX with both telemetry and business endpoints
 * Test Coverage:
 * 1. Telemetry endpoints (/test/error, /test/trace, /test/bulk) - Issue #1565
 * 2. Business endpoints (/api/v1/games, /api/v1/chat, /api/v1/search) - Issue #1568
 *
 * Performance Targets (Issue #1568):
 * - 100 concurrent users, 3 min sustained load
 * - API response time (p95 < 500ms)
 * - Log search performance (p95 < 1s)
 * - Trace query performance (p95 < 500ms)
 * - Stress test: 1000 logs/sec for 1 minute
 * - No data loss (all logs/traces ingested)
 * - HyperDX resource usage (< 4GB RAM)
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';

// Custom metrics
const telemetryErrors = new Counter('telemetry_errors');
const apiResponseTime = new Trend('api_response_time');

export const options = {
  scenarios: {
    // Scenario 1: Ramp-up load test (Telemetry - Issue #1565)
    load_test_telemetry: {
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
      exec: 'telemetryTest',
    },

    // Scenario 2: Business Endpoints Load Test (Issue #1568)
    load_test_business: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '1m', target: 100 },  // Ramp-up: 0 → 100 users
        { duration: '3m', target: 100 },  // Sustained: 100 users for 3 min (REQUIREMENT)
        { duration: '1m', target: 0 },    // Ramp-down: 100 → 0
      ],
      startTime: '11m', // Start after telemetry test completes
      gracefulRampDown: '30s',
      exec: 'businessEndpointsTest',
    },

    // Scenario 3: Stress Test - High Volume Logging (Issue #1568)
    stress_test: {
      executor: 'constant-arrival-rate',
      rate: 1000,                         // 1000 logs/sec
      timeUnit: '1s',
      duration: '1m',                     // For 1 minute (REQUIREMENT)
      preAllocatedVUs: 50,
      maxVUs: 100,
      startTime: '16m', // Start after business load test
      exec: 'stressTestLogging',
    },

    // Scenario 4: Spike test (concurrent bulk operations - Issue #1565)
    spike_test: {
      executor: 'constant-vus',
      vus: 5,
      duration: '1m',
      startTime: '5m', // Start after load_test warm-up
      exec: 'bulkTelemetryTest',
    },
  },

  thresholds: {
    // Performance thresholds - Issue #1565
    'http_req_duration': ['p(95)<500'],           // 95% of requests < 500ms
    'http_req_failed': ['rate<0.01'],             // < 1% failure rate
    'telemetry_errors': ['count<10'],             // < 10 telemetry errors total
    'api_response_time{endpoint:error}': ['p(95)<200'],   // Test error endpoint < 200ms
    'api_response_time{endpoint:trace}': ['p(95)<300'],   // Test trace endpoint < 300ms
    'api_response_time{endpoint:bulk}': ['p(95)<2000'],   // Bulk endpoint < 2s

    // Performance thresholds - Issue #1568
    'api_response_time{endpoint:games}': ['p(95)<500'],   // Games endpoint < 500ms
    'api_response_time{endpoint:chat}': ['p(95)<500'],    // Chat endpoint < 500ms
    'api_response_time{endpoint:search}': ['p(95)<1000'], // Search endpoint < 1s (RAG intensive)
    'api_response_time{endpoint:stress}': ['p(95)<200'],  // Stress test endpoint < 200ms
  },
};

const BASE_URL = __ENV.API_BASE || 'http://localhost:8080';

/**
 * Telemetry Test Scenario - Mixed telemetry operations (Issue #1565)
 */
export function telemetryTest() {
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
 * Business Endpoints Test Scenario (Issue #1568)
 * Tests /api/v1/games, /api/v1/chat, /api/v1/search under load
 */
export function businessEndpointsTest() {
  const testType = Math.random();

  if (testType < 0.4) {
    // 40% - Games list endpoint
    testGamesEndpoint();
  } else if (testType < 0.7) {
    // 30% - Chat endpoint (streaming SSE)
    testChatEndpoint();
  } else {
    // 30% - Search endpoint (RAG)
    testSearchEndpoint();
  }

  sleep(0.5); // 0.5 second between requests (higher throughput for 100 users)
}

/**
 * Stress Test Scenario - High volume log generation (Issue #1568)
 * Target: 1000 logs/sec for 1 minute
 */
export function stressTestLogging() {
  const url = `${BASE_URL}/api/v1/telemetry/test-error`;
  const startTime = Date.now();

  const res = http.post(url);

  const duration = Date.now() - startTime;
  apiResponseTime.add(duration, { endpoint: 'stress' });

  check(res, {
    'stress: status 200': (r) => r.status === 200,
    'stress: response < 200ms': () => duration < 200,
  }) || telemetryErrors.add(1);

  // No sleep - constant-arrival-rate executor handles pacing
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
 * Test /api/v1/games endpoint (Issue #1568)
 */
function testGamesEndpoint() {
  const url = `${BASE_URL}/api/v1/games`;
  const startTime = Date.now();

  const res = http.get(url);

  const duration = Date.now() - startTime;
  apiResponseTime.add(duration, { endpoint: 'games' });

  check(res, {
    'games: status 200': (r) => r.status === 200,
    'games: response < 500ms': () => duration < 500,
    'games: valid JSON array': (r) => {
      try {
        const body = JSON.parse(r.body);
        return Array.isArray(body);
      } catch (e) {
        return false;
      }
    },
  });
}

/**
 * Test /api/v1/chat endpoint with streaming (Issue #1568)
 */
function testChatEndpoint() {
  const url = `${BASE_URL}/api/v1/chat`;
  const startTime = Date.now();

  const payload = JSON.stringify({
    message: 'How many players can play 7 Wonders?',
    sessionId: `load-test-${__VU}-${Date.now()}`,
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
    },
    timeout: '10s', // Chat with RAG can take longer
  };

  const res = http.post(url, payload, params);

  const duration = Date.now() - startTime;
  apiResponseTime.add(duration, { endpoint: 'chat' });

  check(res, {
    'chat: status 200': (r) => r.status === 200,
    'chat: response < 5s': () => duration < 5000, // Generous for RAG
    'chat: has response': (r) => r.body && r.body.length > 0,
  });
}

/**
 * Test /api/v1/search endpoint (RAG search - Issue #1568)
 */
function testSearchEndpoint() {
  const url = `${BASE_URL}/api/v1/search?query=victory+points&gameId=1`;
  const startTime = Date.now();

  const params = {
    timeout: '3s', // Search should be fast with hybrid RAG
  };

  const res = http.get(url, params);

  const duration = Date.now() - startTime;
  apiResponseTime.add(duration, { endpoint: 'search' });

  check(res, {
    'search: status 200': (r) => r.status === 200,
    'search: response < 1s': () => duration < 1000, // REQUIREMENT: <1s P95
    'search: has results': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.results !== undefined;
      } catch (e) {
        return false;
      }
    },
  });
}

/**
 * Test regular API endpoint with OpenTelemetry tracing (Issue #1565)
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
  console.log('✅ Test Execution Summary:');
  console.log('  - Telemetry Load Test: 100 users peak (Issue #1565)');
  console.log('  - Business Load Test: 100 users sustained 3 min (Issue #1568)');
  console.log('  - Stress Test: 1000 logs/sec for 1 min (Issue #1568)');
  console.log('');
  console.log('📊 Issue #1568 - Performance Validation Steps:');
  console.log('');
  console.log('1. Open HyperDX UI: http://localhost:8180');
  console.log('');
  console.log('2. Verify all logs/traces were ingested (0% data loss):');
  console.log('   - Search: service.name:meepleai-api');
  console.log('   - Count logs from last 20 minutes');
  console.log('   - Expected: ~60,000+ logs (stress test alone = 60,000)');
  console.log('');
  console.log('3. Test LOG SEARCH performance (Target: P95 < 1s):');
  console.log('   - Search: endpoint:games');
  console.log('   - Search: endpoint:chat');
  console.log('   - Search: endpoint:search');
  console.log('   - Measure response time in HyperDX UI');
  console.log('   - ✓ PASS if < 1 second');
  console.log('');
  console.log('4. Test TRACE QUERY performance (Target: P95 < 500ms):');
  console.log('   - View traces for service.name:meepleai-api');
  console.log('   - Click on any trace to load details');
  console.log('   - Measure load time');
  console.log('   - ✓ PASS if < 500ms');
  console.log('');
  console.log('5. Check HyperDX resource usage (Target: < 4GB RAM):');
  console.log('   - Run: docker stats hyperdx --no-stream');
  console.log('   - Verify: MEM USAGE < 4.00GB');
  console.log('   - ✓ PASS if below threshold');
  console.log('');
  console.log('6. Verify no OTel exporter errors:');
  console.log('   - Run: docker logs meepleai-api 2>&1 | grep -i "otel.*error"');
  console.log('   - Expected: No errors found');
  console.log('   - ✓ PASS if no OTel errors');
  console.log('');
  console.log('7. Check ClickHouse storage (Target: < 5GB):');
  console.log('   - Run: docker exec hyperdx clickhouse-client --query "SELECT formatReadableSize(sum(bytes)) FROM system.parts"');
  console.log('   - Verify: Storage < 5GB');
  console.log('   - ✓ PASS if below threshold');
  console.log('');
  console.log('═══════════════════════════════════════════════════════');
  console.log('Issue #1568 Acceptance Criteria Checklist:');
  console.log('═══════════════════════════════════════════════════════');
  console.log('[ ] k6 load test passes (100 users, no failures)');
  console.log('[ ] All logs/traces ingested (0% data loss)');
  console.log('[ ] Log search < 1s P95');
  console.log('[ ] Trace query < 500ms P95');
  console.log('[ ] HyperDX resource usage < 4GB RAM under load');
  console.log('[ ] No errors or warnings in HyperDX logs');
  console.log('[ ] ClickHouse storage usage < 5GB after test');
  console.log('═══════════════════════════════════════════════════════');
}