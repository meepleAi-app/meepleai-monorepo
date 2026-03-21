import { z } from 'zod';

// Prompt Template Schema
export const promptTemplateSchema = z.object({
  role: z.enum(['system', 'user', 'assistant', 'function']),
  content: z.string().min(1, 'Content is required').max(10000, 'Content too long'),
});

// Tool Config Schema
export const toolConfigSchema = z.object({
  name: z.string().min(1, 'Tool name required').max(100),
  settings: z.record(z.string(), z.unknown()).optional().default({}),
});

// Agent Config Schema
export const agentConfigSchema = z.object({
  model: z.string().min(1, 'Model required'),
  maxTokens: z.number().min(100).max(32000),
  temperature: z.number().min(0).max(2),
});

// Create Agent Definition Schema
export const createAgentDefinitionSchema = z.object({
  name: z.string().min(3, 'Name must be at least 3 characters').max(100),
  description: z.string().max(1000).optional().default(''),
  model: z.string().min(1, 'Model required'),
  maxTokens: z.number().min(100).max(32000),
  temperature: z.number().min(0).max(2),
  prompts: z.array(promptTemplateSchema).max(20).optional().default([]),
  tools: z.array(toolConfigSchema).max(50).optional().default([]),
  kbCardIds: z.array(z.string().uuid()).optional().default([]),
  chatLanguage: z.string().max(10).optional().default('auto'),
});

// Update Agent Definition Schema
export const updateAgentDefinitionSchema = createAgentDefinitionSchema.extend({
  id: z.string().uuid(),
});

// KB Card DTO (Issue #4925)
export const kbCardDtoSchema = z.object({
  id: z.string().uuid(),
  pdfDocumentId: z.string().uuid(),
  fileName: z.string(),
  indexingStatus: z.string(),
  chunkCount: z.number().int(),
  indexedAt: z.string().datetime().nullable(),
  documentType: z.string().nullable(),
  version: z.string().nullable(),
  isActive: z.boolean(),
});

// Agent Definition DTO (response)
export const agentDefinitionDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  description: z.string(),
  type: z.string().optional().default(''),
  config: agentConfigSchema,
  strategyName: z.string().optional().default(''),
  strategyParameters: z.record(z.string(), z.unknown()).optional().default({}),
  prompts: z.array(promptTemplateSchema),
  tools: z.array(toolConfigSchema),
  kbCardIds: z.array(z.string().uuid()).optional().default([]),
  chatLanguage: z.string().optional().default('auto'),
  isActive: z.boolean(),
  status: z.number().optional().default(0),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime().nullable(),
});

// Types
export type PromptTemplate = z.infer<typeof promptTemplateSchema>;
export type ToolConfig = z.infer<typeof toolConfigSchema>;
export type AgentConfig = z.infer<typeof agentConfigSchema>;
export type CreateAgentDefinition = z.infer<typeof createAgentDefinitionSchema>;
export type UpdateAgentDefinition = z.infer<typeof updateAgentDefinitionSchema>;
export type AgentDefinitionDto = z.infer<typeof agentDefinitionDtoSchema>;
export type KbCardDto = z.infer<typeof kbCardDtoSchema>;
