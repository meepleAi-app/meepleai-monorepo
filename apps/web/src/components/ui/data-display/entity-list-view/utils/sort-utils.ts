/**
 * Sort utilities for EntityListView
 *
 * Provides common sort comparator functions and helpers for creating
 * custom comparators with nested field access.
 *
 * @module components/ui/data-display/entity-list-view/utils/sort-utils
 */

/**
 * Get nested value from object using dot notation or keyof
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
 * Common sort comparator functions
 */
export const sortComparators = {
  /**
   * Alphabetical sort (case-insensitive)
   */
  alphabetical: (a: string, b: string) => a.localeCompare(b, undefined, { sensitivity: 'base' }),

  /**
   * Numeric sort (ascending)
   */
  numeric: (a: number, b: number) => a - b,

  /**
   * Date sort (ascending)
   */
  date: (a: Date | string, b: Date | string) => new Date(a).getTime() - new Date(b).getTime(),

  /**
   * Rating sort (descending - highest first)
   */
  rating: (a: number, b: number) => b - a,
};

/**
 * Create a comparator function for a specific field
 *
 * @template T - Type of items to sort
 * @param field - Field to sort by (supports dot notation for nested fields)
 * @param type - Type of comparison to use
 * @returns Comparator function for Array.sort()
 *
 * @example
 * ```ts
 * const games = [
 *   { title: 'Wingspan', rating: 8.1 },
 *   { title: 'Gloomhaven', rating: 8.8 },
 * ];
 *
 * games.sort(createComparator('rating', 'rating'));
 * // Sorted by rating descending: [Gloomhaven, Wingspan]
 * ```
 */
export function createComparator<T>(
  field: keyof T | string,
  type: keyof typeof sortComparators
): (a: T, b: T) => number {
  return (a, b) => {
    const valueA = getNestedValue(a, field);
    const valueB = getNestedValue(b, field);

    // Handle null/undefined values (push to end)
    if (valueA === null || valueA === undefined) {
      if (valueB === null || valueB === undefined) return 0;
      return 1;
    }
    if (valueB === null || valueB === undefined) return -1;

    // Apply comparator with type assertion
    // eslint-disable-next-line security/detect-object-injection
    return (sortComparators[type] as (a: unknown, b: unknown) => number)(valueA, valueB);
  };
}
