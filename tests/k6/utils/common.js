/**
 * Common utilities for k6 tests
 *
 * Shared functions for configuration, logging, and data generation.
 */

import { SharedArray } from 'k6/data';
import { check, sleep } from 'k6';

/**
 * Load environment configuration
 * Prioritizes environment variables over config file
 */
export function loadConfig() {
  const defaultConfig = {
    apiBaseUrl: __ENV.API_BASE_URL || 'http://localhost:8080',
    testUser: {
      // Test credentials - users should be created programmatically in test setup
      email: __ENV.TEST_USER_EMAIL || 'k6-test-user@example.com',
      password: __ENV.TEST_USER_PASSWORD || 'K6TestPassword123!',
    },
    testGameId: __ENV.TEST_GAME_ID || '00000000-0000-0000-0000-000000000001',
    reportDir: __ENV.REPORT_DIR || './reports',
    websocketUrl: __ENV.WEBSOCKET_URL || 'ws://localhost:8080/ws',
  };

  // Note: k6 doesn't support file loading at runtime reliably
  // Use environment variables instead for configuration
  return defaultConfig;
}

/**
 * Get test type from environment (smoke, load, stress, spike)
 */
export function getTestType() {
  return __ENV.TEST_TYPE || 'load';
}

/**
 * Common HTTP headers
 */
export function getHeaders(sessionToken = null) {
  const headers = {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  };

  if (sessionToken) {
    headers['Cookie'] = `meepleai_session=${sessionToken}`;
  }

  return headers;
}

/**
 * Shared test queries for RAG/Chat
 */
export const testQueries = new SharedArray('test-queries', function () {
  return [
    'How do I set up the game?',
    'What are the winning conditions?',
    'How many players can play?',
    'What is the objective of the game?',
    'How do turns work?',
    'Can I trade resources with other players?',
    'What happens when I run out of cards?',
    'How long does a typical game last?',
    'What are the different phases of a turn?',
    'Can I play this game solo?',
  ];
});

/**
 * Random query selector
 */
export function getRandomQuery() {
  return testQueries[Math.floor(Math.random() * testQueries.length)];
}

/**
 * Common response validation
 */
export function validateResponse(response, expectedStatus = 200) {
  const checks = {
    'status is correct': response.status === expectedStatus,
    'response time < 5s': response.timings.duration < 5000,
  };

  if (response.status === 200) {
    checks['has body'] = response.body && response.body.length > 0;

    try {
      const body = JSON.parse(response.body);
      checks['valid JSON'] = true;

      if (body.error) {
        checks['no error in response'] = false;
      }
    } catch (e) {
      checks['valid JSON'] = false;
    }
  }

  return check(response, checks);
}

/**
 * Generate load profile based on test type
 */
export function getLoadProfile(testType, baseVUs = 50) {
  const profiles = {
    smoke: {
      stages: [
        { duration: '30s', target: 1 },
        { duration: '30s', target: 1 },
      ],
    },
    load: {
      stages: [
        { duration: '2m', target: baseVUs },
        { duration: '5m', target: baseVUs },
        { duration: '2m', target: 0 },
      ],
    },
    stress: {
      stages: [
        { duration: '2m', target: baseVUs },
        { duration: '5m', target: baseVUs * 2 },
        { duration: '5m', target: baseVUs * 4 },
        { duration: '2m', target: 0 },
      ],
    },
    spike: {
      stages: [
        { duration: '1m', target: baseVUs },
        { duration: '10s', target: baseVUs * 10 }, // Sudden spike
        { duration: '3m', target: baseVUs * 10 },
        { duration: '10s', target: baseVUs },
        { duration: '1m', target: 0 },
      ],
    },
  };

  return profiles[testType] || profiles.load;
}

/**
 * Sleep with jitter to avoid thundering herd
 */
export function sleepWithJitter(baseMs = 1000, jitterMs = 200) {
  const actualSleep = baseMs + (Math.random() * jitterMs * 2 - jitterMs);
  sleep(actualSleep / 1000); // k6 sleep takes seconds
}

/**
 * Retry HTTP request with exponential backoff for rate limit handling (Issue #1663)
 * @param {function} requestFn - Function that makes the HTTP request
 * @param {object} options - Retry options
 * @returns {object} HTTP response
 */
export function retryWithBackoff(requestFn, options = {}) {
  const maxRetries = options.maxRetries || 3;
  const initialDelayMs = options.initialDelayMs || 1000;
  const maxDelayMs = options.maxDelayMs || 5000;
  const retryOn = options.retryOn || [429, 503]; // Rate limit + service unavailable

  let lastResponse = null;

  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    lastResponse = requestFn();

    // Success - return immediately
    if (!retryOn.includes(lastResponse.status)) {
      return lastResponse;
    }

    // Max retries reached
    if (attempt === maxRetries) {
      console.warn(`Max retries (${maxRetries}) reached for request. Last status: ${lastResponse.status}`);
      return lastResponse;
    }

    // Calculate backoff delay with exponential increase
    const delayMs = Math.min(initialDelayMs * Math.pow(2, attempt), maxDelayMs);

    // Check for Retry-After header
    const retryAfter = lastResponse.headers['Retry-After'];
    const actualDelayMs = retryAfter ? parseInt(retryAfter) * 1000 : delayMs;

    console.log(`Request failed with ${lastResponse.status}. Retrying in ${actualDelayMs}ms (attempt ${attempt + 1}/${maxRetries})`);
    sleep(actualDelayMs / 1000);
  }

  return lastResponse;
}

/**
 * Format bytes for logging
 */
export function formatBytes(bytes) {
  if (bytes < 1024) return bytes + ' B';
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB';
  return (bytes / 1024 / 1024).toFixed(2) + ' MB';
}

/**
 * Log test summary
 */
export function logSummary(scenario, metrics) {
  console.log(`\n=== ${scenario} Summary ===`);
  console.log(`Requests: ${metrics.requests || 0}`);
  console.log(`Failures: ${metrics.failures || 0}`);
  console.log(`Avg Duration: ${metrics.avgDuration || 0}ms`);
  console.log(`P95 Duration: ${metrics.p95Duration || 0}ms`);
  console.log(`Error Rate: ${((metrics.failures / metrics.requests) * 100 || 0).toFixed(2)}%`);
  console.log('===========================\n');
}
