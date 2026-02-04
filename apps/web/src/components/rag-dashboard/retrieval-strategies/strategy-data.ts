/**
 * RAG Retrieval Strategy Data
 *
 * Single source of truth for the 6 retrieval strategies in the RAG Dashboard.
 * Each strategy represents a different retrieval mechanism.
 */

import type { RetrievalStrategy, RetrievalStrategyType } from './types';

/**
 * Complete data for all 6 RAG retrieval strategies.
 */
export const RETRIEVAL_STRATEGIES: Record<RetrievalStrategyType, RetrievalStrategy> = {
  Hybrid: {
    id: 'Hybrid',
    name: 'Hybrid Search',
    shortDescription: 'Vector + Keyword combined for best of both worlds',
    icon: '🔀',
    color: 'blue',
    colorHsl: 'hsl(221 83% 53%)',
    glowColor: 'hsla(221, 83%, 53%, 0.4)',
    metrics: {
      latency: 'medium',
      accuracy: 'high',
      costTier: 'medium',
      latencyMs: { min: 100, max: 500, display: '100-500ms' },
      accuracyPercent: { min: 88, max: 95, display: '88-95%' },
      costPerQuery: { min: 0.001, max: 0.005, display: '$0.001-0.005' },
    },
    tags: ['balanced', 'general-purpose', 'recommended'],
  },
  Semantic: {
    id: 'Semantic',
    name: 'Semantic Search',
    shortDescription: 'Pure embedding/vector search for meaning-based retrieval',
    icon: '🧠',
    color: 'purple',
    colorHsl: 'hsl(262 83% 62%)',
    glowColor: 'hsla(262, 83%, 62%, 0.4)',
    metrics: {
      latency: 'low',
      accuracy: 'high',
      costTier: 'low',
      latencyMs: { min: 50, max: 200, display: '50-200ms' },
      accuracyPercent: { min: 85, max: 92, display: '85-92%' },
      costPerQuery: { min: 0.0005, max: 0.002, display: '$0.0005-0.002' },
    },
    tags: ['fast', 'meaning-based', 'conceptual'],
  },
  Keyword: {
    id: 'Keyword',
    name: 'Keyword Search',
    shortDescription: 'Classic BM25/TF-IDF for exact term matching',
    icon: '🔑',
    color: 'green',
    colorHsl: 'hsl(142 76% 36%)',
    glowColor: 'hsla(142, 76%, 36%, 0.4)',
    metrics: {
      latency: 'low',
      accuracy: 'medium',
      costTier: 'low',
      latencyMs: { min: 20, max: 100, display: '20-100ms' },
      accuracyPercent: { min: 70, max: 85, display: '70-85%' },
      costPerQuery: { min: 0.0001, max: 0.001, display: '$0.0001-0.001' },
    },
    tags: ['exact-match', 'fast', 'traditional'],
  },
  Contextual: {
    id: 'Contextual',
    name: 'Contextual Search',
    shortDescription: 'Context-aware retrieval with conversation memory',
    icon: '💬',
    color: 'orange',
    colorHsl: 'hsl(25 95% 53%)',
    glowColor: 'hsla(25, 95%, 53%, 0.4)',
    metrics: {
      latency: 'medium',
      accuracy: 'high',
      costTier: 'medium',
      latencyMs: { min: 200, max: 800, display: '200-800ms' },
      accuracyPercent: { min: 90, max: 96, display: '90-96%' },
      costPerQuery: { min: 0.002, max: 0.008, display: '$0.002-0.008' },
    },
    tags: ['memory', 'conversation', 'stateful'],
  },
  MultiQuery: {
    id: 'MultiQuery',
    name: 'Multi-Query',
    shortDescription: 'Automatic query expansion for comprehensive retrieval',
    icon: '🔄',
    color: 'cyan',
    colorHsl: 'hsl(186 78% 42%)',
    glowColor: 'hsla(186, 78%, 42%, 0.4)',
    metrics: {
      latency: 'high',
      accuracy: 'high',
      costTier: 'high',
      latencyMs: { min: 500, max: 2000, display: '500ms-2s' },
      accuracyPercent: { min: 92, max: 97, display: '92-97%' },
      costPerQuery: { min: 0.005, max: 0.02, display: '$0.005-0.02' },
    },
    tags: ['comprehensive', 'expansion', 'thorough'],
  },
  Agentic: {
    id: 'Agentic',
    name: 'Agentic RAG',
    shortDescription: 'Multi-hop reasoning with autonomous decision making',
    icon: '🤖',
    color: 'red',
    colorHsl: 'hsl(0 72% 51%)',
    glowColor: 'hsla(0, 72%, 51%, 0.4)',
    metrics: {
      latency: 'variable',
      accuracy: 'high',
      costTier: 'high',
      latencyMs: { min: 2000, max: 15000, display: '2-15s' },
      accuracyPercent: { min: 95, max: 99, display: '95-99%' },
      costPerQuery: { min: 0.02, max: 0.15, display: '$0.02-0.15' },
    },
    tags: ['multi-hop', 'autonomous', 'complex'],
  },
};

/**
 * Get all retrieval strategies as an array.
 */
export function getAllRetrievalStrategies(): RetrievalStrategy[] {
  return Object.values(RETRIEVAL_STRATEGIES);
}

/**
 * Get a retrieval strategy by ID.
 */
export function getRetrievalStrategy(id: RetrievalStrategyType): RetrievalStrategy | undefined {
  return RETRIEVAL_STRATEGIES[id];
}

/**
 * Strategy order for display (recommended first).
 */
export const RETRIEVAL_STRATEGY_ORDER: RetrievalStrategyType[] = [
  'Hybrid',
  'Semantic',
  'Keyword',
  'Contextual',
  'MultiQuery',
  'Agentic',
];
