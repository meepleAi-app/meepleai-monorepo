/**
 * HTTP Client Request Deduplication and Download Tests
 *
 * Tests for request deduplication (Issue #1454) and downloadFile utility.
 */

import { downloadFile } from '../core/httpClient';
import { setStoredApiKey, clearStoredApiKey } from '../core/apiKeyStore';
import { globalRequestCache } from '../core/requestCache';
import {
  setupTestEnvironment,
  setupDownloadMocks,
  createSuccessResponse,
  createErrorResponse,
  type TestSetup,
} from './httpClient.test-helpers';

describe('HttpClient - Request Deduplication (Issue #1454)', () => {
  let setup: TestSetup;

  beforeEach(() => {
    setup = setupTestEnvironment();
    // Clear cache before each test
    globalRequestCache.clear();
  });

  afterEach(() => {
    globalRequestCache.clear();
  });

  it('should deduplicate identical GET requests by default', async () => {
    let callCount = 0;
    setup.mockFetch.mockImplementation(async () => {
      callCount++;
      return createSuccessResponse({ data: `result-${callCount}` });
    });

    // Execute three simultaneous GET requests
    const promises = [
      setup.client.get('/api/v1/users'),
      setup.client.get('/api/v1/users'),
      setup.client.get('/api/v1/users'),
    ];

    const results = await Promise.all(promises);

    // All should return same result
    expect(results).toEqual([{ data: 'result-1' }, { data: 'result-1' }, { data: 'result-1' }]);

    // Fetch should only be called once
    expect(callCount).toBe(1);
  });

  it('should not deduplicate GET requests when skipDedup is true', async () => {
    let callCount = 0;
    setup.mockFetch.mockImplementation(async () => {
      callCount++;
      return createSuccessResponse({ data: `result-${callCount}` });
    });

    // Execute three requests with skipDedup
    await setup.client.get('/api/v1/users', undefined, { skipDedup: true });
    await setup.client.get('/api/v1/users', undefined, { skipDedup: true });
    await setup.client.get('/api/v1/users', undefined, { skipDedup: true });

    // Fetch should be called three times
    expect(callCount).toBe(3);
  });

  it('should not deduplicate POST requests by default', async () => {
    let callCount = 0;
    setup.mockFetch.mockImplementation(async () => {
      callCount++;
      return createSuccessResponse({ success: true, count: callCount });
    });

    // Execute three POST requests
    await setup.client.post('/api/v1/users', { name: 'John' });
    await setup.client.post('/api/v1/users', { name: 'John' });
    await setup.client.post('/api/v1/users', { name: 'John' });

    // Fetch should be called three times (not deduplicated)
    expect(callCount).toBe(3);
  });

  it('should deduplicate POST requests when skipDedup is false', async () => {
    let callCount = 0;
    setup.mockFetch.mockImplementation(async () => {
      callCount++;
      return createSuccessResponse({ success: true, count: callCount });
    });

    // Execute three POST requests with skipDedup=false
    const promises = [
      setup.client.post('/api/v1/users', { name: 'John' }, undefined, { skipDedup: false }),
      setup.client.post('/api/v1/users', { name: 'John' }, undefined, { skipDedup: false }),
      setup.client.post('/api/v1/users', { name: 'John' }, undefined, { skipDedup: false }),
    ];

    await Promise.all(promises);

    // Fetch should be called once (deduplicated)
    expect(callCount).toBe(1);
  });

  it('should not deduplicate PUT requests by default', async () => {
    let callCount = 0;
    setup.mockFetch.mockImplementation(async () => {
      callCount++;
      return createSuccessResponse({ updated: true, count: callCount });
    });

    await setup.client.put('/api/v1/users/1', { name: 'Updated' });
    await setup.client.put('/api/v1/users/1', { name: 'Updated' });

    expect(callCount).toBe(2);
  });

  it('should not deduplicate DELETE requests by default', async () => {
    let callCount = 0;
    setup.mockFetch.mockImplementation(async () => {
      callCount++;
      return {
        ok: true,
        status: 204,
        headers: new Headers(),
      };
    });

    await setup.client.delete('/api/v1/users/1');
    await setup.client.delete('/api/v1/users/1');

    expect(callCount).toBe(2);
  });

  it('should generate different cache keys for different URLs', async () => {
    let callCount = 0;
    setup.mockFetch.mockImplementation(async () => {
      callCount++;
      return createSuccessResponse({ data: `result-${callCount}` });
    });

    const promises = [setup.client.get('/api/v1/users'), setup.client.get('/api/v1/games')];

    await Promise.all(promises);

    // Different URLs should not be deduplicated
    expect(callCount).toBe(2);
  });

  it('should generate different cache keys for different request bodies', async () => {
    let callCount = 0;
    setup.mockFetch.mockImplementation(async () => {
      callCount++;
      return createSuccessResponse({ success: true });
    });

    const promises = [
      setup.client.post('/api/v1/test', { data: 'test1' }),
      setup.client.post('/api/v1/test', { data: 'test2' }),
    ];

    await Promise.all(promises);

    // Different bodies should not be deduplicated (even though dedup is disabled by default for POST)
    expect(callCount).toBe(2);
  });

  it('should generate different cache keys for different auth contexts', async () => {
    let callCount = 0;
    setup.mockFetch.mockImplementation(async () => {
      callCount++;
      return createSuccessResponse({ data: `result-${callCount}` });
    });

    // First request without API key
    await setup.client.get('/api/v1/profile');

    // Second request with API key
    await setStoredApiKey('mpl_test_demo');
    await setup.client.get('/api/v1/profile');
    clearStoredApiKey();

    // Different auth contexts should not be deduplicated
    expect(callCount).toBe(2);
  });

  it('should handle failed requests and allow retry', async () => {
    let callCount = 0;
    setup.mockFetch.mockImplementation(async () => {
      callCount++;
      if (callCount === 1) {
        return createErrorResponse(500, { error: 'Server error' });
      }
      return createSuccessResponse({ data: 'success' });
    });

    // First request should fail (skip retry to test deduplication directly)
    await expect(
      setup.client.get('/api/v1/users', undefined, { retry: { skipRetry: true } })
    ).rejects.toThrow();

    // Second request should succeed (cache cleared on failure)
    const result = await setup.client.get('/api/v1/users', undefined, {
      retry: { skipRetry: true },
    });
    expect(result).toEqual({ data: 'success' });
    expect(callCount).toBe(2);
  });

  it('should track cache metrics', async () => {
    setup.mockFetch.mockResolvedValue(createSuccessResponse({ data: 'test' }));

    const initialMetrics = globalRequestCache.getMetrics();
    const initialHits = initialMetrics.hits;
    const initialMisses = initialMetrics.misses;

    // First request - cache miss
    await setup.client.get('/api/v1/test');

    // Second request - cache hit
    await setup.client.get('/api/v1/test');

    const finalMetrics = globalRequestCache.getMetrics();
    expect(finalMetrics.misses).toBe(initialMisses + 1);
    expect(finalMetrics.hits).toBe(initialHits + 1);
  });

  it('should NOT deduplicate requests with custom onRetry callback (Issue #1453)', async () => {
    const responses = [
      createSuccessResponse({ data: 'result-A' }),
      createSuccessResponse({ data: 'result-B' }),
    ];
    let callIndex = 0;

    setup.mockFetch.mockImplementation(async () => {
      const response = responses[callIndex % responses.length];
      callIndex++;
      return response;
    });

    const onRetryCallback = () => {
      // No-op callback
    };

    // Execute two simultaneous GET requests with custom onRetry callback
    const promises = [
      setup.client.get('/api/v1/users', undefined, {
        retry: { onRetry: onRetryCallback },
      }),
      setup.client.get('/api/v1/users', undefined, {
        retry: { onRetry: onRetryCallback },
      }),
    ];

    const results = await Promise.all(promises);

    // Both requests should execute (not deduplicated) because of custom callback
    expect(callIndex).toBe(2);
    // Results should be different
    const resultData = (results as Array<{ data: string }>).map(r => r.data).sort();
    expect(resultData).toEqual(['result-A', 'result-B']);
  });

  it('should deduplicate requests without onRetry callback (Issue #1453)', async () => {
    let callCount = 0;

    setup.mockFetch.mockImplementation(async () => {
      callCount++;
      return createSuccessResponse({ data: `result-${callCount}` });
    });

    // Execute two simultaneous GET requests WITHOUT custom onRetry callback
    const promises = [
      setup.client.get('/api/v1/users', undefined, {
        retry: { retryConfig: { maxAttempts: 3 } },
      }),
      setup.client.get('/api/v1/users', undefined, {
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

describe('downloadFile', () => {
  let mocks: ReturnType<typeof setupDownloadMocks>;

  beforeEach(() => {
    mocks = setupDownloadMocks();
  });

  afterEach(() => {
    mocks.cleanup();
  });

  it('should trigger browser download', () => {
    const blob = new Blob(['test content'], { type: 'text/plain' });

    downloadFile(blob, 'test.txt');

    expect(mocks.mockCreateObjectURL).toHaveBeenCalledWith(blob);
    expect(mocks.mockLink.href).toBe('blob:test-url');
    expect(mocks.mockLink.download).toBe('test.txt');
    expect(mocks.mockAppendChild).toHaveBeenCalledWith(mocks.mockLink);
    expect(mocks.mockLink.click).toHaveBeenCalled();
    expect(mocks.mockRemoveChild).toHaveBeenCalledWith(mocks.mockLink);
    expect(mocks.mockRevokeObjectURL).toHaveBeenCalledWith('blob:test-url');
  });
});
