/**
 * RAG Performance Metrics Types
 *
 * Type definitions for the Performance Metrics Dashboard.
 */

import type { RetrievalStrategyType } from '../retrieval-strategies';

/**
 * Latency metrics with percentile breakdowns.
 */
export interface LatencyMetrics {
  p50: number;
  p95: number;
  p99: number;
  avg: number;
  trend: number; // percentage change from previous period
}

/**
 * Token usage distribution.
 */
export interface TokenUsage {
  input: number;
  output: number;
  context: number;
  total: number;
  costEstimate: number;
}

/**
 * Cache performance metrics.
 */
export interface CacheMetrics {
  hitRate: number;
  missRate: number;
  totalHits: number;
  totalMisses: number;
  cacheSize: number;
  ttlSeconds: number;
}

/**
 * Accuracy and relevance metrics.
 */
export interface AccuracyMetrics {
  overallScore: number;
  byStrategy: Record<RetrievalStrategyType, number>;
  userFeedbackScore: number;
  citationAccuracy: number;
}

/**
 * Cost breakdown metrics.
 */
export interface CostMetrics {
  currentSession: number;
  projectedMonthly: number;
  avgPerQuery: number;
  byStrategy: Record<RetrievalStrategyType, number>;
  budgetUsed: number;
  budgetLimit: number;
}

/**
 * Complete RAG metrics snapshot.
 */
export interface RagMetrics {
  timestamp: string;
  latency: LatencyMetrics;
  tokenUsage: TokenUsage;
  cache: CacheMetrics;
  accuracy: AccuracyMetrics;
  cost: CostMetrics;
}

/**
 * Props for PerformanceMetrics container component.
 */
export interface PerformanceMetricsProps {
  metrics?: RagMetrics | null;
  isLoading?: boolean;
  error?: string | null;
  onRefresh?: () => void;
  showHistorical?: boolean;
  className?: string;
}

/**
 * Props for individual metric widgets.
 */
export interface MetricWidgetProps {
  className?: string;
}

export interface LatencyChartProps extends MetricWidgetProps {
  data: LatencyMetrics;
  targetThreshold?: number;
}

export interface TokenDistributionProps extends MetricWidgetProps {
  data: TokenUsage;
}

export interface CacheHitGaugeProps extends MetricWidgetProps {
  data: CacheMetrics;
}

export interface AccuracyScoreProps extends MetricWidgetProps {
  data: AccuracyMetrics;
}

export interface CostBreakdownProps extends MetricWidgetProps {
  data: CostMetrics;
}
