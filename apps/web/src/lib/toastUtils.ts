/**
 * Toast notification utilities for error handling
 *
 * Provides helpers for showing error toasts based on error category.
 * Uses Sonner for toast notifications with automatic categorization.
 */

import { toast } from 'sonner';

import { type CategorizedError, ErrorCategory } from './errorUtils';

/**
 * Configuration for toast notifications
 */
export const TOAST_CONFIG = {
  duration: {
    transient: 5000, // Auto-dismiss after 5 seconds
    persistent: Infinity, // Require manual dismissal
  },
  position: 'top-right' as const,
};

/**
 * Determines if an error should show a toast notification
 */
export function shouldShowToast(error: CategorizedError): boolean {
  // Only show toasts for transient errors that can be retried
  return (
    error.canRetry &&
    (error.category === ErrorCategory.Network || error.category === ErrorCategory.Server)
  );
}

/**
 * Shows a toast notification for an error
 */
export function showErrorToast(error: CategorizedError): void {
  const isTransient = shouldShowToast(error);
  const duration = isTransient ? TOAST_CONFIG.duration.transient : TOAST_CONFIG.duration.persistent;

  // Determine toast type based on error category
  switch (error.category) {
    case ErrorCategory.Network:
      toast.warning(error.message, {
        description: error.correlationId ? `Error ID: ${error.correlationId}` : undefined,
        duration,
      });
      break;

    case ErrorCategory.Server:
      if (error.statusCode === 429) {
        toast.warning('Rate limit exceeded', {
          description: 'Please wait a moment before trying again.',
          duration,
        });
      } else {
        toast.error(error.message, {
          description: error.correlationId ? `Error ID: ${error.correlationId}` : undefined,
          duration,
        });
      }
      break;

    case ErrorCategory.Validation:
      toast.error(error.message, {
        description: error.suggestions[0] || undefined,
        duration: TOAST_CONFIG.duration.persistent,
      });
      break;

    case ErrorCategory.Processing:
      toast.error(error.message, {
        description: 'Please check the file and try again.',
        duration: TOAST_CONFIG.duration.persistent,
      });
      break;

    case ErrorCategory.Unknown:
    default:
      toast.error('An unexpected error occurred', {
        description: error.correlationId ? `Error ID: ${error.correlationId}` : 'Please try again.',
        duration,
      });
      break;
  }
}

/**
 * Shows a success toast
 */
export function showSuccessToast(message: string, description?: string): void {
  toast.success(message, {
    description,
    duration: TOAST_CONFIG.duration.transient,
  });
}

/**
 * Shows an info toast
 */
export function showInfoToast(message: string, description?: string): void {
  toast.info(message, {
    description,
    duration: TOAST_CONFIG.duration.transient,
  });
}

/**
 * Shows a warning toast
 */
export function showWarningToast(message: string, description?: string): void {
  toast.warning(message, {
    description,
    duration: TOAST_CONFIG.duration.transient,
  });
}
