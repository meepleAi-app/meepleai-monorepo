/**
 * HTTP Client Core (FE-IMP-005)
 *
 * Base HTTP client with error handling, correlation ID support,
 * and Zod validation for responses.
 */

import { z } from 'zod';

import { getStoredApiKeySync } from './apiKeyStore';
import {
  canExecute as canExecuteCircuit,
  recordSuccess as recordCircuitSuccess,
  recordFailure as recordCircuitFailure,
} from './circuitBreaker';
import { createApiError, NetworkError, SchemaValidationError, ApiError } from './errors';
import { logApiError, logger } from './logger';
import { recordRetryAttempt, recordRetrySuccess, recordRetryFailure } from './metrics';
import { globalRequestCache, CacheKeyOptions } from './requestCache';
import { withRetry, RetryOptions, parseRetryAfter } from './retryPolicy';

export interface HttpClientConfig {
  baseUrl: string;
  fetchImpl?: typeof fetch;
}

export interface RequestOptions extends RequestInit {
  skipErrorLogging?: boolean;
  /** Retry configuration for this request */
  retry?: RetryOptions;
  /** Disable circuit breaker for this request */
  skipCircuitBreaker?: boolean;
  /**
   * Skip request deduplication for this request
   * @default false for GET requests, true for POST/PUT/DELETE
   */
  skipDedup?: boolean;
}

/**
 * Get API base URL from environment with fallback
 *
 * In development (browser runtime), returns empty string to use Next.js API proxy (prevents CORS).
 * In test/production, returns absolute URL from NEXT_PUBLIC_API_BASE or localhost.
 *
 * @see apps/web/src/app/api/v1/[...path]/route.ts - Next.js API proxy
 * @see Issue #2366 - BGG search CORS fix
 */
export function getApiBase(): string {
  // Development (browser only): Use Next.js API proxy (relative paths, no CORS)
  // Skip in test environment to maintain test mock compatibility
  if (process.env.NODE_ENV === 'development' && typeof window !== 'undefined') {
    return '';
  }

  // Production/Test: Use absolute URL from environment
  const envBase = process.env.NEXT_PUBLIC_API_BASE?.trim();
  if (envBase && envBase !== 'undefined' && envBase !== 'null') {
    return envBase;
  }

  // Fallback for test environment and development SSR
  return 'http://localhost:8080';
}

/**
 * Base HTTP client with centralized error handling and validation
 */
export class HttpClient {
  private readonly baseUrl: string;
  private readonly fetchImpl: typeof fetch;

  constructor(config?: Partial<HttpClientConfig>) {
    this.baseUrl = config?.baseUrl || getApiBase();
    // Bind fetch to prevent "Illegal invocation" error (client-side only)
    this.fetchImpl =
      config?.fetchImpl || (typeof window !== 'undefined' ? fetch.bind(window) : fetch);
  }

  /**
   * GET request with optional Zod validation, automatic retry, and circuit breaker
   */
  async get<T>(path: string, schema?: z.ZodSchema<T>, options?: RequestOptions): Promise<T | null> {
    // Generate cache key for deduplication (opt-in by default for GET)
    const cacheKey = globalRequestCache.generateKey(
      'GET',
      `${this.baseUrl}${path}`,
      undefined,
      this.getAuthContext(),
      this.getCacheKeyOptions(options)
    );

    // Force skipDedup if custom onRetry callback is present
    const skipDedup =
      options?.retry?.onRetry !== undefined
        ? true // Force skipDedup when custom onRetry callback is present
        : (options?.skipDedup ?? false); // Otherwise use explicit value or default to false for GET

    // Use request cache for deduplication (wraps retry logic)
    return globalRequestCache.dedupe(
      cacheKey,
      async () => {
        // Check circuit breaker before attempting request
        if (!options?.skipCircuitBreaker && !canExecuteCircuit(path)) {
          const error = new Error(
            `Circuit breaker is OPEN for ${path}. Request denied to prevent cascading failures.`
          );
          error.name = 'CircuitBreakerError';
          throw error;
        }

        let retryCount = 0;

        const result = await withRetry(
          async () => {
            try {
              const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
                method: 'GET',
                credentials: 'include',
                headers: this.getHeaders(),
                ...options,
              });

              // 401 returns null for optional authentication (not an error to retry)
              if (response.status === 401) {
                return null;
              }

              if (!response.ok) {
                await this.handleError(path, response, options);
              }

              const data = await response.json();

              // Validate response with Zod if schema provided
              if (schema) {
                const validated = this.validateResponse(path, data, schema);

                // Track success after retry
                if (retryCount > 0) {
                  recordRetrySuccess();
                }

                // Record circuit breaker success
                if (!options?.skipCircuitBreaker) {
                  recordCircuitSuccess(path);
                }

                return validated;
              }

              // Track success after retry
              if (retryCount > 0) {
                recordRetrySuccess();
              }

              // Record circuit breaker success
              if (!options?.skipCircuitBreaker) {
                recordCircuitSuccess(path);
              }

              return data as T;
            } catch (error) {
              // Convert fetch errors to NetworkError for retry logic
              if (error instanceof TypeError && error.message.includes('fetch')) {
                throw new NetworkError({
                  message: `Network error for ${path}: ${error.message}`,
                  endpoint: path,
                });
              }
              throw error;
            }
          },
          {
            ...options?.retry,
            onRetry: (attempt, error, delayMs) => {
              retryCount = attempt;

              // Check for Retry-After header for adaptive backoff
              if (error instanceof ApiError && error.response) {
                const retryAfterHeader = error.response.headers.get('Retry-After');
                const retryAfterMs = parseRetryAfter(retryAfterHeader);

                if (retryAfterMs) {
                  logger.info(
                    `[AdaptiveBackoff] Server requested delay via Retry-After header for ${path}: ${retryAfterMs}ms`
                  );
                }
              }

              // Record metrics for each retry
              const statusCode = error instanceof ApiError ? error.statusCode : undefined;
              recordRetryAttempt(path, statusCode, delayMs);

              // Call user-provided callback if exists
              if (options?.retry?.onRetry) {
                options.retry.onRetry(attempt, error, delayMs);
              }
            },
          }
        ).catch(error => {
          // Track failure after all retries exhausted
          if (retryCount > 0) {
            recordRetryFailure();
          }

          // Record circuit breaker failure
          if (!options?.skipCircuitBreaker) {
            recordCircuitFailure(path);
          }

          throw error;
        });

        return result;
      },
      skipDedup // Deduplication disabled if custom onRetry callback or skipDedup is true
    );
  }

  /**
   * POST request with optional Zod validation and automatic retry
   */
  async post<T>(
    path: string,
    body?: unknown,
    schema?: z.ZodSchema<T>,
    options?: RequestOptions
  ): Promise<T> {
    // Generate cache key for deduplication (opt-out by default for POST)
    const cacheKey = globalRequestCache.generateKey(
      'POST',
      `${this.baseUrl}${path}`,
      body,
      this.getAuthContext(),
      this.getCacheKeyOptions(options)
    );

    // Force skipDedup if custom onRetry callback is present
    const skipDedup =
      options?.retry?.onRetry !== undefined
        ? true // Force skipDedup when custom onRetry callback is present
        : (options?.skipDedup ?? true); // Otherwise use explicit value or default to true for POST

    // Use request cache for deduplication (wraps retry logic)
    return globalRequestCache.dedupe(
      cacheKey,
      async () => {
        // Check circuit breaker before attempting request
        if (!options?.skipCircuitBreaker && !canExecuteCircuit(path)) {
          const error = new Error(
            `Circuit breaker is OPEN for ${path}. Request denied to prevent cascading failures.`
          );
          error.name = 'CircuitBreakerError';
          throw error;
        }

        let retryCount = 0;

        const result = await withRetry(
          async () => {
            try {
              const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
                method: 'POST',
                credentials: 'include',
                headers: {
                  ...this.getHeaders(),
                  'Content-Type': 'application/json',
                },
                body: JSON.stringify(body ?? {}),
                ...options,
              });

              if (response.status === 401) {
                const error = await createApiError(path, response);
                if (!options?.skipErrorLogging) {
                  logApiError(error);
                }
                throw error;
              }

              if (!response.ok) {
                await this.handleError(path, response, options);
              }

              // Handle 204 No Content (no body to parse)
              if (response.status === 204) {
                // Track success after retry
                if (retryCount > 0) {
                  recordRetrySuccess();
                }
                // Record circuit breaker success
                if (!options?.skipCircuitBreaker) {
                  recordCircuitSuccess(path);
                }
                return undefined as T;
              }

              const data = await response.json();

              // Validate response with Zod if schema provided
              if (schema) {
                const validated = this.validateResponse(path, data, schema);
                // Track success after retry
                if (retryCount > 0) {
                  recordRetrySuccess();
                }
                // Record circuit breaker success
                if (!options?.skipCircuitBreaker) {
                  recordCircuitSuccess(path);
                }
                return validated;
              }

              // Track success after retry
              if (retryCount > 0) {
                recordRetrySuccess();
              }

              // Record circuit breaker success
              if (!options?.skipCircuitBreaker) {
                recordCircuitSuccess(path);
              }

              return data as T;
            } catch (error) {
              // Convert fetch errors to NetworkError for retry logic
              if (error instanceof TypeError && error.message.includes('fetch')) {
                throw new NetworkError({
                  message: `Network error for ${path}: ${error.message}`,
                  endpoint: path,
                });
              }
              throw error;
            }
          },
          {
            ...options?.retry,
            onRetry: (attempt, error, delayMs) => {
              retryCount = attempt;
              // Record metrics for each retry
              const statusCode = error instanceof ApiError ? error.statusCode : undefined;
              recordRetryAttempt(path, statusCode, delayMs);

              // Call user-provided callback if exists
              if (options?.retry?.onRetry) {
                options.retry.onRetry(attempt, error, delayMs);
              }
            },
          }
        ).catch(error => {
          // Track failure after all retries exhausted
          if (retryCount > 0) {
            recordRetryFailure();
          }

          // Record circuit breaker failure
          if (!options?.skipCircuitBreaker) {
            recordCircuitFailure(path);
          }

          throw error;
        });

        return result;
      },
      skipDedup // Deduplication disabled if custom onRetry callback or skipDedup is true
    );
  }

  /**
   * PUT request with optional Zod validation and automatic retry
   */
  async put<T>(
    path: string,
    body: unknown,
    schema?: z.ZodSchema<T>,
    options?: RequestOptions
  ): Promise<T> {
    // Generate cache key for deduplication (opt-out by default for PUT)
    const cacheKey = globalRequestCache.generateKey(
      'PUT',
      `${this.baseUrl}${path}`,
      body,
      this.getAuthContext(),
      this.getCacheKeyOptions(options)
    );

    // Force skipDedup if custom onRetry callback is present
    const skipDedup =
      options?.retry?.onRetry !== undefined
        ? true // Force skipDedup when custom onRetry callback is present
        : (options?.skipDedup ?? true); // Otherwise use explicit value or default to true for PUT

    // Use request cache for deduplication (wraps retry logic)
    return globalRequestCache.dedupe(
      cacheKey,
      async () => {
        // Check circuit breaker before attempting request
        if (!options?.skipCircuitBreaker && !canExecuteCircuit(path)) {
          const error = new Error(
            `Circuit breaker is OPEN for ${path}. Request denied to prevent cascading failures.`
          );
          error.name = 'CircuitBreakerError';
          throw error;
        }

        let retryCount = 0;

        const result = await withRetry(
          async () => {
            try {
              const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
                method: 'PUT',
                credentials: 'include',
                headers: {
                  ...this.getHeaders(),
                  'Content-Type': 'application/json',
                },
                body: JSON.stringify(body),
                ...options,
              });

              if (response.status === 401) {
                const error = await createApiError(path, response);
                if (!options?.skipErrorLogging) {
                  logApiError(error);
                }
                throw error;
              }

              if (!response.ok) {
                await this.handleError(path, response, options);
              }

              const data = await response.json();

              // Validate response with Zod if schema provided
              if (schema) {
                const validated = this.validateResponse(path, data, schema);
                // Track success after retry
                if (retryCount > 0) {
                  recordRetrySuccess();
                }
                // Record circuit breaker success
                if (!options?.skipCircuitBreaker) {
                  recordCircuitSuccess(path);
                }
                return validated;
              }

              // Track success after retry
              if (retryCount > 0) {
                recordRetrySuccess();
              }

              // Record circuit breaker success
              if (!options?.skipCircuitBreaker) {
                recordCircuitSuccess(path);
              }

              return data as T;
            } catch (error) {
              // Convert fetch errors to NetworkError for retry logic
              if (error instanceof TypeError && error.message.includes('fetch')) {
                throw new NetworkError({
                  message: `Network error for ${path}: ${error.message}`,
                  endpoint: path,
                });
              }
              throw error;
            }
          },
          {
            ...options?.retry,
            onRetry: (attempt, error, delayMs) => {
              retryCount = attempt;
              // Record metrics for each retry
              const statusCode = error instanceof ApiError ? error.statusCode : undefined;
              recordRetryAttempt(path, statusCode, delayMs);

              // Call user-provided callback if exists
              if (options?.retry?.onRetry) {
                options.retry.onRetry(attempt, error, delayMs);
              }
            },
          }
        ).catch(error => {
          // Track failure after all retries exhausted
          if (retryCount > 0) {
            recordRetryFailure();
          }

          // Record circuit breaker failure
          if (!options?.skipCircuitBreaker) {
            recordCircuitFailure(path);
          }

          throw error;
        });

        return result;
      },
      skipDedup // Deduplication disabled if custom onRetry callback or skipDedup is true
    );
  }

  /**
   * PATCH request with optional Zod validation and automatic retry (for partial updates)
   */
  async patch<T>(
    path: string,
    body: unknown,
    schema?: z.ZodSchema<T>,
    options?: RequestOptions
  ): Promise<T> {
    // Generate cache key for deduplication (opt-out by default for PATCH)
    const cacheKey = globalRequestCache.generateKey(
      'PATCH',
      `${this.baseUrl}${path}`,
      body,
      this.getAuthContext(),
      this.getCacheKeyOptions(options)
    );

    const skipDedup = options?.retry?.onRetry !== undefined ? true : (options?.skipDedup ?? true);

    return globalRequestCache.dedupe(
      cacheKey,
      async () => {
        if (!options?.skipCircuitBreaker && !canExecuteCircuit(path)) {
          const error = new Error(
            `Circuit breaker is OPEN for ${path}. Request denied to prevent cascading failures.`
          );
          error.name = 'CircuitBreakerError';
          throw error;
        }

        let retryCount = 0;

        const result = await withRetry(
          async () => {
            try {
              const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
                method: 'PATCH',
                credentials: 'include',
                headers: {
                  ...this.getHeaders(),
                  'Content-Type': 'application/json',
                },
                body: JSON.stringify(body),
                ...options,
              });

              if (response.status === 401) {
                const error = await createApiError(path, response);
                if (!options?.skipErrorLogging) {
                  logApiError(error);
                }
                throw error;
              }

              if (!response.ok) {
                await this.handleError(path, response, options);
              }

              // Handle 204 No Content (no body to parse)
              if (response.status === 204) {
                if (retryCount > 0) {
                  recordRetrySuccess();
                }
                if (!options?.skipCircuitBreaker) {
                  recordCircuitSuccess(path);
                }
                return undefined as T;
              }

              const data = await response.json();

              if (schema) {
                const validated = this.validateResponse(path, data, schema);
                if (retryCount > 0) {
                  recordRetrySuccess();
                }
                if (!options?.skipCircuitBreaker) {
                  recordCircuitSuccess(path);
                }
                return validated;
              }

              if (retryCount > 0) {
                recordRetrySuccess();
              }

              if (!options?.skipCircuitBreaker) {
                recordCircuitSuccess(path);
              }

              return data as T;
            } catch (error) {
              if (error instanceof TypeError && error.message.includes('fetch')) {
                throw new NetworkError({
                  message: `Network error for ${path}: ${error.message}`,
                  endpoint: path,
                });
              }
              throw error;
            }
          },
          {
            ...options?.retry,
            onRetry: (attempt, error, delayMs) => {
              retryCount = attempt;
              const statusCode = error instanceof ApiError ? error.statusCode : undefined;
              recordRetryAttempt(path, statusCode, delayMs);

              if (options?.retry?.onRetry) {
                options.retry.onRetry(attempt, error, delayMs);
              }
            },
          }
        ).catch(error => {
          if (retryCount > 0) {
            recordRetryFailure();
          }

          if (!options?.skipCircuitBreaker) {
            recordCircuitFailure(path);
          }

          throw error;
        });

        return result;
      },
      skipDedup
    );
  }

  /**
   * DELETE request with automatic retry
   */
  async delete(path: string, options?: RequestOptions): Promise<void> {
    // Generate cache key for deduplication (opt-out by default for DELETE)
    const cacheKey = globalRequestCache.generateKey(
      'DELETE',
      `${this.baseUrl}${path}`,
      undefined,
      this.getAuthContext(),
      this.getCacheKeyOptions(options)
    );

    // Force skipDedup if custom onRetry callback is present
    const skipDedup =
      options?.retry?.onRetry !== undefined
        ? true // Force skipDedup when custom onRetry callback is present
        : (options?.skipDedup ?? true); // Otherwise use explicit value or default to true for DELETE

    // Use request cache for deduplication (wraps retry logic)
    return globalRequestCache.dedupe(
      cacheKey,
      async () => {
        // Check circuit breaker before attempting request
        if (!options?.skipCircuitBreaker && !canExecuteCircuit(path)) {
          const error = new Error(
            `Circuit breaker is OPEN for ${path}. Request denied to prevent cascading failures.`
          );
          error.name = 'CircuitBreakerError';
          throw error;
        }

        let retryCount = 0;

        await withRetry(
          async () => {
            try {
              const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
                method: 'DELETE',
                credentials: 'include',
                headers: this.getHeaders(),
                ...options,
              });

              if (response.status === 401) {
                const error = await createApiError(path, response);
                if (!options?.skipErrorLogging) {
                  logApiError(error);
                }
                throw error;
              }

              if (!response.ok) {
                await this.handleError(path, response, options);
              }

              // Track success after retry
              if (retryCount > 0) {
                recordRetrySuccess();
              }

              // Record circuit breaker success
              if (!options?.skipCircuitBreaker) {
                recordCircuitSuccess(path);
              }

              // DELETE returns 204 NoContent, no body to parse
            } catch (error) {
              // Convert fetch errors to NetworkError for retry logic
              if (error instanceof TypeError && error.message.includes('fetch')) {
                throw new NetworkError({
                  message: `Network error for ${path}: ${error.message}`,
                  endpoint: path,
                });
              }
              throw error;
            }
          },
          {
            ...options?.retry,
            onRetry: (attempt, error, delayMs) => {
              retryCount = attempt;
              // Record metrics for each retry
              const statusCode = error instanceof ApiError ? error.statusCode : undefined;
              recordRetryAttempt(path, statusCode, delayMs);

              // Call user-provided callback if exists
              if (options?.retry?.onRetry) {
                options.retry.onRetry(attempt, error, delayMs);
              }
            },
          }
        ).catch(error => {
          // Track failure after all retries exhausted
          if (retryCount > 0) {
            recordRetryFailure();
          }

          // Record circuit breaker failure
          if (!options?.skipCircuitBreaker) {
            recordCircuitFailure(path);
          }

          throw error;
        });
      },
      skipDedup // Deduplication disabled if custom onRetry callback or skipDedup is true
    );
  }

  /**
   * POST request for file downloads (blob response) with automatic retry
   */
  async postFile(
    path: string,
    body: unknown,
    options?: RequestOptions
  ): Promise<{ blob: Blob; filename: string }> {
    // Check circuit breaker before attempting request
    if (!options?.skipCircuitBreaker && !canExecuteCircuit(path)) {
      const error = new Error(
        `Circuit breaker is OPEN for ${path}. Request denied to prevent cascading failures.`
      );
      error.name = 'CircuitBreakerError';
      throw error;
    }

    let retryCount = 0;

    const result = await withRetry(
      async () => {
        try {
          const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
            method: 'POST',
            credentials: 'include',
            headers: {
              ...this.getHeaders(),
              'Content-Type': 'application/json',
            },
            body: JSON.stringify(body),
            ...options,
          });

          if (response.status === 401) {
            const error = await createApiError(path, response);
            if (!options?.skipErrorLogging) {
              logApiError(error);
            }
            throw error;
          }

          if (!response.ok) {
            await this.handleError(path, response, options);
          }

          // Extract filename from Content-Disposition header
          const contentDisposition = response.headers.get('Content-Disposition');
          let filename = `download-${Date.now()}`;

          if (contentDisposition) {
            const filenameMatch = contentDisposition.match(
              /filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/
            );
            if (filenameMatch && filenameMatch[1]) {
              filename = filenameMatch[1].replace(/['"]/g, '');
            }
          }

          const blob = await response.blob();

          // Track success after retry
          if (retryCount > 0) {
            recordRetrySuccess();
          }

          // Record circuit breaker success
          if (!options?.skipCircuitBreaker) {
            recordCircuitSuccess(path);
          }

          return { blob, filename };
        } catch (error) {
          // Convert fetch errors to NetworkError for retry logic
          if (error instanceof TypeError && error.message.includes('fetch')) {
            throw new NetworkError({
              message: `Network error for ${path}: ${error.message}`,
              endpoint: path,
            });
          }
          throw error;
        }
      },
      {
        ...options?.retry,
        onRetry: (attempt, error, delayMs) => {
          retryCount = attempt;
          // Record metrics for each retry
          const statusCode = error instanceof ApiError ? error.statusCode : undefined;
          recordRetryAttempt(path, statusCode, delayMs);

          // Call user-provided callback if exists
          if (options?.retry?.onRetry) {
            options.retry.onRetry(attempt, error, delayMs);
          }
        },
      }
    ).catch(error => {
      // Track failure after all retries exhausted
      if (retryCount > 0) {
        recordRetryFailure();
      }

      // Record circuit breaker failure
      if (!options?.skipCircuitBreaker) {
        recordCircuitFailure(path);
      }

      throw error;
    });

    return result;
  }

  /**
   * Get request headers with correlation ID for distributed tracing
   */
  private getHeaders(): HeadersInit {
    const headers: HeadersInit = {};

    const apiKey = getStoredApiKeySync();
    if (apiKey) {
      headers['Authorization'] = `ApiKey ${apiKey}`;
    }

    // Correlation ID for distributed tracing (browser or test mock)
    if (typeof sessionStorage !== 'undefined' && typeof crypto !== 'undefined') {
      let correlationId = sessionStorage.getItem('correlation_id');
      if (!correlationId) {
        correlationId = crypto.randomUUID();
        sessionStorage.setItem('correlation_id', correlationId);
      }
      headers['X-Correlation-ID'] = correlationId;
    }

    return headers;
  }

  /**
   * Get authentication context for cache key generation
   */
  private getAuthContext(): string | undefined {
    const apiKey = getStoredApiKeySync();
    return apiKey ? `apikey:${apiKey}` : undefined;
  }

  /**
   * Extract option-sensitive fields for cache key generation
   * to prevent requests with different options from sharing cached promises.
   *
   * This method extracts only the fields from RequestOptions that affect
   * request behavior (circuit breaker, retry logic) to include in the cache key.
   *
   * Special handling:
   * - Empty options {} is treated as undefined (returns undefined)
   * - Custom onRetry callbacks force deduplication to be disabled (returns undefined)
   *   to ensure each request can use its own callback
   *
   * @param options - The request options to extract cache-sensitive fields from
   * @returns CacheKeyOptions if any relevant fields are present, undefined otherwise
   */
  private getCacheKeyOptions(options?: RequestOptions): CacheKeyOptions | undefined {
    if (!options) {
      return undefined;
    }

    // If custom onRetry callback is present, disable deduplication
    // to ensure each request can execute its own callback
    if (options.retry?.onRetry) {
      return undefined;
    }

    const cacheKeyOptions: CacheKeyOptions = {};
    let hasOptions = false;

    // Include circuit breaker flag
    if (options.skipCircuitBreaker !== undefined) {
      cacheKeyOptions.skipCircuitBreaker = options.skipCircuitBreaker;
      hasOptions = true;
    }

    // Include retry configuration
    if (options.retry) {
      if (options.retry.skipRetry !== undefined) {
        cacheKeyOptions.skipRetry = options.retry.skipRetry;
        hasOptions = true;
      }
      if (options.retry.retryConfig) {
        cacheKeyOptions.retryConfig = options.retry.retryConfig;
        hasOptions = true;
      }
    }

    return hasOptions ? cacheKeyOptions : undefined;
  }

  /**
   * Handle HTTP errors and throw appropriate error types
   */
  private async handleError(
    path: string,
    response: Response,
    options?: RequestOptions
  ): Promise<never> {
    const error = await createApiError(path, response);

    if (!options?.skipErrorLogging) {
      logApiError(error);
    }

    throw error;
  }

  /**
   * Validate response data against Zod schema
   */
  private validateResponse<T>(path: string, data: unknown, schema: z.ZodSchema<T>): T {
    const result = schema.safeParse(data);

    if (!result.success) {
      const error = new SchemaValidationError({
        message: `Response validation failed for ${path}`,
        zodError: result.error,
        endpoint: path,
      });

      logApiError(error);
      throw error;
    }

    return result.data;
  }
}

/**
 * Helper function to trigger file download in browser
 */
export function downloadFile(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}
