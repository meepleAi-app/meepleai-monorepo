/**
 * RAG Retrieval Strategy Types
 *
 * Type definitions for the 6 retrieval strategies displayed in the RAG Dashboard.
 * These represent the underlying retrieval mechanisms, not the user-facing strategies.
 */

/**
 * Available RAG retrieval strategies.
 */
export type RetrievalStrategyType =
  | 'Hybrid'
  | 'Semantic'
  | 'Keyword'
  | 'Contextual'
  | 'MultiQuery'
  | 'Agentic';

/**
 * Metric tier for latency, accuracy, and cost indicators.
 */
export type MetricTier = 'low' | 'medium' | 'high' | 'variable';

/**
 * Metrics associated with a retrieval strategy.
 */
export interface StrategyMetrics {
  latency: MetricTier;
  accuracy: MetricTier;
  costTier: MetricTier;
  latencyMs: { min: number; max: number; display: string };
  accuracyPercent: { min: number; max: number; display: string };
  costPerQuery: { min: number; max: number; display: string };
}

/**
 * Complete retrieval strategy data.
 */
export interface RetrievalStrategy {
  id: RetrievalStrategyType;
  name: string;
  shortDescription: string;
  icon: string;
  color: string;
  colorHsl: string;
  glowColor: string;
  metrics: StrategyMetrics;
  tags: string[];
}

/**
 * Props for RetrievalStrategyCard component.
 */
export interface RetrievalStrategyCardProps {
  strategy: RetrievalStrategy;
  isSelected?: boolean;
  onClick?: (strategy: RetrievalStrategy) => void;
  className?: string;
}

/**
 * Props for RetrievalStrategyGrid component.
 */
export interface RetrievalStrategyGridProps {
  strategies?: RetrievalStrategy[];
  selectedStrategy?: RetrievalStrategyType | null;
  onStrategySelect?: (strategy: RetrievalStrategy) => void;
  className?: string;
}
