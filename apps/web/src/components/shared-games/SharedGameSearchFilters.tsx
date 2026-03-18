/* eslint-disable security/detect-object-injection */
/**
 * SharedGameSearchFilters Component (Issue #2373: Phase 4, Updated Issue #2873)
 *
 * Advanced search filters for SharedGameCatalog game search.
 * Filters: Category, Mechanic, Player count, Playing time, Complexity, Catalog-only toggle.
 * Uses AdvancedFilterPanel with slide-in Sheet for filter selection.
 *
 * @see claudedocs/shared-game-catalog-spec.md (Section: Search Filters)
 * @see Issue #2873: Advanced Filter Panel Component
 */

'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import { SlidersHorizontal, X } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { GameCategory, GameMechanic } from '@/lib/api/schemas/shared-games.schemas';
import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';

import { AdvancedFilterPanel } from './AdvancedFilterPanel';

// ============================================================================
// Types
// ============================================================================

export interface SearchFilters {
  categoryIds: string[];
  mechanicIds: string[];
  minPlayers: number | null;
  maxPlayers: number | null;
  minPlayingTime: number | null;
  maxPlayingTime: number | null;
  minComplexity: number | null;
  maxComplexity: number | null;
  catalogOnly: boolean;
}

export interface SharedGameSearchFiltersProps {
  /** Current filter values */
  filters: SearchFilters;
  /** Called when filters change */
  onFiltersChange: (filters: SearchFilters) => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Default Filters
// ============================================================================

export const DEFAULT_FILTERS: SearchFilters = {
  categoryIds: [],
  mechanicIds: [],
  minPlayers: null,
  maxPlayers: null,
  minPlayingTime: null,
  maxPlayingTime: null,
  minComplexity: null,
  maxComplexity: null,
  catalogOnly: false,
};

// ============================================================================
// Component
// ============================================================================

export function SharedGameSearchFilters({
  filters,
  onFiltersChange,
  className,
}: SharedGameSearchFiltersProps) {
  // Reference data for displaying names
  const [categories, setCategories] = useState<GameCategory[]>([]);
  const [mechanics, setMechanics] = useState<GameMechanic[]>([]);

  // UI state
  const [isPanelOpen, setIsPanelOpen] = useState(false);

  // ============================================================================
  // Load Reference Data (for displaying badge names)
  // ============================================================================

  useEffect(() => {
    const loadReferenceData = async () => {
      try {
        const [categoriesData, mechanicsData] = await Promise.all([
          api.sharedGames.getCategories(),
          api.sharedGames.getMechanics(),
        ]);
        setCategories(categoriesData);
        setMechanics(mechanicsData);
      } catch (error) {
        logger.error('Failed to load filter reference data:', error);
      }
    };

    void loadReferenceData();
  }, []);

  // ============================================================================
  // Active Filters Count
  // ============================================================================

  const activeFiltersCount = useMemo(() => {
    let count = 0;
    if (filters.categoryIds.length > 0) count++;
    if (filters.mechanicIds.length > 0) count++;
    if (filters.minPlayers !== null || filters.maxPlayers !== null) count++;
    if (filters.minPlayingTime !== null || filters.maxPlayingTime !== null) count++;
    if (filters.minComplexity !== null || filters.maxComplexity !== null) count++;
    if (filters.catalogOnly) count++;
    return count;
  }, [filters]);

  // ============================================================================
  // Handlers
  // ============================================================================

  const handleClearFilters = useCallback(() => {
    onFiltersChange(DEFAULT_FILTERS);
  }, [onFiltersChange]);

  const handleRemoveCategory = useCallback(
    (categoryId: string) => {
      onFiltersChange({
        ...filters,
        categoryIds: filters.categoryIds.filter(id => id !== categoryId),
      });
    },
    [filters, onFiltersChange]
  );

  const handleRemoveMechanic = useCallback(
    (mechanicId: string) => {
      onFiltersChange({
        ...filters,
        mechanicIds: filters.mechanicIds.filter(id => id !== mechanicId),
      });
    },
    [filters, onFiltersChange]
  );

  const handleApplyFilters = useCallback(
    (newFilters: SearchFilters) => {
      onFiltersChange(newFilters);
    },
    [onFiltersChange]
  );

  // ============================================================================
  // Get Names for Selected Items
  // ============================================================================

  const selectedCategoryNames = useMemo(() => {
    return filters.categoryIds
      .map(id => categories.find(c => c.id === id)?.name)
      .filter(Boolean) as string[];
  }, [filters.categoryIds, categories]);

  const selectedMechanicNames = useMemo(() => {
    return filters.mechanicIds
      .map(id => mechanics.find(m => m.id === id)?.name)
      .filter(Boolean) as string[];
  }, [filters.mechanicIds, mechanics]);

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <div className={cn('space-y-3', className)}>
      {/* Filter Toggle Button */}
      <div className="flex items-center justify-between">
        <Button
          variant="outline"
          size="sm"
          onClick={() => setIsPanelOpen(true)}
          className={cn(
            'gap-2 transition-colors',
            activeFiltersCount > 0 && 'border-orange-500/50 hover:border-orange-500'
          )}
        >
          <SlidersHorizontal className="h-4 w-4" />
          Filtri Avanzati
          {activeFiltersCount > 0 && (
            <Badge className="ml-1 h-5 min-w-5 px-1.5 rounded-full text-xs bg-orange-500 hover:bg-orange-500 text-white border-0">
              {activeFiltersCount}
            </Badge>
          )}
        </Button>

        {activeFiltersCount > 0 && (
          <Button variant="ghost" size="sm" onClick={handleClearFilters} className="text-xs">
            Cancella tutti
          </Button>
        )}
      </div>

      {/* Active Filters Badges */}
      {activeFiltersCount > 0 && (
        <div className="flex flex-wrap gap-2">
          {selectedCategoryNames.map((name, index) => (
            <Badge
              key={`cat-${filters.categoryIds[index]}`}
              className="gap-1 bg-orange-500/10 text-orange-700 dark:text-orange-400 border-orange-500/30 hover:bg-orange-500/20"
            >
              {name}
              <button
                type="button"
                onClick={() => handleRemoveCategory(filters.categoryIds[index])}
                className="ml-1 hover:text-orange-900 dark:hover:text-orange-300"
                aria-label={`Rimuovi ${name}`}
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          ))}
          {selectedMechanicNames.map((name, index) => (
            <Badge
              key={`mec-${filters.mechanicIds[index]}`}
              variant="outline"
              className="gap-1 border-orange-500/30 text-orange-700 dark:text-orange-400"
            >
              {name}
              <button
                type="button"
                onClick={() => handleRemoveMechanic(filters.mechanicIds[index])}
                className="ml-1 hover:text-orange-900 dark:hover:text-orange-300"
                aria-label={`Rimuovi ${name}`}
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          ))}
          {(filters.minPlayers !== null || filters.maxPlayers !== null) && (
            <Badge className="gap-1 bg-orange-500/10 text-orange-700 dark:text-orange-400 border-orange-500/30">
              {filters.minPlayers ?? '1'}-{filters.maxPlayers ?? '12+'} giocatori
              <button
                type="button"
                onClick={() => onFiltersChange({ ...filters, minPlayers: null, maxPlayers: null })}
                className="ml-1 hover:text-orange-900 dark:hover:text-orange-300"
                aria-label="Rimuovi filtro giocatori"
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          )}
          {(filters.minComplexity !== null || filters.maxComplexity !== null) && (
            <Badge className="gap-1 bg-orange-500/10 text-orange-700 dark:text-orange-400 border-orange-500/30">
              Complessit&agrave;: {filters.minComplexity?.toFixed(1) ?? '1.0'}-
              {filters.maxComplexity?.toFixed(1) ?? '5.0'}
              <button
                type="button"
                onClick={() =>
                  onFiltersChange({ ...filters, minComplexity: null, maxComplexity: null })
                }
                className="ml-1 hover:text-orange-900 dark:hover:text-orange-300"
                aria-label="Rimuovi filtro complessit&agrave;"
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          )}
          {(filters.minPlayingTime !== null || filters.maxPlayingTime !== null) && (
            <Badge className="gap-1 bg-orange-500/10 text-orange-700 dark:text-orange-400 border-orange-500/30">
              {filters.minPlayingTime ?? '5'}-{filters.maxPlayingTime ?? '300+'} min
              <button
                type="button"
                onClick={() =>
                  onFiltersChange({ ...filters, minPlayingTime: null, maxPlayingTime: null })
                }
                className="ml-1 hover:text-orange-900 dark:hover:text-orange-300"
                aria-label="Rimuovi filtro durata"
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          )}
          {filters.catalogOnly && (
            <Badge className="gap-1 bg-orange-500 text-white border-orange-500">
              Solo catalogo
              <button
                type="button"
                onClick={() => onFiltersChange({ ...filters, catalogOnly: false })}
                className="ml-1 hover:text-orange-100"
                aria-label="Rimuovi filtro catalogo"
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          )}
        </div>
      )}

      {/* Advanced Filter Panel (Slide-in Sheet) */}
      <AdvancedFilterPanel
        open={isPanelOpen}
        onOpenChange={setIsPanelOpen}
        filters={filters}
        onApplyFilters={handleApplyFilters}
      />
    </div>
  );
}
