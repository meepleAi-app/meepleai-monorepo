/**
 * RAG strategies available in the playground.
 * Must match backend AgentStrategy value object names.
 */
export const RAG_STRATEGIES = [
  { value: '__default__', label: 'Default (auto)' },
  { value: 'HybridSearch', label: 'Hybrid Search' },
  { value: 'VectorOnly', label: 'Vector Only' },
  { value: 'KeywordOnly', label: 'Keyword Only' },
  { value: 'RetrievalOnly', label: 'Retrieval Only' },
  { value: 'SentenceWindowRAG', label: 'Sentence Window' },
  { value: 'ColBERTReranking', label: 'ColBERT Reranking' },
  { value: 'SingleModel', label: 'Single Model' },
  { value: 'IterativeRAG', label: 'Iterative RAG' },
  { value: 'MultiModel', label: 'Multi-Model' },
] as const;

export type RagStrategyValue = (typeof RAG_STRATEGIES)[number]['value'];
