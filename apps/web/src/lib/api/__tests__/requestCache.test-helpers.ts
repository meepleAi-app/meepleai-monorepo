/**
 * Test Helpers for Request Cache Tests
 * Shared utilities and factories for cache testing
 */

import { RequestCache } from '../core/requestCache';

/**
 * Create a test cache with custom configuration
 */
export function createTestCache(config = {}) {
  return new RequestCache({
    enabled: true,
    ttl: 100,
    maxSize: 3,
    ...config,
  });
}

/**
 * Mock request function factory
 */
export function createMockRequest<T>(value: T) {
  return vi.fn().mockResolvedValue(value);
}

/**
 * Mock request function with counter
 */
export function createCountingMockRequest() {
  let callCount = 0;
  return vi.fn().mockImplementation(async () => {
    callCount++;
    return { data: `result-${callCount}` };
  });
}

/**
 * Time management helpers
 */
export const timeHelpers = {
  setSystemTime: (time: number) => {
    vi.setSystemTime(time);
  },

  advanceTime: (ms: number) => {
    vi.advanceTimersByTime(ms);
  },

  getCurrentTime: () => Date.now(),
};

/**
 * Common test objects
 */
export const testObjects = {
  simple: { name: 'John' },
  complex: {
    nested: {
      deeply: {
        value: 123,
        array: [1, 2, 3],
      },
    },
  },
  withOrder1: { name: 'John', age: 30, city: 'NYC' },
  withOrder2: { city: 'NYC', name: 'John', age: 30 },
};

/**
 * Common test configurations
 */
export const testConfigs = {
  default: {
    enabled: true,
    ttl: 100,
    maxSize: 100,
  },
  custom: {
    enabled: false,
    ttl: 500,
    maxSize: 50,
  },
  small: {
    enabled: true,
    ttl: 100,
    maxSize: 2,
  },
};

/**
 * Assertion helpers
 */
export const assertionHelpers = {
  expectMetrics: (cache: RequestCache, expected: Partial<ReturnType<RequestCache['getMetrics']>>) => {
    const metrics = cache.getMetrics();
    Object.entries(expected).forEach(([key, value]) => {
      expect(metrics[key as keyof typeof metrics]).toBe(value);
    });
  },

  expectPrometheusMetric: (prometheus: string, metric: string, value: string | number) => {
    expect(prometheus).toContain(`${metric} ${value}`);
  },
};

/**
 * Environment variable helpers
 */
export const envHelpers = {
  setEnvVars: (vars: Record<string, string>) => {
    Object.entries(vars).forEach(([key, value]) => {
      process.env[key] = value;
    });
  },

  clearEnvVars: (keys: string[]) => {
    keys.forEach(key => {
      delete process.env[key];
    });
  },

  restoreEnv: (originalEnv: NodeJS.ProcessEnv) => {
    process.env = originalEnv;
  },
};
