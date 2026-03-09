/**
 * LLM System Configuration Schemas (Issue #5495)
 *
 * Type-safe schemas for the unified LLM configuration dashboard.
 * Covers: circuit breaker, budget limits, fallback chain, source tracking.
 */

import { z } from 'zod';

/**
 * LLM System Config DTO — matches backend LlmSystemConfigDto
 */
export const LlmSystemConfigDtoSchema = z.object({
  circuitBreakerFailureThreshold: z.number().int().min(1).max(100),
  circuitBreakerOpenDurationSeconds: z.number().int().min(1).max(3600),
  circuitBreakerSuccessThreshold: z.number().int().min(1).max(100),
  dailyBudgetUsd: z.number().min(0),
  monthlyBudgetUsd: z.number().min(0),
  fallbackChainJson: z.string(),
  source: z.enum(['database', 'appsettings']),
  lastUpdatedAt: z.string().datetime().nullable(),
  lastUpdatedByUserId: z.string().uuid().nullable(),
});

export type LlmSystemConfigDto = z.infer<typeof LlmSystemConfigDtoSchema>;

/**
 * Update LLM System Config Request
 */
export interface UpdateLlmSystemConfigRequest {
  circuitBreakerFailureThreshold: number;
  circuitBreakerOpenDurationSeconds: number;
  circuitBreakerSuccessThreshold: number;
  dailyBudgetUsd: number;
  monthlyBudgetUsd: number;
  fallbackChainJson: string;
}
