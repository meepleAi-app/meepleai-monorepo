/**
 * Session Cache Metrics Manager
 *
 * Collects metrics about proxy session cache performance
 * for monitoring via Prometheus endpoint.
 *
 * Issue #3797: Created to monitor cache effectiveness and
 * detect API performance issues early.
 */

// Metrics counters
let cacheHitCount = 0;
let cacheMissCount = 0;
let validationSuccessCount = 0;
let validationFailureCount = 0;
let validationTimeoutCount = 0;

// Session start time for uptime calculation
const startTime = Date.now();

/**
 * Record a cache hit event
 */
export function recordCacheHit(): void {
  cacheHitCount++;
}

/**
 * Record a cache miss event
 */
export function recordCacheMiss(): void {
  cacheMissCount++;
}

/**
 * Record a successful session validation
 */
export function recordValidationSuccess(): void {
  validationSuccessCount++;
}

/**
 * Record a failed session validation
 */
export function recordValidationFailure(): void {
  validationFailureCount++;
}

/**
 * Record a session validation timeout
 */
export function recordValidationTimeout(): void {
  validationTimeoutCount++;
}

/**
 * Get current metrics snapshot
 */
export function getMetrics() {
  const totalRequests = cacheHitCount + cacheMissCount;
  const cacheHitRate = totalRequests > 0 ? cacheHitCount / totalRequests : 0;
  const totalValidations = validationSuccessCount + validationFailureCount + validationTimeoutCount;
  const validationSuccessRate = totalValidations > 0 ? validationSuccessCount / totalValidations : 0;
  const uptimeSeconds = Math.floor((Date.now() - startTime) / 1000);

  return {
    // Cache metrics
    cache_hit_total: cacheHitCount,
    cache_miss_total: cacheMissCount,
    cache_hit_rate: cacheHitRate,

    // Validation metrics
    validation_success_total: validationSuccessCount,
    validation_failure_total: validationFailureCount,
    validation_timeout_total: validationTimeoutCount,
    validation_success_rate: validationSuccessRate,

    // System metrics
    uptime_seconds: uptimeSeconds,
    total_requests: totalRequests,
    total_validations: totalValidations,
  };
}

/**
 * Format metrics in Prometheus text format
 */
export function getPrometheusMetrics(): string {
  const metrics = getMetrics();

  return `# HELP nextjs_middleware_cache_hit_total Total number of session cache hits
# TYPE nextjs_middleware_cache_hit_total counter
nextjs_middleware_cache_hit_total ${metrics.cache_hit_total}

# HELP nextjs_middleware_cache_miss_total Total number of session cache misses
# TYPE nextjs_middleware_cache_miss_total counter
nextjs_middleware_cache_miss_total ${metrics.cache_miss_total}

# HELP nextjs_middleware_cache_hit_rate Session cache hit rate
# TYPE nextjs_middleware_cache_hit_rate gauge
nextjs_middleware_cache_hit_rate ${metrics.cache_hit_rate.toFixed(4)}

# HELP nextjs_middleware_validation_success_total Successful session validations
# TYPE nextjs_middleware_validation_success_total counter
nextjs_middleware_validation_success_total ${metrics.validation_success_total}

# HELP nextjs_middleware_validation_failure_total Failed session validations
# TYPE nextjs_middleware_validation_failure_total counter
nextjs_middleware_validation_failure_total ${metrics.validation_failure_total}

# HELP nextjs_middleware_validation_timeout_total Session validation timeouts
# TYPE nextjs_middleware_validation_timeout_total counter
nextjs_middleware_validation_timeout_total ${metrics.validation_timeout_total}

# HELP nextjs_middleware_validation_success_rate Session validation success rate
# TYPE nextjs_middleware_validation_success_rate gauge
nextjs_middleware_validation_success_rate ${metrics.validation_success_rate.toFixed(4)}

# HELP nextjs_middleware_uptime_seconds Middleware uptime in seconds
# TYPE nextjs_middleware_uptime_seconds counter
nextjs_middleware_uptime_seconds ${metrics.uptime_seconds}

# HELP nextjs_middleware_total_requests Total middleware requests processed
# TYPE nextjs_middleware_total_requests counter
nextjs_middleware_total_requests ${metrics.total_requests}
`;
}

/**
 * Reset all metrics (for testing purposes)
 */
export function resetMetrics(): void {
  cacheHitCount = 0;
  cacheMissCount = 0;
  validationSuccessCount = 0;
  validationFailureCount = 0;
  validationTimeoutCount = 0;
}
