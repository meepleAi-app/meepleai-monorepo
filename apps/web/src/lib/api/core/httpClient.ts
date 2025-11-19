/**
 * HTTP Client Core (FE-IMP-005)
 *
 * Base HTTP client with error handling, correlation ID support,
 * and Zod validation for responses.
 */

import { z } from 'zod';
import { createApiError, NetworkError, SchemaValidationError } from './errors';
import { logApiError } from './logger';

export interface HttpClientConfig {
  baseUrl: string;
  fetchImpl?: typeof fetch;
}

export interface RequestOptions extends RequestInit {
  skipErrorLogging?: boolean;
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
   * GET request with optional Zod validation
   */
  async get<T>(
    path: string,
    schema?: z.ZodSchema<T>,
    options?: RequestOptions
  ): Promise<T | null> {
    const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
      method: 'GET',
      credentials: 'include',
      headers: this.getHeaders(),
      ...options,
    });

    // 401 returns null for optional authentication
    if (response.status === 401) {
      return null;
    }

    if (!response.ok) {
      await this.handleError(path, response, options);
    }

    const data = await response.json();

    // Validate response with Zod if schema provided
    if (schema) {
      return this.validateResponse(path, data, schema);
    }

    return data as T;
  }

  /**
   * POST request with optional Zod validation
   */
  async post<T>(
    path: string,
    body?: unknown,
    schema?: z.ZodSchema<T>,
    options?: RequestOptions
  ): Promise<T> {
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
      return undefined as T;
    }

    const data = await response.json();

    // Validate response with Zod if schema provided
    if (schema) {
      return this.validateResponse(path, data, schema);
    }

    return data as T;
  }

  /**
   * PUT request with optional Zod validation
   */
  async put<T>(
    path: string,
    body: unknown,
    schema?: z.ZodSchema<T>,
    options?: RequestOptions
  ): Promise<T> {
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
      return this.validateResponse(path, data, schema);
    }

    return data as T;
  }

  /**
   * DELETE request
   */
  async delete(path: string, options?: RequestOptions): Promise<void> {
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

    // DELETE returns 204 NoContent, no body to parse
  }

  /**
   * POST request for file downloads (blob response)
   */
  async postFile(
    path: string,
    body: unknown,
    options?: RequestOptions
  ): Promise<{ blob: Blob; filename: string }> {
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
    return { blob, filename };
  }

  /**
   * Get request headers with correlation ID for distributed tracing
   */
  private getHeaders(): HeadersInit {
    const headers: HeadersInit = {};

    // API Key authentication via httpOnly cookie (secure, XSS-protected)
    // API keys are now stored in secure httpOnly cookies set by the backend.
    // The browser automatically includes the cookie in requests.
    // For programmatic API access (CLI, scripts), use the X-API-Key header directly.
    // SECURITY: API keys are NO LONGER stored in localStorage to prevent XSS attacks.

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
