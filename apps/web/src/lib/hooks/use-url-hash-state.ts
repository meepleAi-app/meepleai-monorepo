/**
 * useUrlHashState - Generic URL hash state hook with multi-key sync
 *
 * Syncs a typed state object to the URL hash fragment (`#k1=v1&k2=v2,v3`),
 * preserving state on reload, supporting back/forward navigation via
 * `hashchange`, and remaining SSR-safe (returns defaults during SSR).
 *
 * Designed for filter UIs where state should be shareable via URL but should
 * NOT trigger Next.js full-route refetches (hash changes don't notify the
 * router). Updates use `history.replaceState` to avoid scroll/rerender.
 *
 * @module lib/hooks/use-url-hash-state
 *
 * @example
 * ```tsx
 * const [state, update, reset] = useUrlHashState(
 *   {
 *     q: { decode: (raw) => raw, encode: (v) => v },
 *     chips: {
 *       decode: (raw) => raw.split(',').filter(Boolean),
 *       encode: (v) => v.join(','),
 *     },
 *   },
 *   { q: '', chips: [] as string[] },
 * );
 *
 * // state.q, state.chips reflect current hash
 * update({ q: 'catan' }); // hash → #q=catan
 * update({ chips: ['tk', 'top'] }); // hash → #q=catan&chips=tk,top
 * reset(); // hash → #
 * ```
 */

import { useCallback, useEffect, useState } from 'react';

/**
 * Codec for a single hash key.
 *
 * - `decode` receives the raw URL-decoded string (empty string if absent)
 *   and returns the typed value.
 * - `encode` receives the typed value and returns the raw string to be
 *   URL-encoded. Return empty string to omit the key from the hash.
 */
export interface HashCodec<T> {
  decode: (raw: string) => T;
  encode: (value: T) => string;
}

/**
 * Schema mapping each state key to its codec.
 */
export type HashSchema<T> = {
  [K in keyof T]: HashCodec<T[K]>;
};

/**
 * Parse a hash string (with or without leading `#`) into a record of
 * URL-decoded raw strings. Empty hash → `{}`.
 */
function parseHash(hash: string): Record<string, string> {
  const trimmed = hash.startsWith('#') ? hash.slice(1) : hash;
  if (!trimmed) {
    return {};
  }

  const result: Record<string, string> = {};
  for (const pair of trimmed.split('&')) {
    if (!pair) {
      continue;
    }
    const eqIdx = pair.indexOf('=');
    const key = eqIdx === -1 ? pair : pair.slice(0, eqIdx);
    const rawValue = eqIdx === -1 ? '' : pair.slice(eqIdx + 1);
    if (!key) {
      continue;
    }
    try {
      result[decodeURIComponent(key)] = decodeURIComponent(rawValue);
    } catch {
      // Malformed URI components → skip silently rather than throw.
      result[key] = rawValue;
    }
  }
  return result;
}

/**
 * Serialize a record of raw strings into a hash fragment (no leading `#`).
 * Keys with empty-string values are omitted.
 */
function serializeHash(values: Record<string, string>): string {
  const parts: string[] = [];
  for (const [key, value] of Object.entries(values)) {
    if (value === '') {
      continue;
    }
    parts.push(`${encodeURIComponent(key)}=${encodeURIComponent(value)}`);
  }
  return parts.join('&');
}

/**
 * Decode a record of raw hash strings into a typed state, falling back to
 * `defaults[key]` when a key is absent.
 */
function decodeState<T extends object>(
  raw: Record<string, string>,
  schema: HashSchema<T>,
  defaults: T
): T {
  const result = { ...defaults };
  for (const key of Object.keys(schema) as Array<keyof T>) {
    if (key in raw) {
      const codec = schema[key];
      try {
        result[key] = codec.decode(raw[key as string]);
      } catch {
        // Decode error → keep default for that key.
        result[key] = defaults[key];
      }
    }
  }
  return result;
}

/**
 * Encode a typed state into a record of raw hash strings, omitting any key
 * whose codec returns empty string.
 */
function encodeState<T extends object>(state: T, schema: HashSchema<T>): Record<string, string> {
  const result: Record<string, string> = {};
  for (const key of Object.keys(schema) as Array<keyof T>) {
    const codec = schema[key];
    const encoded = codec.encode(state[key]);
    if (encoded !== '') {
      result[key as string] = encoded;
    }
  }
  return result;
}

/**
 * Sync state to the URL hash fragment. Returns `[state, update, reset]`.
 *
 * - **SSR-safe**: returns `defaults` until the first browser-side effect.
 * - **Multi-key**: full hash is rewritten on every update (atomic patch).
 * - **No scroll/rerender**: uses `history.replaceState` rather than
 *   `location.hash =`, which avoids triggering scroll-into-view on `<a id>`
 *   anchors and preserves the Next.js router state.
 * - **Back/forward**: `hashchange` listener re-decodes when the user
 *   navigates history.
 *
 * @param schema - Codec per key. Schema reference is captured once on mount.
 * @param defaults - Default value per key, used when the key is missing
 *   from the hash or when decoding fails.
 */
export function useUrlHashState<T extends object>(
  schema: HashSchema<T>,
  defaults: T
): [T, (patch: Partial<T>) => void, () => void] {
  const [state, setState] = useState<T>(defaults);

  // Hydrate from hash on mount + listen for hashchange (back/forward).
  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    const sync = () => {
      const raw = parseHash(window.location.hash);
      setState(decodeState(raw, schema, defaults));
    };

    sync();
    window.addEventListener('hashchange', sync);
    return () => {
      window.removeEventListener('hashchange', sync);
    };
    // schema/defaults are intentionally captured-once; consumers should
    // pass stable references (module-level objects).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const writeHash = useCallback(
    (next: T) => {
      if (typeof window === 'undefined') {
        return;
      }
      const serialized = serializeHash(encodeState(next, schema));
      const url = new URL(window.location.href);
      url.hash = serialized ? `#${serialized}` : '';
      // replaceState avoids creating history entries for every keystroke
      // and does not trigger scroll-into-view on hash anchors.
      window.history.replaceState(window.history.state, '', url.toString());
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    []
  );

  const update = useCallback(
    (patch: Partial<T>) => {
      setState(prev => {
        const next = { ...prev, ...patch };
        writeHash(next);
        return next;
      });
    },
    [writeHash]
  );

  const reset = useCallback(() => {
    setState(defaults);
    writeHash(defaults);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [writeHash]);

  return [state, update, reset];
}
