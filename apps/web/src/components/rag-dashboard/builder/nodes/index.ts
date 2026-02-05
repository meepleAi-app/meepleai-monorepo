/**
 * RAG Pipeline Builder - Custom Node Components
 *
 * Specialized ReactFlow nodes for Tier 1 & 2 RAG blocks with enhanced visuals.
 *
 * @see #3458 - Implement Tier 1 blocks (7 essential RAG blocks)
 * @see #3465 - Implement Tier 2 advanced blocks (6 blocks)
 */

// Tier 1 nodes
export { VectorSearchNode } from './VectorSearchNode';
export { KeywordSearchNode } from './KeywordSearchNode';
export { HybridSearchNode } from './HybridSearchNode';
export { RerankerNode } from './RerankerNode';
export { CragEvaluatorNode } from './CragEvaluatorNode';
export { ConfidenceScoringNode } from './ConfidenceScoringNode';
export { CitationVerificationNode } from './CitationVerificationNode';

// Tier 2 nodes
export { MultiHopRetrievalNode } from './MultiHopRetrievalNode';
export { QueryRewritingNode } from './QueryRewritingNode';
export { QueryDecompositionNode } from './QueryDecompositionNode';
export { MetadataFilteringNode } from './MetadataFilteringNode';
export { DocumentRepackingNode } from './DocumentRepackingNode';
export { HallucinationDetectionNode } from './HallucinationDetectionNode';

// Node type registry for ReactFlow - Tier 1
export const TIER1_NODE_TYPES = {
  'vector-search': 'vectorSearch',
  'keyword-search': 'keywordSearch',
  'hybrid-search': 'hybridSearch',
  'cross-encoder-reranking': 'reranker',
  'crag-evaluator': 'cragEvaluator',
  'confidence-scoring': 'confidenceScoring',
  'citation-verification': 'citationVerification',
} as const;

// Node type registry for ReactFlow - Tier 2
export const TIER2_NODE_TYPES = {
  'multi-hop-retrieval': 'multiHopRetrieval',
  'query-rewriting': 'queryRewriting',
  'query-decomposition': 'queryDecomposition',
  'metadata-filtering': 'metadataFiltering',
  'document-repacking': 'documentRepacking',
  'hallucination-detection': 'hallucinationDetection',
} as const;

// Combined registry
export const ALL_SPECIALIZED_NODE_TYPES = {
  ...TIER1_NODE_TYPES,
  ...TIER2_NODE_TYPES,
} as const;

export type Tier1NodeType = keyof typeof TIER1_NODE_TYPES;
export type Tier2NodeType = keyof typeof TIER2_NODE_TYPES;
export type SpecializedNodeType = keyof typeof ALL_SPECIALIZED_NODE_TYPES;

/** Check if block type has a specialized node */
export function hasSpecializedNode(blockType: string): boolean {
  return blockType in ALL_SPECIALIZED_NODE_TYPES;
}
