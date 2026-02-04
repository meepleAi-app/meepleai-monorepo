/**
 * RAG Dashboard Components
 *
 * Interactive dashboard for visualizing TOMAC-RAG system.
 * Part of MeepleAI documentation at /rag
 */

export { RagDashboard } from './RagDashboard';
export { StatsGrid } from './StatsGrid';
export { QuerySimulator } from './QuerySimulator';
export { TokenFlowVisualizer } from './TokenFlowVisualizer';
export { CostCalculator } from './CostCalculator';
export { ArchitectureExplorer } from './ArchitectureExplorer';
export { AgentRagIntegration } from './AgentRagIntegration';
export { VariantComparisonTool } from './VariantComparisonTool';
export { LayerDeepDocs } from './LayerDeepDocs';
export { PromptTemplateBuilder } from './PromptTemplateBuilder';
export { AgentRoleConfigurator } from './AgentRoleConfigurator';
export { DecisionWalkthrough } from './DecisionWalkthrough';
export { ModelSelectionOptimizer } from './ModelSelectionOptimizer';
export { PerformanceMetricsTable } from './PerformanceMetricsTable';
export { TechnicalReference } from './TechnicalReference';
export { PocStatus } from './PocStatus';
export { ParameterGuide } from './ParameterGuide';

export * from './types';
export * from './types-configurable';
// Explicit rag-data exports to avoid conflicts with types-configurable
export {
  STRATEGIES,
  LAYERS,
  MODEL_PRICING,
  USER_TIERS,
  TIER_STRATEGY_ACCESS,
  STRATEGY_MODEL_MAPPING,
  AGENT_CLASSIFICATION,
  METRICS,
  DOC_STRUCTURE,
  getStrategy,
  getLayer,
  // Note: calculateQueryCost, calculateMonthlyCost, formatLatency conflict with types-configurable
  // Use rag-data versions with explicit import if needed
} from './rag-data';
export type {
  StrategyData,
  LayerData,
  ModelPricingData,
  UserTierData,
  TierStrategyAccess,
  StrategyModelMapping,
  AgentClassificationData,
} from './rag-data';
export { RagConfigurationForm } from './RagConfigurationForm';
export { StrategyCard } from './StrategyCard';
export { StrategySelector } from './StrategySelector';

// Navigation components
export { DashboardSidebar } from './DashboardSidebar';
export { DashboardNav } from './DashboardNav';
export { SectionGroup } from './SectionGroup';
export { ProgressIndicator } from './ProgressIndicator';
export { useScrollSpy, scrollToSection } from './hooks/useScrollSpy';
export { NAVIGATION_GROUPS } from './RagDashboard';

export type { NavGroup, NavSection, DashboardSidebarProps } from './DashboardSidebar';
export type { DashboardNavProps } from './DashboardNav';
export type { SectionGroupProps } from './SectionGroup';
export type { ProgressIndicatorProps } from './ProgressIndicator';
export type { UseScrollSpyOptions } from './hooks/useScrollSpy';

// Retrieval Strategy Components (Issue #3300, #3301)
export {
  // Cards (Issue #3300)
  RetrievalStrategyCard,
  RetrievalStrategyGrid,
  // Modal (Issue #3301)
  StrategyDetailModal,
  StrategyFlowDiagram,
  StrategyExample,
  // Data
  RETRIEVAL_STRATEGIES,
  RETRIEVAL_STRATEGY_ORDER,
  STRATEGY_DETAILS,
  getAllRetrievalStrategies,
  getRetrievalStrategy,
  getStrategyDetails,
} from './retrieval-strategies';
export type {
  RetrievalStrategyType,
  RetrievalStrategy,
  RetrievalStrategyCardProps,
  RetrievalStrategyGridProps,
  StrategyMetrics,
  MetricTier,
  StrategyDetailModalProps,
  StrategyDetailContent,
  FlowStep,
  StrategyExampleType,
} from './retrieval-strategies';

// Performance Metrics Components (Issue #3302)
export {
  // Main container
  PerformanceMetrics,
  // Individual widgets
  LatencyChart,
  TokenDistribution,
  CacheHitGauge,
  AccuracyScore,
  CostBreakdown,
  // Data
  MOCK_RAG_METRICS,
  generateMockMetrics,
} from './metrics';
export type {
  RagMetrics,
  LatencyMetrics,
  TokenUsage,
  CacheMetrics,
  AccuracyMetrics,
  CostMetrics,
  PerformanceMetricsProps,
  LatencyChartProps,
  TokenDistributionProps,
  CacheHitGaugeProps,
  AccuracyScoreProps,
  CostBreakdownProps,
} from './metrics';

// Configuration Panel Components (Issue #3303)
export {
  // Main container
  StrategyConfigPanel,
  // Individual config components
  GenerationParams,
  RetrievalParams,
  RerankerSettings,
  ModelSelector,
  StrategySpecificSettings,
  // Constants
  DEFAULT_RAG_CONFIG,
  STRATEGY_PRESETS,
  LLM_MODELS,
  RERANKER_MODELS,
} from './config';
export type {
  RagConfig,
  GenerationParamsType,
  RetrievalParamsType,
  RerankerSettingsType,
  ModelSelection,
  StrategySpecificSettingsType,
  LlmModelId,
  RerankerModelId,
  GenerationParamsProps,
  RetrievalParamsProps,
  RerankerSettingsProps,
  ModelSelectorProps,
  StrategySpecificProps,
  StrategyConfigPanelProps,
} from './config';
