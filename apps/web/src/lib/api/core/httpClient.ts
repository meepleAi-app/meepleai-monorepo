/**
 * HTTP Client Core (FE-IMP-005)
 *
 * Base HTTP client with error handling, correlation ID support,
 * and Zod validation for responses.
 */

import { z } from 'zod';
import { createApiError, NetworkError, SchemaValidationError, ApiError } from './errors';
import { logApiError } from './logger';
import { getStoredApiKey } from './apiKeyStore';
import { withRetry, RetryOptions, parseRetryAfter } from './retryPolicy';
import {
  recordRetryAttempt,
  recordRetrySuccess,
  recordRetryFailure,
} from './metrics';
import {
  canExecute as canExecuteCircuit,
  recordSuccess as recordCircuitSuccess,
  recordFailure as recordCircuitFailure,
} from './circuitBreaker';
import { globalRequestCache } from './requestCache';

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
 */
export function getApiBase(): string {
  const envBase = process.env.NEXT_PUBLIC_API_BASE?.trim();
  if (envBase && envBase !== 'undefined' && envBase !== 'null') {
    return envBase;
  }
  return 'http://localhost:5080';
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
    this.fetchImpl = config?.fetchImpl || (typeof window !== 'undefined' ? fetch.bind(window) : fetch);
  }


  /**
   * GET request with optional Zod validation, automatic retry, and circuit breaker
   */
  async get<T>(
    path: string,
    schema?: z.ZodSchema<T>,
    options?: RequestOptions
  ): Promise<T | null> {
    // Generate cache key for deduplication (opt-in by default for GET)
    const cacheKey = globalRequestCache.generateKey(
      'GET',
      `${this.baseUrl}${path}`,
      undefined,
      this.getAuthContext()
    );

    // Use request cache for deduplication (wraps retry logic)
    return globalRequestCache.dedupe(
      cacheKey,
      async () => {
        // Check circuit breaker before attempting request
        if (!options?.skipCircuitBreaker && !canExecuteCircuit(path)) {
          const error = new Error(`Circuit breaker is OPEN for ${path}. Request denied to prevent cascading failures.`);
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
                  console.info(
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
        ).catch((error) => {
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
      options?.skipDedup ?? false // Default: deduplication enabled for GET
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
      this.getAuthContext()
    );

    // Use request cache for deduplication (wraps retry logic)
    return globalRequestCache.dedupe(
      cacheKey,
      async () => {
        // Check circuit breaker before attempting request
        if (!options?.skipCircuitBreaker && !canExecuteCircuit(path)) {
          const error = new Error(`Circuit breaker is OPEN for ${path}. Request denied to prevent cascading failures.`);
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
        ).catch((error) => {
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
      options?.skipDedup ?? true // Default: deduplication disabled for POST
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
      this.getAuthContext()
    );

    // Use request cache for deduplication (wraps retry logic)
    return globalRequestCache.dedupe(
      cacheKey,
      async () => {
        // Check circuit breaker before attempting request
        if (!options?.skipCircuitBreaker && !canExecuteCircuit(path)) {
          const error = new Error(`Circuit breaker is OPEN for ${path}. Request denied to prevent cascading failures.`);
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
        ).catch((error) => {
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
      options?.skipDedup ?? true // Default: deduplication disabled for PUT
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
      this.getAuthContext()
    );

    // Use request cache for deduplication (wraps retry logic)
    return globalRequestCache.dedupe(
      cacheKey,
      async () => {
        // Check circuit breaker before attempting request
        if (!options?.skipCircuitBreaker && !canExecuteCircuit(path)) {
          const error = new Error(`Circuit breaker is OPEN for ${path}. Request denied to prevent cascading failures.`);
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
        ).catch((error) => {
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
      options?.skipDedup ?? true // Default: deduplication disabled for DELETE
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
      const error = new Error(`Circuit breaker is OPEN for ${path}. Request denied to prevent cascading failures.`);
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
            const filenameMatch = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
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
    ).catch((error) => {
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

    const apiKey = getStoredApiKey();
    if (apiKey) {
      headers['Authorization'] = `ApiKey ${apiKey}`;
    }

    if (typeof window !== 'undefined') {
      // Correlation ID for distributed tracing
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
    const apiKey = getStoredApiKey();
    return apiKey ? `apikey:${apiKey}` : undefined;
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
  private validateResponse<T>(
    path: string,
    data: unknown,
    schema: z.ZodSchema<T>
  ): T {
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
