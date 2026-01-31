/**
 * Retry Utility (Issue #3188)
 *
 * Exponential backoff retry logic for API calls
 */

export interface RetryOptions {
  /** Maximum number of retry attempts */
  maxAttempts?: number;

  /** Initial delay in milliseconds */
  initialDelay?: number;

  /** Maximum delay in milliseconds */
  maxDelay?: number;

  /** Backoff multiplier */
  backoffFactor?: number;

  /** Error codes to retry */
  retryableErrorCodes?: string[];
}

const DEFAULT_OPTIONS: Required<RetryOptions> = {
  maxAttempts: 3,
  initialDelay: 500,
  maxDelay: 5000,
  backoffFactor: 2,
  retryableErrorCodes: ['NETWORK_ERROR', 'TIMEOUT', 'SERVER_ERROR'],
};

/**
 * Retry a function with exponential backoff
 */
export async function retryWithBackoff<T>(
  fn: () => Promise<T>,
  options: RetryOptions = {}
): Promise<T> {
  const opts = { ...DEFAULT_OPTIONS, ...options };
  let lastError: Error | undefined;
  let delay = opts.initialDelay;

  for (let attempt = 1; attempt <= opts.maxAttempts; attempt++) {
    try {
      return await fn();
    } catch (error) {
      lastError = error instanceof Error ? error : new Error('Unknown error');

      // Check if error is retryable
      const errorCode = (error as { code?: string }).code;
      const isRetryable = !errorCode || opts.retryableErrorCodes.includes(errorCode);

      // Don't retry if not retryable or last attempt
      if (!isRetryable || attempt >= opts.maxAttempts) {
        throw lastError;
      }

      // Wait before retry
      await new Promise(resolve => setTimeout(resolve, delay));

      // Increase delay with backoff
      delay = Math.min(delay * opts.backoffFactor, opts.maxDelay);
    }
  }

  throw lastError;
}

/**
 * Check if error is retryable
 */
export function isRetryableError(error: unknown, retryableErrorCodes: string[] = []): boolean {
  if (!(error instanceof Error)) return false;

  const errorCode = (error as { code?: string }).code;
  if (!errorCode) return false;

  return retryableErrorCodes.includes(errorCode);
}
