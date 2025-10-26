/**
 * Load test for AI-11.3: Performance Testing for Quality Scoring System
 *
 * This test measures the performance overhead of the quality scoring system
 * implemented in AI-11 (#410). It compares latency with quality scoring enabled
 * vs baseline performance to ensure overhead stays within acceptable limits.
 *
 * Performance Targets (AI-11.3):
 * - p50 latency overhead < 20ms
 * - p95 latency overhead < 50ms
 * - p99 latency overhead < 100ms
 * - Throughput degradation < 10%
 * - Error rate < 5%
 *
 * Test Scenarios:
 * - baseline: Quality scoring disabled (for overhead calculation)
 * - users100: 100 concurrent users for 5 minutes
 * - users500: 500 concurrent users for 2 minutes (stress test)
 *
 * Quality Scoring Components (measured overhead):
 * - RAG confidence calculation
 * - LLM confidence heuristics
 * - Citation quality scoring
 * - Overall confidence aggregation
 * - Prometheus metrics recording
 * - Database writes (async)
 *
 * Usage:
 *   k6 run --env SCENARIO=baseline quality-scoring-load-test.js   # Baseline test
 *   k6 run --env SCENARIO=users100 quality-scoring-load-test.js   # 100 RPS test
 *   k6 run --env SCENARIO=users500 quality-scoring-load-test.js   # 500 RPS stress test
 *
 * Environment Variables:
 *   SCENARIO: baseline | users100 | users500 (default: users100)
 *   API_BASE_URL: Base API URL (default: http://localhost:8080)
 *   QUALITY_ENABLED: true | false (default: true, override for baseline)
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { config } from './config.js';
import { authenticate, getAuthHeaders, fetchGameIds, checkResponse, randomItem, randomSleep } from './utils.js';

// Determine which scenario to run (default: users100)
const scenario = __ENV.SCENARIO || 'users100';

// Quality scoring enabled flag (can be disabled for baseline testing)
const qualityEnabled = __ENV.QUALITY_ENABLED !== 'false';

// Configure test options with quality-specific thresholds
export const options = {
  scenarios: {
    [scenario]: getScenarioConfig(scenario),
  },
  thresholds: getThresholds(scenario),
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(50)', 'p(90)', 'p(95)', 'p(99)'],
};

/**
 * Get scenario configuration based on scenario name
 */
function getScenarioConfig(scenarioName) {
  if (scenarioName === 'baseline') {
    // Baseline: shorter duration for comparison
    return {
      executor: 'ramping-vus',
      stages: [
        { duration: '30s', target: 50 },   // ramp up to 50 users
        { duration: '2m', target: 50 },    // maintain 50 users
        { duration: '30s', target: 0 },    // ramp down
      ],
    };
  }

  // Use config scenarios for users100, users500
  return config.scenarios[scenarioName] || config.scenarios.users100;
}

/**
 * Get performance thresholds based on scenario
 */
function getThresholds(scenarioName) {
  if (scenarioName === 'baseline') {
    // Baseline: stricter thresholds (no quality overhead)
    return {
      http_req_duration: ['p(95)<400'], // Baseline should be faster
      http_req_failed: ['rate<0.05'],   // 5% error rate max
      'quality_score_present': ['rate>0.90'], // 90%+ should have scores
    };
  } else if (scenarioName === 'users500') {
    // Stress test: relaxed thresholds
    return {
      http_req_duration: ['p(50)<800', 'p(95)<1500', 'p(99)<2500'], // AI-11.3 targets + stress margin
      http_req_failed: ['rate<0.05'],   // 5% error rate max
      'quality_score_present': ['rate>0.85'], // 85%+ should have scores under stress
    };
  } else {
    // users100: AI-11.3 target thresholds
    return {
      http_req_duration: ['p(50)<500', 'p(95)<700', 'p(99)<1200'], // Includes overhead targets
      http_req_failed: ['rate<0.05'],   // 5% error rate max
      'quality_score_present': ['rate>0.90'], // 90%+ should have scores
    };
  }
}

/**
 * Setup function - runs once before test starts
 */
export function setup() {
  console.log(`\n═══════════════════════════════════════════════════════════`);
  console.log(`  AI-11.3: Quality Scoring Performance Test`);
  console.log(`  Scenario: ${scenario}`);
  console.log(`  Quality Scoring: ${qualityEnabled ? 'ENABLED' : 'DISABLED (baseline)'}`);
  console.log(`  Base URL: ${config.baseUrl}`);
  console.log(`═══════════════════════════════════════════════════════════\n`);

  // Verify API is accessible
  const healthUrl = `${config.baseUrl}/health`;
  const healthCheck = http.get(healthUrl);

  if (healthCheck.status !== 200) {
    throw new Error(`API health check failed: ${healthCheck.status}`);
  }

  console.log('✓ API health check passed');

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

  console.log(`✓ Authentication successful`);
  console.log(`✓ Found ${Object.keys(gameIds).length} games for testing\n`);
  console.log(`Starting load test...\n`);

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
    tags: {
      name: 'QAAgent_QualityScoring',
      scenario: scenario,
      quality_enabled: qualityEnabled.toString(),
    },
    timeout: '30s', // Allow longer timeout for LLM generation
  };

  // Make POST request to Q&A agent
  const response = http.post(qaUrl, payload, params);

  // Validate response
  const success = checkResponse(response, 'qa_quality_test', 200);

  if (success) {
    // Parse response data
    const responseData = response.json();

    // Core validations
    check(response, {
      'qa_quality: has answer': (r) => {
        const data = r.json();
        return data.answer && data.answer.length > 0;
      },
      'qa_quality: answer not empty': (r) => {
        const data = r.json();
        return data.answer && data.answer.trim().length > 10;
      },
      'qa_quality: has citations': (r) => {
        const data = r.json();
        return Array.isArray(data.citations);
      },
    });

    // Quality scoring specific validations
    if (qualityEnabled) {
      const qualityCheck = check(response, {
        'quality_score_present': (r) => {
          const data = r.json();
          return data.confidence !== undefined && typeof data.confidence === 'number';
        },
        'quality_score_valid_range': (r) => {
          const data = r.json();
          return data.confidence >= 0 && data.confidence <= 1;
        },
        'quality_score_has_rag_confidence': (r) => {
          const data = r.json();
          return data.ragConfidence !== undefined;
        },
        'quality_score_has_llm_confidence': (r) => {
          const data = r.json();
          return data.llmConfidence !== undefined;
        },
        'quality_score_has_citation_quality': (r) => {
          const data = r.json();
          return data.citationQuality !== undefined;
        },
      });

      // Log quality score details for analysis (sampling: 5% of requests)
      if (Math.random() < 0.05) {
        console.log(`Quality Scores - Overall: ${responseData.confidence?.toFixed(3)}, RAG: ${responseData.ragConfidence?.toFixed(3)}, LLM: ${responseData.llmConfidence?.toFixed(3)}, Citation: ${responseData.citationQuality?.toFixed(3)}`);
      }

      // Track low-quality responses for analysis
      if (responseData.confidence !== undefined && responseData.confidence < 0.60) {
        check(response, {
          'low_quality_response_detected': () => true,
        });
      }
    } else {
      // Baseline: quality scores should NOT be present
      check(response, {
        'baseline_no_quality_scores': (r) => {
          const data = r.json();
          return data.confidence === undefined &&
                 data.ragConfidence === undefined &&
                 data.llmConfidence === undefined;
        },
      });
    }

    // Track cache hits for AI-05 response caching analysis
    if (responseData.cached !== undefined) {
      check(response, {
        'cache_status_tracked': () => true,
      });

      if (responseData.cached) {
        check(response, {
          'cached_response': () => true,
        });
      }
    }
  } else {
    // Log error details for debugging (sample 10% to avoid log spam)
    if (Math.random() < 0.1) {
      console.error(`Q&A request failed: ${response.status} - ${response.body.substring(0, 200)}`);
    }
  }

  // Simulate user reading response (5-10 seconds)
  sleep(randomSleep(5, 10));
}

/**
 * Teardown function - runs once after test completes
 */
export function teardown(data) {
  console.log('\n═══════════════════════════════════════════════════════════');
  console.log('  Quality Scoring Performance Test Completed');
  console.log('═══════════════════════════════════════════════════════════\n');
  console.log('Check results in quality-scoring-results.json and console summary above.');
  console.log('');
}

/**
 * Handle test summary for custom reporting
 */
export function handleSummary(data) {
  const resultFile = scenario === 'baseline'
    ? 'baseline-results.json'
    : `quality-scoring-${scenario}-results.json`;

  return {
    'stdout': textSummary(data, { indent: ' ', enableColors: true }),
    [resultFile]: JSON.stringify(data, null, 2),
  };
}

/**
 * Generate text summary for console output
 */
function textSummary(data, options) {
  const indent = options.indent || '';

  let output = '\n';
  output += `${indent}╔═══════════════════════════════════════════════════════════╗\n`;
  output += `${indent}║  AI-11.3 QUALITY SCORING PERFORMANCE TEST SUMMARY        ║\n`;
  output += `${indent}║  Scenario: ${scenario.padEnd(45)}║\n`;
  output += `${indent}║  Quality Scoring: ${(qualityEnabled ? 'ENABLED' : 'DISABLED').padEnd(39)}║\n`;
  output += `${indent}╚═══════════════════════════════════════════════════════════╝\n\n`;

  // Test duration
  if (data.state && data.state.testRunDurationMs) {
    const duration = data.state.testRunDurationMs / 1000;
    output += `${indent}📊 Duration: ${duration.toFixed(2)}s\n`;
  }

  // Iterations
  if (data.metrics.iterations) {
    const iterations = data.metrics.iterations.values.count;
    const iterRate = data.metrics.iterations.values.rate.toFixed(2);
    output += `${indent}🔄 Iterations: ${iterations} (${iterRate}/s)\n`;
  }

  // HTTP metrics
  if (data.metrics.http_req_duration) {
    const duration = data.metrics.http_req_duration.values;
    output += `\n${indent}⏱️  HTTP Request Duration:\n`;
    output += `${indent}   avg  = ${duration.avg.toFixed(2)}ms\n`;
    output += `${indent}   min  = ${duration.min.toFixed(2)}ms\n`;
    output += `${indent}   med  = ${duration.med.toFixed(2)}ms\n`;
    output += `${indent}   max  = ${duration.max.toFixed(2)}ms\n`;
    output += `${indent}   p(50) = ${duration['p(50)'].toFixed(2)}ms\n`;
    output += `${indent}   p(90) = ${duration['p(90)'].toFixed(2)}ms\n`;
    output += `${indent}   p(95) = ${duration['p(95)'].toFixed(2)}ms ← AI-11.3 Target: < 700ms\n`;
    output += `${indent}   p(99) = ${duration['p(99)'].toFixed(2)}ms ← AI-11.3 Target: < 1200ms\n`;
  }

  if (data.metrics.http_req_failed) {
    const failRate = (data.metrics.http_req_failed.values.rate * 100).toFixed(2);
    const failed = data.metrics.http_req_failed.values.passes;
    const status = failRate < 5 ? '✅' : '⚠️';
    output += `\n${indent}${status} Error Rate: ${failRate}% (${failed} failed) ← Target: < 5%\n`;
  }

  if (data.metrics.http_reqs) {
    const totalReqs = data.metrics.http_reqs.values.count;
    const reqPerSec = data.metrics.http_reqs.values.rate.toFixed(2);
    output += `${indent}📡 Total Requests: ${totalReqs} (${reqPerSec}/s)\n`;
  }

  // Quality scoring specific metrics
  if (data.metrics['quality_score_present']) {
    const qualityRate = (data.metrics['quality_score_present'].values.rate * 100).toFixed(2);
    const status = qualityRate >= 90 ? '✅' : '⚠️';
    output += `\n${indent}${status} Quality Scores Present: ${qualityRate}% ← Target: > 90%\n`;
  }

  if (data.metrics['low_quality_response_detected']) {
    const lowQualityCount = data.metrics['low_quality_response_detected'].values.passes;
    output += `${indent}⚠️  Low-Quality Responses: ${lowQualityCount} (confidence < 0.60)\n`;
  }

  if (data.metrics['cached_response']) {
    const cacheHitRate = data.metrics['cached_response'].values.passes;
    output += `${indent}💾 Cache Hits: ${cacheHitRate}\n`;
  }

  // Checks
  if (data.metrics.checks) {
    const passRate = (data.metrics.checks.values.rate * 100).toFixed(2);
    const passed = data.metrics.checks.values.passes;
    const failed = data.metrics.checks.values.fails;
    const status = passRate >= 95 ? '✅' : '⚠️';
    output += `\n${indent}${status} Checks: ${passRate}% passed (${passed}/${passed + failed})\n`;
  }

  // VUs
  if (data.metrics.vus) {
    output += `\n${indent}👥 Virtual Users:\n`;
    output += `${indent}   min = ${data.metrics.vus.values.min}\n`;
    output += `${indent}   max = ${data.metrics.vus.values.max}\n`;
  }

  // Threshold results
  output += `\n${indent}╔═══════════════════════════════════════════════════════════╗\n`;
  output += `${indent}║  THRESHOLD RESULTS                                        ║\n`;
  output += `${indent}╚═══════════════════════════════════════════════════════════╝\n\n`;

  const thresholds = data.metrics;
  let allPassed = true;

  for (const [metric, details] of Object.entries(thresholds)) {
    if (details.thresholds) {
      for (const [threshold, result] of Object.entries(details.thresholds)) {
        const status = result.ok ? '✅' : '❌';
        if (!result.ok) allPassed = false;
        output += `${indent}${status} ${metric}: ${threshold}\n`;
      }
    }
  }

  // Final result
  output += `\n${indent}╔═══════════════════════════════════════════════════════════╗\n`;
  if (allPassed) {
    output += `${indent}║  ✅ RESULT: PASSED - All thresholds met                  ║\n`;
  } else {
    output += `${indent}║  ❌ RESULT: FAILED - Some thresholds not met             ║\n`;
  }
  output += `${indent}╚═══════════════════════════════════════════════════════════╝\n`;

  return output;
}
