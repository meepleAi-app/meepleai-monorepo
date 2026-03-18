/**
 * Admin System Configuration Schemas
 *
 * N8N configuration, batch jobs, processing queue, emergency controls, and audit logs.
 */

import { z } from 'zod';

// ========== N8N Configuration (Issue #60) ==========

export const N8nConfigDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  baseUrl: z.string(),
  webhookUrl: z.string().nullable().optional(),
  isActive: z.boolean(),
  lastTestedAt: z.string().datetime().nullable().optional(),
  lastTestResult: z.string().nullable().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
});

export type N8nConfigDto = z.infer<typeof N8nConfigDtoSchema>;

export const N8nTestResultDtoSchema = z.object({
  success: z.boolean(),
  message: z.string(),
  latencyMs: z.number().nullable().optional(),
});

export type N8nTestResultDto = z.infer<typeof N8nTestResultDtoSchema>;

// ========== Batch Jobs (Issue #3693) ==========

export const BatchJobTypeSchema = z.enum([
  'ResourceForecast',
  'CostAnalysis',
  'DataCleanup',
  'BggSync',
  'AgentBenchmark',
]);
export type BatchJobType = z.infer<typeof BatchJobTypeSchema>;

export const BatchJobStatusSchema = z.enum([
  'Queued',
  'Running',
  'Completed',
  'Failed',
  'Cancelled',
]);
export type BatchJobStatus = z.infer<typeof BatchJobStatusSchema>;

export const BatchJobDtoSchema = z.object({
  id: z.string().uuid(),
  type: BatchJobTypeSchema,
  status: BatchJobStatusSchema,
  parameters: z.record(z.string(), z.any()).nullable(),
  results: z.record(z.string(), z.any()).nullable(),
  errorMessage: z.string().nullable(),
  progress: z.number().min(0).max(100),
  createdAt: z.string().datetime(),
  startedAt: z.string().datetime().nullable(),
  completedAt: z.string().datetime().nullable(),
  duration: z.number().nullable(),
});
export type BatchJobDto = z.infer<typeof BatchJobDtoSchema>;

export const BatchJobListSchema = z.object({
  jobs: z.array(BatchJobDtoSchema),
  total: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
});
export type BatchJobList = z.infer<typeof BatchJobListSchema>;

export const CreateBatchJobRequestSchema = z.object({
  type: BatchJobTypeSchema,
  parameters: z.record(z.string(), z.any()).optional(),
});
export type CreateBatchJobRequest = z.infer<typeof CreateBatchJobRequestSchema>;

export const CreateBatchJobResponseSchema = z.object({
  id: z.string().uuid(),
});
export type CreateBatchJobResponse = z.infer<typeof CreateBatchJobResponseSchema>;

// ========== Processing Queue (Issue #125) ==========

export const ProcessingJobSchema = z.object({
  id: z.string().uuid(),
  pdfDocumentId: z.string().uuid(),
  pdfFileName: z.string(),
  userId: z.string().uuid(),
  status: z.string(),
  priority: z.number(),
  currentStep: z.string().nullable(),
  createdAt: z.string().datetime(),
  startedAt: z.string().datetime().nullable(),
  completedAt: z.string().datetime().nullable(),
  errorMessage: z.string().nullable(),
  retryCount: z.number(),
  maxRetries: z.number(),
  canRetry: z.boolean(),
});
export type ProcessingJob = z.infer<typeof ProcessingJobSchema>;

export const PaginatedQueueSchema = z.object({
  jobs: z.array(ProcessingJobSchema),
  total: z.number(),
  page: z.number(),
  pageSize: z.number(),
  totalPages: z.number(),
});
export type PaginatedQueue = z.infer<typeof PaginatedQueueSchema>;

export const QueueStatusSchema = z.object({
  queueDepth: z.number(),
  backpressureThreshold: z.number(),
  isUnderPressure: z.boolean(),
  isPaused: z.boolean(),
  maxConcurrentWorkers: z.number(),
  estimatedWaitMinutes: z.number(),
});
export type QueueStatus = z.infer<typeof QueueStatusSchema>;

// ========== Emergency Controls (Issue #125) ==========

export const ActiveOverrideSchema = z.object({
  action: z.string(),
  reason: z.string(),
  adminUserId: z.string().uuid(),
  targetProvider: z.string().nullable(),
  activatedAt: z.string().datetime(),
  expiresAt: z.string().datetime(),
  remainingMinutes: z.number(),
});
export type ActiveOverride = z.infer<typeof ActiveOverrideSchema>;

// ========== Audit Log (Issue #3691) ==========

export const AuditLogEntrySchema = z.object({
  id: z.string().uuid(),
  adminUserId: z.string().uuid().nullable(),
  action: z.string(),
  resource: z.string(),
  resourceId: z.string().nullable().optional(),
  result: z.string(),
  details: z.string().nullable().optional(),
  ipAddress: z.string().nullable().optional(),
  createdAt: z.string().datetime(),
  userName: z.string().nullable().optional(),
  userEmail: z.string().nullable().optional(),
});
export type AuditLogEntry = z.infer<typeof AuditLogEntrySchema>;

export const AuditLogListResultSchema = z.object({
  entries: z.array(AuditLogEntrySchema),
  totalCount: z.number().int().nonnegative(),
  limit: z.number().int().positive(),
  offset: z.number().int().nonnegative(),
});
export type AuditLogListResult = z.infer<typeof AuditLogListResultSchema>;
