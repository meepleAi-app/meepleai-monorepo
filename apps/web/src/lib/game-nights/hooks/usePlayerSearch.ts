/**
 * usePlayerSearch — debounced player autocomplete hook for the SP7 wizard
 * Step 3. Issue #950 W2 Foundation, spec §6.
 *
 * Wraps `api.auth.searchUsers` with a 250ms debounce + React Query caching.
 * The hook returns the same shape as the BE so consumers (Step 3 autocomplete
 * dropdown) can render directly without an additional adapter.
 *
 * Disable the underlying query by passing `enabled: false` or an empty query.
 * The BE refuses queries shorter than 2 chars anyway (returns empty array),
 * so we mirror that gate client-side to avoid wasting HTTP calls.
 */

import { useEffect, useState } from 'react';

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { UserSearchResult } from '@/lib/api/schemas/auth.schemas';

/**
 * Internal: react-query key namespace for player search results.
 */
export const playerSearchKeys = {
  all: ['game-nights', 'wizard', 'player-search'] as const,
  query: (q: string, limit: number) => [...playerSearchKeys.all, { q, limit }] as const,
};

export interface UsePlayerSearchOptions {
  query: string;
  limit?: number;
  debounceMs?: number;
  enabled?: boolean;
}

/**
 * Client-side minimum query length. Matches the BE
 * `SearchUsersQueryHandler` minLength=2 gate.
 */
export const PLAYER_SEARCH_MIN_QUERY_LENGTH = 2;

export const PLAYER_SEARCH_DEFAULT_DEBOUNCE_MS = 250;

export const PLAYER_SEARCH_DEFAULT_LIMIT = 20;

/**
 * Hard ceiling honored by `GET /api/v1/users/search` (PR #1294 W1-PR2
 * adds server-side `Math.Clamp(limit, 1, 50)`). Mirroring the cap
 * client-side keeps the React Query cache key stable and prevents the
 * UI from silently displaying fewer results than the user requested.
 */
export const PLAYER_SEARCH_MAX_LIMIT = 50;

function useDebouncedValue<T>(value: T, ms: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const handle = setTimeout(() => setDebounced(value), ms);
    return () => clearTimeout(handle);
  }, [value, ms]);

  return debounced;
}

export function usePlayerSearch({
  query,
  limit = PLAYER_SEARCH_DEFAULT_LIMIT,
  debounceMs = PLAYER_SEARCH_DEFAULT_DEBOUNCE_MS,
  enabled = true,
}: UsePlayerSearchOptions): UseQueryResult<UserSearchResult[], Error> & {
  debouncedQuery: string;
} {
  // PR #1296 review-fix: clamp limit client-side so cache keys stay stable
  // and the UI doesn't silently receive fewer results than the caller asked
  // for. Mirrors the BE clamp + matches the parallel pattern in
  // `useRegularsForUser`.
  const safeLimit = Math.min(Math.max(limit, 1), PLAYER_SEARCH_MAX_LIMIT);

  const trimmed = query.trim();
  const debouncedQuery = useDebouncedValue(trimmed, debounceMs);
  const shouldFetch = enabled && debouncedQuery.length >= PLAYER_SEARCH_MIN_QUERY_LENGTH;

  const result = useQuery({
    queryKey: playerSearchKeys.query(debouncedQuery, safeLimit),
    queryFn: () => api.auth.searchUsers(debouncedQuery, safeLimit),
    enabled: shouldFetch,
    // 30s stale: typing the same prefix multiple times within 30s reuses the
    // cached payload without re-hitting the BE (spec §6 cache hint).
    staleTime: 30_000,
  });

  return Object.assign(result, { debouncedQuery });
}
