/**
 * Performance Thresholds Configuration
 *
 * Defines SLA targets for all API endpoints with dynamic adjustment based on test type.
 *
 * Issue #873, #1599, #1629
 *
 * Test Types:
 * - smoke: Infrastructure validation, relaxed thresholds (no external services required)
 * - load/stress/spike: Comprehensive testing with strict thresholds
 */

/**
 * Get thresholds based on test type
 * @param {string} testType - Test type: smoke, load, stress, spike
 * @returns {object} Thresholds configuration for k6
 */
export function getThresholds(testType = 'load') {
  // Base thresholds for all test types
  const baseThresholds = {
    // RAG Search: < 3s p95 latency
    'http_req_duration{endpoint:rag-search}': ['p(95)<3000', 'p(99)<5000'],

    // Chat Messaging: < 1.5s p95 latency
    'http_req_duration{endpoint:chat}': ['p(95)<1500', 'p(99)<2500'],

    // Game Search: < 800ms p95 latency
    'http_req_duration{endpoint:games}': ['p(95)<800', 'p(99)<1200'],

    // Session Updates: < 200ms p95 latency
    'http_req_duration{endpoint:sessions}': ['p(95)<200', 'p(99)<400'],

    // Database Operations: < 300ms p95
    'http_req_duration{endpoint:database}': ['p(95)<300', 'p(99)<700'],

    // Redis Cache: < 100ms p95
    'http_req_duration{endpoint:redis}': ['p(95)<100', 'p(99)<200'],

    // Overall HTTP duration
    'http_req_duration': ['p(95)<3000'],

    // Cache hit rate
    'cache_hit_rate': ['rate>0.70'],
  };

  // Smoke tests: Infrastructure validation only
  // Relaxed thresholds as external services (Ollama, n8n) are not required
  if (testType === 'smoke') {
    console.log('🔵 Using SMOKE test thresholds (relaxed, no external services)');
    return {
      ...baseThresholds,
      'http_req_failed': ['rate<0.10'], // 10% tolerable (external service failures expected)
      // Skip rag_confidence (requires Ollama for embeddings)
    };
  }

  // Load/Stress/Spike: Comprehensive testing with strict thresholds
  // Requires all services including Ollama and n8n
  console.log(`🔴 Using ${testType.toUpperCase()} test thresholds (strict, all services required)`);
  return {
    ...baseThresholds,
    'http_req_failed': ['rate<0.02'], // < 2% error rate (strict)
    'rag_confidence': ['avg>0.65'],   // RAG confidence > 65% (strict)
  };
}

/**
 * Legacy export for backward compatibility
 * Uses 'load' test type by default
 * @deprecated Use getThresholds(__ENV.TEST_TYPE) instead
 */
export const thresholds = getThresholds('load');

export const throughputTargets = {
  'rag-search': 1000, // req/s
  'chat': 500,        // req/s
  'games': 2000,      // req/s
  'sessions': 1000,   // req/s
};

export default getThresholds;
