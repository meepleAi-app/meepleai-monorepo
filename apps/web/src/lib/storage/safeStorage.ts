/**
 * safeStorage — Type-safe, Zod-validated localStorage/sessionStorage wrapper
 *
 * Provides validated reads from browser storage with Zod schemas,
 * SSR-safe guards, and consistent error handling.
 *
 * @module lib/storage/safeStorage
 *
 * Issue #5238: Add validation wrapper for localStorage/sessionStorage reads
 *
 * @example
 * ```ts
 * import { z } from 'zod';
 * import { safeStorage } from '@/lib/storage/safeStorage';
 *
 * const ThemeSchema = z.enum(['light', 'dark', 'system']);
 * const theme = safeStorage.get('theme', ThemeSchema, 'system');
 *
 * safeStorage.set('theme', 'dark');
 * safeStorage.remove('theme');
 * ```
 */

import type { z } from 'zod';

type StorageType = 'local' | 'session';

function getStorage(type: StorageType): Storage | null {
  if (typeof window === 'undefined') return null;
  return type === 'local' ? window.localStorage : window.sessionStorage;
}

/**
 * Read a value from storage, validating it against a Zod schema.
 * Returns the fallback if the key is missing, unparseable, or fails validation.
 */
function get<T>(
  key: string,
  schema: z.ZodType<T>,
  fallback: T,
  type: StorageType = 'local'
): T {
  const storage = getStorage(type);
  if (!storage) return fallback;

  try {
    const raw = storage.getItem(key);
    if (raw === null) return fallback;

    const parsed = JSON.parse(raw);
    const result = schema.safeParse(parsed);

    if (result.success) {
      return result.data;
    }

    // Validation failed — log and return fallback
    if (process.env.NODE_ENV !== 'production') {
      console.warn(
        `[safeStorage] Validation failed for key "${key}":`,
        result.error.issues ?? result.error
      );
    }
    return fallback;
  } catch {
    // JSON parse error or unexpected failure
    if (process.env.NODE_ENV !== 'production') {
      console.warn(`[safeStorage] Failed to read key "${key}" from ${type}Storage`);
    }
    return fallback;
  }
}

/**
 * Read a raw string value from storage (no JSON parsing, no schema validation).
 * Useful for values stored as plain strings (not JSON-encoded).
 */
function getRaw(key: string, fallback: string = '', type: StorageType = 'local'): string {
  const storage = getStorage(type);
  if (!storage) return fallback;

  try {
    return storage.getItem(key) ?? fallback;
  } catch {
    return fallback;
  }
}

/**
 * Write a JSON-serializable value to storage.
 * Returns true on success, false on failure (e.g. QuotaExceeded).
 */
function set<T>(key: string, value: T, type: StorageType = 'local'): boolean {
  const storage = getStorage(type);
  if (!storage) return false;

  try {
    storage.setItem(key, JSON.stringify(value));
    return true;
  } catch (error) {
    if (process.env.NODE_ENV !== 'production') {
      console.error(`[safeStorage] Failed to write key "${key}":`, error);
    }
    return false;
  }
}

/**
 * Write a raw string value to storage (no JSON serialization).
 */
function setRaw(key: string, value: string, type: StorageType = 'local'): boolean {
  const storage = getStorage(type);
  if (!storage) return false;

  try {
    storage.setItem(key, value);
    return true;
  } catch (error) {
    if (process.env.NODE_ENV !== 'production') {
      console.error(`[safeStorage] Failed to write key "${key}":`, error);
    }
    return false;
  }
}

/**
 * Remove a key from storage.
 */
function remove(key: string, type: StorageType = 'local'): void {
  const storage = getStorage(type);
  if (!storage) return;

  try {
    storage.removeItem(key);
  } catch {
    // Ignore removal errors
  }
}

/**
 * Check if a key exists in storage.
 */
function has(key: string, type: StorageType = 'local'): boolean {
  const storage = getStorage(type);
  if (!storage) return false;

  try {
    return storage.getItem(key) !== null;
  } catch {
    return false;
  }
}

export const safeStorage = {
  get,
  getRaw,
  set,
  setRaw,
  remove,
  has,
} as const;
