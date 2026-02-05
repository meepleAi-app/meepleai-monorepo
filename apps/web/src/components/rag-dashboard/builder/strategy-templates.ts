/**
 * RAG Pipeline Builder - Strategy Templates
 *
 * Pre-built strategy templates for quick start:
 * - Simple FAQ Bot: Basic retrieval for Q&A
 * - Balanced+: Hybrid search with reranking
 * - Research Assistant: Multi-hop with validation
 * - Maximum Quality: Full pipeline with all validations
 *
 * @see #3467 - Add strategy templates for quick start
 */

import {
  VECTOR_SEARCH,
  HYBRID_SEARCH,
  CROSS_ENCODER_RERANKING,
  CRAG_EVALUATOR,
  CONFIDENCE_SCORING,
  CITATION_VERIFICATION,
  HALLUCINATION_DETECTION,
  QUERY_REWRITING,
  MULTI_HOP_RETRIEVAL,
  DEDUPLICATION,
  DOCUMENT_REPACKING,
  SEQUENTIAL_AGENT,
  WEB_SEARCH,
} from './block-definitions';

import type { RagNode, RagEdge, PipelineDefinition, RagNodeData, UserTier } from './types';

// =============================================================================
// Template Types
// =============================================================================

export interface StrategyTemplate {
  id: string;
  name: string;
  description: string;
  icon: string;
  category: TemplateCategory;
  difficulty: 'beginner' | 'intermediate' | 'advanced';
  requiredTier: UserTier;
  estimatedTokens: number;
  estimatedLatencyMs: number;
  estimatedCost: number;
  accuracyScore: number; // 0-100
  useCases: string[];
  pipeline: PipelineDefinition;
}

export type TemplateCategory =
  | 'simple'
  | 'balanced'
  | 'research'
  | 'quality'
  | 'custom';

// =============================================================================
// Helper Functions
// =============================================================================

/** Create a node from a block definition */
function createNode(
  id: string,
  block: typeof VECTOR_SEARCH,
  x: number,
  y: number,
  paramOverrides?: Record<string, unknown>
): RagNode {
  const data: RagNodeData = {
    block,
    params: { ...block.defaultParams, ...paramOverrides },
    status: 'idle',
  };
  return {
    id,
    type: block.type,
    position: { x, y },
    data,
  };
}

/** Create an edge between nodes */
function createEdge(
  sourceId: string,
  targetId: string,
  sourcePortId: string = 'docs-out',
  targetPortId: string = 'docs-in'
): RagEdge {
  return {
    id: `edge-${sourceId}-${targetId}`,
    source: sourceId,
    target: targetId,
    sourcePortId,
    targetPortId,
    dataType: 'documents',
    animated: true,
  };
}

// =============================================================================
// Template 1: Simple FAQ Bot
// =============================================================================

const SIMPLE_FAQ_BOT_NODES: RagNode[] = [
  createNode('vector-1', VECTOR_SEARCH, 100, 200, { topK: 5 }),
  createNode('confidence-1', CONFIDENCE_SCORING, 400, 200),
];

const SIMPLE_FAQ_BOT_EDGES: RagEdge[] = [
  createEdge('vector-1', 'confidence-1'),
];

export const SIMPLE_FAQ_BOT: StrategyTemplate = {
  id: 'simple-faq-bot',
  name: 'Simple FAQ Bot',
  description: 'Basic vector search for straightforward Q&A. Fast, low cost, good for simple FAQ scenarios.',
  icon: '💬',
  category: 'simple',
  difficulty: 'beginner',
  requiredTier: 'User',
  estimatedTokens: 1100,
  estimatedLatencyMs: 130,
  estimatedCost: 0.0001,
  accuracyScore: 65,
  useCases: [
    'Simple FAQ systems',
    'Customer support basics',
    'Quick prototyping',
    'Low-volume applications',
  ],
  pipeline: {
    id: 'template-simple-faq-bot',
    name: 'Simple FAQ Bot',
    description: 'Basic vector search for straightforward Q&A',
    version: '1.0.0',
    nodes: SIMPLE_FAQ_BOT_NODES,
    edges: SIMPLE_FAQ_BOT_EDGES,
    createdAt: new Date('2026-02-05'),
    updatedAt: new Date('2026-02-05'),
    createdBy: 'system',
    isActive: true,
  },
};

// =============================================================================
// Template 2: Balanced+ Strategy
// =============================================================================

const BALANCED_PLUS_NODES: RagNode[] = [
  createNode('query-rewrite-1', QUERY_REWRITING, 100, 200, { numVariations: 2 }),
  createNode('hybrid-1', HYBRID_SEARCH, 350, 200, { alpha: [0.6, 0.6] }),
  createNode('reranker-1', CROSS_ENCODER_RERANKING, 600, 200, { topN: 5 }),
  createNode('crag-1', CRAG_EVALUATOR, 850, 200),
  createNode('confidence-1', CONFIDENCE_SCORING, 1100, 200),
];

const BALANCED_PLUS_EDGES: RagEdge[] = [
  createEdge('query-rewrite-1', 'hybrid-1', 'query-out', 'query-in'),
  createEdge('hybrid-1', 'reranker-1'),
  createEdge('reranker-1', 'crag-1'),
  createEdge('crag-1', 'confidence-1'),
];

export const BALANCED_PLUS: StrategyTemplate = {
  id: 'balanced-plus',
  name: 'Balanced+',
  description: 'Hybrid search with query rewriting and cross-encoder reranking. Best balance of quality and cost.',
  icon: '⚖️',
  category: 'balanced',
  difficulty: 'intermediate',
  requiredTier: 'User',
  estimatedTokens: 2500,
  estimatedLatencyMs: 850,
  estimatedCost: 0.003,
  accuracyScore: 80,
  useCases: [
    'Production RAG systems',
    'Customer support',
    'Knowledge base search',
    'General Q&A applications',
  ],
  pipeline: {
    id: 'template-balanced-plus',
    name: 'Balanced+',
    description: 'Hybrid search with query rewriting and cross-encoder reranking',
    version: '1.0.0',
    nodes: BALANCED_PLUS_NODES,
    edges: BALANCED_PLUS_EDGES,
    createdAt: new Date('2026-02-05'),
    updatedAt: new Date('2026-02-05'),
    createdBy: 'system',
    isActive: true,
  },
};

// =============================================================================
// Template 3: Research Assistant
// =============================================================================

const RESEARCH_ASSISTANT_NODES: RagNode[] = [
  createNode('query-rewrite-1', QUERY_REWRITING, 100, 100, { numVariations: 3 }),
  createNode('multi-hop-1', MULTI_HOP_RETRIEVAL, 350, 100, { hops: 3 }),
  createNode('dedup-1', DEDUPLICATION, 600, 100),
  createNode('reranker-1', CROSS_ENCODER_RERANKING, 850, 100, { topN: 7 }),
  createNode('repack-1', DOCUMENT_REPACKING, 100, 300, { method: 'sandwich' }),
  createNode('crag-1', CRAG_EVALUATOR, 350, 300),
  createNode('web-search-1', WEB_SEARCH, 600, 300, { maxQueries: 2 }),
  createNode('sequential-1', SEQUENTIAL_AGENT, 850, 300),
];

const RESEARCH_ASSISTANT_EDGES: RagEdge[] = [
  createEdge('query-rewrite-1', 'multi-hop-1', 'query-out', 'query-in'),
  createEdge('multi-hop-1', 'dedup-1'),
  createEdge('dedup-1', 'reranker-1'),
  createEdge('reranker-1', 'repack-1'),
  createEdge('repack-1', 'crag-1'),
  createEdge('crag-1', 'web-search-1'),
  createEdge('web-search-1', 'sequential-1'),
];

export const RESEARCH_ASSISTANT: StrategyTemplate = {
  id: 'research-assistant',
  name: 'Research Assistant',
  description: 'Multi-hop retrieval with web fallback for complex research queries. Thorough but slower.',
  icon: '🔬',
  category: 'research',
  difficulty: 'advanced',
  requiredTier: 'Editor',
  estimatedTokens: 18000,
  estimatedLatencyMs: 9500,
  estimatedCost: 0.08,
  accuracyScore: 88,
  useCases: [
    'Deep research queries',
    'Multi-step reasoning',
    'Academic research',
    'Complex analysis tasks',
  ],
  pipeline: {
    id: 'template-research-assistant',
    name: 'Research Assistant',
    description: 'Multi-hop retrieval with web fallback for complex research queries',
    version: '1.0.0',
    nodes: RESEARCH_ASSISTANT_NODES,
    edges: RESEARCH_ASSISTANT_EDGES,
    createdAt: new Date('2026-02-05'),
    updatedAt: new Date('2026-02-05'),
    createdBy: 'system',
    isActive: true,
  },
};

// =============================================================================
// Template 4: Maximum Quality
// =============================================================================

const MAXIMUM_QUALITY_NODES: RagNode[] = [
  // Query processing row
  createNode('query-rewrite-1', QUERY_REWRITING, 100, 100, { numVariations: 3 }),
  createNode('hybrid-1', HYBRID_SEARCH, 350, 100, { alpha: [0.5, 0.5], vectorTopK: 20 }),
  createNode('dedup-1', DEDUPLICATION, 600, 100, { similarityThreshold: [0.9, 1.0] }),
  createNode('reranker-1', CROSS_ENCODER_RERANKING, 850, 100, { topN: 10, inputTopK: 50 }),
  // Validation row
  createNode('repack-1', DOCUMENT_REPACKING, 100, 300, { method: 'sandwich' }),
  createNode('crag-1', CRAG_EVALUATOR, 350, 300, { evaluatorModel: 'llm' }),
  createNode('citation-1', CITATION_VERIFICATION, 600, 300, { method: 'nli' }),
  createNode('hallucination-1', HALLUCINATION_DETECTION, 850, 300, { useFactChecker: true }),
  // Output row
  createNode('confidence-1', CONFIDENCE_SCORING, 450, 500, { method: 'multi-model' }),
];

const MAXIMUM_QUALITY_EDGES: RagEdge[] = [
  // Query processing chain
  createEdge('query-rewrite-1', 'hybrid-1', 'query-out', 'query-in'),
  createEdge('hybrid-1', 'dedup-1'),
  createEdge('dedup-1', 'reranker-1'),
  createEdge('reranker-1', 'repack-1'),
  // Validation chain
  createEdge('repack-1', 'crag-1'),
  createEdge('crag-1', 'citation-1'),
  createEdge('citation-1', 'hallucination-1'),
  // Output
  createEdge('hallucination-1', 'confidence-1', 'eval-out', 'docs-in'),
];

export const MAXIMUM_QUALITY: StrategyTemplate = {
  id: 'maximum-quality',
  name: 'Maximum Quality',
  description: 'Full pipeline with all validation layers. Highest accuracy but slowest and most expensive.',
  icon: '🏆',
  category: 'quality',
  difficulty: 'advanced',
  requiredTier: 'Admin',
  estimatedTokens: 4500,
  estimatedLatencyMs: 1200,
  estimatedCost: 0.006,
  accuracyScore: 95,
  useCases: [
    'High-stakes applications',
    'Medical/legal domains',
    'Financial advice',
    'Safety-critical systems',
  ],
  pipeline: {
    id: 'template-maximum-quality',
    name: 'Maximum Quality',
    description: 'Full pipeline with all validation layers for maximum accuracy',
    version: '1.0.0',
    nodes: MAXIMUM_QUALITY_NODES,
    edges: MAXIMUM_QUALITY_EDGES,
    createdAt: new Date('2026-02-05'),
    updatedAt: new Date('2026-02-05'),
    createdBy: 'system',
    isActive: true,
  },
};

// =============================================================================
// Template Registry
// =============================================================================

/** All available strategy templates */
export const ALL_TEMPLATES: StrategyTemplate[] = [
  SIMPLE_FAQ_BOT,
  BALANCED_PLUS,
  RESEARCH_ASSISTANT,
  MAXIMUM_QUALITY,
];

/** Templates by ID for quick lookup */
export const TEMPLATES_BY_ID: Record<string, StrategyTemplate> = ALL_TEMPLATES.reduce(
  (acc, template) => ({ ...acc, [template.id]: template }),
  {} as Record<string, StrategyTemplate>
);

/** Templates grouped by category */
export const TEMPLATES_BY_CATEGORY: Record<TemplateCategory, StrategyTemplate[]> = {
  simple: ALL_TEMPLATES.filter((t) => t.category === 'simple'),
  balanced: ALL_TEMPLATES.filter((t) => t.category === 'balanced'),
  research: ALL_TEMPLATES.filter((t) => t.category === 'research'),
  quality: ALL_TEMPLATES.filter((t) => t.category === 'quality'),
  custom: [],
};

/** Get template by ID */
export function getTemplate(id: string): StrategyTemplate | undefined {
  return TEMPLATES_BY_ID[id];
}

/** Get templates available for a user tier */
export function getTemplatesForTier(userTier: UserTier): StrategyTemplate[] {
  const tierOrder: Record<UserTier, number> = { User: 1, Editor: 2, Admin: 3 };
  const userLevel = tierOrder[userTier];
  return ALL_TEMPLATES.filter((t) => tierOrder[t.requiredTier] <= userLevel);
}

/** Get recommended template based on use case keywords */
export function getRecommendedTemplate(useCase: string): StrategyTemplate {
  const lowerCase = useCase.toLowerCase();

  if (lowerCase.includes('research') || lowerCase.includes('complex') || lowerCase.includes('multi')) {
    return RESEARCH_ASSISTANT;
  }

  if (lowerCase.includes('quality') || lowerCase.includes('medical') || lowerCase.includes('legal') || lowerCase.includes('critical')) {
    return MAXIMUM_QUALITY;
  }

  if (lowerCase.includes('simple') || lowerCase.includes('faq') || lowerCase.includes('basic')) {
    return SIMPLE_FAQ_BOT;
  }

  // Default to balanced
  return BALANCED_PLUS;
}

/** Clone a template's pipeline for customization */
export function cloneTemplatePipeline(template: StrategyTemplate): PipelineDefinition {
  const now = new Date();
  return {
    ...template.pipeline,
    id: `custom-${template.id}-${Date.now()}`,
    name: `${template.name} (Custom)`,
    createdAt: now,
    updatedAt: now,
    createdBy: 'user',
    isActive: false,
    // Deep clone nodes and edges
    nodes: template.pipeline.nodes.map((node) => ({
      ...node,
      id: `${node.id}-${Date.now()}`,
      data: { ...node.data },
    })),
    edges: template.pipeline.edges.map((edge) => ({
      ...edge,
      id: `${edge.id}-${Date.now()}`,
    })),
  };
}
