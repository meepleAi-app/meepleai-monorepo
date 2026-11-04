/**
 * Admin System Configuration Schemas
 *
 * Batch jobs, processing queue, emergency controls, and audit logs.
 */

import { z } from 'zod';

// ========== Batch Jobs (Issue #3693) ==========

// I valori vengono da `JobType` (Administration/Domain/Enums/JobType.cs), serializzato
// come stringa da JsonStringEnumConverter. VectorReembedding mancava: un job di quel tipo
// faceva fallire la validazione dell'intera risposta.
export const BatchJobTypeSchema = z.enum([
  'ResourceForecast',
  'CostAnalysis',
  'DataCleanup',
  'BggSync',
  'AgentBenchmark',
  'VectorReembedding',
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

/**
 * #3853 — allineato campo per campo a `BatchJobDto`
 * (Administration/Application/DTOs/BatchJobDtos.cs), che il query handler costruisce
 * posizionalmente da `job.*`:
 *
 *   Id · Type · Status · Progress · StartedAt · CompletedAt · DurationSeconds ·
 *   ResultSummary · ErrorMessage · CreatedAt
 *
 * Lo schema pretendeva `parameters`, `results` e `duration`. `parameters` e `results`
 * non esistono nel contratto; `duration` si chiama `durationSeconds`. Essendo
 * `.nullable()` e non `.optional()`, una chiave assente fa fallire la validazione: il
 * client scartava una risposta valida.
 *
 * Non si vedeva perché il solo chiamante (`use-infrastructure-kpis`) chiede
 * `?status=queued&pageSize=1` e legge `total`: con la coda vuota `jobs` è `[]` e lo
 * schema dell'elemento non viene mai esercitato. Il KPI funzionava finché non c'era
 * nulla da mostrare, e si rompeva esattamente quando qualcosa c'era.
 */
export const BatchJobDtoSchema = z.object({
  id: z.string().uuid(),
  type: BatchJobTypeSchema,
  status: BatchJobStatusSchema,
  progress: z.number().min(0).max(100),
  startedAt: z.string().datetime({ offset: true }).nullable(),
  completedAt: z.string().datetime({ offset: true }).nullable(),
  durationSeconds: z.number().int().nullable(),
  resultSummary: z.string().nullable(),
  errorMessage: z.string().nullable(),
  createdAt: z.string().datetime({ offset: true }),
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

// #3853 — il backend restituisce `CreateBatchJobResponse(Guid JobId)`, non `id`.
// Allineato lo schema invece di rinominare il campo lato backend: qui il frontend non
// aveva ragione su un dato mancante, aveva usato un nome diverso da quello servito, e
// una rinomina di contratto e' una rottura a fronte di un beneficio solo estetico.
export const CreateBatchJobResponseSchema = z.object({
  jobId: z.string().uuid(),
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
  createdAt: z.string().datetime({ offset: true }),
  startedAt: z.string().datetime({ offset: true }).nullable(),
  completedAt: z.string().datetime({ offset: true }).nullable(),
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
  activatedAt: z.string().datetime({ offset: true }),
  expiresAt: z.string().datetime({ offset: true }),
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
  createdAt: z.string().datetime({ offset: true }),
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
