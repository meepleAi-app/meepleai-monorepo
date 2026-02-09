/**
 * Dashboard Components (Issue #3286: User Dashboard Layout System)
 *
 * Exports all dashboard-related components for the MeepleAI navigation hub.
 */

// Main Dashboard
export { Dashboard, type SectionConfig } from './Dashboard';
export { DashboardHub } from './DashboardHub'; // Issue #3975 - New Hub Layout

// Sections and Layout
export { DashboardSection, type ViewMode, type DashboardSectionProps } from './DashboardSection';
export { DashboardHeader } from './DashboardHeader';
export { HeroStats, type DashboardStats, type HeroStatsProps } from './HeroStats';
export { KpiCard, KpiCardSkeleton, type KpiCardProps, type KpiColorVariant, type KpiTrend, type KpiStreak } from './KpiCard';

// Cards and Items
export { GameCard, type GameData, type GameCardProps } from './GameCard';
export { ActivityItem, type ActivityData, type ActivityType, type ActivityItemProps } from './ActivityItem';
export { WishlistCard, type WishlistItemData, type WishlistPriority, type WishlistVisibility, type WishlistCardProps } from './WishlistCard';
export { NotificationItem, type NotificationData, type NotificationType, type NotificationStatus, type NotificationAction, type NotificationActionType, type NotificationItemProps } from './NotificationItem';

// Widgets (Issue #3309, #3310, #3311, #3312, #3313)
export { ActiveSessionsWidget, type ActiveSession, type ActiveSessionsWidgetProps } from './ActiveSessionsWidget';
export { LibrarySnapshot, type TopGame, type LibraryQuota, type LibrarySnapshotProps } from './LibrarySnapshot';
export { ChatHistorySection, type ChatThread, type ChatHistorySectionProps } from './ChatHistorySection';
export { QuickActionsGrid, type QuickAction as QuickActionGridItem, type QuickActionsGridProps } from './QuickActionsGrid';
export { ActivityFeed, type ActivityEvent, type ActivityEventType, type ActivityFeedProps } from './ActivityFeed';

// Legacy components (Issue #1834: UI-007)
export { QuickActions, type QuickActionsProps, type QuickAction } from './QuickActions';
export { QuickActionCard, type QuickActionCardProps } from './QuickActionCard';
export { LibraryQuotaWidget, type LibraryQuotaWidgetProps } from './LibraryQuotaWidget';
export { ActiveSessionsPanel, type ActiveSessionsPanelProps } from './ActiveSessionsPanel';

// MeepleCard-based dashboard components (Issue #3334)
export { MeepleGameWidget, MeepleGameWidgetSkeleton, type DashboardGame, type MeepleGameWidgetProps } from './MeepleGameWidget';

// Legacy UserDashboard and UserDashboardCompact removed - Issue #3910
// Use Dashboard.tsx for modern hub layout
