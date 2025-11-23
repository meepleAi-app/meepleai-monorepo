/**
 * API Core Utilities - Barrel Export
 *
 * Central export point for core API utilities including:
 * - Circuit breaker pattern
 * - Retry metrics tracking
 * - Prometheus metrics formatting (with security)
 */

// Circuit Breaker
export {
  CircuitState,
  type CircuitBreakerConfig,
  getCircuitBreakerConfig,
  canExecute,
  recordSuccess,
  recordFailure,
  resetCircuit,
  resetAllCircuits,
  getCircuitMetrics,
  getAllCircuitMetrics,
  exportCircuitBreakerMetrics,
} from './circuitBreaker';

// Retry Metrics
export {
  type RetryMetrics,
  recordRetryAttempt,
  recordRetrySuccess,
  recordRetryFailure,
  getRetryMetrics,
  resetRetryMetrics,
  exportPrometheusMetrics,
} from './metrics';

// Prometheus Utilities (Security)
export {
  MAX_LABEL_VALUE_LENGTH,
  escapePrometheusLabelValue,
  sanitizePrometheusName,
  formatPrometheusMetric,
  isPrometheusLabelValueSafe,
} from './prometheusUtils';
