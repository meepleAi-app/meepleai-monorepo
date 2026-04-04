/**
 * useLocalStorage - SSR-safe localStorage hook with cross-tab sync
 *
 * A generic React hook for persisting state in localStorage with full
 * TypeScript support, error handling, and cross-tab synchronization.
 *
 * @module hooks/useLocalStorage
 *
 * Features:
 * - SSR-safe: Returns default value during server-side rendering
 * - JSON serialization: Automatically handles complex objects
 * - Error handling: Graceful fallback on QuotaExceeded or parse errors
 * - Cross-tab sync: Updates across browser tabs via storage events
 * - TypeScript generic: Full type safety for stored values
 *
 * @example
 * ```tsx
 * const [theme, setTheme] = useLocalStorage<'light' | 'dark'>('theme', 'light');
 *
 * // Function updater pattern (like useState)
 * setTheme(prev => prev === 'light' ? 'dark' : 'light');
 * ```
 */

import { useCallback, useEffect, useState } from 'react';

import { logger } from '@/lib/logger';

/**
 * Custom hook for persisting state in localStorage
 *
 * @template T - Type of the stored value
 * @param key - localStorage key (should be unique per use case)
 * @param defaultValue - Initial value if no stored value exists
 * @returns Tuple of [value, setValue] similar to useState
 */
export function useLocalStorage<T>(
  key: string,
  defaultValue: T
): [T, (value: T | ((prev: T) => T)) => void] {
  // Initialize state from localStorage or default
  const [storedValue, setStoredValue] = useState<T>(() => {
    // SSR-safe: window is undefined during server-side rendering
    if (typeof window === 'undefined') {
      return defaultValue;
    }

    try {
      const item = window.localStorage.getItem(key);

      if (item) {
        // Parse and return stored JSON
        return JSON.parse(item) as T;
      } else {
        // No stored value - persist defaultValue to localStorage
        window.localStorage.setItem(key, JSON.stringify(defaultValue));
        return defaultValue;
      }
    } catch (error) {
      // Handle JSON parse errors gracefully
      logger.warn(`Error reading localStorage key "${key}": ${error}`);
      return defaultValue;
    }
  });

  /**
   * Update both state and localStorage
   * Supports function updater pattern like setState
   */
  const setValue = useCallback(
    (value: T | ((prev: T) => T)) => {
      try {
        // Allow value to be a function (for updater pattern)
        const valueToStore = value instanceof Function ? value(storedValue) : value;

        // Update React state
        setStoredValue(valueToStore);

        // Persist to localStorage (client-side only)
        if (typeof window !== 'undefined') {
          window.localStorage.setItem(key, JSON.stringify(valueToStore));

          // Manually dispatch storage event for same-tab updates
          // (storage event only fires for OTHER tabs by default)
          window.dispatchEvent(
            new StorageEvent('storage', {
              key,
              newValue: JSON.stringify(valueToStore),
              storageArea: window.localStorage,
            })
          );
        }
      } catch (error) {
        // Handle QuotaExceededError and other localStorage errors
        if (error instanceof Error && error.name === 'QuotaExceededError') {
          logger.error(`localStorage quota exceeded for key "${key}"`);
        } else {
          logger.error(`Error setting localStorage key "${key}":`, error);
        }
      }
    },
    [key, storedValue]
  );

  /**
   * Sync state across browser tabs
   * Listens to storage events from other tabs
   */
  useEffect(() => {
    // Only run on client-side
    if (typeof window === 'undefined') {
      return;
    }

    const handleStorageChange = (e: StorageEvent) => {
      // Only react to changes for this specific key
      if (e.key === key && e.newValue !== null) {
        try {
          const newValue = JSON.parse(e.newValue) as T;
          setStoredValue(newValue);
        } catch (error) {
          logger.warn(`Error parsing storage event for key "${key}": ${error}`);
        }
      }

      // Handle key deletion (newValue === null)
      if (e.key === key && e.newValue === null) {
        setStoredValue(defaultValue);
      }
    };

    window.addEventListener('storage', handleStorageChange);

    return () => {
      window.removeEventListener('storage', handleStorageChange);
    };
  }, [key, defaultValue]);

  return [storedValue, setValue];
}
