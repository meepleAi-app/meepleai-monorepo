/**
 * Retry Policy with Exponential Backoff (Issue #1453)
 *
 * Implements automatic retry logic for transient HTTP errors with exponential
 * backoff and jitter to prevent thundering herd problems.
 *
 * Retryable errors:
 * - 500 Internal Server Error
 * - 502 Bad Gateway
 * - 503 Service Unavailable
 * - Network errors (fetch failures)
 *
 * Non-retryable errors:
 * - 4xx client errors (except timeouts)
 * - 401/403 authentication errors
 * - 429 rate limits (handled separately)
 */

import { logApiError } from './logger';
import { ApiError, NetworkError, ServerError } from './errors';
import { API_CONFIG } from '@/config';

export interface RetryConfig {
  /** Maximum number of retry attempts (default: 3) */
  maxAttempts: number;
  /** Base delay in milliseconds (default: 1000ms) */
  baseDelay: number;
  /** Maximum delay in milliseconds (default: 10000ms) */
  maxDelay: number;
  /** Enable/disable retry logic (default: true) */
  enabled: boolean;
  /** Jitter percentage (0-1, default: 0.3 for 30%) */
  jitter: number;
}

/**
 * Get retry configuration from environment variables with fallback defaults
 */
export function getRetryConfig(): RetryConfig {
  // Check if running in browser environment
  const isBrowser = typeof window !== 'undefined';

  return {
    maxAttempts: isBrowser
      ? parseInt(process.env.NEXT_PUBLIC_RETRY_MAX_ATTEMPTS || String(API_CONFIG.RETRY_MAX_ATTEMPTS), 10)
      : API_CONFIG.RETRY_MAX_ATTEMPTS,
    baseDelay: isBrowser
      ? parseInt(process.env.NEXT_PUBLIC_RETRY_BASE_DELAY || String(API_CONFIG.RETRY_BASE_DELAY_MS), 10)
      : API_CONFIG.RETRY_BASE_DELAY_MS,
    maxDelay: isBrowser
      ? parseInt(process.env.NEXT_PUBLIC_RETRY_MAX_DELAY || String(API_CONFIG.RETRY_MAX_DELAY_MS), 10)
      : API_CONFIG.RETRY_MAX_DELAY_MS,
    enabled: isBrowser
      ? process.env.NEXT_PUBLIC_RETRY_ENABLED !== 'false'
      : true,
    jitter: API_CONFIG.RETRY_JITTER,
  };
}

/**
 * Determine if an error is retryable
 */
export function isRetryableError(error: unknown): boolean {
  // Network errors are retryable
  if (error instanceof NetworkError) {
    return true;
  }

  // Server errors (5xx) are retryable
  if (error instanceof ServerError) {
    const statusCode = error.statusCode;
    return statusCode === 500 || statusCode === 502 || statusCode === 503;
  }

  // Generic fetch errors are retryable
  if (error instanceof TypeError && error.message.includes('fetch')) {
    return true;
  }

  // All other errors are not retryable (4xx, auth errors, etc.)
  return false;
}

/**
 * Parse Retry-After header value (supports both seconds and HTTP-date formats)
 */
export function parseRetryAfter(retryAfterValue: string | null | undefined): number | undefined {
  if (!retryAfterValue) {
    return undefined;
  }

  // Try parsing as number of seconds
  const seconds = parseInt(retryAfterValue, 10);
  if (!isNaN(seconds) && seconds > 0) {
    return seconds * 1000; // Convert to milliseconds
  }

  // Try parsing as HTTP-date (RFC 7231)
  try {
    const retryDate = new Date(retryAfterValue);
    if (!isNaN(retryDate.getTime())) {
      const now = new Date();
      const diffMs = retryDate.getTime() - now.getTime();
      return Math.max(0, diffMs);
    }
  } catch {
    // Invalid date format
  }

  return undefined;
}

/**
 * Calculate delay with exponential backoff and jitter
 *
 * Formula: min(maxDelay, baseDelay * 2^attempt) * (1 ± jitter)
 *
 * Examples (baseDelay=1000ms, jitter=30%):
 * - Attempt 0: 1000ms * (1 ± 0.3) = 700-1300ms
 * - Attempt 1: 2000ms * (1 ± 0.3) = 1400-2600ms
 * - Attempt 2: 4000ms * (1 ± 0.3) = 2800-5200ms
 *
 * Supports adaptive backoff with server-provided Retry-After header.
 */
export function calculateBackoffDelay(
  attempt: number,
  config: RetryConfig,
  retryAfterMs?: number
): number {
  // If server provided Retry-After, use it (with jitter)
  if (retryAfterMs !== undefined && retryAfterMs > 0) {
    const jitterFactor = 1 + (Math.random() * 2 - 1) * config.jitter;
    const adaptiveDelay = Math.min(retryAfterMs, config.maxDelay) * jitterFactor;
    return Math.floor(adaptiveDelay);
  }

  // Exponential backoff: baseDelay * 2^attempt
  const exponentialDelay = config.baseDelay * Math.pow(2, attempt);

  // Cap at maxDelay
  const cappedDelay = Math.min(exponentialDelay, config.maxDelay);

  // Add jitter: random value between (1 - jitter) and (1 + jitter)
  const jitterFactor = 1 + (Math.random() * 2 - 1) * config.jitter;
  const delayWithJitter = cappedDelay * jitterFactor;

  return Math.floor(delayWithJitter);
}

/**
 * Sleep for specified milliseconds
 */
export async function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/**
 * Retry options for per-request configuration
 */
export interface RetryOptions {
  /** Override global retry config for this request */
  retryConfig?: Partial<RetryConfig>;
  /** Disable retry for this specific request */
  skipRetry?: boolean;
  /** Callback invoked before each retry attempt */
  onRetry?: (attempt: number, error: unknown, delayMs: number) => void;
}

/**
 * Execute a function with automatic retry on transient errors
 *
 * @param fn The async function to execute
 * @param options Retry configuration options
 * @returns The result of the function
 * @throws The last error if all retries are exhausted
 */
export async function withRetry<T>(
  fn: () => Promise<T>,
  options: RetryOptions = {}
): Promise<T> {
  const config: RetryConfig = {
    ...getRetryConfig(),
    ...options.retryConfig,
  };

  // If retry is disabled globally or for this request, execute once
  if (!config.enabled || options.skipRetry) {
    return fn();
  }

  let lastError: unknown;
  let attempt = 0;

  while (attempt <= config.maxAttempts) {
    try {
      // First attempt (attempt === 0) is not a retry
      return await fn();
    } catch (error) {
      lastError = error;

      // Check if we should retry this error
      if (!isRetryableError(error)) {
        // Log non-retryable error and throw immediately
        if (error instanceof ApiError) {
          logApiError(error);
        }
        throw error;
      }

      // Check if we have retries left
      if (attempt >= config.maxAttempts) {
        // All retries exhausted, log and throw
        if (error instanceof ApiError) {
          logApiError(error);
        }
        throw error;
      }

      // Extract Retry-After header from server response (if available)
      let retryAfterMs: number | undefined;
      if (error instanceof ApiError && error.response) {
        const retryAfterHeader = error.response.headers.get('Retry-After');
        retryAfterMs = parseRetryAfter(retryAfterHeader);
      }

      // Calculate backoff delay for this retry attempt
      // Honor server-provided Retry-After if available (adaptive backoff)
      const delayMs = calculateBackoffDelay(attempt, config, retryAfterMs);

      // Log retry attempt
      if (error instanceof ApiError) {
        const backoffType = retryAfterMs ? 'server Retry-After' : 'exponential backoff';
        console.warn(
          `[Retry] Attempt ${attempt + 1}/${config.maxAttempts} failed for ${error.endpoint || 'unknown'}. ` +
          `Retrying in ${delayMs}ms (${backoffType})... (Status: ${error.statusCode || 'network error'}, ` +
          `CorrelationId: ${error.correlationId || 'none'})`
        );
      } else {
        console.warn(
          `[Retry] Attempt ${attempt + 1}/${config.maxAttempts} failed. ` +
          `Retrying in ${delayMs}ms...`,
          error
        );
      }

      // Invoke retry callback if provided
      if (options.onRetry) {
        options.onRetry(attempt + 1, error, delayMs);
      }

      // Wait before retrying
      await sleep(delayMs);

      // Increment attempt counter
      attempt++;
    }
  }

  // This should never be reached, but TypeScript requires it
  throw lastError;
}
