/**
 * ISSUE-3709: Agent Builder UI - Zod Validation Schemas
 * Schema definitions for Agent Definition creation and management
 */

import { z } from 'zod';

/**
 * Agent types supported by the platform
 */
export const AgentTypeSchema = z.enum([
  'RAG',
  'Citation',
  'Confidence',
  'RulesInterpreter',
  'Conversation',
  'Custom',
]);

export type AgentType = z.infer<typeof AgentTypeSchema>;

/**
 * Retrieval strategies for RAG agents
 */
export const StrategyNameSchema = z.enum([
  'HybridSearch',
  'VectorOnly',
  'KeywordOnly',
  'TemporalScoring',
  'CapabilityMatching',
]);

export type StrategyName = z.infer<typeof StrategyNameSchema>;

/**
 * Prompt template for agent system/user/assistant prompts
 */
export const PromptTemplateSchema = z.object({
  role: z.enum(['system', 'user', 'assistant']),
  content: z.string().min(1, 'Prompt content is required').max(10000, 'Prompt too long'),
});

export type PromptTemplate = z.infer<typeof PromptTemplateSchema>;

/**
 * Tool configuration for agent
 */
export const ToolConfigSchema = z.object({
  name: z.string().min(1),
  settings: z.record(z.string(), z.unknown()).optional(),
});

export type ToolConfig = z.infer<typeof ToolConfigSchema>;

/**
 * Strategy parameters (dynamic based on strategy type)
 */
export const StrategyParametersSchema = z.record(z.string(), z.unknown()).optional();

/**
 * Agent form data (for builder UI)
 */
export const AgentFormSchema = z.object({
  name: z
    .string()
    .min(1, 'Name is required')
    .max(100, 'Name must be 100 characters or less'),
  description: z.string().max(1000, 'Description must be 1000 characters or less').optional(),
  type: AgentTypeSchema,
  model: z.string().min(1, 'Model is required'),
  maxTokens: z.number().int().min(100, 'Min 100 tokens').max(32000, 'Max 32000 tokens'),
  temperature: z.number().min(0, 'Min temperature is 0').max(2, 'Max temperature is 2'),
  strategyName: z.string().optional(),
  strategyParameters: StrategyParametersSchema,
  prompts: z.array(PromptTemplateSchema).max(20, 'Maximum 20 prompts allowed'),
  tools: z.array(ToolConfigSchema).max(50, 'Maximum 50 tools allowed'),
});

export type AgentForm = z.infer<typeof AgentFormSchema>;

/**
 * Create agent definition request (API payload)
 */
export const CreateAgentDefinitionRequestSchema = z.object({
  name: z.string().min(1).max(100),
  description: z.string().max(1000).optional(),
  type: z.string(), // Sent as string to backend
  model: z.string(),
  maxTokens: z.number().int(),
  temperature: z.number(),
  strategyName: z.string().optional(),
  strategyParameters: z.record(z.string(), z.unknown()).optional(),
  prompts: z.array(PromptTemplateSchema).optional(),
  tools: z.array(ToolConfigSchema).optional(),
});

export type CreateAgentDefinitionRequest = z.infer<typeof CreateAgentDefinitionRequestSchema>;

/**
 * Update agent definition request
 */
export const UpdateAgentDefinitionRequestSchema = CreateAgentDefinitionRequestSchema;
export type UpdateAgentDefinitionRequest = z.infer<typeof UpdateAgentDefinitionRequestSchema>;

/**
 * Agent definition response from backend
 */
export const AgentDefinitionResponseSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  description: z.string().optional(),
  type: z.string(),
  model: z.string(),
  maxTokens: z.number(),
  temperature: z.number(),
  strategyName: z.string().optional(),
  strategyParameters: z.record(z.string(), z.unknown()).optional(),
  prompts: z.array(PromptTemplateSchema).optional(),
  tools: z.array(ToolConfigSchema).optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime().optional(),
});

export type AgentDefinitionResponse = z.infer<typeof AgentDefinitionResponseSchema>;

/**
 * Default agent form values
 */
export const defaultAgentForm: AgentForm = {
  name: '',
  description: '',
  type: 'RAG',
  model: 'gpt-4',
  maxTokens: 2048,
  temperature: 0.7,
  strategyName: 'HybridSearch',
  strategyParameters: { topK: 10, minScore: 0.55 },
  prompts: [
    {
      role: 'system',
      content: 'You are a helpful AI assistant for board game rules and strategy.',
    },
  ],
  tools: [],
};

/**
 * Available models
 */
export const AVAILABLE_MODELS = [
  { value: 'gpt-4', label: 'GPT-4 (OpenAI)' },
  { value: 'gpt-4-turbo', label: 'GPT-4 Turbo (OpenAI)' },
  { value: 'claude-3-5-sonnet', label: 'Claude 3.5 Sonnet (Anthropic)' },
  { value: 'claude-3-5-haiku', label: 'Claude 3.5 Haiku (Anthropic)' },
  { value: 'deepseek-chat', label: 'DeepSeek Chat' },
  { value: 'deepseek-reasoner', label: 'DeepSeek Reasoner' },
] as const;

/**
 * Available strategies with descriptions
 */
export const AVAILABLE_STRATEGIES = [
  {
    value: 'HybridSearch',
    label: 'Hybrid Search',
    description: 'Combines vector similarity and keyword matching',
  },
  {
    value: 'VectorOnly',
    label: 'Vector Only',
    description: 'Pure semantic vector search',
  },
  {
    value: 'KeywordOnly',
    label: 'Keyword Only',
    description: 'BM25 keyword-based search',
  },
  {
    value: 'TemporalScoring',
    label: 'Temporal Scoring',
    description: 'Prioritizes recent documents',
  },
  {
    value: 'CapabilityMatching',
    label: 'Capability Matching',
    description: 'Matches based on agent capabilities',
  },
] as const;
