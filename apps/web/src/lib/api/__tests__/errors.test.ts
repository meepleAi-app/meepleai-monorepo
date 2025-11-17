/**
 * Error Classes Tests (FE-IMP-005)
 *
 * Tests for API error handling with correlation ID support.
 */

import {
  ApiError,
  UnauthorizedError,
  ForbiddenError,
  NotFoundError,
  ValidationError,
  RateLimitError,
  ServerError,
  NetworkError,
  SchemaValidationError,
  createApiError,
} from '../core/errors';

describe('ApiError', () => {
  it('should create basic API error with message', () => {
    const error = new ApiError({ message: 'Test error' });

    expect(error.name).toBe('ApiError');
    expect(error.message).toBe('Test error');
    expect(error.timestamp).toBeDefined();
  });

  it('should include correlation ID', () => {
    const error = new ApiError({
      message: 'Test error',
      correlationId: 'test-correlation-123',
    });

    expect(error.correlationId).toBe('test-correlation-123');
  });

  it('should include status code', () => {
    const error = new ApiError({
      message: 'Test error',
      statusCode: 500,
    });

    expect(error.statusCode).toBe(500);
  });

  it('should include endpoint', () => {
    const error = new ApiError({
      message: 'Test error',
      endpoint: '/api/v1/test',
    });

    expect(error.endpoint).toBe('/api/v1/test');
  });

  it('should serialize to JSON with all fields', () => {
    const error = new ApiError({
      message: 'Test error',
      statusCode: 500,
      correlationId: 'test-123',
      endpoint: '/api/v1/test',
    });

    const json = error.toJSON();

    expect(json.name).toBe('ApiError');
    expect(json.message).toBe('Test error');
    expect(json.statusCode).toBe(500);
    expect(json.correlationId).toBe('test-123');
    expect(json.endpoint).toBe('/api/v1/test');
    expect(json.timestamp).toBeDefined();
    expect(json.stack).toBeDefined();
  });

  it('should maintain proper stack trace', () => {
    const error = new ApiError({ message: 'Test error' });

    expect(error.stack).toBeDefined();
    expect(error.stack).toContain('ApiError');
  });
});

describe('UnauthorizedError', () => {
  it('should create 401 error', () => {
    const error = new UnauthorizedError({ message: 'Not authenticated' });

    expect(error.name).toBe('UnauthorizedError');
    expect(error.statusCode).toBe(401);
    expect(error.message).toBe('Not authenticated');
  });

  it('should include correlation ID', () => {
    const error = new UnauthorizedError({
      message: 'Not authenticated',
      correlationId: 'test-401',
    });

    expect(error.correlationId).toBe('test-401');
  });
});

describe('ForbiddenError', () => {
  it('should create 403 error', () => {
    const error = new ForbiddenError({ message: 'Insufficient permissions' });

    expect(error.name).toBe('ForbiddenError');
    expect(error.statusCode).toBe(403);
    expect(error.message).toBe('Insufficient permissions');
  });
});

describe('NotFoundError', () => {
  it('should create 404 error', () => {
    const error = new NotFoundError({ message: 'Resource not found' });

    expect(error.name).toBe('NotFoundError');
    expect(error.statusCode).toBe(404);
    expect(error.message).toBe('Resource not found');
  });
});

describe('ValidationError', () => {
  it('should create 422 error', () => {
    const error = new ValidationError({ message: 'Validation failed' });

    expect(error.name).toBe('ValidationError');
    expect(error.statusCode).toBe(422);
    expect(error.message).toBe('Validation failed');
  });

  it('should include validation errors', () => {
    const validationErrors = {
      email: ['Invalid email format'],
      password: ['Password too short', 'Password requires uppercase'],
    };

    const error = new ValidationError({
      message: 'Validation failed',
      validationErrors,
    });

    expect(error.validationErrors).toEqual(validationErrors);
  });

  it('should serialize validation errors to JSON', () => {
    const validationErrors = {
      email: ['Invalid email format'],
    };

    const error = new ValidationError({
      message: 'Validation failed',
      validationErrors,
    });

    const json = error.toJSON();

    expect(json.validationErrors).toEqual(validationErrors);
  });
});

describe('RateLimitError', () => {
  it('should create 429 error', () => {
    const error = new RateLimitError({ message: 'Too many requests' });

    expect(error.name).toBe('RateLimitError');
    expect(error.statusCode).toBe(429);
    expect(error.message).toBe('Too many requests');
  });

  it('should include retry after value', () => {
    const error = new RateLimitError({
      message: 'Too many requests',
      retryAfter: 60,
    });

    expect(error.retryAfter).toBe(60);
  });

  it('should serialize retry after to JSON', () => {
    const error = new RateLimitError({
      message: 'Too many requests',
      retryAfter: 120,
    });

    const json = error.toJSON();

    expect(json.retryAfter).toBe(120);
  });
});

describe('ServerError', () => {
  it('should create 500 error by default', () => {
    const error = new ServerError({ message: 'Internal server error' });

    expect(error.name).toBe('ServerError');
    expect(error.statusCode).toBe(500);
    expect(error.message).toBe('Internal server error');
  });

  it('should accept custom status code (502)', () => {
    const error = new ServerError({
      message: 'Bad gateway',
      statusCode: 502,
    });

    expect(error.statusCode).toBe(502);
  });

  it('should accept custom status code (503)', () => {
    const error = new ServerError({
      message: 'Service unavailable',
      statusCode: 503,
    });

    expect(error.statusCode).toBe(503);
  });
});

describe('NetworkError', () => {
  it('should create network error with status code 0', () => {
    const error = new NetworkError({ message: 'Network request failed' });

    expect(error.name).toBe('NetworkError');
    expect(error.statusCode).toBe(0);
    expect(error.message).toBe('Network request failed');
  });
});

describe('SchemaValidationError', () => {
  it('should create schema validation error', () => {
    const zodError = { issues: [{ path: ['email'], message: 'Invalid email' }] };

    const error = new SchemaValidationError({
      message: 'Invalid response format',
      zodError,
    });

    expect(error.name).toBe('SchemaValidationError');
    expect(error.statusCode).toBe(500);
    expect(error.message).toContain('Schema validation failed');
    expect(error.zodError).toEqual(zodError);
  });

  it('should serialize zod error to JSON', () => {
    const zodError = { issues: [{ path: ['email'], message: 'Invalid email' }] };

    const error = new SchemaValidationError({
      message: 'Invalid response format',
      zodError,
    });

    const json = error.toJSON();

    expect(json.zodError).toEqual(zodError);
  });
});

describe('createApiError', () => {
  it('should create UnauthorizedError for 401', async () => {
    const response = new Response(JSON.stringify({ error: 'Unauthorized' }), {
      status: 401,
      headers: { 'X-Correlation-Id': 'test-401' },
    });

    const error = await createApiError('/api/v1/test', response);

    expect(error).toBeInstanceOf(UnauthorizedError);
    expect(error.statusCode).toBe(401);
    expect(error.message).toBe('Unauthorized');
    expect(error.correlationId).toBe('test-401');
    expect(error.endpoint).toBe('/api/v1/test');
  });

  it('should create ForbiddenError for 403', async () => {
    const response = new Response(JSON.stringify({ message: 'Forbidden' }), {
      status: 403,
    });

    const error = await createApiError('/api/v1/test', response);

    expect(error).toBeInstanceOf(ForbiddenError);
    expect(error.statusCode).toBe(403);
    expect(error.message).toBe('Forbidden');
  });

  it('should create NotFoundError for 404', async () => {
    const response = new Response(JSON.stringify({ error: 'Not found' }), {
      status: 404,
    });

    const error = await createApiError('/api/v1/test', response);

    expect(error).toBeInstanceOf(NotFoundError);
    expect(error.statusCode).toBe(404);
  });

  it('should create ValidationError for 422 with validation errors', async () => {
    const validationErrors = {
      email: ['Invalid email'],
      password: ['Too short'],
    };

    const response = new Response(
      JSON.stringify({
        message: 'Validation failed',
        errors: validationErrors,
      }),
      { status: 422 }
    );

    const error = await createApiError('/api/v1/test', response);

    expect(error).toBeInstanceOf(ValidationError);
    expect(error.statusCode).toBe(422);
    expect((error as ValidationError).validationErrors).toEqual(validationErrors);
  });

  it('should create RateLimitError for 429 with retry after', async () => {
    const response = new Response(JSON.stringify({ error: 'Rate limit exceeded' }), {
      status: 429,
      headers: { 'Retry-After': '60' },
    });

    const error = await createApiError('/api/v1/test', response);

    expect(error).toBeInstanceOf(RateLimitError);
    expect(error.statusCode).toBe(429);
    expect((error as RateLimitError).retryAfter).toBe(60);
  });

  it('should create ServerError for 500', async () => {
    const response = new Response(JSON.stringify({ error: 'Internal error' }), {
      status: 500,
    });

    const error = await createApiError('/api/v1/test', response);

    expect(error).toBeInstanceOf(ServerError);
    expect(error.statusCode).toBe(500);
  });

  it('should create ServerError for 502', async () => {
    const response = new Response(JSON.stringify({ error: 'Bad gateway' }), {
      status: 502,
    });

    const error = await createApiError('/api/v1/test', response);

    expect(error).toBeInstanceOf(ServerError);
    expect(error.statusCode).toBe(502);
  });

  it('should create ServerError for 503', async () => {
    const response = new Response(JSON.stringify({ error: 'Service unavailable' }), {
      status: 503,
    });

    const error = await createApiError('/api/v1/test', response);

    expect(error).toBeInstanceOf(ServerError);
    expect(error.statusCode).toBe(503);
  });

  it('should create generic ApiError for unknown status codes', async () => {
    const response = new Response(JSON.stringify({ error: 'Unknown error' }), {
      status: 418,
    });

    const error = await createApiError('/api/v1/test', response);

    expect(error).toBeInstanceOf(ApiError);
    expect(error.statusCode).toBe(418);
  });

  it('should use default message when JSON parsing fails', async () => {
    const response = new Response('Invalid JSON', { status: 500 });

    const error = await createApiError('/api/v1/test', response);

    expect(error.message).toBe('API /api/v1/test failed with status 500');
  });

  it('should extract correlation ID from response headers', async () => {
    const response = new Response(JSON.stringify({ error: 'Test' }), {
      status: 500,
      headers: { 'X-Correlation-Id': 'test-correlation-abc' },
    });

    const error = await createApiError('/api/v1/test', response);

    expect(error.correlationId).toBe('test-correlation-abc');
  });

  it('should handle missing correlation ID gracefully', async () => {
    const response = new Response(JSON.stringify({ error: 'Test' }), {
      status: 500,
    });

    const error = await createApiError('/api/v1/test', response);

    expect(error.correlationId).toBeUndefined();
  });
});
