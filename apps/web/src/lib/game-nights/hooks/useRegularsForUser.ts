/**
 * useRegularsForUser — Step 3 wizard "your regulars" hook.
 *
 * Issue #950 W2 Foundation. Spec §6 + §7b.2.
 *
 * Returns the registered users the current organizer has invited to past
 * game nights in the last 12 months, ranked by event count DESC then
 * last-invited DESC. Backed by the `/api/v1/game-nights/regulars` endpoint
 * shipped in W1-PR2.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { RegularDto } from '@/lib/api/schemas/game-nights.schemas';

export const regularsKeys = {
  all: ['game-nights', 'wizard', 'regulars'] as const,
  list: (limit: number) => [...regularsKeys.all, { limit }] as const,
};

export const REGULARS_DEFAULT_LIMIT = 10;
export const REGULARS_MAX_LIMIT = 30;

export interface UseRegularsForUserOptions {
  limit?: number;
  enabled?: boolean;
}

export function useRegularsForUser({
  limit = REGULARS_DEFAULT_LIMIT,
  enabled = true,
}: UseRegularsForUserOptions = {}): UseQueryResult<RegularDto[], Error> {
  const safeLimit = Math.min(Math.max(limit, 1), REGULARS_MAX_LIMIT);

  return useQuery({
    queryKey: regularsKeys.list(safeLimit),
    queryFn: () => api.gameNights.getRegulars(safeLimit),
    enabled,
    // Spec §7b.2: server may cache for 5 minutes; mirror client-side so
    // the user doesn't see flicker when re-opening the wizard.
    staleTime: 5 * 60_000,
  });
}
