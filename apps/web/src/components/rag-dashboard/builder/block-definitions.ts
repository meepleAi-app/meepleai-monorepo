/**
 * RAG Pipeline Builder - Block Definitions
 *
 * Definitions for all 23 RAG building blocks with parameters and connections.
 * Based on research: docs/claudedocs/research_rag-building-blocks_2026-02-02.md
 *
 * @see #3454 - Define block type system and metadata
 */

import type {
  RagBlock,
  BlockParameter,
  ConnectionPort,
  RagBlockType,
  BlockCategory,
} from './types';

// =============================================================================
// Helper Functions
// =============================================================================

/** Create standard query input port */
const queryInput = (id = 'query-in'): ConnectionPort => ({
  id,
  name: 'Query',
  type: 'input',
  dataType: 'query',
  position: 'left',
});

/** Create standard documents input port */
const docsInput = (id = 'docs-in'): ConnectionPort => ({
  id,
  name: 'Documents',
  type: 'input',
  dataType: 'documents',
  position: 'left',
});

/** Create standard documents output port */
const docsOutput = (id = 'docs-out'): ConnectionPort => ({
  id,
  name: 'Documents',
  type: 'output',
  dataType: 'documents',
  position: 'right',
});

/** Create standard query output port */
const queryOutput = (id = 'query-out'): ConnectionPort => ({
  id,
  name: 'Query',
  type: 'output',
  dataType: 'query',
  position: 'right',
});

/** Create evaluation output port */
const evalOutput = (id = 'eval-out'): ConnectionPort => ({
  id,
  name: 'Evaluation',
  type: 'output',
  dataType: 'evaluation',
  position: 'right',
});

/** Create scores output port */
const scoresOutput = (id = 'scores-out'): ConnectionPort => ({
  id,
  name: 'Scores',
  type: 'output',
  dataType: 'scores',
  position: 'right',
});

// =============================================================================
// Category 1: Retrieval Blocks (7)
// =============================================================================

export const VECTOR_SEARCH: RagBlock = {
  id: 'vector-search',
  type: 'vector-search',
  name: 'Vector Search',
  category: 'retrieval',
  icon: '🔍',
  color: 'hsl(142 76% 36%)',
  parameters: [
    {
      id: 'topK',
      name: 'Top K',
      description: 'Number of chunks to retrieve',
      type: 'number',
      required: true,
      default: 5,
      min: 1,
      max: 50,
    },
    {
      id: 'minScore',
      name: 'Min Score',
      description: 'Minimum similarity threshold',
      type: 'range',
      required: true,
      default: [0.55, 1.0],
      min: 0,
      max: 1,
      step: 0.05,
    },
    {
      id: 'embeddingModel',
      name: 'Embedding Model',
      description: 'Model for generating embeddings',
      type: 'select',
      required: true,
      default: 'text-embedding-3-large',
      options: [
        { value: 'text-embedding-3-large', label: 'OpenAI Large' },
        { value: 'text-embedding-3-small', label: 'OpenAI Small' },
        { value: 'voyage-large-2', label: 'Voyage Large' },
      ],
    },
  ] as BlockParameter[],
  defaultParams: { topK: 5, minScore: [0.55, 1.0], embeddingModel: 'text-embedding-3-large' },
  inputs: [queryInput()],
  outputs: [docsOutput()],
  canConnectTo: ['cross-encoder-reranking', 'metadata-filtering', 'deduplication', 'document-repacking', 'crag-evaluator'],
  estimatedTokens: 1000,
  estimatedLatencyMs: 100,
  estimatedCost: 0.0001,
  accuracyImpact: 0.5,
  requiredTier: 'User',
  maxInstances: 3,
  description: 'Semantic similarity search using embeddings',
  useCases: ['Conceptual queries', 'Explain X queries', 'Semantic matching'],
  codeReferences: [
    { type: 'backend', path: 'BoundedContexts/KnowledgeBase/Domain/Plugins/Implementations/Retrieval/RetrievalVectorPlugin.cs', description: 'Vector retrieval implementation' },
  ],
};

export const KEYWORD_SEARCH: RagBlock = {
  id: 'keyword-search',
  type: 'keyword-search',
  name: 'Keyword Search',
  category: 'retrieval',
  icon: '📝',
  color: 'hsl(142 76% 36%)',
  parameters: [
    {
      id: 'topK',
      name: 'Top K',
      description: 'Number of documents to retrieve',
      type: 'number',
      required: true,
      default: 10,
      min: 1,
      max: 100,
    },
    {
      id: 'boostFactors',
      name: 'Field Boost',
      description: 'Boost weights for different fields',
      type: 'select',
      required: false,
      default: 'balanced',
      options: [
        { value: 'balanced', label: 'Balanced' },
        { value: 'title-heavy', label: 'Title Heavy' },
        { value: 'content-heavy', label: 'Content Heavy' },
      ],
    },
  ] as BlockParameter[],
  defaultParams: { topK: 10, boostFactors: 'balanced' },
  inputs: [queryInput()],
  outputs: [docsOutput()],
  canConnectTo: ['cross-encoder-reranking', 'metadata-filtering', 'deduplication', 'hybrid-search'],
  estimatedTokens: 500,
  estimatedLatencyMs: 50,
  estimatedCost: 0.00001,
  accuracyImpact: 0.3,
  requiredTier: 'User',
  maxInstances: 3,
  description: 'Traditional keyword/term matching with BM25',
  useCases: ['Exact matches', 'Product codes', 'Specific terms'],
  codeReferences: [],
};

export const HYBRID_SEARCH: RagBlock = {
  id: 'hybrid-search',
  type: 'hybrid-search',
  name: 'Hybrid Search',
  category: 'retrieval',
  icon: '⚖️',
  color: 'hsl(142 76% 36%)',
  parameters: [
    {
      id: 'alpha',
      name: 'Alpha',
      description: 'Weight: 0=keyword, 1=vector',
      type: 'range',
      required: true,
      default: [0.5, 0.5],
      min: 0,
      max: 1,
      step: 0.1,
    },
    {
      id: 'vectorTopK',
      name: 'Vector Top K',
      description: 'Vector search candidates',
      type: 'number',
      required: true,
      default: 10,
      min: 1,
      max: 50,
    },
    {
      id: 'keywordTopK',
      name: 'Keyword Top K',
      description: 'Keyword search candidates',
      type: 'number',
      required: true,
      default: 10,
      min: 1,
      max: 50,
    },
    {
      id: 'fusionMethod',
      name: 'Fusion Method',
      description: 'How to combine results',
      type: 'select',
      required: true,
      default: 'rrf',
      options: [
        { value: 'rrf', label: 'Reciprocal Rank Fusion' },
        { value: 'weighted', label: 'Weighted Sum' },
      ],
    },
  ] as BlockParameter[],
  defaultParams: { alpha: [0.5, 0.5], vectorTopK: 10, keywordTopK: 10, fusionMethod: 'rrf' },
  inputs: [queryInput()],
  outputs: [docsOutput()],
  canConnectTo: ['cross-encoder-reranking', 'crag-evaluator', 'document-repacking'],
  estimatedTokens: 1500,
  estimatedLatencyMs: 200,
  estimatedCost: 0.0002,
  accuracyImpact: 0.7,
  requiredTier: 'User',
  maxInstances: 2,
  description: 'Combines dense + sparse retrieval with score fusion (+35-48% improvement)',
  useCases: ['Production standard', 'Best recall+precision'],
  codeReferences: [
    { type: 'backend', path: 'BoundedContexts/KnowledgeBase/Domain/Plugins/Implementations/Retrieval/RetrievalHybridPlugin.cs', description: 'Hybrid retrieval implementation' },
  ],
};

export const MULTI_HOP_RETRIEVAL: RagBlock = {
  id: 'multi-hop-retrieval',
  type: 'multi-hop-retrieval',
  name: 'Multi-Hop Retrieval',
  category: 'retrieval',
  icon: '🔄',
  color: 'hsl(142 76% 36%)',
  parameters: [
    {
      id: 'hops',
      name: 'Number of Hops',
      description: 'Iterations to perform',
      type: 'number',
      required: true,
      default: 2,
      min: 2,
      max: 5,
    },
    {
      id: 'topKPerHop',
      name: 'Top K per Hop',
      description: 'Chunks retrieved per iteration',
      type: 'number',
      required: true,
      default: 3,
      min: 1,
      max: 10,
    },
    {
      id: 'strategy',
      name: 'Strategy',
      description: 'Traversal strategy',
      type: 'select',
      required: true,
      default: 'breadth-first',
      options: [
        { value: 'breadth-first', label: 'Breadth First' },
        { value: 'depth-first', label: 'Depth First' },
      ],
    },
  ] as BlockParameter[],
  defaultParams: { hops: 2, topKPerHop: 3, strategy: 'breadth-first' },
  inputs: [queryInput()],
  outputs: [docsOutput()],
  canConnectTo: ['cross-encoder-reranking', 'crag-evaluator'],
  estimatedTokens: 5000,
  estimatedLatencyMs: 1500,
  estimatedCost: 0.005,
  accuracyImpact: 0.6,
  requiredTier: 'Editor',
  maxInstances: 1,
  description: 'Iterative retrieval following entity/concept chains',
  useCases: ['Complex reasoning', 'Multi-step questions'],
  codeReferences: [
    { type: 'backend', path: 'BoundedContexts/KnowledgeBase/Domain/Plugins/Implementations/Retrieval/RetrievalMultiSourcePlugin.cs', description: 'Multi-source retrieval' },
  ],
};

export const GRAPH_RAG: RagBlock = {
  id: 'graph-rag',
  type: 'graph-rag',
  name: 'GraphRAG',
  category: 'retrieval',
  icon: '🕸️',
  color: 'hsl(142 76% 36%)',
  parameters: [
    {
      id: 'maxDepth',
      name: 'Max Depth',
      description: 'Graph traversal depth',
      type: 'number',
      required: true,
      default: 2,
      min: 1,
      max: 5,
    },
    {
      id: 'relationshipTypes',
      name: 'Relationship Types',
      description: 'Filter edges by type',
      type: 'select',
      required: false,
      default: 'all',
      options: [
        { value: 'all', label: 'All Relationships' },
        { value: 'hierarchical', label: 'Hierarchical Only' },
        { value: 'semantic', label: 'Semantic Only' },
      ],
    },
  ] as BlockParameter[],
  defaultParams: { maxDepth: 2, relationshipTypes: 'all' },
  inputs: [queryInput()],
  outputs: [docsOutput()],
  canConnectTo: ['cross-encoder-reranking', 'crag-evaluator'],
  estimatedTokens: 3000,
  estimatedLatencyMs: 500,
  estimatedCost: 0.003,
  accuracyImpact: 0.9,
  requiredTier: 'Admin',
  maxInstances: 1,
  description: 'Knowledge graph traversal with relationships (up to 99% precision)',
  useCases: ['Entity relationships', 'Deterministic accuracy', 'Taxonomy queries'],
  codeReferences: [],
};

export const WEB_SEARCH: RagBlock = {
  id: 'web-search',
  type: 'web-search',
  name: 'Web Search',
  category: 'retrieval',
  icon: '🌐',
  color: 'hsl(142 76% 36%)',
  parameters: [
    {
      id: 'maxQueries',
      name: 'Max Queries',
      description: 'Number of searches to perform',
      type: 'number',
      required: true,
      default: 3,
      min: 1,
      max: 10,
    },
    {
      id: 'sources',
      name: 'Sources',
      description: 'Allowed domains',
      type: 'select',
      required: false,
      default: 'all',
      options: [
        { value: 'all', label: 'All Sources' },
        { value: 'official', label: 'Official Sources Only' },
        { value: 'academic', label: 'Academic Sources' },
      ],
    },
    {
      id: 'fallbackThreshold',
      name: 'Fallback Threshold',
      description: 'Confidence below which to trigger',
      type: 'range',
      required: true,
      default: [0.5, 1.0],
      min: 0,
      max: 1,
      step: 0.1,
    },
  ] as BlockParameter[],
  defaultParams: { maxQueries: 3, sources: 'all', fallbackThreshold: [0.5, 1.0] },
  inputs: [queryInput(), docsInput('context-in')],
  outputs: [docsOutput()],
  canConnectTo: ['cross-encoder-reranking', 'crag-evaluator', 'deduplication'],
  estimatedTokens: 3500,
  estimatedLatencyMs: 3000,
  estimatedCost: 0.01,
  accuracyImpact: 0.4,
  requiredTier: 'Editor',
  maxInstances: 1,
  description: 'External web search when corpus insufficient',
  useCases: ['Current information', 'External facts', 'Official errata/FAQ'],
  codeReferences: [
    { type: 'backend', path: 'BoundedContexts/KnowledgeBase/Domain/Plugins/Implementations/Retrieval/RetrievalWebPlugin.cs', description: 'Web retrieval plugin' },
  ],
};

export const SENTENCE_WINDOW: RagBlock = {
  id: 'sentence-window',
  type: 'sentence-window',
  name: 'Sentence Window',
  category: 'retrieval',
  icon: '📋',
  color: 'hsl(142 76% 36%)',
  parameters: [
    {
      id: 'searchChunkSize',
      name: 'Search Chunk Size',
      description: 'Tokens for search matching',
      type: 'number',
      required: true,
      default: 128,
      min: 64,
      max: 512,
    },
    {
      id: 'contextWindowSize',
      name: 'Context Window Size',
      description: 'Tokens for generation context',
      type: 'number',
      required: true,
      default: 512,
      min: 256,
      max: 2048,
    },
    {
      id: 'overlap',
      name: 'Overlap',
      description: 'Token overlap between windows',
      type: 'number',
      required: true,
      default: 32,
      min: 0,
      max: 128,
    },
  ] as BlockParameter[],
  defaultParams: { searchChunkSize: 128, contextWindowSize: 512, overlap: 32 },
  inputs: [queryInput()],
  outputs: [docsOutput()],
  canConnectTo: ['cross-encoder-reranking', 'crag-evaluator'],
  estimatedTokens: 2000,
  estimatedLatencyMs: 150,
  estimatedCost: 0.0002,
  accuracyImpact: 0.5,
  requiredTier: 'Editor',
  maxInstances: 2,
  description: 'Retrieve small chunks for search, expand to larger context (+10-15%)',
  useCases: ['Precise matching', 'Rich context generation'],
  codeReferences: [],
};

// =============================================================================
// Category 2: Ranking Blocks (4)
// =============================================================================

export const CROSS_ENCODER_RERANKING: RagBlock = {
  id: 'cross-encoder-reranking',
  type: 'cross-encoder-reranking',
  name: 'Cross-Encoder Reranking',
  category: 'ranking',
  icon: '📊',
  color: 'hsl(45 93% 47%)',
  parameters: [
    {
      id: 'model',
      name: 'Reranker Model',
      description: 'Model for reranking',
      type: 'select',
      required: true,
      default: 'mxbai-rerank-large',
      options: [
        { value: 'mxbai-rerank-large', label: 'mxbai Rerank Large' },
        { value: 'colbert-v2', label: 'ColBERTv2' },
        { value: 'cohere-rerank-v3', label: 'Cohere Rerank v3' },
      ],
    },
    {
      id: 'topN',
      name: 'Top N',
      description: 'Final docs after rerank',
      type: 'number',
      required: true,
      default: 5,
      min: 1,
      max: 20,
    },
    {
      id: 'inputTopK',
      name: 'Input Candidates',
      description: 'Candidates to rerank',
      type: 'number',
      required: true,
      default: 25,
      min: 10,
      max: 100,
    },
  ] as BlockParameter[],
  defaultParams: { model: 'mxbai-rerank-large', topN: 5, inputTopK: 25 },
  inputs: [queryInput(), docsInput()],
  outputs: [docsOutput()],
  canConnectTo: ['crag-evaluator', 'document-repacking', 'sequential-agent', 'parallel-agent'],
  estimatedTokens: 0,
  estimatedLatencyMs: 300,
  estimatedCost: 0.001,
  accuracyImpact: 0.8,
  requiredTier: 'User',
  maxInstances: 2,
  description: 'Re-score top-K docs with cross-attention (+20-40% precision)',
  useCases: ['Solve lost in middle', 'Improve precision'],
  codeReferences: [
    { type: 'backend', path: 'BoundedContexts/KnowledgeBase/Domain/Plugins/Implementations/Transform/TransformRankerPlugin.cs', description: 'Ranker plugin' },
  ],
};

export const METADATA_FILTERING: RagBlock = {
  id: 'metadata-filtering',
  type: 'metadata-filtering',
  name: 'Metadata Filtering',
  category: 'ranking',
  icon: '🏷️',
  color: 'hsl(45 93% 47%)',
  parameters: [
    {
      id: 'filters',
      name: 'Filter Fields',
      description: 'Fields to filter by',
      type: 'select',
      required: true,
      default: 'category',
      options: [
        { value: 'category', label: 'Category' },
        { value: 'date', label: 'Date' },
        { value: 'author', label: 'Author' },
        { value: 'language', label: 'Language' },
      ],
    },
    {
      id: 'combineMode',
      name: 'Combine Mode',
      description: 'How to combine filters',
      type: 'select',
      required: true,
      default: 'and',
      options: [
        { value: 'and', label: 'AND (all match)' },
        { value: 'or', label: 'OR (any match)' },
      ],
    },
  ] as BlockParameter[],
  defaultParams: { filters: 'category', combineMode: 'and' },
  inputs: [docsInput()],
  outputs: [docsOutput()],
  canConnectTo: ['cross-encoder-reranking', 'deduplication', 'crag-evaluator'],
  estimatedTokens: 0,
  estimatedLatencyMs: 10,
  estimatedCost: 0,
  accuracyImpact: 0.2,
  requiredTier: 'User',
  maxInstances: 3,
  description: 'Pre-filter docs by metadata before retrieval',
  useCases: ['Domain-specific queries', 'Time-sensitive queries'],
  codeReferences: [
    { type: 'backend', path: 'BoundedContexts/KnowledgeBase/Domain/Plugins/Implementations/Filter/FilterContentPlugin.cs', description: 'Content filter plugin' },
  ],
};

export const DEDUPLICATION: RagBlock = {
  id: 'deduplication',
  type: 'deduplication',
  name: 'Deduplication',
  category: 'ranking',
  icon: '🗑️',
  color: 'hsl(45 93% 47%)',
  parameters: [
    {
      id: 'similarityThreshold',
      name: 'Similarity Threshold',
      description: 'Threshold above which = duplicate',
      type: 'range',
      required: true,
      default: [0.95, 1.0],
      min: 0.8,
      max: 1,
      step: 0.01,
    },
    {
      id: 'method',
      name: 'Method',
      description: 'Deduplication method',
      type: 'select',
      required: true,
      default: 'fuzzy',
      options: [
        { value: 'exact', label: 'Exact Hash' },
        { value: 'fuzzy', label: 'Fuzzy Matching' },
      ],
    },
  ] as BlockParameter[],
  defaultParams: { similarityThreshold: [0.95, 1.0], method: 'fuzzy' },
  inputs: [docsInput()],
  outputs: [docsOutput()],
  canConnectTo: ['cross-encoder-reranking', 'crag-evaluator', 'document-repacking'],
  estimatedTokens: 0,
  estimatedLatencyMs: 50,
  estimatedCost: 0,
  accuracyImpact: 0.1,
  requiredTier: 'User',
  maxInstances: 2,
  description: 'Remove duplicate or highly similar chunks (saves 10-30% tokens)',
  useCases: ['Reduce redundancy', 'Save tokens'],
  codeReferences: [
    { type: 'backend', path: 'BoundedContexts/KnowledgeBase/Domain/Plugins/Implementations/Filter/FilterDeduplicatorPlugin.cs', description: 'Deduplication plugin' },
  ],
};

export const DOCUMENT_REPACKING: RagBlock = {
  id: 'document-repacking',
  type: 'document-repacking',
  name: 'Document Repacking',
  category: 'ranking',
  icon: '📦',
  color: 'hsl(45 93% 47%)',
  parameters: [
    {
      id: 'method',
      name: 'Repack Method',
      description: 'How to reorder documents',
      type: 'select',
      required: true,
      default: 'reverse',
      options: [
        { value: 'reverse', label: 'Reverse (best first/last)' },
        { value: 'sandwich', label: 'Sandwich (best at edges)' },
        { value: 'interleaved', label: 'Interleaved' },
      ],
    },
  ] as BlockParameter[],
  defaultParams: { method: 'reverse' },
  inputs: [docsInput()],
  outputs: [docsOutput()],
  canConnectTo: ['crag-evaluator', 'sequential-agent', 'parallel-agent'],
  estimatedTokens: 0,
  estimatedLatencyMs: 5,
  estimatedCost: 0,
  accuracyImpact: 0.3,
  requiredTier: 'Editor',
  maxInstances: 1,
  description: 'Reorder docs to optimize for LLM attention patterns (+5-12% accuracy)',
  useCases: ['Optimize LLM attention', 'Long context handling'],
  codeReferences: [],
};

// =============================================================================
// Category 3: Validation Blocks (5)
// =============================================================================

export const CRAG_EVALUATOR: RagBlock = {
  id: 'crag-evaluator',
  type: 'crag-evaluator',
  name: 'CRAG Evaluator',
  category: 'validation',
  icon: '⚖️',
  color: 'hsl(0 72% 51%)',
  parameters: [
    {
      id: 'evaluatorModel',
      name: 'Evaluator Model',
      description: 'Model for evaluation',
      type: 'select',
      required: true,
      default: 't5-large',
      options: [
        { value: 't5-large', label: 'T5-Large' },
        { value: 'classifier', label: 'Fine-tuned Classifier' },
        { value: 'llm', label: 'LLM Judge' },
      ],
    },
    {
      id: 'correctThreshold',
      name: 'Correct Threshold',
      description: 'Score above = correct',
      type: 'range',
      required: true,
      default: [0.8, 1.0],
      min: 0,
      max: 1,
      step: 0.05,
    },
    {
      id: 'ambiguousThreshold',
      name: 'Ambiguous Threshold',
      description: 'Score range for ambiguous',
      type: 'range',
      required: true,
      default: [0.5, 0.8],
      min: 0,
      max: 1,
      step: 0.05,
    },
    {
      id: 'correctionAction',
      name: 'Correction Action',
      description: 'Action when incorrect',
      type: 'select',
      required: true,
      default: 'web-search',
      options: [
        { value: 'web-search', label: 'Trigger Web Search' },
        { value: 'rewrite-query', label: 'Rewrite Query' },
        { value: 'skip-doc', label: 'Skip Document' },
      ],
    },
  ] as BlockParameter[],
  defaultParams: { evaluatorModel: 't5-large', correctThreshold: [0.8, 1.0], ambiguousThreshold: [0.5, 0.8], correctionAction: 'web-search' },
  inputs: [queryInput(), docsInput()],
  outputs: [docsOutput(), evalOutput()],
  canConnectTo: ['web-search', 'query-rewriting', 'sequential-agent', 'parallel-agent'],
  estimatedTokens: 500,
  estimatedLatencyMs: 300,
  estimatedCost: 0.001,
  accuracyImpact: 0.6,
  requiredTier: 'User',
  maxInstances: 2,
  description: 'Retrieval evaluator grades docs, triggers correction (+5-15% accuracy)',
  useCases: ['Quality gate', 'Prevent bad documents'],
  codeReferences: [
    { type: 'backend', path: 'BoundedContexts/KnowledgeBase/Domain/Plugins/Implementations/Evaluation/EvaluationCragPlugin.cs', description: 'CRAG evaluation plugin' },
  ],
};

export const SELF_RAG: RagBlock = {
  id: 'self-rag',
  type: 'self-rag',
  name: 'Self-RAG Reflection',
  category: 'validation',
  icon: '🎭',
  color: 'hsl(0 72% 51%)',
  parameters: [
    {
      id: 'threshold',
      name: 'Confidence Threshold',
      description: 'Confidence to trigger retrieval',
      type: 'range',
      required: true,
      default: [0.7, 1.0],
      min: 0,
      max: 1,
      step: 0.05,
    },
    {
      id: 'critiqueMode',
      name: 'Critique Mode',
      description: 'When to apply critique',
      type: 'select',
      required: true,
      default: 'during',
      options: [
        { value: 'during', label: 'During Generation' },
        { value: 'post', label: 'Post Generation' },
      ],
    },
  ] as BlockParameter[],
  defaultParams: { threshold: [0.7, 1.0], critiqueMode: 'during' },
  inputs: [queryInput(), docsInput()],
  outputs: [evalOutput(), scoresOutput()],
  canConnectTo: ['sequential-agent', 'parallel-agent'],
  estimatedTokens: 800,
  estimatedLatencyMs: 400,
  estimatedCost: 0.002,
  accuracyImpact: 0.5,
  requiredTier: 'Admin',
  maxInstances: 1,
  description: 'LLM generates reflection tokens to self-assess (+10-20% on knowledge tasks)',
  useCases: ['Adaptive retrieval', 'Self-correction'],
  codeReferences: [],
};

export const CONFIDENCE_SCORING: RagBlock = {
  id: 'confidence-scoring',
  type: 'confidence-scoring',
  name: 'Confidence Scoring',
  category: 'validation',
  icon: '📈',
  color: 'hsl(0 72% 51%)',
  parameters: [
    {
      id: 'method',
      name: 'Scoring Method',
      description: 'How to calculate confidence',
      type: 'select',
      required: true,
      default: 'logprobs',
      options: [
        { value: 'logprobs', label: 'Log Probabilities' },
        { value: 'multi-model', label: 'Multi-Model Agreement' },
        { value: 'citation', label: 'Citation Coverage' },
      ],
    },
    {
      id: 'threshold',
      name: 'Min Threshold',
      description: 'Minimum acceptable confidence',
      type: 'range',
      required: true,
      default: [0.7, 1.0],
      min: 0,
      max: 1,
      step: 0.05,
    },
  ] as BlockParameter[],
  defaultParams: { method: 'logprobs', threshold: [0.7, 1.0] },
  inputs: [docsInput()],
  outputs: [scoresOutput(), evalOutput()],
  canConnectTo: ['hallucination-detection', 'citation-verification'],
  estimatedTokens: 100,
  estimatedLatencyMs: 30,
  estimatedCost: 0,
  accuracyImpact: 0.2,
  requiredTier: 'User',
  maxInstances: 2,
  description: 'Assign confidence score to generated answer',
  useCases: ['Filter low-quality answers', 'Show uncertainty'],
  codeReferences: [
    { type: 'backend', path: 'BoundedContexts/KnowledgeBase/Domain/Plugins/Implementations/Evaluation/EvaluationConfidenceGatePlugin.cs', description: 'Confidence gate plugin' },
  ],
};

export const CITATION_VERIFICATION: RagBlock = {
  id: 'citation-verification',
  type: 'citation-verification',
  name: 'Citation Verification',
  category: 'validation',
  icon: '🔍',
  color: 'hsl(0 72% 51%)',
  parameters: [
    {
      id: 'method',
      name: 'Verification Method',
      description: 'How to verify citations',
      type: 'select',
      required: true,
      default: 'nli',
      options: [
        { value: 'nli', label: 'NLI Model' },
        { value: 'fuzzy', label: 'Fuzzy Matching' },
        { value: 'llm', label: 'LLM Verification' },
      ],
    },
    {
      id: 'minCoverage',
      name: 'Min Citation Coverage',
      description: 'Percentage of claims that must be cited',
      type: 'range',
      required: true,
      default: [0.8, 1.0],
      min: 0,
      max: 1,
      step: 0.05,
    },
  ] as BlockParameter[],
  defaultParams: { method: 'nli', minCoverage: [0.8, 1.0] },
  inputs: [docsInput()],
  outputs: [evalOutput(), scoresOutput()],
  canConnectTo: ['hallucination-detection'],
  estimatedTokens: 500,
  estimatedLatencyMs: 200,
  estimatedCost: 0.001,
  accuracyImpact: 0.4,
  requiredTier: 'Editor',
  maxInstances: 1,
  description: 'Check if answer claims are grounded in sources',
  useCases: ['Factual accuracy', 'Reduce hallucinations'],
  codeReferences: [],
};

export const HALLUCINATION_DETECTION: RagBlock = {
  id: 'hallucination-detection',
  type: 'hallucination-detection',
  name: 'Hallucination Detection',
  category: 'validation',
  icon: '🛡️',
  color: 'hsl(0 72% 51%)',
  parameters: [
    {
      id: 'forbiddenPhrases',
      name: 'Forbidden Phrases',
      description: 'Phrases that indicate uncertainty',
      type: 'select',
      required: false,
      default: 'standard',
      options: [
        { value: 'standard', label: 'Standard (I think, probably)' },
        { value: 'strict', label: 'Strict (includes maybe, might)' },
        { value: 'custom', label: 'Custom List' },
      ],
    },
    {
      id: 'useFactChecker',
      name: 'Use Fact Checker',
      description: 'Enable specialized detector model',
      type: 'boolean',
      required: true,
      default: true,
    },
  ] as BlockParameter[],
  defaultParams: { forbiddenPhrases: 'standard', useFactChecker: true },
  inputs: [docsInput()],
  outputs: [evalOutput()],
  canConnectTo: [],
  estimatedTokens: 300,
  estimatedLatencyMs: 100,
  estimatedCost: 0.0005,
  accuracyImpact: 0.3,
  requiredTier: 'Editor',
  maxInstances: 1,
  description: 'Detect fabricated or ungrounded information',
  useCases: ['Safety-critical applications'],
  codeReferences: [
    { type: 'backend', path: 'BoundedContexts/KnowledgeBase/Domain/Plugins/Implementations/Evaluation/EvaluationHallucinationCheckPlugin.cs', description: 'Hallucination check plugin' },
  ],
};

// =============================================================================
// Category 4: Agent Blocks (3)
// =============================================================================

export const SEQUENTIAL_AGENT: RagBlock = {
  id: 'sequential-agent',
  type: 'sequential-agent',
  name: 'Sequential Agent Chain',
  category: 'agents',
  icon: '➡️',
  color: 'hsl(262 83% 62%)',
  parameters: [
    {
      id: 'agents',
      name: 'Agent Chain',
      description: 'Agents to execute in sequence',
      type: 'select',
      required: true,
      default: 'analyzer-synthesizer',
      options: [
        { value: 'analyzer-synthesizer', label: 'Analyzer → Synthesizer' },
        { value: 'analyzer-validator-synthesizer', label: 'Analyzer → Validator → Synthesizer' },
        { value: 'planner-executor-validator', label: 'Planner → Executor → Validator' },
      ],
    },
    {
      id: 'handoff',
      name: 'Handoff Mode',
      description: 'How agents transfer control',
      type: 'select',
      required: true,
      default: 'automatic',
      options: [
        { value: 'automatic', label: 'Automatic' },
        { value: 'conditional', label: 'Conditional' },
      ],
    },
  ] as BlockParameter[],
  defaultParams: { agents: 'analyzer-synthesizer', handoff: 'automatic' },
  inputs: [queryInput(), docsInput()],
  outputs: [{ id: 'response-out', name: 'Response', type: 'output', dataType: 'response', position: 'right' }],
  canConnectTo: ['confidence-scoring', 'citation-verification', 'hallucination-detection'],
  estimatedTokens: 8000,
  estimatedLatencyMs: 5000,
  estimatedCost: 0.05,
  accuracyImpact: 0.7,
  requiredTier: 'Editor',
  maxInstances: 1,
  description: 'Agents execute in sequence, each processes previous output',
  useCases: ['Research → Planning → Writing workflows'],
  codeReferences: [
    { type: 'backend', path: 'BoundedContexts/KnowledgeBase/Domain/Plugins/Implementations/Generation/GenerationMultiAgentPlugin.cs', description: 'Multi-agent generation' },
  ],
};

export const PARALLEL_AGENT: RagBlock = {
  id: 'parallel-agent',
  type: 'parallel-agent',
  name: 'Parallel Agent Execution',
  category: 'agents',
  icon: '⚡',
  color: 'hsl(262 83% 62%)',
  parameters: [
    {
      id: 'agentCount',
      name: 'Agent Count',
      description: 'Number of parallel agents',
      type: 'number',
      required: true,
      default: 3,
      min: 2,
      max: 5,
    },
    {
      id: 'mergeStrategy',
      name: 'Merge Strategy',
      description: 'How to combine results',
      type: 'select',
      required: true,
      default: 'vote',
      options: [
        { value: 'vote', label: 'Majority Vote' },
        { value: 'concatenate', label: 'Concatenate' },
        { value: 'llm-synthesis', label: 'LLM Synthesis' },
      ],
    },
  ] as BlockParameter[],
  defaultParams: { agentCount: 3, mergeStrategy: 'vote' },
  inputs: [queryInput(), docsInput()],
  outputs: [{ id: 'response-out', name: 'Response', type: 'output', dataType: 'response', position: 'right' }],
  canConnectTo: ['confidence-scoring', 'citation-verification'],
  estimatedTokens: 12000,
  estimatedLatencyMs: 3000,
  estimatedCost: 0.08,
  accuracyImpact: 0.8,
  requiredTier: 'Admin',
  maxInstances: 1,
  description: 'Multiple agents work simultaneously, merge results (latency = max, not sum)',
  useCases: ['Diverse perspectives', 'CONSENSUS strategy'],
  codeReferences: [
    { type: 'backend', path: 'BoundedContexts/KnowledgeBase/Domain/Plugins/Implementations/Generation/GenerationMultiAgentPlugin.cs', description: 'Multi-agent generation' },
  ],
};

export const SUPERVISOR_WORKER: RagBlock = {
  id: 'supervisor-worker',
  type: 'supervisor-worker',
  name: 'Supervisor-Worker',
  category: 'agents',
  icon: '👔',
  color: 'hsl(262 83% 62%)',
  parameters: [
    {
      id: 'workers',
      name: 'Worker Types',
      description: 'Specialized worker agents',
      type: 'select',
      required: true,
      default: 'rules-strategy-setup',
      options: [
        { value: 'rules-strategy-setup', label: 'Rules, Strategy, Setup' },
        { value: 'faq-rules-expert', label: 'FAQ, Rules, Expert' },
        { value: 'custom', label: 'Custom Configuration' },
      ],
    },
    {
      id: 'routingLogic',
      name: 'Routing Logic',
      description: 'How supervisor routes tasks',
      type: 'select',
      required: true,
      default: 'classification',
      options: [
        { value: 'classification', label: 'Query Classification' },
        { value: 'confidence', label: 'Confidence-based' },
        { value: 'hybrid', label: 'Hybrid' },
      ],
    },
  ] as BlockParameter[],
  defaultParams: { workers: 'rules-strategy-setup', routingLogic: 'classification' },
  inputs: [queryInput(), docsInput()],
  outputs: [{ id: 'response-out', name: 'Response', type: 'output', dataType: 'response', position: 'right' }],
  canConnectTo: ['confidence-scoring', 'citation-verification'],
  estimatedTokens: 10000,
  estimatedLatencyMs: 6000,
  estimatedCost: 0.06,
  accuracyImpact: 0.75,
  requiredTier: 'Admin',
  maxInstances: 1,
  description: 'Coordinator routes tasks to specialized workers (+25-40% task completion)',
  useCases: ['Complex multi-domain tasks', 'Dynamic routing'],
  codeReferences: [
    { type: 'backend', path: 'BoundedContexts/KnowledgeBase/Domain/Plugins/Implementations/Routing/RoutingLlmPlugin.cs', description: 'LLM routing' },
  ],
};

// =============================================================================
// Category 5: Optimization Blocks (4)
// =============================================================================

export const QUERY_REWRITING: RagBlock = {
  id: 'query-rewriting',
  type: 'query-rewriting',
  name: 'Query Rewriting',
  category: 'optimization',
  icon: '✏️',
  color: 'hsl(221 83% 53%)',
  parameters: [
    {
      id: 'method',
      name: 'Rewrite Method',
      description: 'How to rewrite query',
      type: 'select',
      required: true,
      default: 'llm',
      options: [
        { value: 'llm', label: 'LLM Rewrite' },
        { value: 'template', label: 'Template Expansion' },
      ],
    },
    {
      id: 'numVariations',
      name: 'Variations',
      description: 'Number of query variants',
      type: 'number',
      required: true,
      default: 3,
      min: 1,
      max: 5,
    },
  ] as BlockParameter[],
  defaultParams: { method: 'llm', numVariations: 3 },
  inputs: [queryInput()],
  outputs: [queryOutput()],
  canConnectTo: ['vector-search', 'keyword-search', 'hybrid-search', 'rag-fusion'],
  estimatedTokens: 400,
  estimatedLatencyMs: 200,
  estimatedCost: 0.001,
  accuracyImpact: 0.4,
  requiredTier: 'User',
  maxInstances: 2,
  description: 'Rephrase query for better retrieval (+10-20% improvement)',
  useCases: ['Ambiguous queries', 'Improve recall'],
  codeReferences: [
    { type: 'backend', path: 'BoundedContexts/KnowledgeBase/Domain/Plugins/Implementations/Transform/TransformExpanderPlugin.cs', description: 'Query expander' },
  ],
};

export const QUERY_DECOMPOSITION: RagBlock = {
  id: 'query-decomposition',
  type: 'query-decomposition',
  name: 'Query Decomposition',
  category: 'optimization',
  icon: '🔀',
  color: 'hsl(221 83% 53%)',
  parameters: [
    {
      id: 'maxSubQueries',
      name: 'Max Sub-Queries',
      description: 'Maximum sub-questions',
      type: 'number',
      required: true,
      default: 5,
      min: 2,
      max: 10,
    },
    {
      id: 'strategy',
      name: 'Strategy',
      description: 'How to handle sub-queries',
      type: 'select',
      required: true,
      default: 'parallel',
      options: [
        { value: 'sequential', label: 'Sequential' },
        { value: 'parallel', label: 'Parallel' },
      ],
    },
  ] as BlockParameter[],
  defaultParams: { maxSubQueries: 5, strategy: 'parallel' },
  inputs: [queryInput()],
  outputs: [queryOutput(), { id: 'queries-out', name: 'Sub-Queries', type: 'output', dataType: 'query', position: 'bottom', multiple: true }],
  canConnectTo: ['vector-search', 'hybrid-search', 'multi-hop-retrieval'],
  estimatedTokens: 600,
  estimatedLatencyMs: 300,
  estimatedCost: 0.002,
  accuracyImpact: 0.5,
  requiredTier: 'Editor',
  maxInstances: 1,
  description: 'Break complex query into sub-questions',
  useCases: ['Multi-part questions', 'Complex reasoning'],
  codeReferences: [],
};

export const HYDE: RagBlock = {
  id: 'hyde',
  type: 'hyde',
  name: 'HyDE',
  category: 'optimization',
  icon: '🎯',
  color: 'hsl(221 83% 53%)',
  parameters: [
    {
      id: 'model',
      name: 'Generation Model',
      description: 'Model for hypothetical doc',
      type: 'model',
      required: true,
      default: 'claude-3-haiku',
      allowedModels: ['claude-3-haiku', 'gpt-4o-mini', 'llama-3.3-70b'],
    },
    {
      id: 'useForSearch',
      name: 'Use for Search',
      description: 'Embed fake doc instead of query',
      type: 'boolean',
      required: true,
      default: true,
    },
  ] as BlockParameter[],
  defaultParams: { model: 'claude-3-haiku', useForSearch: true },
  inputs: [queryInput()],
  outputs: [{ id: 'embeddings-out', name: 'Embeddings', type: 'output', dataType: 'embeddings', position: 'right' }],
  canConnectTo: ['vector-search'],
  estimatedTokens: 1500,
  estimatedLatencyMs: 800,
  estimatedCost: 0.005,
  accuracyImpact: 0.5,
  requiredTier: 'Admin',
  maxInstances: 1,
  description: 'Generate fake answer, use for retrieval (+15-25% for abstract queries, HIGH cost)',
  useCases: ['Zero-shot domains', 'Concept-heavy queries'],
  codeReferences: [],
};

export const RAG_FUSION: RagBlock = {
  id: 'rag-fusion',
  type: 'rag-fusion',
  name: 'RAG-Fusion',
  category: 'optimization',
  icon: '🔍',
  color: 'hsl(221 83% 53%)',
  parameters: [
    {
      id: 'numQueries',
      name: 'Number of Queries',
      description: 'Query variations to generate',
      type: 'number',
      required: true,
      default: 4,
      min: 2,
      max: 7,
    },
    {
      id: 'rrfK',
      name: 'RRF K Constant',
      description: 'Rank fusion constant',
      type: 'number',
      required: true,
      default: 60,
      min: 1,
      max: 100,
    },
  ] as BlockParameter[],
  defaultParams: { numQueries: 4, rrfK: 60 },
  inputs: [queryInput()],
  outputs: [docsOutput()],
  canConnectTo: ['cross-encoder-reranking', 'crag-evaluator'],
  estimatedTokens: 4000,
  estimatedLatencyMs: 1000,
  estimatedCost: 0.01,
  accuracyImpact: 0.6,
  requiredTier: 'Editor',
  maxInstances: 1,
  description: 'Generate multiple queries, retrieve for each, fuse with RRF (+15-30%)',
  useCases: ['Comprehensive search', 'Diverse perspectives'],
  codeReferences: [],
};

// =============================================================================
// Block Registry
// =============================================================================

/** All 23 RAG blocks */
export const ALL_BLOCKS: RagBlock[] = [
  // Retrieval (7)
  VECTOR_SEARCH,
  KEYWORD_SEARCH,
  HYBRID_SEARCH,
  MULTI_HOP_RETRIEVAL,
  GRAPH_RAG,
  WEB_SEARCH,
  SENTENCE_WINDOW,
  // Ranking (4)
  CROSS_ENCODER_RERANKING,
  METADATA_FILTERING,
  DEDUPLICATION,
  DOCUMENT_REPACKING,
  // Validation (5)
  CRAG_EVALUATOR,
  SELF_RAG,
  CONFIDENCE_SCORING,
  CITATION_VERIFICATION,
  HALLUCINATION_DETECTION,
  // Agents (3)
  SEQUENTIAL_AGENT,
  PARALLEL_AGENT,
  SUPERVISOR_WORKER,
  // Optimization (4)
  QUERY_REWRITING,
  QUERY_DECOMPOSITION,
  HYDE,
  RAG_FUSION,
];

/** Blocks by type for quick lookup */
export const BLOCKS_BY_TYPE: Record<RagBlockType, RagBlock> = ALL_BLOCKS.reduce(
  (acc, block) => ({ ...acc, [block.type]: block }),
  {} as Record<RagBlockType, RagBlock>
);

/** Blocks grouped by category */
export const BLOCKS_BY_CATEGORY: Record<BlockCategory, RagBlock[]> = {
  retrieval: ALL_BLOCKS.filter((b) => b.category === 'retrieval'),
  optimization: ALL_BLOCKS.filter((b) => b.category === 'optimization'),
  ranking: ALL_BLOCKS.filter((b) => b.category === 'ranking'),
  validation: ALL_BLOCKS.filter((b) => b.category === 'validation'),
  agents: ALL_BLOCKS.filter((b) => b.category === 'agents'),
  control: [],
};

/** Get block by type */
export function getBlock(type: RagBlockType): RagBlock | undefined {
  return BLOCKS_BY_TYPE[type];
}

/** Get blocks for a category */
export function getBlocksByCategory(category: BlockCategory): RagBlock[] {
  return BLOCKS_BY_CATEGORY[category] || [];
}

/** Check if connection is valid between two block types */
export function isValidConnection(
  sourceType: RagBlockType,
  targetType: RagBlockType
): boolean {
  const sourceBlock = BLOCKS_BY_TYPE[sourceType];
  return sourceBlock?.canConnectTo.includes(targetType) ?? false;
}
