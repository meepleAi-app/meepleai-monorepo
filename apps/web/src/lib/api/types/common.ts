/**
 * Common API Types (FE-IMP-005)
 *
 * Shared types used across multiple API clients
 */

// Re-export error types for consumer convenience
export type {
  ApiError,
  UnauthorizedError,
  ForbiddenError,
  NotFoundError,
  ValidationError,
  RateLimitError,
  ServerError,
  NetworkError,
  SchemaValidationError,
} from '../core/errors';

// Re-export logger for custom logging needs
export type { Logger, LogContext } from '../core/logger';

// Export getApiBase helper
export { getApiBase } from '../core/httpClient';
