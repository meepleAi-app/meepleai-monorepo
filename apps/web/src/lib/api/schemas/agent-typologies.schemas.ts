import { z } from 'zod';

// Strategy enum - available RAG strategies
export const strategyEnum = z.enum([
  'HybridSearch',
  'VectorOnly',
  'KeywordOnly',
  'MultiModel',
  'Reranked',
]);

// Full typology entity (for GET responses) - Aligned with backend AgentTypologyDto
export const typologySchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(3).max(100),
  description: z.string(),
  basePrompt: z.string().min(1).max(5000),
  defaultStrategyName: z.string(), // Backend uses string, not enum
  defaultStrategyParameters: z.record(z.string(), z.unknown()).optional().nullable(),
  status: z.enum(['Draft', 'PendingReview', 'Approved', 'Rejected']),
  createdBy: z.string().uuid(),
  approvedBy: z.string().uuid().optional().nullable(),
  createdAt: z.string().datetime(),
  approvedAt: z.string().datetime().optional().nullable(),
  isDeleted: z.boolean(),
});

// Create typology schema with validation - For ProposeAgentTypologyCommand
export const createTypologySchema = z.object({
  name: z
    .string()
    .min(3, 'Il nome deve avere almeno 3 caratteri')
    .max(100, 'Il nome non può superare 100 caratteri'),
  description: z.string().max(500, 'La descrizione non può superare 500 caratteri'),
  basePrompt: z
    .string()
    .min(1, 'Il prompt è obbligatorio')
    .max(5000, 'Il prompt non può superare 5000 caratteri'),
  defaultStrategyName: z.string().min(1, 'La strategia è obbligatoria'),
  defaultStrategyParameters: z.record(z.string(), z.unknown()).optional(),
});

// Update typology schema (same as create)
export const updateTypologySchema = createTypologySchema;

// Type exports
export type Typology = z.infer<typeof typologySchema>;
export type CreateTypology = z.infer<typeof createTypologySchema>;
export type UpdateTypology = z.infer<typeof updateTypologySchema>;
export type Strategy = z.infer<typeof strategyEnum>;
