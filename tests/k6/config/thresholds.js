/**
 * Performance Thresholds Configuration
 *
 * Defines SLA targets for all API endpoints.
 * Tests fail if any threshold is exceeded.
 *
 * Issue #873
 * Updated: Relaxed thresholds for CI/CD environment (Issue #1599)
 */

export const thresholds = {
  // RAG Search: < 3s p95 latency (relaxed for CI/CD)
  'http_req_duration{endpoint:rag-search}': ['p(95)<3000', 'p(99)<5000'],

  // Chat Messaging: < 1.5s p95 latency (relaxed for CI/CD)
  'http_req_duration{endpoint:chat}': ['p(95)<1500', 'p(99)<2500'],

  // Game Search: < 800ms p95 latency (relaxed for CI/CD)
  'http_req_duration{endpoint:games}': ['p(95)<800', 'p(99)<1200'],

  // Session Updates: < 200ms p95 latency (relaxed for CI/CD)
  'http_req_duration{endpoint:sessions}': ['p(95)<200', 'p(99)<400'],

  // Database Operations: < 300ms p95 (relaxed for CI/CD)
  'http_req_duration{endpoint:database}': ['p(95)<300', 'p(99)<700'],

  // Redis Cache: < 100ms p95 (relaxed for CI/CD)
  'http_req_duration{endpoint:redis}': ['p(95)<100', 'p(99)<200'],

  // Overall HTTP metrics
  'http_req_failed': ['rate<0.02'], // < 2% error rate (relaxed for CI/CD)
  'http_req_duration': ['p(95)<3000'], // Overall 95th percentile (relaxed)

  // Custom metrics
  'rag_confidence': ['avg>0.65'], // RAG confidence > 65% (relaxed for CI/CD)
  'cache_hit_rate': ['rate>0.70'], // Cache hit rate > 70% (relaxed for CI/CD)
};

export const throughputTargets = {
  'rag-search': 1000, // req/s
  'chat': 500,        // req/s
  'games': 2000,      // req/s
  'sessions': 1000,   // req/s
};

export default thresholds;
