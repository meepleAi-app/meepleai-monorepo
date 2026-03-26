/**
 * Dashboard Components
 * Consolidated barrel export for all Dashboard components
 */

// Dashboard Engine
export { dashboardMachine } from './DashboardEngine';
export type { DashboardEvent, DashboardEngineContext } from './DashboardEngine';
export { DashboardEngineProvider } from './DashboardEngineProvider';
export { useDashboardMode } from './useDashboardMode';

// New Dashboard Components
export { HeroBanner } from './HeroBanner';
export { QuickActionsRow } from './QuickActionsRow';
export { DashboardScrollRow } from './DashboardScrollRow';

// Dashboard V2 Components (Issue #4581)
export { EmptyState } from './empty-states';
export { FilterBar } from './filter-bar';
export { GameCollectionGrid } from './game-collection-grid';
export { RecentSessions } from './recent-sessions';
export { SessionRow } from './session-row';
export { StatCard } from './stat-card';
// Issue #5094 — Dashboard Redesign
export { DashboardSessionHero } from './session-hero';
export { RecentGamesSection } from './recent-games-section';
export { AgentsDashboardSection } from './agents-section';
export { RecentChatsDashboardSection } from './recent-chats-section';
// Issue #448 — Contextual Dashboard
export { GameNightHero } from './game-night-hero';
export { IncompleteSessionHero } from './incomplete-session-hero';
export { QuickCardsCarousel } from './quick-cards-carousel';
export { ActivityFeed } from './activity-feed';
export type { ActivityItem } from './activity-feed';
export { SessionModeDashboard } from './session-mode-dashboard';
export { SessionQuickActions } from './session-quick-actions';
