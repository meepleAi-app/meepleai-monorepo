/**
 * AI Models Schemas (Issue #2521)
 *
 * Type-safe schemas for AI model management, configuration, and cost tracking.
 * Covers: Model CRUD, primary selection, usage statistics, cost monitoring
 */

import { z } from 'zod';

/**
 * AI Model Provider
 */
export const AiProviderSchema = z.enum([
  'meta',
  'google',
  'anthropic',
  'deepseek',
  'openai',
  'openrouter',
]);

export type AiProvider = z.infer<typeof AiProviderSchema>;

/**
 * AI Model Status
 */
export const ModelStatusSchema = z.enum([
  'active',
  'inactive',
  'deprecated',
]);

export type ModelStatus = z.infer<typeof ModelStatusSchema>;

/**
 * Cost Information
 */
export const ModelCostSchema = z.object({
  inputCostPer1kTokens: z.number().min(0),
  outputCostPer1kTokens: z.number().min(0),
  currency: z.string().default('USD'),
});

export type ModelCost = z.infer<typeof ModelCostSchema>;

/**
 * Model Usage Statistics
 */
export const ModelUsageStatsSchema = z.object({
  totalRequests: z.number().int().min(0),
  totalInputTokens: z.number().int().min(0),
  totalOutputTokens: z.number().int().min(0),
  estimatedCost: z.number().min(0),
  lastUsedAt: z.string().datetime().nullable().optional(),
});

export type ModelUsageStats = z.infer<typeof ModelUsageStatsSchema>;

/**
 * AI Model DTO
 */
export const AiModelDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  displayName: z.string(),
  provider: AiProviderSchema,
  modelIdentifier: z.string(), // e.g., "google/gemini-pro"
  isPrimary: z.boolean(),
  status: ModelStatusSchema,
  cost: ModelCostSchema,
  temperature: z.number().min(0).max(2),
  maxTokens: z.number().int().min(512).max(8192),
  usageStats: ModelUsageStatsSchema.optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime().nullable().optional(),
});

export type AiModelDto = z.infer<typeof AiModelDtoSchema>;

/**
 * Paginated AI Models Response
 */
export const PagedAiModelsSchema = z.object({
  items: z.array(AiModelDtoSchema),
  total: z.number().int().min(0),
  page: z.number().int().min(1),
  pageSize: z.number().int().min(1),
});

export type PagedAiModels = z.infer<typeof PagedAiModelsSchema>;

/**
 * Configure Model Request
 */
export const ConfigureModelRequestSchema = z.object({
  temperature: z.number().min(0).max(2).optional(),
  maxTokens: z.number().int().min(512).max(8192).optional(),
});

export type ConfigureModelRequest = z.infer<typeof ConfigureModelRequestSchema>;

/**
 * Set Primary Model Request
 */
export const SetPrimaryModelRequestSchema = z.object({
  modelId: z.string().uuid(),
});

export type SetPrimaryModelRequest = z.infer<typeof SetPrimaryModelRequestSchema>;

/**
 * Cost Tracking DTO
 */
export const CostTrackingDtoSchema = z.object({
  today: z.object({
    totalCost: z.number().min(0),
    totalRequests: z.number().int().min(0),
    budgetLimit: z.number().min(0),
    percentageUsed: z.number().min(0).max(100),
  }),
  thisMonth: z.object({
    totalCost: z.number().min(0),
    totalRequests: z.number().int().min(0),
    budgetLimit: z.number().min(0),
    percentageUsed: z.number().min(0).max(100),
  }),
  budgetStatus: z.enum(['on_track', 'warning', 'critical', 'exceeded']),
  lastUpdatedAt: z.string().datetime(),
});

export type CostTrackingDto = z.infer<typeof CostTrackingDtoSchema>;

/**
 * Test Model Request
 */
export const TestModelRequestSchema = z.object({
  modelId: z.string().uuid(),
  testPrompt: z.string().min(1).max(500).default('Explain quantum computing in one sentence.'),
});

export type TestModelRequest = z.infer<typeof TestModelRequestSchema>;

/**
 * Test Model Response
 */
export const TestModelResponseSchema = z.object({
  response: z.string(),
  responseTimeMs: z.number().int().min(0),
  inputTokens: z.number().int().min(0),
  outputTokens: z.number().int().min(0),
  estimatedCost: z.number().min(0),
});

export type TestModelResponse = z.infer<typeof TestModelResponseSchema>;

/**
 * Usage Report Export Params
 */
export interface ExportUsageReportParams {
  modelId?: string;
  startDate?: string;
  endDate?: string;
  format: 'csv' | 'json';
}

/**
 * Budget Alert Thresholds
 */
export const BUDGET_ALERT_THRESHOLDS = {
  warning: 80,
  critical: 95,
  exceeded: 100,
} as const;

/**
 * Default Model Configuration
 */
export const DEFAULT_MODEL_CONFIG = {
  temperature: 0.7,
  maxTokens: 4096,
} as const;
