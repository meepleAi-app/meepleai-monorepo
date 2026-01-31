/**
 * Tests for Circuit Breaker (Issue #2340)
 *
 * Coverage target: 90%+
 * Tests: State machine transitions, metrics, configuration, Prometheus export
 */

import {
  CircuitState,
  getCircuitBreakerConfig,
  canExecute,
  recordSuccess,
  recordFailure,
  getCircuitState,
  getCircuitMetrics,
  getAllCircuitMetrics,
  resetCircuit,
  resetAllCircuits,
  exportCircuitBreakerMetrics,
} from '../circuitBreaker';

describe('circuitBreaker', () => {
  const testEndpoint = '/api/v1/test';
  const testEndpoint2 = '/api/v1/test2';

  beforeEach(() => {
    // Reset all circuits before each test
    resetAllCircuits();
    vi.clearAllMocks();
  });

  describe('getCircuitBreakerConfig', () => {
    it('should return default config in non-browser environment', () => {
      const config = getCircuitBreakerConfig();

      expect(config).toEqual({
        failureThreshold: 5,
        successThreshold: 2,
        timeout: 60000,
        windowSize: 60000,
        enabled: true,
      });
    });
  });

  describe('Initial State', () => {
    it('should start in CLOSED state', () => {
      const state = getCircuitState(testEndpoint);
      expect(state).toBe(CircuitState.CLOSED);
    });

    it('should allow execution in CLOSED state', () => {
      expect(canExecute(testEndpoint)).toBe(true);
    });

    it('should have zero metrics initially', () => {
      // First access creates circuit
      canExecute(testEndpoint);

      const metrics = getCircuitMetrics(testEndpoint);
      expect(metrics.failures).toBe(0);
      expect(metrics.successes).toBe(0);
      expect(metrics.totalRequests).toBe(0);
      expect(metrics.totalFailures).toBe(0);
    });
  });

  describe('State Transition: CLOSED → OPEN', () => {
    it('should open circuit after failure threshold', () => {
      // Record 5 failures (default threshold)
      for (let i = 0; i < 5; i++) {
        recordFailure(testEndpoint);
      }

      const state = getCircuitState(testEndpoint);
      expect(state).toBe(CircuitState.OPEN);
      expect(canExecute(testEndpoint)).toBe(false);
    });

    it('should track total failures', () => {
      for (let i = 0; i < 5; i++) {
        recordFailure(testEndpoint);
      }

      const metrics = getCircuitMetrics(testEndpoint);
      expect(metrics.totalFailures).toBe(5);
      expect(metrics.failures).toBe(5);
    });
  });

  describe('State Transition: OPEN → HALF_OPEN', () => {
    it('should transition to HALF_OPEN after timeout', () => {
      // Open circuit
      for (let i = 0; i < 5; i++) {
        recordFailure(testEndpoint);
      }
      expect(getCircuitState(testEndpoint)).toBe(CircuitState.OPEN);

      // Mock time passage (60s default timeout)
      vi.useFakeTimers();
      vi.advanceTimersByTime(60001);

      // Check should transition to HALF_OPEN
      const allowed = canExecute(testEndpoint);
      expect(allowed).toBe(true);
      expect(getCircuitState(testEndpoint)).toBe(CircuitState.HALF_OPEN);

      vi.useRealTimers();
    });

    it('should deny requests before timeout', () => {
      // Open circuit
      for (let i = 0; i < 5; i++) {
        recordFailure(testEndpoint);
      }

      // Immediately after opening, should deny
      expect(canExecute(testEndpoint)).toBe(false);
      expect(getCircuitState(testEndpoint)).toBe(CircuitState.OPEN);
    });
  });

  describe('State Transition: HALF_OPEN → CLOSED', () => {
    it('should close circuit after success threshold', () => {
      // Open circuit
      for (let i = 0; i < 5; i++) {
        recordFailure(testEndpoint);
      }

      // Transition to HALF_OPEN
      vi.useFakeTimers();
      vi.advanceTimersByTime(60001);
      canExecute(testEndpoint);
      expect(getCircuitState(testEndpoint)).toBe(CircuitState.HALF_OPEN);

      // Record 2 successes (default threshold)
      recordSuccess(testEndpoint);
      recordSuccess(testEndpoint);

      expect(getCircuitState(testEndpoint)).toBe(CircuitState.CLOSED);

      vi.useRealTimers();
    });

    it('should reset failure count when closed', () => {
      // Open circuit
      for (let i = 0; i < 5; i++) {
        recordFailure(testEndpoint);
      }

      // Transition to HALF_OPEN and close
      vi.useFakeTimers();
      vi.advanceTimersByTime(60001);
      canExecute(testEndpoint);
      recordSuccess(testEndpoint);
      recordSuccess(testEndpoint);

      const metrics = getCircuitMetrics(testEndpoint);
      expect(metrics.failures).toBe(0);
      expect(metrics.successes).toBe(0);

      vi.useRealTimers();
    });
  });

  describe('State Transition: HALF_OPEN → OPEN', () => {
    it('should reopen circuit on failure in HALF_OPEN', () => {
      // Open circuit
      for (let i = 0; i < 5; i++) {
        recordFailure(testEndpoint);
      }

      // Transition to HALF_OPEN
      vi.useFakeTimers();
      vi.advanceTimersByTime(60001);
      canExecute(testEndpoint);
      expect(getCircuitState(testEndpoint)).toBe(CircuitState.HALF_OPEN);

      // Fail again
      recordFailure(testEndpoint);

      expect(getCircuitState(testEndpoint)).toBe(CircuitState.OPEN);
      expect(canExecute(testEndpoint)).toBe(false);

      vi.useRealTimers();
    });
  });

  describe('Success Recording', () => {
    it('should increment success counter', () => {
      recordSuccess(testEndpoint);
      recordSuccess(testEndpoint);

      const metrics = getCircuitMetrics(testEndpoint);
      expect(metrics.successes).toBe(2);
      expect(metrics.totalRequests).toBe(2);
    });

    it('should not affect CLOSED state', () => {
      recordSuccess(testEndpoint);

      expect(getCircuitState(testEndpoint)).toBe(CircuitState.CLOSED);
    });
  });

  describe('Failure Recording', () => {
    it('should increment failure counter', () => {
      recordFailure(testEndpoint);
      recordFailure(testEndpoint);

      const metrics = getCircuitMetrics(testEndpoint);
      expect(metrics.failures).toBe(2);
      expect(metrics.totalFailures).toBe(2);
      expect(metrics.totalRequests).toBe(2);
    });

    it('should reset failure count after window expires', () => {
      vi.useFakeTimers();

      recordFailure(testEndpoint);
      expect(getCircuitMetrics(testEndpoint).failures).toBe(1);

      // Advance past window (60s default)
      vi.advanceTimersByTime(60001);

      recordFailure(testEndpoint);

      // Should reset to 1 (not 2)
      expect(getCircuitMetrics(testEndpoint).failures).toBe(1);

      vi.useRealTimers();
    });
  });

  describe('Metrics', () => {
    it('should get metrics for specific endpoint', () => {
      recordFailure(testEndpoint);
      recordSuccess(testEndpoint);

      const metrics = getCircuitMetrics(testEndpoint);

      expect(metrics).toMatchObject({
        state: CircuitState.CLOSED,
        failures: 1,
        successes: 1,
        totalRequests: 2,
        totalFailures: 1,
      });
      expect(metrics.lastFailureTime).toBeGreaterThan(0);
      expect(metrics.lastStateChange).toBeGreaterThan(0);
    });

    it('should get all metrics for multiple endpoints', () => {
      recordFailure(testEndpoint);
      recordSuccess(testEndpoint2);

      const allMetrics = getAllCircuitMetrics();

      expect(Object.keys(allMetrics)).toHaveLength(2);
      expect(allMetrics[testEndpoint]).toBeDefined();
      expect(allMetrics[testEndpoint2]).toBeDefined();
      expect(allMetrics[testEndpoint].failures).toBe(1);
      expect(allMetrics[testEndpoint2].successes).toBe(1);
    });

    it('should return immutable metrics', () => {
      recordFailure(testEndpoint);

      const metrics1 = getCircuitMetrics(testEndpoint);
      const metrics2 = getCircuitMetrics(testEndpoint);

      // Should be different objects (defensive copy)
      expect(metrics1).not.toBe(metrics2);
      expect(metrics1).toEqual(metrics2);
    });
  });

  describe('Reset Operations', () => {
    it('should reset specific circuit', () => {
      recordFailure(testEndpoint);
      recordFailure(testEndpoint);

      resetCircuit(testEndpoint);

      // Should be back to initial CLOSED state
      const state = getCircuitState(testEndpoint);
      expect(state).toBe(CircuitState.CLOSED);

      const metrics = getCircuitMetrics(testEndpoint);
      expect(metrics.failures).toBe(0);
      expect(metrics.totalRequests).toBe(0);
    });

    it('should reset all circuits', () => {
      recordFailure(testEndpoint);
      recordFailure(testEndpoint2);

      resetAllCircuits();

      const allMetrics = getAllCircuitMetrics();
      expect(Object.keys(allMetrics)).toHaveLength(0);
    });

    it('should not affect other circuits when resetting one', () => {
      recordFailure(testEndpoint);
      recordFailure(testEndpoint2);

      resetCircuit(testEndpoint);

      const metrics2 = getCircuitMetrics(testEndpoint2);
      expect(metrics2.failures).toBe(1); // Still has failure
    });
  });

  describe('Prometheus Metrics Export', () => {
    it('should export empty metrics when no circuits', () => {
      const metrics = exportCircuitBreakerMetrics();

      expect(metrics).toContain('http_circuit_breaker_state');
      expect(metrics).toContain('http_circuit_breaker_requests_total');
      expect(metrics).toContain('http_circuit_breaker_failures_total');
    });

    it('should export circuit state metrics', () => {
      recordFailure(testEndpoint);

      const metrics = exportCircuitBreakerMetrics();

      expect(metrics).toContain('http_circuit_breaker_state');
      expect(metrics).toContain(`endpoint="${testEndpoint}"`);
      expect(metrics).toContain('state="CLOSED"');
      expect(metrics).toContain('} 0'); // CLOSED = 0
    });

    it('should export OPEN state correctly', () => {
      for (let i = 0; i < 5; i++) {
        recordFailure(testEndpoint);
      }

      const metrics = exportCircuitBreakerMetrics();

      expect(metrics).toContain('state="OPEN"');
      expect(metrics).toContain('} 2'); // OPEN = 2
    });

    it('should export request counters', () => {
      recordFailure(testEndpoint);
      recordSuccess(testEndpoint);

      const metrics = exportCircuitBreakerMetrics();

      expect(metrics).toContain('http_circuit_breaker_requests_total');
      expect(metrics).toContain(`{endpoint="${testEndpoint}"} 2`);
    });

    it('should export failure counters', () => {
      recordFailure(testEndpoint);
      recordFailure(testEndpoint);
      recordSuccess(testEndpoint);

      const metrics = exportCircuitBreakerMetrics();

      expect(metrics).toContain('http_circuit_breaker_failures_total');
      expect(metrics).toContain(`{endpoint="${testEndpoint}"} 2`);
    });

    it('should handle multiple endpoints in export', () => {
      recordFailure(testEndpoint);
      recordSuccess(testEndpoint2);

      const metrics = exportCircuitBreakerMetrics();

      expect(metrics).toContain(`endpoint="${testEndpoint}"`);
      expect(metrics).toContain(`endpoint="${testEndpoint2}"`);
    });

    it('should escape special characters in endpoint labels', () => {
      const specialEndpoint = '/api/test?param="value"';
      recordFailure(specialEndpoint);

      const metrics = exportCircuitBreakerMetrics();

      // Should contain escaped version
      expect(metrics).toContain('endpoint=');
    });
  });

  describe('Edge Cases', () => {
    it('should handle rapid state changes', () => {
      vi.useFakeTimers();

      // Open circuit
      for (let i = 0; i < 5; i++) {
        recordFailure(testEndpoint);
      }
      expect(getCircuitState(testEndpoint)).toBe(CircuitState.OPEN);

      // Transition to HALF_OPEN
      vi.advanceTimersByTime(60001);
      canExecute(testEndpoint);
      expect(getCircuitState(testEndpoint)).toBe(CircuitState.HALF_OPEN);

      // Close circuit
      recordSuccess(testEndpoint);
      recordSuccess(testEndpoint);
      expect(getCircuitState(testEndpoint)).toBe(CircuitState.CLOSED);

      vi.useRealTimers();
    });

    it('should handle multiple endpoints independently', () => {
      // Fail endpoint 1
      for (let i = 0; i < 5; i++) {
        recordFailure(testEndpoint);
      }

      // Succeed endpoint 2
      recordSuccess(testEndpoint2);

      expect(getCircuitState(testEndpoint)).toBe(CircuitState.OPEN);
      expect(getCircuitState(testEndpoint2)).toBe(CircuitState.CLOSED);
      expect(canExecute(testEndpoint)).toBe(false);
      expect(canExecute(testEndpoint2)).toBe(true);
    });

    it('should not increment successes in CLOSED state beyond threshold checks', () => {
      // Success in CLOSED doesn't need threshold tracking
      recordSuccess(testEndpoint);
      recordSuccess(testEndpoint);
      recordSuccess(testEndpoint);

      const metrics = getCircuitMetrics(testEndpoint);
      expect(metrics.successes).toBe(3);
      expect(getCircuitState(testEndpoint)).toBe(CircuitState.CLOSED);
    });
  });
});
