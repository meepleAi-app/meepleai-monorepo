/**
 * RAG Configuration Types
 *
 * Extended type definitions for configurable RAG parameters.
 * Allows admin override of hardcoded estimates with measured production values.
 *
 * @see docs/03-api/rag/appendix/F-calculation-formulas.md
 */

import type { RagStrategy, RagPhase, UserTier } from './types';

// =============================================================================
// Configurable Value Types
// =============================================================================

/**
 * A value that can be estimated or measured from production data.
 * When measured value is available, it takes precedence over estimated.
 */
export interface ConfigurableValue<T> {
  /** Theoretical estimate based on documentation/research */
  estimated: T;
  /** Actual measured value from production (overrides estimated when present) */
  measured?: T;
  /** When the measured value was last updated */
  measuredAt?: string; // ISO date
  /** Statistical confidence in the measured value (0-1) */
  confidence?: number;
  /** Source/method of measurement */
  source?: string;
}

/**
 * Get the effective value (measured if available, otherwise estimated)
 */
export function getEffectiveValue<T>(config: ConfigurableValue<T>): T {
  return config.measured !== undefined ? config.measured : config.estimated;
}

/**
 * Check if measured data is available
 */
export function hasMeasuredData<T>(config: ConfigurableValue<T>): boolean {
  return config.measured !== undefined;
}

// =============================================================================
// Model Pricing Configuration
// =============================================================================

export interface ModelPricing {
  modelId: string;
  modelName: string;
  provider: 'Anthropic' | 'OpenAI' | 'DeepSeek' | 'OpenRouter' | 'Other';
  /** Cost per 1M input tokens in USD */
  inputCost: ConfigurableValue<number>;
  /** Cost per 1M output tokens in USD */
  outputCost: ConfigurableValue<number>;
  /** Cost per 1M cached tokens in USD (if available) */
  cacheCost?: ConfigurableValue<number>;
  /** Is this a free model? */
  isFree: boolean;
  /** Last price update date */
  lastUpdated: string; // ISO date
  /** Source URL for pricing info */
  pricingSource?: string;
}

export const DEFAULT_MODEL_PRICING: ModelPricing[] = [
  // Anthropic Claude
  {
    modelId: 'claude-opus-4.5',
    modelName: 'Claude Opus 4.5',
    provider: 'Anthropic',
    inputCost: { estimated: 5.0 },
    outputCost: { estimated: 25.0 },
    cacheCost: { estimated: 0.5 },
    isFree: false,
    lastUpdated: '2026-02-02',
    pricingSource: 'https://platform.claude.com/docs/en/about-claude/pricing',
  },
  {
    modelId: 'claude-sonnet-4.5',
    modelName: 'Claude Sonnet 4.5',
    provider: 'Anthropic',
    inputCost: { estimated: 3.0 },
    outputCost: { estimated: 15.0 },
    cacheCost: { estimated: 0.3 },
    isFree: false,
    lastUpdated: '2026-02-02',
    pricingSource: 'https://platform.claude.com/docs/en/about-claude/pricing',
  },
  {
    modelId: 'claude-haiku-4.5',
    modelName: 'Claude Haiku 4.5',
    provider: 'Anthropic',
    inputCost: { estimated: 1.0 },
    outputCost: { estimated: 5.0 },
    cacheCost: { estimated: 0.1 },
    isFree: false,
    lastUpdated: '2026-02-02',
    pricingSource: 'https://platform.claude.com/docs/en/about-claude/pricing',
  },
  // OpenAI
  {
    modelId: 'gpt-4o',
    modelName: 'GPT-4o',
    provider: 'OpenAI',
    inputCost: { estimated: 2.5 },
    outputCost: { estimated: 10.0 },
    isFree: false,
    lastUpdated: '2026-02-02',
    pricingSource: 'https://platform.openai.com/docs/pricing',
  },
  {
    modelId: 'gpt-4o-mini',
    modelName: 'GPT-4o-mini',
    provider: 'OpenAI',
    inputCost: { estimated: 0.15 },
    outputCost: { estimated: 0.6 },
    cacheCost: { estimated: 0.08 },
    isFree: false,
    lastUpdated: '2026-02-02',
    pricingSource: 'https://platform.openai.com/docs/pricing',
  },
  // DeepSeek
  {
    modelId: 'deepseek-chat',
    modelName: 'DeepSeek Chat',
    provider: 'DeepSeek',
    inputCost: { estimated: 0.28 },
    outputCost: { estimated: 0.42 },
    cacheCost: { estimated: 0.028 },
    isFree: false,
    lastUpdated: '2026-02-02',
    pricingSource: 'https://api-docs.deepseek.com/quick_start/pricing',
  },
  // Free Models (OpenRouter)
  {
    modelId: 'llama-3.3-70b',
    modelName: 'Llama 3.3 70B Instruct',
    provider: 'OpenRouter',
    inputCost: { estimated: 0 },
    outputCost: { estimated: 0 },
    isFree: true,
    lastUpdated: '2026-02-02',
    pricingSource: 'https://openrouter.ai/collections/free-models',
  },
  {
    modelId: 'gemini-2.0-flash-exp',
    modelName: 'Gemini 2.0 Flash Exp',
    provider: 'OpenRouter',
    inputCost: { estimated: 0 },
    outputCost: { estimated: 0 },
    isFree: true,
    lastUpdated: '2026-02-02',
    pricingSource: 'https://openrouter.ai/collections/free-models',
  },
];

// =============================================================================
// Strategy Configuration (Configurable)
// =============================================================================

export interface ConfigurableStrategyConfig {
  name: RagStrategy;
  displayName: string;
  description: string;
  requiredPhases: RagPhase[];

  /** Token consumption */
  tokens: ConfigurableValue<number>;

  /** Cost per query in USD */
  cost: ConfigurableValue<number>;

  /** Latency metrics */
  latency: {
    /** Minimum latency in ms */
    minMs: ConfigurableValue<number>;
    /** Maximum latency in ms */
    maxMs: ConfigurableValue<number>;
    /** P50 latency (median) */
    p50Ms?: ConfigurableValue<number>;
    /** P95 latency */
    p95Ms?: ConfigurableValue<number>;
    /** P99 latency */
    p99Ms?: ConfigurableValue<number>;
  };

  /** Accuracy metrics */
  accuracy: {
    /** Minimum expected accuracy (0-1) */
    min: ConfigurableValue<number>;
    /** Maximum expected accuracy (0-1) */
    max: ConfigurableValue<number>;
    /** Measured average accuracy (0-1) */
    average?: ConfigurableValue<number>;
  };

  /** Usage distribution (percentage of total queries) */
  usagePercent: ConfigurableValue<{ min: number; max: number }>;

  /** Primary use cases */
  useCases: string[];

  /** Primary models used */
  primaryModels: string[];
}

export const DEFAULT_CONFIGURABLE_STRATEGIES: Record<RagStrategy, ConfigurableStrategyConfig> = {
  FAST: {
    name: 'FAST',
    displayName: 'FAST',
    description: 'FAQ semplici, risposte rapide',
    requiredPhases: ['synthesis'],
    tokens: { estimated: 2060 },
    cost: { estimated: 0.0001 }, // With free models
    latency: {
      minMs: { estimated: 50 },
      maxMs: { estimated: 200 },
    },
    accuracy: {
      min: { estimated: 0.78 },
      max: { estimated: 0.85 },
    },
    usagePercent: { estimated: { min: 60, max: 70 } },
    useCases: ['FAQ semplici', 'regole base', 'risposte rapide'],
    primaryModels: ['llama-3.3-70b', 'gemini-2.0-flash-exp'],
  },
  BALANCED: {
    name: 'BALANCED',
    displayName: 'BALANCED',
    description: 'Query standard con validazione CRAG',
    requiredPhases: ['synthesis', 'cragEvaluation'],
    tokens: { estimated: 2820 },
    cost: { estimated: 0.01 },
    latency: {
      minMs: { estimated: 1000 },
      maxMs: { estimated: 2000 },
    },
    accuracy: {
      min: { estimated: 0.85 },
      max: { estimated: 0.92 },
    },
    usagePercent: { estimated: { min: 25, max: 30 } },
    useCases: ['Query standard', 'regole complesse', 'setup guide'],
    primaryModels: ['deepseek-chat', 'claude-sonnet-4.5'],
  },
  PRECISE: {
    name: 'PRECISE',
    displayName: 'PRECISE',
    description: 'Query critiche, multi-agent pipeline',
    requiredPhases: ['retrieval', 'analysis', 'synthesis', 'validation', 'selfReflection'],
    tokens: { estimated: 22396 },
    cost: { estimated: 0.132 },
    latency: {
      minMs: { estimated: 5000 },
      maxMs: { estimated: 10000 },
    },
    accuracy: {
      min: { estimated: 0.95 },
      max: { estimated: 0.98 },
    },
    usagePercent: { estimated: { min: 5, max: 10 } },
    useCases: ['Decisioni critiche', 'strategie complesse', 'tournament rules'],
    primaryModels: ['claude-haiku-4.5', 'claude-sonnet-4.5', 'claude-opus-4.5'],
  },
  EXPERT: {
    name: 'EXPERT',
    displayName: 'EXPERT',
    description: 'Web search + multi-hop reasoning',
    requiredPhases: ['webSearch', 'multiHop', 'synthesis'],
    tokens: { estimated: 15000 },
    cost: { estimated: 0.099 },
    latency: {
      minMs: { estimated: 8000 },
      maxMs: { estimated: 15000 },
    },
    accuracy: {
      min: { estimated: 0.92 },
      max: { estimated: 0.96 },
    },
    usagePercent: { estimated: { min: 2, max: 5 } },
    useCases: ['Ricerca web', 'informazioni esterne', 'errata/FAQ ufficiali'],
    primaryModels: ['claude-sonnet-4.5'],
  },
  CONSENSUS: {
    name: 'CONSENSUS',
    displayName: 'CONSENSUS',
    description: 'Multi-LLM voting (3 voters + aggregator)',
    requiredPhases: ['consensusVoter1', 'consensusVoter2', 'consensusVoter3', 'consensusAggregator'],
    tokens: { estimated: 18000 },
    cost: { estimated: 0.09 },
    latency: {
      minMs: { estimated: 10000 },
      maxMs: { estimated: 20000 },
    },
    accuracy: {
      min: { estimated: 0.97 },
      max: { estimated: 0.99 },
    },
    usagePercent: { estimated: { min: 1, max: 3 } },
    useCases: ['Decisioni ad alto rischio', 'arbitraggio regole', 'contestazioni'],
    primaryModels: ['claude-sonnet-4.5', 'gpt-4o', 'deepseek-chat'],
  },
  CUSTOM: {
    name: 'CUSTOM',
    displayName: 'CUSTOM',
    description: 'Configurazione admin personalizzata',
    requiredPhases: ['synthesis'],
    tokens: { estimated: 5000 },
    cost: { estimated: 0.025 },
    latency: {
      minMs: { estimated: 1000 },
      maxMs: { estimated: 10000 },
    },
    accuracy: {
      min: { estimated: 0.8 },
      max: { estimated: 0.95 },
    },
    usagePercent: { estimated: { min: 0, max: 1 } },
    useCases: ['Configurazioni specifiche', 'testing', 'casi particolari'],
    primaryModels: ['claude-haiku-4.5', 'claude-sonnet-4.5'],
  },
};

// =============================================================================
// Layer Configuration (Configurable)
// =============================================================================

export interface ConfigurableLayerConfig {
  id: string;
  name: string;
  shortName: string;
  description: string;
  icon: string;
  color: string;

  /** Token range for this layer */
  tokenRange: {
    min: ConfigurableValue<number>;
    max: ConfigurableValue<number>;
  };

  /** Latency range for this layer */
  latencyRange: {
    minMs: ConfigurableValue<number>;
    maxMs: ConfigurableValue<number>;
  };

  /** Dependencies on external services */
  dependencies: string[];
}

export const DEFAULT_CONFIGURABLE_LAYERS: ConfigurableLayerConfig[] = [
  {
    id: 'routing',
    name: 'Intelligent Routing',
    shortName: 'L1',
    description: 'Classifies query and selects optimal strategy',
    icon: '🧠',
    color: 'hsl(221 83% 53%)',
    tokenRange: {
      min: { estimated: 280 },
      max: { estimated: 360 },
    },
    latencyRange: {
      minMs: { estimated: 20 },
      maxMs: { estimated: 50 },
    },
    dependencies: ['LLM Classifier'],
  },
  {
    id: 'cache',
    name: 'Semantic Cache',
    shortName: 'L2',
    description: 'Memory + semantic similarity matching',
    icon: '💾',
    color: 'hsl(262 83% 62%)',
    tokenRange: {
      min: { estimated: 50 },
      max: { estimated: 310 },
    },
    latencyRange: {
      minMs: { estimated: 10 },
      maxMs: { estimated: 50 },
    },
    dependencies: ['Redis', 'Embedding Service'],
  },
  {
    id: 'retrieval',
    name: 'Modular Retrieval',
    shortName: 'L3',
    description: 'Vector + keyword hybrid search',
    icon: '📚',
    color: 'hsl(142 76% 36%)',
    tokenRange: {
      min: { estimated: 1500 },
      max: { estimated: 8000 },
    },
    latencyRange: {
      minMs: { estimated: 50 },
      maxMs: { estimated: 500 },
    },
    dependencies: ['Qdrant', 'PostgreSQL FTS'],
  },
  {
    id: 'crag',
    name: 'CRAG Evaluation',
    shortName: 'L4',
    description: 'Quality gate with T5-Large evaluation',
    icon: '✅',
    color: 'hsl(45 93% 47%)',
    tokenRange: {
      min: { estimated: 0 },
      max: { estimated: 500 },
    },
    latencyRange: {
      minMs: { estimated: 100 },
      maxMs: { estimated: 500 },
    },
    dependencies: ['T5-Large Model'],
  },
  {
    id: 'generation',
    name: 'Adaptive Generation',
    shortName: 'L5',
    description: 'LLM response with template-specific prompts',
    icon: '✨',
    color: 'hsl(25 95% 53%)',
    tokenRange: {
      min: { estimated: 1500 },
      max: { estimated: 5000 },
    },
    latencyRange: {
      minMs: { estimated: 200 },
      maxMs: { estimated: 5000 },
    },
    dependencies: ['LLM Provider'],
  },
  {
    id: 'validation',
    name: 'Self-Validation',
    shortName: 'L6',
    description: 'Citation check + hallucination detection',
    icon: '🔍',
    color: 'hsl(0 72% 51%)',
    tokenRange: {
      min: { estimated: 0 },
      max: { estimated: 4400 },
    },
    latencyRange: {
      minMs: { estimated: 0 },
      maxMs: { estimated: 2000 },
    },
    dependencies: ['LLM Provider', 'Citation Matcher'],
  },
];

// =============================================================================
// Global Configuration
// =============================================================================

export interface RagGlobalConfig {
  /** Token distribution assumption (input vs output) */
  tokenDistribution: {
    inputRatio: ConfigurableValue<number>; // e.g., 0.70 = 70% input
    outputRatio: ConfigurableValue<number>; // e.g., 0.30 = 30% output
  };

  /** Cache performance */
  cacheConfig: {
    hitRate: ConfigurableValue<number>; // e.g., 0.80 = 80%
    ttlHours: Record<UserTier, ConfigurableValue<number>>;
  };

  /** Chunk configuration */
  chunkConfig: {
    sizeAvg: ConfigurableValue<number>; // Average tokens per chunk
    fastChunks: ConfigurableValue<number>; // Chunks for FAST strategy
    balancedChunks: ConfigurableValue<number>; // Chunks for BALANCED
    preciseChunks: ConfigurableValue<number>; // Chunks for PRECISE
  };
}

export const DEFAULT_GLOBAL_CONFIG: RagGlobalConfig = {
  tokenDistribution: {
    inputRatio: { estimated: 0.7 },
    outputRatio: { estimated: 0.3 },
  },
  cacheConfig: {
    hitRate: { estimated: 0.8 },
    ttlHours: {
      Anonymous: { estimated: 0 }, // No access
      User: { estimated: 48 },
      Editor: { estimated: 72 },
      Admin: { estimated: 168 },
      Premium: { estimated: 336 },
    },
  },
  chunkConfig: {
    sizeAvg: { estimated: 500 },
    fastChunks: { estimated: 3 },
    balancedChunks: { estimated: 10 },
    preciseChunks: { estimated: 20 },
  },
};

// =============================================================================
// Full Configuration State
// =============================================================================

export interface RagConfigurationState {
  /** Version of the configuration schema */
  schemaVersion: string;

  /** When this configuration was last updated */
  lastUpdated: string; // ISO date

  /** Who last updated this configuration */
  updatedBy?: string;

  /** Global settings */
  global: RagGlobalConfig;

  /** Model pricing */
  modelPricing: ModelPricing[];

  /** Strategy configurations */
  strategies: Record<RagStrategy, ConfigurableStrategyConfig>;

  /** Layer configurations */
  layers: ConfigurableLayerConfig[];
}

export const DEFAULT_CONFIGURATION: RagConfigurationState = {
  schemaVersion: '1.0.0',
  lastUpdated: new Date().toISOString(),
  global: DEFAULT_GLOBAL_CONFIG,
  modelPricing: DEFAULT_MODEL_PRICING,
  strategies: DEFAULT_CONFIGURABLE_STRATEGIES,
  layers: DEFAULT_CONFIGURABLE_LAYERS,
};

// =============================================================================
// Cost Calculation Helpers
// =============================================================================

/**
 * Calculate cost for a query given token count and model pricing
 */
export function calculateQueryCost(
  tokens: number,
  modelPricing: ModelPricing,
  inputRatio: number = 0.7
): number {
  const inputTokens = tokens * inputRatio;
  const outputTokens = tokens * (1 - inputRatio);

  const inputCost = (inputTokens / 1_000_000) * getEffectiveValue(modelPricing.inputCost);
  const outputCost = (outputTokens / 1_000_000) * getEffectiveValue(modelPricing.outputCost);

  return inputCost + outputCost;
}

/**
 * Calculate monthly cost projection
 */
export function calculateMonthlyCost(
  queriesPerMonth: number,
  strategyDistribution: Record<RagStrategy, number>,
  config: RagConfigurationState,
  cacheHitRate?: number
): {
  totalCost: number;
  byStrategy: Record<RagStrategy, number>;
  savings: { fromCache: number };
} {
  const effectiveCacheRate = cacheHitRate ?? getEffectiveValue(config.global.cacheConfig.hitRate);
  const byStrategy: Record<string, number> = {};
  let totalCost = 0;
  let savingsFromCache = 0;

  for (const [strategy, percentage] of Object.entries(strategyDistribution) as [RagStrategy, number][]) {
    const strategyConfig = config.strategies[strategy];
    const queries = queriesPerMonth * percentage;
    const costPerQuery = getEffectiveValue(strategyConfig.cost);

    // Apply cache savings (cache hit = minimal cost)
    const cacheMissCost = queries * (1 - effectiveCacheRate) * costPerQuery;
    const cacheHitCost = queries * effectiveCacheRate * 0.0001; // Minimal lookup cost
    const strategyCost = cacheMissCost + cacheHitCost;

    byStrategy[strategy] = strategyCost;
    totalCost += strategyCost;
    savingsFromCache += queries * effectiveCacheRate * costPerQuery;
  }

  return {
    totalCost,
    byStrategy: byStrategy as Record<RagStrategy, number>,
    savings: { fromCache: savingsFromCache },
  };
}

/**
 * Format accuracy as percentage string
 */
export function formatAccuracy(config: ConfigurableStrategyConfig): string {
  const min = getEffectiveValue(config.accuracy.min) * 100;
  const max = getEffectiveValue(config.accuracy.max) * 100;
  return `${min.toFixed(0)}-${max.toFixed(0)}%`;
}

/**
 * Format latency as display string
 */
export function formatLatency(config: ConfigurableStrategyConfig): string {
  const minMs = getEffectiveValue(config.latency.minMs);
  const maxMs = getEffectiveValue(config.latency.maxMs);

  if (minMs >= 1000 || maxMs >= 1000) {
    const minS = (minMs / 1000).toFixed(0);
    const maxS = (maxMs / 1000).toFixed(0);
    return `${minS}-${maxS}s`;
  }

  return `${minMs}-${maxMs}ms`;
}
