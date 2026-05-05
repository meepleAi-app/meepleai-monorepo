/**
 * useUrlHashState — bidirectional sync between component state and URL hash.
 *
 * Wave A.3b (Issue #596). Generic, reusable for A.4/A.5 (shared-games detail tab
 * persistence, future filter UIs). Hash format: `#key1=value1&key2=value2`
 * (URLSearchParams compatible — multiple instances with different keys coexist).
 *
 * Contract:
 *  - Initial render (server + client first paint): ALWAYS returns `defaultValue`.
 *    Hash read happens in `useEffect` after mount → no hydration mismatch.
 *  - `hashchange` event subscribe: keeps multi-instance + back/forward nav in sync.
 *  - `setHash(value)` writes via `history.replaceState` (no scroll jump,
 *    no history pollution).
 *  - When `serialize(value)` returns `null`, the key is removed from the hash
 *    (avoid `#sort=rating` clutter when value equals default).
 *  - Caller is responsible for debouncing if the value changes rapidly
 *    (e.g. search input). Hook itself does NOT debounce.
 */

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

export interface UseUrlHashStateOptions<T> {
  /**
   * Convert a value to its hash representation. Return `null` to remove the
   * key from the hash entirely (typically when value equals defaultValue).
   * Default: `String(value)` if value is non-empty, `null` otherwise.
   */
  serialize?: (value: T) => string | null;

  /**
   * Parse a raw string from the hash back to a typed value.
   * Default: identity cast `raw as T` (only safe for `T = string`).
   */
  deserialize?: (raw: string) => T;
}

function defaultSerialize<T>(value: T): string | null {
  if (value === null || value === undefined) return null;
  if (typeof value === 'string') return value.length > 0 ? value : null;
  if (typeof value === 'number') return String(value);
  if (typeof value === 'boolean') return value ? 'true' : null;
  // Arrays / objects: caller must provide custom serializer.
  return String(value);
}

function defaultDeserialize<T>(raw: string): T {
  return raw as unknown as T;
}

function readHashParams(): URLSearchParams {
  if (typeof window === 'undefined') return new URLSearchParams();
  // window.location.hash starts with '#'; strip it before parsing.
  const raw = window.location.hash.startsWith('#')
    ? window.location.hash.slice(1)
    : window.location.hash;
  return new URLSearchParams(raw);
}

function writeHashParams(params: URLSearchParams): void {
  if (typeof window === 'undefined') return;
  const url = new URL(window.location.href);
  const serialized = params.toString();
  url.hash = serialized.length > 0 ? serialized : '';
  window.history.replaceState(null, '', url.toString());
}

export function useUrlHashState<T>(
  key: string,
  defaultValue: T,
  options?: UseUrlHashStateOptions<T>
): [T, (next: T) => void] {
  const serialize = options?.serialize ?? defaultSerialize;
  const deserialize = options?.deserialize ?? defaultDeserialize;

  // SSR + first-paint client render: always return defaultValue (no hydration mismatch).
  const [value, setValueState] = useState<T>(defaultValue);

  // Stable refs so the hashchange handler picks up latest serializer/deserializer
  // without re-subscribing the listener on every render.
  const deserializeRef = useRef(deserialize);
  const defaultRef = useRef(defaultValue);
  deserializeRef.current = deserialize;
  defaultRef.current = defaultValue;

  // Hydrate from hash + subscribe to hashchange (multi-instance + back/forward).
  useEffect(() => {
    if (typeof window === 'undefined') return;

    const syncFromHash = () => {
      const params = readHashParams();
      const raw = params.get(key);
      if (raw === null) {
        setValueState(defaultRef.current);
      } else {
        try {
          setValueState(deserializeRef.current(raw));
        } catch {
          setValueState(defaultRef.current);
        }
      }
    };

    syncFromHash();
    window.addEventListener('hashchange', syncFromHash);
    return () => {
      window.removeEventListener('hashchange', syncFromHash);
    };
  }, [key]);

  const setValue = useCallback(
    (next: T) => {
      setValueState(next);
      if (typeof window === 'undefined') return;
      const params = readHashParams();
      const serialized = serialize(next);
      if (serialized === null || serialized === '') {
        params.delete(key);
      } else {
        params.set(key, serialized);
      }
      writeHashParams(params);
    },
    [key, serialize]
  );

  return [value, setValue];
}
