/**
 * Collection Components
 * Issue #3632 - EPIC #3475: User Private Library & Collections Management
 *
 * Exports all collection-related components for user game library management.
 */

// Main Dashboard
export { CollectionDashboard, type CollectionDashboardProps, type ViewMode, type SortOption } from './CollectionDashboard';

// Grid and Display
export { CollectionGrid } from './CollectionGrid';

// Cards
// MeepleCard removed - use @/components/ui/data-display/meeple-card instead

// Stats
export { CollectionStats } from './CollectionStats';

// Hero Stats Bar - Issue #3649
export { CollectionStatsBar, type CollectionStatsBarProps } from './CollectionStatsBar';

// Filters - Issue #3649
export { CollectionFilters, type CollectionFiltersProps } from './CollectionFilters';
