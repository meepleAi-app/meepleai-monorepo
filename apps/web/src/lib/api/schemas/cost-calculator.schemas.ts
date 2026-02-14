/**
 * Agent Cost Calculator Zod Schemas
 * Issue #3725: Agent Cost Calculator (Epic #3688)
 */

import { z } from 'zod';

// ========== Strategy Constants ==========

export const RAG_STRATEGIES = [
  'Fast',
  'Balanced',
  'Precise',
  'Expert',
  'Consensus',
  'SentenceWindow',
  'Iterative',
  'Custom',
  'MultiAgent',
  'StepBack',
  'QueryExpansion',
  'RagFusion',
] as const;

export type RagStrategy = (typeof RAG_STRATEGIES)[number];

export const RAG_STRATEGY_LABELS: Record<RagStrategy, string> = {
  Fast: 'Fast (Free tier)',
  Balanced: 'Balanced',
  Precise: 'Precise',
  Expert: 'Expert',
  Consensus: 'Consensus (Multi-model)',
  SentenceWindow: 'Sentence Window',
  Iterative: 'Iterative',
  Custom: 'Custom',
  MultiAgent: 'Multi-Agent',
  StepBack: 'Step Back',
  QueryExpansion: 'Query Expansion',
  RagFusion: 'RAG Fusion',
};

// ========== Estimation Result ==========

export const AgentCostEstimationResultSchema = z.object({
  strategy: z.string(),
  modelId: z.string(),
  provider: z.string(),
  inputCostPer1MTokens: z.number(),
  outputCostPer1MTokens: z.number(),
  costPerRequest: z.number(),
  dailyProjection: z.number(),
  monthlyProjection: z.number(),
  totalDailyRequests: z.number(),
  avgTokensPerRequest: z.number(),
  warnings: z.array(z.string()),
});
export type AgentCostEstimationResult = z.infer<typeof AgentCostEstimationResultSchema>;

// ========== Cost Scenario DTO ==========

export const CostScenarioDtoSchema = z.object({
  id: z.string().uuid(),
  name: z.string(),
  strategy: z.string(),
  modelId: z.string(),
  messagesPerDay: z.number(),
  activeUsers: z.number(),
  avgTokensPerRequest: z.number(),
  costPerRequest: z.number(),
  dailyProjection: z.number(),
  monthlyProjection: z.number(),
  warnings: z.array(z.string()),
  createdByUserId: z.string().uuid(),
  createdAt: z.string(),
});
export type CostScenarioDto = z.infer<typeof CostScenarioDtoSchema>;

export const CostScenariosResponseSchema = z.object({
  items: z.array(CostScenarioDtoSchema),
  total: z.number(),
  page: z.number(),
  pageSize: z.number(),
});
export type CostScenariosResponse = z.infer<typeof CostScenariosResponseSchema>;

export const SaveScenarioResponseSchema = z.object({
  id: z.string().uuid(),
});
export type SaveScenarioResponse = z.infer<typeof SaveScenarioResponseSchema>;

// ========== Request Types ==========

export type EstimateAgentCostRequest = {
  strategy: string;
  modelId: string;
  messagesPerDay: number;
  activeUsers: number;
  avgTokensPerRequest: number;
};

export type SaveCostScenarioRequest = {
  name: string;
  strategy: string;
  modelId: string;
  messagesPerDay: number;
  activeUsers: number;
  avgTokensPerRequest: number;
  costPerRequest: number;
  dailyProjection: number;
  monthlyProjection: number;
  warnings?: string[] | null;
};

export type GetCostScenariosParams = {
  page?: number;
  pageSize?: number;
};
