/**
 * BGG Search Hook (Wave 3 Phase 0 — Issue #805)
 *
 * Lightweight TanStack Query hook for the SP6 wizard step 1 BGG tab. Sits next
 * to the heavier {@link useSearchBggGames} hook (Issue #4164/#4167) which adds
 * retry-with-toast behaviour for the admin-side `Add from BGG` flow.
 *
 * Differences vs. {@link useSearchBggGames}:
 *   - 24 h `staleTime` (BGG catalog data is effectively static at the resolution
 *     SP6 needs; matches the `?cache=24h` semantics of the public route).
 *   - Min 3-char gate (the SP6 search bar already debounces input; 2-char
 *     search produces too-noisy BGG XML hits to be useful in the wizard).
 *   - No retry/toast side-effects (the orchestrator surfaces errors via the
 *     `step1-no-results` ActionCards instead of toasts).
 *
 * Backend gate (Issue #805 / Wave 3 Phase 0):
 *   `/api/v1/bgg/search` was admin-only until this phase landed; it now requires
 *   any authenticated user. The 60 req/hour/user `BggSearch` rate-limit policy
 *   still protects the BoardGameGeek external quota.
 *
 * @see useSearchBggGames for retry-heavy variant
 */

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { BggSearchResponse } from '@/lib/api/schemas/games.schemas';

export const BGG_SEARCH_MIN_QUERY_LENGTH = 3;
export const BGG_SEARCH_STALE_TIME_MS = 24 * 60 * 60 * 1000; // 24 hours

export interface UseBggSearchOptions {
  /** Search query string. Must be ≥ {@link BGG_SEARCH_MIN_QUERY_LENGTH} chars to fire. */
  readonly query: string;
  /** Override the auto-enable rule. Set `false` to defer the request. */
  readonly enabled?: boolean;
}

/**
 * Search BoardGameGeek games (SP6 wizard variant).
 *
 * @example
 * const { data, isLoading } = useBggSearch({ query: 'wingspan' });
 * // data?.results — BggSearchResult[]
 */
export function useBggSearch({ query, enabled }: UseBggSearchOptions) {
  const trimmed = query.trim();
  const meetsLength = trimmed.length >= BGG_SEARCH_MIN_QUERY_LENGTH;
  const shouldEnable = enabled !== undefined ? enabled && meetsLength : meetsLength;

  return useQuery<BggSearchResponse, Error>({
    queryKey: ['bgg', 'search', trimmed],
    queryFn: () => api.bgg.search(trimmed, false, 1, 20),
    enabled: shouldEnable,
    staleTime: BGG_SEARCH_STALE_TIME_MS,
    gcTime: BGG_SEARCH_STALE_TIME_MS,
  });
}
