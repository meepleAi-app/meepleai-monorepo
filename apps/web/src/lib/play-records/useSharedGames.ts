/**
 * useSharedGames — Task 0.3 (Issue #1488 / Epic #1475 Phase D).
 *
 * TanStack Query batch hook fetching multiple `SharedGameDetail`s concurrently
 * so callers can resolve covers/titles for a list of play records (each
 * `PlayRecordSummary.gameId` is fetched once and joined client-side).
 *
 * Behaviour:
 *   - Deduplicates input (null entries filtered, duplicates collapsed).
 *   - Fans out `Promise.all(getSharedGameDetail)` — the backend currently
 *     exposes only a single `GET /shared-games/{id}` endpoint (no batch).
 *     A `GET /shared-games?ids=` batch endpoint is tracked as a BE follow-up
 *     issue per plan §Non-goals. MVP accepts up to N parallel requests
 *     (browsers throttle to 6 concurrent per host so practically capped).
 *   - Stable `queryKey` via sorting the deduped id list — request order
 *     permutations hit the same cache entry.
 *   - `staleTime: 1h` — game catalog mutations are rare; consumers calling
 *     this hook (index/detail/stats) accept slightly stale covers/titles.
 *   - `enabled: uniqueIds.length > 0` so an empty / all-null array doesn't
 *     fire a no-op fetch.
 *   - Returns `data: Map<gameId, SharedGameDetail>` for O(1) lookup at the
 *     call-site (vs. linear search through an array).
 *
 * **Failure mode** (MVP): individual fetch failures reject the whole batch;
 * the query enters `isError` state. Partial-success (`Promise.allSettled`) is
 * deferred until we observe real-world transient failures hurting UX.
 *
 * @see plan `docs/superpowers/plans/2026-05-29-play-records-reskin.md` Task 0 Step 3
 * @see plan §Non-goals — BE batch endpoint follow-up
 */
import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { getSharedGameDetail, type SharedGameDetail } from '@/lib/api/shared-games';

const STALE_TIME_MS = 60 * 60 * 1000; // 1h
const PLAY_RECORDS_SHARED_GAMES_KEY = 'play-records' as const;
const BATCH_SUBKEY = 'shared-games-batch' as const;

/**
 * Fetch multiple SharedGame details by id; null entries are filtered out and
 * the input is deduplicated before hitting the network.
 *
 * @param gameIds — heterogeneous array of `gameId` values (typically pulled
 *   from `PlayRecordSummary.gameId`, which is nullable for freeform records).
 */
export function useSharedGames(
  gameIds: ReadonlyArray<string | null>
): UseQueryResult<Map<string, SharedGameDetail>, Error> {
  const uniqueIds = [...new Set(gameIds.filter((id): id is string => id !== null && id !== ''))];
  // Sort to keep queryKey stable across input permutations (cache reuse).
  const sortedIds = [...uniqueIds].sort();

  return useQuery({
    queryKey: [PLAY_RECORDS_SHARED_GAMES_KEY, BATCH_SUBKEY, sortedIds],
    queryFn: async () => {
      const details = await Promise.all(sortedIds.map(id => getSharedGameDetail(id)));
      const map = new Map<string, SharedGameDetail>();
      for (const d of details) {
        map.set(d.id, d);
      }
      return map;
    },
    staleTime: STALE_TIME_MS,
    enabled: sortedIds.length > 0,
  });
}
