/**
 * Error types and utilities for frontend error handling
 */

/**
 * API Error with structured information
 */
export class ApiError extends Error {
  constructor(
    message: string,
    public statusCode: number,
    public endpoint: string,
    public method: string = 'GET',
    public correlationId?: string,
    public retryable: boolean = false
  ) {
    super(message);
    this.name = 'ApiError';
    Object.setPrototypeOf(this, ApiError.prototype);
  }

  /**
   * Determines if this error can be retried
   */
  isRetryable(): boolean {
    return this.retryable || [408, 429, 500, 502, 503, 504].includes(this.statusCode);
  }

  /**
   * Gets user-friendly error message
   */
  getUserMessage(): string {
    if (this.statusCode === 401) {
      return 'You need to log in to access this resource';
    }
    if (this.statusCode === 403) {
      return 'You do not have permission to perform this action';
    }
    if (this.statusCode === 404) {
      return 'The requested resource was not found';
    }
    if (this.statusCode === 429) {
      return 'Too many requests. Please try again later';
    }
    if (this.statusCode >= 500) {
      return 'Server error. Our team has been notified';
    }
    return this.message || 'An unexpected error occurred';
  }
}

/**
 * Network error (connection failure, timeout, etc.)
 */
export class NetworkError extends Error {
  constructor(
    message: string,
    public endpoint: string,
    public originalError?: unknown
  ) {
    super(message);
    this.name = 'NetworkError';
    Object.setPrototypeOf(this, NetworkError.prototype);
  }

  getUserMessage(): string {
    return 'Network connection failed. Please check your internet connection';
  }
}

/**
 * Validation error (client-side validation failure)
 */
export class ValidationError extends Error {
  constructor(
    message: string,
    public field?: string,
    public fields?: Record<string, string>
  ) {
    super(message);
    this.name = 'ValidationError';
    Object.setPrototypeOf(this, ValidationError.prototype);
  }

  getUserMessage(): string {
    return this.message;
  }
}

/**
 * Error severity levels
 */
export enum ErrorSeverity {
  INFO = 'info',
  WARNING = 'warning',
  ERROR = 'error',
  CRITICAL = 'critical'
}

/**
 * Error context for logging
 */
export interface ErrorContext {
  component?: string;
  action?: string;
  userId?: string;
  metadata?: Record<string, unknown>;
  timestamp: string;
  userAgent: string;
  url: string;
}

/**
 * Retry configuration
 */
export interface RetryConfig {
  maxAttempts: number;
  initialDelayMs: number;
  maxDelayMs: number;
  backoffMultiplier: number;
}

/**
 * Default retry configuration
 */
export const DEFAULT_RETRY_CONFIG: RetryConfig = {
  maxAttempts: 3,
  initialDelayMs: 1000,
  maxDelayMs: 10000,
  backoffMultiplier: 2
};

/**
 * Exponential backoff delay calculator
 */
export function calculateBackoffDelay(
  attempt: number,
  config: RetryConfig = DEFAULT_RETRY_CONFIG
): number {
  const delay = config.initialDelayMs * Math.pow(config.backoffMultiplier, attempt - 1);
  return Math.min(delay, config.maxDelayMs);
}

/**
 * Sleep utility for retry delays
 */
export function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/**
 * Determines error severity based on error type and status code
 */
export function getErrorSeverity(error: Error): ErrorSeverity {
  if (error instanceof ApiError) {
    if (error.statusCode >= 500) return ErrorSeverity.CRITICAL;
    if (error.statusCode === 429) return ErrorSeverity.WARNING;
    if (error.statusCode === 401 || error.statusCode === 403) return ErrorSeverity.INFO;
    return ErrorSeverity.ERROR;
  }
  if (error instanceof NetworkError) {
    return ErrorSeverity.ERROR;
  }
  if (error instanceof ValidationError) {
    return ErrorSeverity.WARNING;
  }
  return ErrorSeverity.ERROR;
}

/**
 * Creates error context for logging
 */
export function createErrorContext(
  component?: string,
  action?: string,
  metadata?: Record<string, unknown>
): ErrorContext {
  return {
    component,
    action,
    metadata,
    timestamp: new Date().toISOString(),
    userAgent: typeof navigator !== 'undefined' ? navigator.userAgent : 'unknown',
    url: typeof window !== 'undefined' ? window.location.href : 'unknown'
  };
}

/**
 * Sanitizes error for safe logging (removes sensitive data)
 */
export function sanitizeError(error: Error): Record<string, unknown> {
  const sanitized: Record<string, unknown> = {
    name: error.name,
    message: error.message,
    stack: error.stack
  };

  if (error instanceof ApiError) {
    sanitized.statusCode = error.statusCode;
    sanitized.endpoint = error.endpoint.replace(/\/([\w-]{36})\//g, '/{id}/'); // Replace UUIDs
    sanitized.method = error.method;
    sanitized.correlationId = error.correlationId;
  }

  if (error instanceof NetworkError) {
    sanitized.endpoint = error.endpoint;
  }

  return sanitized;
}
