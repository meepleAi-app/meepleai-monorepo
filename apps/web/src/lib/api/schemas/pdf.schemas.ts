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
  // Issue #5186: granular state + progress for KbCardStatusRow
  processingState: z.string().default('Pending'), // PdfProcessingState enum value
  progressPercentage: z.number().int().min(0).max(100).default(0),
  retryCount: z.number().int().nonnegative().default(0),
  maxRetries: z.number().int().positive().default(3),
  // Issue #5183: retry eligibility + error categorization
  canRetry: z.boolean().default(false),
  errorCategory: z.string().nullable().default(null),
  processingError: z.string().nullable().default(null),
  // Issue #5443: document classification for pipeline routing
  documentCategory: z
    .enum(['Rulebook', 'Expansion', 'Errata', 'QuickStart', 'Reference', 'PlayerAid', 'Other'])
    .default('Rulebook'),
  // Issue #5444: base document linkage
  baseDocumentId: z.string().uuid().nullable().default(null),
  // Issue #5446: copyright disclaimer and RAG toggle
  isActiveForRag: z.boolean().default(true),
  hasAcceptedDisclaimer: z.boolean().default(false),
  // Issue #5447: user-editable version label
  versionLabel: z.string().nullable().default(null),
});

export type PdfDocumentDto = z.infer<typeof PdfDocumentDtoSchema>;

// ========== Game PDF DTO (Issue #4915) ==========

/**
 * Game PDF DTO from GET /api/v1/library/games/{gameId}/pdfs
 * Matches GamePdfDto from UserLibrary bounded context
 */
export const GamePdfDtoSchema = z.object({
  id: z.string(),
  name: z.string(),
  pageCount: z.number().int().nonnegative(),
  fileSizeBytes: z.number().nonnegative(),
  uploadedAt: z.string().datetime(),
  source: z.string(), // "Custom" or "Catalog"
  language: z.string().nullable().optional(),
});

export type GamePdfDto = z.infer<typeof GamePdfDtoSchema>;

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

// ========== PDF Analytics (Issue #3715) ==========

/**
 * Daily upload stats for time series chart.
 * @see apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/DTOs/PdfAnalyticsDto.cs
 */
export const DailyUploadStatsSchema = z.object({
  date: z.string(), // DateOnly serialized as "YYYY-MM-DD"
  totalCount: z.number().int().nonnegative(),
  successCount: z.number().int().nonnegative(),
  failedCount: z.number().int().nonnegative(),
});

export type DailyUploadStats = z.infer<typeof DailyUploadStatsSchema>;

/**
 * Aggregated PDF analytics response.
 * Issue #3715: PDF processing metrics for admin dashboard.
 * TimeSpan fields serialized as "HH:mm:ss.fffffff" strings by .NET.
 */
export const PdfAnalyticsDtoSchema = z.object({
  totalUploaded: z.number().int().nonnegative(),
  successCount: z.number().int().nonnegative(),
  failedCount: z.number().int().nonnegative(),
  successRate: z.number().nonnegative(),
  avgProcessingTime: z.string().nullable(),
  p95ProcessingTime: z.string().nullable(),
  totalStorageBytes: z.number().nonnegative(),
  storageByTier: z.record(z.string(), z.number()),
  uploadsByDay: z.array(DailyUploadStatsSchema),
});

export type PdfAnalyticsDto = z.infer<typeof PdfAnalyticsDtoSchema>;
