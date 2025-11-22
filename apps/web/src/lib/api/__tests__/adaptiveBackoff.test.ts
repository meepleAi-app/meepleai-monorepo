/**
 * Adaptive Backoff Tests (Enhancement for #1453)
 *
 * Tests for Retry-After header parsing and adaptive backoff logic.
 */

import {
  parseRetryAfter,
  calculateBackoffDelay,
  getRetryConfig,
  RetryConfig,
} from '../core/retryPolicy';

describe('Adaptive Backoff', () => {
  describe('parseRetryAfter', () => {
    it('should return undefined for null value', () => {
      expect(parseRetryAfter(null)).toBeUndefined();
    });

    it('should return undefined for undefined value', () => {
      expect(parseRetryAfter(undefined)).toBeUndefined();
    });

    it('should return undefined for empty string', () => {
      expect(parseRetryAfter('')).toBeUndefined();
    });

    it('should parse seconds format', () => {
      expect(parseRetryAfter('30')).toBe(30000); // 30s in milliseconds
    });

    it('should parse large seconds value', () => {
      expect(parseRetryAfter('120')).toBe(120000); // 2 minutes
    });

    it('should parse single digit seconds', () => {
      expect(parseRetryAfter('5')).toBe(5000);
    });

    it('should return undefined for negative seconds', () => {
      expect(parseRetryAfter('-30')).toBeUndefined();
    });

    it('should return undefined for zero seconds', () => {
      expect(parseRetryAfter('0')).toBeUndefined();
    });

    it('should parse HTTP-date format (RFC 7231)', () => {
      const futureDate = new Date(Date.now() + 60000); // 1 minute from now
      const httpDate = futureDate.toUTCString();

      const result = parseRetryAfter(httpDate);

      expect(result).toBeDefined();
      expect(result).toBeGreaterThan(55000); // At least 55s
      expect(result).toBeLessThan(65000); // At most 65s (accounting for test execution time)
    });

    it('should handle past HTTP-date gracefully', () => {
      const pastDate = new Date(Date.now() - 60000); // 1 minute ago
      const httpDate = pastDate.toUTCString();

      const result = parseRetryAfter(httpDate);

      expect(result).toBe(0); // Max with 0 for past dates
    });

    it('should return undefined for invalid date string', () => {
      expect(parseRetryAfter('not-a-date')).toBeUndefined();
    });

    it('should return undefined for invalid number', () => {
      expect(parseRetryAfter('abc123')).toBeUndefined();
    });

    it('should handle fractional seconds by truncating', () => {
      // parseInt truncates, so "30.5" becomes 30
      expect(parseRetryAfter('30.5')).toBe(30000);
    });
  });

  describe('calculateBackoffDelay with adaptive backoff', () => {
    const config: RetryConfig = {
      maxAttempts: 3,
      baseDelay: 1000,
      maxDelay: 10000,
      enabled: true,
      jitter: 0.3,
    };

    describe('with Retry-After header', () => {
      it('should use server-provided delay when available', () => {
        const retryAfterMs = 5000; // 5 seconds

        const delay = calculateBackoffDelay(0, config, retryAfterMs);

        // With 30% jitter: 3500-6500ms
        expect(delay).toBeGreaterThanOrEqual(3500);
        expect(delay).toBeLessThanOrEqual(6500);
      });

      it('should apply jitter to server-provided delay', () => {
        const retryAfterMs = 10000; // 10 seconds

        const delays: number[] = [];
        for (let i = 0; i < 100; i++) {
          delays.push(calculateBackoffDelay(0, config, retryAfterMs));
        }

        // Check that delays vary due to jitter
        const uniqueDelays = new Set(delays);
        expect(uniqueDelays.size).toBeGreaterThan(50); // Should have many unique values
      });

      it('should cap server delay at maxDelay', () => {
        const retryAfterMs = 20000; // 20 seconds (> maxDelay)

        const delay = calculateBackoffDelay(0, config, retryAfterMs);

        // Should be capped at maxDelay (10000) with jitter: 7000-13000ms
        expect(delay).toBeGreaterThanOrEqual(7000);
        expect(delay).toBeLessThanOrEqual(13000);
      });

      it('should ignore attempt number when using server delay', () => {
        const retryAfterMs = 3000;

        const delay1 = calculateBackoffDelay(0, config, retryAfterMs);
        const delay2 = calculateBackoffDelay(5, config, retryAfterMs);

        // Both should be in the same range (2100-3900ms with jitter)
        expect(delay1).toBeGreaterThanOrEqual(2100);
        expect(delay1).toBeLessThanOrEqual(3900);
        expect(delay2).toBeGreaterThanOrEqual(2100);
        expect(delay2).toBeLessThanOrEqual(3900);
      });

      it('should handle small server delays', () => {
        const retryAfterMs = 100; // 100ms

        const delay = calculateBackoffDelay(0, config, retryAfterMs);

        // With 30% jitter: 70-130ms
        expect(delay).toBeGreaterThanOrEqual(70);
        expect(delay).toBeLessThanOrEqual(130);
      });

      it('should return integer values', () => {
        const retryAfterMs = 1234;

        const delay = calculateBackoffDelay(0, config, retryAfterMs);

        expect(Number.isInteger(delay)).toBe(true);
      });
    });

    describe('without Retry-After header (fallback to exponential)', () => {
      it('should use exponential backoff when no server delay', () => {
        const delay = calculateBackoffDelay(0, config);

        // Attempt 0: 1000ms with 30% jitter = 700-1300ms
        expect(delay).toBeGreaterThanOrEqual(700);
        expect(delay).toBeLessThanOrEqual(1300);
      });

      it('should use exponential backoff when server delay is undefined', () => {
        const delay = calculateBackoffDelay(1, config, undefined);

        // Attempt 1: 2000ms with 30% jitter = 1400-2600ms
        expect(delay).toBeGreaterThanOrEqual(1400);
        expect(delay).toBeLessThanOrEqual(2600);
      });

      it('should use exponential backoff when server delay is 0', () => {
        const delay = calculateBackoffDelay(2, config, 0);

        // Server delay of 0 means fallback to exponential
        // Attempt 2: 4000ms with 30% jitter = 2800-5200ms
        expect(delay).toBeGreaterThanOrEqual(2800);
        expect(delay).toBeLessThanOrEqual(5200);
      });
    });

    describe('edge cases', () => {
      it('should handle retryAfterMs exactly at maxDelay', () => {
        const retryAfterMs = config.maxDelay; // 10000ms

        const delay = calculateBackoffDelay(0, config, retryAfterMs);

        // Should be at maxDelay with jitter: 7000-13000ms
        expect(delay).toBeGreaterThanOrEqual(7000);
        expect(delay).toBeLessThanOrEqual(13000);
      });

      it('should handle very large retryAfterMs', () => {
        const retryAfterMs = 999999999; // Very large

        const delay = calculateBackoffDelay(0, config, retryAfterMs);

        // Should be capped at maxDelay: 7000-13000ms
        expect(delay).toBeGreaterThanOrEqual(7000);
        expect(delay).toBeLessThanOrEqual(13000);
      });

      it('should handle config with zero jitter', () => {
        const noJitterConfig: RetryConfig = {
          ...config,
          jitter: 0,
        };

        const retryAfterMs = 5000;

        const delay = calculateBackoffDelay(0, noJitterConfig, retryAfterMs);

        // Should be exactly retryAfterMs when jitter is 0
        expect(delay).toBe(5000);
      });

      it('should handle config with high jitter', () => {
        const highJitterConfig: RetryConfig = {
          ...config,
          jitter: 0.9, // 90% jitter
        };

        const retryAfterMs = 10000;

        const delay = calculateBackoffDelay(0, highJitterConfig, retryAfterMs);

        // With 90% jitter: 1000-19000ms (but capped at maxDelay)
        expect(delay).toBeGreaterThanOrEqual(1000);
        expect(delay).toBeLessThanOrEqual(19000);
      });
    });
  });

  describe('integration with retry config', () => {
    it('should respect maxDelay from config', () => {
      const config: RetryConfig = {
        maxAttempts: 3,
        baseDelay: 1000,
        maxDelay: 3000, // Low max delay
        enabled: true,
        jitter: 0,
      };

      const retryAfterMs = 10000; // Server wants 10s

      const delay = calculateBackoffDelay(0, config, retryAfterMs);

      // Should be capped at 3000ms
      expect(delay).toBe(3000);
    });

    it('should work with default config', () => {
      const config = getRetryConfig();

      const retryAfterMs = 5000;

      const delay = calculateBackoffDelay(0, config, retryAfterMs);

      expect(delay).toBeGreaterThan(0);
      expect(delay).toBeLessThanOrEqual(config.maxDelay * 1.3); // Account for jitter
    });
  });

  describe('realistic scenarios', () => {
    it('should handle rate limit scenario (429 with Retry-After)', () => {
      const config = getRetryConfig();
      const retryAfterMs = 60000; // Server says wait 60 seconds

      const delay = calculateBackoffDelay(0, config, retryAfterMs);

      // Should use server delay (capped at maxDelay=10000ms with jitter)
      expect(delay).toBeGreaterThanOrEqual(7000);
      expect(delay).toBeLessThanOrEqual(13000);
    });

    it('should handle server overload scenario (503 with Retry-After)', () => {
      const config = getRetryConfig();
      const retryAfterMs = 30000; // Server says wait 30 seconds

      const delay = calculateBackoffDelay(0, config, retryAfterMs);

      // Should be capped at maxDelay (10000ms with jitter)
      expect(delay).toBeGreaterThanOrEqual(7000);
      expect(delay).toBeLessThanOrEqual(13000);
    });

    it('should handle quick recovery scenario', () => {
      const config = getRetryConfig();
      const retryAfterMs = 1000; // Server says wait 1 second

      const delay = calculateBackoffDelay(0, config, retryAfterMs);

      // Should use exact server delay (with jitter): 700-1300ms
      expect(delay).toBeGreaterThanOrEqual(700);
      expect(delay).toBeLessThanOrEqual(1300);
    });

    it('should fallback to exponential for transient errors without Retry-After', () => {
      const config = getRetryConfig();

      // Simulate 3 retry attempts without server guidance
      const delays = [
        calculateBackoffDelay(0, config), // ~1000ms
        calculateBackoffDelay(1, config), // ~2000ms
        calculateBackoffDelay(2, config), // ~4000ms
      ];

      // Each should be roughly double the previous (with jitter tolerance)
      expect(delays[0]).toBeGreaterThanOrEqual(700);
      expect(delays[0]).toBeLessThanOrEqual(1300);

      expect(delays[1]).toBeGreaterThanOrEqual(1400);
      expect(delays[1]).toBeLessThanOrEqual(2600);

      expect(delays[2]).toBeGreaterThanOrEqual(2800);
      expect(delays[2]).toBeLessThanOrEqual(5200);
    });
  });
});
