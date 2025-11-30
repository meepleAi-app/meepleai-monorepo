/**
 * Test helpers for ErrorModal component tests
 * Shared error creation utilities and mock functions
 */

import { ApiError, NetworkError, ValidationError } from '../../lib/errors';

/**
 * Create ApiError with common status codes
 */
export const createApiError = (
  message: string,
  status: number,
  endpoint = '/api/test',
  method = 'GET',
  correlationId?: string
): ApiError => {
  return new ApiError(message, status, endpoint, method, correlationId);
};

/**
 * Create NetworkError
 */
export const createNetworkError = (
  message = 'Connection failed',
  endpoint = '/api/test'
): NetworkError => {
  return new NetworkError(message, endpoint);
};

/**
 * Create ValidationError
 */
export const createValidationError = (
  message: string,
  field?: string
): ValidationError => {
  return new ValidationError(message, field);
};

/**
 * HTTP status codes for testing
 */
export const HTTP_STATUS = {
  UNAUTHORIZED: 401,
  FORBIDDEN: 403,
  NOT_FOUND: 404,
  REQUEST_TIMEOUT: 408,
  TOO_MANY_REQUESTS: 429,
  INTERNAL_SERVER_ERROR: 500,
  BAD_REQUEST: 400
} as const;

/**
 * Expected user messages for different error types
 */
export const USER_MESSAGES = {
  UNAUTHORIZED: 'You need to log in to access this resource',
  FORBIDDEN: 'You do not have permission to perform this action',
  NOT_FOUND: 'The requested resource was not found',
  TOO_MANY_REQUESTS: 'Too many requests. Please try again later',
  SERVER_ERROR: 'Server error. Our team has been notified',
  NETWORK_ERROR: 'Network connection failed. Please check your internet connection',
  GENERIC_ERROR: 'An unexpected error occurred'
} as const;

/**
 * Check if error is retryable based on status code
 */
export const isRetryableError = (error: ApiError): boolean => {
  const retryableStatuses = [408, 429, 500, 502, 503, 504];
  return retryableStatuses.includes(error.status);
};

/**
 * Create mock callback functions
 */
export const createMockCallbacks = () => ({
  onClose: vi.fn(),
  onRetry: vi.fn()
});

/**
 * Default modal props for testing
 */
export const defaultModalProps = {
  isOpen: true,
  onClose: vi.fn()
};
