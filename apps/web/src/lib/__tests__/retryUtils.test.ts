/**
 * Tests for Retry Utilities (Issue #2340)
 *
 * Coverage target: 90%+
 * Tests: Exponential backoff, retry logic, retryable error detection
 */

import { retryWithBackoff, isRetryableError, createRetryableFetch } from '../retryUtils';
import { ApiError } from '@/lib/api';

describe('retryUtils', () => {
  describe('retryWithBackoff', () => {
    it('should return result on first success', async () => {
      const fn = vi.fn().mockResolvedValueOnce('success');

      const result = await retryWithBackoff(fn);

      expect(result).toBe('success');
      expect(fn).toHaveBeenCalledTimes(1);
    });

    it('should retry on failure and succeed', async () => {
      const fn = vi
        .fn()
        .mockRejectedValueOnce(new Error('Fail 1'))
        .mockRejectedValueOnce(new Error('Fail 2'))
        .mockResolvedValueOnce('success');

      const result = await retryWithBackoff(fn, { maxAttempts: 3, initialDelayMs: 10 });

      expect(result).toBe('success');
      expect(fn).toHaveBeenCalledTimes(3);
    });

    it('should throw after max attempts', async () => {
      const fn = vi.fn().mockRejectedValue(new Error('Always fails'));

      await expect(retryWithBackoff(fn, { maxAttempts: 2, initialDelayMs: 10 })).rejects.toThrow(
        'Always fails'
      );

      expect(fn).toHaveBeenCalledTimes(2);
    });

    it('should respect shouldRetry predicate', async () => {
      const fn = vi.fn().mockRejectedValue(new Error('No retry'));

      const shouldRetry = vi.fn().mockReturnValue(false);

      await expect(retryWithBackoff(fn, { shouldRetry, initialDelayMs: 10 })).rejects.toThrow(
        'No retry'
      );

      expect(fn).toHaveBeenCalledTimes(1);
      expect(shouldRetry).toHaveBeenCalledWith(expect.any(Error), 0);
    });

    it('should call onRetry callback', async () => {
      const fn = vi.fn().mockRejectedValueOnce(new Error('Fail')).mockResolvedValueOnce('success');

      const onRetry = vi.fn();

      await retryWithBackoff(fn, { maxAttempts: 2, initialDelayMs: 10, onRetry });

      expect(onRetry).toHaveBeenCalledTimes(1);
      expect(onRetry).toHaveBeenCalledWith(expect.any(Error), 1, 10);
    });

    it('should use exponential backoff delays', async () => {
      const fn = vi
        .fn()
        .mockRejectedValueOnce(new Error('1'))
        .mockRejectedValueOnce(new Error('2'))
        .mockResolvedValueOnce('success');

      const onRetry = vi.fn();

      await retryWithBackoff(fn, {
        maxAttempts: 3,
        initialDelayMs: 100,
        onRetry,
      });

      // Delays should be: 100ms, 200ms
      expect(onRetry).toHaveBeenNthCalledWith(1, expect.any(Error), 1, 100);
      expect(onRetry).toHaveBeenNthCalledWith(2, expect.any(Error), 2, 200);
    });

    it('should cap delay at maxDelayMs', async () => {
      const fn = vi
        .fn()
        .mockRejectedValueOnce(new Error('1'))
        .mockRejectedValueOnce(new Error('2'))
        .mockResolvedValueOnce('success');

      const onRetry = vi.fn();

      await retryWithBackoff(fn, {
        maxAttempts: 3,
        initialDelayMs: 1000,
        maxDelayMs: 1500,
        onRetry,
      });

      // Delays: 1000ms (capped), 1500ms (would be 2000, capped to 1500)
      expect(onRetry).toHaveBeenNthCalledWith(1, expect.any(Error), 1, 1000);
      expect(onRetry).toHaveBeenNthCalledWith(2, expect.any(Error), 2, 1500);
    });
  });

  describe('isRetryableError', () => {
    it('should identify network fetch errors as retryable', () => {
      const error = new TypeError('fetch failed');
      expect(isRetryableError(error)).toBe(true);
    });

    it('should identify 5xx errors as retryable', () => {
      const error = new ApiError({ message: 'Server error', statusCode: 500 });
      expect(isRetryableError(error)).toBe(true);

      const error503 = new ApiError({ message: 'Service unavailable', statusCode: 503 });
      expect(isRetryableError(error503)).toBe(true);
    });

    it('should identify 408 timeout as retryable', () => {
      const error = new ApiError({ message: 'Request timeout', statusCode: 408 });
      expect(isRetryableError(error)).toBe(true);
    });

    it('should identify 429 rate limit as retryable', () => {
      const error = new ApiError({ message: 'Too many requests', statusCode: 429 });
      expect(isRetryableError(error)).toBe(true);
    });

    it('should NOT retry 4xx client errors (except 408, 429)', () => {
      const error400 = new ApiError({ message: 'Bad request', statusCode: 400 });
      expect(isRetryableError(error400)).toBe(false);

      const error404 = new ApiError({ message: 'Not found', statusCode: 404 });
      expect(isRetryableError(error404)).toBe(false);

      const error422 = new ApiError({ message: 'Validation failed', statusCode: 422 });
      expect(isRetryableError(error422)).toBe(false);
    });

    it('should handle errors with Response object', () => {
      const mockResponse = { status: 500 } as Response;
      const error = { response: mockResponse };
      expect(isRetryableError(error)).toBe(true);
    });

    it('should handle errors with Response 408', () => {
      const mockResponse = { status: 408 } as Response;
      const error = { response: mockResponse };
      expect(isRetryableError(error)).toBe(true);
    });

    it('should NOT retry non-network errors', () => {
      const error = new Error('Generic error');
      expect(isRetryableError(error)).toBe(false);
    });

    it('should NOT retry string errors', () => {
      expect(isRetryableError('Some error')).toBe(false);
    });

    it('should NOT retry null/undefined', () => {
      expect(isRetryableError(null)).toBe(false);
      expect(isRetryableError(undefined)).toBe(false);
    });
  });

  describe('createRetryableFetch', () => {
    it('should create fetch wrapper that succeeds', async () => {
      const mockResponse = { status: 200, ok: true } as Response;
      const fetchFn = vi.fn().mockResolvedValueOnce(mockResponse);

      const retryableFetch = createRetryableFetch(fetchFn);
      const result = await retryableFetch();

      expect(result).toEqual(mockResponse);
      expect(fetchFn).toHaveBeenCalledTimes(1);
    });

    it('should retry on network errors', async () => {
      const mockResponse = { status: 200, ok: true } as Response;
      const fetchFn = vi
        .fn()
        .mockRejectedValueOnce(new TypeError('fetch failed'))
        .mockResolvedValueOnce(mockResponse);

      const retryableFetch = createRetryableFetch(fetchFn, {
        maxAttempts: 2,
        initialDelayMs: 10,
      });

      const result = await retryableFetch();

      expect(result).toEqual(mockResponse);
      expect(fetchFn).toHaveBeenCalledTimes(2);
    });

    it('should retry on 500 errors', async () => {
      const mockResponse = { status: 200, ok: true } as Response;
      const error = new ApiError({ message: 'Server error', statusCode: 500 });

      const fetchFn = vi.fn().mockRejectedValueOnce(error).mockResolvedValueOnce(mockResponse);

      const retryableFetch = createRetryableFetch(fetchFn, {
        maxAttempts: 2,
        initialDelayMs: 10,
      });

      const result = await retryableFetch();

      expect(result).toEqual(mockResponse);
      expect(fetchFn).toHaveBeenCalledTimes(2);
    });

    it('should NOT retry on 404 errors', async () => {
      const error = new ApiError({ message: 'Not found', statusCode: 404 });
      const fetchFn = vi.fn().mockRejectedValue(error);

      const retryableFetch = createRetryableFetch(fetchFn, { maxAttempts: 3 });

      await expect(retryableFetch()).rejects.toThrow('Not found');
      expect(fetchFn).toHaveBeenCalledTimes(1); // No retries
    });

    it('should respect custom shouldRetry', async () => {
      const error = new Error('Custom no-retry');
      const fetchFn = vi.fn().mockRejectedValue(error);

      const customShouldRetry = vi.fn().mockReturnValue(false);

      const retryableFetch = createRetryableFetch(fetchFn, {
        shouldRetry: customShouldRetry,
        maxAttempts: 3,
      });

      await expect(retryableFetch()).rejects.toThrow('Custom no-retry');
      expect(fetchFn).toHaveBeenCalledTimes(1);
    });
  });
});
