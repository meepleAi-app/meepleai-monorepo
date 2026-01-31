/**
 * Request Cache Tests - Advanced Features
 *
 * Tests for configuration, options, validation, memory management, and Prometheus metrics
 */

import { RequestCache } from '../core/requestCache';
import {
  createTestCache,
  createMockRequest,
  createCountingMockRequest,
  timeHelpers,
  testConfigs,
  assertionHelpers,
  envHelpers,
} from './requestCache.test-helpers';

// Mock timers for TTL testing
vi.useFakeTimers();

describe('RequestCache - Advanced Features', () => {
  let cache: RequestCache;

  beforeEach(() => {
    cache = createTestCache();
    vi.clearAllTimers();
  });

  afterEach(() => {
    cache.clear();
  });

  describe('Configuration', () => {
    it('should use default configuration', () => {
      const defaultCache = new RequestCache();
      const config = defaultCache.getConfig();

      expect(config.enabled).toBe(true);
      expect(config.ttl).toBe(100);
      expect(config.maxSize).toBe(100);
    });

    it('should accept custom configuration', () => {
      const customCache = new RequestCache(testConfigs.custom);

      const config = customCache.getConfig();
      expect(config.enabled).toBe(false);
      expect(config.ttl).toBe(500);
      expect(config.maxSize).toBe(50);
    });

    it('should update configuration', () => {
      cache.updateConfig({ ttl: 200 });
      const config = cache.getConfig();
      expect(config.ttl).toBe(200);
      expect(config.maxSize).toBe(3);
    });

    it('should read configuration from environment variables', () => {
      const originalEnv = process.env;
      envHelpers.setEnvVars({
        NEXT_PUBLIC_REQUEST_DEDUP_ENABLED: 'false',
        NEXT_PUBLIC_REQUEST_DEDUP_TTL: '250',
        NEXT_PUBLIC_REQUEST_DEDUP_MAX_SIZE: '75',
      });

      const envCache = new RequestCache();
      const config = envCache.getConfig();

      expect(config.enabled).toBe(false);
      expect(config.ttl).toBe(250);
      expect(config.maxSize).toBe(75);

      envHelpers.restoreEnv(originalEnv);
    });
  });

  describe('Option-Sensitive Cache Keys (Issue #1453)', () => {
    it('should include skipCircuitBreaker in cache key', () => {
      const key1 = cache.generateKey('GET', '/api/test', undefined, undefined, {
        skipCircuitBreaker: true,
      });
      const key2 = cache.generateKey('GET', '/api/test', undefined, undefined, {
        skipCircuitBreaker: false,
      });
      const key3 = cache.generateKey('GET', '/api/test', undefined, undefined, undefined);

      expect(key1).not.toBe(key2);
      expect(key1).not.toBe(key3);
      expect(key2).not.toBe(key3);
    });

    it('should include skipRetry in cache key', () => {
      const key1 = cache.generateKey('GET', '/api/test', undefined, undefined, {
        skipRetry: true,
      });
      const key2 = cache.generateKey('GET', '/api/test', undefined, undefined, {
        skipRetry: false,
      });
      const key3 = cache.generateKey('GET', '/api/test', undefined, undefined, undefined);

      expect(key1).not.toBe(key2);
      expect(key1).not.toBe(key3);
      expect(key2).not.toBe(key3);
    });

    it('should include retryConfig in cache key', () => {
      const key1 = cache.generateKey('GET', '/api/test', undefined, undefined, {
        retryConfig: { maxAttempts: 3 },
      });
      const key2 = cache.generateKey('GET', '/api/test', undefined, undefined, {
        retryConfig: { maxAttempts: 5 },
      });
      const key3 = cache.generateKey('GET', '/api/test', undefined, undefined, undefined);

      expect(key1).not.toBe(key2);
      expect(key1).not.toBe(key3);
    });

    it('should generate same key for identical options', () => {
      const options1 = {
        skipCircuitBreaker: true,
        skipRetry: false,
        retryConfig: { maxAttempts: 3, baseDelay: 1000 },
      };
      const options2 = {
        skipCircuitBreaker: true,
        skipRetry: false,
        retryConfig: { maxAttempts: 3, baseDelay: 1000 },
      };

      const key1 = cache.generateKey('GET', '/api/test', undefined, undefined, options1);
      const key2 = cache.generateKey('GET', '/api/test', undefined, undefined, options2);

      expect(key1).toBe(key2);
    });

    it('should handle empty options object', () => {
      const key1 = cache.generateKey('GET', '/api/test', undefined, undefined, {});
      const key2 = cache.generateKey('GET', '/api/test', undefined, undefined, undefined);

      expect(key1).toBe(key2);
    });
  });

  describe('Option-Sensitive Deduplication (Issue #1453)', () => {
    it('should NOT deduplicate requests with different skipCircuitBreaker options', async () => {
      const mockFn = createCountingMockRequest();

      const key1 = cache.generateKey('GET', '/api/test', undefined, undefined, {
        skipCircuitBreaker: true,
      });
      const key2 = cache.generateKey('GET', '/api/test', undefined, undefined, {
        skipCircuitBreaker: false,
      });

      const [result1, result2] = await Promise.all([
        cache.dedupe(key1, mockFn),
        cache.dedupe(key2, mockFn),
      ]);

      expect(mockFn).toHaveBeenCalledTimes(2);
      expect((result1 as { data: string }).data).toBe('result-1');
      expect((result2 as { data: string }).data).toBe('result-2');
    });

    it('should NOT deduplicate requests with different retry config', async () => {
      const mockFn = createCountingMockRequest();

      const key1 = cache.generateKey('GET', '/api/test', undefined, undefined, {
        retryConfig: { maxAttempts: 3 },
      });
      const key2 = cache.generateKey('GET', '/api/test', undefined, undefined, {
        retryConfig: { maxAttempts: 5 },
      });

      const [result1, result2] = await Promise.all([
        cache.dedupe(key1, mockFn),
        cache.dedupe(key2, mockFn),
      ]);

      expect(mockFn).toHaveBeenCalledTimes(2);
      expect((result1 as { data: string }).data).toBe('result-1');
      expect((result2 as { data: string }).data).toBe('result-2');
    });

    it('should deduplicate requests with identical options', async () => {
      const mockFn = createCountingMockRequest();

      const options = {
        skipCircuitBreaker: true,
        retryConfig: { maxAttempts: 3 },
      };

      const key1 = cache.generateKey('GET', '/api/test', undefined, undefined, options);
      const key2 = cache.generateKey('GET', '/api/test', undefined, undefined, options);

      const [result1, result2] = await Promise.all([
        cache.dedupe(key1, mockFn),
        cache.dedupe(key2, mockFn),
      ]);

      expect(mockFn).toHaveBeenCalledTimes(1);
      expect((result1 as { data: string }).data).toBe('result-1');
      expect((result2 as { data: string }).data).toBe('result-1');
    });
  });

  describe('Memory Management', () => {
    it('should cleanup timeouts when cache is cleared', async () => {
      const requestFn = createMockRequest('result');

      await cache.dedupe('key1', requestFn);
      await cache.dedupe('key2', requestFn);
      await cache.dedupe('key3', requestFn);

      cache.clear();

      timeHelpers.advanceTime(200);

      expect(cache.getMetrics().size).toBe(0);
    });

    it('should cleanup timeout when entry is evicted', async () => {
      const customCache = createTestCache({ maxSize: 2 });
      const requestFn = createMockRequest('result');

      await customCache.dedupe('key1', requestFn);
      await customCache.dedupe('key2', requestFn);

      await customCache.dedupe('key3', requestFn);

      timeHelpers.advanceTime(150);

      const metrics = customCache.getMetrics();
      expect(metrics.evictions).toBe(1);

      customCache.clear();
    });

    it('should cleanup timeout when entry is deleted', async () => {
      const requestFn = createMockRequest('result');

      await cache.dedupe('key1', requestFn);

      const now = Date.now();
      timeHelpers.setSystemTime(now + 150);

      await cache.dedupe('key1', requestFn);

      expect(cache.getMetrics().expirations).toBeGreaterThan(0);
    });
  });

  describe('Configuration Validation', () => {
    it('should validate negative TTL', () => {
      const consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation();

      const invalidCache = new RequestCache({
        ttl: -50,
        maxSize: 10,
        enabled: true,
      });

      expect(consoleWarnSpy).toHaveBeenCalledWith(
        '[API Warning]',
        expect.stringContaining('TTL cannot be negative'),
        undefined
      );

      const config = invalidCache.getConfig();
      expect(config.ttl).toBe(100);

      consoleWarnSpy.mockRestore();
      invalidCache.clear();
    });

    it('should validate invalid maxSize', () => {
      const consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation();

      const invalidCache = new RequestCache({
        ttl: 100,
        maxSize: 0,
        enabled: true,
      });

      expect(consoleWarnSpy).toHaveBeenCalledWith(
        '[API Warning]',
        expect.stringContaining('maxSize must be >= 1'),
        undefined
      );

      const config = invalidCache.getConfig();
      expect(config.maxSize).toBe(100);

      consoleWarnSpy.mockRestore();
      invalidCache.clear();
    });

    it('should validate config in updateConfig', () => {
      const consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation();

      cache.updateConfig({ ttl: -100 });

      expect(consoleWarnSpy).toHaveBeenCalledWith(
        '[API Warning]',
        expect.stringContaining('TTL cannot be negative'),
        undefined
      );

      const config = cache.getConfig();
      expect(config.ttl).toBe(100);

      consoleWarnSpy.mockRestore();
    });
  });

  describe('Prometheus Metrics Export', () => {
    it('should export metrics in Prometheus format', async () => {
      const requestFn = createMockRequest('result');

      await cache.dedupe('key1', requestFn);
      await cache.dedupe('key1', requestFn);
      await cache.dedupe('key2', requestFn);

      const prometheus = cache.toPrometheusFormat();

      expect(prometheus).toContain('# HELP http_client_cache_hits_total');
      expect(prometheus).toContain('# TYPE http_client_cache_hits_total counter');
      assertionHelpers.expectPrometheusMetric(prometheus, 'http_client_cache_hits_total', 1);

      expect(prometheus).toContain('# HELP http_client_cache_misses_total');
      expect(prometheus).toContain('# TYPE http_client_cache_misses_total counter');
      assertionHelpers.expectPrometheusMetric(prometheus, 'http_client_cache_misses_total', 2);

      expect(prometheus).toContain('# HELP http_client_cache_size');
      expect(prometheus).toContain('# TYPE http_client_cache_size gauge');

      expect(prometheus).toContain('# HELP http_client_cache_hit_rate_percent');
      expect(prometheus).toContain('# TYPE http_client_cache_hit_rate_percent gauge');

      expect(prometheus).toContain('# HELP http_client_cache_evictions_total');
      expect(prometheus).toContain('# TYPE http_client_cache_evictions_total counter');

      expect(prometheus).toContain('# HELP http_client_cache_expirations_total');
      expect(prometheus).toContain('# TYPE http_client_cache_expirations_total counter');
    });

    it('should calculate hit rate correctly', async () => {
      cache.clear();
      const requestFn = createMockRequest('result');

      await cache.dedupe('key1', requestFn);
      await cache.dedupe('key1', requestFn);
      await cache.dedupe('key1', requestFn);
      await cache.dedupe('key1', requestFn);

      const prometheus = cache.toPrometheusFormat();

      assertionHelpers.expectPrometheusMetric(
        prometheus,
        'http_client_cache_hit_rate_percent',
        '75.00'
      );
    });

    it('should handle zero requests gracefully', () => {
      cache.clear();

      const prometheus = cache.toPrometheusFormat();

      assertionHelpers.expectPrometheusMetric(prometheus, 'http_client_cache_hits_total', 0);
      assertionHelpers.expectPrometheusMetric(prometheus, 'http_client_cache_misses_total', 0);
      assertionHelpers.expectPrometheusMetric(
        prometheus,
        'http_client_cache_hit_rate_percent',
        '0.00'
      );
      assertionHelpers.expectPrometheusMetric(prometheus, 'http_client_cache_size', 0);
    });

    it('should track evictions in Prometheus format', async () => {
      const smallCache = createTestCache({ maxSize: 2 });
      const requestFn = createMockRequest('result');

      await smallCache.dedupe('key1', requestFn);
      await smallCache.dedupe('key2', requestFn);
      await smallCache.dedupe('key3', requestFn);

      const prometheus = smallCache.toPrometheusFormat();

      assertionHelpers.expectPrometheusMetric(prometheus, 'http_client_cache_evictions_total', 1);

      smallCache.clear();
    });

    it('should format output with newlines', () => {
      const prometheus = cache.toPrometheusFormat();

      expect(prometheus).toMatch(/\n$/);

      const lines = prometheus.split('\n').filter(line => line.length > 0);
      expect(lines.length).toBeGreaterThan(10);
    });
  });
});
