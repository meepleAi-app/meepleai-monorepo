/**
 * Request Cache Tests (Issue #1454)
 *
 * Tests for request deduplication cache with TTL and LRU eviction.
 */

import { RequestCache } from '../core/requestCache';

// Mock timers for TTL testing
jest.useFakeTimers();

describe('RequestCache', () => {
  let cache: RequestCache;

  beforeEach(() => {
    cache = new RequestCache({
      enabled: true,
      ttl: 100,
      maxSize: 3,
    });
    jest.clearAllTimers();
  });

  afterEach(() => {
    cache.clear();
  });

  describe('Cache Key Generation', () => {
    it('should generate consistent cache keys', () => {
      const key1 = cache.generateKey('GET', '/api/users', undefined, undefined);
      const key2 = cache.generateKey('GET', '/api/users', undefined, undefined);
      expect(key1).toBe(key2);
    });

    it('should include method in cache key', () => {
      const getKey = cache.generateKey('GET', '/api/users', undefined, undefined);
      const postKey = cache.generateKey('POST', '/api/users', undefined, undefined);
      expect(getKey).not.toBe(postKey);
    });

    it('should include URL in cache key', () => {
      const key1 = cache.generateKey('GET', '/api/users', undefined, undefined);
      const key2 = cache.generateKey('GET', '/api/games', undefined, undefined);
      expect(key1).not.toBe(key2);
    });

    it('should include body hash in cache key', () => {
      const key1 = cache.generateKey('POST', '/api/users', { name: 'John' }, undefined);
      const key2 = cache.generateKey('POST', '/api/users', { name: 'Jane' }, undefined);
      expect(key1).not.toBe(key2);
    });

    it('should generate same hash for identical bodies', () => {
      const key1 = cache.generateKey('POST', '/api/users', { name: 'John' }, undefined);
      const key2 = cache.generateKey('POST', '/api/users', { name: 'John' }, undefined);
      expect(key1).toBe(key2);
    });

    it('should include auth context in cache key', () => {
      const key1 = cache.generateKey('GET', '/api/users', undefined, 'user1');
      const key2 = cache.generateKey('GET', '/api/users', undefined, 'user2');
      expect(key1).not.toBe(key2);
    });

    it('should handle undefined body', () => {
      const key1 = cache.generateKey('GET', '/api/users', undefined, undefined);
      const key2 = cache.generateKey('GET', '/api/users', null, undefined);
      expect(key1).toBe(key2);
    });
  });

  describe('Deduplication', () => {
    it('should deduplicate identical simultaneous requests', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');
      const cacheKey = 'test-key';

      // Execute three simultaneous requests
      const promises = [
        cache.dedupe(cacheKey, requestFn),
        cache.dedupe(cacheKey, requestFn),
        cache.dedupe(cacheKey, requestFn),
      ];

      const results = await Promise.all(promises);

      // All should return same result
      expect(results).toEqual(['result', 'result', 'result']);

      // Request function should only be called once
      expect(requestFn).toHaveBeenCalledTimes(1);
    });

    it('should not deduplicate when skipDedup is true', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');
      const cacheKey = 'test-key';

      // Execute three requests with skipDedup
      await cache.dedupe(cacheKey, requestFn, true);
      await cache.dedupe(cacheKey, requestFn, true);
      await cache.dedupe(cacheKey, requestFn, true);

      // Request function should be called three times
      expect(requestFn).toHaveBeenCalledTimes(3);
    });

    it('should not deduplicate when cache is disabled', async () => {
      cache.updateConfig({ enabled: false });

      const requestFn = jest.fn().mockResolvedValue('result');
      const cacheKey = 'test-key';

      await cache.dedupe(cacheKey, requestFn);
      await cache.dedupe(cacheKey, requestFn);

      expect(requestFn).toHaveBeenCalledTimes(2);
    });

    it('should handle request failures', async () => {
      const error = new Error('Request failed');
      const requestFn = jest.fn().mockRejectedValue(error);
      const cacheKey = 'test-key';

      // First request should fail
      await expect(cache.dedupe(cacheKey, requestFn)).rejects.toThrow('Request failed');

      // Second request should retry (cache cleared on failure)
      await expect(cache.dedupe(cacheKey, requestFn)).rejects.toThrow('Request failed');

      // Request function should be called twice (not deduplicated after failure)
      expect(requestFn).toHaveBeenCalledTimes(2);
    });
  });

  describe('TTL Cleanup', () => {
    it('should expire entries after TTL', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');
      const cacheKey = 'test-key';

      const now = Date.now();
      jest.setSystemTime(now);

      // First request
      await cache.dedupe(cacheKey, requestFn);
      expect(requestFn).toHaveBeenCalledTimes(1);

      // Advance time past TTL
      jest.setSystemTime(now + 150);

      // Second request should not be deduplicated (expired)
      await cache.dedupe(cacheKey, requestFn);
      expect(requestFn).toHaveBeenCalledTimes(2);
    });

    it('should not expire entries before TTL', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');
      const cacheKey = 'test-key';

      const now = Date.now();
      jest.setSystemTime(now);

      // First request
      await cache.dedupe(cacheKey, requestFn);
      expect(requestFn).toHaveBeenCalledTimes(1);

      // Advance time but not past TTL
      jest.setSystemTime(now + 50);

      // Second request should be deduplicated (not expired)
      await cache.dedupe(cacheKey, requestFn);
      expect(requestFn).toHaveBeenCalledTimes(1);
    });

    it('should auto-cleanup after promise settles', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');
      const cacheKey = 'test-key';

      await cache.dedupe(cacheKey, requestFn);

      // Advance timers to trigger cleanup
      jest.advanceTimersByTime(100);

      const metrics = cache.getMetrics();
      expect(metrics.size).toBe(0);
    });
  });

  describe('LRU Eviction', () => {
    it('should evict oldest entry when cache is full', async () => {
      const requestFn1 = jest.fn().mockResolvedValue('result1');
      const requestFn2 = jest.fn().mockResolvedValue('result2');
      const requestFn3 = jest.fn().mockResolvedValue('result3');
      const requestFn4 = jest.fn().mockResolvedValue('result4');

      // Fill cache (maxSize = 3)
      await cache.dedupe('key1', requestFn1);
      await cache.dedupe('key2', requestFn2);
      await cache.dedupe('key3', requestFn3);

      const metrics1 = cache.getMetrics();
      expect(metrics1.size).toBe(3);
      expect(metrics1.evictions).toBe(0);

      // Add fourth entry (should evict key1)
      await cache.dedupe('key4', requestFn4);

      const metrics2 = cache.getMetrics();
      expect(metrics2.size).toBe(3);
      expect(metrics2.evictions).toBe(1);

      // key1 should be evicted, so new request should execute
      await cache.dedupe('key1', requestFn1);
      expect(requestFn1).toHaveBeenCalledTimes(2);
    });

    it('should update access order on cache hit', async () => {
      const requestFn1 = jest.fn().mockResolvedValue('result1');
      const requestFn2 = jest.fn().mockResolvedValue('result2');
      const requestFn3 = jest.fn().mockResolvedValue('result3');
      const requestFn4 = jest.fn().mockResolvedValue('result4');

      // Fill cache
      await cache.dedupe('key1', requestFn1);
      await cache.dedupe('key2', requestFn2);
      await cache.dedupe('key3', requestFn3);

      // Access key1 (should move to end of LRU)
      await cache.dedupe('key1', requestFn1);

      // Add key4 (should evict key2, not key1)
      await cache.dedupe('key4', requestFn4);

      // key1 should still be cached
      await cache.dedupe('key1', requestFn1);
      expect(requestFn1).toHaveBeenCalledTimes(1);

      // key2 should be evicted
      await cache.dedupe('key2', requestFn2);
      expect(requestFn2).toHaveBeenCalledTimes(2);
    });
  });

  describe('Metrics', () => {
    it('should track cache hits', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');
      const cacheKey = 'test-key';

      await cache.dedupe(cacheKey, requestFn);
      await cache.dedupe(cacheKey, requestFn);
      await cache.dedupe(cacheKey, requestFn);

      const metrics = cache.getMetrics();
      expect(metrics.hits).toBe(2);
      expect(metrics.misses).toBe(1);
    });

    it('should track cache misses', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');

      await cache.dedupe('key1', requestFn);
      await cache.dedupe('key2', requestFn);
      await cache.dedupe('key3', requestFn);

      const metrics = cache.getMetrics();
      expect(metrics.hits).toBe(0);
      expect(metrics.misses).toBe(3);
    });

    it('should track evictions', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');

      // Fill cache and trigger evictions
      await cache.dedupe('key1', requestFn);
      await cache.dedupe('key2', requestFn);
      await cache.dedupe('key3', requestFn);
      await cache.dedupe('key4', requestFn);
      await cache.dedupe('key5', requestFn);

      const metrics = cache.getMetrics();
      expect(metrics.evictions).toBe(2);
    });

    it('should track expirations', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');
      const cacheKey = 'test-key';

      const now = Date.now();
      jest.setSystemTime(now);

      await cache.dedupe(cacheKey, requestFn);

      // Advance system time past TTL
      jest.setSystemTime(now + 150);

      // Trigger expiration check
      await cache.dedupe(cacheKey, requestFn);

      const metrics = cache.getMetrics();
      expect(metrics.expirations).toBe(1);
    });

    it('should track current cache size', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');

      await cache.dedupe('key1', requestFn);
      expect(cache.getMetrics().size).toBe(1);

      await cache.dedupe('key2', requestFn);
      expect(cache.getMetrics().size).toBe(2);

      await cache.dedupe('key3', requestFn);
      expect(cache.getMetrics().size).toBe(3);
    });
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
      const customCache = new RequestCache({
        enabled: false,
        ttl: 500,
        maxSize: 50,
      });

      const config = customCache.getConfig();
      expect(config.enabled).toBe(false);
      expect(config.ttl).toBe(500);
      expect(config.maxSize).toBe(50);
    });

    it('should update configuration', () => {
      cache.updateConfig({ ttl: 200 });
      const config = cache.getConfig();
      expect(config.ttl).toBe(200);
      expect(config.maxSize).toBe(3); // Other values unchanged
    });

    it('should read configuration from environment variables', () => {
      const originalEnv = process.env;
      process.env.NEXT_PUBLIC_REQUEST_DEDUP_ENABLED = 'false';
      process.env.NEXT_PUBLIC_REQUEST_DEDUP_TTL = '250';
      process.env.NEXT_PUBLIC_REQUEST_DEDUP_MAX_SIZE = '75';

      const envCache = new RequestCache();
      const config = envCache.getConfig();

      expect(config.enabled).toBe(false);
      expect(config.ttl).toBe(250);
      expect(config.maxSize).toBe(75);

      process.env = originalEnv;
    });
  });

  describe('Clear Cache', () => {
    it('should clear all cache entries', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');

      await cache.dedupe('key1', requestFn);
      await cache.dedupe('key2', requestFn);
      await cache.dedupe('key3', requestFn);

      expect(cache.getMetrics().size).toBe(3);

      cache.clear();

      expect(cache.getMetrics().size).toBe(0);
    });

    it('should allow new entries after clear', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');

      await cache.dedupe('key1', requestFn);
      cache.clear();

      await cache.dedupe('key2', requestFn);
      expect(cache.getMetrics().size).toBe(1);
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty cache key', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');
      await cache.dedupe('', requestFn);
      expect(requestFn).toHaveBeenCalledTimes(1);
    });

    it('should handle very long cache keys', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');
      const longKey = 'a'.repeat(10000);
      await cache.dedupe(longKey, requestFn);
      expect(requestFn).toHaveBeenCalledTimes(1);
    });

    it('should handle complex object bodies', async () => {
      const complexBody = {
        nested: {
          deeply: {
            value: 123,
            array: [1, 2, 3],
          },
        },
      };

      const key1 = cache.generateKey('POST', '/api/test', complexBody, undefined);
      const key2 = cache.generateKey('POST', '/api/test', complexBody, undefined);

      expect(key1).toBe(key2);
    });

    it('should handle null and undefined auth context', () => {
      const key1 = cache.generateKey('GET', '/api/test', undefined, undefined);
      const key2 = cache.generateKey('GET', '/api/test', undefined, null as unknown as undefined);

      expect(key1).toBe(key2);
    });

    it('should handle objects with different property order (hash collision prevention)', () => {
      // Objects with same properties but different order should hash identically
      const obj1 = { name: 'John', age: 30, city: 'NYC' };
      const obj2 = { city: 'NYC', name: 'John', age: 30 };
      const obj3 = { age: 30, city: 'NYC', name: 'John' };

      const key1 = cache.generateKey('POST', '/api/test', obj1, undefined);
      const key2 = cache.generateKey('POST', '/api/test', obj2, undefined);
      const key3 = cache.generateKey('POST', '/api/test', obj3, undefined);

      expect(key1).toBe(key2);
      expect(key2).toBe(key3);
    });

    it('should handle circular references', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');

      // Create circular reference
      const circular: Record<string, unknown> = { name: 'test' };
      circular.self = circular;

      // Should not throw, should return random hash
      const key = cache.generateKey('POST', '/api/test', circular, undefined);
      await cache.dedupe(key, requestFn);

      expect(requestFn).toHaveBeenCalled();
    });

    it('should handle very large request bodies (DoS protection)', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');

      // Create large object (>10KB)
      const largeBody = {
        data: 'x'.repeat(15000),
      };

      const consoleWarnSpy = jest.spyOn(console, 'warn').mockImplementation();

      const key = cache.generateKey('POST', '/api/test', largeBody, undefined);
      await cache.dedupe(key, requestFn);

      expect(consoleWarnSpy).toHaveBeenCalledWith(
        expect.stringContaining('Request body too large for caching')
      );
      expect(requestFn).toHaveBeenCalled();

      consoleWarnSpy.mockRestore();
    });

    it('should handle nested objects with sorted keys', () => {
      const obj1 = {
        user: { name: 'John', age: 30 },
        settings: { theme: 'dark', lang: 'en' },
      };
      const obj2 = {
        settings: { lang: 'en', theme: 'dark' },
        user: { age: 30, name: 'John' },
      };

      const key1 = cache.generateKey('POST', '/api/test', obj1, undefined);
      const key2 = cache.generateKey('POST', '/api/test', obj2, undefined);

      expect(key1).toBe(key2);
    });
  });

  describe('Memory Management', () => {
    it('should cleanup timeouts when cache is cleared', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');

      // Create several entries
      await cache.dedupe('key1', requestFn);
      await cache.dedupe('key2', requestFn);
      await cache.dedupe('key3', requestFn);

      // Clear cache (should clear all timeouts)
      cache.clear();

      // Advance time past TTL
      jest.advanceTimersByTime(200);

      // Cache should be empty
      expect(cache.getMetrics().size).toBe(0);
    });

    it('should cleanup timeout when entry is evicted', async () => {
      const customCache = new RequestCache({
        enabled: true,
        ttl: 100,
        maxSize: 2,
      });

      const requestFn = jest.fn().mockResolvedValue('result');

      // Fill cache
      await customCache.dedupe('key1', requestFn);
      await customCache.dedupe('key2', requestFn);

      // This should evict key1
      await customCache.dedupe('key3', requestFn);

      // Advance time past TTL
      jest.advanceTimersByTime(150);

      // key1 should have been evicted and its timeout cleared
      const metrics = customCache.getMetrics();
      expect(metrics.evictions).toBe(1);

      customCache.clear();
    });

    it('should cleanup timeout when entry is deleted', async () => {
      const requestFn = jest.fn().mockResolvedValue('result');

      await cache.dedupe('key1', requestFn);

      // Manually trigger expiration
      const now = Date.now();
      jest.setSystemTime(now + 150);

      // Trigger expiration check
      await cache.dedupe('key1', requestFn);

      // Should have cleaned up properly
      expect(cache.getMetrics().expirations).toBeGreaterThan(0);
    });
  });

  describe('Configuration Validation', () => {
    it('should validate negative TTL', () => {
      const consoleWarnSpy = jest.spyOn(console, 'warn').mockImplementation();

      const invalidCache = new RequestCache({
        ttl: -50,
        maxSize: 10,
        enabled: true,
      });

      expect(consoleWarnSpy).toHaveBeenCalledWith(
        expect.stringContaining('TTL cannot be negative')
      );

      const config = invalidCache.getConfig();
      expect(config.ttl).toBe(100); // Should use default

      consoleWarnSpy.mockRestore();
      invalidCache.clear();
    });

    it('should validate invalid maxSize', () => {
      const consoleWarnSpy = jest.spyOn(console, 'warn').mockImplementation();

      const invalidCache = new RequestCache({
        ttl: 100,
        maxSize: 0,
        enabled: true,
      });

      expect(consoleWarnSpy).toHaveBeenCalledWith(
        expect.stringContaining('maxSize must be >= 1')
      );

      const config = invalidCache.getConfig();
      expect(config.maxSize).toBe(100); // Should use default

      consoleWarnSpy.mockRestore();
      invalidCache.clear();
    });

    it('should validate config in updateConfig', () => {
      const consoleWarnSpy = jest.spyOn(console, 'warn').mockImplementation();

      cache.updateConfig({ ttl: -100 });

      expect(consoleWarnSpy).toHaveBeenCalledWith(
        expect.stringContaining('TTL cannot be negative')
      );

      const config = cache.getConfig();
      expect(config.ttl).toBe(100); // Should use default

      consoleWarnSpy.mockRestore();
    });
  });

  describe('Concurrent Requests', () => {
    it('should handle many concurrent requests to same endpoint', async () => {
      let callCount = 0;
      const requestFn = jest.fn().mockImplementation(async () => {
        callCount++;
        return { data: `result-${callCount}` };
      });

      const cacheKey = 'concurrent-test';

      // Fire 50 concurrent requests
      const promises = Array.from({ length: 50 }, () =>
        cache.dedupe(cacheKey, requestFn)
      );

      const results = await Promise.all(promises);

      // All should return same result
      expect(results.every((r) => r.data === 'result-1')).toBe(true);

      // Request function should only be called once
      expect(callCount).toBe(1);
    });

    it('should handle concurrent requests to different endpoints', async () => {
      let callCount = 0;
      const requestFn = jest.fn().mockImplementation(async () => {
        callCount++;
        return { data: `result-${callCount}` };
      });

      // Fire concurrent requests to 10 different endpoints
      const promises = Array.from({ length: 10 }, (_, i) =>
        cache.dedupe(`endpoint-${i}`, requestFn)
      );

      await Promise.all(promises);

      // Each endpoint should trigger one request
      expect(callCount).toBe(10);
    });
  });
});
