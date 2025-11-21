/**
 * Retry Policy Tests (Issue #1453)
 *
 * Tests for retry logic with exponential backoff and jitter.
 */

import {
  getRetryConfig,
  isRetryableError,
  calculateBackoffDelay,
  sleep,
  withRetry,
  RetryConfig,
} from '../core/retryPolicy';
import { ServerError, NetworkError, UnauthorizedError, RateLimitError } from '../core/errors';

describe('getRetryConfig', () => {
  let originalEnv: NodeJS.ProcessEnv;

  beforeEach(() => {
    originalEnv = process.env;
  });

  afterEach(() => {
    process.env = originalEnv;
  });

  it('should return default config when environment variables not set', () => {
    delete process.env.NEXT_PUBLIC_RETRY_MAX_ATTEMPTS;
    delete process.env.NEXT_PUBLIC_RETRY_BASE_DELAY;
    delete process.env.NEXT_PUBLIC_RETRY_MAX_DELAY;
    delete process.env.NEXT_PUBLIC_RETRY_ENABLED;

    const config = getRetryConfig();

    expect(config.maxAttempts).toBe(3);
    expect(config.baseDelay).toBe(1000);
    expect(config.maxDelay).toBe(10000);
    expect(config.enabled).toBe(true);
    expect(config.jitter).toBe(0.3);
  });

  it('should use environment variables when set', () => {
    process.env.NEXT_PUBLIC_RETRY_MAX_ATTEMPTS = '5';
    process.env.NEXT_PUBLIC_RETRY_BASE_DELAY = '2000';
    process.env.NEXT_PUBLIC_RETRY_MAX_DELAY = '15000';
    process.env.NEXT_PUBLIC_RETRY_ENABLED = 'true';

    const config = getRetryConfig();

    expect(config.maxAttempts).toBe(5);
    expect(config.baseDelay).toBe(2000);
    expect(config.maxDelay).toBe(15000);
    expect(config.enabled).toBe(true);
  });

  it('should disable retry when NEXT_PUBLIC_RETRY_ENABLED is false', () => {
    process.env.NEXT_PUBLIC_RETRY_ENABLED = 'false';

    const config = getRetryConfig();

    expect(config.enabled).toBe(false);
  });
});

describe('isRetryableError', () => {
  it('should return true for NetworkError', () => {
    const error = new NetworkError({ message: 'Network failed', endpoint: '/test' });
    expect(isRetryableError(error)).toBe(true);
  });

  it('should return true for 500 ServerError', () => {
    const error = new ServerError({ message: 'Internal error', statusCode: 500, endpoint: '/test' });
    expect(isRetryableError(error)).toBe(true);
  });

  it('should return true for 502 ServerError', () => {
    const error = new ServerError({ message: 'Bad Gateway', statusCode: 502, endpoint: '/test' });
    expect(isRetryableError(error)).toBe(true);
  });

  it('should return true for 503 ServerError', () => {
    const error = new ServerError({ message: 'Service Unavailable', statusCode: 503, endpoint: '/test' });
    expect(isRetryableError(error)).toBe(true);
  });

  it('should return false for UnauthorizedError (401)', () => {
    const error = new UnauthorizedError({ message: 'Unauthorized', endpoint: '/test' });
    expect(isRetryableError(error)).toBe(false);
  });

  it('should return false for RateLimitError (429)', () => {
    const error = new RateLimitError({ message: 'Too many requests', endpoint: '/test' });
    expect(isRetryableError(error)).toBe(false);
  });

  it('should return false for generic Error', () => {
    const error = new Error('Generic error');
    expect(isRetryableError(error)).toBe(false);
  });

  it('should return true for TypeError with fetch in message', () => {
    const error = new TypeError('fetch failed');
    expect(isRetryableError(error)).toBe(true);
  });

  it('should return false for TypeError without fetch in message', () => {
    const error = new TypeError('Something else failed');
    expect(isRetryableError(error)).toBe(false);
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
    // baseDelay * 2^0 = 1000ms, with 30% jitter = 700-1300ms
    expect(delay).toBeGreaterThanOrEqual(700);
    expect(delay).toBeLessThanOrEqual(1300);
  });

  it('should calculate exponential backoff for attempt 1', () => {
    const delay = calculateBackoffDelay(1, config);
    // baseDelay * 2^1 = 2000ms, with 30% jitter = 1400-2600ms
    expect(delay).toBeGreaterThanOrEqual(1400);
    expect(delay).toBeLessThanOrEqual(2600);
  });

  it('should calculate exponential backoff for attempt 2', () => {
    const delay = calculateBackoffDelay(2, config);
    // baseDelay * 2^2 = 4000ms, with 30% jitter = 2800-5200ms
    expect(delay).toBeGreaterThanOrEqual(2800);
    expect(delay).toBeLessThanOrEqual(5200);
  });

  it('should cap delay at maxDelay', () => {
    const delay = calculateBackoffDelay(10, config);
    // baseDelay * 2^10 = 1024000ms, but capped at maxDelay
    // 10000ms with 30% jitter = 7000-13000ms
    expect(delay).toBeGreaterThanOrEqual(7000);
    expect(delay).toBeLessThanOrEqual(13000);
  });

  it('should return integer values', () => {
    const delay = calculateBackoffDelay(1, config);
    expect(Number.isInteger(delay)).toBe(true);
  });

  it('should apply jitter consistently', () => {
    const delays: number[] = [];
    for (let i = 0; i < 100; i++) {
      delays.push(calculateBackoffDelay(0, config));
    }

    // Check that delays vary due to jitter
    const uniqueDelays = new Set(delays);
    expect(uniqueDelays.size).toBeGreaterThan(50); // Should have many unique values
  });
});

describe('sleep', () => {
  it('should sleep for specified milliseconds', async () => {
    const start = Date.now();
    await sleep(100);
    const end = Date.now();

    expect(end - start).toBeGreaterThanOrEqual(95); // Allow 5ms tolerance
    expect(end - start).toBeLessThan(150);
  });
});

describe('withRetry', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('should execute function successfully on first attempt', async () => {
    const fn = jest.fn().mockResolvedValue('success');

    const result = await withRetry(fn);

    expect(result).toBe('success');
    expect(fn).toHaveBeenCalledTimes(1);
  });

  it('should retry on ServerError (500) and succeed', async () => {
    const fn = jest
      .fn()
      .mockRejectedValueOnce(new ServerError({ message: 'Error', statusCode: 500, endpoint: '/test' }))
      .mockResolvedValueOnce('success');

    const result = await withRetry(fn, {
      retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
    });

    expect(result).toBe('success');
    expect(fn).toHaveBeenCalledTimes(2);
  });

  it('should retry on NetworkError and succeed', async () => {
    const fn = jest
      .fn()
      .mockRejectedValueOnce(new NetworkError({ message: 'Network failed', endpoint: '/test' }))
      .mockResolvedValueOnce('success');

    const result = await withRetry(fn, {
      retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
    });

    expect(result).toBe('success');
    expect(fn).toHaveBeenCalledTimes(2);
  });

  it('should not retry on UnauthorizedError (401)', async () => {
    const fn = jest
      .fn()
      .mockRejectedValue(new UnauthorizedError({ message: 'Unauthorized', endpoint: '/test' }));

    await expect(
      withRetry(fn, {
        retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
      })
    ).rejects.toThrow(UnauthorizedError);

    expect(fn).toHaveBeenCalledTimes(1);
  });

  it('should exhaust retries and throw last error', async () => {
    const error = new ServerError({ message: 'Error', statusCode: 500, endpoint: '/test' });
    const fn = jest.fn().mockRejectedValue(error);

    await expect(
      withRetry(fn, {
        retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
      })
    ).rejects.toThrow(error);

    expect(fn).toHaveBeenCalledTimes(4); // 1 initial + 3 retries
  });

  it('should skip retry when retry is disabled globally', async () => {
    const fn = jest
      .fn()
      .mockRejectedValue(new ServerError({ message: 'Error', statusCode: 500, endpoint: '/test' }));

    await expect(
      withRetry(fn, {
        retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: false, jitter: 0 },
      })
    ).rejects.toThrow(ServerError);

    expect(fn).toHaveBeenCalledTimes(1);
  });

  it('should skip retry when skipRetry option is true', async () => {
    const fn = jest
      .fn()
      .mockRejectedValue(new ServerError({ message: 'Error', statusCode: 500, endpoint: '/test' }));

    await expect(
      withRetry(fn, {
        skipRetry: true,
      })
    ).rejects.toThrow(ServerError);

    expect(fn).toHaveBeenCalledTimes(1);
  });

  it('should call onRetry callback on each retry attempt', async () => {
    const fn = jest
      .fn()
      .mockRejectedValueOnce(new ServerError({ message: 'Error', statusCode: 500, endpoint: '/test' }))
      .mockRejectedValueOnce(new ServerError({ message: 'Error', statusCode: 500, endpoint: '/test' }))
      .mockResolvedValueOnce('success');

    const onRetry = jest.fn();

    await withRetry(fn, {
      retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
      onRetry,
    });

    expect(onRetry).toHaveBeenCalledTimes(2);
    expect(onRetry).toHaveBeenNthCalledWith(
      1,
      1,
      expect.any(ServerError),
      expect.any(Number)
    );
    expect(onRetry).toHaveBeenNthCalledWith(
      2,
      2,
      expect.any(ServerError),
      expect.any(Number)
    );
  });

  it('should wait between retry attempts', async () => {
    const fn = jest
      .fn()
      .mockRejectedValueOnce(new ServerError({ message: 'Error', statusCode: 500, endpoint: '/test' }))
      .mockResolvedValueOnce('success');

    const start = Date.now();

    await withRetry(fn, {
      retryConfig: { maxAttempts: 3, baseDelay: 50, maxDelay: 100, enabled: true, jitter: 0 },
    });

    const end = Date.now();

    // Should have waited at least 50ms (baseDelay * 2^0)
    expect(end - start).toBeGreaterThanOrEqual(45); // Allow 5ms tolerance
  });

  it('should handle TypeError with fetch in message as retryable', async () => {
    const fn = jest
      .fn()
      .mockRejectedValueOnce(new TypeError('fetch failed'))
      .mockResolvedValueOnce('success');

    const result = await withRetry(fn, {
      retryConfig: { maxAttempts: 3, baseDelay: 10, maxDelay: 100, enabled: true, jitter: 0 },
    });

    expect(result).toBe('success');
    expect(fn).toHaveBeenCalledTimes(2);
  });

  it('should apply jitter to delay', async () => {
    const fn = jest
      .fn()
      .mockRejectedValueOnce(new ServerError({ message: 'Error', statusCode: 500, endpoint: '/test' }))
      .mockResolvedValueOnce('success');

    const delays: number[] = [];
    const onRetry = jest.fn((attempt, error, delayMs) => {
      delays.push(delayMs);
    });

    // Run multiple times to check jitter variation
    for (let i = 0; i < 10; i++) {
      fn.mockClear();
      fn.mockRejectedValueOnce(new ServerError({ message: 'Error', statusCode: 500, endpoint: '/test' }))
        .mockResolvedValueOnce('success');

      await withRetry(fn, {
        retryConfig: { maxAttempts: 3, baseDelay: 1000, maxDelay: 10000, enabled: true, jitter: 0.3 },
        onRetry,
      });
    }

    // Check that delays vary due to jitter
    const uniqueDelays = new Set(delays);
    expect(uniqueDelays.size).toBeGreaterThan(5); // Should have variation
  });
});
