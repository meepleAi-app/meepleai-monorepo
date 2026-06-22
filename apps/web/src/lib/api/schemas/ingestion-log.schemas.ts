import { z } from 'zod';

/**
 * Mirrors backend `StepLogEntryDto` (DocumentProcessing/Application/DTOs/ProcessingJobDto.cs).
 * Level is a stringified enum: "Info" | "Warning" | "Error".
 */
export const IngestionLogEntrySchema = z.object({
  id: z.string().uuid(),
  timestamp: z.string().datetime({ offset: true }),
  level: z.enum(['Info', 'Warning', 'Error']),
  message: z.string(),
});
export type IngestionLogEntry = z.infer<typeof IngestionLogEntrySchema>;

/**
 * Mirrors backend `ProcessingStepDto`. `StepName` is a stringified `ProcessingStepType`
 * enum: "Upload" | "Extract" | "Chunk" | "Embed" | "Index".
 */
export const IngestionStepSchema = z.object({
  id: z.string().uuid(),
  stepName: z.enum(['Upload', 'Extract', 'Chunk', 'Embed', 'Index']),
  status: z.string(),
  startedAt: z.string().datetime({ offset: true }).nullable(),
  completedAt: z.string().datetime({ offset: true }).nullable(),
  durationMs: z.number().nullable(),
  metadataJson: z.string().nullable(),
  logEntries: z.array(IngestionLogEntrySchema),
});
export type IngestionStep = z.infer<typeof IngestionStepSchema>;

/**
 * Mirrors backend `ProcessingJobDetailDto` — top-level shape returned by
 * GET /api/v1/admin/kb/docs/{docId}/ingestion-log.
 * Null body = no job for the document (e.g. legacy PDFs).
 */
export const IngestionLogSchema = z.object({
  id: z.string().uuid(),
  pdfDocumentId: z.string().uuid(),
  pdfFileName: z.string(),
  userId: z.string().uuid(),
  status: z.string(),
  priority: z.number(),
  currentStep: z.string().nullable(),
  createdAt: z.string().datetime({ offset: true }),
  startedAt: z.string().datetime({ offset: true }).nullable(),
  completedAt: z.string().datetime({ offset: true }).nullable(),
  errorMessage: z.string().nullable(),
  retryCount: z.number().int().nonnegative(),
  maxRetries: z.number().int().positive(),
  canRetry: z.boolean(),
  steps: z.array(IngestionStepSchema),
});
export type IngestionLog = z.infer<typeof IngestionLogSchema>;

export const IngestionLogResponseSchema = IngestionLogSchema.nullable();
