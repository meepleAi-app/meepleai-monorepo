/**
 * Comprehensive tests for Enhanced API Client with Retry and Error Handling
 *
 * Test Coverage: ~30 tests following BDD structure
 * Features: Retry logic, exponential backoff, timeout handling, error recovery
 */

import { apiEnhanced, ApiRequestOptions } from '../api-enhanced';
import { ApiError, NetworkError } from '../errors';

// Mock logger to avoid side effects
jest.mock('../logger', () => ({
  logger: {
    debug: jest.fn(),
    info: jest.fn(),
    warn: jest.fn(),
    error: jest.fn(),
    setCorrelationId: jest.fn(),
  },
}));

// Mock sleep function to avoid real delays and fake timer issues
jest.mock('../errors', () => {
  const actual = jest.requireActual('../errors');
  return {
    ...actual,
    sleep: jest.fn().mockResolvedValue(undefined),
  };
});

describe('Feature: Enhanced API Client with Retry and Error Handling', () => {
  let fetchMock: jest.MockedFunction<typeof fetch>;

  /**
   * Helper to create mock Response with proper JSON parsing
   */
  const createMockResponse = (status: number, payload?: unknown, headers?: Record<string, string>): Response => {
    const responseHeaders = new Headers(headers || {});

    // Set content-type header if not already set
    if (payload && !responseHeaders.has('content-type')) {
      responseHeaders.set('content-type', 'application/json');
    }

    return {
      ok: status >= 200 && status < 300,
      status,
      statusText: status === 500 ? 'Internal Server Error' :
                  status === 502 ? 'Bad Gateway' :
                  status === 503 ? 'Service Unavailable' :
                  status === 400 ? 'Bad Request' :
                  status === 401 ? 'Unauthorized' :
                  status === 403 ? 'Forbidden' :
                  status === 404 ? 'Not Found' :
                  status === 408 ? 'Request Timeout' :
                  status === 422 ? 'Unprocessable Entity' :
                  status === 204 ? 'No Content' :
                  'OK',
      json: async () => payload,
      text: async () => typeof payload === 'string' ? payload : JSON.stringify(payload),
      headers: responseHeaders,
    } as Response;
  };

  beforeEach(() => {
    fetchMock = jest.fn();
    global.fetch = fetchMock;
    jest.clearAllMocks();
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  describe('Scenario: Successful API Calls', () => {
    it('should return response data for successful GET request', async () => {
      // Given: API client configured and endpoint ready
      const mockData = { id: 1, name: 'Test Game' };
      fetchMock.mockResolvedValue(createMockResponse(200, mockData));

      // When: GET request made
      const result = await apiEnhanced.get('/api/v1/games');

      // Then: Response data returned
      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledTimes(1);
      expect(fetchMock).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/games',
        expect.objectContaining({
          method: 'GET',
          credentials: 'include'
        })
      );
    });

    it('should return response data for successful POST request with body', async () => {
      // Given: API client configured and endpoint ready
      const requestBody = { name: 'New Game' };
      const mockData = { id: 2, name: 'New Game' };
      fetchMock.mockResolvedValue(createMockResponse(201, mockData));

      // When: POST request made
      const result = await apiEnhanced.post('/api/v1/games', requestBody);

      // Then: Response data returned with correct request
      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledTimes(1);
      expect(fetchMock).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/games',
        expect.objectContaining({
          method: 'POST',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(requestBody)
        })
      );
    });

    it('should return response data for successful PUT request with body', async () => {
      // Given: API client configured and endpoint ready
      const requestBody = { name: 'Updated Game' };
      const mockData = { id: 1, name: 'Updated Game' };
      fetchMock.mockResolvedValue(createMockResponse(200, mockData));

      // When: PUT request made
      const result = await apiEnhanced.put('/api/v1/games/1', requestBody);

      // Then: Response data returned with correct request
      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledTimes(1);
      expect(fetchMock).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/games/1',
        expect.objectContaining({
          method: 'PUT',
          credentials: 'include',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(requestBody)
        })
      );
    });

    it('should complete DELETE request successfully', async () => {
      // Given: API client configured and endpoint ready
      fetchMock.mockResolvedValue(createMockResponse(204));

      // When: DELETE request made
      await apiEnhanced.delete('/api/v1/games/1');

      // Then: Request completed without error
      expect(fetchMock).toHaveBeenCalledTimes(1);
      expect(fetchMock).toHaveBeenCalledWith(
        'http://localhost:8080/api/v1/games/1',
        expect.objectContaining({
          method: 'DELETE',
          credentials: 'include'
        })
      );
    });

    it('should include correlation ID in response headers', async () => {
      // Given: API returns correlation ID header
      const mockData = { id: 1, name: 'Test' };
      const correlationId = 'test-correlation-123';
      const response = await createMockResponse(200, mockData);
      response.headers.set('X-Correlation-Id', correlationId);
      fetchMock.mockResolvedValue(response);

      // When: Request made with metadata
      const result = await apiEnhanced.getWithMetadata('/api/v1/test');

      // Then: Correlation ID extracted from headers
      expect(result.correlationId).toBe(correlationId);
      expect(result.data).toEqual(mockData);
      expect(result.statusCode).toBe(200);
    });
  });

  describe('Scenario: Retry on Transient Failure', () => {
    it('should retry on 500 Internal Server Error and succeed', async () => {
      // Given: API fails with 500 on first attempt, succeeds on retry
      const mockData = { success: true };
      fetchMock
        .mockResolvedValueOnce(createMockResponse(500, { error: 'Server error' }))
        .mockResolvedValueOnce(createMockResponse(200, mockData));

      // When: Request made with retry enabled
      const result = await apiEnhanced.get('/api/v1/test');

      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledTimes(2);
    });

    it('should retry on 502 Bad Gateway', async () => {
      // Given: API fails with 502 on first attempt, succeeds on retry
      const mockData = { success: true };
      fetchMock
        .mockResolvedValueOnce(createMockResponse(502, { error: 'Bad gateway' }))
        .mockResolvedValueOnce(createMockResponse(200, mockData));

      // When: Request made
      const result = await apiEnhanced.get('/api/v1/test');

      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledTimes(2);
    });

    it('should retry on 503 Service Unavailable', async () => {
      // Given: API fails with 503 on first attempt, succeeds on retry
      const mockData = { success: true };
      fetchMock
        .mockResolvedValueOnce(createMockResponse(503, { error: 'Service unavailable' }))
        .mockResolvedValueOnce(createMockResponse(200, mockData));

      // When: Request made
      const result = await apiEnhanced.get('/api/v1/test');

      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledTimes(2);
    });

    it('should use exponential backoff delays (1000ms, 2000ms, 4000ms)', async () => {
      // Given: API fails multiple times before succeeding
      const mockData = { success: true };
      const { sleep } = jest.requireMock('../errors');

      fetchMock
        .mockResolvedValueOnce(createMockResponse(500))
        .mockResolvedValueOnce(createMockResponse(500))
        .mockResolvedValueOnce(createMockResponse(200, mockData));

      // When: Request made
      const result = await apiEnhanced.get('/api/v1/test');

      // Then: Exponential backoff applied
      // sleep() called twice with correct delays
      expect(sleep).toHaveBeenCalledTimes(2);
      expect(sleep).toHaveBeenNthCalledWith(1, 1000); // First retry: 1000ms
      expect(sleep).toHaveBeenNthCalledWith(2, 2000); // Second retry: 2000ms
      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledTimes(3);
    });

    it('should succeed after 2nd retry attempt', async () => {
      // Given: API fails twice, succeeds on third attempt
      const mockData = { success: true };
      fetchMock
        .mockResolvedValueOnce(createMockResponse(503))
        .mockResolvedValueOnce(createMockResponse(503))
        .mockResolvedValueOnce(createMockResponse(200, mockData));

      // When: Request made
      const result = await apiEnhanced.get('/api/v1/test');

      // Then: Request succeeds after 2 retries (3 total attempts)
      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledTimes(3);
    });

    it('should fail after max retries reached (3 attempts)', async () => {
      // Given: API fails all retry attempts
      fetchMock.mockResolvedValue(createMockResponse(500, { error: 'Persistent error' }));

      // When: Request made
      const promise = apiEnhanced.get('/api/v1/test');

      // Then: Error thrown after max retries
      await expect(promise).rejects.toThrow(ApiError);
      expect(fetchMock).toHaveBeenCalledTimes(3); // Default maxAttempts = 3
    });

    it('should retry on 408 Request Timeout', async () => {
      // Given: API returns 408 on first attempt, succeeds on retry
      const mockData = { success: true };
      fetchMock
        .mockResolvedValueOnce(createMockResponse(408, { error: 'Request timeout' }))
        .mockResolvedValueOnce(createMockResponse(200, mockData));

      // When: Request made
      const result = await apiEnhanced.get('/api/v1/test');

      // Then: Request retries and succeeds
      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledTimes(2);
    });
  });

  describe('Scenario: Non-Retryable Errors', () => {
    it('should NOT retry on 400 Bad Request', async () => {
      // Given: API returns 400 error
      fetchMock.mockResolvedValue(createMockResponse(400, { error: 'Bad request' }));

      // When: Request made
      // Then: Error thrown immediately without retry
      await expect(apiEnhanced.get('/api/v1/test')).rejects.toThrow(ApiError);
      expect(fetchMock).toHaveBeenCalledTimes(1); // No retry
    });

    it('should NOT retry on 401 Unauthorized', async () => {
      // Given: API returns 401 error
      fetchMock.mockResolvedValue(createMockResponse(401, { error: 'Unauthorized' }));

      // When: Request made with GET
      const result = await apiEnhanced.get('/api/v1/test');

      // Then: Returns null without retry (GET special handling)
      expect(result).toBeNull();
      expect(fetchMock).toHaveBeenCalledTimes(1); // No retry
    });

    it('should NOT retry on 403 Forbidden', async () => {
      // Given: API returns 403 error
      fetchMock.mockResolvedValue(createMockResponse(403, { error: 'Forbidden' }));

      // When: Request made
      // Then: Error thrown immediately without retry
      await expect(apiEnhanced.get('/api/v1/test')).rejects.toThrow(ApiError);
      expect(fetchMock).toHaveBeenCalledTimes(1); // No retry
    });

    it('should NOT retry on 404 Not Found', async () => {
      // Given: API returns 404 error
      fetchMock.mockResolvedValue(createMockResponse(404, { error: 'Not found' }));

      // When: Request made
      // Then: Error thrown immediately without retry
      await expect(apiEnhanced.get('/api/v1/test')).rejects.toThrow(ApiError);
      expect(fetchMock).toHaveBeenCalledTimes(1); // No retry
    });

    it('should NOT retry on 422 Unprocessable Entity', async () => {
      // Given: API returns 422 error
      fetchMock.mockResolvedValue(createMockResponse(422, { error: 'Validation failed' }));

      // When: Request made
      // Then: Error thrown immediately without retry
      await expect(apiEnhanced.get('/api/v1/test')).rejects.toThrow(ApiError);
      expect(fetchMock).toHaveBeenCalledTimes(1); // No retry
    });
  });

  describe('Scenario: Request Timeout Handling', () => {
    it('should abort request after timeout expires', async () => {
      // Given: Slow API endpoint and timeout configured
      let abortSignalReceived: AbortSignal | undefined;
      fetchMock.mockImplementation((_url, init) => {
        abortSignalReceived = init?.signal ?? undefined;
        // Simulate hanging request that throws when aborted
        return new Promise((_, reject) => {
          init?.signal?.addEventListener('abort', () => {
            reject(new DOMException('The operation was aborted', 'AbortError'));
          });
        });
      });

      const options: ApiRequestOptions = { timeout: 10 }; // Very short timeout for fast test

      // When: Request made with timeout
      const promise = apiEnhanced.get('/api/v1/test', options);

      // Then: Request rejected with NetworkError after timeout
      await expect(promise).rejects.toThrow(NetworkError);
      expect(abortSignalReceived?.aborted).toBe(true);
    });

    it('should pass AbortController signal to fetch', async () => {
      // Given: Timeout configured
      let abortSignalReceived: AbortSignal | undefined;
      fetchMock.mockImplementation(async (_url, init) => {
        abortSignalReceived = init?.signal ?? undefined;
        return createMockResponse(200, { success: true });
      });

      const options: ApiRequestOptions = { timeout: 5000 };

      // When: Request made
      await apiEnhanced.get('/api/v1/test', options);

      // Then: AbortController signal passed to fetch
      expect(abortSignalReceived).toBeDefined();
      expect(abortSignalReceived).toBeInstanceOf(AbortSignal);
    });

    it('should include timeout error message in thrown error', async () => {
      // Given: Request will timeout
      fetchMock.mockImplementation((_url, init) => {
        // Simulate hanging request that throws when aborted
        return new Promise((_, reject) => {
          init?.signal?.addEventListener('abort', () => {
            reject(new DOMException('The operation was aborted', 'AbortError'));
          });
        });
      });

      const options: ApiRequestOptions = { timeout: 10 }; // Very short timeout for fast test

      // When: Request made with timeout
      const promise = apiEnhanced.get('/api/v1/test', options);

      // Then: Error includes timeout message
      await expect(promise).rejects.toThrow(NetworkError);
      await expect(promise).rejects.toThrow(/timeout/i);
    });
  });

  describe('Scenario: Error Response Parsing', () => {
    it('should parse JSON error response body', async () => {
      // Given: API returns JSON error
      const errorBody = { error: 'Validation failed', details: 'Name is required' };
      fetchMock.mockResolvedValue(createMockResponse(400, errorBody));

      // When: Request made
      try {
        await apiEnhanced.get('/api/v1/test');
        fail('Should have thrown error');
      } catch (error) {
        // Then: Error is ApiError with status code
        expect(error).toBeInstanceOf(ApiError);
        expect((error as ApiError).statusCode).toBe(400);
        expect((error as ApiError).endpoint).toBe('/api/v1/test');
      }
    });

    it('should parse plain text error response body', async () => {
      // Given: API returns text error
      const response = await createMockResponse(500, 'Internal Server Error');
      response.headers.set('content-type', 'text/plain');
      fetchMock.mockResolvedValue(response);

      // When: Request made
      try {
        await apiEnhanced.get('/api/v1/test');
        fail('Should have thrown error');
      } catch (error) {
        // Then: Error is ApiError
        expect(error).toBeInstanceOf(ApiError);
        expect((error as ApiError).statusCode).toBe(500);
      }
    });

    it('should handle empty error response body', async () => {
      // Given: API returns empty response
      fetchMock.mockResolvedValue(createMockResponse(500, undefined));

      // When: Request made
      try {
        await apiEnhanced.get('/api/v1/test');
        fail('Should have thrown error');
      } catch (error) {
        // Then: Error created with generic message
        expect(error).toBeInstanceOf(ApiError);
        expect((error as ApiError).statusCode).toBe(500);
        expect((error as ApiError).message).toContain('API request failed');
      }
    });

    it('should include HTTP status code in error', async () => {
      // Given: API returns error with specific status
      fetchMock.mockResolvedValue(createMockResponse(403, { error: 'Forbidden' }));

      // When: Request made
      try {
        await apiEnhanced.post('/api/v1/test', {});
        fail('Should have thrown error');
      } catch (error) {
        // Then: Error includes status code
        expect(error).toBeInstanceOf(ApiError);
        expect((error as ApiError).statusCode).toBe(403);
      }
    });

    it('should include correlation ID in error object when present', async () => {
      // Given: API returns error with correlation ID
      const correlationId = 'error-correlation-456';
      const response = await createMockResponse(500, { error: 'Server error' });
      response.headers.set('X-Correlation-Id', correlationId);
      fetchMock.mockResolvedValue(response);

      // When: Request made
      try {
        await apiEnhanced.post('/api/v1/test', {});
        fail('Should have thrown error');
      } catch (error) {
        // Then: Error includes correlation ID
        expect(error).toBeInstanceOf(ApiError);
        expect((error as ApiError).correlationId).toBe(correlationId);
      }
    });
  });

  describe('Scenario: Network Errors', () => {
    it('should retry on network error (TypeError)', async () => {
      // Given: Network error on first attempt, success on retry
      const mockData = { success: true };
      fetchMock
        .mockRejectedValueOnce(new TypeError('Network request failed'))
        .mockResolvedValueOnce(createMockResponse(200, mockData));

      // When: Request made
      const result = await apiEnhanced.get('/api/v1/test');

      // Then: Request retries and succeeds
      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledTimes(2);
    });

    it('should fail after max retries on persistent network error', async () => {
      // Given: Network error on all attempts
      fetchMock.mockRejectedValue(new TypeError('Network request failed'));

      // When: Request made
      const promise = apiEnhanced.get('/api/v1/test');

      // Then: NetworkError thrown after max retries
      await expect(promise).rejects.toThrow(NetworkError);
      expect(fetchMock).toHaveBeenCalledTimes(3); // maxAttempts
    });

    it('should wrap TypeError in NetworkError', async () => {
      // Given: Network connection failure
      fetchMock.mockRejectedValue(new TypeError('Failed to fetch'));

      // When: Request made
      const promise = apiEnhanced.get('/api/v1/test');

      // Then: Error wrapped in NetworkError
      try {
        await promise;
        fail('Should have thrown error');
      } catch (error) {
        expect(error).toBeInstanceOf(NetworkError);
        expect((error as NetworkError).message).toContain('Network request failed');
        expect((error as NetworkError).endpoint).toBe('/api/v1/test');
      }
    });
  });

  describe('Scenario: Skip Retry Option', () => {
    it('should NOT retry when skipRetry is true', async () => {
      // Given: API returns retryable error and skipRetry enabled
      fetchMock.mockResolvedValue(createMockResponse(500, { error: 'Server error' }));

      const options: ApiRequestOptions = { skipRetry: true };

      // When: Request made with skipRetry
      // Then: Error thrown immediately without retry
      await expect(apiEnhanced.get('/api/v1/test', options)).rejects.toThrow(ApiError);
      expect(fetchMock).toHaveBeenCalledTimes(1);
    });
  });

  describe('Scenario: Custom Retry Configuration', () => {
    it('should respect custom maxAttempts', async () => {
      // Given: Custom retry config with maxAttempts = 5
      fetchMock.mockResolvedValue(createMockResponse(500));

      const options: ApiRequestOptions = {
        retry: { maxAttempts: 5 }
      };

      // When: Request made with custom config
      const promise = apiEnhanced.get('/api/v1/test', options);

      // Then: Retries up to custom maxAttempts
      await expect(promise).rejects.toThrow(ApiError);
      expect(fetchMock).toHaveBeenCalledTimes(5);
    });

    it('should respect custom initialDelayMs', async () => {
      // Given: Custom retry config with initialDelayMs = 500
      const mockData = { success: true };
      const { sleep } = jest.requireMock('../errors');

      fetchMock
        .mockResolvedValueOnce(createMockResponse(500))
        .mockResolvedValueOnce(createMockResponse(200, mockData));

      const options: ApiRequestOptions = {
        retry: { initialDelayMs: 500 }
      };

      // When: Request made with custom delay
      const result = await apiEnhanced.get('/api/v1/test', options);

      // Then: Uses custom initial delay
      expect(sleep).toHaveBeenCalledWith(500); // Custom delay instead of default 1000ms
      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledTimes(2);
    });
  });

  describe('Scenario: 401 Unauthorized Special Handling', () => {
    it('should return null for GET requests on 401', async () => {
      // Given: API returns 401 Unauthorized
      fetchMock.mockResolvedValue(createMockResponse(401, { error: 'Unauthorized' }));

      // When: GET request made
      const result = await apiEnhanced.get('/api/v1/test');

      // Then: Returns null instead of throwing
      expect(result).toBeNull();
      expect(fetchMock).toHaveBeenCalledTimes(1);
    });

    it('should throw ApiError for POST requests on 401', async () => {
      // Given: API returns 401 Unauthorized
      fetchMock.mockResolvedValue(createMockResponse(401, { error: 'Unauthorized' }));

      // When: POST request made
      // Then: Throws ApiError
      await expect(apiEnhanced.post('/api/v1/test', {})).rejects.toThrow(ApiError);
    });

    it('should NOT retry on 401 Unauthorized', async () => {
      // Given: API returns 401
      fetchMock.mockResolvedValue(createMockResponse(401));

      // When: POST request made
      // Then: No retry attempted
      await expect(apiEnhanced.post('/api/v1/test', {})).rejects.toThrow(ApiError);
      expect(fetchMock).toHaveBeenCalledTimes(1); // No retry
    });
  });

  describe('Scenario: Response Content-Type Handling', () => {
    it('should parse JSON response when content-type is application/json', async () => {
      // Given: API returns JSON content
      const mockData = { id: 1, name: 'Test' };
      const response = await createMockResponse(200, mockData);
      response.headers.set('content-type', 'application/json');
      fetchMock.mockResolvedValue(response);

      // When: Request made
      const result = await apiEnhanced.get('/api/v1/test');

      // Then: JSON parsed correctly
      expect(result).toEqual(mockData);
    });

    it('should return null for 204 No Content responses', async () => {
      // Given: API returns 204 No Content
      fetchMock.mockResolvedValue(createMockResponse(204));

      // When: Request made with metadata
      const result = await apiEnhanced.getWithMetadata('/api/v1/test');

      // Then: Data is null
      expect(result.data).toBeNull();
      expect(result.statusCode).toBe(204);
    });

    it('should return text for non-JSON content-type', async () => {
      // Given: API returns plain text
      const textResponse = 'Plain text response';
      const response = await createMockResponse(200, textResponse);
      response.headers.set('content-type', 'text/plain');
      fetchMock.mockResolvedValue(response);

      // When: Request made
      const result = await apiEnhanced.get('/api/v1/test');

      // Then: Text returned
      expect(result).toBe(textResponse);
    });
  });

  describe('Scenario: Edge Cases for 90% Coverage', () => {
    beforeEach(() => {
      fetchMock = global.fetch as jest.MockedFunction<typeof fetch>;
      fetchMock.mockClear();
    });

    it('should use fallback API base when env var is empty string', async () => {
      // Given: Env var is set to empty string
      const originalEnv = process.env.NEXT_PUBLIC_API_BASE;
      process.env.NEXT_PUBLIC_API_BASE = '';
      const mockData = { test: true };
      fetchMock.mockResolvedValue(createMockResponse(200, mockData));

      // When: Request made
      const result = await apiEnhanced.get('/api/v1/test');

      // Then: Request uses fallback base URL
      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledWith(
        expect.stringContaining('http://localhost:8080/api/v1/test'),
        expect.anything()
      );

      // Cleanup
      process.env.NEXT_PUBLIC_API_BASE = originalEnv;
    });

    it('should handle existing signal abort during timeout', async () => {
      // Given: External AbortController that will be aborted
      const externalController = new AbortController();

      // Mock fetch to reject with AbortError when signal is aborted
      fetchMock.mockImplementation(async (url, init) => {
        return new Promise((_, reject) => {
          const checkAbort = () => {
            if (externalController.signal.aborted) {
              const error = new Error('The operation was aborted');
              error.name = 'AbortError';
              reject(error);
            }
          };

          // Check immediately and periodically
          checkAbort();
          const interval = setInterval(checkAbort, 10);

          // Cleanup on abort
          externalController.signal.addEventListener('abort', () => {
            clearInterval(interval);
            const error = new Error('The operation was aborted');
            error.name = 'AbortError';
            reject(error);
          });
        });
      });

      // When: External signal is aborted
      setTimeout(() => externalController.abort(), 10);

      const promise = apiEnhanced.get('/api/v1/test', {
        timeout: 1000,
        signal: externalController.signal
      });

      // Then: Request is aborted by external signal
      await expect(promise).rejects.toThrow(NetworkError);
    });

    it('should retry on 503 Service Unavailable until success', async () => {
      // Given: API returns 503 twice, then succeeds
      const mockData = { success: true };
      fetchMock
        .mockResolvedValueOnce(createMockResponse(503))
        .mockResolvedValueOnce(createMockResponse(503))
        .mockResolvedValueOnce(createMockResponse(200, mockData));

      // When: Request made with retries
      const result = await apiEnhanced.get('/api/v1/test');

      // Then: Request succeeds after retries
      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledTimes(3);
    });

    it('should throw ApiError after max retries on 503', async () => {
      // Given: API returns 503 persistently (more than maxAttempts)
      fetchMock.mockResolvedValue(createMockResponse(503));

      // When/Then: Request fails after max retries
      await expect(apiEnhanced.get('/api/v1/test', { retry: { maxAttempts: 2 } }))
        .rejects.toThrow(ApiError);

      // Verify retry attempts
      expect(fetchMock).toHaveBeenCalledTimes(2);
    });

    it('should apply exponential backoff on 503 retries', async () => {
      // Given: API returns 503 twice, then succeeds
      fetchMock.mockClear();
      const mockData = { success: true };
      fetchMock
        .mockResolvedValueOnce(createMockResponse(503))
        .mockResolvedValueOnce(createMockResponse(503))
        .mockResolvedValueOnce(createMockResponse(200, mockData));

      // When: Request made (will retry with exponential backoff delays)
      const result = await apiEnhanced.get('/api/v1/test');

      // Then: Request succeeds after retries (exponential backoff is mocked, so instant)
      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledTimes(3); // 1 initial + 2 retries
    });

    it('should retry on 502 Bad Gateway', async () => {
      // Given: API returns 502, then succeeds
      const mockData = { success: true };
      fetchMock
        .mockResolvedValueOnce(createMockResponse(502))
        .mockResolvedValueOnce(createMockResponse(200, mockData));

      // When: Request made
      const result = await apiEnhanced.get('/api/v1/test');

      // Then: Request succeeds after retry
      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledTimes(2);
    });

    it('should not retry on 500 when skipRetry is true', async () => {
      // Given: API returns 500 with skipRetry option
      fetchMock.mockClear();
      fetchMock.mockResolvedValue(createMockResponse(500));

      // When/Then: Request fails immediately without retry
      await expect(apiEnhanced.get('/api/v1/test', { skipRetry: true }))
        .rejects.toThrow(ApiError);

      // Verify no retries
      expect(fetchMock).toHaveBeenCalledTimes(1);
    });

    it('should respect custom retry configuration', async () => {
      // Given: API returns 500 three times, then succeeds (tests maxAttempts=4)
      fetchMock.mockClear();
      const mockData = { success: true };
      fetchMock
        .mockResolvedValueOnce(createMockResponse(500))
        .mockResolvedValueOnce(createMockResponse(500))
        .mockResolvedValueOnce(createMockResponse(500))
        .mockResolvedValueOnce(createMockResponse(200, mockData));

      // When: Request made with custom maxAttempts=4
      const result = await apiEnhanced.get('/api/v1/test', {
        retry: { maxAttempts: 4 }
      });

      // Then: Request succeeds after 3 retries (total 4 attempts)
      expect(result).toEqual(mockData);
      expect(fetchMock).toHaveBeenCalledTimes(4);
    }, 10000); // 10-second timeout for retry test
  });

  describe('Scenario: RuleSpec Comments API', () => {
    beforeEach(() => {
      fetchMock = global.fetch as jest.MockedFunction<typeof fetch>;
      fetchMock.mockClear();
    });

    describe('getComments', () => {
      it('should fetch comments for a game and version successfully', async () => {
        // Given: API returns comments for a game
        const mockResponse = {
          gameId: 'chess-123',
          version: '1.0.0',
          comments: [
            {
              id: 'comment-1',
              gameId: 'chess-123',
              version: '1.0.0',
              atomId: 'atom-1',
              userId: 'user-1',
              userDisplayName: 'John Doe',
              commentText: 'Great rule!',
              createdAt: '2025-10-19T10:00:00Z',
              updatedAt: null,
            },
            {
              id: 'comment-2',
              gameId: 'chess-123',
              version: '1.0.0',
              atomId: null,
              userId: 'user-2',
              userDisplayName: 'Jane Smith',
              commentText: 'Needs clarification',
              createdAt: '2025-10-19T11:00:00Z',
              updatedAt: '2025-10-19T12:00:00Z',
            },
          ],
          totalComments: 2,
        };
        fetchMock.mockResolvedValue(createMockResponse(200, mockResponse));

        // When: getComments is called
        const { ruleSpecComments } = await import('../api-enhanced');
        const result = await ruleSpecComments.getComments('chess-123', '1.0.0');

        // Then: Comments are returned
        expect(result).toEqual(mockResponse);
        expect(fetchMock).toHaveBeenCalledTimes(1);
        expect(fetchMock).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/games/chess-123/rulespec/versions/1.0.0/comments'),
          expect.objectContaining({ method: 'GET', credentials: 'include' })
        );
      });

      it('should return null on 401 Unauthorized', async () => {
        // Given: API returns 401 Unauthorized
        fetchMock.mockResolvedValue(createMockResponse(401));

        // When: getComments is called without authentication
        const { ruleSpecComments } = await import('../api-enhanced');
        const result = await ruleSpecComments.getComments('chess-123', '1.0.0');

        // Then: null is returned
        expect(result).toBeNull();
      });

      it('should return empty comments array when no comments exist', async () => {
        // Given: API returns empty comments array
        const mockResponse = {
          gameId: 'chess-123',
          version: '1.0.0',
          comments: [],
          totalComments: 0,
        };
        fetchMock.mockResolvedValue(createMockResponse(200, mockResponse));

        // When: getComments is called
        const { ruleSpecComments } = await import('../api-enhanced');
        const result = await ruleSpecComments.getComments('chess-123', '1.0.0');

        // Then: Empty comments array is returned
        expect(result).toEqual(mockResponse);
        expect(result?.comments).toHaveLength(0);
        expect(result?.totalComments).toBe(0);
      });
    });

    describe('createComment', () => {
      it('should create a comment successfully', async () => {
        // Given: API accepts comment creation
        const request = {
          atomId: 'atom-1',
          commentText: 'This is my comment',
        };
        const mockResponse = {
          id: 'comment-new',
          gameId: 'chess-123',
          version: '1.0.0',
          atomId: 'atom-1',
          userId: 'user-1',
          userDisplayName: 'John Doe',
          commentText: 'This is my comment',
          createdAt: '2025-10-19T13:00:00Z',
          updatedAt: null,
        };
        fetchMock.mockResolvedValue(createMockResponse(200, mockResponse));

        // When: createComment is called
        const { ruleSpecComments } = await import('../api-enhanced');
        const result = await ruleSpecComments.createComment('chess-123', '1.0.0', request);

        // Then: Comment is created and returned
        expect(result).toEqual(mockResponse);
        expect(fetchMock).toHaveBeenCalledTimes(1);
        expect(fetchMock).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/games/chess-123/rulespec/versions/1.0.0/comments'),
          expect.objectContaining({
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(request),
          })
        );
      });

      it('should create a comment without atomId (general comment)', async () => {
        // Given: API accepts general comment (no atomId)
        const request = {
          atomId: null,
          commentText: 'General comment about the game',
        };
        const mockResponse = {
          id: 'comment-general',
          gameId: 'chess-123',
          version: '1.0.0',
          atomId: null,
          userId: 'user-1',
          userDisplayName: 'John Doe',
          commentText: 'General comment about the game',
          createdAt: '2025-10-19T13:00:00Z',
          updatedAt: null,
        };
        fetchMock.mockResolvedValue(createMockResponse(200, mockResponse));

        // When: createComment is called with null atomId
        const { ruleSpecComments } = await import('../api-enhanced');
        const result = await ruleSpecComments.createComment('chess-123', '1.0.0', request);

        // Then: General comment is created
        expect(result).toEqual(mockResponse);
        expect(result.atomId).toBeNull();
      });

      it('should throw ApiError on 400 Bad Request (validation error)', async () => {
        // Given: API rejects comment with validation error
        const request = {
          atomId: 'atom-1',
          commentText: '', // Empty comment text (validation error)
        };
        fetchMock.mockResolvedValue(createMockResponse(400));

        // When/Then: createComment throws ApiError
        const { ruleSpecComments } = await import('../api-enhanced');
        await expect(ruleSpecComments.createComment('chess-123', '1.0.0', request))
          .rejects.toThrow(ApiError);
      });
    });

    describe('updateComment', () => {
      it('should update a comment successfully', async () => {
        // Given: API accepts comment update
        const request = {
          commentText: 'Updated comment text',
        };
        const mockResponse = {
          id: 'comment-1',
          gameId: 'chess-123',
          version: '1.0.0',
          atomId: 'atom-1',
          userId: 'user-1',
          userDisplayName: 'John Doe',
          commentText: 'Updated comment text',
          createdAt: '2025-10-19T10:00:00Z',
          updatedAt: '2025-10-19T14:00:00Z',
        };
        fetchMock.mockResolvedValue(createMockResponse(200, mockResponse));

        // When: updateComment is called
        const { ruleSpecComments } = await import('../api-enhanced');
        const result = await ruleSpecComments.updateComment('chess-123', 'comment-1', request);

        // Then: Comment is updated and returned
        expect(result).toEqual(mockResponse);
        expect(result.updatedAt).not.toBeNull();
        expect(fetchMock).toHaveBeenCalledTimes(1);
        expect(fetchMock).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/games/chess-123/rulespec/comments/comment-1'),
          expect.objectContaining({
            method: 'PUT',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(request),
          })
        );
      });

      it('should throw ApiError on 404 Not Found (comment not exists)', async () => {
        // Given: API returns 404 for non-existent comment
        const request = {
          commentText: 'Updated text',
        };
        fetchMock.mockResolvedValue(createMockResponse(404));

        // When/Then: updateComment throws ApiError
        const { ruleSpecComments } = await import('../api-enhanced');
        await expect(ruleSpecComments.updateComment('chess-123', 'nonexistent-id', request))
          .rejects.toThrow(ApiError);
      });

      it('should throw ApiError on 403 Forbidden (not owner)', async () => {
        // Given: API returns 403 when user tries to update another user's comment
        const request = {
          commentText: 'Trying to update someone else comment',
        };
        fetchMock.mockResolvedValue(createMockResponse(403));

        // When/Then: updateComment throws ApiError
        const { ruleSpecComments } = await import('../api-enhanced');
        await expect(ruleSpecComments.updateComment('chess-123', 'comment-1', request))
          .rejects.toThrow(ApiError);
      });
    });

    describe('deleteComment', () => {
      it('should delete a comment successfully', async () => {
        // Given: API accepts comment deletion
        fetchMock.mockResolvedValue(createMockResponse(204));

        // When: deleteComment is called
        const { ruleSpecComments } = await import('../api-enhanced');
        await ruleSpecComments.deleteComment('chess-123', 'comment-1');

        // Then: Comment is deleted (no return value)
        expect(fetchMock).toHaveBeenCalledTimes(1);
        expect(fetchMock).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/games/chess-123/rulespec/comments/comment-1'),
          expect.objectContaining({ method: 'DELETE', credentials: 'include' })
        );
      });

      it('should throw ApiError on 404 Not Found (comment not exists)', async () => {
        // Given: API returns 404 for non-existent comment
        fetchMock.mockResolvedValue(createMockResponse(404));

        // When/Then: deleteComment throws ApiError
        const { ruleSpecComments } = await import('../api-enhanced');
        await expect(ruleSpecComments.deleteComment('chess-123', 'nonexistent-id'))
          .rejects.toThrow(ApiError);
      });

      it('should throw ApiError on 403 Forbidden (not owner or admin)', async () => {
        // Given: API returns 403 when user tries to delete another user's comment
        fetchMock.mockResolvedValue(createMockResponse(403));

        // When/Then: deleteComment throws ApiError
        const { ruleSpecComments } = await import('../api-enhanced');
        await expect(ruleSpecComments.deleteComment('chess-123', 'comment-1'))
          .rejects.toThrow(ApiError);
      });
    });
  });
});
