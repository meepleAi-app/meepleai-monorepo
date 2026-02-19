/**
 * EntityListView - Public API
 *
 * @module components/ui/data-display/entity-list-view
 */

// Main component
export { EntityListView } from './entity-list-view';

// Types
export type {
  EntityListViewProps,
  ViewMode,
  SortOption,
  FilterConfig,
  SelectFilter,
  CheckboxFilter,
  RangeFilter,
  DateRangeFilter,
  FilterState,
  GridColumns,
  CarouselOptions,
  TableColumnConfig,
} from './entity-list-view.types';

// Subcomponents (exported for advanced use cases)
export { ViewModeSwitcher } from './components/view-mode-switcher';
export { EmptyState } from './components/empty-state';
export { LoadingSkeleton } from './components/loading-skeleton';
export { EntityTableView } from './components/entity-table-view';
export { SidebarFilters } from './components/sidebar-filters';

// Hooks (exported for custom implementations)
export { useViewMode } from './hooks/use-view-mode';
export type { UseViewModeReturn } from './hooks/use-view-mode';
