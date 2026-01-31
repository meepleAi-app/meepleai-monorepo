/**
 * Retry utility with exponential backoff for transient errors
 * Implements PDF-06: Automatic retry logic for network and server errors
 */

export interface RetryOptions {
  maxAttempts?: number;
  initialDelayMs?: number;
  maxDelayMs?: number;
  shouldRetry?: (error: unknown, attempt: number) => boolean;
  onRetry?: (error: unknown, attempt: number, delayMs: number) => void;
}

const DEFAULT_OPTIONS: Required<RetryOptions> = {
  maxAttempts: 3,
  initialDelayMs: 1000, // 1 second
  maxDelayMs: 8000, // 8 seconds
  shouldRetry: () => true,
  onRetry: () => {}
};

/**
 * Delays execution for the specified number of milliseconds
 */
function delay(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}

/**
 * Calculates exponential backoff delay
 * Formula: initialDelay * (2 ^ attempt)
 * Capped at maxDelayMs
 */
function calculateBackoffDelay(
  attempt: number,
  initialDelayMs: number,
  maxDelayMs: number
): number {
  const exponentialDelay = initialDelayMs * Math.pow(2, attempt);
  return Math.min(exponentialDelay, maxDelayMs);
}

/**
 * Executes a function with automatic retry on failure
 * Uses exponential backoff: 1s, 2s, 4s (by default)
 *
 * @param fn - The async function to execute
 * @param options - Retry configuration options
 * @returns The result of the successful execution
 * @throws The last error if all retries fail
 *
 * @example
 * ```typescript
 * const result = await retryWithBackoff(
 *   () => fetch('/api/upload', { method: 'POST', body: formData }),
 *   {
 *     maxAttempts: 3,
 *     shouldRetry: (error) => error instanceof TypeError,
 *     onRetry: (error, attempt, delay) => {
 *       console.log(`Retry attempt ${attempt} after ${delay}ms`);
 *     }
 *   }
 * );
 * ```
 */
export async function retryWithBackoff<T>(
  fn: () => Promise<T>,
  options: RetryOptions = {}
): Promise<T> {
  const opts = { ...DEFAULT_OPTIONS, ...options };
  let lastError: unknown;

  for (let attempt = 0; attempt < opts.maxAttempts; attempt++) {
    try {
      return await fn();
    } catch (error) {
      lastError = error;

      // Check if we should retry
      const isLastAttempt = attempt === opts.maxAttempts - 1;
      if (isLastAttempt || !opts.shouldRetry(error, attempt)) {
        throw error;
      }

      // Calculate delay and wait
      const delayMs = calculateBackoffDelay(
        attempt,
        opts.initialDelayMs,
        opts.maxDelayMs
      );

      opts.onRetry(error, attempt + 1, delayMs);
      await delay(delayMs);
    }
  }

  // This should never be reached, but TypeScript requires it
  throw lastError;
}

/**
 * Determines if an error is retryable (network or server error)
 */
export function isRetryableError(error: unknown): boolean {
  // Network errors (fetch failures)
  if (error instanceof TypeError && error.message.includes('fetch')) {
    return true;
  }

  // Check for status code in custom ApiError
  if (error && typeof error === 'object' && 'statusCode' in error) {
    const statusCode = (error as { statusCode?: number }).statusCode;
    if (statusCode) {
      // Retry on server errors (5xx) and request timeout (408)
      return statusCode >= 500 || statusCode === 408 || statusCode === 429;
    }
  }

  // Check for Response object
  if (error && typeof error === 'object' && 'response' in error) {
    const response = (error as { response?: Response }).response;
    if (response && response.status) {
      return response.status >= 500 || response.status === 408 || response.status === 429;
    }
  }

  return false;
}

/**
 * Creates a retry wrapper for a fetch operation
 * Automatically retries on network and server errors
 *
 * @example
 * ```typescript
 * const upload = createRetryableFetch(
 *   () => fetch('/api/upload', { method: 'POST', body: formData }),
 *   { maxAttempts: 3 }
 * );
 *
 * try {
 *   const response = await upload();
 * } catch (error) {
 *   // All retries failed
 * }
 * ```
 */
export function createRetryableFetch(
  fetchFn: () => Promise<Response>,
  options: RetryOptions = {}
): () => Promise<Response> {
  return () =>
    retryWithBackoff(fetchFn, {
      ...options,
      shouldRetry: (error, attempt) => {
        // Use custom shouldRetry if provided
        if (options.shouldRetry && !options.shouldRetry(error, attempt)) {
          return false;
        }
        // Otherwise use default retryable error check
        return isRetryableError(error);
      }
    });
}
