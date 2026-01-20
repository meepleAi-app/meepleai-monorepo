/**
 * LibraryFilters Component (Issue #2464)
 *
 * Filtering controls for user library page.
 * Features:
 * - Search by game title
 * - Toggle favorites only filter
 * - Sort dropdown (date added, title, favorites)
 * - Clear all filters button
 */

'use client';

import React, { useState, useEffect } from 'react';

import { Search, X, SortAsc } from 'lucide-react';

import { Switch } from '@/components/ui/forms/switch';
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
  /** Current sort option */
  sortBy?: 'addedAt' | 'title' | 'favorite';
  /** Sort descending */
  sortDescending?: boolean;
  /** Callback when sort changes */
  onSortChange: (sortBy: 'addedAt' | 'title' | 'favorite', descending: boolean) => void;
  /** Callback when filters are cleared */
  onClearFilters: () => void;
  /** Additional className */
  className?: string;
}

export function LibraryFilters({
  searchQuery = '',
  onSearchChange,
  favoritesOnly = false,
  onFavoritesChange,
  sortBy = 'addedAt',
  sortDescending = true,
  onSortChange,
  onClearFilters,
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

  const hasActiveFilters = searchQuery || favoritesOnly || sortBy !== 'addedAt' || !sortDescending;

  return (
    <div className={cn('flex flex-col gap-4 md:flex-row md:items-end', className)}>
      {/* Search Input */}
      <div className="flex-1 space-y-2">
        <Label htmlFor="library-search" className="text-sm font-medium">
          Cerca Giochi
        </Label>
        <div className="relative">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            id="library-search"
            type="search"
            placeholder="Cerca per titolo..."
            value={localSearch}
            onChange={e => setLocalSearch(e.target.value)}
            className="pl-9 pr-9"
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

      {/* Favorites Filter */}
      <div className="flex items-center space-x-2">
        <Switch
          id="favorites-only"
          checked={favoritesOnly}
          onCheckedChange={onFavoritesChange}
          aria-label="Show favorites only"
        />
        <Label
          htmlFor="favorites-only"
          className="text-sm font-medium cursor-pointer select-none"
        >
          Solo Preferiti
        </Label>
      </div>

      {/* Sort Dropdown */}
      <div className="space-y-2">
        <Label htmlFor="library-sort" className="text-sm font-medium">
          Ordina
        </Label>
        <Select
          value={`${sortBy}-${sortDescending ? 'desc' : 'asc'}`}
          onValueChange={handleSortChange}
        >
          <SelectTrigger id="library-sort" className="w-[200px]" aria-label="Sort library">
            <SortAsc className="mr-2 h-4 w-4" />
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="addedAt-desc">Più recenti</SelectItem>
            <SelectItem value="addedAt-asc">Meno recenti</SelectItem>
            <SelectItem value="title-asc">Titolo (A-Z)</SelectItem>
            <SelectItem value="title-desc">Titolo (Z-A)</SelectItem>
            <SelectItem value="favorite-desc">Preferiti prima</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {/* Clear Filters */}
      {hasActiveFilters && (
        <Button variant="outline" size="sm" onClick={onClearFilters} className="md:mb-0">
          <X className="mr-1 h-3 w-3" />
          Pulisci Filtri
        </Button>
      )}
    </div>
  );
}
