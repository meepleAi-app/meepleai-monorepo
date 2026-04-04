/**
 * useValidatedStorage — React hook for Zod-validated browser storage
 *
 * Combines safeStorage with React state management, providing:
 * - Zod schema validation on every read
 * - SSR-safe access
 * - Cross-tab synchronization via StorageEvent
 * - Function updater pattern (like useState)
 *
 * @module lib/storage/useValidatedStorage
 *
 * Issue #5238: Add validation wrapper for localStorage/sessionStorage reads
 *
 * @example
 * ```tsx
 * import { z } from 'zod';
 * import { useValidatedStorage } from '@/lib/storage/useValidatedStorage';
 *
 * const ThemeSchema = z.enum(['light', 'dark', 'system']);
 *
 * function ThemePicker() {
 *   const [theme, setTheme] = useValidatedStorage('theme', ThemeSchema, 'system');
 *   return <button onClick={() => setTheme('dark')}>{theme}</button>;
 * }
 * ```
 */

import { useCallback, useEffect, useState } from 'react';

import { logger } from '@/lib/logger';

import { safeStorage } from './safeStorage';

import type { z } from 'zod';

type StorageType = 'local' | 'session';

/**
 * React hook for type-safe, Zod-validated browser storage.
 *
 * @param key - Storage key
 * @param schema - Zod schema for validation
 * @param fallback - Default value when key is missing or invalid
 * @param type - 'local' (default) or 'session'
 */
export function useValidatedStorage<T>(
  key: string,
  schema: z.ZodType<T>,
  fallback: T,
  type: StorageType = 'local'
): [T, (value: T | ((prev: T) => T)) => void] {
  const [storedValue, setStoredValue] = useState<T>(() =>
    safeStorage.get(key, schema, fallback, type)
  );

  const setValue = useCallback(
    (value: T | ((prev: T) => T)) => {
      try {
        const next = value instanceof Function ? value(storedValue) : value;
        setStoredValue(next);
        safeStorage.set(key, next, type);

        // Dispatch event for cross-tab and same-tab sync
        if (typeof window !== 'undefined') {
          window.dispatchEvent(
            new StorageEvent('storage', {
              key,
              newValue: JSON.stringify(next),
            })
          );
        }
      } catch (error) {
        if (process.env.NODE_ENV !== 'production') {
          logger.error(`[useValidatedStorage] Failed to set "${key}":`, error);
        }
      }
    },
    [key, storedValue, type]
  );

  // Cross-tab sync
  useEffect(() => {
    if (typeof window === 'undefined') return;

    const handleStorage = (e: StorageEvent) => {
      if (e.key !== key) return;

      if (e.newValue === null) {
        setStoredValue(fallback);
        return;
      }

      try {
        const parsed = JSON.parse(e.newValue);
        const result = schema.safeParse(parsed);
        if (result.success) {
          setStoredValue(result.data);
        }
      } catch {
        // Ignore invalid cross-tab data
      }
    };

    window.addEventListener('storage', handleStorage);
    return () => window.removeEventListener('storage', handleStorage);
  }, [key, schema, fallback]);

  return [storedValue, setValue];
}
