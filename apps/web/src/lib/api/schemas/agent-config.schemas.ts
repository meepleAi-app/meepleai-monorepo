/**
 * Agent Configuration Schemas (Issue #2518)
 *
 * Type-safe schemas for per-game AI agent customization.
 * Covers: Model selection, parameters, personality, detail level, custom instructions
 */

import { z } from 'zod';

/**
 * AI Model Types
 * Default: llama-3.3-70b-free (gratis)
 */
export const AIModelSchema = z.enum([
  'llama-3.3-70b-free', // Free (Gratis) ⭐ Default
  'google-gemini-pro', // €€ - Best quality
  'deepseek-chat', // € - Affordable
  'llama-3.3-70b', // € - Balanced
]);

export type AIModel = z.infer<typeof AIModelSchema>;

/**
 * Agent Personality Types
 */
export const AgentPersonalitySchema = z.enum([
  'formal', // Professional tone, detailed responses
  'friendly', // Casual tone, practical examples
  'expert', // Technical tone, advanced references
]);

export type AgentPersonality = z.infer<typeof AgentPersonalitySchema>;

/**
 * Response Detail Level
 */
export const DetailLevelSchema = z.enum([
  'brief', // Short responses (1-2 sentences)
  'normal', // Balanced responses (2-4 sentences)
  'detailed', // Complete responses with examples
]);

export type DetailLevel = z.infer<typeof DetailLevelSchema>;

/**
 * Agent Configuration Request (User -> Backend)
 */
export const UpdateAgentConfigRequestSchema = z.object({
  modelType: AIModelSchema.default('llama-3.3-70b-free'),
  temperature: z.number().min(0).max(2).default(0.7),
  maxTokens: z.number().min(512).max(8192).default(4096),
  personality: AgentPersonalitySchema.default('friendly'),
  detailLevel: DetailLevelSchema.default('normal'),
  customInstructions: z.string().max(1000).nullable().optional(),
});

export type UpdateAgentConfigRequest = z.infer<typeof UpdateAgentConfigRequestSchema>;

/**
 * Agent Configuration Response (Backend -> User)
 */
export const AgentConfigDtoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  gameId: z.string().uuid(),
  modelType: AIModelSchema,
  temperature: z.number(),
  maxTokens: z.number(),
  personality: AgentPersonalitySchema,
  detailLevel: DetailLevelSchema,
  customInstructions: z.string().nullable().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime().nullable().optional(),
});

export type AgentConfigDto = z.infer<typeof AgentConfigDtoSchema>;

/**
 * Default Agent Configuration (fallback values)
 */
export const DEFAULT_AGENT_CONFIG: Omit<UpdateAgentConfigRequest, 'customInstructions'> = {
  modelType: 'llama-3.3-70b-free',
  temperature: 0.7,
  maxTokens: 4096,
  personality: 'friendly',
  detailLevel: 'normal',
};

/**
 * Model Display Information
 */
export interface ModelInfo {
  value: AIModel;
  label: string;
  costLevel: string;
  description: string;
  isDefault?: boolean;
}

export const MODEL_OPTIONS: ModelInfo[] = [
  {
    value: 'llama-3.3-70b-free',
    label: 'Llama 3.3 70B Free',
    costLevel: 'Gratis',
    description: 'Default model - balanced performance',
    isDefault: true,
  },
  {
    value: 'google-gemini-pro',
    label: 'Google Gemini Pro',
    costLevel: '€€',
    description: 'Best quality responses',
  },
  {
    value: 'deepseek-chat',
    label: 'DeepSeek Chat',
    costLevel: '€',
    description: 'Cost-effective option',
  },
  {
    value: 'llama-3.3-70b',
    label: 'Llama 3.3 70B',
    costLevel: '€',
    description: 'Balanced quality and cost',
  },
];

// ============================================================================
// Backend Model DTOs (from GET /api/v1/models and PATCH /api/v1/agents/:id/configuration)
// ============================================================================

/** ModelDto from GET /api/v1/models?tier= */
export interface BackendModelDto {
  id: string;
  name: string;
  provider: string;
  tier: string;
  costPer1kInputTokens: number;
  costPer1kOutputTokens: number;
  maxTokens: number;
  supportsStreaming: boolean;
  description?: string;
}

/** Response from GET /api/v1/models */
export interface GetModelsResponse {
  models: BackendModelDto[];
}

/** AgentConfigurationDto from PATCH/GET /api/v1/agents/:id/configuration */
export interface BackendAgentConfigurationDto {
  id: string;
  agentId: string;
  llmModel: string;
  llmProvider: string;
  temperature: number;
  maxTokens: number;
  selectedDocumentIds: string[];
  isCurrent: boolean;
  createdAt: string;
}

/** Request body for PATCH /api/v1/agents/:id/configuration */
export interface UpdateAgentConfigurationRequest {
  modelId?: string;
  temperature?: number;
  maxTokens?: number;
  selectedDocumentIds?: string[];
}

/**
 * Personality Display Information
 */
export interface PersonalityInfo {
  value: AgentPersonality;
  label: string;
  description: string;
}

export const PERSONALITY_OPTIONS: PersonalityInfo[] = [
  {
    value: 'formal',
    label: 'Formale',
    description: 'Tono professionale, risposte dettagliate',
  },
  {
    value: 'friendly',
    label: 'Amichevole',
    description: 'Tono casual, esempi pratici',
  },
  {
    value: 'expert',
    label: 'Esperto',
    description: 'Tono tecnico, riferimenti avanzati',
  },
];

/**
 * Detail Level Display Information
 */
export interface DetailLevelInfo {
  value: DetailLevel;
  label: string;
  description: string;
}

export const DETAIL_LEVEL_OPTIONS: DetailLevelInfo[] = [
  {
    value: 'brief',
    label: 'Sintetico',
    description: 'Risposte brevi (1-2 frasi)',
  },
  {
    value: 'normal',
    label: 'Normale',
    description: 'Risposte equilibrate (2-4 frasi)',
  },
  {
    value: 'detailed',
    label: 'Dettagliato',
    description: 'Risposte complete con esempi',
  },
];
