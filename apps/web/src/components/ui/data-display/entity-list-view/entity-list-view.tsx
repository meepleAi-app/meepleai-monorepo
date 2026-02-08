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
import { MeepleCard } from '../meeple-card';
import { GameCarousel, type CarouselGame } from '../game-carousel';
import { useViewMode } from './hooks/use-view-mode';
import { ViewModeSwitcher } from './components/view-mode-switcher';
import { EmptyState } from './components/empty-state';
import { LoadingSkeleton } from './components/loading-skeleton';
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
  const { mode, setMode, isAvailable } = useViewMode(
    persistenceKey,
    defaultViewMode,
    availableModes || ['grid', 'list', 'carousel'],
    controlledViewMode
  );

  // Notify parent of mode changes
  React.useEffect(() => {
    onViewModeChange?.(mode);
  }, [mode, onViewModeChange]);

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
        {items.map((item, idx) => {
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
        {items.map((item, idx) => {
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
    const carouselGames: CarouselGame[] = items.map((item) => {
      const cardProps = renderItem(item);
      return {
        id: cardProps.id || String(items.indexOf(item)),
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
          onGameSelect={(game) => {
            const originalItem = items.find((item) => renderItem(item).id === game.id);
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
      {(title || subtitle || showViewSwitcher) && (
        <header className="flex items-start justify-between mb-6">
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
        </header>
      )}

      {/* Content - View mode dependent */}
      {mode === 'grid' && renderGridLayout()}
      {mode === 'list' && renderListLayout()}
      {mode === 'carousel' && renderCarouselLayout()}

      {/* Screen Reader Announcements */}
      <div className="sr-only" aria-live="polite" aria-atomic="true">
        {loading ? 'Loading...' : `Showing ${items.length} items in ${mode} view`}
      </div>
    </section>
  );
}

// Memoize for performance
EntityListView.displayName = 'EntityListView';
