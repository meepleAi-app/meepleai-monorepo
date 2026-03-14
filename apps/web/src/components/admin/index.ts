// Barrel exports for admin module
export {
  EndpointDistributionChart,
  LatencyDistributionChart,
  RequestsTimeSeriesChart,
  FeedbackChart,
} from './AdminCharts';
export { default as CategoryConfigTab } from './CategoryConfigTab';
export { default as FeatureFlagsTab } from './FeatureFlagsTab';

// Issue #874: Enhanced dashboard components
export { StatCard, type StatCardProps } from './StatCard';
export { MetricsGrid, type MetricsGridProps } from './MetricsGrid';

// Issue #2803: Unified Activity Feed/Timeline component
// ActivityFeed is the primary component with configurable icon modes and i18n
export { ActivityFeed, type ActivityFeedProps } from './ActivityFeed';
// ActivityTimeline is a re-export alias for backward compatibility
export { ActivityTimeline, type ActivityTimelineProps } from './ActivityTimeline';
// ActivityEvent type is centralized in utils/activityUtils
export type { ActivityEvent } from './utils/activityUtils';

// Issue #881: Modular admin layout components (AdminHeader/AdminSidebar/AdminLayout removed — replaced by UnifiedShell)
export {
  AdminBreadcrumbs,
  type AdminBreadcrumbsProps,
  type BreadcrumbItem,
} from './AdminBreadcrumbs';
export { AdminAuthGuard } from './AdminAuthGuard';
export { QuickActions, type QuickActionsProps, type QuickAction } from './QuickActions';
export { SystemStatus, type SystemStatusProps, type ServiceStatus } from './SystemStatus';

// Issue #896: Infrastructure monitoring components
export { ServiceCard, type ServiceCardProps } from './ServiceCard';
export { ServiceHealthMatrix, type ServiceHealthMatrixProps } from './ServiceHealthMatrix';

// Issue #2784: Dashboard Redesign - Enhanced Header
export { DashboardHeader, type DashboardHeaderProps } from './DashboardHeader';

// Issue #2785: Dashboard Redesign - KPI Cards
export { KPICard, type KPICardData, type KPICardProps, type BadgeVariant } from './KPICard';
export {
  KPICardsGrid,
  buildKPICards,
  calculateTrendPercent,
  estimateAiCost,
  type KPICardsGridProps,
  type BuildKPICardsOptions,
} from './KPICardsGrid';

// Issue #901: Grafana embed iframe
export { GrafanaEmbed } from './GrafanaEmbed';

// Issue #910: Filter panel component
export { ApiKeyFilterPanel, type ApiKeyFilterPanelProps } from './ApiKeyFilterPanel';

// Issue #912: Bulk action bar component
export {
  BulkActionBar,
  EmptyBulkActionBar,
  type BulkActionBarProps,
  type EmptyBulkActionBarProps,
  type BulkAction,
} from './BulkActionBar';

// Issue #911: User activity timeline components
export { UserActivityTimeline, type UserActivityTimelineProps } from './UserActivityTimeline';
export {
  UserActivityItem,
  type UserActivityItemProps,
  type UserActivityEvent,
} from './UserActivityItem';
export {
  UserActivityFilters,
  type UserActivityFiltersProps,
  type UserActivityFilters as UserActivityFiltersType,
} from './UserActivityFilters';

// Issue #2391 Sprint 2: Agent Configuration components
export { AgentModeSelector, LlmProviderSelector } from './agents';
export type { AgentModeSelectorProps, LlmProviderSelectorProps } from './agents';

// Issue #2372: Shared Game Catalog components
export {
  GameStatusBadge,
  getStatusLabel,
  getStatusColorClass,
  PlayersBadge,
  PlayTimeBadge,
  ComplexityBadge,
  RatingBadge,
  AgeBadge,
  CategoryPill,
  CategoryPillList,
  MechanicPill,
  MechanicPillList,
  GameForm,
  VersionBadge,
  TagInput,
  PdfDocumentList,
} from './shared-games';
export type {
  GameStatusBadgeProps,
  PlayersBadgeProps,
  PlayTimeBadgeProps,
  ComplexityBadgeProps,
  RatingBadgeProps,
  AgeBadgeProps,
  CategoryPillProps,
  CategoryPillListProps,
  MechanicPillProps,
  MechanicPillListProps,
  GameFormProps,
  VersionBadgeProps,
  TagInputProps,
  PdfDocumentListProps,
} from './shared-games';
