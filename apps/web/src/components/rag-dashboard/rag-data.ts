/**
 * RAG Single Source of Truth
 *
 * CANONICAL DATA for all RAG strategies, layers, and configurations.
 * This file is the authoritative source - all documentation and components
 * should reference these values.
 *
 * @version 2.0.0
 * @lastUpdated 2026-02-02
 * @see docs/03-api/rag/appendix/F-calculation-formulas.md
 */

// =============================================================================
// STRATEGY DATA - 6 Official Strategies
// =============================================================================

export interface StrategyData {
  id: string;
  name: string;
  description: string;
  descriptionEn: string;
  tokens: number;
  cost: number;
  latency: { min: number; max: number; display: string };
  accuracy: { min: number; max: number; display: string };
  usage: { min: number; max: number; display: string };
  phases: string[];
  models: string[];
  useCases: string[];
  color: string;
  icon: string;
}

export const STRATEGIES: Record<string, StrategyData> = {
  FAST: {
    id: 'FAST',
    name: 'FAST',
    description: 'FAQ semplici, risposte rapide',
    descriptionEn: 'Simple FAQs, quick responses',
    tokens: 2060,
    cost: 0.0001, // With free models (Llama 3.3)
    latency: { min: 50, max: 200, display: '<200ms' },
    accuracy: { min: 78, max: 85, display: '78-85%' },
    usage: { min: 60, max: 70, display: '60-70%' },
    phases: ['L1-Routing', 'L2-Cache', 'L3-Retrieval-FAST', 'L5-Generation'],
    models: ['llama-3.3-70b', 'gemini-2.0-flash-exp'],
    useCases: ['FAQ semplici', 'Regole base', 'Risposte rapide', 'Lookup diretto'],
    color: 'hsl(142 76% 36%)', // Green
    icon: '⚡',
  },
  BALANCED: {
    id: 'BALANCED',
    name: 'BALANCED',
    description: 'Query standard con validazione CRAG',
    descriptionEn: 'Standard queries with CRAG validation',
    tokens: 2820,
    cost: 0.01,
    latency: { min: 1000, max: 2000, display: '1-2s' },
    accuracy: { min: 85, max: 92, display: '85-92%' },
    usage: { min: 25, max: 30, display: '25-30%' },
    phases: ['L1-Routing', 'L2-Cache', 'L3-Retrieval-BALANCED', 'L4-CRAG', 'L5-Generation'],
    models: ['deepseek-chat', 'claude-sonnet-4.5'],
    useCases: ['Query standard', 'Regole complesse', 'Setup guide', 'Resource planning'],
    color: 'hsl(221 83% 53%)', // Blue
    icon: '⚖️',
  },
  PRECISE: {
    id: 'PRECISE',
    name: 'PRECISE',
    description: 'Query critiche, multi-agent pipeline',
    descriptionEn: 'Critical queries, multi-agent pipeline',
    tokens: 22396,
    cost: 0.132,
    latency: { min: 5000, max: 10000, display: '5-10s' },
    accuracy: { min: 95, max: 98, display: '95-98%' },
    usage: { min: 5, max: 10, display: '5-10%' },
    phases: ['L1-Routing', 'L3-MultiHop', 'L4-LLMGrading', 'L5-MultiAgent', 'L6-SelfRAG'],
    models: ['claude-haiku-4.5', 'claude-sonnet-4.5', 'claude-opus-4.5'],
    useCases: ['Decisioni critiche', 'Strategie complesse', 'Tournament rules', 'Edge cases'],
    color: 'hsl(262 83% 62%)', // Purple
    icon: '🎯',
  },
  EXPERT: {
    id: 'EXPERT',
    name: 'EXPERT',
    description: 'Web search + multi-hop reasoning',
    descriptionEn: 'Web search + multi-hop reasoning',
    tokens: 15000,
    cost: 0.099,
    latency: { min: 8000, max: 15000, display: '8-15s' },
    accuracy: { min: 92, max: 96, display: '92-96%' },
    usage: { min: 2, max: 5, display: '2-5%' },
    phases: ['L1-Routing', 'WebSearch', 'MultiHop', 'L5-Synthesis'],
    models: ['claude-sonnet-4.5'],
    useCases: ['Ricerca web', 'Informazioni esterne', 'Errata/FAQ ufficiali', 'Aggiornamenti'],
    color: 'hsl(25 95% 53%)', // Orange
    icon: '🔍',
  },
  CONSENSUS: {
    id: 'CONSENSUS',
    name: 'CONSENSUS',
    description: 'Multi-LLM voting (3 voters + aggregator)',
    descriptionEn: 'Multi-LLM voting (3 voters + aggregator)',
    tokens: 18000,
    cost: 0.09,
    latency: { min: 10000, max: 20000, display: '10-20s' },
    accuracy: { min: 97, max: 99, display: '97-99%' },
    usage: { min: 1, max: 3, display: '1-3%' },
    phases: ['L1-Routing', 'Voter1-Sonnet', 'Voter2-GPT4o', 'Voter3-DeepSeek', 'Aggregator'],
    models: ['claude-sonnet-4.5', 'gpt-4o', 'deepseek-chat'],
    useCases: ['Decisioni alto rischio', 'Arbitraggio regole', 'Contestazioni', 'Validazione critica'],
    color: 'hsl(0 72% 51%)', // Red
    icon: '🗳️',
  },
  CUSTOM: {
    id: 'CUSTOM',
    name: 'CUSTOM',
    description: 'Configurazione admin personalizzata',
    descriptionEn: 'Admin custom configuration',
    tokens: 5000,
    cost: 0.025,
    latency: { min: 1000, max: 10000, display: 'Variable' },
    accuracy: { min: 80, max: 95, display: 'Variable' },
    usage: { min: 0, max: 1, display: '<1%' },
    phases: ['Configurable'],
    models: ['claude-haiku-4.5', 'claude-sonnet-4.5'],
    useCases: ['Configurazioni specifiche', 'Testing', 'Casi particolari', 'Debug'],
    color: 'hsl(45 93% 47%)', // Gold
    icon: '⚙️',
  },
};

// =============================================================================
// LAYER DATA - 6-Layer Architecture
// =============================================================================

export interface LayerData {
  id: string;
  number: number;
  name: string;
  shortName: string;
  description: string;
  descriptionEn: string;
  tokenRange: { min: number; max: number };
  latencyRange: { min: number; max: number };
  color: string;
  icon: string;
  dependencies: string[];
  docPath: string;
}

export const LAYERS: LayerData[] = [
  {
    id: 'routing',
    number: 1,
    name: 'Intelligent Routing',
    shortName: 'L1',
    description: 'Classifica query e seleziona strategia ottimale',
    descriptionEn: 'Classifies query and selects optimal strategy',
    tokenRange: { min: 280, max: 360 },
    latencyRange: { min: 20, max: 50 },
    color: 'hsl(221 83% 53%)',
    icon: '🧠',
    dependencies: ['LLM Classifier'],
    docPath: '02-layer1-routing.md',
  },
  {
    id: 'cache',
    number: 2,
    name: 'Semantic Cache',
    shortName: 'L2',
    description: 'Memory + semantic similarity matching (80% hit rate)',
    descriptionEn: 'Memory + semantic similarity matching',
    tokenRange: { min: 50, max: 310 },
    latencyRange: { min: 10, max: 50 },
    color: 'hsl(262 83% 62%)',
    icon: '💾',
    dependencies: ['Redis', 'Embedding Service'],
    docPath: '03-layer2-caching.md',
  },
  {
    id: 'retrieval',
    number: 3,
    name: 'Modular Retrieval',
    shortName: 'L3',
    description: 'Vector + keyword hybrid search',
    descriptionEn: 'Vector + keyword hybrid search',
    tokenRange: { min: 1500, max: 8000 },
    latencyRange: { min: 50, max: 500 },
    color: 'hsl(142 76% 36%)',
    icon: '📚',
    dependencies: ['Qdrant', 'PostgreSQL FTS'],
    docPath: '04-layer3-retrieval.md',
  },
  {
    id: 'crag',
    number: 4,
    name: 'CRAG Evaluation',
    shortName: 'L4',
    description: 'Quality gate con T5-Large evaluation',
    descriptionEn: 'Quality gate with T5-Large evaluation',
    tokenRange: { min: 0, max: 500 },
    latencyRange: { min: 100, max: 500 },
    color: 'hsl(45 93% 47%)',
    icon: '✅',
    dependencies: ['T5-Large Model'],
    docPath: '05-layer4-crag-evaluation.md',
  },
  {
    id: 'generation',
    number: 5,
    name: 'Adaptive Generation',
    shortName: 'L5',
    description: 'LLM response con template-specific prompts',
    descriptionEn: 'LLM response with template-specific prompts',
    tokenRange: { min: 1500, max: 5000 },
    latencyRange: { min: 200, max: 5000 },
    color: 'hsl(25 95% 53%)',
    icon: '✨',
    dependencies: ['LLM Provider'],
    docPath: '06-layer5-generation.md',
  },
  {
    id: 'validation',
    number: 6,
    name: 'Self-Validation',
    shortName: 'L6',
    description: 'Citation check + hallucination detection',
    descriptionEn: 'Citation check + hallucination detection',
    tokenRange: { min: 0, max: 4400 },
    latencyRange: { min: 0, max: 2000 },
    color: 'hsl(0 72% 51%)',
    icon: '🔍',
    dependencies: ['LLM Provider', 'Citation Matcher'],
    docPath: '07-layer6-validation.md',
  },
];

// =============================================================================
// MODEL PRICING - Updated 2026-02-02
// =============================================================================

export interface ModelPricingData {
  id: string;
  name: string;
  provider: 'Anthropic' | 'OpenAI' | 'DeepSeek' | 'OpenRouter';
  inputCost: number; // $ per 1M tokens
  outputCost: number; // $ per 1M tokens
  cacheCost?: number; // $ per 1M tokens
  isFree: boolean;
  lastUpdated: string;
}

export const MODEL_PRICING: ModelPricingData[] = [
  // Anthropic Claude
  { id: 'claude-opus-4.5', name: 'Claude Opus 4.5', provider: 'Anthropic', inputCost: 5.0, outputCost: 25.0, cacheCost: 0.5, isFree: false, lastUpdated: '2026-02-02' },
  { id: 'claude-sonnet-4.5', name: 'Claude Sonnet 4.5', provider: 'Anthropic', inputCost: 3.0, outputCost: 15.0, cacheCost: 0.3, isFree: false, lastUpdated: '2026-02-02' },
  { id: 'claude-haiku-4.5', name: 'Claude Haiku 4.5', provider: 'Anthropic', inputCost: 1.0, outputCost: 5.0, cacheCost: 0.1, isFree: false, lastUpdated: '2026-02-02' },
  // OpenAI
  { id: 'gpt-4o', name: 'GPT-4o', provider: 'OpenAI', inputCost: 2.5, outputCost: 10.0, isFree: false, lastUpdated: '2026-02-02' },
  { id: 'gpt-4o-mini', name: 'GPT-4o-mini', provider: 'OpenAI', inputCost: 0.15, outputCost: 0.6, cacheCost: 0.08, isFree: false, lastUpdated: '2026-02-02' },
  // DeepSeek
  { id: 'deepseek-chat', name: 'DeepSeek Chat', provider: 'DeepSeek', inputCost: 0.28, outputCost: 0.42, cacheCost: 0.028, isFree: false, lastUpdated: '2026-02-02' },
  // Free Models (OpenRouter)
  { id: 'llama-3.3-70b', name: 'Llama 3.3 70B', provider: 'OpenRouter', inputCost: 0, outputCost: 0, isFree: true, lastUpdated: '2026-02-02' },
  { id: 'gemini-2.0-flash-exp', name: 'Gemini 2.0 Flash', provider: 'OpenRouter', inputCost: 0, outputCost: 0, isFree: true, lastUpdated: '2026-02-02' },
];

// =============================================================================
// USER TIERS - Access Control
// =============================================================================

export interface UserTierData {
  id: string;
  name: string;
  hasAccess: boolean;
  maxStrategy: string;
  cacheTtlHours: number;
  description: string;
}

export const USER_TIERS: UserTierData[] = [
  { id: 'anonymous', name: 'Anonymous', hasAccess: false, maxStrategy: 'NONE', cacheTtlHours: 0, description: 'NO ACCESS - Authentication required' },
  { id: 'user', name: 'User', hasAccess: true, maxStrategy: 'BALANCED', cacheTtlHours: 48, description: 'Standard authenticated user' },
  { id: 'editor', name: 'Editor', hasAccess: true, maxStrategy: 'PRECISE', cacheTtlHours: 72, description: 'Content editor with elevated access' },
  { id: 'admin', name: 'Admin', hasAccess: true, maxStrategy: 'CONSENSUS', cacheTtlHours: 168, description: 'Full system access' },
  { id: 'premium', name: 'Premium', hasAccess: true, maxStrategy: 'CONSENSUS', cacheTtlHours: 336, description: 'Premium subscriber with priority' },
];

// =============================================================================
// GLOBAL METRICS - Key Performance Indicators
// =============================================================================

/**
 * Global metrics - Key Performance Indicators
 * NOTE: totalVariants = 31 (was 36, removed 5 Anonymous variants)
 * Anonymous users have NO ACCESS to RAG system.
 */
export const METRICS = {
  totalVariants: 31, // Updated: removed 5 Anonymous variants
  avgTokensPerQuery: 1310,
  tokenReduction: -35, // percentage
  cacheHitRateTarget: 80, // percentage
  accuracy: {
    ruleLookup: { baseline: 80, target: 95 },
    strategy: { baseline: 75, target: 90 },
  },
  monthlyCost: {
    baseline: 800,
    optimized: 419, // at 100K queries (original estimate)
    updated: 2053, // recalculated with 2026 pricing
  },
};

// =============================================================================
// DOCUMENTATION STRUCTURE
// =============================================================================

export const DOC_STRUCTURE = {
  core: [
    { path: '00-overview.md', title: 'Overview', description: 'Executive summary with performance targets' },
    { path: 'HOW-IT-WORKS.md', title: 'How It Works', description: 'Complete step-by-step explanation' },
  ],
  layers: [
    { path: '02-layer1-routing.md', title: 'L1: Routing', description: '3D routing logic' },
    { path: '03-layer2-caching.md', title: 'L2: Cache', description: 'Semantic cache implementation' },
    { path: '04-layer3-retrieval.md', title: 'L3: Retrieval', description: 'Modular retrieval strategies' },
    { path: '05-layer4-crag-evaluation.md', title: 'L4: CRAG', description: 'Quality evaluation' },
    { path: '06-layer5-generation.md', title: 'L5: Generation', description: 'Adaptive generation' },
    { path: '07-layer6-validation.md', title: 'L6: Validation', description: 'Self-validation' },
  ],
  advanced: [
    { path: '08-token-optimization.md', title: 'Token Optimization', description: '10 optimization techniques' },
    { path: '09-multi-agent-orchestration.md', title: 'Multi-Agent', description: 'LangGraph 3-agent system' },
  ],
  implementation: [
    { path: '10-implementation-guide.md', title: 'Implementation', description: 'Code examples and setup' },
    { path: '11-testing-strategy.md', title: 'Testing', description: 'Test specifications' },
    { path: '12-monitoring-metrics.md', title: 'Monitoring', description: 'Prometheus + Grafana' },
    { path: '13-deployment-rollout.md', title: 'Deployment', description: '12-week roadmap' },
  ],
  appendices: [
    { path: 'appendix/A-research-sources.md', title: 'Research Sources', description: '53 citations' },
    { path: 'appendix/B-variant-comparison.md', title: 'Variant Comparison', description: '31 variants matrix' },
    { path: 'appendix/C-token-cost-breakdown.md', title: 'Token Breakdown', description: 'Detailed analysis' },
    { path: 'appendix/D-data-consistency-audit.md', title: 'Data Audit', description: 'Consistency validation' },
    { path: 'appendix/E-model-pricing-2026.md', title: 'Model Pricing', description: 'Current pricing' },
    { path: 'appendix/F-calculation-formulas.md', title: 'Formulas', description: 'Calculation documentation' },
    { path: 'appendix/G-admin-configuration-system.md', title: 'Admin Config', description: 'Configuration system' },
  ],
};

// =============================================================================
// UTILITY FUNCTIONS
// =============================================================================

/**
 * Calculate cost for a query
 */
export function calculateQueryCost(
  tokens: number,
  modelId: string,
  inputRatio: number = 0.7
): number {
  const model = MODEL_PRICING.find((m) => m.id === modelId);
  if (!model || model.isFree) return 0;

  const inputTokens = tokens * inputRatio;
  const outputTokens = tokens * (1 - inputRatio);

  return (inputTokens / 1_000_000) * model.inputCost + (outputTokens / 1_000_000) * model.outputCost;
}

/**
 * Get strategy by ID
 */
export function getStrategy(id: string): StrategyData | undefined {
  return STRATEGIES[id];
}

/**
 * Get layer by ID
 */
export function getLayer(id: string): LayerData | undefined {
  return LAYERS.find((l) => l.id === id);
}

/**
 * Format latency for display
 */
export function formatLatency(minMs: number, maxMs: number): string {
  if (maxMs < 1000) return `${minMs}-${maxMs}ms`;
  return `${(minMs / 1000).toFixed(0)}-${(maxMs / 1000).toFixed(0)}s`;
}

/**
 * Calculate monthly cost projection
 */
export function calculateMonthlyCost(
  queriesPerMonth: number,
  distribution: Record<string, number> = {
    FAST: 0.6,
    BALANCED: 0.25,
    PRECISE: 0.1,
    EXPERT: 0.03,
    CONSENSUS: 0.02,
  },
  cacheHitRate: number = 0.8
): number {
  let total = 0;
  for (const [strategyId, percentage] of Object.entries(distribution)) {
    const strategy = STRATEGIES[strategyId];
    if (!strategy) continue;

    const queries = queriesPerMonth * percentage;
    const cacheMissCost = queries * (1 - cacheHitRate) * strategy.cost;
    const cacheHitCost = queries * cacheHitRate * 0.0001; // Minimal lookup
    total += cacheMissCost + cacheHitCost;
  }
  return total;
}
