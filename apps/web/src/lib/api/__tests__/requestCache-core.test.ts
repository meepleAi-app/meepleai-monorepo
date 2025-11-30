/**
 * Request Cache Tests - Core Functionality
 *
 * Tests for cache key generation, deduplication, TTL, LRU eviction, and metrics
 */

import { RequestCache } from '../core/requestCache';
import {
  createTestCache,
  createMockRequest,
  createCountingMockRequest,
  timeHelpers,
  testObjects,
  assertionHelpers,
} from './requestCache.test-helpers';

// Mock timers for TTL testing
vi.useFakeTimers();

describe('RequestCache - Core Functionality', () => {
  let cache: RequestCache;

  beforeEach(() => {
    cache = createTestCache();
    vi.clearAllTimers();
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
      const requestFn = createMockRequest('result');
      const cacheKey = 'test-key';

      const promises = [
        cache.dedupe(cacheKey, requestFn),
        cache.dedupe(cacheKey, requestFn),
        cache.dedupe(cacheKey, requestFn),
      ];

      const results = await Promise.all(promises);

      expect(results).toEqual(['result', 'result', 'result']);
      expect(requestFn).toHaveBeenCalledTimes(1);
    });

    it('should not deduplicate when skipDedup is true', async () => {
      const requestFn = createMockRequest('result');
      const cacheKey = 'test-key';

      await cache.dedupe(cacheKey, requestFn, true);
      await cache.dedupe(cacheKey, requestFn, true);
      await cache.dedupe(cacheKey, requestFn, true);

      expect(requestFn).toHaveBeenCalledTimes(3);
    });

    it('should not deduplicate when cache is disabled', async () => {
      cache.updateConfig({ enabled: false });

      const requestFn = createMockRequest('result');
      const cacheKey = 'test-key';

      await cache.dedupe(cacheKey, requestFn);
      await cache.dedupe(cacheKey, requestFn);

      expect(requestFn).toHaveBeenCalledTimes(2);
    });

    it('should handle request failures', async () => {
      const error = new Error('Request failed');
      const requestFn = vi.fn().mockRejectedValue(error);
      const cacheKey = 'test-key';

      await expect(cache.dedupe(cacheKey, requestFn)).rejects.toThrow('Request failed');
      await expect(cache.dedupe(cacheKey, requestFn)).rejects.toThrow('Request failed');

      expect(requestFn).toHaveBeenCalledTimes(2);
    });
  });

  describe('TTL Cleanup', () => {
    it('should expire entries after TTL', async () => {
      const requestFn = createMockRequest('result');
      const cacheKey = 'test-key';

      const now = Date.now();
      timeHelpers.setSystemTime(now);

      await cache.dedupe(cacheKey, requestFn);
      expect(requestFn).toHaveBeenCalledTimes(1);

      timeHelpers.setSystemTime(now + 150);

      await cache.dedupe(cacheKey, requestFn);
      expect(requestFn).toHaveBeenCalledTimes(2);
    });

    it('should not expire entries before TTL', async () => {
      const requestFn = createMockRequest('result');
      const cacheKey = 'test-key';

      const now = Date.now();
      timeHelpers.setSystemTime(now);

      await cache.dedupe(cacheKey, requestFn);
      expect(requestFn).toHaveBeenCalledTimes(1);

      timeHelpers.setSystemTime(now + 50);

      await cache.dedupe(cacheKey, requestFn);
      expect(requestFn).toHaveBeenCalledTimes(1);
    });

    it('should auto-cleanup after promise settles', async () => {
      const requestFn = createMockRequest('result');
      const cacheKey = 'test-key';

      await cache.dedupe(cacheKey, requestFn);

      timeHelpers.advanceTime(100);

      const metrics = cache.getMetrics();
      expect(metrics.size).toBe(0);
    });
  });

  describe('LRU Eviction', () => {
    it('should evict oldest entry when cache is full', async () => {
      const requestFn1 = createMockRequest('result1');
      const requestFn2 = createMockRequest('result2');
      const requestFn3 = createMockRequest('result3');
      const requestFn4 = createMockRequest('result4');

      await cache.dedupe('key1', requestFn1);
      await cache.dedupe('key2', requestFn2);
      await cache.dedupe('key3', requestFn3);

      assertionHelpers.expectMetrics(cache, { size: 3, evictions: 0 });

      await cache.dedupe('key4', requestFn4);

      assertionHelpers.expectMetrics(cache, { size: 3, evictions: 1 });

      await cache.dedupe('key1', requestFn1);
      expect(requestFn1).toHaveBeenCalledTimes(2);
    });

    it('should update access order on cache hit', async () => {
      const requestFn1 = createMockRequest('result1');
      const requestFn2 = createMockRequest('result2');
      const requestFn3 = createMockRequest('result3');
      const requestFn4 = createMockRequest('result4');

      await cache.dedupe('key1', requestFn1);
      await cache.dedupe('key2', requestFn2);
      await cache.dedupe('key3', requestFn3);

      await cache.dedupe('key1', requestFn1);

      await cache.dedupe('key4', requestFn4);

      await cache.dedupe('key1', requestFn1);
      expect(requestFn1).toHaveBeenCalledTimes(1);

      await cache.dedupe('key2', requestFn2);
      expect(requestFn2).toHaveBeenCalledTimes(2);
    });
  });

  describe('Metrics', () => {
    it('should track cache hits', async () => {
      const requestFn = createMockRequest('result');
      const cacheKey = 'test-key';

      await cache.dedupe(cacheKey, requestFn);
      await cache.dedupe(cacheKey, requestFn);
      await cache.dedupe(cacheKey, requestFn);

      assertionHelpers.expectMetrics(cache, { hits: 2, misses: 1 });
    });

    it('should track cache misses', async () => {
      const requestFn = createMockRequest('result');

      await cache.dedupe('key1', requestFn);
      await cache.dedupe('key2', requestFn);
      await cache.dedupe('key3', requestFn);

      assertionHelpers.expectMetrics(cache, { hits: 0, misses: 3 });
    });

    it('should track evictions', async () => {
      const requestFn = createMockRequest('result');

      await cache.dedupe('key1', requestFn);
      await cache.dedupe('key2', requestFn);
      await cache.dedupe('key3', requestFn);
      await cache.dedupe('key4', requestFn);
      await cache.dedupe('key5', requestFn);

      const metrics = cache.getMetrics();
      expect(metrics.evictions).toBe(2);
    });

    it('should track expirations', async () => {
      const requestFn = createMockRequest('result');
      const cacheKey = 'test-key';

      const now = Date.now();
      timeHelpers.setSystemTime(now);

      await cache.dedupe(cacheKey, requestFn);

      timeHelpers.setSystemTime(now + 150);

      await cache.dedupe(cacheKey, requestFn);

      const metrics = cache.getMetrics();
      expect(metrics.expirations).toBe(1);
    });

    it('should track current cache size', async () => {
      const requestFn = createMockRequest('result');

      await cache.dedupe('key1', requestFn);
      expect(cache.getMetrics().size).toBe(1);

      await cache.dedupe('key2', requestFn);
      expect(cache.getMetrics().size).toBe(2);

      await cache.dedupe('key3', requestFn);
      expect(cache.getMetrics().size).toBe(3);
    });
  });

  describe('Clear Cache', () => {
    it('should clear all cache entries', async () => {
      const requestFn = createMockRequest('result');

      await cache.dedupe('key1', requestFn);
      await cache.dedupe('key2', requestFn);
      await cache.dedupe('key3', requestFn);

      expect(cache.getMetrics().size).toBe(3);

      cache.clear();

      expect(cache.getMetrics().size).toBe(0);
    });

    it('should allow new entries after clear', async () => {
      const requestFn = createMockRequest('result');

      await cache.dedupe('key1', requestFn);
      cache.clear();

      await cache.dedupe('key2', requestFn);
      expect(cache.getMetrics().size).toBe(1);
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty cache key', async () => {
      const requestFn = createMockRequest('result');
      await cache.dedupe('', requestFn);
      expect(requestFn).toHaveBeenCalledTimes(1);
    });

    it('should handle very long cache keys', async () => {
      const requestFn = createMockRequest('result');
      const longKey = 'a'.repeat(10000);
      await cache.dedupe(longKey, requestFn);
      expect(requestFn).toHaveBeenCalledTimes(1);
    });

    it('should handle complex object bodies', async () => {
      const key1 = cache.generateKey('POST', '/api/test', testObjects.complex, undefined);
      const key2 = cache.generateKey('POST', '/api/test', testObjects.complex, undefined);

      expect(key1).toBe(key2);
    });

    it('should handle null and undefined auth context', () => {
      const key1 = cache.generateKey('GET', '/api/test', undefined, undefined);
      const key2 = cache.generateKey('GET', '/api/test', undefined, null as unknown as undefined);

      expect(key1).toBe(key2);
    });

    it('should handle objects with different property order', () => {
      const key1 = cache.generateKey('POST', '/api/test', testObjects.withOrder1, undefined);
      const key2 = cache.generateKey('POST', '/api/test', testObjects.withOrder2, undefined);
      const key3 = cache.generateKey('POST', '/api/test', { age: 30, city: 'NYC', name: 'John' }, undefined);

      expect(key1).toBe(key2);
      expect(key2).toBe(key3);
    });

    it('should handle circular references', async () => {
      const requestFn = createMockRequest('result');

      const circular: Record<string, unknown> = { name: 'test' };
      circular.self = circular;

      const key = cache.generateKey('POST', '/api/test', circular, undefined);
      await cache.dedupe(key, requestFn);

      expect(requestFn).toHaveBeenCalled();
    });

    it('should handle very large request bodies (DoS protection)', async () => {
      const requestFn = createMockRequest('result');

      const largeBody = {
        data: 'x'.repeat(15000),
      };

      const consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation();

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

  describe('Concurrent Requests', () => {
    it('should handle many concurrent requests to same endpoint', async () => {
      const mockFn = createCountingMockRequest();
      const cacheKey = 'concurrent-test';

      const promises = Array.from({ length: 50 }, () => cache.dedupe(cacheKey, mockFn));

      const results = await Promise.all(promises);

      expect((results as Array<{ data: string }>).every(r => r.data === 'result-1')).toBe(true);
      expect(mockFn).toHaveBeenCalledTimes(1);
    });

    it('should handle concurrent requests to different endpoints', async () => {
      const mockFn = createCountingMockRequest();

      const promises = Array.from({ length: 10 }, (_, i) =>
        cache.dedupe(`endpoint-${i}`, mockFn)
      );

      await Promise.all(promises);

      expect(mockFn).toHaveBeenCalledTimes(10);
    });
  });
});
