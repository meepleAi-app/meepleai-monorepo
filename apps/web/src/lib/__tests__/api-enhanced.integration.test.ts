/**
 * Integration tests for enhanced API client
 */

import { apiEnhanced } from '../api-enhanced';
import { ApiError, NetworkError } from '../errors';

// Mock fetch globally
global.fetch = jest.fn();

describe('apiEnhanced integration tests', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  });

  describe('successful requests', () => {
    it('should make successful GET request', async () => {
      const mockData = { id: '123', name: 'Test Game' };
      (fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Headers({ 'X-Correlation-Id': 'corr-123' }),
        json: async () => mockData
      });

      const result = await apiEnhanced.get('/api/v1/games/123');

      expect(result).toEqual(mockData);
      expect(fetch).toHaveBeenCalledTimes(1);
      expect(fetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/games/123'),
        expect.objectContaining({
          method: 'GET',
          credentials: 'include'
        })
      );
    });

    it('should make successful POST request', async () => {
      const requestBody = { name: 'New Game' };
      const mockData = { id: '456', name: 'New Game' };

      (fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 201,
        headers: new Headers({ 'X-Correlation-Id': 'corr-456' }),
        json: async () => mockData
      });

      const result = await apiEnhanced.post('/api/v1/games', requestBody);

      expect(result).toEqual(mockData);
      expect(fetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/games'),
        expect.objectContaining({
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(requestBody)
        })
      );
    });

    it('should make successful PUT request', async () => {
      const requestBody = { name: 'Updated Game' };
      const mockData = { id: '789', name: 'Updated Game' };

      (fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Headers(),
        json: async () => mockData
      });

      const result = await apiEnhanced.put('/api/v1/games/789', requestBody);

      expect(result).toEqual(mockData);
      expect(fetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/games/789'),
        expect.objectContaining({
          method: 'PUT',
          body: JSON.stringify(requestBody)
        })
      );
    });

    it('should make successful DELETE request', async () => {
      (fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 204,
        headers: new Headers()
      });

      await apiEnhanced.delete('/api/v1/games/123');

      expect(fetch).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/games/123'),
        expect.objectContaining({
          method: 'DELETE'
        })
      );
    });
  });

  describe('error handling', () => {
    it('should return null for 401 on GET request', async () => {
      (fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 401,
        statusText: 'Unauthorized',
        headers: new Headers()
      });

      const result = await apiEnhanced.get('/api/v1/protected');

      expect(result).toBeNull();
    });

    it('should throw ApiError for 404', async () => {
      (fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 404,
        statusText: 'Not Found',
        headers: new Headers({ 'X-Correlation-Id': 'corr-404' })
      });

      await expect(apiEnhanced.get('/api/v1/games/999')).rejects.toThrow(ApiError);

      try {
        await apiEnhanced.get('/api/v1/games/999');
      } catch (error) {
        expect(error).toBeInstanceOf(ApiError);
        expect((error as ApiError).statusCode).toBe(404);
        expect((error as ApiError).correlationId).toBe('corr-404');
      }
    });

    it('should throw ApiError for 500', async () => {
      (fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
        headers: new Headers()
      });

      await expect(apiEnhanced.get('/api/v1/games')).rejects.toThrow(ApiError);
    });

    it('should throw NetworkError on connection failure', async () => {
      (fetch as jest.Mock).mockRejectedValue(new TypeError('Failed to fetch'));

      await expect(apiEnhanced.get('/api/v1/games')).rejects.toThrow(NetworkError);
    });
  });

  describe('retry logic', () => {
    it('should retry on 500 error and succeed', async () => {
      const mockData = { id: '123', name: 'Test Game' };

      (fetch as jest.Mock)
        .mockResolvedValueOnce({
          ok: false,
          status: 500,
          statusText: 'Internal Server Error',
          headers: new Headers()
        })
        .mockResolvedValueOnce({
          ok: true,
          status: 200,
          headers: new Headers(),
          json: async () => mockData
        });

      const promise = apiEnhanced.get('/api/v1/games/123');

      // Advance timers for retry delay
      await jest.advanceTimersByTimeAsync(1000);

      const result = await promise;

      expect(result).toEqual(mockData);
      expect(fetch).toHaveBeenCalledTimes(2);
    });

    it('should retry on network error and succeed', async () => {
      const mockData = { id: '123', name: 'Test Game' };

      (fetch as jest.Mock)
        .mockRejectedValueOnce(new TypeError('Network error'))
        .mockResolvedValueOnce({
          ok: true,
          status: 200,
          headers: new Headers(),
          json: async () => mockData
        });

      const promise = apiEnhanced.get('/api/v1/games/123');

      await jest.advanceTimersByTimeAsync(1000);

      const result = await promise;

      expect(result).toEqual(mockData);
      expect(fetch).toHaveBeenCalledTimes(2);
    });

    it('should not retry 404 errors', async () => {
      (fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 404,
        statusText: 'Not Found',
        headers: new Headers()
      });

      await expect(apiEnhanced.get('/api/v1/games/999')).rejects.toThrow(ApiError);

      expect(fetch).toHaveBeenCalledTimes(1);
    });

    it('should use exponential backoff for retries', async () => {
      (fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 500,
        statusText: 'Internal Server Error',
        headers: new Headers()
      });

      const promise = apiEnhanced.get('/api/v1/games/123');

      // First attempt fails immediately
      await jest.advanceTimersByTimeAsync(10);

      // Second attempt after 1000ms
      await jest.advanceTimersByTimeAsync(1000);

      // Third attempt after 2000ms
      await jest.advanceTimersByTimeAsync(2000);

      await expect(promise).rejects.toThrow(ApiError);

      expect(fetch).toHaveBeenCalledTimes(3); // Default maxAttempts
    });

    it('should respect custom retry config', async () => {
      const mockData = { id: '123', name: 'Test Game' };

      (fetch as jest.Mock)
        .mockResolvedValueOnce({
          ok: false,
          status: 500,
          headers: new Headers()
        })
        .mockResolvedValueOnce({
          ok: true,
          status: 200,
          headers: new Headers(),
          json: async () => mockData
        });

      const promise = apiEnhanced.get('/api/v1/games/123', {
        retry: {
          maxAttempts: 2,
          initialDelayMs: 500,
          maxDelayMs: 5000,
          backoffMultiplier: 2
        }
      });

      await jest.advanceTimersByTimeAsync(500);

      const result = await promise;

      expect(result).toEqual(mockData);
      expect(fetch).toHaveBeenCalledTimes(2);
    });

    it('should skip retry when skipRetry is true', async () => {
      (fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 500,
        headers: new Headers()
      });

      await expect(
        apiEnhanced.get('/api/v1/games/123', { skipRetry: true })
      ).rejects.toThrow(ApiError);

      expect(fetch).toHaveBeenCalledTimes(1);
    });
  });

  describe('timeout handling', () => {
    it('should timeout if request exceeds timeout', async () => {
      (fetch as jest.Mock).mockImplementation(
        () =>
          new Promise((resolve) => {
            setTimeout(
              () =>
                resolve({
                  ok: true,
                  status: 200,
                  headers: new Headers(),
                  json: async () => ({ data: 'slow' })
                }),
              5000
            );
          })
      );

      const promise = apiEnhanced.get('/api/v1/slow', { timeout: 2000, skipRetry: true });

      await jest.advanceTimersByTimeAsync(2000);

      await expect(promise).rejects.toThrow();
    }, 10000);
  });

  describe('correlation ID tracking', () => {
    it('should extract and track correlation ID from response', async () => {
      const mockData = { id: '123' };
      (fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Headers({ 'X-Correlation-Id': 'test-corr-123' }),
        json: async () => mockData
      });

      const response = await apiEnhanced.getWithMetadata('/api/v1/games/123');

      expect(response.correlationId).toBe('test-corr-123');
      expect(response.data).toEqual(mockData);
      expect(response.statusCode).toBe(200);
    }, 10000);

    it('should handle missing correlation ID', async () => {
      const mockData = { id: '123' };
      (fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Headers(),
        json: async () => mockData
      });

      const response = await apiEnhanced.getWithMetadata('/api/v1/games/123');

      expect(response.correlationId).toBeUndefined();
    }, 10000);
  });

  describe('different content types', () => {
    it('should handle JSON response', async () => {
      const mockData = { id: '123', name: 'Test' };
      (fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Headers({ 'content-type': 'application/json' }),
        json: async () => mockData
      });

      const result = await apiEnhanced.get('/api/v1/games/123');

      expect(result).toEqual(mockData);
    });

    it('should handle 204 No Content response', async () => {
      (fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 204,
        headers: new Headers()
      });

      const result = await apiEnhanced.get('/api/v1/status');

      expect(result).toBeNull();
    });

    it('should handle text response', async () => {
      (fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 200,
        headers: new Headers({ 'content-type': 'text/plain' }),
        text: async () => 'Plain text response'
      });

      const result = await apiEnhanced.get('/api/v1/text');

      expect(result).toBe('Plain text response');
    });
  });
});
