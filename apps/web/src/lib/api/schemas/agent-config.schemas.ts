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
 *
 * Canonical values are the Italian labels enforced by the backend
 * ConfigureGameAgentCommandValidator (`ValidPersonalities`). The FE contract
 * mirrors the BE exactly — sending legacy English values yields HTTP 400.
 */
export const AgentPersonalitySchema = z.enum([
  'Amichevole', // Casual tone, practical examples
  'Professionale', // Professional, formal tone
  'Umoristico', // Light, witty tone
  'Conciso', // Short, direct answers
  'Dettagliato', // In-depth, complete answers
]);

export type AgentPersonality = z.infer<typeof AgentPersonalitySchema>;

/**
 * Response Detail Level
 *
 * Canonical values are the Italian labels enforced by the backend
 * ConfigureGameAgentCommandValidator (`ValidDetailLevels`).
 */
export const DetailLevelSchema = z.enum([
  'Breve', // Short responses (1-2 sentences)
  'Normale', // Balanced responses (2-4 sentences)
  'Dettagliato', // Complete responses with examples
  'Esaustivo', // Very thorough responses
]);

export type DetailLevel = z.infer<typeof DetailLevelSchema>;

/**
 * Agent Configuration Request (User -> Backend)
 *
 * Field names mirror the backend `AgentConfigDto` record
 * (LlmModel / PersonalNotes) — camelCased on the wire.
 */
export const UpdateAgentConfigRequestSchema = z.object({
  llmModel: AIModelSchema.default('llama-3.3-70b-free'),
  temperature: z.number().min(0).max(2).default(0.7),
  maxTokens: z.number().min(512).max(8192).default(4096),
  personality: AgentPersonalitySchema.default('Amichevole'),
  detailLevel: DetailLevelSchema.default('Normale'),
  personalNotes: z.string().max(1000).nullable().optional(),
});

export type UpdateAgentConfigRequest = z.infer<typeof UpdateAgentConfigRequestSchema>;

/**
 * Agent Configuration Response (Backend -> User)
 *
 * Mirrors the backend `AgentConfigDto` (UserLibrary bounded context): a flat
 * config record with no id/userId/gameId/timestamps. Returned by
 * GET /api/v1/library/games/{gameId}/agent-config (null when unconfigured).
 */
export const AgentConfigDtoSchema = z.object({
  llmModel: AIModelSchema,
  temperature: z.number(),
  maxTokens: z.number(),
  personality: AgentPersonalitySchema,
  detailLevel: DetailLevelSchema,
  personalNotes: z.string().nullable().optional(),
});

export type AgentConfigDto = z.infer<typeof AgentConfigDtoSchema>;

/**
 * Response wrapper for PUT /api/v1/library/games/{gameId}/agent-config.
 *
 * The backend endpoint returns the full UserLibraryEntryDto after configuring
 * the agent; only its `customAgentConfig` field carries the persisted config.
 * `.passthrough()` tolerates the remaining entry fields we don't consume here.
 */
export const UpdateAgentConfigResponseSchema = z
  .object({
    customAgentConfig: AgentConfigDtoSchema.nullable().optional(),
  })
  .passthrough();

export type UpdateAgentConfigResponse = z.infer<typeof UpdateAgentConfigResponseSchema>;

/**
 * Default Agent Configuration (fallback values)
 */
export const DEFAULT_AGENT_CONFIG: Omit<UpdateAgentConfigRequest, 'personalNotes'> = {
  llmModel: 'llama-3.3-70b-free',
  temperature: 0.7,
  maxTokens: 4096,
  personality: 'Amichevole',
  detailLevel: 'Normale',
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
    value: 'Amichevole',
    label: 'Amichevole',
    description: 'Tono casual, esempi pratici',
  },
  {
    value: 'Professionale',
    label: 'Professionale',
    description: 'Tono professionale e formale',
  },
  {
    value: 'Umoristico',
    label: 'Umoristico',
    description: 'Tono leggero e spiritoso',
  },
  {
    value: 'Conciso',
    label: 'Conciso',
    description: 'Risposte brevi e dirette',
  },
  {
    value: 'Dettagliato',
    label: 'Dettagliato',
    description: 'Risposte approfondite e complete',
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
    value: 'Breve',
    label: 'Breve',
    description: 'Risposte brevi (1-2 frasi)',
  },
  {
    value: 'Normale',
    label: 'Normale',
    description: 'Risposte equilibrate (2-4 frasi)',
  },
  {
    value: 'Dettagliato',
    label: 'Dettagliato',
    description: 'Risposte complete con esempi',
  },
  {
    value: 'Esaustivo',
    label: 'Esaustivo',
    description: 'Risposte molto approfondite',
  },
];
