/**
 * Admin Knowledge Base Schemas (Issues #4784, #4789)
 *
 * Zod schemas for admin PDF management, vector collections,
 * processing queue, and storage health endpoints.
 */

import { z } from 'zod';

// ========== Bulk Delete ==========

export const BulkDeleteItemResultSchema = z.object({
  pdfId: z.string(),
  success: z.boolean(),
  error: z.string().nullable(),
});

export const BulkDeleteResultSchema = z.object({
  totalRequested: z.number(),
  successCount: z.number(),
  failedCount: z.number(),
  items: z.array(BulkDeleteItemResultSchema),
});

export type BulkDeleteResult = z.infer<typeof BulkDeleteResultSchema>;

// ========== Reindex Response ==========

export const ReindexResponseSchema = z.object({
  success: z.boolean(),
  message: z.string(),
});

export type ReindexResponse = z.infer<typeof ReindexResponseSchema>;

// ========== Purge / Cleanup ==========

export const MaintenanceResultSchema = z.object({
  affected: z.number().optional(),
  message: z.string().optional(),
}).passthrough();

export type MaintenanceResult = z.infer<typeof MaintenanceResultSchema>;

// ========== PDF Status Distribution ==========

export const PdfSizeRankItemSchema = z.object({
  id: z.string(),
  fileName: z.string(),
  fileSizeBytes: z.number(),
});

export const PdfStatusDistributionSchema = z.object({
  countByState: z.record(z.string(), z.number()),
  totalDocuments: z.number(),
  topBySize: z.array(PdfSizeRankItemSchema),
});

export type PdfStatusDistribution = z.infer<typeof PdfStatusDistributionSchema>;

// ========== PDF Storage Health ==========

export const PostgresInfoSchema = z.object({
  totalDocuments: z.number(),
  totalChunks: z.number(),
  estimatedChunksSizeMB: z.number(),
});

export const QdrantInfoSchema = z.object({
  vectorCount: z.number(),
  memoryBytes: z.number(),
  memoryFormatted: z.string(),
  isAvailable: z.boolean(),
});

export const FileStorageInfoSchema = z.object({
  totalFiles: z.number(),
  totalSizeBytes: z.number(),
  totalSizeFormatted: z.string(),
  sizeByState: z.record(z.string(), z.number()),
});

export const PdfStorageHealthSchema = z.object({
  postgres: PostgresInfoSchema,
  qdrant: QdrantInfoSchema,
  fileStorage: FileStorageInfoSchema,
  overallHealth: z.string(),
  measuredAt: z.string(),
});

export type PdfStorageHealth = z.infer<typeof PdfStorageHealthSchema>;

// ========== PDF Processing Metrics ==========

export const StepAveragesSchema = z.object({
  step: z.string(),
  avgDuration: z.number(),
  sampleSize: z.number(),
});

export const StepPercentilesSchema = z.object({
  p50: z.number(),
  p95: z.number(),
  p99: z.number(),
});

export const ProcessingMetricsSchema = z.object({
  averages: z.record(z.string(), StepAveragesSchema),
  percentiles: z.record(z.string(), StepPercentilesSchema),
  lastUpdated: z.string(),
});

export type ProcessingMetrics = z.infer<typeof ProcessingMetricsSchema>;

// ========== Vector Collections ==========

export const VectorCollectionSchema = z.object({
  name: z.string(),
  vectorCount: z.number(),
  dimensions: z.number(),
  storage: z.string(),
  health: z.number(),
});

export const VectorCollectionsResponseSchema = z.object({
  collections: z.array(VectorCollectionSchema),
});

export type VectorCollection = z.infer<typeof VectorCollectionSchema>;
export type VectorCollectionsResponse = z.infer<typeof VectorCollectionsResponseSchema>;

// ========== Processing Queue (Admin) ==========

export const ProcessingJobDtoSchema = z.object({
  id: z.string(),
  pdfDocumentId: z.string(),
  pdfFileName: z.string(),
  userId: z.string(),
  status: z.string(),
  priority: z.number(),
  currentStep: z.string().nullable(),
  createdAt: z.string(),
  startedAt: z.string().nullable(),
  completedAt: z.string().nullable(),
  errorMessage: z.string().nullable(),
  retryCount: z.number(),
  maxRetries: z.number(),
  canRetry: z.boolean(),
});

export const ProcessingQueueResponseSchema = z.object({
  jobs: z.array(ProcessingJobDtoSchema),
  total: z.number(),
  page: z.number(),
  pageSize: z.number(),
  totalPages: z.number(),
});

export type ProcessingJobDto = z.infer<typeof ProcessingJobDtoSchema>;
export type ProcessingQueueResponse = z.infer<typeof ProcessingQueueResponseSchema>;
