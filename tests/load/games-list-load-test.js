/**
 * Load test for /api/v1/games endpoint (GET)
 *
 * This test validates the performance of the games listing endpoint
 * under different load scenarios (100, 500, 1000 concurrent users).
 *
 * Target performance (TEST-04):
 * - 100 users: p95 < 200ms, error rate < 0.1%
 * - 500 users: p95 < 500ms, error rate < 0.1%
 *
 * Usage:
 *   k6 run --env SCENARIO=users100 games-list-load-test.js
 *   k6 run --env SCENARIO=users500 games-list-load-test.js
 *   k6 run --env SCENARIO=users1000 games-list-load-test.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { config } from './config.js';
import { checkResponse, randomSleep } from './utils.js';

// Determine which scenario to run (default: users100)
const scenario = __ENV.SCENARIO || 'users100';

// Configure test options
export const options = {
  scenarios: {
    [scenario]: config.scenarios[scenario],
  },
  thresholds: config.thresholds.games[scenario] || config.thresholds.games.users100,
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

/**
 * Main test function - executed by each VU in each iteration
 */
export default function () {
  const gamesUrl = `${config.baseUrl}/api/v1/games`;

  // Make GET request to fetch games list
  const response = http.get(gamesUrl, {
    tags: { name: 'GetGames' },
  });

  // Validate response
  const success = checkResponse(response, 'get_games', 200);

  if (success) {
    // Additional validations
    check(response, {
      'get_games: has games array': (r) => Array.isArray(r.json()),
      'get_games: games count > 0': (r) => r.json().length > 0,
      'get_games: has chess game': (r) => {
        const games = r.json();
        return games.some(g => g.name && g.name.toLowerCase().includes('chess'));
      },
    });
  }

  // Simulate user think time (1-3 seconds)
  sleep(randomSleep(1, 3));
}

/**
 * Setup function - runs once before test starts
 */
export function setup() {
  console.log(`Starting games list load test - Scenario: ${scenario}`);
  console.log(`Base URL: ${config.baseUrl}`);
  console.log(`Thresholds: ${JSON.stringify(options.thresholds)}`);

  // Verify API is accessible
  const healthUrl = `${config.baseUrl}/health`;
  const healthCheck = http.get(healthUrl);

  if (healthCheck.status !== 200) {
    throw new Error(`API health check failed: ${healthCheck.status}`);
  }

  console.log('API health check passed');
  return {};
}

/**
 * Teardown function - runs once after test completes
 */
export function teardown(data) {
  console.log('Games list load test completed');
}

/**
 * Handle test summary for custom reporting
 */
export function handleSummary(data) {
  return {
    'stdout': textSummary(data, { indent: ' ', enableColors: true }),
    'games-list-load-test-results.json': JSON.stringify(data, null, 2),
  };
}

/**
 * Generate text summary for console output
 */
function textSummary(data, options) {
  const indent = options.indent || '';
  const enableColors = options.enableColors || false;

  let output = '\n';
  output += `${indent}█ GAMES LIST LOAD TEST SUMMARY (${scenario})\n\n`;

  // Test duration
  if (data.metrics.iteration_duration) {
    output += `${indent}Duration: ${data.state.testRunDurationMs / 1000}s\n`;
  }

  // HTTP metrics
  if (data.metrics.http_req_duration) {
    const duration = data.metrics.http_req_duration.values;
    output += `${indent}HTTP Request Duration:\n`;
    output += `${indent}  avg=${duration.avg.toFixed(2)}ms\n`;
    output += `${indent}  p(95)=${duration['p(95)'].toFixed(2)}ms\n`;
    output += `${indent}  p(99)=${duration['p(99)'].toFixed(2)}ms\n`;
  }

  if (data.metrics.http_req_failed) {
    const failRate = (data.metrics.http_req_failed.values.rate * 100).toFixed(2);
    output += `${indent}Error Rate: ${failRate}%\n`;
  }

  // Checks
  if (data.metrics.checks) {
    const passRate = (data.metrics.checks.values.rate * 100).toFixed(2);
    output += `${indent}Checks Passed: ${passRate}%\n`;
  }

  // Threshold results
  output += `\n${indent}█ THRESHOLDS\n\n`;
  const thresholds = data.metrics;
  for (const [metric, details] of Object.entries(thresholds)) {
    if (details.thresholds) {
      for (const [threshold, result] of Object.entries(details.thresholds)) {
        const status = result.ok ? '✓' : '✗';
        output += `${indent}  ${status} ${metric}: ${threshold}\n`;
      }
    }
  }

  return output;
}
