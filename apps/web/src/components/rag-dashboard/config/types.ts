/**
 * RAG Configuration Types
 *
 * Type definitions for the RAG Strategy Configuration Panel.
 */

import type { RetrievalStrategyType } from '../retrieval-strategies';

/**
 * Generation parameters for LLM.
 */
export interface GenerationParams {
  temperature: number;
  topK: number;
  topP: number;
  maxTokens: number;
}

/**
 * Default generation parameters.
 */
export const DEFAULT_GENERATION_PARAMS: GenerationParams = {
  temperature: 0.7,
  topK: 40,
  topP: 0.9,
  maxTokens: 1000,
};

/**
 * Retrieval parameters for document chunks.
 */
export interface RetrievalParams {
  chunkSize: number;
  chunkOverlap: number; // percentage 0-50
  topResults: number;
  similarityThreshold: number;
}

/**
 * Default retrieval parameters.
 */
export const DEFAULT_RETRIEVAL_PARAMS: RetrievalParams = {
  chunkSize: 500,
  chunkOverlap: 10,
  topResults: 5,
  similarityThreshold: 0.7,
};

/**
 * Reranker model options.
 */
export const RERANKER_MODELS = [
  { id: 'cross-encoder/ms-marco-MiniLM-L-6-v2', name: 'MiniLM L6 (Fast)' },
  { id: 'cross-encoder/ms-marco-MiniLM-L-12-v2', name: 'MiniLM L12 (Balanced)' },
  { id: 'BAAI/bge-reranker-base', name: 'BGE Reranker Base' },
  { id: 'BAAI/bge-reranker-large', name: 'BGE Reranker Large (Accurate)' },
] as const;

export type RerankerModelId = (typeof RERANKER_MODELS)[number]['id'];

/**
 * Reranker settings.
 */
export interface RerankerSettings {
  enabled: boolean;
  model: RerankerModelId;
  topN: number;
}

/**
 * Default reranker settings.
 */
export const DEFAULT_RERANKER_SETTINGS: RerankerSettings = {
  enabled: true,
  model: 'cross-encoder/ms-marco-MiniLM-L-12-v2',
  topN: 10,
};

/**
 * Available LLM models.
 */
export const LLM_MODELS = [
  { id: 'gpt-4o', name: 'GPT-4o', provider: 'OpenAI' },
  { id: 'gpt-4o-mini', name: 'GPT-4o Mini', provider: 'OpenAI' },
  { id: 'gpt-4-turbo', name: 'GPT-4 Turbo', provider: 'OpenAI' },
  { id: 'claude-3-5-sonnet', name: 'Claude 3.5 Sonnet', provider: 'Anthropic' },
  { id: 'claude-3-opus', name: 'Claude 3 Opus', provider: 'Anthropic' },
  { id: 'claude-3-haiku', name: 'Claude 3 Haiku', provider: 'Anthropic' },
  { id: 'gemini-1.5-pro', name: 'Gemini 1.5 Pro', provider: 'Google' },
  { id: 'gemini-1.5-flash', name: 'Gemini 1.5 Flash', provider: 'Google' },
] as const;

export type LlmModelId = (typeof LLM_MODELS)[number]['id'];

/**
 * Model selection settings.
 */
export interface ModelSelection {
  primaryModel: LlmModelId;
  fallbackModel: LlmModelId | null;
  evaluationModel: LlmModelId | null; // For CRAG evaluation
}

/**
 * Default model selection.
 */
export const DEFAULT_MODEL_SELECTION: ModelSelection = {
  primaryModel: 'gpt-4o-mini',
  fallbackModel: null,
  evaluationModel: null,
};

/**
 * Strategy-specific settings.
 */
export interface StrategySpecificSettings {
  // Hybrid strategy
  hybridAlpha: number; // 0-1, keyword vs vector weight

  // Contextual strategy
  contextWindow: number; // Number of previous messages to include

  // Agentic strategy
  maxHops: number; // Maximum retrieval iterations
}

/**
 * Default strategy-specific settings.
 */
export const DEFAULT_STRATEGY_SPECIFIC: StrategySpecificSettings = {
  hybridAlpha: 0.5,
  contextWindow: 5,
  maxHops: 3,
};

/**
 * Complete RAG configuration.
 */
export interface RagConfig {
  generation: GenerationParams;
  retrieval: RetrievalParams;
  reranker: RerankerSettings;
  models: ModelSelection;
  strategySpecific: StrategySpecificSettings;
  activeStrategy: RetrievalStrategyType;
}

/**
 * Default RAG configuration.
 */
export const DEFAULT_RAG_CONFIG: RagConfig = {
  generation: DEFAULT_GENERATION_PARAMS,
  retrieval: DEFAULT_RETRIEVAL_PARAMS,
  reranker: DEFAULT_RERANKER_SETTINGS,
  models: DEFAULT_MODEL_SELECTION,
  strategySpecific: DEFAULT_STRATEGY_SPECIFIC,
  activeStrategy: 'Hybrid',
};

/**
 * Preset configurations per strategy.
 */
export const STRATEGY_PRESETS: Record<RetrievalStrategyType, Partial<RagConfig>> = {
  Hybrid: {
    retrieval: { ...DEFAULT_RETRIEVAL_PARAMS, topResults: 5 },
    strategySpecific: { ...DEFAULT_STRATEGY_SPECIFIC, hybridAlpha: 0.5 },
  },
  Semantic: {
    retrieval: { ...DEFAULT_RETRIEVAL_PARAMS, similarityThreshold: 0.8 },
    generation: { ...DEFAULT_GENERATION_PARAMS, temperature: 0.3 },
  },
  Keyword: {
    retrieval: { ...DEFAULT_RETRIEVAL_PARAMS, topResults: 10, similarityThreshold: 0.5 },
    reranker: { ...DEFAULT_RERANKER_SETTINGS, enabled: true },
  },
  Contextual: {
    strategySpecific: { ...DEFAULT_STRATEGY_SPECIFIC, contextWindow: 8 },
    generation: { ...DEFAULT_GENERATION_PARAMS, maxTokens: 1500 },
  },
  MultiQuery: {
    retrieval: { ...DEFAULT_RETRIEVAL_PARAMS, topResults: 3 },
    generation: { ...DEFAULT_GENERATION_PARAMS, temperature: 0.5 },
    strategySpecific: { ...DEFAULT_STRATEGY_SPECIFIC, maxHops: 3 },
  },
  Agentic: {
    retrieval: { ...DEFAULT_RETRIEVAL_PARAMS, topResults: 8 },
    strategySpecific: { ...DEFAULT_STRATEGY_SPECIFIC, maxHops: 5 },
    models: { ...DEFAULT_MODEL_SELECTION, evaluationModel: 'gpt-4o' },
  },
};

/**
 * Props for configuration panel components.
 */
export interface ConfigPanelProps {
  className?: string;
}

export interface GenerationParamsProps extends ConfigPanelProps {
  params: GenerationParams;
  onChange: (params: GenerationParams) => void;
}

export interface RetrievalParamsProps extends ConfigPanelProps {
  params: RetrievalParams;
  onChange: (params: RetrievalParams) => void;
}

export interface RerankerSettingsProps extends ConfigPanelProps {
  settings: RerankerSettings;
  onChange: (settings: RerankerSettings) => void;
}

export interface ModelSelectorProps extends ConfigPanelProps {
  selection: ModelSelection;
  onChange: (selection: ModelSelection) => void;
  showEvaluationModel?: boolean;
}

export interface StrategySpecificProps extends ConfigPanelProps {
  settings: StrategySpecificSettings;
  onChange: (settings: StrategySpecificSettings) => void;
  activeStrategy: RetrievalStrategyType;
}

export interface StrategyConfigPanelProps extends ConfigPanelProps {
  onSave?: (config: RagConfig) => Promise<void>;
  onReset?: () => void;
}
