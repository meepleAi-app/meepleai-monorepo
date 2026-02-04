/**
 * Tier-Strategy Configuration Schemas
 * Issue #3440: Admin UI for tier-strategy configuration
 */

import { z } from 'zod';

// ========================================
// Strategy Info
// ========================================

export const StrategyInfoSchema = z.object({
  name: z.string(),
  displayName: z.string(),
  description: z.string(),
  complexityLevel: z.number(),
  requiresAdmin: z.boolean(),
});

export type StrategyInfoDto = z.infer<typeof StrategyInfoSchema>;

// ========================================
// Tier-Strategy Access
// ========================================

export const TierStrategyAccessSchema = z.object({
  id: z.string().uuid().nullable(),
  tier: z.string(),
  strategy: z.string(),
  isEnabled: z.boolean(),
  isDefault: z.boolean(),
});

export type TierStrategyAccessDto = z.infer<typeof TierStrategyAccessSchema>;

// ========================================
// Tier-Strategy Matrix
// ========================================

export const TierStrategyMatrixSchema = z.object({
  tiers: z.array(z.string()),
  strategies: z.array(StrategyInfoSchema),
  accessMatrix: z.array(TierStrategyAccessSchema),
});

export type TierStrategyMatrixDto = z.infer<typeof TierStrategyMatrixSchema>;

// ========================================
// Strategy-Model Mapping
// ========================================

export const StrategyModelMappingSchema = z.object({
  id: z.string().uuid().nullable(),
  strategy: z.string(),
  provider: z.string(),
  primaryModel: z.string(),
  fallbackModels: z.array(z.string()),
  isCustomizable: z.boolean(),
  adminOnly: z.boolean(),
  isDefault: z.boolean(),
});

export type StrategyModelMappingDto = z.infer<typeof StrategyModelMappingSchema>;

// ========================================
// Request DTOs
// ========================================

export const UpdateTierStrategyAccessRequestSchema = z.object({
  tier: z.string().min(1),
  strategy: z.string().min(1),
  isEnabled: z.boolean(),
});

export type UpdateTierStrategyAccessRequest = z.infer<typeof UpdateTierStrategyAccessRequestSchema>;

export const UpdateStrategyModelMappingRequestSchema = z.object({
  strategy: z.string().min(1),
  provider: z.string().min(1),
  primaryModel: z.string().min(1),
  fallbackModels: z.array(z.string()).optional(),
});

export type UpdateStrategyModelMappingRequest = z.infer<typeof UpdateStrategyModelMappingRequestSchema>;

export const ResetTierStrategyConfigRequestSchema = z.object({
  resetAccessMatrix: z.boolean().default(true),
  resetModelMappings: z.boolean().default(true),
});

export type ResetTierStrategyConfigRequest = z.infer<typeof ResetTierStrategyConfigRequestSchema>;

// ========================================
// Reset Result
// ========================================

export const TierStrategyResetResultSchema = z.object({
  accessEntriesDeleted: z.number(),
  modelMappingsDeleted: z.number(),
  message: z.string(),
});

export type TierStrategyResetResultDto = z.infer<typeof TierStrategyResetResultSchema>;

// ========================================
// Constants
// ========================================

export const RAG_STRATEGIES = ['FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS', 'CUSTOM'] as const;
export type RagStrategy = (typeof RAG_STRATEGIES)[number];

export const TIER_STRATEGY_USER_TIERS = ['Anonymous', 'User', 'Editor', 'Admin'] as const;
export type TierStrategyUserTier = (typeof TIER_STRATEGY_USER_TIERS)[number];

export const LLM_PROVIDERS = ['OpenRouter', 'Anthropic', 'DeepSeek', 'Mixed', 'Ollama'] as const;
export type StrategyLlmProvider = (typeof LLM_PROVIDERS)[number];
