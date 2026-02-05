/**
 * RAG Pipeline Builder - Public API
 *
 * Exports for building RAG visual pipelines with 23 blocks.
 *
 * @see #3454 - Block type system and metadata
 * @see #3456 - Block palette UI component
 * @see #3457 - ReactFlow canvas infrastructure
 * @see #3458 - Tier 1 blocks (7 essential RAG blocks)
 * @see #3459 - Drag-drop mechanics
 * @see #3460 - Block connection validation
 * @see #3461 - Parameter configuration panel
 * @see #3462 - Strategy validation engine
 */

// Types
export * from './types';

// Components
export { BlockPalette, type BlockPaletteProps } from './BlockPalette';
export { PipelineCanvas, type PipelineCanvasProps } from './PipelineCanvas';
export { RagBlockNode, type RagBlockNodeProps } from './RagBlockNode';
export { StrategyBuilder, type StrategyBuilderProps } from './StrategyBuilder';
export { BlockConfigPanel, type BlockConfigPanelProps } from './BlockConfigPanel';
export { ValidationPanel, type ValidationPanelProps } from './ValidationPanel';

// Specialized Tier 1 Nodes
export {
  VectorSearchNode,
  KeywordSearchNode,
  HybridSearchNode,
  RerankerNode,
  CragEvaluatorNode,
  ConfidenceScoringNode,
  CitationVerificationNode,
  TIER1_NODE_TYPES,
  hasSpecializedNode,
  type Tier1NodeType,
} from './nodes';

// Validation Engine
export {
  validatePipeline,
  isValidPipeline,
  validateConnection,
  ValidationCodes,
  type ValidationContext,
  type DetailedValidation,
} from './validation-engine';

// Block Definitions
export {
  ALL_BLOCKS,
  BLOCKS_BY_TYPE,
  BLOCKS_BY_CATEGORY,
  getBlock,
  getBlocksByCategory,
  isValidConnection,
  // Individual blocks for direct import
  VECTOR_SEARCH,
  KEYWORD_SEARCH,
  HYBRID_SEARCH,
  MULTI_HOP_RETRIEVAL,
  GRAPH_RAG,
  WEB_SEARCH,
  SENTENCE_WINDOW,
  CROSS_ENCODER_RERANKING,
  METADATA_FILTERING,
  DEDUPLICATION,
  DOCUMENT_REPACKING,
  CRAG_EVALUATOR,
  SELF_RAG,
  CONFIDENCE_SCORING,
  CITATION_VERIFICATION,
  HALLUCINATION_DETECTION,
  SEQUENTIAL_AGENT,
  PARALLEL_AGENT,
  SUPERVISOR_WORKER,
  QUERY_REWRITING,
  QUERY_DECOMPOSITION,
  HYDE,
  RAG_FUSION,
} from './block-definitions';

// Metadata
export {
  CATEGORY_META,
  TIER_HIERARCHY,
  BLOCKS_BY_TIER,
  PIPELINE_CONSTRAINTS,
  RECOMMENDED_COMBINATIONS,
  BLOCK_SUMMARY,
  getOrderedCategories,
  hasTierAccess,
  getBlocksForTier,
  getPerformanceTier,
  calculateBlockStatistics,
  generatePaletteGroups,
  filterPaletteGroups,
  getCombinationsForTier,
  validatePipelineConstraints,
  type CategoryMeta,
  type PerformanceTier,
  type BlockStatistics,
  type BlockCombination,
  type PipelineConstraintValidation,
} from './block-metadata';
