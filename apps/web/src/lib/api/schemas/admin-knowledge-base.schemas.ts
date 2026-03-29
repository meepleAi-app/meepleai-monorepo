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

export const MaintenanceResultSchema = z
  .object({
    affected: z.number().optional(),
    message: z.string().optional(),
  })
  .passthrough();

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

export const VectorStoreInfoSchema = z.object({
  vectorCount: z.number(),
  isAvailable: z.boolean(),
});

export type VectorStoreInfo = z.infer<typeof VectorStoreInfoSchema>;

export const FileStorageInfoSchema = z.object({
  totalFiles: z.number(),
  totalSizeBytes: z.number(),
  totalSizeFormatted: z.string(),
  sizeByState: z.record(z.string(), z.number()),
});

export const PdfStorageHealthSchema = z.object({
  postgres: PostgresInfoSchema,
  vectorStore: VectorStoreInfoSchema,
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

// ========== Vector Stats (pgvector) ==========

export const VectorGameBreakdownSchema = z.object({
  gameId: z.string(),
  gameName: z.string(),
  vectorCount: z.number(),
  completedCount: z.number(),
  failedCount: z.number(),
  healthPercent: z.number(),
});

export const VectorStatsSchema = z.object({
  totalVectors: z.number(),
  dimensions: z.number(),
  gamesIndexed: z.number(),
  avgHealthPercent: z.number(),
  sizeEstimateBytes: z.number(),
  gameBreakdown: z.array(VectorGameBreakdownSchema),
});

export const VectorSearchResultItemSchema = z.object({
  documentId: z.string(),
  text: z.string(),
  chunkIndex: z.number(),
  pageNumber: z.number(),
});

export const VectorSemanticSearchResultSchema = z.object({
  results: z.array(VectorSearchResultItemSchema),
  errorMessage: z.string().nullable(),
});

export type VectorGameBreakdown = z.infer<typeof VectorGameBreakdownSchema>;
export type VectorStats = z.infer<typeof VectorStatsSchema>;
export type VectorSearchResultItem = z.infer<typeof VectorSearchResultItemSchema>;
export type VectorSemanticSearchResult = z.infer<typeof VectorSemanticSearchResultSchema>;

// ========== PDF List (Admin) ==========

export const PdfListItemSchema = z.object({
  id: z.string(),
  fileName: z.string(),
  gameTitle: z.string().nullable(),
  gameId: z.string().nullable(),
  processingStatus: z.string().optional(),
  processingState: z.string(),
  progressPercentage: z.number(),
  fileSizeBytes: z.number(),
  pageCount: z.number().nullable(),
  chunkCount: z.number(),
  processingError: z.string().nullable(),
  errorCategory: z.string().nullable(),
  retryCount: z.number(),
  uploadedAt: z.string(),
  processedAt: z.string().nullable(),
});

export const PdfListResultSchema = z.object({
  items: z.array(PdfListItemSchema),
  total: z.number(),
  page: z.number(),
  pageSize: z.number(),
});

export type PdfListItem = z.infer<typeof PdfListItemSchema>;
export type PdfListResult = z.infer<typeof PdfListResultSchema>;

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

// ─── OpenRouter Status (Issue #5077) ─────────────────────────────────────────

export const OpenRouterStatusDtoSchema = z.object({
  balanceUsd: z.number(),
  dailySpendUsd: z.number(),
  todayRequestCount: z.number(),
  currentRpm: z.number(),
  limitRpm: z.number(),
  utilizationPercent: z.number(),
  isThrottled: z.boolean(),
  isFreeTier: z.boolean(),
  rateLimitInterval: z.string(),
  lastUpdated: z.string().nullable(),
});

export type OpenRouterStatusDto = z.infer<typeof OpenRouterStatusDtoSchema>;

// ─── Usage Timeline (Issue #5078) ────────────────────────────────────────────

export const TimelineBucketSchema = z.object({
  bucket: z.string(),
  manual: z.number(),
  ragPipeline: z.number(),
  eventDriven: z.number(),
  automatedTest: z.number(),
  agentTask: z.number(),
  adminOperation: z.number(),
  totalCostUsd: z.number(),
});

export const UsageTimelineDtoSchema = z.object({
  buckets: z.array(TimelineBucketSchema),
  period: z.string(),
  groupedByHour: z.boolean(),
  totalRequests: z.number(),
  totalCostUsd: z.number(),
});

export type UsageTimelineDto = z.infer<typeof UsageTimelineDtoSchema>;

// ─── Usage Costs (Issue #5080) ───────────────────────────────────────────────

export const UsageModelCostSchema = z.object({
  modelId: z.string(),
  costUsd: z.number(),
  requests: z.number(),
  totalTokens: z.number(),
});

export const UsageSourceCostSchema = z.object({
  source: z.string(),
  costUsd: z.number(),
  requests: z.number(),
});

export const UsageTierCostSchema = z.object({
  tier: z.string(),
  costUsd: z.number(),
  requests: z.number(),
});

export const UsageCostsDtoSchema = z.object({
  byModel: z.array(UsageModelCostSchema),
  bySource: z.array(UsageSourceCostSchema),
  byTier: z.array(UsageTierCostSchema),
  totalCostUsd: z.number(),
  totalRequests: z.number(),
  period: z.string(),
});

export type UsageCostsDto = z.infer<typeof UsageCostsDtoSchema>;

// ─── Free Quota (Issue #5082) ─────────────────────────────────────────────────

export const FreeModelUsageSchema = z.object({
  modelId: z.string(),
  requestsToday: z.number(),
  dailyLimit: z.number(),
  percentUsed: z.number(),
  isExhausted: z.boolean(),
  nextResetUtc: z.string().nullable(),
});

export const FreeQuotaDtoSchema = z.object({
  models: z.array(FreeModelUsageSchema),
  totalFreeRequestsToday: z.number(),
  generatedAt: z.string(),
});

export type FreeQuotaDto = z.infer<typeof FreeQuotaDtoSchema>;

// ─── Recent LLM Requests (Issue #5083) ───────────────────────────────────────

export const LlmRequestSummarySchema = z.object({
  id: z.string(),
  requestedAt: z.string(),
  modelId: z.string(),
  provider: z.string(),
  source: z.string(),
  userId: z.string().nullable(),
  userRole: z.string().nullable(),
  promptTokens: z.number(),
  completionTokens: z.number(),
  totalTokens: z.number(),
  costUsd: z.number(),
  latencyMs: z.number(),
  success: z.boolean(),
  errorMessage: z.string().nullable(),
  isStreaming: z.boolean(),
  isFreeModel: z.boolean(),
});

export const RecentLlmRequestsDtoSchema = z.object({
  items: z.array(LlmRequestSummarySchema),
  total: z.number(),
  page: z.number(),
  pageSize: z.number(),
  totalPages: z.number(),
});

export type RecentLlmRequestsDto = z.infer<typeof RecentLlmRequestsDtoSchema>;
