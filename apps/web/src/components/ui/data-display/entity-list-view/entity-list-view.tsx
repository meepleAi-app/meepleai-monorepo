/**
 * EntityListView - Generic Multi-View List Component
 *
 * A flexible, reusable component for displaying entity lists in three view modes:
 * Grid, List, and Carousel. Supports search, sort, filter, and localStorage persistence.
 *
 * @module components/ui/data-display/entity-list-view
 *
 * Phase 2 Status: All view modes supported (Grid/List/Carousel)
 *
 * Features (Phase 1-2):
 * - Grid layout with responsive columns
 * - List layout with compact vertical cards
 * - Carousel integration with GameCarousel
 * - View mode switcher (all 3 modes)
 * - localStorage persistence across modes
 * - Empty and loading states
 * - TypeScript generic support
 *
 * @example
 * ```tsx
 * <EntityListView
 *   items={games}
 *   entity="game"
 *   persistenceKey="games-browse"
 *   renderItem={(game) => ({
 *     id: game.id,
 *     title: game.title,
 *     subtitle: game.publisher,
 *     imageUrl: game.coverUrl,
 *     rating: game.averageRating,
 *   })}
 *   onItemClick={(game) => router.push(`/games/${game.id}`)}
 *   title="Featured Games"
 * />
 * ```
 */

'use client';

import React from 'react';

import { cn } from '@/lib/utils';

import { GameCarousel, type CarouselGame } from '../game-carousel';
import { MeepleCard } from '../meeple-card';
import { EmptyState } from './components/empty-state';
import { EntityTableView } from './components/entity-table-view';
import { FilterPanel } from './components/filter-panel';
import { LoadingSkeleton } from './components/loading-skeleton';
import { SearchBar } from './components/search-bar';
import { SidebarFilters } from './components/sidebar-filters';
import { SortDropdown } from './components/sort-dropdown';
import { ViewModeSwitcher } from './components/view-mode-switcher';
import { useFilters } from './hooks/use-filters';
import { useSearch } from './hooks/use-search';
import { useSort } from './hooks/use-sort';
import { useViewMode } from './hooks/use-view-mode';

import type { EntityListViewProps } from './entity-list-view.types';

// ============================================================================
// Main Component
// ============================================================================

/**
 * EntityListView - Render entity lists in Grid/List/Carousel modes
 *
 * Phase 1: Grid mode only
 * Phase 2: List + Carousel modes
 * Phase 3: Search + Sort
 * Phase 4: Filters
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function EntityListView<T = any>({
  // Required
  items,
  entity,
  persistenceKey,
  renderItem,

  // View mode
  defaultViewMode = 'grid',
  viewMode: controlledViewMode,
  onViewModeChange,
  availableModes,

  // Item interaction
  onItemClick,
  cardClassName,

  // Grid configuration
  gridColumns = {
    default: 1,
    sm: 2,
    lg: 3,
    xl: 4,
  },
  gridGap = 4,

  // Carousel configuration
  carouselOptions,

  // Table configuration
  tableColumns,

  // Search & Sort
  searchable = false,
  searchPlaceholder,
  searchFields = [],
  onSearch: customSearch,
  sortOptions = [],
  defaultSort,
  sort: controlledSort,
  onSortChange,

  // Filters (Phase 4)
  filters = [],
  onFilterChange,

  // Layout & styling
  title,
  subtitle,
  showViewSwitcher = true,
  className,
  emptyMessage = 'No items to display',
  loading = false,
  'data-testid': testId,
}: EntityListViewProps<T>) {
  // View mode state with localStorage persistence
  const {
    mode,
    setMode,
    isAvailable: _isAvailable,
  } = useViewMode(
    persistenceKey,
    defaultViewMode,
    availableModes || ['grid', 'list', 'carousel', 'table'],
    controlledViewMode
  );

  // Notify parent of mode changes
  React.useEffect(() => {
    onViewModeChange?.(mode);
  }, [mode, onViewModeChange]);

  // Search functionality (Phase 3)
  const {
    query,
    setQuery,
    filteredItems: searchedItems,
  } = useSearch(items, searchFields as string[], customSearch);

  // Filter functionality (Phase 4)
  const {
    filterState,
    setFilterState,
    filteredItems: searchFilteredItems,
    clearFilters,
    activeCount: activeFilterCount,
  } = useFilters(searchedItems, filters); // Apply search first, then filters

  // Notify parent of filter changes
  React.useEffect(() => {
    onFilterChange?.(filterState);
  }, [filterState, onFilterChange]);

  // Sort functionality (Phase 3)
  const { currentSort, setCurrentSort, sortedItems } = useSort(
    searchFilteredItems, // Apply search → filter, then sort
    sortOptions,
    defaultSort,
    controlledSort
  );

  // Notify parent of sort changes
  React.useEffect(() => {
    onSortChange?.(currentSort);
  }, [currentSort, onSortChange]);

  // Final items after search → filter → sort pipeline
  const displayItems = sortedItems;

  // ========== Render Functions ==========

  /**
   * Render grid layout
   * Phase 1: Main implementation
   */
  const renderGridLayout = () => {
    // Build responsive grid classes
    const gridClasses = cn(
      'grid',
      `gap-${gridGap}`,
      gridColumns.default && `grid-cols-${gridColumns.default}`,
      gridColumns.sm && `sm:grid-cols-${gridColumns.sm}`,
      gridColumns.md && `md:grid-cols-${gridColumns.md}`,
      gridColumns.lg && `lg:grid-cols-${gridColumns.lg}`,
      gridColumns.xl && `xl:grid-cols-${gridColumns.xl}`,
      gridColumns['2xl'] && `2xl:grid-cols-${gridColumns['2xl']}`
    );

    return (
      <div className={gridClasses} data-testid="grid-layout">
        {displayItems.map((item, idx) => {
          const cardProps = renderItem(item);

          return (
            <MeepleCard
              key={cardProps.id || `item-${idx}`}
              entity={entity}
              variant="grid"
              {...cardProps}
              onClick={onItemClick ? () => onItemClick(item) : cardProps.onClick}
              className={cn(cardClassName, cardProps.className)}
            />
          );
        })}
      </div>
    );
  };

  /**
   * Render list layout
   * Vertical stack with MeepleCard variant="list"
   */
  const renderListLayout = () => {
    return (
      <div className="space-y-2" data-testid="list-layout">
        {displayItems.map((item, idx) => {
          const cardProps = renderItem(item);

          return (
            <MeepleCard
              key={cardProps.id || `item-${idx}`}
              entity={entity}
              variant="list"
              {...cardProps}
              onClick={onItemClick ? () => onItemClick(item) : cardProps.onClick}
              className={cn(cardClassName, cardProps.className)}
            />
          );
        })}
      </div>
    );
  };

  /**
   * Render carousel layout
   * Wraps GameCarousel component with item transformation
   */
  const renderCarouselLayout = () => {
    // Transform generic items to CarouselGame format
    const carouselGames: CarouselGame[] = displayItems.map(item => {
      const cardProps = renderItem(item);
      return {
        id: cardProps.id || String(displayItems.indexOf(item)),
        title: cardProps.title,
        subtitle: cardProps.subtitle,
        imageUrl: cardProps.imageUrl,
        rating: cardProps.rating,
        ratingMax: cardProps.ratingMax,
        metadata: cardProps.metadata,
        badge: cardProps.badge,
      };
    });

    return (
      <div data-testid="carousel-layout">
        <GameCarousel
          games={carouselGames}
          onGameSelect={game => {
            const originalItem = items.find(item => renderItem(item).id === game.id);
            if (originalItem) onItemClick?.(originalItem);
          }}
          autoPlay={carouselOptions?.autoPlay ?? false}
          autoPlayInterval={carouselOptions?.autoPlayInterval}
          showDots={carouselOptions?.showDots ?? true}
          sortable={false}
        />
      </div>
    );
  };

  /**
   * Render table layout
   * Uses EntityTableView with DataTable integration
   */
  const renderTableLayout = () => {
    return (
      <EntityTableView
        displayItems={displayItems}
        items={items}
        entity={entity}
        renderItem={renderItem}
        tableColumns={tableColumns}
        onItemClick={onItemClick}
        emptyMessage={emptyMessage}
      />
    );
  };

  // ========== Loading State ==========

  if (loading) {
    return <LoadingSkeleton mode={mode} className={className} />;
  }

  // ========== Empty State ==========

  if (items.length === 0) {
    return <EmptyState message={emptyMessage} className={className} />;
  }

  // ========== Main Render ==========

  return (
    <section
      className={cn('w-full', className)}
      data-testid={testId || 'entity-list-view'}
      aria-label={title || 'Entity list'}
    >
      {/* Header */}
      {(title ||
        subtitle ||
        showViewSwitcher ||
        searchable ||
        sortOptions.length > 0 ||
        filters.length > 0) && (
        <header className="space-y-4 mb-6">
          {/* Title, Subtitle & View Mode Switcher */}
          {(title || subtitle || showViewSwitcher) && (
            <div className="flex items-start justify-between">
              {/* Title & Subtitle */}
              <div className="flex-1">
                {title && (
                  <h2 className="font-quicksand font-bold text-2xl md:text-3xl text-foreground">
                    {title}
                  </h2>
                )}
                {subtitle && (
                  <p className="mt-2 text-muted-foreground text-sm md:text-base">{subtitle}</p>
                )}
              </div>

              {/* View Mode Switcher */}
              {showViewSwitcher && (
                <ViewModeSwitcher
                  value={mode}
                  onChange={setMode}
                  availableModes={availableModes || ['grid', 'list', 'carousel']}
                />
              )}
            </div>
          )}

          {/* Search, Sort & Filter Controls (hidden in list mode - sidebar handles it) */}
          {mode !== 'list' && (searchable || sortOptions.length > 0 || filters.length > 0) && (
            <div className="space-y-3">
              <div className="flex flex-col sm:flex-row gap-3">
                {/* SearchBar */}
                {searchable && (
                  <div className="flex-1">
                    <SearchBar value={query} onChange={setQuery} placeholder={searchPlaceholder} />
                  </div>
                )}

                {/* SortDropdown */}
                {sortOptions.length > 0 && (
                  <SortDropdown
                    value={currentSort}
                    options={sortOptions}
                    onChange={setCurrentSort}
                  />
                )}
              </div>

              {/* FilterPanel */}
              {filters.length > 0 && (
                <FilterPanel
                  filters={filters}
                  filterState={filterState}
                  onChange={setFilterState}
                  onClear={clearFilters}
                  activeCount={activeFilterCount}
                />
              )}
            </div>
          )}
        </header>
      )}

      {/* Content - View mode dependent */}
      {mode === 'grid' && renderGridLayout()}
      {mode === 'list' && (
        <div className="flex gap-6" data-testid="list-with-sidebar">
          {/* Sidebar Filters (list mode only) */}
          {(searchable || sortOptions.length > 0) && (
            <SidebarFilters
              searchQuery={query}
              onSearchChange={setQuery}
              searchPlaceholder={searchPlaceholder}
              searchable={searchable}
              sortOptions={sortOptions}
              currentSort={currentSort}
              onSortChange={setCurrentSort}
              activeFilterCount={activeFilterCount}
              onClearFilters={clearFilters}
            />
          )}
          {/* List content */}
          <div className="flex-1 min-w-0">{renderListLayout()}</div>
        </div>
      )}
      {mode === 'carousel' && renderCarouselLayout()}
      {mode === 'table' && renderTableLayout()}

      {/* Screen Reader Announcements */}
      <div className="sr-only" aria-live="polite" aria-atomic="true">
        {loading ? 'Loading...' : `Showing ${displayItems.length} items in ${mode} view`}
      </div>
    </section>
  );
}

// Memoize for performance
EntityListView.displayName = 'EntityListView';
