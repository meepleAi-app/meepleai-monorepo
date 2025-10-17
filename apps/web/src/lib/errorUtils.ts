/**
 * Error categorization and handling utilities for PDF upload
 * Implements PDF-06: User-friendly error messages with correlation ID tracking
 */

export enum ErrorCategory {
  Validation = 'validation',
  Network = 'network',
  Server = 'server',
  Processing = 'processing',
  Unknown = 'unknown'
}

export interface CategorizedError {
  category: ErrorCategory;
  message: string;
  technicalMessage?: string;
  correlationId?: string;
  statusCode?: number;
  canRetry: boolean;
  suggestions: string[];
}

/**
 * Extracts correlation ID from response headers
 */
export function extractCorrelationId(response: Response): string | undefined {
  return response.headers.get('X-Correlation-Id') || undefined;
}

/**
 * Categorizes an error based on status code, error message, and type
 */
export function categorizeError(
  error: unknown,
  response?: Response,
  correlationId?: string
): CategorizedError {
  // Network errors (fetch failures, connection issues)
  if (error instanceof TypeError && error.message.includes('fetch')) {
    return {
      category: ErrorCategory.Network,
      message: 'Connection lost. Please check your internet and try again.',
      technicalMessage: error.message,
      correlationId,
      canRetry: true,
      suggestions: [
        'Check your internet connection',
        'Try again in a few moments',
        'Contact support if the problem persists'
      ]
    };
  }

  // Response-based errors
  if (response) {
    const statusCode = response.status;

    // Validation errors (400)
    if (statusCode === 400) {
      return {
        category: ErrorCategory.Validation,
        message: 'Invalid file or request.',
        technicalMessage: error instanceof Error ? error.message : String(error),
        correlationId,
        statusCode,
        canRetry: false,
        suggestions: [
          'Check file size (maximum 100MB)',
          'Ensure file is a valid PDF',
          'Verify all required fields are filled'
        ]
      };
    }

    // Forbidden (403)
    if (statusCode === 403) {
      return {
        category: ErrorCategory.Validation,
        message: 'You do not have permission to perform this action.',
        technicalMessage: 'Forbidden',
        correlationId,
        statusCode,
        canRetry: false,
        suggestions: [
          'Contact an administrator for access',
          'Verify you have the required role (Editor or Admin)'
        ]
      };
    }

    // Not found (404)
    if (statusCode === 404) {
      return {
        category: ErrorCategory.Validation,
        message: 'Resource not found.',
        technicalMessage: error instanceof Error ? error.message : String(error),
        correlationId,
        statusCode,
        canRetry: false,
        suggestions: [
          'Verify the game or file exists',
          'Try refreshing the page'
        ]
      };
    }

    // Server errors (500, 502, 503, 504)
    if (statusCode >= 500) {
      return {
        category: ErrorCategory.Server,
        message: 'Server error. Please try again in a few minutes.',
        technicalMessage: error instanceof Error ? error.message : String(error),
        correlationId,
        statusCode,
        canRetry: true,
        suggestions: [
          'Wait a few minutes and try again',
          'Contact support with the error ID if the problem persists'
        ]
      };
    }

    // Request timeout (408)
    if (statusCode === 408) {
      return {
        category: ErrorCategory.Network,
        message: 'Request timed out. Please try again.',
        technicalMessage: 'Request timeout',
        correlationId,
        statusCode,
        canRetry: true,
        suggestions: [
          'Check your internet connection',
          'Try again with a smaller file',
          'Contact support if the problem persists'
        ]
      };
    }

    // Too many requests (429)
    if (statusCode === 429) {
      return {
        category: ErrorCategory.Server,
        message: 'Too many requests. Please wait a moment and try again.',
        technicalMessage: 'Rate limit exceeded',
        correlationId,
        statusCode,
        canRetry: true,
        suggestions: [
          'Wait a few moments before trying again',
          'Reduce the number of concurrent requests'
        ]
      };
    }
  }

  // Processing errors (based on message content)
  if (error instanceof Error) {
    const message = error.message.toLowerCase();

    if (message.includes('corrupted') || message.includes('invalid pdf') || message.includes('format')) {
      return {
        category: ErrorCategory.Processing,
        message: 'Unable to process PDF. The file may be corrupted or in an unsupported format.',
        technicalMessage: error.message,
        correlationId,
        canRetry: false,
        suggestions: [
          'Re-download the original file',
          'Convert to a standard PDF format',
          'Ensure the file is not password-protected',
          'Try a different PDF file'
        ]
      };
    }

    if (message.includes('too large') || message.includes('size') || message.includes('exceed')) {
      return {
        category: ErrorCategory.Validation,
        message: 'File is too large. Maximum size is 100MB.',
        technicalMessage: error.message,
        correlationId,
        canRetry: false,
        suggestions: [
          'Compress the PDF file',
          'Split into smaller files',
          'Remove unnecessary images or pages'
        ]
      };
    }

    if (message.includes('parsing') || message.includes('extraction')) {
      return {
        category: ErrorCategory.Processing,
        message: 'Unable to extract text from PDF.',
        technicalMessage: error.message,
        correlationId,
        canRetry: true,
        suggestions: [
          'Verify the PDF contains text (not just images)',
          'Try OCR processing if the PDF is scanned',
          'Contact support with the error ID'
        ]
      };
    }
  }

  // Default unknown error
  return {
    category: ErrorCategory.Unknown,
    message: 'An unexpected error occurred. Please try again.',
    technicalMessage: error instanceof Error ? error.message : String(error),
    correlationId,
    canRetry: true,
    suggestions: [
      'Try again',
      'Refresh the page',
      'Contact support with the error ID if the problem persists'
    ]
  };
}

/**
 * Formats a user-friendly error message with the correlation ID
 */
export function formatErrorMessage(categorizedError: CategorizedError): string {
  let message = categorizedError.message;

  if (categorizedError.correlationId) {
    message += `\n\nError ID: ${categorizedError.correlationId} (for support)`;
  }

  return message;
}

/**
 * Gets an appropriate icon for the error category
 */
export function getErrorIcon(category: ErrorCategory): string {
  switch (category) {
    case ErrorCategory.Validation:
      return '❌';
    case ErrorCategory.Network:
      return '⚠️';
    case ErrorCategory.Server:
      return '❌';
    case ErrorCategory.Processing:
      return '❌';
    case ErrorCategory.Unknown:
      return '⚠️';
    default:
      return '❌';
  }
}

/**
 * Gets an appropriate title for the error category
 */
export function getErrorTitle(category: ErrorCategory): string {
  switch (category) {
    case ErrorCategory.Validation:
      return 'Invalid File';
    case ErrorCategory.Network:
      return 'Connection Lost';
    case ErrorCategory.Server:
      return 'Upload Failed';
    case ErrorCategory.Processing:
      return 'Unable to Process PDF';
    case ErrorCategory.Unknown:
      return 'Unexpected Error';
    default:
      return 'Error';
  }
}
