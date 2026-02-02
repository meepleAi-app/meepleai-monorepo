/**
 * Retry Utility Tests
 * Issue #3188: Exponential backoff retry logic
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { retryWithBackoff, isRetryableError } from '../retry';

describe('retryWithBackoff', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('Successful Operations', () => {
    it('returns result on first successful attempt', async () => {
      const fn = vi.fn().mockResolvedValue('success');

      const resultPromise = retryWithBackoff(fn);
      await vi.runAllTimersAsync();
      const result = await resultPromise;

      expect(result).toBe('success');
      expect(fn).toHaveBeenCalledTimes(1);
    });

    it('returns result when function eventually succeeds', async () => {
      const fn = vi
        .fn()
        .mockRejectedValueOnce(new Error('Fail 1'))
        .mockRejectedValueOnce(new Error('Fail 2'))
        .mockResolvedValue('success');

      const resultPromise = retryWithBackoff(fn, { maxAttempts: 3 });
      await vi.runAllTimersAsync();
      const result = await resultPromise;

      expect(result).toBe('success');
      expect(fn).toHaveBeenCalledTimes(3);
    });
  });

  describe('Failure Handling', () => {
    it('throws after max attempts exceeded', async () => {
      const fn = vi.fn().mockRejectedValue(new Error('Always fails'));

      const resultPromise = retryWithBackoff(fn, { maxAttempts: 3 });

      // Catch the rejection to prevent unhandled rejection warning
      resultPromise.catch(() => {});

      await vi.runAllTimersAsync();

      await expect(resultPromise).rejects.toThrow('Always fails');
      expect(fn).toHaveBeenCalledTimes(3);
    });

    it('wraps non-Error thrown values', async () => {
      const fn = vi.fn().mockRejectedValue('string error');

      const resultPromise = retryWithBackoff(fn, { maxAttempts: 1 });

      // Catch the rejection to prevent unhandled rejection warning
      resultPromise.catch(() => {});

      await vi.runAllTimersAsync();

      await expect(resultPromise).rejects.toThrow('Unknown error');
    });
  });

  describe('Retryable Error Codes', () => {
    it('does not retry for non-retryable error codes', async () => {
      const error = new Error('Not retryable') as Error & { code: string };
      error.code = 'AUTH_ERROR';
      const fn = vi.fn().mockRejectedValue(error);

      const resultPromise = retryWithBackoff(fn, {
        maxAttempts: 3,
        retryableErrorCodes: ['NETWORK_ERROR'],
      });

      // Catch the rejection to prevent unhandled rejection warning
      resultPromise.catch(() => {});

      await vi.runAllTimersAsync();

      await expect(resultPromise).rejects.toThrow('Not retryable');
      expect(fn).toHaveBeenCalledTimes(1);
    });

    it('retries for retryable error codes', async () => {
      const error = new Error('Network issue') as Error & { code: string };
      error.code = 'NETWORK_ERROR';
      const fn = vi
        .fn()
        .mockRejectedValueOnce(error)
        .mockResolvedValue('recovered');

      const resultPromise = retryWithBackoff(fn, {
        maxAttempts: 3,
        retryableErrorCodes: ['NETWORK_ERROR'],
      });
      await vi.runAllTimersAsync();
      const result = await resultPromise;

      expect(result).toBe('recovered');
      expect(fn).toHaveBeenCalledTimes(2);
    });

    it('retries for errors without code (undefined code)', async () => {
      const fn = vi
        .fn()
        .mockRejectedValueOnce(new Error('Generic error'))
        .mockResolvedValue('recovered');

      const resultPromise = retryWithBackoff(fn, { maxAttempts: 2 });
      await vi.runAllTimersAsync();
      const result = await resultPromise;

      expect(result).toBe('recovered');
      expect(fn).toHaveBeenCalledTimes(2);
    });
  });

  describe('Timing and Backoff', () => {
    it('uses initial delay for first retry', async () => {
      const fn = vi
        .fn()
        .mockRejectedValueOnce(new Error('Fail'))
        .mockResolvedValue('success');

      const resultPromise = retryWithBackoff(fn, {
        maxAttempts: 2,
        initialDelay: 1000,
      });

      // First call happens immediately
      expect(fn).toHaveBeenCalledTimes(1);

      // Advance by half the delay - should not have retried yet
      await vi.advanceTimersByTimeAsync(500);
      expect(fn).toHaveBeenCalledTimes(1);

      // Advance to complete the delay
      await vi.advanceTimersByTimeAsync(500);
      expect(fn).toHaveBeenCalledTimes(2);

      await resultPromise;
    });

    it('applies backoff multiplier to subsequent retries', async () => {
      const fn = vi
        .fn()
        .mockRejectedValueOnce(new Error('Fail 1'))
        .mockRejectedValueOnce(new Error('Fail 2'))
        .mockResolvedValue('success');

      const resultPromise = retryWithBackoff(fn, {
        maxAttempts: 3,
        initialDelay: 100,
        backoffFactor: 2,
      });

      // First call immediately
      expect(fn).toHaveBeenCalledTimes(1);

      // After 100ms (first retry)
      await vi.advanceTimersByTimeAsync(100);
      expect(fn).toHaveBeenCalledTimes(2);

      // After 200ms more (second retry with 2x backoff)
      await vi.advanceTimersByTimeAsync(200);
      expect(fn).toHaveBeenCalledTimes(3);

      await resultPromise;
    });

    it('respects maxDelay cap', async () => {
      const fn = vi
        .fn()
        .mockRejectedValueOnce(new Error('Fail 1'))
        .mockRejectedValueOnce(new Error('Fail 2'))
        .mockRejectedValueOnce(new Error('Fail 3'))
        .mockResolvedValue('success');

      const resultPromise = retryWithBackoff(fn, {
        maxAttempts: 4,
        initialDelay: 1000,
        maxDelay: 1500,
        backoffFactor: 2,
      });

      expect(fn).toHaveBeenCalledTimes(1);

      // First retry after 1000ms
      await vi.advanceTimersByTimeAsync(1000);
      expect(fn).toHaveBeenCalledTimes(2);

      // Second retry would be 2000ms but capped at 1500ms
      await vi.advanceTimersByTimeAsync(1500);
      expect(fn).toHaveBeenCalledTimes(3);

      // Third retry also capped at 1500ms
      await vi.advanceTimersByTimeAsync(1500);
      expect(fn).toHaveBeenCalledTimes(4);

      await resultPromise;
    });
  });

  describe('Default Options', () => {
    it('uses default maxAttempts of 3', async () => {
      const fn = vi.fn().mockRejectedValue(new Error('Always fails'));

      const resultPromise = retryWithBackoff(fn);

      // Catch the rejection to prevent unhandled rejection warning
      resultPromise.catch(() => {});

      await vi.runAllTimersAsync();

      await expect(resultPromise).rejects.toThrow();
      expect(fn).toHaveBeenCalledTimes(3);
    });

    it('merges custom options with defaults', async () => {
      const fn = vi.fn().mockRejectedValue(new Error('Always fails'));

      const resultPromise = retryWithBackoff(fn, { maxAttempts: 5 });

      // Catch the rejection to prevent unhandled rejection warning
      resultPromise.catch(() => {});

      await vi.runAllTimersAsync();

      await expect(resultPromise).rejects.toThrow();
      expect(fn).toHaveBeenCalledTimes(5);
    });
  });
});

describe('isRetryableError', () => {
  describe('Input Validation', () => {
    it('returns false for non-Error values', () => {
      expect(isRetryableError('string error', ['NETWORK_ERROR'])).toBe(false);
      expect(isRetryableError(null, ['NETWORK_ERROR'])).toBe(false);
      expect(isRetryableError(undefined, ['NETWORK_ERROR'])).toBe(false);
      expect(isRetryableError(123, ['NETWORK_ERROR'])).toBe(false);
    });

    it('returns false for Error without code', () => {
      const error = new Error('No code');

      expect(isRetryableError(error, ['NETWORK_ERROR'])).toBe(false);
    });
  });

  describe('Code Matching', () => {
    it('returns true when error code is in retryable list', () => {
      const error = new Error('Network issue') as Error & { code: string };
      error.code = 'NETWORK_ERROR';

      expect(isRetryableError(error, ['NETWORK_ERROR', 'TIMEOUT'])).toBe(true);
    });

    it('returns false when error code is not in retryable list', () => {
      const error = new Error('Auth issue') as Error & { code: string };
      error.code = 'AUTH_ERROR';

      expect(isRetryableError(error, ['NETWORK_ERROR', 'TIMEOUT'])).toBe(false);
    });

    it('returns false for empty retryable codes list', () => {
      const error = new Error('Some issue') as Error & { code: string };
      error.code = 'NETWORK_ERROR';

      expect(isRetryableError(error, [])).toBe(false);
    });

    it('uses empty array as default when no list provided', () => {
      const error = new Error('Some issue') as Error & { code: string };
      error.code = 'NETWORK_ERROR';

      expect(isRetryableError(error)).toBe(false);
    });
  });
});
