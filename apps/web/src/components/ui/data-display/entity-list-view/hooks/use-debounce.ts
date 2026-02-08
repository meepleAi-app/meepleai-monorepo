/**
 * useDebounce - Debounce hook for delayed value updates
 *
 * Delays updating the returned value until the input value has stopped
 * changing for the specified delay period. Useful for search inputs,
 * API calls, and other operations that should not trigger on every keystroke.
 *
 * @module components/ui/data-display/entity-list-view/hooks/use-debounce
 *
 * @example
 * ```tsx
 * const [searchQuery, setSearchQuery] = useState('');
 * const debouncedQuery = useDebounce(searchQuery, 300);
 *
 * // debouncedQuery only updates 300ms after user stops typing
 * useEffect(() => {
 *   performSearch(debouncedQuery);
 * }, [debouncedQuery]);
 * ```
 */

import { useEffect, useState } from 'react';

/**
 * Debounce a value with a specified delay
 *
 * @template T - Type of the value to debounce
 * @param value - The value to debounce
 * @param delay - Delay in milliseconds (default: 300ms for search)
 * @returns Debounced value that updates after delay period
 */
export function useDebounce<T>(value: T, delay: number = 300): T {
  const [debouncedValue, setDebouncedValue] = useState<T>(value);

  useEffect(() => {
    // Set up timeout to update debounced value after delay
    const handler = setTimeout(() => {
      setDebouncedValue(value);
    }, delay);

    // Clean up timeout if value changes before delay expires
    return () => clearTimeout(handler);
  }, [value, delay]);

  return debouncedValue;
}
