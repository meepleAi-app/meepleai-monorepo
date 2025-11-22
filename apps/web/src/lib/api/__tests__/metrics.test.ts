/**
 * Metrics Tests (Issue #1453)
 *
 * Tests for Prometheus-compatible retry metrics tracking.
 */

import {
  recordRetryAttempt,
  recordRetrySuccess,
  recordRetryFailure,
  getRetryMetrics,
  resetRetryMetrics,
  exportPrometheusMetrics,
} from '../core/metrics';

describe('Retry Metrics', () => {
  beforeEach(() => {
    resetRetryMetrics();
  });

  describe('recordRetryAttempt', () => {
    it('should increment total retries', () => {
      recordRetryAttempt('/api/test', 500, 1000);

      const metrics = getRetryMetrics();
      expect(metrics.totalRetries).toBe(1);
    });

    it('should track retries by status code', () => {
      recordRetryAttempt('/api/test', 500, 1000);
      recordRetryAttempt('/api/test', 502, 2000);
      recordRetryAttempt('/api/test', 500, 1500);

      const metrics = getRetryMetrics();
      expect(metrics.retriesByStatusCode[500]).toBe(2);
      expect(metrics.retriesByStatusCode[502]).toBe(1);
    });

    it('should track retries by endpoint', () => {
      recordRetryAttempt('/api/test1', 500, 1000);
      recordRetryAttempt('/api/test2', 500, 1000);
      recordRetryAttempt('/api/test1', 500, 1000);

      const metrics = getRetryMetrics();
      expect(metrics.retriesByEndpoint['/api/test1']).toBe(2);
      expect(metrics.retriesByEndpoint['/api/test2']).toBe(1);
    });

    it('should track network errors with status code 0', () => {
      recordRetryAttempt('/api/test', undefined, 1000);

      const metrics = getRetryMetrics();
      expect(metrics.retriesByStatusCode[0]).toBe(1);
    });

    it('should calculate average retry delay', () => {
      recordRetryAttempt('/api/test', 500, 1000);
      recordRetryAttempt('/api/test', 500, 2000);
      recordRetryAttempt('/api/test', 500, 3000);

      const metrics = getRetryMetrics();
      expect(metrics.avgRetryDelayMs).toBe(2000); // (1000 + 2000 + 3000) / 3
    });

    it('should track total retry delay', () => {
      recordRetryAttempt('/api/test', 500, 1000);
      recordRetryAttempt('/api/test', 500, 2000);

      const metrics = getRetryMetrics();
      expect(metrics.totalRetryDelayMs).toBe(3000);
    });
  });

  describe('recordRetrySuccess', () => {
    it('should increment success after retry counter', () => {
      recordRetrySuccess();

      const metrics = getRetryMetrics();
      expect(metrics.successAfterRetry).toBe(1);
    });

    it('should track multiple successes', () => {
      recordRetrySuccess();
      recordRetrySuccess();
      recordRetrySuccess();

      const metrics = getRetryMetrics();
      expect(metrics.successAfterRetry).toBe(3);
    });
  });

  describe('recordRetryFailure', () => {
    it('should increment failure after retry counter', () => {
      recordRetryFailure();

      const metrics = getRetryMetrics();
      expect(metrics.failedAfterRetry).toBe(1);
    });

    it('should track multiple failures', () => {
      recordRetryFailure();
      recordRetryFailure();

      const metrics = getRetryMetrics();
      expect(metrics.failedAfterRetry).toBe(2);
    });
  });

  describe('getRetryMetrics', () => {
    it('should return immutable snapshot of metrics', () => {
      recordRetryAttempt('/api/test', 500, 1000);

      const metrics1 = getRetryMetrics();
      recordRetryAttempt('/api/test', 500, 1000);
      const metrics2 = getRetryMetrics();

      expect(metrics1.totalRetries).toBe(1);
      expect(metrics2.totalRetries).toBe(2);
    });

    it('should return all metrics fields', () => {
      const metrics = getRetryMetrics();

      expect(metrics).toHaveProperty('totalRetries');
      expect(metrics).toHaveProperty('successAfterRetry');
      expect(metrics).toHaveProperty('failedAfterRetry');
      expect(metrics).toHaveProperty('retriesByStatusCode');
      expect(metrics).toHaveProperty('retriesByEndpoint');
      expect(metrics).toHaveProperty('avgRetryDelayMs');
      expect(metrics).toHaveProperty('totalRetryDelayMs');
    });
  });

  describe('resetRetryMetrics', () => {
    it('should reset all metrics to zero', () => {
      recordRetryAttempt('/api/test', 500, 1000);
      recordRetrySuccess();
      recordRetryFailure();

      resetRetryMetrics();

      const metrics = getRetryMetrics();
      expect(metrics.totalRetries).toBe(0);
      expect(metrics.successAfterRetry).toBe(0);
      expect(metrics.failedAfterRetry).toBe(0);
      expect(metrics.retriesByStatusCode).toEqual({});
      expect(metrics.retriesByEndpoint).toEqual({});
      expect(metrics.avgRetryDelayMs).toBe(0);
      expect(metrics.totalRetryDelayMs).toBe(0);
    });
  });

  describe('exportPrometheusMetrics', () => {
    it('should export metrics in Prometheus format', () => {
      recordRetryAttempt('/api/test', 500, 1000);
      recordRetrySuccess();

      const output = exportPrometheusMetrics();

      expect(output).toContain('# HELP http_client_retries_total');
      expect(output).toContain('# TYPE http_client_retries_total counter');
      expect(output).toContain('http_client_retries_total 1');
    });

    it('should export success counter', () => {
      recordRetrySuccess();
      recordRetrySuccess();

      const output = exportPrometheusMetrics();

      expect(output).toContain('# HELP http_client_success_after_retry_total');
      expect(output).toContain('http_client_success_after_retry_total 2');
    });

    it('should export failure counter', () => {
      recordRetryFailure();

      const output = exportPrometheusMetrics();

      expect(output).toContain('# HELP http_client_failed_after_retry_total');
      expect(output).toContain('http_client_failed_after_retry_total 1');
    });

    it('should export retries by status code', () => {
      recordRetryAttempt('/api/test', 500, 1000);
      recordRetryAttempt('/api/test', 502, 1000);

      const output = exportPrometheusMetrics();

      expect(output).toContain('# HELP http_client_retries_by_status');
      expect(output).toContain('http_client_retries_by_status{status_code="500"} 1');
      expect(output).toContain('http_client_retries_by_status{status_code="502"} 1');
    });

    it('should export retries by endpoint', () => {
      recordRetryAttempt('/api/test1', 500, 1000);
      recordRetryAttempt('/api/test2', 500, 1000);

      const output = exportPrometheusMetrics();

      expect(output).toContain('# HELP http_client_retries_by_endpoint');
      expect(output).toContain('http_client_retries_by_endpoint{endpoint="/api/test1"} 1');
      expect(output).toContain('http_client_retries_by_endpoint{endpoint="/api/test2"} 1');
    });

    it('should export average retry delay', () => {
      recordRetryAttempt('/api/test', 500, 1000);
      recordRetryAttempt('/api/test', 500, 2000);

      const output = exportPrometheusMetrics();

      expect(output).toContain('# HELP http_client_retry_delay_avg_ms');
      expect(output).toContain('# TYPE http_client_retry_delay_avg_ms gauge');
      expect(output).toContain('http_client_retry_delay_avg_ms 1500.00');
    });

    it('should export total retry delay', () => {
      recordRetryAttempt('/api/test', 500, 1000);
      recordRetryAttempt('/api/test', 500, 2000);

      const output = exportPrometheusMetrics();

      expect(output).toContain('# HELP http_client_retry_delay_total_ms');
      expect(output).toContain('http_client_retry_delay_total_ms 3000');
    });

    it('should escape endpoint paths for Prometheus labels', () => {
      recordRetryAttempt('/api/test"with"quotes', 500, 1000);

      const output = exportPrometheusMetrics();

      expect(output).toContain('http_client_retries_by_endpoint{endpoint="/api/test\\"with\\"quotes"} 1');
    });

    it('should end with newline', () => {
      const output = exportPrometheusMetrics();

      expect(output).toMatch(/\n$/);
    });
  });

  describe('integration scenarios', () => {
    it('should track complete retry flow with success', () => {
      // Simulate 2 retries before success
      recordRetryAttempt('/api/games', 500, 1000);
      recordRetryAttempt('/api/games', 500, 2000);
      recordRetrySuccess();

      const metrics = getRetryMetrics();
      expect(metrics.totalRetries).toBe(2);
      expect(metrics.successAfterRetry).toBe(1);
      expect(metrics.failedAfterRetry).toBe(0);
      expect(metrics.avgRetryDelayMs).toBe(1500);
    });

    it('should track complete retry flow with failure', () => {
      // Simulate 3 retries then failure
      recordRetryAttempt('/api/games', 503, 1000);
      recordRetryAttempt('/api/games', 503, 2000);
      recordRetryAttempt('/api/games', 503, 4000);
      recordRetryFailure();

      const metrics = getRetryMetrics();
      expect(metrics.totalRetries).toBe(3);
      expect(metrics.successAfterRetry).toBe(0);
      expect(metrics.failedAfterRetry).toBe(1);
      expect(metrics.avgRetryDelayMs).toBeCloseTo(2333.33, 1);
    });

    it('should track multiple concurrent endpoints', () => {
      recordRetryAttempt('/api/games', 500, 1000);
      recordRetryAttempt('/api/chat', 502, 1500);
      recordRetryAttempt('/api/games', 500, 2000);
      recordRetrySuccess();
      recordRetrySuccess();

      const metrics = getRetryMetrics();
      expect(metrics.totalRetries).toBe(3);
      expect(metrics.successAfterRetry).toBe(2);
      expect(metrics.retriesByEndpoint['/api/games']).toBe(2);
      expect(metrics.retriesByEndpoint['/api/chat']).toBe(1);
    });
  });
});
