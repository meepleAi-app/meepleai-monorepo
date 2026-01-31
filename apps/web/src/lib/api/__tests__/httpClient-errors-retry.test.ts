/**
 * HTTP Client Error Handling and Retry Tests
 *
 * Tests for error handling, retry logic, correlation IDs, and circuit breaker.
 */

import { ServerError, NotFoundError, UnauthorizedError } from '../core/errors';
import { resetRetryMetrics, getRetryMetrics } from '../core/metrics';
import {
  setupTestEnvironment,
  createSuccessResponse,
  createErrorResponse,
  type TestSetup,
} from './httpClient.test-helpers';

describe('HttpClient - Error Handling', () => {
  let setup: TestSetup;

  beforeEach(() => {
    setup = setupTestEnvironment();
  });

  describe('error handling', () => {
    it('should skip error logging when requested', async () => {
      const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation();

      setup.mockFetch.mockResolvedValueOnce(
        createErrorResponse(500, { error: 'Server error' })
      );

      await expect(
        setup.client.get('/api/v1/test', undefined, {
          skipErrorLogging: true,
          retry: { skipRetry: true }, // Skip retry to test error handling directly
        })
      ).rejects.toThrow(ServerError);

      // Logger should not be called
      expect(consoleErrorSpy).not.toHaveBeenCalled();

      consoleErrorSpy.mockRestore();
    });

    it('should extract correlation ID from response headers', async () => {
      setup.mockFetch.mockResolvedValueOnce(
        createErrorResponse(500, { error: 'Server error' }, 'test-correlation-xyz')
      );

      try {
        await setup.client.get('/api/v1/test', undefined, {
          retry: { skipRetry: true }, // Skip retry to test error handling directly
        });
      } catch (error) {
        expect((error as ServerError).correlationId).toBe('test-correlation-xyz');
      }
    });
  });

  describe('correlation ID management', () => {
    it('should generate correlation ID on first request', async () => {
      setup.mockFetch.mockResolvedValueOnce(createSuccessResponse({}));

      await setup.client.get('/api/v1/test');

      expect(setup.storages.sessionStorage['correlation_id']).toBe('test-uuid-123');
    });

    it('should reuse existing correlation ID', async () => {
      setup.storages.sessionStorage['correlation_id'] = 'existing-correlation-id';

      setup.mockFetch.mockResolvedValueOnce(createSuccessResponse({}));

      await setup.client.get('/api/v1/test');

      expect(setup.mockFetch).toHaveBeenCalledWith(
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
      setup = setupTestEnvironment();
    });

    it('should retry on 500 error and succeed', async () => {
      // First call fails with 500, second call succeeds
      setup.mockFetch
        .mockResolvedValueOnce(createErrorResponse(500, { error: 'Server error' }))
        .mockResolvedValueOnce(createSuccessResponse({ data: 'success' }));

      const result = await setup.client.get('/api/v1/test', undefined, {
        retry: {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        },
      });

      expect(result).toEqual({ data: 'success' });
      expect(setup.mockFetch).toHaveBeenCalledTimes(2);
    });

    it('should retry on 503 error', async () => {
      setup.mockFetch
        .mockResolvedValueOnce(createErrorResponse(503, { error: 'Service Unavailable' }))
        .mockResolvedValueOnce(createSuccessResponse({ data: 'success' }));

      const result = await setup.client.get('/api/v1/test', undefined, {
        retry: {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        },
      });

      expect(result).toEqual({ data: 'success' });
      expect(setup.mockFetch).toHaveBeenCalledTimes(2);
    });

    it('should not retry on 404 error', async () => {
      setup.mockFetch.mockResolvedValueOnce(createErrorResponse(404, { error: 'Not Found' }));

      await expect(
        setup.client.get('/api/v1/test', undefined, {
          retry: {
            retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
          },
        })
      ).rejects.toThrow(NotFoundError);

      expect(setup.mockFetch).toHaveBeenCalledTimes(1);
    });

    it('should not retry on 401 error', async () => {
      setup.mockFetch.mockResolvedValueOnce(
        createErrorResponse(401, { error: 'Unauthorized' })
      );

      const result = await setup.client.get('/api/v1/test', undefined, {
        retry: {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        },
      });

      // GET returns null for 401
      expect(result).toBeNull();
      expect(setup.mockFetch).toHaveBeenCalledTimes(1);
    });

    it('should skip retry when skipRetry is true', async () => {
      setup.mockFetch.mockResolvedValueOnce(createErrorResponse(500, { error: 'Server error' }));

      await expect(
        setup.client.get('/api/v1/test', undefined, {
          retry: { skipRetry: true },
        })
      ).rejects.toThrow(ServerError);

      expect(setup.mockFetch).toHaveBeenCalledTimes(1);
    });

    it('should record retry metrics', async () => {
      setup.mockFetch
        .mockResolvedValueOnce(createErrorResponse(500, { error: 'Server error' }))
        .mockResolvedValueOnce(createSuccessResponse({ data: 'success' }));

      await setup.client.get('/api/v1/test', undefined, {
        retry: {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        },
      });

      const metrics = getRetryMetrics();
      expect(metrics.totalRetries).toBeGreaterThan(0);
      expect(metrics.successAfterRetry).toBe(1);
    });

    it('should call onRetry callback', async () => {
      setup.mockFetch
        .mockResolvedValueOnce(createErrorResponse(500, { error: 'Server error' }))
        .mockResolvedValueOnce(createSuccessResponse({ data: 'success' }));

      const onRetry = vi.fn();

      await setup.client.get('/api/v1/test', undefined, {
        retry: {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
          onRetry,
        },
      });

      expect(onRetry).toHaveBeenCalledTimes(1);
      expect(onRetry).toHaveBeenCalledWith(1, expect.any(ServerError), expect.any(Number));
    });

    it('should work with PUT requests', async () => {
      setup.mockFetch
        .mockResolvedValueOnce(createErrorResponse(503, { error: 'Service Unavailable' }))
        .mockResolvedValueOnce(createSuccessResponse({ updated: true }));

      const result = await setup.client.put('/api/v1/test', { data: 'updated' }, undefined, {
        retry: {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        },
      });

      expect(result).toEqual({ updated: true });
      expect(setup.mockFetch).toHaveBeenCalledTimes(2);
    });

    it('should work with DELETE requests', async () => {
      setup.mockFetch
        .mockResolvedValueOnce(createErrorResponse(500, { error: 'Server error' }))
        .mockResolvedValueOnce({
          ok: true,
          status: 204,
          headers: new Headers(),
        });

      await setup.client.delete('/api/v1/test', {
        retry: {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        },
      });

      expect(setup.mockFetch).toHaveBeenCalledTimes(2);
    });

    it('should exhaust retries and throw last error', async () => {
      setup.mockFetch.mockResolvedValue(createErrorResponse(500, { error: 'Server error' }));

      await expect(
        setup.client.get('/api/v1/test', undefined, {
          retry: {
            retryConfig: { maxAttempts: 2, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
          },
        })
      ).rejects.toThrow(ServerError);

      // 1 initial attempt + 2 retries = 3 total
      expect(setup.mockFetch).toHaveBeenCalledTimes(3);

      const metrics = getRetryMetrics();
      expect(metrics.failedAfterRetry).toBe(1);
    });
  });
});
