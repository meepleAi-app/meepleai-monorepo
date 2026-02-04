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
