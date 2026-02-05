/**
 * RAG Pipeline Builder - Block Metadata
 *
 * Aggregated metadata, statistics, and configuration for 23 RAG building blocks.
 * Provides palette organization, tier requirements, and cost/performance estimates.
 *
 * @see #3454 - Define block type system and metadata
 */

import { ALL_BLOCKS, BLOCKS_BY_CATEGORY, BLOCKS_BY_TYPE } from './block-definitions';

import type {
  BlockCategory,
  RagBlockType,
  UserTier,
  PaletteGroup,
  PaletteItem,
} from './types';

// =============================================================================
// Category Metadata
// =============================================================================

/** Category display information */
export interface CategoryMeta {
  id: BlockCategory;
  label: string;
  description: string;
  icon: string;
  color: string;
  order: number;
}

/** Category metadata for palette organization */
export const CATEGORY_META: Record<BlockCategory, CategoryMeta> = {
  retrieval: {
    id: 'retrieval',
    label: 'Retrieval',
    description: 'Search and retrieve documents from knowledge base',
    icon: '📦',
    color: 'hsl(142 76% 36%)',
    order: 1,
  },
  optimization: {
    id: 'optimization',
    label: 'Optimization',
    description: 'Transform and optimize queries before retrieval',
    icon: '⚡',
    color: 'hsl(221 83% 53%)',
    order: 2,
  },
  ranking: {
    id: 'ranking',
    label: 'Ranking',
    description: 'Rerank, filter, and organize retrieved documents',
    icon: '🎯',
    color: 'hsl(45 93% 47%)',
    order: 3,
  },
  validation: {
    id: 'validation',
    label: 'Validation',
    description: 'Evaluate and validate retrieval quality',
    icon: '✅',
    color: 'hsl(0 72% 51%)',
    order: 4,
  },
  agents: {
    id: 'agents',
    label: 'Agents',
    description: 'Multi-agent patterns for complex workflows',
    icon: '🤖',
    color: 'hsl(262 83% 62%)',
    order: 5,
  },
  control: {
    id: 'control',
    label: 'Control Flow',
    description: 'Conditional logic and flow control',
    icon: '🔀',
    color: 'hsl(200 83% 53%)',
    order: 6,
  },
};

/** Get ordered categories for palette */
export function getOrderedCategories(): CategoryMeta[] {
  return Object.values(CATEGORY_META).sort((a, b) => a.order - b.order);
}

// =============================================================================
// Tier Requirements
// =============================================================================

/** Tier hierarchy for access control */
export const TIER_HIERARCHY: Record<UserTier, number> = {
  User: 1,
  Editor: 2,
  Admin: 3,
};

/** Check if user tier has access to block */
export function hasTierAccess(userTier: UserTier, requiredTier: UserTier): boolean {
  return TIER_HIERARCHY[userTier] >= TIER_HIERARCHY[requiredTier];
}

/** Get blocks available for a tier */
export function getBlocksForTier(userTier: UserTier): RagBlockType[] {
  return ALL_BLOCKS
    .filter((block) => hasTierAccess(userTier, block.requiredTier))
    .map((block) => block.type);
}

/** Blocks by tier requirement */
export const BLOCKS_BY_TIER: Record<UserTier, RagBlockType[]> = {
  User: ALL_BLOCKS.filter((b) => b.requiredTier === 'User').map((b) => b.type),
  Editor: ALL_BLOCKS.filter((b) => b.requiredTier === 'Editor').map((b) => b.type),
  Admin: ALL_BLOCKS.filter((b) => b.requiredTier === 'Admin').map((b) => b.type),
};

// =============================================================================
// Cost & Performance Metadata
// =============================================================================

/** Block performance tier classification */
export type PerformanceTier = 'fast' | 'standard' | 'slow' | 'expensive';

/** Performance thresholds */
const PERFORMANCE_THRESHOLDS = {
  fast: { maxLatency: 100, maxTokens: 500, maxCost: 0.001 },
  standard: { maxLatency: 500, maxTokens: 2000, maxCost: 0.01 },
  slow: { maxLatency: 2000, maxTokens: 8000, maxCost: 0.05 },
  // expensive: anything above slow thresholds
};

/** Classify block by performance */
export function getPerformanceTier(blockType: RagBlockType): PerformanceTier {
  const block = BLOCKS_BY_TYPE[blockType];
  if (!block) return 'standard';

  const { estimatedLatencyMs, estimatedTokens, estimatedCost } = block;

  if (
    estimatedLatencyMs <= PERFORMANCE_THRESHOLDS.fast.maxLatency &&
    estimatedTokens <= PERFORMANCE_THRESHOLDS.fast.maxTokens &&
    estimatedCost <= PERFORMANCE_THRESHOLDS.fast.maxCost
  ) {
    return 'fast';
  }

  if (
    estimatedLatencyMs <= PERFORMANCE_THRESHOLDS.standard.maxLatency &&
    estimatedTokens <= PERFORMANCE_THRESHOLDS.standard.maxTokens &&
    estimatedCost <= PERFORMANCE_THRESHOLDS.standard.maxCost
  ) {
    return 'standard';
  }

  if (
    estimatedLatencyMs <= PERFORMANCE_THRESHOLDS.slow.maxLatency &&
    estimatedTokens <= PERFORMANCE_THRESHOLDS.slow.maxTokens &&
    estimatedCost <= PERFORMANCE_THRESHOLDS.slow.maxCost
  ) {
    return 'slow';
  }

  return 'expensive';
}

/** Aggregated statistics for all blocks */
export interface BlockStatistics {
  totalBlocks: number;
  blocksByCategory: Record<BlockCategory, number>;
  blocksByTier: Record<UserTier, number>;
  avgTokens: number;
  avgLatency: number;
  avgCost: number;
  totalMaxTokens: number;
  totalMaxLatency: number;
  totalMaxCost: number;
}

/** Calculate aggregate statistics */
export function calculateBlockStatistics(): BlockStatistics {
  const totalBlocks = ALL_BLOCKS.length;
  const blocksByCategory = Object.fromEntries(
    Object.keys(CATEGORY_META).map((cat) => [
      cat,
      BLOCKS_BY_CATEGORY[cat as BlockCategory]?.length || 0,
    ])
  ) as Record<BlockCategory, number>;

  const blocksByTier: Record<UserTier, number> = {
    User: BLOCKS_BY_TIER.User.length,
    Editor: BLOCKS_BY_TIER.Editor.length,
    Admin: BLOCKS_BY_TIER.Admin.length,
  };

  const totalTokens = ALL_BLOCKS.reduce((sum, b) => sum + b.estimatedTokens, 0);
  const totalLatency = ALL_BLOCKS.reduce((sum, b) => sum + b.estimatedLatencyMs, 0);
  const totalCost = ALL_BLOCKS.reduce((sum, b) => sum + b.estimatedCost, 0);

  return {
    totalBlocks,
    blocksByCategory,
    blocksByTier,
    avgTokens: Math.round(totalTokens / totalBlocks),
    avgLatency: Math.round(totalLatency / totalBlocks),
    avgCost: totalCost / totalBlocks,
    totalMaxTokens: Math.max(...ALL_BLOCKS.map((b) => b.estimatedTokens)),
    totalMaxLatency: Math.max(...ALL_BLOCKS.map((b) => b.estimatedLatencyMs)),
    totalMaxCost: Math.max(...ALL_BLOCKS.map((b) => b.estimatedCost)),
  };
}

// =============================================================================
// Palette Generation
// =============================================================================

/** Generate palette groups for UI */
export function generatePaletteGroups(userTier: UserTier): PaletteGroup[] {
  const orderedCategories = getOrderedCategories();

  return orderedCategories.map((categoryMeta) => {
    const categoryBlocks = BLOCKS_BY_CATEGORY[categoryMeta.id] || [];

    const items: PaletteItem[] = categoryBlocks.map((block) => {
      const hasAccess = hasTierAccess(userTier, block.requiredTier);
      return {
        block,
        disabled: !hasAccess,
        disabledReason: hasAccess
          ? undefined
          : `Requires ${block.requiredTier} tier or higher`,
      };
    });

    return {
      category: categoryMeta.id,
      label: categoryMeta.label,
      icon: categoryMeta.icon,
      items,
      collapsed: false,
    };
  });
}

/** Filter palette groups by search query */
export function filterPaletteGroups(
  groups: PaletteGroup[],
  searchQuery: string
): PaletteGroup[] {
  if (!searchQuery.trim()) return groups;

  const query = searchQuery.toLowerCase();

  return groups
    .map((group) => ({
      ...group,
      items: group.items.filter(
        (item) =>
          item.block.name.toLowerCase().includes(query) ||
          item.block.description.toLowerCase().includes(query) ||
          item.block.useCases.some((uc) => uc.toLowerCase().includes(query))
      ),
    }))
    .filter((group) => group.items.length > 0);
}

// =============================================================================
// Recommended Combinations
// =============================================================================

/** Pre-defined block combinations for common use cases */
export interface BlockCombination {
  id: string;
  name: string;
  description: string;
  blocks: RagBlockType[];
  estimatedTokens: number;
  estimatedLatency: number;
  estimatedCost: number;
  accuracy: string;
  requiredTier: UserTier;
}

/** Recommended block combinations */
export const RECOMMENDED_COMBINATIONS: BlockCombination[] = [
  {
    id: 'simple-fast',
    name: 'Simple (FAST)',
    description: 'Basic vector search for quick FAQ answers',
    blocks: ['vector-search', 'confidence-scoring'],
    estimatedTokens: 1100,
    estimatedLatency: 130,
    estimatedCost: 0.0001,
    accuracy: '78-85%',
    requiredTier: 'User',
  },
  {
    id: 'standard-balanced',
    name: 'Standard (BALANCED)',
    description: 'Hybrid search with CRAG evaluation',
    blocks: ['hybrid-search', 'cross-encoder-reranking', 'crag-evaluator'],
    estimatedTokens: 2000,
    estimatedLatency: 800,
    estimatedCost: 0.003,
    accuracy: '88-94%',
    requiredTier: 'User',
  },
  {
    id: 'advanced-precise',
    name: 'Advanced (PRECISE)',
    description: 'Multi-agent pipeline with full validation',
    blocks: [
      'query-decomposition',
      'hybrid-search',
      'cross-encoder-reranking',
      'crag-evaluator',
      'sequential-agent',
      'citation-verification',
      'hallucination-detection',
    ],
    estimatedTokens: 12000,
    estimatedLatency: 6500,
    estimatedCost: 0.07,
    accuracy: '95-98%',
    requiredTier: 'Editor',
  },
  {
    id: 'expert-web',
    name: 'Expert (Web-Augmented)',
    description: 'Web search with multi-hop reasoning',
    blocks: [
      'query-rewriting',
      'hybrid-search',
      'web-search',
      'cross-encoder-reranking',
      'crag-evaluator',
      'sequential-agent',
    ],
    estimatedTokens: 15000,
    estimatedLatency: 8000,
    estimatedCost: 0.08,
    accuracy: '92-96%',
    requiredTier: 'Editor',
  },
  {
    id: 'consensus-voting',
    name: 'Consensus (Multi-LLM)',
    description: 'Parallel agents with majority voting',
    blocks: [
      'hybrid-search',
      'cross-encoder-reranking',
      'parallel-agent',
      'confidence-scoring',
    ],
    estimatedTokens: 14000,
    estimatedLatency: 3500,
    estimatedCost: 0.09,
    accuracy: '97-99%',
    requiredTier: 'Admin',
  },
];

/** Get combinations available for a tier */
export function getCombinationsForTier(userTier: UserTier): BlockCombination[] {
  return RECOMMENDED_COMBINATIONS.filter((combo) =>
    hasTierAccess(userTier, combo.requiredTier)
  );
}

// =============================================================================
// Validation Constraints
// =============================================================================

/** Pipeline validation constraints */
export const PIPELINE_CONSTRAINTS = {
  /** Maximum tokens per pipeline execution */
  maxTokens: 30000,
  /** Maximum latency in milliseconds */
  maxLatencyMs: 30000,
  /** Maximum cost per execution in USD */
  maxCostUsd: 0.50,
  /** Minimum accuracy target */
  minAccuracy: 0.80,
  /** Maximum nodes in pipeline */
  maxNodes: 20,
  /** Maximum edges in pipeline */
  maxEdges: 50,
};

/** Validate pipeline against constraints */
export interface PipelineConstraintValidation {
  isValid: boolean;
  tokensValid: boolean;
  latencyValid: boolean;
  costValid: boolean;
  nodeCountValid: boolean;
  edgeCountValid: boolean;
  details: {
    estimatedTokens: number;
    estimatedLatency: number;
    estimatedCost: number;
    nodeCount: number;
    edgeCount: number;
  };
}

/** Validate a set of blocks against constraints */
export function validatePipelineConstraints(
  blockTypes: RagBlockType[],
  edgeCount: number
): PipelineConstraintValidation {
  const blocks = blockTypes.map((type) => BLOCKS_BY_TYPE[type]).filter(Boolean);

  const estimatedTokens = blocks.reduce((sum, b) => sum + b.estimatedTokens, 0);
  const estimatedLatency = blocks.reduce((sum, b) => sum + b.estimatedLatencyMs, 0);
  const estimatedCost = blocks.reduce((sum, b) => sum + b.estimatedCost, 0);
  const nodeCount = blocks.length;

  const tokensValid = estimatedTokens <= PIPELINE_CONSTRAINTS.maxTokens;
  const latencyValid = estimatedLatency <= PIPELINE_CONSTRAINTS.maxLatencyMs;
  const costValid = estimatedCost <= PIPELINE_CONSTRAINTS.maxCostUsd;
  const nodeCountValid = nodeCount <= PIPELINE_CONSTRAINTS.maxNodes;
  const edgeCountValid = edgeCount <= PIPELINE_CONSTRAINTS.maxEdges;

  return {
    isValid: tokensValid && latencyValid && costValid && nodeCountValid && edgeCountValid,
    tokensValid,
    latencyValid,
    costValid,
    nodeCountValid,
    edgeCountValid,
    details: {
      estimatedTokens,
      estimatedLatency,
      estimatedCost,
      nodeCount,
      edgeCount,
    },
  };
}

// =============================================================================
// Export Summary
// =============================================================================

/** Quick stats summary for display */
export const BLOCK_SUMMARY = {
  total: ALL_BLOCKS.length,
  categories: Object.keys(CATEGORY_META).length,
  retrieval: BLOCKS_BY_CATEGORY.retrieval.length,
  optimization: BLOCKS_BY_CATEGORY.optimization.length,
  ranking: BLOCKS_BY_CATEGORY.ranking.length,
  validation: BLOCKS_BY_CATEGORY.validation.length,
  agents: BLOCKS_BY_CATEGORY.agents.length,
  userTierBlocks: BLOCKS_BY_TIER.User.length,
  editorTierBlocks: BLOCKS_BY_TIER.Editor.length,
  adminTierBlocks: BLOCKS_BY_TIER.Admin.length,
};
