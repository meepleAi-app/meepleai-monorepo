/**
 * API Configuration
 *
 * Centralized configuration for HTTP client behavior including
 * request timeouts, retry logic, and caching strategies.
 *
 * All values are type-safe using `as const` to enable literal type inference.
 */

export const API_CONFIG = {
  /**
   * Request timeout duration (milliseconds)
   * Requests taking longer than this will be aborted
   * @default 30000
   */
  REQUEST_TIMEOUT_MS: 30000,

  /**
   * Maximum number of retry attempts for failed requests
   * Applies to transient errors (5xx, network failures)
   * @default 3
   */
  RETRY_MAX_ATTEMPTS: 3,

  /**
   * Base delay for exponential backoff (milliseconds)
   * Actual delay calculated as: baseDelay * 2^attempt
   * @default 1000
   */
  RETRY_BASE_DELAY_MS: 1000,

  /**
   * Maximum retry delay (milliseconds)
   * Caps exponential backoff to prevent excessive wait times
   * @default 10000
   */
  RETRY_MAX_DELAY_MS: 10000,

  /**
   * Jitter factor for retry delays (0-1)
   * Adds randomness to prevent thundering herd: delay * (1 ± jitter)
   * @default 0.3 (30% jitter)
   */
  RETRY_JITTER: 0.3,

  /**
   * Cache TTL (Time To Live) in milliseconds
   * How long cached responses remain valid before refresh
   * @default 300000 (5 minutes)
   */
  CACHE_TTL_MS: 5 * 60 * 1000,

  /**
   * Circuit breaker failure threshold
   * Number of consecutive failures before circuit opens
   * @default 5
   */
  CIRCUIT_BREAKER_FAILURE_THRESHOLD: 5,

  /**
   * Circuit breaker timeout (milliseconds)
   * How long circuit stays open before attempting recovery
   * @default 60000 (1 minute)
   */
  CIRCUIT_BREAKER_TIMEOUT_MS: 60000,
} as const;

/**
 * Type-safe keys for API_CONFIG
 */
export type ApiConfigKey = keyof typeof API_CONFIG;
