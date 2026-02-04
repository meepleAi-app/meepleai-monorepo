/**
 * SharedGamesFilters Component - Issue #3534
 *
 * Filter controls for shared games catalog:
 * - Search input (title, BGG ID, publisher)
 * - Status filter dropdown
 * - Sort dropdown
 * - Submitter filter (admins only)
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import { Search, X } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';

export type GameStatusFilter = 'all' | 'Draft' | 'PendingApproval' | 'Published' | 'Archived';
export type GameSortOption = 'modifiedAt:desc' | 'modifiedAt:asc' | 'title:asc' | 'title:desc' | 'createdAt:desc' | 'yearPublished:desc';

export interface SharedGamesFiltersProps {
  search: string;
  status: GameStatusFilter;
  sortBy: GameSortOption;
  submittedBy?: string;
  onSearchChange: (search: string) => void;
  onStatusChange: (status: GameStatusFilter) => void;
  onSortChange: (sort: GameSortOption) => void;
  onSubmitterChange?: (submitterId: string | undefined) => void;
  isAdmin?: boolean;
  debounceMs?: number;
}

export function SharedGamesFilters({
  search,
  status,
  sortBy,
  submittedBy,
  onSearchChange,
  onStatusChange,
  onSortChange,
  onSubmitterChange,
  isAdmin = false,
  debounceMs = 300,
}: SharedGamesFiltersProps) {
  const [localSearch, setLocalSearch] = useState(search);

  // Debounced search
  useEffect(() => {
    const timer = setTimeout(() => {
      if (localSearch !== search) {
        onSearchChange(localSearch);
      }
    }, debounceMs);

    return () => clearTimeout(timer);
  }, [localSearch, search, onSearchChange, debounceMs]);

  // Sync external search changes
  useEffect(() => {
    setLocalSearch(search);
  }, [search]);

  const handleClearSearch = useCallback(() => {
    setLocalSearch('');
    onSearchChange('');
  }, [onSearchChange]);

  const handleClearFilters = useCallback(() => {
    setLocalSearch('');
    onSearchChange('');
    onStatusChange('all');
    onSortChange('modifiedAt:desc');
    if (onSubmitterChange) {
      onSubmitterChange(undefined);
    }
  }, [onSearchChange, onStatusChange, onSortChange, onSubmitterChange]);

  const hasActiveFilters = search !== '' || status !== 'all' || sortBy !== 'modifiedAt:desc' || submittedBy;

  return (
    <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:flex-wrap">
      {/* Search Input */}
      <div className="relative flex-1 min-w-[280px]">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          placeholder="Search by title, BGG ID, publisher..."
          value={localSearch}
          onChange={(e) => setLocalSearch(e.target.value)}
          className="pl-10 pr-10"
        />
        {localSearch && (
          <Button
            variant="ghost"
            size="sm"
            className="absolute right-1 top-1/2 -translate-y-1/2 h-7 w-7 p-0"
            onClick={handleClearSearch}
          >
            <X className="h-4 w-4" />
            <span className="sr-only">Clear search</span>
          </Button>
        )}
      </div>

      {/* Status Filter */}
      <Select value={status} onValueChange={(value) => onStatusChange(value as GameStatusFilter)}>
        <SelectTrigger className="w-[160px]">
          <SelectValue placeholder="All Statuses" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">All Statuses</SelectItem>
          <SelectItem value="Draft">Draft</SelectItem>
          <SelectItem value="PendingApproval">Pending Approval</SelectItem>
          <SelectItem value="Published">Published</SelectItem>
          <SelectItem value="Archived">Archived</SelectItem>
        </SelectContent>
      </Select>

      {/* Sort Dropdown */}
      <Select value={sortBy} onValueChange={(value) => onSortChange(value as GameSortOption)}>
        <SelectTrigger className="w-[180px]">
          <SelectValue placeholder="Sort by" />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="modifiedAt:desc">Recently Updated</SelectItem>
          <SelectItem value="modifiedAt:asc">Oldest Updated</SelectItem>
          <SelectItem value="title:asc">Title A-Z</SelectItem>
          <SelectItem value="title:desc">Title Z-A</SelectItem>
          <SelectItem value="createdAt:desc">Recently Created</SelectItem>
          <SelectItem value="yearPublished:desc">Year (Newest)</SelectItem>
        </SelectContent>
      </Select>

      {/* Submitter Filter (Admin only) */}
      {isAdmin && onSubmitterChange && (
        <Select
          value={submittedBy || 'all'}
          onValueChange={(value) => onSubmitterChange(value === 'all' ? undefined : value)}
        >
          <SelectTrigger className="w-[160px]">
            <SelectValue placeholder="All Submitters" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Submitters</SelectItem>
            {/* TODO: Populate with actual users from API */}
          </SelectContent>
        </Select>
      )}

      {/* Clear Filters Button */}
      {hasActiveFilters && (
        <Button variant="ghost" size="sm" onClick={handleClearFilters}>
          <X className="h-4 w-4 mr-1" />
          Clear Filters
        </Button>
      )}
    </div>
  );
}
