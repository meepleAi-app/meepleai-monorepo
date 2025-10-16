/**
 * Unit tests for logger service
 */

import { getLogger, resetLogger, LogLevel } from '../logger';
import { ApiError } from '../errors';

// Mock fetch
global.fetch = jest.fn(() =>
  Promise.resolve({
    ok: true,
    status: 204,
    json: async () => ({}),
  } as Response)
);

describe('Logger', () => {
  beforeEach(() => {
    resetLogger();
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
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
      const consoleSpy = jest.spyOn(console, 'debug').mockImplementation();
      const logger = getLogger({ enableConsole: true, minLevel: LogLevel.DEBUG });

      logger.debug('Debug message', { component: 'TestComponent' });

      expect(consoleSpy).toHaveBeenCalled();
      consoleSpy.mockRestore();
    });

    it('should log info messages', () => {
      const consoleSpy = jest.spyOn(console, 'info').mockImplementation();
      const logger = getLogger({ enableConsole: true, minLevel: LogLevel.INFO });

      logger.info('Info message');

      expect(consoleSpy).toHaveBeenCalled();
      consoleSpy.mockRestore();
    });

    it('should log warnings', () => {
      const consoleSpy = jest.spyOn(console, 'warn').mockImplementation();
      const logger = getLogger({ enableConsole: true, minLevel: LogLevel.WARN });

      logger.warn('Warning message');

      expect(consoleSpy).toHaveBeenCalled();
      consoleSpy.mockRestore();
    });

    it('should log errors with error object', () => {
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();
      const logger = getLogger({ enableConsole: true });

      const error = new ApiError('Not found', 404, '/api/v1/test');
      logger.error('Error occurred', error);

      expect(consoleSpy).toHaveBeenCalled();
      consoleSpy.mockRestore();
    });

    it('should respect minimum log level', () => {
      const consoleSpy = jest.spyOn(console, 'debug').mockImplementation();
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

      jest.advanceTimersByTime(5000);

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
      (fetch as jest.Mock).mockRejectedValue(new Error('Network error'));
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

      (fetch as jest.Mock).mockClear();
      logger.flush();

      expect(fetch).not.toHaveBeenCalled();
    });
  });

  describe('destroy', () => {
    it('should stop flush timer on destroy', () => {
      const logger = getLogger({ enableRemote: true, flushIntervalMs: 5000 });

      logger.info('Message 1');
      logger.destroy();

      jest.advanceTimersByTime(10000);

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
