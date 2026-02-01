/**
 * ISSUE-2928: Catalog Search Load Testing - Search & Filtering
 *
 * Scenario 3: Shared game catalog search with filters and pagination.
 * Simulates users searching the public game catalog.
 *
 * Endpoints tested:
 * - GET /api/v1/shared-games (paginated search)
 * - GET /api/v1/shared-games/search (advanced search)
 * - GET /api/v1/shared-games/{id} (game details)
 * - GET /api/v1/shared-games/stats
 *
 * Performance Targets:
 * - Response time p95 < 500ms
 * - Response time p99 < 1s
 * - Error rate < 1%
 */

import http from 'k6/http';
import { sleep, check } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';
import {
  config,
  authenticateUser,
  authGet,
  authPost,
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

const searchLatency = new Trend('catalog_search_latency');
const filterLatency = new Trend('catalog_filter_latency');
const detailLatency = new Trend('catalog_detail_latency');
const catalogErrors = new Counter('catalog_errors');
const searchErrors = new Counter('search_errors');

// ============================================
// TEST CONFIGURATION
// ============================================

const TEST_TYPE = __ENV.TEST_TYPE || 'smoke';

export const options = {
  scenarios: {
    // Smoke test
    smoke: {
      ...scenarioConfigs.smoke,
      tags: { test_type: 'smoke', scenario: 'catalog-search' },
      exec: 'catalogSearchScenario',
    },

    // Load test: Realistic catalog browsing (75 VUs - higher as it's public)
    load: {
      executor: 'constant-vus',
      vus: 75,
      duration: '5m',
      tags: { test_type: 'load', scenario: 'catalog-search' },
      exec: 'catalogSearchScenario',
    },

    // Stress test: Peak load
    stress: {
      ...scenarioConfigs.stress,
      stages: [
        { duration: '2m', target: 75 },
        { duration: '3m', target: 150 },
        { duration: '3m', target: 300 },
        { duration: '2m', target: 0 },
      ],
      tags: { test_type: 'stress', scenario: 'catalog-search' },
      exec: 'catalogSearchScenario',
    },
  },

  // Performance thresholds
  thresholds: {
    ...standardThresholds,
    'catalog_search_latency': ['p(95)<500', 'p(99)<1000'],
    'catalog_filter_latency': ['p(95)<500', 'p(99)<1000'],
    'catalog_detail_latency': ['p(95)<750', 'p(99)<1500'],
    'catalog_errors': ['count<20'],
    'search_errors': ['count<30'],
  },

  // HTTP configuration
  httpDebug: __ENV.HTTP_DEBUG === 'true' ? 'full' : '',
  insecureSkipTLSVerify: true,
  noConnectionReuse: false,

  // Output
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

// ============================================
// SEARCH TERMS & FILTERS
// ============================================

// Common board game search terms
const searchTerms = [
  'catan', 'monopoly', 'risk', 'pandemic', 'ticket',
  'chess', 'scrabble', 'dominion', 'carcassonne', 'splendor',
  'azul', 'wingspan', 'everdell', 'terraforming', 'gloomhaven',
  'settlers', 'codenames', 'agricola', 'clue', 'uno',
];

// Player count filters
const playerCounts = [
  { min: 1, max: 2 },
  { min: 2, max: 4 },
  { min: 3, max: 6 },
  { min: 4, max: 8 },
  { min: 6, max: 12 },
];

// Play time filters (minutes)
const playTimes = [
  { min: 15, max: 30 },
  { min: 30, max: 60 },
  { min: 60, max: 120 },
  { min: 120, max: 240 },
];

// Complexity weights
const complexityFilters = ['1-2', '2-3', '3-4', '4-5'];

/**
 * Get random search term
 */
function getRandomSearchTerm() {
  return searchTerms[Math.floor(Math.random() * searchTerms.length)];
}

/**
 * Generate random filter combination for catalog search.
 */
function getRandomFilters() {
  const filters = {
    page: randomPage(10),
    pageSize: randomPageSize(),
  };

  // 60% chance to include search term
  if (Math.random() < 0.6) {
    filters.search = getRandomSearchTerm();
  }

  // 30% chance to filter by player count
  if (Math.random() < 0.3) {
    const playerFilter = playerCounts[Math.floor(Math.random() * playerCounts.length)];
    filters.minPlayers = playerFilter.min;
    filters.maxPlayers = playerFilter.max;
  }

  // 20% chance to filter by play time
  if (Math.random() < 0.2) {
    const timeFilter = playTimes[Math.floor(Math.random() * playTimes.length)];
    filters.minPlayTime = timeFilter.min;
    filters.maxPlayTime = timeFilter.max;
  }

  // 15% chance to filter by complexity
  if (Math.random() < 0.15) {
    filters.complexity = complexityFilters[Math.floor(Math.random() * complexityFilters.length)];
  }

  return filters;
}

// ============================================
// SEARCH PATTERNS
// ============================================

/**
 * Pattern 1: Simple search - text search with pagination
 * User types a search term and browses results.
 */
function simpleSearchPattern(token) {
  const searchTerm = getRandomSearchTerm();

  // Initial search
  const startTime = Date.now();
  const response = authGet(token, '/api/v1/shared-games', {
    search: searchTerm,
    page: 1,
    pageSize: 20,
  });

  if (!validatePaginatedResponse(response, 'simple-search')) {
    searchErrors.add(1);
    return;
  }
  searchLatency.add(Date.now() - startTime);

  // Browse through pages
  const pageCount = Math.floor(Math.random() * 3) + 2;
  for (let i = 2; i <= pageCount; i++) {
    sleep(1.5);

    const pageStart = Date.now();
    const pageResponse = authGet(token, '/api/v1/shared-games', {
      search: searchTerm,
      page: i,
      pageSize: 20,
    });

    validatePaginatedResponse(pageResponse, `search-page-${i}`);
    searchLatency.add(Date.now() - pageStart);
  }
}

/**
 * Pattern 2: Filtered search - apply multiple filters
 * User applies various filters to narrow down results.
 */
function filteredSearchPattern(token) {
  // Start with basic filters
  let filters = getRandomFilters();

  const startTime = Date.now();
  const response = authGet(token, '/api/v1/shared-games', filters);

  if (!validatePaginatedResponse(response, 'filtered-search')) {
    searchErrors.add(1);
  }
  filterLatency.add(Date.now() - startTime);

  // Refine filters 3-5 times (simulating user adjusting filters)
  const refinementCount = Math.floor(Math.random() * 3) + 3;
  for (let i = 0; i < refinementCount; i++) {
    sleep(1);

    filters = getRandomFilters();
    const filterStart = Date.now();
    const filterResponse = authGet(token, '/api/v1/shared-games', filters);

    validatePaginatedResponse(filterResponse, 'filter-refinement');
    filterLatency.add(Date.now() - filterStart);
  }
}

/**
 * Pattern 3: Browse and view - browse catalog and view game details
 * User browses catalog and clicks into specific games.
 */
function browseAndViewPattern(token) {
  // Browse catalog
  const browseResponse = authGet(token, '/api/v1/shared-games', {
    page: 1,
    pageSize: 20,
  });

  if (!validatePaginatedResponse(browseResponse, 'catalog-browse')) {
    catalogErrors.add(1);
    return;
  }

  // Extract game IDs
  let games = [];
  try {
    const data = browseResponse.json();
    games = data.items || data.data || data.games || [];
  } catch (e) {
    catalogErrors.add(1);
    return;
  }

  if (games.length === 0) {
    console.log('No games in catalog for detail view');
    return;
  }

  // View 3-5 game details
  const viewCount = Math.min(games.length, Math.floor(Math.random() * 3) + 3);
  for (let i = 0; i < viewCount; i++) {
    sleep(2); // User reading time

    const game = games[Math.floor(Math.random() * games.length)];
    const gameId = game.id || game.gameId;

    if (!gameId) continue;

    const detailStart = Date.now();
    const detailResponse = authGet(token, `/api/v1/shared-games/${gameId}`);

    const success = check(detailResponse, {
      'game-detail: status OK': (r) => r.status === 200 || r.status === 404,
      'game-detail: response time OK': (r) => r.timings.duration < 1500,
    });

    if (!success) {
      catalogErrors.add(1);
    }
    detailLatency.add(Date.now() - detailStart);
  }
}

/**
 * Pattern 4: Discovery browsing - explore various categories
 * User explores catalog without specific search intent.
 */
function discoveryPattern(token) {
  // Get catalog stats first
  const statsResponse = authGet(token, '/api/v1/shared-games/stats');
  validateResponse(statsResponse, 'catalog-stats');

  sleep(1);

  // Browse different "sections" with various filters
  const sections = [
    { page: 1, pageSize: 10 },
    { page: 1, pageSize: 10, minPlayers: 2, maxPlayers: 4 },
    { page: 1, pageSize: 10, minPlayTime: 30, maxPlayTime: 60 },
    { page: 1, pageSize: 10, sortBy: 'popularity' },
  ];

  for (const section of sections) {
    sleep(2);

    const sectionStart = Date.now();
    const response = authGet(token, '/api/v1/shared-games', section);
    validatePaginatedResponse(response, 'discovery-section');
    filterLatency.add(Date.now() - sectionStart);
  }
}

/**
 * Pattern 5: Anonymous browsing - no authentication
 * Public user browsing without logging in.
 */
function anonymousBrowsePattern() {
  // Anonymous request (no token)
  const url = `${config.apiBaseUrl}/api/v1/shared-games?page=1&pageSize=20`;

  const startTime = Date.now();
  const response = http.get(url, {
    headers: { 'Content-Type': 'application/json' },
    tags: { endpoint: 'anonymous-browse', authenticated: 'false' },
  });

  const success = check(response, {
    'anonymous: status OK': (r) => r.status === 200 || r.status === 401,
    'anonymous: response time OK': (r) => r.timings.duration < 1000,
  });

  if (response.status === 200) {
    searchLatency.add(Date.now() - startTime);
  }

  // If anonymous access works, browse a few pages
  if (response.status === 200) {
    for (let i = 2; i <= 3; i++) {
      sleep(1);
      const pageUrl = `${config.apiBaseUrl}/api/v1/shared-games?page=${i}&pageSize=20`;
      http.get(pageUrl, {
        headers: { 'Content-Type': 'application/json' },
        tags: { endpoint: 'anonymous-page' },
      });
    }
  }
}

// ============================================
// MAIN SCENARIO
// ============================================

/**
 * Catalog search scenario.
 * Simulates realistic user behavior when searching the game catalog.
 */
export function catalogSearchScenario() {
  // 30% of users browse anonymously
  const isAnonymous = Math.random() < 0.3;

  let token = null;
  if (!isAnonymous) {
    token = authenticateUser();
    if (!token) {
      console.error('Failed to authenticate, falling back to anonymous');
    }
  }

  // Search patterns with weights
  const patterns = token
    ? [
        { fn: simpleSearchPattern, weight: 0.30 },
        { fn: filteredSearchPattern, weight: 0.25 },
        { fn: browseAndViewPattern, weight: 0.25 },
        { fn: discoveryPattern, weight: 0.20 },
      ]
    : [{ fn: anonymousBrowsePattern, weight: 1.0 }];

  // Execute multiple search sessions
  const sessionCount = 4;
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
    if (selectedPattern === anonymousBrowsePattern) {
      selectedPattern();
    } else {
      selectedPattern(token);
    }

    // Wait between sessions
    if (i < sessionCount - 1) {
      sleep(Math.random() * 4 + 2); // 2-6 seconds
    }
  }
}

// ============================================
// LIFECYCLE HOOKS
// ============================================

export function setup() {
  return standardSetup('Catalog Search', options);
}

export function teardown(data) {
  standardTeardown();
}

export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    'reports/catalog-search-summary.json': JSON.stringify(data, null, 2),
  };
}
