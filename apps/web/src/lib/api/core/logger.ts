/**
 * API Error Logger (FE-IMP-005)
 *
 * Centralized logging for API errors.
 * Sends structured logs with correlationId for distributed tracing.
 * Seq integration removed - replaced by HyperDX unified observability (Issue #1564).
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
 * Seq logger implementation removed (Issue #1564)
 *
 * SeqLogger class removed - frontend logs now use ConsoleLogger only.
 * Backend telemetry (logs, traces) is sent to HyperDX via OpenTelemetry.
 * For frontend observability, consider HyperDX browser SDK integration in future.
 */

/**
 * Create logger instance
 *
 * Uses ConsoleLogger for all environments.
 * Seq integration removed (Issue #1564) - replaced by HyperDX for backend telemetry.
 */
function createLogger(): Logger {
  return new ConsoleLogger();
}

/**
 * Global logger instance
 */
export const logger: Logger = createLogger();

/**
 * Log API error with full context
 */
export function logApiError(error: ApiError, additionalContext?: LogContext): void {
  const context: LogContext = {
    correlationId: error.correlationId,
    endpoint: error.endpoint,
    statusCode: error.statusCode,
    ...additionalContext,
  };

  logger.error(`API request failed: ${error.message}`, error, context);
}
