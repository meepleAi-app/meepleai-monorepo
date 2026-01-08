/**
 * Tests for Retry Policy (Issue #2340)
 *
 * Coverage target: 90%+
 * Tests: Exponential backoff with jitter, retry logic, Retry-After header support
 */

import {
  getRetryConfig,
  isRetryableError,
  parseRetryAfter,
  calculateBackoffDelay,
  sleep,
  withRetry,
  type RetryConfig,
} from '../retryPolicy';
import { ApiError, NetworkError, ServerError } from '../errors';

describe('retryPolicy', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.restoreAllMocks();
  });

  describe('getRetryConfig', () => {
    it('should return default config', () => {
      const config = getRetryConfig();

      expect(config).toMatchObject({
        maxAttempts: expect.any(Number),
        baseDelay: expect.any(Number),
        maxDelay: expect.any(Number),
        enabled: expect.any(Boolean),
        jitter: expect.any(Number),
      });
      expect(config.jitter).toBeGreaterThanOrEqual(0);
      expect(config.jitter).toBeLessThanOrEqual(1);
    });
  });

  describe('isRetryableError', () => {
    it('should identify NetworkError as retryable', () => {
      const error = new NetworkError({ message: 'Connection failed' });
      expect(isRetryableError(error)).toBe(true);
    });

    it('should identify 500 ServerError as retryable', () => {
      const error = new ServerError({ message: 'Internal error', statusCode: 500 });
      expect(isRetryableError(error)).toBe(true);
    });

    it('should identify 502 ServerError as retryable', () => {
      const error = new ServerError({ message: 'Bad gateway', statusCode: 502 });
      expect(isRetryableError(error)).toBe(true);
    });

    it('should identify 503 ServerError as retryable', () => {
      const error = new ServerError({ message: 'Service unavailable', statusCode: 503 });
      expect(isRetryableError(error)).toBe(true);
    });

    it('should identify TypeError fetch as retryable', () => {
      const error = new TypeError('fetch failed');
      expect(isRetryableError(error)).toBe(true);
    });

    it('should NOT retry 504 ServerError', () => {
      const error = new ServerError({ message: 'Gateway timeout', statusCode: 504 });
      expect(isRetryableError(error)).toBe(false);
    });

    it('should NOT retry ApiError 400', () => {
      const error = new ApiError({ message: 'Bad request', statusCode: 400 });
      expect(isRetryableError(error)).toBe(false);
    });

    it('should NOT retry generic Error', () => {
      const error = new Error('Generic error');
      expect(isRetryableError(error)).toBe(false);
    });
  });

  describe('parseRetryAfter', () => {
    it('should parse seconds format', () => {
      const result = parseRetryAfter('120');
      expect(result).toBe(120000); // 120 seconds = 120000ms
    });

    it('should parse HTTP-date format', () => {
      const futureDate = new Date(Date.now() + 5000);
      const result = parseRetryAfter(futureDate.toUTCString());

      expect(result).toBeGreaterThan(4000);
      expect(result).toBeLessThan(6000);
    });

    it('should return undefined for null', () => {
      expect(parseRetryAfter(null)).toBeUndefined();
    });

    it('should return undefined for undefined', () => {
      expect(parseRetryAfter(undefined)).toBeUndefined();
    });

    it('should return undefined for invalid format', () => {
      expect(parseRetryAfter('invalid')).toBeUndefined();
    });

    it('should handle negative seconds as invalid number', () => {
      // parseInt("-10") = -10, which fails > 0 check, then tries date parsing
      // "-10" as date may parse to 0 (past) or undefined depending on implementation
      const result = parseRetryAfter('-10');
      expect(result === undefined || result === 0).toBe(true);
    });

    it('should return 0 for past dates', () => {
      const pastDate = new Date(Date.now() - 5000);
      const result = parseRetryAfter(pastDate.toUTCString());

      expect(result).toBe(0);
    });
  });

  describe('calculateBackoffDelay', () => {
    const config: RetryConfig = {
      maxAttempts: 3,
      baseDelay: 1000,
      maxDelay: 10000,
      enabled: true,
      jitter: 0.3,
    };

    it('should calculate exponential backoff for attempt 0', () => {
      const delay = calculateBackoffDelay(0, config);

      // baseDelay * 2^0 = 1000ms ± 30% = 700-1300ms
      expect(delay).toBeGreaterThanOrEqual(700);
      expect(delay).toBeLessThanOrEqual(1300);
    });

    it('should calculate exponential backoff for attempt 1', () => {
      const delay = calculateBackoffDelay(1, config);

      // baseDelay * 2^1 = 2000ms ± 30% = 1400-2600ms
      expect(delay).toBeGreaterThanOrEqual(1400);
      expect(delay).toBeLessThanOrEqual(2600);
    });

    it('should cap delay at maxDelay', () => {
      const delay = calculateBackoffDelay(10, config);

      // Would be huge but capped at maxDelay ± jitter
      expect(delay).toBeLessThanOrEqual(config.maxDelay * 1.3);
    });

    it('should use Retry-After when provided', () => {
      const retryAfterMs = 5000;
      const delay = calculateBackoffDelay(0, config, retryAfterMs);

      // Should be >= retryAfterMs (server minimum)
      expect(delay).toBeGreaterThanOrEqual(retryAfterMs);
      // But capped at maxDelay + jitter
      expect(delay).toBeLessThanOrEqual(config.maxDelay * 1.3);
    });

    it('should cap Retry-After at maxDelay', () => {
      const retryAfterMs = 50000; // Way above maxDelay
      const delay = calculateBackoffDelay(0, config, retryAfterMs);

      // Should be capped
      expect(delay).toBeLessThanOrEqual(config.maxDelay * 1.3);
      expect(delay).toBeGreaterThanOrEqual(config.maxDelay);
    });

    it('should return integer delays', () => {
      const delay = calculateBackoffDelay(0, config);
      expect(Number.isInteger(delay)).toBe(true);
    });
  });

  describe('sleep', () => {
    it('should wait for specified time', async () => {
      vi.useFakeTimers();

      const sleepPromise = sleep(1000);

      vi.advanceTimersByTime(1000);

      await expect(sleepPromise).resolves.toBeUndefined();

      vi.useRealTimers();
    });
  });

  describe('withRetry', () => {
    it('should return result on first success', async () => {
      const fn = vi.fn().mockResolvedValueOnce('success');

      const result = await withRetry(fn);

      expect(result).toBe('success');
      expect(fn).toHaveBeenCalledTimes(1);
    });

    it('should retry on retryable error and succeed', async () => {
      const networkError = new NetworkError({ message: 'Connection failed' });

      const fn = vi.fn().mockRejectedValueOnce(networkError).mockResolvedValueOnce('success');

      const result = await withRetry(fn, {
        retryConfig: { maxAttempts: 2, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
      });

      expect(result).toBe('success');
      expect(fn).toHaveBeenCalledTimes(2);
    });

    it('should NOT retry on non-retryable error', async () => {
      const error = new ApiError({ message: 'Bad request', statusCode: 400 });
      const fn = vi.fn().mockRejectedValue(error);

      await expect(
        withRetry(fn, {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        })
      ).rejects.toThrow('Bad request');

      expect(fn).toHaveBeenCalledTimes(1); // No retries
    });

    it('should throw after max attempts', async () => {
      const error = new ServerError({ message: 'Server error', statusCode: 500 });
      const fn = vi.fn().mockRejectedValue(error);

      await expect(
        withRetry(fn, {
          retryConfig: { maxAttempts: 2, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        })
      ).rejects.toThrow('Server error');

      expect(fn).toHaveBeenCalledTimes(3); // Initial + 2 retries
    });

    it('should skip retry when disabled globally', async () => {
      const error = new NetworkError({ message: 'Connection failed' });
      const fn = vi.fn().mockRejectedValue(error);

      await expect(
        withRetry(fn, {
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: false, jitter: 0 },
        })
      ).rejects.toThrow('Connection failed');

      expect(fn).toHaveBeenCalledTimes(1); // No retries
    });

    it('should skip retry when skipRetry=true', async () => {
      const error = new NetworkError({ message: 'Connection failed' });
      const fn = vi.fn().mockRejectedValue(error);

      await expect(
        withRetry(fn, {
          skipRetry: true,
          retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        })
      ).rejects.toThrow('Connection failed');

      expect(fn).toHaveBeenCalledTimes(1); // No retries
    });

    it('should call onRetry callback', async () => {
      const error = new ServerError({ message: 'Server error', statusCode: 500 });
      const fn = vi.fn().mockRejectedValueOnce(error).mockResolvedValueOnce('success');

      const onRetry = vi.fn();

      await withRetry(fn, {
        retryConfig: { maxAttempts: 2, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
        onRetry,
      });

      expect(onRetry).toHaveBeenCalledTimes(1);
      expect(onRetry).toHaveBeenCalledWith(1, error, expect.any(Number));
    });

    it('should honor Retry-After header', async () => {
      const mockResponse = {
        headers: new Headers({ 'Retry-After': '5' }), // 5 seconds
      } as Response;

      const error = new ServerError({
        message: 'Server error',
        statusCode: 503,
        response: mockResponse,
      });

      const fn = vi.fn().mockRejectedValueOnce(error).mockResolvedValueOnce('success');

      const onRetry = vi.fn();

      vi.useFakeTimers();

      const promise = withRetry(fn, {
        retryConfig: {
          maxAttempts: 2,
          baseDelay: 1000,
          maxDelay: 10000,
          enabled: true,
          jitter: 0.3,
        },
        onRetry,
      });

      // Fast forward past the delay
      await vi.runAllTimersAsync();

      await promise;

      // Verify Retry-After was used (5000ms base, with jitter)
      expect(onRetry).toHaveBeenCalledWith(1, error, expect.any(Number));
      const delayUsed = onRetry.mock.calls[0][2];
      expect(delayUsed).toBeGreaterThanOrEqual(5000); // Server minimum

      vi.useRealTimers();
    });
  });
});
