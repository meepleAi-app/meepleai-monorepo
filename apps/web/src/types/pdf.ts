/**
 * PDF Processing Types (PDF-08)
 * Type definitions for PDF processing progress tracking
 *
 * NOTE (FE-IMP-005): Using API schema types directly instead of local definitions
 */

// Re-export ProcessingProgress from API schemas
export type { ProcessingProgress } from '@/lib/api';

/**
 * Helper to check if processing is in a terminal state
 */
export function isProcessingComplete(status: string): boolean {
  return status === 'Completed' || status === 'Failed';
}
