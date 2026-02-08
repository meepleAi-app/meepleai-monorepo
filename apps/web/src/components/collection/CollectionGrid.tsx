/**
 * CollectionGrid Component - Issue #3476
 *
 * Grid display of games with sort/filter controls
 *
 * Features:
 * - Responsive grid layout (1-4 columns)
 * - Sort controls (date, title, last played, chat activity, play count)
 * - Filter controls (has PDF, has active chat, category)
 * - Loading skeleton state
 * - Empty state
 * - MeepleCard instances
 *
 * @example
 * ```tsx
 * <CollectionGrid
 *   games={gamesData}
 *   sortBy="date-added-desc"
 *   filters={{}}
 *   onSortChange={(sort) => console.log(sort)}
 *   onFilterChange={(filters) => console.log(filters)}
 * />
 * ```
 */

'use client';

import { useState } from 'react';

import { motion } from 'framer-motion';
import {
  SlidersHorizontal,
  Filter,
  X,
  Sparkles,
} from 'lucide-react';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';
import type { CollectionGridProps, SortOption, SortConfig, FilterOption } from '@/types/collection';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

// ============================================================================
// Sort & Filter Configuration
// ============================================================================

const SORT_OPTIONS: SortConfig[] = [
  { value: 'date-added-desc', label: 'Data Aggiunta (Recente)' },
  { value: 'date-added-asc', label: 'Data Aggiunta (Vecchio)' },
  { value: 'title-asc', label: 'Titolo (A-Z)' },
  { value: 'title-desc', label: 'Titolo (Z-A)' },
  { value: 'last-played-desc', label: 'Ultimo Gioco (Recente)' },
  { value: 'last-played-asc', label: 'Ultimo Gioco (Vecchio)' },
  { value: 'chat-activity-desc', label: 'Chat Attività (Alta)' },
  { value: 'chat-activity-asc', label: 'Chat Attività (Bassa)' },
  { value: 'play-count-desc', label: 'Partite (Alta)' },
  { value: 'play-count-asc', label: 'Partite (Bassa)' },
];

const FILTER_OPTIONS: FilterOption[] = [
  { type: 'has-pdf', value: 'true', label: 'Ha PDF' },
  { type: 'has-active-chat', value: 'true', label: 'Ha Chat Attiva' },
  { type: 'category', value: 'Strategy', label: 'Strategia' },
  { type: 'category', value: 'Family', label: 'Famiglia' },
];

// ============================================================================
// Filter Tag Component
// ============================================================================

interface FilterTagProps {
  filter: { type: string; value: string; label: string };
  onRemove: () => void;
}

function FilterTag({ filter, onRemove }: FilterTagProps) {
  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.9 }}
      animate={{ opacity: 1, scale: 1 }}
      exit={{ opacity: 0, scale: 0.9 }}
      transition={{ duration: 0.15 }}
      className="inline-flex items-center gap-1 px-3 py-1 rounded-full bg-primary/10 text-primary text-sm"
      data-testid={`filter-tag-${filter.type}-${filter.value}`}
    >
      <span>{filter.label}</span>
      <button
        onClick={onRemove}
        className="hover:bg-primary/20 rounded-full p-0.5 transition-colors"
        aria-label={`Rimuovi filtro ${filter.label}`}
      >
        <X className="h-3 w-3" />
      </button>
    </motion.div>
  );
}

// ============================================================================
// Empty State Component
// ============================================================================

function EmptyState() {
  return (
    <div
      className="flex flex-col items-center justify-center py-16 text-center"
      data-testid="collection-grid-empty"
    >
      <div className="h-16 w-16 rounded-full bg-muted/50 flex items-center justify-center mb-4">
        <Sparkles className="h-8 w-8 text-muted-foreground/50" />
      </div>
      <h3 className="text-lg font-semibold mb-1">Nessun gioco trovato</h3>
      <p className="text-sm text-muted-foreground mb-4">
        Prova a modificare i filtri o aggiungi nuovi giochi alla collezione
      </p>
      <Button variant="outline" size="sm">
        Aggiungi Gioco
      </Button>
    </div>
  );
}

// ============================================================================
// Loading Skeleton Component
// ============================================================================

function CollectionGridSkeleton() {
  return (
    <div
      className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4"
      data-testid="collection-grid-skeleton"
    >
      {Array.from({ length: 8 }).map((_, i) => (
        <div
          key={i}
          className="rounded-xl border border-border/60 bg-card/80 backdrop-blur-xl overflow-hidden"
        >
          <Skeleton className="aspect-[4/3] w-full" />
          <div className="p-3 space-y-2">
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-3 w-1/4" />
            <div className="grid grid-cols-2 gap-2">
              <Skeleton className="h-3 w-full" />
              <Skeleton className="h-3 w-full" />
            </div>
            <Skeleton className="h-8 w-full" />
          </div>
        </div>
      ))}
    </div>
  );
}

// ============================================================================
// CollectionGrid Component
// ============================================================================

export function CollectionGrid({
  games,
  sortBy,
  filters,
  onSortChange,
  onFilterChange,
  onGameClick,
  isLoading = false,
  className,
}: CollectionGridProps) {
  const [showFilters, setShowFilters] = useState(false);

  // Active filters count
  const activeFiltersCount = Object.values(filters).filter(
    (v) => v !== undefined && v !== null
  ).length;

  // Handle filter toggle
  const handleFilterToggle = (option: FilterOption) => {
    const newFilters = { ...filters };

    if (option.type === 'has-pdf') {
      newFilters.hasPdf = newFilters.hasPdf ? undefined : true;
    } else if (option.type === 'has-active-chat') {
      newFilters.hasActiveChat = newFilters.hasActiveChat ? undefined : true;
    } else if (option.type === 'category') {
      newFilters.category = newFilters.category === option.value ? undefined : option.value;
    }

    onFilterChange(newFilters);
  };

  // Handle filter removal
  const handleFilterRemove = (type: string, value?: string) => {
    const newFilters = { ...filters };

    if (type === 'has-pdf') {
      newFilters.hasPdf = undefined;
    } else if (type === 'has-active-chat') {
      newFilters.hasActiveChat = undefined;
    } else if (type === 'category' && value) {
      if (newFilters.category === value) {
        newFilters.category = undefined;
      }
    }

    onFilterChange(newFilters);
  };

  // Clear all filters
  const handleClearFilters = () => {
    onFilterChange({});
  };

  // Get active filter tags
  const getActiveFilterTags = (): FilterTagProps['filter'][] => {
    const tags: FilterTagProps['filter'][] = [];

    if (filters.hasPdf) {
      tags.push({ type: 'has-pdf', value: 'true', label: 'Ha PDF' });
    }
    if (filters.hasActiveChat) {
      tags.push({ type: 'has-active-chat', value: 'true', label: 'Ha Chat Attiva' });
    }
    if (filters.category) {
      const option = FILTER_OPTIONS.find(
        (o) => o.type === 'category' && o.value === filters.category
      );
      if (option) {
        tags.push({ type: 'category', value: filters.category, label: option.label });
      }
    }

    return tags;
  };

  const activeFilterTags = getActiveFilterTags();

  if (isLoading) {
    return <CollectionGridSkeleton />;
  }

  return (
    <section className={cn('space-y-4', className)} data-testid="collection-grid-section">
      {/* Controls Header */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
        {/* Sort Control */}
        <div className="flex items-center gap-2 w-full sm:w-auto">
          <SlidersHorizontal className="h-4 w-4 text-muted-foreground shrink-0" />
          <Select value={sortBy} onValueChange={(value: string) => onSortChange(value as SortOption)}>
            <SelectTrigger className="w-full sm:w-[240px]" data-testid="sort-select">
              <SelectValue placeholder="Ordina per..." />
            </SelectTrigger>
            <SelectContent>
              {SORT_OPTIONS.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Filter Toggle */}
        <Button
          variant="outline"
          size="sm"
          onClick={() => setShowFilters(!showFilters)}
          className="w-full sm:w-auto"
          data-testid="filter-toggle"
        >
          <Filter className="h-4 w-4 mr-2" />
          Filtri
          {activeFiltersCount > 0 && (
            <span className="ml-2 px-1.5 py-0.5 rounded-full bg-primary text-primary-foreground text-xs font-medium">
              {activeFiltersCount}
            </span>
          )}
        </Button>
      </div>

      {/* Filter Panel */}
      {showFilters && (
        <motion.div
          initial={{ opacity: 0, height: 0 }}
          animate={{ opacity: 1, height: 'auto' }}
          exit={{ opacity: 0, height: 0 }}
          className="rounded-xl border border-border/60 bg-card/80 backdrop-blur-xl p-4 space-y-3"
          data-testid="filter-panel"
        >
          <div className="flex items-center justify-between">
            <h4 className="text-sm font-semibold">Filtri</h4>
            {activeFiltersCount > 0 && (
              <Button
                variant="ghost"
                size="sm"
                onClick={handleClearFilters}
                data-testid="clear-filters"
              >
                Pulisci Tutto
              </Button>
            )}
          </div>

          <div className="flex flex-wrap gap-2">
            {FILTER_OPTIONS.map((option) => {
              const isActive =
                (option.type === 'has-pdf' && filters.hasPdf) ||
                (option.type === 'has-active-chat' && filters.hasActiveChat) ||
                (option.type === 'category' && filters.category === option.value);

              return (
                <Button
                  key={`${option.type}-${option.value}`}
                  variant={isActive ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => handleFilterToggle(option)}
                  data-testid={`filter-option-${option.type}-${option.value}`}
                >
                  {option.label}
                </Button>
              );
            })}
          </div>
        </motion.div>
      )}

      {/* Active Filter Tags */}
      {activeFilterTags.length > 0 && (
        <div className="flex flex-wrap items-center gap-2" data-testid="active-filters">
          <span className="text-xs text-muted-foreground">Filtri attivi:</span>
          {activeFilterTags.map((filter) => (
            <FilterTag
              key={`${filter.type}-${filter.value}`}
              filter={filter}
              onRemove={() => handleFilterRemove(filter.type, filter.value)}
            />
          ))}
        </div>
      )}

      {/* Games Grid */}
      {games.length === 0 ? (
        <EmptyState />
      ) : (
        <div
          className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4"
          data-testid="games-grid"
        >
          {games.map((game) => (
            <MeepleCard
              key={game.id}
              game={game}
              onPlay={onGameClick}
            />
          ))}
        </div>
      )}
    </section>
  );
}

export default CollectionGrid;
