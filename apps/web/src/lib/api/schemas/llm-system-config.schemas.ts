/**
 * LLM System Configuration Schemas (Issue #5495)
 *
 * Type-safe schemas for the admin «Budget» tab: budget caps, degrade threshold and quotas
 * (#4166, ADR-095 D6), and the spend the LLM gateway counts against them (#4169).
 *
 * #4169: the fallback chain is gone from the FE. The runtime never read `fallbackChainJson` (the real
 * fallback order comes from the model catalog), so the page no longer edits it nor draws it.
 */

import { z } from 'zod';

/**
 * LLM System Config DTO — matches backend LlmSystemConfigDto.
 *
 * The circuit breaker thresholds are still returned (CircuitBreakerGrid shows the open duration) but
 * no page edits them: the runtime takes them from `AI:CircuitBreaker` in appsettings (#4169).
 */
export const LlmSystemConfigDtoSchema = z.object({
  circuitBreakerFailureThreshold: z.number().int().min(1).max(100),
  circuitBreakerOpenDurationSeconds: z.number().int().min(1).max(3600),
  circuitBreakerSuccessThreshold: z.number().int().min(1).max(100),
  dailyBudgetUsd: z.number().min(0),
  monthlyBudgetUsd: z.number().min(0),
  degradeThresholdPercent: z.number().int().min(1).max(100),
  backgroundQuotaPercent: z.number().int().min(0).max(100),
  evaluationQuotaPercent: z.number().int().min(0).max(100),
  source: z.enum(['database', 'appsettings']),
  lastUpdatedAt: z.string().datetime({ offset: true }).nullable(),
  lastUpdatedByUserId: z.string().uuid().nullable(),
});

export type LlmSystemConfigDto = z.infer<typeof LlmSystemConfigDtoSchema>;

/**
 * Update LLM System Config Request — what the gateway applies, and only that (#4169).
 */
export interface UpdateLlmSystemConfigRequest {
  dailyBudgetUsd: number;
  monthlyBudgetUsd: number;
  degradeThresholdPercent: number;
  backgroundQuotaPercent: number;
  evaluationQuotaPercent: number;
}

/**
 * #4169 — a spend in USD, in total and per budget class (answers, background, evaluations).
 */
export const LlmBudgetSpendSchema = z.object({
  totalUsd: z.number(),
  answersUsd: z.number(),
  backgroundUsd: z.number(),
  evaluationsUsd: z.number(),
});

export type LlmBudgetSpend = z.infer<typeof LlmBudgetSpendSchema>;

/**
 * #4169 — GET /api/v1/admin/llm/budget/usage: the spend the LLM gateway counts today and this month
 * (cost log plus calls still open), and whether the degrade is active. `available` is false when the
 * spend cannot be read: the gateway then refuses paid calls.
 */
export const LlmBudgetUsageSchema = z.object({
  available: z.boolean(),
  day: z.string().nullable(),
  today: LlmBudgetSpendSchema.nullable(),
  month: LlmBudgetSpendSchema.nullable(),
  isDegraded: z.boolean(),
});

export type LlmBudgetUsage = z.infer<typeof LlmBudgetUsageSchema>;
