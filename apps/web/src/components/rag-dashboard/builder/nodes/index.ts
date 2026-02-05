/**
 * RAG Pipeline Builder - Custom Node Components
 *
 * Specialized ReactFlow nodes for Tier 1 RAG blocks with enhanced visuals.
 *
 * @see #3458 - Implement Tier 1 blocks (7 essential RAG blocks)
 */

export { VectorSearchNode } from './VectorSearchNode';
export { KeywordSearchNode } from './KeywordSearchNode';
export { HybridSearchNode } from './HybridSearchNode';
export { RerankerNode } from './RerankerNode';
export { CragEvaluatorNode } from './CragEvaluatorNode';
export { ConfidenceScoringNode } from './ConfidenceScoringNode';
export { CitationVerificationNode } from './CitationVerificationNode';

// Node type registry for ReactFlow
export const TIER1_NODE_TYPES = {
  'vector-search': 'vectorSearch',
  'keyword-search': 'keywordSearch',
  'hybrid-search': 'hybridSearch',
  'cross-encoder-reranking': 'reranker',
  'crag-evaluator': 'cragEvaluator',
  'confidence-scoring': 'confidenceScoring',
  'citation-verification': 'citationVerification',
} as const;

export type Tier1NodeType = keyof typeof TIER1_NODE_TYPES;

/** Check if block type has a specialized node */
export function hasSpecializedNode(blockType: string): boolean {
  return blockType in TIER1_NODE_TYPES;
}
