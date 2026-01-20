/* eslint-disable security/detect-object-injection */
/**
 * SharedGameSearchFilters Component (Issue #2373: Phase 4)
 *
 * Advanced search filters for SharedGameCatalog game search.
 * Filters: Category, Mechanic, Player count, Playing time, Catalog-only toggle.
 *
 * @see claudedocs/shared-game-catalog-spec.md (Section: Search Filters)
 */

'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import { ChevronDown, Filter, X } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Switch } from '@/components/ui/forms/switch';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { api } from '@/lib/api';
import type { GameCategory, GameMechanic } from '@/lib/api/schemas/shared-games.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface SearchFilters {
  categoryIds: string[];
  mechanicIds: string[];
  minPlayers: number | null;
  maxPlayers: number | null;
  maxPlayingTime: number | null;
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
  maxPlayingTime: null,
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
  // Reference data
  const [categories, setCategories] = useState<GameCategory[]>([]);
  const [mechanics, setMechanics] = useState<GameMechanic[]>([]);
  const [loading, setLoading] = useState(true);

  // UI state
  const [isExpanded, setIsExpanded] = useState(false);
  const [categoryDropdownOpen, setCategoryDropdownOpen] = useState(false);
  const [mechanicDropdownOpen, setMechanicDropdownOpen] = useState(false);

  // ============================================================================
  // Load Reference Data
  // ============================================================================

  useEffect(() => {
    const loadReferenceData = async () => {
      setLoading(true);
      try {
        const [categoriesData, mechanicsData] = await Promise.all([
          api.sharedGames.getCategories(),
          api.sharedGames.getMechanics(),
        ]);
        setCategories(categoriesData);
        setMechanics(mechanicsData);
      } catch (error) {
        console.error('Failed to load filter reference data:', error);
      } finally {
        setLoading(false);
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
    if (filters.maxPlayingTime !== null) count++;
    if (filters.catalogOnly) count++;
    return count;
  }, [filters]);

  // ============================================================================
  // Handlers
  // ============================================================================

  const handleCategoryToggle = useCallback(
    (categoryId: string, checked: boolean) => {
      const newCategoryIds = checked
        ? [...filters.categoryIds, categoryId]
        : filters.categoryIds.filter(id => id !== categoryId);
      onFiltersChange({ ...filters, categoryIds: newCategoryIds });
    },
    [filters, onFiltersChange]
  );

  const handleMechanicToggle = useCallback(
    (mechanicId: string, checked: boolean) => {
      const newMechanicIds = checked
        ? [...filters.mechanicIds, mechanicId]
        : filters.mechanicIds.filter(id => id !== mechanicId);
      onFiltersChange({ ...filters, mechanicIds: newMechanicIds });
    },
    [filters, onFiltersChange]
  );

  const handleMinPlayersChange = useCallback(
    (value: string) => {
      const num = value ? parseInt(value, 10) : null;
      onFiltersChange({ ...filters, minPlayers: num && !isNaN(num) ? num : null });
    },
    [filters, onFiltersChange]
  );

  const handleMaxPlayersChange = useCallback(
    (value: string) => {
      const num = value ? parseInt(value, 10) : null;
      onFiltersChange({ ...filters, maxPlayers: num && !isNaN(num) ? num : null });
    },
    [filters, onFiltersChange]
  );

  const handleMaxPlayingTimeChange = useCallback(
    (value: string) => {
      const num = value ? parseInt(value, 10) : null;
      onFiltersChange({ ...filters, maxPlayingTime: num && !isNaN(num) ? num : null });
    },
    [filters, onFiltersChange]
  );

  const handleCatalogOnlyChange = useCallback(
    (checked: boolean) => {
      onFiltersChange({ ...filters, catalogOnly: checked });
    },
    [filters, onFiltersChange]
  );

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
          onClick={() => setIsExpanded(!isExpanded)}
          className="gap-2"
        >
          <Filter className="h-4 w-4" />
          Filtri
          {activeFiltersCount > 0 && (
            <Badge variant="secondary" className="ml-1 h-5 w-5 rounded-full p-0 text-xs">
              {activeFiltersCount}
            </Badge>
          )}
          <ChevronDown className={cn('h-4 w-4 transition-transform', isExpanded && 'rotate-180')} />
        </Button>

        {activeFiltersCount > 0 && (
          <Button variant="ghost" size="sm" onClick={handleClearFilters} className="text-xs">
            Cancella filtri
          </Button>
        )}
      </div>

      {/* Active Filters Badges */}
      {activeFiltersCount > 0 && !isExpanded && (
        <div className="flex flex-wrap gap-2">
          {selectedCategoryNames.map((name, index) => (
            <Badge key={`cat-${index}`} variant="secondary" className="gap-1">
              {name}
              <button
                type="button"
                onClick={() => handleRemoveCategory(filters.categoryIds[index])}
                className="ml-1 hover:text-destructive"
                aria-label={`Rimuovi ${name}`}
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          ))}
          {selectedMechanicNames.map((name, index) => (
            <Badge key={`mec-${index}`} variant="outline" className="gap-1">
              {name}
              <button
                type="button"
                onClick={() => handleRemoveMechanic(filters.mechanicIds[index])}
                className="ml-1 hover:text-destructive"
                aria-label={`Rimuovi ${name}`}
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          ))}
          {(filters.minPlayers !== null || filters.maxPlayers !== null) && (
            <Badge variant="secondary" className="gap-1">
              {filters.minPlayers ?? '?'}-{filters.maxPlayers ?? '?'} giocatori
              <button
                type="button"
                onClick={() => onFiltersChange({ ...filters, minPlayers: null, maxPlayers: null })}
                className="ml-1 hover:text-destructive"
                aria-label="Rimuovi filtro giocatori"
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          )}
          {filters.maxPlayingTime !== null && (
            <Badge variant="secondary" className="gap-1">
              Max {filters.maxPlayingTime} min
              <button
                type="button"
                onClick={() => onFiltersChange({ ...filters, maxPlayingTime: null })}
                className="ml-1 hover:text-destructive"
                aria-label="Rimuovi filtro tempo"
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          )}
          {filters.catalogOnly && (
            <Badge variant="default" className="gap-1">
              Solo catalogo
              <button
                type="button"
                onClick={() => onFiltersChange({ ...filters, catalogOnly: false })}
                className="ml-1 hover:text-destructive-foreground"
                aria-label="Rimuovi filtro catalogo"
              >
                <X className="h-3 w-3" />
              </button>
            </Badge>
          )}
        </div>
      )}

      {/* Expanded Filters Panel */}
      {isExpanded && (
        <div className="rounded-lg border bg-card p-4 space-y-4">
          {loading ? (
            <div className="text-sm text-muted-foreground">Caricamento filtri...</div>
          ) : (
            <>
              {/* Categories Multi-Select */}
              <div className="space-y-2">
                <Label className="text-sm font-medium">Categorie</Label>
                <div className="relative">
                  <Button
                    variant="outline"
                    className="w-full justify-between"
                    onClick={() => setCategoryDropdownOpen(!categoryDropdownOpen)}
                    type="button"
                  >
                    <span className="truncate text-left">
                      {filters.categoryIds.length > 0
                        ? `${filters.categoryIds.length} selezionate`
                        : 'Seleziona categorie...'}
                    </span>
                    <ChevronDown
                      className={cn(
                        'h-4 w-4 shrink-0 transition-transform',
                        categoryDropdownOpen && 'rotate-180'
                      )}
                    />
                  </Button>
                  {categoryDropdownOpen && (
                    <div className="absolute z-50 mt-1 w-full rounded-md border bg-popover p-2 shadow-lg max-h-60 overflow-auto">
                      {categories.map(category => (
                        <label
                          key={category.id}
                          className="flex items-center gap-2 py-1.5 px-2 rounded hover:bg-muted cursor-pointer"
                        >
                          <Checkbox
                            checked={filters.categoryIds.includes(category.id)}
                            onCheckedChange={checked =>
                              handleCategoryToggle(category.id, checked as boolean)
                            }
                          />
                          <span className="text-sm">{category.name}</span>
                        </label>
                      ))}
                      {categories.length === 0 && (
                        <div className="text-sm text-muted-foreground p-2">
                          Nessuna categoria disponibile
                        </div>
                      )}
                    </div>
                  )}
                </div>
              </div>

              {/* Mechanics Multi-Select */}
              <div className="space-y-2">
                <Label className="text-sm font-medium">Meccaniche</Label>
                <div className="relative">
                  <Button
                    variant="outline"
                    className="w-full justify-between"
                    onClick={() => setMechanicDropdownOpen(!mechanicDropdownOpen)}
                    type="button"
                  >
                    <span className="truncate text-left">
                      {filters.mechanicIds.length > 0
                        ? `${filters.mechanicIds.length} selezionate`
                        : 'Seleziona meccaniche...'}
                    </span>
                    <ChevronDown
                      className={cn(
                        'h-4 w-4 shrink-0 transition-transform',
                        mechanicDropdownOpen && 'rotate-180'
                      )}
                    />
                  </Button>
                  {mechanicDropdownOpen && (
                    <div className="absolute z-50 mt-1 w-full rounded-md border bg-popover p-2 shadow-lg max-h-60 overflow-auto">
                      {mechanics.map(mechanic => (
                        <label
                          key={mechanic.id}
                          className="flex items-center gap-2 py-1.5 px-2 rounded hover:bg-muted cursor-pointer"
                        >
                          <Checkbox
                            checked={filters.mechanicIds.includes(mechanic.id)}
                            onCheckedChange={checked =>
                              handleMechanicToggle(mechanic.id, checked as boolean)
                            }
                          />
                          <span className="text-sm">{mechanic.name}</span>
                        </label>
                      ))}
                      {mechanics.length === 0 && (
                        <div className="text-sm text-muted-foreground p-2">
                          Nessuna meccanica disponibile
                        </div>
                      )}
                    </div>
                  )}
                </div>
              </div>

              {/* Player Count Range */}
              <div className="space-y-2">
                <Label className="text-sm font-medium">Numero giocatori</Label>
                <div className="flex items-center gap-2">
                  <Input
                    type="number"
                    placeholder="Min"
                    min={1}
                    max={99}
                    value={filters.minPlayers ?? ''}
                    onChange={e => handleMinPlayersChange(e.target.value)}
                    className="w-20"
                    aria-label="Minimo giocatori"
                  />
                  <span className="text-muted-foreground">-</span>
                  <Input
                    type="number"
                    placeholder="Max"
                    min={1}
                    max={99}
                    value={filters.maxPlayers ?? ''}
                    onChange={e => handleMaxPlayersChange(e.target.value)}
                    className="w-20"
                    aria-label="Massimo giocatori"
                  />
                </div>
              </div>

              {/* Max Playing Time */}
              <div className="space-y-2">
                <Label className="text-sm font-medium">Tempo massimo di gioco</Label>
                <div className="flex items-center gap-2">
                  <Input
                    type="number"
                    placeholder="Max minuti"
                    min={1}
                    max={9999}
                    value={filters.maxPlayingTime ?? ''}
                    onChange={e => handleMaxPlayingTimeChange(e.target.value)}
                    className="w-32"
                    aria-label="Tempo massimo in minuti"
                  />
                  <span className="text-sm text-muted-foreground">minuti</span>
                </div>
              </div>

              {/* Catalog Only Toggle */}
              <div className="flex items-center justify-between pt-2 border-t">
                <div className="space-y-0.5">
                  <Label htmlFor="catalog-only" className="text-sm font-medium">
                    Solo dal catalogo
                  </Label>
                  <p className="text-xs text-muted-foreground">
                    Mostra solo giochi nel nostro database
                  </p>
                </div>
                <Switch
                  id="catalog-only"
                  checked={filters.catalogOnly}
                  onCheckedChange={handleCatalogOnlyChange}
                />
              </div>
            </>
          )}
        </div>
      )}
    </div>
  );
}
