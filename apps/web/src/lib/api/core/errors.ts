/**
 * API Error Handling (FE-IMP-005)
 *
 * Centralized error classes with correlationId support for distributed tracing.
 * Maps backend HTTP status codes to frontend error types.
 */

export interface ApiErrorDetails {
  message: string;
  statusCode?: number;
  correlationId?: string;
  response?: Response;
  endpoint?: string;
  timestamp?: string;
}

/**
 * Base API error class with correlation ID and distributed tracing support
 */
export class ApiError extends Error {
  public readonly statusCode?: number;
  public readonly correlationId?: string;
  public readonly response?: Response;
  public readonly endpoint?: string;
  public readonly timestamp: string;

  constructor(details: ApiErrorDetails) {
    super(details.message);
    this.name = 'ApiError';
    this.statusCode = details.statusCode;
    this.correlationId = details.correlationId;
    this.response = details.response;
    this.endpoint = details.endpoint;
    this.timestamp = details.timestamp || new Date().toISOString();

    // Maintains proper stack trace for where error was thrown
    if (Error.captureStackTrace) {
      Error.captureStackTrace(this, this.constructor);
    }
  }

  /**
   * Serialize error for logging to Seq or other observability platforms
   */
  public toJSON(): Record<string, unknown> {
    return {
      name: this.name,
      message: this.message,
      statusCode: this.statusCode,
      correlationId: this.correlationId,
      endpoint: this.endpoint,
      timestamp: this.timestamp,
      stack: this.stack,
    };
  }
}

/**
 * 401 Unauthorized - Authentication required or session expired
 */
export class UnauthorizedError extends ApiError {
  constructor(details: Omit<ApiErrorDetails, 'statusCode'>) {
    super({ ...details, statusCode: 401 });
    this.name = 'UnauthorizedError';
  }
}

/**
 * 403 Forbidden - Authenticated but lacks permission
 */
export class ForbiddenError extends ApiError {
  constructor(details: Omit<ApiErrorDetails, 'statusCode'>) {
    super({ ...details, statusCode: 403 });
    this.name = 'ForbiddenError';
  }
}

/**
 * 404 Not Found - Resource does not exist
 */
export class NotFoundError extends ApiError {
  constructor(details: Omit<ApiErrorDetails, 'statusCode'>) {
    super({ ...details, statusCode: 404 });
    this.name = 'NotFoundError';
  }
}

/**
 * 422 Validation Error - Request validation failed
 */
export class ValidationError extends ApiError {
  public readonly validationErrors?: Record<string, string[]>;

  constructor(
    details: Omit<ApiErrorDetails, 'statusCode'> & {
      validationErrors?: Record<string, string[]>;
    }
  ) {
    super({ ...details, statusCode: 422 });
    this.name = 'ValidationError';
    this.validationErrors = details.validationErrors;
  }

  public override toJSON(): Record<string, unknown> {
    return {
      ...super.toJSON(),
      validationErrors: this.validationErrors,
    };
  }
}

/**
 * 429 Rate Limit - Too many requests
 */
export class RateLimitError extends ApiError {
  public readonly retryAfter?: number;

  constructor(
    details: Omit<ApiErrorDetails, 'statusCode'> & { retryAfter?: number }
  ) {
    super({ ...details, statusCode: 429 });
    this.name = 'RateLimitError';
    this.retryAfter = details.retryAfter;
  }

  public override toJSON(): Record<string, unknown> {
    return {
      ...super.toJSON(),
      retryAfter: this.retryAfter,
    };
  }
}

/**
 * 500/502/503 Server Error - Backend internal error
 */
export class ServerError extends ApiError {
  constructor(details: Omit<ApiErrorDetails, 'statusCode'> & { statusCode?: number }) {
    super({ ...details, statusCode: details.statusCode || 500 });
    this.name = 'ServerError';
  }
}

/**
 * Network Error - Request failed before reaching server
 */
export class NetworkError extends ApiError {
  constructor(details: Omit<ApiErrorDetails, 'statusCode'>) {
    super({ ...details, statusCode: 0 });
    this.name = 'NetworkError';
  }
}

/**
 * Validation Schema Error - Zod validation failed on API response
 */
export class SchemaValidationError extends ApiError {
  public readonly zodError: unknown;

  constructor(
    details: Omit<ApiErrorDetails, 'statusCode'> & { zodError: unknown }
  ) {
    super({ ...details, statusCode: 500, message: `Schema validation failed: ${details.message}` });
    this.name = 'SchemaValidationError';
    this.zodError = details.zodError;
  }

  public override toJSON(): Record<string, unknown> {
    return {
      ...super.toJSON(),
      zodError: this.zodError,
    };
  }
}

/**
 * Creates appropriate error instance from HTTP response
 */
export async function createApiError(
  endpoint: string,
  response: Response
): Promise<ApiError> {
  const correlationId = response.headers.get('X-Correlation-Id') || undefined;
  const statusCode = response.status;

  // Extract error message from response body
  let message = `API ${endpoint} failed with status ${statusCode}`;
  let validationErrors: Record<string, string[]> | undefined;

  try {
    const body = await response.json();
    if (body?.error) {
      message = body.error;
    } else if (body?.message) {
      message = body.message;
    }

    // Extract validation errors if present (422 response)
    if (statusCode === 422 && body?.errors) {
      validationErrors = body.errors;
    }
  } catch {
    // If JSON parsing fails, use default message
  }

  const baseDetails = {
    message,
    correlationId,
    response,
    endpoint,
  };

  // Map status codes to specific error types
  switch (statusCode) {
    case 401:
      return new UnauthorizedError(baseDetails);
    case 403:
      return new ForbiddenError(baseDetails);
    case 404:
      return new NotFoundError(baseDetails);
    case 422:
      return new ValidationError({ ...baseDetails, validationErrors });
    case 429: {
      const retryAfter = response.headers.get('Retry-After');
      return new RateLimitError({
        ...baseDetails,
        retryAfter: retryAfter ? parseInt(retryAfter, 10) : undefined,
      });
    }
    case 500:
    case 502:
    case 503:
      return new ServerError({ ...baseDetails, statusCode });
    default:
      return new ApiError({ ...baseDetails, statusCode });
  }
}
