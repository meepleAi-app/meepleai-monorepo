/**
 * Circuit Breaker Pattern for Retry Logic (Enhancement for #1453)
 *
 * Implements circuit breaker pattern to prevent wasted retries on
 * consistently failing endpoints. Uses three states:
 * - CLOSED: Normal operation, retries enabled
 * - OPEN: Endpoint failing, retries disabled temporarily
 * - HALF_OPEN: Testing if endpoint recovered
 *
 * State transitions:
 * CLOSED → OPEN: After failure threshold reached
 * OPEN → HALF_OPEN: After timeout period
 * HALF_OPEN → CLOSED: After successful request
 * HALF_OPEN → OPEN: After failed request
 */

import { logger } from '@/lib/logger';

import { escapePrometheusLabelValue } from './prometheusUtils';

export enum CircuitState {
  CLOSED = 'CLOSED', // Normal operation
  OPEN = 'OPEN', // Failing, retries disabled
  HALF_OPEN = 'HALF_OPEN', // Testing recovery
}

export interface CircuitBreakerConfig {
  /** Failure threshold before opening circuit (default: 5) */
  failureThreshold: number;
  /** Success threshold to close circuit from half-open (default: 2) */
  successThreshold: number;
  /** Timeout before transitioning from open to half-open in ms (default: 60000) */
  timeout: number;
  /** Time window for tracking failures in ms (default: 60000) */
  windowSize: number;
  /** Enable/disable circuit breaker (default: true) */
  enabled: boolean;
}

interface CircuitMetrics {
  state: CircuitState;
  failures: number;
  successes: number;
  lastFailureTime: number;
  lastStateChange: number;
  totalRequests: number;
  totalFailures: number;
}

/**
 * Get circuit breaker configuration from environment
 */
export function getCircuitBreakerConfig(): CircuitBreakerConfig {
  const isBrowser = typeof window !== 'undefined';

  return {
    failureThreshold: isBrowser
      ? parseInt(process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_FAILURE_THRESHOLD || '5', 10)
      : 5,
    successThreshold: isBrowser
      ? parseInt(process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_SUCCESS_THRESHOLD || '2', 10)
      : 2,
    timeout: isBrowser
      ? parseInt(process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_TIMEOUT || '60000', 10)
      : 60000,
    windowSize: isBrowser
      ? parseInt(process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_WINDOW_SIZE || '60000', 10)
      : 60000,
    enabled: isBrowser ? process.env.NEXT_PUBLIC_CIRCUIT_BREAKER_ENABLED !== 'false' : true,
  };
}

/**
 * Circuit Breaker implementation
 */
class CircuitBreaker {
  private circuits: Map<string, CircuitMetrics> = new Map();
  private config: CircuitBreakerConfig;

  constructor(config?: Partial<CircuitBreakerConfig>) {
    this.config = {
      ...getCircuitBreakerConfig(),
      ...config,
    };
  }

  /**
   * Get or create circuit for endpoint
   */
  private getCircuit(endpoint: string): CircuitMetrics {
    if (!this.circuits.has(endpoint)) {
      this.circuits.set(endpoint, {
        state: CircuitState.CLOSED,
        failures: 0,
        successes: 0,
        lastFailureTime: 0,
        lastStateChange: Date.now(),
        totalRequests: 0,
        totalFailures: 0,
      });
    }
    const circuit = this.circuits.get(endpoint);
    if (!circuit) {
      throw new Error(`Circuit for endpoint "${endpoint}" not found`);
    }
    return circuit;
  }

  /**
   * Check if request is allowed (circuit not open)
   */
  canExecute(endpoint: string): boolean {
    if (!this.config.enabled) {
      return true;
    }

    const circuit = this.getCircuit(endpoint);
    const now = Date.now();

    // Transition from OPEN to HALF_OPEN after timeout
    if (circuit.state === CircuitState.OPEN) {
      if (now - circuit.lastStateChange >= this.config.timeout) {
        this.transitionToHalfOpen(endpoint);
        return true;
      }
      return false; // Circuit open, deny request
    }

    return true; // CLOSED or HALF_OPEN, allow request
  }

  /**
   * Record successful request
   */
  recordSuccess(endpoint: string): void {
    if (!this.config.enabled) {
      return;
    }

    const circuit = this.getCircuit(endpoint);
    circuit.totalRequests++;
    circuit.successes++;

    if (circuit.state === CircuitState.HALF_OPEN) {
      // Check if we have enough successes to close circuit
      if (circuit.successes >= this.config.successThreshold) {
        this.transitionToClosed(endpoint);
      }
    }
  }

  /**
   * Record failed request
   */
  recordFailure(endpoint: string): void {
    if (!this.config.enabled) {
      return;
    }

    const circuit = this.getCircuit(endpoint);
    const now = Date.now();

    circuit.totalRequests++;
    circuit.totalFailures++;
    circuit.failures++;
    circuit.lastFailureTime = now;

    // Reset failure count if outside window
    if (now - circuit.lastStateChange > this.config.windowSize) {
      circuit.failures = 1;
      circuit.lastStateChange = now;
    }

    if (circuit.state === CircuitState.HALF_OPEN) {
      // Failed in half-open state, go back to open
      this.transitionToOpen(endpoint);
    } else if (circuit.state === CircuitState.CLOSED) {
      // Check if we exceeded failure threshold
      if (circuit.failures >= this.config.failureThreshold) {
        this.transitionToOpen(endpoint);
      }
    }
  }

  /**
   * Get current state for endpoint
   */
  getState(endpoint: string): CircuitState {
    const circuit = this.getCircuit(endpoint);
    return circuit.state;
  }

  /**
   * Get metrics for endpoint
   */
  getMetrics(endpoint: string): Readonly<CircuitMetrics> {
    return { ...this.getCircuit(endpoint) };
  }

  /**
   * Get all circuit metrics
   */
  getAllMetrics(): Record<string, CircuitMetrics> {
    const metrics: Record<string, CircuitMetrics> = {};
    this.circuits.forEach((circuit, endpoint) => {
      // Safe: endpoint comes from Map keys, not user input
      metrics[endpoint] = { ...circuit };
    });
    return metrics;
  }

  /**
   * Reset circuit for endpoint
   */
  reset(endpoint: string): void {
    this.circuits.delete(endpoint);
  }

  /**
   * Reset all circuits
   */
  resetAll(): void {
    this.circuits.clear();
  }

  /**
   * Transition to CLOSED state
   */
  private transitionToClosed(endpoint: string): void {
    const circuit = this.getCircuit(endpoint);
    circuit.state = CircuitState.CLOSED;
    circuit.failures = 0;
    circuit.successes = 0;
    circuit.lastStateChange = Date.now();

    logger.warn(`[CircuitBreaker] ${endpoint} → CLOSED (recovered)`);
  }

  /**
   * Transition to OPEN state
   */
  private transitionToOpen(endpoint: string): void {
    const circuit = this.getCircuit(endpoint);
    circuit.state = CircuitState.OPEN;
    circuit.lastStateChange = Date.now();

    logger.warn(
      `[CircuitBreaker] ${endpoint} → OPEN (${circuit.failures}/${this.config.failureThreshold} failures)`
    );
  }

  /**
   * Transition to HALF_OPEN state
   */
  private transitionToHalfOpen(endpoint: string): void {
    const circuit = this.getCircuit(endpoint);
    circuit.state = CircuitState.HALF_OPEN;
    circuit.successes = 0;
    circuit.failures = 0;
    circuit.lastStateChange = Date.now();

    logger.warn(`[CircuitBreaker] ${endpoint} → HALF_OPEN (testing recovery)`);
  }
}

/**
 * Global circuit breaker instance
 */
const globalCircuitBreaker = new CircuitBreaker();

/**
 * Check if request is allowed for endpoint
 */
export function canExecute(endpoint: string): boolean {
  return globalCircuitBreaker.canExecute(endpoint);
}

/**
 * Record successful request
 */
export function recordSuccess(endpoint: string): void {
  globalCircuitBreaker.recordSuccess(endpoint);
}

/**
 * Record failed request
 */
export function recordFailure(endpoint: string): void {
  globalCircuitBreaker.recordFailure(endpoint);
}

/**
 * Get circuit state for endpoint
 */
export function getCircuitState(endpoint: string): CircuitState {
  return globalCircuitBreaker.getState(endpoint);
}

/**
 * Get circuit metrics for endpoint
 */
export function getCircuitMetrics(endpoint: string): Readonly<CircuitMetrics> {
  return globalCircuitBreaker.getMetrics(endpoint);
}

/**
 * Get all circuit metrics
 */
export function getAllCircuitMetrics(): Record<string, CircuitMetrics> {
  return globalCircuitBreaker.getAllMetrics();
}

/**
 * Reset circuit for endpoint
 */
export function resetCircuit(endpoint: string): void {
  globalCircuitBreaker.reset(endpoint);
}

/**
 * Reset all circuits
 */
export function resetAllCircuits(): void {
  globalCircuitBreaker.resetAll();
}

/**
 * Export circuit breaker metrics in Prometheus format
 */
export function exportCircuitBreakerMetrics(): string {
  const metrics = getAllCircuitMetrics();
  const lines: string[] = [];

  // Circuit state gauge
  lines.push(
    '# HELP http_circuit_breaker_state Circuit breaker state (0=CLOSED, 1=HALF_OPEN, 2=OPEN)'
  );
  lines.push('# TYPE http_circuit_breaker_state gauge');
  Object.entries(metrics).forEach(([endpoint, metric]) => {
    const stateValue =
      metric.state === CircuitState.CLOSED ? 0 : metric.state === CircuitState.HALF_OPEN ? 1 : 2;
    const escapedEndpoint = escapePrometheusLabelValue(endpoint);
    lines.push(
      `http_circuit_breaker_state{endpoint="${escapedEndpoint}",state="${metric.state}"} ${stateValue}`
    );
  });

  // Total requests counter
  lines.push('# HELP http_circuit_breaker_requests_total Total requests through circuit breaker');
  lines.push('# TYPE http_circuit_breaker_requests_total counter');
  Object.entries(metrics).forEach(([endpoint, metric]) => {
    const escapedEndpoint = escapePrometheusLabelValue(endpoint);
    lines.push(
      `http_circuit_breaker_requests_total{endpoint="${escapedEndpoint}"} ${metric.totalRequests}`
    );
  });

  // Total failures counter
  lines.push('# HELP http_circuit_breaker_failures_total Total failures through circuit breaker');
  lines.push('# TYPE http_circuit_breaker_failures_total counter');
  Object.entries(metrics).forEach(([endpoint, metric]) => {
    const escapedEndpoint = escapePrometheusLabelValue(endpoint);
    lines.push(
      `http_circuit_breaker_failures_total{endpoint="${escapedEndpoint}"} ${metric.totalFailures}`
    );
  });

  return lines.join('\n') + '\n';
}
