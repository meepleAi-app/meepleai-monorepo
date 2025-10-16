/**
 * Client-side error logging service with structured logging
 * Integrates with backend observability stack (correlation IDs, structured logs)
 */

import { ErrorContext, ErrorSeverity, getErrorSeverity, sanitizeError } from './errors';

/**
 * Log level
 */
export enum LogLevel {
  DEBUG = 'debug',
  INFO = 'info',
  WARN = 'warn',
  ERROR = 'error'
}

/**
 * Structured log entry
 */
export interface LogEntry {
  level: LogLevel;
  message: string;
  timestamp: string;
  context?: ErrorContext;
  error?: Record<string, unknown>;
  severity?: ErrorSeverity;
  tags?: string[];
}

/**
 * Logger configuration
 */
export interface LoggerConfig {
  enableConsole: boolean;
  enableRemote: boolean;
  remoteEndpoint?: string;
  minLevel: LogLevel;
  correlationIdHeader: string;
  batchSize: number;
  flushIntervalMs: number;
}

/**
 * Default logger configuration
 */
const DEFAULT_CONFIG: LoggerConfig = {
  enableConsole: process.env.NODE_ENV === 'development',
  enableRemote: true,
  remoteEndpoint: '/api/v1/logs/client',
  minLevel: LogLevel.INFO,
  correlationIdHeader: 'X-Correlation-Id',
  batchSize: 10,
  flushIntervalMs: 5000
};

/**
 * Client-side logger with batching and remote logging
 */
class Logger {
  private config: LoggerConfig;
  private logQueue: LogEntry[] = [];
  private flushTimer: NodeJS.Timeout | null = null;
  private correlationId: string | null = null;

  constructor(config: Partial<LoggerConfig> = {}) {
    this.config = { ...DEFAULT_CONFIG, ...config };

    if (typeof window !== 'undefined' && this.config.enableRemote) {
      this.startFlushTimer();

      // Flush on page unload
      window.addEventListener('beforeunload', () => {
        this.flush();
      });
    }
  }

  /**
   * Sets correlation ID for all subsequent logs
   */
  setCorrelationId(id: string): void {
    this.correlationId = id;
  }

  /**
   * Gets current correlation ID
   */
  getCorrelationId(): string | null {
    return this.correlationId;
  }

  /**
   * Log debug message
   */
  debug(message: string, context?: Partial<ErrorContext>): void {
    this.log(LogLevel.DEBUG, message, context);
  }

  /**
   * Log info message
   */
  info(message: string, context?: Partial<ErrorContext>): void {
    this.log(LogLevel.INFO, message, context);
  }

  /**
   * Log warning message
   */
  warn(message: string, context?: Partial<ErrorContext>): void {
    this.log(LogLevel.WARN, message, context);
  }

  /**
   * Log error
   */
  error(message: string, error?: Error, context?: Partial<ErrorContext>): void {
    const entry: LogEntry = {
      level: LogLevel.ERROR,
      message,
      timestamp: new Date().toISOString(),
      context: context as ErrorContext,
      error: error ? sanitizeError(error) : undefined,
      severity: error ? getErrorSeverity(error) : ErrorSeverity.ERROR
    };

    this.addToQueue(entry);
  }

  /**
   * Log with custom level
   */
  private log(level: LogLevel, message: string, context?: Partial<ErrorContext>): void {
    if (!this.shouldLog(level)) {
      return;
    }

    const entry: LogEntry = {
      level,
      message,
      timestamp: new Date().toISOString(),
      context: context as ErrorContext
    };

    this.addToQueue(entry);
  }

  /**
   * Adds log entry to queue and logs to console
   */
  private addToQueue(entry: LogEntry): void {
    // Console logging
    if (this.config.enableConsole) {
      this.logToConsole(entry);
    }

    // Remote logging
    if (this.config.enableRemote) {
      this.logQueue.push(entry);

      if (this.logQueue.length >= this.config.batchSize) {
        this.flush();
      }
    }
  }

  /**
   * Logs to console with appropriate formatting
   */
  private logToConsole(entry: LogEntry): void {
    const prefix = `[${entry.timestamp}] [${entry.level.toUpperCase()}]`;
    const message = entry.context?.component
      ? `${prefix} [${entry.context.component}] ${entry.message}`
      : `${prefix} ${entry.message}`;

    switch (entry.level) {
      case LogLevel.DEBUG:
        console.debug(message, entry);
        break;
      case LogLevel.INFO:
        console.info(message, entry);
        break;
      case LogLevel.WARN:
        console.warn(message, entry);
        break;
      case LogLevel.ERROR:
        console.error(message, entry.error || entry);
        break;
    }
  }

  /**
   * Checks if log level should be logged
   */
  private shouldLog(level: LogLevel): boolean {
    const levels = [LogLevel.DEBUG, LogLevel.INFO, LogLevel.WARN, LogLevel.ERROR];
    const currentLevelIndex = levels.indexOf(this.config.minLevel);
    const logLevelIndex = levels.indexOf(level);
    return logLevelIndex >= currentLevelIndex;
  }

  /**
   * Starts periodic flush timer
   */
  private startFlushTimer(): void {
    this.flushTimer = setInterval(() => {
      this.flush();
    }, this.config.flushIntervalMs);
  }

  /**
   * Flushes log queue to remote endpoint
   */
  flush(): void {
    if (this.logQueue.length === 0 || !this.config.remoteEndpoint) {
      return;
    }

    const logs = [...this.logQueue];
    this.logQueue = [];

    // Send logs to backend (fire and forget)
    if (typeof navigator !== 'undefined' && navigator.sendBeacon) {
      // Use sendBeacon for reliability during page unload
      const blob = new Blob([JSON.stringify({ logs })], { type: 'application/json' });
      navigator.sendBeacon(this.config.remoteEndpoint, blob);
    } else {
      // Fallback to fetch (won't work during page unload)
      const fetchPromise = fetch(this.config.remoteEndpoint, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(this.correlationId && { [this.config.correlationIdHeader]: this.correlationId })
        },
        body: JSON.stringify({ logs }),
        credentials: 'include',
        keepalive: true
      });

      if (fetchPromise && typeof fetchPromise.catch === 'function') {
        fetchPromise.catch((err) => {
          // Silent fail - logging shouldn't break the app
          if (this.config.enableConsole) {
            console.warn('Failed to send logs to remote endpoint:', err);
          }
        });
      }
    }
  }

  /**
   * Stops flush timer
   */
  destroy(): void {
    if (this.flushTimer) {
      clearInterval(this.flushTimer);
      this.flushTimer = null;
    }
    this.flush();
  }
}

// Singleton logger instance
let loggerInstance: Logger | null = null;

/**
 * Gets or creates the logger instance
 */
export function getLogger(config?: Partial<LoggerConfig>): Logger {
  if (!loggerInstance) {
    loggerInstance = new Logger(config);
  }
  return loggerInstance;
}

/**
 * Resets logger instance (useful for testing)
 */
export function resetLogger(): void {
  if (loggerInstance) {
    loggerInstance.destroy();
    loggerInstance = null;
  }
}

// Export default logger
export const logger = getLogger();
