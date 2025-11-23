/**
 * HTTP Client Tests (FE-IMP-005)
 *
 * Tests for base HTTP client with error handling and Zod validation.
 */

import { z } from 'zod';
import { HttpClient, getApiBase, downloadFile } from '../core/httpClient';
import { setStoredApiKey, clearStoredApiKey } from '../core/apiKeyStore';
import {
  UnauthorizedError,
  NotFoundError,
  ServerError,
  SchemaValidationError,
  NetworkError,
} from '../core/errors';
import { resetRetryMetrics, getRetryMetrics } from '../core/metrics';
import { globalRequestCache } from '../core/requestCache';
import { resetAllCircuits } from '../core/circuitBreaker';

describe('getApiBase', () => {
  let originalEnv: NodeJS.ProcessEnv;

  beforeEach(() => {
    originalEnv = process.env;
  });

  afterEach(() => {
    process.env = originalEnv;
  });

  it('should return environment variable when set', () => {
    process.env.NEXT_PUBLIC_API_BASE = 'https://api.meepleai.dev';
    expect(getApiBase()).toBe('https://api.meepleai.dev');
  });

  it('should return localhost when env not set', () => {
    delete process.env.NEXT_PUBLIC_API_BASE;
    expect(getApiBase()).toBe('http://localhost:8080');
  });

  it('should return localhost when env is undefined string', () => {
    process.env.NEXT_PUBLIC_API_BASE = 'undefined';
    expect(getApiBase()).toBe('http://localhost:8080');
  });

  it('should return localhost when env is null string', () => {
    process.env.NEXT_PUBLIC_API_BASE = 'null';
    expect(getApiBase()).toBe('http://localhost:8080');
  });

  it('should trim whitespace from env variable', () => {
    process.env.NEXT_PUBLIC_API_BASE = '  https://api.meepleai.dev  ';
    expect(getApiBase()).toBe('https://api.meepleai.dev');
  });
});

describe('HttpClient', () => {
  let mockFetch: jest.Mock;
  let client: HttpClient;
  let mockLocalStorage: { [key: string]: string };
  let mockSessionStorage: { [key: string]: string };

  beforeEach(() => {
    // Clear request cache before each test
    globalRequestCache.clear();
    // Reset circuit breaker to prevent test interference
    resetAllCircuits();

    mockFetch = jest.fn();
    client = new HttpClient({ baseUrl: 'http://localhost:8080', fetchImpl: mockFetch });

    // Mock browser storage
    mockLocalStorage = {};
    mockSessionStorage = {};

    Object.defineProperty(window, 'localStorage', {
      value: {
        getItem: jest.fn((key: string) => mockLocalStorage[key] || null),
        setItem: jest.fn((key: string, value: string) => {
          mockLocalStorage[key] = value;
        }),
      },
      writable: true,
    });

    Object.defineProperty(window, 'sessionStorage', {
      value: {
        getItem: jest.fn((key: string) => mockSessionStorage[key] || null),
        setItem: jest.fn((key: string, value: string) => {
          mockSessionStorage[key] = value;
        }),
        removeItem: jest.fn((key: string) => {
          delete mockSessionStorage[key];
        }),
        clear: jest.fn(() => {
          mockSessionStorage = {};
        }),
      },
      writable: true,
    });
    if (typeof URL.createObjectURL !== 'function') {
      URL.createObjectURL = () => 'blob:mock';
    }
    if (typeof URL.revokeObjectURL !== 'function') {
      URL.revokeObjectURL = () => {};
    }

    // Mock crypto.randomUUID
    Object.defineProperty(global, 'crypto', {
      value: {
        randomUUID: jest.fn(() => 'test-uuid-123'),
      },
      writable: true,
    });
  });

  describe('GET requests', () => {
    it('should make GET request with correct headers', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ data: 'test' }),
        headers: new Headers(),
      });

      await client.get('/api/v1/test');

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/test',
        expect.objectContaining({
          method: 'GET',
          credentials: 'include',
          headers: expect.objectContaining({
            'X-Correlation-ID': 'test-uuid-123',
          }),
        })
      );
    });

    it('should return null for 401 responses', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401,
        headers: new Headers(),
      });

      const result = await client.get('/api/v1/test');

      expect(result).toBeNull();
    });

    it('should validate response with Zod schema', async () => {
      const schema = z.object({ id: z.string(), name: z.string() });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ id: '123', name: 'Test' }),
        headers: new Headers(),
      });

      const result = await client.get('/api/v1/test', schema);

      expect(result).toEqual({ id: '123', name: 'Test' });
    });

    it('should throw SchemaValidationError on invalid response', async () => {
      const schema = z.object({ id: z.string(), name: z.string() });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ id: 123 }), // Invalid: missing name, wrong id type
        headers: new Headers(),
      });

      await expect(client.get('/api/v1/test', schema)).rejects.toThrow(
        SchemaValidationError
      );
    });

    it('should include Authorization header when an API key is stored', async () => {
      await setStoredApiKey('mpl_test_demo');

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ ok: true }),
        headers: new Headers(),
      });

      await client.get('/api/v1/test');

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/test',
        expect.objectContaining({
          headers: expect.objectContaining({
            Authorization: 'ApiKey mpl_test_demo',
          }),
        })
      );

      clearStoredApiKey();
    });

    it('should throw error for non-401 failures', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 404,
        json: async () => ({ error: 'Not found' }),
        headers: new Headers({ 'X-Correlation-Id': 'test-404' }),
      });

      await expect(client.get('/api/v1/test')).rejects.toThrow(NotFoundError);
    });
  });

  describe('POST requests', () => {
    it('should make POST request with JSON body', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ success: true }),
        headers: new Headers(),
      });

      await client.post('/api/v1/test', { key: 'value' });

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/test',
        expect.objectContaining({
          method: 'POST',
          credentials: 'include',
          headers: expect.objectContaining({
            'Content-Type': 'application/json',
          }),
          body: JSON.stringify({ key: 'value' }),
        })
      );
    });

    it('should throw error for 401 responses', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401,
        json: async () => ({ error: 'Unauthorized' }),
        headers: new Headers(),
      });

      await expect(client.post('/api/v1/test', {})).rejects.toThrow(
        UnauthorizedError
      );
    });

    it('should handle 204 No Content responses', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 204,
        headers: new Headers(),
      });

      const result = await client.post('/api/v1/test', {});

      expect(result).toBeUndefined();
    });

    it('should validate response with Zod schema', async () => {
      const schema = z.object({ success: z.boolean() });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ success: true }),
        headers: new Headers(),
      });

      const result = await client.post('/api/v1/test', {}, schema);

      expect(result).toEqual({ success: true });
    });

    it('should handle empty body', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ success: true }),
        headers: new Headers(),
      });

      await client.post('/api/v1/test');

      expect(mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          body: JSON.stringify({}),
        })
      );
    });
  });

  describe('PUT requests', () => {
    it('should make PUT request with JSON body', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ updated: true }),
        headers: new Headers(),
      });

      await client.put('/api/v1/test', { key: 'updated' });

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/test',
        expect.objectContaining({
          method: 'PUT',
          credentials: 'include',
          headers: expect.objectContaining({
            'Content-Type': 'application/json',
          }),
          body: JSON.stringify({ key: 'updated' }),
        })
      );
    });

    it('should throw error for 401 responses', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401,
        json: async () => ({ error: 'Unauthorized' }),
        headers: new Headers(),
      });

      await expect(client.put('/api/v1/test', {})).rejects.toThrow(
        UnauthorizedError
      );
    });

    it('should validate response with Zod schema', async () => {
      const schema = z.object({ updated: z.boolean() });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({ updated: true }),
        headers: new Headers(),
      });

      const result = await client.put('/api/v1/test', {}, schema);

      expect(result).toEqual({ updated: true });
    });
  });

  describe('DELETE requests', () => {
    it('should make DELETE request', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 204,
        headers: new Headers(),
      });

      await client.delete('/api/v1/test');

      expect(mockFetch).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/test',
        expect.objectContaining({
          method: 'DELETE',
          credentials: 'include',
        })
      );
    });

    it('should throw error for 401 responses', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401,
        json: async () => ({ error: 'Unauthorized' }),
        headers: new Headers(),
      });

      await expect(client.delete('/api/v1/test')).rejects.toThrow(
        UnauthorizedError
      );
    });

    it('should not attempt to parse response body', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 204,
        headers: new Headers(),
      });

      const result = await client.delete('/api/v1/test');

      expect(result).toBeUndefined();
    });
  });

  describe('postFile (file downloads)', () => {
    it('should handle file download with blob response', async () => {
      const blob = new Blob(['test content'], { type: 'application/json' });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        blob: async () => blob,
        headers: new Headers({
          'Content-Disposition': 'attachment; filename="export.json"',
        }),
      });

      const result = await client.postFile('/api/v1/export', { format: 'json' });

      expect(result.blob).toBe(blob);
      expect(result.filename).toBe('export.json');
    });

    it('should extract filename from Content-Disposition header', async () => {
      const blob = new Blob(['test'], { type: 'text/plain' });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        blob: async () => blob,
        headers: new Headers({
          'Content-Disposition': 'attachment; filename="test-file.txt"',
        }),
      });

      const result = await client.postFile('/api/v1/export', {});

      expect(result.filename).toBe('test-file.txt');
    });

    it('should use default filename if header missing', async () => {
      const blob = new Blob(['test'], { type: 'text/plain' });

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        blob: async () => blob,
        headers: new Headers(),
      });

      const result = await client.postFile('/api/v1/export', {});

      expect(result.filename).toMatch(/^download-\d+$/);
    });

    it('should throw error for 401 responses', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401,
        json: async () => ({ error: 'Unauthorized' }),
        headers: new Headers(),
      });

      await expect(client.postFile('/api/v1/export', {})).rejects.toThrow(
        UnauthorizedError
      );
    });
  });

  describe('error handling', () => {
    it('should skip error logging when requested', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation();

      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 500,
        json: async () => ({ error: 'Server error' }),
        headers: new Headers(),
      });

      await expect(
        client.get('/api/v1/test', undefined, {
          skipErrorLogging: true,
          retry: { skipRetry: true }, // Skip retry to test error handling directly
        })
      ).rejects.toThrow(ServerError);

      // Logger should not be called
      expect(consoleErrorSpy).not.toHaveBeenCalled();

      consoleErrorSpy.mockRestore();
    });

    it('should extract correlation ID from response headers', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 500,
        json: async () => ({ error: 'Server error' }),
        headers: new Headers({ 'X-Correlation-Id': 'test-correlation-xyz' }),
      });

      try {
        await client.get('/api/v1/test', undefined, {
          retry: { skipRetry: true }, // Skip retry to test error handling directly
        });
      } catch (error) {
        expect((error as ServerError).correlationId).toBe('test-correlation-xyz');
      }
    });
  });

  describe('correlation ID management', () => {
    it('should generate correlation ID on first request', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({}),
        headers: new Headers(),
      });

      await client.get('/api/v1/test');

      expect(mockSessionStorage['correlation_id']).toBe('test-uuid-123');
    });

    it('should reuse existing correlation ID', async () => {
      mockSessionStorage['correlation_id'] = 'existing-correlation-id';

      mockFetch.mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({}),
        headers: new Headers(),
      });

      await client.get('/api/v1/test');

      expect(mockFetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          headers: expect.objectContaining({
            'X-Correlation-ID': 'existing-correlation-id',
          }),
        })
      );
    });
  });

  describe('retry logic (Issue #1453)', () => {
    beforeEach(() => {
      resetRetryMetrics();
    });

    it('should retry on 500 error and succeed', async () => {
      // First call fails with 500, second call succeeds
      mockFetch
        .mockResolvedValueOnce({
          ok: false,
          status: 500,
          json: async () => ({ error: 'Server error' }),
          headers: new Headers(),
        })
        .mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => ({ data: 'success' }),
          headers: new Headers(),
        });

      const result = await client.get('/api/v1/test', undefined, {
        retry: {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        },
      });

      expect(result).toEqual({ data: 'success' });
      expect(mockFetch).toHaveBeenCalledTimes(2);
    });

    it('should retry on 503 error', async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: false,
          status: 503,
          json: async () => ({ error: 'Service Unavailable' }),
          headers: new Headers(),
        })
        .mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => ({ data: 'success' }),
          headers: new Headers(),
        });

      const result = await client.get('/api/v1/test', undefined, {
        retry: {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        },
      });

      expect(result).toEqual({ data: 'success' });
      expect(mockFetch).toHaveBeenCalledTimes(2);
    });

    it('should not retry on 404 error', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 404,
        json: async () => ({ error: 'Not Found' }),
        headers: new Headers(),
      });

      await expect(
        client.get('/api/v1/test', undefined, {
          retry: {
            retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
          },
        })
      ).rejects.toThrow(NotFoundError);

      expect(mockFetch).toHaveBeenCalledTimes(1);
    });

    it('should not retry on 401 error', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401,
        json: async () => ({ error: 'Unauthorized' }),
        headers: new Headers(),
      });

      const result = await client.get('/api/v1/test', undefined, {
        retry: {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        },
      });

      // GET returns null for 401
      expect(result).toBeNull();
      expect(mockFetch).toHaveBeenCalledTimes(1);
    });

    it('should skip retry when skipRetry is true', async () => {
      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 500,
        json: async () => ({ error: 'Server error' }),
        headers: new Headers(),
      });

      await expect(
        client.get('/api/v1/test', undefined, {
          retry: { skipRetry: true },
        })
      ).rejects.toThrow(ServerError);

      expect(mockFetch).toHaveBeenCalledTimes(1);
    });

    it('should record retry metrics', async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: false,
          status: 500,
          json: async () => ({ error: 'Server error' }),
          headers: new Headers(),
        })
        .mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => ({ data: 'success' }),
          headers: new Headers(),
        });

      await client.get('/api/v1/test', undefined, {
        retry: {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        },
      });

      const metrics = getRetryMetrics();
      expect(metrics.totalRetries).toBeGreaterThan(0);
      expect(metrics.successAfterRetry).toBe(1);
    });

    it('should call onRetry callback', async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: false,
          status: 500,
          json: async () => ({ error: 'Server error' }),
          headers: new Headers(),
        })
        .mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => ({ data: 'success' }),
          headers: new Headers(),
        });

      const onRetry = jest.fn();

      await client.get('/api/v1/test', undefined, {
        retry: {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
          onRetry,
        },
      });

      expect(onRetry).toHaveBeenCalledTimes(1);
      expect(onRetry).toHaveBeenCalledWith(1, expect.any(ServerError), expect.any(Number));
    });
  });

  describe('Request Deduplication (Issue #1454)', () => {
    beforeEach(() => {
      // Clear cache before each test
      globalRequestCache.clear();
    });

    afterEach(() => {
      globalRequestCache.clear();
    });

    it('should deduplicate identical GET requests by default', async () => {
      let callCount = 0;
      mockFetch.mockImplementation(async () => {
        callCount++;
        return {
          ok: true,
          status: 200,
          json: async () => ({ data: `result-${callCount}` }),
          headers: new Headers(),
        };
      });

      // Execute three simultaneous GET requests
      const promises = [
        client.get('/api/v1/users'),
        client.get('/api/v1/users'),
        client.get('/api/v1/users'),
      ];

      const results = await Promise.all(promises);

      // All should return same result
      expect(results).toEqual([
        { data: 'result-1' },
        { data: 'result-1' },
        { data: 'result-1' },
      ]);

      // Fetch should only be called once
      expect(callCount).toBe(1);
    });

    it('should not deduplicate GET requests when skipDedup is true', async () => {
      let callCount = 0;
      mockFetch.mockImplementation(async () => {
        callCount++;
        return {
          ok: true,
          status: 200,
          json: async () => ({ data: `result-${callCount}` }),
          headers: new Headers(),
        };
      });

      // Execute three requests with skipDedup
      await client.get('/api/v1/users', undefined, { skipDedup: true });
      await client.get('/api/v1/users', undefined, { skipDedup: true });
      await client.get('/api/v1/users', undefined, { skipDedup: true });

      // Fetch should be called three times
      expect(callCount).toBe(3);
    });

    it('should not deduplicate POST requests by default', async () => {
      let callCount = 0;
      mockFetch.mockImplementation(async () => {
        callCount++;
        return {
          ok: true,
          status: 200,
          json: async () => ({ success: true, count: callCount }),
          headers: new Headers(),
        };
      });

      // Execute three POST requests
      await client.post('/api/v1/users', { name: 'John' });
      await client.post('/api/v1/users', { name: 'John' });
      await client.post('/api/v1/users', { name: 'John' });

      // Fetch should be called three times (not deduplicated)
      expect(callCount).toBe(3);
    });

    it('should deduplicate POST requests when skipDedup is false', async () => {
      let callCount = 0;
      mockFetch.mockImplementation(async () => {
        callCount++;
        return {
          ok: true,
          status: 200,
          json: async () => ({ success: true, count: callCount }),
          headers: new Headers(),
        };
      });

      // Execute three POST requests with skipDedup=false
      const promises = [
        client.post('/api/v1/users', { name: 'John' }, undefined, { skipDedup: false }),
        client.post('/api/v1/users', { name: 'John' }, undefined, { skipDedup: false }),
        client.post('/api/v1/users', { name: 'John' }, undefined, { skipDedup: false }),
      ];

      await Promise.all(promises);

      // Fetch should be called once (deduplicated)
      expect(callCount).toBe(1);
    });

    it('should not deduplicate PUT requests by default', async () => {
      let callCount = 0;
      mockFetch.mockImplementation(async () => {
        callCount++;
        return {
          ok: true,
          status: 200,
          json: async () => ({ updated: true, count: callCount }),
          headers: new Headers(),
        };
      });

      await client.put('/api/v1/users/1', { name: 'Updated' });
      await client.put('/api/v1/users/1', { name: 'Updated' });

      expect(callCount).toBe(2);
    });

    it('should not deduplicate DELETE requests by default', async () => {
      let callCount = 0;
      mockFetch.mockImplementation(async () => {
        callCount++;
        return {
          ok: true,
          status: 204,
          headers: new Headers(),
        };
      });

      await client.delete('/api/v1/users/1');
      await client.delete('/api/v1/users/1');

      expect(callCount).toBe(2);
    });

    it('should generate different cache keys for different URLs', async () => {
      let callCount = 0;
      mockFetch.mockImplementation(async () => {
        callCount++;
        return {
          ok: true,
          status: 200,
          json: async () => ({ data: `result-${callCount}` }),
          headers: new Headers(),
        };
      });

      const promises = [
        client.get('/api/v1/users'),
        client.get('/api/v1/games'),
      ];

      await Promise.all(promises);

      // Different URLs should not be deduplicated
      expect(callCount).toBe(2);
    });

    it('should generate different cache keys for different request bodies', async () => {
      let callCount = 0;
      mockFetch.mockImplementation(async () => {
        callCount++;
        return {
          ok: true,
          status: 200,
          json: async () => ({ success: true }),
          headers: new Headers(),
        };
      });

      const promises = [
        client.post('/api/v1/test', { data: 'test1' }),
        client.post('/api/v1/test', { data: 'test2' }),
      ];

      await Promise.all(promises);

      // Different bodies should not be deduplicated (even though dedup is disabled by default for POST)
      expect(callCount).toBe(2);
    });

    it('should work with PUT requests', async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: false,
          status: 503,
          json: async () => ({ error: 'Service Unavailable' }),
          headers: new Headers(),
        })
        .mockResolvedValueOnce({
          ok: true,
          status: 200,
          json: async () => ({ updated: true }),
          headers: new Headers(),
        });

      const result = await client.put('/api/v1/test', { data: 'updated' }, undefined, {
        retry: {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        },
      });

      expect(result).toEqual({ updated: true });
      expect(mockFetch).toHaveBeenCalledTimes(2);
    });

    it('should work with DELETE requests', async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: false,
          status: 500,
          json: async () => ({ error: 'Server error' }),
          headers: new Headers(),
        })
        .mockResolvedValueOnce({
          ok: true,
          status: 204,
          headers: new Headers(),
        });

      await client.delete('/api/v1/test', {
        retry: {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        },
      });

      expect(mockFetch).toHaveBeenCalledTimes(2);
    });

    it('should exhaust retries and throw last error', async () => {
      mockFetch
        .mockResolvedValue({
          ok: false,
          status: 500,
          json: async () => ({ error: 'Server error' }),
          headers: new Headers(),
        });

      await expect(
        client.get('/api/v1/test', undefined, {
          retry: {
            retryConfig: { maxAttempts: 2, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
          },
        })
      ).rejects.toThrow(ServerError);

      // 1 initial attempt + 2 retries = 3 total
      expect(mockFetch).toHaveBeenCalledTimes(3);

      const metrics = getRetryMetrics();
      expect(metrics.failedAfterRetry).toBe(1);
    });

    it('should generate different cache keys for different auth contexts', async () => {
      let callCount = 0;
      mockFetch.mockImplementation(async () => {
        callCount++;
        return {
          ok: true,
          status: 200,
          json: async () => ({ data: `result-${callCount}` }),
          headers: new Headers(),
        };
      });

      // First request without API key
      await client.get('/api/v1/profile');

      // Second request with API key
      await setStoredApiKey('mpl_test_demo');
      await client.get('/api/v1/profile');
      clearStoredApiKey();

      // Different auth contexts should not be deduplicated
      expect(callCount).toBe(2);
    });

    it('should handle failed requests and allow retry', async () => {
      let callCount = 0;
      mockFetch.mockImplementation(async () => {
        callCount++;
        if (callCount === 1) {
          return {
            ok: false,
            status: 500,
            json: async () => ({ error: 'Server error' }),
            headers: new Headers(),
          };
        }
        return {
          ok: true,
          status: 200,
          json: async () => ({ data: 'success' }),
          headers: new Headers(),
        };
      });

      // First request should fail (skip retry to test deduplication directly)
      await expect(
        client.get('/api/v1/users', undefined, { retry: { skipRetry: true } })
      ).rejects.toThrow();

      // Second request should succeed (cache cleared on failure)
      const result = await client.get('/api/v1/users', undefined, { retry: { skipRetry: true } });
      expect(result).toEqual({ data: 'success' });
      expect(callCount).toBe(2);
    });

    it('should track cache metrics', async () => {
      mockFetch.mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ data: 'test' }),
        headers: new Headers(),
      });

      const initialMetrics = globalRequestCache.getMetrics();
      const initialHits = initialMetrics.hits;
      const initialMisses = initialMetrics.misses;

      // First request - cache miss
      await client.get('/api/v1/test');

      // Second request - cache hit
      await client.get('/api/v1/test');

      const finalMetrics = globalRequestCache.getMetrics();
      expect(finalMetrics.misses).toBe(initialMisses + 1);
      expect(finalMetrics.hits).toBe(initialHits + 1);
    });

    it('should NOT deduplicate requests with custom onRetry callback (Issue #1453)', async () => {
      const responses = [
        { ok: true, status: 200, json: async () => ({ data: 'result-A' }), headers: new Headers() },
        { ok: true, status: 200, json: async () => ({ data: 'result-B' }), headers: new Headers() },
      ];
      let callIndex = 0;

      mockFetch.mockImplementation(async () => {
        const response = responses[callIndex % responses.length];
        callIndex++;
        return response;
      });

      const onRetryCallback = () => {
        // No-op callback
      };

      // Execute two simultaneous GET requests with custom onRetry callback
      const promises = [
        client.get('/api/v1/users', undefined, {
          retry: { onRetry: onRetryCallback },
        }),
        client.get('/api/v1/users', undefined, {
          retry: { onRetry: onRetryCallback },
        }),
      ];

      const results = await Promise.all(promises);

      // Both requests should execute (not deduplicated) because of custom callback
      expect(callIndex).toBe(2);
      // Results should be different
      const resultData = results.map(r => r.data).sort();
      expect(resultData).toEqual(['result-A', 'result-B']);
    });

    it('should deduplicate requests without onRetry callback (Issue #1453)', async () => {
      let callCount = 0;

      mockFetch.mockImplementation(async () => {
        callCount++;
        return {
          ok: true,
          status: 200,
          json: async () => ({ data: `result-${callCount}` }),
          headers: new Headers(),
        };
      });

      // Execute two simultaneous GET requests WITHOUT custom onRetry callback
      const promises = [
        client.get('/api/v1/users', undefined, {
          retry: { retryConfig: { maxAttempts: 3 } },
        }),
        client.get('/api/v1/users', undefined, {
          retry: { retryConfig: { maxAttempts: 3 } },
        }),
      ];

      const results = await Promise.all(promises);

      // Only one request should execute (deduplicated)
      expect(callCount).toBe(1);
      expect(results[0]).toEqual({ data: 'result-1' });
      expect(results[1]).toEqual({ data: 'result-1' });
    });
  });
});

describe('downloadFile', () => {
  let mockCreateElement: jest.SpyInstance;
  let mockAppendChild: jest.SpyInstance;
  let mockRemoveChild: jest.SpyInstance;
  let mockCreateObjectURL: jest.SpyInstance;
  let mockRevokeObjectURL: jest.SpyInstance;
  let mockLink: { href: string; download: string; click: jest.Mock };

  beforeEach(() => {
    mockLink = {
      href: '',
      download: '',
      click: jest.fn(),
    };

    mockCreateElement = jest.spyOn(document, 'createElement').mockReturnValue(mockLink as any);
    mockAppendChild = jest.spyOn(document.body, 'appendChild').mockImplementation();
    mockRemoveChild = jest.spyOn(document.body, 'removeChild').mockImplementation();
    mockCreateObjectURL = jest.spyOn(URL, 'createObjectURL').mockReturnValue('blob:test-url');
    mockRevokeObjectURL = jest.spyOn(URL, 'revokeObjectURL').mockImplementation();
  });

  afterEach(() => {
    mockCreateElement.mockRestore();
    mockAppendChild.mockRestore();
    mockRemoveChild.mockRestore();
    mockCreateObjectURL.mockRestore();
    mockRevokeObjectURL.mockRestore();
  });

  it('should trigger browser download', () => {
    const blob = new Blob(['test content'], { type: 'text/plain' });

    downloadFile(blob, 'test.txt');

    expect(mockCreateObjectURL).toHaveBeenCalledWith(blob);
    expect(mockLink.href).toBe('blob:test-url');
    expect(mockLink.download).toBe('test.txt');
    expect(mockAppendChild).toHaveBeenCalledWith(mockLink);
    expect(mockLink.click).toHaveBeenCalled();
    expect(mockRemoveChild).toHaveBeenCalledWith(mockLink);
    expect(mockRevokeObjectURL).toHaveBeenCalledWith('blob:test-url');
  });
});
