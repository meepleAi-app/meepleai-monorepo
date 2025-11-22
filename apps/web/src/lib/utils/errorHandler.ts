/**
 * Error handling utilities for type-safe error processing
 */

import { ApiError } from '@/lib/api';

/**
 * Extract a user-friendly error message from an unknown error
 * @param error - The caught error (unknown type)
 * @param fallbackMessage - Default message if error cannot be parsed
 * @returns User-friendly error message string
 */
export function getErrorMessage(error: unknown, fallbackMessage: string = 'An unexpected error occurred'): string {
  if (error instanceof ApiError) {
    return error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  if (typeof error === 'string') {
    return error;
  }

  return fallbackMessage;
}

/**
 * Extract error details for logging/debugging
 * @param error - The caught error (unknown type)
 * @returns Object with error details
 */
export function getErrorDetails(error: unknown): { message: string; statusCode?: number; correlationId?: string } {
  if (error instanceof ApiError) {
    return {
      message: error.message,
      statusCode: error.statusCode,
      correlationId: error.correlationId,
    };
  }

  if (error instanceof Error) {
    return { message: error.message };
  }

  return { message: String(error) };
}
