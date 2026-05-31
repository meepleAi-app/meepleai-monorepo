/**
 * Admin AI Request Log Schemas
 *
 * AI request logging/analytics schemas (NOT model configuration — that's in ai-models.schemas.ts).
 * Also includes prompt version activation response.
 */

import { z } from 'zod';

// ========== AI Request Logs & Stats ==========

export const AiRequestSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid().nullable(),
  gameId: z.string().uuid().nullable(),
  endpoint: z.string(),
  query: z.string().nullable(),
  responseSnippet: z.string().nullable(),
  latencyMs: z.number(),
  tokenCount: z.number(),
  promptTokens: z.number(),
  completionTokens: z.number(),
  confidence: z.number().nullable(),
  status: z.string(),
  errorMessage: z.string().nullable(),
  ipAddress: z.string().nullable(),
  userAgent: z.string().nullable(),
  createdAt: z.string().datetime({ offset: true }),
  model: z.string().nullable(),
  finishReason: z.string().nullable(),
});

export type AiRequest = z.infer<typeof AiRequestSchema>;

export const AiRequestsResponseSchema = z.object({
  requests: z.array(AiRequestSchema),
  totalCount: z.number(),
});

export type AiRequestsResponse = z.infer<typeof AiRequestsResponseSchema>;

// ========== AI Query Drill (#1728) ==========

export const RetrievedChunkDtoSchema = z.object({
  id: z.string(),
  score: z.number(),
  text: z.string(),
  page: z.number(),
  chunkIndex: z.number(),
  pdfName: z.string(),
  used: z.boolean(),
});

export type RetrievedChunkDto = z.infer<typeof RetrievedChunkDtoSchema>;

export const LatencyBreakdownDtoSchema = z.object({
  retrievalMs: z.number(),
  rerankMs: z.number(),
  llmMs: z.number(),
  postMs: z.number(),
});

export type LatencyBreakdownDto = z.infer<typeof LatencyBreakdownDtoSchema>;

export const AiQueryDrillResponseSchema = z.object({
  request: AiRequestSchema,
  chunks: z.array(RetrievedChunkDtoSchema),
  breakdown: LatencyBreakdownDtoSchema.nullable(),
});

export type AiQueryDrillResponse = z.infer<typeof AiQueryDrillResponseSchema>;

// ========== AI Metrics Trend (#1729) ==========

export const AiMetricsDatapointSchema = z.object({
  timestamp: z.string().datetime({ offset: true }),
  requestCount: z.number().int().nonnegative(),
  avgLatencyMs: z.number().int().nonnegative(),
  p50LatencyMs: z.number().int().nonnegative(),
  p95LatencyMs: z.number().int().nonnegative(),
  errorRate: z.number().min(0).max(1),
});

export type AiMetricsDatapoint = z.infer<typeof AiMetricsDatapointSchema>;

export const AiMetricsTrendResponseSchema = z.object({
  range: z.string(),
  bucketSize: z.string(),
  datapoints: z.array(AiMetricsDatapointSchema),
});

export type AiMetricsTrendResponse = z.infer<typeof AiMetricsTrendResponseSchema>;

// ========== Prompt Version Activation ==========

export const ActivateVersionResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type ActivateVersionResponse = z.infer<typeof ActivateVersionResponseSchema>;
