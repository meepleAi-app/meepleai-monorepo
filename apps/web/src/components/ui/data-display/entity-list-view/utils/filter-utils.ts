/**
 * Filter utilities for EntityListView
 *
 * Provides filter application logic for all 4 filter types:
 * - SelectFilter (single/multi-select)
 * - CheckboxFilter (boolean)
 * - RangeFilter (numeric min-max)
 * - DateRangeFilter (date start-end)
 *
 * @module components/ui/data-display/entity-list-view/utils/filter-utils
 */

import type { FilterConfig, FilterState, FilterValue } from '../entity-list-view.types';

/**
 * Get nested value from object using dot notation
 */
function getNestedValue<T>(obj: T, field: keyof T | string): unknown {
  if (typeof field === 'string' && field.includes('.')) {
    return field
      .split('.')
      .reduce((acc: unknown, part) => (acc as Record<string, unknown>)?.[part], obj as unknown);
  }
  return obj[field as keyof T];
}

/**
 * Apply all active filters to items array
 *
 * @template T - Type of items to filter
 * @param items - Array of items
 * @param filterState - Active filter values (filterId -> value)
 * @param filterConfig - Filter configuration array
 * @returns Filtered array of items
 */
export function applyFilters<T>(
  items: T[],
  filterState: FilterState,
  filterConfig: FilterConfig<T>[]
): T[] {
  return items.filter(item => {
    // Item must pass ALL active filters
    return filterConfig.every(filter => {
      const filterValue = filterState[filter.id];

      // Skip inactive/empty filters
      if (filterValue === undefined || filterValue === null || filterValue === '') {
        return true;
      }

      const itemValue = getNestedValue(item, filter.field);

      switch (filter.type) {
        case 'select': {
          if (filter.multiple) {
            // Multi-select: filterValue is string[]
            const selectedValues = filterValue as string[];
            return selectedValues.length === 0 || selectedValues.includes(String(itemValue));
          }
          // Single-select: filterValue is string
          return String(filterValue) === String(itemValue);
        }

        case 'checkbox': {
          // Checkbox: only filter if checked (true)
          const isChecked = filterValue as boolean;
          return !isChecked || itemValue === true;
        }

        case 'range': {
          // Range: filterValue is { min: number, max: number }
          const range = filterValue as { min: number; max: number };
          const numValue = Number(itemValue);

          if (isNaN(numValue)) return false;

          return numValue >= range.min && numValue <= range.max;
        }

        case 'date-range': {
          // DateRange: filterValue is { start: Date, end: Date }
          const dateRange = filterValue as { start: Date; end: Date };
          const itemDate = new Date(itemValue as string | number | Date);

          if (isNaN(itemDate.getTime())) return false;

          const startTime = new Date(dateRange.start).getTime();
          const endTime = new Date(dateRange.end).getTime();
          const itemTime = itemDate.getTime();

          return itemTime >= startTime && itemTime <= endTime;
        }

        default:
          return true;
      }
    });
  });
}

/**
 * Count active filters (non-empty values)
 */
export function countActiveFilters(filterState: FilterState): number {
  return Object.values(filterState).filter(value => {
    if (value === undefined || value === null || value === '') return false;
    if (Array.isArray(value) && value.length === 0) return false;
    if (typeof value === 'boolean' && value === false) return false;
    return true;
  }).length;
}

/**
 * Get filter display value for chip
 */
export function getFilterDisplayValue<T>(
  filterId: string,
  filterValue: FilterValue,
  filterConfig: FilterConfig<T>[]
): string {
  const filter = filterConfig.find(f => f.id === filterId);
  if (!filter) return String(filterValue);

  switch (filter.type) {
    case 'select': {
      if (filter.multiple && Array.isArray(filterValue)) {
        const labels = filterValue
          .map(val => filter.options.find(opt => opt.value === val)?.label || val)
          .join(', ');
        return labels || String(filterValue);
      }
      const option = filter.options.find(opt => opt.value === filterValue);
      return option?.label || String(filterValue);
    }

    case 'checkbox':
      return filterValue ? 'Yes' : 'No';

    case 'range': {
      const range = filterValue as { min: number; max: number };
      const unit = filter.unit || '';
      return `${range.min}${unit} - ${range.max}${unit}`;
    }

    case 'date-range': {
      const dateRange = filterValue as { start: Date; end: Date };
      const start = new Date(dateRange.start).toLocaleDateString();
      const end = new Date(dateRange.end).toLocaleDateString();
      return `${start} - ${end}`;
    }

    default:
      return String(filterValue);
  }
}
