/**
 * useGamebooks — SP6 Phase B v1 carryover STUB hooks (Issue #788).
 *
 * Schema reality v1 carryover (Gate B):
 *   The backend `GET /api/v1/gamebooks` endpoint and `GET /api/v1/users/me/quota`
 *   (or equivalent) DO NOT exist on main-dev. Verified via:
 *     grep -rn "MapGet.*gamebook\|MapGet.*GameBook" apps/api/src/Api/Routing/
 *     grep -rn "MapGet.*quota\|MapGet.*Quota" apps/api/src/Api/Routing/
 *
 *   To unblock the orchestrator (Phase B Task 3) without coupling to a
 *   non-existent endpoint, this module exposes 2 hooks that resolve via the
 *   canonical visual-test fixture data. Both queries report `isPending=false`
 *   immediately so the FSM never gets stuck in the `loading` cell during real
 *   data flows (the orchestrator simulates the loading cell via the
 *   `?fixture=loading` URL override gated by `STATE_OVERRIDE_ENABLED`).
 *
 *   Real backend integration is deferred to a follow-up issue post-Phase B
 *   that will:
 *     1. Expose `GET /api/v1/gamebooks` returning the canonical
 *        `GamebookCardData[]` shape (or an adapter from the actual DTO).
 *     2. Expose `GET /api/v1/users/me/quota` returning `QuotaInfo`.
 *     3. Replace the fixture-data `queryFn` with `api.gamebooks.list()` and
 *        `api.users.quota()` (or equivalent client paths).
 *
 * Used by:
 *   - `apps/web/src/app/(authenticated)/gamebook/_components/GamebookIndexView.tsx`
 *
 * @see docs/superpowers/specs/2026-05-06-sp6-libro-game.md (Gate B)
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

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
 * v1 carryover: returns the canonical fixture data via `gamebookIndexFixtures.default`.
 * The backend `GET /api/v1/gamebooks` endpoint is NOT exposed; integration
 * deferred to a follow-up issue.
 *
 * @returns UseQueryResult with the readonly array of gamebooks.
 */
export function useGamebooks(): UseQueryResult<readonly GamebookCardData[], Error> {
  return useQuery({
    queryKey: gamebookKeys.myGamebooks(),
    queryFn: async (): Promise<readonly GamebookCardData[]> => {
      // v1 carryover stub — return canonical fixture data.
      return gamebookIndexFixtures.default.gamebooks;
    },
    staleTime: 60_000,
  });
}

/**
 * Hook to fetch the current user's translation quota.
 *
 * v1 carryover: returns the canonical fixture data via `gamebookIndexFixtures.default`.
 * The backend `GET /api/v1/users/me/quota` endpoint is NOT exposed; integration
 * deferred to a follow-up issue.
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
