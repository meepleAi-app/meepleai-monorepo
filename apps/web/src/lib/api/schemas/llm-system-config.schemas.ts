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
  lastUpdatedAt: z.string().datetime({ offset: true }).nullable(),
  lastUpdatedByUserId: z.string().uuid().nullable(),
});

export type LlmSystemConfigDto = z.infer<typeof LlmSystemConfigDtoSchema>;

/**
 * #1834 PR2 — Fallback chain entry schema.
 *
 * FE-only runtime guard for parsing `fallbackChainJson` (string).
 * The BE persists this as a raw JSON string with zero validation, so we
 * normalize known shape variants here. Issue tracker → schema rigido BE.
 *
 * Tolerated variants (best-effort):
 * - `provider` | `name` | `providerName` for provider key
 * - `model` | `defaultModel` | `modelName` for model key
 * - `priority` derived from index when absent (0=primary, 1=secondary, 2=tertiary)
 * - `failoverConditions` | `conditions` | absent (defaults to common conditions for primary,
 *   `circuit-open` for fallback nodes)
 */
export const FallbackChainEntrySchema = z.object({
  provider: z.string().min(1),
  model: z.string().min(1),
  priority: z.enum(['primary', 'secondary', 'tertiary', 'standby']),
  failoverConditions: z.array(z.string()).default([]),
});

export type FallbackChainEntry = z.infer<typeof FallbackChainEntrySchema>;

/**
 * Parse `fallbackChainJson` into a validated array of entries.
 * Returns empty array on invalid input.
 */
export function parseFallbackChain(json: string | undefined | null): FallbackChainEntry[] {
  if (!json) return [];
  let raw: unknown;
  try {
    raw = JSON.parse(json);
  } catch {
    return [];
  }
  if (!Array.isArray(raw)) return [];

  const result: FallbackChainEntry[] = [];
  raw.forEach((item, idx) => {
    if (typeof item !== 'object' || item === null) return;
    const obj = item as Record<string, unknown>;

    const providerKey = obj.provider ?? obj.name ?? obj.providerName;
    const modelKey = obj.model ?? obj.defaultModel ?? obj.modelName;
    const priorityRaw = obj.priority;
    const conditionsRaw = obj.failoverConditions ?? obj.conditions;

    const provider = typeof providerKey === 'string' ? providerKey : null;
    const model = typeof modelKey === 'string' ? modelKey : null;
    if (!provider || !model) return;

    const priorityCandidate =
      typeof priorityRaw === 'string'
        ? priorityRaw.toLowerCase()
        : idx === 0
          ? 'primary'
          : idx === 1
            ? 'secondary'
            : idx === 2
              ? 'tertiary'
              : 'standby';
    const priority = (['primary', 'secondary', 'tertiary', 'standby'] as const).includes(
      priorityCandidate as FallbackChainEntry['priority']
    )
      ? (priorityCandidate as FallbackChainEntry['priority'])
      : 'standby';

    const failoverConditions = Array.isArray(conditionsRaw)
      ? conditionsRaw.filter((c): c is string => typeof c === 'string')
      : idx === 0
        ? ['429', '5xx', 'timeout']
        : ['circuit-open'];

    const parsed = FallbackChainEntrySchema.safeParse({
      provider,
      model,
      priority,
      failoverConditions,
    });
    if (parsed.success) result.push(parsed.data);
  });
  return result;
}

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
