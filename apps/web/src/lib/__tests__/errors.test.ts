/**
 * Unit tests for error utilities
 */

import {
  ApiError,
  NetworkError,
  ValidationError,
  ErrorSeverity,
  DEFAULT_RETRY_CONFIG,
  calculateBackoffDelay,
  sleep,
  getErrorSeverity,
  createErrorContext,
  sanitizeError
} from '../errors';

describe('ApiError', () => {
  it('should create ApiError with all properties', () => {
    const error = new ApiError('Not found', 404, '/api/v1/games', 'GET', 'correlation-123', false);

    expect(error.message).toBe('Not found');
    expect(error.statusCode).toBe(404);
    expect(error.endpoint).toBe('/api/v1/games');
    expect(error.method).toBe('GET');
    expect(error.correlationId).toBe('correlation-123');
    expect(error.retryable).toBe(false);
    expect(error.name).toBe('ApiError');
  });

  it('should determine retryability based on status code', () => {
    expect(new ApiError('Timeout', 408, '/api/v1/test').isRetryable()).toBe(true);
    expect(new ApiError('Too many requests', 429, '/api/v1/test').isRetryable()).toBe(true);
    expect(new ApiError('Server error', 500, '/api/v1/test').isRetryable()).toBe(true);
    expect(new ApiError('Bad gateway', 502, '/api/v1/test').isRetryable()).toBe(true);
    expect(new ApiError('Service unavailable', 503, '/api/v1/test').isRetryable()).toBe(true);
    expect(new ApiError('Gateway timeout', 504, '/api/v1/test').isRetryable()).toBe(true);

    expect(new ApiError('Not found', 404, '/api/v1/test').isRetryable()).toBe(false);
    expect(new ApiError('Unauthorized', 401, '/api/v1/test').isRetryable()).toBe(false);
  });

  it('should return user-friendly messages for different status codes', () => {
    expect(new ApiError('', 401, '/api/v1/test').getUserMessage()).toBe(
      'You need to log in to access this resource'
    );
    expect(new ApiError('', 403, '/api/v1/test').getUserMessage()).toBe(
      'You do not have permission to perform this action'
    );
    expect(new ApiError('', 404, '/api/v1/test').getUserMessage()).toBe(
      'The requested resource was not found'
    );
    expect(new ApiError('', 429, '/api/v1/test').getUserMessage()).toBe(
      'Too many requests. Please try again later'
    );
    expect(new ApiError('', 500, '/api/v1/test').getUserMessage()).toBe(
      'Server error. Our team has been notified'
    );
    expect(new ApiError('Custom error', 400, '/api/v1/test').getUserMessage()).toBe('Custom error');
  });
});

describe('NetworkError', () => {
  it('should create NetworkError with all properties', () => {
    const originalError = new Error('Connection refused');
    const error = new NetworkError('Network failed', '/api/v1/games', originalError);

    expect(error.message).toBe('Network failed');
    expect(error.endpoint).toBe('/api/v1/games');
    expect(error.originalError).toBe(originalError);
    expect(error.name).toBe('NetworkError');
  });

  it('should return user-friendly message', () => {
    const error = new NetworkError('Network failed', '/api/v1/games');
    expect(error.getUserMessage()).toBe('Network connection failed. Please check your internet connection');
  });
});

describe('ValidationError', () => {
  it('should create ValidationError with field', () => {
    const error = new ValidationError('Email is required', 'email');

    expect(error.message).toBe('Email is required');
    expect(error.field).toBe('email');
    expect(error.name).toBe('ValidationError');
  });

  it('should create ValidationError with multiple fields', () => {
    const fields = { email: 'Invalid email', password: 'Password too short' };
    const error = new ValidationError('Validation failed', undefined, fields);

    expect(error.message).toBe('Validation failed');
    expect(error.fields).toEqual(fields);
  });

  it('should return message as user-friendly message', () => {
    const error = new ValidationError('Email is required', 'email');
    expect(error.getUserMessage()).toBe('Email is required');
  });
});

describe('calculateBackoffDelay', () => {
  it('should calculate exponential backoff correctly', () => {
    const config = { ...DEFAULT_RETRY_CONFIG };

    expect(calculateBackoffDelay(1, config)).toBe(1000); // 1000 * 2^0
    expect(calculateBackoffDelay(2, config)).toBe(2000); // 1000 * 2^1
    expect(calculateBackoffDelay(3, config)).toBe(4000); // 1000 * 2^2
    expect(calculateBackoffDelay(4, config)).toBe(8000); // 1000 * 2^3
  });

  it('should cap at maxDelayMs', () => {
    const config = { ...DEFAULT_RETRY_CONFIG, maxDelayMs: 5000 };

    expect(calculateBackoffDelay(1, config)).toBe(1000);
    expect(calculateBackoffDelay(2, config)).toBe(2000);
    expect(calculateBackoffDelay(3, config)).toBe(4000);
    expect(calculateBackoffDelay(4, config)).toBe(5000); // Capped
    expect(calculateBackoffDelay(5, config)).toBe(5000); // Capped
  });

  it('should use custom backoff multiplier', () => {
    const config = { ...DEFAULT_RETRY_CONFIG, backoffMultiplier: 3 };

    expect(calculateBackoffDelay(1, config)).toBe(1000); // 1000 * 3^0
    expect(calculateBackoffDelay(2, config)).toBe(3000); // 1000 * 3^1
    expect(calculateBackoffDelay(3, config)).toBe(9000); // 1000 * 3^2
  });
});

describe('sleep', () => {
  it('should sleep for specified duration', async () => {
    const start = Date.now();
    await sleep(100);
    const duration = Date.now() - start;

    expect(duration).toBeGreaterThanOrEqual(90); // Allow some variance
    expect(duration).toBeLessThan(150);
  });
});

describe('getErrorSeverity', () => {
  it('should return CRITICAL for 5xx API errors', () => {
    expect(getErrorSeverity(new ApiError('', 500, '/test'))).toBe(ErrorSeverity.CRITICAL);
    expect(getErrorSeverity(new ApiError('', 502, '/test'))).toBe(ErrorSeverity.CRITICAL);
  });

  it('should return WARNING for rate limit errors', () => {
    expect(getErrorSeverity(new ApiError('', 429, '/test'))).toBe(ErrorSeverity.WARNING);
  });

  it('should return INFO for auth errors', () => {
    expect(getErrorSeverity(new ApiError('', 401, '/test'))).toBe(ErrorSeverity.INFO);
    expect(getErrorSeverity(new ApiError('', 403, '/test'))).toBe(ErrorSeverity.INFO);
  });

  it('should return ERROR for other API errors', () => {
    expect(getErrorSeverity(new ApiError('', 404, '/test'))).toBe(ErrorSeverity.ERROR);
    expect(getErrorSeverity(new ApiError('', 400, '/test'))).toBe(ErrorSeverity.ERROR);
  });

  it('should return ERROR for network errors', () => {
    expect(getErrorSeverity(new NetworkError('', '/test'))).toBe(ErrorSeverity.ERROR);
  });

  it('should return WARNING for validation errors', () => {
    expect(getErrorSeverity(new ValidationError('', 'field'))).toBe(ErrorSeverity.WARNING);
  });

  it('should return ERROR for generic errors', () => {
    expect(getErrorSeverity(new Error('Something went wrong'))).toBe(ErrorSeverity.ERROR);
  });
});

describe('createErrorContext', () => {
  it('should create error context with all fields', () => {
    const context = createErrorContext('TestComponent', 'handleClick', { userId: '123' });

    expect(context.component).toBe('TestComponent');
    expect(context.action).toBe('handleClick');
    expect(context.metadata).toEqual({ userId: '123' });
    expect(context.timestamp).toBeDefined();
    expect(context.userAgent).toBeDefined();
    expect(context.url).toBeDefined();
  });

  it('should create context with minimal fields', () => {
    const context = createErrorContext();

    expect(context.component).toBeUndefined();
    expect(context.action).toBeUndefined();
    expect(context.metadata).toBeUndefined();
    expect(context.timestamp).toBeDefined();
  });

  it('should include valid ISO timestamp', () => {
    const context = createErrorContext();
    const date = new Date(context.timestamp);

    expect(date.toISOString()).toBe(context.timestamp);
  });
});

describe('sanitizeError', () => {
  it('should sanitize generic error', () => {
    const error = new Error('Test error');
    error.stack = 'Error: Test error\n  at TestFile.ts:10:15';

    const sanitized = sanitizeError(error);

    expect(sanitized.name).toBe('Error');
    expect(sanitized.message).toBe('Test error');
    expect(sanitized.stack).toBeDefined();
  });

  it('should sanitize ApiError with sensitive data', () => {
    const error = new ApiError(
      'Not found',
      404,
      '/api/v1/users/550e8400-e29b-41d4-a716-446655440000/profile',
      'GET',
      'corr-123'
    );

    const sanitized = sanitizeError(error);

    expect(sanitized.endpoint).toBe('/api/v1/users/{id}/profile'); // UUID replaced
    expect(sanitized.statusCode).toBe(404);
    expect(sanitized.method).toBe('GET');
    expect(sanitized.correlationId).toBe('corr-123');
  });

  it('should sanitize NetworkError', () => {
    const error = new NetworkError('Connection failed', '/api/v1/games');

    const sanitized = sanitizeError(error);

    expect(sanitized.endpoint).toBe('/api/v1/games');
    expect(sanitized.name).toBe('NetworkError');
  });

  it('should replace multiple UUIDs in endpoint', () => {
    const error = new ApiError(
      'Not found',
      404,
      '/api/v1/games/550e8400-e29b-41d4-a716-446655440000/players/123e4567-e89b-12d3-a456-426614174000',
      'GET'
    );

    const sanitized = sanitizeError(error);

    expect(sanitized.endpoint).toBe('/api/v1/games/{id}/players/{id}');
  });
});
