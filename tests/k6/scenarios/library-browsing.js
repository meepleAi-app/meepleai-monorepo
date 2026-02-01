/**
 * ISSUE-2928: Library Browsing Load Testing - Pagination & Filtering
 *
 * Scenario 2: User library browsing with pagination, filtering, and detail views.
 * Simulates authenticated users managing their game library.
 *
 * Endpoints tested:
 * - GET /api/v1/library (paginated list)
 * - GET /api/v1/library/stats
 * - GET /api/v1/library/quota
 * - GET /api/v1/library/games/{id}/status
 * - GET /api/v1/library/games/{id} (detail view)
 *
 * Performance Targets:
 * - Response time p95 < 500ms
 * - Response time p99 < 1s
 * - Error rate < 1%
 */

import { sleep, check } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';
import {
  config,
  authenticateUser,
  authGet,
  validateResponse,
  validatePaginatedResponse,
  standardThresholds,
  scenarioConfigs,
  standardSetup,
  standardTeardown,
  randomPage,
  randomPageSize,
} from '../utils/shared-config.js';

// ============================================
// CUSTOM METRICS
// ============================================

const libraryLatency = new Trend('library_browsing_latency');
const paginationLatency = new Trend('pagination_latency');
const detailViewLatency = new Trend('detail_view_latency');
const libraryErrors = new Counter('library_errors');
const paginationErrors = new Counter('pagination_errors');

// ============================================
// TEST CONFIGURATION
// ============================================

const TEST_TYPE = __ENV.TEST_TYPE || 'smoke';

export const options = {
  scenarios: {
    // Smoke test: Quick validation
    smoke: {
      ...scenarioConfigs.smoke,
      tags: { test_type: 'smoke', scenario: 'library-browsing' },
      exec: 'libraryBrowsingScenario',
    },

    // Load test: Realistic library browsing (50 VUs, 5 minutes)
    load: {
      ...scenarioConfigs.load,
      tags: { test_type: 'load', scenario: 'library-browsing' },
      exec: 'libraryBrowsingScenario',
    },

    // Stress test: Peak load
    stress: {
      ...scenarioConfigs.stress,
      tags: { test_type: 'stress', scenario: 'library-browsing' },
      exec: 'libraryBrowsingScenario',
    },
  },

  // Performance thresholds
  thresholds: {
    ...standardThresholds,
    'library_browsing_latency': ['p(95)<500', 'p(99)<1000'],
    'pagination_latency': ['p(95)<500', 'p(99)<1000'],
    'detail_view_latency': ['p(95)<750', 'p(99)<1500'],
    'library_errors': ['count<10'],
    'pagination_errors': ['count<20'],
  },

  // HTTP configuration
  httpDebug: __ENV.HTTP_DEBUG === 'true' ? 'full' : '',
  insecureSkipTLSVerify: true,
  noConnectionReuse: false,

  // Output
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

// ============================================
// FILTER CONFIGURATIONS
// ============================================

const stateFilters = ['Nuovo', 'InPrestito', 'Wishlist', 'Owned'];
const sortOptions = ['addedAt', 'title'];

/**
 * Generate random filter combination for library browsing.
 */
function getRandomFilters() {
  const filters = {
    page: randomPage(5),
    pageSize: randomPageSize(),
    sortDescending: Math.random() > 0.5,
  };

  // 40% chance to filter by favorites
  if (Math.random() < 0.4) {
    filters.favoritesOnly = true;
  }

  // 30% chance to filter by state
  if (Math.random() < 0.3) {
    filters.stateFilter = stateFilters[Math.floor(Math.random() * stateFilters.length)];
  }

  // 50% chance to change sort order
  if (Math.random() < 0.5) {
    filters.sortBy = sortOptions[Math.floor(Math.random() * sortOptions.length)];
  }

  return filters;
}

// ============================================
// BROWSING PATTERNS
// ============================================

/**
 * Pattern 1: Quick browse - scan library pages
 * User quickly browses through pages without viewing details.
 */
function quickBrowsePattern(token) {
  const startTime = Date.now();

  // Initial library load
  const initialResponse = authGet(token, '/api/v1/library', { page: 1, pageSize: 20 });
  validatePaginatedResponse(initialResponse, 'initial-library');
  libraryLatency.add(Date.now() - startTime);

  // Browse through 3-5 pages
  const pageCount = Math.floor(Math.random() * 3) + 3;
  for (let i = 2; i <= pageCount; i++) {
    sleep(1); // Simulate user reading time

    const pageStart = Date.now();
    const response = authGet(token, '/api/v1/library', { page: i, pageSize: 20 });

    if (!validatePaginatedResponse(response, `page-${i}`)) {
      paginationErrors.add(1);
    }
    paginationLatency.add(Date.now() - pageStart);
  }
}

/**
 * Pattern 2: Filtered browse - apply filters and browse
 * User applies filters and browses filtered results.
 */
function filteredBrowsePattern(token) {
  // Get library stats first
  const statsResponse = authGet(token, '/api/v1/library/stats');
  validateResponse(statsResponse, 'library-stats');

  // Apply random filters
  const filters = getRandomFilters();
  const startTime = Date.now();
  const response = authGet(token, '/api/v1/library', filters);

  if (!validatePaginatedResponse(response, 'filtered-library')) {
    libraryErrors.add(1);
  }
  libraryLatency.add(Date.now() - startTime);

  // Try different filter combinations
  for (let i = 0; i < 3; i++) {
    sleep(1);

    const newFilters = getRandomFilters();
    const filterStart = Date.now();
    const filterResponse = authGet(token, '/api/v1/library', newFilters);

    validatePaginatedResponse(filterResponse, 'filter-change');
    paginationLatency.add(Date.now() - filterStart);
  }
}

/**
 * Pattern 3: Detail view - browse and view game details
 * User browses library and clicks into game details.
 */
function detailViewPattern(token) {
  // Load library
  const libraryResponse = authGet(token, '/api/v1/library', { page: 1, pageSize: 10 });

  if (!validatePaginatedResponse(libraryResponse, 'library-for-details')) {
    libraryErrors.add(1);
    return;
  }

  // Extract game IDs from response
  let games = [];
  try {
    const data = libraryResponse.json();
    games = data.items || data.data || data || [];
  } catch (e) {
    libraryErrors.add(1);
    return;
  }

  if (games.length === 0) {
    console.log('No games in library for detail view pattern');
    return;
  }

  // View 2-4 game details
  const viewCount = Math.min(games.length, Math.floor(Math.random() * 3) + 2);
  for (let i = 0; i < viewCount; i++) {
    sleep(2); // Simulate user selecting a game

    const game = games[Math.floor(Math.random() * games.length)];
    const gameId = game.gameId || game.id;

    if (!gameId) continue;

    // Check game status
    const statusStart = Date.now();
    const statusResponse = authGet(token, `/api/v1/library/games/${gameId}/status`);
    validateResponse(statusResponse, 'game-status');
    libraryLatency.add(Date.now() - statusStart);

    // View game detail
    const detailStart = Date.now();
    const detailResponse = authGet(token, `/api/v1/library/games/${gameId}`);

    const detailSuccess = check(detailResponse, {
      'detail: status 200': (r) => r.status === 200 || r.status === 404,
      'detail: response time OK': (r) => r.timings.duration < 1500,
    });

    if (!detailSuccess) {
      libraryErrors.add(1);
    }
    detailViewLatency.add(Date.now() - detailStart);
  }
}

/**
 * Pattern 4: Quota check - check library quota and stats
 * User checks their library limits and usage.
 */
function quotaCheckPattern(token) {
  // Check quota
  const quotaStart = Date.now();
  const quotaResponse = authGet(token, '/api/v1/library/quota');
  validateResponse(quotaResponse, 'library-quota');
  libraryLatency.add(Date.now() - quotaStart);

  sleep(1);

  // Check stats
  const statsStart = Date.now();
  const statsResponse = authGet(token, '/api/v1/library/stats');
  validateResponse(statsResponse, 'library-stats');
  libraryLatency.add(Date.now() - statsStart);
}

// ============================================
// MAIN SCENARIO
// ============================================

/**
 * Library browsing scenario.
 * Simulates realistic user behavior when managing game library.
 */
export function libraryBrowsingScenario() {
  // Authenticate once per VU
  const token = authenticateUser();
  if (!token) {
    console.error('Failed to authenticate user, skipping scenario');
    return;
  }

  // Browsing patterns with weights
  const patterns = [
    { fn: quickBrowsePattern, weight: 0.30 },
    { fn: filteredBrowsePattern, weight: 0.35 },
    { fn: detailViewPattern, weight: 0.25 },
    { fn: quotaCheckPattern, weight: 0.10 },
  ];

  // Execute multiple browsing sessions
  const sessionCount = 5;
  for (let i = 0; i < sessionCount; i++) {
    // Select pattern based on weight
    const random = Math.random();
    let cumulative = 0;
    let selectedPattern = patterns[0].fn;

    for (const pattern of patterns) {
      cumulative += pattern.weight;
      if (random <= cumulative) {
        selectedPattern = pattern.fn;
        break;
      }
    }

    // Execute selected pattern
    selectedPattern(token);

    // Wait between sessions
    if (i < sessionCount - 1) {
      sleep(Math.random() * 5 + 3); // 3-8 seconds
    }
  }
}

// ============================================
// LIFECYCLE HOOKS
// ============================================

export function setup() {
  return standardSetup('Library Browsing', options);
}

export function teardown(data) {
  standardTeardown();
}

export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    'reports/library-browsing-summary.json': JSON.stringify(data, null, 2),
  };
}
