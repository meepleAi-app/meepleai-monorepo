/**
 * FilterPanel - Filter controls for EntityListView
 *
 * Collapsible panel with active filter chips and filter controls.
 * Supports 4 filter types: Select, Checkbox, Range, DateRange.
 *
 * @module components/ui/data-display/entity-list-view/components/filter-panel
 */

'use client';

import React from 'react';

import { Filter } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Collapsible, CollapsibleContent, CollapsibleTrigger } from '@/components/ui/collapsible';
import { cn } from '@/lib/utils';

import { FilterChip } from './filter-chip';
import { CheckboxFilter } from './filters/checkbox-filter';
import { DateRangeFilter } from './filters/date-range-filter';
import { RangeFilter } from './filters/range-filter';
import { SelectFilter } from './filters/select-filter';
import { getFilterDisplayValue } from '../utils/filter-utils';

import type { FilterConfig, FilterState } from '../entity-list-view.types';

export interface FilterPanelProps<T> {
  /** Filter configuration */
  filters: FilterConfig<T>[];
  /** Current filter state */
  filterState: FilterState;
  /** Callback when filter state changes */
  onChange: (state: FilterState) => void;
  /** Callback to clear all filters */
  onClear: () => void;
  /** Count of active filters */
  activeCount: number;
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
}

/**
 * FilterPanel component with collapsible controls
 */
export function FilterPanel<T>({
  filters,
  filterState,
  onChange,
  onClear,
  activeCount,
  className,
  'data-testid': testId,
}: FilterPanelProps<T>) {
  const [isOpen, setIsOpen] = React.useState(false);

  if (filters.length === 0) return null;

  return (
    <Collapsible
      open={isOpen}
      onOpenChange={setIsOpen}
      className={cn('space-y-3', className)}
      data-testid={testId || 'filter-panel'}
    >
      {/* Trigger Button */}
      <div className="flex items-center gap-2">
        <CollapsibleTrigger asChild>
          <Button variant="outline" size="sm" className="gap-2">
            <Filter className="w-4 h-4" />
            <span>Filters</span>
            {activeCount > 0 && (
              <span className="ml-1 px-1.5 py-0.5 rounded-full bg-primary text-primary-foreground text-xs">
                {activeCount}
              </span>
            )}
          </Button>
        </CollapsibleTrigger>

        {activeCount > 0 && (
          <Button
            variant="ghost"
            size="sm"
            onClick={onClear}
            className="text-xs text-muted-foreground"
          >
            Clear all
          </Button>
        )}
      </div>

      {/* Active Filter Chips */}
      {activeCount > 0 && (
        <div className="flex flex-wrap gap-2">
          {filters.map(filter => {
            const value = filterState[filter.id];
            if (!value) return null;

            const displayValue = getFilterDisplayValue(filter.id, value, filters);

            return (
              <FilterChip
                key={filter.id}
                label={filter.label}
                value={displayValue}
                onRemove={() => {
                  const newState = { ...filterState };
                  delete newState[filter.id];
                  onChange(newState);
                }}
              />
            );
          })}
        </div>
      )}

      {/* Filter Controls */}
      <CollapsibleContent className="space-y-4 pt-2">
        {filters.map(filter => {
          const value = filterState[filter.id];

          switch (filter.type) {
            case 'select':
              return (
                <SelectFilter
                  key={filter.id}
                  filter={filter}
                  value={value as string | string[] | undefined}
                  onChange={val => onChange({ ...filterState, [filter.id]: val })}
                />
              );

            case 'checkbox':
              return (
                <CheckboxFilter
                  key={filter.id}
                  filter={filter}
                  value={value as boolean | undefined}
                  onChange={val => onChange({ ...filterState, [filter.id]: val })}
                />
              );

            case 'range':
              return (
                <RangeFilter
                  key={filter.id}
                  filter={filter}
                  value={value as { min: number; max: number } | undefined}
                  onChange={val => onChange({ ...filterState, [filter.id]: val })}
                />
              );

            case 'date-range':
              return (
                <DateRangeFilter
                  key={filter.id}
                  filter={filter}
                  value={value as { start: Date; end: Date } | undefined}
                  onChange={val => onChange({ ...filterState, [filter.id]: val })}
                />
              );

            default:
              return null;
          }
        })}
      </CollapsibleContent>
    </Collapsible>
  );
}
