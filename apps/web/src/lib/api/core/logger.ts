/**
 * API Error Logger (FE-IMP-005)
 *
 * Centralized logging for API errors.
 * Sends structured logs with correlationId for distributed tracing.
 */

/* eslint-disable no-console */

import type { ApiError } from './errors';

export interface LogContext {
  correlationId?: string;
  endpoint?: string;
  statusCode?: number;
  userId?: string;
  [key: string]: unknown;
}

export interface Logger {
  error(message: string, error?: Error | ApiError, context?: LogContext): void;
  warn(message: string, context?: LogContext): void;
  info(message: string, context?: LogContext): void;
  debug(message: string, context?: LogContext): void;
}

/**
 * Console logger implementation (fallback)
 */
class ConsoleLogger implements Logger {
  error(message: string, error?: Error | ApiError, context?: LogContext): void {
    console.error('[API Error]', message, {
      error:
        error instanceof Error
          ? {
              name: error.name,
              message: error.message,
              stack: error.stack,
              ...(error as ApiError).toJSON?.(),
            }
          : error,
      context,
    });
  }

  warn(message: string, context?: LogContext): void {
    console.warn('[API Warning]', message, context);
  }

  info(message: string, context?: LogContext): void {
    console.info('[API Info]', message, context);
  }

  debug(message: string, context?: LogContext): void {
    if (process.env.NODE_ENV === 'development') {
      console.debug('[API Debug]', message, context);
    }
  }
}

/**
 * Create logger instance
 *
 * Uses ConsoleLogger for all environments.
 */
function createLogger(): Logger {
  return new ConsoleLogger();
}

/**
 * Global logger instance
 */
export const logger: Logger = createLogger();

/**
 * Log API error with full context.
 * 404 Not Found is intentionally suppressed — hooks treat it as expected
 * "resource absent" state (e.g. no PDF uploaded, no agents configured)
 * and the polling/retry strategy handles eventual consistency.
 */
export function logApiError(error: ApiError, additionalContext?: LogContext): void {
  // 404s are expected "not found" responses, not actionable errors
  if (error.statusCode === 404) return;

  const context: LogContext = {
    correlationId: error.correlationId,
    endpoint: error.endpoint,
    statusCode: error.statusCode,
    ...additionalContext,
  };

  logger.error(`API request failed: ${error.message}`, error, context);
}
