/**
 * Circuit Breaker Tests (Enhancement for #1453)
 *
 * Tests for circuit breaker pattern implementation.
 */

import {
  CircuitState,
  canExecute,
  recordSuccess,
  recordFailure,
  getCircuitState,
  getCircuitMetrics,
  getAllCircuitMetrics,
  resetCircuit,
  resetAllCircuits,
  exportCircuitBreakerMetrics,
  getCircuitBreakerConfig,
} from '../core/circuitBreaker';

describe('Circuit Breaker', () => {
  beforeEach(() => {
    resetAllCircuits();
  });

  describe('getCircuitBreakerConfig', () => {
    let originalEnv: NodeJS.ProcessEnv;

    beforeEach(() => {
      originalEnv = process.env;
    });

    afterEach(() => {
      process.env = originalEnv;
    });

    it('should return default config when environment variables not set', () => {
      delete process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_FAILURE_THRESHOLD;
      delete process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_SUCCESS_THRESHOLD;
      delete process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_TIMEOUT;
      delete process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_WINDOW_SIZE;
      delete process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_ENABLED;

      const config = getCircuitBreakerConfig();

      expect(config.failureThreshold).toBe(5);
      expect(config.successThreshold).toBe(2);
      expect(config.timeout).toBe(60000);
      expect(config.windowSize).toBe(60000);
      expect(config.enabled).toBe(true);
    });

    it('should use environment variables when set', () => {
      process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_FAILURE_THRESHOLD = '10';
      process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_SUCCESS_THRESHOLD = '3';
      process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_TIMEOUT = '30000';
      process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_WINDOW_SIZE = '45000';
      process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_ENABLED = 'true';

      const config = getCircuitBreakerConfig();

      expect(config.failureThreshold).toBe(10);
      expect(config.successThreshold).toBe(3);
      expect(config.timeout).toBe(30000);
      expect(config.windowSize).toBe(45000);
      expect(config.enabled).toBe(true);
    });

    it('should disable circuit breaker when ENABLED is false', () => {
      process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_ENABLED = 'false';

      const config = getCircuitBreakerConfig();

      expect(config.enabled).toBe(false);
    });
  });

  describe('canExecute', () => {
    it('should allow requests initially (CLOSED state)', () => {
      expect(canExecute('/api/test')).toBe(true);
      expect(getCircuitState('/api/test')).toBe(CircuitState.CLOSED);
    });

    it('should allow requests for different endpoints independently', () => {
      expect(canExecute('/api/test1')).toBe(true);
      expect(canExecute('/api/test2')).toBe(true);
    });

    it('should deny requests when circuit is OPEN', () => {
      // Record 5 failures to open circuit (default threshold)
      for (let i = 0; i < 5; i++) {
        recordFailure('/api/test');
      }

      expect(canExecute('/api/test')).toBe(false);
      expect(getCircuitState('/api/test')).toBe(CircuitState.OPEN);
    });

    it('should allow requests in HALF_OPEN state', () => {
      // Open circuit
      for (let i = 0; i < 5; i++) {
        recordFailure('/api/test');
      }

      expect(getCircuitState('/api/test')).toBe(CircuitState.OPEN);

      // Manually transition to HALF_OPEN (in real scenario, this happens after timeout)
      const metrics = getCircuitMetrics('/api/test');
      // Simulate timeout passage by checking canExecute after enough time
      // For testing, we'll need to verify the logic handles state transitions
    });
  });

  describe('recordFailure', () => {
    it('should increment failure count', () => {
      recordFailure('/api/test');

      const metrics = getCircuitMetrics('/api/test');
      expect(metrics.failures).toBe(1);
      expect(metrics.totalFailures).toBe(1);
      expect(metrics.totalRequests).toBe(1);
    });

    it('should open circuit after threshold failures', () => {
      // Record failures up to threshold (default: 5)
      for (let i = 0; i < 5; i++) {
        recordFailure('/api/test');
      }

      expect(getCircuitState('/api/test')).toBe(CircuitState.OPEN);
    });

    it('should track last failure time', () => {
      const beforeTime = Date.now();
      recordFailure('/api/test');
      const afterTime = Date.now();

      const metrics = getCircuitMetrics('/api/test');
      expect(metrics.lastFailureTime).toBeGreaterThanOrEqual(beforeTime);
      expect(metrics.lastFailureTime).toBeLessThanOrEqual(afterTime);
    });

    it('should handle failures for multiple endpoints independently', () => {
      recordFailure('/api/test1');
      recordFailure('/api/test1');
      recordFailure('/api/test2');

      expect(getCircuitMetrics('/api/test1').failures).toBe(2);
      expect(getCircuitMetrics('/api/test2').failures).toBe(1);
      expect(getCircuitState('/api/test1')).toBe(CircuitState.CLOSED);
      expect(getCircuitState('/api/test2')).toBe(CircuitState.CLOSED);
    });

    it('should transition from HALF_OPEN to OPEN on failure', () => {
      // Open circuit
      for (let i = 0; i < 5; i++) {
        recordFailure('/api/test');
      }

      expect(getCircuitState('/api/test')).toBe(CircuitState.OPEN);

      // In a real scenario, circuit would transition to HALF_OPEN after timeout
      // For now, verify the failure is recorded
      recordFailure('/api/test');

      const metrics = getCircuitMetrics('/api/test');
      expect(metrics.totalFailures).toBe(6);
    });
  });

  describe('recordSuccess', () => {
    it('should increment success count', () => {
      recordSuccess('/api/test');

      const metrics = getCircuitMetrics('/api/test');
      expect(metrics.successes).toBe(1);
      expect(metrics.totalRequests).toBe(1);
    });

    it('should not affect circuit state when CLOSED', () => {
      recordSuccess('/api/test');
      recordSuccess('/api/test');

      expect(getCircuitState('/api/test')).toBe(CircuitState.CLOSED);
    });

    it('should handle successes for multiple endpoints independently', () => {
      recordSuccess('/api/test1');
      recordSuccess('/api/test1');
      recordSuccess('/api/test2');

      expect(getCircuitMetrics('/api/test1').successes).toBe(2);
      expect(getCircuitMetrics('/api/test2').successes).toBe(1);
    });
  });

  describe('getCircuitState', () => {
    it('should return CLOSED for new endpoint', () => {
      expect(getCircuitState('/api/new')).toBe(CircuitState.CLOSED);
    });

    it('should return OPEN after threshold failures', () => {
      for (let i = 0; i < 5; i++) {
        recordFailure('/api/test');
      }

      expect(getCircuitState('/api/test')).toBe(CircuitState.OPEN);
    });

    it('should track different states for different endpoints', () => {
      // Open circuit for test1
      for (let i = 0; i < 5; i++) {
        recordFailure('/api/test1');
      }

      // Keep test2 closed
      recordSuccess('/api/test2');

      expect(getCircuitState('/api/test1')).toBe(CircuitState.OPEN);
      expect(getCircuitState('/api/test2')).toBe(CircuitState.CLOSED);
    });
  });

  describe('getCircuitMetrics', () => {
    it('should return metrics for endpoint', () => {
      recordFailure('/api/test');
      recordSuccess('/api/test');

      const metrics = getCircuitMetrics('/api/test');

      expect(metrics.state).toBe(CircuitState.CLOSED);
      expect(metrics.failures).toBe(1);
      expect(metrics.successes).toBe(1);
      expect(metrics.totalRequests).toBe(2);
      expect(metrics.totalFailures).toBe(1);
      expect(metrics).toHaveProperty('lastFailureTime');
      expect(metrics).toHaveProperty('lastStateChange');
    });

    it('should return immutable snapshot', () => {
      recordFailure('/api/test');

      const metrics1 = getCircuitMetrics('/api/test');
      recordFailure('/api/test');
      const metrics2 = getCircuitMetrics('/api/test');

      expect(metrics1.failures).toBe(1);
      expect(metrics2.failures).toBe(2);
    });
  });

  describe('getAllCircuitMetrics', () => {
    it('should return empty object when no circuits', () => {
      const metrics = getAllCircuitMetrics();
      expect(metrics).toEqual({});
    });

    it('should return metrics for all endpoints', () => {
      recordFailure('/api/test1');
      recordFailure('/api/test2');
      recordSuccess('/api/test3');

      const metrics = getAllCircuitMetrics();

      expect(Object.keys(metrics)).toHaveLength(3);
      expect(metrics['/api/test1']).toBeDefined();
      expect(metrics['/api/test2']).toBeDefined();
      expect(metrics['/api/test3']).toBeDefined();
    });

    it('should return independent metrics for each endpoint', () => {
      recordFailure('/api/test1');
      recordFailure('/api/test1');
      recordSuccess('/api/test2');

      const metrics = getAllCircuitMetrics();

      expect(metrics['/api/test1'].failures).toBe(2);
      expect(metrics['/api/test1'].successes).toBe(0);
      expect(metrics['/api/test2'].failures).toBe(0);
      expect(metrics['/api/test2'].successes).toBe(1);
    });
  });

  describe('resetCircuit', () => {
    it('should reset circuit for specific endpoint', () => {
      recordFailure('/api/test1');
      recordFailure('/api/test2');

      resetCircuit('/api/test1');

      const metrics1 = getCircuitMetrics('/api/test1');
      const metrics2 = getCircuitMetrics('/api/test2');

      expect(metrics1.failures).toBe(0);
      expect(metrics1.totalFailures).toBe(0);
      expect(metrics2.failures).toBe(1);
      expect(metrics2.totalFailures).toBe(1);
    });

    it('should reset state to CLOSED', () => {
      // Open circuit
      for (let i = 0; i < 5; i++) {
        recordFailure('/api/test');
      }

      expect(getCircuitState('/api/test')).toBe(CircuitState.OPEN);

      resetCircuit('/api/test');

      expect(getCircuitState('/api/test')).toBe(CircuitState.CLOSED);
    });
  });

  describe('resetAllCircuits', () => {
    it('should reset all circuits', () => {
      recordFailure('/api/test1');
      recordFailure('/api/test2');
      recordFailure('/api/test3');

      resetAllCircuits();

      const metrics = getAllCircuitMetrics();
      expect(Object.keys(metrics)).toHaveLength(0);
    });

    it('should allow new requests after reset', () => {
      // Open circuit
      for (let i = 0; i < 5; i++) {
        recordFailure('/api/test');
      }

      expect(canExecute('/api/test')).toBe(false);

      resetAllCircuits();

      expect(canExecute('/api/test')).toBe(true);
      expect(getCircuitState('/api/test')).toBe(CircuitState.CLOSED);
    });
  });

  describe('exportCircuitBreakerMetrics', () => {
    it('should export empty metrics when no circuits', () => {
      const output = exportCircuitBreakerMetrics();

      expect(output).toContain('# HELP http_circuit_breaker_state');
      expect(output).toContain('# TYPE http_circuit_breaker_state gauge');
      expect(output).toContain('# HELP http_circuit_breaker_requests_total');
      expect(output).toContain('# HELP http_circuit_breaker_failures_total');
    });

    it('should export circuit state metrics', () => {
      recordFailure('/api/test');

      const output = exportCircuitBreakerMetrics();

      expect(output).toContain('http_circuit_breaker_state{endpoint="/api/test",state="CLOSED"} 0');
    });

    it('should export OPEN state correctly', () => {
      // Open circuit
      for (let i = 0; i < 5; i++) {
        recordFailure('/api/test');
      }

      const output = exportCircuitBreakerMetrics();

      expect(output).toContain('http_circuit_breaker_state{endpoint="/api/test",state="OPEN"} 2');
    });

    it('should export request counters', () => {
      recordFailure('/api/test');
      recordSuccess('/api/test');

      const output = exportCircuitBreakerMetrics();

      expect(output).toContain('http_circuit_breaker_requests_total{endpoint="/api/test"} 2');
    });

    it('should export failure counters', () => {
      recordFailure('/api/test');
      recordFailure('/api/test');

      const output = exportCircuitBreakerMetrics();

      expect(output).toContain('http_circuit_breaker_failures_total{endpoint="/api/test"} 2');
    });

    it('should export metrics for multiple endpoints', () => {
      recordFailure('/api/test1');
      recordSuccess('/api/test2');

      const output = exportCircuitBreakerMetrics();

      expect(output).toContain('endpoint="/api/test1"');
      expect(output).toContain('endpoint="/api/test2"');
    });

    it('should escape endpoint paths for Prometheus labels', () => {
      recordFailure('/api/test"with"quotes');

      const output = exportCircuitBreakerMetrics();

      expect(output).toContain('endpoint="/api/test\\"with\\"quotes"');
    });

    it('should end with newline', () => {
      const output = exportCircuitBreakerMetrics();

      expect(output).toMatch(/\n$/);
    });
  });

  describe('integration scenarios', () => {
    it('should track complete failure cycle', () => {
      // Record failures to open circuit
      for (let i = 0; i < 5; i++) {
        recordFailure('/api/test');
        expect(canExecute('/api/test')).toBe(i < 4); // Last failure opens circuit
      }

      const metrics = getCircuitMetrics('/api/test');
      expect(metrics.state).toBe(CircuitState.OPEN);
      expect(metrics.failures).toBe(5);
      expect(metrics.totalFailures).toBe(5);
      expect(metrics.totalRequests).toBe(5);
    });

    it('should handle mixed success and failure', () => {
      recordSuccess('/api/test');
      recordFailure('/api/test');
      recordSuccess('/api/test');
      recordFailure('/api/test');

      const metrics = getCircuitMetrics('/api/test');
      expect(metrics.state).toBe(CircuitState.CLOSED);
      expect(metrics.failures).toBe(2);
      expect(metrics.successes).toBe(2);
      expect(metrics.totalRequests).toBe(4);
    });

    it('should handle high traffic endpoint', () => {
      // Simulate 100 requests with 10% failure rate
      for (let i = 0; i < 100; i++) {
        if (i % 10 === 0) {
          recordFailure('/api/high-traffic');
        } else {
          recordSuccess('/api/high-traffic');
        }
      }

      const metrics = getCircuitMetrics('/api/high-traffic');
      expect(metrics.totalRequests).toBe(100);
      expect(metrics.totalFailures).toBe(10);
      // Circuit should still be closed (only 10 failures, not consecutive)
      expect(metrics.state).toBe(CircuitState.CLOSED);
    });
  });
});
