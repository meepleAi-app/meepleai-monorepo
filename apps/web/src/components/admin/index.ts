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
