/**
 * RAG Dashboard Configuration Components
 *
 * This module exports all configuration-related components for the RAG Dashboard.
 * These components allow users to customize RAG strategy parameters including:
 * - Generation parameters (temperature, top-k, top-p, max tokens)
 * - Retrieval parameters (chunk size, overlap, top results, similarity threshold)
 * - Reranker settings (enable/disable, model selection, top-n)
 * - LLM model selection (primary, fallback, evaluation models)
 * - Strategy-specific settings (hybrid alpha, context window, max hops)
 */

// Main container component
export { StrategyConfigPanel } from './StrategyConfigPanel';
export type { StrategyConfigPanelProps } from './StrategyConfigPanel';

// Individual configuration components
export { GenerationParams } from './GenerationParams';
export { RetrievalParams } from './RetrievalParams';
export { RerankerSettings } from './RerankerSettings';
export { ModelSelector } from './ModelSelector';
export { StrategySpecificSettings } from './StrategySpecificSettings';

// Moved from root level
export { RagConfigurationForm } from './RagConfigurationForm';
export { AgentRoleConfigurator } from './AgentRoleConfigurator';

// Types and constants
export type {
  // Core configuration types
  RagConfig,
  GenerationParams as GenerationParamsType,
  RetrievalParams as RetrievalParamsType,
  RerankerSettings as RerankerSettingsType,
  ModelSelection,
  StrategySpecificSettings as StrategySpecificSettingsType,
  // Model types
  LlmModelId,
  RerankerModelId,
  // Props types
  GenerationParamsProps,
  RetrievalParamsProps,
  RerankerSettingsProps,
  ModelSelectorProps,
  StrategySpecificProps,
} from './types';

export {
  // Default configuration
  DEFAULT_RAG_CONFIG,
  // Strategy presets
  STRATEGY_PRESETS,
  // Model constants
  LLM_MODELS,
  RERANKER_MODELS,
} from './types';
