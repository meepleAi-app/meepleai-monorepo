/**
 * Request Cache Tests
 *
 * Tests for request deduplication cache with TTL and LRU eviction.
 * @see requestCache.ts
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { RequestCache, globalRequestCache, exportCacheMetricsPrometheus } from '../requestCache';

describe('RequestCache', () => {
  let cache: RequestCache;

  beforeEach(() => {
    cache = new RequestCache({ enabled: true, ttl: 100, maxSize: 10 });
    vi.useFakeTimers();
  });

  afterEach(() => {
    cache.clear();
    vi.useRealTimers();
  });

  describe('constructor', () => {
    it('should use default configuration when no config provided', () => {
      const defaultCache = new RequestCache();
      const config = defaultCache.getConfig();

      expect(config.enabled).toBe(true);
      expect(config.ttl).toBe(100);
      expect(config.maxSize).toBe(100);
    });

    it('should use provided configuration', () => {
      const customCache = new RequestCache({ enabled: false, ttl: 500, maxSize: 50 });
      const config = customCache.getConfig();

      expect(config.enabled).toBe(false);
      expect(config.ttl).toBe(500);
      expect(config.maxSize).toBe(50);
    });

    it('should validate negative TTL and use default', () => {
      const invalidCache = new RequestCache({ ttl: -100 });
      const config = invalidCache.getConfig();

      expect(config.ttl).toBe(100); // default
    });

    it('should validate zero maxSize and use default', () => {
      const invalidCache = new RequestCache({ maxSize: 0 });
      const config = invalidCache.getConfig();

      expect(config.maxSize).toBe(100); // default
    });
  });

  describe('dedupe', () => {
    it('should return cached promise for duplicate requests', async () => {
      const requestFn = vi.fn().mockResolvedValue({ data: 'test' });
      const key = 'test-key';

      // First request - should call requestFn
      const promise1 = cache.dedupe(key, requestFn);
      // Second request - should return cached promise
      const promise2 = cache.dedupe(key, requestFn);

      // Both should return same promise
      expect(promise1).toBe(promise2);
      expect(requestFn).toHaveBeenCalledTimes(1);

      const result = await promise1;
      expect(result).toEqual({ data: 'test' });
    });

    it('should execute requestFn when cache misses', async () => {
      const requestFn = vi.fn().mockResolvedValue({ data: 'test' });

      const result = await cache.dedupe('key-1', requestFn);

      expect(requestFn).toHaveBeenCalledTimes(1);
      expect(result).toEqual({ data: 'test' });
    });

    it('should skip deduplication when skipDedup is true', async () => {
      const requestFn = vi.fn().mockResolvedValue({ data: 'test' });
      const key = 'skip-key';

      await cache.dedupe(key, requestFn, true);
      await cache.dedupe(key, requestFn, true);

      expect(requestFn).toHaveBeenCalledTimes(2);
    });

    it('should skip deduplication when cache is disabled', async () => {
      const disabledCache = new RequestCache({ enabled: false });
      const requestFn = vi.fn().mockResolvedValue({ data: 'test' });
      const key = 'disabled-key';

      await disabledCache.dedupe(key, requestFn);
      await disabledCache.dedupe(key, requestFn);

      expect(requestFn).toHaveBeenCalledTimes(2);
    });

    it('should remove failed requests from cache immediately', async () => {
      const error = new Error('Request failed');
      const failingFn = vi.fn().mockRejectedValue(error);
      const successFn = vi.fn().mockResolvedValue({ data: 'success' });
      const key = 'fail-key';

      // First request fails
      await expect(cache.dedupe(key, failingFn)).rejects.toThrow('Request failed');

      // Second request should execute (not use cached failed promise)
      const result = await cache.dedupe(key, successFn);
      expect(result).toEqual({ data: 'success' });
      expect(successFn).toHaveBeenCalledTimes(1);
    });

    it('should update metrics on cache hit', async () => {
      const requestFn = vi.fn().mockResolvedValue({ data: 'test' });
      const key = 'metrics-key';

      await cache.dedupe(key, requestFn);
      await cache.dedupe(key, requestFn);

      const metrics = cache.getMetrics();
      expect(metrics.hits).toBe(1);
      expect(metrics.misses).toBe(1);
    });
  });

  describe('generateKey', () => {
    it('should generate key from method and URL', () => {
      const key = cache.generateKey('GET', '/api/users');
      expect(key).toBe('GET::/api/users');
    });

    it('should include body hash in key', () => {
      const key1 = cache.generateKey('POST', '/api/users', { name: 'John' });
      const key2 = cache.generateKey('POST', '/api/users', { name: 'Jane' });
      const key3 = cache.generateKey('POST', '/api/users', { name: 'John' });

      expect(key1).not.toBe(key2);
      expect(key1).toBe(key3); // Same body = same key
    });

    it('should include auth context in key', () => {
      const key1 = cache.generateKey('GET', '/api/users', undefined, 'user:123');
      const key2 = cache.generateKey('GET', '/api/users', undefined, 'user:456');
      const key3 = cache.generateKey('GET', '/api/users');

      expect(key1).not.toBe(key2);
      expect(key1).not.toBe(key3);
    });

    it('should include options in key', () => {
      const key1 = cache.generateKey('GET', '/api/users', undefined, undefined, {
        skipCircuitBreaker: true,
      });
      const key2 = cache.generateKey('GET', '/api/users', undefined, undefined, {
        skipCircuitBreaker: false,
      });
      const key3 = cache.generateKey('GET', '/api/users');

      expect(key1).not.toBe(key2);
      expect(key1).not.toBe(key3);
    });

    it('should handle null and undefined body', () => {
      const key1 = cache.generateKey('POST', '/api/users', null);
      const key2 = cache.generateKey('POST', '/api/users', undefined);
      const key3 = cache.generateKey('POST', '/api/users');

      // null and undefined should be treated differently
      expect(key2).toBe(key3);
    });

    it('should produce consistent keys for same input', () => {
      const body = { a: 1, b: 2, c: { d: 3 } };
      const key1 = cache.generateKey('POST', '/api/test', body);
      const key2 = cache.generateKey('POST', '/api/test', body);

      expect(key1).toBe(key2);
    });

    it('should produce same key regardless of object property order', () => {
      const body1 = { a: 1, b: 2 };
      const body2 = { b: 2, a: 1 };
      const key1 = cache.generateKey('POST', '/api/test', body1);
      const key2 = cache.generateKey('POST', '/api/test', body2);

      expect(key1).toBe(key2);
    });
  });

  describe('TTL expiration', () => {
    it('should expire cache entries after TTL', async () => {
      const requestFn = vi.fn().mockResolvedValue({ data: 'test' });
      const key = 'ttl-key';

      // First request
      await cache.dedupe(key, requestFn);
      expect(requestFn).toHaveBeenCalledTimes(1);

      // Advance time past TTL
      vi.advanceTimersByTime(150);

      // Second request should execute again (entry expired)
      await cache.dedupe(key, requestFn);
      expect(requestFn).toHaveBeenCalledTimes(2);

      const metrics = cache.getMetrics();
      expect(metrics.expirations).toBe(1);
    });

    it('should not expire entries before TTL', async () => {
      const requestFn = vi.fn().mockResolvedValue({ data: 'test' });
      const key = 'no-expire-key';

      await cache.dedupe(key, requestFn);

      // Advance time but not past TTL
      vi.advanceTimersByTime(50);

      await cache.dedupe(key, requestFn);
      expect(requestFn).toHaveBeenCalledTimes(1);
    });
  });

  describe('LRU eviction', () => {
    it('should evict oldest entry when cache is full', async () => {
      const smallCache = new RequestCache({ maxSize: 3, ttl: 1000 });
      const requestFn = vi.fn().mockResolvedValue({ data: 'test' });

      // Fill cache
      await smallCache.dedupe('key-1', requestFn);
      await smallCache.dedupe('key-2', requestFn);
      await smallCache.dedupe('key-3', requestFn);

      expect(requestFn).toHaveBeenCalledTimes(3);

      // Add new entry - should evict key-1
      await smallCache.dedupe('key-4', requestFn);
      expect(requestFn).toHaveBeenCalledTimes(4);

      const metrics = smallCache.getMetrics();
      expect(metrics.evictions).toBe(1);

      // key-1 should be evicted, so new request should execute
      await smallCache.dedupe('key-1', requestFn);
      expect(requestFn).toHaveBeenCalledTimes(5);
    });

    it('should update access order on cache hit', async () => {
      const smallCache = new RequestCache({ maxSize: 3, ttl: 1000 });
      const requestFn = vi.fn().mockResolvedValue({ data: 'test' });

      // Fill cache
      await smallCache.dedupe('key-1', requestFn);
      await smallCache.dedupe('key-2', requestFn);
      await smallCache.dedupe('key-3', requestFn);

      // Access key-1 again (moves to most recently used)
      await smallCache.dedupe('key-1', requestFn);

      // Add new entry - should evict key-2 (oldest accessed)
      await smallCache.dedupe('key-4', requestFn);

      // key-1 should still be cached
      await smallCache.dedupe('key-1', requestFn);
      expect(requestFn).toHaveBeenCalledTimes(4); // Not called again

      // key-2 should be evicted
      await smallCache.dedupe('key-2', requestFn);
      expect(requestFn).toHaveBeenCalledTimes(5); // Called again
    });
  });

  describe('getMetrics', () => {
    it('should return correct metrics', async () => {
      const requestFn = vi.fn().mockResolvedValue({ data: 'test' });

      await cache.dedupe('key-1', requestFn);
      await cache.dedupe('key-1', requestFn); // hit
      await cache.dedupe('key-2', requestFn); // miss

      const metrics = cache.getMetrics();

      expect(metrics.hits).toBe(1);
      expect(metrics.misses).toBe(2);
      expect(metrics.size).toBe(2);
    });
  });

  describe('clear', () => {
    it('should clear all cache entries and reset metrics', async () => {
      const requestFn = vi.fn().mockResolvedValue({ data: 'test' });

      await cache.dedupe('key-1', requestFn);
      await cache.dedupe('key-2', requestFn);

      cache.clear();

      const metrics = cache.getMetrics();
      expect(metrics.hits).toBe(0);
      expect(metrics.misses).toBe(0);
      expect(metrics.size).toBe(0);
      expect(metrics.evictions).toBe(0);
      expect(metrics.expirations).toBe(0);
    });

    it('should allow new entries after clear', async () => {
      const requestFn = vi.fn().mockResolvedValue({ data: 'test' });

      await cache.dedupe('key-1', requestFn);
      cache.clear();
      await cache.dedupe('key-1', requestFn);

      // Should be called twice (cache was cleared)
      expect(requestFn).toHaveBeenCalledTimes(2);
    });
  });

  describe('updateConfig', () => {
    it('should update configuration', () => {
      cache.updateConfig({ ttl: 500 });
      const config = cache.getConfig();

      expect(config.ttl).toBe(500);
    });

    it('should validate updated configuration', () => {
      cache.updateConfig({ ttl: -100 });
      const config = cache.getConfig();

      expect(config.ttl).toBe(100); // reverted to default
    });
  });

  describe('toPrometheusFormat', () => {
    it('should export metrics in Prometheus format', async () => {
      const requestFn = vi.fn().mockResolvedValue({ data: 'test' });

      await cache.dedupe('key-1', requestFn);
      await cache.dedupe('key-1', requestFn);

      const prometheus = cache.toPrometheusFormat();

      expect(prometheus).toContain('http_client_cache_hits_total 1');
      expect(prometheus).toContain('http_client_cache_misses_total 1');
      expect(prometheus).toContain('http_client_cache_hit_rate_percent');
      expect(prometheus).toContain('http_client_cache_size');
      expect(prometheus).toContain('# HELP');
      expect(prometheus).toContain('# TYPE');
    });

    it('should calculate hit rate correctly', async () => {
      const requestFn = vi.fn().mockResolvedValue({ data: 'test' });

      // 1 miss, 3 hits = 75% hit rate
      await cache.dedupe('key-1', requestFn);
      await cache.dedupe('key-1', requestFn);
      await cache.dedupe('key-1', requestFn);
      await cache.dedupe('key-1', requestFn);

      const prometheus = cache.toPrometheusFormat();
      expect(prometheus).toContain('http_client_cache_hit_rate_percent 75.00');
    });
  });

  describe('edge cases', () => {
    it('should handle circular references in body', () => {
      interface CircularObject {
        name: string;
        self?: CircularObject;
      }
      const circular: CircularObject = { name: 'test' };
      circular.self = circular;

      // Should not throw, should generate a valid key
      const key = cache.generateKey('POST', '/api/test', circular);
      expect(typeof key).toBe('string');
      expect(key.length).toBeGreaterThan(0);
    });

    it('should handle very large request bodies', () => {
      const largeBody = { data: 'x'.repeat(20000) };

      // Should generate a random key (cache miss) for large bodies
      const key1 = cache.generateKey('POST', '/api/test', largeBody);
      const key2 = cache.generateKey('POST', '/api/test', largeBody);

      // Keys should be different (random) for oversized bodies
      expect(key1).not.toBe(key2);
    });

    it('should handle nested objects in body', () => {
      const nested = {
        level1: {
          level2: {
            level3: {
              value: 'deep',
            },
          },
        },
      };

      const key1 = cache.generateKey('POST', '/api/test', nested);
      const key2 = cache.generateKey('POST', '/api/test', nested);

      expect(key1).toBe(key2);
    });

    it('should handle arrays in body', () => {
      const arrayBody = [1, 2, 3, { nested: true }];

      const key1 = cache.generateKey('POST', '/api/test', arrayBody);
      const key2 = cache.generateKey('POST', '/api/test', arrayBody);

      expect(key1).toBe(key2);
    });

    it('should handle empty options', () => {
      const key1 = cache.generateKey('GET', '/api/test', undefined, undefined, {});
      const key2 = cache.generateKey('GET', '/api/test');

      // Empty options should produce same key as no options
      expect(key1).toBe(key2);
    });
  });
});

describe('globalRequestCache', () => {
  afterEach(() => {
    globalRequestCache.clear();
  });

  it('should be a singleton instance', () => {
    expect(globalRequestCache).toBeDefined();
    expect(globalRequestCache).toBeInstanceOf(RequestCache);
  });

  it('should be usable for deduplication', async () => {
    const requestFn = vi.fn().mockResolvedValue({ data: 'global' });
    const key = 'global-key';

    const result = await globalRequestCache.dedupe(key, requestFn);
    expect(result).toEqual({ data: 'global' });
  });
});

describe('exportCacheMetricsPrometheus', () => {
  afterEach(() => {
    globalRequestCache.clear();
  });

  it('should export global cache metrics', () => {
    const metrics = exportCacheMetricsPrometheus();

    expect(metrics).toContain('http_client_cache_hits_total');
    expect(metrics).toContain('http_client_cache_misses_total');
  });
});
