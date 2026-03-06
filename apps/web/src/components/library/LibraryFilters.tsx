/**
 * LibraryFilters Component (Issue #2464, #2866)
 *
 * Filtering controls for user library page.
 * Features:
 * - Search by game title
 * - Filter chips: All, Favorites, Nuovo, In Prestito, Wishlist
 * - Sort dropdown (date added, title, favorites)
 * - Clear all filters button
 */

'use client';

import React, { useState, useEffect } from 'react';

import { Search, X, SortAsc, Heart, Sparkles, Share2, Star, Package } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import type { GameStateType } from '@/lib/api/schemas/library.schemas';
import { cn } from '@/lib/utils';

export interface LibraryFiltersProps {
  /** Current search query */
  searchQuery?: string;
  /** Callback when search changes */
  onSearchChange: (query: string) => void;
  /** Favorites only filter */
  favoritesOnly?: boolean;
  /** Callback when favorites toggle changes */
  onFavoritesChange: (enabled: boolean) => void;
  /** Current state filter */
  stateFilter?: GameStateType[];
  /** Callback when state filter changes */
  onStateFilterChange: (states: GameStateType[]) => void;
  /** Current sort option */
  sortBy?: 'addedAt' | 'title' | 'favorite';
  /** Sort descending */
  sortDescending?: boolean;
  /** Callback when sort changes */
  onSortChange: (sortBy: 'addedAt' | 'title' | 'favorite', descending: boolean) => void;
  /** Callback when filters are cleared */
  onClearFilters: () => void;
  /** State counts for displaying badges */
  stateCounts?: {
    total: number;
    favorites: number;
    nuovo: number;
    inPrestito: number;
    wishlist: number;
    owned: number;
  };
  /** Additional className */
  className?: string;
}

type FilterChip = {
  id: 'all' | 'favorites' | GameStateType;
  label: string;
  icon: React.ReactNode;
  color: string;
  activeColor: string;
};

const filterChips: FilterChip[] = [
  {
    id: 'all',
    label: 'Tutti',
    icon: null,
    color: 'bg-muted text-muted-foreground hover:bg-muted/80',
    activeColor: 'bg-primary text-primary-foreground',
  },
  {
    id: 'favorites',
    label: 'Preferiti',
    icon: <Heart className="h-3 w-3" />,
    color: 'bg-muted text-muted-foreground hover:bg-muted/80',
    activeColor: 'bg-purple-600 text-white',
  },
  {
    id: 'Nuovo',
    label: 'Nuovo',
    icon: <Sparkles className="h-3 w-3" />,
    color: 'bg-muted text-muted-foreground hover:bg-muted/80',
    activeColor: 'bg-green-600 text-white',
  },
  {
    id: 'InPrestito',
    label: 'In Prestito',
    icon: <Share2 className="h-3 w-3" />,
    color: 'bg-muted text-muted-foreground hover:bg-muted/80',
    activeColor: 'bg-red-600 text-white',
  },
  {
    id: 'Wishlist',
    label: 'Wishlist',
    icon: <Star className="h-3 w-3" />,
    color: 'bg-muted text-muted-foreground hover:bg-muted/80',
    activeColor: 'bg-yellow-600 text-white',
  },
  {
    id: 'Owned',
    label: 'Posseduto',
    icon: <Package className="h-3 w-3" />,
    color: 'bg-muted text-muted-foreground hover:bg-muted/80',
    activeColor: 'bg-blue-600 text-white',
  },
];

export function LibraryFilters({
  searchQuery = '',
  onSearchChange,
  favoritesOnly = false,
  onFavoritesChange,
  stateFilter = [],
  onStateFilterChange,
  sortBy = 'addedAt',
  sortDescending = true,
  onSortChange,
  onClearFilters,
  stateCounts,
  className,
}: LibraryFiltersProps) {
  const [localSearch, setLocalSearch] = useState(searchQuery);

  // Debounce search input (300ms)
  useEffect(() => {
    const timer = setTimeout(() => {
      if (localSearch !== searchQuery) {
        onSearchChange(localSearch);
      }
    }, 300);

    return () => clearTimeout(timer);
  }, [localSearch, searchQuery, onSearchChange]);

  // Sync external search with local state
  useEffect(() => {
    setLocalSearch(searchQuery);
  }, [searchQuery]);

  const handleSearchClear = () => {
    setLocalSearch('');
    onSearchChange('');
  };

  const handleSortChange = (value: string) => {
    const [field, order] = value.split('-') as ['addedAt' | 'title' | 'favorite', 'asc' | 'desc'];
    onSortChange(field, order === 'desc');
  };

  const handleFilterChipClick = (chipId: FilterChip['id']) => {
    if (chipId === 'all') {
      // Clear all filters
      onFavoritesChange(false);
      onStateFilterChange([]);
    } else if (chipId === 'favorites') {
      // Toggle favorites
      onFavoritesChange(!favoritesOnly);
      if (!favoritesOnly) {
        onStateFilterChange([]); // Clear state filters when selecting favorites
      }
    } else {
      // Toggle state filter
      const state = chipId as GameStateType;
      if (stateFilter.includes(state)) {
        onStateFilterChange(stateFilter.filter(s => s !== state));
      } else {
        onStateFilterChange([...stateFilter, state]);
        onFavoritesChange(false); // Clear favorites when selecting state
      }
    }
  };

  const isChipActive = (chipId: FilterChip['id']): boolean => {
    if (chipId === 'all') {
      return !favoritesOnly && stateFilter.length === 0;
    }
    if (chipId === 'favorites') {
      return favoritesOnly;
    }
    return stateFilter.includes(chipId as GameStateType);
  };

  const getChipCount = (chipId: FilterChip['id']): number | undefined => {
    if (!stateCounts) return undefined;
    switch (chipId) {
      case 'all':
        return stateCounts.total;
      case 'favorites':
        return stateCounts.favorites;
      case 'Nuovo':
        return stateCounts.nuovo;
      case 'InPrestito':
        return stateCounts.inPrestito;
      case 'Wishlist':
        return stateCounts.wishlist;
      case 'Owned':
        return stateCounts.owned;
      default:
        return undefined;
    }
  };

  const hasActiveFilters =
    searchQuery ||
    favoritesOnly ||
    stateFilter.length > 0 ||
    sortBy !== 'addedAt' ||
    !sortDescending;

  return (
    <div className={cn('space-y-3 sm:space-y-4', className)}>
      {/* Search and Sort Row — single row on mobile */}
      <div className="flex items-end gap-2">
        {/* Search Input */}
        <div className="flex-1 min-w-0">
          <Label
            htmlFor="library-search"
            className="sr-only sm:not-sr-only text-sm font-medium sm:mb-2 sm:block"
          >
            Cerca Giochi
          </Label>
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              id="library-search"
              type="search"
              placeholder="Cerca..."
              value={localSearch}
              onChange={e => setLocalSearch(e.target.value)}
              className="pl-9 pr-9 h-9"
              aria-label="Search library"
            />
            {localSearch && (
              <Button
                variant="ghost"
                size="icon"
                className="absolute right-1 top-1/2 h-7 w-7 -translate-y-1/2"
                onClick={handleSearchClear}
                aria-label="Clear search"
              >
                <X className="h-4 w-4" />
              </Button>
            )}
          </div>
        </div>

        {/* Sort Dropdown */}
        <div className="flex-shrink-0">
          <Label
            htmlFor="library-sort"
            className="sr-only sm:not-sr-only text-sm font-medium sm:mb-2 sm:block"
          >
            Ordina
          </Label>
          <Select
            value={`${sortBy}-${sortDescending ? 'desc' : 'asc'}`}
            onValueChange={handleSortChange}
          >
            <SelectTrigger
              id="library-sort"
              className="w-[120px] sm:w-[200px] h-9"
              aria-label="Sort library"
            >
              <SortAsc className="mr-1.5 h-4 w-4 flex-shrink-0" />
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="addedAt-desc">Più recenti</SelectItem>
              <SelectItem value="addedAt-asc">Meno recenti</SelectItem>
              <SelectItem value="title-asc">Titolo (A-Z)</SelectItem>
              <SelectItem value="title-desc">Titolo (Z-A)</SelectItem>
              <SelectItem value="favorite-desc">Preferiti prima</SelectItem>
              {/* TODO: Add "Most Played" sort option when backend supports playCount sorting
                  Requires: API schema update to include 'playCount' in sortBy options
                  Refs: Issue #2866 acceptance criteria */}
            </SelectContent>
          </Select>
        </div>

        {/* Clear Filters */}
        {hasActiveFilters && (
          <Button
            variant="outline"
            size="sm"
            onClick={onClearFilters}
            className="h-9 w-9 p-0 flex-shrink-0 sm:w-auto sm:px-3"
          >
            <X className="h-4 w-4 sm:mr-1" />
            <span className="hidden sm:inline">Pulisci</span>
          </Button>
        )}
      </div>

      {/* Filter Chips Row — horizontal scroll on mobile */}
      <div className="flex gap-2 overflow-x-auto pb-1 -mx-4 px-4 sm:mx-0 sm:px-0 sm:flex-wrap sm:overflow-x-visible sm:pb-0 scrollbar-none">
        {filterChips.map(chip => {
          const isActive = isChipActive(chip.id);
          const count = getChipCount(chip.id);

          return (
            <Badge
              key={chip.id}
              variant="outline"
              className={cn(
                'cursor-pointer px-2.5 py-1 sm:px-3 sm:py-1.5 text-xs sm:text-sm font-medium transition-all flex-shrink-0',
                'hover:scale-105',
                isActive ? chip.activeColor : chip.color
              )}
              onClick={() => handleFilterChipClick(chip.id)}
              role="button"
              aria-pressed={isActive}
              data-testid={`filter-chip-${chip.id.toLowerCase()}`}
            >
              {chip.icon && <span className="mr-1">{chip.icon}</span>}
              {chip.label}
              {count !== undefined && (
                <span
                  className={cn(
                    'ml-1.5 rounded-full px-1.5 text-xs',
                    isActive ? 'bg-white/20' : 'bg-muted-foreground/20'
                  )}
                >
                  {count}
                </span>
              )}
            </Badge>
          );
        })}
      </div>
    </div>
  );
}
