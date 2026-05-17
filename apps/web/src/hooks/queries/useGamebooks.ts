/**
 * useGamebooks — SP6 Phase B hooks (Issue #788; backend wired in #869).
 *
 * Issue #869 status:
 *   - `useGamebooks`  → wired to `GET /api/v1/gamebooks` (real data)
 *   - `useQuotaInfo`  → STILL STUB pending follow-up (`GET /api/v1/users/me/quota`
 *                       not yet exposed). The stub returns canonical fixture
 *                       data so the orchestrator FSM keeps rendering correctly
 *                       until the quota endpoint lands.
 *
 * Used by:
 *   - `apps/web/src/app/(authenticated)/gamebook/_components/GamebookIndexView.tsx`
 *
 * @see docs/superpowers/specs/2026-05-06-sp6-libro-game.md (Gate B)
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { fetchUserGamebooks } from '@/lib/api/gamebooks-list';
import { gamebookIndexFixtures, type GamebookCardData, type QuotaInfo } from '@/lib/gamebook-index';

/**
 * Stable query keys for the gamebook index hooks.
 *
 * Exported so tests can verify the queryKey contract and so a future cache
 * invalidation layer (mutations) can target these queries precisely.
 */
export const gamebookKeys = {
  all: ['gamebooks'] as const,
  myGamebooks: () => [...gamebookKeys.all, 'me'] as const,
  quota: () => [...gamebookKeys.all, 'quota'] as const,
};

/**
 * Hook to fetch the current user's gamebook library.
 *
 * Wired to `GET /api/v1/gamebooks` via `fetchUserGamebooks`. The endpoint
 * returns 200 with `[]` when the user has no gamebooks; 401 when the
 * session is invalid (TanStack Query surfaces the error and the
 * orchestrator renders the `error` FSM cell).
 *
 * @returns UseQueryResult with the readonly array of gamebooks.
 */
export function useGamebooks(): UseQueryResult<readonly GamebookCardData[], Error> {
  return useQuery({
    queryKey: gamebookKeys.myGamebooks(),
    queryFn: ({ signal }) => fetchUserGamebooks(signal),
    staleTime: 60_000,
  });
}

/**
 * Hook to fetch the current user's translation quota.
 *
 * v1 carryover: returns the canonical fixture data via `gamebookIndexFixtures.default`.
 * The backend `GET /api/v1/users/me/quota` endpoint is NOT exposed; integration
 * deferred to a follow-up (issue #869b — split from #869 to keep PR small).
 *
 * @returns UseQueryResult with the QuotaInfo object.
 */
export function useQuotaInfo(): UseQueryResult<QuotaInfo, Error> {
  return useQuery({
    queryKey: gamebookKeys.quota(),
    queryFn: async (): Promise<QuotaInfo> => {
      // v1 carryover stub — return canonical fixture data.
      return gamebookIndexFixtures.default.quota;
    },
    staleTime: 60_000,
  });
}
