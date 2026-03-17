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
  createdAt: z.string().datetime(),
  model: z.string().nullable(),
  finishReason: z.string().nullable(),
});

export type AiRequest = z.infer<typeof AiRequestSchema>;

export const AiRequestsResponseSchema = z.object({
  requests: z.array(AiRequestSchema),
  totalCount: z.number(),
});

export type AiRequestsResponse = z.infer<typeof AiRequestsResponseSchema>;

// ========== Prompt Version Activation ==========

export const ActivateVersionResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type ActivateVersionResponse = z.infer<typeof ActivateVersionResponseSchema>;
