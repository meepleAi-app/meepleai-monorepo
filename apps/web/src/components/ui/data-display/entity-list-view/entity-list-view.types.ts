/**
 * TypeScript type definitions for EntityListView component system
 *
 * @module components/ui/data-display/entity-list-view/types
 */

import type { LucideIcon } from 'lucide-react';
import type { MeepleCardProps, MeepleEntityType } from '../meeple-card';

// ============================================================================
// View Mode Types
// ============================================================================

/**
 * Available view modes for entity lists
 */
export type ViewMode = 'grid' | 'list' | 'carousel';

// ============================================================================
// Sort Configuration
// ============================================================================

/**
 * Sort option configuration
 */
export interface SortOption<T> {
  /** Unique identifier for this sort option */
  value: string;
  /** Display label */
  label: string;
  /** Optional icon from lucide-react */
  icon?: LucideIcon;
  /** Comparator function for Array.sort() */
  compareFn: (a: T, b: T) => number;
}

// ============================================================================
// Filter Configuration
// ============================================================================

/**
 * Base filter properties shared by all filter types
 */
interface BaseFilter {
  /** Unique filter identifier */
  id: string;
  /** Display label */
  label: string;
  /** Optional description/help text */
  description?: string;
}

/**
 * Select filter (single or multi-select dropdown)
 */
export interface SelectFilter<T> extends BaseFilter {
  type: 'select';
  /** Field to filter on (supports dot notation for nested fields) */
  field: keyof T | string;
  /** Available options */
  options: Array<{ value: string; label: string }>;
  /** Enable multi-select mode */
  multiple?: boolean;
}

/**
 * Checkbox filter (boolean toggle)
 */
export interface CheckboxFilter<T> extends BaseFilter {
  type: 'checkbox';
  /** Field to filter on (should be boolean or truthy/falsy) */
  field: keyof T | string;
}

/**
 * Range filter (numeric slider)
 */
export interface RangeFilter<T> extends BaseFilter {
  type: 'range';
  /** Field to filter on (should be numeric) */
  field: keyof T | string;
  /** Minimum value */
  min: number;
  /** Maximum value */
  max: number;
  /** Step size (default: 1) */
  step?: number;
  /** Unit label (e.g., "players", "minutes") */
  unit?: string;
}

/**
 * Date range filter (start/end date picker)
 */
export interface DateRangeFilter<T> extends BaseFilter {
  type: 'date-range';
  /** Field to filter on (should be Date or ISO string) */
  field: keyof T | string;
}

/**
 * Union type of all filter configurations
 */
export type FilterConfig<T> =
  | SelectFilter<T>
  | CheckboxFilter<T>
  | RangeFilter<T>
  | DateRangeFilter<T>;

/**
 * Active filter state (filterId -> filterValue)
 */
export type FilterState = Record<string, any>;

// ============================================================================
// Grid Configuration
// ============================================================================

/**
 * Responsive grid column configuration
 */
export interface GridColumns {
  /** Default columns (mobile-first) */
  default?: number;
  /** Small screens (640px+) */
  sm?: number;
  /** Medium screens (768px+) */
  md?: number;
  /** Large screens (1024px+) */
  lg?: number;
  /** Extra large screens (1280px+) */
  xl?: number;
  /** 2XL screens (1536px+) */
  '2xl'?: number;
}

// ============================================================================
// Carousel Configuration
// ============================================================================

/**
 * Carousel-specific options
 */
export interface CarouselOptions {
  /** Enable auto-play rotation */
  autoPlay?: boolean;
  /** Auto-play interval in milliseconds */
  autoPlayInterval?: number;
  /** Show navigation dots */
  showDots?: boolean;
  /** Carousel orientation */
  orientation?: 'horizontal' | 'vertical' | 'auto';
}

// ============================================================================
// Main Component Props
// ============================================================================

/**
 * EntityListView component props
 *
 * Generic component for displaying lists of entities in Grid/List/Carousel modes
 * with search, sort, filter capabilities and localStorage persistence.
 *
 * @template T - Type of items in the list
 */
export interface EntityListViewProps<T = any> {
  // ========== REQUIRED ==========

  /** Array of items to display */
  items: T[];

  /** Entity type for MeepleCard (determines color scheme) */
  entity: MeepleEntityType;

  /** Unique key for localStorage persistence (e.g., "games-browse", "collections-dashboard") */
  persistenceKey: string;

  /** Transform item to MeepleCard props (omit entity and variant) */
  renderItem: (item: T) => Omit<MeepleCardProps, 'entity' | 'variant'>;

  // ========== VIEW MODE ==========

  /** Default view mode if no localStorage value (default: 'grid') */
  defaultViewMode?: ViewMode;

  /** Controlled view mode (overrides internal state and localStorage) */
  viewMode?: ViewMode;

  /** Callback when view mode changes */
  onViewModeChange?: (mode: ViewMode) => void;

  /** Available view modes (default: all 3) */
  availableModes?: ViewMode[];

  // ========== ITEM INTERACTION ==========

  /** Callback when item is clicked */
  onItemClick?: (item: T) => void;

  /** Custom className for individual cards */
  cardClassName?: string;

  // ========== GRID CONFIGURATION ==========

  /** Grid responsive columns (default: 1 → 2 → 3 → 4) */
  gridColumns?: GridColumns;

  /** Grid gap spacing (Tailwind scale: 2-8, default: 4 = 16px) */
  gridGap?: 2 | 3 | 4 | 5 | 6 | 8;

  // ========== CAROUSEL CONFIGURATION ==========

  /** Carousel-specific options (passed to GameCarousel) */
  carouselOptions?: CarouselOptions;

  // ========== SEARCH & FILTERS ==========

  /** Enable search bar */
  searchable?: boolean;

  /** Search placeholder text (default: "Search...") */
  searchPlaceholder?: string;

  /** Item fields to search (supports dot notation for nested fields) */
  searchFields?: Array<keyof T | string>;

  /** Custom search function (overrides default fuzzy search) */
  onSearch?: (query: string, items: T[]) => T[];

  /** Sort options */
  sortOptions?: SortOption<T>[];

  /** Default sort option value */
  defaultSort?: string;

  /** Controlled sort value */
  sort?: string;

  /** Callback when sort changes */
  onSortChange?: (sort: string) => void;

  /** Filter configuration */
  filters?: FilterConfig<T>[];

  /** Callback when filters change */
  onFilterChange?: (filters: FilterState) => void;

  // ========== LAYOUT & STYLING ==========

  /** Section title */
  title?: string;

  /** Section subtitle/description */
  subtitle?: string;

  /** Show ViewModeSwitcher (default: true) */
  showViewSwitcher?: boolean;

  /** Additional CSS classes */
  className?: string;

  /** Empty state message (default: "No items to display") */
  emptyMessage?: string;

  /** Loading state */
  loading?: boolean;

  /** Test ID for testing */
  'data-testid'?: string;
}
