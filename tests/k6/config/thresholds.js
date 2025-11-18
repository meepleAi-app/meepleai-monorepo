/**
 * Performance Thresholds Configuration
 *
 * Defines SLA targets for all API endpoints.
 * Tests fail if any threshold is exceeded.
 *
 * Issue #873
 */

export const thresholds = {
  // RAG Search: < 2s p95 latency
  'http_req_duration{endpoint:rag-search}': ['p(95)<2000', 'p(99)<3000'],

  // Chat Messaging: < 1s p95 latency
  'http_req_duration{endpoint:chat}': ['p(95)<1000', 'p(99)<1500'],

  // Game Search: < 500ms p95 latency
  'http_req_duration{endpoint:games}': ['p(95)<500', 'p(99)<800'],

  // Session Updates: < 100ms p95 latency
  'http_req_duration{endpoint:sessions}': ['p(95)<100', 'p(99)<200'],

  // Database Operations: < 200ms p95
  'http_req_duration{endpoint:database}': ['p(95)<200', 'p(99)<500'],

  // Redis Cache: < 50ms p95
  'http_req_duration{endpoint:redis}': ['p(95)<50', 'p(99)<100'],

  // Overall HTTP metrics
  'http_req_failed': ['rate<0.01'], // < 1% error rate
  'http_req_duration': ['p(95)<2000'], // Overall 95th percentile

  // Custom metrics
  'rag_confidence': ['avg>0.70'], // RAG confidence > 70%
  'cache_hit_rate': ['rate>0.80'], // Cache hit rate > 80%
};

export const throughputTargets = {
  'rag-search': 1000, // req/s
  'chat': 500,        // req/s
  'games': 2000,      // req/s
  'sessions': 1000,   // req/s
};

export default thresholds;
