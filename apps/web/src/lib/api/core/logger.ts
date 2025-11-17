/**
 * API Error Logger (FE-IMP-005)
 *
 * Centralized logging for API errors with Seq integration support.
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
      error: error instanceof Error ? {
        name: error.name,
        message: error.message,
        stack: error.stack,
        ...(error as ApiError).toJSON?.(),
      } : error,
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
 * Seq logger implementation (structured logging)
 *
 * Sends logs to Seq endpoint configured in environment variables.
 * Falls back to console if Seq is unavailable.
 */
class SeqLogger implements Logger {
  private readonly seqUrl: string;
  private readonly apiKey?: string;
  private readonly fallbackLogger: Logger;

  constructor() {
    this.seqUrl = process.env.NEXT_PUBLIC_SEQ_URL || 'http://localhost:8081';
    this.apiKey = process.env.NEXT_PUBLIC_SEQ_API_KEY;
    this.fallbackLogger = new ConsoleLogger();
  }

  async error(message: string, error?: Error | ApiError, context?: LogContext): Promise<void> {
    // Always log to console first for immediate visibility
    this.fallbackLogger.error(message, error, context);

    // Send to Seq
    await this.sendToSeq('Error', message, {
      error: error instanceof Error ? {
        name: error.name,
        message: error.message,
        stack: error.stack,
        ...(error as ApiError).toJSON?.(),
      } : error,
      ...context,
    });
  }

  async warn(message: string, context?: LogContext): Promise<void> {
    this.fallbackLogger.warn(message, context);
    await this.sendToSeq('Warning', message, context);
  }

  async info(message: string, context?: LogContext): Promise<void> {
    this.fallbackLogger.info(message, context);
    await this.sendToSeq('Information', message, context);
  }

  async debug(message: string, context?: LogContext): Promise<void> {
    this.fallbackLogger.debug(message, context);
    if (process.env.NODE_ENV === 'development') {
      await this.sendToSeq('Debug', message, context);
    }
  }

  private async sendToSeq(
    level: 'Error' | 'Warning' | 'Information' | 'Debug',
    message: string,
    properties?: Record<string, unknown>
  ): Promise<void> {
    try {
      const payload = {
        '@t': new Date().toISOString(),
        '@mt': message,
        '@l': level,
        ...properties,
      };

      const headers: HeadersInit = {
        'Content-Type': 'application/json',
      };

      if (this.apiKey) {
        headers['X-Seq-ApiKey'] = this.apiKey;
      }

      // Non-blocking: fire and forget
      fetch(`${this.seqUrl}/api/events/raw`, {
        method: 'POST',
        headers,
        body: JSON.stringify(payload),
      }).catch((err) => {
        // Silently fail if Seq is unavailable
        console.debug('[Seq] Failed to send log:', err);
      });
    } catch (err) {
      // Silently fail if Seq logging fails
      console.debug('[Seq] Failed to send log:', err);
    }
  }
}

/**
 * Create logger instance based on configuration
 */
function createLogger(): Logger {
  // Check if Seq is configured
  const seqUrl = process.env.NEXT_PUBLIC_SEQ_URL;

  if (seqUrl && seqUrl !== 'undefined' && seqUrl !== 'null') {
    return new SeqLogger();
  }

  // Fallback to console logger
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
