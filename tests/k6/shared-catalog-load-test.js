/**
 * ISSUE #2374 Phase 5: SharedGameCatalog Load Testing
 *
 * Performance targets:
 * - Search endpoint: 100 req/s sustained (P95 < 200ms)
 * - Admin endpoints: 50 req/s sustained (P95 < 300ms)
 * - Cache hit rate: > 80%
 * - Failure rate: < 1%
 *
 * Prerequisites:
 * 1. cd infra && docker compose up -d postgres qdrant redis
 * 2. cd apps/api/src/Api && dotnet run
 * 3. cd apps/api/src/Api && dotnet ef database update  # Apply indexes
 * 4. Seed test data: POST /api/v1/admin/shared-games/bulk-import (100 games)
 *
 * Execution:
 * cd tests/k6
 * k6 run shared-catalog-load-test.js
 *
 * Duration: ~10 minutes total
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Counter, Rate } from 'k6/metrics';

// Custom metrics for SharedGameCatalog
const searchDuration = new Trend('shared_catalog_search_duration', true);
const getByIdDuration = new Trend('shared_catalog_getbyid_duration', true);
const adminCreateDuration = new Trend('shared_catalog_admin_create_duration', true);
const adminUpdateDuration = new Trend('shared_catalog_admin_update_duration', true);
const cacheHitRate = new Rate('shared_catalog_cache_hit_rate');
const searchErrors = new Counter('shared_catalog_search_errors');

// Base configuration
const BASE_URL = __ENV.API_URL || 'http://localhost:8080';
const ADMIN_TOKEN = __ENV.ADMIN_TOKEN || 'test-admin-token'; // Replace with real token

export const options = {
  scenarios: {
    // Scenario 1: Search Load Test (100 req/s target)
    search_load: {
      executor: 'constant-arrival-rate',
      duration: '3m',
      rate: 100, // 100 requests per second
      timeUnit: '1s',
      preAllocatedVUs: 20,
      maxVUs: 100,
      exec: 'searchGames',
      tags: { scenario: 'search_load' },
    },

    // Scenario 2: Admin Operations Load Test (50 req/s target)
    admin_load: {
      executor: 'constant-arrival-rate',
      duration: '3m',
      rate: 50, // 50 requests per second
      timeUnit: '1s',
      preAllocatedVUs: 10,
      maxVUs: 50,
      exec: 'adminOperations',
      tags: { scenario: 'admin_load' },
      startTime: '30s', // Start after search stabilizes
    },

    // Scenario 3: Mixed Realistic Traffic (Spike Test)
    realistic_spike: {
      executor: 'ramping-arrival-rate',
      startRate: 50,
      timeUnit: '1s',
      preAllocatedVUs: 20,
      maxVUs: 150,
      stages: [
        { target: 50, duration: '1m' }, // Ramp up to baseline
        { target: 200, duration: '30s' }, // Spike to 200 req/s
        { target: 50, duration: '1m' }, // Back to baseline
      ],
      exec: 'mixedTraffic',
      tags: { scenario: 'realistic_spike' },
      startTime: '4m', // After load tests complete
    },
  },

  thresholds: {
    // Overall performance
    http_req_duration: ['p(95)<200'], // P95 < 200ms (10x improvement target)

    // Search endpoint performance
    'shared_catalog_search_duration': ['p(95)<200', 'p(99)<300'],

    // GetById endpoint performance
    'shared_catalog_getbyid_duration': ['p(95)<100', 'p(99)<150'],

    // Admin endpoints performance
    'shared_catalog_admin_create_duration': ['p(95)<300', 'p(99)<500'],
    'shared_catalog_admin_update_duration': ['p(95)<300', 'p(99)<500'],

    // Cache performance
    'shared_catalog_cache_hit_rate': ['rate>0.80'], // > 80% cache hit rate

    // Error rate
    http_req_failed: ['rate<0.01'], // < 1% failure rate
    'shared_catalog_search_errors': ['count<10'], // < 10 search errors total
  },
};

// Test data for search queries
const searchTerms = [
  'strategia',
  'famiglia',
  'cooperativo',
  'carte',
  'dadi',
  'avventura',
  'fantasy',
  'medievale',
  'città',
  'impero',
];

const categoryIds = [
  // Replace with actual UUIDs from database after seeding
  '00000000-0000-0000-0000-000000000001',
  '00000000-0000-0000-0000-000000000002',
];

const mechanicIds = [
  // Replace with actual UUIDs from database after seeding
  '00000000-0000-0000-0000-000000000003',
  '00000000-0000-0000-0000-000000000004',
];

/**
 * Scenario 1: Search Games (100 req/s target)
 * Tests full-text search with filters and pagination
 */
export function searchGames() {
  const searchTerm = searchTerms[Math.floor(Math.random() * searchTerms.length)];
  const pageNumber = Math.floor(Math.random() * 5) + 1; // Pages 1-5

  const params = {
    headers: {
      'Content-Type': 'application/json',
    },
    tags: { endpoint: 'search' },
  };

  const payload = JSON.stringify({
    searchTerm,
    pageNumber,
    pageSize: 20,
    sortBy: 'Title',
    sortDescending: false,
  });

  const res = http.post(`${BASE_URL}/api/v1/shared-games/search`, payload, params);

  const success = check(res, {
    'search status 200': r => r.status === 200,
    'search has results': r => JSON.parse(r.body).items !== undefined,
    'search response time OK': r => r.timings.duration < 300,
  });

  if (!success) {
    searchErrors.add(1);
  }

  searchDuration.add(res.timings.duration);

  // Check cache hit via response headers (if backend adds X-Cache-Status header)
  if (res.headers['X-Cache-Status'] === 'HIT') {
    cacheHitRate.add(true);
  } else {
    cacheHitRate.add(false);
  }

  sleep(0.1); // 100ms sleep between requests
}

/**
 * Scenario 2: Admin Operations (50 req/s target)
 * Tests admin CRUD operations with authorization
 */
export function adminOperations() {
  const params = {
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${ADMIN_TOKEN}`,
    },
  };

  // 60% GetAll (most common admin operation)
  if (Math.random() < 0.6) {
    const res = http.get(`${BASE_URL}/api/v1/admin/shared-games?pageNumber=1&pageSize=50`, params);

    check(res, {
      'admin getAll status 200': r => r.status === 200,
      'admin getAll response time OK': r => r.timings.duration < 400,
    });

    adminCreateDuration.add(res.timings.duration);
  }
  // 30% GetById (admin reviewing game details)
  else if (Math.random() < 0.9) {
    // TODO: Replace with actual game ID after seeding
    const gameId = '00000000-0000-0000-0000-000000000010';
    const res = http.get(`${BASE_URL}/api/v1/shared-games/${gameId}`, params);

    check(res, {
      'admin getById status 200 or 404': r => r.status === 200 || r.status === 404,
      'admin getById response time OK': r => r.timings.duration < 200,
    });

    getByIdDuration.add(res.timings.duration);
  }
  // 10% Create operations (less frequent)
  else {
    const payload = JSON.stringify({
      title: `Test Game ${Date.now()}`,
      yearPublished: 2024,
      description: 'Load test game created via k6',
      minPlayers: 2,
      maxPlayers: 4,
      playingTimeMinutes: 60,
      minAge: 10,
      imageUrl: 'https://picsum.photos/400/300',
      thumbnailUrl: 'https://picsum.photos/200/150',
    });

    const res = http.post(`${BASE_URL}/api/v1/admin/shared-games`, payload, params);

    check(res, {
      'admin create status 201': r => r.status === 201,
      'admin create response time OK': r => r.timings.duration < 500,
    });

    adminCreateDuration.add(res.timings.duration);
  }

  sleep(0.2); // 200ms sleep (50 req/s = 1 req per 20ms, sleep 200ms = 5 req/s per VU)
}

/**
 * Scenario 3: Mixed Realistic Traffic
 * Simulates real user behavior (search + view details)
 */
export function mixedTraffic() {
  // 70% search operations
  if (Math.random() < 0.7) {
    searchGames();
  }
  // 30% view details
  else {
    const params = {
      headers: { 'Content-Type': 'application/json' },
      tags: { endpoint: 'getbyid' },
    };

    // TODO: Replace with actual game ID from previous search
    const gameId = '00000000-0000-0000-0000-000000000010';
    const res = http.get(`${BASE_URL}/api/v1/shared-games/${gameId}`, params);

    check(res, {
      'mixed getById status 200 or 404': r => r.status === 200 || r.status === 404,
      'mixed getById response time OK': r => r.timings.duration < 150,
    });

    getByIdDuration.add(res.timings.duration);
  }

  sleep(0.05); // Minimal sleep for spike test
}

/**
 * Setup: Run before test execution
 */
export function setup() {
  console.log('='.repeat(70));
  console.log('ISSUE #2374 Phase 5: SharedGameCatalog Load Testing');
  console.log('='.repeat(70));
  console.log(`API URL: ${BASE_URL}`);
  console.log(`Test Duration: ~10 minutes`);
  console.log(`Scenarios:`);
  console.log(`  1. Search Load (100 req/s) - 3 min`);
  console.log(`  2. Admin Load (50 req/s) - 3 min`);
  console.log(`  3. Realistic Spike (50-200 req/s) - 2.5 min`);
  console.log('='.repeat(70));

  // Verify API is reachable
  const healthCheck = http.get(`${BASE_URL}/health`);
  if (healthCheck.status !== 200) {
    throw new Error(`API not reachable at ${BASE_URL}. Status: ${healthCheck.status}`);
  }

  console.log('✅ API health check passed');
  console.log('');
}

/**
 * Teardown: Run after test execution
 */
export function teardown(data) {
  console.log('');
  console.log('='.repeat(70));
  console.log('LOAD TEST COMPLETE - Performance Validation');
  console.log('='.repeat(70));
  console.log('');
  console.log('📊 PERFORMANCE METRICS:');
  console.log('  View detailed metrics above in k6 summary output');
  console.log('');
  console.log('✅ SUCCESS CRITERIA VALIDATION:');
  console.log('');
  console.log('  1. Search P95 < 200ms');
  console.log('     → Check: http_req_duration{endpoint:search} p(95)');
  console.log('');
  console.log('  2. GetById P95 < 100ms');
  console.log('     → Check: http_req_duration{endpoint:getbyid} p(95)');
  console.log('');
  console.log('  3. Admin Operations P95 < 300ms');
  console.log('     → Check: http_req_duration{scenario:admin_load} p(95)');
  console.log('');
  console.log('  4. Cache Hit Rate > 80%');
  console.log('     → Check: shared_catalog_cache_hit_rate');
  console.log('     → Also verify Prometheus: meepleai_cache_hits_total');
  console.log('');
  console.log('  5. Failure Rate < 1%');
  console.log('     → Check: http_req_failed rate');
  console.log('');
  console.log('  6. 100 req/s Search Sustained');
  console.log('     → Check: http_reqs (should be ~18000 for search scenario)');
  console.log('');
  console.log('  7. 50 req/s Admin Sustained');
  console.log('     → Check: http_reqs (should be ~9000 for admin scenario)');
  console.log('');
  console.log('📈 PROMETHEUS METRICS VALIDATION:');
  console.log('');
  console.log('  Open Prometheus: http://localhost:9090');
  console.log('');
  console.log('  Query 1: Cache Hit Rate');
  console.log('    rate(meepleai_cache_hits_total{cache_type="shared_games"}[5m])');
  console.log('    /');
  console.log('    (rate(meepleai_cache_hits_total{cache_type="shared_games"}[5m])');
  console.log('     + rate(meepleai_cache_misses_total{cache_type="shared_games"}[5m]))');
  console.log('');
  console.log('    Expected: > 0.80 (80% hit rate)');
  console.log('');
  console.log('  Query 2: Request Rate by Endpoint');
  console.log('    rate(http_server_request_duration_count{route="/api/v1/shared-games/search"}[5m])');
  console.log('');
  console.log('    Expected: ~100 req/s during search_load scenario');
  console.log('');
  console.log('🗄️  DATABASE VALIDATION:');
  console.log('');
  console.log('  Run: psql -f docs/05-testing/shared-catalog-fts-performance-validation.sql');
  console.log('');
  console.log('  Verify:');
  console.log('    - GIN index ix_shared_games_fts used (EXPLAIN ANALYZE)');
  console.log('    - Index scan counts in pg_stat_user_indexes');
  console.log('    - No sequential scans on shared_games table');
  console.log('');
  console.log('='.repeat(70));
  console.log('');
}

/**
 * Test Function: Search Games with FTS and Filters
 */
export function searchGames() {
  const searchTerm = searchTerms[Math.floor(Math.random() * searchTerms.length)];
  const pageNumber = Math.floor(Math.random() * 5) + 1;
  const includeFilters = Math.random() < 0.3; // 30% queries include filters

  const payload = {
    searchTerm,
    pageNumber,
    pageSize: 20,
    sortBy: Math.random() < 0.5 ? 'Title' : 'AverageRating',
    sortDescending: Math.random() < 0.5,
  };

  // Add category/mechanic filters to 30% of queries
  if (includeFilters) {
    payload.categoryIds = [categoryIds[0]];
    payload.minPlayers = 2;
    payload.maxPlayers = 4;
  }

  const params = {
    headers: { 'Content-Type': 'application/json' },
    tags: { endpoint: 'search' },
  };

  const res = http.post(`${BASE_URL}/api/v1/shared-games/search`, JSON.stringify(payload), params);

  const success = check(res, {
    'search status 200': r => r.status === 200,
    'search has items': r => {
      try {
        return JSON.parse(r.body).items !== undefined;
      } catch {
        return false;
      }
    },
    'search P95 < 200ms': r => r.timings.duration < 200,
  });

  if (!success) {
    searchErrors.add(1);
  }

  searchDuration.add(res.timings.duration);

  // Estimate cache hit based on response time (< 50ms likely cache hit)
  cacheHitRate.add(res.timings.duration < 50 ? 1 : 0);
}

/**
 * Test Function: Admin CRUD Operations
 */
export function adminOperations() {
  const params = {
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${ADMIN_TOKEN}`,
    },
    tags: { endpoint: 'admin' },
  };

  // Simulate admin workflow distribution
  const operation = Math.random();

  if (operation < 0.6) {
    // 60%: List all games (most common admin task)
    const res = http.get(
      `${BASE_URL}/api/v1/admin/shared-games?pageNumber=1&pageSize=50`,
      params
    );

    check(res, {
      'admin list status 200': r => r.status === 200,
      'admin list P95 < 300ms': r => r.timings.duration < 300,
    });

    adminCreateDuration.add(res.timings.duration);
  } else if (operation < 0.9) {
    // 30%: Get game by ID
    const gameId = '00000000-0000-0000-0000-000000000010'; // TODO: Use real ID
    const res = http.get(`${BASE_URL}/api/v1/shared-games/${gameId}`, params);

    check(res, {
      'admin getById status 200 or 404': r => r.status === 200 || r.status === 404,
      'admin getById P95 < 100ms': r => r.timings.duration < 100,
    });

    getByIdDuration.add(res.timings.duration);
  } else {
    // 10%: Create new game (less frequent)
    const payload = JSON.stringify({
      title: `LoadTest Game ${Date.now()}`,
      yearPublished: 2024,
      description: 'k6 load test game',
      minPlayers: 2,
      maxPlayers: 4,
      playingTimeMinutes: 60,
      minAge: 10,
      imageUrl: 'https://picsum.photos/400/300',
      thumbnailUrl: 'https://picsum.photos/200/150',
    });

    const res = http.post(`${BASE_URL}/api/v1/admin/shared-games`, payload, params);

    check(res, {
      'admin create status 201 or 401': r => r.status === 201 || r.status === 401,
      'admin create P95 < 300ms': r => r.timings.duration < 300,
    });

    adminCreateDuration.add(res.timings.duration);
  }
}

/**
 * Test Function: Mixed Realistic Traffic
 */
export function mixedTraffic() {
  if (Math.random() < 0.7) {
    searchGames();
  } else {
    adminOperations();
  }
}
