/**
 * AI Models Schemas (Issue #2521)
 *
 * Type-safe schemas for AI model management, configuration, and cost tracking.
 * Covers: Model CRUD, primary selection, usage statistics, cost monitoring
 */

import { z } from 'zod';

/**
 * AI Model Provider.
 *
 * #4093: il backend dichiara `public required string Provider` (AiModelDto.cs:16) e manda
 * etichette in PascalCase — osservate `"OpenRouter"` e `"Ollama"`. Un enum chiuso qui farebbe
 * fallire la lettura dell'intera lista al primo provider nuovo, che è il modo in cui questo
 * contratto si è rotto la prima volta. Resta una stringa; chi deve mostrarla normalizza.
 */
export const AiProviderSchema = z.string();

export type AiProvider = z.infer<typeof AiProviderSchema>;

/**
 * AI Model Status
 */
export const ModelStatusSchema = z.enum(['active', 'inactive', 'deprecated']);

export type ModelStatus = z.infer<typeof ModelStatusSchema>;

/**
 * Prezzi del modello — `ModelPricing` lato backend.
 *
 * #4093: le unità sono **per milione** di token. Lo schema precedente dichiarava
 * `inputCostPer1kTokens`, cioè per mille: un consumatore che l'avesse mostrato sarebbe stato
 * sbagliato di un fattore 1000. Non era un difetto osservabile solo perché la lista era sempre
 * vuota e quei campi non venivano mai valutati.
 */
export const ModelPricingSchema = z.object({
  inputPricePerMillion: z.number().min(0),
  outputPricePerMillion: z.number().min(0),
  currency: z.string(),
});

export type ModelPricing = z.infer<typeof ModelPricingSchema>;

/**
 * Impostazioni del modello — `ModelSettings` lato backend (ModelSettings.cs:9-11).
 * `temperature` e `maxTokens` sono annidati qui, non in radice.
 */
export const ModelSettingsSchema = z.object({
  maxTokens: z.number().int().min(0),
  temperature: z.number().min(0),
  pricing: ModelPricingSchema,
});

export type ModelSettings = z.infer<typeof ModelSettingsSchema>;

/**
 * Statistiche d'uso — `UsageStats` lato backend, campo `usage` (non `usageStats`).
 */
export const ModelUsageSchema = z.object({
  totalRequests: z.number().int().min(0),
  totalInputTokens: z.number().int().min(0),
  totalOutputTokens: z.number().int().min(0),
  totalTokensUsed: z.number().int().min(0),
  totalCostUsd: z.number().min(0),
  lastUsedAt: z.string().datetime({ offset: true }).nullable().optional(),
});

export type ModelUsage = z.infer<typeof ModelUsageSchema>;

/**
 * AI Model DTO — allineato a `AiModelDto.cs:13-31`.
 *
 * #4093: la versione precedente descriveva un'API mai implementata (nata in #2521 «Phase 1 — AI
 * models API foundation»): dichiarava `name`, `modelIdentifier`, `status`, `cost`, `temperature`,
 * `maxTokens`, `usageStats`, di cui il backend non manda nessuno. Coincidevano solo `id`,
 * `displayName`, `isPrimary`, `createdAt`, `updatedAt`, quindi la validazione respingeva ogni
 * risposta e la lista arrivava sempre vuota.
 */
export const AiModelDtoSchema = z.object({
  id: z.string().uuid(),
  modelId: z.string(),
  displayName: z.string(),
  provider: AiProviderSchema,
  priority: z.number().int(),
  isActive: z.boolean(),
  isPrimary: z.boolean(),
  createdAt: z.string().datetime({ offset: true }),
  updatedAt: z.string().datetime({ offset: true }).nullable().optional(),
  settings: ModelSettingsSchema,
  usage: ModelUsageSchema,
});

export type AiModelDto = z.infer<typeof AiModelDtoSchema>;

/**
 * Risposta paginata — chiavi `models`/`totalCount`, non `items`/`total`
 * (GetAllAiModelsQueryHandler.cs:54-57).
 */
export const PagedAiModelsSchema = z.object({
  models: z.array(AiModelDtoSchema),
  totalCount: z.number().int().min(0),
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
  lastUpdatedAt: z.string().datetime({ offset: true }),
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
