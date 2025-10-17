/**
 * Unit tests for retry logic with exponential backoff (PDF-06)
 */

import { retryWithBackoff, isRetryableError, createRetryableFetch } from '../retryUtils';
import { ApiError } from '../api';

// Mock timer functions
jest.useFakeTimers();

describe('retryUtils', () => {
  beforeEach(() => {
    jest.clearAllTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
  });

  describe('retryWithBackoff', () => {
    it('should succeed on first attempt', async () => {
      const fn = jest.fn().mockResolvedValue('success');

      const result = await retryWithBackoff(fn);

      expect(result).toBe('success');
      expect(fn).toHaveBeenCalledTimes(1);
    });

    it('should retry on failure and succeed', async () => {
      const fn = jest
        .fn()
        .mockRejectedValueOnce(new Error('First failure'))
        .mockResolvedValueOnce('success');

      const promise = retryWithBackoff(fn, { maxAttempts: 3 });

      // Fast-forward through the delay
      await jest.runAllTimersAsync();

      const result = await promise;

      expect(result).toBe('success');
      expect(fn).toHaveBeenCalledTimes(2);
    });

    it('should throw after all retries fail', async () => {
      const error = new Error('Persistent failure');
      const fn = jest.fn().mockRejectedValue(error);

      const promise = retryWithBackoff(fn, { maxAttempts: 3 }).catch(e => e);

      // Fast-forward through all delays
      await jest.runAllTimersAsync();

      const result = await promise;

      expect(result).toBeInstanceOf(Error);
      expect(result.message).toBe('Persistent failure');
      expect(fn).toHaveBeenCalledTimes(3);
    });

    it('should use exponential backoff delays', async () => {
      const fn = jest.fn().mockRejectedValue(new Error('Failure'));
      const onRetry = jest.fn();

      const promise = retryWithBackoff(fn, {
        maxAttempts: 3,
        initialDelayMs: 1000,
        onRetry
      }).catch(e => e);

      await jest.runAllTimersAsync();
      await promise;

      // Check that delays follow exponential backoff: 1s, 2s
      expect(onRetry).toHaveBeenCalledTimes(2);
      expect(onRetry).toHaveBeenNthCalledWith(1, expect.any(Error), 1, 1000);
      expect(onRetry).toHaveBeenNthCalledWith(2, expect.any(Error), 2, 2000);
    });

    it('should cap delay at maxDelayMs', async () => {
      const fn = jest.fn().mockRejectedValue(new Error('Failure'));
      const onRetry = jest.fn();

      const promise = retryWithBackoff(fn, {
        maxAttempts: 4,
        initialDelayMs: 1000,
        maxDelayMs: 2000,
        onRetry
      }).catch(e => e);

      await jest.runAllTimersAsync();
      await promise;

      // Delays should be: 1s, 2s, 2s (capped)
      expect(onRetry).toHaveBeenNthCalledWith(1, expect.any(Error), 1, 1000);
      expect(onRetry).toHaveBeenNthCalledWith(2, expect.any(Error), 2, 2000);
      expect(onRetry).toHaveBeenNthCalledWith(3, expect.any(Error), 3, 2000);
    });

    it('should respect custom shouldRetry function', async () => {
      const fn = jest.fn().mockRejectedValue(new Error('Non-retryable'));
      const shouldRetry = jest.fn().mockReturnValue(false);

      const promise = retryWithBackoff(fn, { shouldRetry, maxAttempts: 3 });

      await expect(promise).rejects.toThrow('Non-retryable');
      expect(fn).toHaveBeenCalledTimes(1);
      // shouldRetry is called after first failure to determine if we should retry
      expect(shouldRetry).toHaveBeenCalledTimes(1);
      expect(shouldRetry).toHaveBeenCalledWith(expect.any(Error), 0);
    });

    it('should call onRetry callback with correct parameters', async () => {
      const error = new Error('Retry me');
      const fn = jest.fn().mockRejectedValue(error);
      const onRetry = jest.fn();

      const promise = retryWithBackoff(fn, {
        maxAttempts: 2,
        initialDelayMs: 500,
        onRetry
      }).catch(e => e);

      await jest.runAllTimersAsync();
      await promise;

      expect(onRetry).toHaveBeenCalledTimes(1);
      expect(onRetry).toHaveBeenCalledWith(error, 1, 500);
    });
  });

  describe('isRetryableError', () => {
    it('should identify network errors as retryable', () => {
      const error = new TypeError('fetch failed');

      expect(isRetryableError(error)).toBe(true);
    });

    it('should identify 5xx errors as retryable', () => {
      const error = new ApiError('Server error', 500);

      expect(isRetryableError(error)).toBe(true);
    });

    it('should identify 408 timeout as retryable', () => {
      const error = new ApiError('Timeout', 408);

      expect(isRetryableError(error)).toBe(true);
    });

    it('should identify 429 rate limit as retryable', () => {
      const error = new ApiError('Too many requests', 429);

      expect(isRetryableError(error)).toBe(true);
    });

    it('should not identify 4xx errors (except 408, 429) as retryable', () => {
      const error400 = new ApiError('Bad request', 400);
      const error401 = new ApiError('Unauthorized', 401);
      const error403 = new ApiError('Forbidden', 403);
      const error404 = new ApiError('Not found', 404);

      expect(isRetryableError(error400)).toBe(false);
      expect(isRetryableError(error401)).toBe(false);
      expect(isRetryableError(error403)).toBe(false);
      expect(isRetryableError(error404)).toBe(false);
    });

    it('should not identify unknown errors as retryable', () => {
      const error = new Error('Unknown error');

      expect(isRetryableError(error)).toBe(false);
    });

    it('should handle errors with response object', () => {
      const response = new Response(null, { status: 503 });
      const error = { response };

      expect(isRetryableError(error)).toBe(true);
    });
  });

  describe('createRetryableFetch', () => {
    it('should create a retryable fetch function', async () => {
      const response = new Response('success');
      const fetchFn = jest.fn().mockResolvedValue(response);

      const retryableFetch = createRetryableFetch(fetchFn);
      const result = await retryableFetch();

      expect(result).toBe(response);
      expect(fetchFn).toHaveBeenCalledTimes(1);
    });

    it('should retry on retryable errors', async () => {
      const errorResponse = new Response(null, { status: 500 });
      const successResponse = new Response('success');

      const fetchFn = jest
        .fn()
        .mockRejectedValueOnce(new ApiError('Server error', 500, undefined, errorResponse))
        .mockResolvedValueOnce(successResponse);

      const retryableFetch = createRetryableFetch(fetchFn, { maxAttempts: 2 });

      const promise = retryableFetch();
      await jest.runAllTimersAsync();

      const result = await promise;

      expect(result).toBe(successResponse);
      expect(fetchFn).toHaveBeenCalledTimes(2);
    });

    it('should not retry on non-retryable errors', async () => {
      const errorResponse = new Response(null, { status: 400 });
      const error = new ApiError('Bad request', 400, undefined, errorResponse);
      const fetchFn = jest.fn().mockRejectedValue(error);

      const retryableFetch = createRetryableFetch(fetchFn);

      await expect(retryableFetch()).rejects.toThrow('Bad request');
      expect(fetchFn).toHaveBeenCalledTimes(1);
    });

    it('should respect custom options', async () => {
      const error = new TypeError('fetch failed');
      const fetchFn = jest.fn().mockRejectedValue(error);
      const onRetry = jest.fn();

      const retryableFetch = createRetryableFetch(fetchFn, {
        maxAttempts: 3,
        initialDelayMs: 500,
        onRetry
      });

      const promise = retryableFetch().catch(e => e);
      await jest.runAllTimersAsync();
      await promise;

      expect(fetchFn).toHaveBeenCalledTimes(3);
      expect(onRetry).toHaveBeenCalledTimes(2);
    });

    it('should allow custom shouldRetry override', async () => {
      const error = new ApiError('Server error', 500);
      const fetchFn = jest.fn().mockRejectedValue(error);

      // Custom shouldRetry that prevents retries
      const retryableFetch = createRetryableFetch(fetchFn, {
        shouldRetry: () => false
      });

      await expect(retryableFetch()).rejects.toThrow();
      expect(fetchFn).toHaveBeenCalledTimes(1);
    });
  });

  describe('Real-world scenarios', () => {
    it('should handle intermittent network failures', async () => {
      const fn = jest
        .fn()
        .mockRejectedValueOnce(new TypeError('fetch failed'))
        .mockRejectedValueOnce(new TypeError('fetch failed'))
        .mockResolvedValueOnce('success');

      const promise = retryWithBackoff(fn, { maxAttempts: 3 });
      await jest.runAllTimersAsync();

      const result = await promise;

      expect(result).toBe('success');
      expect(fn).toHaveBeenCalledTimes(3);
    });

    it('should handle server errors with eventual success', async () => {
      const successResponse = new Response('success');
      const fn = jest
        .fn()
        .mockRejectedValueOnce(new ApiError('Server overloaded', 503))
        .mockResolvedValueOnce(successResponse);

      const promise = retryWithBackoff(fn, {
        maxAttempts: 2,
        shouldRetry: isRetryableError
      });

      await jest.runAllTimersAsync();

      const result = await promise;

      expect(result).toBe(successResponse);
      expect(fn).toHaveBeenCalledTimes(2);
    });

    it('should not retry validation errors', async () => {
      const fn = jest.fn().mockRejectedValue(new ApiError('File too large', 400));

      const promise = retryWithBackoff(fn, {
        shouldRetry: isRetryableError
      });

      await expect(promise).rejects.toThrow('File too large');
      expect(fn).toHaveBeenCalledTimes(1);
    });
  });
});
