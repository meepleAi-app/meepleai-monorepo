/**
 * RAG Dashboard Types
 *
 * Type definitions for the TOMAC-RAG visualization dashboard.
 * Includes types for query simulation, token flow, and cost calculation.
 */

// =============================================================================
// Core Enums
// =============================================================================

export type RagStrategy = 'FAST' | 'BALANCED' | 'PRECISE' | 'EXPERT' | 'CONSENSUS' | 'CUSTOM';
/**
 * User tiers for RAG access control.
 * NOTE: Anonymous users have NO ACCESS to the RAG system.
 * Authentication is required for all RAG operations.
 */
export type UserTier = 'Anonymous' | 'User' | 'Editor' | 'Admin' | 'Premium';

/**
 * Check if a user tier has RAG access
 */
export function hasRagAccess(tier: UserTier): boolean {
  return tier !== 'Anonymous';
}
export type QueryTemplate = 'rule_lookup' | 'resource_planning' | 'setup_guide' | 'strategy_advice' | 'educational';
export type ViewMode = 'technical' | 'business';

// Phase types for strategy configuration
export type RagPhase =
  | 'synthesis'
  | 'cragEvaluation'
  | 'retrieval'
  | 'analysis'
  | 'validation'
  | 'selfReflection'
  | 'webSearch'
  | 'multiHop'
  | 'consensusVoter1'
  | 'consensusVoter2'
  | 'consensusVoter3'
  | 'consensusAggregator';

// Strategy configuration with required phases
export interface StrategyConfig {
  name: RagStrategy;
  displayName: string;
  description: string;
  requiredPhases: RagPhase[];
  estimatedTokens: number;
  estimatedCost: number;
  latencyMs: string;
  accuracyRange: string;
  usagePercent: string;
  useCase: string;
}

/**
 * Strategy configurations with token/cost estimates.
 * IMPORTANT: These values should match rag-data.ts (Single Source of Truth)
 * @see docs/api/rag/appendix/E-model-pricing-2026.md for pricing
 * @see docs/api/rag/appendix/F-calculation-formulas.md for token calculations
 */
export const STRATEGY_CONFIGS: Record<RagStrategy, StrategyConfig> = {
  FAST: {
    name: 'FAST',
    displayName: 'FAST',
    description: 'FAQ semplici, risposte rapide',
    requiredPhases: ['synthesis'],
    estimatedTokens: 2060,
    estimatedCost: 0.0001, // Free models (Llama 3.3 via OpenRouter)
    latencyMs: '<200ms',
    accuracyRange: '78-85%',
    usagePercent: '60-70%',
    useCase: 'FAQ semplici, regole base, risposte rapide',
  },
  BALANCED: {
    name: 'BALANCED',
    displayName: 'BALANCED',
    description: 'Query standard con validazione CRAG',
    requiredPhases: ['synthesis', 'cragEvaluation'],
    estimatedTokens: 2820,
    estimatedCost: 0.01, // DeepSeek Chat
    latencyMs: '1-2s',
    accuracyRange: '85-92%',
    usagePercent: '25-30%',
    useCase: 'Query standard, regole complesse, setup guide',
  },
  PRECISE: {
    name: 'PRECISE',
    displayName: 'PRECISE',
    description: 'Query critiche, multi-agent pipeline',
    requiredPhases: ['retrieval', 'analysis', 'synthesis', 'validation'],
    estimatedTokens: 22396,
    estimatedCost: 0.132, // Multi-agent: Haiku + Sonnet + Opus mix
    latencyMs: '5-10s',
    accuracyRange: '95-98%',
    usagePercent: '5-10%',
    useCase: 'Decisioni critiche, strategie complesse, tournament rules',
  },
  EXPERT: {
    name: 'EXPERT',
    displayName: 'EXPERT',
    description: 'Web search + multi-hop reasoning',
    requiredPhases: ['webSearch', 'multiHop', 'synthesis'],
    estimatedTokens: 15000,
    estimatedCost: 0.099, // Claude Sonnet 4.5
    latencyMs: '8-15s',
    accuracyRange: '92-96%',
    usagePercent: '2-5%',
    useCase: 'Ricerca web, informazioni esterne, errata/FAQ ufficiali',
  },
  CONSENSUS: {
    name: 'CONSENSUS',
    displayName: 'CONSENSUS',
    description: 'Multi-LLM voting (3 voters + aggregator)',
    requiredPhases: ['consensusVoter1', 'consensusVoter2', 'consensusVoter3', 'consensusAggregator'],
    estimatedTokens: 18000,
    estimatedCost: 0.09, // Mixed: Sonnet + GPT-4o + DeepSeek
    latencyMs: '10-20s',
    accuracyRange: '97-99%',
    usagePercent: '1-3%',
    useCase: 'Decisioni ad alto rischio, arbitraggio regole, contestazioni',
  },
  CUSTOM: {
    name: 'CUSTOM',
    displayName: 'CUSTOM',
    description: 'Configurazione admin personalizzata',
    requiredPhases: ['synthesis'],
    estimatedTokens: 5000,
    estimatedCost: 0.025,
    latencyMs: 'Variable',
    accuracyRange: 'Variable',
    usagePercent: '<1%',
    useCase: 'Configurazioni specifiche per casi d\'uso particolari',
  },
};

// =============================================================================
// Layer Types
// =============================================================================

export interface RagLayer {
  id: string;
  name: string;
  shortName: string;
  description: string;
  icon: string;
  tokenRange: {
    min: number;
    max: number;
  };
  color: string;
}

export const RAG_LAYERS: RagLayer[] = [
  {
    id: 'routing',
    name: 'Intelligent Routing',
    shortName: 'L1',
    description: 'Classifies query and selects optimal strategy',
    icon: '🧠',
    tokenRange: { min: 280, max: 360 },
    color: 'hsl(221 83% 53%)', // Blue
  },
  {
    id: 'cache',
    name: 'Semantic Cache',
    shortName: 'L2',
    description: 'Memory + semantic similarity matching',
    icon: '💾',
    tokenRange: { min: 50, max: 310 },
    color: 'hsl(262 83% 62%)', // Purple
  },
  {
    id: 'retrieval',
    name: 'Modular Retrieval',
    shortName: 'L3',
    description: 'Vector + keyword hybrid search',
    icon: '📚',
    tokenRange: { min: 1500, max: 8000 },
    color: 'hsl(142 76% 36%)', // Green
  },
  {
    id: 'crag',
    name: 'CRAG Evaluation',
    shortName: 'L4',
    description: 'Quality gate with T5-Large evaluation',
    icon: '✅',
    tokenRange: { min: 0, max: 500 },
    color: 'hsl(45 93% 47%)', // Gold
  },
  {
    id: 'generation',
    name: 'Adaptive Generation',
    shortName: 'L5',
    description: 'LLM response with template-specific prompts',
    icon: '✨',
    tokenRange: { min: 1500, max: 5000 },
    color: 'hsl(25 95% 53%)', // Orange
  },
  {
    id: 'validation',
    name: 'Self-Validation',
    shortName: 'L6',
    description: 'Citation check + hallucination detection',
    icon: '🔍',
    tokenRange: { min: 0, max: 4400 },
    color: 'hsl(0 72% 51%)', // Red
  },
];

// =============================================================================
// Query Simulation Types
// =============================================================================

export interface QueryAnalysis {
  query: string;
  tier: UserTier;
  template: QueryTemplate;
  complexity: number; // 0-5
  strategy: RagStrategy;
  model: string;
  estimatedTokens: number;
  estimatedCost: number;
  cacheHit: boolean;
  layers: LayerExecution[];
}

export interface LayerExecution {
  layerId: string;
  status: 'pending' | 'processing' | 'complete' | 'skipped';
  tokensUsed: number;
  latencyMs: number;
  details?: string;
}

// =============================================================================
// Cost Calculator Types
// =============================================================================

export interface CostProjection {
  queriesPerMonth: number;
  cacheHitRate: number;
  tierDistribution: Record<UserTier, number>;
  strategyDistribution: Record<RagStrategy, number>;
  costs: {
    llm: number;
    embeddings: number;
    infrastructure: number;
    total: number;
  };
  savings: {
    fromCache: number;
    fromOptimization: number;
    total: number;
  };
  perQueryCost: number;
}

// =============================================================================
// Stats Types
// =============================================================================

export interface DashboardStats {
  ragVariants: number;
  avgTokensPerQuery: number;
  tokenReduction: number;
  targetAccuracy: number;
  monthlyCost: number;
  cacheHitRate: number;
}

/**
 * Default dashboard statistics.
 * NOTE: ragVariants = 31 (removed 5 Anonymous variants - NO ACCESS)
 * NOTE: monthlyCost updated to reflect 2026 pricing.
 * See docs/api/rag/appendix/E-model-pricing-2026.md for details.
 */
export const DEFAULT_STATS: DashboardStats = {
  ragVariants: 31, // Updated: removed 5 Anonymous variants
  avgTokensPerQuery: 1310,
  tokenReduction: -35,
  targetAccuracy: 95,
  monthlyCost: 2053, // Updated from 419 with 2026 pricing
  cacheHitRate: 80,
};

// =============================================================================
// Model Configuration Types
// =============================================================================

export interface ModelConfig {
  id: string;
  name: string;
  provider: string;
  inputCostPerMillion: number;
  outputCostPerMillion: number;
  maxTokens: number;
}

export const MODELS: Record<RagStrategy, ModelConfig[]> = {
  FAST: [
    { id: 'llama-3.3-70b', name: 'Llama 3.3 70B', provider: 'OpenRouter', inputCostPerMillion: 0, outputCostPerMillion: 0, maxTokens: 4096 },
    { id: 'gemini-2.0-flash', name: 'Gemini 2.0 Flash', provider: 'OpenRouter', inputCostPerMillion: 0, outputCostPerMillion: 0, maxTokens: 8192 },
  ],
  BALANCED: [
    { id: 'claude-sonnet-4.5', name: 'Claude Sonnet 4.5', provider: 'Anthropic', inputCostPerMillion: 3, outputCostPerMillion: 15, maxTokens: 8192 },
    { id: 'deepseek-chat', name: 'DeepSeek Chat', provider: 'DeepSeek', inputCostPerMillion: 0.28, outputCostPerMillion: 0.42, maxTokens: 16384 },
  ],
  PRECISE: [
    { id: 'claude-opus-4.5', name: 'Claude Opus 4.5', provider: 'Anthropic', inputCostPerMillion: 5, outputCostPerMillion: 25, maxTokens: 8192 },
    { id: 'gpt-4o', name: 'GPT-4o', provider: 'OpenAI', inputCostPerMillion: 2.5, outputCostPerMillion: 10, maxTokens: 8192 },
  ],
  EXPERT: [
    { id: 'claude-sonnet-4.5', name: 'Claude Sonnet 4.5', provider: 'Anthropic', inputCostPerMillion: 3, outputCostPerMillion: 15, maxTokens: 8192 },
    { id: 'gpt-4o', name: 'GPT-4o', provider: 'OpenAI', inputCostPerMillion: 2.5, outputCostPerMillion: 10, maxTokens: 8192 },
  ],
  CONSENSUS: [
    { id: 'claude-sonnet-4.5', name: 'Claude Sonnet 4.5 (Voter)', provider: 'Anthropic', inputCostPerMillion: 3, outputCostPerMillion: 15, maxTokens: 8192 },
    { id: 'gpt-4o', name: 'GPT-4o (Voter)', provider: 'OpenAI', inputCostPerMillion: 2.5, outputCostPerMillion: 10, maxTokens: 8192 },
    { id: 'deepseek-chat', name: 'DeepSeek Chat (Voter)', provider: 'DeepSeek', inputCostPerMillion: 0.28, outputCostPerMillion: 0.42, maxTokens: 16384 },
  ],
  CUSTOM: [
    { id: 'claude-haiku-4.5', name: 'Claude Haiku 4.5', provider: 'Anthropic', inputCostPerMillion: 1, outputCostPerMillion: 5, maxTokens: 8192 },
    { id: 'claude-sonnet-4.5', name: 'Claude Sonnet 4.5', provider: 'Anthropic', inputCostPerMillion: 3, outputCostPerMillion: 15, maxTokens: 8192 },
  ],
};
