/**
 * Load test for /api/v1/agents/qa endpoint (POST)
 *
 * This test validates the performance of the RAG-powered Q&A agent endpoint
 * under different load scenarios (100, 500, 1000 concurrent users).
 *
 * Target performance (TEST-04):
 * - 100 users: p95 < 500ms, error rate < 1%
 * - 500 users: p95 < 1s, error rate < 1%
 *
 * Note: This endpoint involves:
 * - Vector search (Qdrant)
 * - LLM generation (OpenRouter/Ollama)
 * - Response caching (Redis, AI-05)
 * - Chat persistence
 * - AI request logging
 *
 * Usage:
 *   k6 run --env SCENARIO=users100 qa-agent-load-test.js
 *   k6 run --env SCENARIO=users500 qa-agent-load-test.js
 *   k6 run --env SCENARIO=users1000 qa-agent-load-test.js
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
  thresholds: config.thresholds.qa[scenario] || config.thresholds.qa.users100,
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

/**
 * Setup function - runs once before test starts
 * Authenticates and fetches game IDs for the test
 */
export function setup() {
  console.log(`Starting Q&A agent load test - Scenario: ${scenario}`);
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

  // Randomly select a game and question
  // Use more chess queries (70/30 split) since chess has more complex rules
  const useChess = Math.random() > 0.3;
  const gameId = useChess ? gameIds.chess : gameIds.ticTacToe;
  const queries = useChess ? config.testData.queries.chess : config.testData.queries.ticTacToe;
  const query = randomItem(queries);

  // Create Q&A endpoint URL
  const qaUrl = `${config.baseUrl}/api/v1/agents/qa`;

  // Prepare request payload
  const payload = JSON.stringify({
    gameId: gameId,
    query: query,
  });

  const params = {
    headers: getAuthHeaders(sessionCookie),
    tags: { name: 'QAAgent' },
    timeout: '30s', // Allow longer timeout for LLM generation
  };

  // Make POST request to Q&A agent
  const response = http.post(qaUrl, payload, params);

  // Validate response
  const success = checkResponse(response, 'qa_agent', 200);

  if (success) {
    // Additional validations
    const responseData = response.json();

    check(response, {
      'qa_agent: has answer': (r) => {
        const data = r.json();
        return data.answer && data.answer.length > 0;
      },
      'qa_agent: has citations': (r) => {
        const data = r.json();
        return Array.isArray(data.citations);
      },
      'qa_agent: has confidence': (r) => {
        const data = r.json();
        return typeof data.confidence === 'number';
      },
      'qa_agent: confidence in range': (r) => {
        const data = r.json();
        return data.confidence >= 0 && data.confidence <= 1;
      },
      'qa_agent: answer not empty': (r) => {
        const data = r.json();
        return data.answer && data.answer.trim().length > 10;
      },
    });

    // Track cache hits for AI-05 response caching analysis
    if (responseData.cached !== undefined) {
      check(response, {
        'qa_agent: cache status tracked': () => true,
      });
    }
  } else {
    // Log error details for debugging
    console.error(`Q&A request failed: ${response.status} - ${response.body}`);
  }

  // Simulate user reading response (5-10 seconds)
  sleep(randomSleep(5, 10));
}

/**
 * Teardown function - runs once after test completes
 */
export function teardown(data) {
  console.log('Q&A agent load test completed');
}

/**
 * Handle test summary for custom reporting
 */
export function handleSummary(data) {
  return {
    'stdout': textSummary(data, { indent: ' ', enableColors: true }),
    'qa-agent-load-test-results.json': JSON.stringify(data, null, 2),
  };
}

/**
 * Generate text summary for console output
 */
function textSummary(data, options) {
  const indent = options.indent || '';

  let output = '\n';
  output += `${indent}█ Q&A AGENT LOAD TEST SUMMARY (${scenario})\n\n`;

  // Test duration
  if (data.metrics.iteration_duration) {
    const duration = data.state.testRunDurationMs / 1000;
    output += `${indent}Duration: ${duration.toFixed(2)}s\n`;
  }

  // Iterations
  if (data.metrics.iterations) {
    const iterations = data.metrics.iterations.values.count;
    const iterRate = data.metrics.iterations.values.rate.toFixed(2);
    output += `${indent}Iterations: ${iterations} (${iterRate}/s)\n`;
  }

  // HTTP metrics
  if (data.metrics.http_req_duration) {
    const duration = data.metrics.http_req_duration.values;
    output += `\n${indent}HTTP Request Duration:\n`;
    output += `${indent}  avg=${duration.avg.toFixed(2)}ms\n`;
    output += `${indent}  min=${duration.min.toFixed(2)}ms\n`;
    output += `${indent}  med=${duration.med.toFixed(2)}ms\n`;
    output += `${indent}  max=${duration.max.toFixed(2)}ms\n`;
    output += `${indent}  p(90)=${duration['p(90)'].toFixed(2)}ms\n`;
    output += `${indent}  p(95)=${duration['p(95)'].toFixed(2)}ms\n`;
    output += `${indent}  p(99)=${duration['p(99)'].toFixed(2)}ms\n`;
  }

  if (data.metrics.http_req_failed) {
    const failRate = (data.metrics.http_req_failed.values.rate * 100).toFixed(2);
    const failed = data.metrics.http_req_failed.values.passes;
    output += `\n${indent}Error Rate: ${failRate}% (${failed} failed)\n`;
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
    output += `\n${indent}Checks: ${passRate}% passed (${passed}/${passed + failed})\n`;
  }

  // VUs
  if (data.metrics.vus) {
    output += `\n${indent}Virtual Users:\n`;
    output += `${indent}  min=${data.metrics.vus.values.min}\n`;
    output += `${indent}  max=${data.metrics.vus.values.max}\n`;
  }

  // Threshold results
  output += `\n${indent}█ THRESHOLDS\n\n`;
  const thresholds = data.metrics;
  let allPassed = true;

  for (const [metric, details] of Object.entries(thresholds)) {
    if (details.thresholds) {
      for (const [threshold, result] of Object.entries(details.thresholds)) {
        const status = result.ok ? '✓' : '✗';
        if (!result.ok) allPassed = false;
        output += `${indent}  ${status} ${metric}: ${threshold}\n`;
      }
    }
  }

  // Final result
  output += `\n${indent}█ RESULT: ${allPassed ? 'PASSED ✓' : 'FAILED ✗'}\n`;

  return output;
}
