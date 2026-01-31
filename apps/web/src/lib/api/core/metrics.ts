/**
 * Metrics Tracking for Retry Logic (Issue #1453)
 *
 * Provides Prometheus-compatible metrics for monitoring HTTP retry behavior.
 * Metrics are stored in-memory and can be exposed via a metrics endpoint.
 */

import { escapePrometheusLabelValue } from './prometheusUtils';

export interface RetryMetrics {
  /** Total number of retry attempts made */
  totalRetries: number;
  /** Number of requests that succeeded after retry */
  successAfterRetry: number;
  /** Number of requests that failed after all retries */
  failedAfterRetry: number;
  /** Retry attempts by status code */
  retriesByStatusCode: Record<number, number>;
  /** Retry attempts by endpoint */
  retriesByEndpoint: Record<string, number>;
  /** Average retry delay in milliseconds */
  avgRetryDelayMs: number;
  /** Total retry delay in milliseconds */
  totalRetryDelayMs: number;
}

/**
 * In-memory metrics storage
 */
class MetricsCollector {
  private metrics: RetryMetrics = {
    totalRetries: 0,
    successAfterRetry: 0,
    failedAfterRetry: 0,
    retriesByStatusCode: {},
    retriesByEndpoint: {},
    avgRetryDelayMs: 0,
    totalRetryDelayMs: 0,
  };

  /**
   * Record a retry attempt
   */
  recordRetry(
    endpoint: string,
    statusCode: number | undefined,
    delayMs: number
  ): void {
    this.metrics.totalRetries++;
    this.metrics.totalRetryDelayMs += delayMs;
    this.metrics.avgRetryDelayMs =
      this.metrics.totalRetryDelayMs / this.metrics.totalRetries;

    // Track by status code (0 for network errors)
    const code = statusCode || 0;
    // Safe: code is a validated number, not user-controlled string
    // eslint-disable-next-line security/detect-object-injection
    this.metrics.retriesByStatusCode[code] =
      // eslint-disable-next-line security/detect-object-injection
      (this.metrics.retriesByStatusCode[code] || 0) + 1;

    // Track by endpoint
    // Safe: endpoint is used for aggregation only, values are sanitized before output
    // eslint-disable-next-line security/detect-object-injection
    this.metrics.retriesByEndpoint[endpoint] =
      // eslint-disable-next-line security/detect-object-injection
      (this.metrics.retriesByEndpoint[endpoint] || 0) + 1;
  }

  /**
   * Record a successful request after retry
   */
  recordSuccessAfterRetry(): void {
    this.metrics.successAfterRetry++;
  }

  /**
   * Record a failed request after all retries exhausted
   */
  recordFailureAfterRetry(): void {
    this.metrics.failedAfterRetry++;
  }

  /**
   * Get current metrics snapshot
   */
  getMetrics(): Readonly<RetryMetrics> {
    return { ...this.metrics };
  }

  /**
   * Reset all metrics (useful for testing)
   */
  reset(): void {
    this.metrics = {
      totalRetries: 0,
      successAfterRetry: 0,
      failedAfterRetry: 0,
      retriesByStatusCode: {},
      retriesByEndpoint: {},
      avgRetryDelayMs: 0,
      totalRetryDelayMs: 0,
    };
  }

  /**
   * Export metrics in Prometheus format
   */
  toPrometheusFormat(): string {
    const lines: string[] = [];

    // Total retries counter
    lines.push('# HELP http_client_retries_total Total number of retry attempts');
    lines.push('# TYPE http_client_retries_total counter');
    lines.push(`http_client_retries_total ${this.metrics.totalRetries}`);

    // Success after retry counter
    lines.push('# HELP http_client_success_after_retry_total Successful requests after retry');
    lines.push('# TYPE http_client_success_after_retry_total counter');
    lines.push(`http_client_success_after_retry_total ${this.metrics.successAfterRetry}`);

    // Failed after retry counter
    lines.push('# HELP http_client_failed_after_retry_total Failed requests after all retries');
    lines.push('# TYPE http_client_failed_after_retry_total counter');
    lines.push(`http_client_failed_after_retry_total ${this.metrics.failedAfterRetry}`);

    // Retries by status code
    lines.push('# HELP http_client_retries_by_status Retry attempts by HTTP status code');
    lines.push('# TYPE http_client_retries_by_status counter');
    Object.entries(this.metrics.retriesByStatusCode).forEach(([code, count]) => {
      lines.push(`http_client_retries_by_status{status_code="${code}"} ${count}`);
    });

    // Retries by endpoint
    lines.push('# HELP http_client_retries_by_endpoint Retry attempts by endpoint');
    lines.push('# TYPE http_client_retries_by_endpoint counter');
    Object.entries(this.metrics.retriesByEndpoint).forEach(([endpoint, count]) => {
      // Escape endpoint path for Prometheus label (prevents CWE-116: incomplete sanitization)
      const escapedEndpoint = escapePrometheusLabelValue(endpoint);
      lines.push(`http_client_retries_by_endpoint{endpoint="${escapedEndpoint}"} ${count}`);
    });

    // Average retry delay gauge
    lines.push('# HELP http_client_retry_delay_avg_ms Average retry delay in milliseconds');
    lines.push('# TYPE http_client_retry_delay_avg_ms gauge');
    lines.push(`http_client_retry_delay_avg_ms ${this.metrics.avgRetryDelayMs.toFixed(2)}`);

    // Total retry delay counter
    lines.push('# HELP http_client_retry_delay_total_ms Total retry delay in milliseconds');
    lines.push('# TYPE http_client_retry_delay_total_ms counter');
    lines.push(`http_client_retry_delay_total_ms ${this.metrics.totalRetryDelayMs}`);

    return lines.join('\n') + '\n';
  }
}

/**
 * Global metrics collector instance
 */
const metricsCollector = new MetricsCollector();

/**
 * Record a retry attempt with metrics
 */
export function recordRetryAttempt(
  endpoint: string,
  statusCode: number | undefined,
  delayMs: number
): void {
  metricsCollector.recordRetry(endpoint, statusCode, delayMs);
}

/**
 * Record a successful request after retry
 */
export function recordRetrySuccess(): void {
  metricsCollector.recordSuccessAfterRetry();
}

/**
 * Record a failed request after all retries exhausted
 */
export function recordRetryFailure(): void {
  metricsCollector.recordFailureAfterRetry();
}

/**
 * Get current retry metrics
 */
export function getRetryMetrics(): Readonly<RetryMetrics> {
  return metricsCollector.getMetrics();
}

/**
 * Reset all retry metrics (useful for testing)
 */
export function resetRetryMetrics(): void {
  metricsCollector.reset();
}

/**
 * Export metrics in Prometheus format
 */
export function exportPrometheusMetrics(): string {
  return metricsCollector.toPrometheusFormat();
}
