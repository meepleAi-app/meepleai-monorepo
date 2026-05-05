/**
 * useFaqHashQuery — read/write FAQ search query in URL hash with debounce.
 *
 * Wave A.1 (Issue #583). Pattern from spec §3.5:
 *  - Initial state parses `window.location.hash` matching /#q=(.+)/.
 *  - On `hashchange`, refresh state from new hash (back/forward navigation).
 *  - On setQuery(), update local state immediately, then debounce write to
 *    history.replaceState 250ms later. Empty query strips the hash.
 *  - SSR-safe: window guards in every browser-only branch. Initial server
 *    state is empty string; client hydration parses hash via useEffect.
 */

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

const HASH_QUERY_RE = /^#q=(.+)$/;
const DEBOUNCE_MS = 250;

function readHashQuery(): string {
  if (typeof window === 'undefined') return '';
  const match = window.location.hash.match(HASH_QUERY_RE);
  if (!match) return '';
  try {
    return decodeURIComponent(match[1]);
  } catch {
    return '';
  }
}

function writeHashQuery(value: string): void {
  if (typeof window === 'undefined') return;
  const trimmed = value.trim();
  const url = new URL(window.location.href);
  url.hash = trimmed.length > 0 ? `q=${encodeURIComponent(trimmed)}` : '';
  window.history.replaceState(null, '', url.toString());
}

export interface UseFaqHashQueryReturn {
  query: string;
  setQuery: (next: string) => void;
}

export function useFaqHashQuery(): UseFaqHashQueryReturn {
  // Server-render with empty string; client effect hydrates from hash.
  const [query, setQueryState] = useState<string>('');
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Hydrate from hash + subscribe to hashchange (back/forward nav).
  useEffect(() => {
    if (typeof window === 'undefined') return;
    setQueryState(readHashQuery());
    const handler = () => {
      setQueryState(readHashQuery());
    };
    window.addEventListener('hashchange', handler);
    return () => {
      window.removeEventListener('hashchange', handler);
    };
  }, []);

  // Cleanup pending debounce on unmount.
  useEffect(() => {
    return () => {
      if (debounceRef.current !== null) {
        clearTimeout(debounceRef.current);
        debounceRef.current = null;
      }
    };
  }, []);

  const setQuery = useCallback((next: string) => {
    setQueryState(next);
    if (debounceRef.current !== null) {
      clearTimeout(debounceRef.current);
    }
    debounceRef.current = setTimeout(() => {
      writeHashQuery(next);
      debounceRef.current = null;
    }, DEBOUNCE_MS);
  }, []);

  return { query, setQuery };
}
