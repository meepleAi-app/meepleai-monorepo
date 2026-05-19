/**
 * useGameNightConflictCheck — Step 1 wizard conflict-detection hook.
 *
 * Issue #950 W2 Foundation. Spec §6 + §7b.3.
 *
 * Wraps `api.gameNights.checkConflict` with React Query caching and an
 * `enabled` gate that suppresses the call while the user is still typing
 * an incomplete date.
 *
 * The wizard orchestrator dispatches `recordConflict` into the reducer when
 * the query resolves, so the conflict-warning step (state-02) renders on
 * the next render pass.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { ConflictCheckDto } from '@/lib/api/schemas/game-nights.schemas';

export const conflictCheckKeys = {
  all: ['game-nights', 'wizard', 'check-conflict'] as const,
  at: (at: string) => [...conflictCheckKeys.all, { at }] as const,
};

export interface UseGameNightConflictCheckOptions {
  /** ISO 8601 datetime offset of the proposed game night. */
  at: string | null;
  /** Disable the query (e.g. while editing the date). */
  enabled?: boolean;
}

/**
 * Returns the conflict-check result for the proposed time.
 *
 * Failure-mode policy (Nygard, spec §9): the BE 500ms server timeout
 * returns `{ hasConflict: false, conflicts: [] }` for slow queries, so the
 * client doesn't need additional fallback logic. We still mark
 * `gcTime: 0` so a transient failure isn't cached for the entire session.
 */
export function useGameNightConflictCheck({
  at,
  enabled = true,
}: UseGameNightConflictCheckOptions): UseQueryResult<ConflictCheckDto, Error> {
  return useQuery({
    queryKey: conflictCheckKeys.at(at ?? ''),
    queryFn: () => api.gameNights.checkConflict(at as string),
    enabled: enabled && typeof at === 'string' && at.length > 0,
    // Cache for 60s: the user typically takes a few seconds to advance from
    // step 1, and the BE state changes are rare in that window.
    staleTime: 60_000,
    gcTime: 0,
    retry: false,
  });
}
