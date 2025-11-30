/**
 * Unit tests for logger service
 *
 * Vitest Migration (Issue #1503):
 * - ✅ Migrated from Jest to Vitest
 * - ✅ Uses MSW 2.x for API mocking
 * - ✅ Test isolation via server.resetHandlers()
 */

import { describe, it, expect, beforeEach, afterEach, beforeAll, vi } from 'vitest';
import { getLogger, resetLogger, LogLevel } from '../logger';
import { ApiError } from '../errors';
import { server } from '@/__tests__/mocks/server';
import { http, HttpResponse } from 'msw';

describe('Logger', () => {
  let fetchSpy: any;

  // Setup MSW handler for logging endpoint
  beforeAll(() => {
    server.use(
      http.post('*/api/v1/logs', () => {
        return HttpResponse.json({}, {
          status: 204,
          headers: {
            'X-Correlation-Id': `test-correlation-${Date.now()}`,
          },
        });
      })
    );
  });

  beforeEach(() => {
    resetLogger();
    vi.clearAllMocks();
    vi.useFakeTimers();
    // Spy on fetch to track calls (MSW intercepts but doesn't spy by default)
    fetchSpy = vi.spyOn(global, 'fetch');
  });

  afterEach(() => {
    vi.runOnlyPendingTimers();
    vi.useRealTimers();
    fetchSpy?.mockRestore();
  });

  describe('initialization', () => {
    it('should create logger with default config', () => {
      const logger = getLogger();
      expect(logger).toBeDefined();
    });

    it('should return same instance on multiple calls', () => {
      const logger1 = getLogger();
      const logger2 = getLogger();
      expect(logger1).toBe(logger2);
    });

    it('should accept custom config', () => {
      const logger = getLogger({ minLevel: LogLevel.WARN });
      expect(logger).toBeDefined();
    });
  });

  describe('correlation ID', () => {
    it('should set and get correlation ID', () => {
      const logger = getLogger();
      logger.setCorrelationId('test-correlation-123');
      expect(logger.getCorrelationId()).toBe('test-correlation-123');
    });
  });

  describe('logging methods', () => {
    it('should log debug messages', () => {
      const consoleSpy = vi.spyOn(console, 'debug').mockImplementation();
      const logger = getLogger({ enableConsole: true, minLevel: LogLevel.DEBUG });

      logger.debug('Debug message', { component: 'TestComponent' });

      expect(consoleSpy).toHaveBeenCalled();
      consoleSpy.mockRestore();
    });

    it('should log info messages', () => {
      const consoleSpy = vi.spyOn(console, 'info').mockImplementation();
      const logger = getLogger({ enableConsole: true, minLevel: LogLevel.INFO });

      logger.info('Info message');

      expect(consoleSpy).toHaveBeenCalled();
      consoleSpy.mockRestore();
    });

    it('should log warnings', () => {
      const consoleSpy = vi.spyOn(console, 'warn').mockImplementation();
      const logger = getLogger({ enableConsole: true, minLevel: LogLevel.WARN });

      logger.warn('Warning message');

      expect(consoleSpy).toHaveBeenCalled();
      consoleSpy.mockRestore();
    });

    it('should log errors with error object', () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation();
      const logger = getLogger({ enableConsole: true });

      const error = new ApiError('Not found', 404, '/api/v1/test');
      logger.error('Error occurred', error);

      expect(consoleSpy).toHaveBeenCalled();
      consoleSpy.mockRestore();
    });

    it('should respect minimum log level', () => {
      const consoleSpy = vi.spyOn(console, 'debug').mockImplementation();
      const logger = getLogger({ enableConsole: true, minLevel: LogLevel.WARN });

      logger.debug('Debug message');
      logger.info('Info message');

      expect(consoleSpy).not.toHaveBeenCalled();
      consoleSpy.mockRestore();
    });
  });

  describe('remote logging', () => {
    it('should batch logs before sending', () => {
      const logger = getLogger({ enableRemote: true, batchSize: 3 });

      logger.info('Message 1');
      logger.info('Message 2');

      expect(fetch).not.toHaveBeenCalled();

      logger.info('Message 3');

      expect(fetch).toHaveBeenCalledTimes(1);
    });

    it('should flush logs periodically', () => {
      const logger = getLogger({ enableRemote: true, flushIntervalMs: 5000 });

      logger.info('Message 1');
      expect(fetch).not.toHaveBeenCalled();

      vi.advanceTimersByTime(5000);

      expect(fetch).toHaveBeenCalledTimes(1);
    });

    it('should include correlation ID in request headers', () => {
      const logger = getLogger({ enableRemote: true, batchSize: 1 });
      logger.setCorrelationId('test-corr-123');

      logger.info('Test message');

      expect(fetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          headers: expect.objectContaining({
            'X-Correlation-Id': 'test-corr-123'
          })
        })
      );
    });

    it('should handle flush errors gracefully', () => {
      fetchSpy.mockRejectedValue(new Error('Network error'));
      const logger = getLogger({ enableRemote: true, batchSize: 1, enableConsole: false });

      expect(() => {
        logger.info('Test message');
      }).not.toThrow();
    });

    it('should send logs with keepalive flag', () => {
      const logger = getLogger({ enableRemote: true, batchSize: 1 });

      logger.info('Test message');

      expect(fetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          keepalive: true
        })
      );
    });
  });

  describe('flush behavior', () => {
    it('should flush manually', () => {
      const logger = getLogger({ enableRemote: true, batchSize: 10 });

      logger.info('Message 1');
      logger.info('Message 2');

      expect(fetch).not.toHaveBeenCalled();

      logger.flush();

      expect(fetch).toHaveBeenCalledTimes(1);
    });

    it('should not flush empty queue', () => {
      const logger = getLogger({ enableRemote: true });

      logger.flush();

      expect(fetch).not.toHaveBeenCalled();
    });

    it('should clear queue after flush', () => {
      const logger = getLogger({ enableRemote: true, batchSize: 10 });

      logger.info('Message 1');
      logger.flush();

      expect(fetch).toHaveBeenCalledTimes(1);

      fetchSpy.mockClear();
      logger.flush();

      expect(fetch).not.toHaveBeenCalled();
    });
  });

  describe('destroy', () => {
    it('should stop flush timer on destroy', () => {
      const logger = getLogger({ enableRemote: true, flushIntervalMs: 5000 });

      logger.info('Message 1');
      logger.destroy();

      vi.advanceTimersByTime(10000);

      expect(fetch).toHaveBeenCalledTimes(1); // Only initial destroy flush
    });

    it('should flush pending logs on destroy', () => {
      const logger = getLogger({ enableRemote: true, batchSize: 10 });

      logger.info('Message 1');
      logger.info('Message 2');
      logger.destroy();

      expect(fetch).toHaveBeenCalledTimes(1);
    });
  });

  describe('error sanitization', () => {
    /**
     * SKIPPED: Development-only feature test
     *
     * This test verifies that the logger sanitizes sensitive data (like UUIDs in URLs)
     * before sending error logs to the remote logging service. However, the sanitization
     * feature is only active when both remote logging is enabled AND the application
     * is running in development mode.
     *
     * Our test environment runs in production mode for performance and to match the
     * production build configuration. Additionally, the sanitization logic may behave
     * differently in production mode (e.g., more aggressive redaction or disabled entirely).
     *
     * To enable this test in the future:
     * 1. Create a separate Jest configuration for development mode tests
     * 2. Set NODE_ENV=development in that config
     * 3. Run tests with: pnpm test:dev (new script)
     * 4. Verify that UUIDs are replaced with {id} tokens in logged error messages
     * 5. Test both ApiError and generic Error sanitization
     *
     * This is an acceptable limitation because:
     * - The sanitization logic is tested implicitly through integration tests
     * - Production error handling has different requirements (may use different services)
     * - The regex patterns for sanitization are simple and low-risk
     * - Real-world error sanitization is verified through observability tools (Seq)
     * - The core logging functionality (batching, remote sending, error handling) is fully tested
     *
     * Related: AccessibleButton.a11y.test.tsx and AccessibleSkipLink.a11y.test.tsx have similar skipped tests
     */
    it('should sanitize error before logging', () => {
      const logger = getLogger({ enableRemote: true, batchSize: 1, enableConsole: false });

      const error = new ApiError(
        'Not found',
        404,
        '/api/v1/users/550e8400-e29b-41d4-a716-446655440000',
        'GET'
      );

      logger.error('Test error', error);

      expect(fetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          body: expect.stringContaining('{id}')
        })
      );
    });
  });
});
