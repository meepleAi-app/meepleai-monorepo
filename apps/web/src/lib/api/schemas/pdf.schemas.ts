/**
 * PDF & Document Processing API Schemas (FE-IMP-005)
 *
 * Zod schemas for validating DocumentProcessing bounded context responses.
 * Covers: PDF upload, processing progress, document metadata
 */

import { z } from 'zod';

// ========== PDF Document ==========

export const PdfDocumentDtoSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid(),
  fileName: z.string().min(1),
  filePath: z.string().min(1),
  fileSizeBytes: z.number().int().nonnegative(),
  processingStatus: z.string().min(1),
  uploadedAt: z.string().datetime(),
  processedAt: z.string().datetime().nullable(),
  pageCount: z.number().int().positive().nullable(),
  documentType: z.enum(['base', 'expansion', 'errata', 'homerule']).default('base'), // Issue #2051
  isPublic: z.boolean().default(false), // Admin Wizard: Public library visibility
});

export type PdfDocumentDto = z.infer<typeof PdfDocumentDtoSchema>;

// ========== Processing Step Enum ==========

/**
 * Processing pipeline steps matching backend ProcessingStep enum.
 * @see apps/api/src/Api/Models/ProcessingProgress.cs
 */
export const ProcessingStepSchema = z.enum([
  'Uploading',
  'Extracting',
  'Chunking',
  'Embedding',
  'Indexing',
  'Completed',
  'Failed',
]);

export type ProcessingStepDto = z.infer<typeof ProcessingStepSchema>;

// ========== Processing Progress ==========

/**
 * Processing progress response matching backend ProcessingProgress model.
 * @see apps/api/src/Api/Models/ProcessingProgress.cs
 *
 * TimeSpan fields (elapsedTime, estimatedTimeRemaining) are serialized as
 * "HH:mm:ss.fffffff" strings by .NET System.Text.Json.
 */
export const ProcessingProgressSchema = z.object({
  /** Current step in the processing pipeline */
  currentStep: ProcessingStepSchema,
  /** Overall completion percentage (0-100) */
  percentComplete: z.number().int().min(0).max(100),
  /** Time elapsed since processing started (TimeSpan as string "HH:mm:ss.fffffff") */
  elapsedTime: z.string(),
  /** Estimated time remaining, null if unable to estimate (TimeSpan as string) */
  estimatedTimeRemaining: z.string().nullable(),
  /** Number of pages processed so far */
  pagesProcessed: z.number().int().nonnegative(),
  /** Total number of pages in the PDF */
  totalPages: z.number().int().nonnegative(),
  /** When processing started (UTC ISO 8601) */
  startedAt: z.string().datetime(),
  /** When processing completed (UTC ISO 8601), null if still in progress */
  completedAt: z.string().datetime().nullable(),
  /** Error message if processing failed */
  errorMessage: z.string().nullable().optional(),
});

export type ProcessingProgress = z.infer<typeof ProcessingProgressSchema>;

// ========== PDF Metrics (Issue #4219) ==========

/**
 * PDF processing metrics with per-state timing and ETA.
 * Issue #4219: Duration metrics and ETA calculation.
 * @see apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/DTOs/PdfMetricsDto.cs
 */
export const PdfMetricsSchema = z.object({
  /** Document unique identifier */
  documentId: z.string().uuid(),
  /** Current processing state */
  currentState: z.enum([
    'Pending',
    'Uploading',
    'Extracting',
    'Chunking',
    'Embedding',
    'Indexing',
    'Ready',
    'Failed',
  ]),
  /** Overall progress percentage (0-100) */
  progressPercentage: z.number().int().min(0).max(100),
  /** Total duration since upload (TimeSpan as "HH:mm:ss.fffffff" string), null if in progress */
  totalDuration: z.string().nullable(),
  /** Estimated time remaining (TimeSpan string), null if completed/failed */
  estimatedTimeRemaining: z.string().nullable(),
  /** Duration spent in each state (state name → TimeSpan string) */
  stateDurations: z.record(z.string(), z.string()),
  /** Number of retry attempts */
  retryCount: z.number().int().nonnegative(),
  /** Total page count, null if not yet extracted */
  pageCount: z.number().int().positive().nullable(),
});

export type PdfMetrics = z.infer<typeof PdfMetricsSchema>;
