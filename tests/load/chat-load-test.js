/**
 * Load test for /api/v1/chat endpoint (POST)
 *
 * This test validates the performance of the chat message creation endpoint
 * under different load scenarios (100, 500, 1000 concurrent users).
 *
 * Target performance (TEST-04):
 * - 100 users: p95 < 300ms, error rate < 1%
 * - 500 users: p95 < 800ms, error rate < 1%
 *
 * Usage:
 *   k6 run --env SCENARIO=users100 chat-load-test.js
 *   k6 run --env SCENARIO=users500 chat-load-test.js
 *   k6 run --env SCENARIO=users1000 chat-load-test.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { config } from './config.js';
import { authenticate, getAuthHeaders, fetchGameIds, checkResponse, randomItem, randomSleep } from './utils.js';

// Determine which scenario to run (default: users100)
const scenario = __ENV.SCENARIO || 'users100';

// Configure test options
export const options = {
  scenarios: {
    [scenario]: config.scenarios[scenario],
  },
  thresholds: config.thresholds.chat[scenario] || config.thresholds.chat.users100,
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

/**
 * Setup function - runs once before test starts
 * Authenticates and fetches game IDs for the test
 */
export function setup() {
  console.log(`Starting chat load test - Scenario: ${scenario}`);
  console.log(`Base URL: ${config.baseUrl}`);
  console.log(`Thresholds: ${JSON.stringify(options.thresholds)}`);

  // Verify API is accessible
  const healthUrl = `${config.baseUrl}/health`;
  const healthCheck = http.get(healthUrl);

  if (healthCheck.status !== 200) {
    throw new Error(`API health check failed: ${healthCheck.status}`);
  }

  console.log('API health check passed');

  // Authenticate and get session
  const sessionCookie = authenticate(config.testUser.email, config.testUser.password);
  if (!sessionCookie) {
    throw new Error('Failed to authenticate test user');
  }

  // Fetch game IDs for test data
  const gameIds = fetchGameIds();
  if (!gameIds.chess && !gameIds.ticTacToe) {
    throw new Error('Failed to fetch game IDs');
  }

  console.log(`Authentication successful, found ${Object.keys(gameIds).length} games`);

  return {
    sessionCookie,
    gameIds,
  };
}

/**
 * Main test function - executed by each VU in each iteration
 */
export default function (data) {
  const { sessionCookie, gameIds } = data;

  if (!sessionCookie) {
    throw new Error('No session cookie available');
  }

  // Randomly select a game (chess or tic-tac-toe)
  const gameId = Math.random() > 0.5 ? gameIds.chess : gameIds.ticTacToe;
  const queries = gameId === gameIds.chess ? config.testData.queries.chess : config.testData.queries.ticTacToe;

  // Create chat endpoint URL
  const chatUrl = `${config.baseUrl}/api/v1/chat`;

  // Prepare request payload with random query
  const payload = JSON.stringify({
    gameId: gameId,
    message: randomItem(queries),
  });

  const params = {
    headers: getAuthHeaders(sessionCookie),
    tags: { name: 'CreateChatMessage' },
  };

  // Make POST request to create chat message
  const response = http.post(chatUrl, payload, params);

  // Validate response
  const success = checkResponse(response, 'create_chat', 201);

  if (success) {
    // Additional validations
    check(response, {
      'create_chat: has chat ID': (r) => r.json('id') !== undefined,
      'create_chat: has message': (r) => r.json('message') !== undefined,
      'create_chat: has game ID': (r) => r.json('gameId') !== undefined,
    });

    const chatId = response.json('id');

    // Optionally: fetch chat messages to simulate real usage
    if (Math.random() > 0.7) { // 30% of requests also fetch messages
      const getMessagesUrl = `${config.baseUrl}/api/v1/chat/${gameIds.chess || gameIds.ticTacToe}/messages`;
      const getParams = {
        headers: getAuthHeaders(sessionCookie),
        tags: { name: 'GetChatMessages' },
      };

      const messagesResponse = http.get(getMessagesUrl, getParams);
      checkResponse(messagesResponse, 'get_chat_messages', 200);
    }
  }

  // Simulate user think time (2-5 seconds)
  sleep(randomSleep(2, 5));
}

/**
 * Teardown function - runs once after test completes
 */
export function teardown(data) {
  console.log('Chat load test completed');
}

/**
 * Handle test summary for custom reporting
 */
export function handleSummary(data) {
  return {
    'stdout': textSummary(data, { indent: ' ', enableColors: true }),
    'chat-load-test-results.json': JSON.stringify(data, null, 2),
  };
}

/**
 * Generate text summary for console output
 */
function textSummary(data, options) {
  const indent = options.indent || '';

  let output = '\n';
  output += `${indent}█ CHAT LOAD TEST SUMMARY (${scenario})\n\n`;

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

  if (data.metrics.http_reqs) {
    const totalReqs = data.metrics.http_reqs.values.count;
    const reqPerSec = data.metrics.http_reqs.values.rate.toFixed(2);
    output += `${indent}Total Requests: ${totalReqs} (${reqPerSec}/s)\n`;
  }

  // Checks
  if (data.metrics.checks) {
    const passRate = (data.metrics.checks.values.rate * 100).toFixed(2);
    const passed = data.metrics.checks.values.passes;
    const failed = data.metrics.checks.values.fails;
    output += `${indent}Checks: ${passRate}% passed (${passed}/${passed + failed})\n`;
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
