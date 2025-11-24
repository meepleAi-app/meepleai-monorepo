/**
 * Logger Tests (FE-IMP-005)
 *
 * Tests for console and Seq logger implementations.
 */

import { logger, logApiError } from '../core/logger';
import { ApiError, UnauthorizedError } from '../core/errors';

describe('Logger', () => {
  let originalEnv: NodeJS.ProcessEnv;
  let consoleErrorSpy: jest.SpyInstance;
  let consoleWarnSpy: jest.SpyInstance;
  let consoleInfoSpy: jest.SpyInstance;
  let consoleDebugSpy: jest.SpyInstance;

  beforeEach(() => {
    originalEnv = process.env;
    consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation();
    consoleWarnSpy = vi.spyOn(console, 'warn').mockImplementation();
    consoleInfoSpy = vi.spyOn(console, 'info').mockImplementation();
    consoleDebugSpy = vi.spyOn(console, 'debug').mockImplementation();
  });

  afterEach(() => {
    process.env = originalEnv;
    consoleErrorSpy.mockRestore();
    consoleWarnSpy.mockRestore();
    consoleInfoSpy.mockRestore();
    consoleDebugSpy.mockRestore();
  });

  describe('error logging', () => {
    it('should log errors to console', () => {
      const error = new Error('Test error');
      logger.error('Error occurred', error);

      expect(consoleErrorSpy).toHaveBeenCalledWith(
        '[API Error]',
        'Error occurred',
        expect.objectContaining({
          error: expect.objectContaining({
            name: 'Error',
            message: 'Test error',
          }),
        })
      );
    });

    it('should log API errors with context', () => {
      const error = new ApiError({
        message: 'API failed',
        statusCode: 500,
        correlationId: 'test-123',
      });

      logger.error('API Error', error, { endpoint: '/api/v1/test' });

      expect(consoleErrorSpy).toHaveBeenCalledWith(
        '[API Error]',
        'API Error',
        expect.objectContaining({
          error: expect.objectContaining({
            name: 'ApiError',
            message: 'API failed',
            statusCode: 500,
            correlationId: 'test-123',
          }),
          context: expect.objectContaining({
            endpoint: '/api/v1/test',
          }),
        })
      );
    });

    it('should serialize ApiError to JSON', () => {
      const error = new UnauthorizedError({
        message: 'Not authorized',
        correlationId: 'test-401',
      });

      logger.error('Auth error', error);

      expect(consoleErrorSpy).toHaveBeenCalledWith(
        '[API Error]',
        'Auth error',
        expect.objectContaining({
          error: expect.objectContaining({
            name: 'UnauthorizedError',
            message: 'Not authorized',
            statusCode: 401,
            correlationId: 'test-401',
          }),
        })
      );
    });
  });

  describe('warning logging', () => {
    it('should log warnings to console', () => {
      logger.warn('Warning message', { key: 'value' });

      expect(consoleWarnSpy).toHaveBeenCalledWith('[API Warning]', 'Warning message', {
        key: 'value',
      });
    });
  });

  describe('info logging', () => {
    it('should log info to console', () => {
      logger.info('Info message', { userId: '123' });

      expect(consoleInfoSpy).toHaveBeenCalledWith('[API Info]', 'Info message', { userId: '123' });
    });
  });

  describe('debug logging', () => {
    afterEach(() => {
      vi.unstubAllEnvs();
    });

    it('should log debug in development', () => {
      vi.stubEnv('NODE_ENV', 'development');

      logger.debug('Debug message', { debug: true });

      expect(consoleDebugSpy).toHaveBeenCalledWith('[API Debug]', 'Debug message', { debug: true });
    });

    it('should not log debug in production', () => {
      vi.stubEnv('NODE_ENV', 'production');

      logger.debug('Debug message', { debug: true });

      // Should not call console.debug in production
      expect(consoleDebugSpy).not.toHaveBeenCalled();
    });
  });
});

describe('logApiError', () => {
  let consoleErrorSpy: jest.SpyInstance;

  beforeEach(() => {
    consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation();
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
  });

  it('should log API error with extracted context', () => {
    const error = new ApiError({
      message: 'Request failed',
      statusCode: 500,
      correlationId: 'test-correlation-123',
      endpoint: '/api/v1/games',
    });

    logApiError(error);

    expect(consoleErrorSpy).toHaveBeenCalledWith(
      '[API Error]',
      'API request failed: Request failed',
      expect.objectContaining({
        error: expect.any(Object),
        context: expect.objectContaining({
          correlationId: 'test-correlation-123',
          endpoint: '/api/v1/games',
          statusCode: 500,
        }),
      })
    );
  });

  it('should include additional context', () => {
    const error = new ApiError({
      message: 'Request failed',
      statusCode: 404,
      correlationId: 'test-404',
      endpoint: '/api/v1/games/123',
    });

    logApiError(error, { userId: '990e8400-e29b-41d4-a716-000000000456' });

    expect(consoleErrorSpy).toHaveBeenCalledWith(
      '[API Error]',
      'API request failed: Request failed',
      expect.objectContaining({
        error: expect.any(Object),
        context: expect.objectContaining({
          correlationId: 'test-404',
          endpoint: '/api/v1/games/123',
          statusCode: 404,
          userId: '990e8400-e29b-41d4-a716-000000000456',
        }),
      })
    );
  });

  it('should handle errors without correlation ID', () => {
    const error = new ApiError({
      message: 'Request failed',
      statusCode: 500,
      endpoint: '/api/v1/test',
    });

    logApiError(error);

    expect(consoleErrorSpy).toHaveBeenCalledWith(
      '[API Error]',
      'API request failed: Request failed',
      expect.objectContaining({
        error: expect.any(Object),
        context: expect.objectContaining({
          correlationId: undefined,
          endpoint: '/api/v1/test',
          statusCode: 500,
        }),
      })
    );
  });
});
