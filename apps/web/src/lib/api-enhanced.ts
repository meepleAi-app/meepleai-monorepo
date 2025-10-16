/**
 * Enhanced API client with retry logic, error handling, and logging
 */

import { ApiError, NetworkError, RetryConfig, DEFAULT_RETRY_CONFIG, calculateBackoffDelay, sleep } from './errors';
import { logger } from './logger';

export const API_BASE_FALLBACK = "http://localhost:8080";

export const getApiBase = (): string => {
  const envBase = process.env.NEXT_PUBLIC_API_BASE?.trim();
  if (envBase && envBase !== "undefined" && envBase !== "null") {
    return envBase;
  }
  return API_BASE_FALLBACK;
};

/**
 * Request options with retry configuration
 */
export interface ApiRequestOptions {
  retry?: Partial<RetryConfig>;
  skipRetry?: boolean;
  timeout?: number;
  signal?: AbortSignal;
}

/**
 * Response wrapper with metadata
 */
export interface ApiResponse<T> {
  data: T;
  statusCode: number;
  correlationId?: string;
  headers: Headers;
}

/**
 * Extracts correlation ID from response headers
 */
function getCorrelationId(headers: Headers): string | undefined {
  return headers.get('X-Correlation-Id') || headers.get('x-correlation-id') || undefined;
}

/**
 * Creates AbortController with timeout
 */
function createTimeoutController(timeoutMs: number, existingSignal?: AbortSignal): AbortController {
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), timeoutMs);

  if (existingSignal) {
    existingSignal.addEventListener('abort', () => {
      clearTimeout(timeoutId);
      controller.abort();
    });
  }

  return controller;
}

/**
 * Executes fetch with retry logic
 */
async function fetchWithRetry<T>(
  url: string,
  init: RequestInit,
  options: ApiRequestOptions = {}
): Promise<ApiResponse<T>> {
  const retryConfig: RetryConfig = {
    ...DEFAULT_RETRY_CONFIG,
    ...options.retry
  };

  let lastError: Error | null = null;
  const endpoint = url.replace(getApiBase(), '');
  const method = init.method || 'GET';

  for (let attempt = 1; attempt <= retryConfig.maxAttempts; attempt++) {
    try {
      // Add timeout if specified
      const controller = options.timeout
        ? createTimeoutController(options.timeout, options.signal)
        : undefined;

      const signal = controller?.signal || options.signal;

      const response = await fetch(url, {
        ...init,
        signal
      });

      const correlationId = getCorrelationId(response.headers);

      // Update logger correlation ID
      if (correlationId) {
        logger.setCorrelationId(correlationId);
      }

      // Handle 401 specially (don't retry)
      if (response.status === 401) {
        logger.warn('Authentication required', {
          component: 'ApiClient',
          action: `${method} ${endpoint}`,
          metadata: { statusCode: 401, correlationId }
        });
        throw new ApiError('Unauthorized', 401, endpoint, method, correlationId, false);
      }

      // Check if response is OK
      if (!response.ok) {
        const error = new ApiError(
          `API request failed: ${response.statusText}`,
          response.status,
          endpoint,
          method,
          correlationId,
          true // Mark as retryable by default
        );

        // Don't retry 4xx errors (except 408, 429)
        if (response.status >= 400 && response.status < 500 && response.status !== 408 && response.status !== 429) {
          throw error;
        }

        // Retry logic
        if (options.skipRetry || attempt >= retryConfig.maxAttempts) {
          throw error;
        }

        const delay = calculateBackoffDelay(attempt, retryConfig);
        logger.warn(`API request failed, retrying in ${delay}ms (attempt ${attempt}/${retryConfig.maxAttempts})`, {
          component: 'ApiClient',
          action: `${method} ${endpoint}`,
          metadata: { statusCode: response.status, correlationId, attempt, delay }
        });

        await sleep(delay);
        continue;
      }

      // Parse response
      let data: T;
      const contentType = response.headers.get('content-type');

      if (contentType?.includes('application/json')) {
        data = await response.json();
      } else if (response.status === 204) {
        data = null as T;
      } else {
        data = (await response.text()) as unknown as T;
      }

      // Log successful request
      logger.debug(`API request successful: ${method} ${endpoint}`, {
        component: 'ApiClient',
        action: `${method} ${endpoint}`,
        metadata: { statusCode: response.status, correlationId }
      });

      return {
        data,
        statusCode: response.status,
        correlationId,
        headers: response.headers
      };

    } catch (error) {
      lastError = error as Error;

      // Network errors (connection refused, timeout, etc.)
      if (error instanceof TypeError || (error as Error).name === 'AbortError') {
        const networkError = new NetworkError(
          error instanceof TypeError ? 'Network request failed' : 'Request timeout',
          endpoint,
          error
        );

        logger.error('Network error', networkError, {
          component: 'ApiClient',
          action: `${method} ${endpoint}`,
          metadata: { attempt }
        });

        // Retry network errors
        if (!options.skipRetry && attempt < retryConfig.maxAttempts) {
          const delay = calculateBackoffDelay(attempt, retryConfig);
          logger.info(`Retrying after network error in ${delay}ms (attempt ${attempt}/${retryConfig.maxAttempts})`);
          await sleep(delay);
          continue;
        }

        throw networkError;
      }

      // ApiError - already handled above
      if (error instanceof ApiError) {
        logger.error('API error', error, {
          component: 'ApiClient',
          action: `${method} ${endpoint}`,
          metadata: {
            statusCode: error.statusCode,
            correlationId: error.correlationId,
            attempt
          }
        });

        // Don't retry non-retryable errors
        if (!error.isRetryable() || options.skipRetry || attempt >= retryConfig.maxAttempts) {
          throw error;
        }

        const delay = calculateBackoffDelay(attempt, retryConfig);
        await sleep(delay);
        continue;
      }

      // Unknown error
      logger.error('Unexpected error', error as Error, {
        component: 'ApiClient',
        action: `${method} ${endpoint}`,
        metadata: { attempt }
      });
      throw error;
    }
  }

  // All retries exhausted
  if (lastError) {
    throw lastError;
  }

  throw new Error('Request failed without error');
}

/**
 * Enhanced API client
 */
export const apiEnhanced = {
  async get<T>(path: string, options?: ApiRequestOptions): Promise<T | null> {
    try {
      const response = await fetchWithRetry<T>(
        `${getApiBase()}${path}`,
        {
          method: 'GET',
          credentials: 'include'
        },
        options
      );

      return response.data;
    } catch (error) {
      if (error instanceof ApiError && error.statusCode === 401) {
        return null;
      }
      throw error;
    }
  },

  async post<T>(path: string, body?: unknown, options?: ApiRequestOptions): Promise<T> {
    const response = await fetchWithRetry<T>(
      `${getApiBase()}${path}`,
      {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body ?? {})
      },
      options
    );

    return response.data;
  },

  async put<T>(path: string, body: unknown, options?: ApiRequestOptions): Promise<T> {
    const response = await fetchWithRetry<T>(
      `${getApiBase()}${path}`,
      {
        method: 'PUT',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
      },
      options
    );

    return response.data;
  },

  async delete(path: string, options?: ApiRequestOptions): Promise<void> {
    await fetchWithRetry<void>(
      `${getApiBase()}${path}`,
      {
        method: 'DELETE',
        credentials: 'include'
      },
      options
    );
  },

  /**
   * Gets full response with metadata
   */
  async getWithMetadata<T>(path: string, options?: ApiRequestOptions): Promise<ApiResponse<T>> {
    return fetchWithRetry<T>(
      `${getApiBase()}${path}`,
      {
        method: 'GET',
        credentials: 'include'
      },
      options
    );
  }
};

// Re-export types for backward compatibility
export interface RuleSpecComment {
  id: string;
  gameId: string;
  version: string;
  atomId: string | null;
  userId: string;
  userDisplayName: string;
  commentText: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface RuleSpecCommentsResponse {
  gameId: string;
  version: string;
  comments: RuleSpecComment[];
  totalComments: number;
}

export interface CreateRuleSpecCommentRequest {
  atomId: string | null;
  commentText: string;
}

export interface UpdateRuleSpecCommentRequest {
  commentText: string;
}

// RuleSpec Comment API with enhanced client
const ruleSpecComments = {
  async getComments(gameId: string, version: string): Promise<RuleSpecCommentsResponse | null> {
    return apiEnhanced.get<RuleSpecCommentsResponse>(
      `/api/v1/games/${gameId}/rulespec/versions/${version}/comments`
    );
  },

  async createComment(
    gameId: string,
    version: string,
    request: CreateRuleSpecCommentRequest
  ): Promise<RuleSpecComment> {
    return apiEnhanced.post<RuleSpecComment>(
      `/api/v1/games/${gameId}/rulespec/versions/${version}/comments`,
      request
    );
  },

  async updateComment(
    gameId: string,
    commentId: string,
    request: UpdateRuleSpecCommentRequest
  ): Promise<RuleSpecComment> {
    return apiEnhanced.put<RuleSpecComment>(
      `/api/v1/games/${gameId}/rulespec/comments/${commentId}`,
      request
    );
  },

  async deleteComment(gameId: string, commentId: string): Promise<void> {
    return apiEnhanced.delete(`/api/v1/games/${gameId}/rulespec/comments/${commentId}`);
  }
};

export { ruleSpecComments };
