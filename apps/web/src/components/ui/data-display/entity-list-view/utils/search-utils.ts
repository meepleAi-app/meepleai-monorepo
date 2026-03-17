/**
 * Search utilities for EntityListView
 *
 * Provides fuzzy search algorithm with support for nested field access
 * via dot notation (e.g., "game.title", "metadata.rating").
 *
 * @module components/ui/data-display/entity-list-view/utils/search-utils
 */

/**
 * Get nested value from object using dot notation
 *
 * @example
 * ```ts
 * const obj = { game: { title: 'Gloomhaven' } };
 * getNestedValue(obj, 'game.title'); // 'Gloomhaven'
 * ```
 */
function getNestedValue(obj: unknown, path: string): unknown {
  // eslint-disable-next-line security/detect-object-injection
  return path
    .split('.')
    .reduce((acc: unknown, part) => (acc as Record<string, unknown>)?.[part], obj);
}

/**
 * Fuzzy search implementation
 *
 * Case-insensitive partial string matching across multiple fields.
 * Supports nested field access via dot notation.
 *
 * @template T - Type of items to search
 * @param items - Array of items to filter
 * @param query - Search query string
 * @param fields - Fields to search in (supports dot notation for nested fields)
 * @returns Filtered array of items matching the query
 *
 * @example
 * ```ts
 * const games = [
 *   { title: 'Gloomhaven', publisher: 'Cephalofair' },
 *   { title: 'Wingspan', publisher: 'Stonemaier' },
 * ];
 *
 * fuzzySearch(games, 'gloom', ['title', 'publisher']);
 * // Returns: [{ title: 'Gloomhaven', ... }]
 * ```
 */
export function fuzzySearch<T>(items: T[], query: string, fields: string[]): T[] {
  // Empty query returns all items
  if (!query.trim()) {
    return items;
  }

  const lowerQuery = query.toLowerCase();

  return items.filter(item => {
    // Match if ANY field contains the query
    return fields.some(field => {
      const value = getNestedValue(item, field);

      // Only search string values
      if (typeof value === 'string') {
        return value.toLowerCase().includes(lowerQuery);
      }

      // Convert numbers to string for searching
      if (typeof value === 'number') {
        return value.toString().includes(lowerQuery);
      }

      return false;
    });
  });
}
