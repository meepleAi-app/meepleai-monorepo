/**
 * API Error Handling (FE-IMP-005)
 *
 * Centralized error classes with correlationId support for distributed tracing.
 * Maps backend HTTP status codes to frontend error types.
 */

import { parseRetryAfter } from './retryPolicy';

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
 * 409 Conflict - Request conflicts with current state of resource
 * Used for business rule violations, state conflicts, duplicate resources
 */
export class ConflictError extends ApiError {
  constructor(details: Omit<ApiErrorDetails, 'statusCode'>) {
    super({ ...details, statusCode: 409 });
    this.name = 'ConflictError';
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
 *
 * Includes helper methods for displaying countdown timers and user-friendly messages.
 */
export class RateLimitError extends ApiError {
  public readonly retryAfter?: number;

  constructor(details: Omit<ApiErrorDetails, 'statusCode'> & { retryAfter?: number }) {
    super({ ...details, statusCode: 429 });
    this.name = 'RateLimitError';
    this.retryAfter = details.retryAfter;
  }

  /**
   * Get the number of seconds until retry is allowed
   */
  public getRetryAfterSeconds(): number {
    return this.retryAfter || 0;
  }

  /**
   * Get the timestamp when retry is allowed
   */
  public getRetryAfterDate(): Date {
    const now = new Date(this.timestamp);
    const retryAfterMs = (this.retryAfter || 0) * 1000;
    return new Date(now.getTime() + retryAfterMs);
  }

  /**
   * Get a user-friendly message with countdown
   * @param remainingSeconds Optional override for countdown display
   */
  public getUserFriendlyMessage(remainingSeconds?: number): string {
    const seconds = remainingSeconds ?? this.getRetryAfterSeconds();

    if (seconds <= 0) {
      return 'You can now retry your request.';
    }

    if (seconds === 1) {
      return 'Too many requests. Please wait 1 second.';
    }

    if (seconds < 60) {
      return `Too many requests. Please wait ${seconds} seconds.`;
    }

    const minutes = Math.ceil(seconds / 60);
    if (minutes === 1) {
      return 'Too many requests. Please wait 1 minute.';
    }

    return `Too many requests. Please wait ${minutes} minutes.`;
  }

  /**
   * Check if retry is currently allowed based on elapsed time
   */
  public canRetryNow(): boolean {
    const now = new Date();
    const retryDate = this.getRetryAfterDate();
    return now >= retryDate;
  }

  /**
   * Get remaining seconds until retry is allowed
   */
  public getRemainingSeconds(): number {
    const now = new Date();
    const retryDate = this.getRetryAfterDate();
    const diffMs = retryDate.getTime() - now.getTime();
    return Math.max(0, Math.ceil(diffMs / 1000));
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

  constructor(details: Omit<ApiErrorDetails, 'statusCode'> & { zodError: unknown }) {
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
 * Checks whether an error represents a 404 Not Found response.
 * Works with ApiError (statusCode), plain Error with embedded status,
 * and raw fetch errors with response.status.
 */
export function isNotFoundError(error: unknown): boolean {
  if (!error) return false;
  if (error instanceof ApiError) return error.statusCode === 404;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const err = error as any;
  if (err?.statusCode === 404 || err?.status === 404) return true;
  if (err?.response?.status === 404) return true;
  if (typeof err?.message === 'string' && err.message.includes('404')) return true;
  return false;
}

/**
 * Creates appropriate error instance from HTTP response
 */
export async function createApiError(endpoint: string, response: Response): Promise<ApiError> {
  const correlationId = response.headers.get('X-Correlation-Id') || undefined;
  const statusCode = response.status;

  // Extract error message from response body
  let message = `API ${endpoint} failed with status ${statusCode}`;
  let validationErrors: Record<string, string[]> | undefined;

  try {
    const body = await response.json();
    // Prefer body.message (user-friendly message) over body.error (error type)
    if (body?.message) {
      message = body.message;
    } else if (body?.error) {
      message = body.error;
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
    case 409:
      return new ConflictError(baseDetails);
    case 422:
      return new ValidationError({ ...baseDetails, validationErrors });
    case 429: {
      const retryAfterHeader = response.headers.get('Retry-After');
      const retryAfterMs = parseRetryAfter(retryAfterHeader);
      const retryAfter = retryAfterMs != null ? Math.ceil(retryAfterMs / 1000) : undefined;
      return new RateLimitError({
        ...baseDetails,
        retryAfter,
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
