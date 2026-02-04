/**
 * ActivityFeedFilters - Advanced Filters for ActivityFeed
 * Issue #3322 - Advanced ActivityFeed filters and search
 *
 * Features:
 * - Filter bar with activity type checkboxes
 * - Search input with debounce (300ms)
 * - Active filter badges with counts
 * - Clear all button
 * - Mobile-friendly design
 *
 * @example
 * ```tsx
 * <ActivityFeedFilters
 *   filters={filters}
 *   onFiltersChange={setFilters}
 *   counts={activityCounts}
 * />
 * ```
 */

'use client';

import { useState, useCallback, useEffect, useMemo } from 'react';

import {
  Search,
  X,
  Library,
  Dices,
  MessageSquare,
  Star,
  Trophy,
  Filter,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { cn } from '@/lib/utils';

import type { ActivityEventType } from './ActivityFeed';
import type { LucideIcon } from 'lucide-react';

// ============================================================================
// Types
// ============================================================================

export interface ActivityFilters {
  types: ActivityEventType[];
  search: string;
  dateFrom?: string;
  dateTo?: string;
}

export interface ActivityCounts {
  game_added: number;
  session_completed: number;
  chat_saved: number;
  wishlist_added: number;
  achievement_unlocked: number;
  total: number;
}

export interface ActivityFeedFiltersProps {
  /** Current filter state */
  filters: ActivityFilters;
  /** Callback when filters change */
  onFiltersChange: (filters: ActivityFilters) => void;
  /** Activity counts per type */
  counts?: ActivityCounts;
  /** Loading state */
  isLoading?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Constants
// ============================================================================

const DEBOUNCE_MS = 300;

interface FilterOption {
  type: ActivityEventType;
  label: string;
  icon: LucideIcon;
  color: string;
}

const FILTER_OPTIONS: FilterOption[] = [
  { type: 'game_added', label: 'Giochi', icon: Library, color: 'text-amber-600 dark:text-amber-400' },
  { type: 'session_completed', label: 'Sessioni', icon: Dices, color: 'text-emerald-600 dark:text-emerald-400' },
  { type: 'chat_saved', label: 'Chat', icon: MessageSquare, color: 'text-blue-600 dark:text-blue-400' },
  { type: 'wishlist_added', label: 'Wishlist', icon: Star, color: 'text-yellow-600 dark:text-yellow-400' },
  { type: 'achievement_unlocked', label: 'Achievement', icon: Trophy, color: 'text-purple-600 dark:text-purple-400' },
];

// ============================================================================
// Helper Functions
// ============================================================================

function useDebounce<T>(value: T, delay: number): T {
  const [debouncedValue, setDebouncedValue] = useState<T>(value);

  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedValue(value);
    }, delay);

    return () => {
      clearTimeout(handler);
    };
  }, [value, delay]);

  return debouncedValue;
}

// ============================================================================
// FilterChip Component
// ============================================================================

interface FilterChipProps {
  option: FilterOption;
  isSelected: boolean;
  count?: number;
  onToggle: () => void;
}

function FilterChip({ option, isSelected, count, onToggle }: FilterChipProps) {
  const Icon = option.icon;

  return (
    <button
      type="button"
      onClick={onToggle}
      className={cn(
        'inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-medium transition-all',
        'border focus:outline-none focus:ring-2 focus:ring-offset-1 focus:ring-primary/50',
        isSelected
          ? 'bg-primary/10 border-primary/30 text-foreground'
          : 'bg-muted/30 border-border/50 text-muted-foreground hover:bg-muted/50'
      )}
      data-testid={`filter-chip-${option.type}`}
      aria-pressed={isSelected}
    >
      <Icon className={cn('h-3.5 w-3.5', isSelected ? option.color : 'text-current')} />
      <span>{option.label}</span>
      {count !== undefined && count > 0 && (
        <Badge
          variant={isSelected ? 'default' : 'secondary'}
          className="h-4 px-1.5 text-[10px] rounded-full"
          data-testid={`filter-count-${option.type}`}
        >
          {count}
        </Badge>
      )}
    </button>
  );
}

// ============================================================================
// ActiveFilterBadges Component
// ============================================================================

interface ActiveFilterBadgesProps {
  filters: ActivityFilters;
  counts?: ActivityCounts;
}

function ActiveFilterBadges({ filters, counts }: ActiveFilterBadgesProps) {
  if (filters.types.length === 0 && !filters.search) {
    return null;
  }

  const activeFilterLabels = FILTER_OPTIONS
    .filter(opt => filters.types.includes(opt.type))
    .map(opt => {
      const count = counts?.[opt.type];
      return `${opt.label}${count ? ` (${count})` : ''}`;
    });

  return (
    <div
      className="flex flex-wrap items-center gap-1 text-xs text-muted-foreground"
      data-testid="active-filter-badges"
    >
      <Filter className="h-3 w-3" />
      <span>Attivo:</span>
      {activeFilterLabels.map((label, i) => (
        <span key={label}>
          {label}
          {i < activeFilterLabels.length - 1 && ' •'}
        </span>
      ))}
      {filters.search && (
        <span>
          {activeFilterLabels.length > 0 && ' •'}
          &quot;{filters.search}&quot;
        </span>
      )}
    </div>
  );
}

// ============================================================================
// ActivityFeedFilters Component
// ============================================================================

export function ActivityFeedFilters({
  filters,
  onFiltersChange,
  counts,
  isLoading = false,
  className,
}: ActivityFeedFiltersProps) {
  const [searchInput, setSearchInput] = useState(filters.search);
  const debouncedSearch = useDebounce(searchInput, DEBOUNCE_MS);

  // Sync debounced search with filters
  useEffect(() => {
    if (debouncedSearch !== filters.search) {
      onFiltersChange({ ...filters, search: debouncedSearch });
    }
  }, [debouncedSearch, filters, onFiltersChange]);

  // Handle filter type toggle
  const handleToggleType = useCallback(
    (type: ActivityEventType) => {
      const newTypes = filters.types.includes(type)
        ? filters.types.filter((t) => t !== type)
        : [...filters.types, type];
      onFiltersChange({ ...filters, types: newTypes });
    },
    [filters, onFiltersChange]
  );

  // Handle clear all
  const handleClearAll = useCallback(() => {
    setSearchInput('');
    onFiltersChange({
      types: [],
      search: '',
      dateFrom: undefined,
      dateTo: undefined,
    });
  }, [onFiltersChange]);

  // Check if any filters are active
  const hasActiveFilters = useMemo(
    () => filters.types.length > 0 || filters.search.length > 0,
    [filters]
  );

  return (
    <div
      className={cn('space-y-3', className)}
      data-testid="activity-feed-filters"
    >
      {/* Search Input */}
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <Input
          type="text"
          placeholder="Cerca attività..."
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          className="pl-9 pr-9 h-9 text-sm rounded-full"
          disabled={isLoading}
          data-testid="activity-search-input"
        />
        {searchInput && (
          <button
            type="button"
            onClick={() => setSearchInput('')}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground transition-colors"
            data-testid="clear-search-button"
            aria-label="Cancella ricerca"
          >
            <X className="h-4 w-4" />
          </button>
        )}
      </div>

      {/* Filter Chips */}
      <div className="flex flex-wrap gap-2" data-testid="filter-chips">
        {FILTER_OPTIONS.map((option) => (
          <FilterChip
            key={option.type}
            option={option}
            isSelected={filters.types.includes(option.type)}
            count={counts?.[option.type]}
            onToggle={() => handleToggleType(option.type)}
          />
        ))}

        {/* Clear All Button */}
        {hasActiveFilters && (
          <Button
            variant="ghost"
            size="sm"
            onClick={handleClearAll}
            className="h-7 px-2 text-xs text-muted-foreground hover:text-destructive"
            data-testid="clear-all-filters"
          >
            <X className="h-3 w-3 mr-1" />
            Cancella Tutto
          </Button>
        )}
      </div>

      {/* Active Filter Summary */}
      <ActiveFilterBadges filters={filters} counts={counts} />
    </div>
  );
}

export default ActivityFeedFilters;
