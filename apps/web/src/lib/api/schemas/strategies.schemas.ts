import { z } from 'zod';

// Strategy Step Schema
export const strategyStepSchema = z.object({
  type: z.enum(['retrieval', 'reranking', 'generation', 'filter', 'analysis']),
  config: z.record(z.string(), z.unknown()),
  order: z.number().int().min(0),
});

// Create Strategy Schema
export const createStrategySchema = z.object({
  name: z.string().min(3).max(100),
  description: z.string().max(500).optional().default(''),
  steps: z.array(strategyStepSchema).min(1).max(10),
});

// Update Strategy Schema
export const updateStrategySchema = createStrategySchema.extend({
  id: z.string().uuid(),
});

// Strategy DTO
export const strategyDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  description: z.string(),
  steps: z.array(strategyStepSchema),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime().nullable(),
});

// Types
export type StrategyStep = z.infer<typeof strategyStepSchema>;
export type CreateStrategy = z.infer<typeof createStrategySchema>;
export type UpdateStrategy = z.infer<typeof updateStrategySchema>;
export type StrategyDto = z.infer<typeof strategyDtoSchema>;

// Predefined Templates
export const STRATEGY_TEMPLATES = {
  FAST: {
    name: 'Fast Retrieval',
    description: 'Quick vector search with single generation step',
    steps: [
      { type: 'retrieval' as const, config: { topK: 5 }, order: 0 },
      { type: 'generation' as const, config: { maxTokens: 500 }, order: 1 },
    ],
  },
  BALANCED: {
    name: 'Balanced Quality',
    description: 'Retrieval + reranking + generation for better accuracy',
    steps: [
      { type: 'retrieval' as const, config: { topK: 10 }, order: 0 },
      { type: 'reranking' as const, config: { topK: 5 }, order: 1 },
      { type: 'generation' as const, config: { maxTokens: 1000 }, order: 2 },
    ],
  },
  PRECISE: {
    name: 'Precise Analysis',
    description: 'Full pipeline with filtering and analysis',
    steps: [
      { type: 'retrieval' as const, config: { topK: 20 }, order: 0 },
      { type: 'filter' as const, config: { minScore: 0.7 }, order: 1 },
      { type: 'reranking' as const, config: { topK: 10 }, order: 2 },
      { type: 'analysis' as const, config: {}, order: 3 },
      { type: 'generation' as const, config: { maxTokens: 2000 }, order: 4 },
    ],
  },
} as const;
