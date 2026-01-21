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
export { AdminLayout, type AdminLayoutProps } from './AdminLayout';
export { StatCard, type StatCardProps } from './StatCard';
export { MetricsGrid, type MetricsGridProps } from './MetricsGrid';
export { ActivityFeed, type ActivityFeedProps, type ActivityEvent } from './ActivityFeed';

// Issue #2787: Activity Timeline with new design system
export { ActivityTimeline, type ActivityTimelineProps } from './ActivityTimeline';
export type { ActivityEvent } from './utils/activityUtils';

// Issue #881: Modular admin layout components
export {
  AdminSidebar,
  defaultNavigation,
  type AdminSidebarProps,
  type NavItem,
  type NavItemBadge,
} from './AdminSidebar';
export { AdminHeader, type AdminHeaderProps, type AdminUser } from './AdminHeader';
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
